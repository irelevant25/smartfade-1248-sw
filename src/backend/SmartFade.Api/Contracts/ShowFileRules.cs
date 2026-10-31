using System.Globalization;

namespace SmartFade.Api.Contracts;

/// <summary>
/// What an edited show file must satisfy before it is rendered: the ranges and shapes the 1248's own files use
/// (docs/formats/asc-format.md, "Step rules learned from the files", OBSERVED on 34 files). The patch: 512 DMX
/// addresses, 1..512 in order; a channel is 0 (unpatched) or 1..96; a level is 0..100 percent. The steps: times in
/// the console's forms; a memory reference as a 0-based page 0..11 and fader 0..23; a memory step carries
/// <c>$$CUEPARAMS 1</c> and repeats its memory's channel list (none when the memory is not in the file); a sequence
/// step's number is its memory's number; stack cue numbers have one decimal, stay below 200 and rise through the
/// stack. Nothing here reaches a console: the rules only keep a rendered file inside what the console was seen to write.
/// </summary>
public static partial class ShowFileRules
{
    public const int DmxAddresses = 512;
    public const int MaxChannel = 96;
    private const int MemoriesPerPage = 24;

    /// <summary>The engine's own limit, from the string "Cue number >199.9" in sfEngine.dll (docs/formats/asc-format.md).</summary>
    private const decimal MaxCueNumber = 199.9m;

    public static string? Check(ShowFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Patch.Count != DmxAddresses)
        {
            return $"The patch must list all {DmxAddresses} DMX addresses; it lists {dto.Patch.Count}.";
        }

        for (var i = 0; i < dto.Patch.Count; i++)
        {
            var entry = dto.Patch[i];
            if (entry.Dmx != i + 1)
            {
                return $"The patch must list DMX addresses 1..{DmxAddresses} in order; entry {i + 1} is DMX {entry.Dmx}.";
            }

            if (entry.Channel < 0 || entry.Channel > MaxChannel)
            {
                return $"DMX {entry.Dmx}: channel {entry.Channel} is outside 0..{MaxChannel} (0 = unpatched).";
            }

            if (entry.Level < 0 || entry.Level > 100)
            {
                return $"DMX {entry.Dmx}: level {entry.Level} % is outside 0..100.";
            }
        }

        var memories = new Dictionary<int, MemoryFileDto>();
        foreach (var memory in dto.Memories)
        {
            memories[memory.Number] = memory;
        }

        var stepProblem = dto.MainStack is { } main ? CheckSteps("the stack", main, memories) : null;
        foreach (var sequence in dto.Sequences)
        {
            stepProblem ??= CheckSteps($"sequence {sequence.Index}", sequence, memories);
        }

