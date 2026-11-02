using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Domain;

namespace SmartFade.Devices.Simulated;

/// <summary>
/// In-process stand-in for a console. It exchanges no bytes and knows no protocol; it models
/// the console at the domain level so the UI and the application layer can be exercised
/// without hardware. Simulation model (console behaviour only where a fact is cited):
/// - channel fader levels, memory fader levels and masters are stored as commanded and
///   reported after a small latency. The reported channel level is the commanded fader value;
///   the channel output (after HTP, bumps and the MASTER) is reported separately, as the real
///   console's ChannelLevels object carries post-MASTER values (F96, F97);
/// - whether a 1248 accepts any PC-side command is UNKNOWN (H6, H24 open); every command here
///   exists for the simulator only (<see cref="SupportedCommands"/>);
/// - intensities pile on Highest Takes Precedence: a channel's look is the highest of its
///   channel fader and of every memory's content scaled by that memory's fader (HTP across
///   channel faders and memory faders is DOCUMENTED, F107; the proportional scaling of a memory
///   by its fader is the simulator's reading, INFERRED);
/// - a held bump adds the BUMPS level for a channel bump, or the memory's content scaled by BUMPS
///   for a memory bump (F6; F97 saw it at BUMPS 0xFF, the scaling below full is INFERRED); with
///   SOLO on the bumped content replaces the output and everything else is muted (F6: "replaces
///   the current output", "the other intensity channels will be temporarily muted"; the owner
///   saw a memory's lights go off when its bump was pressed with SOLO on and BUMPS at zero, E3
///   owner statements);
/// - REC MEM stores the look (channel faders and memory playback) scaled by the MASTER, held bumps
///   excluded, into the memory (F107: "Set up a look you want to record using the channel
///   faders. You may record the output from other memory faders as well"; the MASTER scaling is
///   OBSERVED once, E9b F145: fader 0x7C at MASTER 0x86 was stored as 0x41, which replaced the
///   earlier pre-MASTER reading on 2026-10-31; the exclusion of bumps is INFERRED); a memory whose
///   levels are all zero counts as deleted (F107);
/// - CLEAR: two presses set every channel fader to zero, three presses every memory fader
///   (F109); nothing is erased;
/// - the independents IND 1 and IND 2 drive DMX 97 and 98 by default (DOCUMENTED, F108) at full
///   while on (the console's "Set Level" default is not stated: full is INFERRED), untouched by
///   the MASTER, Blackout and SOLO (F5, F108: "unless they are set to GM", the default is read as
///   not GM, INFERRED);
/// - DMX outputs use the documented 1:1 default patch (console manual printed p.20) and are
///   the channel output proportionally limited by the MASTER (DOCUMENTED, F5): a plain product
///   with rounding, which fits the E3b rest points (0x7F at MASTER 0x7F) but is still a model
///   (H57 rejected; its successor is open);
/// - Blackout sends every intensity output to zero (DOCUMENTED, F5);
/// - the simulated console starts with the MASTER at full and BUMPS at zero (the positions the
///   owner's console reported at the start of E3, F82, F95), SOLO, Blackout and the independents
///   off and no memory recorded; a physical console starts wherever its faders stand.
/// Connection changes are a single state machine with a generation counter: a disconnect or
/// link loss during a connect cancels that connect's outcome, and a second connect while one is
/// in progress is ignored. Test hooks: <see cref="SimulateConsoleChange"/> (the console moved a
/// fader) and <see cref="SimulateLinkLoss"/>, plus <see cref="SimulationOptions.FailCommands"/>.
/// </summary>
public sealed class SimulatedSmartFadeDevice : ISmartFadeDevice
{
    /// <summary>"SmartFade defaults to the outputs 97 &amp; 98 for the independents" (console manual printed p.22, F108).</summary>
    private static readonly int[][] DefaultIndependentAddresses = [[97], [98]];

    private readonly object _gate = new();
    private readonly SimulationOptions _options;
    private readonly ILogger<SimulatedSmartFadeDevice> _logger;
    private readonly byte[] _channels;
    private readonly byte[] _channelOutputs;
    private readonly bool[] _channelBumps;
    private readonly byte[][] _memoryContents;
    private readonly byte[] _memoryFaders;
    private readonly bool[] _memoryBumps;
    private readonly Label[] _memoryLabels;
    private readonly byte[] _outputs;
    private readonly Patch _patch;
    private Masters _masters = new(Level.Max, Level.Min, Blackout: false, Solo: false, Independent1: false, Independent2: false);
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private int _generation;

