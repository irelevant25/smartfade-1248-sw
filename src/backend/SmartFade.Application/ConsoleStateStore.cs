using SmartFade.Application.Ports;
using SmartFade.Domain;

namespace SmartFade.Application;

/// <summary>
/// Thread-safe store of desired and actual state. Desired values come from the user (via
/// <see cref="ConsoleService"/>), actual values only from the device. The two are never
/// merged: a command does not change the actual level, and an actual report does not change
/// the desired level. Until the device has reported (<see cref="ConsoleState.ActualStateReported"/>)
/// the actual values are placeholders. Masters, outputs and memory fader levels are kept as
/// actual values only (no desired copy yet; the UI holds its request until the device confirms).
/// </summary>
public sealed class ConsoleStateStore
{
    private readonly object _gate = new();
    private readonly ConsoleCapabilities _capabilities;
    private readonly DeviceCommands _supportedCommands;
    private readonly byte[] _desired;
    private readonly byte[] _actual;
    private readonly byte[] _output;
    private readonly bool[] _pending;
    private readonly MemoryState[] _memories;
    private Masters _masters;
    private byte[] _outputs;
    private ConnectionState _connection;
    private string? _showFileName;
    private string? _firmwareVersion;
    private bool _actualStateReported;
    private bool _mastersReported;
    private bool _channelsReported;
    private bool _outputsReported;
    private bool _memoriesReported;
    private bool _independentsReported;
    private int? _memoryPage;
    private PanelKeys? _keys;
    private IReadOnlyList<string>? _lcd;
    private CrossfaderState? _crossfader;

    public ConsoleStateStore(ConsoleCapabilities capabilities, ConnectionState initialConnection, DeviceCommands supportedCommands)
    {
        _capabilities = capabilities;
        _supportedCommands = supportedCommands;
        _desired = new byte[capabilities.Channels];
        _actual = new byte[capabilities.Channels];
        _output = new byte[capabilities.Channels];
        _pending = new bool[capabilities.Channels];
        // Placeholders until the device reports (ActualStateReported = false): not console state.
        _masters = new Masters(Level.Max, Level.Min, Blackout: false, Solo: false, Independent1: false, Independent2: false);
        _outputs = new byte[capabilities.DmxAddresses];
        _memories = ConsoleSnapshot.EmptyMemories(capabilities).Select(m => m with { Reported = false }).ToArray();
        _connection = initialConnection;
    }

    public ConsoleCapabilities Capabilities => _capabilities;

    public ConnectionState Connection
    {
        get
        {
            lock (_gate)
            {
                return _connection;
            }
        }
    }

    public ConsoleState GetState()
    {
        lock (_gate)
        {
            return new ConsoleState(
                _connection,
                _capabilities,
                _supportedCommands,
                BuildChannelsUnsafe(),
                _masters,
                (byte[])_outputs.Clone(),
                (MemoryState[])_memories.Clone(),
                _actualStateReported,
                _mastersReported,
                _channelsReported,
                _outputsReported,
                _memoriesReported,
                _independentsReported,
                _memoryPage,
                _keys,
                _lcd,
                _crossfader,
                _showFileName,
                _firmwareVersion);
        }
    }

    /// <summary>
    /// Records a connection change. Leaving the connected state clears every pending flag and
    /// forgets that the device ever reported; the channels whose pending flag was cleared are
    /// returned so that clients can be told.
    /// </summary>
    public IReadOnlyList<ChannelLevel> SetConnection(ConnectionState connection)
    {
        var cleared = new List<ChannelLevel>();
        lock (_gate)
        {
            _connection = connection;
            if (connection.Status != ConnectionStatus.Connected)
            {
                for (var i = 0; i < _pending.Length; i++)
                {
                    if (_pending[i])
                    {
                        _pending[i] = false;
                        cleared.Add(BuildChannelUnsafe(i));
                    }
                }

                _actualStateReported = false;
                _mastersReported = false;
                _channelsReported = false;
                _outputsReported = false;
                _memoriesReported = false;
                _independentsReported = false;
                _memoryPage = null;
                _keys = null;
                _lcd = null;
                _crossfader = null;
                for (var i = 0; i < _memories.Length; i++)
                {
                    _memories[i] = _memories[i] with { Reported = false };   // kept for display, no longer the console's word
                }

                _showFileName = null;
                _firmwareVersion = null;
            }
        }

        return cleared;
    }

    /// <summary>Records what the user asked for; returns the channel's new desired/actual pair.</summary>
    public ChannelLevel SetDesiredChannel(int channel, byte level)
    {
        lock (_gate)
        {
            var i = channel - 1;
            _desired[i] = level;
            _pending[i] = _actual[i] != level;
            return BuildChannelUnsafe(i);
        }
    }

