namespace SmartFade.Domain;

/// <summary>
/// Where the console's state at Connect stands: the real console's start-up pair and the download it starts (safety check
/// SC-4, docs/protocol/safety-checks.md; the switch `Device:Usb:StartupPairDate`). A device without it reports none.
/// </summary>
public enum StateAtConnectPhase
{
    /// <summary>No session runs; the next Connect requests the state.</summary>
    Idle,

    /// <summary>Connected: the request waits for its place (SmartSoft's, about 1.3 s after Connect); controls are refused meanwhile.</summary>
    Waiting,

    /// <summary>The first message went; the console's version is checked before the second (controls refused).</summary>
    Requesting,

    /// <summary>The console's state and show are being downloaded (7.2–7.6 s observed); every control is refused meanwhile.</summary>
    Downloading,

    /// <summary>The download is complete: the console's state and show are known.</summary>
    Done,

    /// <summary>The state was not requested, or the request ended before the download, with a reason; the session goes on as before.</summary>
    NotSent,
}

/// <summary>
/// What became of the MASTER probe after the download (SC-4 clause 4): the console reports the MASTER only with FaderInfo
/// (MASTER = body[194], F82, VERIFIED in part; the download never carries FaderInfo, F182 (d) VERIFIED, F186 OBSERVED).
/// </summary>
public enum MasterProbePhase
{
    /// <summary>The download has not completed yet.</summary>
    Pending,

    /// <summary>The console reported its faders (the MASTER) in this session: no probe is needed.</summary>
    NotNeeded,

    /// <summary>
    /// The probe (one bump tap of an unpatched channel of either bank, with the automatic switch to its bank and back where the
    /// console showed another; SC-4 clause 4 as amended on 2026-11-02, late evening) was queued, is under way or was sent.
    /// </summary>
    Sent,

    /// <summary>The probe could not run, or went home without the MASTER (the reason says why): one fader movement on the panel makes the console report it.</summary>
    NotPossible,

    /// <summary>
    /// The probe stopped on the way, or the session ended inside it (the first review's MINOR 1, 2026-11-03): nothing more of
    /// it was sent and it did not switch back, so the console may be left in another bank or mode than the download found it
    /// in; the reason says why and where ("the console shows …; the download reported …"), and asks for one fader movement
    /// when the MASTER is still unreported.
    /// </summary>
    Stopped,
}

/// <summary>
/// The console's state at Connect as a device reports it: the phase, the download's record count and progress, why the request
/// was not sent or ended, and the MASTER probe's outcome with its reason.
/// </summary>
public sealed record StateAtConnect(StateAtConnectPhase Phase, int? Records, int Delivered, string? Reason, MasterProbePhase MasterProbe, string? MasterProbeReason)
{
    public static StateAtConnect Idle { get; } = new(StateAtConnectPhase.Idle, null, 0, null, MasterProbePhase.Pending, null);

    /// <summary>True while the request or the download is under way: the device refuses every control meanwhile.</summary>
    public bool InProgress => Phase is StateAtConnectPhase.Waiting or StateAtConnectPhase.Requesting or StateAtConnectPhase.Downloading;
}
