namespace SmartFade.Protocol;

/// <summary>What the session wants sent next.</summary>
/// <param name="Bytes">The transfer to write: a poll or fetch header, or a Data header.</param>
/// <param name="Kind">Poll, Fetch or Data.</param>
/// <param name="Payload">For a Data transfer, the payload its header announces, written as the next transfer (F52); null otherwise.</param>
public sealed record HspOutboundTransfer(byte[] Bytes, HspMessageType Kind, byte[]? Payload = null)
{
    /// <summary>A poll is answered by a reply header; a fetch is not (its object comes with the next poll's reply, F53).</summary>
    public bool ExpectsReply => Kind == HspMessageType.Poll;

    /// <summary>A Data write (SC-2): its header, then its payload, as two transfers; nothing answers it (F131).</summary>
    public bool IsWrite => Kind == HspMessageType.Data;
}

/// <summary>What a write gate or a write's own condition says when the session could send a queued write.</summary>
public enum HspWriteVerdict
{
    /// <summary>The write goes now.</summary>
    Send,

    /// <summary>The write waits for a later slot (the state it depends on is not known yet).</summary>
    Hold,

    /// <summary>The write is dropped, never sent (the console's state no longer allows it).</summary>
    Drop,
}

/// <summary>A verdict with its reason (SC-2 conditions 5 and 7).</summary>
public readonly record struct HspWriteDecision(HspWriteVerdict Verdict, string? Reason)
{
    public static HspWriteDecision Send { get; } = new(HspWriteVerdict.Send, null);

    public static HspWriteDecision Hold(string reason) => new(HspWriteVerdict.Hold, reason);

    public static HspWriteDecision Drop(string reason) => new(HspWriteVerdict.Drop, reason);
}

public enum HspReadSessionState
{
    /// <summary>The next transfer may be sent (<see cref="HspReadSession.Next"/>).</summary>
    ReadyToSend,

    /// <summary>A poll was sent; the 12-byte reply header is due (<see cref="HspReadSession.ExpectedReadLength"/>).</summary>
    AwaitingHeader,

    /// <summary>The reply header announced a payload of <see cref="HspReadSession.ExpectedReadLength"/> bytes.</summary>
    AwaitingPayload,

    /// <summary>Something not seen from the console in any capture happened; the session must stop (no retry).</summary>
    Faulted,
}

/// <summary>
/// The HSP session as SmartSoft runs it, as a state machine without I/O: it decides what to send next and checks
/// every reply against the VERIFIED behaviour of the console (F51–F55, F57). It sends polls, and fetches only records
/// that the console listed in a DirtyObjects list of this session, in list order, once; a fetch is always followed by
/// a poll and that poll is always answered by the fetched object (E2, E3, E3b: 1 651 of 1 651). Nothing is re-sent.
/// Any deviation from the observed behaviour faults the session: a reply type other than 3 or 4, a poll reply where
/// the fetched object was due, a wrong echo, a non-zero word in a poll reply, an announced length above anything
/// observed, a payload byte 1 other than 0x00, a break in the sequence counter, an empty list, a listed record with
/// c ≠ 0, with the list's own id or with an id that SmartSoft never fetched. The caller then stops and closes the
/// pipe. Cleared by safety check SC-1 (docs/protocol/safety-checks.md).
/// <para>
/// A session created without a write gate is read-only, exactly as SC-1 reviewed it: it never sends a Data header or
/// a payload, and nothing can be queued on it. A session created with a write gate may also send the ConsoleEvents of
/// SC-2's whitelist
/// (2026-10-31, classes A and B1; conditional GO, nothing sent before E11 on the owner's go):
/// </para>
/// <list type="bullet">
/// <item>a write goes only at the first transfer after a completed poll exchange (the reply header and its payload
/// in), before a pending fetch, never between a fetch and its poll, at most one per poll: where SmartSoft placed all
/// 828 of its writes in E9 and E9b (`tools/protocol-analyzer/write_rules.py`; INFERRED as its rule, SC-2 item 1;
/// condition 7);</item>
/// <item>the header carries the stale words a poll would carry there (F52), the payload the per-session counter,
/// 0x00 at this session's first write and +1 per write (F131), and a fader event the last value this session sent
/// for that index, 0x00 before its first (condition 3, F132);</item>
/// <item>a key is a tap: its press, then its release at the next write slot, and no other write between them
/// (condition 4; OBSERVED, `write_rules.py` 2026-10-31: E9 and E9b sent no write between any press and its release, 0 of 58 pairs); the release is owed until sent, so
/// the session is not idle while a key is held, nor before one poll exchange has followed its last write (OBSERVED: SmartSoft
/// polled 14.5–16.5 ms after every write), and <see cref="TakeReleaseAtEnd"/> gives the release to the caller when the
/// session ends with no exchange open;</item>
/// <item>nothing is sent before this session has received the console's first list and its LED state (condition 8),
/// and at the moment a new write would go the session asks the gate (the device's interlock of condition 5) and then
/// the write's own condition (the console state its control was seen in), each of which may send it, hold it or drop
/// it; a release completes a press they let through and is not held back;</item>
/// <item>a caller that waits before it sends (the read loop keeps the cadence floors) first asks
/// <see cref="PeekNextKind"/>, waits for that kind's slot and only then calls <see cref="Next"/>: the gate and the
/// write's condition are then asked after the wait, right before the write goes, so the console's report they judge
/// cannot age during the wait (SC-2 condition 5; the first review of SC-2 item 10, major 1).</item>
/// </list>
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F55, F57", "cleared by SC-1 (2026-10-30): poll and listed fetch only")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133", "write mode, SC-2 (2026-10-31): whitelisted ConsoleEvents; the placement INFERRED from E9 and E9b (828 of 828 writes)")]
public sealed class HspReadSession
{
    /// <summary>The longest reply payload any capture holds: 792 bytes, the 157-record list (F57, E2 f384 / E3 f352 / E3b); anything longer is unobserved.</summary>
    public const int MaxObservedPayloadLength = 792;

