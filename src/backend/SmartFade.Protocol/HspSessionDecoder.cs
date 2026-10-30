namespace SmartFade.Protocol;

public enum HspDirection
{
    /// <summary>PC → console (bulk OUT).</summary>
    ToConsole,

    /// <summary>Console → PC (bulk IN).</summary>
    FromConsole,
}

/// <summary>An event decoded from the transfer stream of an HSP session.</summary>
public abstract record HspEvent(HspDirection Direction);

public sealed record PollSent(HspHeader Header) : HspEvent(HspDirection.ToConsole);

public sealed record FetchSent(HspHeader Header) : HspEvent(HspDirection.ToConsole)
{
    public DirtyObjectRecord Target => new(Header.ObjectId, Header.QualifierA, Header.QualifierB, Header.QualifierC);
}

public sealed record DataHeaderSent(HspHeader Header) : HspEvent(HspDirection.ToConsole);

public sealed record DataSent(HspHeader Header, HspOutboundPayload Payload) : HspEvent(HspDirection.ToConsole);

public sealed record ReplyHeaderReceived(HspHeader Header) : HspEvent(HspDirection.FromConsole)
{
    public bool IsIdle => Header.MessageType == HspMessageType.PollReply && Header.Length == 0;
}

public sealed record ObjectReceived(HspHeader Header, HspInboundPayload Payload) : HspEvent(HspDirection.FromConsole);

public sealed record DirtyListReceived(HspHeader Header, HspInboundPayload Payload, DirtyObjectsList List) : HspEvent(HspDirection.FromConsole);

public sealed record UnexpectedTransfer(HspDirection Direction, ReadOnlyMemory<byte> Data, string Reason) : HspEvent(Direction);

/// <summary>
/// Decodes the two directions of an HSP session, transfer by transfer, into events. It works
/// on captured transfers (one bulk URB each, as the normalizer writes them): a 12-byte header,
/// then, when the header announces a length, exactly that many payload bytes in the next
/// transfer of the same direction (VERIFIED framing, F51–F54). Decoder only: it never produces
/// bytes for a console.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F51, F52, F53, F54, F57")]
public sealed class HspSessionDecoder
{
    private HspHeader? _pendingOut;
    private HspHeader? _pendingIn;

    public HspEvent Feed(HspDirection direction, ReadOnlyMemory<byte> data) =>
        direction == HspDirection.ToConsole ? FeedOut(data) : FeedIn(data);

    private HspEvent FeedOut(ReadOnlyMemory<byte> data)
    {
        if (_pendingOut is { } header)
        {
            _pendingOut = null;
            if (data.Length != header.Length)
            {
                return new UnexpectedTransfer(HspDirection.ToConsole, data, $"expected a {header.Length}-byte data payload, got {data.Length} bytes");
            }

            return new DataSent(header, HspOutboundPayload.Parse(data));
        }

        if (!HspHeader.TryParse(data.Span, out var parsed))
        {
            return new UnexpectedTransfer(HspDirection.ToConsole, data, $"not a 12-byte header ({data.Length} bytes)");
        }

        switch (parsed.MessageType)
        {
            case HspMessageType.Poll:
                return new PollSent(parsed);
            case HspMessageType.Fetch:
                return new FetchSent(parsed);
            case HspMessageType.Data when parsed.Length > 0:
                _pendingOut = parsed;
                return new DataHeaderSent(parsed);
            default:
                return new UnexpectedTransfer(HspDirection.ToConsole, data, $"unexpected outbound header {parsed}");
        }
    }

    private HspEvent FeedIn(ReadOnlyMemory<byte> data)
    {
        if (_pendingIn is { } header)
        {
            _pendingIn = null;
            if (data.Length != header.Length)
            {
                return new UnexpectedTransfer(HspDirection.FromConsole, data, $"expected a {header.Length}-byte payload, got {data.Length} bytes");
            }

            var payload = HspInboundPayload.Parse(data);
            if (header.MessageType == HspMessageType.PollReply && payload.ObjectId == (int)HspObjectId.DirtyObjects)
            {
                return new DirtyListReceived(header, payload, DirtyObjectsList.Decode(payload.Body.Span));
            }

            return new ObjectReceived(header, payload);
        }

        if (!HspHeader.TryParse(data.Span, out var parsed))
        {
            return new UnexpectedTransfer(HspDirection.FromConsole, data, $"not a 12-byte header ({data.Length} bytes)");
        }

        if (!parsed.IsFromConsole)
        {
            return new UnexpectedTransfer(HspDirection.FromConsole, data, $"unexpected inbound header {parsed}");
        }

        if (parsed.Length > 0)
        {
            _pendingIn = parsed;
        }

        return new ReplyHeaderReceived(parsed);
    }
}
