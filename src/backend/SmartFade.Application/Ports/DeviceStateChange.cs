using SmartFade.Domain;

namespace SmartFade.Application.Ports;

/// <summary>
/// What a device reports as changed. Every part may be null (unchanged). A
/// <see cref="Snapshot"/> replaces the whole actual state. <see cref="ChannelLevels"/> are fader
/// levels by channel number; <see cref="ChannelOutputs"/> is the complete list of channel outputs
/// (index 0 = channel 1); <see cref="Memories"/> lists the memories whose state changed.
/// </summary>
public sealed record DeviceStateChange(
    ConsoleSnapshot? Snapshot = null,
    IReadOnlyDictionary<int, byte>? ChannelLevels = null,
    IReadOnlyList<byte>? ChannelOutputs = null,
    Masters? Masters = null,
    IReadOnlyList<byte>? Outputs = null,
    IReadOnlyList<MemoryState>? Memories = null);
