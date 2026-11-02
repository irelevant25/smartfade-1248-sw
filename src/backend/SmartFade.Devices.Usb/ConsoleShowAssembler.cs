using SmartFade.Domain;
using SmartFade.Protocol;
using SmartFade.Protocol.Objects;

namespace SmartFade.Devices.Usb;

/// <summary>
/// Collects the show objects the console delivers (the start-up pair's download, SC-4 clause 4; and any it lists later) and
/// builds the <see cref="ConsoleShow"/> from them, in wire units: FileName (F60, F103), Patch (F70), Memory (F71, F183),
/// SequenceMain and StepContent (the stack, F74, F153, F154, F160, F161), Sequence (F74, F156), Independents (F76),
/// ChannelText and Page (F73). Every reading is the decoder's, with the status its record carries; what has no source in the
/// download (the file header, the step delays, the patch levels, the settings) is not invented here. Display only: no byte
/// the device sends depends on it, but the MASTER probe's choice of an unpatched channel (<see cref="UnpatchedChannel"/>).
/// Not thread-safe: the device calls it under its lock.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F70, F71, F73, F74, F76, F153, F154, F156, F160, F161, F162, F183", "the download's show objects as the decoders read them; bodies VERIFIED for the E2 / E3 set (F70-F77), the edited shows' objects OBSERVED (E12, E12b)")]
public sealed class ConsoleShowAssembler
{
    /// <summary>ChannelText a = 0..95: 96 label objects in every download (F73; `$CHANNEL 1..96` in every show file), twice the 1248's channels.</summary>
    private const int ChannelLabelCount = 96;

    private readonly ConsoleCapabilities _capabilities;
    private readonly SortedDictionary<(int Page, int Slot), MemoryObject> _memories = new();
    private readonly SortedDictionary<int, StepContentObject> _stackSteps = new();
    private readonly SortedDictionary<(int Page, int Number), SequenceObject> _sequences = new();
    private readonly Dictionary<int, TextBlock> _channelLabels = new();
    private readonly Dictionary<int, TextBlock> _pageLabels = new();
    private string? _fileName;
    private PatchObject? _patch;
    private SequenceMainObject? _stack;
    private IndependentsObject? _independents;

    public ConsoleShowAssembler(ConsoleCapabilities capabilities)
    {
        _capabilities = capabilities;
    }

    /// <summary>The Patch as delivered (entry d − 1 = the channel at DMX address d, 0 = unpatched; F70, H34), else null.</summary>
    public IReadOnlyList<byte>? PatchChannels => _patch?.ChannelByAddress;

    /// <summary>
    /// Keeps a show object; true when <paramref name="received"/> was one (and so the show may have changed). A body that does
    /// not decode throws (<see cref="InvalidDataException"/>), as the other decoders do; the caller logs it.
    /// </summary>
    public bool Apply(ObjectReceived received)
    {
        ArgumentNullException.ThrowIfNull(received);
        var body = received.Payload.Body.Span;
        switch ((HspObjectId)received.Payload.ObjectId)
        {
            case HspObjectId.FileName:
                _fileName = FileNameObject.Decode(body).FileName;
                return true;
            case HspObjectId.Patch:
                _patch = PatchObject.Decode(body);
                return true;
            case HspObjectId.Memory:
                var memory = MemoryObject.Decode(body);
                _memories[(memory.Page, memory.Slot)] = memory;
                return true;
            case HspObjectId.SequenceMain:
                _stack = SequenceMainObject.Decode(body);
                return true;
            case HspObjectId.StepContent:
                var step = StepContentObject.Decode(body);
                _stackSteps[step.Index] = step;
                return true;
            case HspObjectId.Sequence:
                var sequence = SequenceObject.Decode(body);
                _sequences[(sequence.Page, sequence.Number)] = sequence;
                return true;
            case HspObjectId.Independents:
                _independents = IndependentsObject.Decode(body);
                return true;
            case HspObjectId.ChannelText:
                var channel = LabelObject.Decode(body);
                _channelLabels[channel.Index] = channel.Label;
                return true;
            case HspObjectId.Page:
                var page = LabelObject.Decode(body);
                _pageLabels[page.Index] = page.Label;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The show as collected so far: the stack's steps are the StepContent records 0 .. its step count − 1 that arrived (F74).</summary>
    public ConsoleShow Build()
    {
        var stack = _stack is { } main
            ? new Sequence(
                0,
                0,
                _stackSteps.Where(s => s.Key < main.StepCount).Select(s => ToStep(s.Value.Step, s.Value.Levels, s.Value.Label)).ToArray(),
                ToLabel(main.Label),
                new[] { main.B, main.C, main.D, main.E })
            : null;
        var sequences = _sequences.Values
            .Select(s => new Sequence(s.Page, s.Number, s.Steps.Select(r => ToStep(r, null, null)).ToArray(), ToLabel(s.Label), new[] { s.B, s.C, s.D, s.E }))
            .ToArray();
        var memories = _memories.Values
            .Select(m => new Memory(m.Page, m.Slot, m.Levels.ToArray(), ToLabel(m.Label)))
            .ToArray();
        var independents = _independents?.Independents
            .Select((r, i) => new Independent(i + 1, r.A, r.B, r.C, r.D, ToLabel(r.Label)))
            .ToArray() ?? Array.Empty<Independent>();
        return new ConsoleShow(
            _fileName,
            _patch is { } patch ? new Patch(patch.ChannelByAddress) : null,
            memories,
            stack,
            sequences,
            independents,
            Labels(_channelLabels, ChannelLabelCount),
            Labels(_pageLabels, _capabilities.MemoryPages));
    }

    /// <summary>
    /// The first channel of <paramref name="bank"/> (1–24 or 25–48) that no DMX address of <paramref name="patch"/> names, else
    /// null: SC-4 clause 4's MASTER probe taps the bump of such a channel, whose output reaches no DMX address (an unpatched
    /// channel changed no DMX in E8b, channel 49, and E9d, channel 47: OBSERVED). Internal for the tests.
    /// </summary>
    internal static int? UnpatchedChannel(IReadOnlyList<byte> patch, FaderBank bank)
    {
        var first = bank switch
        {
            FaderBank.Channels1To24 => 1,
            FaderBank.Channels25To48 => 25,
            _ => throw new ArgumentOutOfRangeException(nameof(bank), bank, "The probe runs in channel mode only (SC-4 clause 4)."),
        };

        for (var channel = first; channel < first + 24; channel++)
        {
            if (!patch.Contains((byte)channel))
            {
                return channel;
            }
        }

        return null;
    }

    private static Step ToStep(StepRecord record, IReadOnlyList<byte>? levels, TextBlock? label) =>
        new(record.CueTenths, record.Type, record.MemoryIndex, record.UpTenths, record.DownTenths, record.WaitTenths, levels, label is null ? null : ToLabel(label));

    private static Label ToLabel(TextBlock block) => new(block.Line1, block.Line2, block.Line3);

    private static IReadOnlyList<Label> Labels(Dictionary<int, TextBlock> delivered, int count) =>
        Enumerable.Range(0, count).Select(i => delivered.TryGetValue(i, out var block) ? ToLabel(block) : Label.Empty).ToArray();
}
