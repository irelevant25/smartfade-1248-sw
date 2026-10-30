using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SmartFade.Api.Contracts;

namespace SmartFade.Api.Tests;

/// <summary>
/// The first vertical slice end to end, minus the browser: SignalR client → hub → application →
/// simulated console → broadcast back. Runs against the in-memory test server; no hardware, no sockets.
/// </summary>
public sealed class ConsoleHubTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly WebApplicationFactory<Program> _factory;

    public ConsoleHubTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Pinned: a Device__Mode left in the environment must never switch a test host to the console.
            builder.UseSetting("Device:Mode", "Simulated");
            builder.UseSetting("Device:Simulation:CommandLatencyMilliseconds", "0");
            builder.UseSetting("Device:Simulation:ConnectDelayMilliseconds", "0");
        });
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Status_endpoint_reports_simulated_mode_and_the_two_cleared_messages()
    {
        using var client = _factory.CreateClient();
        var status = await client.GetFromJsonAsync<StatusDto>("/api/status");

        Assert.NotNull(status);
        Assert.Equal("simulated", status.Mode);
        Assert.Equal("disconnected", status.Connection.Status);
        // SC-1 (docs/protocol/safety-checks.md): the poll and the listed fetch, for a read-only session
        Assert.Equal(2, status.ProtocolKnowledge.MessagesClearedForSending);
    }

    [Fact]
    public async Task Hub_connect_set_channel_level_and_record_a_memory_round_trip_through_the_simulator()
    {
        var states = new ConcurrentQueue<ConsoleStateDto>();
        var connections = new ConcurrentQueue<ConnectionDto>();
        var channelUpdates = new ConcurrentQueue<ChannelStateDto>();
        var errors = new ConcurrentQueue<ErrorDto>();
        var connected = new TaskCompletionSource<ConnectionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmed = new TaskCompletionSource<ChannelStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recorded = new TaskCompletionSource<MemoryStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var soloed = new TaskCompletionSource<MastersDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorSeen = new TaskCompletionSource<ErrorDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/console"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        hub.On<ConsoleStateDto>("consoleStateChanged", s => states.Enqueue(s));
        hub.On<ConnectionDto>("connectionStateChanged", c =>
        {
            connections.Enqueue(c);
            if (c.Status == "connected")
            {
                connected.TrySetResult(c);
            }
        });
        hub.On<ChannelStateDto[]>("channelStateChanged", updates =>
        {
            foreach (var u in updates)
            {
                channelUpdates.Enqueue(u);
                if (u is { Channel: 5, Actual: 171, Pending: false })
                {
                    confirmed.TrySetResult(u);
                }
            }
        });
        hub.On<MemoryStateDto[]>("memoriesChanged", updates =>
        {
            foreach (var m in updates)
            {
                if (m is { Page: 1, Fader: 1, Recorded: true })
                {
                    recorded.TrySetResult(m);
                }
            }
        });
        hub.On<MastersDto>("mastersChanged", m =>
        {
            if (m.Solo)
            {
                soloed.TrySetResult(m);
            }
        });
        hub.On<ErrorDto>("errorOccurred", e =>
        {
            errors.Enqueue(e);
            errorSeen.TrySetResult(e);
        });

        await hub.StartAsync();

        // the hub pushes the current state on connection
        var initial = await hub.InvokeAsync<ConsoleStateDto>("getState");
        Assert.Equal("disconnected", initial.Connection.Status);
        Assert.Equal("simulated", initial.Connection.Mode);
        Assert.Equal(48, initial.Channels.Count);
        Assert.Equal(288, initial.Memories.Count);
        Assert.Contains("recordMemory", initial.SupportedCommands);

        await hub.InvokeAsync("connect");
        var connection = await connected.Task.WaitAsync(Timeout);
        Assert.Equal("Simulated SmartFade 1248 (no hardware)", connection.DeviceDescription);

        await hub.InvokeAsync("setChannelLevel", 5, 171);
        var final = await confirmed.Task.WaitAsync(Timeout);
        Assert.Equal(171, final.Desired);
        Assert.Equal(171, final.Actual);
        Assert.Equal(171, final.Output);

        // the desired value was broadcast before the simulator confirmed it
        Assert.Contains(channelUpdates, u => u is { Channel: 5, Desired: 171, Actual: 0, Pending: true });

        var state = await hub.InvokeAsync<ConsoleStateDto>("getState");
        Assert.Equal(171, state.Channels[4].Actual);
        Assert.Equal(171, state.Outputs[4]);   // simulator: 1:1 patch, MASTER at full

        // REC MEM into page 1 fader 1, then read it back
        await hub.InvokeAsync("recordMemory", 1, 1);
        var memory = await recorded.Task.WaitAsync(Timeout);
        Assert.Equal(1, memory.Number);
        var content = await hub.InvokeAsync<MemoryDto?>("getMemory", 1, 1);
        Assert.NotNull(content);
        Assert.Equal(171, content.Levels[4]);
        Assert.Equal(48, content.Levels.Count);

        await hub.InvokeAsync("setSolo", true);
        Assert.True((await soloed.Task.WaitAsync(Timeout)).Solo);

        await hub.InvokeAsync("setChannelLevel", 49, 1);
        var error = await errorSeen.Task.WaitAsync(Timeout);
        Assert.Equal("invalid_channel", error.Code);

        var rejected = await hub.InvokeAsync<MemoryDto?>("getMemory", 13, 1);
        Assert.Null(rejected);
        Assert.Contains(errors, e => e.Code == "invalid_memory");

        await hub.InvokeAsync("disconnect");
        await hub.StopAsync();
        Assert.Contains(connections, c => c.Status == "disconnected");
        Assert.NotEmpty(states);
    }
}
