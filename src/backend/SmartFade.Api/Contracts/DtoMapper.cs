using SmartFade.Application;
using SmartFade.Domain;

namespace SmartFade.Api.Contracts;

public static class DtoMapper
{
    /// <summary>
    /// Rule, not a counter: the messages cleared for sending by `/hardware-safety-check`, never by
    /// a fact promotion. SC-1 (2026-10-30, docs/protocol/safety-checks.md) cleared two for a
    /// read-only session: the poll and the fetch of a record the console listed.
    /// </summary>
    public const int MessagesClearedForSending = 2;

    /// <summary>Wire names of the hub commands (docs/api.md), pinned independently of the enum member names.</summary>
    private static readonly (DeviceCommands Flag, string Name)[] CommandWireNames =
    [
        (DeviceCommands.SetChannelLevel, "setChannelLevel"),
        (DeviceCommands.SetMaster, "setMaster"),
        (DeviceCommands.SetBlackout, "setBlackout"),
        (DeviceCommands.SetBumps, "setBumps"),
        (DeviceCommands.SetSolo, "setSolo"),
        (DeviceCommands.BumpChannel, "bumpChannel"),
        (DeviceCommands.SetMemoryLevel, "setMemoryLevel"),
        (DeviceCommands.BumpMemory, "bumpMemory"),
        (DeviceCommands.RecordMemory, "recordMemory"),
        (DeviceCommands.StoreMemory, "storeMemory"),
        (DeviceCommands.SetIndependent, "setIndependent"),
        (DeviceCommands.ClearChannels, "clearChannels"),
        (DeviceCommands.ClearMemories, "clearMemories"),
    ];

    /// <summary>Wire names of docs/api.md, pinned independently of the enum member names.</summary>
    public static string StatusName(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Disconnected => "disconnected",
        ConnectionStatus.Connecting => "connecting",
        ConnectionStatus.Connected => "connected",
        ConnectionStatus.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string ModeName(DeviceMode mode) => mode switch
    {
        DeviceMode.Simulated => "simulated",
        DeviceMode.Replay => "replay",
        DeviceMode.Hardware => "hardware",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static IReadOnlyList<string> CommandNames(DeviceCommands commands) =>
        CommandWireNames.Where(c => commands.HasFlag(c.Flag)).Select(c => c.Name).ToArray();

    public static ConnectionDto ToDto(ConnectionState state) =>
        new(StatusName(state.Status), ModeName(state.Mode), state.DeviceDescription, state.Error);

    public static CapabilitiesDto ToDto(ConsoleCapabilities c) =>
        new(c.Channels, c.PhysicalFaders, c.MemoryPages, c.MemoriesPerPage, c.Sequences, c.StackSteps, c.DmxAddresses, c.Independents);

    public static ChannelStateDto ToDto(ChannelLevel level) =>
        new(level.Channel, level.Desired, level.Actual, level.Pending, level.Output);

    public static MastersDto ToDto(Masters masters) =>
        new(masters.Master, masters.Bumps, masters.Blackout, masters.Solo, masters.Independent1, masters.Independent2);

    public static MemoryStateDto ToDto(MemoryState memory, ConsoleCapabilities capabilities) =>
        new(memory.Page, memory.Fader, memory.Number(capabilities), memory.Level, memory.Recorded, memory.Label.ToString());

    public static MemoryDto ToDto(MemoryContent memory, ConsoleCapabilities capabilities) =>
        new(memory.Page, memory.Fader, capabilities.MemoryNumber(memory.Page, memory.Fader), memory.Label.ToString(), memory.Levels.Select(b => (int)b).ToArray());

    public static ConsoleStateDto ToDto(ConsoleState state) =>
        new(
            ToDto(state.Connection),
            ToDto(state.Capabilities),
            CommandNames(state.SupportedCommands),
            state.Channels.Select(ToDto).ToArray(),
            ToDto(state.Masters),
            state.Outputs.Select(b => (int)b).ToArray(),
            state.Memories.Select(m => ToDto(m, state.Capabilities)).ToArray(),
            state.ActualStateReported,
            state.MastersReported,
            state.ChannelsReported,
            state.OutputsReported,
            state.MemoriesReported,
            state.ShowFileName,
            state.FirmwareVersion);

    public static OutputsDto ToOutputsDto(IReadOnlyList<byte> outputs) =>
        new(outputs.Select(b => (int)b).ToArray());

    /// <summary>Maps a rejected command to a stable error code for the UI.</summary>
    public static ErrorDto ToErrorDto(Exception exception) => exception switch
    {
        NotConnectedException => new ErrorDto("The console is not connected.", "not_connected"),
        NotSupportedException => new ErrorDto("The connected device does not accept this command.", "not_supported"),
        ArgumentOutOfRangeException { ParamName: "channel" } => new ErrorDto("Channel must be 1..48.", "invalid_channel"),
        ArgumentOutOfRangeException { ParamName: "level" } => new ErrorDto("Level must be 0..255.", "invalid_level"),
        ArgumentOutOfRangeException { ParamName: "page" or "fader" } => new ErrorDto("Memory page must be 1..12 and fader 1..24.", "invalid_memory"),
        ArgumentException => new ErrorDto("Invalid argument.", "invalid_argument"),
        _ => new ErrorDto("The device reported an error.", "device_error"),
    };
}
