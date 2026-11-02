using SmartFade.Communication;
using SmartFade.Communication.Capture;
using SmartFade.Protocol.Objects;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// Safety check SC-4 (2026-11-02, evening, class E): SmartSoft's start-up pair, its placement, the version gate and the
/// download, against the committed captures (F181, F182). The golden replays run a session of ours over each of SmartSoft's
/// 20 sessions from its first poll through its download's last record, with the replay pipe expecting the pair where
/// SmartSoft wrote it: every other transfer of ours must equal SmartSoft's too, so the placement, the stale words of the
/// two headers, the payloads and the record-by-record acceptance of the download are all compared byte for byte. The
/// hand-made replies further down follow F53 / F54 / F57 as in the other session tests; zero bodies of an observed length
/// stand in for an object's content where the test is about the session, not the content.
/// </summary>
public class HspStartupPairTests
{
    private const string E2File = "2026-10-28-e2-smartsoft-connect.jsonl";
    private const string E3File = "2026-10-29-e3-console-actions.jsonl";
    private const string E3bFile = "2026-10-30-e3b-live-objects.jsonl";
    private const string E9File = "2026-10-30-e9-smartsoft-writes.jsonl";
    private const string E9bFile = "2026-10-31-e9b-smartsoft-writes-repeat.jsonl";
    private const string E12File = "2026-10-31-e12-smartsoft-patch-stack-edits.jsonl";
    private const string E12bFile = "2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.jsonl";
    private const string E9cFile = "2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl";
    private const string E9dFile = "2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl";
    private const string E9eFile = "2026-11-02-e9e-smartsoft-channel-bumps-spot-check.jsonl";

    private static readonly Dictionary<string, Lazy<CaptureFile>> Captures = new[]
    {
        E2File, E3File, E3bFile, E9File, E9bFile, E12File, E12bFile, E9cFile, E9dFile, E9eFile,
    }.ToDictionary(file => file, file => new Lazy<CaptureFile>(() => CaptureFile.Load(TestPaths.Capture(file))));

    private byte _inboundSequence;

    private static CaptureFile Capture(string file) => Captures[file].Value;

    private static byte[] Frame(string file, int seq, CaptureDirection direction) =>
        Assert.Single(Capture(file).Frames, f => f.Seq == seq && f.Direction == direction && f.IsBulk && !f.Synthetic).Data;

    /// <summary>
    /// F181: SmartSoft's two payloads in each of its 20 sessions (the OUT payload seqs of `09` and `07` the fact lists), and their
    /// headers, which carry the stale words a poll would carry there (w2 = 0x11 after a PlaybackInfo object reply in four).
    /// </summary>
    [Theory]
    [InlineData(E2File, 106, 189)]
    [InlineData(E3File, 90, 173)]
    [InlineData(E3bFile, 92, 175)]
    [InlineData(E3bFile, 2775, 2858)]
    [InlineData(E3bFile, 5124, 5207)]
    [InlineData(E9File, 138, 221)]
    [InlineData(E9bFile, 94, 177)]
    [InlineData(E9bFile, 64763, 64846)]
    [InlineData(E9bFile, 70150, 70233)]
    [InlineData(E12File, 92, 175)]
    [InlineData(E12File, 138926, 139009)]
    [InlineData(E12File, 194186, 194269)]
    [InlineData(E12bFile, 92, 175)]
    [InlineData(E12bFile, 87211, 87294)]
    [InlineData(E9cFile, 147, 230)]
    [InlineData(E9cFile, 107327, 107410)]
    [InlineData(E9dFile, 92, 175)]
    [InlineData(E9dFile, 57840, 57976)]
    [InlineData(E9eFile, 148, 285)]
    [InlineData(E9eFile, 22690, 22827)]
    public void Encode_the_pair_MatchesCapture_in_every_SmartSoft_session(string file, int firstPayloadSeq, int secondPayloadSeq)
    {
        var first = Frame(file, firstPayloadSeq, CaptureDirection.Out);
        var second = Frame(file, secondPayloadSeq, CaptureDirection.Out);

        Assert.Equal(first, HspOutbound.SystemMessage(0x00, ClearedSystemMessage.ListVersion));
        Assert.Equal(second, HspOutbound.SystemMessage(0x01, ClearedSystemMessage.ListShow));
        Assert.True(HspOutbound.IsClearedSystemMessage(first));
        Assert.True(HspOutbound.IsClearedSystemMessage(second));
        Assert.False(HspOutbound.IsClearedConsoleEvent(first));

        foreach (var seq in new[] { firstPayloadSeq - 1, secondPayloadSeq - 1 })
        {
            var header = Frame(file, seq, CaptureDirection.Out);
            var w = Words(header);
            Assert.Equal(header, HspOutbound.DataHeader(seq == firstPayloadSeq - 1 ? first : second, w[2], w[3], w[4], w[5]));
        }
    }

