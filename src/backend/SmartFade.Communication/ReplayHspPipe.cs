using SmartFade.Communication.Capture;

namespace SmartFade.Communication;

/// <summary>
/// An <see cref="IHspPipe"/> over a committed capture. Reads return the console's transfers in
/// capture order; every write is compared byte for byte with the transfer SmartSoft sent at the
/// same point, so a session engine run over a capture proves that it would have put the same
/// bytes on the wire. SmartSoft's Data transfers (its two System writes per session, F58) are
/// skipped and counted: a read-only session never sends them. It reaches no device.
/// <para>
/// Write mode, for the golden tests of the write path (SC-2 condition 2): the SmartSoft Data writes whose payload the
/// test's predicate accepts (the whitelisted ConsoleEvents) are not skipped but expected from the session under test,
/// header then payload, each compared byte for byte; every other SmartSoft write (the System pair, ChannelText, Memory,
/// events outside the whitelist) stays skipped. The one byte not taken from SmartSoft's transfer as it stands is the
/// payload's counter: SmartSoft's counter also counts the writes this mode skips, so the pipe expects SmartSoft's
/// value less the SmartSoft data payloads skipped since <see cref="Open"/>, the counter of a session that never sends
/// those (F131: 0x00 at the session's first data payload, +1 per data payload). SmartSoft restarts that counter and its
/// previous bytes in each of its sessions, so a capture with several SmartSoft sessions is replayed one session at a
/// time (a range of JSONL seq numbers, with a new session under test each, as a reconnect would have). A deviation
/// the knowledge base documents is declared with <see cref="ExpectInstead"/>.
/// </para>
/// <para>
/// A range may also start inside a SmartSoft session, after the show download that SmartSoft's System pair starts (E9c to
/// E9e: a 158-record list of 802 bytes, longer than any a session of ours accepts, F177): SmartSoft's data payloads of that
/// session before the range (its System pair) are then counted as skipped from the start (<c>smartSoftSessionStart</c>), and
/// the SmartSoft writes before <c>expectWritesFrom</c> are skipped like those the predicate refuses, for a first step that
/// a session started there could not have sent (SC-2 condition 8 holds every write until the console's first list and LED
/// state are in).
/// </para>
/// </summary>
public sealed class ReplayHspPipe : IHspPipe
{
    private readonly CaptureFrame[] _frames;
    private readonly Func<ReadOnlyMemory<byte>, bool>? _expectWrite;
    private readonly int _expectWritesFrom;
    private readonly int _payloadsBeforeRange;
    private readonly Dictionary<int, byte[]> _instead = new();
    private readonly List<int> _deviationsMet = new();
    private int _position;
    private int _skippedDataPayloads;
    private bool _awaitingPayload;

    public ReplayHspPipe(CaptureFile capture)
        : this(capture, expectWrite: null)
    {
    }

    /// <summary>
    /// A replay of the capture's bulk transfers with JSONL seq in [<paramref name="firstSeq"/>, <paramref name="endSeq"/>).
    /// With <paramref name="expectWrite"/> (write mode), the SmartSoft Data writes whose payload it accepts are expected
    /// from the session under test; without it every SmartSoft Data write is skipped, as for a read-only session.
    /// <paramref name="smartSoftSessionStart"/>: the JSONL seq at which the SmartSoft session that the range lies in began
    /// (its counter's 0x00); its data payloads before <paramref name="firstSeq"/> count as skipped. <paramref name="expectWritesFrom"/>:
    /// SmartSoft's writes whose Data header has a lower seq are skipped (write mode only).
    /// </summary>
    public ReplayHspPipe(
        CaptureFile capture,
        Func<ReadOnlyMemory<byte>, bool>? expectWrite,
        int firstSeq = int.MinValue,
        int endSeq = int.MaxValue,
        int? smartSoftSessionStart = null,
        int expectWritesFrom = int.MinValue)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Capture = capture;
        var bulk = capture.BulkFrames.Where(f => f.Seq >= firstSeq && f.Seq < endSeq).ToArray();
        var empty = bulk.Count(f => f.Data.Length == 0);
        if (empty > 0)
        {
            // None of E2, E3 and E3b holds one; a capture with zero-length transfers needs its own reading first.
            throw new InvalidDataException($"{capture.Header.SourceFile} holds {empty} zero-length bulk transfer(s); the replay pipe does not know how to present them.");
        }

