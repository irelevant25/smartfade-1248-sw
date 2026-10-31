namespace SmartFade.ShowFile.Asc;

/// <summary>Line ending of a file: console-written SHOWnn.ASC files are LF only, SmartSoft-written files CRLF (docs/formats/asc-format.md, OBSERVED).</summary>
public enum AscLineEnding
{
    Lf,
    CrLf,
}

/// <summary>
/// A label as the file writes it: the <c>Text</c> line and the three <c>$$Text n</c> lines, all verbatim.
/// The relation between <see cref="Text"/> and the numbered lines is version-dependent (asc-format.md,
/// "Text block"), so every value is kept and none is derived from another.
/// </summary>
public sealed record TextBlock(string Text, string Line1, string Line2, string Line3)
{
    public static readonly TextBlock Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);

    public bool IsEmpty => Text.Length == 0 && Line1.Length == 0 && Line2.Length == 0 && Line3.Length == 0;
}

/// <summary>A channel at a level in percent, as the file's <c>CHAN c/l</c> pairs give it (the console's 8-bit level is derived, asc-format.md).</summary>
public readonly record struct ChannelLevelPercent(int Channel, int Percent);

/// <summary>
/// One step of the main stack (<c>CUE</c>) or of a sequence (<c>$CUE</c>). The number and the times are kept as
/// the file writes them (e.g. <c>1.0</c>, <c>10.1</c>, <c>265.0</c>; <c>0</c>, <c>5.0</c>, <c>1:30</c>): their
/// meaning on the console is INFERRED at most (asc-format.md, "Step record").
/// </summary>
public sealed record Step(
    string Number,
    (int Page, int Fader)? Memory,
    int? CueParams,
    string UpTime,
    string UpDelay,
    string DownTime,
    string DownDelay,
    string FollowOn,
    IReadOnlyList<ChannelLevelPercent> Channels,
    TextBlock? Label);

/// <summary>A <c>$SEQUENCE a b c d e</c> header with its optional label and its steps. Index 48 is the main stack (INFERRED, asc-format.md).</summary>
public sealed record Sequence(int A, int B, int C, int D, int E, TextBlock? Label, IReadOnlyList<Step> Steps)
{
    public const int MainStackIndex = 48;

    public bool IsMainStack => A == MainStackIndex;
}

/// <summary>A memory (<c>GROUP n</c>, n = 1..288 = 24 × (page − 1) + fader; INFERRED numbering, OBSERVED against the console in E2) with its channels and label.</summary>
public sealed record Memory(int Number, IReadOnlyList<ChannelLevelPercent> Channels, TextBlock Label);

/// <summary>One <c>PATCH 1 d/c/l</c> triple: DMX address, channel (0 = unpatched) and proportional level in percent (always 100 in every sample; its source is UNKNOWN).</summary>
public readonly record struct PatchEntry(int Dmx, int Channel, int Level);

/// <summary>An <c>$INDEPENDENT n a b c d</c> record with its label; the four fields are UNKNOWN (always <c>0 255 0 1</c> in the samples).</summary>
public sealed record Independent(int Number, int A, int B, int C, int D, TextBlock Label);

/// <summary>A <c>$CHANNEL n</c> or <c>$PAGE n</c> label container.</summary>
public sealed record LabelledItem(int Number, TextBlock Label);

/// <summary>A <c>SET key value</c> line of the settings section.</summary>
public sealed record Setting(string Key, string Value);

/// <summary>A line the reader did not recognise; kept verbatim with its position (asc-format.md, "Unknown data policy").</summary>
public sealed record UnknownRecord(string Keyword, string RawLine, int LineNumber, string Section);

/// <summary>A reader warning: something unexpected that was kept verbatim rather than dropped.</summary>
public sealed record AscWarning(int LineNumber, string Message);

/// <summary>
/// A complete show file as the 1248 and SmartSoft write it (asc-format.md, "Structure"): the header, the main
/// stack, the sequences, the memories, the patch, the independents, the channel and page labels and the
/// settings, plus whatever the reader could not place. Everything is kept as text where the meaning is not
/// settled, so that a file can be written back byte for byte.
/// </summary>
public sealed record ShowFile(
    string Manufacturer,
    string Console,
    string SoftwareVersion,
    string Ident,
    bool ClearAll,
    Sequence? MainStack,
    IReadOnlyList<Sequence> Sequences,
    IReadOnlyList<Memory> Memories,
    IReadOnlyList<PatchEntry> Patch,
    IReadOnlyList<Independent> Independents,
    IReadOnlyList<LabelledItem> Channels,
    IReadOnlyList<LabelledItem> Pages,
    IReadOnlyList<Setting> Settings,
    IReadOnlyList<UnknownRecord> Unknown,
    AscLineEnding LineEnding);

/// <summary>The result of reading a file: the show, the verbatim lines it came from, and the warnings.</summary>
public sealed record AscReadResult(ShowFile Show, IReadOnlyList<string> Lines, IReadOnlyList<AscWarning> Warnings);
