using SmartFade.Api.Contracts;
using SmartFade.Communication.Capture;
using SmartFade.Devices.Usb;
using SmartFade.Domain;
using SmartFade.Protocol;
using SmartFade.ShowFile.Asc;

namespace SmartFade.Api.Tests;

/// <summary>
/// The console's show from the download of its start-up pair (SC-4 clause 4) as the editor's DTO: E2's download (the console's
/// own objects, JSONL seq 191 … 820) through the device's <see cref="ConsoleShowAssembler"/> and <see cref="ConsoleShowMapper"/>,
/// checked against the values the E2 cross-check found in SHOW04.ASC (docs/formats/asc-format.md, F70–F75) and, when the
/// owner's git-ignored SD backup is beside the checkout, field by field against SHOW04.ASC itself, the console's save of that show.
/// </summary>
public class ConsoleShowMapperTests
{
    [Fact]
    public void Maps_E2s_download_to_the_show_the_console_saved_as_SHOW04()
    {
        var dto = ConsoleShowMapper.ToDto(ShowFromE2());

        Assert.Equal("SHOW04.ASC (console)", dto.Name);
        Assert.Equal(512, dto.Patch.Count);
        Assert.All(dto.Patch, entry => Assert.Null(entry.Level));         // no level in the Patch object (F70)
        Assert.Equal(50, dto.Patch[46].Channel);                          // DMX 47 → channel 50
        Assert.Equal(24, dto.Patch[96].Channel);                          // DMX 97 → channel 24
        Assert.Equal(16, dto.Memories.Count);
        var opona = dto.Memories[0];
        Assert.Equal((1, 1, 1, "OPONA"), (opona.Number, opona.Page, opona.Fader, opona.Label.Line1));
        Assert.Equal(new[] { new ChannelPercentDto(7, 100), new ChannelPercentDto(8, 100) }, opona.Channels);

        // `CUE 8.0` / `$$MEMORY 0 4` / `UP 5.0` / `DOWN 0.5` / `FOLLOWON 1:30`; `CUE 10.1` … `FOLLOWON 1.0`
        var stack = dto.MainStack!;
        Assert.Equal((48, 2, 0, 256, 256, true), (stack.Index, stack.B, stack.C, stack.D, stack.E, stack.IsMainStack));
        Assert.Equal(19, stack.Steps.Count);
        var eight = stack.Steps[7];
        Assert.Equal(("8.0", 0, 4, 5, 1, "5.0", "0.5", "1:30"), (eight.Number, eight.MemoryPage, eight.MemoryFader, eight.MemoryNumber, eight.CueParams, eight.UpTime, eight.DownTime, eight.FollowOn));
        Assert.Equal(("10.1", "0", "0", "1.0"), (stack.Steps[9].Number, stack.Steps[9].UpTime, stack.Steps[9].DownTime, stack.Steps[9].FollowOn));
        Assert.Equal(string.Empty, eight.UpDelay);                        // not in the download

        // KOTLIK: `$SEQUENCE 0 0 0 256 256`, its steps numbered by their memories 265.0 … 271.0 (F75)
        var kotlik = Assert.Single(dto.Sequences);
        Assert.Equal((0, "KOTLIK"), (kotlik.Index, kotlik.Label!.Line1));
        Assert.Equal(Enumerable.Range(265, 7).Select(n => $"{n}.0"), kotlik.Steps.Select(s => s.Number));

        Assert.Equal(2, dto.Independents.Count);
        Assert.Equal((0, 255, 0, 1), (dto.Independents[0].A, dto.Independents[0].B, dto.Independents[0].C, dto.Independents[0].D));
        Assert.Equal(96, dto.Channels.Count);
        Assert.Equal(12, dto.Pages.Count);
        Assert.Empty(dto.Settings);
        Assert.Equal((string.Empty, string.Empty, string.Empty, string.Empty), (dto.Manufacturer, dto.Console, dto.SoftwareVersion, dto.Ident));
        Assert.Contains(dto.Warnings, w => w.Message.Contains("no file header, no step delays, no proportional patch levels and no SET lines", StringComparison.Ordinal));

        // the render refuses it as it stands: no level, no delay
        Assert.NotNull(ShowFileRules.Check(dto));
    }

