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

/// <summary>Where SmartSoft's start-up pair (safety check SC-4, class E) stands in a session.</summary>
public enum HspStartupPairPhase
{
    /// <summary>The session sends no pair: SC-1's read-only session, or live control without `Device:Usb:StartupPairDate`.</summary>
    None,

    /// <summary>`09 00 00` waits for its slot: SmartSoft's 40th reply header, nothing listed and unfetched (F181).</summary>
    Waiting,

    /// <summary>`09 00 00` went; the console's ConsoleVersion is awaited (listed within two poll replies, then fetched; F182 a).</summary>
    VersionRequested,

    /// <summary>ConsoleVersion read "3.0.2.9.0.43" (F59): `07 00 00` waits for its slot (the 79th reply header and one more completed exchange).</summary>
    VersionAccepted,

    /// <summary>`07 00 00` went; the download's list is awaited (the next free reply in 20 of 20, F182 b).</summary>
    DownloadRequested,

    /// <summary>The download's list arrived; its records are being fetched, one per poll, and nothing else is sent (F182 e).</summary>
    Downloading,

    /// <summary>Every record of the download's list was delivered.</summary>
    Done,

    /// <summary>The pair ended without the download (refused by the gate, the version gate, the session stopping); <see cref="HspStartupPairStatus.EndReason"/> says why.</summary>
    Ended,
}

/// <summary>The start-up pair's progress (SC-4): its phase, the download's size and progress, and why it ended without the download.</summary>
/// <param name="Phase">Where the pair stands.</param>
/// <param name="DownloadRecords">The download list's record count once it arrived, else null.</param>
/// <param name="DownloadDelivered">The download's records delivered so far.</param>
/// <param name="DownloadListLength">The download list's payload length in bytes once it arrived (792–812 observed, F182 c), else null.</param>
/// <param name="EndReason">Why the pair ended without the download (<see cref="HspStartupPairPhase.Ended"/>), else null.</param>
public sealed record HspStartupPairStatus(HspStartupPairPhase Phase, int? DownloadRecords, int DownloadDelivered, int? DownloadListLength, string? EndReason)
{
    public static HspStartupPairStatus None { get; } = new(HspStartupPairPhase.None, null, 0, null, null);

