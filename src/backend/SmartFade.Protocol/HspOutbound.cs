using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>
/// The two PC → console headers that a read-only session may produce, byte for byte as
/// SmartSoft sends them (VERIFIED, F52: 38 517 polls and 1 651 fetches in E2, E3 and E3b, the
/// stale-word rule re-checked on 2026-10-30 with `tools/protocol-analyzer/poll_rules.py`).
/// Both are cleared for sending by safety check SC-1 (docs/protocol/safety-checks.md), under
/// its conditions: a fetch only for a record that the console itself listed in a DirtyObjects
/// list of the same session, in list order, once. Nothing else is encoded here: no Data header,
/// no System write, no payload.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F52, F53, F57", "cleared by SC-1 (2026-10-30) for a read-only session")]
public static class HspOutbound
{
    /// <summary>
    /// A poll: w0 = 0, w1 = 0, w2..w5 = the "stale" words, which SmartSoft copies from the most
    /// recent fetch or reply header (all zero before the first; F52).
    /// </summary>
    public static byte[] Poll(ushort w2, ushort w3, ushort w4, ushort w5) =>
        Header((ushort)HspMessageType.Poll, 0, w2, w3, w4, w5);

    /// <summary>A fetch of one listed record: w0 = 2, w1 = 0, w2 = object id, w3..w5 = qualifiers a, b, c (F52, F57).</summary>
    public static byte[] Fetch(DirtyObjectRecord record)
    {
        if (record.ObjectId is < 0 or > ushort.MaxValue || record.A is < 0 or > ushort.MaxValue
            || record.B is < 0 or > ushort.MaxValue || record.C is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(record), record, "Fetch words must fit in u16.");
        }

        return Header((ushort)HspMessageType.Fetch, 0, (ushort)record.ObjectId, (ushort)record.A, (ushort)record.B, (ushort)record.C);
    }

    private static byte[] Header(ushort w0, ushort w1, ushort w2, ushort w3, ushort w4, ushort w5)
    {
        var bytes = new byte[HspHeader.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, w0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), w1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), w2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), w3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), w4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), w5);
        return bytes;
    }
}
