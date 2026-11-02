using Microsoft.AspNetCore.SignalR;
using SmartFade.Api.Contracts;
using SmartFade.Api.Hubs;
using SmartFade.Application;
using SmartFade.Domain;

namespace SmartFade.Api.Realtime;

/// <summary>
/// Forwards application events to every SignalR client. Output levels are coalesced to about
/// 30 messages per second; everything else is forwarded as it happens. A failed broadcast is
/// logged and never stops the host.
/// </summary>
public sealed class ConsoleEventsBroadcaster : BackgroundService
{
    private static readonly TimeSpan OutputsInterval = TimeSpan.FromMilliseconds(33);

    private readonly ConsoleService _console;
    private readonly IHubContext<ConsoleHub> _hub;
    private readonly ILogger<ConsoleEventsBroadcaster> _logger;
    private readonly object _gate = new();
    private IReadOnlyList<byte>? _latestOutputs;

    public ConsoleEventsBroadcaster(ConsoleService console, IHubContext<ConsoleHub> hub, ILogger<ConsoleEventsBroadcaster> logger)
    {
        _console = console;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _console.ConnectionChanged += OnConnectionChanged;
        _console.ChannelsChanged += OnChannelsChanged;
        _console.MastersChanged += OnMastersChanged;
        _console.MemoriesChanged += OnMemoriesChanged;
        _console.OutputsChanged += OnOutputsChanged;
        _console.LcdChanged += OnLcdChanged;
        _console.CrossfaderChanged += OnCrossfaderChanged;
        _console.StateChanged += OnStateChanged;
        _console.StateAtConnectChanged += OnStateAtConnectChanged;
        _console.ConsoleShowChanged += OnConsoleShowChanged;
        _console.ErrorOccurred += OnErrorOccurred;

        try
        {
            using var timer = new PeriodicTimer(OutputsInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                IReadOnlyList<byte>? outputs;
                lock (_gate)
                {
                    outputs = _latestOutputs;
                    _latestOutputs = null;
                }

                if (outputs is null)
                {
                    continue;
                }

                try
                {
                    await _hub.Clients.All.SendAsync("outputsChanged", DtoMapper.ToOutputsDto(outputs), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Broadcast of outputsChanged failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        finally
        {
            _console.ConnectionChanged -= OnConnectionChanged;
            _console.ChannelsChanged -= OnChannelsChanged;
            _console.MastersChanged -= OnMastersChanged;
            _console.MemoriesChanged -= OnMemoriesChanged;
            _console.OutputsChanged -= OnOutputsChanged;
            _console.LcdChanged -= OnLcdChanged;
            _console.CrossfaderChanged -= OnCrossfaderChanged;
            _console.StateChanged -= OnStateChanged;
            _console.StateAtConnectChanged -= OnStateAtConnectChanged;
            _console.ConsoleShowChanged -= OnConsoleShowChanged;
            _console.ErrorOccurred -= OnErrorOccurred;
        }
    }

    private void OnConnectionChanged(ConnectionState state)
    {
        // Outputs held for the next tick belong to the connection that delivered them: they must not
        // reach the UI after the state reset that a connection change carries.
        lock (_gate)
        {
            _latestOutputs = null;
        }

        Send("connectionStateChanged", DtoMapper.ToDto(state));
    }

    private void OnChannelsChanged(IReadOnlyList<ChannelLevel> channels) =>
        Send("channelStateChanged", channels.Select(DtoMapper.ToDto).ToArray());

    private void OnMastersChanged(Masters masters) => Send("mastersChanged", DtoMapper.ToDto(masters));

    private void OnMemoriesChanged(IReadOnlyList<MemoryState> memories) =>
        Send("memoriesChanged", memories.Select(m => DtoMapper.ToDto(m, _console.Capabilities)).ToArray());

    private void OnStateChanged(ConsoleState state) => Send("consoleStateChanged", DtoMapper.ToDto(state));

    private void OnLcdChanged(IReadOnlyList<string> lines) => Send("lcdChanged", lines);

    private void OnStateAtConnectChanged(StateAtConnect state) => Send("stateAtConnectChanged", DtoMapper.ToDto(state));

    private void OnConsoleShowChanged(ConsoleShow show) => Send("consoleShowChanged", ConsoleShowMapper.ToDto(show));

    private void OnCrossfaderChanged(CrossfaderState crossfader) => Send("crossfaderChanged", DtoMapper.ToDto(crossfader));

    private void OnErrorOccurred(string message) => Send("errorOccurred", new ErrorDto(message, "device_error"));

    private void OnOutputsChanged(IReadOnlyList<byte> outputs)
    {
        lock (_gate)
        {
            _latestOutputs = outputs;
        }
    }

    private void Send(string method, object payload)
    {
        try
        {
            _ = _hub.Clients.All.SendAsync(method, payload).ContinueWith(
                t => _logger.LogWarning(t.Exception, "Broadcast of {Method} failed", method),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Broadcast of {Method} failed", method);
        }
    }
}