    /// <summary>True from the session's start until the download's last record was delivered or the pair ended: no other write goes meanwhile (SC-4 clauses 1, 2 and 6).</summary>
    public bool BlocksWrites => Phase is HspStartupPairPhase.Waiting or HspStartupPairPhase.VersionRequested or HspStartupPairPhase.VersionAccepted
        or HspStartupPairPhase.DownloadRequested or HspStartupPairPhase.Downloading;
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
/// the SC-2 / SC-5 whitelist
/// (2026-10-31, classes A and B1; 2026-11-02, classes A2–A4, B3–B5; conditional GOs, nothing sent before the review and
/// the owner's go for the session):
/// </para>
/// <list type="bullet">
/// <item>a write goes only at the first transfer after a completed poll exchange (the reply header and its payload
/// in), before a pending fetch, never between a fetch and its poll, at most one per poll: where SmartSoft placed all
/// 828 of its writes in E9 and E9b (`tools/protocol-analyzer/write_rules.py`; INFERRED as its rule, SC-2 item 1;
/// condition 7) and every one of E9c, E9d and E9e that a session of ours can replay (the golden replays);</item>
/// <item>the header carries the stale words a poll would carry there (F52), the payload the per-session counter,
/// 0x00 at this session's first write and +1 per write (F131), and a fader event the last value this session sent
/// for that index, 0x00 before its first, whatever bank the console shows (condition 3, F132; SC-5 condition 4);</item>
/// <item>a key is a tap, its press then its release at the next write slot, or, for a bump only, a hold
/// (<see cref="QueueKeyHold"/>; SC-5 class B3), its press then its release when the owner of the hold asks for it
/// (<see cref="ReleaseKey"/>: the UI let go, or the caller's 3 s bound, SC-5 condition 2); no other write goes between a
/// press and its release (condition 4; OBSERVED, `write_rules.py`: E9 and E9b sent no write between any press and its
/// release, 0 of 58 pairs, nor did E9c, E9d or E9e); the release is owed until sent, so
/// the session is not idle while a key is held, nor before one poll exchange has followed its last write (OBSERVED: SmartSoft
/// polled 14.5–16.5 ms after every write), a stop makes it due at once, and <see cref="TakeReleaseAtEnd"/> gives the
/// release to the caller when the session ends with no exchange open;</item>
/// <item>no queued write is sent before this session has received the console's first list and its LED state (condition 8;
/// SC-4's start-up pair, below, goes blind, as SmartSoft sends it, and the device's gate refuses it in the states clause 3 names),
/// and at the moment a new write would go the session asks the gate (the device's interlock of condition 5) and then
/// the write's own condition (the console state its control was seen in), each of which may send it, hold it or drop
/// it; a release completes a press they let through and is not held back;</item>
/// <item>a caller that waits before it sends (the read loop keeps the cadence floors) first asks
/// <see cref="PeekNextKind"/>, waits for that kind's slot and only then calls <see cref="Next"/>: the gate and the
/// write's condition are then asked after the wait, right before the write goes, so the console's report they judge
/// cannot age during the wait (SC-2 condition 5; the first review of SC-2 item 10, major 1).</item>
/// </list>
/// <para>
/// A session created with the start-up pair's gate (SC-4, 2026-11-02, evening, class E; a conditional GO, nothing sent before
/// the review and the owner's go for E13) also sends SmartSoft's System pair, once, as its first two data writes and where
/// SmartSoft placed them in all 20 of its sessions (F181; S11.17 rows 1–3): `09 00 00` at the first write slot after reply
/// header #40 with nothing listed and unfetched; `07 00 00` at the first write slot after reply header #79, only after the
/// console listed ConsoleVersion within two poll replies of `09`, delivered it as "3.0.2.9.0.43" (F59, F182 a) and one more
/// poll exchange completed; the gate (the device: the report's freshness, the console's state before the pair, clause 3) may
/// hold or refuse each. Until the download's last record is delivered, or the pair ended, no other write goes (clauses 1, 2,
/// 6); a stop ends a pair not yet sent; the download must be listed within two free poll replies of `07` (F182 b: the next
/// free reply in 20 of 20) or the session faults. Such a session accepts what the 20 downloads and E12 / E12b showed and no
/// more: a list of up to 812 bytes (<see cref="MaxDownloadPayloadLength"/>) and the Sequence and StepContent lengths of edited
/// shows (<see cref="DownloadBodyLengths"/>); a session without the pair keeps SC-1's bounds unchanged.
/// </para>
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F55, F57", "cleared by SC-1 (2026-10-30): poll and listed fetch only")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133", "write mode, SC-2 (2026-10-31): whitelisted ConsoleEvents; the placement INFERRED from E9 and E9b (828 of 828 writes)")]
[Evidence(EvidenceStatus.Verified, "F171, F174, F175", "write mode, SC-5 (2026-11-02): every physical fader and bump, the bank keys, IND 1 / 2; a bump held until released (F174: SmartSoft's holds of 0.8-4.67 s); the placement reproduced over E9c, E9d and E9e")]
[Evidence(EvidenceStatus.Verified, "F181, F182", "the start-up pair, SC-4 (2026-11-02, evening) class E: SmartSoft's two System writes at its reply headers #40 and #79 (20 of 20; the count-driven reading INFERRED, S11.17), the version gate (S11.17 row 3, INFERRED high), the download accepted record by record (the golden replay of all 20 downloads)")]
public sealed class HspReadSession
{
    /// <summary>
    /// The longest reply payload of the read-only captures E2 to E9b: 792 bytes, the 157-record list (F57, E2 f384 / E3 f352 / E3b).
    /// E12 and E12b (2026-10-31, OBSERVED) hold longer lists, 812 and 807 bytes, only in the console's download after SmartSoft's
    /// step commands added stack steps (F157): a read-only session of ours faults on such a payload, a safe stop; whether to
    /// accept it is an SC-1 review question, not decided here (as for <see cref="ObservedBodyLengths"/>). A session that sends
    /// the start-up pair accepts up to <see cref="MaxDownloadPayloadLength"/> instead (SC-4 clause 2).
    /// </summary>
    public const int MaxObservedPayloadLength = 792;

