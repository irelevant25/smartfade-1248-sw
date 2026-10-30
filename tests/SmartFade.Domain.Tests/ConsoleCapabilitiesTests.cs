using SmartFade.Domain;

namespace SmartFade.Domain.Tests;

public class ConsoleCapabilitiesTests
{
    [Fact]
    public void SmartFade1248_has_the_documented_limits()
    {
        var c = ConsoleCapabilities.SmartFade1248;
        Assert.Equal(48, c.Channels);
        Assert.Equal(24, c.PhysicalFaders);
        Assert.Equal(288, c.Memories);
        Assert.Equal(512, c.DmxAddresses);
        Assert.Equal(2, c.Independents);
        Assert.True(c.IsValidChannel(1));
        Assert.True(c.IsValidChannel(48));
        Assert.False(c.IsValidChannel(0));
        Assert.False(c.IsValidChannel(49));
    }

    [Fact]
    public void Memory_number_follows_the_observed_numbering()
    {
        // GROUP n = 24 × page + slot + 1 (H13 / H25, OBSERVED): page 0 slot 0 → 1, page 11 slot 0 → 265
        Assert.Equal(1, Memory.ToNumber(0, 0, 24));
        Assert.Equal(265, Memory.ToNumber(11, 0, 24));
    }

    [Fact]
    public void OneToOne_patch_maps_address_to_channel_up_to_the_channel_count()
    {
        var patch = Patch.OneToOne(ConsoleCapabilities.SmartFade1248);
        Assert.Equal(5, patch.ChannelAt(5));
        Assert.Equal(48, patch.ChannelAt(48));
        Assert.Equal(0, patch.ChannelAt(49));
        Assert.Equal(new[] { 7 }, patch.AddressesOf(7));
    }
}