    /// <summary>
    /// Applies an actual-state report from the device; returns the channels whose actual level,
    /// output or pending flag changed, and through the out parameters the masters, outputs and
    /// memories that changed (null when they did not).
    /// </summary>
    public IReadOnlyList<ChannelLevel> ApplyDeviceChange(
        DeviceStateChange change,
        out Masters? masters,
        out IReadOnlyList<byte>? outputs,
        out IReadOnlyList<MemoryState>? memories)
    {
        ArgumentNullException.ThrowIfNull(change);
        var changed = new SortedSet<int>();
        masters = null;
        outputs = null;
        memories = null;

        lock (_gate)
        {
            _actualStateReported = true;
            if (change.Snapshot is { } snapshot)
            {
                for (var i = 0; i < _actual.Length && i < snapshot.ChannelLevels.Count; i++)
                {
                    ApplyActualUnsafe(i, snapshot.ChannelLevels[i], changed);
                }

                ApplyOutputsUnsafe(snapshot.ChannelOutputs, changed);
                _masters = snapshot.Masters;
                _mastersReported = true;
                _independentsReported = true;   // a snapshot is the device's whole word, the independents included
                _channelsReported = true;
                _outputsReported = true;
                _memoriesReported = true;
                masters = _masters;
                _outputs = snapshot.Outputs.ToArray();
                outputs = (byte[])_outputs.Clone();
                memories = ApplyMemoriesUnsafe(snapshot.Memories);
                _showFileName = snapshot.ShowFileName;
                _firmwareVersion = snapshot.FirmwareVersion;
            }

            if (change.ChannelLevels is { } levels)
            {
                _channelsReported = true;
                foreach (var (channel, level) in levels)
                {
                    if (_capabilities.IsValidChannel(channel))
                    {
                        ApplyActualUnsafe(channel - 1, level, changed);
                    }
                }
            }

            if (change.ChannelOutputs is { } channelOutputs)
            {
                _channelsReported = true;
                ApplyOutputsUnsafe(channelOutputs, changed);
            }

            if (change.Masters is { } reportedMasters)
            {
                _masters = reportedMasters;
                _mastersReported = true;
                masters = reportedMasters;
            }

            if (change.Independents is { } independents)
            {
                _masters = _masters with { Independent1 = independents.Independent1, Independent2 = independents.Independent2 };
                _independentsReported = true;
                masters = _masters;
            }

            if (change.MemoryPage is { } page && _capabilities.IsValidMemoryPage(page))
            {
                _memoryPage = page;
            }

            if (change.Keys is { } keys)
            {
                _keys = keys;
            }

            if (change.Lcd is { } lcd)
            {
                _lcd = lcd;
            }

            if (change.Crossfader is { } crossfader)
            {
                _crossfader = crossfader;
            }

            if (change.ShowFileName is { } fileName)
            {
                _showFileName = fileName;
            }

            if (change.FirmwareVersion is { } firmware)
            {
                _firmwareVersion = firmware;
            }

            if (change.Outputs is { } reportedOutputs)
            {
                _outputsReported = true;
                _outputs = reportedOutputs.ToArray();
                outputs = (byte[])_outputs.Clone();
            }

            if (change.Memories is { } reportedMemories)
            {
                _memoriesReported = true;
                memories = ApplyMemoriesUnsafe(reportedMemories) ?? memories;
            }

            var list = new ChannelLevel[changed.Count];
            var n = 0;
            foreach (var index in changed)
            {
                list[n++] = BuildChannelUnsafe(index);
            }

            return list;
        }
    }

    private void ApplyActualUnsafe(int index, byte level, SortedSet<int> changed)
    {
        var wasPending = _pending[index];
        var previous = _actual[index];
        _actual[index] = level;
        _pending[index] = _desired[index] != level && wasPending;
        if (previous != level || wasPending != _pending[index])
        {
            changed.Add(index);
        }
    }

    private void ApplyOutputsUnsafe(IReadOnlyList<byte> channelOutputs, SortedSet<int> changed)
    {
        for (var i = 0; i < _output.Length && i < channelOutputs.Count; i++)
        {
            if (_output[i] != channelOutputs[i])
            {
                _output[i] = channelOutputs[i];
                changed.Add(i);
            }
        }
    }

    /// <summary>Replaces the listed memories; returns those that actually changed, or null.</summary>
    private IReadOnlyList<MemoryState>? ApplyMemoriesUnsafe(IReadOnlyList<MemoryState> reported)
    {
        List<MemoryState>? changed = null;
        foreach (var memory in reported)
        {
            if (!_capabilities.IsValidMemoryPage(memory.Page) || !_capabilities.IsValidMemoryFader(memory.Fader))
            {
                continue;
            }

            var index = _capabilities.MemoryIndex(memory.Page, memory.Fader);
            if (_memories[index] != memory)
            {
                _memories[index] = memory;
                (changed ??= new List<MemoryState>()).Add(memory);
            }
        }

        return changed;
    }

    private ChannelLevel BuildChannelUnsafe(int index) =>
        new(index + 1, _desired[index], _actual[index], _pending[index], _output[index]);

    private IReadOnlyList<ChannelLevel> BuildChannelsUnsafe()
    {
        var list = new ChannelLevel[_actual.Length];
        for (var i = 0; i < list.Length; i++)
        {
            list[i] = BuildChannelUnsafe(i);
        }

        return list;
    }
}
