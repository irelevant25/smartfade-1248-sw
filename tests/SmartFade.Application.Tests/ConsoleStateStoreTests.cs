using SmartFade.Application.Ports;
using SmartFade.Domain;

namespace SmartFade.Application.Tests;

/// <summary>
/// Each "reported" flag follows its own part of the state: a read-only console delivers the
/// parts one object at a time, the UI says "not reported" for each part until then, and
/// everything is forgotten when the connection ends.
/// </summary>
public class ConsoleStateStoreTests
{
    private static ConsoleStateStore Connected()
    {
        var store = new ConsoleStateStore(
            ConsoleCapabilities.SmartFade1248,
            new ConnectionState(ConnectionStatus.Disconnected, DeviceMode.Hardware, "test"),
            DeviceCommands.None);
        store.SetConnection(new ConnectionState(ConnectionStatus.Connected, DeviceMode.Hardware, "test"));
        return store;
    }

    private static (bool Masters, bool Channels, bool Outputs, bool Memories) Flags(ConsoleStateStore store)
    {
        var state = store.GetState();
        return (state.MastersReported, state.ChannelsReported, state.OutputsReported, state.MemoriesReported);
    }

    private static void Apply(ConsoleStateStore store, DeviceStateChange change) =>
        store.ApplyDeviceChange(change, out _, out _, out _);

    [Fact]
    public void Each_part_sets_only_its_own_flag_and_the_end_of_the_connection_clears_them_all()
    {
        var store = Connected();
        Assert.Equal((false, false, false, false), Flags(store));

        Apply(store, new DeviceStateChange(Outputs: new byte[512]));
        Assert.Equal((false, false, true, false), Flags(store));

        Apply(store, new DeviceStateChange(Masters: new Masters(255, 0, Blackout: false, Solo: false, Independent1: false, Independent2: false)));
        Assert.Equal((true, false, true, false), Flags(store));

        Apply(store, new DeviceStateChange(ChannelLevels: new Dictionary<int, byte> { [1] = 10 }));
        Assert.Equal((true, true, true, false), Flags(store));

        Apply(store, new DeviceStateChange(Memories: new[] { new MemoryState(1, 1, 0, Recorded: true, Label.Empty) }));
        Assert.Equal((true, true, true, true), Flags(store));

        store.SetConnection(new ConnectionState(ConnectionStatus.Error, DeviceMode.Hardware, "test", "session ended"));
        Assert.Equal((false, false, false, false), Flags(store));
    }

    [Fact]
    public void File_name_and_firmware_are_forgotten_with_the_connection()
    {
        var store = Connected();
        Apply(store, new DeviceStateChange(ShowFileName: "SHOW05.ASC", FirmwareVersion: "3.0.2.9.0.43"));
        var reported = store.GetState();
        Assert.Equal("SHOW05.ASC", reported.ShowFileName);
        Assert.Equal("3.0.2.9.0.43", reported.FirmwareVersion);

        store.SetConnection(new ConnectionState(ConnectionStatus.Disconnected, DeviceMode.Hardware, "test"));
        var cleared = store.GetState();
        Assert.Null(cleared.ShowFileName);
        Assert.Null(cleared.FirmwareVersion);
        Assert.Equal((false, false, false, false), Flags(store));
    }

    [Fact]
    public void A_delivered_memory_is_reported_and_the_others_stay_placeholders_until_the_connection_ends()
    {
        var store = Connected();
        Assert.All(store.GetState().Memories, m => Assert.False(m.Reported));

        Apply(store, new DeviceStateChange(Memories: new[] { new MemoryState(1, 2, 0, Recorded: true, new Label("OPONA", "", "")) }));
        var state = store.GetState();
        Assert.True(state.MemoriesReported);
        Assert.True(state.Memories[1].Reported);
        Assert.Equal(287, state.Memories.Count(m => !m.Reported));

        store.SetConnection(new ConnectionState(ConnectionStatus.Disconnected, DeviceMode.Hardware, "test"));
        Assert.All(store.GetState().Memories, m => Assert.False(m.Reported));
    }

    [Fact]
    public void The_key_LEDs_and_the_independents_are_kept_until_the_connection_ends()
    {
        var store = Connected();
        Assert.Null(store.GetState().Keys);
        Assert.False(store.GetState().IndependentsReported);

        var keys = new PanelKeys(new KeyLed(KeyLight.Full, false), KeyLed.Off, KeyLed.Off, KeyLed.Off, KeyLed.Off, KeyLed.Off, KeyLed.Off);
        Apply(store, new DeviceStateChange(Keys: keys, Independents: new IndependentStates(true, false), MemoryPage: 12));
        var state = store.GetState();
        Assert.Equal(keys, state.Keys);
        Assert.True(state.IndependentsReported);
        Assert.True(state.Masters.Independent1);
        Assert.Equal(12, state.MemoryPage);

        store.SetConnection(new ConnectionState(ConnectionStatus.Disconnected, DeviceMode.Hardware, "test"));
        var cleared = store.GetState();
        Assert.Null(cleared.Keys);
        Assert.False(cleared.IndependentsReported);
        Assert.Null(cleared.MemoryPage);
    }
}
