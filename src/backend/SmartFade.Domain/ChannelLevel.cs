namespace SmartFade.Domain;

/// <summary>
/// Desired versus actual level of one intensity channel (1-based). The console is the source
/// of truth for <see cref="Actual"/>; <see cref="Desired"/> is what the user asked for;
/// <see cref="Pending"/> is true while they differ and a command is in flight.
/// <see cref="Output"/> is what the console says the channel is doing after HTP with memories
/// and bumps and after the MASTER (the console's ChannelLevels object carries post-MASTER values,
/// F96, F97): it mimics the bump-button LED (F8) and is never compared with the desired level.
/// </summary>
public sealed record ChannelLevel(int Channel, byte Desired, byte Actual, bool Pending, byte Output = 0);
