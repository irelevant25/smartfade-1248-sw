using SmartFade.ShowFile.Asc;
using AscShow = SmartFade.ShowFile.Asc.ShowFile;   // the model's name is also the library's namespace

namespace SmartFade.Api.Contracts;

/// <summary>A label block of a show file: the <c>Text</c> line and the three <c>$$Text n</c> lines, verbatim (docs/formats/asc-format.md).</summary>
public sealed record LabelDto(string Text, string Line1, string Line2, string Line3);

/// <summary>A channel at a level in percent, as the file writes it.</summary>
public sealed record ChannelPercentDto(int Channel, int Percent);

/// <summary>One step of the stack or of a sequence; numbers and times as the file wrote them (their meaning is INFERRED at most).</summary>
public sealed record StepDto(
    string Number,
    int? MemoryPage,
    int? MemoryFader,
    int? MemoryNumber,
    int? CueParams,
    string UpTime,
    string UpDelay,
    string DownTime,
    string DownDelay,
    string FollowOn,
    IReadOnlyList<ChannelPercentDto> Channels,
    LabelDto? Label);

/// <summary>A <c>$SEQUENCE</c> with its raw header fields (meaning UNKNOWN beyond the index), its label and its steps.</summary>
public sealed record SequenceDto(int Index, int B, int C, int D, int E, bool IsMainStack, LabelDto? Label, IReadOnlyList<StepDto> Steps);

/// <summary>A memory (<c>GROUP n</c>) with the page and fader derived from its number (24 per page, INFERRED numbering confirmed against the console in E2).</summary>
public sealed record MemoryFileDto(int Number, int Page, int Fader, IReadOnlyList<ChannelPercentDto> Channels, LabelDto Label);

/// <summary>
/// One patch entry: DMX address, channel (0 = unpatched) and the proportional level in percent as the file writes it; null for a
/// show downloaded from the console, whose Patch object carries no level (F70), so such a show is not rendered as a file.
/// </summary>
public sealed record PatchEntryDto(int Dmx, int Channel, int? Level);

public sealed record IndependentDto(int Number, int A, int B, int C, int D, LabelDto Label);

public sealed record LabelledDto(int Number, LabelDto Label);

public sealed record SettingDto(string Key, string Value);

public sealed record FileWarningDto(int Line, string Message);

/// <summary>
/// A parsed <c>.asc</c> show file for the show-file page: everything the reader found, read-only. The
/// statuses of the fields are those of docs/formats/asc-format.md; nothing here reaches a console.
/// </summary>
public sealed record ShowFileDto(
    string Name,
    int Bytes,
    int Lines,
    string LineEnding,
    string Manufacturer,
    string Console,
    string SoftwareVersion,
    string Ident,
    bool ClearAll,
    SequenceDto? MainStack,
    IReadOnlyList<SequenceDto> Sequences,
    IReadOnlyList<MemoryFileDto> Memories,
    IReadOnlyList<PatchEntryDto> Patch,
    IReadOnlyList<IndependentDto> Independents,
    IReadOnlyList<LabelledDto> Channels,
    IReadOnlyList<LabelledDto> Pages,
    IReadOnlyList<SettingDto> Settings,
    int UnknownLines,
    IReadOnlyList<FileWarningDto> Warnings);

public static class ShowFileMapper
{
    private const int MemoriesPerPage = 24;

    public static ShowFileDto ToDto(string name, int bytes, AscReadResult read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var show = read.Show;
        return new ShowFileDto(
            name,
            bytes,
            read.Lines.Count,
            show.LineEnding == AscLineEnding.CrLf ? "crlf" : "lf",
            show.Manufacturer,
            show.Console,
            show.SoftwareVersion,
            show.Ident,
            show.ClearAll,
            show.MainStack is { } main ? ToDto(main) : null,
            show.Sequences.Select(ToDto).ToArray(),
            show.Memories.Select(ToDto).ToArray(),
            show.Patch.Select(p => new PatchEntryDto(p.Dmx, p.Channel, p.Level)).ToArray(),
            show.Independents.Select(i => new IndependentDto(i.Number, i.A, i.B, i.C, i.D, ToDto(i.Label))).ToArray(),
            show.Channels.Select(c => new LabelledDto(c.Number, ToDto(c.Label))).ToArray(),
            show.Pages.Select(p => new LabelledDto(p.Number, ToDto(p.Label))).ToArray(),
            show.Settings.Select(s => new SettingDto(s.Key, s.Value)).ToArray(),
            show.Unknown.Count,
            read.Warnings.Select(w => new FileWarningDto(w.LineNumber, w.Message)).ToArray());
    }