    /// <summary>
    /// SC-4 clause 2: the longest download list of the 20 SmartSoft sessions, 812 bytes (161 records, E12 session 2, seq 139012;
    /// the others 792, 797, 802 and 807 bytes for 157 to 160 records, F182 c, recounted 2026-11-02 over the ten captures, the
    /// longest list of any kind in them). Only a session that sends the start-up pair accepts more than
    /// <see cref="MaxObservedPayloadLength"/>; a longer reply still faults it.
    /// </summary>
    public const int MaxDownloadPayloadLength = 812;

    /// <summary>SmartSoft sends `09 00 00` after reply header #40 of its session (F181, 20 of 20; S11.17 row 2: countdown 1 of 40 in the DLL image, INFERRED high).</summary>
    public const int StartupPairFirstAfterReplies = 40;

    /// <summary>SmartSoft sends `07 00 00` after reply header #79 (F181, 20 of 20; S11.17 row 2: countdown 2 of 40, counted from the call in which the first ended).</summary>
    public const int StartupPairSecondAfterReplies = 79;

    /// <summary>
    /// SC-4 item 9 (the version gate): ConsoleVersion must be listed within this many free poll replies (type 4, the ones that
    /// can carry a list) after `09 00 00`, else the pair ends there. Observed: at the next free reply in 20 of 20 (F182 a).
    /// </summary>
    public const int StartupVersionListedWithin = 2;

    /// <summary>
    /// The download must be listed within this many free poll replies after `07 00 00`, else the session faults (a deviation:
    /// at the next free reply in 20 of 20, F182 b; E9e's two fetched a pending PlaybackInfo first, an object reply, not free).
    /// </summary>
    public const int StartupDownloadListedWithin = 2;

