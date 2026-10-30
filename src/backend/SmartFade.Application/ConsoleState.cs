using SmartFade.Domain;

namespace SmartFade.Application;

/// <summary>
/// The application's view of the console: desired and actual channel levels side by side (with
/// the channel outputs the device reports), the actual masters, the outputs and memories the
/// device reports, the connection and the commands the device accepts.
/// <see cref="ActualStateReported"/> is false until the device has reported anything in the
/// current connection; <see cref="MastersReported"/>, <see cref="ChannelsReported"/>,
/// <see cref="OutputsReported"/> and <see cref="MemoriesReported"/> until it reported that part; until then
/// the actual levels, masters, outputs and memories are placeholders (zero levels, MASTER full,
/// BUMPS zero, Blackout and SOLO off, no memory recorded), not console state.
/// </summary>
public sealed record ConsoleState(
    ConnectionState Connection,
    ConsoleCapabilities Capabilities,
    DeviceCommands SupportedCommands,
    IReadOnlyList<ChannelLevel> Channels,
    Masters Masters,
    IReadOnlyList<byte> Outputs,
    IReadOnlyList<MemoryState> Memories,
    bool ActualStateReported,
    bool MastersReported,
    bool ChannelsReported,
    bool OutputsReported,
    bool MemoriesReported,
    string? ShowFileName,
    string? FirmwareVersion);
