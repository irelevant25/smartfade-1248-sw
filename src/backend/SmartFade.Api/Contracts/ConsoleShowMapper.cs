using System.Globalization;
using SmartFade.Domain;

namespace SmartFade.Api.Contracts;

/// <summary>
/// The show the real console delivered in the download of its start-up pair (safety check SC-4 clause 4; `ConsoleShow`, in wire
/// units) as the <see cref="ShowFileDto"/> the PATCH and STACK / SEQ pages edit, in the forms the console's own files use: only
/// what the download carries, by the readings the cross-checks of the download against the console's SD saves found
/// (docs/formats/asc-format.md "Cross-check against the E2 capture", "Step rules learned from the files"; F70–F77, F154,
/// F160–F162). What the download does not carry is left empty and named in <see cref="ShowFileDto.Warnings"/> (line 0): the
/// file header lines, the step delays, the patch's proportional levels (null) and the `SET` lines. Such a DTO is refused by
/// `POST /api/showfile/render` (no level, no delay), so it is never saved as a file as it stands. Nothing reaches a console.
/// </summary>
public static class ConsoleShowMapper
{
    private const int MemoriesPerPage = 24;
    private const int MaxMemoryChannel = 48;
    private const int StackIndex = 48;
    private const int SequencesPerPage = 4;

    public static ShowFileDto ToDto(ConsoleShow show)
    {
        ArgumentNullException.ThrowIfNull(show);
        var warnings = new List<string>
        {
            "The console's show as its download at Connect delivered it (SC-4), not a file: the download carries no file header, no step delays, no proportional patch levels and no SET lines, so those are left empty and this show is not saved as a file as it stands.",
        };

        var memories = show.Memories
            .Select(m => (Memory: m, Number: Memory.ToNumber(m.Page, m.Slot, MemoriesPerPage), Channels: Channels(m.Levels, $"memory {Memory.ToNumber(m.Page, m.Slot, MemoriesPerPage)}", warnings)))
            .Where(m => m.Channels.Count > 0 && m.Number is >= 1 and <= MemoriesPerPage * 12)
            .Select(m => new MemoryFileDto(m.Number, (m.Number - 1) / MemoriesPerPage + 1, (m.Number - 1) % MemoriesPerPage + 1, m.Channels, Label(m.Memory.Label)))
            .ToArray();
        var memoryChannels = memories.ToDictionary(m => m.Number, m => m.Channels);

        var stack = show.Stack is { } main
            ? new SequenceDto(StackIndex, Field(main, 0), Field(main, 1), Field(main, 2), Field(main, 3), true, OptionalLabel(main.Label), main.Steps.Select(s => StepDto(s, isStack: true, memoryChannels, warnings)).ToArray())
            : null;
        var sequences = show.Sequences.Select(s =>
        {
            if ((s.Page, s.Number) != (0, 0))
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Sequence ({s.Page}, {s.Number}): its index {s.Page * SequencesPerPage + s.Number} is INFERRED as page × 4 + slot (smartsoft-analysis.md S11.16 row 3); only (0, 0) = index 0 was ever seen."));
            }

            return new SequenceDto(s.Page * SequencesPerPage + s.Number, Field(s, 0), Field(s, 1), Field(s, 2), Field(s, 3), false, OptionalLabel(s.Label), s.Steps.Select(step => StepDto(step, isStack: false, memoryChannels, warnings)).ToArray());
        }).ToArray();

        var patch = show.Patch is { } table
            ? Enumerable.Range(1, table.DmxAddresses).Select(dmx => new PatchEntryDto(dmx, table.ChannelAt(dmx), null)).ToArray()
            : Array.Empty<PatchEntryDto>();
        if (show.Patch is null)
        {
            warnings.Add("The download delivered no Patch.");
        }

