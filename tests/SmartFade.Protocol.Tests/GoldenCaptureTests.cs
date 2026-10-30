using SmartFade.Communication.Capture;
using SmartFade.Protocol.Objects;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// Runs the session decoder over the committed captures and checks the numbers recorded in
/// the experiment records (E2: docs/protocol/captures/2026-10-28-e2-smartsoft-connect.md,
/// E3: 2026-10-29-e3-console-actions.md). The expectations are OBSERVED facts, not a
/// specification; a decoder change that breaks one of them must be explained against the capture.
/// </summary>
public class GoldenCaptureTests
{
    private static List<HspEvent> Decode(string fileName)
    {
        var capture = CaptureFile.Load(TestPaths.Capture(fileName));
        var decoder = new HspSessionDecoder();
        var events = new List<HspEvent>();
        foreach (var frame in capture.BulkFrames.Where(f => f.Data.Length > 0))
        {
            var direction = frame.Direction == CaptureDirection.Out ? HspDirection.ToConsole : HspDirection.FromConsole;
            events.Add(decoder.Feed(direction, frame.Data));
        }

        return events;
    }

    [Fact]
    public void E2_decodes_without_framing_errors_and_matches_the_record()
    {
        var events = Decode("2026-10-28-e2-smartsoft-connect.jsonl");

        Assert.Empty(events.OfType<UnexpectedTransfer>());
        Assert.Equal(3001, events.OfType<PollSent>().Count());
        Assert.Equal(228, events.OfType<FetchSent>().Count());
        Assert.Equal(2, events.OfType<DataSent>().Count());
        Assert.Equal(3001, events.OfType<ReplyHeaderReceived>().Count());
        Assert.Equal(228, events.OfType<ObjectReceived>().Count());
        Assert.Equal(16, events.OfType<DirtyListReceived>().Count());

        // every fetch names the records of the dirty lists, in order (F57)
        var listed = events.OfType<DirtyListReceived>().SelectMany(e => e.List.Records).ToArray();
        var fetched = events.OfType<FetchSent>().Select(e => e.Target).ToArray();
        Assert.Equal(listed, fetched);

        // the two writes are SmartSoft's System messages 09 00 00 and 07 00 00 (F58)
        var writes = events.OfType<DataSent>().ToArray();
        Assert.All(writes, w => Assert.Equal((int)HspObjectId.System, w.Payload.ObjectId));
        Assert.Equal(new byte[] { 0x09, 0x00, 0x00 }, writes[0].Payload.Body.ToArray());
        Assert.Equal(new byte[] { 0x07, 0x00, 0x00 }, writes[1].Payload.Body.ToArray());

        // objects (F59, F60, F61)
        var version = Objects(events, HspObjectId.ConsoleVersion).Select(p => ConsoleVersionObject.Decode(p.Body.Span)).Single();
        Assert.Equal("3.0.2.9.0.43", version.Version);
        var fileName = Objects(events, HspObjectId.FileName).Select(p => FileNameObject.Decode(p.Body.Span)).First();
        Assert.Equal("SHOW04.ASC", fileName.FileName);
        var sizes = Objects(events, HspObjectId.SystemSizes).Select(p => SystemSizesObject.Decode(p.Body)).Single();
        Assert.Equal(48, sizes.Channels);
        Assert.Equal(512, sizes.PatchLength);
        Assert.Equal(24, sizes.MemoriesPerPage);

        // Patch (F70): 512 entries, DMX 5 → 5, 47 → 50, 48 → 48, 97 → 24, 98 unpatched, 49 patched
        var patch = Objects(events, HspObjectId.Patch).Select(p => PatchObject.Decode(p.Body.Span)).Single();
        Assert.Equal(512, patch.ChannelByAddress.Count);
        Assert.Equal(5, patch.ChannelByAddress[4]);
        Assert.Equal(50, patch.ChannelByAddress[46]);
        Assert.Equal(48, patch.ChannelByAddress[47]);
        Assert.Equal(24, patch.ChannelByAddress[96]);
        Assert.Equal(0, patch.ChannelByAddress[97]);
        Assert.Equal(49, patch.ChannelByAddress.Count(c => c != 0));

        // first FaderInfo (F66, F90): fader 5 at 0xAB, MASTER byte 0xAA
        var faders = Objects(events, HspObjectId.FaderInfo).Select(p => FaderInfoObject.Decode(p.Body.Span)).First();
        Assert.Equal(48, faders.FaderLevels.Count);
        Assert.Equal(0xAB, faders.At(5));
        Assert.Equal(0xAA, faders.Master);

        // first ChannelLevels (F78): channel 5 = 0xAB, independents 0
        var channels = Objects(events, HspObjectId.ChannelLevels).Select(p => ChannelLevelsObject.Decode(p.Body.Span)).First();
        Assert.Equal(50, channels.Count);
        Assert.Equal(0xAB, channels.At(5));
        Assert.Equal(0, channels.Trailing1);

        // Memory objects (F71): 16 memories, 96 levels each, labels present; GROUP 1 = page 0 slot 0 has channels 7 and 8 at 100 %
        var memories = Objects(events, HspObjectId.Memory).Select(p => MemoryObject.Decode(p.Body.Span)).ToArray();
        Assert.Equal(16, memories.Length);
        var first = memories.Single(m => m.Page == 0 && m.Slot == 0);
        Assert.Equal(96, first.LevelCount);
        Assert.Equal(0xFF, first.Levels[6]);
        Assert.Equal(0xFF, first.Levels[7]);

        // Independents (F76): two records 00 FF 00 01
        var independents = Objects(events, HspObjectId.Independents).Select(p => IndependentsObject.Decode(p.Body.Span)).First();
        Assert.Equal(2, independents.Independents.Count);
        Assert.Equal((0, 255, 0, 1), (independents.Independents[0].A, (int)independents.Independents[0].B, (int)independents.Independents[0].C, (int)independents.Independents[0].D));

        // LCD (F86): 40 characters, two lines of 20
        var lcd = Objects(events, HspObjectId.DisplayContent).Select(p => DisplayContentObject.Decode(p.Body.Span)).First();
        Assert.Equal(40, lcd.Raw.Count);
        Assert.Equal(2, lcd.Lines.Count);

        // LEDs: 75 blocks
        var leds = Objects(events, HspObjectId.LedStatus).Select(p => LedStatusObject.Decode(p.Body.Span)).First();
        Assert.Equal(75, leds.Blocks.Count);
    }

