namespace SmartFade.Communication.Capture;

/// <summary>One transfer of a normalized capture (`docs/protocol/reverse-engineering.md`, "Capture format as implemented").</summary>
public sealed record CaptureFrame(
    int Seq,
    double Time,
    CaptureDirection Direction,
    string Endpoint,
    string TransferType,
    byte[] Data,
    bool Synthetic)
{
    public bool IsBulk => string.Equals(TransferType, "bulk", StringComparison.OrdinalIgnoreCase);
}

public enum CaptureDirection
{
    /// <summary>PC → console.</summary>
    Out,

    /// <summary>Console → PC.</summary>
    In,
}

public sealed record CaptureNote(double Time, string Text);

public sealed record CaptureHeader(
    string Format,
    int Version,
    string SourceFile,
    string SourceSha256,
    string? ConsoleModel,
    string? Firmware,
    string? HostApp,
    string? HostVersion,
    string? DeviceVid,
    string? DevicePid,
    int? DeviceAddress,
    int? Transfers);
