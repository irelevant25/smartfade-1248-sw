using System.Globalization;
using System.Text.Json;

namespace SmartFade.Communication.Capture;

/// <summary>
/// Reader for the normalized capture JSONL written by tools/protocol-analyzer/usbpcap_to_jsonl.py.
/// One JSON object per line: a header, then frame and note records. Only reads files.
/// </summary>
public sealed class CaptureFile
{
    private CaptureFile(CaptureHeader header, IReadOnlyList<CaptureFrame> frames, IReadOnlyList<CaptureNote> notes)
    {
        Header = header;
        Frames = frames;
        Notes = notes;
    }

    public CaptureHeader Header { get; }

    public IReadOnlyList<CaptureFrame> Frames { get; }

    public IReadOnlyList<CaptureNote> Notes { get; }

    public IEnumerable<CaptureFrame> BulkFrames => Frames.Where(f => f.IsBulk && !f.Synthetic);

    public static CaptureFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static CaptureFile Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        CaptureHeader? header = null;
        var frames = new List<CaptureFrame>();
        var notes = new List<CaptureNote>();
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "header":
                    header = ParseHeader(root);
                    break;
                case "frame":
                    frames.Add(ParseFrame(root));
                    break;
                case "note":
                    notes.Add(new CaptureNote(root.GetProperty("t").GetDouble(), root.GetProperty("text").GetString() ?? string.Empty));
                    break;
                default:
                    throw new InvalidDataException($"Line {lineNumber}: unknown record type '{type}'.");
            }
        }

        if (header is null)
        {
            throw new InvalidDataException("The capture has no header record.");
        }

        return new CaptureFile(header, frames, notes);
    }

    private static CaptureHeader ParseHeader(JsonElement root)
    {
        var console = root.TryGetProperty("console", out var c) ? c : default;
        var host = root.TryGetProperty("host", out var h) ? h : default;
        var device = root.TryGetProperty("device", out var d) ? d : default;
        return new CaptureHeader(
            Format: root.GetProperty("format").GetString() ?? string.Empty,
            Version: root.GetProperty("version").GetInt32(),
            SourceFile: root.GetProperty("sourceFile").GetString() ?? string.Empty,
            SourceSha256: root.GetProperty("sourceSha256").GetString() ?? string.Empty,
            ConsoleModel: GetOptionalString(console, "model"),
            Firmware: GetOptionalString(console, "firmware"),
            HostApp: GetOptionalString(host, "app"),
            HostVersion: GetOptionalString(host, "version"),
            DeviceVid: GetOptionalString(device, "vid"),
            DevicePid: GetOptionalString(device, "pid"),
            DeviceAddress: GetOptionalInt(device, "address"),
            Transfers: GetOptionalInt(root, "transfers"));
    }

    private static CaptureFrame ParseFrame(JsonElement root)
    {
        var dir = root.GetProperty("dir").GetString();
        var direction = dir switch
        {
            "in" => CaptureDirection.In,
            "out" => CaptureDirection.Out,
            _ => throw new InvalidDataException($"Unknown direction '{dir}'."),
        };
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement.GetString() ?? string.Empty : string.Empty;
        return new CaptureFrame(
            Seq: root.GetProperty("seq").GetInt32(),
            Time: root.GetProperty("t").GetDouble(),
            Direction: direction,
            Endpoint: root.GetProperty("ep").GetString() ?? string.Empty,
            TransferType: root.GetProperty("xfer").GetString() ?? string.Empty,
            Data: Convert.FromHexString(data),
            Synthetic: root.TryGetProperty("synthetic", out var synthetic) && synthetic.ValueKind == JsonValueKind.True);
    }

    private static string? GetOptionalString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetOptionalInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Header.SourceFile}: {Frames.Count} frames, {Notes.Count} notes");
}
