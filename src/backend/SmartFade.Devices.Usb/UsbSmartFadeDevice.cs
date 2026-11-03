using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Domain;
using SmartFade.Protocol;
using SmartFade.Protocol.Objects;

namespace SmartFade.Devices.Usb;

/// <summary>
/// The real SmartFade 1248 over USB. It runs the poll / listed-fetch session that safety check SC-1 cleared
/// (docs/protocol/safety-checks.md) and reports what the console delivers. By default it is read-only and accepts no
/// command (<see cref="SupportedCommands"/> is None). With live control switched on for the day
/// (<see cref="UsbDeviceOptions.LiveControlDate"/>, SC-2 item 6) it also sends the SC-2 / SC-5 whitelist, and of it only
/// what SmartSoft was seen to send from the PC with the record keys off, in the console state it was sent in (E9 to E9e;
/// `tools/protocol-analyzer/write_rules.py`): the 24 physical faders as channel faders 1–24 (bank 1-24), 25–48 (bank
/// 25-48) and the memory faders of the page the console shows (memory mode), each with the MASTER at full; the MASTER and
/// BUMPS (channel mode, bank 1-24) as fader events; the 24 physical bumps in the same three banks as holds bounded to 3 s
/// (SC-5 class B3; <see cref="HspReadLoop.MaxHold"/>); SOLO, BLACKOUT, IND 1 and IND 2 as toggles from a steady LED; GO with
/// STACK dim; FADERS MEMS and FADERS 25-48 (from bank 1-24) and FADERS 1-24 (from memory mode or bank 25-48) as taps
/// (press, then release; condition 4). A control of another bank than the console shows starts the <b>automatic bank
/// switch</b> (SC-5 condition 1): the bank key as a tap, then nothing until ConsoleStatus reports the new bank and the LEDs
/// arrived (within <see cref="BankSwitchReportBound"/>, else the session ends as a deviation), then the control. A physical
/// fader whose content the console re-assigned (a bank, mode or page reported, and the session's start) is matched first:
/// its first event carries the target's reported level, and only then the UI's level follows (F172, SC-5 condition 1 d).
/// Every command is refused, with its reason, while REC MEM, EDIT MEM, COPY or REC SEQ is lit, blinking or unreported
/// (condition 5), while NEXT mode is on or unknown (F173), and before the console has reported what the control needs
/// (conditions 6, 8); a channel or memory fader event also while BLACKOUT's LED is not steady off (F125) and while a
/// bump of ours has not been read at its content again after its release (F174; with SOLO on, every fader: F6). A
/// queued write is asked again at the moment it would go, after its slot wait and right before its header (the gate,
/// then its own state), and dropped unsent if the console's state no longer allows it. In a live session a key-LED,
/// fader-mode or FaderInfo report that does not decode ends the session. Every transfer is logged to a capture file in our
/// normalized format. Any error ends the session in the Error state with its reason; nothing is retried. A stop waits for the
/// session to reach an idle point (a fetch is never left without its poll, a press never without its release, a write
/// never without the poll after it), and Connect is refused while a session is still running, or after a Data header
/// went out (or may have) without its payload: until the backend restarts, and while the
/// <see cref="IncompleteWriteMarker"/> it was given exists. The pipe comes from a factory so that tests run the same
/// device over a committed capture (<see cref="ReplayHspPipe"/>) and never over hardware.
/// <para>
/// With the start-up pair switched on for the day (<see cref="UsbDeviceOptions.StartupPairDate"/>, safety check SC-4, a
/// conditional GO: nothing sent before the review and the owner's go for E13), read-only or with live control, every
/// Connect's session sends SmartSoft's System pair once, where SmartSoft placed it (<see cref="HspReadSession"/>), unless what
/// the console already reported shows NEXT mode, BLACKOUT, a record function armed, bank 25-48 or a memory page other than 1
/// (clause 3; the transfer log and the UI say why), and the console's state and show arrive in the download it starts: the assembler takes the
/// state objects, <see cref="ConsoleShowAssembler"/> the show, and every memory the download did not list is reported
/// empty (INFERRED). Every control is refused from Connect until the download's last record (clauses 1, 2, 6). After the
/// download, with live control and no FaderInfo reported yet, the device taps the bump of an unpatched channel once (the
/// MASTER probe, clause 4, as amended on 2026-11-02, late evening): the first of the shown bank, else of the other channel
/// bank (in memory mode bank 1-24's first, else bank 25-48's), after SC-5's automatic switch to that bank where needed and,
/// once the tap's reports are in, the automatic switch back to the bank or mode the download reported, each switch one a PC
/// was seen making (memory mode and bank 25-48 only through bank 1-24, <see cref="ProbeRoute"/>); every control is refused
/// meanwhile. When it cannot, the UI asks for one fader movement on the panel.
/// </para>
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F55, F57", "the read-only session as SC-1 cleared it (2026-10-30)")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133, F134", "live control, SC-2 classes A and B1 (2026-10-31): the whitelisted ConsoleEvents as SmartSoft's and the console's behaviour; a conditional GO, nothing sent before E11 on the owner's go")]
[Evidence(EvidenceStatus.Verified, "F171, F174, F175", "live control, SC-5 classes A2-A4, B3-B5 (2026-11-02, with the addenda): every physical fader and bump in three banks, the bank keys, IND 1 / 2 as SmartSoft's events and the console's answers to them; a conditional GO, nothing sent before the review and E11b on the owner's go")]
[Evidence(EvidenceStatus.Observed, "F172 (a) VERIFIED in part, (b) and (c) OBSERVED once; the 1-24 / 25-48 bank switch in none of its cases", "the automatic bank switch's matching event: after a mode switch the console ignored SmartSoft's events for a re-assigned fader until one reached the content's level (case a, E9c and both E9d passes); after a physical level (b) and a page change (c) once each; that a 1-24 / 25-48 switch behaves alike is INFERRED; a matching event from our own host is unobserved until E11b")]
[Evidence(EvidenceStatus.Inferred, "F82, F96, F112, F174", "the matching level: ChannelLevels[c] agreeing with FaderInfo record k byte 0 (a memory fader: record k bytes 0 and 1) is read as the fader content's own level, which rules out a memory up on another fader and the stack (INFERRED), not a held bump (F174, VERIFIED: the bump's level is in both reports during a hold), so the physical fader of a bump this host pressed is refused until its reports read the content again after the release (BumpShadow)")]
[Evidence(EvidenceStatus.Observed, "F97, F174, F178 VERIFIED at BUMPS 0xFF; the owner's later E11b session (backend-6.jsonl seq 4571 / 4609) OBSERVED once, log-only", "the bump shadow's end: a held bump reads the BUMPS level in FaderInfo record k and ChannelLevels[c] (VERIFIED at BUMPS 0xFF, F97, F174, F178; 0x81 once in memory mode, F178; below full in channel mode never captured, there INFERRED), every observed release that changed a level was reported with at most one quiet poll reply before it (SmartSoft's E9c 7, E9d 52, E9e 8 releases; ours E11 attempt 3 4, E11b 3), a tap's press may be reported after its release (F168), and a bump on a fader whose content was already at the bump's level listed nothing at the press nor at the release (the owner's log); the rule built on them (a report after the release at a level other than the bump's or at the pre-press level, or two quiet poll replies after it) is INFERRED")]
[Evidence(EvidenceStatus.Observed, "F125 (OBSERVED once, timed, E8b); F114 VERIFIED for the LED", "BLACKOUT zeroes FaderInfo record k byte 0 and ChannelLevels[c] and leaves the MASTER byte at 0xFF (E8b seq 26987, 26991, 26999; the content back at 27754 / 27762), so during a blackout the reported level is not the content's: channel and memory fader events go only while LED block 53 reads steady off (F114: steady blue while BLACKOUT is on); how the console answers a matching event at 0x00 during a blackout is UNKNOWN (never sent, never captured)")]
[Evidence(EvidenceStatus.Inferred, "F6 (DOCUMENTED), F125, F176", "a bump held with SOLO on replaces the output (F6), so the other faders' reports may carry it too, ChannelLevels at least and FaderInfo as BLACKOUT (F125) and the MASTER (F176) do: unobserved (E9c's only SOLO-mode hold, header 87996, had nothing else up), so a bump of ours under SOLO keeps every physical fader waiting until FaderInfo and ChannelLevels were reported after its release")]
[Evidence(EvidenceStatus.Inferred, "F57, F116", "the interlock's freshness reading: a poll reply within two poll periods that listed no LedStatus means the key LEDs are current (F57: a list names what changed; F116: a record screen opens and closes with exactly one LedStatus payload, 28 of 28 and 4 of 4); not observed as a rule of the console")]
[Evidence(EvidenceStatus.Observed, "write_rules.py 2026-10-31 (E9, E9b); E9c, E9d, E9e", "the console states SmartSoft sent each whitelisted control in; the device sends each control only in those states")]
[Evidence(EvidenceStatus.Verified, "F181, F182", "the start-up pair, SC-4 class E (2026-11-02, evening): SmartSoft's two System writes and the console's answers as SmartSoft's and the console's behaviour (20 sessions); a conditional GO, nothing sent before the review and E13 on the owner's go")]
[Evidence(EvidenceStatus.Observed, "F185", "the states the 20 pairs were sent in; the pair is refused when the console already reported NEXT, BLACKOUT, a record function armed, bank 25-48 or a memory page other than 1 (SC-4 clause 3, the page added 2026-11-03 after the first review's MAJOR 1), sent blind otherwise, as SmartSoft sends it (S11.17)")]
[Evidence(EvidenceStatus.Inferred, "F174, F178 (VERIFIED at BUMPS 0xFF); E8b channel 49, E9d channel 47 (OBSERVED)", "the MASTER probe (SC-4 clause 4): a bump press makes the console list FaderInfo with the MASTER and BUMPS; an unpatched channel changes no DMX; the probe itself unobserved until E13")]
[Evidence(EvidenceStatus.Observed, "F133, F134 VERIFIED for FADERS 1-24 and FADERS MEMS from SmartSoft (E9 + E9b); F175 for FADERS 25-48 from SmartSoft (OBSERVED, second sessions) with F171 VERIFIED for the faders after it; F179 OBSERVED once for our host (E11b)", "the MASTER probe's switches (SC-4 clause 4 as amended on 2026-11-02, late evening), each a bank transition a PC host was seen making: bank 1-24 to 25-48 (SmartSoft E9b seq 51617, E9c 24528, E9d 18706 / 74718, E9e 19934 / 30238; our host E11b bus seq 25322 and 47929) and back (E9b 51978, E9c 28206, E9d 27831 / 85917, E9e 21374 / 31533; E11b 30193 and 48221), bank 1-24 to memory mode (E9 to E9d, e.g. E9d 29032; E11 attempt 3 bus 23677, E11b 34154) and back (e.g. E9d 44811; E11 attempt 3 32624, E11b 43651); memory mode to bank 25-48 and back never from a PC in any capture, so the probe goes through bank 1-24; every one of those switches came on memory page 1 (body[2] = 0), so a switching probe runs only there")]
public sealed class UsbSmartFadeDevice : ISmartFadeDevice, IAsyncDisposable
{
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// SC-5 condition 1 (c): after the automatic switch's bank key went out, ConsoleStatus must report the new bank and a
    /// LedStatus must arrive within this bound; otherwise the session ends as a deviation (the console answered the panel's
    /// and SmartSoft's bank keys at the next poll in every capture: E9b, E9c S2, E9d B, E9e, OBSERVED).
    /// </summary>
    public static readonly TimeSpan BankSwitchReportBound = TimeSpan.FromSeconds(2);

    /// <summary>The port commands live control accepts (SC-2 item 6, SC-5); each one refuses every control outside the whitelist.</summary>
    private const DeviceCommands LiveCommands = DeviceCommands.SetChannelLevel | DeviceCommands.SetMaster | DeviceCommands.SetBumps
        | DeviceCommands.SetBlackout | DeviceCommands.SetSolo | DeviceCommands.BumpChannel | DeviceCommands.SetMemoryLevel
        | DeviceCommands.BumpMemory | DeviceCommands.SetIndependent | DeviceCommands.PressGo | DeviceCommands.PressFadersMems
        | DeviceCommands.PressFaders1To24 | DeviceCommands.PressFaders25To48;

    /// <summary>
    /// The controls the command methods send, by name (docs/api.md `writableControls`): every channel fader and bump 1–48,
    /// every memory fader and bump of every page (each sent only on the page the console shows, F113), MASTER, BUMPS,
    /// SOLO, BLACKOUT, IND 1, IND 2, GO, FADERS MEMS, FADERS 1-24, FADERS 25-48. Not CLEAR, STACK, PAUSE, NEXT or a record key.
    /// </summary>
    private static readonly IReadOnlySet<string> LiveControls = new HashSet<string>(
        new[]
        {
            DeviceControls.Master, DeviceControls.Bumps, DeviceControls.Solo, DeviceControls.Blackout, DeviceControls.Go,
            DeviceControls.FadersMems, DeviceControls.Faders1To24, DeviceControls.Faders25To48,
            DeviceControls.Independent(1), DeviceControls.Independent(2),
        }
            .Concat(Enumerable.Range(1, ConsoleCapabilities.SmartFade1248.Channels).SelectMany(channel => new[] { DeviceControls.ChannelFader(channel), DeviceControls.ChannelBump(channel) }))
            .Concat(Enumerable.Range(1, ConsoleCapabilities.SmartFade1248.Memories).SelectMany(memory => new[] { DeviceControls.MemoryFader(memory), DeviceControls.MemoryBump(memory) })));

    private static readonly IReadOnlySet<string> NoControls = new HashSet<string>();

    /// <summary>The objects live control's interlocks read (key LEDs, fader mode and page, the MASTER): in a live session one that does not decode ends it.</summary>
    private static readonly IReadOnlySet<int> InterlockObjects = new HashSet<int>
    {
        (int)HspObjectId.LedStatus, (int)HspObjectId.ConsoleStatus, (int)HspObjectId.FaderInfo,
    };

    private readonly object _gate = new();
    private readonly UsbDeviceOptions _options;
    private readonly Func<IHspPipe> _pipeFactory;
    private readonly Func<string, ITransferSink?> _logFactory;
    private readonly ILogger<UsbSmartFadeDevice> _logger;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delay;
    private readonly IncompleteWriteMarker? _marker;
    private readonly TimeSpan _stopTimeout;
    private readonly Dictionary<ClearedKey, (KeyLed Before, Func<KeyLed?> Led)> _togglesAwaitingLed = new();
    private readonly HashSet<ClearedKey> _togglesSent = new();

    // SC-5 condition 1 (all under _gate): the physical faders this session has matched since the console last re-assigned
    // them, the UI's latest level for a fader whose matching event waits (or whose switch does), the automatic switch in
    // progress, what the console's faders controlled at its last ConsoleStatus, and the hold the UI is pressing.
    private readonly HashSet<ClearedFader> _matched = new();
    private readonly Dictionary<ClearedFader, PendingLevel> _pendingLevels = new();
    private BankSwitch? _switch;
    private (FaderBank? Bank, int? Page)? _assignment;
    private HeldBump? _hold;
    private ITransferSink? _liveLog;

    // The physical faders whose reports may carry a bump this session pressed (F174), from the press until their reports read
    // the content again after the release (BumpShadow, ShadowEnds); and the notes the writes' sent callbacks leave for the
    // transfer log, written at the next event that ends an exchange, after the write's payload (all under _gate).
    private readonly List<BumpShadow> _bumpShadows = new();
    private readonly List<string> _pendingNotes = new();
    // The start-up pair (SC-4; all under _gate): the session that sends it, the show its download delivers, what the device
    // last reported of it, and the MASTER probe's outcome.
    private HspReadSession? _pairSession;
    private ITransferSink? _pairLog;
    private ConsoleShowAssembler _show;
    private StateAtConnect? _lastStateAtConnect;
    private bool _downloadComplete;
    private bool _probeTried;
    private MasterProbePhase _probe = MasterProbePhase.Pending;
    private string? _probeReason;
    private string? _probeNote;
    private MasterProbeRun? _probeRun;
    private string? _probeInterruption;   // a session that ended inside the probe: said by its error or by the Disconnect, then cleared
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private ConsoleStateAssembler _assembler;
    private CancellationTokenSource? _sessionStop;
    private Task? _session;
    private HspReadSession? _liveSession;
    private long? _lastReportStamp;
    private string? _incompleteWrite;
    private long? _lastPollStamp;
    private int _generation;
    private bool _stopTimedOut;

    /// <param name="incompleteWriteMarker">
    /// Where an incomplete write is recorded so that it outlives the process (the backend passes one beside the transfer
    /// logs); null keeps the latch in memory only, until the process ends.
    /// </param>
    /// <param name="stopTimeout">
    /// How long a Disconnect waits for the session to end before it reports Error and leaves the late end to the session
    /// (20 s; null keeps that; the tests shorten it to reach that path).
    /// </param>
    public UsbSmartFadeDevice(
        IOptions<UsbDeviceOptions> options,
        Func<IHspPipe> pipeFactory,
        Func<string, ITransferSink?> logFactory,
        ILogger<UsbSmartFadeDevice> logger,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        IncompleteWriteMarker? incompleteWriteMarker = null,
        TimeSpan? stopTimeout = null)
    {
        _options = options.Value;
        _pipeFactory = pipeFactory;
        _logFactory = logFactory;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _delay = delay;
        _marker = incompleteWriteMarker;
        _stopTimeout = stopTimeout ?? DefaultStopTimeout;
        Capabilities = ConsoleCapabilities.SmartFade1248;
        _assembler = new ConsoleStateAssembler(Capabilities);
        _show = new ConsoleShowAssembler(Capabilities);
    }

    public DeviceMode Mode => DeviceMode.Hardware;

    /// <summary>No transport detail here (the UI never sees transports); the device path goes to the log.</summary>
    public string Description => (IsLiveControlConfirmed() ? "SmartFade 1248, live control" : "SmartFade 1248, read-only")
        + (IsStartupPairConfirmed() ? ", state at Connect" : string.Empty);

    /// <summary>True while the start-up pair's switch and hardware mode both name today (SC-4 item 6): every Connect then requests the console's state.</summary>
    public bool StateAtConnectEnabled => IsStartupPairConfirmed();

    public ConnectionStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public ConsoleCapabilities Capabilities { get; }

    /// <summary>
    /// None while the console is only read (SC-1 clears the poll and the listed fetch, nothing that changes state);
    /// with live control confirmed for today, the port commands that carry the SC-2 whitelist (SC-2 item 6). Each of
    /// them still refuses every control outside it (channel faders 2–48, bumps other than 1, 7, 9).
    /// </summary>
    public DeviceCommands SupportedCommands => IsLiveControlConfirmed() ? LiveCommands : DeviceCommands.None;

