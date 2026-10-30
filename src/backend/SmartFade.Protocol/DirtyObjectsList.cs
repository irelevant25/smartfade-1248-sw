using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>One record of a DirtyObjects list: the object the console wants fetched, with its qualifiers.</summary>
public readonly record struct DirtyObjectRecord(int ObjectId, int A, int B, int C)
{
    public string Name => HspObjectIds.Name(ObjectId);
}

/// <summary>
/// Body of object 40 (DirtyObjects), the only content seen in poll replies with a payload:
/// [u16 BE count] + count × [id][u16 BE a][b][c]. SmartSoft fetches the records in list
/// order (VERIFIED, F57).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F57")]
public sealed record DirtyObjectsList(IReadOnlyList<DirtyObjectRecord> Records)
{
    public const int RecordSize = 5;

    public static DirtyObjectsList Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 2)
        {
            throw new InvalidDataException("A DirtyObjects body has at least 2 bytes.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(body);
        if (body.Length != 2 + count * RecordSize)
        {
            throw new InvalidDataException($"DirtyObjects count {count} does not match the body length {body.Length}.");
        }

        var records = new DirtyObjectRecord[count];
        for (var i = 0; i < count; i++)
        {
            var record = body.Slice(2 + i * RecordSize, RecordSize);
            records[i] = new DirtyObjectRecord(record[0], BinaryPrimitives.ReadUInt16BigEndian(record[1..]), record[3], record[4]);
        }

        return new DirtyObjectsList(records);
    }
}