    /// <summary>
    /// The most writes that may wait in the queue. A newer value for a fader replaces its queued one (the console sets
    /// the control at once from any value, F135, so only the latest matters), so only key taps add up here.
    /// </summary>
    public const int MaxQueuedWrites = 8;

    /// <summary>
    /// Body length of every object the console delivered, one length per id in the read-only captures
    /// E2 to E9b (F67 for the ids of E2 and E3; InputLevels 19 from E3 alone, F81, OBSERVED once). E12
    /// (2026-10-31, OBSERVED once, F156) saw other lengths, but only after SmartSoft's step commands and in
    /// its following download: a Sequence (2) of 144 bytes and a type-2 StepContent (4) of 148 bytes, plus
    /// Sequence list records with c not 0. A body of another length faults the session, a safe stop; whether
    /// a read-only session should accept those shapes is an SC-1 review question, not decided here.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> ObservedBodyLengths = new Dictionary<int, int>
    {
        [0] = 138, [2] = 132, [3] = 46, [4] = 13, [5] = 514, [8] = 39, [9] = 39, [12] = 85, [13] = 514,
        [14] = 195, [15] = 51, [17] = 81, [19] = 515, [21] = 41, [22] = 451, [23] = 7, [36] = 61, [37] = 13,
        [38] = 8, [39] = 3, [41] = 18,
    };

    /// <summary>
    /// Object ids that SmartSoft was seen to fetch, i.e. that the console listed (F67 for the ids
    /// of E2 and E3; PlaybackInfo 17 in E2 and E3b; InputLevels 19 in E3 alone, F81, OBSERVED
    /// once). A record naming any other id has never been observed and faults the session rather
    /// than being fetched. The guard is per id: the qualifier ranges seen (Memory a 0–11, b 0–12;
    /// StepContent a 0–18; Page a 0–11; ChannelText a 0–95; System a 0–6; zero elsewhere) are not
    /// enforced, consistent with F57 (every listed record was fetched).
    /// </summary>
    public static readonly IReadOnlySet<int> ObservedListedIds = new HashSet<int>
    {
        0, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 17, 19, 21, 22, 23, 36, 37, 38, 39, 41,
    };

    private readonly HspSessionDecoder _decoder = new();
    private readonly Queue<DirtyObjectRecord> _pending = new();
    private ushort _w2, _w3, _w4, _w5;
    private DirtyObjectRecord? _outstandingFetch;
    private bool _pollDueAfterFetch;
    private HspHeader _lastReplyHeader;
    private byte? _lastInboundSequence;

