using System.Globalization;
using System.Text;

namespace SmartFade.ShowFile.Asc;

/// <summary>
/// Writes a <see cref="ShowFile"/> the way the SmartFade engine writes it: the section order, the banners,
/// the blank lines between records and sections, the fixed-width padding of the step times and the
/// trailing spaces (asc-format.md, "Physical format" and "Structure"). Every rule here was taken from the
/// files in hand and is checked by the round-trip tests (parse, render, compare byte for byte); none
/// comes from a specification. A file read with <see cref="AscReader"/> and rendered unchanged must come
/// out byte-identical; where it does not, the format rule is wrong, not the file.
/// </summary>
public static class AscWriter
{
    private const int ChannelsPerLine = 8;
    private const int PatchEntriesPerLine = 5;

    public static byte[] Render(ShowFile show)
    {
        ArgumentNullException.ThrowIfNull(show);
        var lines = new List<string>();
        lines.Add("MANUFACTURER " + show.Manufacturer);
        lines.Add("CONSOLE " + show.Console);
        lines.Add("$SOFTWAREVERSION " + show.SoftwareVersion);
        lines.Add("IDENT " + show.Ident);
        lines.Add(string.Empty);
        if (show.ClearAll)
        {
            lines.Add("CLEAR ALL");
            lines.Add(string.Empty);
        }

        lines.Add("! Main Sequence");
        if (show.MainStack is { } main)
        {
            WriteSequence(lines, main, "CUE");
        }

        lines.Add("! Sequences");
        foreach (var sequence in show.Sequences)
        {
            WriteSequence(lines, sequence, "$CUE");
        }

        lines.Add(string.Empty);

        lines.Add("! Memories");
        foreach (var memory in show.Memories)
        {
            lines.Add("GROUP " + Int(memory.Number));
            WriteChannels(lines, memory.Channels);
            WriteTextBlock(lines, memory.Label);
            lines.Add(string.Empty);
            lines.Add(string.Empty);
        }

        lines.Add(string.Empty);

        lines.Add("! Patch");
        foreach (var chunk in show.Patch.Chunk(PatchEntriesPerLine))
        {
            var builder = new StringBuilder("PATCH 1 ");
            foreach (var entry in chunk)
            {
                builder.Append(Int(entry.Dmx)).Append('/').Append(Int(entry.Channel)).Append('/').Append(Int(entry.Level)).Append(' ');
            }

            lines.Add(builder.ToString());
        }

        lines.Add(string.Empty);

        lines.Add("! Independents");
        foreach (var independent in show.Independents)
        {
            lines.Add($"$INDEPENDENT {Int(independent.Number)} {Int(independent.A)} {Int(independent.B)} {Int(independent.C)} {Int(independent.D)}");
            WriteTextBlock(lines, independent.Label);
            lines.Add(string.Empty);
        }

        lines.Add(string.Empty);

        lines.Add("! Channel Info");
        foreach (var channel in show.Channels)
        {
            lines.Add("$CHANNEL " + Int(channel.Number));
            WriteTextBlock(lines, channel.Label);
            lines.Add(string.Empty);
        }

        lines.Add("! Pages");
        foreach (var page in show.Pages)
        {
            lines.Add("$PAGE " + Int(page.Number));
            WriteTextBlock(lines, page.Label);
            lines.Add(string.Empty);
            lines.Add(string.Empty);   // pages, like memories, end with two blank lines (Ostrov lines 1073-1074)
        }

        lines.Add("! Settings");
        foreach (var setting in show.Settings)
        {
            lines.Add($"SET {setting.Key} {setting.Value}");
        }

        lines.Add(string.Empty);
        lines.Add("ENDDATA ");

        return Join(lines, show.LineEnding);
    }

    /// <summary>The verbatim lines of a read file, joined with its line ending: byte-identical to the input by construction.</summary>
    public static byte[] RenderVerbatim(AscReadResult read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Join(read.Lines, read.Show.LineEnding);
    }

    /// <summary>A step as the engine writes it: the number right-aligned in four characters, the times in fixed-width columns.</summary>
    public static IEnumerable<string> StepLines(Step step, string keyword)
    {
        ArgumentNullException.ThrowIfNull(step);
        yield return keyword + " " + step.Number.PadLeft(4);
        if (step.Memory is { } memory)
        {
            yield return $"$$MEMORY {Int(memory.Page)} {Int(memory.Fader)}";
        }

        if (step.CueParams is { } cueParams)
        {
            yield return "$$CUEPARAMS " + Int(cueParams);
        }

        yield return "UP " + step.UpTime.PadRight(6) + step.UpDelay.PadRight(5);
        yield return "DOWN " + step.DownTime.PadRight(6) + step.DownDelay.PadRight(5);
        yield return "FOLLOWON " + step.FollowOn.PadRight(5);
        foreach (var line in ChannelLines(step.Channels))
        {
            yield return line;
        }

        if (step.Label is { } label)
        {
            foreach (var line in TextBlockLines(label))
            {
                yield return line;
            }

            // A step that carries a label block ends with two blank lines, like memories and pages: the one here and the
            // caller's (SHOW15.ASC, E12b, 2026-10-31: the "State" step 13.1, the only labelled step seen in a console-written
            // file; OBSERVED once).
            yield return string.Empty;
        }
    }

    public static IEnumerable<string> ChannelLines(IReadOnlyList<ChannelLevelPercent> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        foreach (var chunk in channels.Chunk(ChannelsPerLine))
        {
            var builder = new StringBuilder("CHAN ");
            foreach (var entry in chunk)
            {
                builder.Append(Int(entry.Channel)).Append('/').Append(Int(entry.Percent)).Append(' ');
            }

            yield return builder.ToString();
        }
    }

    public static IEnumerable<string> TextBlockLines(TextBlock label)
    {
        ArgumentNullException.ThrowIfNull(label);
        yield return "Text " + label.Text;
        yield return "$$Text 1 " + label.Line1;
        yield return "$$Text 2 " + label.Line2;
        yield return "$$Text 3 " + label.Line3;
    }

    private static void WriteSequence(List<string> lines, Sequence sequence, string stepKeyword)
    {
        lines.Add($"$SEQUENCE {Int(sequence.A)} {Int(sequence.B)} {Int(sequence.C)} {Int(sequence.D)} {Int(sequence.E)}");
        lines.Add(string.Empty);
        if (sequence.Label is { } label)
        {
            WriteTextBlock(lines, label);
            lines.Add(string.Empty);
        }

        foreach (var step in sequence.Steps)
        {
            lines.AddRange(StepLines(step, stepKeyword));
            lines.Add(string.Empty);
        }
    }

    private static void WriteChannels(List<string> lines, IReadOnlyList<ChannelLevelPercent> channels) => lines.AddRange(ChannelLines(channels));

    private static void WriteTextBlock(List<string> lines, TextBlock label) => lines.AddRange(TextBlockLines(label));

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static byte[] Join(IReadOnlyList<string> lines, AscLineEnding lineEnding)
    {
        var separator = lineEnding == AscLineEnding.CrLf ? "\r\n" : "\n";
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.Append(line).Append(separator);
        }

        return Encoding.Latin1.GetBytes(builder.ToString());
    }
}