    /// <summary>
    /// SC-4 clause 5 and item 9: the whitelist is exactly the two first body bytes SmartSoft's pair carries (E2 seq 106 and
    /// 189), and nothing else of id 39 can be represented: every other byte cast into the enum is refused by the encoder (the
    /// upload framing 0 / 1 / 6, the page select 10, the erase commands 11–16 among them, S11.17 row 7, F121), each of the two
    /// only with the pseq SmartSoft's had, and a payload is accepted only when it is one of the two exactly: every other first
    /// body byte, any b or c but 0, the other pseq. A Data header is refused for all of those (as SC-5's NO-GO test does for
    /// the keys).
    /// </summary>
    [Fact]
    public void The_System_whitelist_is_SmartSofts_two_bodies_and_every_other_value_is_unrepresentable()
    {
        var first = Frame(E2File, 106, CaptureDirection.Out);
        var second = Frame(E2File, 189, CaptureDirection.Out);
        Assert.Equal(new[] { second[4], first[4] }, Enum.GetValues<ClearedSystemMessage>().Select(m => (byte)m).Order().ToArray());

        for (var value = 0; value <= 0xFF; value++)
        {
            var message = (ClearedSystemMessage)value;
            if (value is 0x07 or 0x09)
            {
                Assert.True(Enum.IsDefined(message));
                continue;
            }

            Assert.False(Enum.IsDefined(message));
            Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.SystemMessage(0x00, message));
            Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.SystemMessage(0x01, message));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.SystemMessage(0x01, ClearedSystemMessage.ListVersion));
        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.SystemMessage(0x00, ClearedSystemMessage.ListShow));
        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.SystemMessage(0x02, ClearedSystemMessage.ListShow));

        // every variant of SmartSoft's two payloads at the pseq, body byte a, b or c is refused but the two themselves
        foreach (var captured in new[] { first, second })
        {
            foreach (var position in new[] { 0, 4, 5, 6 })
            {
                for (var value = 0; value <= 0xFF; value++)
                {
                    var variant = captured.ToArray();
                    variant[position] = (byte)value;
                    var accepted = variant.AsSpan().SequenceEqual(first) || variant.AsSpan().SequenceEqual(second);
                    Assert.Equal(accepted, HspOutbound.IsClearedSystemMessage(variant));
                    if (!accepted && !HspOutbound.IsClearedConsoleEvent(variant))
                    {
                        Assert.Throws<ArgumentException>(() => HspOutbound.DataHeader(variant, 0, 0, 0, 0));
                    }
                }
            }
        }

        // SmartSoft's other writes are no System message of SC-4 (a ConsoleEvent, a ChannelText label, E9b's Memory write)
        Assert.False(HspOutbound.IsClearedSystemMessage(Frame(E9File, 9665, CaptureDirection.Out)));
        Assert.False(HspOutbound.IsClearedSystemMessage(Frame(E9File, 57881, CaptureDirection.Out)));
        Assert.False(HspOutbound.IsClearedSystemMessage(Frame(E9bFile, 92053, CaptureDirection.Out)));
    }

    /// <summary>
    /// SC-4 clauses 1, 2 and 5: each of SmartSoft's 20 sessions (F181, F182), replayed from its first poll through the
    /// download's last record with a session of ours that sends the pair: the pipe expects `09 00 00` and `07 00 00` exactly
    /// where SmartSoft wrote them and every other transfer as SmartSoft's, so the placement (#40, #79, nothing listed at
    /// `09`, E9e's `07` before a pending fetch), the version gate, the stale words of both headers and the download accepted
    /// record by record are all compared; the record count and the list's length are F182 (c)'s.
    /// </summary>
    [Theory]
    [InlineData(E2File, 6, 821, 157, 792)]
    [InlineData(E3File, 6, 805, 157, 792)]
    [InlineData(E3bFile, 6, 807, 157, 792)]
    [InlineData(E3bFile, 2694, 3490, 157, 792)]
    [InlineData(E3bFile, 5043, 5839, 157, 792)]
    [InlineData(E9File, 6, 853, 157, 792)]
    [InlineData(E9bFile, 6, 809, 157, 792)]
    [InlineData(E9bFile, 64682, 65478, 157, 792)]
    [InlineData(E9bFile, 70069, 70865, 157, 792)]
    [InlineData(E12File, 6, 807, 157, 792)]
    [InlineData(E12File, 138845, 139657, 161, 812)]
    [InlineData(E12File, 194105, 194901, 157, 792)]
    [InlineData(E12bFile, 6, 807, 157, 792)]
    [InlineData(E12bFile, 87130, 87938, 160, 807)]
    [InlineData(E9cFile, 17, 866, 158, 797)]
    [InlineData(E9cFile, 107246, 108046, 158, 797)]
    [InlineData(E9dFile, 6, 811, 158, 797)]
    [InlineData(E9dFile, 57702, 58612, 158, 797)]
    [InlineData(E9eFile, 5, 929, 159, 802)]
    [InlineData(E9eFile, 22552, 23471, 159, 802)]
    public void Sends_SmartSofts_pair_where_SmartSoft_sent_it_and_takes_the_whole_download(string file, int firstSeq, int endSeq, int records, int listLength)
    {
        var pipe = new ReplayHspPipe(Capture(file), payload => HspOutbound.IsClearedSystemMessage(payload.Span), firstSeq, endSeq);

        var (session, pairAt, notes) = ReplayWithPair(pipe);

        Assert.Equal(0, pipe.Remaining);
        Assert.Equal(2, pipe.MatchedDataWrites);            // the two payloads (their headers are matched writes too)
        Assert.Equal(0, pipe.SkippedSmartSoftWrites);       // the pair is SmartSoft's first two data writes (F181)
        Assert.Equal(2, session.WritesSent);
        Assert.Null(session.Fault);
        Assert.Equal(new[] { HspReadSession.StartupPairFirstAfterReplies, HspReadSession.StartupPairSecondAfterReplies }, pairAt);
        var pair = session.StartupPair;
        Assert.Equal(HspStartupPairPhase.Done, pair.Phase);
        Assert.Equal(records, pair.DownloadRecords);
        Assert.Equal(records, pair.DownloadDelivered);
        Assert.Equal(listLength, pair.DownloadListLength);
        Assert.Null(pair.EndReason);
        Assert.Contains(notes, n => n.Contains("the version gate passed", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.Contains($"the download listed {records} records in {listLength} bytes", StringComparison.Ordinal));
        Assert.True(session.IsIdle);
    }

    /// <summary>
    /// SC-4 clause 1 and item 9 over three sessions' sequences (E2, E9c, E9e): `09` right after a completed poll exchange, the
    /// ConsoleVersion listed at the next free reply, `07` after it, the download listed at the next free reply (E9e: after the
    /// object reply of a PlaybackInfo listed and pending at `07`, F182 b).
    /// </summary>
    [Theory]
    [InlineData(E2File, 6, 821)]
    [InlineData(E9cFile, 17, 866)]
    [InlineData(E9eFile, 5, 929)]
    public void The_pair_follows_a_poll_exchange_the_version_comes_next_and_the_download_after_07(string file, int firstSeq, int endSeq)
    {
        var pipe = new ReplayHspPipe(Capture(file), payload => HspOutbound.IsClearedSystemMessage(payload.Span), firstSeq, endSeq);

        var (_, _, notes) = ReplayWithPair(pipe);

        var order = notes.Select(n => n switch
        {
            _ when n.Contains("09 00 00 sent after reply header #40", StringComparison.Ordinal) => "09",
            _ when n.Contains("ConsoleVersion listed 1 poll reply", StringComparison.Ordinal) => "version listed",
            _ when n.Contains("07 00 00 sent after reply header #79", StringComparison.Ordinal) => "07",
            _ when n.Contains("version gate passed", StringComparison.Ordinal) => "version read",
            _ when n.Contains("1 poll reply/replies after 07 00 00", StringComparison.Ordinal) => "download listed",
            _ when n.Contains("last record delivered", StringComparison.Ordinal) => "download done",
            _ => n,
        }).ToArray();
        Assert.Equal(new[] { "09", "version listed", "version read", "07", "download listed", "download done" }, order);
    }

    [Fact]
    public void Decodes_the_show_objects_of_E2s_download_as_SHOW04_has_them()
    {
        // KOTLIK (`$SEQUENCE 0 0 0 256 256`, 7 `$CUE` steps; F74), E2 seq 328
        var kotlik = SequenceObject.Decode(Body(E2File, 328));
        Assert.Equal((0, 0, 0, 0, 256, 256), (kotlik.Page, kotlik.Number, kotlik.B, kotlik.C, kotlik.D, kotlik.E));
        Assert.Equal("KOTLIK", kotlik.Label.Line1);
        Assert.Equal(7, kotlik.Steps.Count);
        Assert.Equal(Enumerable.Range(1, 7).Select(n => n * 10), kotlik.Steps.Select(s => s.CueTenths));   // the wire's 1.0 .. 7.0 (F75)
        Assert.Equal(Enumerable.Range(264, 7), kotlik.Steps.Select(s => s.MemoryIndex));                  // `$$MEMORY 11 0..6`

        // the stack: `$SEQUENCE 48 2 0 256 256` with 19 `CUE` steps, E2 seq 332
        var stack = SequenceMainObject.Decode(Body(E2File, 332));
        Assert.Equal((19, 2, 0, 256, 256), (stack.StepCount, stack.B, stack.C, stack.D, stack.E));

        // `CUE 8.0` / `$$MEMORY 0 4` / `UP 5.0` / `DOWN 0.5` / `FOLLOWON 1:30` (seq 364) and `CUE 10.1` / `$$MEMORY 0 12` / `UP 0` /
        // `DOWN 0` / `FOLLOWON 1.0` (seq 372)
        var eight = StepContentObject.Decode(Body(E2File, 364));
        Assert.Equal((7, 80, (byte)1, 4, 50, 5, 900), (eight.Index, eight.Step.CueTenths, eight.Step.Type, eight.Step.MemoryIndex, eight.Step.UpTenths, eight.Step.DownTenths, eight.Step.WaitTenths));
        Assert.Null(eight.Levels);
        var pointCue = StepContentObject.Decode(Body(E2File, 372));
        Assert.Equal((9, 101, 12, 0, 0, 10), (pointCue.Index, pointCue.Step.CueTenths, pointCue.Step.MemoryIndex, pointCue.Step.UpTenths, pointCue.Step.DownTenths, pointCue.Step.WaitTenths));

        // a page label (empty in SHOW04, seq 216) and a channel label object (seq 412: channel 1)
        Assert.Equal(0, LabelObject.Decode(Body(E2File, 216)).Index);
        Assert.Equal(0, LabelObject.Decode(Body(E2File, 412)).Index);
        Assert.True(LabelObject.Decode(Body(E2File, 216)).Label.ToString().Length == 0);
    }

    [Fact]
    public void Decodes_the_edited_shows_objects_of_E12bs_second_download_as_SHOW15_has_them()
    {
        // KOTLIK with 9 steps, Manual (`$SEQUENCE 0 2 0 256 256`, F162), 156 bytes (seq 87433)
        var kotlik = SequenceObject.Decode(Body(E12bFile, 87433));
        Assert.Equal((9, 2), (kotlik.Steps.Count, kotlik.B));

        // the stack with 22 steps (seq 87437); step 19 the labelled State step `CUE 13.1` (148 bytes, seq 87517, F161), step 20
        // the channel step `CUE 13.2` / `CHAN 47/100` (seq 87521, F160: bytes 3-4 = channel − 1, `UP 3.0`)
        Assert.Equal(22, SequenceMainObject.Decode(Body(E12bFile, 87437)).StepCount);
        var state = StepContentObject.Decode(Body(E12bFile, 87517));
        Assert.Equal((19, 131, (byte)2), (state.Index, state.Step.CueTenths, state.Step.Type));
        Assert.Equal(96, state.Levels!.Count);
        Assert.All(state.Levels, level => Assert.Equal(0, level));
        Assert.Equal(("ČAJ123", "456789", "01234"), (state.Label!.Line1, state.Label.Line2, state.Label.Line3));
        var channelStep = StepContentObject.Decode(Body(E12bFile, 87521));
        Assert.Equal((20, 132, (byte)0, 46, 30, 50, 0), (channelStep.Index, channelStep.Step.CueTenths, channelStep.Step.Type, channelStep.Step.MemoryIndex, channelStep.Step.UpTenths, channelStep.Step.DownTenths, channelStep.Step.WaitTenths));

        // `$PAGE 12` / `Text T` (seq 87401, F162)
        var page = LabelObject.Decode(Body(E12bFile, 87401));
        Assert.Equal((11, "T"), (page.Index, page.Label.Line1));
    }

    [Fact]
    public void The_pair_waits_for_SmartSofts_40th_reply_with_nothing_listed_and_goes_with_a_polls_stale_words()
    {
        var (session, gate) = PairSession();
        for (var i = 0; i < 39; i++)
        {
            IdleExchange(session);
        }

        Assert.Equal(0, gate.Asked);
        Assert.Equal(HspStartupPairPhase.Waiting, session.StartupPair.Phase);

        // reply #40 lists LedStatus: nothing goes before its fetch and object; then `09` takes the next write slot, its header
        // carrying the object reply's words, as a poll would (F52; SmartSoft's 4 headers with w2 = 0x11, F181)
        ListExchange(session, new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        Assert.Equal(HspMessageType.Fetch, session.PeekNextKind());
        FetchExchange(session, HspObjectId.LedStatus, new byte[451]);
        Assert.Equal(HspMessageType.Data, session.PeekNextKind());
        var write = session.Next();

        Assert.True(write.IsWrite);
        Assert.Equal(Frame(E2File, 106, CaptureDirection.Out), write.Payload);
        Assert.Equal(HspOutbound.DataHeader(write.Payload, (ushort)HspObjectId.LedStatus, 0, 0, 0), write.Bytes);
        Assert.Equal(1, gate.Asked);
        Assert.Equal(41, session.RepliesReceived);
        Assert.Equal(HspStartupPairPhase.VersionRequested, session.StartupPair.Phase);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);   // the poll SmartSoft always sent after a write
    }

    [Fact]
    public void The_second_goes_only_after_the_version_gate_one_more_exchange_and_SmartSofts_79th_reply()
    {
        var (session, _) = PairSession();
        var first = UpToFirst(session);
        Assert.Equal(Frame(E2File, 106, CaptureDirection.Out), first.Payload);

        // the reply to the poll after `09` lists ConsoleVersion; its object is E2's (seq 113, F59)
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        ReceiveList(session, new DirtyObjectRecord((int)HspObjectId.ConsoleVersion, 0, 0, 0));
        FetchExchange(session, HspObjectId.ConsoleVersion, Body(E2File, 113));
        Assert.Equal(HspStartupPairPhase.VersionAccepted, session.StartupPair.Phase);

        while (session.RepliesReceived < HspReadSession.StartupPairSecondAfterReplies)
        {
            Assert.NotEqual(HspMessageType.Data, session.PeekNextKind());
            IdleExchange(session);
        }

        var second = session.Next();
        Assert.True(second.IsWrite);
        Assert.Equal(Frame(E2File, 189, CaptureDirection.Out), second.Payload);
        Assert.Equal(HspStartupPairPhase.DownloadRequested, session.StartupPair.Phase);
        Assert.Contains(session.TakeNotes(), n => n.Contains("07 00 00 sent after reply header #79", StringComparison.Ordinal));
    }

    [Fact]
    public void A_version_other_than_SmartSofts_ends_the_pair_without_07_and_the_session_goes_on()
    {
        var (session, _) = PairSession();
        UpToFirst(session);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        ReceiveList(session, new DirtyObjectRecord((int)HspObjectId.ConsoleVersion, 0, 0, 0));
        FetchExchange(session, HspObjectId.ConsoleVersion, new byte[13]);   // not "3.0.2.9.0.43": a zero body of the observed length

        var pair = session.StartupPair;
        Assert.Equal(HspStartupPairPhase.Ended, pair.Phase);
        Assert.Contains("not the one SmartSoft's pair was observed with", pair.EndReason, StringComparison.Ordinal);
        for (var i = 0; i < 60; i++)
        {
            IdleExchange(session);   // past reply #79: no `07` ever
        }

        Assert.Equal(1, session.WritesSent);
        Assert.Null(session.Fault);
    }

    [Fact]
    public void ConsoleVersion_not_listed_within_two_poll_replies_ends_the_pair()
    {
        var (session, _) = PairSession();
        UpToFirst(session);
        IdleExchange(session);
        Assert.Equal(HspStartupPairPhase.VersionRequested, session.StartupPair.Phase);
        IdleExchange(session);

        var pair = session.StartupPair;
        Assert.Equal(HspStartupPairPhase.Ended, pair.Phase);
        Assert.Contains("did not list ConsoleVersion within 2 poll replies", pair.EndReason, StringComparison.Ordinal);
        for (var i = 0; i < 60; i++)
        {
            IdleExchange(session);
        }

        Assert.Equal(1, session.WritesSent);
    }

    [Fact]
    public void A_download_not_listed_within_two_free_replies_after_07_ends_the_session()
    {
        var (session, _) = PairSession();
        UpToSecond(session);
        IdleExchange(session);
        Assert.Equal(HspStartupPairPhase.DownloadRequested, session.StartupPair.Phase);
        Assert.False(session.IsIdle);   // a stop waits for the download

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        session.Receive(Header(4, 0));

        Assert.Equal(HspReadSessionState.Faulted, session.State);
        Assert.Contains("did not list its download within 2 poll replies after 07 00 00", session.Fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_else_goes_from_the_sessions_start_until_the_downloads_last_record_then_writes_go_again()
    {
        var (session, _) = PairSession(live: true);
        session.QueueFader(ClearedFader.Master, 0x80);
        ListExchange(session, new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        FetchExchange(session, HspObjectId.LedStatus, new byte[451]);   // SC-2 condition 8 met: still nothing but the pair
        UpToSecond(session);

        // the download: SystemSizes first (F182 d), then ConsoleStatus and LedStatus; the queued MASTER waits through it
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        ReceiveList(
            session,
            new DirtyObjectRecord((int)HspObjectId.SystemSizes, 0, 0, 0),
            new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0),
            new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        Assert.Equal(HspStartupPairPhase.Downloading, session.StartupPair.Phase);
        FetchExchange(session, HspObjectId.SystemSizes, new byte[8]);
        Assert.NotEqual(HspMessageType.Data, session.PeekNextKind());
        FetchExchange(session, HspObjectId.ConsoleStatus, new byte[7]);
        Assert.False(session.IsIdle);
        FetchExchange(session, HspObjectId.LedStatus, new byte[451]);

        var pair = session.StartupPair;
        Assert.Equal((HspStartupPairPhase.Done, 3, 3), (pair.Phase, pair.DownloadRecords, pair.DownloadDelivered));
        Assert.Equal(HspMessageType.Data, session.PeekNextKind());
        var master = session.Next();
        Assert.Equal(HspOutbound.FaderEvent(0x02, ClearedFader.Master, 0x80, 0x00), master.Payload);   // pseq 2: after the pair's 0 and 1
    }

    [Fact]
    public void The_gate_holds_or_refuses_the_pair_and_a_refused_pair_is_never_sent_later()
    {
        var (session, gate) = PairSession();
        gate.Decision = HspWriteDecision.Hold("the console's last report is older than two poll periods");
        for (var i = 0; i < 40; i++)
        {
            IdleExchange(session);
        }

        Assert.False(session.Next().IsWrite);
        Assert.Contains("start-up pair, 09 00 00: the console's last report is older", session.WriteHeldReason, StringComparison.Ordinal);
        session.Receive(Header(4, 0));

        gate.Decision = HspWriteDecision.Drop("NEXT mode is on");
        Assert.False(session.Next().IsWrite);
        session.Receive(Header(4, 0));
        Assert.Equal(HspStartupPairPhase.Ended, session.StartupPair.Phase);
        Assert.Contains("09 00 00 refused: NEXT mode is on", session.StartupPair.EndReason, StringComparison.Ordinal);

        gate.Decision = HspWriteDecision.Send;
        for (var i = 0; i < 60; i++)
        {
            IdleExchange(session);
        }

        Assert.Equal(0, session.WritesSent);
    }

    [Fact]
    public void A_stop_ends_a_pair_not_yet_sent_and_waits_for_a_download_already_requested()
    {
        var (waiting, _) = PairSession();
        IdleExchange(waiting);
        waiting.DiscardQueuedWrites(acceptNoMore: true);
        Assert.Equal(HspStartupPairPhase.Ended, waiting.StartupPair.Phase);
        Assert.True(waiting.IsIdle);
        for (var i = 0; i < 60; i++)
        {
            IdleExchange(waiting);
        }

        Assert.Equal(0, waiting.WritesSent);

        var (requested, _) = PairSession();
        UpToSecond(requested);
        requested.DiscardQueuedWrites(acceptNoMore: true);
        Assert.Equal(HspStartupPairPhase.DownloadRequested, requested.StartupPair.Phase);
        Assert.False(requested.IsIdle);
    }

    [Fact]
    public void Only_a_session_with_the_pair_takes_the_downloads_bounds_and_SC1s_session_keeps_its_own()
    {
        // SC-1's read-only session, unchanged: nothing above 792 bytes, one length per id (F67)
        var readOnly = new HspReadSession();
        Assert.Equal(HspReadSession.MaxObservedPayloadLength, readOnly.MaxPayloadLength);
        Assert.Equal(HspMessageType.Poll, readOnly.Next().Kind);
        readOnly.Receive(Header(4, HspReadSession.MaxObservedPayloadLength + 1));
        Assert.Contains("nothing above 792", readOnly.Fault, StringComparison.Ordinal);

        var sequence = new HspReadSession();
        ListExchange(sequence, new DirtyObjectRecord((int)HspObjectId.Sequence, 0, 0, 0));
        Assert.Equal(HspMessageType.Fetch, sequence.Next().Kind);
        Assert.Equal(HspMessageType.Poll, sequence.Next().Kind);
        ReceiveObject(sequence, HspObjectId.Sequence, Body(E12bFile, 87433));
        Assert.Contains("156-byte body", sequence.Fault, StringComparison.Ordinal);

        // a session with the pair: 812 bytes (F182 c) and the edited shows' Sequence 144 / 156 and StepContent 148 (F156)
        var (pair, _) = PairSession();
        Assert.Equal(812, pair.MaxPayloadLength);
        ListExchange(pair, new DirtyObjectRecord((int)HspObjectId.Sequence, 0, 0, 0), new DirtyObjectRecord((int)HspObjectId.StepContent, 19, 0, 0));
        FetchExchange(pair, HspObjectId.Sequence, Body(E12bFile, 87433));
        FetchExchange(pair, HspObjectId.StepContent, Body(E12bFile, 87517), a: 19);
        Assert.Null(pair.Fault);
        Assert.Equal(HspMessageType.Poll, pair.Next().Kind);
        pair.Receive(Header(4, HspReadSession.MaxDownloadPayloadLength + 1));
        Assert.Contains("nothing above 812", pair.Fault, StringComparison.Ordinal);

        Assert.Equal(new[] { 132, 144, 156 }, HspReadSession.DownloadBodyLengths[(int)HspObjectId.Sequence].Order());
        Assert.Equal(new[] { 13, 148 }, HspReadSession.DownloadBodyLengths[(int)HspObjectId.StepContent].Order());
        Assert.All(
            HspReadSession.ObservedBodyLengths.Where(e => e.Key is not ((int)HspObjectId.Sequence or (int)HspObjectId.StepContent)),
            e => Assert.Equal(new[] { e.Value }, HspReadSession.DownloadBodyLengths[e.Key]));
    }

    private static (HspReadSession Session, List<int> PairAt, List<string> Notes) ReplayWithPair(ReplayHspPipe pipe)
    {
        var session = new HspReadSession(writeGate: null, startupPairGate: (_, _) => HspWriteDecision.Send);
        var buffer = new byte[session.MaxPayloadLength];
        var pairAt = new List<int>();
        var notes = new List<string>();
        pipe.Open();
        while (!pipe.AtEnd)
        {
            var next = session.Next(writeSlotMet: session.PeekNextKind() == HspMessageType.Data);
            pipe.Write(next.Bytes);
            if (next.Payload is { } payload)
            {
                pairAt.Add(session.RepliesReceived);
                pipe.Write(payload);
                notes.AddRange(session.TakeNotes());
                continue;
            }

            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                session.Receive(span.ToArray());
                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }

            notes.AddRange(session.TakeNotes());
        }

        return (session, pairAt, notes);
    }

    private static byte[] Body(string file, int payloadSeq) => Frame(file, payloadSeq, CaptureDirection.In)[5..];

    private static ushort[] Words(byte[] header) =>
        Enumerable.Range(0, 6).Select(i => (ushort)(header[2 * i] | (header[2 * i + 1] << 8))).ToArray();

    private static (HspReadSession Session, TestPairGate Gate) PairSession(bool live = false)
    {
        var gate = new TestPairGate();
        return (new HspReadSession(live ? _ => HspWriteDecision.Send : null, gate.Check), gate);
    }

    /// <summary>39 idle exchanges and the 40th, then `09 00 00` in the slot after it.</summary>
    private HspOutboundTransfer UpToFirst(HspReadSession session)
    {
        while (session.RepliesReceived < HspReadSession.StartupPairFirstAfterReplies)
        {
            IdleExchange(session);
        }

        var write = session.Next();
        Assert.True(write.IsWrite);
        return write;
    }

    /// <summary>The pair up to `07 00 00` with E2's ConsoleVersion (seq 113).</summary>
    private void UpToSecond(HspReadSession session)
    {
        UpToFirst(session);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        ReceiveList(session, new DirtyObjectRecord((int)HspObjectId.ConsoleVersion, 0, 0, 0));
        FetchExchange(session, HspObjectId.ConsoleVersion, Body(E2File, 113));
        while (session.RepliesReceived < HspReadSession.StartupPairSecondAfterReplies)
        {
            IdleExchange(session);
        }

        var second = session.Next();
        Assert.True(second.IsWrite);
        Assert.Equal(Frame(E2File, 189, CaptureDirection.Out), second.Payload);
    }

    private static void IdleExchange(HspReadSession session)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        session.Receive(Header(4, 0));
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    private void ListExchange(HspReadSession session, params DirtyObjectRecord[] records)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        ReceiveList(session, records);
    }

    private void ReceiveList(HspReadSession session, params DirtyObjectRecord[] records)
    {
        var list = ListPayload(_inboundSequence++, records);
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    private void FetchExchange(HspReadSession session, HspObjectId id, byte[] body, int a = 0)
    {
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var payload = Payload(_inboundSequence++, (int)id, body);
        session.Receive(Header(3, (ushort)payload.Length, (ushort)id, (ushort)a));
        session.Receive(payload);
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    private void ReceiveObject(HspReadSession session, HspObjectId id, byte[] body)
    {
        var payload = Payload(_inboundSequence++, (int)id, body);
        session.Receive(Header(3, (ushort)payload.Length, (ushort)id));
        session.Receive(payload);
    }

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

    /// <summary>The pair's gate as the test sets it; it counts how often it was asked.</summary>
    private sealed class TestPairGate
    {
        public HspWriteDecision Decision { get; set; } = HspWriteDecision.Send;

        public int Asked { get; private set; }

        public HspWriteDecision Check(HspReadSession session, ClearedSystemMessage message)
        {
            Asked++;
            return Decision;
        }
    }
}