    /// <summary>
    /// The ConsoleVersion body SmartSoft's pair was observed with: `0C` + ASCII "3.0.2.9.0.43" (F59, F182 a: 20 of 20; the
    /// engine's own version, S11.17 row 3). `07 00 00` goes only after this exact body.
    /// </summary>
    public static ReadOnlySpan<byte> StartupPairConsoleVersion => new byte[] { 0x0C, 0x33, 0x2E, 0x30, 0x2E, 0x32, 0x2E, 0x39, 0x2E, 0x30, 0x2E, 0x34, 0x33 };

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
    /// SC-4 clause 2: the body lengths a session that sends the start-up pair accepts, per id: <see cref="ObservedBodyLengths"/>
    /// plus every other length the console delivered in the 20 downloads and in E12 / E12b, recounted on 2026-11-02 over every
    /// object of the ten SmartSoft captures (OBSERVED; F156, F182): Sequence (2) 144 bytes (8 steps; E12 seq 87591, download 2
    /// of E12) and 156 bytes (9 steps; E12b seq 73712, its download 2), StepContent (4) 148 bytes (a type-2 "State" step; E12
    /// seq 139240, E12b seq 87517). No other id ever came with a second length (SmartSoft's own 100-byte StepContent write of
    /// E12 is PC → console and never came back at that length). Any other length still faults the session, SC-1's safe stop.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, IReadOnlySet<int>> DownloadBodyLengths = ObservedBodyLengths.ToDictionary(
        entry => entry.Key,
        entry => (IReadOnlySet<int>)(entry.Key switch
        {
            (int)HspObjectId.Sequence => new HashSet<int> { 132, 144, 156 },
            (int)HspObjectId.StepContent => new HashSet<int> { 13, 148 },
            _ => new HashSet<int> { entry.Value },
        }));

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

    // The start-up pair (SC-4). The phase and its reason are written on the session's thread and read from others under
    // _writeLock; the counters belong to the session's thread.
    private readonly Func<HspReadSession, ClearedSystemMessage, HspWriteDecision>? _pairGate;
    private readonly Queue<string> _notes = new();
    private HspStartupPairPhase _pairPhase;
    private string? _pairEndReason;
    private int _pairFreeReplies;
    private bool _versionListed;
    private int _exchangesSinceVersion;
    private int? _downloadRecords;
    private int _downloadDelivered;
    private int? _downloadListLength;

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

    /// <summary>
    /// A session with or without live control (<paramref name="writeGate"/>, as above; null: no ConsoleEvent can be queued)
    /// and with or without SmartSoft's start-up pair (<paramref name="startupPairGate"/>, SC-4; null: no pair, and SC-1's
    /// bounds). The pair's gate is asked, on the session's thread and without the session's lock, at the slot where each of
    /// the two writes would go (after the session's own placement and version conditions), and sends, holds or refuses it; a
    /// refusal ends the pair (<see cref="HspStartupPairPhase.Ended"/>) and the session goes on as before.
    /// </summary>
    public HspReadSession(Func<HspReadSession, HspWriteDecision>? writeGate, Func<HspReadSession, ClearedSystemMessage, HspWriteDecision>? startupPairGate)
    {
        _writeGate = writeGate;
        _pairGate = startupPairGate;
        _pairPhase = startupPairGate is null ? HspStartupPairPhase.None : HspStartupPairPhase.Waiting;
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
        && HeldKey is null && !_exchangeOwedAfterWrite && _pairPhase is not (HspStartupPairPhase.DownloadRequested or HspStartupPairPhase.Downloading);

    public int PollsSent { get; private set; }

    /// <summary>Reply headers received in this session, of both types (SmartSoft's pair counts them, S11.17 row 1).</summary>
    public int RepliesReceived { get; private set; }

    /// <summary>True for a session created with the start-up pair's gate (SC-4).</summary>
    public bool SendsStartupPair => _pairGate is not null;

    /// <summary>The longest reply payload this session accepts: SC-1's 792 bytes, or 812 with the start-up pair (SC-4 clause 2).</summary>
    public int MaxPayloadLength => SendsStartupPair ? MaxDownloadPayloadLength : MaxObservedPayloadLength;

    /// <summary>The start-up pair's progress; <see cref="HspStartupPairStatus.None"/> for a session without it. Thread-safe.</summary>
    public HspStartupPairStatus StartupPair
    {
        get
        {
            lock (_writeLock)
            {
                return _pairPhase == HspStartupPairPhase.None
                    ? HspStartupPairStatus.None
                    : new HspStartupPairStatus(_pairPhase, _downloadRecords, _downloadDelivered, _downloadListLength, _pairEndReason);
            }
        }
    }

    /// <summary>
    /// The notes the session left for the transfer log since the last call (the start-up pair's steps and decisions), in
    /// order; the caller writes them between exchanges, never between a reply header and its payload or a write's gate and
    /// its header. Called on the session's thread.
    /// </summary>
    public IReadOnlyList<string> TakeNotes()
    {
        if (_notes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var notes = _notes.ToArray();
        _notes.Clear();
        return notes;
    }

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

    /// <summary>
    /// True while a hold (<see cref="QueueKeyHold"/>) waits for its release: its press queued, or sent with the release
    /// not yet asked for. A tap's press does not count (its release is due at once).
    /// </summary>
    public bool HoldInProgress
    {
        get
        {
            lock (_writeLock)
            {
                return (_heldKey is not null && !_releaseDue) || _queuedWrites.Exists(w => w is KeyPress { Tap: false });
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
    /// Queues a fader event (SC-2 class A, SC-5 classes A2-A4). A value still queued for the same fader is replaced, keeping its place;
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
            throw new ArgumentOutOfRangeException(nameof(fader), fader, "Not a fader index of the SC-2 / SC-5 whitelist.");
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
    /// Queues a key tap (SC-2 class B1, SC-5 classes B3-B5): its press, then its release at the next write slot, as one operation
    /// (condition 4). <paramref name="condition"/> is asked before the press as for <see cref="QueueFader"/>; once the
    /// press is out, the release is owed and nothing holds it back. <paramref name="sent"/> runs once the press is taken
    /// for sending, as for <see cref="QueueFader"/>.
    /// </summary>
    public void QueueKeyTap(ClearedKey key, Func<HspWriteDecision>? condition = null, Action? sent = null) => QueuePress(key, tap: true, condition, sent);

    /// <summary>
    /// Queues a held bump (SC-5 class B3): its press, then its release only once <see cref="ReleaseKey"/> asks for it (the
    /// UI let go, or the caller's bound; SC-5 condition 2: the device releases at 3 s), or at once when the session stops
    /// or discards its queue (condition 4). Only a bump code is held (<see cref="PhysicalControls.IsBump"/>): every other
    /// key is a tap. Nothing but the release goes while the key is held. <paramref name="condition"/> and
    /// <paramref name="sent"/> as for <see cref="QueueKeyTap"/>.
    /// </summary>
    public void QueueKeyHold(ClearedKey key, Func<HspWriteDecision>? condition = null, Action? sent = null)
    {
        if (!PhysicalControls.IsBump(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Only a bump is held (SC-5 class B3); every other key is a tap.");
        }

        QueuePress(key, tap: false, condition, sent);
    }

    /// <summary>
    /// The golden tests' path (condition 2): a press of any whitelisted key whose release waits for
    /// <see cref="ReleaseKey"/>, so that a replay can release where SmartSoft released (E9: 46 ms to 5.80 s after the
    /// press). Not reachable from the device, which holds bumps only (<see cref="QueueKeyHold"/>) and taps every other key.
    /// </summary>
    internal void QueueKeyPress(ClearedKey key) => QueuePress(key, tap: false, condition: null, sent: null);

    /// <summary>
    /// Asks for the release of a held key: due at the next write slot when its press was sent, or, when its press is still
    /// queued, the press becomes a tap (its release follows it at the next slot). Returns false when the key is neither held
    /// nor queued as a hold, or its release is already due: a release is sent once (SC-5 condition 2).
    /// </summary>
    public bool ReleaseKey(ClearedKey key)
    {
        lock (_writeLock)
        {
            if (_heldKey == key)
            {
                if (_releaseDue)
                {
                    return false;
                }

                _releaseDue = true;
                return true;
            }

            var queued = _queuedWrites.FindIndex(w => w is KeyPress { Tap: false } p && p.Key == key);
            if (queued < 0)
            {
                return false;
            }

            _queuedWrites[queued] = ((KeyPress)_queuedWrites[queued]) with { Tap = true };
            return true;
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

    /// <summary>True when an event for <paramref name="fader"/> is queued and not sent yet.</summary>
    public bool IsFaderQueued(ClearedFader fader)
    {
        lock (_writeLock)
        {
            return _queuedWrites.Exists(w => w is FaderWrite f && f.Fader == fader);
        }
    }

    /// <summary>
    /// Drops every queued write that has not started and returns how many. A press already sent keeps its release,
    /// which becomes due at once (condition 4). With <paramref name="acceptNoMore"/> (a stop) nothing can be queued
    /// afterwards.
    /// </summary>
    public int DiscardQueuedWrites(bool acceptNoMore = false)
    {
        int dropped;
        lock (_writeLock)
        {
            dropped = _queuedWrites.Count;
            _queuedWrites.Clear();
            if (_heldKey is not null)
            {
                _releaseDue = true;
            }

            if (acceptNoMore)
            {
                _acceptingWrites = false;
            }
        }

        // a stop sends nothing new: a start-up pair not sent yet, or its second write not sent yet, is not sent (SC-4 clause 7);
        // a download already requested is taken whole before the session can stop (IsIdle)
        if (acceptNoMore && _pairPhase is HspStartupPairPhase.Waiting or HspStartupPairPhase.VersionRequested or HspStartupPairPhase.VersionAccepted)
        {
            EndPair(_pairPhase == HspStartupPairPhase.Waiting
                ? "the session stopped before the start-up pair was sent"
                : "the session stopped between the start-up pair's two writes: 07 00 00 is not sent");
        }

        return dropped;
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
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key code of the SC-2 / SC-5 whitelist.");
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

    /// <summary>
    /// What <see cref="TakeWrite"/> would consider: while the start-up pair is under way only its write whose place has come
    /// (<see cref="PairWriteDue"/>); else a release that is due, else (no key held) a queued write.
    /// </summary>
    private bool HasWriteCandidate()
    {
        if (PairBlocksWrites)
        {
            return PairWriteDue() is not null;
        }

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
        // SC-4 clauses 1, 2 and 6: the start-up pair is first or not at all, and nothing else goes until the download's last
        // record is in or the pair ended (no key can be held meanwhile: no press went before the pair)
        if (PairBlocksWrites)
        {
            return TakePairWrite();
        }

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

    /// <summary>The start-up pair is under way (<see cref="HspStartupPairStatus.BlocksWrites"/>); read on the session's thread.</summary>
    private bool PairBlocksWrites => _pairPhase is HspStartupPairPhase.Waiting or HspStartupPairPhase.VersionRequested or HspStartupPairPhase.VersionAccepted
        or HspStartupPairPhase.DownloadRequested or HspStartupPairPhase.Downloading;

    /// <summary>
    /// The pair's write whose place has come at this write slot, else null (SC-4 clause 1; F181: where SmartSoft placed all 40):
    /// `09 00 00` once reply header #40 is in and nothing is listed and unfetched; `07 00 00` once reply header #79 is in, the
    /// version gate passed and one more poll exchange completed after the ConsoleVersion object (a fetch may still be
    /// pending then: E9e's two `07`s went with a PlaybackInfo listed, before its fetch, as every write of ours goes).
    /// </summary>
    private ClearedSystemMessage? PairWriteDue() => _pairPhase switch
    {
        HspStartupPairPhase.Waiting when RepliesReceived >= StartupPairFirstAfterReplies && _pending.Count == 0 && _outstandingFetch is null
            => ClearedSystemMessage.ListVersion,
        HspStartupPairPhase.VersionAccepted when RepliesReceived >= StartupPairSecondAfterReplies && _exchangesSinceVersion >= 1
            => ClearedSystemMessage.ListShow,
        _ => null,
    };

    /// <summary>
    /// The pair's write for this slot, or null: its place must have come (<see cref="PairWriteDue"/>), the session must not be
    /// stopping, and the gate (asked without the lock) must send it; a hold keeps it for a later slot, a refusal ends the pair.
    /// The counter must stand at the pseq SmartSoft's pair always had (0, then 1): the pair is the session's first two data
    /// writes. A write taken here is committed; the caller writes it at once.
    /// </summary>
    private HspOutboundTransfer? TakePairWrite()
    {
        if (PairWriteDue() is not { } message)
        {
            return null;
        }

        var name = message == ClearedSystemMessage.ListVersion ? "09 00 00" : "07 00 00";
        if (!_acceptingWrites)
        {
            EndPair($"the session is stopping: {name} is not sent (a stop sends nothing new)");
            return null;
        }

        var decision = _pairGate!(this, message);
        switch (decision.Verdict)
        {
            case HspWriteVerdict.Hold:
                WriteHeldReason = $"start-up pair, {name}: {decision.Reason ?? "held back"}";
                return null;
            case HspWriteVerdict.Drop:
                WriteHeldReason = null;
                EndPair($"{name} refused: {decision.Reason ?? "refused"}");
                return null;
        }

        var expected = message == ClearedSystemMessage.ListVersion ? 0x00 : 0x01;
        if (_outboundSequence != expected)
        {
            WriteHeldReason = null;
            EndPair($"{name} is not sent: {WritesSent} other data write(s) went first, and the pair goes first or not at all (SC-4 clause 6)");
            return null;
        }

        WriteHeldReason = null;
        var payload = HspOutbound.SystemMessage(TakeSequence(), message);
        var replies = RepliesReceived;
        lock (_writeLock)
        {
            _pairPhase = message == ClearedSystemMessage.ListVersion ? HspStartupPairPhase.VersionRequested : HspStartupPairPhase.DownloadRequested;
        }

        _pairFreeReplies = 0;
        _notes.Enqueue(message == ClearedSystemMessage.ListVersion
            ? $"start-up pair (SC-4): System 09 00 00 sent after reply header #{replies}, nothing listed and unfetched (SmartSoft's #40, F181); ConsoleVersion awaited within {StartupVersionListedWithin} poll replies"
            : $"start-up pair (SC-4): System 07 00 00 sent after reply header #{replies} (SmartSoft's #79, F181), the version gate passed; the download awaited, nothing else is sent until its last record");
        return BuildWrite(payload);
    }

    /// <summary>The pair ends without the download, with its reason (refused, the version gate, a stop); the session goes on as before.</summary>
    private void EndPair(string reason)
    {
        lock (_writeLock)
        {
            if (_pairPhase is HspStartupPairPhase.None or HspStartupPairPhase.Done or HspStartupPairPhase.Ended)
            {
                return;
            }

            _pairPhase = HspStartupPairPhase.Ended;
            _pairEndReason = reason;
        }

        _notes.Enqueue($"start-up pair ended without the download: {reason}");
    }

    /// <summary>
    /// A free poll reply (type 4, idle or a list; one that answers a fetch is an object reply and cannot list) arrived while the
    /// pair waits for the console's answer: the ConsoleVersion listing after `09` (within <see cref="StartupVersionListedWithin"/>,
    /// else the pair ends), the download after `07` (its list names SystemSizes first, as all 20 do, F182 d; within
    /// <see cref="StartupDownloadListedWithin"/>, else a fault). Returns the fault's reason, else null.
    /// </summary>
    private string? PairFreeReply(DirtyObjectsList? list, int payloadLength)
    {
        switch (_pairPhase)
        {
            case HspStartupPairPhase.VersionRequested when !_versionListed:
                if (list is not null && list.Records.Contains(new DirtyObjectRecord((int)HspObjectId.ConsoleVersion, 0, 0, 0)))
                {
                    _versionListed = true;
                    _notes.Enqueue($"start-up pair: ConsoleVersion listed {_pairFreeReplies + 1} poll reply/replies after 09 00 00 (F182 a: the next free reply in 20 of 20)");
                    return null;
                }

                if (++_pairFreeReplies >= StartupVersionListedWithin)
                {
                    EndPair($"the console did not list ConsoleVersion within {StartupVersionListedWithin} poll replies after 09 00 00 (F182 a: at the next free reply in 20 of 20), so 07 00 00 is never sent in this session");
                }

                return null;
            case HspStartupPairPhase.DownloadRequested:
                if (list is not null && list.Records.Count > 0 && list.Records[0] == new DirtyObjectRecord((int)HspObjectId.SystemSizes, 0, 0, 0))
                {
                    lock (_writeLock)
                    {
                        _pairPhase = HspStartupPairPhase.Downloading;
                        _downloadRecords = list.Records.Count;
                        _downloadDelivered = 0;
                        _downloadListLength = payloadLength;
                    }

                    _notes.Enqueue($"start-up pair: the download listed {list.Records.Count} records in {payloadLength} bytes, {_pairFreeReplies + 1} poll reply/replies after 07 00 00 (F182: 157-161 records, 792-812 bytes)");
                    return null;
                }

                return ++_pairFreeReplies >= StartupDownloadListedWithin
                    ? $"the console did not list its download within {StartupDownloadListedWithin} poll replies after 07 00 00 (F182 b: at the next free reply in 20 of 20); the session ends rather than go on in a state no capture shows"
                    : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// An object was delivered: the ConsoleVersion of the version gate (SC-4 item 9: `07` only after "3.0.2.9.0.43", F59;
    /// anything else ends the pair), or one of the download's records (the last one ends the download, SC-4 clause 2).
    /// </summary>
    private void PairObjectDelivered(HspInboundPayload payload)
    {
        if (_pairPhase == HspStartupPairPhase.VersionRequested && _versionListed && payload.ObjectId == (int)HspObjectId.ConsoleVersion)
        {
            if (payload.Body.Span.SequenceEqual(StartupPairConsoleVersion))
            {
                lock (_writeLock)
                {
                    _pairPhase = HspStartupPairPhase.VersionAccepted;
                }

                _exchangesSinceVersion = -1;   // the exchange that delivered it does not count as the one after it
                _notes.Enqueue("start-up pair: ConsoleVersion reads 3.0.2.9.0.43 (F59), the version gate passed (S11.17 row 3)");
            }
            else
            {
                var text = new string(payload.Body.Span.ToArray().Skip(1).Select(b => b is >= 0x20 and < 0x7F ? (char)b : '?').ToArray());
                EndPair($"the console's version is not the one SmartSoft's pair was observed with (\"{text}\", body {Convert.ToHexString(payload.Body.Span)}; observed \"3.0.2.9.0.43\", F59), so 07 00 00 is never sent in this session");
            }

            return;
        }

        if (_pairPhase != HspStartupPairPhase.Downloading)
        {
            return;
        }

        bool done;
        lock (_writeLock)
        {
            _downloadDelivered++;
            done = _downloadDelivered >= _downloadRecords;
            if (done)
            {
                _pairPhase = HspStartupPairPhase.Done;
            }
        }

        if (done)
        {
            _notes.Enqueue($"start-up pair: the download's last record delivered ({_downloadRecords} records); writes are possible again");
        }
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

        if (parsed.Length > MaxPayloadLength)
        {
            return FaultWith($"reply header {parsed} announces {parsed.Length} bytes; nothing above {MaxPayloadLength} was ever observed{(SendsStartupPair ? " (the longest download list, F182)" : string.Empty)}", data);
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
        RepliesReceived++;
        if (header.Length > 0)
        {
            State = HspReadSessionState.AwaitingPayload;
        }
        else
        {
            // an idle reply: a free reply that listed nothing (the start-up pair counts it while it waits for an answer)
            if (PairFreeReply(list: null, payloadLength: 0) is { } deviation)
            {
                return FaultWith(deviation, data);
            }

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
        DirtyObjectsList? listed = null;
        switch (evt)
        {
            case DirtyListReceived list:
                payload = list.Payload;
                listed = list.List;
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

                if (!AcceptsBodyLength(payload.ObjectId, payload.Body.Length))
                {
                    return FaultWith($"object {payload.ObjectId} ({HspObjectIds.Name(payload.ObjectId)}) with a {payload.Body.Length}-byte body; every observed body of that id had {DescribeLengths(payload.ObjectId)} bytes (F67{(SendsStartupPair ? "; the downloads and E12 / E12b, F156, F182" : string.Empty)})", data);
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

        // the start-up pair (SC-4): a list is a free reply it may be waiting for; an object may be its ConsoleVersion or a
        // record of its download
        if (listed is not null)
        {
            if (PairFreeReply(listed, data.Length) is { } deviation)
            {
                return FaultWith(deviation, data);
            }
        }
        else
        {
            PairObjectDelivered(payload);
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
        if (_pairPhase == HspStartupPairPhase.VersionAccepted)
        {
            _exchangesSinceVersion++;
        }
    }

    /// <summary>SC-1's one length per id (<see cref="ObservedBodyLengths"/>), or with the start-up pair the lengths of <see cref="DownloadBodyLengths"/>.</summary>
    private bool AcceptsBodyLength(int objectId, int length) => SendsStartupPair
        ? DownloadBodyLengths.TryGetValue(objectId, out var lengths) && lengths.Contains(length)
        : ObservedBodyLengths.TryGetValue(objectId, out var observed) && observed == length;

    private string DescribeLengths(int objectId) => SendsStartupPair
        ? DownloadBodyLengths.TryGetValue(objectId, out var lengths) ? string.Join(" or ", lengths.Order()) : "an unknown number of"
        : ObservedBodyLengths.TryGetValue(objectId, out var known) ? known.ToString(System.Globalization.CultureInfo.InvariantCulture) : "an unknown number of";

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

    /// <summary>A key press; with <paramref name="Tap"/> its release is due at the next write slot, else when <see cref="ReleaseKey"/> asks for it (a held bump, or the golden tests' press).</summary>
    private sealed record KeyPress(ClearedKey Key, bool Tap, Func<HspWriteDecision>? Condition, Action? Sent) : QueuedWrite(Condition, Sent);
}
