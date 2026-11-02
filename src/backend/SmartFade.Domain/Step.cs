namespace SmartFade.Domain;

/// <summary>
/// A stack or sequence step as the console sends it in its 12-byte step record (OBSERVED
/// against the show file, F74; layout hypothesis H41): cue number in tenths, step type
/// (`$$CUEPARAMS`: 1 a memory step, 2 a "State" step, 0 a channel step; H12 CONFIRMED, F154, F160),
/// memory index 24 × page + slot (for a channel step the channel − 1, F160, OBSERVED once), and
/// the up, down and follow-on times in tenths of a second (the console names the third "W", H14
/// CONFIRMED). A "State" step also carries its own 96 levels and a label (F154, F161); the other
/// kinds carry neither (null).
/// </summary>
public sealed record Step(
    int CueTenths,
    int StepType,
    int MemoryIndex,
    int UpTenths,
    int DownTenths,
    int FollowOnTenths,
    IReadOnlyList<byte>? Levels = null,
    Label? Label = null)
{
    public decimal Cue => CueTenths / 10m;
}
