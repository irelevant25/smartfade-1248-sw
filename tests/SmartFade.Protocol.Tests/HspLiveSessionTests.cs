using SmartFade.Communication;
using SmartFade.Communication.Capture;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// The write path of safety check SC-2 (classes A and B1) against SmartSoft's own writes in E9
/// (docs/protocol/captures/2026-10-30-e9-smartsoft-writes.jsonl) and E9b
/// (docs/protocol/captures/2026-10-31-e9b-smartsoft-writes-repeat.jsonl). The encoder reproduces SmartSoft's bytes for
/// every whitelisted index and code; the session, replayed over the captures and handed SmartSoft's event at each of
/// SmartSoft's whitelisted writes, reproduces header, stale words, counter, previous byte and placement (SC-2
/// condition 2). The rules a replay cannot show (the whitelist typing, the read-only session, condition 8, the tap,
/// the gate, the stop, the release after a fault) are tested on hand-made replies below.
/// </summary>
public class HspLiveSessionTests
{
    private const string E9File = "2026-10-30-e9-smartsoft-writes.jsonl";
    private const string E9bFile = "2026-10-31-e9b-smartsoft-writes-repeat.jsonl";

    private static readonly Lazy<CaptureFile> E9 = new(() => CaptureFile.Load(TestPaths.Capture(E9File)));
    private static readonly Lazy<CaptureFile> E9b = new(() => CaptureFile.Load(TestPaths.Capture(E9bFile)));

    private byte _inboundSequence;

    /// <summary>The bulk OUT transfer on endpoint 0x04 with that JSONL seq: bytes SmartSoft sent.</summary>
    private static byte[] Out(Lazy<CaptureFile> capture, int seq) =>
        Assert.Single(capture.Value.Frames, f => f.Seq == seq && f.Direction == CaptureDirection.Out && f.IsBulk && f.Endpoint == "0x04" && !f.Synthetic).Data;

    [Fact]
    public void Encode_FaderEvent_MatchesCapture_E9_Seq9665()
    {
        // SmartSoft's first fader event in E9: header seq 9664 (f19327), payload seq 9665 (f19329), channel fader 1 to 0x05
        // from previous 0x00 with counter 0x02 (its System pair had 0x00 and 0x01; F131, F132).
        var smartSofts = Out(E9, 9665);
        var payload = HspOutbound.FaderEvent(smartSofts[0], ClearedFader.ChannelFader1, smartSofts[6], smartSofts[7]);

        Assert.Equal(smartSofts, payload);
        Assert.Equal(Out(E9, 9664), HspOutbound.DataHeader(payload, 0, 0, 0, 0));

        // the same bytes spelled out, so a reader sees them without the capture (E9 seq 9664 / 9665)
        Assert.Equal(Convert.FromHexString("010008000000000000000000"), Out(E9, 9664));
        Assert.Equal(Convert.FromHexString("0204001400000500"), smartSofts);
    }

    [Theory]
    [InlineData(E9File, 9665, ClearedFader.ChannelFader1)]
    [InlineData(E9File, 24115, ClearedFader.Master)]
    [InlineData(E9File, 29434, ClearedFader.Bumps)]
    [InlineData(E9bFile, 8038, ClearedFader.ChannelFader1)]
    [InlineData(E9bFile, 13144, ClearedFader.Master)]
    [InlineData(E9bFile, 16306, ClearedFader.Bumps)]
    public void Encode_each_whitelisted_fader_MatchesCapture(string file, int payloadSeq, ClearedFader fader)
    {
        // The first event of each index in E9 and E9b (F132): only the typed index comes from the test.
        var capture = file == E9File ? E9 : E9b;
        var smartSofts = Out(capture, payloadSeq);
        var header = Out(capture, payloadSeq - 1);

        var payload = HspOutbound.FaderEvent(smartSofts[0], fader, smartSofts[6], smartSofts[7]);

        Assert.Equal(smartSofts, payload);
        Assert.Equal(header, HspOutbound.DataHeader(payload, Word(header, 4), Word(header, 6), Word(header, 8), Word(header, 10)));
    }

