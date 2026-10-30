using SmartFade.Communication;
using SmartFade.Communication.Capture;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// The read-only session engine replayed over the committed captures: every transfer it would
/// send is compared byte for byte with what SmartSoft sent at the same point (ReplayHspPipe),
/// and every reply the console gave passes its checks. SmartSoft's own Data writes (two System
/// messages per session, F58) are the only transfers skipped. These are the golden tests behind
/// safety check SC-1 (docs/protocol/safety-checks.md).
/// </summary>
public class HspReadSessionTests
{
    private static (HspReadSession Session, ReplayHspPipe Pipe, List<HspEvent> Events) Replay(string fileName)
    {
        var pipe = new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture(fileName)));
        var session = new HspReadSession();
        var events = new List<HspEvent>();
        pipe.Open();
        var buffer = new byte[4096];
        while (!pipe.AtEnd)
        {
            var next = session.Next();
            pipe.Write(next.Bytes);
            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                events.AddRange(session.Receive(span.ToArray()));
                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }
        }

        return (session, pipe, events);
    }

    [Theory]
    [InlineData("2026-10-28-e2-smartsoft-connect.jsonl", 3001, 228, 16, 2)]
    [InlineData("2026-10-29-e3-console-actions.jsonl", 9692, 423, 87, 2)]
    [InlineData("2026-10-30-e3b-live-objects.jsonl", 25824, 1000, 268, 6)]
    public void Sends_exactly_what_SmartSoft_sent_and_accepts_every_reply(string fileName, int polls, int fetches, int lists, int smartSoftWrites)
    {
        var (session, pipe, events) = Replay(fileName);

        Assert.Equal(polls, session.PollsSent);
        Assert.Equal(fetches, session.FetchesSent);
        Assert.Equal(lists, session.ListsReceived);
        Assert.Equal(fetches, session.ObjectsReceived);
        Assert.Equal(polls + fetches, pipe.MatchedWrites);
        Assert.Equal(smartSoftWrites, pipe.SkippedSmartSoftWrites);
        Assert.Equal(0, pipe.Remaining);
        Assert.Equal(0, session.PendingFetches);
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
        Assert.Null(session.Fault);
        Assert.Empty(events.OfType<UnexpectedTransfer>());
        Assert.Equal(polls, events.OfType<ReplyHeaderReceived>().Count());
    }

    private static readonly Lazy<CaptureFile> E2 = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl")));

    /// <summary>The bulk OUT transfer on endpoint 0x04 (interface 1, F51) of the E2 capture with that JSONL seq: bytes SmartSoft sent.</summary>
    private static byte[] E2Out(int seq) =>
        Assert.Single(E2.Value.Frames, f => f.Seq == seq && f.Direction == CaptureDirection.Out && f.IsBulk && f.Endpoint == "0x04" && !f.Synthetic).Data;

    [Fact]
    public void The_first_poll_is_all_zero_and_a_fetch_carries_the_listed_record()
    {
        // Golden bytes loaded from docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl (JSONL seq numbers):
        // seq 6 the all-zero first poll (pcap f11), seq 9 the first fetch, PlaybackInfo id 17 with a = b = c = 0 (f17),
        // seq 10 the poll after it, repeating the fetch's words (f19), seq 14 the poll after the fetch of FaderInfo (id 14).
        Assert.Equal(E2Out(6), HspOutbound.Poll(0, 0, 0, 0));
        Assert.Equal(E2Out(9), HspOutbound.Fetch(new DirtyObjectRecord(17, 0, 0, 0)));
        Assert.Equal(E2Out(10), HspOutbound.Poll(17, 0, 0, 0));
        Assert.Equal(E2Out(14), HspOutbound.Poll(14, 0, 0, 0));

        // the same bytes spelled out, so a reader sees them without the capture (F52)
        Assert.Equal(new byte[12], E2Out(6));
        Assert.Equal(Convert.FromHexString("020000001100000000000000"), E2Out(9));
        Assert.Equal(Convert.FromHexString("000000001100000000000000"), E2Out(10));
        Assert.Equal(Convert.FromHexString("000000000E00000000000000"), E2Out(14));
    }

    [Fact]
    public void A_fetch_is_always_followed_by_a_poll_even_with_more_records_listed()
    {
        var session = new HspReadSession();
        Drive(session, Idle());
        // a list of two records arrives
        var list = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0), new DirtyObjectRecord(15, 0, 0, 0));
        Drive(session, Header(4, (ushort)list.Length), list);

        var first = session.Next();
        Assert.Equal(HspMessageType.Fetch, first.Kind);
        Assert.Equal(1, session.PendingFetches);
        var second = session.Next();
        Assert.Equal(HspMessageType.Poll, second.Kind);
        // the poll repeats the fetch's words: E2 JSONL seq 14, the poll after the fetch of FaderInfo (F52), 00 00 00 00 0E 00 00 00 00 00 00 00
        Assert.Equal(E2Out(14), second.Bytes);
    }

    [Fact]
    public void Faults_on_an_unobserved_reply_type()
    {
        var session = new HspReadSession();
        session.Next();
        var events = session.Receive(Header(5, 0));

        Assert.Equal(HspReadSessionState.Faulted, session.State);
        Assert.Contains("unobserved type", session.Fault, StringComparison.Ordinal);
        Assert.IsType<UnexpectedTransfer>(Assert.Single(events));
        Assert.Throws<InvalidOperationException>(() => session.Next());
    }

    [Fact]
    public void Faults_when_an_object_reply_does_not_echo_the_fetch()
    {
        var session = new HspReadSession();
        Drive(session, Idle());
        var list = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0));
        Drive(session, Header(4, (ushort)list.Length), list);
        session.Next();   // the fetch of 14
        session.Next();   // the poll

        session.Receive(Header(3, 20, 15, 0, 0, 0));

        Assert.Equal(HspReadSessionState.Faulted, session.State);
        Assert.Contains("does not echo", session.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Faults_on_a_payload_byte_1_or_a_sequence_break_never_seen_from_the_console()
    {
        var session = new HspReadSession();
        var withByte1 = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0));
        withByte1[1] = 0x01;
        session.Next();
        session.Receive(Header(4, (ushort)withByte1.Length));
        session.Receive(withByte1);
        Assert.Contains("byte 1", session.Fault, StringComparison.Ordinal);

        // a complete list → fetch → object cycle, then a list whose sequence skips a value
        var broken = new HspReadSession();
        var list = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0));
        Drive(broken, Header(4, (ushort)list.Length), list);
        broken.Next();   // fetch
        broken.Next();   // poll
        var obj = Payload(0x11, 14, new byte[195]);
        broken.Receive(Header(3, (ushort)obj.Length, 14));
        broken.Receive(obj);
        Assert.True(broken.IsIdle, broken.Fault);
        var skipped = ListPayload(0x13, new DirtyObjectRecord(14, 0, 0, 0));
        broken.Next();
        broken.Receive(Header(4, (ushort)skipped.Length));
        broken.Receive(skipped);
        Assert.Contains("sequence", broken.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Faults_when_the_poll_after_a_fetch_is_answered_idle_instead_of_with_the_object()
    {
        var session = new HspReadSession();
        Drive(session, Idle());
        var list = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0));
        Drive(session, Header(4, (ushort)list.Length), list);
        session.Next();   // the fetch of 14
        session.Next();   // the poll

        session.Receive(Idle());

        Assert.Equal(HspReadSessionState.Faulted, session.State);
        Assert.Contains("where the object of the fetch", session.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Faults_on_lists_no_capture_has_shown()
    {
        var empty = new HspReadSession();
        empty.Next();
        var emptyList = ListPayload(0x10);
        empty.Receive(Header(4, (ushort)emptyList.Length));
        empty.Receive(emptyList);
        Assert.Contains("empty DirtyObjects list", empty.Fault, StringComparison.Ordinal);

        var withC = new HspReadSession();
        withC.Next();
        var cList = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 1));
        withC.Receive(Header(4, (ushort)cList.Length));
        withC.Receive(cList);
        Assert.Contains("c = 1", withC.Fault, StringComparison.Ordinal);

        var unknownId = new HspReadSession();
        unknownId.Next();
        var idList = ListPayload(0x10, new DirtyObjectRecord(20, 0, 0, 0));   // ConsoleEvent: never listed in any capture
        unknownId.Receive(Header(4, (ushort)idList.Length));
        unknownId.Receive(idList);
        Assert.Contains("never seen to fetch", unknownId.Fault, StringComparison.Ordinal);
        Assert.Equal(0, unknownId.PendingFetches);

        var itself = new HspReadSession();
        itself.Next();
        var selfList = ListPayload(0x10, new DirtyObjectRecord(40, 0, 0, 0));
        itself.Receive(Header(4, (ushort)selfList.Length));
        itself.Receive(selfList);
        Assert.Contains("never seen to fetch", itself.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Faults_on_an_announced_length_above_anything_observed_or_a_payload_too_short_to_parse()
    {
        var huge = new HspReadSession();
        huge.Next();
        huge.Receive(Header(4, 2000));
        Assert.Contains("nothing above 792", huge.Fault, StringComparison.Ordinal);

        var tiny = new HspReadSession();
        tiny.Next();
        tiny.Receive(Header(4, 3));
        tiny.Receive(new byte[3]);
        Assert.Contains("shorter than", tiny.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Is_idle_only_with_nothing_outstanding_or_pending()
    {
        var session = new HspReadSession();
        Assert.True(session.IsIdle);
        session.Next();
        Assert.False(session.IsIdle);   // a reply is due
        session.Receive(Idle());
        Assert.True(session.IsIdle);

        var list = ListPayload(0x10, new DirtyObjectRecord(14, 0, 0, 0));
        Drive(session, Header(4, (ushort)list.Length), list);
        Assert.False(session.IsIdle);   // a record is pending
        session.Next();                 // fetch
        Assert.False(session.IsIdle);   // its poll is due
        session.Next();                 // poll
        var obj = Payload(0x11, 14, new byte[195]);
        session.Receive(Header(3, (ushort)obj.Length, 14));
        session.Receive(obj);
        Assert.True(session.IsIdle);
    }

    [Fact]
    public void Faults_on_a_poll_reply_with_non_zero_words_or_a_short_header()
    {
        var session = new HspReadSession();
        session.Next();
        session.Receive(Header(4, 0, 1, 0, 0, 0));
        Assert.Contains("non-zero", session.Fault, StringComparison.Ordinal);

        var shortHeader = new HspReadSession();
        shortHeader.Next();
        shortHeader.Receive(new byte[11]);
        Assert.Contains("11 bytes", shortHeader.Fault, StringComparison.Ordinal);
    }

    private static void Drive(HspReadSession session, params byte[][] inbound)
    {
        var next = session.Next();
        Assert.Equal(HspMessageType.Poll, next.Kind);
        foreach (var transfer in inbound)
        {
            session.Receive(transfer);
            Assert.True(session.State != HspReadSessionState.Faulted, session.Fault);
        }

        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    /// <summary>The idle poll reply `04 00 00 00 00 00 00 00 00 00 00 00` (F53, E2 f92 and every idle reply since).</summary>
    private static byte[] Idle() => Header(4, 0);

    /*
     * The replies built by Header / Payload / ListPayload below are hand-made inputs for the fault
     * paths and the state machine (a capture cannot contain the console misbehaving); their shapes
     * follow F53 (reply header), F54 (payload layout) and F57 (DirtyObjects body). Bodies passed as
     * zero bytes stand in for an object's content only where the test is about the framing, not
     * the content (a captured FaderInfo body starts with 0x30, F82).
     */

    private static byte[] Header(ushort type, ushort length, ushort w2 = 0, ushort w3 = 0, ushort w4 = 0, ushort w5 = 0)
    {
        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes.AsSpan(0), type);
        BitConverter.TryWriteBytes(bytes.AsSpan(2), length);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), w2);
        BitConverter.TryWriteBytes(bytes.AsSpan(6), w3);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), w4);
        BitConverter.TryWriteBytes(bytes.AsSpan(10), w5);
        return bytes;
    }

    private static byte[] Payload(byte sequence, int objectId, byte[] body)
    {
        var payload = new byte[5 + body.Length];
        payload[0] = sequence;
        payload[1] = 0;
        payload[2] = (byte)body.Length;
        payload[3] = (byte)(body.Length >> 8);
        payload[4] = (byte)objectId;
        body.CopyTo(payload, 5);
        return payload;
    }

    /// <summary>A DirtyObjects payload as the console sends it: [seq][00][u16 LE len][40][u16 BE count][records] (F54, F57).</summary>
    private static byte[] ListPayload(byte sequence, params DirtyObjectRecord[] records)
    {
        var body = new byte[2 + records.Length * 5];
        body[0] = (byte)(records.Length >> 8);
        body[1] = (byte)records.Length;
        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            body[2 + i * 5] = (byte)r.ObjectId;
            body[3 + i * 5] = (byte)(r.A >> 8);
            body[4 + i * 5] = (byte)r.A;
            body[5 + i * 5] = (byte)r.B;
            body[6 + i * 5] = (byte)r.C;
        }

        var payload = new byte[5 + body.Length];
        payload[0] = sequence;
        payload[1] = 0;
        payload[2] = (byte)body.Length;
        payload[3] = (byte)(body.Length >> 8);
        payload[4] = (byte)HspObjectId.DirtyObjects;
        body.CopyTo(payload, 5);
        return payload;
    }
}