    public SimulatedSmartFadeDevice(IOptions<SimulationOptions> options, ILogger<SimulatedSmartFadeDevice> logger)
    {
        _options = options.Value;
        _logger = logger;
        Capabilities = _options.Capabilities;
        _channels = new byte[Capabilities.Channels];
        _channelOutputs = new byte[Capabilities.Channels];
        _channelBumps = new bool[Capabilities.Channels];
        _memoryContents = new byte[Capabilities.Memories][];
        for (var i = 0; i < _memoryContents.Length; i++)
        {
            _memoryContents[i] = new byte[Capabilities.Channels];
        }

        _memoryFaders = new byte[Capabilities.Memories];
        _memoryBumps = new bool[Capabilities.Memories];
        _memoryLabels = new Label[Capabilities.Memories];
        Array.Fill(_memoryLabels, Label.Empty);
        _outputs = new byte[Capabilities.DmxAddresses];
        _patch = Patch.OneToOne(Capabilities);
    }

    public DeviceMode Mode => DeviceMode.Simulated;

    public string Description => $"Simulated {Capabilities.Model} (no hardware)";

    public ConnectionStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public ConsoleCapabilities Capabilities { get; }

    public DeviceCommands SupportedCommands => DeviceCommands.All;

    public event EventHandler<DeviceStateChange>? StateChanged;

    public event EventHandler<ConnectionState>? ConnectionChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        int generation;
        lock (_gate)
        {
            if (_status is ConnectionStatus.Connected or ConnectionStatus.Connecting)
            {
                _logger.LogDebug("Connect ignored: already {Status}", _status);
                return;
            }

            _status = ConnectionStatus.Connecting;
            generation = ++_generation;
        }

        RaiseConnection(ConnectionStatus.Connecting);
        try
        {
            await Task.Delay(_options.ConnectDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var reverted = false;
            lock (_gate)
            {
                if (_generation == generation && _status == ConnectionStatus.Connecting)
                {
                    _status = ConnectionStatus.Disconnected;
                    reverted = true;
                }
            }

            if (reverted)
            {
                RaiseConnection(ConnectionStatus.Disconnected);
            }

            throw;
        }

        lock (_gate)
        {
            if (_generation != generation)
            {
                return;   // a disconnect or link loss intervened; its state stands
            }

            _status = ConnectionStatus.Connected;
        }

        _logger.LogInformation("Simulated console online");
        RaiseConnection(ConnectionStatus.Connected);
        StateChanged?.Invoke(this, new DeviceStateChange(Snapshot: BuildSnapshot()));
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_status == ConnectionStatus.Disconnected)
            {
                return Task.CompletedTask;
            }

