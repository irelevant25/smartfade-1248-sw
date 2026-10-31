using System.Globalization;

namespace SmartFade.Api.Contracts;

/// <summary>
/// What an edited show file must satisfy before it is rendered: the ranges the 1248's own files use
/// (docs/formats/asc-format.md). 512 DMX addresses, 1..512 in order; a channel is 0 (unpatched) or
/// 1..96 (the file lists 96 `$CHANNEL` entries; 1..48 are the console's faders, 49 and 50 the
/// independents by hypothesis H11); a level is 0..100 percent. Memory and step edits are not
/// validated here yet (E10 steps 2 and 3 add them with their rules).
/// </summary>
public static class ShowFileRules
{
    public const int DmxAddresses = 512;
    public const int MaxChannel = 96;

    public static string? Check(ShowFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Patch.Count != DmxAddresses)
        {
            return $"The patch must list all {DmxAddresses} DMX addresses; it lists {dto.Patch.Count}.";
        }

        for (var i = 0; i < dto.Patch.Count; i++)
        {
            var entry = dto.Patch[i];
            if (entry.Dmx != i + 1)
            {
                return $"The patch must list DMX addresses 1..{DmxAddresses} in order; entry {i + 1} is DMX {entry.Dmx}.";
            }

            if (entry.Channel < 0 || entry.Channel > MaxChannel)
            {
                return $"DMX {entry.Dmx}: channel {entry.Channel} is outside 0..{MaxChannel} (0 = unpatched).";
            }

            if (entry.Level < 0 || entry.Level > 100)
            {
                return $"DMX {entry.Dmx}: level {entry.Level} % is outside 0..100.";
            }
        }

        return null;
    }

    /// <summary>The name offered for the download: never the original, so a save can never replace it by accident.</summary>
    public static string EditedFileName(string original)
    {
        var name = string.IsNullOrWhiteSpace(original) ? "show.asc" : Path.GetFileName(original.Trim());
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".asc";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{stem}-edited{extension}");
    }
}
