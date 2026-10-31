using SmartFade.Communication;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb;

/// <summary>The session stopped because the console behaved in a way no capture has shown (<see cref="HspReadSession.Fault"/>).</summary>
public sealed class HspSessionFaultException : Exception
{
    public HspSessionFaultException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A Data header went out and its payload did not: the console holds a header announcing bytes that never came, a
/// state no capture shows (what it takes as the payload next is UNKNOWN). Nothing is retried, and the device refuses
/// to connect again until the backend restarts.
/// </summary>
public sealed class HspIncompleteWriteException : IOException
{
    public HspIncompleteWriteException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>The transfer log could not be written: the session ends and nothing more is sent, since every OUT transfer must be logged (SC-1 and SC-2 item 8).</summary>
public sealed class TransferLogException : IOException
{
    public TransferLogException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Drives an <see cref="HspReadSession"/> over an <see cref="IHspPipe"/> at SmartSoft's measured
/// cadence (<see cref="UsbDeviceOptions"/>): a poll about every 31 ms when idle, a fetch about
/// 28 ms after the payload that listed it, the next poll about 15 ms after a fetch, never faster
/// than the observed minimums (the waits are re-checked against the clock after every delay).
/// Every transfer goes to the <see cref="ITransferSink"/>, written after the exchange so that no
/// disk write sits between a poll and its reply. It never retries: a pipe error or a session
/// fault ends the loop with an exception. A stop request is honoured only when the session is
/// idle (nothing listed and unfetched, no fetch without its poll, no press without its release;
/// SmartSoft's own sessions ended one step later, on an idle poll, F64); should the console keep
/// listing objects the loop drains up to <see cref="MaxDrainTransfers"/> more exchanges and then
/// stops anyway, noting the forced stop (unreachable with the observed lists, which are drained
/// within one list).
/// <para>
/// A session in write mode (SC-2) may also hand over a Data transfer: the loop writes its header and then its
/// payload at once, both logged like every other transfer, and reads nothing back (F131: no acknowledgement); the
/// next transfer is the usual poll or fetch. A write takes the poll's slot and keeps SmartSoft's spacing (OBSERVED minimums, E9 and E9b, `write_rules.py` 2026-10-31:
/// at least 29.81 ms after the poll, 20.53 ms after the last payload, 44.77 ms after the previous write; the next
/// transfer at least 14.51 ms after it), with the floors of <see cref="UsbDeviceOptions.Floors"/>. A stop sends
/// nothing new: queued writes that have not started are dropped, a press already sent gets its release, and the loop
/// returns only after the poll exchange that follows the last write. When the session ends any other way than by a
/// pipe error (a fault, or an error in an event handler) with a key held, the release is still sent before the loop
/// ends if no exchange is open (SC-2 condition 4); otherwise <see cref="ReleaseAfterFault"/> says it could not be.
/// </para>
/// </summary>
public sealed class HspReadLoop
{
    /// <summary>Transfers allowed after a stop request while the session is not idle (E3b's longest list took 157 fetches × 3 transfers).</summary>
    public const int MaxDrainTransfers = 2000;

    private readonly IHspPipe _pipe;
    private readonly UsbDeviceOptions _options;
    private readonly ITransferSink? _log;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private long? _lastFetch;
    private long? _lastPayload;
    private long? _lastWrite;
    private ClearedKey? _releaseInFlight;

    public HspReadLoop(
        IHspPipe pipe,
        HspReadSession session,
        UsbDeviceOptions options,
        ITransferSink? log,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        long? lastPollStamp = null)
    {
        _pipe = pipe;
        Session = session;
        _options = options;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        _delay = delay ?? ((wait, ct) => Task.Delay(wait, _clock, ct));
        LastPollStamp = lastPollStamp;
    }

    public HspReadSession Session { get; }

    /// <summary>Clock stamp of the last poll sent, carried from session to session so that a reconnect keeps the cadence.</summary>
    public long? LastPollStamp { get; private set; }

    /// <summary>True when the loop stopped on request while the console still owed or listed something.</summary>
    public bool ForcedStop { get; private set; }

    /// <summary>Queued writes that the stop request dropped before they started (a stop sends nothing new).</summary>
    public int WritesDroppedAtStop { get; private set; }

    /// <summary>After a session that ended with a key held: whether its release was sent or why it could not be; null otherwise.</summary>
    public string? ReleaseAfterFault { get; private set; }

    /// <summary>Set when a Data header went out and its payload did not, in any path (the device then refuses to connect again).</summary>
    public string? IncompleteWrite { get; private set; }

