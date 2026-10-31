using System.Globalization;

namespace SmartFade.Api.Contracts;

/// <summary>
/// What an edited show file must satisfy before it is rendered: the ranges the 1248's own files use
/// (docs/formats/asc-format.md). 512 DMX addresses, 1..512 in order; a channel is 0 (unpatched) or
/// 1..96 (the file lists 96 `$CHANNEL` entries; 1..48 are the console's faders, 49 and 50 the
/// independents by hypothesis H11); a level is 0..100 percent. Memory and step edits are not
/// validated here yet (E10 steps 2 and 3 add them with their rules).
/// </summary>
public static partial class ShowFileRules
{
    public const int DmxAddresses = 512;
    public const int MaxChannel = 96;

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

        var stepProblem = dto.MainStack is { } main ? CheckSteps("the stack", main) : null;
        foreach (var sequence in dto.Sequences)
        {
            stepProblem ??= CheckSteps($"sequence {sequence.Index}", sequence);
        }

        return stepProblem;
    }

    /// <summary>
    /// The times of a step as the files write them: seconds with at most one decimal (<c>0</c>, <c>5.0</c>, <c>58</c>) or
    /// minutes and seconds (<c>1:30</c>); the referenced memory a 0-based page 0..11 and fader 0..23; <c>$$CUEPARAMS</c>
    /// 0..2 (the three values seen, hypothesis H12). The step number is the file's own text and is not edited here.
    /// </summary>
    private static string? CheckSteps(string where, SequenceDto sequence)
    {
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
        }

        return null;
    }

    private static bool IsTime(string value) => TimePattern().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex("^([0-9]+([.][0-9])?|[0-9]+:[0-9]{2})$")]
    private static partial System.Text.RegularExpressions.Regex TimePattern();

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
