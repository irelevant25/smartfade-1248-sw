using SmartFade.Protocol;

namespace SmartFade.Devices.Usb;

/// <summary>
/// Settings of the USB device ("Device:Usb"), read-only unless <see cref="LiveControlDate"/> names today. The timings default to what SmartSoft
/// was measured doing on the owner's PC: F62 (idle poll-to-poll median ≈ 31 ms, next poll
/// ≥ 10.3 ms after a fetch) and the 2026-10-30 re-count with `tools/protocol-analyzer/poll_rules.py`
/// over E2, E3 and E3b (poll submit to poll submit: min 20.59 ms, median 30.8–31.2 ms, n = 38 512;
/// fetch submit to next poll submit: min 10.35 ms, median 15.3–15.5 ms, n = 1 651; payload
/// completion to fetch submit: min 20.53 ms, median 28.3–28.8 ms, n = 1 651). These are OBSERVED
/// per-session host figures, not console limits (F62): the console has never been driven faster,
/// so the loop treats the observed minimums as floors, rounded up, and the settings can only make
/// it slower. The write floors (live control, SC-2) come from SmartSoft's 828 writes in E9 and E9b,
/// counted on 2026-10-31 with `tools/protocol-analyzer/write_rules.py`: poll to the next write min 29.81 ms,
/// a write to the next poll or fetch min 14.51 ms, write to write min 44.77 ms, a key's press to its
/// release min 45.63 ms.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F62, poll_rules.py 2026-10-30", "SmartSoft's host-side cadence used as floors; the console's own limits are UNKNOWN")]
[Evidence(EvidenceStatus.Observed, "F131, write_rules.py 2026-10-31 (E9, E9b)", "SmartSoft's write spacing used as floors for live control (SC-2 condition 7)")]
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

        /// <summary>Payload completion to the next fetch submit: min 20.53 ms over 1 651 fetches. Also the least gap between a payload and a write (E9b min 20.53 ms after an object, 828 writes).</summary>
        public const int FetchDelay = 21;

        /// <summary>Poll submit to the next write submit (a write takes the poll's slot): min 29.81 ms over 828 writes (E9 401, E9b 427).</summary>
        public const int WriteAfterPoll = 30;

        /// <summary>Write payload to the next poll or fetch submit: min 14.51 ms (E9; E9b 14.69 ms), 13.63 ms from the payload's completion (E9b).</summary>
        public const int PollAfterWrite = 15;

        /// <summary>Write to the next write, header submit to header submit: min 44.77 ms between any two writes (E9; E9b 45.55 ms) and 45.63 ms from a key's press to its release (E9; E9b 45.88 ms), the larger rounded up.</summary>
        public const int WriteInterval = 46;
    }

    /// <summary>The default settings (ms): SmartSoft's measured cadence, rounded (OBSERVED, the sources above): the medians, except the write interval, which is the observed write-to-write minimum rounded up (44.77 / 45.55 ms in E9 / E9b; the median was 61.7 / 62.1 ms); live control never runs slower than these.</summary>
    public static class Defaults
    {
        public const int PollInterval = 31;

        public const int PollAfterFetch = 15;

        public const int FetchDelay = 28;

        public const int PollAfterWrite = 15;

        public const int WriteInterval = 46;

        /// <summary>The reply timeout's default (ms); live control never waits longer for a reply (the console answered within 12 ms in every capture up to E12; E12b's SD save is the one exception, F163).</summary>
        public const int ReplyTimeout = 500;
    }

    /// <summary>Idle poll cadence; SmartSoft's median was 31 ms.</summary>
    public int PollIntervalMilliseconds { get; set; } = Defaults.PollInterval;

    /// <summary>Delay between a fetch and the poll that follows it; SmartSoft's median was 15 ms.</summary>
    public int PollAfterFetchMilliseconds { get; set; } = Defaults.PollAfterFetch;

    /// <summary>Delay between a received payload and the next fetch; SmartSoft's median was 28 ms.</summary>
    public int FetchDelayMilliseconds { get; set; } = Defaults.FetchDelay;

    /// <summary>Delay between a write and the poll or fetch after it; SmartSoft's median was 15.4 ms (E9, E9b).</summary>
    public int PollAfterWriteMilliseconds { get; set; } = Defaults.PollAfterWrite;

    /// <summary>Least time from one write to the next; SmartSoft's writes in a burst were a median 61.8 ms apart (E9b), and one write per poll cycle (31 + 15 ms) gives 46 ms.</summary>
    public int WriteIntervalMilliseconds { get; set; } = Defaults.WriteInterval;

    /// <summary>How long a reply may take before the session is abandoned (the console answered within 12 ms in every capture up to E12; in E12b it answered nothing for about 0.85 s while it wrote the SD card, OBSERVED once, F163 / H109, so a save on the console during a session of ours ends it with a fault, and the E11 guide forbids one); with live control on, at most its default (<see cref="Defaults.ReplyTimeout"/>).</summary>
    public int ReplyTimeoutMilliseconds { get; set; } = Defaults.ReplyTimeout;

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

    /// <summary>
    /// The write switch of live control (SC-2 item 6): the local date (yyyy-MM-dd) for which ConsoleEvents of SC-2's
    /// whitelist may be sent, and of it only what SmartSoft was seen to send from the PC (channel fader 1, MASTER, BUMPS;
    /// bump 7, memory bump 1, GO, SOLO, BLACKOUT, FADERS 1-24, MEMS; see <see cref="UsbSmartFadeDevice"/>). It works only on top of the read-only mode (`Device:Mode=Usb` and <see cref="ConfirmDate"/> for today);
    /// the backend refuses to start when it is set to any other day or without that mode, and the device checks it
    /// again at Connect and at every command. Absent, or stale at Connect, the device stays read-only. Never a default.
    /// </summary>
    public string? LiveControlDate { get; set; }

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Max(PollIntervalMilliseconds, Floors.PollInterval));

    public TimeSpan PollAfterFetch => TimeSpan.FromMilliseconds(Math.Max(PollAfterFetchMilliseconds, Floors.PollAfterFetch));

    public TimeSpan FetchDelay => TimeSpan.FromMilliseconds(Math.Max(FetchDelayMilliseconds, Floors.FetchDelay));

    /// <summary>A write takes the poll's slot: the poll cadence, never below the 30 ms floor.</summary>
    public TimeSpan WriteAfterPoll => TimeSpan.FromMilliseconds(Math.Max(PollIntervalMilliseconds, Floors.WriteAfterPoll));

    public TimeSpan PollAfterWrite => TimeSpan.FromMilliseconds(Math.Max(PollAfterWriteMilliseconds, Floors.PollAfterWrite));

    public TimeSpan WriteInterval => TimeSpan.FromMilliseconds(Math.Max(WriteIntervalMilliseconds, Floors.WriteInterval));

    public TimeSpan ReplyTimeout => TimeSpan.FromMilliseconds(Math.Clamp(ReplyTimeoutMilliseconds, 50, 10_000));

    /// <summary>
    /// Live control runs at SmartSoft's cadence or faster down to the floors, never slower (the first review of SC-2
    /// item 10, minor: the write timings had no upper bound). The interlock accepts a report younger than two poll
    /// periods, a write waits for the poll, the last payload and the previous write, and a tap lasts until the next
    /// write slot, so each of these settings must stay at or below its default, SmartSoft's median
    /// (<see cref="Defaults"/>): a write then goes about 31 ms after its poll (SmartSoft: 29.81–47.99 ms, E9 and E9b)
    /// with a report at most 62 ms old, and a tap lasts about 46 ms. The reply timeout is bounded too (at most its default,
    /// 500 ms; the second pass of item 10, MINOR C): it caps how long a tap's press can stay unreleased when the console is
    /// slow to answer the poll between press and release (within 12 ms in every capture up to E12; E12b's SD save, F163, is the
    /// one case where the console answered nothing, for about 0.85 s, and a session of ours ends then). Null when they all
    /// do; else what does not.
    /// </summary>
    public string? LiveControlTimingProblem()
    {
        var over = new List<string>();
        Check(nameof(PollIntervalMilliseconds), PollIntervalMilliseconds, Defaults.PollInterval);
        Check(nameof(PollAfterFetchMilliseconds), PollAfterFetchMilliseconds, Defaults.PollAfterFetch);
        Check(nameof(FetchDelayMilliseconds), FetchDelayMilliseconds, Defaults.FetchDelay);
        Check(nameof(PollAfterWriteMilliseconds), PollAfterWriteMilliseconds, Defaults.PollAfterWrite);
        Check(nameof(WriteIntervalMilliseconds), WriteIntervalMilliseconds, Defaults.WriteInterval);
        Check(nameof(ReplyTimeoutMilliseconds), ReplyTimeoutMilliseconds, Defaults.ReplyTimeout);
        return over.Count == 0
            ? null
            : $"live control runs no slower than SmartSoft's measured cadence (the defaults): {string.Join(", ", over)}";

        void Check(string name, int value, int limit)
        {
            if (value > limit)
            {
                over.Add($"Device:Usb:{name}={value}, at most {limit}");
            }
        }
    }
}