    /// <summary>Raised for every event the session decoded, on the loop's thread.</summary>
    public event Action<HspEvent>? EventDecoded;

    /// <summary>Raised on the loop's thread when the session dropped a queued write unsent, with the reason.</summary>
    public event Action<string>? WriteDropped;

    public async Task RunAsync(CancellationToken stop)
    {
        try
        {
            await ExchangeAsync(stop).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_releaseInFlight is { } inFlight)
            {
                // The session already counts the key as released; the console may never have had the release.
                ReleaseAfterFault = $"the release of {inFlight} was being written when the session failed ({ex.Message}): {inFlight} may still be held, press it on the panel";
            }
            else if (ex is not (HspPipeException or HspIncompleteWriteException or TransferLogException))
            {
                // SC-2 condition 4: a press already sent gets its release before the pipe closes, when the pipe is
                // alive. After a pipe error nothing more is written, nor after a log error (it could not be logged).
                await ReleaseHeldKeyAfterFaultAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task ExchangeAsync(CancellationToken stop)
    {
        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        var drained = 0;
        var stopping = false;
        string? notedHold = null;
        var notedDrops = 0;
        var pendingRecords = new List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)>(4);

        while (true)
        {
            if (stop.IsCancellationRequested)
            {
                if (!stopping)
                {
                    stopping = true;
                    WritesDroppedAtStop = Session.DiscardQueuedWrites(acceptNoMore: true);
                    if (WritesDroppedAtStop > 0)
                    {
                        Note($"stop: {WritesDroppedAtStop} queued write(s) dropped before they started");
                    }
                }

                if (Session.IsIdle)
                {
                    return;
                }

                if (++drained > MaxDrainTransfers)
                {
                    ForcedStop = true;
                    Note($"forced stop: the console still listed {Session.PendingFetches} object(s) after {MaxDrainTransfers} transfers (deviation)");
                    return;
                }
            }

            var next = Session.Next();
            if (Session.WriteHeldReason != notedHold)
            {
                notedHold = Session.WriteHeldReason;
                if (notedHold is not null)
                {
                    Note($"write held back: {notedHold}");
                }
            }

            if (Session.WritesDropped != notedDrops)
            {
                notedDrops = Session.WritesDropped;
                Note($"queued write dropped unsent: {Session.LastDropReason}");
                WriteDropped?.Invoke(Session.LastDropReason ?? "dropped");
            }

            await WaitForSlotAsync(next.Kind).ConfigureAwait(false);

            try
            {
                if (next.IsWrite)
                {
                    // SC-2 conditions 7 and 8: the header, then its payload, both logged; nothing is read back (F131).
                    WriteData(next, pendingRecords);
                    continue;
                }

                var started = _clock.GetUtcNow();
                _pipe.Write(next.Bytes);
                var stamp = _clock.GetTimestamp();
                pendingRecords.Add((true, next.Bytes, started, _clock.GetUtcNow()));

                if (next.Kind == HspMessageType.Fetch)
                {
                    _lastFetch = stamp;
                    continue;
                }

                LastPollStamp = stamp;
                while (Session.ExpectedReadLength > 0)
                {
                    var length = Session.ExpectedReadLength;
                    var wasPayload = Session.State == HspReadSessionState.AwaitingPayload;
                    var span = buffer.AsSpan(0, length);
                    var readStarted = _clock.GetUtcNow();
                    _pipe.ReadExactly(span);
                    var data = span.ToArray();
                    pendingRecords.Add((false, data, readStarted, _clock.GetUtcNow()));

                    var events = Session.Receive(data);
                    if (wasPayload)
                    {
                        _lastPayload = _clock.GetTimestamp();
                    }

                    foreach (var evt in events)
                    {
                        EventDecoded?.Invoke(evt);
                    }

                    if (Session.State == HspReadSessionState.Faulted)
                    {
                        throw new HspSessionFaultException(Session.Fault ?? "unknown fault");
                    }
                }
            }
            finally
            {
                Flush(pendingRecords);
            }
        }
    }

    /// <summary>
    /// Waits for the transfer's slot. A poll keeps the poll cadence and never comes sooner after a fetch or a write
    /// than observed; a fetch keeps its delay after the payload that listed it; a write takes the poll's slot and
    /// keeps SmartSoft's spacing from the poll, the last payload, a fetch and the previous write.
    /// </summary>
    private Task WaitForSlotAsync(HspMessageType kind) => kind switch
    {
        HspMessageType.Poll => WaitAsync((LastPollStamp, _options.PollInterval), (_lastFetch, _options.PollAfterFetch), (_lastWrite, _options.PollAfterWrite)),
        HspMessageType.Fetch => WaitAsync((_lastPayload, _options.FetchDelay), (_lastWrite, _options.PollAfterWrite)),
        HspMessageType.Data => WaitAsync((LastPollStamp, _options.WriteAfterPoll), (_lastPayload, _options.FetchDelay), (_lastFetch, _options.PollAfterFetch), (_lastWrite, _options.WriteInterval)),
        _ => throw new InvalidOperationException($"The session asked for a {kind} transfer."),
    };

    /// <summary>
    /// A Data transfer: the header, then the payload at once (OBSERVED: SmartSoft's payload followed its header after 0.05–0.32 ms,
    /// E9), with no disk write between them. A payload that fails after its header was sent ends the session with that
    /// said: the console then holds a header whose payload never came, and nothing is retried (SC-2 item 7).
    /// </summary>
    private void WriteData(HspOutboundTransfer write, List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)> records)
    {
        var payload = write.Payload ?? throw new InvalidOperationException("A Data transfer came without its payload.");

        // A key release (kind 1, state 0, F133) is in flight until both transfers are out.
        _releaseInFlight = payload.Length == 7 && payload[4] == 0x01 && payload[6] == 0x00 ? (ClearedKey)payload[5] : null;
        var started = _clock.GetUtcNow();
        _pipe.Write(write.Bytes);
        records.Add((true, write.Bytes, started, _clock.GetUtcNow()));

        started = _clock.GetUtcNow();
        try
        {
            _pipe.Write(payload);
        }
        catch (Exception ex)
        {
            var incomplete = $"the Data header {Convert.ToHexString(write.Bytes)} was sent and its {payload.Length}-byte payload was not: {ex.Message}";
            IncompleteWrite = incomplete;
            throw new HspIncompleteWriteException(incomplete, ex);
        }

        records.Add((true, payload, started, _clock.GetUtcNow()));
        _lastWrite = _clock.GetTimestamp();
        _releaseInFlight = null;
    }

