using SmartFade.Communication.Capture;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb.Tests;

/// <summary>
/// The assembler fed with objects taken from the committed E3b capture, in the order the console
/// delivered them: LedStatus comes first in every E3b session (JSONL seq 14; F94 / H65) and
/// FaderInfo only in session 3 (seq 7959), so nothing about the masters may be claimed in between;
/// E2 delivered them the other way round (FaderInfo seq 16, LedStatus seq 20), with the same rule.
/// </summary>
public class ConsoleStateAssemblerTests
{
    private static Dictionary<int, ObjectReceived> FirstObjectsOf(string fileName)
    {
        var capture = CaptureFile.Load(TestPaths.Capture(fileName));
        var decoder = new HspSessionDecoder();
        var first = new Dictionary<int, ObjectReceived>();
        foreach (var frame in capture.BulkFrames)
        {
            var direction = frame.Direction == CaptureDirection.Out ? HspDirection.ToConsole : HspDirection.FromConsole;
            if (decoder.Feed(direction, frame.Data) is ObjectReceived received)
            {
                first.TryAdd(received.Payload.ObjectId, received);
            }
        }

        return first;
    }

    [Fact]
    public void Claims_nothing_about_the_masters_until_FaderInfo_has_arrived()
    {
        var objects = FirstObjectsOf("2026-10-30-e3b-live-objects.jsonl");
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        // E3b seq 14: the first object of the capture is LedStatus (F94); SOLO was on in no E3b session start
        var afterLeds = assembler.Apply(objects[(int)HspObjectId.LedStatus]);

        Assert.Null(afterLeds);
        Assert.Null(assembler.Masters);

        // E3b seq 7959: the first FaderInfo of the capture (session 3)
        var afterFaders = assembler.Apply(objects[(int)HspObjectId.FaderInfo]);

        Assert.NotNull(afterFaders?.Masters);
        Assert.NotNull(assembler.Masters);
        Assert.False(assembler.Masters!.Solo);
        Assert.Equal(afterFaders.Masters, assembler.Masters);
    }

    [Fact]
    public void A_solo_change_after_FaderInfo_is_reported_and_one_before_it_is_remembered()
    {
        var objects = FirstObjectsOf("2026-10-30-e3b-live-objects.jsonl");
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        var leds = objects[(int)HspObjectId.LedStatus];
        var soloOn = WithSoloLit(leds);

        // before FaderInfo: the LED reading is kept, not reported
        Assert.Null(assembler.Apply(soloOn));
        Assert.Null(assembler.Masters);

        var masters = assembler.Apply(objects[(int)HspObjectId.FaderInfo])!.Masters!;
        Assert.True(masters.Solo);   // the remembered reading travels with the first FaderInfo

        // after FaderInfo: a change of the LED is a masters change, an unchanged LED is nothing
        Assert.Null(assembler.Apply(soloOn));
        var off = assembler.Apply(leds);
        Assert.False(off?.Masters?.Solo);
    }

    [Fact]
    public void Claims_nothing_about_the_masters_until_LedStatus_has_arrived_either()
    {
        // E2 delivered the two objects the other way round: FaderInfo (JSONL seq 16) before LedStatus (seq 20)
        var objects = FirstObjectsOf("2026-10-28-e2-smartsoft-connect.jsonl");
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        Assert.Null(assembler.Apply(objects[(int)HspObjectId.FaderInfo]));
        Assert.Null(assembler.Masters);   // MASTER and BUMPS are known, SOLO is not: no reading yet

        var masters = assembler.Apply(objects[(int)HspObjectId.LedStatus])?.Masters;

        Assert.NotNull(masters);
        Assert.Equal(0xAA, masters.Master);   // F90: the MASTER stood at 0xAA in E2
        Assert.Equal(masters, assembler.Masters);
    }

    [Fact]
    public void File_name_and_firmware_come_as_their_own_change_not_as_a_snapshot()
    {
        var objects = FirstObjectsOf("2026-10-30-e3b-live-objects.jsonl");
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        var version = assembler.Apply(objects[(int)HspObjectId.ConsoleVersion]);
        var fileName = assembler.Apply(objects[(int)HspObjectId.FileName]);

        Assert.Equal("3.0.2.9.0.43", version?.FirmwareVersion);   // F59
        Assert.Equal("SHOW05.ASC", fileName?.ShowFileName);        // F103
        Assert.Null(version?.Snapshot);
        Assert.Null(fileName?.Snapshot);
        Assert.Null(assembler.Masters);
    }

    /// <summary>The same LedStatus object with block 52 (SOLO, INFERRED F100) set to its "on" value FF × 6.</summary>
    private static ObjectReceived WithSoloLit(ObjectReceived leds)
    {
        var body = leds.Payload.Body.ToArray();
        var block52 = 1 + 51 * 6;
        for (var i = 0; i < 6; i++)
        {
            body[block52 + i] = 0xFF;
        }

        var payload = leds.Payload with { Body = body };
        return new ObjectReceived(leds.Header, payload);
    }
}
