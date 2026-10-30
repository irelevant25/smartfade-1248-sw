namespace SmartFade.Api.Contracts;

// Wire shapes of docs/api.md. Levels are 0..255 (the console's unit); no bytes, ids or transports.

public sealed record ConnectionDto(string Status, string Mode, string DeviceDescription, string? Error);

public sealed record CapabilitiesDto(
    int Channels,
    int PhysicalFaders,
    int MemoryPages,
    int MemoriesPerPage,
    int Sequences,
    int StackSteps,
    int DmxAddresses,
    int Independents);

/// <summary>Output is what the console says the channel is doing after HTP, bumps and the MASTER; never compared with Desired.</summary>
public sealed record ChannelStateDto(int Channel, int Desired, int Actual, bool Pending, int Output);

/// <summary>Independent1 / Independent2 are the IND 1 / IND 2 toggles (F108).</summary>
public sealed record MastersDto(int Master, int Bumps, bool Blackout, bool Solo, bool Independent1, bool Independent2);

/// <summary>A memory as the console addresses it: page 1..12, fader 1..24; Number = 24 × (page − 1) + fader.</summary>
public sealed record MemoryStateDto(int Page, int Fader, int Number, int Level, bool Recorded, string Label, bool Reported);

/// <summary>A memory's channel levels (index 0 = channel 1) and label.</summary>
public sealed record MemoryDto(int Page, int Fader, int Number, string Label, IReadOnlyList<int> Levels);

public sealed record ConsoleStateDto(
    ConnectionDto Connection,
    CapabilitiesDto Capabilities,
    IReadOnlyList<string> SupportedCommands,
    IReadOnlyList<ChannelStateDto> Channels,
    MastersDto Masters,
    IReadOnlyList<int> Outputs,
    IReadOnlyList<MemoryStateDto> Memories,
    bool ActualStateReported,
    bool MastersReported,
    bool ChannelsReported,
    bool OutputsReported,
    bool MemoriesReported,
    string? ShowFileName,
    string? FirmwareVersion);

public sealed record OutputsDto(IReadOnlyList<int> Outputs);

/// <summary>Stable codes: not_connected, invalid_channel, invalid_level, invalid_memory, invalid_argument, not_supported, device_error.</summary>
public sealed record ErrorDto(string Message, string? Code);

/// <summary>Counts come from docs/protocol/known-facts.md; only the rule-backed zero is hard-coded here.</summary>
public sealed record ProtocolKnowledgeDto(int MessagesClearedForSending);

public sealed record StatusDto(string Mode, ConnectionDto Connection, string BackendVersion, ProtocolKnowledgeDto ProtocolKnowledge);
