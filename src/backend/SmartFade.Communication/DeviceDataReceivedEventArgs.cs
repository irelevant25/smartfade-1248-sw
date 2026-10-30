namespace SmartFade.Communication;

public sealed class DeviceDataReceivedEventArgs : EventArgs
{
    public DeviceDataReceivedEventArgs(ReadOnlyMemory<byte> data, TimeSpan? captureTime = null)
    {
        Data = data;
        CaptureTime = captureTime;
    }

    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>For replayed data: the capture-relative time of the transfer.</summary>
    public TimeSpan? CaptureTime { get; }
}
