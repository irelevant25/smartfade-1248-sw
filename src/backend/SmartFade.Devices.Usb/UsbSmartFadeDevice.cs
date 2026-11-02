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
/// (conditions 6, 8); a queued write is asked again at the moment it would go, after its slot
/// wait and right before its header (the gate, then its own state), and dropped unsent if the console's state no longer
/// allows it. In a live session a key-LED, fader-mode or FaderInfo report that does not decode ends the session. Every
/// transfer is logged to a capture file in our normalized format. Any error ends the session in the Error state with its
/// reason; nothing is retried. A stop waits for the session to reach an idle point (a fetch is never left without its
/// poll, a press never without its release, a write never without the poll after it), and Connect is refused while a
/// session is still running, or after a Data header went out (or may have) without its payload: until the backend
/// restarts, and while the <see cref="IncompleteWriteMarker"/> it was given exists. The pipe comes from a factory so
/// that tests run the same device over a committed capture (<see cref="ReplayHspPipe"/>) and never over hardware.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F55, F57", "the read-only session as SC-1 cleared it (2026-10-30)")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133, F134", "live control, SC-2 classes A and B1 (2026-10-31): the whitelisted ConsoleEvents as SmartSoft's and the console's behaviour; a conditional GO, nothing sent before E11 on the owner's go")]
[Evidence(EvidenceStatus.Verified, "F171, F172 (in part), F174, F175", "live control, SC-5 classes A2-A4, B3-B5 (2026-11-02, with the addenda): every physical fader and bump in three banks, the bank keys, IND 1 / 2, the automatic bank switch and the matching event; a conditional GO, nothing sent before the review and E11b on the owner's go")]
[Evidence(EvidenceStatus.Inferred, "F57, F116", "the interlock's freshness reading: a poll reply within two poll periods that listed no LedStatus means the key LEDs are current (F57: a list names what changed; F116: a record screen opens and closes with exactly one LedStatus payload, 28 of 28 and 4 of 4); not observed as a rule of the console")]
[Evidence(EvidenceStatus.Observed, "write_rules.py 2026-10-31 (E9, E9b); E9c, E9d, E9e", "the console states SmartSoft sent each whitelisted control in; the device sends each control only in those states")]
public sealed class UsbSmartFadeDevice : ISmartFadeDevice, IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);

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
    public UsbSmartFadeDevice(
        IOptions<UsbDeviceOptions> options,
        Func<IHspPipe> pipeFactory,
        Func<string, ITransferSink?> logFactory,
        ILogger<UsbSmartFadeDevice> logger,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        IncompleteWriteMarker? incompleteWriteMarker = null)
    {
        _options = options.Value;
        _pipeFactory = pipeFactory;
        _logFactory = logFactory;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _delay = delay;
        _marker = incompleteWriteMarker;
        Capabilities = ConsoleCapabilities.SmartFade1248;
        _assembler = new ConsoleStateAssembler(Capabilities);
    }

    public DeviceMode Mode => DeviceMode.Hardware;

    /// <summary>No transport detail here (the UI never sees transports); the device path goes to the log.</summary>
    public string Description => IsLiveControlConfirmed() ? "SmartFade 1248, live control" : "SmartFade 1248, read-only";

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
                : "read-only session (SC-1): polls and listed fetches only");
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

        var session = liveControl ? new HspReadSession(LiveWriteGate) : new HspReadSession();
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
            _logger.LogWarning("HARDWARE MODE: read-only session starting on {Pipe}; only the poll and the listed fetch are sent (SC-1)", pipe.Description);
        }

        RaiseConnection(ConnectionStatus.Connected);
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
        }

        stop?.Cancel();
        var ended = session is null;
        if (session is not null)
        {
            try
            {
                await session.WaitAsync(StopTimeout, cancellationToken).ConfigureAwait(false);
                ended = true;
            }
            catch (TimeoutException)
            {
                _logger.LogError("The USB session did not stop within {Timeout}; the pipe stays owned by it", StopTimeout);
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
            _logger.LogInformation("USB session closed");
            RaiseConnection(ConnectionStatus.Disconnected);
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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported() ?? SwitchInProgress() ?? state());
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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? state());
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
    /// Channel c's bank and physical fader: channels 1–24 are physical faders 1–24 in bank 1-24, channels 25–48 the same
    /// faders in bank 25-48 (F171 VERIFIED: in bank 25-48 the fader labelled N moved ChannelLevels[N] through index N − 25,
    /// both E9d passes). The physical number names the whitelist member through <see cref="PhysicalControls"/>' table.
    /// </summary>
    private static (FaderBank Bank, int Physical) ChannelPosition(int channel) =>
        channel <= 24 ? (FaderBank.Channels1To24, channel) : (FaderBank.Channels25To48, channel - 24);

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
    /// SC-5 condition 1 (d), F172: the level a re-assigned physical fader is matched at, the target's reported level. A channel:
    /// ChannelLevels[c], and only when FaderInfo record k byte 0 (the physical fader's post-MASTER level, F82, F96; with the
    /// MASTER at full its own) reads the same, so that both reports name one level and no other source (a memory, a bump, the
    /// stack) drives the channel; a memory fader: FaderInfo record k bytes 0 and 1, equal (F112: equal in all 26 memory-mode
    /// samples; H71). Null with the reason when the level is not known or not one.
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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported() ?? MasterNotAtFull(target.Name));
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
                session.QueueFader(fader, level, () => Recheck(session, () => NotIn(target) ?? MasterNotAtFull(target.Name), holdWhileFaderInfoListed: true));
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
            var reason = ReferenceEquals(_liveSession, session) ? NotIn(target) ?? MasterNotAtFull(target.Name) : "the live-control session is ending";
            if (reason is null)
            {
                var (now, why) = MatchingLevel(target);
                reason = why ?? (now == level ? null : $"{target.Name}'s reported level changed from 0x{level:X2} to 0x{now:X2} since its matching event was queued; the next drag matches it again");
            }

            return reason is null ? HspWriteDecision.Send : HspWriteDecision.Drop(reason);
        }
    }

    /// <summary>
    /// The matching event was taken for sending (on the session's thread): the fader counts as matched, and the UI's latest
    /// level, when it differs, is queued behind it ("then the drag", SC-5 condition 1 d). Never throws: a write it follows
    /// is already committed.
    /// </summary>
    private void MatchingSent(HspReadSession session, ClearedFader fader, Target target, byte level)
    {
        string note;
        lock (_gate)
        {
            _matched.Add(fader);
            note = $"matched {target.Name} at its reported level 0x{level:X2} (SC-5 condition 1 d, F172)";
            if (_pendingLevels.TryGetValue(fader, out var wish) && wish.Target == target)
            {
                _pendingLevels.Remove(fader);
                if (wish.Level != level)
                {
                    try
                    {
                        session.QueueFader(fader, wish.Level, () => Recheck(session, () => NotIn(target) ?? MasterNotAtFull(target.Name), holdWhileFaderInfoListed: true));
                    }
                    catch (InvalidOperationException ex)
                    {
                        note += $"; the UI's level 0x{wish.Level:X2} was not queued ({ex.Message})";
                    }
                }
            }
        }

        SessionNote(note);
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

    /// <summary>The switch key's press was taken for sending (the session's thread): its report is now awaited, within <see cref="BankSwitchReportBound"/>.</summary>
    private void SwitchKeySent(BankSwitch sw)
    {
        lock (_gate)
        {
            sw.KeySent = _clock.GetTimestamp();
        }

        SessionNote($"{sw.Kind} (SC-5 condition 1) for {sw.Reason}: {KeyName(sw.Key)} sent, from {Describe(sw.From)} to {Describe(sw.Target)}; nothing else goes until the console reports it");
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
                var (level, reason) = (NotIn(pending.Target) ?? MasterNotAtFull(pending.Target.Name)) is { } state ? (null, state) : MatchingLevel(pending.Target);
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
            Refuse(RecordFunctionArmed(_assembler.Keys)
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

    /// <summary>Queues a bump as a hold (released by the UI or the loop's 3 s bound, <see cref="HspReadLoop.MaxHold"/>) or, when the UI already let go, as a tap; asked again right before it goes.</summary>
    private void QueueBump(HspReadSession session, HeldBump bump, bool tap)
    {
        if (tap)
        {
            session.QueueKeyTap(bump.Key, () => Recheck(session, () => NotIn(bump.Target), holdWhileFaderInfoListed: false));
            return;
        }

        session.QueueKeyHold(bump.Key, () => Recheck(session, () => NotIn(bump.Target), holdWhileFaderInfoListed: false));
        _hold = bump;
    }

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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? (ReportedBank() is { } bank ? BankKeyNotFrom(key, bank) : WhyNoBank(KeyName(key)))
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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? SwitchInProgress() ?? NotInChannelBank1To24(name));
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
            var live = session.CanWrite;
            loop.EventDecoded += evt => OnEvent(evt, live);
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
            lock (_gate)
            {
                if (ReferenceEquals(_liveSession, session))
                {
                    _liveSession = null;
                }

                if (ReferenceEquals(_liveLog, log))
                {
                    _liveLog = null;
                    ResetLiveState();
                }
            }

            try
            {
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
        }

        if (failure is not null)
        {
            Fail(generation, $"Session ended: {failure}");
        }

        ReportLateEnd();
    }

    /// <summary>A session that outlived its Disconnect (StopTimeout) reports its real end here.</summary>
    private void ReportLateEnd()
    {
        lock (_gate)
        {
            if (!_stopTimedOut)
            {
                return;
            }

            _stopTimedOut = false;
            _session = null;
            _status = ConnectionStatus.Disconnected;
        }

        _logger.LogInformation("The USB session that outlived its Disconnect has now ended");
        RaiseConnection(ConnectionStatus.Disconnected);
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
    /// session as a deviation when the console does not report the new bank in time.
    /// </summary>
    private void OnEvent(HspEvent evt, bool live)
    {
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
                }

                break;
            case ObjectReceived received:
                DeviceStateChange? change;
                try
                {
                    lock (_gate)
                    {
                        change = _assembler.Apply(received);
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
                }

                break;
            case DirtyListReceived list:
                _logger.LogDebug("Console lists {Count} object(s): {Names}", list.List.Records.Count, string.Join(", ", list.List.Records.Select(r => r.Name)));
                if (live)
                {
                    AdvanceSwitch();
                }

                break;
            case UnexpectedTransfer unexpected:
                _logger.LogError("Unobserved behaviour: {Reason}", unexpected.Reason);
                break;
            default:
                break;
        }
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
        }

        if (note is not null)
        {
            SessionNote(note);
        }
    }

    /// <summary>A new session, or the end of one: nothing matched, nothing pending, no switch, no hold (all under <c>_gate</c>).</summary>
    private void ResetLiveState()
    {
        _matched.Clear();
        _pendingLevels.Clear();
        _switch = null;
        _assignment = null;
        _hold = null;
    }

    /// <summary>
    /// A note in the live session's transfer log, written on the session's thread (SC-5 condition 1: the switch is logged as
    /// one operation). It never throws: it may run inside a write's <c>sent</c> callback, after the write was committed; a log
    /// that fails here fails the loop's own next write of it, which ends the session.
    /// </summary>
    private void SessionNote(string text)
    {
        ITransferSink? log;
        lock (_gate)
        {
            log = _liveLog;
        }

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
