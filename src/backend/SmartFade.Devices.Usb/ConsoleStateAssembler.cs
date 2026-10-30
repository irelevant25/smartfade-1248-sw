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
[Evidence(EvidenceStatus.Observed, "F59, F60, F67, F71, F72, F73, F82, F83, F95, F96, F97, F100, F103", "display-only decoding; INFERRED: SOLO (F100), recorded flag (F107), fader position not reported; HYPOTHESIS: H40 label byte order, H25 page / fader")]
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
    private string? _showFileName;
    private string? _firmwareVersion;

    public ConsoleStateAssembler(ConsoleCapabilities capabilities)
    {
        _capabilities = capabilities;
        _channelLevels = new byte[capabilities.Channels];
        _outputs = new byte[capabilities.DmxAddresses];
        _memories = ConsoleSnapshot.EmptyMemories(capabilities).ToArray();
        _memoryContents = new byte[capabilities.Memories][];
    }

    /// <summary>The LCD as last reported (DisplayContent, F86), for logs; empty until reported.</summary>
    public IReadOnlyList<string> DisplayLines { get; private set; } = Array.Empty<string>();

    public string? ShowFileName => _showFileName;

    public string? FirmwareVersion => _firmwareVersion;

    /// <summary>
    /// MASTER, BUMPS and SOLO as last reported; null, and no masters change raised, until FaderInfo
    /// has arrived (the console's power-on list starts with LedStatus, F94 / H65, so the LED reading
    /// of SOLO is remembered and reported together with the first FaderInfo). Blackout and the
    /// independents are never claimed.
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
            case HspObjectId.DisplayContent:
                DisplayLines = DisplayContentObject.Decode(body.Span).Lines;
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
        return ReportMasters();
    }

    private DeviceStateChange? ApplyLeds(LedStatusObject leds)
    {
        if (leds.Blocks.Count < 52)
        {
            return null;
        }

        _soloFromLeds = leds.Block(52) is { R1: 0xFF, G1: 0xFF, B1: 0xFF, R2: 0xFF, G2: 0xFF, B2: 0xFF };
        return ReportMasters();
    }

    /// <summary>
    /// The masters are one reading from two objects: MASTER and BUMPS from FaderInfo, SOLO from
    /// LedStatus. Nothing is claimed before both have arrived (E2 delivered FaderInfo first, JSONL
    /// seq 16, then LedStatus, seq 20; every E3b session LedStatus first, F94), and an unchanged
    /// reading is no change.
    /// </summary>
    private DeviceStateChange? ReportMasters()
    {
        if (_faderInfo is not { } faders || _soloFromLeds is not { } solo)
        {
            return null;
        }

        var masters = new Masters(faders.Master, faders.Bumps, Blackout: false, Solo: solo, Independent1: false, Independent2: false);
        if (masters == _masters)
        {
            return null;
        }

        _masters = masters;
        return new DeviceStateChange(Masters: _masters);
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
        var state = new MemoryState(page, fader, _memories[index].Level, Recorded: levels.Any(l => l > 0), new Label(label.Line1, label.Line2, label.Line3));
        _memories[index] = state;
        return new DeviceStateChange(Memories: new[] { state });
    }
}