    // Write mode (SC-2). The gate and the queue are the only parts touched from another thread (the device queues);
    // everything else runs on the thread that calls Next and Receive. The gate is never called with _writeLock held.
    private readonly Func<HspReadSession, HspWriteDecision>? _writeGate;
    private readonly object _writeLock = new();
    private readonly List<QueuedWrite> _queuedWrites = new();
    private readonly Dictionary<ClearedFader, byte> _lastSentFaderValues = new();
    private byte _outboundSequence;
    private ClearedKey? _heldKey;
    private bool _releaseDue;
    private bool _acceptingWrites = true;
    private bool _writeSlotOpen;
    private bool _writeSentSincePoll;
    private bool _exchangeOwedAfterWrite;
    private bool _exchangeOpen;
    private bool _ledStatusReceived;

    /// <summary>A read-only session, as SC-1 cleared it: polls and listed fetches, never a write.</summary>
    public HspReadSession()
    {
    }

    /// <summary>
    /// A session that may also send the whitelisted ConsoleEvents of SC-2. <paramref name="writeGate"/> is asked each
    /// time a queued write could be placed (SC-2 condition 5: the device's record-function interlock) and sends, holds
    /// or drops it; it is called on the session's thread, without the session's lock held.
    /// </summary>
    public HspReadSession(Func<HspReadSession, HspWriteDecision> writeGate)
    {
        ArgumentNullException.ThrowIfNull(writeGate);
        _writeGate = writeGate;
    }

    public HspReadSessionState State { get; private set; } = HspReadSessionState.ReadyToSend;

    /// <summary>Why the session faulted, else null.</summary>
    public string? Fault { get; private set; }

    /// <summary>Bytes the caller must read next: 12 for a reply header, the announced length for a payload, 0 when nothing is due.</summary>
    public int ExpectedReadLength => State switch
    {
        HspReadSessionState.AwaitingHeader => HspHeader.Size,
        HspReadSessionState.AwaitingPayload => _lastReplyHeader.Length,
        _ => 0,
    };

    /// <summary>Records the console has listed and that are still to be fetched.</summary>
    public int PendingFetches => _pending.Count;

    /// <summary>
    /// True when the console owes nothing and this session owes nothing that must follow what it sent: no fetch
    /// outstanding, nothing listed and unfetched, no reply due, no key held without its release, and a poll exchange
    /// completed since the last write. A fetch is then never left without its poll, nor a press without its release,
    /// nor a write without the poll SmartSoft always sent after one. (SmartSoft's own sessions all ended one step later,
    /// on an all-zero poll answered idle, F64; this state is also true right after the last listed object was
    /// delivered, before the next poll.) Queued writes that have not started do not count: a stop discards them
    /// (<see cref="DiscardQueuedWrites"/>).
    /// </summary>
    public bool IsIdle => State == HspReadSessionState.ReadyToSend && _outstandingFetch is null && _pending.Count == 0 && !_pollDueAfterFetch
        && HeldKey is null && !_exchangeOwedAfterWrite;

    public int PollsSent { get; private set; }

    public int FetchesSent { get; private set; }

    public int ListsReceived { get; private set; }

    public int ObjectsReceived { get; private set; }

    /// <summary>True for a session created with a write gate (SC-2 live control); false for the read-only session of SC-1.</summary>
    public bool CanWrite => _writeGate is not null;

    /// <summary>Data writes sent (header and payload count as one).</summary>
    public int WritesSent { get; private set; }

    /// <summary>Operations queued and not started (a tap counts once).</summary>
    public int QueuedWrites
    {
        get
        {
            lock (_writeLock)
            {
                return _queuedWrites.Count;
            }
        }
    }

    /// <summary>The key whose press was sent and whose release is still owed, else null.</summary>
    public ClearedKey? HeldKey
    {
        get
        {
            lock (_writeLock)
            {
                return _heldKey;
            }
        }
    }

    /// <summary>Why the write at the head of the queue waits, as of the last write slot; null when nothing waits.</summary>
    public string? WriteHeldReason { get; private set; }

    /// <summary>Queued writes dropped at the moment they would have gone (the gate or their condition said so).</summary>
    public int WritesDropped { get; private set; }

    /// <summary>Why the last dropped write was dropped, else null.</summary>
    public string? LastDropReason { get; private set; }

    /// <summary>
    /// The kind of transfer <see cref="Next"/> would hand over now, without changing anything and without asking the
    /// gate: Data when this is a write slot and a write could go in it (an owed release that is due, or a queued write
    /// still to be asked about), else Fetch or Poll. A caller that waits before it sends waits for this kind's slot and
    /// then calls <see cref="Next"/>, passing whether the wait was a write's.
    /// </summary>
    public HspMessageType PeekNextKind()
    {
        if (State != HspReadSessionState.ReadyToSend)
        {
            throw new InvalidOperationException($"Nothing may be sent while the session is {State}.");
        }

        if (IsWriteSlot && HasWriteCandidate())
        {
            return HspMessageType.Data;
        }

        return _pending.Count > 0 && !_pollDueAfterFetch ? HspMessageType.Fetch : HspMessageType.Poll;
    }

