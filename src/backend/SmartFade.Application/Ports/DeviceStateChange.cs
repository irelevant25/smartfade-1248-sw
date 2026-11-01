using SmartFade.Domain;

namespace SmartFade.Application.Ports;

/// <summary>
/// What a device reports as changed. Every part may be null (unchanged). A
/// <see cref="Snapshot"/> replaces the whole actual state. <see cref="ChannelLevels"/> are fader
/// levels by channel number; <see cref="ChannelOutputs"/> is the complete list of channel outputs
/// (index 0 = channel 1); <see cref="Memories"/> lists the memories whose state changed;
/// <see cref="ShowFileName"/> and <see cref="FirmwareVersion"/> are reported on their own when a
/// device learns them without a full snapshot; <see cref="Independents"/> and <see cref="MemoryPage"/> (the
/// console's selected memory page, 1-based) likewise; <see cref="Keys"/> is the LED state of the keys a real
/// console reports (the simulator reports none); <see cref="FaderBank"/> what the physical faders control, when a
/// real console reports it (ConsoleStatus, F98).
/// </summary>
public sealed record DeviceStateChange(
    ConsoleSnapshot? Snapshot = null,
    IReadOnlyDictionary<int, byte>? ChannelLevels = null,
    IReadOnlyList<byte>? ChannelOutputs = null,
    Masters? Masters = null,
    IReadOnlyList<byte>? Outputs = null,
    IReadOnlyList<MemoryState>? Memories = null,
    string? ShowFileName = null,
    string? FirmwareVersion = null,
    IndependentStates? Independents = null,
    int? MemoryPage = null,
    PanelKeys? Keys = null,
    IReadOnlyList<string>? Lcd = null,
    CrossfaderState? Crossfader = null,
    FaderBank? FaderBank = null);

/// <summary>The on / off state of IND 1 and IND 2, reported on its own when a device learns it apart from the masters.</summary>
public sealed record IndependentStates(bool Independent1, bool Independent2);
