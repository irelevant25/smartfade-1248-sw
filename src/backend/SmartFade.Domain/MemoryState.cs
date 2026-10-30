namespace SmartFade.Domain;

/// <summary>
/// One memory as the console presents it: <paramref name="Page"/> 1..12 and <paramref name="Fader"/>
/// 1..24 (the memory fader, and the bump button under it, in MEMS mode), both 1-based as printed
/// on the console; the memory number is 24 × (page − 1) + fader. <see cref="Level"/> is the memory
/// fader's playback level (0..255). <see cref="Recorded"/> says whether the memory has content: the
/// console lights a recorded memory's bump button dimmed red, and a memory edited to all-zero
/// intensities counts as deleted (console manual v3.0 printed pp. 32 and 44; F107).
/// <see cref="Reported"/> is false for a placeholder the console has not delivered yet: a read-only
/// session receives memories one object at a time, if at all, while the simulator reports every
/// memory. The
/// wire-oriented <see cref="Memory"/> record keeps the protocol's 0-based qualifiers (a, b) instead.
/// </summary>
public sealed record MemoryState(int Page, int Fader, byte Level, bool Recorded, Label Label, bool Reported = true)
{
    public int Number(ConsoleCapabilities capabilities) => capabilities.MemoryNumber(Page, Fader);
}

/// <summary>
/// The channel levels stored in one memory (index 0 = channel 1, 0..255; F71, F72) with its label,
/// addressed like <see cref="MemoryState"/> (1-based page and fader).
/// </summary>
public sealed record MemoryContent(int Page, int Fader, IReadOnlyList<byte> Levels, Label Label);
