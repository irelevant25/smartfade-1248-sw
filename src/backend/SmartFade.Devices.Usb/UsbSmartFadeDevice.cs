using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb;

/// <summary>
/// The real SmartFade 1248 over USB, read-only. It runs the poll / listed-fetch session that
/// safety check SC-1 cleared (docs/protocol/safety-checks.md) and reports what the console
/// delivers; it accepts no command (<see cref="SupportedCommands"/> is None: no message that
/// changes the console is cleared for sending). Every transfer is logged to a capture file in
/// our normalized format. Any error ends the session in the Error state with its reason; nothing
/// is retried. A stop waits for the session to reach an idle point (a fetch is never left without
/// its poll), and Connect is refused while a session is still running. The pipe comes from a
/// factory so that tests run the same device over a committed capture (<see cref="ReplayHspPipe"/>)
/// and never over hardware.
/// </summary>
public sealed class UsbSmartFadeDevice : ISmartFadeDevice, IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);

    private readonly object _gate = new();
    private readonly UsbDeviceOptions _options;
    private readonly Func<IHspPipe> _pipeFactory;
    private readonly Func<string, ITransferSink?> _logFactory;
    private readonly ILogger<UsbSmartFadeDevice> _logger;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delay;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private ConsoleStateAssembler _assembler;
    private CancellationTokenSource? _sessionStop;
    private Task? _session;
    private long? _lastPollStamp;
    private int _generation;
    private bool _stopTimedOut;

    public UsbSmartFadeDevice(
        IOptions<UsbDeviceOptions> options,
        Func<IHspPipe> pipeFactory,
        Func<string, ITransferSink?> logFactory,
        ILogger<UsbSmartFadeDevice> logger,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _options = options.Value;
        _pipeFactory = pipeFactory;
        _logFactory = logFactory;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _delay = delay;
        Capabilities = ConsoleCapabilities.SmartFade1248;
        _assembler = new ConsoleStateAssembler(Capabilities);
    }

    public DeviceMode Mode => DeviceMode.Hardware;

    /// <summary>No transport detail here (the UI never sees transports); the device path goes to the log.</summary>
    public string Description => "SmartFade 1248, read-only";

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

    /// <summary>None: the console is only read (SC-1 clears the poll and the listed fetch, nothing that changes state).</summary>
    public DeviceCommands SupportedCommands => DeviceCommands.None;

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

            _status = ConnectionStatus.Connecting;
            generation = ++_generation;
            _assembler = new ConsoleStateAssembler(Capabilities);
        }

        RaiseConnection(ConnectionStatus.Connecting);

        IHspPipe pipe;
        try
        {
            pipe = _pipeFactory();
            pipe.Open();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The console could not be opened");
            Fail(generation, $"Cannot open the console: {WithoutDevicePath(ex.Message)}");
            return Task.CompletedTask;
        }

        var stop = new CancellationTokenSource();
        lock (_gate)
        {
            if (_generation != generation)
            {
                pipe.Dispose();
                stop.Dispose();
                return Task.CompletedTask;
            }

            _sessionStop = stop;
            _status = ConnectionStatus.Connected;
        }

        // Connected is reported before the first transfer, so that a session that fails at once
        // still ends in the Error state after it (never Error, then Connected).
        _logger.LogWarning("HARDWARE MODE: read-only session starting on {Pipe}; only the poll and the listed fetch are sent (SC-1)", pipe.Description);
        RaiseConnection(ConnectionStatus.Connected);
        lock (_gate)
        {
            if (_generation != generation)
            {
                pipe.Dispose();
                stop.Dispose();
                return Task.CompletedTask;
            }

            _session = Task.Run(() => RunSessionAsync(pipe, generation, stop.Token), CancellationToken.None);
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

    public Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetMasterAsync(byte level, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetBumpsAsync(byte level, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetSoloAsync(bool on, CancellationToken cancellationToken) => throw ReadOnly();

    public Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken) => throw ReadOnly();

    public Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken) => throw ReadOnly();

    public Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken) => throw ReadOnly();

    public Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken) => throw ReadOnly();

    public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken) => throw ReadOnly();

    public Task ClearChannelLevelsAsync(CancellationToken cancellationToken) => throw ReadOnly();

    public Task ClearMemoryLevelsAsync(CancellationToken cancellationToken) => throw ReadOnly();

    public Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        MemoryContent? content;
        lock (_gate)
        {
            content = _assembler.Memory(page, fader);
        }

        return content is not null
            ? Task.FromResult(content)
            : throw new NotSupportedException("The console has not delivered this memory in the current read-only session.");
    }

    private static NotSupportedException ReadOnly() =>
        new("The USB device is read-only: no message that changes the console is cleared for sending.");

    private async Task RunSessionAsync(IHspPipe pipe, int generation, CancellationToken stop)
    {
        var session = new HspReadSession();
        LastSession = session;
        LastStopWasForced = false;
        ITransferSink? log = null;
        string? failure = null;
        try
        {
            try
            {
                log = _logFactory(pipe.Description) ?? throw new InvalidOperationException("no transfer sink was provided");
                log.Note($"session start on {pipe.Description}");
            }
            catch (Exception ex)
            {
                // The log is evidence (SC-1 item 8): without it the session does not run.
                throw new HspPipeException($"transfer log unavailable: {ex.Message}", ex);
            }

            var loop = new HspReadLoop(pipe, session, _options, log, _clock, _delay, _lastPollStamp);
            loop.EventDecoded += OnEvent;
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
            _logger.LogError(ex, "The USB session ended: {Reason}", ex.Message);
            try
            {
                log?.Note($"session ended: {ex.Message}");
            }
            catch (Exception logEx)
            {
                _logger.LogDebug(logEx, "The session note could not be written");
            }
        }
        finally
        {
            try
            {
                log?.Note($"polls {session.PollsSent}, fetches {session.FetchesSent}, lists {session.ListsReceived}, objects {session.ObjectsReceived}");
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

    /// <summary>Messages shown in the UI name no device path (the path is in the log, F42 / E1 keep the identification).</summary>
    private static string WithoutDevicePath(string message)
    {
        var start = message.IndexOf(@"\?", StringComparison.Ordinal);
        if (start < 0)
        {
            return message;
        }

        var end = message.IndexOfAny(new[] { ' ', ':' }, start);
        return end < 0 ? message[..start] + "the console's interface" : message[..start] + "the console's interface" + message[end..];
    }

    private void OnEvent(HspEvent evt)
    {
        switch (evt)
        {
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
                    _logger.LogWarning(ex, "Object {Id} ({Name}) did not decode", received.Payload.ObjectId, HspObjectIds.Name(received.Payload.ObjectId));
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

    private void Fail(int generation, string reason)
    {
        lock (_gate)
        {
            if (_generation != generation)
            {
                return;
            }

            _status = ConnectionStatus.Error;
            _sessionStop?.Dispose();
            _sessionStop = null;
        }

        ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Error, Mode, Description, reason));
    }

    private void RaiseConnection(ConnectionStatus status) =>
        ConnectionChanged?.Invoke(this, new ConnectionState(status, Mode, Description));
}
