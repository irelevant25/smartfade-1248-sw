using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SmartFade.Devices.Usb;

/// <summary>Receives every transfer of a session and the session's notes.</summary>
public interface ITransferSink : IDisposable
{
    void Transfer(bool toConsole, ReadOnlySpan<byte> data, DateTimeOffset started, DateTimeOffset completed);

    void Note(string text);
}

/// <summary>
/// Writes a session as a normalized capture (`docs/protocol/reverse-engineering.md`, "Capture format
/// as implemented"; one JSON object per line, readable by CaptureFile and hsp_summary.py), so that
/// every session our software runs leaves the same kind of evidence as a USBPcap capture.
/// </summary>
public sealed class TransferLog : ITransferSink
{
    private readonly StreamWriter _writer;
    private readonly DateTimeOffset _start;
    private int _seq;

    public TransferLog(string path, string pipeDescription, string hostVersion)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? ".");
        _writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        _start = DateTimeOffset.UtcNow;
        Path = System.IO.Path.GetFullPath(path);
        WriteLine(new
        {
            type = "header",
            format = "smartfade-capture",
            version = 1,
            transport = "usb",
            source = "smartfade-backend",
            sourceFile = System.IO.Path.GetFileName(path),
            sourceSha256 = string.Empty,
            captureStart = _start.ToString("O", CultureInfo.InvariantCulture),
            console = new { model = "SF1248" },
            host = new { app = "SmartFade backend (USB device; the session's first notes say read-only or live control)", version = hostVersion, pipe = pipeDescription },
            device = new { vid = "14D5", pid = "0200" },
            normalizer = new { name = "SmartFade.Devices.Usb.TransferLog", version = 1 },
            statusNote = "frames carry no USBD status: the backend only sees WinUSB succeed or fail; failed transfers are notes, and the health counts come from the parallel USBPcap capture",
        });
    }

    public string Path { get; }

    public void Transfer(bool toConsole, ReadOnlySpan<byte> data, DateTimeOffset started, DateTimeOffset completed)
    {
        WriteLine(new
        {
            type = "frame",
            seq = ++_seq,
            t = Seconds(completed),
            ts = completed.ToString("O", CultureInfo.InvariantCulture),
            dir = toConsole ? "out" : "in",
            iface = 1,
            ep = toConsole ? "0x04" : "0x83",
            xfer = "bulk",
            len = data.Length,
            data = Convert.ToHexString(data),
            status = (string?)null,
            tSubmit = Seconds(started),
        });
    }

    public void Note(string text)
    {
        WriteLine(new { type = "note", t = Seconds(DateTimeOffset.UtcNow), text });
    }

    public void Dispose() => _writer.Dispose();

    private double Seconds(DateTimeOffset at) => Math.Round((at - _start).TotalSeconds, 6);

    private void WriteLine(object record)
    {
        _writer.WriteLine(JsonSerializer.Serialize(record));
        _writer.Flush();
    }
}
