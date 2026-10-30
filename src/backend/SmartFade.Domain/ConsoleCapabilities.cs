namespace SmartFade.Domain;

/// <summary>
/// Fixed limits of a console model.
/// The SmartFade 1248 values are DOCUMENTED (docs/protocol/known-facts.md F3, F7: spec sheet
/// and console manual v3.0). The captures showed 48 channel levels and 512 DMX addresses in
/// the console's objects (F67, F70); the other numbers are documentation only.
/// </summary>
public sealed record ConsoleCapabilities(
    string Model,
    int Channels,
    int PhysicalFaders,
    int MemoryPages,
    int MemoriesPerPage,
    int Sequences,
    int StackSteps,
    int DmxAddresses,
    int Independents)
{
    public static ConsoleCapabilities SmartFade1248 { get; } = new(
        Model: "SmartFade 1248",
        Channels: 48,
        PhysicalFaders: 24,
        MemoryPages: 12,
        MemoriesPerPage: 24,
        Sequences: 48,
        StackSteps: 199,
        DmxAddresses: 512,
        Independents: 2);

    public int Memories => MemoryPages * MemoriesPerPage;

    public bool IsValidChannel(int channel) => channel >= 1 && channel <= Channels;

    public bool IsValidDmxAddress(int address) => address >= 1 && address <= DmxAddresses;

    /// <summary>Memory pages are numbered 1..12 on the console (manual: "fader pages 1-12", F7).</summary>
    public bool IsValidMemoryPage(int page) => page >= 1 && page <= MemoryPages;

    /// <summary>Memory faders are numbered 1..24 on the console (the bump buttons 1-24 in MEMS mode, F7).</summary>
    public bool IsValidMemoryFader(int fader) => fader >= 1 && fader <= MemoriesPerPage;

    /// <summary>Memory number 1..<see cref="Memories"/> of a 1-based page and fader.</summary>
    public int MemoryNumber(int page, int fader) => MemoriesPerPage * (page - 1) + fader;

    /// <summary>0-based index of a 1-based page and fader into a list of all memories.</summary>
    public int MemoryIndex(int page, int fader) => MemoryNumber(page, fader) - 1;
}
