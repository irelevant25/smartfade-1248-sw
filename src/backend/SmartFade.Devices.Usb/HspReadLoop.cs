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
/// A Data header went out, or may have (its transfer failed), and its payload did not: the console may hold a header
/// announcing bytes that never came, a state no capture shows (what it takes as the payload next is UNKNOWN). Nothing
/// is retried, and the device refuses to connect again (<see cref="IncompleteWriteMarker"/>).
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
/// transfer at least 14.51 ms after it), with the floors of <see cref="UsbDeviceOptions.Floors"/>. The loop waits for
/// a write's slot before the session chooses the write (<see cref="HspReadSession.PeekNextKind"/>), so the gate and the
/// write's own condition answer right before its header goes out, never before a wait (SC-2 condition 5). A stop sends
/// nothing new, also when it comes during a write's wait: queued writes that have not started are dropped, a press already
/// sent gets its release, and the loop returns only after the poll exchange that follows the last write. When the
/// session ends any other way than by a pipe error (a fault, or an error in an event handler) with a key held, the
/// release is still sent before the loop ends if no exchange is open (SC-2 condition 4); otherwise
/// <see cref="ReleaseAfterFault"/> says it could not be.
/// </para>
/// <para>
/// A held bump (SC-5 class B3) is bounded here, on the loop's clock, whatever its owner does: once
/// <see cref="MaxHold"/> − <see cref="HoldReleaseLead"/> has passed since its press header, the loop asks the session for
/// its release, which goes at the next write slot (SC-5 condition 2); a release that still went out later than
/// <see cref="MaxHold"/> after its press is noted in the transfer log as a deviation.
/// </para>
/// </summary>
public sealed class HspReadLoop
{
    /// <summary>Transfers allowed after a stop request while the session is not idle (E3b's longest list took 157 fetches × 3 transfers).</summary>
    public const int MaxDrainTransfers = 2000;

    /// <summary>
    /// SC-5 condition 2: the longest a bump stays held from the PC, its press header to its release header. 3 s is the bound
    /// the SC-5 addendum keeps (every longer hold was a single observation of its length: 4.67 s and 3.42 s in E9d, 3.35 s in
    /// E9b; each shown and ended cleanly, F174).
    /// </summary>
    public static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long before <see cref="MaxHold"/> the loop asks for a held bump's release. The release goes at the next write slot,
    /// which came 46.6–76.7 ms after a press in E11 attempt 3 (OBSERVED: a tap's press to its release, our host, the default
    /// timings), so the hold ends by about 2.83 s; only a console answering a poll more than about 170 ms late could carry it
    /// past 3 s (never observed: every reply came within 12 ms up to E12; the reply timeout is 500 ms), and the transfer log
    /// notes such a hold as a deviation.
    /// </summary>
    public static readonly TimeSpan HoldReleaseLead = TimeSpan.FromMilliseconds(250);

    private readonly IHspPipe _pipe;
    private readonly UsbDeviceOptions _options;
    private readonly ITransferSink? _log;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private long? _lastFetch;
    private long? _lastPayload;
    private long? _lastWrite;
    private long _lastWriteHeader;
    private long? _pressHeader;
    private ClearedKey? _pressedKey;
    private ClearedKey? _releaseInFlight;
    private ClearedKey? _pressInFlight;

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

    /// <summary>
    /// After a session that ended with a key held, or with a key's press or release still being written: what became of
    /// it (its release sent, or why it could not be, or that the press itself did not complete); null otherwise.
    /// </summary>
    public string? ReleaseAfterFault { get; private set; }

