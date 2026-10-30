namespace SmartFade.Communication;

/// <summary>
/// Physical transport abstraction (brief §10). Phase 1 ships only transports that cannot reach
/// a console: <see cref="LoopbackTransport"/> for tests and <see cref="ReplayTransport"/> for
/// committed captures. The USB path added with safety check SC-1 (2026-10-30) does not implement
/// this interface: it is the 64-byte bulk pipe <c>WinUsb.WinUsbHspPipe</c> behind <c>IHspPipe</c>,
/// used by the read-only device for the cleared poll and listed fetch only.
/// </summary>
public interface IDeviceTransport
{
    bool IsOpen { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);

    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    event EventHandler<DeviceDataReceivedEventArgs>? DataReceived;
}
