namespace SmartFade.ShowFile.Asc.Tests;

/// <summary>
/// The committed sample (the owner's production show "Ostrov", in smartsoft-source-materials, read only)
/// and, when the checkout has the owner's SD-card backups beside it, every show in that folder: each is
/// read and rendered again, and the bytes must match. A mismatch means a format rule in the writer is
/// wrong, never that the file is.
/// </summary>
public class RoundTripTests
{
    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string OstrovSample => Path.Combine(RepositoryRoot, "smartsoft-source-materials", "example lighting output file - Ostrov - 16.09.2026.asc");

    [Fact]
    public void Reads_the_committed_sample_completely()
    {
        var read = AscReader.Read(OstrovSample);
        var show = read.Show;

        Assert.Empty(read.Warnings);
        Assert.Empty(show.Unknown);
        Assert.Equal(AscLineEnding.CrLf, show.LineEnding);
        Assert.Equal("AVAB", show.Manufacturer);
        Assert.Equal("SMARTFADE", show.Console);
        Assert.Equal("3.0.2 43", show.SoftwareVersion);
        Assert.Equal("3.0", show.Ident);
        Assert.True(show.ClearAll);
        Assert.NotNull(show.MainStack);
        Assert.Equal((48, 2, 0, 256, 256), (show.MainStack!.A, show.MainStack.B, show.MainStack.C, show.MainStack.D, show.MainStack.E));
        Assert.Equal(19, show.MainStack.Steps.Count);                 // 19 CUE records (asc-format.md, the E2 cross-check)
        Assert.Equal("1.0", show.MainStack.Steps[0].Number);
        Assert.Equal((0, 0), show.MainStack.Steps[0].Memory);
        Assert.Equal(("5.0", "0"), (show.MainStack.Steps[0].UpTime, show.MainStack.Steps[0].UpDelay));
        Assert.Equal(new[] { new ChannelLevelPercent(7, 100), new ChannelLevelPercent(8, 100) }, show.MainStack.Steps[0].Channels);
        var sequence = Assert.Single(show.Sequences);
        Assert.Equal("KOTLIK", sequence.Label?.Line1);
        Assert.Equal(8, sequence.Steps.Count);
        Assert.Equal("265.0", sequence.Steps[0].Number);
        Assert.Equal(16, show.Memories.Count);
        Assert.Equal("OPONA", show.Memories[0].Label.Line1);
        Assert.Equal(512, show.Patch.Count);
        Assert.Equal(new PatchEntry(98, 50, 100), show.Patch[97]);     // DMX 98 -> channel 50 in the Ostrov show (asc-format.md, PATCH)
        Assert.Equal(new PatchEntry(1, 1, 100), show.Patch[0]);
        Assert.Equal(2, show.Independents.Count);
        Assert.Equal(96, show.Channels.Count);
        Assert.Equal(12, show.Pages.Count);
        Assert.Equal(6, show.Settings.Count);
        Assert.Equal(new Setting("MSCDeviceId", "127"), show.Settings[4]);
    }

    [Fact]
    public void Renders_the_committed_sample_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(OstrovSample);
        var read = AscReader.Read(bytes);

        Assert.Equal(bytes, AscWriter.RenderVerbatim(read));
        AssertSameBytes(bytes, AscWriter.Render(read.Show), OstrovSample);
    }

    public static IEnumerable<object[]> LocalShows()
    {
        var folder = Path.Combine(RepositoryRoot, "smartfade-sd-card-backup");
        var files = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Where(f => f.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        if (files.Length == 0)
        {
            yield return new object[] { string.Empty };   // no local corpus on this machine: the theory has nothing to check
            yield break;
        }

        foreach (var file in files)
        {
            yield return new object[] { file };
        }
    }

    /// <summary>The owner's SD-card backups are not in git (proprietary show data); on his machine every one of them must round-trip.</summary>
    [Theory]
    [MemberData(nameof(LocalShows))]
    public void Renders_every_local_show_byte_for_byte(string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var read = AscReader.Read(bytes);

        Assert.Empty(read.Show.Unknown);
        Assert.Equal(bytes, AscWriter.RenderVerbatim(read));
        AssertSameBytes(bytes, AscWriter.Render(read.Show), path);
    }

    [Fact]
    public void Step_lines_use_the_engine_s_fixed_width_columns()
    {
        var step = new Step("1.0", (0, 0), 1, "5.0", "0", "5.0", "0", "0", new[] { new ChannelLevelPercent(7, 100), new ChannelLevelPercent(8, 100) }, null);

        Assert.Equal(
            new[] { "CUE  1.0", "$$MEMORY 0 0", "$$CUEPARAMS 1", "UP 5.0   0    ", "DOWN 5.0   0    ", "FOLLOWON 0    ", "CHAN 7/100 8/100 " },
            AscWriter.StepLines(step, "CUE"));
        Assert.Equal("$CUE 265.0", AscWriter.StepLines(step with { Number = "265.0" }, "$CUE").First());
        Assert.Equal("FOLLOWON 1:30 ", AscWriter.StepLines(step with { FollowOn = "1:30" }, "CUE").ElementAt(5));
    }

    private static void AssertSameBytes(byte[] expected, byte[] actual, string path)
    {
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return;
        }

        var first = 0;
        while (first < expected.Length && first < actual.Length && expected[first] == actual[first])
        {
            first++;
        }

        var line = expected.AsSpan(0, Math.Min(first, expected.Length)).ToArray().Count(b => b == (byte)'\n') + 1;
        Assert.Fail($"{Path.GetFileName(path)}: rendered bytes differ from the file at byte {first} (line {line}); expected {expected.Length} bytes, rendered {actual.Length}.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartFade.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("SmartFade.sln not found above " + AppContext.BaseDirectory);
    }
}
