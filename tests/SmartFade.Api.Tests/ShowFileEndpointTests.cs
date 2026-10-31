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
        steps[0] = steps[0] with { UpTime = "3.5", DownDelay = "2", FollowOn = "1:30", MemoryPage = 11, MemoryFader = 2, MemoryNumber = 267 };
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