    [Fact]
    public void E3_decodes_without_framing_errors_and_shows_the_logged_actions()
    {
        var events = Decode("2026-10-29-e3-console-actions.jsonl");

        Assert.Empty(events.OfType<UnexpectedTransfer>());
        Assert.Equal(9692, events.OfType<PollSent>().Count());
        Assert.Equal(423, events.OfType<FetchSent>().Count());
        Assert.Equal(2, events.OfType<DataSent>().Count());
        Assert.Equal(510, events.OfType<ObjectReceived>().Count() + events.OfType<DirtyListReceived>().Count());

        var version = Objects(events, HspObjectId.ConsoleVersion).Select(p => ConsoleVersionObject.Decode(p.Body.Span)).Single();
        Assert.Equal("3.0.2.9.0.43", version.Version);

        // after a power cycle the console's sequence byte started at 0 (F80)
        var firstPayload = events.First(e => e is DirtyListReceived or ObjectReceived);
        var firstSeq = firstPayload switch
        {
            DirtyListReceived d => d.Payload.Sequence,
            ObjectReceived o => o.Payload.Sequence,
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(0, firstSeq);

        // fader 1 and fader 5 reached 0xFF with the MASTER at 0xFF; the MASTER byte went down to 0x83 (F82, F83)
        var faders = Objects(events, HspObjectId.FaderInfo).Select(p => FaderInfoObject.Decode(p.Body.Span)).ToArray();
        Assert.Contains(faders, f => f.At(1) == 0xFF && f.Master == 0xFF);
        Assert.Contains(faders, f => f.At(5) == 0xFF && f.Master == 0xFF);
        Assert.Equal(0x83, faders.Min(f => f.Master));
        Assert.All(faders, f => Assert.Equal(0, f.Bumps));

        // the memory-mode switch: ConsoleStatus byte 0 went 0 → 1 (F87)
        var statuses = Objects(events, HspObjectId.ConsoleStatus).Select(p => ConsoleStatusObject.Decode(p.Body).Byte0).ToArray();
        Assert.Contains((byte)0, statuses);
        Assert.Contains((byte)1, statuses);

        // the Blackout LED (block 53) blinked while the MASTER was below full (F85)
        var leds = Objects(events, HspObjectId.LedStatus).Select(p => LedStatusObject.Decode(p.Body.Span)).ToArray();
        Assert.Contains(leds, l => l.Block(53).PhasesDiffer);
    }

    private static IEnumerable<HspInboundPayload> Objects(IEnumerable<HspEvent> events, HspObjectId id) =>
        events.OfType<ObjectReceived>().Where(e => e.Payload.ObjectId == (int)id).Select(e => e.Payload);
}
