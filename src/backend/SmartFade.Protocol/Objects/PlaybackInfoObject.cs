using System.Buffers.Binary;

namespace SmartFade.Protocol.Objects;

/// <summary>
/// Object 17 (PlaybackInfo): [count = 5][5 × 16-byte records], 81 bytes (F67). Record r (0-based) is
/// body[1 + 16r .. 16r + 16]: bytes 0-1 the step position (two equal bytes; the Next step's 0-based
/// index, H76), byte 2 Live and byte 3 Next (summing to 255 at rest and in timed fades, independent
/// under the manual crossfaders: F101 as limited, F130), bytes 4-9 `00 00 01 00 01 00` in every sample
/// (rate / fade overrides? H74), bytes 10-15 three u16 BE times counting down during a fade. Record 5
/// (index 4) is the main playback, the crossfader pair and the stack (H50, F130); record 1 carried a
/// running sequence in E8 (F118). The record structure is VERIFIED in part (F101: E3b, E8, E8b); the
/// readings are OBSERVED and shown as such.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F67, F101, F118, F130", "record structure VERIFIED in part (F101); readings OBSERVED; H50 main record, H76 step index, H80 A / B assignment")]
public sealed record PlaybackInfoObject(IReadOnlyList<PlaybackRecord> Records)
{
    public const int RecordSize = 16;

    public static PlaybackInfoObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException("PlaybackInfo: empty body.");
        }

        var count = body[0];
        if (body.Length != 1 + count * RecordSize)
        {
            throw new InvalidDataException($"PlaybackInfo: count {count} does not match the body length {body.Length}.");
        }

        var records = new PlaybackRecord[count];
        for (var i = 0; i < count; i++)
        {
            var r = body.Slice(1 + i * RecordSize, RecordSize);
            records[i] = new PlaybackRecord(
                r[0],
                r[1],
                r[2],
                r[3],
                r.Slice(4, 6).ToArray(),
                BinaryPrimitives.ReadUInt16BigEndian(r[10..]),
                BinaryPrimitives.ReadUInt16BigEndian(r[12..]),
                BinaryPrimitives.ReadUInt16BigEndian(r[14..]));
        }

        return new PlaybackInfoObject(records);
    }

    /// <summary>Record 5 (index 4), the main playback: the crossfader pair and the stack; null when the object is shorter.</summary>
    public PlaybackRecord? Main => Records.Count >= 5 ? Records[4] : null;
}

/// <summary>One 16-byte PlaybackInfo record (see <see cref="PlaybackInfoObject"/> for the byte readings and their status).</summary>
public sealed record PlaybackRecord(byte Step, byte StepRepeat, byte Live, byte Next, IReadOnlyList<byte> Bytes4To9, ushort Time1, ushort Time2, ushort Time3);
