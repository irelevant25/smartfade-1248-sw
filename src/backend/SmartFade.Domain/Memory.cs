namespace SmartFade.Domain;

/// <summary>
/// One of the console's memories. <paramref name="Page"/> and <paramref name="Slot"/> are the
/// 0-based qualifiers (a, b) of the wire and the show file's `$$MEMORY p f`; the memory number is
/// 24 × page + slot + 1 (H13 / H25 numbering, OBSERVED). That a and b are the console's physical
/// page and fader is still OPEN (H25, E6 probe). Levels are per channel, index 0 = channel 1
/// (F71: 96 level bytes on the wire, channels 49–96 unused on a 1248).
/// </summary>
public sealed record Memory(int Page, int Slot, IReadOnlyList<byte> Levels, Label Label)
{
    public static int ToNumber(int page, int slot, int memoriesPerPage) => memoriesPerPage * page + slot + 1;

    public int Number(ConsoleCapabilities capabilities) => ToNumber(Page, Slot, capabilities.MemoriesPerPage);
}
