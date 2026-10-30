using System.Buffers.Binary;
using System.Text;

namespace SmartFade.Protocol.Objects;

/// <summary>Object 37 (ConsoleVersion): [n][n ASCII bytes], e.g. "3.0.2.9.0.43" (VERIFIED, F59; value F48).</summary>
[Evidence(EvidenceStatus.Verified, "F59, F48")]
public sealed record ConsoleVersionObject(string Version)
{
    public static ConsoleVersionObject Decode(ReadOnlySpan<byte> body)
    {
        var n = RequireLengthPrefix(body, 1, "ConsoleVersion");
        return new ConsoleVersionObject(Encoding.ASCII.GetString(body.Slice(1, n)));
    }

    internal static int RequireLengthPrefix(ReadOnlySpan<byte> body, int unitSize, string name)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException($"{name}: empty body.");
        }

        var n = body[0];
        if (body.Length < 1 + n * unitSize)
        {
            throw new InvalidDataException($"{name}: length byte {n} exceeds the body ({body.Length} bytes).");
        }

        return n;
    }
}

/// <summary>Object 36 (FileName): [n][n UTF-16LE characters], e.g. "SHOW04.ASC" padded with zeros (VERIFIED, F60).</summary>
[Evidence(EvidenceStatus.Verified, "F60", "UTF-16LE, unlike the label text blocks (H40)")]
public sealed record FileNameObject(string FileName)
{
    public static FileNameObject Decode(ReadOnlySpan<byte> body)
    {
        var n = ConsoleVersionObject.RequireLengthPrefix(body, 2, "FileName");
        var text = Encoding.Unicode.GetString(body.Slice(1, n * 2));
        var end = text.IndexOf('\0');
        return new FileNameObject(end >= 0 ? text[..end] : text);
    }
}

/// <summary>
/// Object 38 (SystemSizes), 8 bytes on the 1248, seen as 30 02 00 18 00 0C 04 01 (bytes VERIFIED,
/// F61). The readings [channels][u16 BE patch length][memories per page] are INFERRED from the
/// sfEngine parser (S11.14.11) and fit the values 48 / 512 / 24; the 4 tail bytes are open (H33).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F61", "bytes VERIFIED; field readings INFERRED (S11.14.11); tail bytes H33")]
public sealed record SystemSizesObject(int Channels, int PatchLength, int MemoriesPerPage, ReadOnlyMemory<byte> Tail)
{
    public static SystemSizesObject Decode(ReadOnlyMemory<byte> body)
    {
        var span = body.Span;
        if (span.Length < 4)
        {
            throw new InvalidDataException("SystemSizes: body shorter than 4 bytes.");
        }

        return new SystemSizesObject(span[0], BinaryPrimitives.ReadUInt16BigEndian(span[1..]), span[3], body[4..]);
    }
}

/// <summary>
/// Object 5 (Patch): [u16 BE 512][512 bytes] (bytes VERIFIED, F70). The reading "entry d − 1 =
/// channel at DMX address d, 0 = unpatched" is OBSERVED against SHOW04.ASC, 512 of 512 entries
/// (H34); the show file's proportional level is not in this object.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F70", "bytes VERIFIED; DMX → channel reading OBSERVED (H34)")]
public sealed record PatchObject(IReadOnlyList<byte> ChannelByAddress)
{
    public static PatchObject Decode(ReadOnlySpan<byte> body)
    {
        var count = RequireCount(body, "Patch");
        return new PatchObject(body.Slice(2, count).ToArray());
    }

