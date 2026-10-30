using Microsoft.AspNetCore.SignalR;
using SmartFade.Api.Contracts;
using SmartFade.Application;

namespace SmartFade.Api.Hubs;

/// <summary>
/// Realtime API of docs/api.md. Commands return once the application accepted them; the
/// device's confirmation arrives through the broadcast events. Rejections are answered to the
/// caller as `errorOccurred` with a stable code; device execution failures are broadcast by
/// the application layer.
/// </summary>
public sealed class ConsoleHub : Hub
{
    private readonly ConsoleService _console;
    private readonly ILogger<ConsoleHub> _logger;

    public ConsoleHub(ConsoleService console, ILogger<ConsoleHub> logger)
    {
        _console = console;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("consoleStateChanged", DtoMapper.ToDto(_console.GetState()), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public ConsoleStateDto GetState() => DtoMapper.ToDto(_console.GetState());

    public Task Connect() => Guard(() => _console.ConnectAsync(Context.ConnectionAborted), awaitExecution: true);

    public Task Disconnect() => Guard(() => _console.DisconnectAsync(Context.ConnectionAborted), awaitExecution: true);

    public Task SetChannelLevel(int channel, int level) =>
        Guard(() => _console.SetChannelLevelAsync(channel, level, Context.ConnectionAborted), awaitExecution: false);

    public Task SetMaster(int level) => Guard(() => _console.SetMasterAsync(level, Context.ConnectionAborted), awaitExecution: false);

    public Task SetBlackout(bool on) => Guard(() => _console.SetBlackoutAsync(on, Context.ConnectionAborted), awaitExecution: false);

    public Task SetBumps(int level) => Guard(() => _console.SetBumpsAsync(level, Context.ConnectionAborted), awaitExecution: false);

    public Task SetSolo(bool on) => Guard(() => _console.SetSoloAsync(on, Context.ConnectionAborted), awaitExecution: false);

    public Task BumpChannel(int channel, bool pressed) =>
        Guard(() => _console.BumpChannelAsync(channel, pressed, Context.ConnectionAborted), awaitExecution: false);

    public Task SetMemoryLevel(int page, int fader, int level) =>
        Guard(() => _console.SetMemoryLevelAsync(page, fader, level, Context.ConnectionAborted), awaitExecution: false);

    public Task BumpMemory(int page, int fader, bool pressed) =>
        Guard(() => _console.BumpMemoryAsync(page, fader, pressed, Context.ConnectionAborted), awaitExecution: false);

    public Task RecordMemory(int page, int fader) =>
        Guard(() => _console.RecordMemoryAsync(page, fader, Context.ConnectionAborted), awaitExecution: false);

    public Task StoreMemory(int page, int fader, int[] levels) =>
        Guard(() => _console.StoreMemoryAsync(page, fader, levels, Context.ConnectionAborted), awaitExecution: false);

    public Task SetIndependent(int number, bool on) =>
        Guard(() => _console.SetIndependentAsync(number, on, Context.ConnectionAborted), awaitExecution: false);

    public Task ClearChannels() => Guard(() => _console.ClearChannelsAsync(Context.ConnectionAborted), awaitExecution: false);

    public Task ClearMemories() => Guard(() => _console.ClearMemoriesAsync(Context.ConnectionAborted), awaitExecution: false);

    /// <summary>Reads one memory; null when the request was rejected (the caller also receives `errorOccurred`).</summary>
    public async Task<MemoryDto?> GetMemory(int page, int fader)
    {
        try
        {
            var memory = await _console.GetMemoryAsync(page, fader, Context.ConnectionAborted);
            return DtoMapper.ToDto(memory, _console.Capabilities);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning("getMemory rejected: {Message}", ex.Message);
            await Clients.Caller.SendAsync("errorOccurred", DtoMapper.ToErrorDto(ex), Context.ConnectionAborted);
            return null;
        }
    }

    private async Task Guard(Func<Task> action, bool awaitExecution)
    {
        try
        {
            var execution = action();
            if (awaitExecution)
            {
                await execution;
            }
            else
            {
                Observe(execution);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning("Command rejected: {Message}", ex.Message);
            await Clients.Caller.SendAsync("errorOccurred", DtoMapper.ToErrorDto(ex), Context.ConnectionAborted);
        }
    }

    /// <summary>Execution failures are already logged and broadcast by the application layer; this only prevents an unobserved task.</summary>
    private static void Observe(Task execution) =>
        _ = execution.ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
}
