namespace SmartFade.Communication;

/// <summary>Test transport: everything sent is received back unchanged. Reaches no device.</summary>
public sealed class LoopbackTransport : IDeviceTransport
{
    private readonly List<byte[]> _sent = new();

    public bool IsOpen { get; private set; }

    public IReadOnlyList<byte[]> Sent => _sent;

    public event EventHandler<DeviceDataReceivedEventArgs>? DataReceived;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        IsOpen = true;
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

        var copy = data.ToArray();
        _sent.Add(copy);
        DataReceived?.Invoke(this, new DeviceDataReceivedEventArgs(copy));
        return Task.CompletedTask;
    }
}
