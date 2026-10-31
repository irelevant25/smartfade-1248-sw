using SmartFade.Application.Ports;
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

        Assert.Null(afterLeds?.Masters);   // the key LEDs are reported, the masters are not
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
        Assert.Null(assembler.Apply(soloOn)?.Masters);
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

    /// <summary>Every object of a capture by the JSONL seq of its payload frame (the E8 record cites objects that way).</summary>
    private static Dictionary<int, ObjectReceived> ObjectsOf(string fileName)
    {
        var capture = CaptureFile.Load(TestPaths.Capture(fileName));
        var decoder = new HspSessionDecoder();
        var objects = new Dictionary<int, ObjectReceived>();
        foreach (var frame in capture.BulkFrames)
        {
            var direction = frame.Direction == CaptureDirection.Out ? HspDirection.ToConsole : HspDirection.FromConsole;
            if (decoder.Feed(direction, frame.Data) is ObjectReceived received)
            {
                objects[frame.Seq] = received;
            }
        }

        return objects;
    }

    private static readonly Lazy<Dictionary<int, ObjectReceived>> E8 = new(() => ObjectsOf("2026-10-30-e8-first-read-only-session.jsonl"));

    [Fact]
    public void The_memory_faders_of_the_selected_page_come_from_FaderInfo_in_memory_mode()
    {
        // E8: memory mode at seq 4095 (ConsoleStatus body[0] = 1, page 1), the page-1 LED map at seq 4103
        // (blocks 1-8 and 13 recorded), memory fader 2 at full at seq 4478 (F112, F113)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        var page = assembler.Apply(E8.Value[4095]);
        Assert.Equal(1, page?.MemoryPage);
        assembler.Apply(E8.Value[4103]);

        var change = assembler.Apply(E8.Value[4478]);

        var fader2 = Assert.Single(change?.Memories ?? Array.Empty<MemoryState>(), m => m.Page == 1 && m.Fader == 2);
        Assert.Equal(0xFF, fader2.Level);
        Assert.True(fader2.Recorded);
        Assert.True(fader2.Reported);
        var all = assembler.Snapshot().Memories;
        Assert.True(all[ConsoleCapabilities.SmartFade1248.MemoryIndex(1, 1)].Recorded);
        Assert.False(all[ConsoleCapabilities.SmartFade1248.MemoryIndex(1, 9)].Recorded);
        Assert.True(all[ConsoleCapabilities.SmartFade1248.MemoryIndex(1, 9)].Reported);
        Assert.False(all[ConsoleCapabilities.SmartFade1248.MemoryIndex(2, 1)].Reported);   // another page: nothing known
    }

    [Fact]
    public void The_selected_page_comes_from_ConsoleStatus()
    {
        // E8 seq 18003: body 01 00 0B 00 00 01 00 while the LCD showed "Memory page:12"; seq 18102: back to page 1 (F113)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        Assert.Equal(12, assembler.Apply(E8.Value[18003])?.MemoryPage);
        Assert.Equal(1, assembler.Apply(E8.Value[18102])?.MemoryPage);
        Assert.Null(assembler.Apply(E8.Value[18102]));   // unchanged: nothing to report
    }

    [Fact]
    public void BLACKOUT_is_the_steady_blue_of_LED_block_53()
    {
        // E8: FaderInfo seq 47 and LedStatus seq 11 complete the masters; block 53 steady blue at seq 5820 (BLACKOUT on),
        // the MASTER-below-full blink at seq 6165 (BLACKOUT off, F114 / F85)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        assembler.Apply(E8.Value[11]);
        var first = assembler.Apply(E8.Value[47]);
        Assert.False(first?.Masters?.Blackout);

        Assert.True(assembler.Apply(E8.Value[5820])?.Masters?.Blackout);
        Assert.False(assembler.Apply(E8.Value[6165])?.Masters?.Blackout);
    }

    [Fact]
    public void The_independents_come_from_the_Independents_object()
    {
        // E8 seq 6300: record 2 on (IND 2 by order, INFERRED); seq 6421: record 1 on as well (F115)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        assembler.Apply(E8.Value[11]);
        assembler.Apply(E8.Value[47]);

        var second = assembler.Apply(E8.Value[6300]);
        Assert.Equal(new IndependentStates(false, true), second?.Independents);
        Assert.True(second?.Masters?.Independent2);
        Assert.False(second?.Masters?.Independent1);

        var both = assembler.Apply(E8.Value[6421]);
        Assert.Equal(new IndependentStates(true, true), both?.Independents);
        Assert.Null(assembler.Apply(E8.Value[6421]));   // unchanged: nothing to report
    }

    private static readonly Lazy<Dictionary<int, ObjectReceived>> E9 = new(() => ObjectsOf("2026-10-30-e9-smartsoft-writes.jsonl"));

    [Fact]
    public void A_memory_the_console_lists_after_a_recording_is_shown_with_its_content()
    {
        // E9 seq 74007: the console listed Memory (0, 0, 8) by itself after REC MEM + bump 9 from SmartSoft's
        // Simulator (F136): page 1, fader 9, channel 5 at the recorded level 0x79. A read-only session would
        // receive the same object after a recording made on the console itself (H83 / H85 open).
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        var change = assembler.Apply(E9.Value[74007]);

        var memory = Assert.Single(change?.Memories ?? Array.Empty<MemoryState>());
        Assert.Equal((1, 9), (memory.Page, memory.Fader));
        Assert.True(memory.Recorded);
        Assert.True(memory.Reported);
        var content = assembler.Memory(1, 9);
        Assert.NotNull(content);
        Assert.Equal(0x79, content!.Levels[4]);                       // channel 5
        Assert.Equal(0, content.Levels.Where((_, i) => i != 4).Sum(l => (int)l));   // every other channel at zero
    }

    private static readonly Lazy<Dictionary<int, ObjectReceived>> E8B = new(() => ObjectsOf("2026-10-30-e8b-second-read-only-session.jsonl"));

    [Fact]
    public void The_crossfader_follows_PlaybackInfo_record_5()
    {
        // E8b seq 58658: GO with STACK lit starts a 5-s fade, record 5 reads 00 00 FA 05 ...; seq 59036: the fade is over,
        // 01 01 FF 00 (F130; the step as the Next step's index, H76)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        var start = assembler.Apply(E8B.Value[58658])?.Crossfader;
        Assert.Equal(new CrossfaderState(0, 0xFA, 0x05), start);

        var done = assembler.Apply(E8B.Value[59036])?.Crossfader;
        Assert.Equal(new CrossfaderState(1, 0xFF, 0x00), done);
        Assert.Null(assembler.Apply(E8B.Value[59036]));   // unchanged: nothing to report
    }

    [Fact]
    public void The_LCD_lines_are_reported_as_text()
    {
        // E8 seq 24967: "Live:14.0 Next: 1.0" and "Mem:01/01 Mem:01/01" after STACK was pressed (F117; layout F86)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        var lcd = assembler.Apply(E8.Value[24967])?.Lcd;

        Assert.NotNull(lcd);
        Assert.Equal(2, lcd!.Count);
        Assert.StartsWith("Live:14.0 Next: 1.0", lcd[0]);
        Assert.Contains("Mem:01/01", lcd[1]);
        Assert.Null(assembler.Apply(E8.Value[24967]));   // unchanged: nothing to report
    }

    [Fact]
    public void The_key_LEDs_follow_the_record_functions_and_STACK()
    {
        // E8 seq 11: the idle map (CLEAR steady full, STACK dim); seq 26865: the "Record Memory" screen (CLEAR, STACK and
        // MAGIC blinking, REC MEM steady full); seq 24971: STACK pressed (full, steady) (F116, F117; identities H72)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);

        var idle = assembler.Apply(E8.Value[11])?.Keys;
        Assert.Equal(new KeyLed(KeyLight.Full, false), idle?.Clear);
        Assert.Equal(new KeyLed(KeyLight.Dim, false), idle?.Stack);
        Assert.Equal(KeyLed.Off, idle?.RecMem);

        var recording = assembler.Apply(E8.Value[26865])?.Keys;
        Assert.Equal(new KeyLed(KeyLight.Full, true), recording?.Clear);
        Assert.Equal(new KeyLed(KeyLight.Full, true), recording?.Stack);
        Assert.Equal(new KeyLed(KeyLight.Full, true), recording?.Magic);
        Assert.Equal(new KeyLed(KeyLight.Full, false), recording?.RecMem);
        Assert.Equal(KeyLed.Off, recording?.EditMem);

        Assert.Equal(new KeyLed(KeyLight.Full, false), assembler.Apply(E8.Value[24971])?.Keys?.Stack);
        // seq 6165: the BLACKOUT LED's blink while the MASTER stood at 0x88 (F85); seq 5820: steady blue, BLACKOUT on (F114)
        Assert.Equal(new KeyLed(KeyLight.Full, true), assembler.Apply(E8.Value[6165])?.Keys?.Blackout);
        Assert.Equal(new KeyLed(KeyLight.Full, false), assembler.Apply(E8.Value[5820])?.Keys?.Blackout);
        Assert.Null(assembler.Apply(E8.Value[5820])?.Keys);   // unchanged: nothing to report
    }

    [Fact]
    public void A_record_function_screen_does_not_change_the_recorded_flags()
    {
        // E8 seq 26861 / 26865: the "Record Memory" screen's LCD and LED set (every memory key blinking, F116)
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        assembler.Apply(E8.Value[4095]);
        assembler.Apply(E8.Value[4103]);
        assembler.Apply(E8.Value[4478]);
        var before = assembler.Snapshot().Memories.Take(24).Select(m => m.Recorded).ToArray();

        assembler.Apply(E8.Value[26861]);
        var change = assembler.Apply(E8.Value[26865]);

        Assert.Null(change?.Memories);
        Assert.Equal(before, assembler.Snapshot().Memories.Take(24).Select(m => m.Recorded).ToArray());
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