    public static SequenceDto ToDto(Sequence sequence) =>
        new(sequence.A, sequence.B, sequence.C, sequence.D, sequence.E, sequence.IsMainStack, sequence.Label is { } label ? ToDto(label) : null, sequence.Steps.Select(ToDto).ToArray());

    public static StepDto ToDto(Step step) =>
        new(
            step.Number,
            step.Memory?.Page,
            step.Memory?.Fader,
            step.Memory is { } memory ? memory.Page * MemoriesPerPage + memory.Fader + 1 : null,
            step.CueParams,
            step.UpTime,
            step.UpDelay,
            step.DownTime,
            step.DownDelay,
            step.FollowOn,
            step.Channels.Select(c => new ChannelPercentDto(c.Channel, c.Percent)).ToArray(),
            step.Label is { } label ? ToDto(label) : null);

    public static MemoryFileDto ToDto(Memory memory) =>
        new(memory.Number, (memory.Number - 1) / MemoriesPerPage + 1, (memory.Number - 1) % MemoriesPerPage + 1, memory.Channels.Select(c => new ChannelPercentDto(c.Channel, c.Percent)).ToArray(), ToDto(memory.Label));

    public static LabelDto ToDto(TextBlock label) => new(label.Text, label.Line1, label.Line2, label.Line3);

    /// <summary>
    /// The way back, for the editing pages: a DTO the UI edited becomes the model the writer renders. Every field the
    /// DTO carries is taken verbatim; a file whose reader kept unknown lines cannot be rebuilt from the DTO and is refused,
    /// because those lines would be lost.
    /// </summary>
    public static AscShow ToModel(ShowFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.UnknownLines > 0)
        {
            throw new ArgumentException("The file holds lines the reader did not recognise; it cannot be rebuilt without losing them.", nameof(dto));
        }

        return new AscShow(
            dto.Manufacturer,
            dto.Console,
            dto.SoftwareVersion,
            dto.Ident,
            dto.ClearAll,
            dto.MainStack is { } main ? ToModel(main) : null,
            dto.Sequences.Select(ToModel).ToArray(),
            dto.Memories.Select(m => new Memory(m.Number, ToModel(m.Channels), ToModel(m.Label))).ToArray(),
            dto.Patch.Select(p => new PatchEntry(p.Dmx, p.Channel, p.Level ?? throw new ArgumentException($"DMX {p.Dmx} has no proportional level (a show downloaded from the console carries none, F70).", nameof(dto)))).ToArray(),
            dto.Independents.Select(i => new Independent(i.Number, i.A, i.B, i.C, i.D, ToModel(i.Label))).ToArray(),
            dto.Channels.Select(c => new LabelledItem(c.Number, ToModel(c.Label))).ToArray(),
            dto.Pages.Select(p => new LabelledItem(p.Number, ToModel(p.Label))).ToArray(),
            dto.Settings.Select(s => new Setting(s.Key, s.Value)).ToArray(),
            Array.Empty<UnknownRecord>(),
            string.Equals(dto.LineEnding, "crlf", StringComparison.OrdinalIgnoreCase) ? AscLineEnding.CrLf : AscLineEnding.Lf);
    }

    public static Sequence ToModel(SequenceDto dto) =>
        new(dto.Index, dto.B, dto.C, dto.D, dto.E, dto.Label is { } label ? ToModel(label) : null, dto.Steps.Select(ToModel).ToArray());

    public static Step ToModel(StepDto dto) =>
        new(
            dto.Number,
            dto.MemoryPage is { } page && dto.MemoryFader is { } fader ? (page, fader) : null,
            dto.CueParams,
            dto.UpTime,
            dto.UpDelay,
            dto.DownTime,
            dto.DownDelay,
            dto.FollowOn,
            ToModel(dto.Channels),
            dto.Label is { } label ? ToModel(label) : null);

    public static TextBlock ToModel(LabelDto label) => new(label.Text, label.Line1, label.Line2, label.Line3);

    private static IReadOnlyList<ChannelLevelPercent> ToModel(IReadOnlyList<ChannelPercentDto> channels) =>
        channels.Select(c => new ChannelLevelPercent(c.Channel, c.Percent)).ToArray();
}
