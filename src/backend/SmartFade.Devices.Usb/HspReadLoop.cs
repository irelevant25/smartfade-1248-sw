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
/// Drives an <see cref="HspReadSession"/> over an <see cref="IHspPipe"/> at SmartSoft's measured
/// cadence (<see cref="UsbDeviceOptions"/>): a poll about every 31 ms when idle, a fetch about
/// 28 ms after the payload that listed it, the next poll about 15 ms after a fetch, never faster
/// than the observed minimums (the waits are re-checked against the clock after every delay).
/// Every transfer goes to the <see cref="ITransferSink"/>, written after the exchange so that no
/// disk write sits between a poll and its reply. It never retries: a pipe error or a session
/// fault ends the loop with an exception. A stop request is honoured only when the session is
/// idle (nothing listed and unfetched, no fetch without its poll; SmartSoft's own sessions ended
/// one step later, on an idle poll, F64); should the console keep listing objects the loop drains
/// up to <see cref="MaxDrainTransfers"/> more exchanges and then stops anyway, noting the forced
/// stop (unreachable with the observed lists, which are drained within one list).
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

    /// <summary>Raised for every event the session decoded, on the loop's thread.</summary>
    public event Action<HspEvent>? EventDecoded;

    public async Task RunAsync(CancellationToken stop)
    {
        long? lastFetch = null;
        long? lastPayload = null;
        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        var drained = 0;
        var pendingRecords = new List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)>(4);

        while (true)
        {
            if (stop.IsCancellationRequested)
            {
                if (Session.IsIdle)
                {
                    return;
                }

                if (++drained > MaxDrainTransfers)
                {
                    ForcedStop = true;
                    _log?.Note($"forced stop: the console still listed {Session.PendingFetches} object(s) after {MaxDrainTransfers} transfers (deviation)");
                    return;
                }
            }

            var next = Session.Next();
            // TODO (SC-2 conditions 7 and 8): a Data transfer from the session is written as its header and then its
            // payload, both logged like every other transfer; nothing is read back (F131: no acknowledgement) and the
            // next transfer is the usual poll at the usual cadence.
            if (next.Kind == HspMessageType.Poll)
            {
                await WaitAsync((LastPollStamp, _options.PollInterval), (lastFetch, _options.PollAfterFetch)).ConfigureAwait(false);
            }
            else
            {
                await WaitAsync((lastPayload, _options.FetchDelay)).ConfigureAwait(false);
            }

            try
            {
                var started = _clock.GetUtcNow();
                _pipe.Write(next.Bytes);
                var stamp = _clock.GetTimestamp();
                pendingRecords.Add((true, next.Bytes, started, _clock.GetUtcNow()));

                if (next.Kind == HspMessageType.Fetch)
                {
                    lastFetch = stamp;
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
                        lastPayload = _clock.GetTimestamp();
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

    private void Flush(List<(bool ToConsole, byte[] Data, DateTimeOffset Started, DateTimeOffset Completed)> records)
    {
        if (_log is null)
        {
            records.Clear();
            return;
        }

        foreach (var (toConsole, data, started, completed) in records)
        {
            _log.Transfer(toConsole, data, started, completed);
        }

        records.Clear();
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
