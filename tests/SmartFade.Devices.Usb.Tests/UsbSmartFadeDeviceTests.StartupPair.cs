using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Communication.Capture;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb.Tests;

/// <summary>
/// The start-up pair on the device (safety check SC-4, 2026-11-02, evening): the switch, the pair's placement and gate, the
/// version gate, the refusals before the pair (clause 3), every control refused until the download's last record, the show
/// and the state the download fills, the MASTER probe (clause 4) and its refusals. The stand-in console
/// (<see cref="PairConsole"/>) answers `09 00 00` with a ConsoleVersion listing and `07 00 00` with a captured download,
/// serving the console's own bodies of that session (E2 or E3); its framing is hand-made like the other stand-ins' (F53, F54,
/// F57). Every byte compared with what the device sent comes from a committed capture.
/// </summary>
public partial class UsbSmartFadeDeviceTests
{
    private static readonly Lazy<CaptureFile> E2 = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl")));

    private static readonly Lazy<CaptureFile> E3 = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-29-e3-console-actions.jsonl")));

    /// <summary>E3's download (list seq 176, last record seq 804; 157 records, F182) and its ConsoleVersion (seq 97).</summary>
    private static readonly Lazy<CapturedDownload> E3Download = new(() => LoadDownload(E3.Value, 176, 804));

    /// <summary>E2's download (list seq 192, last record seq 820): SOLO lit with the MASTER below full (F185).</summary>
    private static readonly Lazy<CapturedDownload> E2Download = new(() => LoadDownload(E2.Value, 192, 820));

    private static readonly Lazy<CaptureFile> E12b = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.jsonl")));

    /// <summary>The Patch record of a download (F70: one Patch object, a = b = c = 0).</summary>
    private static readonly DirtyObjectRecord PatchRecord = new((int)HspObjectId.Patch, 0, 0, 0);

    /// <summary>
    /// E12b's edited Patch as the console delivered it in E12b's second download (seq 87317, F162; SmartSoft's W3 byte for byte,
    /// the E12b record): channel 47 on DMX 100, so every channel 1–48 has a DMX address (DMX 47 still drives channel 50; channel
    /// 49, IND 1, none). With E12's own edited Patch, the only captured kind with no unpatched channel in either bank.
    /// </summary>
    private static byte[] AllChannelsPatched() => Body(E12b, 87317);

    [Fact]
    public void The_start_up_pair_switch_needs_the_USB_mode_and_today()
    {
        var today = new DateOnly(2026, 11, 2);

        ServiceCollectionExtensions.ValidateStartupPairDate(null, usbMode: true, today);           // absent: no pair
        ServiceCollectionExtensions.ValidateStartupPairDate("2026-11-02", usbMode: true, today);
        Assert.Throws<InvalidOperationException>(() => ServiceCollectionExtensions.ValidateStartupPairDate("2026-11-01", usbMode: true, today));
        Assert.Throws<InvalidOperationException>(() => ServiceCollectionExtensions.ValidateStartupPairDate("2026-11-02", usbMode: false, today));
    }

