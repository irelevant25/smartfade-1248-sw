namespace SmartFade.Domain;

/// <summary>
/// The MASTER (Grand Master) and BUMPS fader levels, the Blackout and SOLO button states and
/// the two independent buttons IND 1 / IND 2. The names follow the console's panel labels
/// (owner, 2026-10-29) and the manual (known-facts F5, F6, F108). SOLO selects the bump mode
/// (F6, F107): off = pile-on, a held bump adds into the live output at the BUMPS level; on =
/// solo, the bumped content replaces the current output. The independents are two outputs
/// "separated from all other controls" (F108), here as toggles.
/// </summary>
public sealed record Masters(byte Master, byte Bumps, bool Blackout, bool Solo, bool Independent1, bool Independent2);
