using SmartFade.Domain;

namespace SmartFade.Application.Ports;

/// <summary>
/// Port to a console, real or simulated. An implementation reports the console's actual state
/// through <see cref="StateChanged"/>; the commands express desired state and never imply
/// that the console accepted them. <see cref="SupportedCommands"/> says which commands this
/// implementation accepts: the simulator accepts all, while whether a real 1248 accepts any
/// PC-side command (levels, masters, bumps, memories) is UNKNOWN (H6 open; H24 and the PC → console
/// part of H5 untested), and MASTER, BUMPS, SOLO and Blackout are physical, show-critical
/// controls. Phase 1 ships no real-device implementation: no protocol message is cleared for
/// sending (CLAUDE.md). Calls are made one at a time by <see cref="ConsoleService"/>'s command
/// pump, with a token owned by the service, never by a UI client.
/// Memories are addressed as on the console: page 1..12, fader 1..24 (<see cref="MemoryState"/>).
/// </summary>
public interface ISmartFadeDevice
{
    DeviceMode Mode { get; }

    string Description { get; }

    ConnectionStatus Status { get; }

    ConsoleCapabilities Capabilities { get; }

    DeviceCommands SupportedCommands { get; }

    /// <summary>
    /// The individual controls the device accepts commands for (<see cref="DeviceControls"/> names), or null when
    /// every control of a supported command's kind is accepted (the simulator). A device cleared for a few controls
    /// only (the real console under SC-2) names exactly those; the UI enables nothing else, and the device still
    /// refuses the rest itself.
    /// </summary>
    IReadOnlySet<string>? WritableControls => null;

    /// <summary>Raised whenever the device reports a change of its actual state.</summary>
    event EventHandler<DeviceStateChange>? StateChanged;

    /// <summary>Raised on every connection status change, with the transition's value.</summary>
    event EventHandler<ConnectionState>? ConnectionChanged;

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    Task<ConsoleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken);

    Task SetMasterAsync(byte level, CancellationToken cancellationToken);

    Task SetBlackoutAsync(bool on, CancellationToken cancellationToken);

    Task SetBumpsAsync(byte level, CancellationToken cancellationToken);

    Task SetSoloAsync(bool on, CancellationToken cancellationToken);

    /// <summary>Presses (true) or releases (false) the bump button under a channel fader.</summary>
    Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken);

    /// <summary>GO as a tap (press and release as one operation): the next step of the stack. Only a device reporting <see cref="DeviceCommands.PressGo"/> accepts it.</summary>
    Task PressGoAsync(CancellationToken cancellationToken);

    /// <summary>FADERS MEMS as a tap: the physical faders to memory mode. Only a device reporting <see cref="DeviceCommands.PressFadersMems"/> accepts it.</summary>
    Task PressFadersMemsAsync(CancellationToken cancellationToken);

    /// <summary>FADERS 1-24 as a tap: the physical faders to channel mode, bank 1-24. Only a device reporting <see cref="DeviceCommands.PressFaders1To24"/> accepts it.</summary>
    Task PressFaders1To24Async(CancellationToken cancellationToken);

    /// <summary>Moves a memory fader: the playback level of one memory.</summary>
    Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken);

    /// <summary>Presses (true) or releases (false) the bump button under a memory fader.</summary>
    Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken);

    /// <summary>REC MEM: stores the current look into the memory.</summary>
    Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken);

    /// <summary>EDIT MEM: replaces the memory's channel levels (index 0 = channel 1); all zero deletes the memory.</summary>
    Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken);

    /// <summary>Reads the memory's channel levels and label.</summary>
    Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken);

    /// <summary>Switches independent 1 or 2 (IND 1 / IND 2) on or off.</summary>
    Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken);

    /// <summary>CLEAR pressed twice: every channel fader to zero.</summary>
    Task ClearChannelLevelsAsync(CancellationToken cancellationToken);

    /// <summary>CLEAR pressed three times: every memory fader to zero.</summary>
    Task ClearMemoryLevelsAsync(CancellationToken cancellationToken);
}
