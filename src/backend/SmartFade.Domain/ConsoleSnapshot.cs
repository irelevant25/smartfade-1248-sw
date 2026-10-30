namespace SmartFade.Domain;

/// <summary>
/// The actual state a device reports in one piece: channel fader levels and channel outputs
/// (index 0 = channel 1), masters, DMX output levels (index 0 = DMX address 1) and the memories
/// (index 0 = memory 1, page 1 fader 1). A channel's output is what the console says the channel
/// is doing after HTP, bumps and the MASTER (the console's ChannelLevels object, F96, F97); its
/// level is the fader position the device reports.
/// </summary>
public sealed record ConsoleSnapshot(
    ConsoleCapabilities Capabilities,
    IReadOnlyList<byte> ChannelLevels,
    IReadOnlyList<byte> ChannelOutputs,
    Masters Masters,
    IReadOnlyList<byte> Outputs,
    IReadOnlyList<MemoryState> Memories,
    string? ShowFileName,
    string? FirmwareVersion)
{
    public static ConsoleSnapshot Empty(ConsoleCapabilities capabilities) => new(
        capabilities,
        new byte[capabilities.Channels],
        new byte[capabilities.Channels],
        new Masters(Level.Max, Level.Min, Blackout: false, Solo: false, Independent1: false, Independent2: false),
        new byte[capabilities.DmxAddresses],
        EmptyMemories(capabilities),
        ShowFileName: null,
        FirmwareVersion: null);

    /// <summary>Every memory of the console, unrecorded, faders at zero, no label.</summary>
    public static IReadOnlyList<MemoryState> EmptyMemories(ConsoleCapabilities capabilities)
    {
        var memories = new MemoryState[capabilities.Memories];
        for (var i = 0; i < memories.Length; i++)
        {
            memories[i] = new MemoryState(
                Page: i / capabilities.MemoriesPerPage + 1,
                Fader: i % capabilities.MemoriesPerPage + 1,
                Level.Min,
                Recorded: false,
                Label.Empty);
        }

        return memories;
    }
}