    /// <summary>Best effort when the session ends with a key held: sends the release when the session allows it, and records the outcome; never throws.</summary>
    private async Task ReleaseHeldKeyAfterFaultAsync()
    {
        if (Session.HeldKey is not { } key)
        {
            return;
        }

        var records = new List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)>(2);
        string outcome;
        try
        {
            if (Session.TakeReleaseAtEnd() is { } release)
            {
                await WaitForSlotAsync(HspMessageType.Data).ConfigureAwait(false);
                WriteData(release, records);
                outcome = $"the release of {key} was sent as the session ended";
            }
            else
            {
                outcome = $"{key} was pressed and its release could not be sent (the session ended with an exchange open): press {key} on the panel";
            }
        }
        catch (Exception ex)
        {
            outcome = $"{key} was pressed and its release could not be sent ({ex.Message}): press {key} on the panel";
        }

        try
        {
            Flush(records);
            _log?.Note(outcome);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            outcome += $"; the transfer log could not record it ({ex.Message})";
        }

        ReleaseAfterFault = outcome;
    }

    private void Flush(List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)> records)
    {
        try
        {
            if (_log is not null)
            {
                foreach (var (toConsole, data, started, completed) in records)
                {
                    _log.Transfer(toConsole, data, started, completed);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new TransferLogException($"the transfer log could not be written: {ex.Message}", ex);
        }
        finally
        {
            records.Clear();
        }
    }

    private void Note(string text)
    {
        try
        {
            _log?.Note(text);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new TransferLogException($"the transfer log could not be written: {ex.Message}", ex);
        }
    }

    /// <summary>Waits until every (reference stamp, interval) constraint has elapsed on the clock; a null stamp imposes nothing.</summary>
    private async Task WaitAsync(params (long? Stamp, TimeSpan Interval)[] constraints)
    {
        while (true)
        {
            var wait = TimeSpan.Zero;
            foreach (var (stamp, interval) in constraints)
            {
                if (stamp is { } reference)
                {
                    var remaining = interval - _clock.GetElapsedTime(reference);
                    if (remaining > wait)
                    {
                        wait = remaining;
                    }
                }
            }

            if (wait <= TimeSpan.Zero)
            {
                return;
            }

            // Never cancelled: a stop is honoured between exchanges, when the session is idle. A
            // remainder under a millisecond still waits a millisecond, so an early timer never
            // turns this into a spin.
            await _delay(wait < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : wait, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