    [Theory]
    [InlineData(19055, ClearedKey.Bump7, true)]
    [InlineData(19436, ClearedKey.Bump7, false)]
    [InlineData(39596, ClearedKey.Bump1, true)]
    [InlineData(73996, ClearedKey.Bump9, true)]
    [InlineData(43870, ClearedKey.Go, true)]
    [InlineData(36386, ClearedKey.Solo, true)]
    [InlineData(33542, ClearedKey.Blackout, true)]
    [InlineData(41449, ClearedKey.Faders1To24, true)]
    [InlineData(39472, ClearedKey.FadersMems, true)]
    public void Encode_each_whitelisted_key_MatchesCapture_E9(int payloadSeq, ClearedKey key, bool pressed)
    {
        // E9's first press of each of the eight codes, and bump 7's release (F133; the code tables of the E9 record).
        var smartSofts = Out(E9, payloadSeq);
        var header = Out(E9, payloadSeq - 1);

        var payload = HspOutbound.KeyEvent(smartSofts[0], key, pressed);

        Assert.Equal(smartSofts, payload);
        Assert.Equal(header, HspOutbound.DataHeader(payload, Word(header, 4), Word(header, 6), Word(header, 8), Word(header, 10)));
    }

    [Fact]
    public void The_whitelist_is_exactly_SC2s()
    {
        // SC-2 condition 1: the indices of channel fader 1, MASTER and BUMPS and the codes of bumps 1 / 7 / 9, GO, SOLO,
        // BLACKOUT, FADERS 1-24 and MEMS, as SmartSoft's first event of each carries them in E9 (the payload's byte 5),
        // and nothing else.
        var indices = new[] { 9665, 24115, 29434 }.Select(seq => Out(E9, seq)[5]).Order().ToArray();
        var codes = new[] { 39596, 19055, 73996, 43870, 36386, 33542, 41449, 39472 }.Select(seq => Out(E9, seq)[5]).Order().ToArray();

        Assert.Equal(indices, Enum.GetValues<ClearedFader>().Select(f => (byte)f).Order().ToArray());
        Assert.Equal(codes, Enum.GetValues<ClearedKey>().Select(k => (byte)k).Order().ToArray());
    }

