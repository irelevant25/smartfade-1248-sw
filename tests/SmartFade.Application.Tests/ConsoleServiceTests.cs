using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartFade.Application;
using SmartFade.Application.Ports;
using SmartFade.Devices.Simulated;
using SmartFade.Domain;

namespace SmartFade.Application.Tests;

public class ConsoleServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static (ConsoleService Service, SimulatedSmartFadeDevice Device) CreateSimulated(int latencyMs = 0, int connectDelayMs = 0, bool failCommands = false)
    {
        var options = Options.Create(new SimulationOptions
        {
            CommandLatencyMilliseconds = latencyMs,
            ConnectDelayMilliseconds = connectDelayMs,
            FailCommands = failCommands,
        });
        var device = new SimulatedSmartFadeDevice(options, NullLogger<SimulatedSmartFadeDevice>.Instance);
        return (Create(device), device);
    }

    private static ConsoleService Create(ISmartFadeDevice device)
    {
        var store = new ConsoleStateStore(device.Capabilities, new ConnectionState(ConnectionStatus.Disconnected, device.Mode, device.Description), device.SupportedCommands);
        return new ConsoleService(device, store, NullLogger<ConsoleService>.Instance);
    }

    private static Task<T> WaitFor<T>(Action<Action<T>> subscribe, Func<T, bool> condition)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscribe(value =>
        {
            if (condition(value))
            {
                tcs.TrySetResult(value);
            }
        });
        return tcs.Task.WaitAsync(Timeout);
    }

    private static int[] LevelsWith(params (int Channel, int Level)[] entries)
    {
        var levels = new int[48];
        foreach (var (channel, level) in entries)
        {
            levels[channel - 1] = level;
        }

        return levels;
    }

    [Fact]
    public void Starts_disconnected_in_simulated_mode()
    {
        var (service, _) = CreateSimulated();
        var state = service.GetState();
        Assert.Equal(ConnectionStatus.Disconnected, state.Connection.Status);
        Assert.Equal(DeviceMode.Simulated, state.Connection.Mode);
        Assert.False(state.ActualStateReported);
        Assert.Equal(48, state.Channels.Count);
        Assert.Equal(512, state.Outputs.Count);
        Assert.Equal(288, state.Memories.Count);
        Assert.All(state.Memories, m => Assert.False(m.Recorded));
        Assert.Equal(new MemoryState(1, 1, 0, Recorded: false, Label.Empty, Reported: false), state.Memories[0]);   // placeholders until a device reports
        Assert.Equal(new MemoryState(12, 24, 0, Recorded: false, Label.Empty, Reported: false), state.Memories[287]);
        Assert.Equal(DeviceCommands.All, state.SupportedCommands);
    }

    [Fact]
    public async Task Connect_reports_connected_and_a_full_snapshot()
    {
        var (service, _) = CreateSimulated();
        var statuses = new List<ConnectionStatus>();
        service.ConnectionChanged += info => statuses.Add(info.Status);
        var snapshots = 0;
        service.StateChanged += _ => snapshots++;

        await service.ConnectAsync(CancellationToken.None);

        Assert.Equal(new[] { ConnectionStatus.Connecting, ConnectionStatus.Connected }, statuses);
        Assert.True(snapshots >= 1);
        Assert.Equal(ConnectionStatus.Connected, service.GetState().Connection.Status);
        Assert.True(service.GetState().ActualStateReported);
        // the simulated console starts with the MASTER at full, BUMPS at zero, everything else off
        Assert.Equal(new Masters(255, 0, Blackout: false, Solo: false, Independent1: false, Independent2: false), service.GetState().Masters);
    }

    [Fact]
    public async Task Commands_are_rejected_while_disconnected()
    {
        var (service, _) = CreateSimulated();
        await Assert.ThrowsAsync<NotConnectedException>(() => service.SetChannelLevelAsync(1, 100, CancellationToken.None));
        await Assert.ThrowsAsync<NotConnectedException>(() => service.BumpChannelAsync(1, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotConnectedException>(() => service.RecordMemoryAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotConnectedException>(() => service.GetMemoryAsync(1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Out_of_range_commands_are_rejected()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetChannelLevelAsync(49, 100, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetChannelLevelAsync(1, 256, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetBumpsAsync(-1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.BumpChannelAsync(0, true, CancellationToken.None));
    }

    [Fact]
    public async Task Memory_commands_validate_page_fader_and_levels()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);

        var page = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetMemoryLevelAsync(0, 1, 10, CancellationToken.None));
        Assert.Equal("page", page.ParamName);
        var fader = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetMemoryLevelAsync(12, 25, 10, CancellationToken.None));
        Assert.Equal("fader", fader.ParamName);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RecordMemoryAsync(13, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetMemoryAsync(1, 0, CancellationToken.None));

        var count = await Assert.ThrowsAsync<ArgumentException>(() => service.StoreMemoryAsync(1, 1, new int[47], CancellationToken.None));
        Assert.Equal("levels", count.ParamName);
        var level = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.StoreMemoryAsync(1, 1, LevelsWith((3, 256)), CancellationToken.None));
        Assert.Equal("level", level.ParamName);
    }

    [Fact]
    public async Task Desired_and_actual_levels_are_kept_apart_until_the_device_reports()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        var reports = new List<IReadOnlyList<ChannelLevel>>();
        service.ChannelsChanged += channels => reports.Add(channels);

        await service.SetChannelLevelAsync(5, 171, CancellationToken.None);

        // first report: desired set, actual still 0, pending; later: the device confirmed
        Assert.True(reports.Count >= 2, $"expected at least 2 reports, got {reports.Count}");
        var first = Assert.Single(reports[0]);
        Assert.Equal(new ChannelLevel(5, 171, 0, Pending: true), first);
        var confirmed = service.GetState().Channels[4];
        Assert.Equal(new ChannelLevel(5, 171, 171, Pending: false, Output: 171), confirmed);
    }

    [Fact]
    public async Task A_silent_device_leaves_the_command_pending_and_nothing_assumes_success()
    {
        var device = new SilentDevice();
        var service = Create(device);
        await service.ConnectAsync(CancellationToken.None);

        var execution = service.SetChannelLevelAsync(12, 100, CancellationToken.None);
        await execution;   // the device accepted the call but reports nothing

        var channel = service.GetState().Channels[11];
        Assert.Equal(new ChannelLevel(12, Desired: 100, Actual: 0, Pending: true), channel);
        Assert.False(service.GetState().ActualStateReported);
    }

    [Fact]
    public async Task A_failing_device_keeps_the_desired_level_and_raises_an_error()
    {
        var (service, _) = CreateSimulated(failCommands: true);
        await service.ConnectAsync(CancellationToken.None);
        var error = WaitFor<string>(h => service.ErrorOccurred += h, _ => true);

        var execution = service.SetChannelLevelAsync(3, 50, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => execution);

        Assert.Contains("setChannelLevel(3)", await error, StringComparison.Ordinal);
        Assert.Equal(new ChannelLevel(3, 50, 0, Pending: true), service.GetState().Channels[2]);
    }

    [Fact]
    public async Task Disconnect_clears_pending_flags_and_tells_subscribers()
    {
        var device = new SilentDevice();
        var service = Create(device);
        await service.ConnectAsync(CancellationToken.None);
        await service.SetChannelLevelAsync(1, 10, CancellationToken.None);
        Assert.True(service.GetState().Channels[0].Pending);

        var cleared = WaitFor<IReadOnlyList<ChannelLevel>>(h => service.ChannelsChanged += h, c => c.Any(x => x.Channel == 1 && !x.Pending));
        await service.DisconnectAsync(CancellationToken.None);

        Assert.Equal(ConnectionStatus.Disconnected, service.GetState().Connection.Status);
        Assert.All(service.GetState().Channels, c => Assert.False(c.Pending));
        Assert.Equal(1, (await cleared).Single().Channel);
    }

    [Fact]
    public async Task Simulator_outputs_follow_the_one_to_one_patch_and_the_master()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);

        await service.SetChannelLevelAsync(5, 200, CancellationToken.None);
        Assert.Equal(200, service.GetState().Outputs[4]);
        Assert.Equal(200, service.GetState().Channels[4].Output);

        await service.SetMasterAsync(128, CancellationToken.None);
        Assert.Equal(128, service.GetState().Masters.Master);
        Assert.Equal(100, service.GetState().Outputs[4]);   // 200 × 128 / 255 = 100.4 → 100 (simulation model)
        Assert.Equal(100, service.GetState().Channels[4].Output);
        Assert.Equal(200, service.GetState().Channels[4].Actual);   // the fader did not move

        await service.SetBlackoutAsync(true, CancellationToken.None);
        Assert.True(service.GetState().Masters.Blackout);
        Assert.Equal(0, service.GetState().Outputs[4]);
        Assert.Equal(0, service.GetState().Channels[4].Output);
    }

    [Fact]
    public async Task Channel_bumps_pile_on_at_the_bumps_level_and_solo_mutes_the_rest()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await service.SetChannelLevelAsync(1, 100, CancellationToken.None);
        await service.SetChannelLevelAsync(2, 200, CancellationToken.None);

        // BUMPS starts at zero: a bump adds nothing until it is raised
        await service.BumpChannelAsync(1, true, CancellationToken.None);
        Assert.Equal(100, service.GetState().Outputs[0]);
        await service.BumpChannelAsync(1, false, CancellationToken.None);
        await service.SetBumpsAsync(255, CancellationToken.None);

        // pile-on (SOLO off): the bumped channel goes to the BUMPS level, the others stay
        await service.BumpChannelAsync(1, true, CancellationToken.None);
        Assert.Equal(255, service.GetState().Outputs[0]);
        Assert.Equal(200, service.GetState().Outputs[1]);
        Assert.Equal(100, service.GetState().Channels[0].Actual);   // the fader itself did not move

        // SOLO: the other intensity channels are muted while the bump is held
        await service.SetSoloAsync(true, CancellationToken.None);
        Assert.True(service.GetState().Masters.Solo);
        Assert.Equal(255, service.GetState().Outputs[0]);
        Assert.Equal(0, service.GetState().Outputs[1]);

        // release: everything comes back
        await service.BumpChannelAsync(1, false, CancellationToken.None);
        Assert.Equal(100, service.GetState().Outputs[0]);
        Assert.Equal(200, service.GetState().Outputs[1]);

        // a lower BUMPS level piles on below the fader: HTP keeps the fader's level
        await service.SetSoloAsync(false, CancellationToken.None);
        await service.SetBumpsAsync(60, CancellationToken.None);
        Assert.Equal(60, service.GetState().Masters.Bumps);
        await service.BumpChannelAsync(1, true, CancellationToken.None);
        Assert.Equal(100, service.GetState().Outputs[0]);
        await service.BumpChannelAsync(2, true, CancellationToken.None);
        Assert.Equal(200, service.GetState().Outputs[1]);
    }

    [Fact]
    public async Task Record_memory_stores_the_look_and_memory_faders_play_it_back_proportionally()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        var recorded = WaitFor<IReadOnlyList<MemoryState>>(h => service.MemoriesChanged += h, m => m.Any(x => x is { Page: 1, Fader: 1, Recorded: true }));
        await service.SetChannelLevelAsync(3, 200, CancellationToken.None);
        await service.SetChannelLevelAsync(4, 50, CancellationToken.None);

        await service.RecordMemoryAsync(1, 1, CancellationToken.None);

        Assert.True((await recorded).Single().Recorded);
        Assert.True(service.GetState().Memories[0].Recorded);
        var content = await service.GetMemoryAsync(1, 1, CancellationToken.None);
        Assert.Equal(200, content.Levels[2]);
        Assert.Equal(50, content.Levels[3]);
        Assert.Equal(0, content.Levels[0]);

        // clear the faders: the look is gone until the memory fader brings it back
        await service.SetChannelLevelAsync(3, 0, CancellationToken.None);
        await service.SetChannelLevelAsync(4, 0, CancellationToken.None);
        Assert.Equal(0, service.GetState().Outputs[2]);

        await service.SetMemoryLevelAsync(1, 1, 255, CancellationToken.None);
        Assert.Equal(255, service.GetState().Memories[0].Level);
        Assert.Equal(200, service.GetState().Outputs[2]);
        Assert.Equal(50, service.GetState().Outputs[3]);

        await service.SetMemoryLevelAsync(1, 1, 128, CancellationToken.None);
        Assert.Equal(100, service.GetState().Outputs[2]);   // 200 × 128 / 255 → 100 (simulation model)

        // HTP with a channel fader: the higher source wins
        await service.SetChannelLevelAsync(3, 150, CancellationToken.None);
        Assert.Equal(150, service.GetState().Outputs[2]);
        Assert.Equal(150, service.GetState().Channels[2].Output);
        await service.SetChannelLevelAsync(3, 20, CancellationToken.None);
        Assert.Equal(100, service.GetState().Outputs[2]);
        Assert.Equal(20, service.GetState().Channels[2].Actual);
    }

    [Fact]
    public async Task Store_memory_replaces_the_levels_and_all_zero_deletes_it()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);

        await service.StoreMemoryAsync(2, 5, LevelsWith((10, 255), (11, 128)), CancellationToken.None);

        var memory = service.GetState().Memories[28];   // page 2 fader 5 = memory 29
        Assert.Equal(new MemoryState(2, 5, 0, Recorded: true, Label.Empty), memory);
        Assert.Equal(29, memory.Number(service.Capabilities));
        var content = await service.GetMemoryAsync(2, 5, CancellationToken.None);
        Assert.Equal(255, content.Levels[9]);
        Assert.Equal(128, content.Levels[10]);

        await service.StoreMemoryAsync(2, 5, new int[48], CancellationToken.None);

        Assert.False(service.GetState().Memories[28].Recorded);
    }

    [Fact]
    public async Task Memory_bumps_use_the_bumps_level_and_solo_replaces_the_output()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await service.StoreMemoryAsync(1, 2, LevelsWith((4, 255), (6, 128)), CancellationToken.None);
        await service.SetChannelLevelAsync(5, 255, CancellationToken.None);
        await service.SetBumpsAsync(255, CancellationToken.None);

        // pile-on: the memory's content at the BUMPS level adds to the live output
        await service.BumpMemoryAsync(1, 2, true, CancellationToken.None);
        Assert.Equal(255, service.GetState().Outputs[3]);
        Assert.Equal(128, service.GetState().Outputs[5]);
        Assert.Equal(255, service.GetState().Outputs[4]);

        // SOLO: the bumped content replaces the output
        await service.SetSoloAsync(true, CancellationToken.None);
        Assert.Equal(255, service.GetState().Outputs[3]);
        Assert.Equal(0, service.GetState().Outputs[4]);

        // the owner's observation: SOLO on with BUMPS at zero turns the bumped memory's lights off
        await service.SetBumpsAsync(0, CancellationToken.None);
        Assert.Equal(0, service.GetState().Outputs[3]);
        Assert.Equal(0, service.GetState().Outputs[4]);

        await service.BumpMemoryAsync(1, 2, false, CancellationToken.None);
        Assert.Equal(0, service.GetState().Outputs[3]);
        Assert.Equal(255, service.GetState().Outputs[4]);
    }

    [Fact]
    public async Task Independents_drive_DMX_97_and_98_at_full_whatever_the_masters_say()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await service.SetMasterAsync(0, CancellationToken.None);
        await service.SetBlackoutAsync(true, CancellationToken.None);
        await service.SetSoloAsync(true, CancellationToken.None);

        await service.SetIndependentAsync(1, true, CancellationToken.None);

        Assert.True(service.GetState().Masters.Independent1);
        Assert.False(service.GetState().Masters.Independent2);
        Assert.Equal(255, service.GetState().Outputs[96]);   // IND 1 = DMX 97 (DOCUMENTED default)
        Assert.Equal(0, service.GetState().Outputs[97]);

        await service.SetIndependentAsync(2, true, CancellationToken.None);
        Assert.Equal(255, service.GetState().Outputs[97]);   // IND 2 = DMX 98

        await service.SetIndependentAsync(1, false, CancellationToken.None);
        Assert.Equal(0, service.GetState().Outputs[96]);
        Assert.Equal(255, service.GetState().Outputs[97]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetIndependentAsync(3, true, CancellationToken.None));
    }

    [Fact]
    public async Task Clear_sets_the_channel_faders_and_then_the_memory_faders_to_zero()
    {
        var (service, _) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await service.StoreMemoryAsync(1, 1, LevelsWith((3, 255)), CancellationToken.None);
        await service.SetMemoryLevelAsync(1, 1, 128, CancellationToken.None);
        await service.SetChannelLevelAsync(1, 200, CancellationToken.None);
        await service.SetChannelLevelAsync(2, 100, CancellationToken.None);
        var reports = new List<IReadOnlyList<ChannelLevel>>();
        service.ChannelsChanged += channels => reports.Add(channels);

        await service.ClearChannelsAsync(CancellationToken.None);

        // the desired levels drop at once, for every channel, then the device confirms
        Assert.Equal(48, reports[0].Count);
        Assert.All(reports[0], c => Assert.Equal(0, c.Desired));
        Assert.All(service.GetState().Channels, c => Assert.Equal(new ChannelLevel(c.Channel, 0, 0, Pending: false, Output: c.Channel == 3 ? (byte)128 : (byte)0), c));
        Assert.Equal(0, service.GetState().Outputs[0]);
        Assert.Equal(128, service.GetState().Outputs[2]);   // the memory fader is still up

        await service.ClearMemoriesAsync(CancellationToken.None);

        Assert.Equal(0, service.GetState().Memories[0].Level);
        Assert.True(service.GetState().Memories[0].Recorded);   // CLEAR never erases content
        Assert.Equal(0, service.GetState().Outputs[2]);
    }

    [Fact]
    public async Task Console_side_changes_update_the_actual_level_without_touching_the_desired_one()
    {
        var (service, device) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        await service.SetChannelLevelAsync(7, 40, CancellationToken.None);

        device.SimulateConsoleChange(7, 90);

        Assert.Equal(new ChannelLevel(7, Desired: 40, Actual: 90, Pending: false, Output: 90), service.GetState().Channels[6]);
    }

    [Fact]
    public async Task Link_loss_reports_an_error_state_and_clears_pending_flags()
    {
        var (service, device) = CreateSimulated();
        await service.ConnectAsync(CancellationToken.None);
        var error = WaitFor<string>(h => service.ErrorOccurred += h, _ => true);

        device.SimulateLinkLoss("cable pulled");

        Assert.Equal(ConnectionStatus.Error, service.GetState().Connection.Status);
        Assert.Equal("cable pulled", service.GetState().Connection.Error);
        Assert.Equal("cable pulled", await error);
        await Assert.ThrowsAsync<NotConnectedException>(() => service.SetChannelLevelAsync(1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_commands_to_one_channel_are_applied_in_order()
    {
        var (service, _) = CreateSimulated(latencyMs: 1);
        await service.ConnectAsync(CancellationToken.None);

        Task? last = null;
        for (var level = 1; level <= 60; level++)
        {
            last = service.SetChannelLevelAsync(9, level, CancellationToken.None);
        }

        await last!;
        Assert.Equal(new ChannelLevel(9, 60, 60, Pending: false, Output: 60), service.GetState().Channels[8]);
        Assert.Equal(60, service.GetState().Outputs[8]);
    }

    [Fact]
    public async Task A_callers_cancellation_never_strands_the_connection()
    {
        var (service, _) = CreateSimulated(connectDelayMs: 100);
        using var cts = new CancellationTokenSource(10);
        var connected = WaitFor<ConnectionState>(h => service.ConnectionChanged += h, s => s.Status == ConnectionStatus.Connected);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ConnectAsync(cts.Token));

        // the connect belongs to the server, not to the caller: it still completes
        Assert.Equal(ConnectionStatus.Connected, (await connected).Status);
    }

    [Fact]
    public async Task A_disconnect_requested_during_a_connect_wins()
    {
        var (service, _) = CreateSimulated(connectDelayMs: 50);
        var connect = service.ConnectAsync(CancellationToken.None);
        var disconnect = service.DisconnectAsync(CancellationToken.None);
        await Task.WhenAll(connect, disconnect);

        Assert.Equal(ConnectionStatus.Disconnected, service.GetState().Connection.Status);
    }

    [Fact]
    public async Task Two_connects_produce_one_connection()
    {
        var (service, _) = CreateSimulated(connectDelayMs: 20);
        var statuses = new List<ConnectionStatus>();
        service.ConnectionChanged += info => statuses.Add(info.Status);

        await Task.WhenAll(service.ConnectAsync(CancellationToken.None), service.ConnectAsync(CancellationToken.None));

        Assert.Equal(new[] { ConnectionStatus.Connecting, ConnectionStatus.Connected }, statuses);
    }

    /// <summary>A device that connects but never reports the effect of a command.</summary>
    private sealed class SilentDevice : ISmartFadeDevice
    {
        public DeviceMode Mode => DeviceMode.Simulated;

        public string Description => "silent test device";

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        public ConsoleCapabilities Capabilities => ConsoleCapabilities.SmartFade1248;

        public DeviceCommands SupportedCommands => DeviceCommands.All;

#pragma warning disable CS0067 // never raised: that is the point of this device
        public event EventHandler<DeviceStateChange>? StateChanged;
#pragma warning restore CS0067

        public event EventHandler<ConnectionState>? ConnectionChanged;

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            Status = ConnectionStatus.Connected;
            ConnectionChanged?.Invoke(this, new ConnectionState(Status, Mode, Description));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Status = ConnectionStatus.Disconnected;
            ConnectionChanged?.Invoke(this, new ConnectionState(Status, Mode, Description));
            return Task.CompletedTask;
        }

        public Task<ConsoleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConsoleSnapshot.Empty(Capabilities));

        public Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetMasterAsync(byte level, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetBumpsAsync(byte level, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetSoloAsync(bool on, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken) =>
            Task.FromResult(new MemoryContent(page, fader, new byte[Capabilities.Channels], Label.Empty));

        public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearChannelLevelsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearMemoryLevelsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