    internal static int RequireCount(ReadOnlySpan<byte> body, string name)
    {
        if (body.Length < 2)
        {
            throw new InvalidDataException($"{name}: body shorter than 2 bytes.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(body);
        if (body.Length != 2 + count)
        {
            throw new InvalidDataException($"{name}: count {count} does not match the body length {body.Length}.");
        }

        return count;
    }
}

/// <summary>Object 13 (OutputLevels): [u16 BE count][levels], DMX address d = body[1 + d] (length VERIFIED F67; index mapping OBSERVED for DMX 1 and 5, F83).</summary>
[Evidence(EvidenceStatus.Observed, "F67, F83")]
public sealed record OutputLevelsObject(IReadOnlyList<byte> Levels)
{
    public static OutputLevelsObject Decode(ReadOnlySpan<byte> body)
    {
        var count = PatchObject.RequireCount(body, "OutputLevels");
        return new OutputLevelsObject(body.Slice(2, count).ToArray());
    }

    /// <summary>Level of DMX address <paramref name="address"/> (1-based).</summary>
    public byte At(int address) => Levels[address - 1];
}

/// <summary>Object 19 (InputLevels): [00][u16 BE 512][512 levels], all zero in E3 (bytes OBSERVED once, F81; meaning HYPOTHESIS H52).</summary>
[Evidence(EvidenceStatus.Observed, "F81", "meaning: H52")]
public sealed record InputLevelsObject(byte Byte0, IReadOnlyList<byte> Levels)
{
    public static InputLevelsObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 3)
        {
            throw new InvalidDataException("InputLevels: body shorter than 3 bytes.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(body[1..]);
        if (body.Length != 3 + count)
        {
            throw new InvalidDataException($"InputLevels: count {count} does not match the body length {body.Length}.");
        }

        return new InputLevelsObject(body[0], body.Slice(3, count).ToArray());
    }
}

/// <summary>
/// Object 15 (ChannelLevels): [n][n − 2 channel levels][2 trailing bytes]; channel c = body[c]
/// (VERIFIED length F67; mapping OBSERVED for channels 1, 5, 7 and 25, F83 and the E3b record).
/// The values are post-MASTER outputs, not fader positions (E3b: a full channel read 0x7F with the
/// MASTER at 0x7F), and bump output appears in them. The two trailing bytes are read by sfEngine
/// as the independents (H26, OPEN: always 0 so far).
/// </summary>
[Evidence(EvidenceStatus.Observed, "F67, F83, F96, F97", "post-MASTER values; trailing bytes: H26; length VERIFIED (F67)")]
public sealed record ChannelLevelsObject(int Count, IReadOnlyList<byte> Levels, byte Trailing1, byte Trailing2)
{
    public static ChannelLevelsObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 3)
        {
            throw new InvalidDataException("ChannelLevels: body shorter than 3 bytes.");
        }

        var n = body[0];
        if (body.Length != 1 + n)
        {
            throw new InvalidDataException($"ChannelLevels: n = {n} does not match the body length {body.Length}.");
        }

        return new ChannelLevelsObject(n, body.Slice(1, n - 2).ToArray(), body[n - 1], body[n]);
    }

    /// <summary>Level of channel <paramref name="channel"/> (1-based).</summary>
    public byte At(int channel) => Levels[channel - 1];
}

/// <summary>
/// Object 14 (FaderInfo): [count = 48][48 × 4-byte records][BUMPS][MASTER], 195 bytes. Record N
/// byte 0 is the post-MASTER output of physical fader N in whatever mode and bank the console is in
/// (OBSERVED for faders 1, 5 and 7, F82 and the E3b record; in bank 25-48 physical fader 1 still
/// drives record 1, so records 25–48 are unused on the 1248). Record byte 1 moved during a memory
/// bump (E3b); bytes 2–3 are UNKNOWN. body[194] = MASTER (OBSERVED, E3 + E3b); body[193] = BUMPS
/// (OBSERVED in E3b: 104 changes with the four logged moves).
/// </summary>
[Evidence(EvidenceStatus.Observed, "F82, F95, F96, F97, F99", "record byte 0 = post-MASTER output of the physical fader; bytes 2..3 UNKNOWN; layout VERIFIED in part (F82)")]
public sealed record FaderInfoObject(IReadOnlyList<byte> FaderLevels, IReadOnlyList<byte> RawRecords, byte Bumps, byte Master)
{
    public const int RecordSize = 4;

    public static FaderInfoObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException("FaderInfo: empty body.");
        }

        var count = body[0];
        if (body.Length != 1 + count * RecordSize + 2)
        {
            throw new InvalidDataException($"FaderInfo: count {count} does not match the body length {body.Length}.");
        }

        var levels = new byte[count];
        for (var i = 0; i < count; i++)
        {
            levels[i] = body[1 + i * RecordSize];
        }

        return new FaderInfoObject(levels, body.Slice(1, count * RecordSize).ToArray(), body[1 + count * RecordSize], body[2 + count * RecordSize]);
    }

    /// <summary>Post-MASTER output level of physical fader <paramref name="fader"/> (1-based).</summary>
    public byte At(int fader) => FaderLevels[fader - 1];
}

/// <summary>
/// Object 23 (ConsoleStatus), 7 bytes. body[0] went 0 → 1 at the switch to memory mode and back
/// (OBSERVED, F87, E3b); body[4] went 0 → 1 with the 25-48 FADERS button (OBSERVED, E3b). The
/// readings "fader mode" and "bank" are HYPOTHESIS H53; the other bytes are UNKNOWN.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F87, F98", "meaning: H53; body[0] transitions VERIFIED in part (F87)")]
public sealed record ConsoleStatusObject(ReadOnlyMemory<byte> Raw)
{
    public byte Byte0 => Raw.Span[0];

    public static ConsoleStatusObject Decode(ReadOnlyMemory<byte> body)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException("ConsoleStatus: empty body.");
        }

        return new ConsoleStatusObject(body);
    }
}