            _generation++;
            _status = ConnectionStatus.Disconnected;
        }

        _logger.LogInformation("Simulated console disconnected");
        RaiseConnection(ConnectionStatus.Disconnected);
        return Task.CompletedTask;
    }

    public Task<ConsoleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(BuildSnapshot());

    public async Task SetChannelLevelAsync(int channel, byte level, CancellationToken cancellationToken)
    {
        RequireChannel(channel);
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        lock (_gate)
        {
            _channels[channel - 1] = level;
            computed = RecomputeUnsafe();
        }

        Raise(new DeviceStateChange(
            ChannelLevels: new Dictionary<int, byte> { [channel] = level },
            ChannelOutputs: computed.ChannelOutputs,
            Outputs: computed.Outputs));
    }

    public Task SetMasterAsync(byte level, CancellationToken cancellationToken) =>
        ChangeMastersAsync(masters => masters with { Master = level }, cancellationToken);

    public Task SetBlackoutAsync(bool on, CancellationToken cancellationToken) =>
        ChangeMastersAsync(masters => masters with { Blackout = on }, cancellationToken);

    public Task SetBumpsAsync(byte level, CancellationToken cancellationToken) =>
        ChangeMastersAsync(masters => masters with { Bumps = level }, cancellationToken);

    public Task SetSoloAsync(bool on, CancellationToken cancellationToken) =>
        ChangeMastersAsync(masters => masters with { Solo = on }, cancellationToken);

    /// <summary>Not modelled: the simulator has no stack playback (<see cref="SupportedCommands"/> leaves it out, so the application refuses it first).</summary>
    public Task PressGoAsync(CancellationToken cancellationToken) => throw new NotSupportedException("The simulator has no stack playback yet.");

    /// <summary>Not modelled: the simulator's channel and memory faders are separate controls, there is no fader mode to switch.</summary>
    public Task PressFadersMemsAsync(CancellationToken cancellationToken) => throw new NotSupportedException("The simulator has no fader mode: its channel and memory faders are separate.");

    /// <summary>Not modelled, as <see cref="PressFadersMemsAsync"/>.</summary>
    public Task PressFaders1To24Async(CancellationToken cancellationToken) => throw new NotSupportedException("The simulator has no fader mode: its channel and memory faders are separate.");

    /// <summary>Not modelled, as <see cref="PressFadersMemsAsync"/>.</summary>
    public Task PressFaders25To48Async(CancellationToken cancellationToken) => throw new NotSupportedException("The simulator has no fader mode: its channel and memory faders are separate.");

    public Task SetIndependentAsync(int number, bool on, CancellationToken cancellationToken)
    {
        if (number < 1 || number > Capabilities.Independents)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        return ChangeMastersAsync(
            masters => number == 1 ? masters with { Independent1 = on } : masters with { Independent2 = on },
            cancellationToken);
    }

    public async Task BumpChannelAsync(int channel, bool pressed, CancellationToken cancellationToken)
    {
        RequireChannel(channel);
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        lock (_gate)
        {
            _channelBumps[channel - 1] = pressed;
            computed = RecomputeUnsafe();
        }

        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs));
    }

    public async Task SetMemoryLevelAsync(int page, int fader, byte level, CancellationToken cancellationToken)
    {
        var index = RequireMemory(page, fader);
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        MemoryState memory;
        lock (_gate)
        {
            _memoryFaders[index] = level;
            computed = RecomputeUnsafe();
            memory = MemoryStateUnsafe(index);
        }

        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs, Memories: new[] { memory }));
    }

    public async Task BumpMemoryAsync(int page, int fader, bool pressed, CancellationToken cancellationToken)
    {
        var index = RequireMemory(page, fader);
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        lock (_gate)
        {
            _memoryBumps[index] = pressed;
            computed = RecomputeUnsafe();
        }

        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs));
    }

    public async Task RecordMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        var index = RequireMemory(page, fader);
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        MemoryState memory;
        lock (_gate)
        {
            _memoryContents[index] = RecordedLookUnsafe();
            computed = RecomputeUnsafe();
            memory = MemoryStateUnsafe(index);
        }

        _logger.LogInformation("Simulated REC MEM into page {Page} fader {Fader}", page, fader);
        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs, Memories: new[] { memory }));
    }

    public async Task StoreMemoryAsync(int page, int fader, IReadOnlyList<byte> levels, CancellationToken cancellationToken)
    {
        var index = RequireMemory(page, fader);
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count != Capabilities.Channels)
        {
            throw new ArgumentException($"Expected {Capabilities.Channels} channel levels, got {levels.Count}.", nameof(levels));
        }

        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        MemoryState memory;
        lock (_gate)
        {
            _memoryContents[index] = levels.ToArray();
            computed = RecomputeUnsafe();
            memory = MemoryStateUnsafe(index);
        }

        _logger.LogInformation("Simulated EDIT MEM stored page {Page} fader {Fader}", page, fader);
        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs, Memories: new[] { memory }));
    }

    public Task<MemoryContent> GetMemoryAsync(int page, int fader, CancellationToken cancellationToken)
    {
        var index = RequireMemory(page, fader);
        EnsureConnected();
        lock (_gate)
        {
            return Task.FromResult(new MemoryContent(page, fader, (byte[])_memoryContents[index].Clone(), _memoryLabels[index]));
        }
    }

    public async Task ClearChannelLevelsAsync(CancellationToken cancellationToken)
    {
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        var levels = new Dictionary<int, byte>(_channels.Length);
        lock (_gate)
        {
            Array.Clear(_channels);
            for (var channel = 1; channel <= _channels.Length; channel++)
            {
                levels[channel] = Level.Min;
            }

            computed = RecomputeUnsafe();
        }

        _logger.LogInformation("Simulated CLEAR: channel faders to zero");
        Raise(new DeviceStateChange(ChannelLevels: levels, ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs));
    }

    public async Task ClearMemoryLevelsAsync(CancellationToken cancellationToken)
    {
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Computed computed;
        var changed = new List<MemoryState>();
        lock (_gate)
        {
            for (var i = 0; i < _memoryFaders.Length; i++)
            {
                if (_memoryFaders[i] != Level.Min)
                {
                    _memoryFaders[i] = Level.Min;
                    changed.Add(MemoryStateUnsafe(i));
                }
            }

            computed = RecomputeUnsafe();
        }

        _logger.LogInformation("Simulated CLEAR: memory faders to zero ({Count} were up)", changed.Count);
        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Outputs: computed.Outputs, Memories: changed));
    }

    /// <summary>Test hook: the simulated console's own fader moved (a console-side change, as on the real console).</summary>
    public void SimulateConsoleChange(int channel, byte level)
    {
        RequireChannel(channel);
        EnsureConnected();
        Computed computed;
        lock (_gate)
        {
            _channels[channel - 1] = level;
            computed = RecomputeUnsafe();
        }

        Raise(new DeviceStateChange(
            ChannelLevels: new Dictionary<int, byte> { [channel] = level },
            ChannelOutputs: computed.ChannelOutputs,
            Outputs: computed.Outputs));
    }

    /// <summary>Test hook: the link to the simulated console dropped unexpectedly.</summary>
    public void SimulateLinkLoss(string reason)
    {
        lock (_gate)
        {
            _generation++;
            _status = ConnectionStatus.Error;
        }

        _logger.LogWarning("Simulated link loss: {Reason}", reason);
        ConnectionChanged?.Invoke(this, new ConnectionState(ConnectionStatus.Error, Mode, Description, reason));
    }

    private async Task ChangeMastersAsync(Func<Masters, Masters> change, CancellationToken cancellationToken)
    {
        await BeforeCommandAsync(cancellationToken).ConfigureAwait(false);

        Masters masters;
        Computed computed;
        lock (_gate)
        {
            _masters = change(_masters);
            masters = _masters;
            computed = RecomputeUnsafe();
        }

        Raise(new DeviceStateChange(ChannelOutputs: computed.ChannelOutputs, Masters: masters, Outputs: computed.Outputs));
    }

    private void Raise(DeviceStateChange change) => StateChanged?.Invoke(this, change);

    private async Task BeforeCommandAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        await Task.Delay(_options.CommandLatencyMilliseconds, cancellationToken).ConfigureAwait(false);
        if (_options.FailCommands)
        {
            throw new InvalidOperationException("Simulated command failure (SimulationOptions.FailCommands).");
        }

        EnsureConnected();
    }

    private void EnsureConnected()
    {
        if (Status != ConnectionStatus.Connected)
        {
            throw new InvalidOperationException("The simulated console is not connected.");
        }
    }

    private void RequireChannel(int channel)
    {
        if (!Capabilities.IsValidChannel(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }
    }

    private int RequireMemory(int page, int fader)
    {
        if (!Capabilities.IsValidMemoryPage(page))
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (!Capabilities.IsValidMemoryFader(fader))
        {
            throw new ArgumentOutOfRangeException(nameof(fader));
        }

        return Capabilities.MemoryIndex(page, fader);
    }

    private void RaiseConnection(ConnectionStatus status) =>
        ConnectionChanged?.Invoke(this, new ConnectionState(status, Mode, Description));

    private ConsoleSnapshot BuildSnapshot()
    {
        lock (_gate)
        {
            var memories = new MemoryState[_memoryFaders.Length];
            for (var i = 0; i < memories.Length; i++)
            {
                memories[i] = MemoryStateUnsafe(i);
            }

            return new ConsoleSnapshot(
                Capabilities,
                (byte[])_channels.Clone(),
                (byte[])_channelOutputs.Clone(),
                _masters,
                (byte[])_outputs.Clone(),
                memories,
                ShowFileName: null,
                FirmwareVersion: null);
        }
    }

    private MemoryState MemoryStateUnsafe(int index) => new(
        Page: index / Capabilities.MemoriesPerPage + 1,
        Fader: index % Capabilities.MemoriesPerPage + 1,
        _memoryFaders[index],
        Recorded: Array.Exists(_memoryContents[index], level => level > 0),
        _memoryLabels[index]);

    /// <summary>
    /// Simulation model: channel output = the look (HTP of the channel fader and the memory
    /// faders) piled on with the held bumps at the BUMPS level, or the bumps alone under SOLO;
    /// then × MASTER / 255 (rounded), 0 under Blackout; DMX through the 1:1 patch; the
    /// independents' addresses at full while IND 1 / IND 2 are on, whatever the masters say.
    /// </summary>
    private Computed RecomputeUnsafe()
    {
        var anyBumpHeld = Array.IndexOf(_channelBumps, true) >= 0 || Array.IndexOf(_memoryBumps, true) >= 0;
        for (var c = 0; c < _channels.Length; c++)
        {
            var bump = BumpOfUnsafe(c);
            var level = _masters.Solo && anyBumpHeld ? bump : Math.Max(LookOfUnsafe(c), bump);
            _channelOutputs[c] = _masters.Blackout ? Level.Min : Scale(level, _masters.Master);
        }

        for (var address = 1; address <= _outputs.Length; address++)
        {
            var channel = _patch.ChannelAt(address);
            _outputs[address - 1] = channel == 0 ? Level.Min : _channelOutputs[channel - 1];
        }

        ApplyIndependentUnsafe(0, _masters.Independent1);
        ApplyIndependentUnsafe(1, _masters.Independent2);

        return new Computed((byte[])_channelOutputs.Clone(), (byte[])_outputs.Clone());
    }

    private void ApplyIndependentUnsafe(int index, bool on)
    {
        foreach (var address in DefaultIndependentAddresses[index])
        {
            if (Capabilities.IsValidDmxAddress(address))
            {
                _outputs[address - 1] = on ? Level.Max : Level.Min;
            }
        }
    }

    /// <summary>HTP of the channel fader and of every memory's content scaled by its fader (F107), before bumps and the MASTER.</summary>
    private byte LookOfUnsafe(int channelIndex)
    {
        var level = _channels[channelIndex];
        for (var m = 0; m < _memoryFaders.Length; m++)
        {
            if (_memoryFaders[m] > 0)
            {
                level = Math.Max(level, Scale(_memoryContents[m][channelIndex], _memoryFaders[m]));
            }
        }

        return level;
    }

    /// <summary>What the held bump buttons contribute to a channel: the BUMPS level for a channel bump, the memory's content scaled by BUMPS for a memory bump (F6, F97).</summary>
    private byte BumpOfUnsafe(int channelIndex)
    {
        var level = _channelBumps[channelIndex] ? _masters.Bumps : Level.Min;
        for (var m = 0; m < _memoryBumps.Length; m++)
        {
            if (_memoryBumps[m])
            {
                level = Math.Max(level, Scale(_memoryContents[m][channelIndex], _masters.Bumps));
            }
        }

        return level;
    }

    private byte[] LookUnsafe()
    {
        var look = new byte[_channels.Length];
        for (var c = 0; c < look.Length; c++)
        {
            look[c] = LookOfUnsafe(c);
        }

        return look;
    }

    /// <summary>
    /// The look REC MEM stores: the look after the MASTER. E9b (F145, OBSERVED once): channel fader 5 at 0x7C with
    /// the console's MASTER at 0x86 was stored as 0x41, the post-MASTER level the console reported for the channel.
    /// Whether BLACKOUT or SOLO change what is stored is UNKNOWN; they do not enter the recorded look here.
    /// </summary>
    private byte[] RecordedLookUnsafe()
    {
        var look = LookUnsafe();
        for (var c = 0; c < look.Length; c++)
        {
            look[c] = Scale(look[c], _masters.Master);
        }

        return look;
    }

    private static byte Scale(byte level, byte factor) =>
        (byte)Math.Round(level * factor / 255.0, MidpointRounding.AwayFromZero);

    private sealed record Computed(byte[] ChannelOutputs, byte[] Outputs);
}
