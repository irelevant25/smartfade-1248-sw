namespace SmartFade.Domain;

/// <summary>
/// The show as the real console delivers it in the download its start-up pair starts (safety check SC-4 clause 4; the
/// download's objects F182 d, their layouts cross-checked against the console's own show files, F70–F77, F154, F160–F162):
/// the file name of its last SD save (F103), the patch (DMX address → channel, F70), the memories it lists (F71; the empty
/// ones it lists too, F183), the stack (SequenceMain and StepContent, F74) and the sequences (F74, F156), the independents
/// (F76), and the channel and page labels (F73). Wire units throughout (levels 0..255, times in tenths of a second, 0-based
/// qualifiers); what the show file writes instead (percent, time text, step numbers) is a separate mapping. Not in the
/// download, so not here: the file's header lines, the step delays, the patch's proportional levels, the `SET` lines.
/// </summary>
public sealed record ConsoleShow(
    string? FileName,
    Patch? Patch,
    IReadOnlyList<Memory> Memories,
    Sequence? Stack,
    IReadOnlyList<Sequence> Sequences,
    IReadOnlyList<Independent> Independents,
    IReadOnlyList<Label> ChannelLabels,
    IReadOnlyList<Label> PageLabels);