        return stepProblem;
    }

    /// <summary>
    /// The times of a step as the files write them: seconds with at most one decimal (<c>0</c>, <c>5.0</c>, <c>58</c>) or
    /// minutes and seconds (<c>1:30</c>); <c>$$CUEPARAMS</c> 0..2 (the three values seen, hypothesis H12). A memory step
    /// (908 of 908 in the 34 files): <c>$$CUEPARAMS 1</c>, the memory's own channel list repeated (894 of 894 when the memory
    /// is in the file, none in the 14 cases where it is not) and, in a sequence, the memory's number as the step number
    /// (383 of 383, as <c>265.0</c>). Stack cue numbers: one decimal, at most 199.9, rising through the stack (525 of 525).
    /// </summary>
    private static string? CheckSteps(string where, SequenceDto sequence, IReadOnlyDictionary<int, MemoryFileDto> memories)
    {
        decimal? previousCue = null;
        for (var i = 0; i < sequence.Steps.Count; i++)
        {
            var step = sequence.Steps[i];
            var name = $"{where}, step {step.Number}";
            if (string.IsNullOrWhiteSpace(step.Number))
            {
                return $"{where}, step {i + 1}: the step number is empty.";
            }

            foreach (var (field, value) in new[] { ("up time", step.UpTime), ("up delay", step.UpDelay), ("down time", step.DownTime), ("down delay", step.DownDelay), ("follow-on", step.FollowOn) })
            {
                if (!IsTime(value))
                {
                    return $"{name}: the {field} '{value}' is not a time the console writes (seconds with at most one decimal, or m:ss).";
                }
            }

            if (step.MemoryPage is { } page && (page < 0 || page > 11))
            {
                return $"{name}: the memory page {page} is outside 0..11.";
            }

            if (step.MemoryFader is { } fader && (fader < 0 || fader > 23))
            {
                return $"{name}: the memory fader {fader} is outside 0..23.";
            }

            if ((step.MemoryPage is null) != (step.MemoryFader is null))
            {
                return $"{name}: a memory reference needs both a page and a fader.";
            }

            if (step.CueParams is { } cueParams && (cueParams < 0 || cueParams > 2))
            {
                return $"{name}: $$CUEPARAMS {cueParams} is outside the values seen (0..2).";
            }

            if (sequence.IsMainStack)
            {
                if (!CueNumberPattern().IsMatch(step.Number) || !decimal.TryParse(step.Number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var cue) || cue > MaxCueNumber)
                {
                    return $"{name}: a stack cue number has one decimal and is at most {MaxCueNumber.ToString(CultureInfo.InvariantCulture)} (the engine's own limit).";
                }

                if (previousCue is { } previous && cue <= previous)
                {
                    return $"{name}: cue numbers rise through the stack in every file the console wrote; {step.Number} follows {previous.ToString("0.0", CultureInfo.InvariantCulture)}.";
                }

                previousCue = cue;
            }

            if (step.MemoryPage is { } memoryPage && step.MemoryFader is { } memoryFader)
            {
                var memoryNumber = memoryPage * MemoriesPerPage + memoryFader + 1;
                if (step.CueParams != 1)
                {
                    return $"{name}: a step with a memory carries $$CUEPARAMS 1 in every file the console wrote.";
                }

                if (!sequence.IsMainStack && step.Number != string.Create(CultureInfo.InvariantCulture, $"{memoryNumber}.0"))
                {
                    return $"{name}: a sequence step is numbered by its memory ({memoryNumber}.0) in every file the console wrote.";
                }

                var expected = memories.TryGetValue(memoryNumber, out var memory) ? memory.Channels : Array.Empty<ChannelPercentDto>();
                if (!SameChannels(step.Channels, expected))
                {
                    return memory is null
                        ? $"{name}: memory {memoryNumber} is not in the file, so the step carries no channels."
                        : $"{name}: the step's channels must repeat memory {memoryNumber}'s ({Describe(expected)}).";
                }
            }
        }

        return null;
    }

    private static bool SameChannels(IReadOnlyList<ChannelPercentDto> actual, IReadOnlyList<ChannelPercentDto> expected)
    {
        if (actual.Count != expected.Count)
        {
            return false;
        }

        for (var i = 0; i < actual.Count; i++)
        {
            if (actual[i].Channel != expected[i].Channel || actual[i].Percent != expected[i].Percent)
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(IReadOnlyList<ChannelPercentDto> channels) =>
        channels.Count == 0 ? "no channels" : string.Join(' ', channels.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Channel}/{c.Percent}")));

    private static bool IsTime(string value) => TimePattern().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex("^([0-9]+([.][0-9])?|[0-9]+:[0-9]{2})$")]
    private static partial System.Text.RegularExpressions.Regex TimePattern();

    [System.Text.RegularExpressions.GeneratedRegex("^[0-9]{1,3}[.][0-9]$")]
    private static partial System.Text.RegularExpressions.Regex CueNumberPattern();

    /// <summary>The name offered for the download: never the original, so a save can never replace it by accident.</summary>
    public static string EditedFileName(string original)
    {
        var name = string.IsNullOrWhiteSpace(original) ? "show.asc" : Path.GetFileName(original.Trim());
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".asc";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{stem}-edited{extension}");
    }
}
