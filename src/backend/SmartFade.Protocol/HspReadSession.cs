namespace SmartFade.Protocol;

/// <summary>What the session wants sent next.</summary>
public sealed record HspOutboundTransfer(byte[] Bytes, HspMessageType Kind)
{
    /// <summary>A poll is answered by a reply header; a fetch is not (its object comes with the next poll's reply, F53).</summary>
    public bool ExpectsReply => Kind == HspMessageType.Poll;
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
/// The read-only HSP session as SmartSoft runs it, as a state machine without I/O: it decides
/// what to send next and checks every reply against the VERIFIED behaviour of the console
/// (F51–F55, F57). It sends polls, and fetches only records that the console listed in a
/// DirtyObjects list of this session, in list order, once; a fetch is always followed by a poll
/// and that poll is always answered by the fetched object (E2, E3, E3b: 1 651 of 1 651). It
/// never sends a Data header or a payload, and nothing is re-sent. Any deviation from the
/// observed behaviour faults the session: a reply type other than 3 or 4, a poll reply where
/// the fetched object was due, a wrong echo, a non-zero word in a poll reply, an announced
/// length above anything observed, a payload byte 1 other than 0x00, a break in the sequence
/// counter, an empty list, a listed record with c ≠ 0, with the list's own id or with an id that
/// SmartSoft never fetched. The caller then stops and closes the pipe. Cleared by safety check
/// SC-1 (docs/protocol/safety-checks.md).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F55, F57", "cleared by SC-1 (2026-10-30): poll and listed fetch only")]
public sealed class HspReadSession
{
    /// <summary>The longest reply payload any capture holds: 792 bytes, the 157-record list (F57, E2 f384 / E3 f352 / E3b); anything longer is unobserved.</summary>
    public const int MaxObservedPayloadLength = 792;

    /// <summary>
    /// Body length of every object the console delivered, one length per id in every capture
    /// (F67 for the ids of E2 and E3; InputLevels 19 from E3 alone, F81, OBSERVED once). A body of
    /// another length has never been observed and faults the session.
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
    /// True when the console owes nothing: no fetch outstanding, nothing listed and unfetched, no
    /// reply due. A fetch is then never left without its poll. (SmartSoft's own sessions all
    /// ended one step later, on an all-zero poll answered idle, F64; this state is also true right
    /// after the last listed object was delivered, before the next poll.)
    /// </summary>
    public bool IsIdle => State == HspReadSessionState.ReadyToSend && _outstandingFetch is null && _pending.Count == 0 && !_pollDueAfterFetch;

    public int PollsSent { get; private set; }

    public int FetchesSent { get; private set; }

    public int ListsReceived { get; private set; }

    public int ObjectsReceived { get; private set; }

    /// <summary>The next transfer: the next listed record's fetch, or a poll (always a poll right after a fetch).</summary>
    public HspOutboundTransfer Next()
    {
        if (State != HspReadSessionState.ReadyToSend)
        {
            throw new InvalidOperationException($"Nothing may be sent while the session is {State}.");
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

    private IReadOnlyList<HspEvent> ReceiveHeader(ReadOnlyMemory<byte> data)
    {
        if (data.Length != HspHeader.Size)
        {
            return FaultWith($"reply header of {data.Length} bytes instead of {HspHeader.Size}", data);
        }

        var parsed = HspHeader.Parse(data.Span);
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
        State = header.Length > 0 ? HspReadSessionState.AwaitingPayload : HspReadSessionState.ReadyToSend;
        return new HspEvent[] { reply };
    }

    private IReadOnlyList<HspEvent> ReceivePayload(ReadOnlyMemory<byte> data)
    {
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
                        return FaultWith($"listed record {record} with c = {record.C} (c = 0 in every observed record)", data);
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
        State = HspReadSessionState.ReadyToSend;
        return new[] { evt };
    }

    private IReadOnlyList<HspEvent> FaultWith(string reason, ReadOnlyMemory<byte> data)
    {
        State = HspReadSessionState.Faulted;
        Fault = reason;
        return new HspEvent[] { new UnexpectedTransfer(HspDirection.FromConsole, data, reason) };
    }
}
