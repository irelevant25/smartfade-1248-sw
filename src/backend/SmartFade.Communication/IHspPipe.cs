namespace SmartFade.Communication;

/// <summary>
/// A bulk pipe pair to a console as the HSP session uses it: one write per transfer, one read
/// of a known length per transfer (the 12-byte reply header, then the payload the header
/// announces), as the ETC driver issues them (F51: one URB per transfer). Implementations:
/// <see cref="WinUsb.WinUsbHspPipe"/> (a real console, Windows, WinUSB on interface 1) and
/// <see cref="ReplayHspPipe"/> (a committed capture; writes are compared with what SmartSoft sent).
/// </summary>
public interface IHspPipe : IDisposable
{
    /// <summary>Where the pipe leads, for logs and the UI (device path, or the capture file).</summary>
    string Description { get; }

    bool IsOpen { get; }

    void Open();

    void Close();

    /// <summary>Sends one transfer (bulk OUT).</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes as one transfer (bulk IN), or
    /// throws <see cref="HspPipeException"/> (timeout, short transfer, device gone).
    /// </summary>
    void ReadExactly(Span<byte> buffer);
}

/// <summary>A transfer did not happen as the session requires; the session stops, nothing is retried.</summary>
public sealed class HspPipeException : IOException
{
    public HspPipeException(string message)
        : base(message)
    {
    }

    public HspPipeException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
