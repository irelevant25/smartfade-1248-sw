namespace SmartFade.Domain;

/// <summary>Connection state of the device the backend is configured for.</summary>
public sealed record ConnectionState(
    ConnectionStatus Status,
    DeviceMode Mode,
    string DeviceDescription,
    string? Error = null);
