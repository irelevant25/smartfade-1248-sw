namespace SmartFade.Protocol;

/// <summary>
/// Object ids of the HSP protocol. The names come from the 42-entry name table in
/// sfEngine.dll (docs/protocol/smartsoft-analysis.md S11.6, INFERRED); the id → name mapping
/// is VERIFIED for the ids seen in both captures (F67) and OBSERVED for InputLevels (E3).
/// Names 24–35 come from S11.14.6 (Ghidra, same table). `MidiPatch` is the table's `MIDIPatch`
/// in .NET casing. Ids 24–35 and 1, 6, 7, 10, 11, 16, 18, 20, 42 have never been seen on the wire.
/// </summary>
[Evidence(EvidenceStatus.Inferred, "S11.6; F67 for the ids seen")]
public enum HspObjectId
{
    Memory = 0,
    Palette = 1,
    Sequence = 2,
    SequenceMain = 3,
    StepContent = 4,
    Patch = 5,
    Device = 6,
    Group = 7,
    Page = 8,
    ChannelText = 9,
    Effects = 10,
    MidiPatch = 11,
    Independents = 12,
    OutputLevels = 13,
    FaderInfo = 14,
    ChannelLevels = 15,
    OutputPalettes = 16,
    PlaybackInfo = 17,
    BlindLevels = 18,
    InputLevels = 19,
    ConsoleEvent = 20,
    DisplayContent = 21,
    LedStatus = 22,
    ConsoleStatus = 23,
    DeviceStatus = 24,
    ChangedStatus = 25,
    Personality = 26,
    PersonalityLibraryReset = 27,
    PersonalityUpdate = 28,
    PatchDevice = 29,
    UnpatchDevice = 30,
    RepatchDevice = 31,
    InsertStep = 32,
    InsertPointQStep = 33,
    DeleteStep = 34,
    MoveStep = 35,
    FileName = 36,
    ConsoleVersion = 37,
    SystemSizes = 38,
    System = 39,
    DirtyObjects = 40,
    SystemSettings = 41,
}

public static class HspObjectIds
{
    public const int TableSize = 42;

    public static string Name(int id) =>
        Enum.IsDefined(typeof(HspObjectId), id) ? ((HspObjectId)id).ToString() : $"Id{id}";
}
