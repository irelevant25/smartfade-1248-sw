using SmartFade.Protocol;

namespace SmartFade.Devices.Usb;

/// <summary>
/// Settings of the read-only USB device ("Device:Usb"). The timings default to what SmartSoft
/// was measured doing on the owner's PC: F62 (idle poll-to-poll median ≈ 31 ms, next poll
/// ≥ 10.3 ms after a fetch) and the 2026-10-30 re-count with `tools/protocol-analyzer/poll_rules.py`
/// over E2, E3 and E3b (poll submit to poll submit: min 20.59 ms, median 30.8–31.2 ms, n = 38 512;
/// fetch submit to next poll submit: min 10.35 ms, median 15.3–15.5 ms, n = 1 651; payload
/// completion to fetch submit: min 20.53 ms, median 28.3–28.8 ms, n = 1 651). These are OBSERVED
/// per-session host figures, not console limits (F62): the console has never been driven faster,
/// so the loop treats the observed minimums as floors, rounded up, and the settings can only make
/// it slower.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F62, poll_rules.py 2026-10-30", "SmartSoft's host-side cadence used as floors; the console's own limits are UNKNOWN")]
public sealed class UsbDeviceOptions
{
    public const string SectionName = "Device:Usb";

    /// <summary>Observed minimums (ms), rounded up, below which the console was never driven; the loop never goes below them.</summary>
    public static class Floors
    {
        /// <summary>Poll submit to the next poll submit: min 20.59 ms over 38 512 intervals (all three captures, every poll pair).</summary>
        public const int PollInterval = 21;

        /// <summary>Fetch submit to the next poll submit: min 10.35 ms over 1 651 fetches (F62 measures ≥ 10.30 ms from the fetch's completion).</summary>
        public const int PollAfterFetch = 11;

        /// <summary>Payload completion to the next fetch submit: min 20.53 ms over 1 651 fetches.</summary>
        public const int FetchDelay = 21;
    }

    /// <summary>Idle poll cadence; SmartSoft's median was 31 ms.</summary>
    public int PollIntervalMilliseconds { get; set; } = 31;

    /// <summary>Delay between a fetch and the poll that follows it; SmartSoft's median was 15 ms.</summary>
    public int PollAfterFetchMilliseconds { get; set; } = 15;

    /// <summary>Delay between a received payload and the next fetch; SmartSoft's median was 28 ms.</summary>
    public int FetchDelayMilliseconds { get; set; } = 28;

    /// <summary>How long a reply may take before the session is abandoned (the console answered within 12 ms in every capture).</summary>
    public int ReplyTimeoutMilliseconds { get; set; } = 500;

    /// <summary>Device path override; when empty the console's WinUSB interface 1 is located automatically. Any path must still name VID 14D5 / PID 0200 interface 1.</summary>
    public string? DevicePath { get; set; }

    /// <summary>Folder for the session's transfer log (our capture format); every session writes one file. Outside the repository by default.</summary>
    public string CaptureLogDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartFade", "captures");

    /// <summary>
    /// The local date (yyyy-MM-dd) for which hardware mode is confirmed; the backend refuses to
    /// start in Usb mode unless this equals today, so a value left in a shell or a settings file
    /// expires by itself ("this run only", SC-1 item 6).
    /// </summary>
    public string? ConfirmDate { get; set; }

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Max(PollIntervalMilliseconds, Floors.PollInterval));

    public TimeSpan PollAfterFetch => TimeSpan.FromMilliseconds(Math.Max(PollAfterFetchMilliseconds, Floors.PollAfterFetch));

    public TimeSpan FetchDelay => TimeSpan.FromMilliseconds(Math.Max(FetchDelayMilliseconds, Floors.FetchDelay));

    public TimeSpan ReplyTimeout => TimeSpan.FromMilliseconds(Math.Clamp(ReplyTimeoutMilliseconds, 50, 10_000));
}