    [Fact]
    public void Matches_SHOW04_field_by_field_when_the_owners_SD_backup_is_beside_the_checkout()
    {
        var path = Path.Combine(RepositoryRoot, "smartfade-sd-card-backup", "SHOW04.ASC");
        if (!File.Exists(path))
        {
            return;   // the backup is git-ignored; the test above holds the documented values
        }

        var bytes = File.ReadAllBytes(path);
        var file = ShowFileMapper.ToDto("SHOW04.ASC", bytes.Length, AscReader.Read(bytes));
        var console = ConsoleShowMapper.ToDto(ShowFromE2());

        Assert.Equal(file.Memories.Select(m => (m.Number, Channels(m.Channels), Label(m.Label))), console.Memories.Select(m => (m.Number, Channels(m.Channels), Label(m.Label))));
        Assert.Equal(file.Patch.Select(p => (p.Dmx, p.Channel)), console.Patch.Select(p => (p.Dmx, p.Channel)));
        Assert.Equal(Sequence(file.MainStack!), Sequence(console.MainStack!));
        Assert.Equal(file.Sequences.Select(Sequence), console.Sequences.Select(Sequence));
        Assert.Equal(file.Independents.Select(i => (i.Number, i.A, i.B, i.C, i.D, Label(i.Label))), console.Independents.Select(i => (i.Number, i.A, i.B, i.C, i.D, Label(i.Label))));
        Assert.Equal(file.Channels.Select(c => (c.Number, Label(c.Label))), console.Channels.Select(c => (c.Number, Label(c.Label))));
        Assert.Equal(file.Pages.Select(p => (p.Number, Label(p.Label))), console.Pages.Select(p => (p.Number, Label(p.Label))));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(3, "0.3")]
    [InlineData(5, "0.5")]
    [InlineData(50, "5.0")]
    [InlineData(49, "4.9")]
    [InlineData(100, "10")]
    [InlineData(580, "58")]
    [InlineData(900, "1:30")]
    [InlineData(6000, "10:00")]
    public void Writes_times_in_the_forms_the_consoles_files_use(int tenths, string text)
    {
        var warnings = new List<string>();

        Assert.Equal(text, ConsoleShowMapper.Time(tenths, warnings));
        Assert.Empty(warnings);
    }

    [Fact]
    public void A_time_or_a_level_no_file_shows_is_kept_and_named()
    {
        var warnings = new List<string>();

        Assert.Equal("10.5", ConsoleShowMapper.Time(105, warnings));
        Assert.Single(warnings);
        // every level stored from a whole percent maps back to it (raw = ceil(pct × 2.55), 53 of 53, F72)
        Assert.All(Enumerable.Range(1, 100), pct => Assert.Equal(pct, ConsoleShowMapper.Percent((byte)Math.Ceiling(pct * 255 / 100.0))));
    }

    private static ConsoleShow ShowFromE2()
    {
        var capture = CaptureFile.Load(Path.Combine(RepositoryRoot, "docs", "protocol", "captures", "2026-10-28-e2-smartsoft-connect.jsonl"));
        var decoder = new HspSessionDecoder();
        var assembler = new ConsoleShowAssembler(ConsoleCapabilities.SmartFade1248);
        foreach (var frame in capture.BulkFrames.Where(f => f.Seq >= 191 && f.Seq <= 820))
        {
            var direction = frame.Direction == CaptureDirection.Out ? HspDirection.ToConsole : HspDirection.FromConsole;
            if (decoder.Feed(direction, frame.Data) is ObjectReceived received)
            {
                assembler.Apply(received);
            }
        }

        return assembler.Build();
    }

    private static string Channels(IReadOnlyList<ChannelPercentDto> channels) => string.Join(' ', channels.Select(c => $"{c.Channel}/{c.Percent}"));

    private static string Label(LabelDto label) => $"{label.Text}|{label.Line1}|{label.Line2}|{label.Line3}";

    private static object Sequence(SequenceDto sequence) => (
        sequence.Index,
        sequence.B,
        sequence.C,
        sequence.D,
        sequence.E,
        sequence.Label is { } label ? Label(label) : null,
        string.Join(
            " / ",
            sequence.Steps.Select(s => $"{s.Number} {s.MemoryPage} {s.MemoryFader} {s.MemoryNumber} {s.CueParams} {s.UpTime} {s.DownTime} {s.FollowOn} {Channels(s.Channels)}")));

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SmartFade.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("SmartFade.sln not found above " + AppContext.BaseDirectory);
    }
}
