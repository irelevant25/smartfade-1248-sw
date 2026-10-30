using SmartFade.Communication.Capture;
using SmartFade.Protocol.Objects;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// Golden test over the E3b capture (docs/protocol/captures/2026-10-30-e3b-live-objects.md):
/// three SmartSoft sessions without a console power cycle, the BUMPS fader, bumps, SOLO and the
/// 25-48 bank. Expectations are the record's OBSERVED numbers.
/// </summary>
public class GoldenCaptureE3bTests
{
    [Fact]
    public void E3b_decodes_without_framing_errors_and_matches_the_record()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-30-e3b-live-objects.jsonl"));
        var decoder = new HspSessionDecoder();
        var events = new List<HspEvent>();
        foreach (var frame in capture.BulkFrames.Where(f => f.Data.Length > 0))
        {
            var direction = frame.Direction == CaptureDirection.Out ? HspDirection.ToConsole : HspDirection.FromConsole;
            events.Add(decoder.Feed(direction, frame.Data));
        }

        Assert.Empty(events.OfType<UnexpectedTransfer>());
        Assert.Equal(25824, events.OfType<PollSent>().Count());
        Assert.Equal(1000, events.OfType<FetchSent>().Count());
        Assert.Equal(6, events.OfType<DataSent>().Count());
        Assert.Equal(268, events.OfType<DirtyListReceived>().Count());

        // three sessions: System 09 00 00 and 07 00 00, three times each, in that order
        var writes = events.OfType<DataSent>().Select(w => w.Payload.Body.ToArray()).ToArray();
        for (var session = 0; session < 3; session++)
        {
            Assert.Equal(new byte[] { 0x09, 0x00, 0x00 }, writes[2 * session]);
            Assert.Equal(new byte[] { 0x07, 0x00, 0x00 }, writes[2 * session + 1]);
        }

        // FileName follows the last SD save (the backup made before the session)
        var fileNames = Objects(events, HspObjectId.FileName).Select(p => FileNameObject.Decode(p.Body.Span).FileName).Distinct().ToArray();
        Assert.Equal(new[] { "SHOW05.ASC" }, fileNames);

        // BUMPS (body[193]) moved up to 0xFF; the MASTER byte rested at 0x7F at "half"
        var faders = Objects(events, HspObjectId.FaderInfo).Select(p => FaderInfoObject.Decode(p.Body.Span)).ToArray();
        Assert.Equal(0xFF, faders.Max(f => f.Bumps));
        Assert.Contains(faders, f => f.Master == 0x7F);

        // the 25-48 bank: ConsoleStatus byte 4 took the value 1; byte 0 took 0 (channel) and 1 (memory)
        var statuses = Objects(events, HspObjectId.ConsoleStatus).Select(p => ConsoleStatusObject.Decode(p.Body).Raw.ToArray()).ToArray();
        Assert.Contains(statuses, s => s[4] == 1);
        Assert.Contains(statuses, s => s[0] == 0);
        Assert.Contains(statuses, s => s[0] == 1);

        // channel-level objects show the channel-7 bump (0xFF) and channel 25 moving in bank 25-48
        var levels = Objects(events, HspObjectId.ChannelLevels).Select(p => ChannelLevelsObject.Decode(p.Body.Span)).ToArray();
        Assert.Contains(levels, l => l.At(7) == 0xFF);
        Assert.Contains(levels, l => l.At(25) > 0);
    }

    private static IEnumerable<HspInboundPayload> Objects(IEnumerable<HspEvent> events, HspObjectId id) =>
        events.OfType<ObjectReceived>().Where(e => e.Payload.ObjectId == (int)id).Select(e => e.Payload);
}
