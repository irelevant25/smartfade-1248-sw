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
/// <see cref="IndependentsReported"/> says whether IND 1 / IND 2 in the masters are the device's word;
/// <see cref="MemoryPage"/> is the console's selected memory page when the device reports one, else null;
/// <see cref="Keys"/> the key LEDs a real console reports, null until reported (the simulator never does);
/// <see cref="WritableControls"/> the controls the device accepts commands for, null when every control of a
/// supported command's kind is accepted (<see cref="DeviceControls"/>);
/// <see cref="FaderBank"/> what the console's physical faders control when the device reports it (a real console,
/// ConsoleStatus), else null (the simulator plays every bank at once).
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
    bool IndependentsReported,
    int? MemoryPage,
    PanelKeys? Keys,
    IReadOnlyList<string>? Lcd,
    CrossfaderState? Crossfader,
    string? ShowFileName,
    string? FirmwareVersion,
    IReadOnlySet<string>? WritableControls = null,
    FaderBank? FaderBank = null);