    /// <summary>
    /// The next transfer: at the first transfer after a completed poll exchange a queued write (or an owed release),
    /// else the next listed record's fetch, else a poll (always a poll right after a fetch). With
    /// <paramref name="writeSlotMet"/> false (the caller waited for a fetch's or a poll's slot, as
    /// <see cref="PeekNextKind"/> said) no write goes in this slot, not even one queued meanwhile: its gate would be asked
    /// before the wait it still needs. A write slot passed that way is gone; the write waits for the next poll exchange.
    /// </summary>
    public HspOutboundTransfer Next(bool writeSlotMet = true)
    {
        if (State != HspReadSessionState.ReadyToSend)
        {
            throw new InvalidOperationException($"Nothing may be sent while the session is {State}.");
        }

        // SC-2 condition 7, placement: between a poll reply and the next poll, never between a fetch and its poll, at
        // most one write per poll. The slot is the first transfer after a completed exchange, and it closes with it.
        var writeSlot = IsWriteSlot;
        _writeSlotOpen = false;
        if (writeSlot && writeSlotMet && TakeWrite() is { } write)
        {
            return write;
        }

        if (_pending.Count > 0 && !_pollDueAfterFetch)
        {
            var record = _pending.Dequeue();
            var fetch = HspOutbound.Fetch(record);
            _decoder.Feed(HspDirection.ToConsole, fetch);
            (_w2, _w3, _w4, _w5) = ((ushort)record.ObjectId, (ushort)record.A, (ushort)record.B, (ushort)record.C);
            _outstandingFetch = record;
            _pollDueAfterFetch = true;
            FetchesSent++;
            return new HspOutboundTransfer(fetch, HspMessageType.Fetch);
        }

        var poll = HspOutbound.Poll(_w2, _w3, _w4, _w5);
        _decoder.Feed(HspDirection.ToConsole, poll);
        _pollDueAfterFetch = false;
        _writeSentSincePoll = false;
        _exchangeOpen = true;
        PollsSent++;
        State = HspReadSessionState.AwaitingHeader;
        return new HspOutboundTransfer(poll, HspMessageType.Poll);
    }

    /// <summary>
    /// Feeds the transfer that was read (a reply header, or the payload the header announced).
    /// Returns the decoded events; after a fault the session's <see cref="State"/> is
    /// <see cref="HspReadSessionState.Faulted"/> and <see cref="Fault"/> says why.
    /// </summary>
    public IReadOnlyList<HspEvent> Receive(ReadOnlyMemory<byte> data)
    {
        switch (State)
        {
            case HspReadSessionState.AwaitingHeader:
                return ReceiveHeader(data);
            case HspReadSessionState.AwaitingPayload:
                return ReceivePayload(data);
            default:
                throw new InvalidOperationException($"Nothing is expected from the console while the session is {State}.");
        }
    }

    /// <summary>
    /// Queues a fader event (SC-2 class A). A value still queued for the same fader is replaced, keeping its place;
    /// the previous byte is fixed when the event is sent (the last value this session sent, condition 3).
    /// <paramref name="condition"/>, asked without the session's lock at the moment the event would go, sends, holds or
    /// drops it (the console state its control needs); it must not change anything, since it may be asked at slots
    /// where the write then does not go. <paramref name="sent"/> runs once the event is taken for sending (the caller
    /// writes it at once), on the session's thread and without the session's lock.
    /// </summary>
    public void QueueFader(ClearedFader fader, byte value, Func<HspWriteDecision>? condition = null, Action? sent = null)
    {
        if (!Enum.IsDefined(fader))
        {
            throw new ArgumentOutOfRangeException(nameof(fader), fader, "Not a fader index of the SC-2 whitelist.");
        }

        lock (_writeLock)
        {
            RequireAcceptingWrites();
            var queued = _queuedWrites.FindIndex(w => w is FaderWrite f && f.Fader == fader);
            if (queued >= 0)
            {
                _queuedWrites[queued] = new FaderWrite(fader, value, condition, sent);
                return;
            }

            RequireRoom();
            _queuedWrites.Add(new FaderWrite(fader, value, condition, sent));
        }
    }

