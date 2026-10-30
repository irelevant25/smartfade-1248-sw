namespace SmartFade.Domain;

/// <summary>
/// Which kind of device the backend talks to. <see cref="Hardware"/> exists since safety check
/// SC-1 (2026-10-30, docs/protocol/safety-checks.md) cleared the poll and the listed fetch for a
/// read-only session; it is never the default, it needs the explicit configuration
/// `Device:Mode=Usb`, and the UI shows it as HARDWARE.
/// </summary>
public enum DeviceMode
{
    /// <summary>In-process simulator; the default.</summary>
    Simulated,

    /// <summary>A device fed from a committed capture (read-only playback).</summary>
    Replay,

    /// <summary>The real console over USB (WinUSB on interface 1), read-only.</summary>
    Hardware,
}
