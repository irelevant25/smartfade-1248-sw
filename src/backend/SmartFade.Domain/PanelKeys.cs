namespace SmartFade.Domain;

/// <summary>How a key's LED is lit: off, dimmed (the console's "has content" / "selectable" level) or at full.</summary>
public enum KeyLight
{
    Off,
    Dim,
    Full,
}

/// <summary>
/// One key LED as the console reports it: the brightness of its first blink phase and whether the
/// two phases differ (the console blinks a key that "can be selected for the current function",
/// manual v3.0 printed p.11; the LedStatus object carries both phases, F85, F116).
/// </summary>
public readonly record struct KeyLed(KeyLight Light, bool Blinking)
{
    public static readonly KeyLed Off = new(KeyLight.Off, false);
}

/// <summary>
/// The LEDs of the keys around the faders that a real console reports and the UI mirrors: STACK
/// and CLEAR of the crossfader section and the record keys COPY, REC SEQ, MAGIC, REC MEM and
/// EDIT MEM. Which LED block belongs to which key is INFERRED from the E8 session (hypothesis H72:
/// each block changed only in its own function or with its key); the patterns themselves are
/// OBSERVED (F116, F117). SNAPSHOT and RATE have no identified block and are not reported.
/// </summary>
public sealed record PanelKeys(KeyLed Stack, KeyLed Clear, KeyLed Copy, KeyLed RecSeq, KeyLed Magic, KeyLed RecMem, KeyLed EditMem);