    /// <summary>
    /// Queues a key tap (SC-2 class B1): its press, then its release at the next write slot, as one operation
    /// (condition 4). <paramref name="condition"/> is asked before the press as for <see cref="QueueFader"/>; once the
    /// press is out, the release is owed and nothing holds it back. <paramref name="sent"/> runs once the press is taken
    /// for sending, as for <see cref="QueueFader"/>.
    /// </summary>
    public void QueueKeyTap(ClearedKey key, Func<HspWriteDecision>? condition = null, Action? sent = null) => QueuePress(key, tap: true, condition, sent);

    /// <summary>
    /// The golden tests' path (condition 2): a press whose release waits for <see cref="QueueKeyRelease"/>, so that a
    /// replay can release where SmartSoft released (E9: 46 ms to 5.80 s after the press). Not reachable from the
    /// device, which sends taps only (no held bump, condition 4). A stop or a discard makes the release due at once.
    /// </summary>
    internal void QueueKeyPress(ClearedKey key) => QueuePress(key, tap: false, condition: null, sent: null);

    /// <summary>Makes the release of a press queued with <see cref="QueueKeyPress"/> due (golden tests only).</summary>
    internal void QueueKeyRelease(ClearedKey key)
    {
        lock (_writeLock)
        {
            if (_heldKey == key)
            {
                _releaseDue = true;
                return;
            }

            var queued = _queuedWrites.FindIndex(w => w is KeyPress p && p.Key == key);
            if (queued < 0)
            {
                throw new InvalidOperationException($"No press of {key} is queued or held.");
            }

            _queuedWrites[queued] = ((KeyPress)_queuedWrites[queued]) with { Tap = true };
        }
    }

    /// <summary>True when a press of <paramref name="key"/> is queued and not sent yet.</summary>
    public bool IsKeyQueued(ClearedKey key)
    {
        lock (_writeLock)
        {
            return _queuedWrites.Exists(w => w is KeyPress p && p.Key == key);
        }
    }

    /// <summary>
    /// Drops every queued write that has not started and returns how many. A press already sent keeps its release,
    /// which becomes due at once (condition 4). With <paramref name="acceptNoMore"/> (a stop) nothing can be queued
    /// afterwards.
    /// </summary>
    public int DiscardQueuedWrites(bool acceptNoMore = false)
    {
        lock (_writeLock)
        {
            var dropped = _queuedWrites.Count;
            _queuedWrites.Clear();
            if (_heldKey is not null)
            {
                _releaseDue = true;
            }

            if (acceptNoMore)
            {
                _acceptingWrites = false;
            }

            return dropped;
        }
    }

    /// <summary>True when the console listed a record of <paramref name="id"/> in this session and it has not been delivered yet.</summary>
    public bool IsListedAndUnfetched(HspObjectId id) =>
        (_outstandingFetch is { } fetch && fetch.ObjectId == (int)id) || _pending.Any(r => r.ObjectId == (int)id);

    /// <summary>
    /// When the session ends otherwise than by a stop (a fault, or an error of its caller) with a key held: the
    /// release, for the caller to send before it closes the pipe (SC-2 condition 4: the release is sent before the pipe
    /// closes when the pipe is alive). Only when no exchange is open (the last reply was read whole, so the console owes
    /// nothing), no fetch waits for its poll or its object and no write went since the last poll, so the release sits
    /// where every write sits: after a completed poll exchange, one per poll. Null otherwise, or when no key is held;
    /// the caller then reports the press whose release could not be sent (SC-2 item 7).
    /// </summary>
    public HspOutboundTransfer? TakeReleaseAtEnd()
    {
        if (State is not (HspReadSessionState.Faulted or HspReadSessionState.ReadyToSend)
            || _exchangeOpen || _pollDueAfterFetch || _outstandingFetch is not null || _writeSentSincePoll)
        {
            return null;
        }

        ClearedKey key;
        lock (_writeLock)
        {
            if (_heldKey is not { } held)
            {
                return null;
            }

            key = held;
            _heldKey = null;
            _releaseDue = false;
        }

        return BuildWrite(HspOutbound.KeyEvent(TakeSequence(), key, pressed: false));
    }

