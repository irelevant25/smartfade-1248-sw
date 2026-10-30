namespace SmartFade.Communication;

/// <summary>
/// Physical transport abstraction (brief §10). Phase 1 ships only transports that cannot reach
/// a console: <see cref="LoopbackTransport"/> for tests and <see cref="ReplayTransport"/> for
/// committed captures. A USB transport is added only when a message is cleared for sending.
/// </summary>
public interface IDeviceTransport
{
    bool IsOpen { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);

    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    event EventHandler<DeviceDataReceivedEventArgs>? DataReceived;
}
