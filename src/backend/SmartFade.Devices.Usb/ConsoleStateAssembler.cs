using SmartFade.Application.Ports;
using SmartFade.Domain;
using SmartFade.Protocol;
using SmartFade.Protocol.Objects;

namespace SmartFade.Devices.Usb;

/// <summary>
/// Turns the objects the console delivers into the application's actual state, for display.
/// No transmitted byte depends on anything here (the session sends from F51–F57 alone); the
/// readings below are inbound decoding at the status their facts carry, and nothing is reported
/// before the console delivered it: the masters stay unreported until FaderInfo (MASTER, BUMPS)
/// and LedStatus (SOLO) have both arrived, file name and firmware until their object arrives,
/// and the Blackout and independent states are never claimed (no
/// observed object carries them). What a read-only session can know: the live level objects
/// (ChannelLevels, OutputLevels, FaderInfo, LedStatus), the file name and firmware version, and
/// the memories the console lists on its own (the full download needs SmartSoft's System writes,
/// which are not cleared for sending, F58). Readings and their status:
/// - channel level and output: the console's ChannelLevels object carries post-MASTER values
///   (OBSERVED, F96, one fader and one MASTER sweep; held bumps F97); that a fader's physical
///   position is not reported at all is INFERRED from F82 / F99 (FaderInfo records move with the
///   MASTER too). Both numbers are therefore the same post-MASTER level here;
/// - OutputLevels: DMX address d = body[1 + d] (length VERIFIED F67; index OBSERVED for DMX 1 and 5, F83);
/// - MASTER = FaderInfo body[194] (VERIFIED in part, F82); BUMPS = body[193] (OBSERVED, one session, F95);
/// - SOLO = LedStatus block 52 all 0xFF (INFERRED from one press / release pair, F100);
/// - file name (VERIFIED F60, follows the last save F103) and firmware version (VERIFIED F59);
/// - memories: Memory objects give a page and fader from their qualifiers (0-based a, b; whether
///   they are the physical page and fader is OPEN, H25), the label text (OBSERVED F73; byte order
///   HYPOTHESIS H40) and the levels (VERIFIED bodies F71, scale OBSERVED F72). "Recorded" is read as
///   "some level above zero": INFERRED from the manual's deletion rule (a memory edited to all zero
///   is deleted, F107); body[2] = 0x01 keeps its UNKNOWN meaning (H39). The playback level is not
///   reported.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F59, F60, F67, F71, F72, F73, F82, F83, F87, F95, F96, F97, F100, F103, F112, F113, F114, F115, F116, F117", "display-only decoding; INFERRED: SOLO (F100), BLACKOUT (F114), IND 1 / IND 2 identity (F115, H72), the key LEDs' identities (H72), recorded flags (F107 from a Memory object; F84 / F8 from the LED map), fader position not reported; HYPOTHESIS: H40 label byte order, H25 page / fader, H71 memory fader bytes")]
public sealed class ConsoleStateAssembler
{
    private readonly ConsoleCapabilities _capabilities;
    private readonly byte[] _channelLevels;
    private readonly byte[] _outputs;
    private readonly MemoryState[] _memories;
    private readonly byte[][] _memoryContents;
    private Masters? _masters;
    private FaderInfoObject? _faderInfo;
    private bool? _soloFromLeds;
    private bool? _blackoutFromLeds;
    private LedStatusObject? _leds;
    private IndependentStates? _independents;
    private bool _memoryMode;
    private int _memoryPage = 1;
    private bool _pageReported;
    private bool _functionScreen;
    private PanelKeys? _keys;
    private string? _showFileName;
    private string? _firmwareVersion;

    public ConsoleStateAssembler(ConsoleCapabilities capabilities)
    {
        _capabilities = capabilities;
        _channelLevels = new byte[capabilities.Channels];
        _outputs = new byte[capabilities.DmxAddresses];
        _memories = ConsoleSnapshot.EmptyMemories(capabilities).Select(m => m with { Reported = false }).ToArray();
        _memoryContents = new byte[capabilities.Memories][];
    }

    /// <summary>The LCD as last reported (DisplayContent, F86), for logs; empty until reported.</summary>
    public IReadOnlyList<string> DisplayLines { get; private set; } = Array.Empty<string>();

    public string? ShowFileName => _showFileName;

    public string? FirmwareVersion => _firmwareVersion;

