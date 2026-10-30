using SmartFade.Communication.Capture;

namespace SmartFade.Communication;

/// <summary>
/// Plays the console → PC side of a committed capture into a consumer. It reaches no device:
/// <see cref="SendAsync"/> only records what the caller would have sent, so a protocol engine
/// under test can be compared with what SmartSoft sent at the same point of the capture.
/// </summary>
public sealed class ReplayTransport : IDeviceTransport
{
    private readonly CaptureFrame[] _inbound;
    private readonly List<byte[]> _sent = new();
    private int _position;

    public ReplayTransport(CaptureFile capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Capture = capture;
        _inbound = capture.BulkFrames.Where(f => f.Direction == CaptureDirection.In && f.Data.Length > 0).ToArray();
    }

    public CaptureFile Capture { get; }

    public bool IsOpen { get; private set; }

    public int RemainingInbound => _inbound.Length - _position;

    public IReadOnlyList<byte[]> Sent => _sent;

    public event EventHandler<DeviceDataReceivedEventArgs>? DataReceived;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        IsOpen = true;
        _position = 0;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Transport is closed.");
        }

        _sent.Add(data.ToArray());
        return Task.CompletedTask;
    }

    /// <summary>Delivers the next inbound transfer of the capture; false when the capture is exhausted.</summary>
    public bool PlayNext()
    {
        if (!IsOpen || _position >= _inbound.Length)
        {
            return false;
        }

        var frame = _inbound[_position++];
        DataReceived?.Invoke(this, new DeviceDataReceivedEventArgs(frame.Data, TimeSpan.FromSeconds(frame.Time)));
        return true;
    }

    public int PlayAll()
    {
        var count = 0;
        while (PlayNext())
        {
            count++;
        }

        return count;
    }
}
