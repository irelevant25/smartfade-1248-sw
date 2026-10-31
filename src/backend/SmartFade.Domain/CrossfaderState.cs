namespace SmartFade.Domain;

/// <summary>
/// The console's main playback as it reports it in PlaybackInfo record 5: <see cref="Step"/> is the
/// 0-based step position in bytes 0-1 (read as the Next step's index, hypothesis H76), <see cref="Live"/>
/// is byte 2 and <see cref="Next"/> byte 3 (0..255). The pair sums to 255 at rest and in timed fades
/// (GO) and moves independently under the manual crossfaders, where A follows Live and B follows Next
/// (F101 as limited, F130 OBSERVED in E8b; H80 for the A / B assignment). Display only: a reading of
/// what the console sends, never a command.
/// </summary>
public sealed record CrossfaderState(int Step, byte Live, byte Next);