    /// <summary>
    /// The controls live control sends, by name (SC-2 item 6; docs/api.md `writableControls`): exactly what the command
    /// methods accept, so the UI enables nothing the device would refuse. Empty while the device is read-only.
    /// </summary>
    public IReadOnlySet<string>? WritableControls => IsLiveControlConfirmed() ? LiveControls : NoControls;

    /// <summary>Counters of the current or last session, for the log and the experiment record.</summary>
    public HspReadSession? LastSession { get; private set; }

    /// <summary>True when the last session had to be stopped while the console still listed objects.</summary>
    public bool LastStopWasForced { get; private set; }

    public event EventHandler<DeviceStateChange>? StateChanged;

    public event EventHandler<ConnectionState>? ConnectionChanged;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        int generation;
        lock (_gate)
        {
            if (_status is ConnectionStatus.Connected or ConnectionStatus.Connecting)
            {
                return Task.CompletedTask;
            }

            if (_session is { IsCompleted: false })
            {
                _logger.LogWarning("Connect refused: the previous session has not ended yet");
                ConnectionChanged?.Invoke(this, new ConnectionState(_status, Mode, Description, "The previous session has not ended yet; wait for it or restart the backend."));
                return Task.CompletedTask;
            }

            if (IncompleteWriteRefusal() is { } refusal)
            {
                // The console may still wait for a payload; a new session's first poll would arrive in its place (never observed).
                _logger.LogWarning("Connect refused: {Reason}", refusal);
                ConnectionChanged?.Invoke(this, new ConnectionState(_status, Mode, Description, refusal));
                return Task.CompletedTask;
            }

            _status = ConnectionStatus.Connecting;
            generation = ++_generation;
            _assembler = new ConsoleStateAssembler(Capabilities);
            _show = new ConsoleShowAssembler(Capabilities);
        }

        RaiseConnection(ConnectionStatus.Connecting);

        // Hardware mode is confirmed per day (Program.cs refuses to start otherwise); a backend left
        // running past midnight must not connect again without a fresh confirmation.
        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        if (!ServiceCollectionExtensions.IsConfirmedForToday(_options.ConfirmDate, today))
        {
            _logger.LogWarning("Connect refused: hardware mode is confirmed for {ConfirmDate}, today is {Today}", _options.ConfirmDate ?? "no date", today);
            Fail(generation, $"Hardware mode is confirmed for {_options.ConfirmDate ?? "no date"} and today is {today:yyyy-MM-dd}: restart the backend with --Device:Usb:ConfirmDate={today:yyyy-MM-dd}.");
            return Task.CompletedTask;
        }

        // Live control is decided per session (SC-2 item 6): only when its own switch names today too and the timings stay
        // within SmartSoft's cadence; otherwise the session is read-only.
        var liveControl = IsLiveControlConfirmed();
        if (!liveControl && !string.IsNullOrWhiteSpace(_options.LiveControlDate))
        {
            if (_options.LiveControlTimingProblem() is { } timing)
            {
                _logger.LogWarning("Live control refused ({Problem}): this session is read-only", timing);
            }
            else
            {
                _logger.LogWarning("Live control is confirmed for {LiveControlDate}, today is {Today}: this session is read-only", _options.LiveControlDate, today);
            }
        }

        // The start-up pair is decided per session as well (SC-4 item 6): only when its own switch names today; otherwise this
        // session sends no pair and keeps SC-1's bounds.
        var startupPair = IsStartupPairConfirmed();
        if (!startupPair && !string.IsNullOrWhiteSpace(_options.StartupPairDate))
        {
            _logger.LogWarning("The start-up pair is confirmed for {StartupPairDate}, today is {Today}: this session sends no pair", _options.StartupPairDate, today);
        }

        IHspPipe pipe;
        try
        {
            pipe = _pipeFactory();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The console was not found");
            Fail(generation, $"Cannot open the console: {WithoutDevicePath(ex.Message)}");
            return Task.CompletedTask;
        }

        // The log is evidence (SC-1 item 8). It exists before the console is opened, so that whatever
        // the open itself puts on the bus (an open question, H68) happens inside a logged session.
        ITransferSink log;
        try
        {
            log = _logFactory(pipe.Description) ?? throw new InvalidOperationException("no transfer sink was provided");
            log.Note($"session start on {pipe.Description}");
            log.Note(liveControl
                ? "live control on (SC-2): only the whitelisted ConsoleEvents may be sent, behind the record-function interlock"
                : startupPair
                    ? "read-only session (SC-1) with the start-up pair: polls, listed fetches and SmartSoft's two System writes"
                    : "read-only session (SC-1): polls and listed fetches only");
            if (startupPair)
            {
                log.Note("start-up pair on (SC-4): System 09 00 00 at the first write slot after reply header #40 with nothing listed, 07 00 00 after #79 and only after ConsoleVersion read 3.0.2.9.0.43, each once and as the session's first two data writes; refused when the console already reported NEXT, BLACKOUT, a record function armed, bank 25-48 or a memory page other than 1; nothing else is sent before the download's last record");
            }
        }
        catch (Exception ex)
        {
            pipe.Dispose();
            _logger.LogError(ex, "The transfer log could not be created; the console was not opened");
            Fail(generation, $"transfer log unavailable: {ex.Message}");
            return Task.CompletedTask;
        }

        try
        {
            pipe.Open();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The console could not be opened");
            CloseLog(log, $"open failed: {ex.Message}");
            pipe.Dispose();
            Fail(generation, $"Cannot open the console: {WithoutDevicePath(ex.Message)}");
            return Task.CompletedTask;
        }

        // Without the pair, the sessions SC-1 and SC-2 reviewed, constructed as before; with it, the pair's gate (SC-4).
        var session = startupPair
            ? new HspReadSession(liveControl ? LiveWriteGate : null, StartupPairGate)
            : liveControl ? new HspReadSession(LiveWriteGate) : new HspReadSession();
        var stop = new CancellationTokenSource();
        lock (_gate)
        {
            if (_generation != generation)
            {
                log.Dispose();
                pipe.Dispose();
                stop.Dispose();
                return Task.CompletedTask;
            }

            _sessionStop = stop;
            _liveSession = liveControl ? session : null;
            _liveLog = liveControl ? log : null;
            _pairSession = startupPair ? session : null;
            _pairLog = startupPair ? log : null;
            _lastStateAtConnect = null;
            _downloadComplete = false;
            _probeTried = false;
            _probe = MasterProbePhase.Pending;
            _probeReason = null;
            _probeInterruption = null;
            _lastReportStamp = null;
            _togglesAwaitingLed.Clear();
            _togglesSent.Clear();
            ResetLiveState();   // every physical fader starts unmatched: the console's state at our start is not ours (F172)
            _status = ConnectionStatus.Connected;
        }

        // Connected is reported before the first transfer, so that a session that fails at once
        // still ends in the Error state after it (never Error, then Connected).
        if (liveControl)
        {
            _logger.LogWarning("HARDWARE MODE, LIVE CONTROL: session starting on {Pipe}; the poll, the listed fetch (SC-1) and the SC-2 whitelist of ConsoleEvents may be sent", pipe.Description);
        }
        else
        {
            _logger.LogWarning("HARDWARE MODE: read-only session starting on {Pipe}; only the poll and the listed fetch are sent (SC-1){Pair}", pipe.Description, startupPair ? ", and SmartSoft's start-up pair (SC-4)" : string.Empty);
        }

        if (startupPair)
        {
            _logger.LogWarning("STATE AT CONNECT (SC-4): SmartSoft's start-up pair 09 00 00 / 07 00 00 is sent once in this session; every control is refused until the download's last record");
        }