    /// <summary>
    /// MASTER, BUMPS and SOLO as last reported; null, and no masters change raised, until FaderInfo
    /// has arrived (the console's power-on list starts with LedStatus, F94 / H65, so the LED reading
    /// of SOLO is remembered and reported together with the first FaderInfo). BLACKOUT is the LED
    /// reading of block 53 (F114, INFERRED) and the independents come from the Independents object
    /// (F115) once it has arrived.
    /// </summary>
    public Masters? Masters => _masters;

    public IReadOnlyList<byte> ChannelLevels => _channelLevels;

    /// <summary>Applies one delivered object; returns the change to report, or null when the object carries nothing the state model holds.</summary>
    public DeviceStateChange? Apply(ObjectReceived received)
    {
        ArgumentNullException.ThrowIfNull(received);
        var body = received.Payload.Body;
        switch ((HspObjectId)received.Payload.ObjectId)
        {
            case HspObjectId.ChannelLevels:
                return ApplyChannelLevels(ChannelLevelsObject.Decode(body.Span));
            case HspObjectId.OutputLevels:
                return ApplyOutputs(OutputLevelsObject.Decode(body.Span));
            case HspObjectId.FaderInfo:
                return ApplyFaderInfo(FaderInfoObject.Decode(body.Span));
            case HspObjectId.LedStatus:
                return ApplyLeds(LedStatusObject.Decode(body.Span));
            case HspObjectId.FileName:
                _showFileName = FileNameObject.Decode(body.Span).FileName;
                return new DeviceStateChange(ShowFileName: _showFileName);
            case HspObjectId.ConsoleVersion:
                _firmwareVersion = ConsoleVersionObject.Decode(body.Span).Version;
                return new DeviceStateChange(FirmwareVersion: _firmwareVersion);
            case HspObjectId.Memory:
                return ApplyMemory(MemoryObject.Decode(body.Span));
            case HspObjectId.ConsoleStatus:
                return ApplyConsoleStatus(ConsoleStatusObject.Decode(body));
            case HspObjectId.Independents:
                return ApplyIndependents(IndependentsObject.Decode(body.Span));
            case HspObjectId.DisplayContent:
                DisplayLines = DisplayContentObject.Decode(body.Span).Lines;
                _functionScreen = IsFunctionScreen(DisplayLines);
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// The state as reported so far. Parts the console has not delivered keep the application's
    /// placeholders (zero levels; MASTER full, BUMPS zero, everything off), which the device must
    /// present as unreported, never as console state.
    /// </summary>
    public ConsoleSnapshot Snapshot() => new(
        _capabilities,
        (byte[])_channelLevels.Clone(),
        (byte[])_channelLevels.Clone(),
        _masters ?? new Masters(Level.Max, Level.Min, Blackout: false, Solo: false, Independent1: false, Independent2: false),
        (byte[])_outputs.Clone(),
        (MemoryState[])_memories.Clone(),
        _showFileName,
        _firmwareVersion);

    /// <summary>The levels of a memory the console delivered in this session, else null.</summary>
    public MemoryContent? Memory(int page, int fader)
    {
        if (!_capabilities.IsValidMemoryPage(page) || !_capabilities.IsValidMemoryFader(fader))
        {
            return null;
        }

        var index = _capabilities.MemoryIndex(page, fader);
        return _memoryContents[index] is { } levels ? new MemoryContent(page, fader, (byte[])levels.Clone(), _memories[index].Label) : null;
    }

    private DeviceStateChange ApplyChannelLevels(ChannelLevelsObject levels)
    {
        var changed = new Dictionary<int, byte>();
        for (var channel = 1; channel <= _channelLevels.Length && channel <= levels.Levels.Count; channel++)
        {
            var level = levels.At(channel);
            if (_channelLevels[channel - 1] != level)
            {
                _channelLevels[channel - 1] = level;
                changed[channel] = level;
            }
        }

        return new DeviceStateChange(ChannelLevels: changed, ChannelOutputs: (byte[])_channelLevels.Clone());
    }

    private DeviceStateChange ApplyOutputs(OutputLevelsObject outputs)
    {
        for (var i = 0; i < _outputs.Length && i < outputs.Levels.Count; i++)
        {
            _outputs[i] = outputs.Levels[i];
        }

        return new DeviceStateChange(Outputs: (byte[])_outputs.Clone());
    }

    private DeviceStateChange? ApplyFaderInfo(FaderInfoObject faders)
    {
        _faderInfo = faders;
        return Combine(ReportMasters(), ReportMemoryFaders());
    }

    private DeviceStateChange? ApplyLeds(LedStatusObject leds)
    {
        if (leds.Blocks.Count < 53)
        {
            return null;
        }

        _leds = leds;
        _soloFromLeds = leds.Block(52) is { R1: 0xFF, G1: 0xFF, B1: 0xFF, R2: 0xFF, G2: 0xFF, B2: 0xFF };
        // Block 53 steady blue while BLACKOUT is active (F114, OBSERVED three times in E8; that the
        // LED is BLACKOUT's is INFERRED from the owner's account and the manual, F5). Its blink with
        // the MASTER below full (F85) is phase A != phase B and reads as "not active" here.
        _blackoutFromLeds = leds.Block(53) is { R1: 0, G1: 0, B1: 0xFF, R2: 0, G2: 0, B2: 0xFF };
        return Combine(Combine(ReportMasters(), ReportMemoryFaders()), ReportKeys(leds));
    }

    /// <summary>
    /// The key LEDs: block 57 CLEAR, 60 STACK, 63 COPY, 64 REC SEQ, 65 MAGIC, 66 REC MEM, 67 EDIT MEM, plus
    /// 53 BLACKOUT and 52 SOLO for their blink (F85: block 53 blinks while the MASTER is below full)
    /// (identities INFERRED, H72: in E8 each block changed only in its own record function or with its
    /// key; the patterns OBSERVED, F116, F117). Brightness is the first phase's strongest channel:
    /// at most 0x0A off, below 0xC8 dim (0x46), else full; blinking = the two phases differ (F85).
    /// </summary>
    private DeviceStateChange? ReportKeys(LedStatusObject leds)
    {
        if (leds.Blocks.Count < 67)
        {
            return null;
        }

        var keys = new PanelKeys(
            Stack: Led(leds.Block(60)),
            Clear: Led(leds.Block(57)),
            Copy: Led(leds.Block(63)),
            RecSeq: Led(leds.Block(64)),
            Magic: Led(leds.Block(65)),
            RecMem: Led(leds.Block(66)),
            EditMem: Led(leds.Block(67)),
            Blackout: Led(leds.Block(53)),
            Solo: Led(leds.Block(52)));
        if (keys == _keys)
        {
            return null;
        }

        _keys = keys;
        return new DeviceStateChange(Keys: keys);
    }

    private static KeyLed Led(LedBlock block)
    {
        var strongest = Math.Max(block.R1, Math.Max(block.G1, block.B1));
        var light = strongest <= 0x0A ? KeyLight.Off : strongest < 0xC8 ? KeyLight.Dim : KeyLight.Full;
        return new KeyLed(light, block.PhasesDiffer);
    }

    /// <summary>
    /// body[0] = 1 in memory mode (F87, VERIFIED in part); body[2] = the selected memory page − 1
    /// (F113, OBSERVED once in E8: 0x0B while the LCD showed "Memory page:12").
    /// </summary>
    private DeviceStateChange? ApplyConsoleStatus(ConsoleStatusObject status)
    {
        var raw = status.Raw.Span;
        _memoryMode = raw.Length > 0 && raw[0] == 1;
        if (raw.Length < 3)
        {
            return null;
        }

        var page = raw[2] + 1;
        if (!_capabilities.IsValidMemoryPage(page))
        {
            return null;
        }

        var changed = !_pageReported || page != _memoryPage;
        _memoryPage = page;
        _pageReported = true;
        return Combine(changed ? new DeviceStateChange(MemoryPage: page) : null, ReportMemoryFaders());
    }

    /// <summary>
    /// Record byte a = the independent's on state (F115, OBSERVED in E8: 6 + 6 changes with the keys,
    /// in step with ChannelLevels[49] / [50] and LED blocks 72 / 73); record 1 = IND 1 by order
    /// (INFERRED, H72).
    /// </summary>
    private DeviceStateChange? ApplyIndependents(IndependentsObject independents)
    {
        if (independents.Independents.Count < 2)
        {
            return null;
        }

        var states = new IndependentStates(independents.Independents[0].A == 1, independents.Independents[1].A == 1);
        if (states == _independents)
        {
            return null;
        }

        _independents = states;
        var masters = ReportMasters();
        return masters is null ? new DeviceStateChange(Independents: states) : masters with { Independents = states };
    }

    /// <summary>
    /// The masters are one reading from two objects: MASTER and BUMPS from FaderInfo, SOLO and
    /// BLACKOUT from LedStatus. Nothing is claimed before both have arrived (E2 delivered FaderInfo
    /// first, JSONL seq 16, then LedStatus, seq 20; every E3b session LedStatus first, F94), and an
    /// unchanged reading is no change. IND 1 / IND 2 come from the Independents object when it has
    /// arrived (false until then; the state carries its own reported flag for them).
    /// </summary>
    private DeviceStateChange? ReportMasters()
    {
        if (_faderInfo is not { } faders || _soloFromLeds is not { } solo)
        {
            return null;
        }

        var masters = new Masters(
            faders.Master,
            faders.Bumps,
            Blackout: _blackoutFromLeds ?? false,
            Solo: solo,
            Independent1: _independents?.Independent1 ?? false,
            Independent2: _independents?.Independent2 ?? false);
        if (masters == _masters)
        {
            return null;
        }

        _masters = masters;
        return new DeviceStateChange(Masters: _masters);
    }

    /// <summary>
    /// In memory mode physical fader k is memory fader k of the selected page: FaderInfo record k
    /// byte 0 is its level (F112, OBSERVED in E8; bytes 0 and 1 were equal there, their difference
    /// is H71), and LED block k says whether that memory is recorded: dim red 0x46 or full red 0xFF
    /// in both phases, 0x0A when empty (F84 VERIFIED in part; the reading INFERRED from the manual's
    /// "a recorded memory is indicated in red", F8). The MEMS page view and the record-function
    /// screens reuse those LEDs (F113, F116: blinking blocks, LCD titles), so the recorded reading is
    /// not taken while any of blocks 1–24 blinks or the LCD shows one of those screens. Labels and
    /// contents stay unknown in a read-only session (F89).
    /// </summary>
    private DeviceStateChange? ReportMemoryFaders()
    {
        if (!_memoryMode || !_pageReported || _faderInfo is not { } faders)
        {
            return null;
        }

        var readLeds = _leds is { } leds && !_functionScreen && leds.Blocks.Count >= _capabilities.MemoriesPerPage
            && !leds.Blocks.Take(_capabilities.MemoriesPerPage).Any(b => b.PhasesDiffer);
        List<MemoryState>? changed = null;
        for (var fader = 1; fader <= _capabilities.MemoriesPerPage && fader <= faders.FaderLevels.Count; fader++)
        {
            var index = _capabilities.MemoryIndex(_memoryPage, fader);
            var current = _memories[index];
            var recorded = current.Recorded;
            if (readLeds)
            {
                var block = _leds!.Block(fader);
                if (block.G1 == 0 && block.B1 == 0)
                {
                    recorded = block.R1 >= 0x46;
                }
            }

            var next = current with { Level = faders.At(fader), Recorded = recorded, Reported = true };
            if (next != current)
            {
                _memories[index] = next;
                (changed ??= new List<MemoryState>()).Add(next);
            }
        }

        return changed is null ? null : new DeviceStateChange(Memories: changed);
    }

    /// <summary>The LCD titles of the views that reuse the memory LEDs for something else (F113, F116; OBSERVED texts, E8).</summary>
    private static readonly string[] FunctionScreenTitles = { "Memory page:", "Record Memory", "Edit Memory", "Record Sequence", "Copy Memory/Seq" };

    private static bool IsFunctionScreen(IReadOnlyList<string> lines) =>
        lines.Count > 0 && FunctionScreenTitles.Any(title => lines[0].StartsWith(title, StringComparison.Ordinal));

    /// <summary>Merges two partial changes (each carries parts the other does not).</summary>
    private static DeviceStateChange? Combine(DeviceStateChange? first, DeviceStateChange? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return first with
        {
            Masters = first.Masters ?? second.Masters,
            Memories = first.Memories ?? second.Memories,
            Independents = first.Independents ?? second.Independents,
            MemoryPage = first.MemoryPage ?? second.MemoryPage,
            Keys = first.Keys ?? second.Keys,
        };
    }

    private DeviceStateChange? ApplyMemory(MemoryObject memory)
    {
        var page = memory.Page + 1;
        var fader = memory.Slot + 1;
        if (!_capabilities.IsValidMemoryPage(page) || !_capabilities.IsValidMemoryFader(fader))
        {
            return null;
        }

        var index = _capabilities.MemoryIndex(page, fader);
        var levels = memory.Levels.Take(_capabilities.Channels).ToArray();
        _memoryContents[index] = levels;
        var label = memory.Label;
        var state = new MemoryState(page, fader, _memories[index].Level, Recorded: levels.Any(l => l > 0), new Label(label.Line1, label.Line2, label.Line3), Reported: true);
        _memories[index] = state;
        return new DeviceStateChange(Memories: new[] { state });
    }
}
