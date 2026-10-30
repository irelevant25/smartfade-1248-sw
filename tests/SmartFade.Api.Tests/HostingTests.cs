using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartFade.Api.Contracts;
using SmartFade.Domain;

namespace SmartFade.Api.Tests;

public class HostingTests
{
    [Fact]
    public void An_unknown_device_mode_refuses_to_start()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Hardware"));
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("not available", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_api_has_no_reference_path_to_the_protocol_or_communication_projects()
    {
        var referenced = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("SmartFade.Protocol", referenced);
        Assert.DoesNotContain("SmartFade.Communication", referenced);
    }

    [Fact]
    public async Task Requests_from_a_foreign_origin_are_refused_before_they_execute()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/console/connect");
        request.Headers.Add("Origin", "http://evil.example");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var status = await client.GetAsync("/api/status");
        Assert.Contains("\"status\":\"disconnected\"", await status.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void Wire_names_are_pinned_independently_of_enum_member_names()
    {
        Assert.Equal("disconnected", DtoMapper.StatusName(ConnectionStatus.Disconnected));
        Assert.Equal("connecting", DtoMapper.StatusName(ConnectionStatus.Connecting));
        Assert.Equal("connected", DtoMapper.StatusName(ConnectionStatus.Connected));
        Assert.Equal("error", DtoMapper.StatusName(ConnectionStatus.Error));
        Assert.Equal("simulated", DtoMapper.ModeName(DeviceMode.Simulated));
        Assert.Equal(new[] { "setChannelLevel", "setMaster", "setBlackout" }, DtoMapper.CommandNames(DeviceCommands.SetChannelLevel | DeviceCommands.SetMaster | DeviceCommands.SetBlackout));
        Assert.Equal(
            new[] { "setChannelLevel", "setMaster", "setBlackout", "setBumps", "setSolo", "bumpChannel", "setMemoryLevel", "bumpMemory", "recordMemory", "storeMemory", "setIndependent", "clearChannels", "clearMemories" },
            DtoMapper.CommandNames(DeviceCommands.All));
        Assert.Empty(DtoMapper.CommandNames(DeviceCommands.None));
        Assert.Equal("invalid_channel", DtoMapper.ToErrorDto(new ArgumentOutOfRangeException("channel")).Code);
        Assert.Equal("invalid_level", DtoMapper.ToErrorDto(new ArgumentOutOfRangeException("level")).Code);
        Assert.Equal("invalid_memory", DtoMapper.ToErrorDto(new ArgumentOutOfRangeException("page")).Code);
        Assert.Equal("invalid_memory", DtoMapper.ToErrorDto(new ArgumentOutOfRangeException("fader")).Code);
        Assert.Equal("invalid_argument", DtoMapper.ToErrorDto(new ArgumentException("bad", "levels")).Code);
        Assert.Equal("not_connected", DtoMapper.ToErrorDto(new SmartFade.Application.NotConnectedException()).Code);
    }
}