    [Fact]
    public void Refuses_every_index_code_and_payload_outside_the_whitelist()
    {
        // A value cast into the typed whitelist is refused at the boundary (SC-2 condition 1): fader 7's index 0x06
        // (E9 seq 67236, inside EDIT MEM) and REC MEM's code 0x41 (E9 seq 73006, class B2).
        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.FaderEvent(0, (ClearedFader)Out(E9, 67236)[5], 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.KeyEvent(0, (ClearedKey)Out(E9, 73006)[5], pressed: true));

        // No Data header for anything SmartSoft wrote outside the whitelist: the System write (E9 seq 138), a ChannelText
        // label (seq 57881), the index-0x06 event, REC MEM, and E9b's Memory write (seq 92053).
        foreach (var payload in new[] { Out(E9, 138), Out(E9, 57881), Out(E9, 67236), Out(E9, 73006), Out(E9b, 92053) })
        {
            Assert.False(HspOutbound.IsClearedConsoleEvent(payload), Convert.ToHexString(payload));
            Assert.Throws<ArgumentException>(() => HspOutbound.DataHeader(payload, 0, 0, 0, 0));
        }

        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9, 9665)));
        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9, 19436)));
    }

    /// <summary>
    /// The golden replays of SC-2 condition 2. Each range is one SmartSoft session with a session of ours: E9 had one
    /// (up to the poll at seq 68339, whose reply lists System (39, 5, 5, 6) with c = 6 after SmartSoft's EDIT MEM
    /// selection, F137: a session cleared for reads faults there by design); E9b had three (its record: S1 seq 4 …
    /// 64680, S2 64681 … 70067, S3 70068 …), each restarting SmartSoft's counter and previous bytes (F131, F132).
    /// </summary>
    [Theory]
    [InlineData(E9File, int.MinValue, 68339, 288, 51)]
    [InlineData(E9bFile, int.MinValue, 64681, 278, 110)]
    [InlineData(E9bFile, 64681, 70068, 0, 3)]
    [InlineData(E9bFile, 70068, int.MaxValue, 29, 7)]
    public void Sends_SmartSofts_whitelisted_events_byte_for_byte_where_SmartSoft_sent_them(string file, int firstSeq, int endSeq, int writes, int skipped)
    {
        var capture = file == E9File ? E9 : E9b;
        var pipe = new ReplayHspPipe(capture.Value, payload => HspOutbound.IsClearedConsoleEvent(payload.Span), firstSeq, endSeq);
        if (file == E9File)
        {
            // F132's one exception in E9: pseq 0x44 (seq 16560) carries previous 0x5D, a value that never reached the wire
            // (INFERRED: SmartSoft's coalescing). This host's previous byte is the last value it sent (SC-2 condition 3):
            // 0x5F, the value of pseq 0x43 (seq 16554).
            var expected = Out(E9, 16560).ToArray();   // a copy: the capture's own bytes stay as recorded
            expected[7] = Out(E9, 16554)[6];
            pipe.ExpectInstead(16560, expected);
        }

        var session = ReplayLive(pipe);

        Assert.Equal(writes, pipe.MatchedDataWrites);
        Assert.Equal(writes, session.WritesSent);
        Assert.Equal(skipped, pipe.SkippedSmartSoftWrites);
        Assert.Equal(0, pipe.Remaining);
        Assert.Null(session.Fault);
        Assert.Null(session.HeldKey);
        Assert.Equal(0, session.QueuedWrites);
        Assert.Equal(file == E9File ? new[] { 16560 } : Array.Empty<int>(), pipe.DeviationsMet);
    }

    [Fact]
    public void A_read_only_session_accepts_no_write()
    {
        var session = new HspReadSession();

        Assert.False(session.CanWrite);
        Assert.Throws<InvalidOperationException>(() => session.QueueFader(ClearedFader.Master, 0x80));
        Assert.Throws<InvalidOperationException>(() => session.QueueKeyTap(ClearedKey.Go));
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
    }

    [Fact]
    public void Nothing_is_written_before_the_first_list_and_the_LED_state()
    {
        var (session, gate) = LiveSession();
        session.QueueFader(ClearedFader.Master, 0x80);

        IdleExchange(session);
        IdleExchange(session);   // SC-2 condition 8: no list, no LED state yet, so no write
        Assert.Contains("condition 8", session.WriteHeldReason, StringComparison.Ordinal);
        ReportLeds(session);     // the list and LedStatus arrive; the write slot after them carries the write
        Assert.Equal(0, session.WritesSent);
        Assert.Equal(0, gate.Asked);

        var write = session.Next();

        Assert.True(write.IsWrite);
        Assert.Equal(1, gate.Asked);
        Assert.Null(session.WriteHeldReason);
    }

    [Fact]
    public void A_write_goes_right_after_a_poll_exchange_before_a_pending_fetch_and_never_between_a_fetch_and_its_poll()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.FaderInfo, 0, 0, 0), new DirtyObjectRecord((int)HspObjectId.ChannelLevels, 0, 0, 0));
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);

        session.QueueFader(ClearedFader.Master, 0x80);
        Assert.True(session.Next().IsWrite);                          // before the pending fetches, as SmartSoft's writes (E9, E9b)
        session.QueueFader(ClearedFader.Bumps, 0x40);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);      // at most one write per poll
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // never between a fetch and its poll
        var faderInfo = Payload(NextInbound(), (int)HspObjectId.FaderInfo, new byte[195]);
        session.Receive(Header(3, (ushort)faderInfo.Length, (ushort)HspObjectId.FaderInfo));
        session.Receive(faderInfo);

        var second = session.Next();                                  // the slot after the object reply

        Assert.True(second.IsWrite);
        Assert.Equal((byte)ClearedFader.Bumps, second.Payload![5]);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);
    }

    [Fact]
    public void The_counter_starts_at_zero_and_the_previous_byte_is_this_hosts_last_value()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);

        session.QueueFader(ClearedFader.Master, 0x80);
        var first = session.Next();
        IdleExchange(session);
        session.QueueFader(ClearedFader.Master, 0x40);
        session.QueueFader(ClearedFader.Master, 0x20);                 // replaces the queued 0x40, which never goes out
        var second = session.Next();
        IdleExchange(session);
        session.QueueFader(ClearedFader.ChannelFader1, 0x10);
        var third = session.Next();

        // SmartSoft's own events (E9 seq 24115, the MASTER's first, and seq 9665, channel fader 1's first) with this
        // session's counter, value and previous byte (F131, F132); after an idle reply the header carries zero stale
        // words, as SmartSoft's at seq 9664
        Assert.Equal(AsSent(24115, 0x00, value: 0x80, previous: 0x00), first.Payload);
        Assert.Equal(AsSent(24115, 0x01, value: 0x20, previous: 0x80), second.Payload);
        Assert.Equal(AsSent(9665, 0x02, value: 0x10, previous: 0x00), third.Payload);
        Assert.Equal(Out(E9, 9664), third.Bytes);
        Assert.Equal(3, session.WritesSent);
    }

    [Fact]
    public void The_counter_wraps_from_0xFF_to_0x00()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);

        byte last = 0;
        for (var i = 0; i < 257; i++)
        {
            session.QueueFader(ClearedFader.Bumps, (byte)i);
            last = session.Next().Payload![0];
            IdleExchange(session);
        }

        Assert.Equal(0x00, last);   // the 257th write (F131: E9b wrapped 0xFF -> 0x00 at seq 39860)
    }

    [Fact]
    public void A_tap_is_its_press_then_its_release_at_the_next_slot_with_nothing_between()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Go);
        session.QueueFader(ClearedFader.Master, 0x10);

        var press = session.Next();
        Assert.Equal(ClearedKey.Go, session.HeldKey);
        Assert.False(session.IsIdle);                                 // a press is never left without its release
        IdleExchange(session);
        var release = session.Next();
        IdleExchange(session);
        var fader = session.Next();

        // SmartSoft's GO press and release (E9 seq 43870, 43953) with this session's counter
        Assert.Equal(AsSent(43870, 0x00), press.Payload);
        Assert.Equal(AsSent(43953, 0x01), release.Payload);
        Assert.Equal((byte)ClearedFader.Master, fader.Payload![5]);
        Assert.Null(session.HeldKey);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
    }

    [Fact]
    public void The_gate_holds_a_new_write_back_but_never_a_release()
    {
        var (session, gate) = LiveSession();
        ReportLeds(session);
        gate.Decision = HspWriteDecision.Hold("REC MEM is lit");
        session.QueueFader(ClearedFader.Master, 0x10);

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        Assert.Equal("REC MEM is lit", session.WriteHeldReason);
        Assert.Equal(1, session.QueuedWrites);
        session.Receive(Header(4, 0));
        gate.Decision = HspWriteDecision.Send;
        Assert.True(session.Next().IsWrite);
        Assert.Null(session.WriteHeldReason);

        IdleExchange(session);
        session.QueueKeyTap(ClearedKey.Solo);
        Assert.True(session.Next().IsWrite);                          // the press
        IdleExchange(session);
        gate.Decision = HspWriteDecision.Drop("COPY is lit");
        var asked = gate.Asked;
        var release = session.Next();

        Assert.Equal(AsSent(36391, 0x02), release.Payload);           // SmartSoft's SOLO release (E9 seq 36391)
        Assert.Equal(asked, gate.Asked);                              // a release completes a press: nothing is asked
        Assert.Equal(0, session.WritesDropped);
    }

    [Fact]
    public void A_writes_own_condition_holds_or_drops_it_and_nothing_else_goes_in_that_slot()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        var condition = HspWriteDecision.Hold("the console listed FaderInfo");
        session.QueueFader(ClearedFader.ChannelFader1, 0x10, () => condition);
        session.QueueFader(ClearedFader.Bumps, 0x20);

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // held: the BUMPS write behind it waits too
        Assert.Equal("the console listed FaderInfo", session.WriteHeldReason);
        session.Receive(Header(4, 0));
        condition = HspWriteDecision.Drop("the console is in memory mode");
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // dropped unsent; no second write in that slot
        Assert.Equal(1, session.WritesDropped);
        Assert.Equal("the console is in memory mode", session.LastDropReason);
        Assert.Equal(1, session.QueuedWrites);
        session.Receive(Header(4, 0));

        var next = session.Next();

        Assert.True(next.IsWrite);
        Assert.Equal((byte)ClearedFader.Bumps, next.Payload![5]);
        Assert.Equal(0x00, next.Payload[0]);                          // the dropped write never took a counter value
        Assert.Equal(1, session.WritesSent);
    }

    [Fact]
    public void A_session_is_not_idle_before_the_poll_exchange_after_its_last_write()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueFader(ClearedFader.Master, 0x10);

        Assert.True(session.Next().IsWrite);
        Assert.False(session.IsIdle);                                 // SmartSoft polled 14.5-16.5 ms after every write
        IdleExchange(session);
        Assert.True(session.IsIdle);
    }

    [Fact]
    public void A_stop_drops_queued_writes_and_keeps_the_owed_release()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Bump7);
        Assert.True(session.Next().IsWrite);
        session.QueueFader(ClearedFader.Bumps, 0x20);

        var dropped = session.DiscardQueuedWrites(acceptNoMore: true);

        Assert.Equal(1, dropped);
        Assert.Equal(ClearedKey.Bump7, session.HeldKey);
        Assert.False(session.IsIdle);
        Assert.Throws<InvalidOperationException>(() => session.QueueFader(ClearedFader.Bumps, 0x20));
        IdleExchange(session);
        var release = session.Next();
        Assert.Equal(AsSent(19436, 0x01), release.Payload);           // SmartSoft's bump 7 release (E9 seq 19436)
        Assert.False(session.IsIdle);                                 // the poll after the release is still owed
        IdleExchange(session);
        Assert.True(session.IsIdle);
    }

    [Fact]
    public void A_release_at_the_end_is_offered_only_when_no_exchange_is_open()
    {
        // a fault on a payload read whole (byte 1 = 0x01, never observed): the console owes nothing more
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(session.Next().IsWrite);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        list[1] = 0x01;
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);
        Assert.Equal(HspReadSessionState.Faulted, session.State);

        var release = session.TakeReleaseAtEnd();

        Assert.NotNull(release);
        Assert.Equal(AsSent(33547, 0x01), release!.Payload);          // SmartSoft's BLACKOUT release (E9 seq 33547)
        Assert.Null(session.HeldKey);

        // a fault on a header that announces a payload: the payload is still due, so no write may follow
        var (open, _) = LiveSession();
        ReportLeds(open);
        open.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(open.Next().IsWrite);
        Assert.Equal(HspMessageType.Poll, open.Next().Kind);
        open.Receive(Header(4, HspReadSession.MaxObservedPayloadLength + 1));
        Assert.Equal(HspReadSessionState.Faulted, open.State);
        Assert.Null(open.TakeReleaseAtEnd());
        Assert.Equal(ClearedKey.Blackout, open.HeldKey);

        // right after the press, before the poll that follows it: a second write in that poll cycle is never offered
        var (fresh, _) = LiveSession();
        ReportLeds(fresh);
        fresh.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(fresh.Next().IsWrite);
        Assert.Null(fresh.TakeReleaseAtEnd());
    }

    /// <summary>SmartSoft's payload at that E9 seq with this session's counter (and a value and previous byte) in place of its own.</summary>
    private static byte[] AsSent(int seq, byte counter, byte? value = null, byte? previous = null)
    {
        var bytes = Out(E9, seq).ToArray();   // a copy: the capture's bytes stay as recorded
        bytes[0] = counter;
        if (value is { } v)
        {
            bytes[6] = v;
        }

        if (previous is { } p)
        {
            bytes[7] = p;
        }

        return bytes;
    }

    /// <summary>
    /// Runs a session in write mode over the pipe, handing it SmartSoft's event at each write the pipe expects. The
    /// interlock of SC-2 condition 5 is the device's: here every point where SmartSoft wrote is a point where the session
    /// must write, so the gate stays open.
    /// </summary>
    private static HspReadSession ReplayLive(ReplayHspPipe pipe)
    {
        var session = new HspReadSession(_ => HspWriteDecision.Send);
        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        pipe.Open();
        while (!pipe.AtEnd)
        {
            if (pipe.ExpectedWrite is { } smartSofts)
            {
                HandOver(session, smartSofts.Span);
            }

            var next = session.Next();
            pipe.Write(next.Bytes);
            if (next.Payload is { } payload)
            {
                pipe.Write(payload);
                continue;
            }

            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                session.Receive(span.ToArray());
                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }
        }

        return session;
    }

    /// <summary>SmartSoft's event as the typed whitelist names it: what its user did, never its bytes.</summary>
    private static void HandOver(HspReadSession session, ReadOnlySpan<byte> payload)
    {
        // [pseq][u16 LE len][0x14][kind][index or code][value or state]([previous]) (F131)
        if (payload[4] == 0x00)
        {
            session.QueueFader((ClearedFader)payload[5], payload[6]);
        }
        else if (payload[6] == 0x01)
        {
            session.QueueKeyPress((ClearedKey)payload[5]);
        }
        else
        {
            session.QueueKeyRelease((ClearedKey)payload[5]);
        }
    }

    private static ushort Word(byte[] header, int offset) => (ushort)(header[offset] | (header[offset + 1] << 8));

    private static (HspReadSession Session, TestGate Gate) LiveSession()
    {
        var gate = new TestGate();
        return (new HspReadSession(gate.Check), gate);
    }

    /// <summary>One completed poll exchange: the poll, answered idle.</summary>
    private static void IdleExchange(HspReadSession session)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        session.Receive(Header(4, 0));
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    /// <summary>The console's first list, naming LedStatus, and the LedStatus object it delivers (SC-2 condition 8).</summary>
    private void ReportLeds(HspReadSession session)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var leds = Payload(NextInbound(), (int)HspObjectId.LedStatus, new byte[451]);
        session.Receive(Header(3, (ushort)leds.Length, (ushort)HspObjectId.LedStatus));
        session.Receive(leds);
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    private byte NextInbound() => _inboundSequence++;

    /*
     * The replies built by Header / Payload / ListPayload are hand-made inputs for the state machine, as in
     * HspReadSessionTests (a capture cannot show the console misbehaving, nor a write queued at a chosen point); their
     * shapes follow F53 (reply header), F54 (payload layout) and F57 (DirtyObjects body). Zero bodies stand in for an
     * object's content where the test is about the session, not the content (LedStatus 451 bytes, FaderInfo 195, F67).
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

    private static byte[] ListPayload(byte sequence, params DirtyObjectRecord[] records)
    {
        var body = new byte[2 + records.Length * 5];
        body[0] = (byte)(records.Length >> 8);
        body[1] = (byte)records.Length;
        for (var i = 0; i < records.Length; i++)
        {
            body[2 + i * 5] = (byte)records[i].ObjectId;
            body[3 + i * 5] = (byte)(records[i].A >> 8);
            body[4 + i * 5] = (byte)records[i].A;
            body[5 + i * 5] = (byte)records[i].B;
            body[6 + i * 5] = (byte)records[i].C;
        }

        return Payload(sequence, (int)HspObjectId.DirtyObjects, body);
    }

    /// <summary>A gate the test sets (send, hold or drop); it counts how often it was asked.</summary>
    private sealed class TestGate
    {
        public HspWriteDecision Decision { get; set; } = HspWriteDecision.Send;

        public int Asked { get; private set; }

        public HspWriteDecision Check(HspReadSession session)
        {
            Asked++;
            return Decision;
        }
    }
}
