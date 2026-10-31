using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartFade.Api.Contracts;

namespace SmartFade.Api.Tests;

/// <summary>The show-file page's endpoint: the committed Ostrov sample goes in as bytes and comes back as the DTO; nothing touches the console.</summary>
public class ShowFileEndpointTests
{
    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string OstrovSample => Path.Combine(RepositoryRoot, "smartsoft-source-materials", "example lighting output file - Ostrov - 16.09.2026.asc");

    [Fact]
    public async Task Parses_an_uploaded_show_file()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        var bytes = await File.ReadAllBytesAsync(OstrovSample);

        using var content = new ByteArrayContent(bytes);
        var response = await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ShowFileDto>();
        Assert.NotNull(dto);
        Assert.Equal("Ostrov.asc", dto.Name);
        Assert.Equal(bytes.Length, dto.Bytes);
        Assert.Equal("crlf", dto.LineEnding);
        Assert.Equal("SMARTFADE", dto.Console);
        Assert.Equal(19, dto.MainStack?.Steps.Count);
        Assert.Equal(1, dto.MainStack?.Steps[0].MemoryNumber);
        Assert.Equal(4, dto.MainStack?.Steps[2].MemoryNumber);       // CUE 3.0 has $$MEMORY 0 3 in the sample: memory 4 of page 1
        var sequence = Assert.Single(dto.Sequences);
        Assert.Equal(8, sequence.Steps.Count);
        Assert.Equal(265, sequence.Steps[0].MemoryNumber);           // $$MEMORY 11 0: page 12 fader 1
        Assert.Equal(16, dto.Memories.Count);
        Assert.Equal((12, 1), (dto.Memories[9].Page, dto.Memories[9].Fader));   // GROUP 265
        Assert.Equal(512, dto.Patch.Count);
        Assert.Equal(96, dto.Channels.Count);
        Assert.Equal(6, dto.Settings.Count);
        Assert.Equal(0, dto.UnknownLines);
        Assert.Empty(dto.Warnings);
    }

    [Fact]
    public async Task Rejects_an_empty_body()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();

        using var content = new ByteArrayContent(Array.Empty<byte>());
        var response = await client.PostAsync("/api/showfile/parse", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Renders_the_parsed_file_back_byte_for_byte_and_names_the_download_as_a_new_file()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        var bytes = await File.ReadAllBytesAsync(OstrovSample);
        using var upload = new ByteArrayContent(bytes);
        var dto = await (await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", upload)).Content.ReadFromJsonAsync<ShowFileDto>();

        var response = await client.PostAsJsonAsync("/api/showfile/render", dto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ostrov-edited.asc", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());   // the DTO carries everything the writer needs
    }

    [Fact]
    public async Task Renders_an_edited_patch_and_refuses_a_channel_outside_the_range()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        using var upload = new ByteArrayContent(await File.ReadAllBytesAsync(OstrovSample));
        var dto = (await (await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", upload)).Content.ReadFromJsonAsync<ShowFileDto>())!;

        var patch = dto.Patch.ToList();
        patch[0] = patch[0] with { Channel = 12 };   // DMX 1 -> channel 12
        var edited = await client.PostAsJsonAsync("/api/showfile/render", dto with { Patch = patch });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var text = System.Text.Encoding.Latin1.GetString(await edited.Content.ReadAsByteArrayAsync());
        Assert.Contains("PATCH 1 1/12/100 2/2/100", text, StringComparison.Ordinal);

        patch[0] = patch[0] with { Channel = 97 };
        var refused = await client.PostAsJsonAsync("/api/showfile/render", dto with { Patch = patch });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var error = await refused.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Equal("invalid_argument", error?.Code);
        Assert.Contains("outside 0..96", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renders_edited_step_times_and_memory_and_refuses_a_time_the_console_never_writes()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        using var upload = new ByteArrayContent(await File.ReadAllBytesAsync(OstrovSample));
        var dto = (await (await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", upload)).Content.ReadFromJsonAsync<ShowFileDto>())!;

        var steps = dto.MainStack!.Steps.ToList();
        steps[0] = steps[0] with { UpTime = "3.5", DownDelay = "2", FollowOn = "1:30", MemoryPage = 11, MemoryFader = 2, MemoryNumber = 267, Channels = dto.Memories.Single(m => m.Number == 267).Channels };   // a memory step repeats its memory's channels
        var edited = await client.PostAsJsonAsync("/api/showfile/render", dto with { MainStack = dto.MainStack with { Steps = steps } });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var text = System.Text.Encoding.Latin1.GetString(await edited.Content.ReadAsByteArrayAsync());
        var cue = text.IndexOf("CUE  1.0", StringComparison.Ordinal);
        var next = text.IndexOf("CUE  2.0", StringComparison.Ordinal);
        var step = text[cue..next];
        Assert.Contains("$$MEMORY 11 2", step, StringComparison.Ordinal);
        Assert.Contains("UP 3.5   0    ", step, StringComparison.Ordinal);
        Assert.Contains("DOWN 5.0   2    ", step, StringComparison.Ordinal);
        Assert.Contains("FOLLOWON 1:30 ", step, StringComparison.Ordinal);

        steps[0] = steps[0] with { UpTime = "3,5" };
        var refused = await client.PostAsJsonAsync("/api/showfile/render", dto with { MainStack = dto.MainStack with { Steps = steps } });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("up time '3,5'", (await refused.Content.ReadFromJsonAsync<ErrorDto>())?.Message, StringComparison.Ordinal);
    }


    [Fact]
    public async Task Renders_an_inserted_cue_and_sequence_step_and_refuses_shapes_the_console_never_wrote()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        using var upload = new ByteArrayContent(await File.ReadAllBytesAsync(OstrovSample));
        var dto = (await (await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", upload)).Content.ReadFromJsonAsync<ShowFileDto>())!;
        var memory = dto.Memories.Single(m => m.Number == 265);

        // a cue 1.1 after cue 1.0 referencing memory 265 (page 12, fader 1), with the memory's channels repeated
        var template = dto.MainStack!.Steps[0];
        var cue = template with { Number = "1.1", MemoryPage = 11, MemoryFader = 0, MemoryNumber = 265, CueParams = 1, Channels = memory.Channels, Label = null };
        var steps = dto.MainStack.Steps.ToList();
        steps.Insert(1, cue);
        var sequenceSteps = dto.Sequences[0].Steps.ToList();
        sequenceSteps.Add(sequenceSteps[0] with { Number = "265.0", MemoryPage = 11, MemoryFader = 0, MemoryNumber = 265, Channels = memory.Channels });
        var sequences = dto.Sequences.ToList();
        sequences[0] = sequences[0] with { Steps = sequenceSteps };
        var edited = await client.PostAsJsonAsync("/api/showfile/render", dto with { MainStack = dto.MainStack with { Steps = steps }, Sequences = sequences });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var text = System.Text.Encoding.Latin1.GetString(await edited.Content.ReadAsByteArrayAsync());
        var inserted = text[text.IndexOf("CUE  1.1", StringComparison.Ordinal)..text.IndexOf("CUE  2.0", StringComparison.Ordinal)];
        Assert.Contains("$$MEMORY 11 0", inserted, StringComparison.Ordinal);
        Assert.Contains("$$CUEPARAMS 1", inserted, StringComparison.Ordinal);
        Assert.Contains("CHAN " + string.Join(' ', memory.Channels.Select(c => $"{c.Channel}/{c.Percent}")), inserted, StringComparison.Ordinal);
        Assert.Equal(dto.Sequences[0].Steps.Count + 1, System.Text.RegularExpressions.Regex.Matches(text, @"^\$CUE ", System.Text.RegularExpressions.RegexOptions.Multiline).Count);

        async Task<string> Refusal(ShowFileDto body)
        {
            var refused = await client.PostAsJsonAsync("/api/showfile/render", body);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            return (await refused.Content.ReadFromJsonAsync<ErrorDto>())!.Message;
        }

        // the inserted cue numbered below its predecessor
        steps[1] = cue with { Number = "0.5" };
        Assert.Contains("rise through the stack", await Refusal(dto with { MainStack = dto.MainStack with { Steps = steps } }), StringComparison.Ordinal);

        // a memory step whose channels do not repeat its memory's
        steps[1] = cue with { Channels = new[] { new ChannelPercentDto(1, 50) } };
        Assert.Contains("must repeat memory 265", await Refusal(dto with { MainStack = dto.MainStack with { Steps = steps } }), StringComparison.Ordinal);

        // a sequence step whose number is not its memory's
        sequenceSteps[^1] = sequenceSteps[^1] with { Number = "9.0" };
        sequences[0] = sequences[0] with { Steps = sequenceSteps };
        Assert.Contains("numbered by its memory (265.0)", await Refusal(dto with { Sequences = sequences }), StringComparison.Ordinal);
    }


    [Fact]
    public async Task Renders_an_edited_memory_and_its_label_and_refuses_shapes_the_console_never_wrote()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();
        using var upload = new ByteArrayContent(await File.ReadAllBytesAsync(OstrovSample));
        var dto = (await (await client.PostAsync("/api/showfile/parse?name=Ostrov.asc", upload)).Content.ReadFromJsonAsync<ShowFileDto>())!;

        // memory 265 (page 12, fader 1): new channels and a label; the sequence steps that use it repeat its channels
        var memories = dto.Memories.ToList();
        var index = memories.FindIndex(m => m.Number == 265);
        var channels = new[] { new ChannelPercentDto(7, 50), new ChannelPercentDto(45, 100) };
        memories[index] = memories[index] with { Channels = channels, Label = new LabelDto("KOTOL", "KOTOL", "", "") };
        var sequences = dto.Sequences.ToList();
        sequences[0] = sequences[0] with { Steps = sequences[0].Steps.Select(s => s.MemoryNumber == 265 ? s with { Channels = channels } : s).ToArray() };
        var edited = await client.PostAsJsonAsync("/api/showfile/render", dto with { Memories = memories, Sequences = sequences });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var text = System.Text.Encoding.Latin1.GetString(await edited.Content.ReadAsByteArrayAsync());
        var group = text[text.IndexOf("GROUP 265", StringComparison.Ordinal)..text.IndexOf("GROUP 266", StringComparison.Ordinal)];
        Assert.Contains("CHAN 7/50 45/100", group, StringComparison.Ordinal);
        Assert.Contains("Text KOTOL", group, StringComparison.Ordinal);
        Assert.Contains("$$Text 1 KOTOL", group, StringComparison.Ordinal);
        var step = text[text.IndexOf("$CUE 265.0", StringComparison.Ordinal)..text.IndexOf("$CUE 266.0", StringComparison.Ordinal)];
        Assert.Contains("CHAN 7/50 45/100", step, StringComparison.Ordinal);

        async Task<string> Refusal(ShowFileDto body)
        {
            var refused = await client.PostAsJsonAsync("/api/showfile/render", body);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            return (await refused.Content.ReadFromJsonAsync<ErrorDto>())!.Message;
        }

        var withLabel = memories.ToList();
        withLabel[index] = memories[index] with { Label = new LabelDto("SEDEM7A", "SEDEM7A", "", "") };
        Assert.Contains("longer than 6", await Refusal(dto with { Memories = withLabel, Sequences = sequences }), StringComparison.Ordinal);
        withLabel[index] = memories[index] with { Label = new LabelDto("WRONG", "KOTOL", "", "") };
        Assert.Contains("does not follow from its lines", await Refusal(dto with { Memories = withLabel, Sequences = sequences }), StringComparison.Ordinal);
        withLabel[index] = memories[index] with { Channels = new[] { new ChannelPercentDto(49, 100) } };
        Assert.Contains("outside 1..48", await Refusal(dto with { Memories = withLabel, Sequences = sequences }), StringComparison.Ordinal);
        withLabel[index] = memories[index] with { Channels = new[] { new ChannelPercentDto(9, 100), new ChannelPercentDto(7, 50) } };
        Assert.Contains("must rise through the list", await Refusal(dto with { Memories = withLabel, Sequences = sequences }), StringComparison.Ordinal);
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