        return new ShowFileDto(
            show.FileName is { } name ? $"{name} (console)" : "Console show",
            0,
            0,
            "lf",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            stack,
            sequences,
            memories,
            patch,
            show.Independents.Select(i => new IndependentDto(i.Number, i.A, i.B, i.C, i.D, Label(i.Label))).ToArray(),
            show.ChannelLabels.Select((label, i) => new LabelledDto(i + 1, Label(label))).ToArray(),
            show.PageLabels.Select((label, i) => new LabelledDto(i + 1, Label(label))).ToArray(),
            Array.Empty<SettingDto>(),
            0,
            warnings.Distinct().Select(w => new FileWarningDto(0, w)).ToArray());
    }

    /// <summary>
    /// A time in tenths of a second as the console's files write it (OBSERVED on the 35 SD saves, docs/formats/asc-format.md): `0`;
    /// below 10 s one decimal (`0.5`, `5.0`); whole seconds from 10 s (`10`, `58`); minutes and seconds from 60 s (`1:30`,
    /// `10:00`). A value no file shows (a fraction at 10 s or more) keeps its decimal and is named in the warnings.
    /// </summary>
    public static string Time(int tenths, ICollection<string>? warnings = null)
    {
        if (tenths == 0)
        {
            return "0";
        }

        if (tenths < 100)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
        }

        if (tenths % 10 != 0)
        {
            warnings?.Add(string.Create(CultureInfo.InvariantCulture, $"A time of {tenths / 10}.{tenths % 10} s has no form in any of the console's files (whole seconds from 10 s); it is shown with its decimal."));
            return string.Create(CultureInfo.InvariantCulture, $"{tenths / 10}.{tenths % 10}");
        }

        var seconds = tenths / 10;
        return seconds < 60
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:D2}");
    }

    /// <summary>
    /// A level 0..255 as the file's percent: `CHAN` levels are raw = ceil(pct × 255 / 100) in 53 of 53 (F72), which rounding to
    /// the nearest percent inverts exactly; for a level that is no whole percent (stored from a fader, F145) the console's own
    /// rounding is UNKNOWN (F72), so such levels are named in the warnings.
    /// </summary>
    public static int Percent(byte raw) => (raw * 100 + 127) / 255;

    private static IReadOnlyList<ChannelPercentDto> Channels(IReadOnlyList<byte> levels, string owner, ICollection<string> warnings)
    {
        var channels = new List<ChannelPercentDto>();
        for (var i = 0; i < levels.Count; i++)
        {
            if (levels[i] == 0)
            {
                continue;
            }

            if (i >= MaxMemoryChannel)
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{owner}: channel {i + 1} holds a level; the console's files name channels 1..48 only, so it is not shown."));
                continue;
            }

            var percent = Percent(levels[i]);
            if ((int)Math.Ceiling(percent * 255 / 100.0) != levels[i])
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{owner}: channel {i + 1} at 0x{levels[i]:X2} is no whole percent; shown as {percent} % (the console's own rounding is UNKNOWN, F72)."));
            }

            if (percent > 0)
            {
                channels.Add(new ChannelPercentDto(i + 1, percent));
            }
        }

        return channels;
    }

    private static StepDto StepDto(Step step, bool isStack, IReadOnlyDictionary<int, IReadOnlyList<ChannelPercentDto>> memoryChannels, ICollection<string> warnings)
    {
        var cue = string.Create(CultureInfo.InvariantCulture, $"{step.CueTenths / 10}.{step.CueTenths % 10}");
        var times = (Up: Time(step.UpTenths, warnings), Down: Time(step.DownTenths, warnings), Wait: Time(step.FollowOnTenths, warnings));
        switch (step.StepType)
        {
            case 1 when step.MemoryIndex < MemoriesPerPage * 12:
                // a memory step: `$$MEMORY p f` = index 24p + f (H13, OBSERVED 26 / 26), its memory's channels repeated (894 / 894),
                // none when the memory is empty (14 / 14); a sequence step numbered by its memory (383 / 383)
                var number = step.MemoryIndex + 1;
                return new StepDto(
                    isStack ? cue : string.Create(CultureInfo.InvariantCulture, $"{number}.0"),
                    step.MemoryIndex / MemoriesPerPage,
                    step.MemoryIndex % MemoriesPerPage,
                    number,
                    1,
                    times.Up,
                    string.Empty,
                    times.Down,
                    string.Empty,
                    times.Wait,
                    memoryChannels.TryGetValue(number, out var channels) ? channels : Array.Empty<ChannelPercentDto>(),
                    null);
            case 0:
                // a channel step: the index bytes hold the channel − 1 and the file writes the channel at 100 % (F160, once; INFERRED)
                var channel = step.MemoryIndex + 1;
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Step {cue}: a channel step of channel {channel}; the step record holds no level and the console's file wrote 100 % for one such step (F160, INFERRED)."));
                return new StepDto(
                    isStack ? cue : string.Create(CultureInfo.InvariantCulture, $"{channel}.0"),
                    null,
                    null,
                    null,
                    0,
                    times.Up,
                    string.Empty,
                    times.Down,
                    string.Empty,
                    times.Wait,
                    new[] { new ChannelPercentDto(channel, 100) },
                    null);
            default:
                // a "State" step (type 2, F154, F161): its own levels and label; or a type never seen, shown without content
                if (step.StepType != 2)
                {
                    warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Step {cue}: step type {step.StepType} was never seen; shown without its content."));
                }
                else if (!isStack)
                {
                    warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Step {cue}: a State step in a sequence was never seen; numbered with the step's own cue."));
                }

                return new StepDto(
                    cue,
                    null,
                    null,
                    null,
                    step.StepType,
                    times.Up,
                    string.Empty,
                    times.Down,
                    string.Empty,
                    times.Wait,
                    step.StepType == 2 && step.Levels is { } levels ? Channels(levels, $"step {cue}", warnings) : Array.Empty<ChannelPercentDto>(),
                    step.Label is { IsEmpty: false } label ? Label(label) : null);
        }
    }

    private static int Field(Sequence sequence, int index) => sequence.RawHeaderFields.Count > index ? sequence.RawHeaderFields[index] : 0;

    private static LabelDto Label(SmartFade.Domain.Label label) =>
        new(ShowFileRules.LabelText(label.Line1, label.Line2, label.Line3), label.Line1, label.Line2, label.Line3);

    private static LabelDto? OptionalLabel(SmartFade.Domain.Label label) => label.IsEmpty ? null : Label(label);
}
