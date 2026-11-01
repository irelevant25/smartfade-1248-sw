namespace SmartFade.Domain;

/// <summary>
/// What the console's 24 physical faders control: channels 1-24, channels 25-48 or the memories
/// of the selected page (the FADERS keys 1-24 / 25-48 / MEMS, manual F7). A real console reports it
/// in ConsoleStatus (body[0] = memory mode, body[4] = bank 25-48; F87, F98); the simulator, which plays
/// every bank at once, reports none.
/// </summary>
public enum FaderBank
{
    Channels1To24,
    Channels25To48,
    Memories,
}
