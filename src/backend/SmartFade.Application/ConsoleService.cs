using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SmartFade.Application.Ports;
using SmartFade.Domain;

namespace SmartFade.Application;

/// <summary>
/// Use cases of the console: connect, disconnect, read state, set desired levels, bump, play
/// and record memories. Every call to the device goes through one command pump, so commands
/// reach the device in the order they were accepted and the device's reports are applied in
/// that order too; the device sees a token owned by this service, never a UI client's. Commands
/// return once they are accepted (validated and queued); the actual state changes only when the
/// device reports it. Events are raised on the pump thread or on the caller; subscribers must be
/// thread-safe.
/// </summary>
public sealed class ConsoleService : IDisposable
{
    private readonly ISmartFadeDevice _device;
    private readonly ConsoleStateStore _store;
    private readonly ILogger<ConsoleService> _logger;
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _pump;

    public ConsoleService(ISmartFadeDevice device, ConsoleStateStore store, ILogger<ConsoleService> logger)
    {
        _device = device;
        _store = store;
        _logger = logger;
        _device.StateChanged += OnDeviceStateChanged;
        _device.ConnectionChanged += OnDeviceConnectionChanged;
        _pump = Task.Run(PumpAsync);
    }

    public event Action<ConnectionState>? ConnectionChanged;

    public event Action<IReadOnlyList<ChannelLevel>>? ChannelsChanged;

    public event Action<Masters>? MastersChanged;

    public event Action<IReadOnlyList<byte>>? OutputsChanged;

    public event Action<IReadOnlyList<MemoryState>>? MemoriesChanged;

    /// <summary>Raised with the full state after a connection change and after a snapshot from the device.</summary>
    public event Action<ConsoleState>? StateChanged;

    public event Action<string>? ErrorOccurred;

    public ConsoleCapabilities Capabilities => _store.Capabilities;

    public ConsoleState GetState() => _store.GetState();

    /// <summary>Connects the device; completes when the connect attempt has run (connected, or failed with its exception).</summary>
    public Task ConnectAsync(CancellationToken cancellationToken) =>
        EnqueueAsync("connect", _device.ConnectAsync, cancellationToken);

    /// <summary>Disconnects the device; completes when the disconnect has run.</summary>
    public Task DisconnectAsync(CancellationToken cancellationToken) =>
        EnqueueAsync("disconnect", _device.DisconnectAsync, cancellationToken);

    /// <summary>
    /// Sets the desired level of a channel. Validation failures throw immediately; the returned
    /// task completes when the device has executed the command (callers that only need
    /// acceptance can ignore it). Execution failures are also raised as <see cref="ErrorOccurred"/>.
    /// </summary>
    public Task SetChannelLevelAsync(int channel, int level, CancellationToken cancellationToken)
    {
        RequireChannel(channel);
        RequireLevel(level);
        EnsureCommandAllowed(DeviceCommands.SetChannelLevel);
        var desired = _store.SetDesiredChannel(channel, (byte)level);
        ChannelsChanged?.Invoke(new[] { desired });
        _logger.LogDebug("Desired level of channel {Channel} set to {Level}", channel, level);
        return EnqueueAsync($"setChannelLevel({channel})", ct => _device.SetChannelLevelAsync(channel, (byte)level, ct), cancellationToken);
    }

    public Task SetMasterAsync(int level, CancellationToken cancellationToken)
    {
        RequireLevel(level);
        EnsureCommandAllowed(DeviceCommands.SetMaster);
        return EnqueueAsync("setMaster", ct => _device.SetMasterAsync((byte)level, ct), cancellationToken);
    }

