using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>
/// The 12-byte header of every HSP transfer: six u16 little-endian words
/// {type, payload length, w2, w3, w4, w5}. For Fetch and ObjectReply, w2..w5 are
/// (object id, a, b, c); for Poll and Data they are stale copies of earlier words and must be
/// ignored (VERIFIED, F52 / F53). This type only parses; the two headers a read-only session
/// may send are produced by <see cref="HspOutbound"/> (cleared by safety check SC-1).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F52, F53", "six u16 LE words; stale w2..w5 in poll/data headers")]
public readonly record struct HspHeader(ushort Type, ushort Length, ushort W2, ushort W3, ushort W4, ushort W5)
{
    public const int Size = 12;

    public HspMessageType MessageType => (HspMessageType)Type;

    public bool IsKnownType => Type <= 4;

    public bool IsFromPc => Type <= 2;

    public bool IsFromConsole => Type is 3 or 4;

    /// <summary>Object id (word 2); meaningful for Fetch and ObjectReply only.</summary>
    public int ObjectId => W2;

    public int QualifierA => W3;

    public int QualifierB => W4;

    public int QualifierC => W5;

    public static HspHeader Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"An HSP header is exactly {Size} bytes, got {bytes.Length}.", nameof(bytes));
        }

        return new HspHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]));
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, out HspHeader header)
    {
        if (bytes.Length != Size)
        {
            header = default;
            return false;
        }

        header = Parse(bytes);
        return true;
    }

    public override string ToString() =>
        $"{MessageType}(len={Length}, id={W2}, a={W3}, b={W4}, c={W5})";
}