        RaiseConnection(ConnectionStatus.Connected);
        if (startupPair)
        {
            StateChanged?.Invoke(this, new DeviceStateChange(StateAtConnect: new StateAtConnect(StateAtConnectPhase.Waiting, null, 0, null, MasterProbePhase.Pending, null)));
        }
        lock (_gate)
        {
            if (_generation != generation)
            {
                _liveSession = null;
                _liveLog = null;
                log.Dispose();
                pipe.Dispose();
                stop.Dispose();
                return Task.CompletedTask;
            }

            _session = Task.Run(() => RunSessionAsync(pipe, log, session, generation, stop.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Task? session;
        CancellationTokenSource? stop;
        int generation;
        lock (_gate)
        {
            if (_status == ConnectionStatus.Disconnected)
            {
                return;
            }

            generation = ++_generation;
            stop = _sessionStop;
            session = _session;
            _sessionStop = null;
            _liveSession = null;   // no command is accepted any more; the loop drops what is queued and sends an owed release
            _pairSession = null;   // the pair's gate refuses from now on: a stop sends nothing new (SC-4 clause 7)
        }

        stop?.Cancel();
        var ended = session is null;
        if (session is not null)
        {
            try
            {
                await session.WaitAsync(_stopTimeout, cancellationToken).ConfigureAwait(false);
                ended = true;
            }
            catch (TimeoutException)
            {
                _logger.LogError("The USB session did not stop within {Timeout}; the pipe stays owned by it", _stopTimeout);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Session ended while disconnecting");
                ended = true;
            }
        }

        stop?.Dispose();
        lock (_gate)
        {
            if (ended)
            {
                _session = null;
            }
            else
            {
                _stopTimedOut = true;   // the session reports its own end when it finally stops
            }

            _status = ended ? ConnectionStatus.Disconnected : ConnectionStatus.Error;
        }

        if (ended)
        {
            string? interruption;
            lock (_gate)
            {
                interruption = _probeInterruption;   // a Disconnect inside the MASTER probe: the UI is told where the console was left (MINOR 1)
                _probeInterruption = null;
            }

            _logger.LogInformation("USB session closed");
            ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Disconnected, Mode, Description, interruption));
        }
        else
        {
            ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Error, Mode, Description, "The session did not stop; restart the backend before connecting again."));
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Disconnect on dispose failed");
        }
    }

    public Task<ConsoleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_assembler.Snapshot());
        }
    }

    /// <summary>
    /// A channel fader, 1–48 (SC-2 class A, SC-5 classes A2 and A3): channels 1–24 are physical faders 1–24 in bank 1-24,
    /// channels 25–48 the same physical faders in bank 25-48 (F171 VERIFIED: index N − 1 moved ChannelLevels[N] in bank 1-24
    /// and ChannelLevels[24 + N] in bank 25-48, E9c and both E9d passes). Sent only with the MASTER at full, as every PC
    /// channel-fader event of E9 to E9e whose MASTER was known but E9c S7's (the reported level is post-MASTER, F96, F176:
    /// only at full is it the fader's own, SC-2 condition 6). In the other bank the automatic switch goes first; a fader
    /// not matched since the console last re-assigned it first gets its reported level (SC-5 condition 1).
    /// </summary>
    public Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken)
    {
        var session = LiveSession();
        if (!Capabilities.IsValidChannel(channel))
        {
            throw NotCleared($"Channel {channel} does not exist on the 1248");
        }

        var (bank, physical) = ChannelPosition(channel);
        return SetFaderLevel(session, new Target(bank, null, physical, $"channel fader {channel}", channel), level);
    }

    /// <summary>The MASTER (index 0x30, SC-2 class A), in channel mode, bank 1-24 as every PC event; it changes the whole live output, as the panel's MASTER does.</summary>
    public Task SetMasterAsync(byte level, CancellationToken cancellationToken) =>
        QueueLiveFader(LiveSession(), ClearedFader.Master, level, () => NotInChannelBank1To24("the MASTER"));

    /// <summary>BUMPS (index 0x31, SC-2 class A), in channel mode, bank 1-24 as every PC event.</summary>
    public Task SetBumpsAsync(byte level, CancellationToken cancellationToken) =>
        QueueLiveFader(LiveSession(), ClearedFader.Bumps, level, () => NotInChannelBank1To24("BUMPS"));

    /// <summary>BLACKOUT as a toggle: one tap, only from a steady LED (block 53) that differs from the wish, in channel mode, bank 1-24 (SC-2 class B1).</summary>
    public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken) =>
        Toggle(ClearedKey.Blackout, on, () => _assembler.Keys?.Blackout, "BLACKOUT");

    /// <summary>SOLO as a toggle: one tap, only from a steady LED (block 52) that differs from the wish, in channel mode, bank 1-24 (SC-2 class B1).</summary>
    public Task SetSoloAsync(bool on, CancellationToken cancellationToken) =>
        Toggle(ClearedKey.Solo, on, () => _assembler.Keys?.Solo, "SOLO");

    /// <summary>
    /// The bump under a channel fader, 1–48, held while the UI holds it and at most 3 s (SC-5 class B3 and condition 2):
    /// physical bump N in bank 1-24 for channel N, in bank 25-48 for channel 24 + N (F174; the channel bumps other than 1,
    /// 2, 7, 10, 12, 24, 25 and 48 on SC-5 addendum 2's physical-control rule). A press queues the hold (after the automatic
    /// switch when the console shows another bank); a release asks for its release, once.
    /// </summary>
    public Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken)
    {
        var control = DeviceControls.ChannelBump(channel);
        if (!pressed)
        {
            return ReleaseBump(control);
        }

        var session = LiveSession();
        if (!Capabilities.IsValidChannel(channel))
        {
            throw NotCleared($"Channel {channel} does not exist on the 1248");
        }

        var (bank, physical) = ChannelPosition(channel);
        return PressBump(session, new Target(bank, null, physical, $"bump {channel}", channel), control);
    }

    /// <summary>
    /// A memory fader of the page the console shows (SC-5 class A4: physical fader k in memory mode is memory fader k of
    /// that page, F171, F112; no page change is ever sent, F113), with the MASTER at full as every PC memory-fader event
    /// of E9c and E9d; from channel mode, bank 1-24, the automatic switch (FADERS MEMS) goes first; the fader is matched
    /// at its reported level first (FaderInfo record k bytes 0 and 1, SC-5 condition 1 d).
    /// </summary>
    public Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken)
    {
        var session = LiveSession();
        if (!Capabilities.IsValidMemoryPage(page) || !Capabilities.IsValidMemoryFader(fader))
        {
            throw NotCleared($"Memory fader {fader} of page {page} does not exist on the 1248");
        }

        return SetFaderLevel(session, new Target(FaderBank.Memories, page, fader, $"memory fader {fader} of page {page}", null), level);
    }

    /// <summary>
    /// The bump under a memory fader of the page the console shows, held as <see cref="BumpChannelAsync"/> (SC-5 class B3:
    /// every memory bump 1–24 in both E9d passes, F174 VERIFIED).
    /// </summary>
    public Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken)
    {
        var control = DeviceControls.MemoryBump(Capabilities.IsValidMemoryPage(page) && Capabilities.IsValidMemoryFader(fader) ? Capabilities.MemoryNumber(page, fader) : 0);
        if (!pressed)
        {
            return ReleaseBump(control);
        }

        var session = LiveSession();
        if (!Capabilities.IsValidMemoryPage(page) || !Capabilities.IsValidMemoryFader(fader))
        {
            throw NotCleared($"Memory bump {fader} of page {page} does not exist on the 1248");
        }

        return PressBump(session, new Target(FaderBank.Memories, page, fader, $"memory bump {fader} of page {page}", null), control);
    }

    public Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken) => throw NotCleared();

    public Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken) => throw NotCleared();

    /// <summary>
    /// IND 1 / IND 2 (codes 0x47 / 0x48, SC-5 class B5) as toggles, like SOLO and BLACKOUT: one tap, only from a steady LED
    /// (blocks 72 / 73, F115) that differs from the wish and that the Independents object, when it has arrived, agrees with;
    /// in channel mode, bank 1-24, where every PC press of E9b and E9c came. What they drive is the rig's (SC-5 item 4).
    /// </summary>
    public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken)
    {
        var (key, name) = number switch
        {
            1 => (ClearedKey.Ind1, "IND 1"),
            2 => (ClearedKey.Ind2, "IND 2"),
            _ => throw NotCleared($"IND {number} does not exist on the 1248"),
        };

        return Toggle(key, on, () => IndependentLed(number), name, () => IndependentState(number));
    }

    public Task ClearChannelLevelsAsync(CancellationToken cancellationToken) => throw NotCleared();

    public Task ClearMemoryLevelsAsync(CancellationToken cancellationToken) => throw NotCleared();

    /// <summary>
    /// GO (code 0x31, SC-2 class B1) as a tap, in channel mode, bank 1-24, while STACK's LED reads dim and steady: both
    /// PC presses of E9 and E9b came then (F134: GO from a PC only with STACK unlit). On the port since 2026-10-31, evening
    /// (<see cref="DeviceCommands.PressGo"/>): the API and the UI reach it behind the same interlocks.
    /// </summary>
    public Task PressGoAsync(CancellationToken cancellationToken) =>
        QueueLiveTap(LiveSession(), ClearedKey.Go, () => NotInChannelBank1To24("GO") ?? StackNotAsObserved());

    /// <summary>FADERS MEMS (code 0x39) as a tap, from channel mode, bank 1-24, as every PC press (E9 to E9d). On the port (<see cref="DeviceCommands.PressFadersMems"/>).</summary>
    public Task PressFadersMemsAsync(CancellationToken cancellationToken) =>
        PressBankKey(ClearedKey.FadersMems);

    /// <summary>FADERS 1-24 (code 0x36) as a tap, from memory mode or from bank 25-48, as every PC press. On the port (<see cref="DeviceCommands.PressFaders1To24"/>).</summary>
    public Task PressFaders1To24Async(CancellationToken cancellationToken) =>
        PressBankKey(ClearedKey.Faders1To24);

    /// <summary>
    /// FADERS 25-48 (code 0x37, SC-5 class B4) as a tap, from channel mode, bank 1-24: every PC press of E9b, E9c, E9d and
    /// E9e came from there (never from memory mode). On the port (<see cref="DeviceCommands.PressFaders25To48"/>).
    /// </summary>
    public Task PressFaders25To48Async(CancellationToken cancellationToken) =>
        PressBankKey(ClearedKey.Faders25To48);

    public Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        MemoryContent? content;
        lock (_gate)
        {
            content = _assembler.Memory(page, fader);
        }

        return content is not null
            ? Task.FromResult(content)
            : throw new NotSupportedException("The console has not delivered this memory in the current session.");
    }

    private static NotSupportedException ReadOnly() =>
        new("The USB device is read-only: no message that changes the console is cleared for sending.");

    /// <summary>Everything outside what live control sends: read-only's refusal, or live control's, which names what it sends.</summary>
    private NotSupportedException NotCleared(string? what = null) =>
        !IsLiveControlConfirmed()
            ? ReadOnly()
            : new NotSupportedException($"{what ?? "This command is not cleared"}. Live control sends only the channel faders and bumps 1-48, the memory faders and bumps of the page the console shows, MASTER, BUMPS, GO, SOLO, BLACKOUT, IND 1, IND 2 and the FADERS keys (SC-2, SC-5, as seen from SmartSoft); never a record key, CLEAR, STACK, PAUSE, NEXT, a page change or a System write.");

    private static void Refuse(string? reason)
    {
        if (reason is not null)
        {
            throw new InvalidOperationException($"Refused: {reason}.");
        }
    }

    /// <summary>True when the start-up pair's switch and hardware mode both name today (SC-4 item 6: the switch expires by itself, like the others).</summary>
    private bool IsStartupPairConfirmed()
    {
        if (string.IsNullOrWhiteSpace(_options.StartupPairDate))
        {
            return false;
        }

        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        return ServiceCollectionExtensions.IsConfirmedForToday(_options.StartupPairDate, today)
            && ServiceCollectionExtensions.IsConfirmedForToday(_options.ConfirmDate, today);
    }

    /// <summary>
    /// The start-up pair's gate (SC-4 clauses 1 and 3), asked on the session's thread at the slot where `09` or `07` would go,
    /// after the session's own placement and version conditions and right before the header: it refuses when the session is
    /// ending or the switch expired; holds while the console's last report is older than two poll periods (condition 5's
    /// freshness, as for every write) or its key LEDs or fader mode are listed and not fetched; and refuses when what the
    /// console already reported shows a state no pair was ever seen in (<see cref="PairStateRefusal"/>). Otherwise it sends,
    /// blind, as SmartSoft does (S11.17): what the console does before it reported anything is the operator's, as the UI says.
    /// </summary>
    private HspWriteDecision StartupPairGate(HspReadSession session, ClearedSystemMessage message)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_pairSession, session))
            {
                return HspWriteDecision.Drop("the session is ending");
            }

            if (!IsStartupPairConfirmed())
            {
                return HspWriteDecision.Drop("the start-up pair is not confirmed for today (Device:Usb:StartupPairDate, SC-4 item 6)");
            }

            var fresh = _lastReportStamp is { } stamp && _clock.GetElapsedTime(stamp) <= 2 * _options.PollInterval;
            if (!fresh)
            {
                return HspWriteDecision.Hold("the console's last report is older than two poll periods (SC-2 condition 5, as for every write)");
            }

            if (session.IsListedAndUnfetched(HspObjectId.LedStatus) || session.IsListedAndUnfetched(HspObjectId.ConsoleStatus))
            {
                return HspWriteDecision.Hold("the console listed its key LEDs or its fader mode and they are not fetched yet");
            }

            return PairStateRefusal() is { } refusal
                ? HspWriteDecision.Drop($"{refusal} (SC-4 clause 3)")
                : HspWriteDecision.Send;
        }
    }

    /// <summary>
    /// SC-4 clause 3: the pair is refused when what the console already reported in this session (a list pending at Connect,
    /// F177 / H65, or anything since) shows a state no pair was ever seen in (F185): NEXT mode on or unknown (ConsoleStatus
    /// body[6], F173), bank 25-48 (body[4], F98, in either mode), a memory page other than 1 (body[2] = page − 1, F113,
    /// VERIFIED for the structure; in either mode: every one of the 20 pairs came on page 1, F185, OBSERVED; added 2026-11-03
    /// after the first review's MAJOR 1), a
    /// ConsoleStatus value never observed, a record function armed (REC MEM, EDIT MEM, COPY, REC SEQ not steady off; the
    /// record-key LEDs, H72), or BLACKOUT on (LED block 53 lit and steady, F114; its blink is the MASTER below full, F85, seen
    /// at E2's pair). Null when nothing reported shows one of those, also when nothing was reported at all: the pair then
    /// goes blind, as SmartSoft sends it. Asked for `07` too.
    /// </summary>
    private string? PairStateRefusal()
    {
        if (_assembler.ConsoleStatusReported)
        {
            switch (_assembler.FaderMode)
            {
                case null:
                    return "the console reported a fader mode (ConsoleStatus) with a value never observed: no pair is sent in a state no capture shows (F185, H114)";
                case { NextMode: true }:
                    return "NEXT mode is on (ConsoleStatus body[6], F173): no pair was ever seen in NEXT mode (F185)";
                case { NextMode: null }:
                    return "ConsoleStatus body[6] carries a value never observed (NEXT mode unknown, F173): no pair is sent then";
                case { Bank25To48: true }:
                    return "the console shows bank 25-48, or memory mode entered from it (ConsoleStatus body[4] = 1, F98): no pair was ever seen there (F185)";
                case { MemoryPage: not 1 } paged:
                    return $"the console shows memory page {paged.MemoryPage} (ConsoleStatus body[2] = {paged.MemoryPage - 1}, F113): no pair was ever seen on a page other than 1 (F185)";
            }
        }

        if (_assembler.Keys is { } keys)
        {
            if (RecordFunctionArmed(keys) is { } armed)
            {
                return $"{armed}: no pair was ever seen with a record function armed (F185)";
            }

            if (keys.Blackout is { Blinking: false, Light: not KeyLight.Off })
            {
                return "BLACKOUT is on (LED block 53 lit and steady, F114): no pair was ever seen during a blackout (F185)";
            }
        }

        return null;
    }

    /// <summary>
    /// Why a control is refused while the start-up pair is under way (SC-4 clauses 1, 2 and 6: the pair is first or not at all,
    /// nothing goes between its two writes nor during the download) or while the MASTER probe runs (clause 4 as amended on
    /// 2026-11-02, late evening: its switches and its tap are the only writes until it has left the console in the bank or
    /// mode the download reported, so no control can start a switch of its own or wait for one of the probe's), else null.
    /// Under <c>_gate</c>.
    /// </summary>
    private string? StartupPairBusy(HspReadSession session) => session.StartupPair.Phase switch
    {
        HspStartupPairPhase.Waiting => "the console's state at Connect is being requested: SmartSoft's start-up pair goes first (SC-4 clause 1)",
        HspStartupPairPhase.VersionRequested or HspStartupPairPhase.VersionAccepted => "the console's state at Connect is being requested: nothing goes between the start-up pair's two writes (SC-4 clause 1)",
        HspStartupPairPhase.DownloadRequested or HspStartupPairPhase.Downloading => "the console's state is being downloaded (SC-4 clause 2)",
        _ => _probeRun is { } run
            ? $"the MASTER probe is under way (SC-4 clause 4): the device taps the bump of channel {run.Bump.Target.Channel} in {Describe(run.Bump.Target.Bank)} and leaves the console in {Describe(run.Home)}, as the download reported it, before anything else goes"
            : null,
    };

    /// <summary>
    /// True when live control's switch and hardware mode both name today (SC-2 item 6: the switch expires by itself) and
    /// the timings are within SmartSoft's cadence (<see cref="UsbDeviceOptions.LiveControlTimingProblem"/>; the start-up
    /// check refuses such settings, this keeps a device built any other way read-only).
    /// </summary>
    private bool IsLiveControlConfirmed()
    {
        if (string.IsNullOrWhiteSpace(_options.LiveControlDate) || _options.LiveControlTimingProblem() is not null)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        return ServiceCollectionExtensions.IsConfirmedForToday(_options.LiveControlDate, today)
            && ServiceCollectionExtensions.IsConfirmedForToday(_options.ConfirmDate, today);
    }

    /// <summary>
    /// Why Connect is refused after a Data write that did not complete: this process's latch (until the backend
    /// restarts), else the marker on disk (until the owner removes it); a marker that cannot be read refuses too. Null
    /// when nothing stands in the way.
    /// </summary>
    private string? IncompleteWriteRefusal()
    {
        const string Rule = "SC-1's E8 rule: never power-cycle the console to \"fix\" it";
        if (_incompleteWrite is { } incomplete)
        {
            var file = _marker is null ? string.Empty : $" and {_marker.FilePath} is removed";
            return $"{incomplete}. Read the transfer log before anything else; nothing connects again until the backend restarts{file} ({Rule}).";
        }

        if (_marker is null)
        {
            return null;
        }

        try
        {
            return _marker.Read() is { } text
                ? $"{_marker.FilePath} records a Data write that did not complete: {text.Trim()}. Read the transfer log of that session before anything else; nothing connects while the file exists, and only the owner removes it ({Rule})."
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"The incomplete-write marker {_marker.FilePath} could not be read ({ex.Message}); nothing connects until it can be ({Rule}).";
        }
    }

    /// <summary>The running session with live control; read-only's refusal when live control is off, a refusal when no such session runs.</summary>
    private HspReadSession LiveSession()
    {
        if (!IsLiveControlConfirmed())
        {
            throw ReadOnly();
        }

        lock (_gate)
        {
            if (_status != ConnectionStatus.Connected || _liveSession is not { } session)
            {
                throw new InvalidOperationException("Refused: no live-control session is running (connect first; a session that started read-only stays read-only).");
            }

            return session;
        }
    }

    /// <summary>Queues a MASTER or BUMPS event behind the interlocks: refused now with the reason, else asked again at the moment it would go.</summary>
    private Task QueueLiveFader(HspReadSession session, ClearedFader fader, byte level, Func<string?> state, bool holdWhileFaderInfoListed = false)
    {
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported() ?? SwitchInProgress() ?? state());
            session.QueueFader(fader, level, () => Recheck(session, state, holdWhileFaderInfoListed));
        }

        return Task.CompletedTask;
    }

    /// <summary>Queues a tap behind the interlocks, as <see cref="QueueLiveFader"/>.</summary>
    private Task QueueLiveTap(HspReadSession session, ClearedKey key, Func<string?> state)
    {
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? state());
            session.QueueKeyTap(key, () => Recheck(session, state, holdWhileFaderInfoListed: false));
        }

        return Task.CompletedTask;
    }

    /// <summary>The session may have ended between <see cref="LiveSession"/> and the queueing lock: then nothing is queued.</summary>
    private void RequireRunning(HspReadSession session)
    {
        if (!ReferenceEquals(_liveSession, session))
        {
            throw new InvalidOperationException("Refused: the live-control session has ended.");
        }
    }

    /// <summary>
    /// A queued write's own condition, asked on the session's thread at the moment it would go (after the gate, right
    /// before the header): the console state its control needs, as it was required when the write was accepted. A
    /// change since (a mode, page or LED changed on the panel) drops it unsent; FaderInfo listed and not fetched holds
    /// a fader that needs the MASTER at full. It changes nothing: what follows a send or a drop is the session's to report.
    /// </summary>
    private HspWriteDecision Recheck(HspReadSession session, Func<string?> state, bool holdWhileFaderInfoListed)
    {
        if (holdWhileFaderInfoListed && session.IsListedAndUnfetched(HspObjectId.FaderInfo))
        {
            return HspWriteDecision.Hold("the console listed FaderInfo (the MASTER) and it is not fetched yet");
        }

        lock (_gate)
        {
            var reason = ReferenceEquals(_liveSession, session) ? state() : "the live-control session is ending";
            return reason is null ? HspWriteDecision.Send : HspWriteDecision.Drop(reason);
        }
    }

    /// <summary>
    /// What a fader or bump command addresses: the bank it needs, the memory page (memory controls only), the physical fader
    /// or bump 1–24 under it, a name for the reasons, and the channel (channel controls only).
    /// </summary>
    private sealed record Target(FaderBank Bank, int? Page, int Physical, string Name, int? Channel);

    /// <summary>The UI's latest level for a physical fader whose matching event (or whose bank switch) has not gone yet.</summary>
    private sealed record PendingLevel(Target Target, byte Level);

    /// <summary>A bump the UI holds (or waits to hold behind a switch): its control name, its key, what it was pressed for.</summary>
    private sealed record HeldBump(string Control, ClearedKey Key, Target Target);

    /// <summary>
    /// A bump this session pressed (SC-5 class B3; the first review of SC-5 item 10, major 1; the end rule the owner asked for on
    /// 2026-11-02): from its press the reports of its physical fader may carry the bump's level, not the content's, so a fader
    /// event of that physical fader, in any bank, is refused or dropped until <see cref="ShadowEnds"/>. A held bump reads the
    /// BUMPS level in FaderInfo record k byte 0 (`ff 00 00 00` channel, `ff ff 00 00` memory) and ChannelLevels[c]: VERIFIED at
    /// BUMPS 0xFF (F97, F174 in 16 of 16 channel-bump and 51 of 51 memory-bump holds, F178: no object marks a press); at 0x81
    /// once, in memory mode (F178); below full in channel mode never captured, so there the reading is INFERRED. After a
    /// channel bump's release the first poll reply named neither object in 9 of 16 holds (E9c header seqs 29262, 86165, 87996;
    /// E9d 27254; E9e 17655, 18352, 18882, 28382, 28803); a short tap's press may be reported after its release (F168); a bump
    /// on a fader whose content was already at the bump's level listed nothing at the press nor at the release (the owner's
    /// later E11b session, `2026-11-02-e11b-second-live-control-session.backend-6.jsonl` seq 4571 / 4609, OBSERVED once,
    /// log-only), where the first rule (both objects listed and delivered after the release) never ended and kept channel fader
    /// 5 refused; the owner rejected that. Where the reports cannot tell the bump from the content (F178: a held bump reads as
    /// its fader at that level), the shadow still fails closed: a bump held on the panel at the same time keeps the bump's
    /// level in the reports after our release (the owner's acceptance for E11b); and a content that reaches the bump's level
    /// during our hold, or just before the press and not yet reported (narrowed by the press's wait, the fourth review's minor
    /// H), reads as the bump after the release; either way the fader waits until it is moved. With SOLO on the shadow covers
    /// every physical fader (<see cref="AllFaders"/>, the re-check of item 10, minor C). Read and changed under <c>_gate</c> only.
    /// </summary>
    private sealed class BumpShadow
    {
        public required ClearedKey Key { get; init; }

        /// <summary>What the bump was pressed for: its physical fader, its name, its channel (a channel bump) or page (a memory bump).</summary>
        public required Target Target { get; init; }

        public int Physical => Target.Physical;

        public string Name => Target.Name;

        /// <summary>The channel of a channel bump, whose level is in ChannelLevels too; null for a memory bump.</summary>
        public int? Channel => Target.Channel;

        public bool ChannelsToo => Channel is not null;

        /// <summary>
        /// SOLO's LED (block 52) read on, blinking or unknown at the press or while the shadow lasted: a bump with SOLO on
        /// replaces the output (F6, DOCUMENTED: the other channels are muted, memory bumps replace the live output), so every
        /// physical fader's reports may carry it, ChannelLevels and perhaps FaderInfo, as BLACKOUT (F125) and the MASTER (F176)
        /// zero FaderInfo too. INFERRED from those, unobserved: E9c's only SOLO-mode hold (header 87996) had nothing else up.
        /// Every physical fader then waits as the bump's own does, and also until both objects were quiet for two poll replies.
        /// </summary>
        public bool AllFaders { get; set; }

        /// <summary>The press was taken for sending (until then the fader's reports are its own).</summary>
        public bool PressSent { get; set; }

        /// <summary>FaderInfo record k byte 0 as reported when the press was taken: the content's own level.</summary>
        public byte? PreFader { get; set; }

        /// <summary>ChannelLevels[c] as reported when the press was taken (a channel bump, once ChannelLevels was reported).</summary>
        public byte? PreChannel { get; set; }

        /// <summary>A poll reply came after the release was taken for sending.</summary>
        public bool Released { get; set; }

        /// <summary>A list after the release named FaderInfo.</summary>
        public bool FaderInfoListed { get; set; }

        /// <summary>A list after the release named ChannelLevels.</summary>
        public bool ChannelLevelsListed { get; set; }

        /// <summary>Poll replies after the release since the last one that listed FaderInfo (idle, or listing other objects only).</summary>
        public int FaderInfoQuiet { get; set; }

        /// <summary>Poll replies after the release since the last one that listed ChannelLevels.</summary>
        public int ChannelLevelsQuiet { get; set; }
    }

    /// <summary>
    /// When <paramref name="shadow"/> ends (the rule of 2026-11-02): its release has been taken and a poll reply came after it,
    /// and the console's latest report of physical fader k in FaderInfo (and, for a channel bump, of channel c in
    /// ChannelLevels) is the content's again, that is (a) reported after the release (listed after it, and delivered) or (b)
    /// unchanged for two poll replies after the release (idle, or a list naming other objects only: a list names what changed,
    /// F57, VERIFIED), and reading a level other than the bump's (BUMPS, FaderInfo body[193]: a held bump reads it, VERIFIED at
    /// BUMPS 0xFF, F97, F174, F178; INFERRED below full in channel mode) or the level reported when the press was taken (the
    /// release restored it). A report at the bump's level that differs from the pre-press level keeps the shadow: a tap's press
    /// reported after its release (F168). Case (b) is the bump that changed nothing, the content already at the bump's level:
    /// nothing listed at the press nor at the release (the owner's log); every observed release that changed a level was
    /// reported with at most one quiet poll reply before it (SmartSoft's E9c 7, E9d 52, E9e 8 releases; ours E11 attempt 3 4,
    /// E11b 3), so two quiet replies leave a margin of one. With SOLO on both objects must also
    /// have been quiet for two poll replies, so that the other faders' reports are no longer the bump's either (INFERRED).
    /// Nothing listed and not yet fetched counts. The rule is INFERRED; its readings carry the statuses given.
    /// </summary>
    private bool ShadowEnds(BumpShadow shadow, HspReadSession live)
    {
        if (!shadow.Released || _assembler.FaderInfo is not { } info)
        {
            return false;
        }

        var bump = info.Bumps;
        bool Content(byte now, byte? before) => now != bump || now == before;
        bool Reported(HspObjectId id, bool listed, int quiet) => ReportedAfterRelease(live, id, listed, quiet);

        if (!Reported(HspObjectId.FaderInfo, shadow.FaderInfoListed, shadow.FaderInfoQuiet)
            || !Content(info.RawRecords[(shadow.Physical - 1) * FaderInfoObject.RecordSize], shadow.PreFader))
        {
            return false;
        }

        if (shadow.Channel is { } channel
            && (!Reported(HspObjectId.ChannelLevels, shadow.ChannelLevelsListed, shadow.ChannelLevelsQuiet)
                || !Content(_assembler.ChannelLevels[channel - 1], shadow.PreChannel)))
        {
            return false;
        }

        return !shadow.AllFaders
            || (shadow.FaderInfoQuiet >= 2 && shadow.ChannelLevelsQuiet >= 2 && !live.IsListedAndUnfetched(HspObjectId.ChannelLevels));
    }

    /// <summary>
    /// An object's report after a bump's release, as the shadow counts it (<see cref="ShadowEnds"/>): listed after the release
    /// and delivered, or unchanged for two poll replies after it (a list names what changed, F57, VERIFIED; every observed
    /// release that changed a level was reported with at most one quiet poll reply before it), and nothing of it listed and
    /// unfetched now. The two-reply count is INFERRED, as in the shadow's rule.
    /// </summary>
    private static bool ReportedAfterRelease(HspReadSession live, HspObjectId id, bool listed, int quiet) =>
        !live.IsListedAndUnfetched(id) && (listed || quiet >= 2);

    /// <summary>
    /// Why a fader event of physical fader k waits for the console: a bump of that physical fader that this session pressed,
    /// or any bump of ours held with SOLO on (<see cref="BumpShadow.AllFaders"/>), whose shadow has not ended
    /// (<see cref="ShadowEnds"/>); null otherwise.
    /// </summary>
    private string? Shadowed(Target target)
    {
        var own = _bumpShadows.FirstOrDefault(s => s.PressSent && s.Physical == target.Physical);
        if ((own ?? _bumpShadows.FirstOrDefault(s => s.PressSent && s.AllFaders)) is not { } shadow)
        {
            return null;
        }

        var objects = shadow.ChannelsToo ? "FaderInfo and ChannelLevels" : "FaderInfo";
        if (own is null)
        {
            return shadow.Released
                ? $"{shadow.Name} was released with SOLO on and the console's reports have not settled since (FaderInfo and ChannelLevels quiet for two poll replies after the release): until then every fader's reports may carry the bump's output, not {target.Name}'s own level (F6; INFERRED, unobserved)"
                : $"{shadow.Name} is held with SOLO on: the bump replaces the output (F6), so every fader's reports may carry it, not {target.Name}'s own level, until they have settled after the release (INFERRED, unobserved)";
        }

        var bump = _assembler.FaderInfo is { } info ? $"0x{info.Bumps:X2}" : "unknown";
        return shadow.Released
            ? $"{shadow.Name} was released and the console's reports of physical fader {shadow.Physical} are not its content's again yet ({objects} after the release at a level other than the bump's, {bump}, or at the level before the press, or unchanged for two poll replies): until then they may carry the bump's level, not {target.Name}'s (F174)"
            : $"{shadow.Name} is held: physical fader {shadow.Physical}'s reports carry the bump's level, not {target.Name}'s, until they read its content's level again after the release (F174)";
    }

    /// <summary>The console state a channel or memory fader event needs, at its command and again right before it goes.</summary>
    private string? FaderState(Target target) => NotIn(target) ?? MasterNotAtFull(target.Name) ?? BlackoutNotOff(target.Name) ?? Shadowed(target);

    /// <summary>The automatic bank switch in progress (SC-5 condition 1), one at a time; read and changed under <c>_gate</c> only.</summary>
    private sealed class BankSwitch
    {
        public required FaderBank Target { get; init; }

        public required int? Page { get; init; }

        public required ClearedKey Key { get; init; }

        public required FaderBank From { get; init; }

        /// <summary>The control that asked for it, for the log and the reasons.</summary>
        public required string Reason { get; init; }

        /// <summary>True for the automatic switch a control started; false for a FADERS key pressed in the UI.</summary>
        public bool Automatic { get; init; } = true;

        /// <summary>"automatic bank switch" or "bank switch", for the log and the reasons.</summary>
        public string Kind => Automatic ? "automatic bank switch" : "bank switch";

        /// <summary>The clock stamp at which the key's press was taken for sending; null before.</summary>
        public long? KeySent { get; set; }

        /// <summary>A LedStatus arrived after the key's press (condition 1 c: "and the LEDs arrived").</summary>
        public bool LedsSinceKey { get; set; }

        /// <summary>A bump press that waits for the switch.</summary>
        public HeldBump? Press { get; set; }

        /// <summary>The UI let go of <see cref="Press"/> before the switch completed: it goes as a tap.</summary>
        public bool PressReleased { get; set; }
    }

    /// <summary>
    /// Channel c's bank and physical fader, one explicit row per channel (the first review of SC-5 item 10, minor 3): each pair
    /// observed in both E9d passes (F171, VERIFIED): channel N of bank 1-24 moved by index N − 1 (part A, also E9c S1), channel N
    /// of bank 25-48 by the fader labelled N, index N − 25 (part B; 25 and 26 also E9c S2). The physical number then names the
    /// whitelist member through <see cref="PhysicalControls"/>' table; nothing here is computed. Internal for the golden test
    /// that checks every row against E9d.
    /// </summary>
    internal static (FaderBank Bank, int Physical) ChannelPosition(int channel) => channel switch
    {
        1 => (FaderBank.Channels1To24, 1),
        2 => (FaderBank.Channels1To24, 2),
        3 => (FaderBank.Channels1To24, 3),
        4 => (FaderBank.Channels1To24, 4),
        5 => (FaderBank.Channels1To24, 5),
        6 => (FaderBank.Channels1To24, 6),
        7 => (FaderBank.Channels1To24, 7),
        8 => (FaderBank.Channels1To24, 8),
        9 => (FaderBank.Channels1To24, 9),
        10 => (FaderBank.Channels1To24, 10),
        11 => (FaderBank.Channels1To24, 11),
        12 => (FaderBank.Channels1To24, 12),
        13 => (FaderBank.Channels1To24, 13),
        14 => (FaderBank.Channels1To24, 14),
        15 => (FaderBank.Channels1To24, 15),
        16 => (FaderBank.Channels1To24, 16),
        17 => (FaderBank.Channels1To24, 17),
        18 => (FaderBank.Channels1To24, 18),
        19 => (FaderBank.Channels1To24, 19),
        20 => (FaderBank.Channels1To24, 20),
        21 => (FaderBank.Channels1To24, 21),
        22 => (FaderBank.Channels1To24, 22),
        23 => (FaderBank.Channels1To24, 23),
        24 => (FaderBank.Channels1To24, 24),
        25 => (FaderBank.Channels25To48, 1),
        26 => (FaderBank.Channels25To48, 2),
        27 => (FaderBank.Channels25To48, 3),
        28 => (FaderBank.Channels25To48, 4),
        29 => (FaderBank.Channels25To48, 5),
        30 => (FaderBank.Channels25To48, 6),
        31 => (FaderBank.Channels25To48, 7),
        32 => (FaderBank.Channels25To48, 8),
        33 => (FaderBank.Channels25To48, 9),
        34 => (FaderBank.Channels25To48, 10),
        35 => (FaderBank.Channels25To48, 11),
        36 => (FaderBank.Channels25To48, 12),
        37 => (FaderBank.Channels25To48, 13),
        38 => (FaderBank.Channels25To48, 14),
        39 => (FaderBank.Channels25To48, 15),
        40 => (FaderBank.Channels25To48, 16),
        41 => (FaderBank.Channels25To48, 17),
        42 => (FaderBank.Channels25To48, 18),
        43 => (FaderBank.Channels25To48, 19),
        44 => (FaderBank.Channels25To48, 20),
        45 => (FaderBank.Channels25To48, 21),
        46 => (FaderBank.Channels25To48, 22),
        47 => (FaderBank.Channels25To48, 23),
        48 => (FaderBank.Channels25To48, 24),
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "The 1248 has channels 1-48."),
    };

    /// <summary>
    /// What the physical faders control as the console last reported it, among the three states SmartSoft sent controls in
    /// (E9 to E9e): channel mode with bank 1-24 or 25-48, memory mode with the bank flag off. Null when unreported, while
    /// NEXT mode is on or unknown (F173: fader events then set blind levels, seen from a PC once; no other control was sent
    /// then), or in memory mode entered from bank 25-48 (the flag kept, F98; no PC control was seen there).
    /// </summary>
    private FaderBank? ReportedBank() => _assembler.FaderMode switch
    {
        null or { NextMode: not false } => null,
        { MemoryMode: false, Bank25To48: false } => FaderBank.Channels1To24,
        { MemoryMode: false, Bank25To48: true } => FaderBank.Channels25To48,
        { MemoryMode: true, Bank25To48: false } => FaderBank.Memories,
        _ => null,
    };

    /// <summary>Why <see cref="ReportedBank"/> is null, for a refusal.</summary>
    private string WhyNoBank(string name) => _assembler.FaderMode switch
    {
        null => $"the console has not reported its fader mode yet (ConsoleStatus; a fader-mode key pressed on the panel reports it): {name} needs it",
        { NextMode: true } => $"NEXT mode is on (ConsoleStatus body[6], F173): fader events then set blind levels, seen from a PC once; {name} is not sent",
        { NextMode: null } => $"ConsoleStatus body[6] carries a value never observed (NEXT mode unknown, F173): {name} is not sent",
        _ => $"the console is in memory mode entered from bank 25-48 (the bank flag kept, F98), where no PC control was seen: {name} is not sent",
    };

    private static string Describe(FaderBank bank) => bank switch
    {
        FaderBank.Channels1To24 => "channel mode, bank 1-24",
        FaderBank.Channels25To48 => "channel mode, bank 25-48",
        _ => "memory mode",
    };

    private static string KeyName(ClearedKey key) => key switch
    {
        ClearedKey.Faders1To24 => "FADERS 1-24",
        ClearedKey.Faders25To48 => "FADERS 25-48",
        ClearedKey.FadersMems => "FADERS MEMS",
        _ => key.ToString(),
    };

    /// <summary>The bank key that makes the physical faders control <paramref name="bank"/>.</summary>
    private static ClearedKey BankKey(FaderBank bank) => bank switch
    {
        FaderBank.Channels1To24 => ClearedKey.Faders1To24,
        FaderBank.Channels25To48 => ClearedKey.Faders25To48,
        _ => ClearedKey.FadersMems,
    };

    /// <summary>
    /// The bank keys only from the banks SmartSoft sent them from (E9 to E9e, OBSERVED): FADERS 1-24 from memory mode or bank
    /// 25-48; FADERS 25-48 and FADERS MEMS from bank 1-24 (FADERS 25-48 never from memory mode). Null when the key may go.
    /// </summary>
    private static string? BankKeyNotFrom(ClearedKey key, FaderBank from) => (key, from) switch
    {
        (ClearedKey.Faders1To24, FaderBank.Memories or FaderBank.Channels25To48) => null,
        (ClearedKey.Faders25To48 or ClearedKey.FadersMems, FaderBank.Channels1To24) => null,
        _ => $"the console shows {Describe(from)}: {KeyName(key)} was sent from a PC only from {(key == ClearedKey.Faders1To24 ? "memory mode or bank 25-48" : "channel mode, bank 1-24")} (E9 to E9e)",
    };

    private string? SwitchInProgress() =>
        _switch is { } sw ? $"{(sw.Automatic ? "an" : "a")} {sw.Kind} to {Describe(sw.Target)} is in progress for {sw.Reason}" : null;

    /// <summary>
    /// The state a fader or bump target needs, at its command and again right before its write goes: the console reports the
    /// target's bank (<see cref="ReportedBank"/>); a channel's physical fader holds no memory level (FaderInfo record k byte 1
    /// is 0 in channel mode in every logged sample, F112; a memory fader left up across FADERS 1-24 was seen once and nothing
    /// was sent in that state); a memory target is on the page the console shows (F113: no page change is sent) with physical
    /// fader k holding that page's memory (FaderInfo record k byte 3 = page − 1, H69 CONFIRMED: a fader raised across a page
    /// change keeps its old page's memory until it is brought to zero). Null when it does; else why not.
    /// </summary>
    private string? NotIn(Target target)
    {
        if (ReportedBank() is not { } bank)
        {
            return WhyNoBank(target.Name);
        }

        if (bank != target.Bank)
        {
            return $"the console shows {Describe(bank)}: {target.Name} needs {Describe(target.Bank)}";
        }

        var record = (target.Physical - 1) * FaderInfoObject.RecordSize;
        if (target.Page is not { } page)
        {
            return _assembler.FaderInfo is { } channelInfo && channelInfo.RawRecords[record + 1] != 0
                ? $"physical fader {target.Physical} still holds a memory at 0x{channelInfo.RawRecords[record + 1]:X2} (FaderInfo record {target.Physical} byte 1, F112): {target.Name} is not sent until it is brought to zero"
                : null;
        }

        if (_assembler.FaderMode!.MemoryPage != page)
        {
            return $"the console shows memory page {_assembler.FaderMode.MemoryPage}, not {page}: no page change is sent from the PC (F113)";
        }

        return _assembler.FaderInfo is { } info && info.RawRecords[record + 3] + 1 is var held && held != page
            ? $"physical fader {target.Physical} still holds memory {target.Physical} of page {held} (FaderInfo record {target.Physical} byte 3, H69): bring it to zero on the console first"
            : null;
    }

    /// <summary>
    /// SC-5 condition 1 (d): the level a re-assigned physical fader is matched at, the target's reported level (F172: case (a),
    /// a mode switch, VERIFIED in part; (b) and (c) OBSERVED once; a 1-24 / 25-48 switch in none of its cases, so INFERRED).
    /// A channel: ChannelLevels[c] (which channel F171, VERIFIED), sent only when FaderInfo record k byte 0 reads the same.
    /// Record k byte 0 is the physical fader's post-MASTER output (F82, F96: OBSERVED; with the MASTER at full, required, the
    /// fader's own unless something else is added to it). The two agreeing is read as "no other source drives the channel": that
    /// holds for a memory up on another fader and for the stack (INFERRED: they reach ChannelLevels, not that fader's record) and
    /// is <b>contradicted for a held bump</b> (F174, VERIFIED: during a hold both read the bump's level, record k `ff 00 00 00`
    /// and ChannelLevels[c] 0xFF), so a bump this session pressed keeps its fader out of here until its reports read the
    /// content again (<see cref="Shadowed"/>); a bump held on the panel cannot be told apart (the owner's acceptance for E11b).
    /// A memory fader: FaderInfo record k bytes 0 and 1, equal (F112: equal in all 26 memory-mode samples; H71), with the
    /// same caveat for a held memory bump (`ff ff 00 00`, F174). Null with the reason when the level is not known or not one.
    /// </summary>
    private (byte? Level, string? Reason) MatchingLevel(Target target)
    {
        if (_assembler.FaderInfo is not { } info)
        {
            return (null, "the console has not reported its faders yet (FaderInfo)");
        }

        var record = (target.Physical - 1) * FaderInfoObject.RecordSize;
        var fader = info.RawRecords[record];
        if (target.Channel is { } channel)
        {
            if (!_assembler.ChannelLevelsReported)
            {
                return (null, $"the console has not reported its channel levels in this session (ChannelLevels): {target.Name}'s first event carries channel {channel}'s reported level (SC-5 condition 1 d); a channel fader or bump moved on the panel makes the console report them");
            }

            var level = _assembler.ChannelLevels[channel - 1];
            return level == fader
                ? (level, null)
                : (null, $"channel {channel} reads 0x{level:X2} (ChannelLevels) and physical fader {target.Physical} 0x{fader:X2} (FaderInfo record {target.Physical}): another source drives the channel, so the level the console matches is not known (SC-5 condition 1 d, F172)");
        }

        var second = info.RawRecords[record + 1];
        return fader == second
            ? (fader, null)
            : (null, $"memory fader {target.Physical}'s record reads 0x{fader:X2} / 0x{second:X2} (bytes 0 and 1 differ, H71): the level the console matches is not known (SC-5 condition 1 d)");
    }

    /// <summary>
    /// A channel or memory fader event (SC-5 classes A2–A4) behind the interlocks: in the bank the console shows, at once
    /// when the fader is matched, else its matching event first (<see cref="QueueMatching"/>); in another bank, the
    /// automatic switch first (<see cref="StartSwitch"/>). Until the matching event has gone, the UI's level waits here
    /// (the newest wins), so it can never replace the matching event in the session's queue.
    /// </summary>
    private Task SetFaderLevel(HspReadSession session, Target target, byte level)
    {
        var fader = PhysicalControls.Fader(target.Physical);
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported() ?? MasterNotAtFull(target.Name) ?? BlackoutNotOff(target.Name) ?? Shadowed(target));
            if (_switch is { } pending)
            {
                Refuse(pending.Target == target.Bank && pending.Page == target.Page ? null : SwitchInProgress());
                _pendingLevels[fader] = new PendingLevel(target, level);   // matched once the console reports the bank (CompleteSwitch)
                return Task.CompletedTask;
            }

            if (ReportedBank() != target.Bank)
            {
                StartSwitch(session, target);
                _pendingLevels[fader] = new PendingLevel(target, level);
                return Task.CompletedTask;
            }

            Refuse(NotIn(target));
            if (_matched.Contains(fader))
            {
                session.QueueFader(fader, level, () => Recheck(session, () => FaderState(target), holdWhileFaderInfoListed: true));
                return Task.CompletedTask;
            }

            // a matching event for this target still queued: the UI's newer level just waits behind it; otherwise (none yet,
            // another target's, or one dropped and not yet forgotten) a new matching event, which replaces any event still
            // queued for that physical fader
            if (!_pendingLevels.TryGetValue(fader, out var waiting) || waiting.Target != target || !session.IsFaderQueued(fader))
            {
                var (matching, reason) = MatchingLevel(target);
                Refuse(reason);
                QueueMatching(session, fader, target, matching!.Value);
            }

            _pendingLevels[fader] = new PendingLevel(target, level);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Queues the matching event (SC-5 condition 1 d): the target's reported level, asked again right before it goes; once it
    /// is taken for sending, the fader counts as matched and the UI's latest level follows it (<see cref="MatchingSent"/>).
    /// </summary>
    private void QueueMatching(HspReadSession session, ClearedFader fader, Target target, byte level) =>
        session.QueueFader(fader, level, () => RecheckMatching(session, target, level), sent: () => MatchingSent(session, fader, target, level));

    /// <summary>The matching event's own condition: held while the console has listed the objects its level comes from; dropped when the state or the reported level changed since it was queued.</summary>
    private HspWriteDecision RecheckMatching(HspReadSession session, Target target, byte level)
    {
        if (session.IsListedAndUnfetched(HspObjectId.FaderInfo) || session.IsListedAndUnfetched(HspObjectId.ChannelLevels))
        {
            return HspWriteDecision.Hold("the console listed FaderInfo or ChannelLevels (the level a re-assigned fader is matched at) and it is not fetched yet");
        }

        lock (_gate)
        {
            var reason = ReferenceEquals(_liveSession, session) ? FaderState(target) : "the live-control session is ending";
            if (reason is null)
            {
                var (now, why) = MatchingLevel(target);
                reason = why ?? (now == level ? null : $"{target.Name}'s reported level changed from 0x{level:X2} to 0x{now:X2} since its matching event was queued; the next drag matches it again");
            }

            return reason is null ? HspWriteDecision.Send : HspWriteDecision.Drop(reason);
        }
    }

    /// <summary>
    /// The matching event was taken for sending (on the session's thread, between the gate and the header): the fader counts
    /// as matched, and the UI's latest level, when it differs, is queued behind it ("then the drag", SC-5 condition 1 d).
    /// State only (the queue is memory); the note is written at the next event. Never throws: the write is already committed.
    /// </summary>
    private void MatchingSent(HspReadSession session, ClearedFader fader, Target target, byte level)
    {
        lock (_gate)
        {
            _matched.Add(fader);
            var note = $"matched {target.Name} at its reported level 0x{level:X2} (SC-5 condition 1 d, F172)";
            if (_pendingLevels.TryGetValue(fader, out var wish) && wish.Target == target)
            {
                _pendingLevels.Remove(fader);
                if (wish.Level != level)
                {
                    try
                    {
                        session.QueueFader(fader, wish.Level, () => Recheck(session, () => FaderState(target), holdWhileFaderInfoListed: true));
                    }
                    catch (InvalidOperationException ex)
                    {
                        note += $"; the UI's level 0x{wish.Level:X2} was not queued ({ex.Message})";
                    }
                }
            }

            LeaveNote(note);
        }
    }

    /// <summary>
    /// SC-5 condition 1 (a, b): starts the automatic switch to the target's bank, or refuses with the reason (a record
    /// function armed is the caller's check): a bump held (a hold does not span a bank switch, condition 2), the console's
    /// bank unknown, a bank key SmartSoft never sent from the bank shown (<see cref="BankKeyNotFrom"/>), a memory page other
    /// than the console's (no page change is sent, F113). The bank key goes as a tap; the switch then waits for the console's
    /// report (<see cref="AdvanceSwitch"/>), and the controls that asked for it wait with it.
    /// </summary>
    private BankSwitch StartSwitch(HspReadSession session, Target target)
    {
        var from = ReportedBank();
        Refuse(from is null ? WhyNoBank(target.Name) : null);
        var key = BankKey(target.Bank);
        Refuse(BankKeyNotFrom(key, from!.Value)
            ?? (session.HoldInProgress ? $"a bump is held: the bank switch for {target.Name} waits for its release (a hold does not span a bank switch, SC-5 condition 2)" : null)
            ?? (target.Page is { } page && _assembler.FaderMode!.MemoryPage != page ? $"the console's memory page is {_assembler.FaderMode.MemoryPage}, not {page}: no page change is sent from the PC (F113)" : null));
        var sw = new BankSwitch { Target = target.Bank, Page = target.Page, Key = key, From = from.Value, Reason = target.Name };
        session.QueueKeyTap(key, () => Recheck(session, () => SwitchKeyState(sw), holdWhileFaderInfoListed: false), sent: () => SwitchKeySent(sw));
        _switch = sw;
        return sw;
    }

    /// <summary>The switch key's own condition: still the switch in progress, and the console still shows the bank the key was chosen for.</summary>
    private string? SwitchKeyState(BankSwitch sw) =>
        !ReferenceEquals(_switch, sw) ? "the automatic bank switch was abandoned"
        : ReportedBank() == sw.From ? null
        : $"the console no longer shows {Describe(sw.From)}: {KeyName(sw.Key)} for {sw.Reason} is not sent";

    /// <summary>
    /// The switch key's press was taken for sending (the session's thread, between the gate and the header): its report is
    /// now awaited, within <see cref="BankSwitchReportBound"/>. State only; the note is written at the next event.
    /// </summary>
    private void SwitchKeySent(BankSwitch sw)
    {
        lock (_gate)
        {
            sw.KeySent = _clock.GetTimestamp();
            LeaveNote($"{sw.Kind} (SC-5 condition 1) for {sw.Reason}: {KeyName(sw.Key)} sent, from {Describe(sw.From)} to {Describe(sw.Target)}; nothing else goes until the console reports it");
        }
    }

    /// <summary>
    /// SC-5 condition 1 (c), asked on the session's thread after each completed exchange: once ConsoleStatus reports the
    /// switch's bank, a LedStatus arrived after its key and nothing that reports the faders or the keys is listed and
    /// unfetched, the switch is complete (<see cref="CompleteSwitch"/>); past <see cref="BankSwitchReportBound"/> after the key
    /// without that, the session ends as a deviation (a fault: nothing is retried, an owed release still goes).
    /// </summary>
    private void AdvanceSwitch()
    {
        HspReadSession session;
        BankSwitch? done = null;
        string? deviation = null;
        lock (_gate)
        {
            if (_liveSession is not { } live || _switch is not { KeySent: { } sent } sw)
            {
                return;
            }

            session = live;
            var reported = ReportedBank() == sw.Target && sw.LedsSinceKey
                && !live.IsListedAndUnfetched(HspObjectId.ConsoleStatus) && !live.IsListedAndUnfetched(HspObjectId.LedStatus)
                && !live.IsListedAndUnfetched(HspObjectId.FaderInfo) && !live.IsListedAndUnfetched(HspObjectId.ChannelLevels);
            if (reported)
            {
                done = sw;
                _switch = null;
            }
            else if (_clock.GetElapsedTime(sent) > BankSwitchReportBound)
            {
                deviation = $"{sw.Kind} for {sw.Reason}: the console did not report {Describe(sw.Target)} with its LEDs within {BankSwitchReportBound.TotalSeconds:F0} s of {KeyName(sw.Key)} (SC-5 condition 1 c); the session ends as a deviation";
            }
        }

        if (deviation is not null)
        {
            throw new HspSessionFaultException(deviation);
        }

        if (done is not null)
        {
            CompleteSwitch(session, done);
        }
    }

    /// <summary>
    /// The console reported the switch's bank: every fader dragged meanwhile gets its matching event at the target's reported
    /// level, and a bump pressed meanwhile its hold (a tap when the UI let go already); a control whose state or level does not
    /// allow it is not sent, with its reason in the log. One operation in the log: the key, the report, what followed.
    /// </summary>
    private void CompleteSwitch(HspReadSession session, BankSwitch sw)
    {
        var notes = new List<string>();
        lock (_gate)
        {
            if (!ReferenceEquals(_liveSession, session))
            {
                return;
            }

            notes.Add($"{sw.Kind} done: the console reports {Describe(sw.Target)} {_clock.GetElapsedTime(sw.KeySent!.Value).TotalMilliseconds:F0} ms after {KeyName(sw.Key)} (SC-5 condition 1 c)");
            foreach (var (fader, pending) in _pendingLevels.ToArray())
            {
                var (level, reason) = FaderState(pending.Target) is { } state ? (null, state) : MatchingLevel(pending.Target);
                if (level is null)
                {
                    _pendingLevels.Remove(fader);
                    notes.Add($"{pending.Target.Name} not sent after the switch: {reason}");
                    continue;
                }

                try
                {
                    QueueMatching(session, fader, pending.Target, level.Value);
                    notes.Add($"{pending.Target.Name}: its matching event at 0x{level:X2} is queued, the UI's level 0x{pending.Level:X2} follows it");
                }
                catch (InvalidOperationException ex)
                {
                    _pendingLevels.Remove(fader);
                    notes.Add($"{pending.Target.Name} not sent after the switch: {ex.Message}");
                }
            }

            if (sw.Press is { } press)
            {
                try
                {
                    Refuse(NotIn(press.Target));
                    QueueBump(session, press, tap: sw.PressReleased);
                    notes.Add($"{press.Target.Name}: {(sw.PressReleased ? "a tap (the UI let go during the switch)" : "its hold")} is queued");
                }
                catch (InvalidOperationException ex)
                {
                    notes.Add($"{press.Target.Name} not sent after the switch: {ex.Message}");
                }
            }
        }

        foreach (var note in notes)
        {
            SessionNote(note);
        }
    }

    /// <summary>
    /// A bump press (SC-5 class B3) behind the interlocks: one hold at a time (SmartSoft sent nothing between a press and its
    /// release, F133, and never held two bumps); in the bank the console shows, the hold at once; in another bank, the
    /// automatic switch first and the hold once the console has reported it (a tap when the UI let go meanwhile).
    /// </summary>
    private Task PressBump(HspReadSession session, Target target, string control)
    {
        var bump = new HeldBump(control, PhysicalControls.Bump(target.Physical), target);
        lock (_gate)
        {
            RequireRunning(session);

            // FaderInfo is required as for a fader (the first review of SC-5 item 10, minor 4): NotIn's own checks of the
            // physical fader's record (byte 1 for a channel, byte 3 for a memory) read it, and must not pass for want of it.
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported()
                ?? (session.HoldInProgress || _switch?.Press is not null ? "another bump is held: one bump at a time is held from the PC (SmartSoft sent nothing between a press and its release, F133)" : null));
            if (_switch is { } pending)
            {
                Refuse(pending.Target == target.Bank && pending.Page == target.Page ? null : SwitchInProgress());
                pending.Press = bump;
                pending.PressReleased = false;
                return Task.CompletedTask;
            }

            if (ReportedBank() != target.Bank)
            {
                StartSwitch(session, target).Press = bump;
                return Task.CompletedTask;
            }

            Refuse(NotIn(target));
            QueueBump(session, bump, tap: false);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Queues a bump as a hold (released by the UI or the loop's 3 s bound, <see cref="HspReadLoop.MaxHold"/>) or, when the UI
    /// already let go, as a tap; asked again right before it goes, with <paramref name="also"/> (the MASTER probe's SOLO and
    /// BLACKOUT, <see cref="ProbeLedRefusal"/>) when given. Returns its shadow.
    /// </summary>
    private BumpShadow QueueBump(HspReadSession session, HeldBump bump, bool tap, Func<string?>? also = null)
    {
        // its physical fader's reports carry the bump from the press until they read the content again after the release
        // (F174, ShadowEnds); with SOLO on every fader's may (F6, INFERRED)
        var shadow = new BumpShadow { Key = bump.Key, Target = bump.Target, AllFaders = SoloNotOff() };

        // The press waits while FaderInfo (and, for a channel bump, ChannelLevels) is listed and not fetched, so that the level its
        // shadow keeps as the content's is the console's current report (the fourth review of item 10, minor H): a write goes
        // before pending fetches, and right after a list naming either object without LedStatus the gate passes (E9d: 38 of 513
        // such channel-mode lists, 348 of 430 memory-mode ones). Where LedStatus is listed too, the gate's wait for it and its
        // freshness (the write's slot after an object reply comes later than two poll periods) keep the write back as well.
        Func<HspWriteDecision> condition = () =>
            session.IsListedAndUnfetched(HspObjectId.FaderInfo) || (shadow.ChannelsToo && session.IsListedAndUnfetched(HspObjectId.ChannelLevels))
                ? HspWriteDecision.Hold($"the console listed FaderInfo{(shadow.ChannelsToo ? " or ChannelLevels" : string.Empty)} and it is not fetched yet: {bump.Target.Name}'s press waits for the level its fader reports now")
                : Recheck(session, () => NotIn(bump.Target) ?? also?.Invoke(), holdWhileFaderInfoListed: false);
        void PressSent()
        {
            lock (_gate)
            {
                // State only (this runs between the gate and the header): the levels reported now are the content's, unless an
                // older shadow of the same target still waits (two bumps in a row): its own pre-press levels are, as its bump may
                // still show in the reports.
                var older = _bumpShadows.FirstOrDefault(s => !ReferenceEquals(s, shadow) && s.PressSent && s.Key == shadow.Key && s.Target == shadow.Target);
                shadow.PreFader = older is not null ? older.PreFader : FaderRecordLevel(shadow.Physical);
                shadow.PreChannel = older is not null ? older.PreChannel
                    : shadow.Channel is { } channel && _assembler.ChannelLevelsReported ? _assembler.ChannelLevels[channel - 1] : null;
                shadow.PressSent = true;
            }
        }

        if (tap)
        {
            session.QueueKeyTap(bump.Key, condition, sent: PressSent);
        }
        else
        {
            session.QueueKeyHold(bump.Key, condition, sent: PressSent);
            _hold = bump;
        }

        _bumpShadows.Add(shadow);   // an older shadow of the same fader stays until its own release is reported
        return shadow;
    }

    /// <summary>FaderInfo record k byte 0, the level physical fader k reports (F82, F171), or null before FaderInfo has arrived.</summary>
    private byte? FaderRecordLevel(int physical) =>
        _assembler.FaderInfo is { } info ? info.RawRecords[(physical - 1) * FaderInfoObject.RecordSize] : null;

    /// <summary>
    /// The UI let go of a bump: its release is asked for once (SC-5 condition 2). A press still waiting for its switch becomes
    /// a tap, one still queued becomes a tap, one held is released at the next write slot. Nothing to release is no error:
    /// the loop's bound or the session's end released it already.
    /// </summary>
    private Task ReleaseBump(string control)
    {
        lock (_gate)
        {
            if (_liveSession is not { } session)
            {
                return IsLiveControlConfirmed() ? Task.CompletedTask : throw ReadOnly();
            }

            if (_switch is { Press: { } waiting } sw && waiting.Control == control)
            {
                sw.PressReleased = true;
            }
            else if (_hold is { } hold && hold.Control == control)
            {
                _hold = null;
                session.ReleaseKey(hold.Key);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A FADERS key from the UI as a tap (SC-2 class B1, SC-5 class B4), only from the banks SmartSoft sent it from
    /// (<see cref="BankKeyNotFrom"/>), never while a bump is held (a hold does not span a bank switch, SC-5 condition 2) or
    /// another switch is in progress. It is a bank switch like the automatic one, without a control waiting for it: once its
    /// press went, nothing else goes until the console has reported the new bank with its LEDs, within
    /// <see cref="BankSwitchReportBound"/> (SC-5 condition 1 c), so no event meant for the old bank can reach the new one.
    /// </summary>
    private Task PressBankKey(ClearedKey key)
    {
        var session = LiveSession();
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? (ReportedBank() is { } bank ? BankKeyNotFrom(key, bank) : WhyNoBank(KeyName(key)))
                ?? (session.HoldInProgress ? $"a bump is held: {KeyName(key)} waits for its release (a hold does not span a bank switch, SC-5 condition 2)" : null));
            var sw = new BankSwitch { Target = BankOf(key), Page = null, Key = key, From = ReportedBank()!.Value, Reason = $"{KeyName(key)} from the UI", Automatic = false };
            session.QueueKeyTap(key, () => Recheck(session, () => SwitchKeyState(sw), holdWhileFaderInfoListed: false), sent: () => SwitchKeySent(sw));
            _switch = sw;
        }

        return Task.CompletedTask;
    }

    /// <summary>The bank a FADERS key selects (F98; F175 for FADERS 25-48).</summary>
    private static FaderBank BankOf(ClearedKey key) => key switch
    {
        ClearedKey.Faders1To24 => FaderBank.Channels1To24,
        ClearedKey.Faders25To48 => FaderBank.Channels25To48,
        _ => FaderBank.Memories,
    };

    /// <summary>
    /// SOLO, BLACKOUT, IND 1 and IND 2 are toggles on the panel: one tap changes the state (VERIFIED, F133 / F134: a PC
    /// press toggled the LED in E9 and in E9b; IND 1 / 2 F115, F175). The state is read from the key's LED, and only from
    /// the two readings SmartSoft's presses started from (E9 to E9c: steady off `0A 0A 0A …`, steady full `FF FF FF …` for
    /// SOLO, `00 00 FF …` for BLACKOUT, `00 FF 00 …` for IND 1 / 2); blinking (BLACKOUT blinks while the MASTER is below
    /// full, F85), dim or unreported refuses, and so does an LED that the console's other report of the key
    /// (<paramref name="reported"/>: the Independents object for IND 1 / 2) contradicts. A tap still queued when the LED
    /// changes (the panel toggled it) is dropped, since it would undo that; a second toggle waits until the console has
    /// reported the first one's LED change, or until the first tap was dropped or discarded without going out. A tap counts
    /// as sent only once the session has taken its press for sending (its <c>sent</c> callback), never when its condition
    /// merely allowed it.
    /// </summary>
    private Task Toggle(ClearedKey key, bool wish, Func<KeyLed?> ledNow, string name, Func<bool?>? reported = null)
    {
        var session = LiveSession();
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(StartupPairBusy(session) ?? RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? NotInChannelBank1To24(name));
            if (_togglesAwaitingLed.ContainsKey(key) || session.IsKeyQueued(key) || session.HeldKey == key)
            {
                Refuse($"the previous {name} press has not been reported by the console yet");
            }

            if (ledNow() is not { } before)
            {
                Refuse($"the console has not reported {name}'s LED yet");
                return Task.CompletedTask;
            }

            if (SteadyState(before) is not { } state)
            {
                Refuse($"{name}'s LED reads {Describe(before)}; a PC press was seen only from a steady off or full LED (E9 to E9c)");
                return Task.CompletedTask;
            }

            if (reported?.Invoke() is { } other && other != state)
            {
                Refuse($"{name}'s LED reads {(state ? "on" : "off")} and the console's other report of it {(other ? "on" : "off")}: its state is not known (SC-5 class B5)");
            }

            if (state == wish)
            {
                return Task.CompletedTask;   // already as wished: nothing is sent
            }

            string? StateNow() =>
                NotInChannelBank1To24(name)
                ?? (ledNow() == before ? null : $"{name} changed on the console since the request; the tap would undo it");

            // A drop or a discard is forgotten through ForgetUnsent (the loop's WriteDropped, AfterLedReport).
            session.QueueKeyTap(key, () => Recheck(session, StateNow, holdWhileFaderInfoListed: false), sent: () => ToggleSent(key));
            _togglesAwaitingLed[key] = (before, ledNow);
            _togglesSent.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>The press of a toggle's tap was taken for sending: its LED change is now what the next toggle waits for.</summary>
    private void ToggleSent(ClearedKey key)
    {
        lock (_gate)
        {
            _togglesSent.Add(key);
        }
    }

    /// <summary>IND 1's or IND 2's LED (blocks 72 / 73), null until LedStatus has arrived.</summary>
    private KeyLed? IndependentLed(int number) =>
        _assembler.IndependentLeds is { } leds ? (number == 1 ? leds.Ind1 : leds.Ind2) : null;

    /// <summary>IND 1's or IND 2's state in the Independents object (F115), null until it has arrived.</summary>
    private bool? IndependentState(int number) =>
        _assembler.Independents is { } states ? (number == 1 ? states.Independent1 : states.Independent2) : null;

    /// <summary>False for a steady off LED, true for a steady full one, null for anything else.</summary>
    private static bool? SteadyState(KeyLed led) => led switch
    {
        { Light: KeyLight.Off, Blinking: false } => false,
        { Light: KeyLight.Full, Blinking: false } => true,
        _ => null,
    };

    private static string Describe(KeyLed led) => $"{led.Light}{(led.Blinking ? ", blinking" : string.Empty)}";

    /// <summary>
    /// SC-2 condition 5 (and 8): REC MEM (block 66), EDIT MEM (67) and COPY (63) must read off, steady, in the console's
    /// latest LedStatus. REC SEQ (64) is held to the same rule as a record function of the same kind (it read off at all
    /// 605 whitelisted SmartSoft writes of E9 and E9b; OBSERVED, `write_rules.py` 2026-10-31). Null when a write may go.
    /// </summary>
    private static string? RecordFunctionArmed(PanelKeys? keys)
    {
        if (keys is null)
        {
            return "the console has not reported its key LEDs in this session yet (SC-2 conditions 5 and 8)";
        }

        if (!IsOff(keys.RecMem))
        {
            return "REC MEM is lit or blinking (SC-2 condition 5)";
        }

        if (!IsOff(keys.EditMem))
        {
            return "EDIT MEM is lit or blinking (SC-2 condition 5)";
        }

        if (!IsOff(keys.Copy))
        {
            return "COPY is lit or blinking (SC-2 condition 5)";
        }

        return !IsOff(keys.RecSeq) ? "REC SEQ is lit or blinking (a record function, held to SC-2 condition 5)" : null;
    }

    private static bool IsOff(KeyLed led) => led.Light == KeyLight.Off && !led.Blinking;

    /// <summary>SC-2 condition 6: a PC fader starts from the reported level, so nothing moves before FaderInfo (with LedStatus) has been reported.</summary>
    private string? FadersUnreported() =>
        _assembler.Masters is null ? "the console has not reported its faders yet (FaderInfo; SC-2 condition 6)" : null;

    /// <summary>
    /// Every channel and memory fader event only with the MASTER at full: the reported level is post-MASTER (F96, F176), its
    /// own only at full (SC-2 condition 6), and every PC fader event of E9c and E9d in memory mode or bank 25-48 came at full.
    /// </summary>
    private string? MasterNotAtFull(string name) => _assembler.Masters switch
    {
        null => "the console has not reported the MASTER yet (FaderInfo)",
        { Master: 0xFF } => null,
        { Master: var master } => $"the MASTER reads 0x{master:X2}: {name} is sent only with the MASTER at full, where its reported level is its own (F96, F176; SC-2 condition 6)",
    };

    /// <summary>
    /// Every channel and memory fader event only while BLACKOUT's LED, block 53, reads steady off (the re-check of SC-5 item 10,
    /// new major): BLACKOUT zeroes FaderInfo record k byte 0 and ChannelLevels[c] and leaves the MASTER byte at 0xFF (F125,
    /// OBSERVED once, timed: E8b seq 26987, 26991, 26999; the content back at 27754 / 27762), so the agreement check and
    /// <see cref="MasterNotAtFull"/> pass while the reported level is not the content's, and a matching event would carry
    /// 0x00 whatever the content; the console's answer to one is UNKNOWN. Block 53 steady blue is BLACKOUT on (F114, VERIFIED);
    /// blinking is the MASTER below full (F85), refused before by the MASTER's own rule. Asked at the command and again right
    /// before the write, so a queued event is dropped; while the LEDs and the level objects are listed and unfetched the gate
    /// and the matching event's own condition hold, and a matching level that changed with them drops it. A BLACKOUT tap of
    /// ours counts from its press until the console has reported block 53 again: E8b listed FaderInfo and ChannelLevels
    /// before LedStatus in the same list (seq 26983), so the level objects may read zero before the LED says why.
    /// </summary>
    private string? BlackoutNotOff(string name)
    {
        if (_togglesSent.Contains(ClearedKey.Blackout))
        {
            return $"BLACKOUT was pressed from the PC and the console has not reported its LED (block 53) since: the faders may already read zero (F125), so {name} waits for that report";
        }

        return _assembler.Keys?.Blackout switch
        {
            null => $"the console has not reported BLACKOUT's LED (block 53) yet: {name} is sent only while it reads steady off (F125)",
            { Light: KeyLight.Off, Blinking: false } => null,
            { } led => $"BLACKOUT's LED (block 53) reads {Describe(led)}: during a blackout the console reports every fader at zero with the MASTER at full (F125), so {name}'s reported level is not its own; it is sent only while the LED reads steady off",
        };
    }

    /// <summary>SOLO's LED (block 52, F100) reads anything but steady off, or is unreported: a bump then may replace the output (F6, <see cref="BumpShadow.AllFaders"/>).</summary>
    private bool SoloNotOff() => _assembler.Keys?.Solo is not { Light: KeyLight.Off, Blinking: false };

    /// <summary>The state SmartSoft sent the MASTER, BUMPS, GO, SOLO, BLACKOUT and IND 1 / 2 in (E9 to E9c): channel mode, bank 1-24, NEXT mode off (F98, F173; the readings H53).</summary>
    private string? NotInChannelBank1To24(string control) => ReportedBank() switch
    {
        FaderBank.Channels1To24 => null,
        null => WhyNoBank(control),
        { } bank => $"the console shows {Describe(bank)}: {control} was sent from a PC only in channel mode, bank 1-24 (E9 to E9c)",
    };

    private string? StackNotAsObserved()
    {
        if (_assembler.Keys is not { } keys)
        {
            return "the console has not reported its key LEDs yet";
        }

        return keys.Stack is { Light: KeyLight.Dim, Blinking: false }
            ? null
            : $"STACK's LED reads {Describe(keys.Stack)}; GO was sent from the PC only with STACK unlit (dim, steady; F134)";
    }

    /// <summary>
    /// The session's gate (SC-2 condition 5), asked on the session's thread each time a queued write could go, after
    /// the write's slot wait and right before its header (<see cref="HspReadSession.PeekNextKind"/>): it drops the write
    /// when the session is ending or live control has expired, and otherwise applies <see cref="Interlock"/> to the
    /// console's latest key LEDs and the age of its last report.
    /// </summary>
    private HspWriteDecision LiveWriteGate(HspReadSession session)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_liveSession, session))
            {
                return HspWriteDecision.Drop("the live-control session is ending");
            }

            if (!IsLiveControlConfirmed())
            {
                return HspWriteDecision.Drop("live control is not confirmed for today (SC-2 item 6)");
            }

            // SC-5 condition 1 (c): after a bank key of ours went, nothing goes until the console has reported the new bank
            // with its LEDs (a release is never asked: it completes a press). An event queued behind the key and meant for the
            // old bank is then dropped by its own condition, never sent to the fader the new bank put under that index.
            if (_switch is { KeySent: not null } sw)
            {
                return HspWriteDecision.Hold($"the console has not reported {Describe(sw.Target)} since {KeyName(sw.Key)} went (SC-5 condition 1 c)");
            }

            // "Younger than two poll periods" (condition 5) read as: a poll reply, the console's report of what changed,
            // came within two poll periods, and it did not list LedStatus (held in Interlock). INFERRED from F57 (VERIFIED: a
            // list names what changed) and F116 (VERIFIED: a record screen opens and closes with exactly one LedStatus
            // payload, 28 of 28 and 4 of 4); not observed as a rule of the console.
            var reportFresh = _lastReportStamp is { } stamp && _clock.GetElapsedTime(stamp) <= 2 * _options.PollInterval;
            return Interlock(session, _assembler.Keys, reportFresh);
        }
    }

    /// <summary>
    /// SC-2 condition 5 as the gate applies it, from the session's list state, the console's latest key LEDs
    /// (<see cref="ConsoleStateAssembler.Keys"/>) and whether its last report is younger than two poll periods: hold
    /// while LedStatus or ConsoleStatus is listed and not fetched, while the report is older, or before the key LEDs
    /// were ever reported; drop while a record function reads armed (<see cref="RecordFunctionArmed"/>); else send.
    /// Internal so that the golden test replays it over E9 and E9b with the session and the assembler fed from the
    /// captures (SmartSoft's whitelisted writes inside a record function must read armed, the others not).
    /// </summary>
    internal static HspWriteDecision Interlock(HspReadSession session, PanelKeys? keys, bool reportFresh)
    {
        if (session.IsListedAndUnfetched(HspObjectId.LedStatus))
        {
            return HspWriteDecision.Hold("the console listed new key LEDs that are not fetched yet (SC-2 condition 5)");
        }

        if (session.IsListedAndUnfetched(HspObjectId.ConsoleStatus))
        {
            return HspWriteDecision.Hold("the console listed a new fader mode or page (ConsoleStatus) that is not fetched yet");
        }

        if (!reportFresh)
        {
            return HspWriteDecision.Hold("the console's last report is older than two poll periods (SC-2 condition 5)");
        }

        if (keys is null)
        {
            return HspWriteDecision.Hold("the console has not reported its key LEDs yet");
        }

        return RecordFunctionArmed(keys) is { } armed ? HspWriteDecision.Drop(armed) : HspWriteDecision.Send;
    }

    private async Task RunSessionAsync(IHspPipe pipe, ITransferSink log, HspReadSession session, int generation, CancellationToken stop)
    {
        LastSession = session;
        LastStopWasForced = false;
        HspReadLoop? loop = null;
        string? failure = null;
        try
        {
            loop = new HspReadLoop(pipe, session, _options, log, _clock, _delay, _lastPollStamp);
            loop.EventDecoded += evt => OnEvent(evt, session);
            loop.WriteDropped += reason =>
            {
                _logger.LogWarning("A queued write was dropped unsent: {Reason}", reason);
                ForgetUnsent(session);
            };
            try
            {
                await loop.RunAsync(stop).ConfigureAwait(false);
            }
            finally
            {
                _lastPollStamp = loop.LastPollStamp;
                LastStopWasForced = loop.ForcedStop;
            }

            log?.Note(loop.ForcedStop ? "session stopped by the application while objects were still listed (deviation)" : "session stopped by the application at an idle point");
        }
        catch (Exception ex)
        {
            failure = WithoutDevicePath(ex.Message);
            if (loop?.IncompleteWrite is { } incomplete)
            {
                // also when the release sent at the end was the incomplete write
                var latch = $"A Data write did not complete ({WithoutDevicePath(incomplete)})";
                lock (_gate)
                {
                    _incompleteWrite = latch;
                }

                RecordIncompleteWrite(latch, pipe.Description);
            }

            // SC-2 item 7: a key press whose release could not be sent is reported as such.
            var release = loop?.ReleaseAfterFault
                ?? (session.HeldKey is { } held ? $"{held} was pressed and its release could not be sent: press {held} on the panel" : null);
            if (release is not null)
            {
                failure += $"; {release}";
            }

            _logger.LogError(ex, "The USB session ended: {Reason}", failure);
            try
            {
                log?.Note($"session ended: {ex.Message}{(release is null ? string.Empty : $"; {release}")}");
            }
            catch (Exception logEx)
            {
                _logger.LogDebug(logEx, "The session note could not be written");
            }
        }
        finally
        {
            string[] lastNotes = Array.Empty<string>();
            StateAtConnect? interruptedState = null;
            lock (_gate)
            {
                if (ReferenceEquals(_liveSession, session))
                {
                    _liveSession = null;
                }

                if (ReferenceEquals(_liveLog, log))
                {
                    _liveLog = null;
                    lastNotes = _pendingNotes.ToArray();   // left by the last writes' sent callbacks: written below, before the log closes
                    if (_probeRun is { } probe)
                    {
                        // a stop or a fault inside the MASTER probe: nothing more of it goes, and the log says where the console was left;
                        // the UI is told as well (the first review's MINOR 1): the outcome Stopped while the session still reports, and
                        // the same words in the session's end (the error, or the Disconnect's note), which outlive the state at Connect
                        var shown = ReportedBank() is { } bank ? Describe(bank) : "no bank a PC sent controls in";
                        lastNotes = lastNotes.Append($"MASTER probe interrupted by the session's end (SC-4 clause 4): the console was last reported in {shown}, the download reported {Describe(probe.Home)}").ToArray();
                        _probe = MasterProbePhase.Stopped;
                        _probeReason = $"the MASTER probe was interrupted by the session's end and did not switch back: the console shows {shown} (its last report); the download reported {Describe(probe.Home)}";
                        _probeInterruption = $"The MASTER probe was interrupted by the session's end (SC-4 clause 4): the console shows {shown} (its last report); the download reported {Describe(probe.Home)}. Check the FADERS keys on the console.";
                        interruptedState = ReportStateAtConnect(session.StartupPair);
                    }

                    ResetLiveState();
                }

                if (ReferenceEquals(_pairSession, session))
                {
                    _pairSession = null;
                }

                if (ReferenceEquals(_pairLog, log))
                {
                    _pairLog = null;
                }
            }

            try
            {
                foreach (var note in lastNotes)
                {
                    _logger.LogInformation("Live control: {Note}", note);
                    log?.Note(note);
                }

                log?.Note($"polls {session.PollsSent}, fetches {session.FetchesSent}, lists {session.ListsReceived}, objects {session.ObjectsReceived}, writes {session.WritesSent}");
                log?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Closing the transfer log failed");
            }

            try
            {
                pipe.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Closing the pipe failed");
            }

            if (interruptedState is not null)
            {
                StateChanged?.Invoke(this, new DeviceStateChange(StateAtConnect: interruptedState));
            }
        }

        if (failure is not null)
        {
            string? interruption = null;
            lock (_gate)
            {
                if (_generation == generation)
                {
                    interruption = _probeInterruption;
                    _probeInterruption = null;   // said by the error below; after a Disconnect, the Disconnect says it
                }
            }

            Fail(generation, $"Session ended: {failure}{(interruption is null ? string.Empty : $"; {interruption}")}");
        }

        ReportLateEnd();
    }

    /// <summary>
    /// A session that outlived its Disconnect (the stop timeout) reports its real end here. A Disconnect inside the MASTER
    /// probe tells the UI where the console was left on this path too, as the Disconnect in time does (the re-check's N4,
    /// 2026-11-03): the session's end wrote the interruption note after the Disconnect had reported Error.
    /// </summary>
    private void ReportLateEnd()
    {
        string? interruption;
        lock (_gate)
        {
            if (!_stopTimedOut)
            {
                return;
            }

            _stopTimedOut = false;
            _session = null;
            _status = ConnectionStatus.Disconnected;
            interruption = _probeInterruption;
            _probeInterruption = null;
        }

        _logger.LogInformation("The USB session that outlived its Disconnect has now ended");
        ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Disconnected, Mode, Description, interruption));
    }

    private void CloseLog(ITransferSink log, string note)
    {
        try
        {
            log.Note(note);
            log.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Closing the transfer log failed");
        }
    }

    /// <summary>
    /// Puts the incomplete-write latch on disk when the backend gave a marker, so that a restart does not lift it (the
    /// first review of SC-2 item 10, minor); a marker that cannot be written leaves the latch in memory and says so.
    /// </summary>
    private void RecordIncompleteWrite(string latch, string pipeDescription)
    {
        if (_marker is null)
        {
            return;
        }

        try
        {
            _marker.Write(_clock.GetLocalNow(), $"{latch}; session on {WithoutDevicePath(pipeDescription)}, the last transfers in its transfer log");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "The incomplete-write marker {Path} could not be written; the latch holds only until the backend restarts", _marker.FilePath);
        }
    }

    /// <summary>Messages shown in the UI name no device path (the path is in the log, F42 / E1 keep the identification).</summary>
    private static string WithoutDevicePath(string message)
    {
        var start = message.IndexOf(@"\\?\", StringComparison.Ordinal);
        if (start < 0)
        {
            return message;
        }

        var end = message.IndexOfAny(new[] { ' ', ':' }, start);
        return end < 0 ? message[..start] + "the console's interface" : message[..start] + "the console's interface" + message[end..];
    }

    /// <summary>
    /// One decoded event of the session, on the loop's thread. In a live session (<paramref name="live"/>) a LedStatus,
    /// ConsoleStatus or FaderInfo that does not decode ends the session (SC-2 item 7; the first review of SC-2 item 10,
    /// major 2): the interlocks read those objects, and an older reading must never stand for the console's state. A
    /// read-only session logs it and goes on, as SC-1 reviewed it. In a live session every completed exchange (an idle
    /// reply, a list, an object) also advances the automatic bank switch (<see cref="AdvanceSwitch"/>), which ends the
    /// session as a deviation when the console does not report the new bank in time, and then the MASTER probe
    /// (<see cref="AdvanceProbe"/>).
    /// </summary>
    private void OnEvent(HspEvent evt, HspReadSession session)
    {
        var live = session.CanWrite;
        var showObject = false;
        if (live)
        {
            // the notes the writes before this exchange left, now after their payloads (MAJOR 2 of the first pass), and only at an
            // event that ends an exchange (the re-check's minor B): never between a reply header and the payload it announces
            if (EndsExchange(evt))
            {
                WriteLeftNotes();
            }

            TrackShadows(evt);
        }

        switch (evt)
        {
            case ReplyHeaderReceived { Header.MessageType: HspMessageType.PollReply } reply:
                // the console's report of what changed (an idle reply or a list; F57, VERIFIED): the age live control's gate checks
                lock (_gate)
                {
                    _lastReportStamp = _clock.GetTimestamp();
                }

                if (live && reply.Header.Length == 0)
                {
                    AdvanceSwitch();   // an idle reply completes its exchange
                    AdvanceProbe();
                }

                break;
            case ObjectReceived received:
                DeviceStateChange? change;
                try
                {
                    lock (_gate)
                    {
                        change = _assembler.Apply(received);

                        // the show objects of the start-up pair's download, and any listed later in that session (SC-4 clause 4)
                        if (ReferenceEquals(_pairSession, session))
                        {
                            showObject = _show.Apply(received);
                        }

                        if (live)
                        {
                            ShadowsAfterObject(received.Payload.ObjectId);
                        }
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
                {
                    var id = received.Payload.ObjectId;
                    if (live && InterlockObjects.Contains(id))
                    {
                        throw new HspSessionFaultException($"{HspObjectIds.Name(id)} did not decode ({ex.Message}): live control's interlocks read it, so the session ends rather than go on with an older reading (SC-2 item 7)");
                    }

                    _logger.LogWarning(ex, "Object {Id} ({Name}) did not decode", id, HspObjectIds.Name(id));
                    return;
                }

                if (received.Payload.ObjectId == (int)HspObjectId.DisplayContent)
                {
                    IReadOnlyList<string> lines;
                    lock (_gate)
                    {
                        lines = _assembler.DisplayLines;
                    }

                    _logger.LogInformation("Console LCD: {Line1} | {Line2}", lines.ElementAtOrDefault(0), lines.ElementAtOrDefault(1));
                }

                if (received.Payload.ObjectId == (int)HspObjectId.LedStatus)
                {
                    AfterLedReport();
                }

                if (live && received.Payload.ObjectId == (int)HspObjectId.ConsoleStatus)
                {
                    AfterStatusReport();
                }

                if (change is not null)
                {
                    StateChanged?.Invoke(this, change);
                }

                if (live)
                {
                    AdvanceSwitch();
                    AdvanceProbe();
                }

                break;
            case DirtyListReceived list:
                _logger.LogDebug("Console lists {Count} object(s): {Names}", list.List.Records.Count, string.Join(", ", list.List.Records.Select(r => r.Name)));
                if (live)
                {
                    AdvanceSwitch();
                    AdvanceProbe();
                }

                break;
            case UnexpectedTransfer unexpected:
                _logger.LogError("Unobserved behaviour: {Reason}", unexpected.Reason);
                break;
            default:
                break;
        }

        TrackStartupPair(session, showObject);
    }

    /// <summary>
    /// After each event of a session with the start-up pair (SC-4), on the session's thread: reports the pair's phase and the
    /// download's progress when they changed (<see cref="DeviceStateChange.StateAtConnect"/>); at the download's last record
    /// builds the console's show (<see cref="ConsoleShowAssembler"/>), reports every memory the download did not list as empty
    /// (INFERRED, <see cref="ConsoleStateAssembler.ReportUndeliveredMemoriesEmpty"/>) and decides the MASTER probe once, in the
    /// same lock and before any of it is reported (<see cref="DecideMasterProbe"/>; the first review's MINOR 2); after the
    /// download a show object the console lists again rebuilds the show. FaderInfo reported at any time makes the probe
    /// unnecessary; a pair that ended without the download leaves it impossible (the UI then asks for one fader movement).
    /// </summary>
    private void TrackStartupPair(HspReadSession session, bool showObject)
    {
        if (!session.SendsStartupPair)
        {
            return;
        }

        var pair = session.StartupPair;
        StateAtConnect? state;
        ConsoleShow? show = null;
        DeviceStateChange? memories = null;
        string? probeNote = null;
        ITransferSink? log;
        lock (_gate)
        {
            if (!ReferenceEquals(_pairSession, session))
            {
                return;
            }

            log = _pairLog;
            if (_assembler.FaderInfo is not null && _probe is MasterProbePhase.Pending or MasterProbePhase.NotPossible)
            {
                _probe = MasterProbePhase.NotNeeded;
                _probeReason = null;
            }

            if (pair.Phase == HspStartupPairPhase.Ended && _probe == MasterProbePhase.Pending)
            {
                _probe = MasterProbePhase.NotPossible;
                _probeReason = "the console's state was not downloaded, so the MASTER probe does not run: move one fader on the panel, or the MASTER, and the console reports it";
            }

            if (pair.Phase == HspStartupPairPhase.Done && !_downloadComplete)
            {
                _downloadComplete = true;
                memories = _assembler.ReportUndeliveredMemoriesEmpty();
                show = _show.Build();

                // the probe is decided in this lock, before the download's end is reported to anyone (the first review's MINOR 2,
                // 2026-11-03): from here on a control meets the probe's own refusal (StartupPairBusy) or comes after the probe was
                // found not to run; one accepted earlier, at the last record's own report, makes the probe refuse to start
                probeNote = DecideMasterProbe(session);
            }
            else if (showObject && _downloadComplete)
            {
                show = _show.Build();
            }

            state = ReportStateAtConnect(pair);
        }

        if (memories is not null)
        {
            StateChanged?.Invoke(this, memories);
        }

        if (state is not null || show is not null)
        {
            StateChanged?.Invoke(this, new DeviceStateChange(StateAtConnect: state, ConsoleShow: show));
        }

        if (probeNote is not null)
        {
            WriteNote(log, probeNote);
        }
    }

    /// <summary>The state to report when it differs from the last one reported, else null (under <c>_gate</c>).</summary>
    private StateAtConnect? ReportStateAtConnect(HspStartupPairStatus pair)
    {
        var now = new StateAtConnect(
            pair.Phase switch
            {
                HspStartupPairPhase.Waiting => StateAtConnectPhase.Waiting,
                HspStartupPairPhase.VersionRequested or HspStartupPairPhase.VersionAccepted => StateAtConnectPhase.Requesting,
                HspStartupPairPhase.DownloadRequested or HspStartupPairPhase.Downloading => StateAtConnectPhase.Downloading,
                HspStartupPairPhase.Done => StateAtConnectPhase.Done,
                HspStartupPairPhase.Ended => StateAtConnectPhase.NotSent,
                _ => StateAtConnectPhase.Idle,
            },
            pair.DownloadRecords,
            pair.DownloadDelivered,
            pair.EndReason,
            _probe,
            _probeReason);
        if (now == _lastStateAtConnect)
        {
            return null;
        }

        _lastStateAtConnect = now;
        return now;
    }

    /// <summary>
    /// SC-4 clause 4, once per session at the download's last record, under <c>_gate</c> and in the same lock that takes the
    /// download as complete (<see cref="TrackStartupPair"/>; the first review's MINOR 2): when FaderInfo (the MASTER) has not
    /// been reported, the probe (<see cref="StartMasterProbe"/>) or the reason it cannot run, as the phase and reason the
    /// caller reports; returns the transfer log's note, null when the probe was decided before or the session is ending.
    /// </summary>
    private string? DecideMasterProbe(HspReadSession session)
    {
        if (_probeTried || !ReferenceEquals(_pairSession, session))
        {
            return null;
        }

        _probeTried = true;
        if (_assembler.FaderInfo is not null)
        {
            _probe = MasterProbePhase.NotNeeded;
            _probeReason = null;
            return "MASTER probe not needed: the console reported FaderInfo (the MASTER) in this session";
        }

        if (StartMasterProbe(session) is { } refusal)
        {
            _probe = MasterProbePhase.NotPossible;
            _probeReason = refusal;
            return $"MASTER probe not sent: {refusal}";
        }

        _probe = MasterProbePhase.Sent;
        _probeReason = null;
        return _probeNote ?? "MASTER probe queued";
    }

    /// <summary>
    /// The MASTER probe (SC-4 clause 4 and its amendment of 2026-11-02, late evening), under <c>_gate</c>: one tap of the bump
    /// of a channel that no DMX address names in the downloaded Patch (<see cref="ConsoleShowAssembler.UnpatchedChannel"/>),
    /// the first of the shown bank, else the first of the other channel bank (from memory mode bank 1-24's first, else bank
    /// 25-48's), through the SC-5 bump path (its shadow applies; the physical bump is the channel's, channel − 24 in bank
    /// 25-48, <see cref="ChannelPosition"/>), only in a live-control session, with the record functions, SOLO and BLACKOUT off
    /// and NEXT off. When the console shows another bank or mode than the channel's, SC-5's automatic switch goes first and,
    /// once the tap's reports are in, the switch back to the bank or mode the download reported (<see cref="ProbeRoute"/>,
    /// <see cref="AdvanceProbe"/>), and only from memory page 1, where every PC switch of the captures came (body[2] = 0). It is
    /// the one bump of ours that goes before FaderInfo was reported, which is what it asks for: the checks of a fader's record
    /// that need FaderInfo (a channel's fader still holding a memory, F112) cannot run for it. Returns null when its first
    /// write is queued (the note in <see cref="_probeNote"/>), else why not.
    /// </summary>
    private string? StartMasterProbe(HspReadSession session)
    {
        _probeNote = null;
        if (!session.CanWrite || !ReferenceEquals(_liveSession, session))
        {
            return "live control is off: the probe is a bump tap, sent only in a live-control session (Device:Usb:LiveControlDate). Move one fader on the panel, or the MASTER, and the console reports it";
        }

        if (_assembler.Keys is not { } keys)
        {
            return "the console did not report its key LEDs in the download";
        }

        if ((RecordFunctionArmed(keys) ?? ProbeLedRefusal()) is { } refused)
        {
            return refused;
        }

        if (ReportedBank() is not { } bank)
        {
            return WhyNoBank("the MASTER probe");
        }

        // a FADERS key of the UI's taken between the download's last record and this lock: the probe never runs beside a switch
        if (SwitchInProgress() is { } switching)
        {
            return $"{switching}: the probe does not run beside it. Move one fader on the panel, or the MASTER, and the console reports it";
        }

        // nor beside anything else of ours (the first review's MINOR 2): a control accepted at the last record's own report
        if (ProbeInFlightRefusal(session) is { } inFlight)
        {
            return inFlight;
        }

        if (_show.PatchChannels is not { } patch)
        {
            return "the download did not deliver the Patch, so no unpatched channel can be chosen";
        }

        // either channel bank (the amendment of 2026-11-02, late evening): the shown bank's first unpatched channel, else the
        // other bank's; from memory mode bank 1-24's first, else bank 25-48's
        var first = bank == FaderBank.Channels25To48 ? FaderBank.Channels25To48 : FaderBank.Channels1To24;
        var other = first == FaderBank.Channels1To24 ? FaderBank.Channels25To48 : FaderBank.Channels1To24;
        if ((ConsoleShowAssembler.UnpatchedChannel(patch, first) ?? ConsoleShowAssembler.UnpatchedChannel(patch, other)) is not { } channel)
        {
            return "every channel of both banks is patched (the downloaded Patch, F70: each of channels 1-48 has a DMX address), so there is no bump of an unpatched channel to tap: move one fader on the panel, or the MASTER, and the console reports it";
        }

        var (channelBank, physical) = ChannelPosition(channel);
        var route = ProbeRoute(bank, channelBank);
        if (route.Length > 0 && _assembler.FaderMode!.MemoryPage != 1)
        {
            return $"the console shows memory page {_assembler.FaderMode.MemoryPage}: every bank or mode switch a PC was seen making came on page 1 (ConsoleStatus body[2] = 0, E9 to E11b) and whether the page outlasts one is UNKNOWN, so the probe does not switch to channel {channel}'s bank: move one fader on the panel, or the MASTER, and the console reports it";
        }

        var target = new Target(channelBank, null, physical, $"bump {channel} (the MASTER probe)", channel);
        var run = new MasterProbeRun
        {
            Bump = new HeldBump(DeviceControls.ChannelBump(channel), PhysicalControls.Bump(physical), target),
            Home = bank,
            Expected = bank,
            Out = new Queue<FaderBank>(route),
            Back = new Queue<FaderBank>(ProbeRoute(channelBank, bank)),
        };
        try
        {
            if (run.Out.TryDequeue(out var next))
            {
                run.Leg = StartSwitch(session, ProbeLeg(run, next, back: false));
            }
            else
            {
                Refuse(NotIn(target));
                run.Shadow = QueueBump(session, run.Bump, tap: true, also: ProbeLedRefusal);
            }
        }
        catch (InvalidOperationException ex)
        {
            return route.Length > 0 ? $"the automatic switch to {Describe(route[0])} was refused ({ex.Message})" : ex.Message;
        }

        _probeRun = run;
        string KeysOf(IEnumerable<FaderBank> banks) => string.Join(", then ", banks.Select(b => KeyName(BankKey(b))));
        var where = route.Length == 0
            ? $"in {Describe(channelBank)}, the bank the console shows"
            : $"in {Describe(channelBank)}, after SC-5's automatic switch from {Describe(bank)} ({KeysOf(route)}) and, once the tap's reports are in, back to {Describe(bank)} as the download reported it ({KeysOf(run.Back)}): each switch a transition a PC was seen making (SmartSoft's FADERS 1-24 and FADERS MEMS F133 / F134 VERIFIED, its FADERS 25-48 F175 in second sessions; our host's F179 OBSERVED once; memory mode and bank 25-48 only through bank 1-24), the amendment of 2026-11-02, late evening";
        _probeNote = $"MASTER probe (SC-4 clause 4): bump {physical} of channel {channel}, unpatched (no DMX address names it in the downloaded Patch, F70), as a tap {where}; a bump press makes the console list FaderInfo with the MASTER and BUMPS (F174, F178, VERIFIED at BUMPS 0xFF), an unpatched channel changes no DMX (E8b channel 49, E9d channel 47: OBSERVED); the probe itself is unobserved until E13";
        return null;
    }

    /// <summary>
    /// The MASTER probe under way (SC-4 clause 4 as amended on 2026-11-02, late evening), read and changed under <c>_gate</c>
    /// only: the channel's bump (a tap), the bank or mode the download reported (where the probe leaves the console), the
    /// switches before the tap and after its reports (<see cref="ProbeRoute"/>), the bank the console must show at the next
    /// step, the switch the probe started last and the tap's shadow once the tap is queued.
    /// </summary>
    private sealed class MasterProbeRun
    {
        public required HeldBump Bump { get; init; }

        public required FaderBank Home { get; init; }

        public required Queue<FaderBank> Out { get; init; }

        /// <summary>The switches back to <see cref="Home"/>; planned from the channel's bank, or from where the probe is when FaderInfo came on the way out.</summary>
        public required Queue<FaderBank> Back { get; set; }

        /// <summary>The bank the console must report at the next step: the download's, then each completed switch's.</summary>
        public required FaderBank Expected { get; set; }

        /// <summary>The probe's switch in progress or just ended, until <see cref="AdvanceProbe"/> has taken its outcome.</summary>
        public BankSwitch? Leg { get; set; }

        /// <summary>The tap's shadow (<see cref="QueueBump"/>), once the tap is queued.</summary>
        public BumpShadow? Shadow { get; set; }

        /// <summary>The tap's reports are in (<see cref="ProbeTapReported"/>): the switches back may go.</summary>
        public bool TapReported { get; set; }
    }

    /// <summary>
    /// The MASTER probe's automatic switches from <paramref name="from"/> to <paramref name="to"/>, in order (the amendment of
    /// 2026-11-02, late evening, rule 2: each a bank transition a committed capture shows a PC host making): none within the
    /// same bank; one where a PC was seen sending that bank's key from that bank (<see cref="BankKeyNotFrom"/>: bank 1-24 to
    /// 25-48 and back, SmartSoft in E9b to E9e and our host in E11b, F179; bank 1-24 to memory mode and back, SmartSoft in E9
    /// to E9d and our host in E11 attempt 3 and E11b); else through bank 1-24, since no PC was ever seen switching memory mode
    /// to bank 25-48 or back directly (OBSERVED: none in any capture).
    /// </summary>
    private static FaderBank[] ProbeRoute(FaderBank from, FaderBank to) =>
        from == to ? Array.Empty<FaderBank>()
        : BankKeyNotFrom(BankKey(to), from) is null ? new[] { to }
        : new[] { FaderBank.Channels1To24, to };

    /// <summary>What a switch of the probe's is for, as SC-5's switch takes it: a bank and a name (no page: none is ever changed, F113).</summary>
    private static Target ProbeLeg(MasterProbeRun run, FaderBank bank, bool back) =>
        new(bank, null, run.Bump.Target.Physical, back ? $"the MASTER probe's return to {Describe(run.Home)}" : $"the MASTER probe (bump {run.Bump.Target.Channel})", null);

    /// <summary>
    /// The MASTER probe's next step (SC-4 clause 4 as amended on 2026-11-02, late evening), on the session's thread after each
    /// completed exchange, once <see cref="AdvanceSwitch"/> has run, so never while a switch of the probe's waits for the
    /// console's report (SC-5 condition 1 c, the 2 s bound): the next switch towards the channel's bank, then the tap (SC-5's
    /// bump path, its shadow and the gate as for any bump of ours), then, once the tap's reports are in
    /// (<see cref="ProbeTapReported"/>), the switches back to the bank or mode the download reported, then the end. One write
    /// of the probe's at a time: each step waits for the previous one's outcome. The probe stops where it is, with nothing
    /// more of it sent (<see cref="EndProbe"/>), when one of its writes was dropped unsent, when the console shows another bank
    /// than the probe expects (a key on the panel, or NEXT mode), or when a record function reads armed, or SOLO or BLACKOUT
    /// on (<see cref="ProbeLedRefusal"/>, before every switch since the first review's NIT 4), before its next write.
    /// </summary>
    private void AdvanceProbe()
    {
        var notes = new List<string>();
        lock (_gate)
        {
            if (_probeRun is not { } run || _liveSession is not { } live || _switch is not null)
            {
                return;
            }

            try
            {
                StepProbe(live, run, notes);
            }
            catch (InvalidOperationException ex)
            {
                EndProbe(run, ex.Message, notes);   // SC-5's switch or bump path refused the next write
            }
        }

        foreach (var note in notes)
        {
            SessionNote(note);
        }
    }

    /// <summary>One step of <see cref="AdvanceProbe"/>, under <c>_gate</c>; a refusal of the next write throws, and the probe stops with it.</summary>
    private void StepProbe(HspReadSession live, MasterProbeRun run, List<string> notes)
    {
        if (run.Leg is { } leg)
        {
            run.Leg = null;
            if (leg.KeySent is null)
            {
                EndProbe(run, $"{KeyName(leg.Key)} for {leg.Reason} was dropped unsent", notes);
                return;
            }

            run.Expected = leg.Target;   // AdvanceSwitch took it as done: the console reported the bank with its LEDs
        }

        if (ReportedBank() != run.Expected)
        {
            EndProbe(run, $"the console shows {(ReportedBank() is { } shown ? Describe(shown) : "no bank a PC sent controls in")}, not {Describe(run.Expected)} as the probe expects (a key on the panel, or NEXT mode)", notes);
            return;
        }

        // FaderInfo reported on the way out, before the tap (a switch whose physical faders' contents differ lists it, OBSERVED:
        // the first list after a mode switch named FaderInfo at E9 seq 75726, E9b 80262, E9c 56950 and E9d 50035, each with
        // ConsoleStatus and LedStatus, E9d's with PlaybackInfo too; or a fader moved on the panel): the clause's condition, "only
        // when FaderInfo has not been reported since Connect", no longer holds, so neither the rest of the way out nor the tap is
        // sent, and the probe goes home from where it is
        if (!run.TapReported && run.Shadow is null && _assembler.FaderInfo is not null && _assembler.Masters is { } known)
        {
            run.TapReported = true;
            run.Out.Clear();
            run.Back = new Queue<FaderBank>(ProbeRoute(run.Expected, run.Home));
            notes.Add($"MASTER probe: the console reported FaderInfo before the tap, the MASTER reads 0x{known.Master:X2} (FaderInfo body[194]), so neither the rest of the way out nor the tap of {run.Bump.Target.Name} is sent{(run.Back.Count > 0 ? $"; the switch back to {Describe(run.Home)} follows" : string.Empty)}");
        }

        // before each switch the clause's LED refusals as well (the first review's NIT 4, 2026-11-03: reading (18), no PC FADERS
        // key was ever seen sent with SOLO lit or BLACKOUT on, OBSERVED (the bank-key census of 2026-11-02 / 03, F175 / F179
        // annotations in known-facts.md)); a refusal stops the probe here, with no switch back
        if (run.Out.TryDequeue(out var outward))
        {
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? ProbeLedRefusal());
            run.Leg = StartSwitch(live, ProbeLeg(run, outward, back: false));
            return;
        }

        if (!run.TapReported)
        {
            if (run.Shadow is not { } shadow)
            {
                // the clause's refusals asked again, now that the probe has come to the channel's bank, and again at the tap's slot
                Refuse(RecordFunctionArmed(_assembler.Keys) ?? ProbeLedRefusal() ?? NotIn(run.Bump.Target));
                run.Shadow = QueueBump(live, run.Bump, tap: true, also: ProbeLedRefusal);
                return;
            }

            if (!shadow.PressSent)
            {
                if (!live.IsKeyQueued(shadow.Key) && live.HeldKey != shadow.Key)
                {
                    EndProbe(run, $"the tap of {run.Bump.Target.Name} was dropped unsent", notes);
                }

                return;
            }

            if (!ProbeTapReported(shadow, live))
            {
                return;   // its release, then FaderInfo and ChannelLevels after it: not all in yet
            }

            run.TapReported = true;
            var then = run.Back.Count > 0 ? $"; the switch back to {Describe(run.Home)} follows" : string.Empty;
            notes.Add(_assembler.Masters is { } masters
                ? $"MASTER probe: the console reported the tap of {run.Bump.Target.Name}, the MASTER reads 0x{masters.Master:X2} (FaderInfo body[194]){then}"
                : $"MASTER probe: the console listed no FaderInfo for the tap of {run.Bump.Target.Name} by two poll replies after its release{then}");
        }

        if (run.Back.TryDequeue(out var homeward))
        {
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? ProbeLedRefusal());
            run.Leg = StartSwitch(live, ProbeLeg(run, homeward, back: true));
            return;
        }

        _probeRun = null;
        var page = run.Home == FaderBank.Memories ? _assembler.FaderMode?.MemoryPage : null;
        notes.Add(page is null or 1
            ? $"MASTER probe done (SC-4 clause 4): the console shows {Describe(run.Home)}{(page is null ? string.Empty : ", page 1")}, as the download reported it"
            : $"MASTER probe done (SC-4 clause 4): the console shows {Describe(run.Home)}, page {page}, where the download reported page 1 (a page change across a mode switch, never observed; no page change is sent from the PC, F113)");
        if (_assembler.FaderInfo is null)
        {
            _probe = MasterProbePhase.NotPossible;
            _probeReason = $"the console listed no FaderInfo for the MASTER probe's tap of {run.Bump.Target.Name}: move one fader on the panel, or the MASTER, and the console reports it";
        }
    }

    /// <summary>
    /// The probe's tap is reported, so the switch back may go (the amendment's rule 3): its shadow ended
    /// (<see cref="ShadowEnds"/>), or its release was taken, a poll reply came after it, and FaderInfo and ChannelLevels were
    /// each reported after it (<see cref="ReportedAfterRelease"/>: listed after it and delivered, or quiet for two poll
    /// replies; nothing of either listed and unfetched). The shadow's level test is not asked here: it guards that physical
    /// fader's events, which the shadow goes on doing until it ends; a tap that listed nothing (the channel's content already
    /// at the bump's level, as in the owner's log, OBSERVED once) is reported by the two quiet replies. LedStatus is the
    /// gate's: no write goes while it is listed and unfetched (SC-2 condition 5).
    /// </summary>
    private bool ProbeTapReported(BumpShadow shadow, HspReadSession live) =>
        !_bumpShadows.Contains(shadow)
        || (shadow.Released
            && ReportedAfterRelease(live, HspObjectId.FaderInfo, shadow.FaderInfoListed, shadow.FaderInfoQuiet)
            && ReportedAfterRelease(live, HspObjectId.ChannelLevels, shadow.ChannelLevelsListed, shadow.ChannelLevelsQuiet));

    /// <summary>
    /// The key LEDs the MASTER probe's tap never goes under (SC-4 clause 4), under <c>_gate</c>: SOLO not steady off (a bump with
    /// SOLO on replaces the output, F6, DOCUMENTED; LED block 52, F100) or BLACKOUT lit and steady (block 53, F114, VERIFIED); its
    /// blink is the MASTER below full (F85), which the probe is there to learn; and, first, a SOLO or BLACKOUT tap of ours whose
    /// LED change the console has not reported yet (queued or sent: the `_togglesSent` rule as <see cref="BlackoutNotOff"/>
    /// uses it; the first review's MINOR 2), since the reported LED is then not the console's state. Asked when the probe
    /// starts, before each of its switches (the first review's NIT 4: no PC FADERS key was seen with SOLO lit or BLACKOUT on,
    /// OBSERVED (the bank-key census of 2026-11-02 / 03, F175 / F179 annotations in known-facts.md), reading (18)), before its
    /// tap is queued (after a switch, later than the download) and at the tap's slot; null when none.
    /// </summary>
    private string? ProbeLedRefusal() => _assembler.Keys switch
    {
        _ when _togglesAwaitingLed.ContainsKey(ClearedKey.Solo) || _togglesSent.Contains(ClearedKey.Solo) =>
            "SOLO was pressed from the PC and the console has not reported its LED (block 52) since: the probe's tap would go before the device knows whether SOLO is on (F6). Move one fader on the panel, or the MASTER, and the console reports it",
        _ when _togglesAwaitingLed.ContainsKey(ClearedKey.Blackout) || _togglesSent.Contains(ClearedKey.Blackout) =>
            "BLACKOUT was pressed from the PC and the console has not reported its LED (block 53) since: the probe's tap would go before the device knows whether BLACKOUT is on (F114, F125). Move one fader on the panel, or the MASTER, and the console reports it",
        null => "the console did not report its key LEDs",
        { Solo: not { Light: KeyLight.Off, Blinking: false } } => "SOLO is not off (LED block 52): a bump with SOLO on replaces the output (F6), so the probe is never sent under SOLO (SC-4 clause 4). Move one fader on the panel, or the MASTER, and the console reports it",
        { Blackout: { Blinking: false, Light: not KeyLight.Off } } => "BLACKOUT is on (LED block 53 steady, F114): the probe is never sent under BLACKOUT (SC-4 clause 4)",
        _ => null,
    };

    /// <summary>
    /// Anything of ours in flight when the probe would start (under <c>_gate</c>; the first review's MINOR 2, 2026-11-03): a key
    /// held, or a write queued and not yet sent (a control accepted at the download's last record, before the probe was decided);
    /// the probe then does not start, with no retry, since its first write would go behind or beside it. SOLO and BLACKOUT taps
    /// of ours awaiting their LED are <see cref="ProbeLedRefusal"/>'s. Null when nothing is in flight.
    /// </summary>
    private static string? ProbeInFlightRefusal(HspReadSession session) =>
        session.HeldKey is { } held ? $"{KeyName(held)} is held by this host: the probe does not start beside it. Move one fader on the panel, or the MASTER, and the console reports it"
        : session.HoldInProgress ? "a bump of ours is held: the probe does not start beside it. Move one fader on the panel, or the MASTER, and the console reports it"
        : session.QueuedWrites > 0 ? $"{session.QueuedWrites} write(s) of ours wait to be sent (a control taken at the download's last record): the probe does not start behind them. Move one fader on the panel, or the MASTER, and the console reports it"
        : null;

    /// <summary>
    /// The probe stops where it is (under <c>_gate</c>): nothing more of it is sent, neither a retry nor a switch back, since the
    /// console's state is no longer the one the probe planned for; the transfer log says why and where the console is, and the
    /// UI is told too, also when FaderInfo is already known (the first review's MINOR 1, 2026-11-03): the phase
    /// <see cref="MasterProbePhase.Stopped"/> with "the console shows …; the download reported …", and without FaderInfo the
    /// request for one fader movement.
    /// </summary>
    private void EndProbe(MasterProbeRun run, string reason, List<string> notes)
    {
        _probeRun = null;
        var why = reason.StartsWith("Refused: ", StringComparison.Ordinal) ? reason["Refused: ".Length..].TrimEnd('.') : reason;
        var shown = ReportedBank() is { } bank ? Describe(bank) : "no bank a PC sent controls in";
        notes.Add($"MASTER probe stopped (SC-4 clause 4): {why}; nothing more of it is sent, no retry and no switch back: the console shows {shown}, the download reported {Describe(run.Home)}");
        _probe = MasterProbePhase.Stopped;
        _probeReason = $"the MASTER probe stopped ({why}) and did not switch back: the console shows {shown}; the download reported {Describe(run.Home)}{MoveOnceUnlessReported()}";
    }

    /// <summary>The request for one fader movement that ends a probe outcome while the MASTER is unreported (under <c>_gate</c>).</summary>
    private string MoveOnceUnlessReported() =>
        _assembler.FaderInfo is null ? ". Move one fader on the panel, or the MASTER, and the console reports it" : string.Empty;

    /// <summary>
    /// An event after which the exchange it belongs to is complete: an idle poll reply (a header announcing no payload), a list,
    /// an object. A reply header that announces a payload is not one: its payload is read next, and nothing may be written to
    /// disk between them (SC-1, the loop's transfer log is written after the exchange for the same reason).
    /// </summary>
    private static bool EndsExchange(HspEvent evt) =>
        evt is DirtyListReceived or ObjectReceived or ReplyHeaderReceived { Header.MessageType: HspMessageType.PollReply, Header.Length: 0 };

    /// <summary>
    /// The bumps this session pressed (<see cref="BumpShadow"/>), on the session's thread: a poll reply after the release was
    /// taken marks it released; after that an idle reply counts as quiet for both objects, a list counts as quiet for an object
    /// it does not name and marks the one it names as listed (its quiet count starting again). The end is asked after each
    /// poll reply here and after each delivered object in <see cref="ShadowsAfterObject"/>, under the lock of the assembler's
    /// update.
    /// </summary>
    private void TrackShadows(HspEvent evt)
    {
        lock (_gate)
        {
            if (_liveSession is not { } live || _bumpShadows.Count == 0)
            {
                return;
            }

            switch (evt)
            {
                case ReplyHeaderReceived { Header.MessageType: HspMessageType.PollReply } reply:
                    var held = live.HeldKey;
                    foreach (var shadow in _bumpShadows.Where(s => s.PressSent && !s.Released && s.Key != held))
                    {
                        shadow.Released = true;   // the release was taken before this poll (nothing goes between a press and its release)
                    }

                    if (reply.Header.Length == 0)
                    {
                        foreach (var shadow in _bumpShadows.Where(s => s.Released))
                        {
                            shadow.FaderInfoQuiet++;   // an idle reply: nothing changed (F57)
                            shadow.ChannelLevelsQuiet++;
                        }
                    }

                    break;
                case DirtyListReceived list:
                    var faderInfo = list.List.Records.Any(r => r.ObjectId == (int)HspObjectId.FaderInfo);
                    var channelLevels = list.List.Records.Any(r => r.ObjectId == (int)HspObjectId.ChannelLevels);
                    foreach (var shadow in _bumpShadows.Where(s => s.Released))
                    {
                        shadow.FaderInfoListed |= faderInfo;
                        shadow.ChannelLevelsListed |= channelLevels;
                        shadow.FaderInfoQuiet = faderInfo ? 0 : shadow.FaderInfoQuiet + 1;
                        shadow.ChannelLevelsQuiet = channelLevels ? 0 : shadow.ChannelLevelsQuiet + 1;
                    }

                    break;
                default:
                    return;
            }

            _bumpShadows.RemoveAll(s => ShadowEnds(s, live));
        }
    }

    /// <summary>
    /// Under <c>_gate</c>, right after the assembler applied a delivered object: a shadow whose fader's reports now read the
    /// content again ends (<see cref="ShadowEnds"/>), never before the new report is in. SOLO's LED read on (or not steady
    /// off) while a shadow lasts widens it to every physical fader (SOLO pressed on the panel during our hold; the PC sends
    /// nothing between a press and its release).
    /// </summary>
    private void ShadowsAfterObject(int objectId)
    {
        if (_bumpShadows.Count == 0 || _liveSession is not { } live)
        {
            return;
        }

        var solo = objectId == (int)HspObjectId.LedStatus && SoloNotOff();
        foreach (var shadow in _bumpShadows)
        {
            shadow.AllFaders |= solo;
        }

        _bumpShadows.RemoveAll(s => ShadowEnds(s, live));
    }

    /// <summary>
    /// After each LedStatus in a live session: a SOLO / BLACKOUT / IND tap counts as reported once its press has gone out and
    /// its LED reads differently (a tap still queued is dropped by its own condition instead); an automatic switch whose key
    /// went has its LEDs (SC-5 condition 1 c); and when a record function now reads armed, every queued write that has not
    /// started is dropped (SC-2 condition 5; a press already sent keeps its release, which becomes due at once).
    /// </summary>
    private void AfterLedReport()
    {
        HspReadSession? live;
        string? armed;
        lock (_gate)
        {
            live = _liveSession;
            if (live is null || _assembler.Keys is not { } keys)
            {
                return;
            }

            foreach (var (key, entry) in _togglesAwaitingLed.ToArray())
            {
                if (entry.Led() is { } now && now != entry.Before && _togglesSent.Contains(key))
                {
                    _togglesAwaitingLed.Remove(key);
                    _togglesSent.Remove(key);
                }
            }

            if (_switch is { KeySent: not null } sw)
            {
                sw.LedsSinceKey = true;
            }

            armed = RecordFunctionArmed(keys);
        }

        if (armed is not null)
        {
            var dropped = live.DiscardQueuedWrites();
            if (dropped > 0)
            {
                _logger.LogWarning("{Count} queued write(s) dropped before they started: {Reason}", dropped, armed);
            }

            ForgetUnsent(live);
        }
    }

    /// <summary>
    /// After each ConsoleStatus in a live session: when what the physical faders control changed (bank, mode or page), the
    /// console treats the re-assigned faders as unmatched (F172), so every fader is matched again before its next event;
    /// and a hold whose bank or page the console no longer shows is released now (a hold does not span a bank switch, SC-5
    /// condition 2).
    /// </summary>
    private void AfterStatusReport()
    {
        string? note = null;
        lock (_gate)
        {
            if (_liveSession is not { } live)
            {
                return;
            }

            var now = (ReportedBank(), _assembler.FaderMode?.MemoryPage);
            if (!Equals(_assignment, now))
            {
                _assignment = now;
                _matched.Clear();
            }

            if (_hold is { } hold && live.HeldKey == hold.Key && NotIn(hold.Target) is { } moved && live.ReleaseKey(hold.Key))
            {
                _hold = null;
                note = $"{hold.Target.Name} released at once: {moved} (a hold does not span a bank switch, SC-5 condition 2)";
            }
        }

        if (note is not null)
        {
            SessionNote(note);
        }
    }

    /// <summary>
    /// After a queued write was dropped or discarded unsent: a toggle's tap that never went leaves nothing to wait for; a
    /// matching event that never went leaves its fader's pending level behind (forgotten: the next drag matches again); a bank
    /// key that never went abandons its switch with everything that waited for it; a hold whose press never went is no hold.
    /// </summary>
    private void ForgetUnsent(HspReadSession live)
    {
        string? note = null;
        lock (_gate)
        {
            foreach (var key in _togglesAwaitingLed.Keys.ToArray())
            {
                if (!_togglesSent.Contains(key) && !live.IsKeyQueued(key) && live.HeldKey != key)
                {
                    _togglesAwaitingLed.Remove(key);
                }
            }

            if (_switch is { KeySent: null } sw && !live.IsKeyQueued(sw.Key) && live.HeldKey != sw.Key)
            {
                note = $"{sw.Kind} for {sw.Reason} abandoned: {KeyName(sw.Key)} was not sent";
                _switch = null;
                _pendingLevels.Clear();
            }
            else if (_switch is null)
            {
                foreach (var fader in _pendingLevels.Keys.ToArray())
                {
                    if (!live.IsFaderQueued(fader))
                    {
                        _pendingLevels.Remove(fader);
                    }
                }
            }

            if (_hold is { } hold && !live.IsKeyQueued(hold.Key) && live.HeldKey != hold.Key)
            {
                _hold = null;
            }

            // a bump whose press never went leaves its fader's reports as they were
            _bumpShadows.RemoveAll(s => !s.PressSent && !live.IsKeyQueued(s.Key) && live.HeldKey != s.Key);
        }

        if (note is not null)
        {
            SessionNote(note);
        }
    }

    /// <summary>A new session, or the end of one: nothing matched, nothing pending, no switch, no hold, no MASTER probe under way (all under <c>_gate</c>).</summary>
    private void ResetLiveState()
    {
        _matched.Clear();
        _pendingLevels.Clear();
        _switch = null;
        _assignment = null;
        _hold = null;
        _bumpShadows.Clear();
        _pendingNotes.Clear();
        _probeRun = null;
    }

    /// <summary>
    /// A note in the live session's transfer log, written on the session's thread from an event (SC-5 condition 1: the switch
    /// is logged as one operation), never between the gate and a write's header: a write's <c>sent</c> callback leaves its
    /// note with <see cref="LeaveNote"/> instead, and the notes left are written first, at the next event (after that write's
    /// payload). It never throws; a log that fails here fails the loop's own next write of it, which ends the session.
    /// </summary>
    private void SessionNote(string text)
    {
        string[] left;
        ITransferSink? log;
        lock (_gate)
        {
            log = _liveLog;
            left = _pendingNotes.ToArray();
            _pendingNotes.Clear();
        }

        foreach (var note in left.Append(text))
        {
            WriteNote(log, note);
        }
    }

    /// <summary>The notes the writes' sent callbacks left, written at an event of the session's thread (after the payload of the write that left them).</summary>
    private void WriteLeftNotes()
    {
        string[] left;
        ITransferSink? log;
        lock (_gate)
        {
            if (_pendingNotes.Count == 0)
            {
                return;
            }

            log = _liveLog;
            left = _pendingNotes.ToArray();
            _pendingNotes.Clear();
        }

        foreach (var note in left)
        {
            WriteNote(log, note);
        }
    }

    /// <summary>
    /// For a write's <c>sent</c> callback, which runs between the gate and the write's header (SC-2 condition 5: nothing may
    /// wait there, no I/O and no log note, the first review of SC-2 item 10, major 1): the note is only kept, under the lock.
    /// </summary>
    private void LeaveNote(string text) => _pendingNotes.Add(text);

    private void WriteNote(ITransferSink? log, string text)
    {
        _logger.LogInformation("Live control: {Note}", text);
        try
        {
            log?.Note(text);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.LogWarning(ex, "The transfer log could not take a note");
        }
    }

    private void Fail(int generation, string reason)
    {
        lock (_gate)
        {
            if (_generation != generation)
            {
                return;
            }

            _status = ConnectionStatus.Error;
            _liveSession = null;
            _sessionStop?.Dispose();
            _sessionStop = null;
        }

        ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Error, Mode, Description, reason));
    }

    private void RaiseConnection(ConnectionStatus status) =>
        ConnectionChanged?.Invoke(this, new ConnectionState(status, Mode, Description));
}
