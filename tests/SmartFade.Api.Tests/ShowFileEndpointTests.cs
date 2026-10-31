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