    private void QueuePress(ClearedKey key, bool tap, Func<HspWriteDecision>? condition, Action? sent)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key code of the SC-2 whitelist.");
        }

        lock (_writeLock)
        {
            RequireAcceptingWrites();
            RequireRoom();
            _queuedWrites.Add(new KeyPress(key, tap, condition, sent));
        }
    }

    private void RequireAcceptingWrites()
    {
        if (_writeGate is null)
        {
            throw new InvalidOperationException("This session is read-only (SC-1): no write can be queued on it.");
        }

        if (!_acceptingWrites)
        {
            throw new InvalidOperationException("The session is stopping: no write is queued any more.");
        }
    }

    private void RequireRoom()
    {
        if (_queuedWrites.Count >= MaxQueuedWrites)
        {
            throw new InvalidOperationException($"{MaxQueuedWrites} writes are already waiting for the console; nothing more is queued until they are sent.");
        }
    }

    /// <summary>A write may go in this transfer: the first after a completed poll exchange, with no fetch waiting for its poll or its object.</summary>
    private bool IsWriteSlot => _writeSlotOpen && !_pollDueAfterFetch && _outstandingFetch is null;

    /// <summary>What <see cref="TakeWrite"/> would consider: a release that is due, else (no key held) a queued write.</summary>
    private bool HasWriteCandidate()
    {
        if (_writeGate is null)
        {
            return false;
        }

        lock (_writeLock)
        {
            return _heldKey is not null ? _releaseDue : _queuedWrites.Count > 0;
        }
    }

    /// <summary>
    /// The write for this slot, or null: an owed release first; else, once the console's first list and LED state are
    /// in, the oldest queued write, if the gate and then its own condition send it (a hold keeps it queued, a drop
    /// removes it unsent; either way nothing else goes in this slot). A write taken here is committed (counter, previous
    /// byte, held key) and its <c>sent</c> callback runs; the caller writes it at once.
    /// </summary>
    private HspOutboundTransfer? TakeWrite()
    {
        if (_writeGate is null)
        {
            return null;
        }

        QueuedWrite? head;
        lock (_writeLock)
        {
            if (_heldKey is { } held)
            {
                // Nothing but its release follows a press; the release completes a press the gate let through.
                if (!_releaseDue)
                {
                    return null;
                }

                _heldKey = null;
                _releaseDue = false;
                return BuildWrite(HspOutbound.KeyEvent(TakeSequence(), held, pressed: false));
            }

            head = _queuedWrites.Count > 0 ? _queuedWrites[0] : null;
        }

        if (head is null)
        {
            WriteHeldReason = null;
            return null;
        }

        // SC-2 condition 8: no write before the session has seen the console's first list and the LED state.
        if (ListsReceived == 0 || !_ledStatusReceived)
        {
            WriteHeldReason = "the console's first list and LED state have not arrived yet (SC-2 condition 8)";
            return null;
        }

        // SC-2 condition 5, then the write's own condition, both asked without the lock.
        var decision = _writeGate(this);
        if (decision.Verdict == HspWriteVerdict.Send && head.Condition is { } condition)
        {
            decision = condition();
        }

        lock (_writeLock)
        {
            if (_queuedWrites.Count == 0 || !ReferenceEquals(_queuedWrites[0], head))
            {
                return null;   // replaced or discarded while it was being asked about: the next slot asks again
            }

            switch (decision.Verdict)
            {
                case HspWriteVerdict.Hold:
                    WriteHeldReason = decision.Reason ?? "held back";
                    return null;
                case HspWriteVerdict.Drop:
                    _queuedWrites.RemoveAt(0);
                    WritesDropped++;
                    LastDropReason = decision.Reason ?? "dropped";
                    WriteHeldReason = null;
                    return null;
            }

            _queuedWrites.RemoveAt(0);
            if (head is KeyPress press)
            {
                _heldKey = press.Key;
                _releaseDue = press.Tap;
            }
        }

        WriteHeldReason = null;
        HspOutboundTransfer transfer;
        switch (head)
        {
            case FaderWrite fader:
                // SC-2 condition 3 (F132): this host's own last value of the index, 0x00 before its first event.
                var previous = _lastSentFaderValues.TryGetValue(fader.Fader, out var last) ? last : (byte)0x00;
                _lastSentFaderValues[fader.Fader] = fader.Value;
                transfer = BuildWrite(HspOutbound.FaderEvent(TakeSequence(), fader.Fader, fader.Value, previous));
                break;
            case KeyPress keyPress:
                transfer = BuildWrite(HspOutbound.KeyEvent(TakeSequence(), keyPress.Key, pressed: true));
                break;
            default:
                throw new InvalidOperationException($"Unknown queued write {head}.");
        }

        // Committed: only now may the owner of the write count it as sent (never at the moment its condition said Send).
        head.Sent?.Invoke();
        return transfer;
    }

    /// <summary>F131: 0x00 at the session's first data payload, +1 per data payload, wrapping 0xFF → 0x00 (E9b wrapped at seq 39860).</summary>
    private byte TakeSequence()
    {
        var sequence = _outboundSequence;
        _outboundSequence = unchecked((byte)(sequence + 1));
        return sequence;
    }

    /// <summary>The Data header with the stale words a poll would carry here (F52), then the payload; both go through the decoder like every transfer.</summary>
    private HspOutboundTransfer BuildWrite(byte[] payload)
    {
        var header = HspOutbound.DataHeader(payload, _w2, _w3, _w4, _w5);
        _decoder.Feed(HspDirection.ToConsole, header);
        _decoder.Feed(HspDirection.ToConsole, payload);
        _writeSentSincePoll = true;
        _exchangeOwedAfterWrite = true;
        WritesSent++;
        return new HspOutboundTransfer(header, HspMessageType.Data, payload);
    }

    private IReadOnlyList<HspEvent> ReceiveHeader(ReadOnlyMemory<byte> data)
    {
        if (data.Length != HspHeader.Size)
        {
            return FaultWith($"reply header of {data.Length} bytes instead of {HspHeader.Size}", data);
        }

        var parsed = HspHeader.Parse(data.Span);

        // The exchange stays open when this reply announces a payload, or when its type is one no capture has shown
        // (nothing says what would follow); only when it is closed may a release still be sent after a fault (TakeReleaseAtEnd).
        _exchangeOpen = !parsed.IsFromConsole || parsed.Length > 0;
        if (!parsed.IsFromConsole)
        {
            return FaultWith($"reply header {parsed} of an unobserved type", data);
        }

        if (parsed.Length > MaxObservedPayloadLength)
        {
            return FaultWith($"reply header {parsed} announces {parsed.Length} bytes; nothing above {MaxObservedPayloadLength} was ever observed", data);
        }

        var evt = _decoder.Feed(HspDirection.FromConsole, data);
        if (evt is not ReplyHeaderReceived reply)
        {
            return FaultWith(evt is UnexpectedTransfer u ? u.Reason : $"unexpected {evt.GetType().Name} where a reply header was due", data);
        }

        var header = reply.Header;
        switch (header.MessageType)
        {
            case HspMessageType.ObjectReply:
                if (_outstandingFetch is not { } fetch)
                {
                    return FaultWith($"object reply {header} without an outstanding fetch", data);
                }

                if (header.ObjectId != fetch.ObjectId || header.QualifierA != fetch.A || header.QualifierB != fetch.B || header.QualifierC != fetch.C)
                {
                    return FaultWith($"object reply {header} does not echo the fetch of {fetch}", data);
                }

                if (header.Length == 0)
                {
                    return FaultWith($"object reply {header} announces no payload", data);
                }

                _outstandingFetch = null;
                break;
            case HspMessageType.PollReply:
                if (_outstandingFetch is { } unanswered)
                {
                    return FaultWith($"poll reply {header} where the object of the fetch {unanswered} was due (answered on the next poll in 1 651 of 1 651 observed fetches)", data);
                }

                if (header.W2 != 0 || header.W3 != 0 || header.W4 != 0 || header.W5 != 0)
                {
                    return FaultWith($"poll reply {header} with non-zero words", data);
                }

                break;
            default:
                return FaultWith($"reply header {header} of an unobserved type", data);
        }

        (_w2, _w3, _w4, _w5) = (header.W2, header.W3, header.W4, header.W5);
        _lastReplyHeader = header;
        if (header.Length > 0)
        {
            State = HspReadSessionState.AwaitingPayload;
        }
        else
        {
            State = HspReadSessionState.ReadyToSend;
            CompleteExchange();
        }

        return new HspEvent[] { reply };
    }

    private IReadOnlyList<HspEvent> ReceivePayload(ReadOnlyMemory<byte> data)
    {
        // The payload transfer has been read whole: whatever follows, the console owes nothing more for this poll.
        _exchangeOpen = false;
        if (data.Length != _lastReplyHeader.Length)
        {
            return FaultWith($"payload of {data.Length} bytes instead of the announced {_lastReplyHeader.Length}", data);
        }

        if (data.Length < HspInboundPayload.HeaderSize)
        {
            return FaultWith($"payload of {data.Length} bytes is shorter than the {HspInboundPayload.HeaderSize}-byte payload header", data);
        }

        HspEvent evt;
        try
        {
            evt = _decoder.Feed(HspDirection.FromConsole, data);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            return FaultWith($"payload does not parse: {ex.Message}", data);
        }

        HspInboundPayload payload;
        switch (evt)
        {
            case DirtyListReceived list:
                payload = list.Payload;
                if (list.List.Records.Count == 0)
                {
                    return FaultWith("empty DirtyObjects list (never observed)", data);
                }

                foreach (var record in list.List.Records)
                {
                    if (record.C != 0)
                    {
                        return FaultWith($"listed record {record} with c = {record.C} (c = 0 in every record SmartSoft was seen to fetch in a read-only session; E9 showed c != 0 only for System records after a PC-side EDIT MEM selection, F137 / H83, which SC-1 does not cover)", data);
                    }

                    if (record.ObjectId == (int)HspObjectId.DirtyObjects || !ObservedListedIds.Contains(record.ObjectId))
                    {
                        return FaultWith($"listed record {record} names an id SmartSoft was never seen to fetch", data);
                    }
                }

                foreach (var record in list.List.Records)
                {
                    _pending.Enqueue(record);
                }

                ListsReceived++;
                break;
            case ObjectReceived obj:
                payload = obj.Payload;
                if (_lastReplyHeader.MessageType != HspMessageType.ObjectReply)
                {
                    return FaultWith($"poll reply payload carries object {payload.ObjectId} instead of a DirtyObjects list", data);
                }

                if (payload.ObjectId != _lastReplyHeader.ObjectId)
                {
                    return FaultWith($"payload object {payload.ObjectId} differs from the reply header's {_lastReplyHeader.ObjectId}", data);
                }

                if (!ObservedBodyLengths.TryGetValue(payload.ObjectId, out var observedLength) || payload.Body.Length != observedLength)
                {
                    return FaultWith($"object {payload.ObjectId} ({HspObjectIds.Name(payload.ObjectId)}) with a {payload.Body.Length}-byte body; every observed body of that id had {(ObservedBodyLengths.TryGetValue(payload.ObjectId, out var known) ? known.ToString() : "an unknown number of")} bytes (F67)", data);
                }

                ObjectsReceived++;
                break;
            default:
                return FaultWith(evt is UnexpectedTransfer u ? u.Reason : $"unexpected {evt.GetType().Name} where a payload was due", data);
        }

        if (payload.Byte1 != 0)
        {
            return FaultWith($"payload byte 1 = 0x{payload.Byte1:X2} (0x00 in every observed payload)", data);
        }

        if (_lastInboundSequence is { } previous && payload.Sequence != (byte)(previous + 1))
        {
            return FaultWith($"payload sequence 0x{payload.Sequence:X2} after 0x{previous:X2} (expected +1)", data);
        }

        _lastInboundSequence = payload.Sequence;
        if (payload.ObjectId == (int)HspObjectId.LedStatus && evt is ObjectReceived)
        {
            _ledStatusReceived = true;   // SC-2 condition 8: the console's LED state is in
        }

        State = HspReadSessionState.ReadyToSend;
        CompleteExchange();
        return new[] { evt };
    }

    /// <summary>A poll exchange is complete: the next transfer may be a write, and a write's following poll has happened.</summary>
    private void CompleteExchange()
    {
        _writeSlotOpen = true;
        _exchangeOwedAfterWrite = false;
    }

    private IReadOnlyList<HspEvent> FaultWith(string reason, ReadOnlyMemory<byte> data)
    {
        State = HspReadSessionState.Faulted;
        Fault = reason;
        return new HspEvent[] { new UnexpectedTransfer(HspDirection.FromConsole, data, reason) };
    }

    /// <summary>A queued write, the condition it is sent under (null: the gate alone decides) and what runs once it is taken for sending.</summary>
    private abstract record QueuedWrite(Func<HspWriteDecision>? Condition, Action? Sent);

    /// <summary>A fader event; the previous byte is fixed when it is sent.</summary>
    private sealed record FaderWrite(ClearedFader Fader, byte Value, Func<HspWriteDecision>? Condition, Action? Sent) : QueuedWrite(Condition, Sent);

    /// <summary>A key press; with <paramref name="Tap"/> its release is due at the next write slot, else when the golden test releases it.</summary>
    private sealed record KeyPress(ClearedKey Key, bool Tap, Func<HspWriteDecision>? Condition, Action? Sent) : QueuedWrite(Condition, Sent);
}
