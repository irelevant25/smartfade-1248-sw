using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb;

/// <summary>
/// The real SmartFade 1248 over USB. It runs the poll / listed-fetch session that safety check SC-1 cleared
/// (docs/protocol/safety-checks.md) and reports what the console delivers. By default it is read-only and accepts no
/// command (<see cref="SupportedCommands"/> is None). With live control switched on for the day
/// (<see cref="UsbDeviceOptions.LiveControlDate"/>, SC-2 item 6) it also sends SC-2's whitelist, and of it only what
/// SmartSoft was seen to send from the PC with the record keys off, in the console state it was sent in (E9, E9b;
/// `tools/protocol-analyzer/write_rules.py`): channel fader 1 (channel mode, bank 1-24, MASTER at full), the MASTER and
/// BUMPS (channel mode, bank 1-24) as fader events; bump 7 (channel mode), memory bump 1 (memory mode, the page shown),
/// SOLO and BLACKOUT from a steady LED, GO with STACK dim, MEMS (channel mode, bank 1-24) and FADERS 1-24 (from memory
/// mode or bank 25-48) as taps (press, then release; condition 4). Bump 9, channel bump 1 and memory bump 7 are
/// whitelisted codes never seen in such a state and are not sent. Every command is refused, with its reason, while
/// REC MEM, EDIT MEM, COPY or REC SEQ is lit, blinking or unreported (condition 5) and before the console has reported
/// what the control needs (conditions 6, 8); a queued write is asked again at the moment it would go, after its slot
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
[Evidence(EvidenceStatus.Inferred, "F57, F116", "the interlock's freshness reading: a poll reply within two poll periods that listed no LedStatus means the key LEDs are current (F57: a list names what changed; F116: a record screen opens and closes with exactly one LedStatus payload, 28 of 28 and 4 of 4); not observed as a rule of the console")]
[Evidence(EvidenceStatus.Observed, "write_rules.py 2026-10-31 (E9, E9b)", "the console states SmartSoft sent each whitelisted control in, counted over 828 writes of one PC and one console; the device sends each control only in those states")]
public sealed class UsbSmartFadeDevice : ISmartFadeDevice, IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);

    /// <summary>The port commands live control accepts (SC-2 item 6); each one refuses every control outside the whitelist.</summary>
    private const DeviceCommands LiveCommands = DeviceCommands.SetChannelLevel | DeviceCommands.SetMaster | DeviceCommands.SetBumps
        | DeviceCommands.SetBlackout | DeviceCommands.SetSolo | DeviceCommands.BumpChannel | DeviceCommands.BumpMemory;

    /// <summary>The controls the command methods send, by name (docs/api.md `writableControls`): channel fader 1, MASTER, BUMPS, bump 7, memory bump 1 of every page, SOLO, BLACKOUT.</summary>
    private static readonly IReadOnlySet<string> LiveControls = new HashSet<string>(
        new[] { DeviceControls.ChannelFader(1), DeviceControls.Master, DeviceControls.Bumps, DeviceControls.ChannelBump(7), DeviceControls.Solo, DeviceControls.Blackout }
            .Concat(Enumerable.Range(0, 12).Select(page => DeviceControls.MemoryBump(1 + 24 * page))));

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
    private readonly Dictionary<ClearedKey, KeyLed> _togglesAwaitingLed = new();
    private readonly HashSet<ClearedKey> _togglesSent = new();
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
            _lastReportStamp = null;
            _togglesAwaitingLed.Clear();
            _togglesSent.Clear();
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
    /// Channel fader 1 only (index 0x00, SC-2 class A): in channel mode, bank 1-24, with the MASTER at full, as all 215
    /// of SmartSoft's events whose MASTER was known (OBSERVED, E9 + E9b, `write_rules.py` 2026-10-31). With the MASTER at full the reported channel level is the
    /// fader's own (F96, OBSERVED: the levels are post-MASTER), so the UI's start from the reported level (condition 6) holds.
    /// </summary>
    public Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken)
    {
        var session = LiveSession();
        if (channel != 1)
        {
            throw NotCleared($"Channel fader {channel} is not cleared: only channel fader 1 (index 0x00) is (SC-2 class A)");
        }

        return QueueLiveFader(session, ClearedFader.ChannelFader1, level, () => NotInChannelBank1To24("channel fader 1") ?? MasterNotAtFull(), holdWhileFaderInfoListed: true);
    }

    /// <summary>The MASTER (index 0x30, SC-2 class A), in channel mode, bank 1-24 as every PC event; it changes the whole live output, as the panel's MASTER does.</summary>
    public Task SetMasterAsync(byte level, CancellationToken cancellationToken) =>
        QueueLiveFader(LiveSession(), ClearedFader.Master, level, () => NotInChannelBank1To24("the MASTER"));

    /// <summary>BUMPS (index 0x31, SC-2 class A), in channel mode, bank 1-24 as every PC event.</summary>
    public Task SetBumpsAsync(byte level, CancellationToken cancellationToken) =>
        QueueLiveFader(LiveSession(), ClearedFader.Bumps, level, () => NotInChannelBank1To24("BUMPS"));

    /// <summary>BLACKOUT as a toggle: one tap, only from a steady LED (block 53) that differs from the wish, in channel mode, bank 1-24 (SC-2 class B1).</summary>
    public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken) => Toggle(ClearedKey.Blackout, on, keys => keys.Blackout, "BLACKOUT");

    /// <summary>SOLO as a toggle: one tap, only from a steady LED (block 52) that differs from the wish, in channel mode, bank 1-24 (SC-2 class B1).</summary>
    public Task SetSoloAsync(bool on, CancellationToken cancellationToken) => Toggle(ClearedKey.Solo, on, keys => keys.Solo, "SOLO");

    /// <summary>
    /// Bump 7 in channel mode, bank 1-24, as a tap (SC-2 condition 4: press and release are one operation, no held
    /// bump): a press sends the tap, a release adds nothing. Bumps 1 and 9 are whitelisted codes that SmartSoft never
    /// sent in channel mode with the record keys off (E9, E9b), so they are not sent.
    /// </summary>
    public Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken)
    {
        var session = LiveSession();
        var key = channel switch
        {
            7 => ClearedKey.Bump7,
            1 => throw NotCleared("Channel bump 1 is not sent: code 0x00 was seen from a PC with the record keys off only in memory mode, as memory bump 1 (E9, E9b)"),
            9 => throw NotCleared(Bump9NotSeen),
            _ => throw NotCleared($"The bump of channel {channel} is not cleared: of the channel bumps, only bump 7 is sent (SC-2 class B1, as seen from SmartSoft)"),
        };

        return pressed ? QueueLiveTap(session, key, () => NotInChannelBank1To24($"bump {channel}")) : Task.CompletedTask;
    }

    public Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken) => throw NotCleared();

    /// <summary>
    /// Memory bump 1 of the page the console shows in memory mode, as a tap (code 0x00, as SmartSoft's memory bump 1,
    /// F134); a release adds nothing. Memory bumps 7 and 9 were never sent from a PC in memory mode with the record keys
    /// off (E9, E9b), so they are not sent.
    /// </summary>
    public Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken)
    {
        var session = LiveSession();
        var key = fader switch
        {
            1 => ClearedKey.Bump1,
            7 => throw NotCleared("Memory bump 7 is not sent: code 0x06 was seen from a PC with the record keys off only in channel mode (E9, E9b)"),
            9 => throw NotCleared(Bump9NotSeen),
            _ => throw NotCleared($"The bump of memory fader {fader} is not cleared: of the memory bumps, only memory bump 1 is sent (SC-2 class B1, as seen from SmartSoft)"),
        };

        return pressed ? QueueLiveTap(session, key, () => NotOnMemoryPage(page)) : Task.CompletedTask;
    }

    public Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken) => throw NotCleared();

    public Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken) => throw NotCleared();

    public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken) => throw NotCleared();

    public Task ClearChannelLevelsAsync(CancellationToken cancellationToken) => throw NotCleared();

    public Task ClearMemoryLevelsAsync(CancellationToken cancellationToken) => throw NotCleared();

    /// <summary>
    /// GO (code 0x31, SC-2 class B1) as a tap, in channel mode, bank 1-24, while STACK's LED reads dim and steady: both
    /// PC presses of E9 and E9b came then (F134: GO from a PC only with STACK unlit). Not on <see cref="ISmartFadeDevice"/>
    /// yet: the port needs its own method before the API and the UI can reach it.
    /// </summary>
    public Task PressGoAsync(CancellationToken cancellationToken) =>
        QueueLiveTap(LiveSession(), ClearedKey.Go, () => NotInChannelBank1To24("GO") ?? StackNotAsObserved());

    /// <summary>FADERS MEMS (code 0x39) as a tap, from channel mode, bank 1-24, as every PC press. Not on <see cref="ISmartFadeDevice"/> yet.</summary>
    public Task PressFadersMemsAsync(CancellationToken cancellationToken) =>
        QueueLiveTap(LiveSession(), ClearedKey.FadersMems, () => NotInChannelBank1To24("FADERS MEMS"));

    /// <summary>FADERS 1-24 (code 0x36) as a tap, from memory mode or from bank 25-48, as every PC press. Not on <see cref="ISmartFadeDevice"/> yet.</summary>
    public Task PressFaders1To24Async(CancellationToken cancellationToken) =>
        QueueLiveTap(LiveSession(), ClearedKey.Faders1To24, NotFromMemoryModeOrBank25To48);

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

    private const string Bump9NotSeen = "Bump 9 is not sent: code 0x08 was seen from a PC only inside REC MEM, EDIT MEM or COPY (E9, E9b), which SC-2 condition 5 forbids";

    private static NotSupportedException ReadOnly() =>
        new("The USB device is read-only: no message that changes the console is cleared for sending.");

    /// <summary>Everything outside what live control sends: read-only's refusal, or live control's, which names what it sends.</summary>
    private NotSupportedException NotCleared(string? what = null) =>
        !IsLiveControlConfirmed()
            ? ReadOnly()
            : new NotSupportedException($"{what ?? "This command is not cleared"}. Live control sends only channel fader 1, MASTER, BUMPS, bump 7, memory bump 1, GO, SOLO, BLACKOUT, FADERS 1-24 and MEMS (SC-2, as seen from SmartSoft).");

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

    /// <summary>Queues a fader event behind the interlocks: refused now with the reason, else asked again at the moment it would go.</summary>
    private Task QueueLiveFader(HspReadSession session, ClearedFader fader, byte level, Func<string?> state, bool holdWhileFaderInfoListed = false)
    {
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? FadersUnreported() ?? state());
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
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? state());
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
    /// channel fader 1 (the MASTER). It changes nothing: what follows a send or a drop is the session's to report.
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
    /// SOLO and BLACKOUT are toggles on the panel: one tap changes the state (VERIFIED, F133 / F134: a PC press toggled
    /// the LED in E9 and in E9b). The state is read from the key's LED, and
    /// only from the two readings SmartSoft's presses started from (E9, E9b: steady off `0A 0A 0A …`, steady full
    /// `FF FF FF …` for SOLO, `00 00 FF …` for BLACKOUT); blinking (BLACKOUT blinks while the MASTER is below full, F85),
    /// dim or unreported refuses. A tap still queued when the LED changes (the panel toggled it) is dropped, since it
    /// would undo that; a second toggle waits until the console has reported the first one's LED change, or until the
    /// first tap was dropped or discarded without going out. A tap counts as sent only once the session has taken its
    /// press for sending (its <c>sent</c> callback), never when its condition merely allowed it.
    /// </summary>
    private Task Toggle(ClearedKey key, bool wish, Func<PanelKeys, KeyLed> ledOf, string name)
    {
        var session = LiveSession();
        lock (_gate)
        {
            RequireRunning(session);
            Refuse(RecordFunctionArmed(_assembler.Keys) ?? NotInChannelBank1To24(name));
            if (_togglesAwaitingLed.ContainsKey(key) || session.IsKeyQueued(key) || session.HeldKey == key)
            {
                Refuse($"the previous {name} press has not been reported by the console yet");
            }

            var before = ledOf(_assembler.Keys!);
            if (SteadyState(before) is not { } state)
            {
                Refuse($"{name}'s LED reads {Describe(before)}; a PC press was seen only from a steady off or full LED (E9, E9b)");
                return Task.CompletedTask;
            }

            if (state == wish)
            {
                return Task.CompletedTask;   // already as wished: nothing is sent
            }

            string? StateNow() =>
                NotInChannelBank1To24(name)
                ?? (_assembler.Keys is { } keys && ledOf(keys) == before ? null : $"{name} changed on the console since the request; the tap would undo it");

            // A drop or a discard is forgotten through ForgetUnsentToggles (the loop's WriteDropped, AfterLedReport).
            session.QueueKeyTap(key, () => Recheck(session, StateNow, holdWhileFaderInfoListed: false), sent: () => ToggleSent(key));
            _togglesAwaitingLed[key] = before;
            _togglesSent.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>The press of a SOLO / BLACKOUT tap was taken for sending: its LED change is now what the next toggle waits for.</summary>
    private void ToggleSent(ClearedKey key)
    {
        lock (_gate)
        {
            _togglesSent.Add(key);
        }
    }

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

    private string? MasterNotAtFull() => _assembler.Masters switch
    {
        null => "the console has not reported the MASTER yet (FaderInfo)",
        { Master: 0xFF } => null,
        { Master: var master } => $"the MASTER reads 0x{master:X2}: channel fader 1 was sent from a PC only with the MASTER at full (E9, E9b), where its reported level is its own (SC-2 condition 6)",
    };

    /// <summary>The state SmartSoft sent the MASTER, BUMPS, channel fader 1, bump 7, GO, SOLO, BLACKOUT and MEMS in (E9, E9b): channel mode, bank 1-24 (F98; the readings H53).</summary>
    private string? NotInChannelBank1To24(string control) => _assembler.FaderMode switch
    {
        null => $"the console has not reported its fader mode yet (ConsoleStatus; a fader-mode key pressed on the panel reports it): {control} is sent only in channel mode, bank 1-24",
        { MemoryMode: true } => $"the console is in memory mode: {control} was sent from a PC only in channel mode, bank 1-24 (E9, E9b)",
        { Bank25To48: true } => $"the console shows bank 25-48: {control} was sent from a PC only in channel mode, bank 1-24 (E9, E9b)",
        _ => null,
    };

    /// <summary>Memory bump 1 as SmartSoft sent it: memory mode, bank 1-24 (E9, E9b), on the page the UI means (F113).</summary>
    private string? NotOnMemoryPage(int page) => _assembler.FaderMode switch
    {
        null => "the console has not reported its fader mode yet (ConsoleStatus)",
        { MemoryMode: false } => "the console is in channel mode: memory bump 1 is sent only in memory mode",
        { Bank25To48: true } => "the console shows bank 25-48: memory bump 1 was sent from a PC only in bank 1-24 (E9, E9b)",
        { MemoryPage: var shown } when shown != page => $"the console shows memory page {shown}, not {page}",
        _ => null,
    };

    /// <summary>FADERS 1-24 as SmartSoft sent it: from memory mode or from bank 25-48 (E9, E9b), never as a repeat in channel mode, bank 1-24.</summary>
    private string? NotFromMemoryModeOrBank25To48() => _assembler.FaderMode switch
    {
        null => "the console has not reported its fader mode yet (ConsoleStatus)",
        { MemoryMode: false, Bank25To48: false } => "the console already shows channel mode, bank 1-24: FADERS 1-24 was sent from a PC only from memory mode or bank 25-48 (E9, E9b)",
        _ => null,
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
                ForgetUnsentToggles(session);
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
    /// read-only session logs it and goes on, as SC-1 reviewed it.
    /// </summary>
    private void OnEvent(HspEvent evt, bool live)
    {
        switch (evt)
        {
            case ReplyHeaderReceived { Header.MessageType: HspMessageType.PollReply }:
                // the console's report of what changed (an idle reply or a list; F57, VERIFIED): the age live control's gate checks
                lock (_gate)
                {
                    _lastReportStamp = _clock.GetTimestamp();
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

                if (change is not null)
                {
                    StateChanged?.Invoke(this, change);
                }

                break;
            case DirtyListReceived list:
                _logger.LogDebug("Console lists {Count} object(s): {Names}", list.List.Records.Count, string.Join(", ", list.List.Records.Select(r => r.Name)));
                break;
            case UnexpectedTransfer unexpected:
                _logger.LogError("Unobserved behaviour: {Reason}", unexpected.Reason);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// After each LedStatus in a live session: a SOLO / BLACKOUT tap counts as reported once its press has gone out and
    /// its LED reads differently (a tap still queued is dropped by its own condition instead), and when a record function
    /// now reads armed, every queued write that has not started is dropped (SC-2 condition 5; a press already sent keeps
    /// its release).
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

            foreach (var (key, before) in _togglesAwaitingLed.ToArray())
            {
                var now = key == ClearedKey.Solo ? keys.Solo : keys.Blackout;
                if (now != before && _togglesSent.Contains(key))
                {
                    _togglesAwaitingLed.Remove(key);
                    _togglesSent.Remove(key);
                }
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

            ForgetUnsentToggles(live);
        }
    }

    /// <summary>A SOLO / BLACKOUT tap dropped or discarded before its press went out leaves nothing to wait for.</summary>
    private void ForgetUnsentToggles(HspReadSession live)
    {
        lock (_gate)
        {
            foreach (var key in _togglesAwaitingLed.Keys.ToArray())
            {
                if (!_togglesSent.Contains(key) && !live.IsKeyQueued(key) && live.HeldKey != key)
                {
                    _togglesAwaitingLed.Remove(key);
                }
            }
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
