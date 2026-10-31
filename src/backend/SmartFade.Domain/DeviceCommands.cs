namespace SmartFade.Domain;

/// <summary>
/// Commands a device implementation accepts. The simulator accepts <see cref="All"/>; whether a
/// real 1248 accepts any PC-side command is UNKNOWN (H6 open), so a real device may well report
/// <see cref="None"/> and be observation-only.
/// </summary>
[Flags]
public enum DeviceCommands
{
    None = 0,
    SetChannelLevel = 1,
    SetMaster = 2,
    SetBlackout = 4,
    SetBumps = 8,
    SetSolo = 16,
    /// <summary>Press or release the bump button under a channel fader.</summary>
    BumpChannel = 32,
    /// <summary>Move a memory fader (playback level of one memory).</summary>
    SetMemoryLevel = 64,
    /// <summary>Press or release the bump button under a memory fader.</summary>
    BumpMemory = 128,
    /// <summary>REC MEM: store the current look into a memory.</summary>
    RecordMemory = 256,
    /// <summary>EDIT MEM: replace a memory's channel levels.</summary>
    StoreMemory = 512,
    /// <summary>IND 1 / IND 2 on or off.</summary>
    SetIndependent = 1024,
    /// <summary>CLEAR pressed twice: every channel fader to zero.</summary>
    ClearChannels = 2048,
    /// <summary>CLEAR pressed three times: every memory fader to zero.</summary>
    ClearMemories = 4096,
    /// <summary>GO: the next step of the stack (a key tap). Not in <see cref="All"/>: the simulator has no stack playback.</summary>
    PressGo = 8192,
    /// <summary>FADERS MEMS: the physical faders to memory mode (a key tap). Not in <see cref="All"/>: the simulator has no fader mode.</summary>
    PressFadersMems = 16384,
    /// <summary>FADERS 1-24: the physical faders to channel mode, bank 1-24 (a key tap). Not in <see cref="All"/>.</summary>
    PressFaders1To24 = 32768,
    /// <summary>Every command the simulator models. The three key taps above are outside it: a real console with live control (SC-2 class B1) reports them.</summary>
    All = SetChannelLevel | SetMaster | SetBlackout | SetBumps | SetSolo | BumpChannel | SetMemoryLevel | BumpMemory | RecordMemory | StoreMemory | SetIndependent | ClearChannels | ClearMemories,
}
