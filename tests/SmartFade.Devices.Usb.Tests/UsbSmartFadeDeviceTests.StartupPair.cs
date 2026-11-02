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
    public void The_MASTER_probes_channel_is_the_first_of_the_shown_bank_that_no_DMX_address_names()
    {
        // E2's Patch (seq 212, F70): DMX 47 drives channel 50, DMX 97 channel 24; channels 47 and 49 are unpatched
        var patch = Body(E2, 212)[2..];

        Assert.Null(ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Channels1To24));
        Assert.Equal(47, ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Channels25To48));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsoleShowAssembler.UnpatchedChannel(patch, FaderBank.Memories));
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
        var clock = new VirtualClock();
        var console = new PairConsole(Body(E3, 97), E3Download.Value);
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
        Assert.Contains("every channel of channel mode, bank 1-24 is patched", last.MasterProbeReason, StringComparison.Ordinal);

        // the show of the download (SHOW04, F70-F77) and the state: every memory reported, 16 recorded (the others INFERRED empty)
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
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe not sent: every channel of channel mode, bank 1-24 is patched", StringComparison.Ordinal));
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
        Assert.Contains(log.Notes, n => n.StartsWith("MASTER probe (SC-4 clause 4): bump 23 of channel 47, unpatched", StringComparison.Ordinal));
        var snapshot = await device.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(0xFF, snapshot.Masters.Master);
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
    /// on (E8b seq 26999, block 53 steady blue, F114). No System write goes, and the transfer log and the state say why.
    /// </summary>
    [Theory]
    [InlineData("E9c", 94604, 94610, "NEXT mode is on")]
    [InlineData("E9d", 18715, 18719, "the console shows bank 25-48")]
    [InlineData("E9b", 796, 83953, "REC MEM is lit or blinking")]
    [InlineData("E8b", 0, 26999, "BLACKOUT is on")]
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
    /// after `07 00 00` the next free poll lists the captured download, whose fetches deliver its bodies; a key press runs the
    /// reaction set for its code; <see cref="Report"/> lists an object at the next free poll; every other poll is idle. After
    /// <see cref="EndAfterPolls"/> more polls it fails the next read (the session ends, as a replay's end does).
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
        private readonly List<byte[]> _payloads = new();
        private readonly List<int> _systemWritesAfterReplies = new();
        private DirtyObjectRecord? _fetched;
        private bool _payloadDue;
        private bool _downloadDue;
        private int _downloadServed;
        private int _replyHeaders;
        private int? _pauseAtRecord;
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
                    else if (data.Length == 7 && data[4] == 0x01 && data[6] == 0x01)
                    {
                        _onPress.TryGetValue(data[5], out reaction);
                    }
                }

                reaction?.Invoke();
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