    /// <summary>Set when a Data header went out, or may have, and its payload did not, in any path (the device then refuses to connect again).</summary>
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
            else if (_pressInFlight is { } pressing)
            {
                // The press itself did not complete: whether the console took it is UNKNOWN, and a press on the panel would
                // toggle or trigger the key again if it did; no release follows a press that may never have arrived.
                ReleaseAfterFault = $"the press of {pressing} was being written when the session failed ({ex.Message}): whether the console took it is UNKNOWN; look at the console before touching {pressing} on the panel, where a press would toggle or trigger it again";
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

            // SC-5 condition 2: a held bump's release is asked for at the bound, whatever its owner does.
            ReleaseHoldAtBound();

            // SC-2 condition 5 (review of item 10, major 1): the slot is waited for first, and only then does the session
            // choose, asking the gate and the write's own condition; between their answer and the header nothing waits.
            var expected = Session.PeekNextKind();
            await WaitForSlotAsync(expected).ConfigureAwait(false);
            if (expected == HspMessageType.Data && stop.IsCancellationRequested && !stopping)
            {
                // A stop that came during a write's wait is handled first: no new write is chosen after it (an owed
                // release still goes). A poll or fetch waited for goes as before (SC-1).
                continue;
            }

            var next = Session.Next(writeSlotMet: expected == HspMessageType.Data);
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

            if (!next.IsWrite)
            {
                // Nothing to wait for when this is the kind waited for (a write's slot also covers a fetch's and a
                // poll's with the default settings); a held or dropped write leaves the fetch or poll its own floors.
                // While a write is held, the transfer that takes its slot has waited for the write's timing, a few ms
                // after its own (30 ms after the poll rather than 28 ms after the payload, the default fetch delay whose
                // floor is 21 ms): slower, never faster.
                await WaitForSlotAsync(next.Kind).ConfigureAwait(false);
            }

            try
            {
                if (next.IsWrite)
                {
                    // SC-2 conditions 7 and 8: the header, then its payload, both logged; nothing is read back (F131).
                    // Its slot was met before the session chose it (writeSlotMet), and no transfer has happened since.
                    WriteData(next, pendingRecords);
                    TrackHold(next.Payload!);
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
    /// said: the console then holds a header whose payload never came, and nothing is retried (SC-2 item 7). A header
    /// whose own transfer fails is counted the same way: whether a failed transfer reached the console is UNKNOWN (a
    /// timeout can fail a transfer the device has taken), so the worse case is assumed.
    /// </summary>
    private void WriteData(HspOutboundTransfer write, List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)> records)
    {
        var payload = write.Payload ?? throw new InvalidOperationException("A Data transfer came without its payload.");

        // A key event (kind 1, F133), a release (state 0) or a press (state 1), is in flight until both transfers are out.
        var keyEvent = payload.Length == 7 && payload[4] == 0x01;
        _releaseInFlight = keyEvent && payload[6] == 0x00 ? (ClearedKey)payload[5] : null;
        _pressInFlight = keyEvent && payload[6] == 0x01 ? (ClearedKey)payload[5] : null;
        var started = _clock.GetUtcNow();
        _lastWriteHeader = _clock.GetTimestamp();
        try
        {
            _pipe.Write(write.Bytes);
        }
        catch (Exception ex)
        {
            var failed = $"the Data header {Convert.ToHexString(write.Bytes)} failed ({ex.Message}); whether it reached the console is UNKNOWN, and its {payload.Length}-byte payload was not sent";
            IncompleteWrite = failed;
            throw new HspIncompleteWriteException(failed, ex);
        }

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
        _pressInFlight = null;
    }

    /// <summary>
    /// SC-5 condition 2, on the loop's clock: once a key whose press this loop wrote has been held for
    /// <see cref="MaxHold"/> − <see cref="HoldReleaseLead"/>, its release is asked for (a no-op for a tap, whose release is
    /// due at once, and for a hold whose release is already due).
    /// </summary>
    private void ReleaseHoldAtBound()
    {
        if (_pressHeader is not { } pressed || Session.HeldKey is not { } key || key != _pressedKey)
        {
            return;
        }

        var held = _clock.GetElapsedTime(pressed);
        if (held >= MaxHold - HoldReleaseLead && Session.ReleaseKey(key))
        {
            Note($"hold bound: the release of {key} is due {held.TotalMilliseconds:F0} ms after its press (SC-5 condition 2: at most {MaxHold.TotalSeconds:F0} s)");
        }
    }

    /// <summary>After a key event went out (F133: kind 1, state 1 press / 0 release): a press starts the hold's clock; a release ends it, noted as a deviation when it came later than <see cref="MaxHold"/>.</summary>
    private void TrackHold(byte[] payload)
    {
        if (payload.Length != 7 || payload[4] != 0x01)
        {
            return;
        }

        var key = (ClearedKey)payload[5];
        if (payload[6] == 0x01)
        {
            _pressHeader = _lastWriteHeader;
            _pressedKey = key;
            return;
        }

        if (_pressHeader is { } pressed && _pressedKey == key)
        {
            var held = _clock.GetElapsedTime(pressed, _lastWriteHeader);
            if (held > MaxHold)
            {
                Note($"deviation: {key} was held {held.TotalMilliseconds:F0} ms, beyond the {MaxHold.TotalSeconds:F0} s bound (SC-5 condition 2)");
            }
        }

        _pressHeader = null;
        _pressedKey = null;
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