        _frames = bulk;
        _expectWrite = expectWrite;
        _expectWritesFrom = expectWritesFrom;
        if (smartSoftSessionStart is { } start)
        {
            if (start > firstSeq)
            {
                throw new ArgumentOutOfRangeException(nameof(smartSoftSessionStart), start, "The SmartSoft session must begin at or before the range.");
            }

            // its Data headers between its start and the range, each followed by its payload (F131: one payload per header)
            var before = capture.BulkFrames.Where(f => f.Seq >= start && f.Seq < firstSeq).ToArray();
            _payloadsBeforeRange = before.Count(f => IsDataHeader(f));
            PayloadsBeforeRange = _payloadsBeforeRange;
        }

        FirstSeq = firstSeq;
        EndSeq = endSeq;
    }

    /// <summary>SmartSoft data payloads of the session the range lies in that precede the range (counted as skipped from <see cref="Open"/> on).</summary>
    public int PayloadsBeforeRange { get; }

    public CaptureFile Capture { get; }

    /// <summary>First JSONL seq of the replayed range.</summary>
    public int FirstSeq { get; }

    /// <summary>JSONL seq after the replayed range.</summary>
    public int EndSeq { get; }

    public string Description => _expectWrite is null && FirstSeq == int.MinValue && EndSeq == int.MaxValue
        ? $"replay of {Capture.Header.SourceFile}"
        : $"replay of {Capture.Header.SourceFile}, seq {FirstSeq}..{EndSeq}{(_expectWrite is null ? string.Empty : ", writes expected")}";

    public bool IsOpen { get; private set; }

    /// <summary>Transfers of the capture consumed so far (matched writes, reads and skipped SmartSoft writes).</summary>
    public int Position => _position;

    public int Remaining => _frames.Length - _position;

    /// <summary>OUT transfers matched (polls, fetches, and in write mode each Data header and payload).</summary>
    public int MatchedWrites { get; private set; }

    /// <summary>Write mode: SmartSoft Data writes (header and payload) that the session under test reproduced.</summary>
    public int MatchedDataWrites { get; private set; }

    public int Reads { get; private set; }

    /// <summary>SmartSoft Data transfers (header + payload) that the capture holds and the session under test did not send.</summary>
    public int SkippedSmartSoftWrites { get; private set; }

    /// <summary>The capture frames at which a declared deviation (<see cref="ExpectInstead"/>) was compared, in order.</summary>
    public IReadOnlyList<int> DeviationsMet => _deviationsMet;

    /// <summary>The capture-relative time of the last transfer consumed.</summary>
    public double LastTime { get; private set; }

    /// <summary>
    /// Write mode: the payload of SmartSoft's write that the next transfers must reproduce (its Data header first), so
    /// a test can hand the session the same event; null when the next transfer expected is no such write.
    /// </summary>
    public ReadOnlyMemory<byte>? ExpectedWrite
    {
        get
        {
            if (_expectWrite is null || _awaitingPayload || !IsOpen)
            {
                return null;
            }

            SkipSmartSoftWrites();
            if (_position + 1 < _frames.Length && IsDataHeader(_frames[_position]))
            {
                return _frames[_position + 1].Data;
            }

            return null;
        }
    }

    public void Open()
    {
        IsOpen = true;
        _position = 0;
        _skippedDataPayloads = _payloadsBeforeRange;
        _awaitingPayload = false;
        _deviationsMet.Clear();
    }

    public void Close() => IsOpen = false;

    public void Dispose() => Close();

    /// <summary>
    /// Write mode: compare the session's transfer at capture frame <paramref name="seq"/> with
    /// <paramref name="expected"/> instead of SmartSoft's bytes (the payload counter is still adjusted as above). Only
    /// for a deviation the knowledge base documents, with bytes taken from the capture.
    /// </summary>
    public void ExpectInstead(int seq, ReadOnlySpan<byte> expected) => _instead[seq] = expected.ToArray();

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureOpen();
        if (_awaitingPayload)
        {
            WritePayload(data);
            return;
        }

        SkipSmartSoftWrites();
        if (_position >= _frames.Length)
        {
            throw new HspPipeException($"write of {data.Length} bytes after the end of the capture");
        }

        var expected = _frames[_position];
        if (expected.Direction != CaptureDirection.Out)
        {
            throw new HspPipeException($"write of {data.Length} bytes where the capture expects a read (frame seq {expected.Seq}, {expected.Data.Length} bytes in)");
        }

        var reference = Reference(expected);
        if (!data.SequenceEqual(reference))
        {
            throw new HspPipeException(
                $"write differs from SmartSoft's transfer at capture seq {expected.Seq} (t = {expected.Time:F6} s): " +
                $"ours {Convert.ToHexString(data)}, SmartSoft's {Convert.ToHexString(reference)}");
        }

        _position++;
        MatchedWrites++;
        LastTime = expected.Time;

        // Only in write mode does a Data header get here (SkipSmartSoftWrites stopped at it): its payload is due next.
        _awaitingPayload = IsDataHeader(expected);
    }

    public void ReadExactly(Span<byte> buffer)
    {
        EnsureOpen();
        if (_awaitingPayload)
        {
            throw new HspPipeException($"read of {buffer.Length} bytes where the payload of the Data header is due");
        }

        if (_position >= _frames.Length)
        {
            throw new HspPipeException($"read of {buffer.Length} bytes after the end of the capture");
        }

        var frame = _frames[_position];
        if (frame.Direction != CaptureDirection.In)
        {
            throw new HspPipeException($"read of {buffer.Length} bytes where the capture expects a write (frame seq {frame.Seq})");
        }

        if (frame.Data.Length != buffer.Length)
        {
            throw new HspPipeException($"read of {buffer.Length} bytes but the capture's transfer at seq {frame.Seq} has {frame.Data.Length} bytes");
        }

        frame.Data.CopyTo(buffer);
        _position++;
        Reads++;
        LastTime = frame.Time;
    }

    /// <summary>True when the capture holds nothing but SmartSoft writes from here on (i.e. the session consumed everything else).</summary>
    public bool AtEnd
    {
        get
        {
            if (_awaitingPayload)
            {
                return false;
            }

            SkipSmartSoftWrites();
            return _position >= _frames.Length;
        }
    }

    private void WritePayload(ReadOnlySpan<byte> data)
    {
        _awaitingPayload = false;
        if (_position >= _frames.Length)
        {
            throw new HspPipeException($"write of {data.Length} bytes after the end of the capture, where SmartSoft's payload was due");
        }

        var expected = _frames[_position];
        if (expected.Direction != CaptureDirection.Out)
        {
            throw new HspPipeException($"write of {data.Length} bytes where the capture expects a read (frame seq {expected.Seq})");
        }

        // The counter of a session that never sends the writes this mode skips (see the class comment).
        var reference = (byte[])Reference(expected).Clone();
        if (reference.Length > 0)
        {
            reference[0] = unchecked((byte)(reference[0] - _skippedDataPayloads));
        }

        if (!data.SequenceEqual(reference))
        {
            throw new HspPipeException(
                $"payload differs from SmartSoft's at capture seq {expected.Seq} (t = {expected.Time:F6} s): ours {Convert.ToHexString(data)}, " +
                $"expected {Convert.ToHexString(reference)} (SmartSoft's {Convert.ToHexString(expected.Data)}, its counter less the {_skippedDataPayloads} data payload(s) skipped)");
        }

        _position++;
        MatchedWrites++;
        MatchedDataWrites++;
        LastTime = expected.Time;
    }

    /// <summary>SmartSoft's bytes at that frame, or the declared deviation's.</summary>
    private byte[] Reference(CaptureFrame frame)
    {
        if (_instead.TryGetValue(frame.Seq, out var instead))
        {
            _deviationsMet.Add(frame.Seq);
            return instead;
        }

        return frame.Data;
    }

    private void SkipSmartSoftWrites()
    {
        while (_position < _frames.Length && IsDataHeader(_frames[_position]))
        {
            var header = _frames[_position];
            var length = header.Data[2] | (header.Data[3] << 8);
            var hasPayload = _position + 1 < _frames.Length && _frames[_position + 1].Direction == CaptureDirection.Out && _frames[_position + 1].Data.Length == length;
            if (_expectWrite is not null && hasPayload && header.Seq >= _expectWritesFrom && _expectWrite(_frames[_position + 1].Data))
            {
                return;   // write mode: the session under test must send this one
            }

            _position++;
            if (hasPayload)
            {
                _position++;
                _skippedDataPayloads++;
            }

            SkippedSmartSoftWrites++;
        }
    }

    private static bool IsDataHeader(CaptureFrame frame) =>
        frame.Direction == CaptureDirection.Out && frame.Data.Length == 12 && frame.Data[0] == 1 && frame.Data[1] == 0;

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new HspPipeException("the replay pipe is closed");
        }
    }
}