    [Fact]
    public void The_MASTER_probes_channel_is_the_first_of_a_bank_that_no_DMX_address_names()
    {
        // E2's Patch (seq 212, F70): DMX 47 drives channel 50, DMX 97 channel 24; channels 47 and 49 are unpatched, so bank 1-24
        // has none and the probe takes bank 25-48's (the amendment of 2026-11-02, late evening); E12b's edited Patch has none in
        // either bank
        var patch = Body(E2, 212)[2..];
        var allPatched = AllChannelsPatched()[2..];

        Assert.Null(ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Channels1To24));
        Assert.Equal(47, ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Channels25To48));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Memories));
        Assert.Null(ConsoleShowAssembler.UnpatchedChannel(allPatched, FaderBank.Channels1To24));
        Assert.Null(ConsoleShowAssembler.UnpatchedChannel(allPatched, FaderBank.Channels25To48));
        Assert.Equal(50, allPatched[46]);
    }

    [Fact]
    public async Task Without_the_switch_or_with_a_stale_date_no_pair_is_sent_and_SC1s_session_is_unchanged()
    {
        foreach (var date in new string?[] { null, "2026-10-29" })   // the virtual clock's day is 2026-10-30
        {
            var console = new PairConsole(Body(E3, 97), E3Download.Value);
            var device = Create(() => console, new UsbDeviceOptions { StartupPairDate = date });
            var ended = WaitForStatus(device, ConnectionStatus.Error);
            Assert.False(device.StateAtConnectEnabled);
            Assert.DoesNotContain("state at Connect", device.Description, StringComparison.Ordinal);

            console.EndAfterPolls(120);   // past SmartSoft's #40 and #79
            await device.ConnectAsync(CancellationToken.None);
            await ended;

            Assert.False(device.LastSession!.SendsStartupPair);
            Assert.Equal(HspReadSession.MaxObservedPayloadLength, device.LastSession.MaxPayloadLength);
            Assert.Equal(HspStartupPairPhase.None, device.LastSession.StartupPair.Phase);
            Assert.Equal(0, device.LastSession.WritesSent);
            Assert.Empty(console.Payloads);
        }
    }

    [Fact]
    public async Task The_pair_goes_where_SmartSoft_sent_it_and_every_control_waits_for_the_downloads_last_record()
    {
        // E3's download with E12b's edited Patch (every channel 1-48 has a DMX address): the MASTER probe finds no channel in
        // either bank and does not run, so the UI asks for one MASTER movement and nothing but the pair and the UI's key goes
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value.With((PatchRecord, AllChannelsPatched())));
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        var states = new List<StateAtConnect>();
        ConsoleShow? show = null;
        device.StateChanged += (_, change) =>
        {
            lock (states)
            {
                if (change.StateAtConnect is { } state)
                {
                    states.Add(state);
                }

                show = change.ConsoleShow ?? show;
            }
        };
        Assert.True(device.StateAtConnectEnabled);
        Assert.Contains("state at Connect", device.Description, StringComparison.Ordinal);

        var paused = console.PauseAtDownloadRecord(100);
        await device.ConnectAsync(CancellationToken.None);
        await paused.WaitAsync(Timeout);

        // inside the download: every control is refused with that reason (SC-4 clause 2)
        Assert.Equal(HspStartupPairPhase.Downloading, device.LastSession!.StartupPair.Phase);
        foreach (var control in new Func<Task>[]
        {
            () => device.SetMasterAsync(0x80, CancellationToken.None),
            () => device.SetChannelLevelAsync(1, 0x10, CancellationToken.None),
            () => device.BumpChannelAsync(7, true, CancellationToken.None),
            () => device.SetSoloAsync(true, CancellationToken.None),
            () => device.PressGoAsync(CancellationToken.None),
            () => device.PressFadersMemsAsync(CancellationToken.None),
            () => device.SetMemoryLevelAsync(1, 1, 0x10, CancellationToken.None),
        })
        {
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(control);
            Assert.Contains("the console's state is being downloaded (SC-4 clause 2)", refused.Message, StringComparison.Ordinal);
        }

        console.OnPress(0x39, () =>
        {
            console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 29039));   // memory mode, as the console answered SmartSoft's MEMS
            console.Report((int)HspObjectId.LedStatus, Body(E9d, 29045));
        });
        console.Resume();
        await WaitUntil(() => device.LastSession!.StartupPair.Phase == HspStartupPairPhase.Done);
        await WaitUntil(() => states.LastOrDefault()?.MasterProbe == MasterProbePhase.NotPossible);

        // after its last record the pair's refusal is gone: the MASTER waits for FaderInfo as before (SC-2 condition 6), and a
        // FADERS key from bank 1-24 goes out
        var master = await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetMasterAsync(0x80, CancellationToken.None));
        Assert.Contains("FaderInfo", master.Message, StringComparison.Ordinal);
        await device.PressFadersMemsAsync(CancellationToken.None);
        await WaitUntil(() => console.Payloads.Count == 4);
        await device.DisconnectAsync(CancellationToken.None);

        // the pair is E3's own two payloads (seq 90, 173), first, and the key's press and release follow with pseq 2 and 3
        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                HspOutbound.KeyEvent(0x02, ClearedKey.FadersMems, pressed: true), HspOutbound.KeyEvent(0x03, ClearedKey.FadersMems, pressed: false),
            },
            console.Payloads.ToArray());
        Assert.Equal(new[] { HspReadSession.StartupPairFirstAfterReplies, HspReadSession.StartupPairSecondAfterReplies }, console.SystemWritesAfterReplies);

        StateAtConnect[] seen;
        lock (states)
        {
            seen = states.ToArray();
        }

        Assert.Equal(
            new[] { StateAtConnectPhase.Waiting, StateAtConnectPhase.Requesting, StateAtConnectPhase.Downloading, StateAtConnectPhase.Done },
            seen.Select(s => s.Phase).Distinct().ToArray());
        var last = seen[^1];
        Assert.Equal((157, 157), (last.Records, last.Delivered));
        Assert.Contains("every channel of both banks is patched", last.MasterProbeReason, StringComparison.Ordinal);

        // the show of the download (SHOW04, F70-F77, with E12b's Patch: DMX 47 still drives channel 50) and the state: every
        // memory reported, 16 recorded (the others INFERRED empty)
        Assert.NotNull(show);
        Assert.Equal("SHOW04.ASC", show.FileName);
        Assert.Equal(50, show.Patch!.ChannelAt(47));
        Assert.Equal(16, show.Memories.Count);
        Assert.Equal(19, show.Stack!.Steps.Count);
        Assert.Equal("KOTLIK", Assert.Single(show.Sequences).Label.Line1);
        Assert.Equal(96, show.ChannelLabels.Count);
        var snapshot = await device.GetSnapshotAsync(CancellationToken.None);
        Assert.All(snapshot.Memories, m => Assert.True(m.Reported));
        Assert.Equal(16, snapshot.Memories.Count(m => m.Recorded));
        Assert.Equal("3.0.2.9.0.43", snapshot.FirmwareVersion);

        Assert.Contains(log.Notes, n => n.StartsWith("start-up pair on (SC-4)", StringComparison.Ordinal));
        Assert.Contains(log.Notes, n => n.Contains("System 09 00 00 sent after reply header #40", StringComparison.Ordinal));
        Assert.Contains(log.Notes, n => n.Contains("the download listed 157 records in 792 bytes", StringComparison.Ordinal));
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe not sent: every channel of both banks is patched", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_MASTER_probe_taps_the_bump_of_the_first_unpatched_channel_of_the_shown_bank_once()
    {
        // E3's download with the console in bank 25-48 (E9d seq 18715 / 18719, served for the download's ConsoleStatus and
        // LedStatus): the pair went blind (nothing reported before it), and channel 47, unpatched in this show (E9d: "no DMX"),
        // is physical fader 23 there. Its bump goes as a tap, SmartSoft's bump 23 (E9d seq 44086 / 44199) with this session's
        // counter; the console then lists FaderInfo (E9b seq 11628), the MASTER.
        var clock = new VirtualClock();
        var download = E3Download.Value.With(
            (new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0), Body(E9d, 18715)),
            (new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0), Body(E9d, 18719)));
        var console = new PairConsole(Body(E3, 97), download);
        console.OnPress(0x16, () => console.Report((int)HspObjectId.FaderInfo, E9bBody(11628)));
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        var mastersReported = false;
        device.StateChanged += (_, change) =>
        {
            last = change.StateAtConnect ?? last;
            mastersReported |= change.Masters is not null;
        };

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => console.Payloads.Count == 4 && mastersReported);
        console.EndAfterPolls(80);
        await WaitUntil(() => device.Status != ConnectionStatus.Connected);

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 44086, 0x02), WithCounter(E9d, 44199, 0x03),
            },
            console.Payloads.ToArray());
        Assert.Equal(MasterProbePhase.Sent, last!.MasterProbe);
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe (SC-4 clause 4): bump 23 of channel 47, unpatched", StringComparison.Ordinal) && n.Contains("as a tap in channel mode, bank 25-48, the bank the console shows", StringComparison.Ordinal));
        Assert.Contains("MASTER probe done (SC-4 clause 4): the console shows channel mode, bank 25-48, as the download reported it", log.Notes);
        Assert.DoesNotContain(log.Notes, n => n.Contains("automatic bank switch", StringComparison.Ordinal));
        var snapshot = await device.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(0xFF, snapshot.Masters.Master);
    }

    /// <summary>
    /// The stand-in console answers the MASTER probe's keys as the 1248 answered SmartSoft's in E9d: FADERS 25-48 with
    /// ConsoleStatus seq 18715 (bank 25-48) and LedStatus seq 18719; FADERS 1-24 with ConsoleStatus seq 27840 (bank 1-24) and
    /// LedStatus seq 27848, its answer from bank 25-48 (from memory mode the same ConsoleStatus body, seq 44818); FADERS MEMS
    /// with seq 29039 (memory mode, page 1) and 29045; and the press and the release of bump 23 each with FaderInfo and
    /// ChannelLevels read as before the tap (F168: a 47–78 ms tap made the console list the live objects at the press and at
    /// the release, every fetched object equal to its value before the press): E9b seq 11628 (every fader at zero, the MASTER
    /// and BUMPS at full) and seq 11632 (every channel at zero). A key left unanswered gets no report (SC-5's 2 s bound).
    /// </summary>
    private static void AnswersTheProbe(PairConsole console, bool faders25To48 = true, bool faders1To24 = true)
    {
        if (faders25To48)
        {
            console.OnPress((byte)ClearedKey.Faders25To48, () =>
            {
                console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 18715));
                console.Report((int)HspObjectId.LedStatus, Body(E9d, 18719));
            });
        }

        if (faders1To24)
        {
            console.OnPress((byte)ClearedKey.Faders1To24, () =>
            {
                console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 27840));
                console.Report((int)HspObjectId.LedStatus, Body(E9d, 27848));
            });
        }

        console.OnPress((byte)ClearedKey.FadersMems, () =>
        {
            console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 29039));
            console.Report((int)HspObjectId.LedStatus, Body(E9d, 29045));
        });
        void Tap()
        {
            console.Report((int)HspObjectId.FaderInfo, E9bBody(11628));
            console.Report((int)HspObjectId.ChannelLevels, E9bBody(11632));
        }

        console.OnPress((byte)ClearedKey.Bump23, Tap);
        console.OnRelease((byte)ClearedKey.Bump23, Tap);
    }

    [Fact]
    public async Task From_bank_1_24_the_MASTER_probe_switches_to_bank_25_48_taps_channel_47s_bump_and_switches_back_once_the_taps_reports_are_in()
    {
        // The usual start, SC-4 clause 4 as amended on 2026-11-02, late evening: E3's download shows channel mode, bank 1-24,
        // whose 24 channels are all patched, and channel 47 is not (F70; physical fader 23 of bank 25-48). The device switches
        // there by SC-5's automatic switch (FADERS 25-48 as a tap, SmartSoft's E9d seq 18706 / 18711), taps bump 23 once the
        // console reported the bank (E9d seq 44086 / 44199), and only after the tap's reports were delivered switches back
        // (FADERS 1-24, E9d seq 27831 / 27836): the console is left in bank 1-24 as found. Every payload with this session's
        // counter; every control is refused while the probe runs, and the probe's refusal is gone after it.
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        var pipe = new TimedPipe(console, clock);
        var log = new RecordingSink();
        var device = Create(() => pipe, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        FaderBank? bank = null;
        device.StateChanged += (_, change) =>
        {
            last = change.StateAtConnect ?? last;
            bank = change.FaderBank ?? bank;
        };

        var switching = console.PauseAtPress((byte)ClearedKey.Faders25To48);
        await device.ConnectAsync(CancellationToken.None);
        await switching.WaitAsync(Timeout);
        foreach (var control in new Func<Task>[]
        {
            () => device.PressFaders1To24Async(CancellationToken.None),
            () => device.SetChannelLevelAsync(30, 0x10, CancellationToken.None),
            () => device.BumpChannelAsync(47, true, CancellationToken.None),
            () => device.SetSoloAsync(true, CancellationToken.None),
        })
        {
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(control);
            Assert.Contains("the MASTER probe is under way (SC-4 clause 4): the device taps the bump of channel 47 in channel mode, bank 25-48 and leaves the console in channel mode, bank 1-24", refused.Message, StringComparison.Ordinal);
        }

        console.Resume();
        await WaitUntil(() => log.Notes.Any(n => n.StartsWith("MASTER probe done", StringComparison.Ordinal)));
        var after = await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressFaders1To24Async(CancellationToken.None));
        Assert.Contains("FADERS 1-24 was sent from a PC only from memory mode or bank 25-48", after.Message, StringComparison.Ordinal);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 18706, 0x02), WithCounter(E9d, 18711, 0x03),
                WithCounter(E9d, 44086, 0x04), WithCounter(E9d, 44199, 0x05),
                WithCounter(E9d, 27831, 0x06), WithCounter(E9d, 27836, 0x07),
            },
            console.Payloads.ToArray());
        Assert.Equal(FaderBank.Channels1To24, bank);
        Assert.Equal(MasterProbePhase.Sent, last!.MasterProbe);
        Assert.Equal(0xFF, (await device.GetSnapshotAsync(CancellationToken.None)).Masters.Master);

        // the switch back went only once the tap's reports were in: every FaderInfo and ChannelLevels the console listed after
        // the bump's release was fetched before FADERS 1-24's header (a write otherwise goes before pending fetches)
        var transfers = Recorded(pipe);
        var release = transfers.FindIndex(t => t.ToConsole && t.Data.Length == 7 && t.Data[4] == 0x01 && t.Data[5] == (byte)ClearedKey.Bump23 && t.Data[6] == 0x00);
        var back = transfers.FindIndex(t => t.ToConsole && t.Data.Length == 7 && t.Data[4] == 0x01 && t.Data[5] == (byte)ClearedKey.Faders1To24 && t.Data[6] == 0x01) - 1;
        Assert.True(release > 0 && IsDataHeader(transfers[back]) && back > release);
        var listedAfterRelease = 0;
        for (var i = release; i < back; i++)
        {
            var data = transfers[i].Data;
            if (transfers[i].ToConsole || data.Length < 7 || data[4] != (int)HspObjectId.DirtyObjects || data[2] + (data[3] << 8) != data.Length - 5)
            {
                continue;
            }

            for (var r = 0; r < ((data[5] << 8) | data[6]); r++)
            {
                int id = data[7 + 5 * r];
                if (id is (int)HspObjectId.FaderInfo or (int)HspObjectId.ChannelLevels)
                {
                    listedAfterRelease++;
                    Assert.Contains(transfers.Skip(i).Take(back - i), t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == id);
                }
            }
        }

        Assert.True(listedAfterRelease >= 2, "the console's report of the release was not listed before the switch back");

        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe (SC-4 clause 4): bump 23 of channel 47, unpatched", StringComparison.Ordinal)
            && n.Contains("as a tap in channel mode, bank 25-48, after SC-5's automatic switch from channel mode, bank 1-24 (FADERS 25-48) and, once the tap's reports are in, back to channel mode, bank 1-24 as the download reported it (FADERS 1-24)", StringComparison.Ordinal));
        Assert.Contains(log.Notes, n => n.StartsWith("automatic bank switch (SC-5 condition 1) for the MASTER probe (bump 47): FADERS 25-48 sent, from channel mode, bank 1-24 to channel mode, bank 25-48", StringComparison.Ordinal));
        Assert.Contains("MASTER probe: the console reported the tap of bump 47 (the MASTER probe), the MASTER reads 0xFF (FaderInfo body[194]); the switch back to channel mode, bank 1-24 follows", log.Notes);
        Assert.Contains(log.Notes, n => n.StartsWith("automatic bank switch (SC-5 condition 1) for the MASTER probe's return to channel mode, bank 1-24: FADERS 1-24 sent, from channel mode, bank 25-48 to channel mode, bank 1-24", StringComparison.Ordinal));
        Assert.Contains("MASTER probe done (SC-4 clause 4): the console shows channel mode, bank 1-24, as the download reported it", log.Notes);
    }

    [Fact]
    public async Task From_memory_mode_the_MASTER_probe_goes_through_bank_1_24_to_bank_25_48_and_back_to_memory_mode()
    {
        // E3's download with the console in memory mode, page 1 (E9d seq 29039 / 29045 served for its ConsoleStatus and
        // LedStatus). Channel 47 is the only unpatched channel and sits in bank 25-48; no PC was ever seen switching memory mode
        // and bank 25-48 directly (none in any capture), so the probe goes memory mode → bank 1-24 → bank 25-48 (FADERS 1-24 as
        // SmartSoft sent it from memory mode, E9d seq 44811 / 44820; FADERS 25-48, 18706 / 18711), taps bump 23 (44086 / 44199)
        // and comes back bank 25-48 → bank 1-24 → memory mode (FADERS 1-24, 27831 / 27836; FADERS MEMS, 29032 / 29041), each
        // switch after the console's report of the one before: the console is left in memory mode, page 1, as found.
        var clock = new VirtualClock();
        var download = E3Download.Value.With(
            (new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0), Body(E9d, 29039)),
            (new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0), Body(E9d, 29045)));
        var console = new PairConsole(Body(E3, 97), download);
        AnswersTheProbe(console);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        FaderBank? bank = null;
        device.StateChanged += (_, change) => bank = change.FaderBank ?? bank;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => log.Notes.Any(n => n.StartsWith("MASTER probe done", StringComparison.Ordinal)));
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 44811, 0x02), WithCounter(E9d, 44820, 0x03),
                WithCounter(E9d, 18706, 0x04), WithCounter(E9d, 18711, 0x05),
                WithCounter(E9d, 44086, 0x06), WithCounter(E9d, 44199, 0x07),
                WithCounter(E9d, 27831, 0x08), WithCounter(E9d, 27836, 0x09),
                WithCounter(E9d, 29032, 0x0A), WithCounter(E9d, 29041, 0x0B),
            },
            console.Payloads.ToArray());
        Assert.Equal(FaderBank.Memories, bank);
        Assert.Equal(4, log.Notes.Count(n => n.StartsWith("automatic bank switch done: the console reports", StringComparison.Ordinal)));
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe (SC-4 clause 4): bump 23 of channel 47, unpatched", StringComparison.Ordinal)
            && n.Contains("after SC-5's automatic switch from memory mode (FADERS 1-24, then FADERS 25-48) and, once the tap's reports are in, back to memory mode as the download reported it (FADERS 1-24, then FADERS MEMS)", StringComparison.Ordinal));
        Assert.Contains("MASTER probe done (SC-4 clause 4): the console shows memory mode, page 1, as the download reported it", log.Notes);
    }

    [Fact]
    public async Task When_a_switch_makes_the_console_report_FaderInfo_the_MASTER_probe_sends_no_tap_and_goes_home_from_there()
    {
        // From memory mode, page 1 (E9d seq 29039 / 29045 in E3's download) the first switch is FADERS 1-24, which the console
        // answers as it answered SmartSoft's from memory mode at E9c seq 56947: ConsoleStatus seq 56954, FaderInfo seq 56960 (the
        // MASTER at full, record 1 at 0x87) and LedStatus seq 56964, all three named by the list at seq 56950 (OBSERVED; first
        // lists naming FaderInfo after a mode switch also at E9 seq 75726, E9b 80262, E9d 50035). Clause 4's own condition (only
        // while FaderInfo has not been reported) no longer holds: neither FADERS 25-48 nor the tap goes, and the probe goes home
        // from bank 1-24, FADERS MEMS (E9d seq 29032 / 29041).
        var clock = new VirtualClock();
        var download = E3Download.Value.With(
            (new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0), Body(E9d, 29039)),
            (new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0), Body(E9d, 29045)));
        var console = new PairConsole(Body(E3, 97), download);
        AnswersTheProbe(console);
        console.OnPress((byte)ClearedKey.Faders1To24, () =>
        {
            console.Report((int)HspObjectId.ConsoleStatus, Body(E9c, 56954));
            console.Report((int)HspObjectId.FaderInfo, Body(E9c, 56960));
            console.Report((int)HspObjectId.LedStatus, Body(E9c, 56964));
        });
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        FaderBank? bank = null;
        StateAtConnect? last = null;
        device.StateChanged += (_, change) =>
        {
            bank = change.FaderBank ?? bank;
            last = change.StateAtConnect ?? last;
        };

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => log.Notes.Any(n => n.StartsWith("MASTER probe done", StringComparison.Ordinal)));
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 44811, 0x02), WithCounter(E9d, 44820, 0x03),
                WithCounter(E9d, 29032, 0x04), WithCounter(E9d, 29041, 0x05),
            },
            console.Payloads.ToArray());
        Assert.Contains("MASTER probe: the console reported FaderInfo before the tap, the MASTER reads 0xFF (FaderInfo body[194]), so neither the rest of the way out nor the tap of bump 47 (the MASTER probe) is sent; the switch back to memory mode follows", log.Notes);
        Assert.Contains("MASTER probe done (SC-4 clause 4): the console shows memory mode, page 1, as the download reported it", log.Notes);
        Assert.Equal(FaderBank.Memories, bank);
        Assert.Equal(MasterProbePhase.Sent, last!.MasterProbe);
    }

    /// <summary>
    /// The clause's SOLO and BLACKOUT refusals are asked again when the probe has come to the channel's bank: the console answers
    /// FADERS 25-48 with bank 25-48 (E9d seq 18715) and LEDs that show SOLO lit (E9c seq 87881, block 52) or BLACKOUT on (E8b
    /// seq 26999, block 53 steady). The tap is not sent; the probe stops where it is, with no switch back, and the UI is told
    /// where the console was left (Stopped, the first review's MINOR 1) and asked for one movement with the reason.
    /// </summary>
    [Theory]
    [InlineData("E9c", 87881, "SOLO is not off (LED block 52)")]
    [InlineData("E8b", 26999, "BLACKOUT is on (LED block 53 steady, F114)")]
    public async Task SOLO_or_BLACKOUT_reported_with_the_switch_stops_the_MASTER_probe_before_its_tap(string capture, int ledStatusSeq, string reason)
    {
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        console.OnPress((byte)ClearedKey.Faders25To48, () =>
        {
            console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 18715));
            console.Report((int)HspObjectId.LedStatus, Body(capture == "E9c" ? E9c : E8b, ledStatusSeq));
        });
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => last?.MasterProbe == MasterProbePhase.Stopped);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(4, console.Payloads.Count);   // the pair and FADERS 25-48: no tap, no switch back
        Assert.Contains($"the MASTER probe stopped ({reason}", last!.MasterProbeReason, StringComparison.Ordinal);
        Assert.Contains("and did not switch back: the console shows channel mode, bank 25-48; the download reported channel mode, bank 1-24. Move one fader on the panel, or the MASTER", last.MasterProbeReason, StringComparison.Ordinal);
        Assert.Contains(log.Notes, n => n.StartsWith($"MASTER probe stopped (SC-4 clause 4): {reason}", StringComparison.Ordinal)
            && n.EndsWith("no retry and no switch back: the console shows channel mode, bank 25-48, the download reported channel mode, bank 1-24", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "the console did not report channel mode, bank 25-48 with its LEDs within 2 s of FADERS 25-48", 4, "channel mode, bank 1-24")]
    [InlineData(true, "the console did not report channel mode, bank 1-24 with its LEDs within 2 s of FADERS 1-24", 8, "channel mode, bank 25-48")]
    public async Task A_switch_of_the_MASTER_probe_the_console_does_not_report_within_two_seconds_ends_the_session_as_a_deviation(bool answersBank25To48, string error, int payloads, string leftIn)
    {
        // SC-5 condition 1 (c) for the probe's switches as for any: the stand-in does not answer FADERS 25-48 (the switch there)
        // or FADERS 1-24 (the switch back); two seconds after the key's press, on the loop's clock, the session ends with that
        // reason, nothing else is sent after the key's tap, and the log says where the console was last reported. The UI is
        // told too (the first review's MINOR 1): the outcome Stopped while the session still reports, and the session's error.
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console, faders25To48: answersBank25To48, faders1To24: false);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var state = await ended;

        Assert.Contains(error, state.Error, StringComparison.Ordinal);
        Assert.Contains("(SC-5 condition 1 c); the session ends as a deviation", state.Error, StringComparison.Ordinal);
        Assert.Contains($"; The MASTER probe was interrupted by the session's end (SC-4 clause 4): the console shows {leftIn} (its last report); the download reported channel mode, bank 1-24.", state.Error, StringComparison.Ordinal);
        Assert.Equal(MasterProbePhase.Stopped, last!.MasterProbe);
        Assert.Equal($"the MASTER probe was interrupted by the session's end and did not switch back: the console shows {leftIn} (its last report); the download reported channel mode, bank 1-24", last.MasterProbeReason);
        Assert.Equal(payloads, console.Payloads.Count);
        Assert.Contains(log.Notes, n => n.StartsWith($"MASTER probe interrupted by the session's end (SC-4 clause 4): the console was last reported in {leftIn}, the download reported channel mode, bank 1-24", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Disconnect_inside_the_MASTER_probe_tells_the_UI_where_the_console_was_left()
    {
        // The usual start (E3's download, bank 1-24); the operator disconnects while the probe's FADERS 25-48 (E9d seq 18706) is
        // being written. The press's release is owed and goes, nothing more of the probe is sent, and the Disconnect carries
        // where the console was last reported and where the download found it (the first review's MINOR 1).
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var disconnected = WaitForStatus(device, ConnectionStatus.Disconnected);

        var switching = console.PauseAtPress((byte)ClearedKey.Faders25To48);
        await device.ConnectAsync(CancellationToken.None);
        await switching.WaitAsync(Timeout);
        var disconnecting = device.DisconnectAsync(CancellationToken.None);
        console.Resume();
        await disconnecting;
        var state = await disconnected;

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 18706, 0x02), WithCounter(E9d, 18711, 0x03),
            },
            console.Payloads.ToArray());
        Assert.StartsWith("The MASTER probe was interrupted by the session's end (SC-4 clause 4): the console shows channel mode, bank ", state.Error, StringComparison.Ordinal);
        Assert.EndsWith(" (its last report); the download reported channel mode, bank 1-24. Check the FADERS keys on the console.", state.Error, StringComparison.Ordinal);
        Assert.Equal(MasterProbePhase.Stopped, last!.MasterProbe);
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe interrupted by the session's end (SC-4 clause 4)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Disconnect_inside_the_MASTER_probe_that_outlives_its_stop_timeout_still_tells_the_UI_where_the_console_was_left()
    {
        // The re-check's N4 (2026-11-03): as above, but the stand-in keeps the probe's FADERS 25-48 press (E9d seq 18706) inside
        // the pipe until the Disconnect has given up waiting (its stop timeout, 20 s, shortened here): the Disconnect reports
        // Error, and when the session does end, its late Disconnected state carries where the console was last reported and
        // where the download found it, as a Disconnect in time does. The same captured bytes as above.
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock, stopTimeout: TimeSpan.FromMilliseconds(200));
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var timedOut = WaitForStatus(device, ConnectionStatus.Error);
        var disconnected = WaitForStatus(device, ConnectionStatus.Disconnected);

        var switching = console.PauseAtPress((byte)ClearedKey.Faders25To48);
        await device.ConnectAsync(CancellationToken.None);
        await switching.WaitAsync(Timeout);
        await device.DisconnectAsync(CancellationToken.None);   // the press is still held inside the pipe: the wait gives up
        var stuck = await timedOut;

        Assert.Equal("The session did not stop; restart the backend before connecting again.", stuck.Error);
        Assert.False(disconnected.IsCompleted);
        console.Resume();
        var state = await disconnected;

        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 18706, 0x02), WithCounter(E9d, 18711, 0x03),
            },
            console.Payloads.ToArray());
        Assert.StartsWith("The MASTER probe was interrupted by the session's end (SC-4 clause 4): the console shows channel mode, bank ", state.Error, StringComparison.Ordinal);
        Assert.EndsWith(" (its last report); the download reported channel mode, bank 1-24. Check the FADERS keys on the console.", state.Error, StringComparison.Ordinal);
        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.Equal(MasterProbePhase.Stopped, last!.MasterProbe);
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe interrupted by the session's end (SC-4 clause 4)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_record_function_armed_during_the_MASTER_probe_stops_it_before_the_switch_back_and_nothing_more_goes()
    {
        // As the usual case, but the console answers bump 23's press with REC MEM lit too (LedStatus E9b seq 83953; SC-2
        // condition 5). The tap's release is owed and goes (a press is never left without its release); the tap's reports come
        // in, then the switch back is refused (no write while a record function is armed): the probe stops where it is, with no
        // retry and no switch back, and the log says where the console was left. FaderInfo came with the tap, so the MASTER is
        // known and the UI does not ask for a movement, but it is told that the probe stopped away from home (the outcome
        // Stopped with where the console is and where the download found it: the first review's MINOR 1); the next control
        // meets the record function's refusal, not the probe's.
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        console.OnPress((byte)ClearedKey.Bump23, () =>
        {
            console.Report((int)HspObjectId.FaderInfo, E9bBody(11628));
            console.Report((int)HspObjectId.ChannelLevels, E9bBody(11632));
            console.Report((int)HspObjectId.LedStatus, E9bBody(83953));
        });
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => log.Notes.Any(n => n.StartsWith("MASTER probe stopped", StringComparison.Ordinal)));
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressFaders1To24Async(CancellationToken.None));
        Assert.Contains("REC MEM is lit or blinking", refused.Message, StringComparison.Ordinal);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(6, console.Payloads.Count);
        Assert.Equal(WithCounter(E9d, 44199, 0x05), console.Payloads[^1]);   // the tap's release is the last write
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe stopped (SC-4 clause 4): REC MEM is lit or blinking", StringComparison.Ordinal)
            && n.EndsWith("; nothing more of it is sent, no retry and no switch back: the console shows channel mode, bank 25-48, the download reported channel mode, bank 1-24", StringComparison.Ordinal));
        Assert.Equal(MasterProbePhase.Stopped, last!.MasterProbe);
        Assert.Equal("the MASTER probe stopped (REC MEM is lit or blinking (SC-2 condition 5)) and did not switch back: the console shows channel mode, bank 25-48; the download reported channel mode, bank 1-24", last.MasterProbeReason);
        Assert.Equal(0xFF, (await device.GetSnapshotAsync(CancellationToken.None)).Masters.Master);
    }

    /// <summary>
    /// The clause's LED refusals before each switch of the probe (the first review's NIT 4; reading (18): no PC FADERS key was
    /// ever seen sent with SOLO lit or BLACKOUT on). SOLO lit is the LedStatus E9c seq 87881 (block 52, after SmartSoft's SOLO
    /// press at seq 87872), reported with the tap's press (from bank 1-24: the switch back is refused) or with the first switch
    /// from memory mode (FADERS 1-24 answered with ConsoleStatus E9d seq 27840: the second switch, FADERS 25-48, is refused).
    /// The probe stops there, with no switch back, and the UI is told where the console was left.
    /// </summary>
    [Theory]
    [InlineData(false, 6, "channel mode, bank 25-48", "channel mode, bank 1-24")]
    [InlineData(true, 4, "channel mode, bank 1-24", "memory mode")]
    public async Task SOLO_lit_before_a_switch_of_the_MASTER_probe_stops_it_before_that_switch(bool fromMemoryMode, int payloads, string shown, string home)
    {
        var clock = new VirtualClock();
        var download = fromMemoryMode
            ? E3Download.Value.With(
                (new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0), Body(E9d, 29039)),
                (new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0), Body(E9d, 29045)))
            : E3Download.Value;
        var console = new PairConsole(Body(E3, 97), download);
        AnswersTheProbe(console);
        if (fromMemoryMode)
        {
            console.OnPress((byte)ClearedKey.Faders1To24, () =>
            {
                console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 27840));
                console.Report((int)HspObjectId.LedStatus, Body(E9c, 87881));
            });
        }
        else
        {
            console.OnPress((byte)ClearedKey.Bump23, () =>
            {
                console.Report((int)HspObjectId.FaderInfo, E9bBody(11628));
                console.Report((int)HspObjectId.ChannelLevels, E9bBody(11632));
                console.Report((int)HspObjectId.LedStatus, Body(E9c, 87881));
            });
        }

        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => last?.MasterProbe == MasterProbePhase.Stopped);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(payloads, console.Payloads.Count);   // nothing after the refused switch's predecessor
        Assert.Contains("the MASTER probe stopped (SOLO is not off (LED block 52)", last!.MasterProbeReason, StringComparison.Ordinal);
        Assert.Contains($"and did not switch back: the console shows {shown}; the download reported {home}", last.MasterProbeReason, StringComparison.Ordinal);
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe stopped (SC-4 clause 4): SOLO is not off (LED block 52)", StringComparison.Ordinal)
            && n.EndsWith($"no retry and no switch back: the console shows {shown}, the download reported {home}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_control_asked_for_when_the_download_ends_meets_the_MASTER_probes_refusal_and_never_goes_before_it()
    {
        // The first review's MINOR 2: the probe is decided in the same lock that takes the download as complete, so by the time
        // anyone hears of the download's end (here: the state at Connect reported Done, on the session's thread) the probe is
        // under way and a SOLO tap is refused with its reason; the payloads are the probe's alone (E9d seq 18706 / 18711,
        // 44086 / 44199, 27831 / 27836 with this session's counter), as in the usual case.
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        string? solo = null;
        device.StateChanged += (_, change) =>
        {
            if (change.StateAtConnect is { Phase: StateAtConnectPhase.Done } && solo is null)
            {
                try
                {
                    device.SetSoloAsync(true, CancellationToken.None).GetAwaiter().GetResult();
                    solo = "queued";
                }
                catch (InvalidOperationException ex)
                {
                    solo = ex.Message;
                }
            }
        };

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => log.Notes.Any(n => n.StartsWith("MASTER probe done", StringComparison.Ordinal)));
        await device.DisconnectAsync(CancellationToken.None);

        Assert.StartsWith("Refused: the MASTER probe is under way (SC-4 clause 4)", solo, StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9d, 18706, 0x02), WithCounter(E9d, 18711, 0x03),
                WithCounter(E9d, 44086, 0x04), WithCounter(E9d, 44199, 0x05),
                WithCounter(E9d, 27831, 0x06), WithCounter(E9d, 27836, 0x07),
            },
            console.Payloads.ToArray());
    }

    /// <summary>
    /// The first review's MINOR 2, the other half: a control accepted at the download's last record itself (its own report,
    /// ChannelLevels, raised before the download is taken as complete) is in flight when the probe would start, so the probe
    /// does not start, with no retry: a SOLO tap of ours (its LED change unreported: SmartSoft's SOLO press and release, E9b
    /// seq 22165 / 22174) or an IND 1 tap queued (E9b seq 49358 / 49367), each with this session's counter. The UI asks for one
    /// movement with the reason; nothing of the probe goes before or after the tap.
    /// </summary>
    [Theory]
    [InlineData(true, 22165, 22174, "SOLO was pressed from the PC and the console has not reported its LED (block 52) since")]
    [InlineData(false, 49358, 49367, "1 write(s) of ours wait to be sent (a control taken at the download's last record): the probe does not start behind them")]
    public async Task A_control_taken_at_the_downloads_last_record_keeps_the_MASTER_probe_from_starting(bool solo, int pressSeq, int releaseSeq, string reason)
    {
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
        AnswersTheProbe(console);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        string? control = null;
        device.StateChanged += (_, change) =>
        {
            last = change.StateAtConnect ?? last;
            if (control is null && device.LastSession?.StartupPair.Phase == HspStartupPairPhase.Done)
            {
                try
                {
                    (solo ? device.SetSoloAsync(true, CancellationToken.None) : device.SetIndependentAsync(1, true, CancellationToken.None)).GetAwaiter().GetResult();
                    control = "queued";
                }
                catch (InvalidOperationException ex)
                {
                    control = ex.Message;
                }
            }
        };

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => last?.MasterProbe == MasterProbePhase.NotPossible && console.Payloads.Count == 4);
        console.EndAfterPolls(40);
        await WaitUntil(() => device.Status != ConnectionStatus.Connected);

        Assert.Equal("queued", control);
        Assert.Equal(
            new[]
            {
                Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out),
                WithCounter(E9b, pressSeq, 0x02), WithCounter(E9b, releaseSeq, 0x03),
            },
            console.Payloads.ToArray());
        Assert.Contains(reason, last!.MasterProbeReason, StringComparison.Ordinal);
        Assert.Contains(log.Notes, n => n.StartsWith($"MASTER probe not sent: {reason}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_MASTER_probe_does_not_switch_away_from_a_memory_page_other_than_1()
    {
        // E3's download with the console in memory mode on page 2 (ConsoleStatus E9c seq 42230, body[2] = 1; LedStatus E9d seq
        // 29045): channel 47 needs two switches, and every bank or mode switch a PC was seen making came on page 1 (whether the
        // page outlasts one is UNKNOWN), so the probe does not run and the UI asks for one MASTER movement: the pair alone goes.
        var clock = new VirtualClock();
        var download = E3Download.Value.With(
            (new DirtyObjectRecord((int)HspObjectId.ConsoleStatus, 0, 0, 0), Body(E9c, 42230)),
            (new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0), Body(E9d, 29045)));
        var console = new PairConsole(Body(E3, 97), download);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => last?.MasterProbe == MasterProbePhase.NotPossible);
        console.EndAfterPolls(60);
        await WaitUntil(() => device.Status != ConnectionStatus.Connected);

        Assert.Contains("the console shows memory page 2: every bank or mode switch a PC was seen making came on page 1", last!.MasterProbeReason, StringComparison.Ordinal);
        Assert.Equal(2, console.Payloads.Count);
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe not sent: the console shows memory page 2", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, "SOLO is not off")]        // E2's download: SOLO lit (F185)
    [InlineData(false, "live control is off")]   // a read-only session with the pair: the probe is a write
    public async Task The_MASTER_probe_does_not_run_under_SOLO_nor_without_live_control(bool live, string reason)
    {
        var clock = new VirtualClock();
        var console = live ? new PairConsole(Body(E2, 113), E2Download.Value) : new PairConsole(Body(E3, 97), E3Download.Value);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = live ? Today(clock) : null, StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;

        await device.ConnectAsync(CancellationToken.None);
        await WaitUntil(() => last?.MasterProbe == MasterProbePhase.NotPossible);
        console.EndAfterPolls(60);
        await WaitUntil(() => device.Status != ConnectionStatus.Connected);

        Assert.Contains(reason, last!.MasterProbeReason, StringComparison.Ordinal);
        Assert.Equal(2, console.Payloads.Count);   // the pair, and nothing else
        Assert.Contains(log.Notes, n => n.StartsWith($"MASTER probe not sent: {reason}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_version_other_than_SmartSofts_ends_the_pair_before_07_and_the_session_goes_on()
    {
        // a ConsoleVersion of the observed length that does not read "3.0.2.9.0.43" (a zero body stands in for another version)
        var clock = new VirtualClock();
        var console = new PairConsole(new byte[13], E3Download.Value);
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        console.EndAfterPolls(150);
        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Equal(new[] { Frame(E3.Value, 90, CaptureDirection.Out) }, console.Payloads.ToArray());   // `09`, never `07`
        Assert.Equal(StateAtConnectPhase.NotSent, last!.Phase);
        Assert.Contains("the console's version is not the one SmartSoft's pair was observed with", last.Reason, StringComparison.Ordinal);
        Assert.Equal(MasterProbePhase.NotPossible, last.MasterProbe);
        Assert.Contains(log.Notes, n => n.StartsWith("start-up pair ended without the download: the console's version is not the one", StringComparison.Ordinal));
        Assert.Contains("end of the stand-in", end.Error, StringComparison.Ordinal);   // the session went on until the stand-in's script ended
        Assert.Null(device.LastSession!.Fault);
    }

    [Fact]
    public async Task ConsoleVersion_not_listed_within_two_poll_replies_ends_the_pair_before_07()
    {
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value) { ListsNoVersion = true };
        var device = Create(() => console, new UsbDeviceOptions { StartupPairDate = Today(clock) }, clock: clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        console.EndAfterPolls(150);
        await device.ConnectAsync(CancellationToken.None);
        await ended;

        Assert.Single(console.Payloads);
        Assert.Equal(StateAtConnectPhase.NotSent, last!.Phase);
        Assert.Contains("did not list ConsoleVersion within 2 poll replies after 09 00 00", last.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// SC-4 clause 3: what the console reported at Connect (its list pending then, F177 / H65) shows a state no pair was seen in
    /// (F185): NEXT mode (E9c seq 94604, body[6] = 1, F173), bank 25-48 (E9d seq 18715, F98), REC MEM lit (E9b seq 83953), BLACKOUT
    /// on (E8b seq 26999, block 53 steady blue, F114), memory page 2 (E9c seq 42230, `01 00 01 00 00 01 00`, body[2] = 1, F113; with
    /// the LedStatus the console delivered right after it, seq 42242; added 2026-11-03 for the first review's MAJOR 1), memory
    /// page 12 in channel mode (E9b seq 58735, `00 00 0b 00 00 01 00`, body[0] = 0, body[2] = 0x0B, F113; with the LedStatus the
    /// console delivered after it in the same list, seq 58751; added 2026-11-03 for the re-check's N5 (b): the page refusal does
    /// not depend on the mode). E9b's only channel-mode page-12 bodies (58735, 58743) came during the console's own EDIT MEM of
    /// memory 1 on page 12 (the E9b record), so 58751 shows EDIT MEM blinking (block 67 `FF 37 37 0A 0A 0A`); the ConsoleStatus
    /// is asked first, so the reason is the page's. No System write goes, and the transfer log and the state say why.
    /// </summary>
    [Theory]
    [InlineData("E9c", 94604, 94610, "NEXT mode is on")]
    [InlineData("E9d", 18715, 18719, "the console shows bank 25-48")]
    [InlineData("E9b", 796, 83953, "REC MEM is lit or blinking")]
    [InlineData("E8b", 0, 26999, "BLACKOUT is on")]
    [InlineData("E9c", 42230, 42242, "the console shows memory page 2 (ConsoleStatus body[2] = 1, F113): no pair was ever seen on a page other than 1 (F185)")]
    [InlineData("E9b", 58735, 58751, "the console shows memory page 12 (ConsoleStatus body[2] = 11, F113): no pair was ever seen on a page other than 1 (F185)")]
    public async Task The_pair_is_refused_when_the_console_already_reported_a_state_no_pair_was_seen_in(string capture, int consoleStatusSeq, int ledStatusSeq, string reason)
    {
        var source = capture switch
        {
            "E9c" => E9c,
            "E9d" => E9d,
            "E9b" => E9b,
            _ => E8b,
        };
        var atConnect = new List<(int, byte[])>();
        if (consoleStatusSeq > 0)
        {
            atConnect.Add(((int)HspObjectId.ConsoleStatus, Body(source, consoleStatusSeq)));
        }

        atConnect.Add(((int)HspObjectId.LedStatus, Body(source, ledStatusSeq)));
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value, atConnect.ToArray());
        var log = new RecordingSink();
        var device = Create(() => console, new UsbDeviceOptions { LiveControlDate = Today(clock), StartupPairDate = Today(clock) }, _ => log, clock);
        StateAtConnect? last = null;
        device.StateChanged += (_, change) => last = change.StateAtConnect ?? last;
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        console.EndAfterPolls(150);
        await device.ConnectAsync(CancellationToken.None);
        await ended;

        Assert.Empty(console.Payloads);
        Assert.Equal(StateAtConnectPhase.NotSent, last!.Phase);
        Assert.Contains(reason, last.Reason, StringComparison.Ordinal);
        Assert.Contains("(SC-4 clause 3)", last.Reason, StringComparison.Ordinal);
        Assert.Contains(log.Notes, n => n.StartsWith("start-up pair ended without the download: 09 00 00 refused: ", StringComparison.Ordinal) && n.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_pair_per_Connect_never_resent_inside_the_session()
    {
        var clock = new VirtualClock();
        var consoles = new List<PairConsole>();
        var device = Create(
            () =>
            {
                var console = new PairConsole(Body(E3, 97), E3Download.Value);
                consoles.Add(console);
                return console;
            },
            new UsbDeviceOptions { StartupPairDate = Today(clock) },
            clock: clock);

        for (var connect = 1; connect <= 2; connect++)
        {
            await device.ConnectAsync(CancellationToken.None);
            await WaitUntil(() => device.LastSession?.StartupPair.Phase == HspStartupPairPhase.Done && consoles.Count == connect);
            consoles[^1].EndAfterPolls(200);   // many replies past the download: the pair is never sent again
            await WaitUntil(() => device.Status == ConnectionStatus.Error);
        }

        Assert.All(consoles, console => Assert.Equal(new[] { Frame(E3.Value, 90, CaptureDirection.Out), Frame(E3.Value, 173, CaptureDirection.Out) }, console.Payloads.ToArray()));
    }

    /// <summary>A captured download: its list body and the body the console delivered for each record.</summary>
    private sealed record CapturedDownload(byte[] ListBody, IReadOnlyDictionary<DirtyObjectRecord, byte[]> Bodies)
    {
        /// <summary>The same download with some records' bodies taken from elsewhere (also from a committed capture).</summary>
        public CapturedDownload With(params (DirtyObjectRecord Record, byte[] Body)[] replacements)
        {
            var bodies = Bodies.ToDictionary(b => b.Key, b => b.Value);
            foreach (var (record, body) in replacements)
            {
                Assert.True(bodies.ContainsKey(record));
                bodies[record] = body;
            }

            return this with { Bodies = bodies };
        }
    }

    /// <summary>The download list at <paramref name="listSeq"/> and the object bodies fetched for it up to <paramref name="lastSeq"/>.</summary>
    private static CapturedDownload LoadDownload(CaptureFile capture, int listSeq, int lastSeq)
    {
        var frames = capture.BulkFrames.Where(f => f.Seq >= listSeq && f.Seq <= lastSeq).ToArray();
        var bodies = new Dictionary<DirtyObjectRecord, byte[]>();
        DirtyObjectRecord? fetched = null;
        int? payloadDue = null;
        var objectReply = false;
        foreach (var frame in frames.Skip(1))
        {
            if (frame.Direction == CaptureDirection.Out)
            {
                if (frame.Data.Length == 12 && frame.Data[0] == 2)
                {
                    fetched = new DirtyObjectRecord(frame.Data[4] | (frame.Data[5] << 8), frame.Data[6] | (frame.Data[7] << 8), frame.Data[8] | (frame.Data[9] << 8), frame.Data[10] | (frame.Data[11] << 8));
                }

                continue;
            }

            if (payloadDue is null)
            {
                var length = frame.Data[2] | (frame.Data[3] << 8);
                objectReply = frame.Data[0] == 3;
                payloadDue = length > 0 ? length : null;
                continue;
            }

            if (objectReply && fetched is { } record)
            {
                bodies[record] = frame.Data[5..];
            }

            payloadDue = null;
        }

        var list = frames[0].Data[5..];
        Assert.Equal(DirtyObjectsList.Decode(list).Records.Count, bodies.Count);
        return new CapturedDownload(list, bodies);
    }

    /// <summary>
    /// A stand-in console for the start-up pair. The first poll lists the objects given for Connect (if any); after `09 00 00`
    /// the next free poll lists ConsoleVersion (unless <see cref="ListsNoVersion"/>), whose fetch delivers the version body given;
    /// after `07 00 00` the next free poll lists the captured download, whose fetches deliver its bodies; a key press or release
    /// runs the reaction set for its code; <see cref="Report"/> lists an object at the next free poll; every other poll is idle.
    /// After <see cref="EndAfterPolls"/> more polls it fails the next read (the session ends, as a replay's end does).
    /// </summary>
    private sealed class PairConsole : IHspPipe
    {
        private readonly object _lock = new();
        private readonly byte[] _version;
        private readonly CapturedDownload _download;
        private readonly Queue<byte[]> _replies = new();
        private readonly Dictionary<DirtyObjectRecord, byte[]> _bodies = new();
        private readonly List<DirtyObjectRecord> _dirty = new();
        private readonly Dictionary<byte, Action> _onPress = new();
        private readonly Dictionary<byte, Action> _onRelease = new();
        private readonly List<byte[]> _payloads = new();
        private readonly List<int> _systemWritesAfterReplies = new();
        private DirtyObjectRecord? _fetched;
        private bool _payloadDue;
        private bool _downloadDue;
        private int _downloadServed;
        private int _replyHeaders;
        private int? _pauseAtRecord;
        private byte? _pauseAtPress;
        private int? _endAfter;
        private ManualResetEventSlim? _paused;
        private TaskCompletionSource? _pausedSignal;
        private byte _sequence;

        public PairConsole(byte[] version, CapturedDownload download, params (int Id, byte[] Body)[] atConnect)
        {
            _version = version;
            _download = download;
            foreach (var (id, body) in atConnect)
            {
                Report(id, body);
            }
        }

        /// <summary>The console never lists ConsoleVersion after `09` (a deviation no capture shows).</summary>
        public bool ListsNoVersion { get; init; }

        /// <summary>The Data payloads written, in order.</summary>
        public IReadOnlyList<byte[]> Payloads
        {
            get
            {
                lock (_lock)
                {
                    return _payloads.ToArray();
                }
            }
        }

        /// <summary>For each System write, the reply headers the console had sent before it.</summary>
        public IReadOnlyList<int> SystemWritesAfterReplies
        {
            get
            {
                lock (_lock)
                {
                    return _systemWritesAfterReplies.ToArray();
                }
            }
        }

        public string Description => "stand-in console for the start-up pair";

        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Dispose()
        {
            Close();
            Resume();
        }

        public void Report(int id, byte[] body)
        {
            lock (_lock)
            {
                var record = new DirtyObjectRecord(id, 0, 0, 0);
                _bodies[record] = body;
                if (!_dirty.Contains(record))
                {
                    _dirty.Add(record);
                }
            }
        }

        public void OnPress(byte code, Action reaction)
        {
            lock (_lock)
            {
                _onPress[code] = reaction;
            }
        }

        public void OnRelease(byte code, Action reaction)
        {
            lock (_lock)
            {
                _onRelease[code] = reaction;
            }
        }

        /// <summary>The next press of <paramref name="code"/> waits inside the pipe, after its reaction ran, until <see cref="Resume"/>.</summary>
        public Task PauseAtPress(byte code)
        {
            lock (_lock)
            {
                _pauseAtPress = code;
                _paused = new ManualResetEventSlim(false);
                _pausedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return _pausedSignal.Task;
            }
        }

        /// <summary>The poll that would deliver the download's <paramref name="record"/>-th record waits until <see cref="Resume"/>.</summary>
        public Task PauseAtDownloadRecord(int record)
        {
            lock (_lock)
            {
                _pauseAtRecord = record;
                _paused = new ManualResetEventSlim(false);
                _pausedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return _pausedSignal.Task;
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                _paused?.Set();
            }
        }

        /// <summary>After <paramref name="polls"/> more polls the next read fails: the session ends.</summary>
        public void EndAfterPolls(int polls)
        {
            lock (_lock)
            {
                _endAfter = _replyHeaders + polls;
            }
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            if (_payloadDue)
            {
                _payloadDue = false;
                Action? reaction = null;
                ManualResetEventSlim? pause = null;
                lock (_lock)
                {
                    _payloads.Add(data.ToArray());
                    if (HspOutbound.IsClearedSystemMessage(data))
                    {
                        _systemWritesAfterReplies.Add(_replyHeaders);
                        if (data[4] == (byte)ClearedSystemMessage.ListVersion && !ListsNoVersion)
                        {
                            var record = new DirtyObjectRecord((int)HspObjectId.ConsoleVersion, 0, 0, 0);
                            _bodies[record] = _version;
                            _dirty.Add(record);
                        }
                        else if (data[4] == (byte)ClearedSystemMessage.ListShow)
                        {
                            _downloadDue = true;
                        }
                    }
                    else if (data.Length == 7 && data[4] == 0x01)
                    {
                        (data[6] == 0x01 ? _onPress : _onRelease).TryGetValue(data[5], out reaction);
                        if (data[6] == 0x01 && _pauseAtPress == data[5])
                        {
                            _pauseAtPress = null;
                            pause = _paused;
                            _pausedSignal?.TrySetResult();
                        }
                    }
                }

                reaction?.Invoke();
                _ = pause?.Wait(Timeout);
                return;
            }

            if (data.Length == 12 && data[0] == 1)
            {
                _payloadDue = true;
                return;
            }

            if (data.Length == 12 && data[0] == 2)
            {
                _fetched = new DirtyObjectRecord(data[4] | (data[5] << 8), data[6] | (data[7] << 8), data[8] | (data[9] << 8), data[10] | (data[11] << 8));
                return;
            }

            ManualResetEventSlim? wait = null;
            lock (_lock)
            {
                if (_fetched is { } record && _pauseAtRecord is { } pauseAt && _download.Bodies.ContainsKey(record) && _downloadServed + 1 == pauseAt)
                {
                    _pauseAtRecord = null;
                    wait = _paused;
                    _pausedSignal?.TrySetResult();
                }
            }

            _ = wait?.Wait(Timeout);
            AnswerPoll();
        }

        public void ReadExactly(Span<byte> buffer)
        {
            if (_replies.Count == 0)
            {
                throw new HspPipeException("end of the stand-in console's script");
            }

            var reply = _replies.Dequeue();
            if (reply.Length != buffer.Length)
            {
                throw new HspPipeException($"answer has {reply.Length} bytes, {buffer.Length} requested");
            }

            reply.CopyTo(buffer);
        }

        private void AnswerPoll()
        {
            lock (_lock)
            {
                if (_endAfter is { } end && _replyHeaders >= end)
                {
                    return;   // nothing queued: the read fails, the session ends
                }

                _replyHeaders++;
                if (_fetched is { } record)
                {
                    _fetched = null;
                    var body = _bodies.TryGetValue(record, out var reported) ? reported : _download.Bodies[record];
                    if (_download.Bodies.ContainsKey(record) && !_bodies.ContainsKey(record))
                    {
                        _downloadServed++;
                    }

                    var payload = Payload(_sequence++, record.ObjectId, body);
                    _replies.Enqueue(Header(3, (ushort)payload.Length, (ushort)record.ObjectId, (ushort)record.A, (ushort)record.B, (ushort)record.C));
                    _replies.Enqueue(payload);
                    return;
                }

                if (_downloadDue)
                {
                    _downloadDue = false;
                    var list = Payload(_sequence++, (int)HspObjectId.DirtyObjects, _download.ListBody);
                    _replies.Enqueue(Header(4, (ushort)list.Length));
                    _replies.Enqueue(list);
                    return;
                }

                if (_dirty.Count > 0)
                {
                    var list = ListPayload(_sequence++, _dirty.ToArray());
                    _dirty.Clear();
                    _replies.Enqueue(Header(4, (ushort)list.Length));
                    _replies.Enqueue(list);
                    return;
                }
            }

            Thread.Sleep(1);   // idle: let the test's thread run
            lock (_lock)
            {
                _replies.Enqueue(Idle());
            }
        }
    }
}
