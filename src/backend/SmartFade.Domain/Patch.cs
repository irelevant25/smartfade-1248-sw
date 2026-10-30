namespace SmartFade.Domain;

/// <summary>
/// DMX address → channel (1-based), 0 = unpatched. The console's Patch object holds exactly
/// this table (VERIFIED as "the console returns these bytes", F70); the proportional level of
/// the show file's PATCH lines is stored somewhere else (UNKNOWN).
/// </summary>
public sealed class Patch
{
    private readonly byte[] _channelByAddress;

    public Patch(IReadOnlyList<byte> channelByAddress)
    {
        ArgumentNullException.ThrowIfNull(channelByAddress);
        _channelByAddress = channelByAddress.ToArray();
    }

    public int DmxAddresses => _channelByAddress.Length;

    /// <summary>Channel patched to <paramref name="dmxAddress"/> (1-based), 0 when unpatched.</summary>
    public int ChannelAt(int dmxAddress)
    {
        if (dmxAddress < 1 || dmxAddress > _channelByAddress.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(dmxAddress));
        }

        return _channelByAddress[dmxAddress - 1];
    }

    public IEnumerable<int> AddressesOf(int channel)
    {
        for (var i = 0; i < _channelByAddress.Length; i++)
        {
            if (_channelByAddress[i] == channel)
            {
                yield return i + 1;
            }
        }
    }

    /// <summary>
    /// The documented default: "SmartFade defaults to a '1 to 1' patch for dimmers" (console
    /// manual v3.0, printed p.20 "The Default Patch"; p.18 "The default is set 1:1"), DOCUMENTED;
    /// the owner's show patches differently (F70), so this is only the simulator's starting point.
    /// </summary>
    public static Patch OneToOne(ConsoleCapabilities capabilities)
    {
        var table = new byte[capabilities.DmxAddresses];
        for (var address = 1; address <= capabilities.Channels && address <= table.Length; address++)
        {
            table[address - 1] = (byte)address;
        }

        return new Patch(table);
    }
}
