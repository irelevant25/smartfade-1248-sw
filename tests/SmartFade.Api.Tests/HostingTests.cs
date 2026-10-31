using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartFade.Api.Contracts;
using SmartFade.Communication;
using SmartFade.Devices.Usb;
using SmartFade.Domain;

namespace SmartFade.Api.Tests;

public class HostingTests
{
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    [Fact]
    public void An_unknown_device_mode_refuses_to_start()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Hardware"));
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("not available", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_default_mode_is_the_simulator_and_no_console_pipe_is_registered()
    {
        // Pinned, so that a Device__Mode left in the environment can never switch a test host to the console.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<StatusDto>("/api/status");
        Assert.NotNull(status);
        Assert.Equal("simulated", status.Mode);
        Assert.Null(factory.Services.GetService<Func<IHspPipe>>());
    }

    [Fact]
    public void Usb_mode_refuses_to_start_unless_confirmed_for_today()
    {
        using var unconfirmed = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Usb"));
        var ex = Assert.ThrowsAny<Exception>(() => unconfirmed.CreateClient());
        Assert.Contains("ConfirmDate", ex.ToString(), StringComparison.Ordinal);

        using var stale = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Device:Mode", "Usb");
            b.UseSetting("Device:Usb:ConfirmDate", "2026-01-01");
        });
        Assert.ThrowsAny<Exception>(() => stale.CreateClient());

        Assert.True(ServiceCollectionExtensions.IsConfirmedForToday("2026-10-31", new DateOnly(2026, 10, 31)));
        Assert.False(ServiceCollectionExtensions.IsConfirmedForToday("2026-10-30", new DateOnly(2026, 10, 31)));
        Assert.False(ServiceCollectionExtensions.IsConfirmedForToday(null, new DateOnly(2026, 10, 31)));
        Assert.False(ServiceCollectionExtensions.IsConfirmedForToday("today", new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public async Task Usb_mode_is_read_only_hardware_mode_and_a_missing_console_leaves_it_in_error()
    {
        // Two independent guards keep this test off real hardware: the pipe factory is replaced, and
        // the device path is one the WinUSB pipe refuses before CreateFile.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Device:Mode", "Usb");
            b.UseSetting("Device:Usb:ConfirmDate", Today);
            b.UseSetting("Device:Usb:DevicePath", @"\\?\usb#vid_0000&pid_0000&mi_09#test");
            b.ConfigureServices(services =>
                services.AddSingleton<Func<IHspPipe>>(() => throw new HspPipeException("test: no console")));
        });
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<StatusDto>("/api/status");
        Assert.NotNull(status);
        Assert.Equal("hardware", status.Mode);
        Assert.Equal(2, status.ProtocolKnowledge.MessagesClearedForSending);

        var state = await client.GetFromJsonAsync<ConsoleStateDto>("/api/console/state");
        Assert.NotNull(state);
        Assert.Empty(state.SupportedCommands);
        Assert.False(state.MastersReported);

        using var connect = await client.PostAsync("/api/console/connect", content: null);
        var connection = await connect.Content.ReadFromJsonAsync<ConnectionDto>();
        Assert.NotNull(connection);
        Assert.Equal("error", connection.Status);
        Assert.Contains("test: no console", connection.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("vid_", connection.DeviceDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_control_needs_the_USB_mode_and_todays_date_and_then_names_its_controls()
    {
        using var withoutUsb = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Device:Mode", "Simulated");
            b.UseSetting("Device:Usb:LiveControlDate", Today);
        });
        Assert.Contains("LiveControlDate", Assert.ThrowsAny<Exception>(() => withoutUsb.CreateClient()).ToString(), StringComparison.Ordinal);

        using var stale = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Device:Mode", "Usb");
            b.UseSetting("Device:Usb:ConfirmDate", Today);
            b.UseSetting("Device:Usb:LiveControlDate", "2026-01-01");
            b.ConfigureServices(services => services.AddSingleton<Func<IHspPipe>>(() => throw new HspPipeException("test: no console")));
        });
        Assert.Contains("LiveControlDate", Assert.ThrowsAny<Exception>(() => stale.CreateClient()).ToString(), StringComparison.Ordinal);

        // live control for today: the device names the SC-2 whitelist and nothing else. The same two guards as above keep
        // this host off real hardware (the pipe factory is replaced; the path is one the WinUSB pipe refuses).
        using var live = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Device:Mode", "Usb");
            b.UseSetting("Device:Usb:ConfirmDate", Today);
            b.UseSetting("Device:Usb:LiveControlDate", Today);
            b.UseSetting("Device:Usb:DevicePath", @"\\?\usb#vid_0000&pid_0000&mi_09#test");
            b.ConfigureServices(services => services.AddSingleton<Func<IHspPipe>>(() => throw new HspPipeException("test: no console")));
        });
        using var client = live.CreateClient();
        var state = await client.GetFromJsonAsync<ConsoleStateDto>("/api/console/state");
        Assert.NotNull(state);
        Assert.Contains("setMaster", state.SupportedCommands);
        Assert.DoesNotContain("recordMemory", state.SupportedCommands);
        Assert.NotNull(state.WritableControls);
        Assert.Contains("channelFader:1", state.WritableControls);
        Assert.DoesNotContain("channelFader:2", state.WritableControls);
    }

    [Fact]
    public async Task Requests_from_a_foreign_origin_are_refused_before_they_execute()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Device:Mode", "Simulated"));
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
        Assert.Equal("replay", DtoMapper.ModeName(DeviceMode.Replay));
        Assert.Equal("hardware", DtoMapper.ModeName(DeviceMode.Hardware));
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
        Assert.Equal("not_supported", DtoMapper.ToErrorDto(new NotSupportedException()).Code);
    }
}
