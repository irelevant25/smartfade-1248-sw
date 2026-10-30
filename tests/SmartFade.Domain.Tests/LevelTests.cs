using SmartFade.Domain;

namespace SmartFade.Domain.Tests;

public class LevelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 100)]
    [InlineData(171, 67)]   // 0xAB, the level SHOW04.ASC stores as 67 % (F72)
    [InlineData(128, 50)]
    public void ToPercent_matches_the_console_scale(int raw, int percent) =>
        Assert.Equal(percent, Level.ToPercent((byte)raw));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 255)]
    [InlineData(67, 171)]   // ceil(67 × 255 / 100) = 171 = 0xAB (F72)
    [InlineData(38, 97)]
    public void FromPercent_uses_the_ceiling_rule(int percent, int raw) =>
        Assert.Equal((byte)raw, Level.FromPercent(percent));

    [Fact]
    public void FromPercent_rejects_out_of_range() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Level.FromPercent(101));

    [Fact]
    public void Clamp_limits_to_a_byte()
    {
        Assert.Equal(255, Level.Clamp(999));
        Assert.Equal(0, Level.Clamp(-5));
    }
}
