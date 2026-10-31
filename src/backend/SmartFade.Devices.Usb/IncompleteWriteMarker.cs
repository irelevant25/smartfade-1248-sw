namespace SmartFade.Devices.Usb;

/// <summary>
/// The incomplete-write latch on disk (SC-2 item 7; the first review of SC-2 item 10, minor: the latch lived in memory
/// only). A text file beside the transfer logs, written the moment a Data header went out, or may have, without its
/// payload. What the console does with a header whose payload never came is UNKNOWN (no capture shows it), so the device
/// refuses to connect while the file exists, also after a restart of the backend; only the owner removes it, after
/// reading the transfer log of that session.
/// </summary>
public sealed class IncompleteWriteMarker
{
    public const string FileName = "INCOMPLETE-WRITE.txt";

    /// <param name="directory">The transfer logs' folder (`Device:Usb:CaptureLogDirectory`); blank, the working folder, where the logs then go too.</param>
    public IncompleteWriteMarker(string? directory)
    {
        FilePath = Path.Combine(Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? "." : directory), FileName);
    }

    /// <summary>Where the marker lives.</summary>
    public string FilePath { get; }

    /// <summary>The marker's text, or null when there is none; a marker that cannot be read throws (the caller refuses to connect).</summary>
    public string? Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    /// <summary>Adds a line to the marker, creating it (and its folder) when it does not exist.</summary>
    public void Write(DateTimeOffset at, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.AppendAllText(FilePath, $"{at:yyyy-MM-dd HH:mm:ss.fff zzz} {text}{Environment.NewLine}");
    }
}
