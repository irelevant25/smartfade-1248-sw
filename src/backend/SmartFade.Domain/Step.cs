namespace SmartFade.Domain;

/// <summary>
/// A stack or sequence step as the console sends it in its 12-byte step record (OBSERVED
/// against the show file, F74; layout hypothesis H41): cue number in tenths, step type
/// (`$$CUEPARAMS`, meaning hypothesis H12), memory index 24 × page + slot, and the up, down
/// and follow-on times in tenths of a second (follow-on versus the console's "Wait" name is
/// open, H14).
/// </summary>
public sealed record Step(
    int CueTenths,
    int StepType,
    int MemoryIndex,
    int UpTenths,
    int DownTenths,
    int FollowOnTenths)
{
    public decimal Cue => CueTenths / 10m;
}
