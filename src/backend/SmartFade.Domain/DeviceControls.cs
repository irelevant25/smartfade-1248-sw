namespace SmartFade.Domain;

/// <summary>
/// Names of the individual controls a device may accept commands for, as the contract carries them
/// (docs/api.md `writableControls`). A device that accepts a command for every control of its kind
/// reports no list (null); a device whose clearance covers only some controls (the real console under
/// safety checks SC-2 and SC-5: the faders and bumps of every bank, MASTER, BUMPS and some keys) names exactly those,
/// and the UI enables nothing else. The names are data for the UI; no byte depends on them.
/// </summary>
public static class DeviceControls
{
    public const string Master = "master";
    public const string Bumps = "bumps";
    public const string Solo = "solo";
    public const string Blackout = "blackout";
    public const string Go = "go";
    public const string FadersMems = "fadersMems";
    public const string Faders1To24 = "faders1To24";
    public const string Faders25To48 = "faders25To48";
    public const string RecMem = "recMem";
    public const string EditMem = "editMem";
    public const string Copy = "copy";
    public const string Clear = "clear";

    /// <summary>A channel fader, 1..48.</summary>
    public static string ChannelFader(int channel) => $"channelFader:{channel}";

    /// <summary>The bump button under a channel fader, 1..48.</summary>
    public static string ChannelBump(int channel) => $"channelBump:{channel}";

    /// <summary>A memory fader by memory number, 1..288 (24 × (page − 1) + fader).</summary>
    public static string MemoryFader(int memoryNumber) => $"memoryFader:{memoryNumber}";

    /// <summary>The bump button under a memory fader, by memory number.</summary>
    public static string MemoryBump(int memoryNumber) => $"memoryBump:{memoryNumber}";

    /// <summary>IND 1 or IND 2.</summary>
    public static string Independent(int number) => $"independent:{number}";
}
