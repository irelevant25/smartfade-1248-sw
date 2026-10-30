namespace SmartFade.Domain;

/// <summary>
/// One of the two independents. The console sends four bytes per independent before its
/// label (`00 FF 00 01` matched `$INDEPENDENT n 0 255 0 1`, F76); the meaning of the four
/// fields is UNKNOWN (H44), so they are kept raw.
/// </summary>
public sealed record Independent(int Number, byte A, byte B, byte C, byte D, Label Label);
