namespace SmartFade.Domain;

/// <summary>
/// A sequence with its steps and label. <paramref name="Page"/> and <paramref name="Number"/> are
/// the 0-based wire qualifiers (a, b); their physical meaning is OPEN (H25). The four header fields
/// the console sends after the step count (`$SEQUENCE b c d e` in the show file) are kept raw
/// because their meaning is UNKNOWN (H42).
/// </summary>
public sealed record Sequence(
    int Page,
    int Number,
    IReadOnlyList<Step> Steps,
    Label Label,
    IReadOnlyList<int> RawHeaderFields);
