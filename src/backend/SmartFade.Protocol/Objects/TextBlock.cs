using System.Text;

namespace SmartFade.Protocol.Objects;

/// <summary>
/// The 38-byte label block found in Memory, Sequence, SequenceMain, Page, ChannelText and
/// Independents objects: [03][06] + 36 bytes = 3 lines × 6 UTF-16 code units, each line cut
/// at its first NUL. The text matched the show file for every label seen (OBSERVED, F73).
/// The byte order is a HYPOTHESIS (H40): reading A (used here) takes the 36 bytes as 18
/// big-endian code units; reading B (ConsoleLink, class 5) takes one pad byte then
/// little-endian units of 12 / 12 / 11 bytes. Both give the same text for every label seen;
/// a label with a character ≥ U+0100 (e.g. "č") decides it.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F73", "byte order HYPOTHESIS H40, reading A implemented")]
public sealed record TextBlock(string Line1, string Line2, string Line3)
{
    public const int Size = 38;
    public const byte Marker0 = 0x03;
    public const byte Marker1 = 0x06;
    public const int Lines = 3;
    public const int UnitsPerLine = 6;

    public static bool TryDecode(ReadOnlySpan<byte> bytes, out TextBlock block)
    {
        block = default!;
        if (bytes.Length < Size || bytes[0] != Marker0 || bytes[1] != Marker1)
        {
            return false;
        }

        var lines = new string[Lines];
        for (var line = 0; line < Lines; line++)
        {
            var units = bytes.Slice(2 + line * UnitsPerLine * 2, UnitsPerLine * 2);
            var text = Encoding.BigEndianUnicode.GetString(units);
            var end = text.IndexOf('\0');
            lines[line] = end >= 0 ? text[..end] : text;
        }

        block = new TextBlock(lines[0], lines[1], lines[2]);
        return true;
    }

    public static TextBlock Decode(ReadOnlySpan<byte> bytes) =>
        TryDecode(bytes, out var block) ? block : throw new InvalidDataException("Not a text block: expected 38 bytes starting with 03 06.");

    public override string ToString() =>
        string.Join(" ", new[] { Line1, Line2, Line3 }.Where(l => l.Length > 0));
}