    public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken)
    {
        EnsureCommandAllowed(DeviceCommands.SetBlackout);
        return EnqueueAsync("setBlackout", ct => _device.SetBlackoutAsync(on, ct), cancellationToken);
    }

    public Task SetBumpsAsync(int level, CancellationToken cancellationToken)
    {
        RequireLevel(level);
        EnsureCommandAllowed(DeviceCommands.SetBumps);
        return EnqueueAsync("setBumps", ct => _device.SetBumpsAsync((byte)level, ct), cancellationToken);
    }

    public Task SetSoloAsync(bool on, CancellationToken cancellationToken)
    {
        EnsureCommandAllowed(DeviceCommands.SetSolo);
        return EnqueueAsync("setSolo", ct => _device.SetSoloAsync(on, ct), cancellationToken);
    }

    /// <summary>Presses (true) or releases (false) the bump button under a channel fader.</summary>
    public Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken)
    {
        RequireChannel(channel);
        EnsureCommandAllowed(DeviceCommands.BumpChannel);
        return EnqueueAsync($"bumpChannel({channel})", ct => _device.BumpChannelAsync(channel, pressed, ct), cancellationToken);
    }

    /// <summary>Moves a memory fader (page 1..12, fader 1..24) to a playback level.</summary>
    public Task SetMemoryLevelAsync(int page, int fader, int level, CancellationToken cancellationToken)
    {
        RequireMemory(page, fader);
        RequireLevel(level);
        EnsureCommandAllowed(DeviceCommands.SetMemoryLevel);
        return EnqueueAsync($"setMemoryLevel({page}/{fader})", ct => _device.SetMemoryLevelAsync(page, fader, (byte)level, ct), cancellationToken);
    }

    /// <summary>Presses (true) or releases (false) the bump button under a memory fader.</summary>
    public Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken)
    {
        RequireMemory(page, fader);
        EnsureCommandAllowed(DeviceCommands.BumpMemory);
        return EnqueueAsync($"bumpMemory({page}/{fader})", ct => _device.BumpMemoryAsync(page, fader, pressed, ct), cancellationToken);
    }

    /// <summary>REC MEM: stores the current look into the memory.</summary>
    public Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        RequireMemory(page, fader);
        EnsureCommandAllowed(DeviceCommands.RecordMemory);
        _logger.LogInformation("Recording the current look into memory page {Page} fader {Fader}", page, fader);
        return EnqueueAsync($"recordMemory({page}/{fader})", ct => _device.RecordMemoryAsync(page, fader, ct), cancellationToken);
    }

    /// <summary>EDIT MEM: replaces the memory's channel levels (one per channel, 0..255); all zero deletes the memory.</summary>
    public Task StoreMemoryAsync(int page, int fader, IReadOnlyList<int> levels, CancellationToken cancellationToken)
    {
        RequireMemory(page, fader);
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count != Capabilities.Channels)
        {
            throw new ArgumentException($"Expected {Capabilities.Channels} channel levels, got {levels.Count}.", nameof(levels));
        }

        var bytes = new byte[levels.Count];
        for (var i = 0; i < bytes.Length; i++)
        {
            RequireLevel(levels[i]);
            bytes[i] = (byte)levels[i];
        }

        EnsureCommandAllowed(DeviceCommands.StoreMemory);
        _logger.LogInformation("Storing edited levels into memory page {Page} fader {Fader}", page, fader);
        return EnqueueAsync($"storeMemory({page}/{fader})", ct => _device.StoreMemoryAsync(page, fader, bytes, ct), cancellationToken);
    }

    /// <summary>Switches independent 1 or 2 (IND 1 / IND 2) on or off.</summary>
    public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken)
    {
        if (number < 1 || number > Capabilities.Independents)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, $"Independent must be 1..{Capabilities.Independents}.");
        }

        EnsureCommandAllowed(DeviceCommands.SetIndependent);
        return EnqueueAsync($"setIndependent({number})", ct => _device.SetIndependentAsync(number, on, ct), cancellationToken);
    }

    /// <summary>CLEAR pressed twice: every channel fader's desired level becomes zero, then the device is told.</summary>
    public Task ClearChannelsAsync(CancellationToken cancellationToken)
    {
        EnsureCommandAllowed(DeviceCommands.ClearChannels);
        var desired = new List<ChannelLevel>(Capabilities.Channels);
        for (var channel = 1; channel <= Capabilities.Channels; channel++)
        {
            desired.Add(_store.SetDesiredChannel(channel, Level.Min));
        }

        ChannelsChanged?.Invoke(desired);
        _logger.LogInformation("CLEAR: every channel fader to zero");
        return EnqueueAsync("clearChannels", _device.ClearChannelLevelsAsync, cancellationToken);
    }

    /// <summary>CLEAR pressed three times: every memory fader to zero.</summary>
    public Task ClearMemoriesAsync(CancellationToken cancellationToken)
    {
        EnsureCommandAllowed(DeviceCommands.ClearMemories);
        _logger.LogInformation("CLEAR: every memory fader to zero");
        return EnqueueAsync("clearMemories", _device.ClearMemoryLevelsAsync, cancellationToken);
    }

    /// <summary>Reads a memory's channel levels and label from the device.</summary>
    public Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        RequireMemory(page, fader);
        if (_store.Connection.Status != ConnectionStatus.Connected)
        {
            throw new NotConnectedException();
        }

        return EnqueueWithResultAsync($"getMemory({page}/{fader})", ct => _device.GetMemoryAsync(page, fader, ct), cancellationToken);
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _lifetime.Cancel();
        _device.StateChanged -= OnDeviceStateChanged;
        _device.ConnectionChanged -= OnDeviceConnectionChanged;
        _lifetime.Dispose();
    }

    private void RequireChannel(int channel)
    {
        if (!Capabilities.IsValidChannel(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, $"Channel must be 1..{Capabilities.Channels}.");
        }
    }

    private static void RequireLevel(int level)
    {
        if (!Level.IsValid(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be 0..255.");
        }
    }

    private void RequireMemory(int page, int fader)
    {
        if (!Capabilities.IsValidMemoryPage(page))
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, $"Memory page must be 1..{Capabilities.MemoryPages}.");
        }

        if (!Capabilities.IsValidMemoryFader(fader))
        {
            throw new ArgumentOutOfRangeException(nameof(fader), fader, $"Memory fader must be 1..{Capabilities.MemoriesPerPage}.");
        }
    }

    private void EnsureCommandAllowed(DeviceCommands command)
    {
        if (_store.Connection.Status != ConnectionStatus.Connected)
        {
            throw new NotConnectedException();
        }

        if (!_device.SupportedCommands.HasFlag(command))
        {
            throw new NotSupportedException($"The connected device ({_device.Description}) does not accept {command}.");
        }
    }

    private Task EnqueueAsync(string name, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(new WorkItem(name, action, completion)))
        {
            throw new ObjectDisposedException(nameof(ConsoleService));
        }

        return completion.Task.WaitAsync(cancellationToken);
    }

    private async Task<T> EnqueueWithResultAsync<T>(string name, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        T result = default!;
        await EnqueueAsync(
            name,
            async ct =>
            {
                result = await action(ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
            {
                try
                {
                    await item.Action(_lifetime.Token).ConfigureAwait(false);
                    item.Completion.TrySetResult();
                }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
                {
                    item.Completion.TrySetCanceled(_lifetime.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Device command {Command} failed", item.Name);
                    ErrorOccurred?.Invoke($"{item.Name}: {ex.Message}");
                    item.Completion.TrySetException(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // disposed
        }
    }

    private void OnDeviceConnectionChanged(object? sender, ConnectionState state)
    {
        var cleared = _store.SetConnection(state);
        _logger.LogInformation("Connection: {Status} ({Mode}, {Device}){Error}", state.Status, state.Mode, state.DeviceDescription, state.Error is null ? string.Empty : $": {state.Error}");
        ConnectionChanged?.Invoke(state);
        if (cleared.Count > 0)
        {
            ChannelsChanged?.Invoke(cleared);
        }

        if (state.Status == ConnectionStatus.Error && state.Error is { } error)
        {
            ErrorOccurred?.Invoke(error);
        }

        StateChanged?.Invoke(_store.GetState());
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChange change)
    {
        var channels = _store.ApplyDeviceChange(change, out var masters, out var outputs, out var memories);
        if (change.Snapshot is not null || change.ShowFileName is not null || change.FirmwareVersion is not null || change.Independents is not null || change.MemoryPage is not null || change.Keys is not null)
        {
            // a snapshot, or a detail the UI only receives with the full state (file name, firmware)
            StateChanged?.Invoke(_store.GetState());
            if (change.Snapshot is not null)
            {
                return;
            }
        }

        if (channels.Count > 0)
        {
            ChannelsChanged?.Invoke(channels);
        }

        if (masters is not null)
        {
            MastersChanged?.Invoke(masters);
        }

        if (memories is not null)
        {
            MemoriesChanged?.Invoke(memories);
        }

        if (outputs is not null)
        {
            OutputsChanged?.Invoke(outputs);
        }
    }

    private sealed record WorkItem(string Name, Func<CancellationToken, Task> Action, TaskCompletionSource Completion);
}
