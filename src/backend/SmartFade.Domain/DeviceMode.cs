namespace SmartFade.Domain;

/// <summary>
/// Which kind of device the backend talks to. There is no hardware mode in Phase 1: no
/// protocol message is cleared for sending (CLAUDE.md, "VERIFIED is necessary, not
/// sufficient"), so a value for a real console cannot exist yet. When one is added it must be
/// enabled deliberately and shown prominently in the UI.
/// </summary>
public enum DeviceMode
{
    /// <summary>In-process simulator; the default.</summary>
    Simulated,

    /// <summary>A device fed from a committed capture (read-only playback).</summary>
    Replay,
}
