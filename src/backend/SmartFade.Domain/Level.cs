namespace SmartFade.Domain;

/// <summary>
/// Levels are carried everywhere in the console's own unit: one byte, 0..255. The reading
/// "0xFF is 100 %" is OBSERVED against one show file for memory levels (F72: the bodies are
/// VERIFIED, the scale reading is OBSERVED once) and live levels read 0xFF at full with the
/// MASTER at full (F83). Percent is a display conversion. The console's own rounding when it
/// writes a show file is UNKNOWN (docs/formats/asc-format.md), so a round trip through percent
/// is not guaranteed to be exact.
/// </summary>
public static class Level
{
    public const byte Min = 0;
    public const byte Max = 255;

    public static int ToPercent(byte level) =>
        (int)Math.Round(level * 100.0 / Max, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Percent to raw with the ceiling rule that fitted all 53 non-zero memory levels of
    /// SHOW04.ASC (F72, OBSERVED once): raw = ceil(pct × 255 / 100), e.g. 67 % → 0xAB.
    /// </summary>
    public static byte FromPercent(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Percent must be 0..100.");
        }

        return (byte)Math.Ceiling(percent * Max / 100.0);
    }

    public static byte Clamp(int value) => (byte)Math.Clamp(value, Min, Max);

    public static bool IsValid(int value) => value is >= Min and <= Max;
}
