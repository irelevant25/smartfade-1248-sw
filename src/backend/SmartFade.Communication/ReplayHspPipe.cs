using SmartFade.Communication.Capture;

namespace SmartFade.Communication;

/// <summary>
/// An <see cref="IHspPipe"/> over a committed capture. Reads return the console's transfers in
/// capture order; every write is compared byte for byte with the transfer SmartSoft sent at the
/// same point, so a session engine run over a capture proves that it would have put the same
/// bytes on the wire. SmartSoft's Data transfers (its two System writes per session, F58) are
/// skipped and counted: a read-only session never sends them. It reaches no device.
/// </summary>
public sealed class ReplayHspPipe : IHspPipe
{
    private readonly CaptureFrame[] _frames;
    private int _position;

    public ReplayHspPipe(CaptureFile capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Capture = capture;
        var bulk = capture.BulkFrames.ToArray();
        var empty = bulk.Count(f => f.Data.Length == 0);
        if (empty > 0)
        {
            // None of E2, E3 and E3b holds one; a capture with zero-length transfers needs its own reading first.
            throw new InvalidDataException($"{capture.Header.SourceFile} holds {empty} zero-length bulk transfer(s); the replay pipe does not know how to present them.");
        }

        _frames = bulk;
    }

    public CaptureFile Capture { get; }

    public string Description => $"replay of {Capture.Header.SourceFile}";

    public bool IsOpen { get; private set; }

    /// <summary>Transfers of the capture consumed so far (matched writes, reads and skipped SmartSoft writes).</summary>
    public int Position => _position;

    public int Remaining => _frames.Length - _position;

    public int MatchedWrites { get; private set; }

    public int Reads { get; private set; }

    /// <summary>SmartSoft Data transfers (header + payload) that the capture holds and the session under test did not send.</summary>
    public int SkippedSmartSoftWrites { get; private set; }

    /// <summary>The capture-relative time of the last transfer consumed.</summary>
    public double LastTime { get; private set; }

    public void Open()
    {
        IsOpen = true;
        _position = 0;
    }

    public void Close() => IsOpen = false;

    public void Dispose() => Close();

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureOpen();
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

        if (!data.SequenceEqual(expected.Data))
        {
            throw new HspPipeException(
                $"write differs from SmartSoft's transfer at capture seq {expected.Seq} (t = {expected.Time:F6} s): " +
                $"ours {Convert.ToHexString(data)}, SmartSoft's {Convert.ToHexString(expected.Data)}");
        }

        _position++;
        MatchedWrites++;
        LastTime = expected.Time;
    }

    public void ReadExactly(Span<byte> buffer)
    {
        EnsureOpen();
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
            SkipSmartSoftWrites();
            return _position >= _frames.Length;
        }
    }

    // TODO (SC-2 condition 2): a second mode for the golden tests of the write path, in which SmartSoft's ConsoleEvent
    // writes are not skipped but expected from the session under test and compared byte for byte (header, then
    // payload); the System pair, ChannelText and Memory writes stay skipped in that mode too.

    private void SkipSmartSoftWrites()
    {
        while (_position < _frames.Length && IsDataHeader(_frames[_position]))
        {
            var header = _frames[_position];
            var length = header.Data[2] | (header.Data[3] << 8);
            _position++;
            if (_position < _frames.Length && _frames[_position].Direction == CaptureDirection.Out && _frames[_position].Data.Length == length)
            {
                _position++;
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
