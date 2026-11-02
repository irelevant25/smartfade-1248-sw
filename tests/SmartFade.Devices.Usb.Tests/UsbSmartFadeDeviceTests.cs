using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Communication.Capture;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb.Tests;

/// <summary>
/// The read-only USB device run over committed captures through the replay pipe, never over
/// hardware: every transfer it sends is checked against SmartSoft's, the cadence against the
/// observed floors on a virtual clock, and the state it assembles against the experiment records.
/// </summary>
public partial class UsbSmartFadeDeviceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Every device here runs on a virtual clock that advances only by the delays the loop asks
    /// for, so the cadence costs no real time and no test ever waits on the console's timing.
    /// </summary>
    private static UsbSmartFadeDevice Create(
        Func<IHspPipe> pipeFactory,
        UsbDeviceOptions? options = null,
        Func<string, ITransferSink?>? logFactory = null,
        VirtualClock? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        IncompleteWriteMarker? marker = null)
    {
        clock ??= new VirtualClock();
        options ??= new UsbDeviceOptions();
        // hardware mode is confirmed per day: the virtual clock's day, unless a test says otherwise
        options.ConfirmDate ??= DateOnly.FromDateTime(clock.GetLocalNow().DateTime).ToString("yyyy-MM-dd");
        return new UsbSmartFadeDevice(
            Options.Create(options),
            pipeFactory,
            logFactory ?? (_ => new DiscardingSink()),
            NullLogger<UsbSmartFadeDevice>.Instance,
            clock,
            delay ?? ((wait, _) =>
            {
                clock.Advance(wait);
                return Task.CompletedTask;
            }),
            marker);
    }

    private static Task<ConnectionState> WaitForStatus(UsbSmartFadeDevice device, ConnectionStatus status)
    {
        var tcs = new TaskCompletionSource<ConnectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        device.ConnectionChanged += (_, state) =>
        {
            if (state.Status == status)
            {
                tcs.TrySetResult(state);
            }
        };
        return tcs.Task.WaitAsync(Timeout);
    }

    private static byte[] Idle() => new byte[] { 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    [Fact]
    public async Task Replays_E3b_sending_only_what_SmartSoft_sent_and_assembles_the_console_state()
    {
        var pipe = new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture("2026-10-30-e3b-live-objects.jsonl")));
        var device = Create(() => pipe);
        var changes = 0;
        var mastersReported = false;
        device.StateChanged += (_, change) =>
        {
            changes++;
            mastersReported |= change.Masters is not null;
        };
        var ended = WaitForStatus(device, ConnectionStatus.Error);   // the capture runs out: the session ends, nothing is retried

        await device.ConnectAsync(CancellationToken.None);
        Assert.Equal(ConnectionStatus.Connected, device.Status);
        var end = await ended;

        Assert.Contains("end of the capture", end.Error, StringComparison.Ordinal);
        Assert.Equal(25824 + 1000, pipe.MatchedWrites);
        Assert.Equal(6, pipe.SkippedSmartSoftWrites);
        // 25 824 polls matched SmartSoft's; the 25 825th found the capture exhausted and never left the pipe
        Assert.Equal(25824 + 1, device.LastSession!.PollsSent);
        Assert.Equal(1000, device.LastSession.FetchesSent);
        Assert.Null(device.LastSession.Fault);
        Assert.False(pipe.IsOpen);
        Assert.True(changes > 0);
        Assert.True(mastersReported);

        var snapshot = await device.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal("3.0.2.9.0.43", snapshot.FirmwareVersion);   // F59
        Assert.Equal("SHOW05.ASC", snapshot.ShowFileName);        // F103
        Assert.Equal(0, snapshot.Masters.Bumps);                   // F95: BUMPS brought down at the end of E3b
        Assert.Equal(255, snapshot.Masters.Master);                // F96: MASTER back at full
        Assert.False(snapshot.Masters.Solo);                       // F100: SOLO released at 791 s
        // the 157-object download names every memory of the show: labels as in the show file (F73);
        // memory 1 (page 1, fader 1) is OPONA, the memory the owner bumped in E3b: channels 7 and 8 at 100 % (F97)
        var opona = snapshot.Memories[0];
        Assert.Equal("OPONA", opona.Label.ToString());
        Assert.True(opona.Recorded);
        Assert.Equal("LES", snapshot.Memories[1].Label.ToString());
        Assert.Equal(16, snapshot.Memories.Count(m => m.Recorded || m.Label.ToString().Length > 0));
        var content = await device.GetMemoryAsync(1, 1, CancellationToken.None);
        Assert.Equal(255, content.Levels[6]);
        Assert.Equal(255, content.Levels[7]);
    }

    [Fact]
    public async Task Replays_E2_and_reports_the_show_file_of_that_day()
    {
        var pipe = new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl")));
        var device = Create(() => pipe);
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        await ended;

        Assert.Equal(3001 + 228, pipe.MatchedWrites);
        var snapshot = await device.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal("SHOW04.ASC", snapshot.ShowFileName);   // F60
        Assert.Equal(0xAA, snapshot.Masters.Master);          // F90: the MASTER stood at 0xAA in E2
    }

    [Theory]
    [InlineData("2026-10-28-e2-smartsoft-connect.jsonl")]
    [InlineData("2026-10-29-e3-console-actions.jsonl")]
    [InlineData("2026-10-30-e3b-live-objects.jsonl")]
    public async Task Keeps_the_observed_cadence_floors_on_every_transfer(string fileName)
    {
        // A virtual clock advances only by the delays the loop asks for (plus 1 ms per transfer),
        // so every gap the loop produces is measured exactly.
        var clock = new VirtualClock();
        var options = new UsbDeviceOptions();
        var pipe = new TimedPipe(new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture(fileName))), clock);
        var device = Create(() => pipe, options, clock: clock);
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        await ended;

        Assert.Null(device.LastSession!.Fault);
        var transfers = pipe.Transfers;
        Assert.True(transfers.Count > 1000);
        TimeSpan? lastPoll = null, lastFetch = null, lastPayload = null;
        var fetches = 0;
        for (var i = 0; i < transfers.Count; i++)
        {
            var (at, toConsole, data) = transfers[i];
            if (toConsole && data[0] == 0)
            {
                if (lastPoll is { } p)
                {
                    Assert.True(at - p >= options.PollInterval, $"poll {i} came {(at - p).TotalMilliseconds:F2} ms after the previous poll");
                }

                if (lastFetch is { } f)
                {
                    Assert.True(at - f >= options.PollAfterFetch, $"poll {i} came {(at - f).TotalMilliseconds:F2} ms after a fetch");
                }

                lastPoll = at;
            }
            else if (toConsole && data[0] == 2)
            {
                Assert.NotNull(lastPayload);
                Assert.True(at - lastPayload!.Value >= options.FetchDelay, $"fetch {i} came {(at - lastPayload.Value).TotalMilliseconds:F2} ms after the payload before it");
                lastFetch = at;
                fetches++;
            }
            else if (!toConsole && data.Length != 12)
            {
                lastPayload = at;
            }
            else if (!toConsole && data.Length == 12 && lastPayloadWasHeaderLength(transfers, i))
            {
                lastPayload = at;
            }
        }

        Assert.Equal(device.LastSession.FetchesSent, fetches);

        // a 12-byte inbound transfer is a payload when the transfer before it is a reply header announcing 12 bytes
        static bool lastPayloadWasHeaderLength(IReadOnlyList<(TimeSpan, bool, byte[])> all, int index)
        {
            if (index == 0)
            {
                return false;
            }

            var (_, previousToConsole, previous) = all[index - 1];
            return !previousToConsole && previous.Length == 12 && (previous[2] | (previous[3] << 8)) == 12;
        }
    }

    [Fact]
    public async Task Accepts_no_command_and_says_so()
    {
        var device = Create(() => new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl"))));

        Assert.Equal(DeviceMode.Hardware, device.Mode);
        Assert.Equal(DeviceCommands.None, device.SupportedCommands);
        Assert.DoesNotContain("WinUSB", device.Description, StringComparison.Ordinal);
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetChannelLevelAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetMasterAsync(1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetBlackoutAsync(true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpChannelAsync(1, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.RecordMemoryAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.StoreMemoryAsync(1, 1, new byte[48], CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.ClearChannelLevelsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.GetMemoryAsync(1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task A_pipe_that_cannot_open_leaves_the_device_in_error_without_a_session()
    {
        var device = Create(() => throw new HspPipeException("no console bound to WinUSB"));
        var statuses = new List<ConnectionStatus>();
        device.ConnectionChanged += (_, s) => statuses.Add(s.Status);

        await device.ConnectAsync(CancellationToken.None);

        Assert.Equal(new[] { ConnectionStatus.Connecting, ConnectionStatus.Error }, statuses);
        Assert.Null(device.LastSession);
        await device.DisconnectAsync(CancellationToken.None);
        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
    }

    [Fact]
    public async Task Any_failure_while_opening_ends_in_error_with_its_reason()
    {
        var device = Create(() => throw new UnauthorizedAccessException("registry denied"));
        var error = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);

        Assert.Contains("registry denied", (await error).Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unobserved_console_behaviour_ends_the_session_after_one_transfer_without_retry()
    {
        var pipe = new ScriptedPipe(replies: new[] { new byte[] { 0x05, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } });
        var device = Create(() => pipe);
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Contains("unobserved type", end.Error, StringComparison.Ordinal);
        var only = Assert.Single(pipe.Writes);
        Assert.Equal(new byte[12], only);   // the all-zero first poll, nothing else
        Assert.False(pipe.IsOpen);
    }

    [Fact]
    public async Task Any_exception_in_the_session_ends_in_error_with_its_reason()
    {
        var pipe = new ScriptedPipe(replies: Array.Empty<byte[]>(), readFailure: new InvalidOperationException("driver vanished"));
        var device = Create(() => pipe);
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Contains("driver vanished", end.Error, StringComparison.Ordinal);
        Assert.False(pipe.IsOpen);
    }

    [Fact]
    public async Task Disconnect_stops_an_idle_session_cleanly_and_only_at_an_idle_point()
    {
        var pipe = new ScriptedPipe(replies: Enumerable.Repeat(Idle(), 100000).ToArray());
        var device = Create(() => pipe, new UsbDeviceOptions { PollIntervalMilliseconds = 1 });

        await device.ConnectAsync(CancellationToken.None);
        await Task.Delay(50);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.False(pipe.IsOpen);
        Assert.False(device.LastStopWasForced);
        Assert.True(pipe.Writes.Count >= 1);
        Assert.All(pipe.Writes, w => Assert.Equal(new byte[12], w));
        // every poll was answered before the stop: no transfer was left half way
        Assert.Equal(pipe.Writes.Count, pipe.Reads);
    }

    [Fact]
    public async Task A_stop_during_a_fetch_cycle_waits_for_the_fetched_object_and_its_poll()
    {
        // one list of one record, then the object, then idle for ever
        var list = ListPayload(0x10, new DirtyObjectRecord(23, 0, 0, 0));
        var objectPayload = ObjectPayload(0x11, 23, new byte[] { 0, 0, 0, 0, 0, 1, 0 });
        var replies = new List<byte[]> { Header(4, (ushort)list.Length), list, Header(3, (ushort)objectPayload.Length, 23), objectPayload };
        replies.AddRange(Enumerable.Repeat(Idle(), 100000));
        var pipe = new ScriptedPipe(replies.ToArray());
        var device = Create(() => pipe, new UsbDeviceOptions { PollIntervalMilliseconds = 1 });

        await device.ConnectAsync(CancellationToken.None);
        await Task.Delay(30);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.Equal(1, device.LastSession!.FetchesSent);
        Assert.Equal(1, device.LastSession.ObjectsReceived);
        Assert.Equal(HspMessageType.Poll, (HspMessageType)pipe.Writes[^1][0]);
        Assert.True(device.LastSession.IsIdle);
    }

    [Fact]
    public void Timings_never_go_below_the_observed_minimums()
    {
        var options = new UsbDeviceOptions { PollIntervalMilliseconds = 1, PollAfterFetchMilliseconds = 1, FetchDelayMilliseconds = 1, ReplyTimeoutMilliseconds = 1 };

        Assert.Equal(TimeSpan.FromMilliseconds(21), options.PollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(11), options.PollAfterFetch);
        Assert.Equal(TimeSpan.FromMilliseconds(21), options.FetchDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(50), options.ReplyTimeout);
        Assert.DoesNotContain("smartfade-1248-sw", options.CaptureLogDirectory, StringComparison.OrdinalIgnoreCase);

        // the write floors (SC-2 condition 7; E9 and E9b): 30 ms after a poll, 15 ms before the next transfer, 46 ms between writes
        var writes = new UsbDeviceOptions { PollIntervalMilliseconds = 1, PollAfterWriteMilliseconds = 1, WriteIntervalMilliseconds = 1 };
        Assert.Equal(TimeSpan.FromMilliseconds(30), writes.WriteAfterPoll);
        Assert.Equal(TimeSpan.FromMilliseconds(15), writes.PollAfterWrite);
        Assert.Equal(TimeSpan.FromMilliseconds(46), writes.WriteInterval);
        Assert.Null(writes.LiveControlDate);   // never a default
    }

    [Fact]
    public async Task Writes_a_transfer_log_in_the_capture_format()
    {
        var directory = Path.Combine(Path.GetTempPath(), "smartfade-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(directory, "session.jsonl");
        var pipe = new ScriptedPipe(replies: new[] { Idle(), new byte[] { 0x05, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } });
        var device = Create(() => pipe, logFactory: description => new TransferLog(file, description, "test"));
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        await ended;

        var capture = CaptureFile.Load(file);
        Assert.Equal("smartfade-capture", capture.Header.Format);
        var frames = capture.BulkFrames.ToArray();
        Assert.Equal(4, frames.Length);   // poll, idle reply, poll, the bad header
        Assert.Equal(CaptureDirection.Out, frames[0].Direction);
        Assert.Equal(new byte[12], frames[0].Data);
        Assert.Equal(CaptureDirection.In, frames[1].Direction);
        Assert.Contains(capture.Notes, n => n.Text.Contains("session ended", StringComparison.Ordinal));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task A_log_that_cannot_be_written_stops_the_session_before_the_first_transfer()
    {
        var pipe = new ScriptedPipe(replies: new[] { Idle() });
        var device = Create(() => pipe, logFactory: _ => throw new IOException("disk full"));
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Contains("transfer log unavailable", end.Error, StringComparison.Ordinal);
        Assert.Empty(pipe.Writes);
        Assert.False(pipe.IsOpen);   // the log comes first; without it the console is never opened
    }

    private static byte[] Header(ushort type, ushort length, ushort w2 = 0, ushort w3 = 0, ushort w4 = 0, ushort w5 = 0)
    {
        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes.AsSpan(0), type);
        BitConverter.TryWriteBytes(bytes.AsSpan(2), length);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), w2);
        BitConverter.TryWriteBytes(bytes.AsSpan(6), w3);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), w4);
        BitConverter.TryWriteBytes(bytes.AsSpan(10), w5);
        return bytes;
    }

    private static byte[] ListPayload(byte sequence, params DirtyObjectRecord[] records)
    {
        var body = new byte[2 + records.Length * 5];
        body[0] = (byte)(records.Length >> 8);
        body[1] = (byte)records.Length;
        for (var i = 0; i < records.Length; i++)
        {
            body[2 + i * 5] = (byte)records[i].ObjectId;
            body[3 + i * 5] = (byte)(records[i].A >> 8);
            body[4 + i * 5] = (byte)records[i].A;
            body[5 + i * 5] = (byte)records[i].B;
            body[6 + i * 5] = (byte)records[i].C;
        }

        return Payload(sequence, (int)HspObjectId.DirtyObjects, body);
    }

    private static byte[] ObjectPayload(byte sequence, int objectId, byte[] body) => Payload(sequence, objectId, body);

    private static byte[] Payload(byte sequence, int objectId, byte[] body)
    {
        var payload = new byte[5 + body.Length];
        payload[0] = sequence;
        payload[1] = 0;
        payload[2] = (byte)body.Length;
        payload[3] = (byte)(body.Length >> 8);
        payload[4] = (byte)objectId;
        body.CopyTo(payload, 5);
        return payload;
    }

    [Fact]
    public async Task A_session_without_a_transfer_sink_does_not_start()
    {
        var pipe = new ScriptedPipe(replies: new[] { Idle() });
        var device = Create(() => pipe, logFactory: _ => null);
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Contains("transfer log unavailable", end.Error, StringComparison.Ordinal);
        Assert.Empty(pipe.Writes);
        Assert.False(pipe.IsOpen);   // the log comes first; without it the console is never opened
    }

    [Fact]
    public async Task Connect_is_refused_when_hardware_mode_was_confirmed_for_another_day()
    {
        var pipe = new ScriptedPipe(replies: new[] { Idle() });
        var device = Create(() => pipe, new UsbDeviceOptions { ConfirmDate = "2026-10-29" });   // the virtual clock's day is 2026-10-30
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        var end = await ended;

        Assert.Contains("confirmed for 2026-10-29", end.Error, StringComparison.Ordinal);
        Assert.False(pipe.IsOpen);
        Assert.Empty(pipe.Writes);
    }

    [Fact]
    public async Task The_transfer_log_exists_before_the_console_is_opened()
    {
        var pipe = new ScriptedPipe(replies: new[] { Idle(), new byte[] { 0x05, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } });
        var created = new List<string>();
        var device = Create(() => pipe, logFactory: _ =>
        {
            created.Add(pipe.IsOpen ? "after the open" : "before the open");
            return new DiscardingSink();
        });
        var ended = WaitForStatus(device, ConnectionStatus.Error);

        await device.ConnectAsync(CancellationToken.None);
        await ended;

        Assert.Equal(new[] { "before the open" }, created);
    }

    private const DeviceCommands LiveCommands = DeviceCommands.SetChannelLevel | DeviceCommands.SetMaster | DeviceCommands.SetBumps
        | DeviceCommands.SetBlackout | DeviceCommands.SetSolo | DeviceCommands.BumpChannel | DeviceCommands.SetMemoryLevel
        | DeviceCommands.BumpMemory | DeviceCommands.SetIndependent | DeviceCommands.PressGo | DeviceCommands.PressFadersMems
        | DeviceCommands.PressFaders1To24 | DeviceCommands.PressFaders25To48;

    private static string Today(VirtualClock clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime).ToString("yyyy-MM-dd");

    /// <summary>The bulk transfer of a committed capture with that JSONL seq and direction.</summary>
    private static byte[] Frame(CaptureFile capture, int seq, CaptureDirection direction) =>
        Assert.Single(capture.Frames, f => f.Seq == seq && f.Direction == direction && f.IsBulk && !f.Synthetic).Data;

    [Fact]
    public void The_live_control_switch_needs_the_USB_mode_and_today()
    {
        var today = new DateOnly(2026, 10, 30);

        ServiceCollectionExtensions.ValidateLiveControlDate(null, usbMode: true, today);           // absent: read-only
        ServiceCollectionExtensions.ValidateLiveControlDate("2026-10-30", usbMode: true, today);
        Assert.Throws<InvalidOperationException>(() => ServiceCollectionExtensions.ValidateLiveControlDate("2026-10-29", usbMode: true, today));
        Assert.Throws<InvalidOperationException>(() => ServiceCollectionExtensions.ValidateLiveControlDate("2026-10-30", usbMode: false, today));
    }

    [Fact]
    public async Task Live_control_is_off_unless_its_own_switch_names_today()
    {
        var clock = new VirtualClock();
        var stale = Create(() => new BlockingPipe(), new UsbDeviceOptions { LiveControlDate = "2026-10-29" }, clock: clock);   // the virtual clock's day is 2026-10-30
        var live = Create(() => new BlockingPipe(), new UsbDeviceOptions { LiveControlDate = Today(clock) }, clock: clock);

        Assert.Equal(DeviceCommands.None, stale.SupportedCommands);
        Assert.Contains("read-only", stale.Description, StringComparison.Ordinal);
        await Assert.ThrowsAsync<NotSupportedException>(() => stale.SetMasterAsync(1, CancellationToken.None));
        Assert.Equal(LiveCommands, live.SupportedCommands);
        Assert.Empty(stale.WritableControls!);   // read-only: nothing is writable
        Assert.Contains(DeviceControls.ChannelFader(1), live.WritableControls!);
        Assert.Contains(DeviceControls.MemoryBump(265), live.WritableControls!);
        Assert.Contains(DeviceControls.Go, live.WritableControls!);
        Assert.Contains(DeviceControls.FadersMems, live.WritableControls!);
        Assert.Contains(DeviceControls.Faders1To24, live.WritableControls!);
        Assert.Contains(DeviceControls.Faders25To48, live.WritableControls!);
        Assert.Contains(DeviceControls.ChannelFader(48), live.WritableControls!);    // SC-5: every physical fader in every bank
        Assert.Contains(DeviceControls.ChannelBump(9), live.WritableControls!);
        Assert.Contains(DeviceControls.MemoryFader(288), live.WritableControls!);
        Assert.Contains(DeviceControls.Independent(2), live.WritableControls!);
        Assert.DoesNotContain(DeviceControls.Clear, live.WritableControls!);        // NO-GO: CLEAR and the record keys (SC-3)
        Assert.DoesNotContain(DeviceControls.RecMem, live.WritableControls!);
        Assert.DoesNotContain(DeviceControls.EditMem, live.WritableControls!);
        Assert.DoesNotContain(DeviceControls.Copy, live.WritableControls!);
        Assert.Contains("live control", live.Description, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => live.SetMasterAsync(1, CancellationToken.None));   // no session yet
    }

    [Fact]
    public async Task Live_control_refuses_what_is_not_whitelisted_and_everything_before_the_console_has_reported()
    {
        var clock = new VirtualClock();
        var pipe = new BlockingPipe();
        var device = Create(() => pipe, new UsbDeviceOptions { LiveControlDate = Today(clock) }, clock: clock);
        var connected = WaitForStatus(device, ConnectionStatus.Connected);
        await device.ConnectAsync(CancellationToken.None);
        await connected;
        await pipe.FirstWrite.Task.WaitAsync(Timeout);   // the first poll is out and its reply is held back

        // outside the whitelist (SC-2 condition 1, item 6; SC-5's NO-GO): the record functions, CLEAR, controls the 1248 lacks
        await Assert.ThrowsAsync<NotSupportedException>(() => device.RecordMemoryAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.StoreMemoryAsync(1, 1, new byte[48], CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.ClearChannelLevelsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.ClearMemoryLevelsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetChannelLevelAsync(49, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpChannelAsync(0, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetMemoryLevelAsync(13, 1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetIndependentAsync(3, true, CancellationToken.None));

        // inside it, but the console has reported nothing yet (SC-2 conditions 5, 6, 8)
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetMasterAsync(1, CancellationToken.None));
        Assert.Contains("key LEDs", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetChannelLevelAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetChannelLevelAsync(30, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetMemoryLevelAsync(1, 1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetSoloAsync(true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetIndependentAsync(1, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.BumpChannelAsync(7, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.BumpMemoryAsync(1, 7, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressGoAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressFaders25To48Async(CancellationToken.None));
        await device.BumpChannelAsync(7, false, CancellationToken.None);   // a release with nothing held: nothing to send, no error

        var ended = WaitForStatus(device, ConnectionStatus.Error);
        pipe.Release();
        await ended;

        Assert.Equal(new byte[12], Assert.Single(pipe.Writes));   // the one poll: no Data header ever left
        Assert.Equal(0, device.LastSession!.WritesSent);
    }

    private static readonly Lazy<CaptureFile> E9 = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-30-e9-smartsoft-writes.jsonl")));

    private static readonly Lazy<CaptureFile> E9b = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-31-e9b-smartsoft-writes-repeat.jsonl")));

    /// <summary>The body of the object the console delivered in E9b's payload frame with that JSONL seq.</summary>
    private static byte[] E9bBody(int seq) => Frame(E9b.Value, seq, CaptureDirection.In)[5..];

    /// <summary>SmartSoft's E9 payload with that JSONL seq, with this session's counter in place of its own.</summary>
    private static byte[] E9Payload(int seq, byte counter)
    {
        var bytes = Frame(E9.Value, seq, CaptureDirection.Out).ToArray();   // a copy: the capture's bytes stay as recorded
        bytes[0] = counter;
        return bytes;
    }

    /// <summary>
    /// A live device on a stand-in console that reports the state SmartSoft saw before its first MASTER event of E9b
    /// (header seq 13143, payload 13144): FaderInfo seq 11628 (every fader at zero, MASTER and BUMPS at full), ConsoleStatus
    /// seq 796 (channel mode, bank 1-24, page 1, NEXT off), ChannelLevels seq 11632 (every channel at zero; left out with
    /// <paramref name="withChannelLevels"/> false) and LedStatus seq 11636 (REC MEM, EDIT MEM, COPY, REC SEQ, SOLO,
    /// BLACKOUT, IND 1 and IND 2 off, STACK dim), served in that order, so the key LEDs are reported last.
    /// <paramref name="delay"/> replaces the virtual clock's plain delay; the transfer log's notes are kept. Without
    /// <paramref name="withFaderInfo"/> FaderInfo is left out; with <paramref name="order"/> the pipe's transfers, the log's
    /// notes and the loop's waits are entered there in the order they happen ("out HEX", "in HEX", "note TEXT", "wait").
    /// </summary>
    private static LiveRig LiveConsole(
        bool relistAfterWrites = false,
        Func<VirtualClock, Func<TimeSpan, CancellationToken, Task>>? delay = null,
        IncompleteWriteMarker? marker = null,
        string? liveControlDate = "today",
        int failDataTransfer = 0,
        ConsoleFault faultAfterWrite = ConsoleFault.None,
        bool withChannelLevels = true,
        bool withFaderInfo = true,
        List<string>? order = null)
    {
        var objects = new List<(int, byte[])>();
        if (withFaderInfo)
        {
            objects.Add(((int)HspObjectId.FaderInfo, E9bBody(11628)));
        }

        objects.Add(((int)HspObjectId.ConsoleStatus, E9bBody(796)));
        if (withChannelLevels)
        {
            objects.Add(((int)HspObjectId.ChannelLevels, E9bBody(11632)));
        }

        objects.Add(((int)HspObjectId.LedStatus, E9bBody(11636)));
        var console = new AnsweringPipe(objects.ToArray())
        {
            RelistFaderInfoAfterWrites = relistAfterWrites,
            FailDataTransfer = failDataTransfer,
            FaultAfterWrite = faultAfterWrite,
        };
        var clock = new VirtualClock();
        var options = new UsbDeviceOptions { LiveControlDate = liveControlDate == "today" ? Today(clock) : liveControlDate };
        var pipe = new TimedPipe(console, clock) { Order = order };
        var log = new RecordingSink { Order = order };
        delay ??= order is null ? null : RecordingWait(order);
        var device = Create(() => pipe, options, _ => log, clock, delay?.Invoke(clock), marker);
        var keysReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.StateChanged += (_, change) =>
        {
            if (change.Keys is not null)
            {
                keysReported.TrySetResult();
            }
        };

        return new LiveRig(device, console, pipe, options, keysReported.Task, clock, log);
    }

    /// <summary>The virtual clock's plain delay, entered in <paramref name="order"/> as "wait": a write's slot wait is the loop's last step before the session asks the gate (SC-2 condition 5).</summary>
    private static Func<VirtualClock, Func<TimeSpan, CancellationToken, Task>> RecordingWait(List<string> order) => clock => (wait, _) =>
    {
        lock (order)
        {
            order.Add("wait");
        }

        clock.Advance(wait);
        return Task.CompletedTask;
    };

    /// <summary>A device on the stand-in console, with its clock and its transfer log.</summary>
    private sealed record LiveRig(UsbSmartFadeDevice Device, AnsweringPipe Console, TimedPipe Pipe, UsbDeviceOptions Options, Task KeysReported, VirtualClock Clock, RecordingSink Log)
    {
        public void Deconstruct(out UsbSmartFadeDevice device, out AnsweringPipe console, out TimedPipe pipe, out UsbDeviceOptions options, out Task keysReported)
        {
            device = Device;
            console = Console;
            pipe = Pipe;
            options = Options;
            keysReported = KeysReported;
        }
    }

    private static List<(TimeSpan At, bool ToConsole, byte[] Data)> Recorded(TimedPipe pipe)
    {
        lock (pipe.Transfers)
        {
            return pipe.Transfers.ToList();
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("the condition was not met in time");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Live_control_sends_a_MASTER_event_with_SmartSofts_framing_once_the_console_has_reported()
    {
        var (device, console, pipe, options, keysReported) = LiveConsole();

        await device.ConnectAsync(CancellationToken.None);
        await keysReported.WaitAsync(Timeout);
        await device.SetMasterAsync(0x80, CancellationToken.None);
        await console.DataWritten.Task.WaitAsync(Timeout);
        await console.PollAfterData.Task.WaitAsync(Timeout);   // the transfer after the write is on record
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.Equal(1, device.LastSession!.WritesSent);
        var transfers = Recorded(pipe);
        var header = transfers.FindIndex(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 1);
        Assert.True(header > 0);
        Assert.Single(transfers, t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 1);

        // SmartSoft's MASTER header at that point had the same all-zero stale words (after an idle reply); the payload is
        // SmartSoft's but for this session's counter (0x00, its first write), the level asked for and the previous byte
        // (0x00: this session had sent no MASTER value, F132).
        var e9b = CaptureFile.Load(TestPaths.Capture("2026-10-31-e9b-smartsoft-writes-repeat.jsonl"));
        var expected = Frame(e9b, 13144, CaptureDirection.Out).ToArray();
        expected[0] = 0x00;
        expected[6] = 0x80;
        expected[7] = 0x00;
        Assert.Equal(Frame(e9b, 13143, CaptureDirection.Out), transfers[header].Data);
        Assert.Equal(expected, transfers[header + 1].Data);

        // the cadence around the write (SC-2 condition 7): no sooner after the poll than SmartSoft's, nor the next transfer after it
        var lastPoll = transfers.FindLastIndex(header, t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 0);
        Assert.True(transfers[header].At - transfers[lastPoll].At >= options.WriteAfterPoll);
        var next = transfers.FindIndex(header + 2, t => t.ToConsole);
        Assert.Equal(0, transfers[next].Data[0]);   // the usual poll
        Assert.True(transfers[next].At - transfers[header + 1].At >= options.PollAfterWrite);
    }

    [Fact]
    public async Task Live_control_sends_each_control_only_in_the_console_state_SmartSoft_sent_it_in()
    {
        var rig = LiveConsole();
        var (device, _, pipe, _, keysReported) = rig;
        await device.ConnectAsync(CancellationToken.None);
        await keysReported.WaitAsync(Timeout);

        // channel mode, bank 1-24: FADERS 1-24 is sent only from memory mode or bank 25-48, a memory of another page never
        // (no page change from the PC, F113)
        var faders = await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressFaders1To24Async(CancellationToken.None));
        Assert.Contains("bank 1-24", faders.Message, StringComparison.Ordinal);
        var page = await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetMemoryLevelAsync(2, 1, 0x10, CancellationToken.None));
        Assert.Contains("memory page is 1, not 2", page.Message, StringComparison.Ordinal);

        // SOLO, BLACKOUT, IND 1 and IND 2 already read off, and a bump's release alone: nothing to send
        await device.SetSoloAsync(false, CancellationToken.None);
        await device.SetBlackoutAsync(false, CancellationToken.None);
        await device.SetIndependentAsync(1, false, CancellationToken.None);
        await device.SetIndependentAsync(2, false, CancellationToken.None);
        await device.BumpChannelAsync(7, pressed: false, CancellationToken.None);

        // channel fader 1 with the MASTER at full: its first event in the session carries its reported level (ChannelLevels[1]
        // = FaderInfo record 1 = 0x00; SC-5 condition 1 d, F172), then the UI's level, as SmartSoft's first channel fader 1
        // event of E9b (seq 8038) but for this session's counter, the level and the previous byte
        await device.SetChannelLevelAsync(1, 0x10, CancellationToken.None);
        await WaitUntil(() => device.LastSession!.WritesSent == 2);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, device.LastSession!.WritesSent);
        var payloads = Recorded(pipe).Where(t => t.ToConsole && t.Data.Length != 12).Select(t => t.Data).ToArray();
        Assert.Equal(new[] { WithCounter(E9b, 8038, 0x00, value: 0x00, previous: 0x00), WithCounter(E9b, 8038, 0x01, value: 0x10, previous: 0x00) }, payloads);
        Assert.Contains("matched channel fader 1 at its reported level 0x00 (SC-5 condition 1 d, F172)", rig.Log.Notes);
    }

    [Fact]
    public async Task Live_control_keeps_every_floor_across_writes_taps_and_the_downloads_they_cause()
    {
        // The stand-in console lists FaderInfo again after every write, as the console lists what an event changed
        // (F134), so writes meet pending fetches as SmartSoft's did (E9, E9b).
        var (device, _, pipe, options, keysReported) = LiveConsole(relistAfterWrites: true);
        await device.ConnectAsync(CancellationToken.None);
        await keysReported.WaitAsync(Timeout);

        await device.SetMasterAsync(0xC0, CancellationToken.None);
        await device.PressGoAsync(CancellationToken.None);                 // a tap: press, then release
        await device.SetBumpsAsync(0x80, CancellationToken.None);
        await device.SetChannelLevelAsync(1, 0x20, CancellationToken.None); // its matching event (held while FaderInfo is listed and not fetched), then the level
        await WaitUntil(() => device.LastSession!.WritesSent == 6);
        await device.DisconnectAsync(CancellationToken.None);               // returns after the poll exchange that follows the last write

        Assert.Equal(0, device.LastSession!.WritesDropped);
        TimeSpan? lastPoll = null, lastFetch = null, lastPayloadIn = null, lastWriteHeader = null, lastWritePayload = null, lastReport = null;
        var inboundPayloadDue = 0;
        var outboundPayloadDue = false;
        var fetchAwaitingPoll = false;
        var writesSincePoll = 0;
        var writes = 0;
        foreach (var (at, toConsole, data) in Recorded(pipe))
        {
            if (!toConsole)
            {
                if (inboundPayloadDue > 0)
                {
                    lastPayloadIn = at;
                    inboundPayloadDue = 0;
                }
                else
                {
                    inboundPayloadDue = data[2] | (data[3] << 8);
                    if (data[0] == 4)
                    {
                        lastReport = at;   // a poll reply: the console's report of what changed (F57)
                    }
                }

                continue;
            }

            if (outboundPayloadDue)
            {
                outboundPayloadDue = false;
                lastWritePayload = at;
                writes++;

                // SC-2 condition 5 at every header but a release's (condition 4: a release is never held back): the gate
                // let the write go on a report younger than two poll periods, as of the header (the first review of SC-2
                // item 10, major 1)
                if (!(data[4] == 1 && data[6] == 0))
                {
                    Assert.True(lastReport is not null && lastWriteHeader - lastReport <= 2 * options.PollInterval, $"write at {lastWriteHeader} on a report from {lastReport}");
                }

                continue;
            }

            switch (data[0])
            {
                case 0:
                    Assert.True(lastPoll is null || at - lastPoll >= options.PollInterval, $"poll {at} after poll {lastPoll}");
                    Assert.True(lastFetch is null || at - lastFetch >= options.PollAfterFetch, $"poll {at} after fetch {lastFetch}");
                    Assert.True(lastWritePayload is null || at - lastWritePayload >= options.PollAfterWrite, $"poll {at} after write {lastWritePayload}");
                    lastPoll = at;
                    fetchAwaitingPoll = false;
                    writesSincePoll = 0;
                    break;
                case 2:
                    Assert.True(lastPayloadIn is null || at - lastPayloadIn >= options.FetchDelay, $"fetch {at} after payload {lastPayloadIn}");
                    Assert.True(lastWritePayload is null || at - lastWritePayload >= options.PollAfterWrite, $"fetch {at} after write {lastWritePayload}");
                    lastFetch = at;
                    fetchAwaitingPoll = true;
                    break;
                case 1:
                    Assert.False(fetchAwaitingPoll, $"write {at} between a fetch and its poll");
                    Assert.Equal(0, writesSincePoll);                   // at most one write per poll
                    Assert.True(lastPoll is not null && at - lastPoll >= options.WriteAfterPoll, $"write {at} after poll {lastPoll}");
                    Assert.True(lastPayloadIn is null || at - lastPayloadIn >= options.FetchDelay, $"write {at} after payload {lastPayloadIn}");
                    Assert.True(lastWriteHeader is null || at - lastWriteHeader >= options.WriteInterval, $"write {at} after write {lastWriteHeader}");
                    lastWriteHeader = at;
                    writesSincePoll++;
                    outboundPayloadDue = true;
                    break;
                default:
                    Assert.Fail($"unexpected transfer {Convert.ToHexString(data)}");
                    break;
            }
        }

        Assert.Equal(6, writes);
    }

    /// <summary>
    /// A delay that stretches, once, the wait for a write's slot that <paramref name="which"/> picks (asked on the loop's
    /// thread, where the session lives) by three poll periods, and records the virtual time it ended at.
    /// </summary>
    private static Func<VirtualClock, Func<TimeSpan, CancellationToken, Task>> StretchOneWriteSlot(Func<UsbSmartFadeDevice?> device, Func<HspReadSession, bool> which, StrongBox<TimeSpan> stretchedTo)
    {
        var once = 1;
        return clock => (wait, _) =>
        {
            if (device()?.LastSession is { State: HspReadSessionState.ReadyToSend } session
                && session.PeekNextKind() == HspMessageType.Data && which(session) && Interlocked.Exchange(ref once, 0) == 1)
            {
                clock.Advance(wait + TimeSpan.FromMilliseconds(3 * UsbDeviceOptions.Defaults.PollInterval));
                stretchedTo.Value = clock.Now;
                return Task.CompletedTask;
            }

            clock.Advance(wait);
            return Task.CompletedTask;
        };
    }

    private static bool IsDataHeader((TimeSpan At, bool ToConsole, byte[] Data) transfer) =>
        transfer.ToConsole && transfer.Data.Length == 12 && transfer.Data[0] == 1;

    [Fact]
    public async Task A_write_is_chosen_after_its_slot_wait_and_held_when_the_report_aged_past_two_poll_periods_in_it()
    {
        // The first review of SC-2 item 10, major 1: the gate used to answer before the slot wait, so a report judged
        // fresh could be stale when the header went out. Here the MASTER write's slot wait lasts three poll periods
        // longer; the gate, asked after it, holds the write (condition 5), a poll goes in that slot, and the write follows
        // the fresh report.
        UsbSmartFadeDevice? device = null;
        var stretchedTo = new StrongBox<TimeSpan>();
        var rig = LiveConsole(delay: StretchOneWriteSlot(() => device, session => session.HeldKey is null, stretchedTo));
        device = rig.Device;
        await device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        // queued while the loop waits inside a poll, so the first write slot after that poll is the one stretched (a write
        // queued between the loop's peek and its delay would see a poll's wait stretched instead)
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await device.SetMasterAsync(0x80, CancellationToken.None);
        rig.Console.Resume();
        await rig.Console.DataWritten.Task.WaitAsync(Timeout);
        await rig.Console.PollAfterData.Task.WaitAsync(Timeout);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.True(stretchedTo.Value > TimeSpan.Zero);
        Assert.Equal(1, device.LastSession!.WritesSent);
        Assert.Equal(0, device.LastSession.WritesDropped);
        Assert.Contains("write held back: the console's last report is older than two poll periods (SC-2 condition 5)", rig.Log.Notes);
        var transfers = Recorded(rig.Pipe);
        var afterStretch = transfers.FindIndex(t => t.ToConsole && t.At >= stretchedTo.Value);
        Assert.Equal(0, transfers[afterStretch].Data[0]);   // a poll in the slot whose wait outlasted the report, not the write
        var header = transfers.FindIndex(IsDataHeader);
        Assert.True(header > afterStretch, $"the write went at {header}, the stretched slot was {afterStretch}");

        // the write's own poll exchange: a poll reply (the console's report) at most two poll periods before the header
        var poll = transfers.FindLastIndex(header, t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 0);
        Assert.False(transfers[poll + 1].ToConsole);
        Assert.Equal(4, transfers[poll + 1].Data[0]);
        Assert.True(transfers[header].At - transfers[poll + 1].At <= 2 * rig.Options.PollInterval);
    }

    [Fact]
    public async Task A_release_still_goes_when_the_report_aged_past_two_poll_periods_during_its_slot_wait()
    {
        // SC-2 condition 4: the release completes a press that went; it is the one write an aged report does not hold back.
        UsbSmartFadeDevice? device = null;
        var stretchedTo = new StrongBox<TimeSpan>();
        var rig = LiveConsole(delay: StretchOneWriteSlot(() => device, session => session.HeldKey is not null, stretchedTo));
        device = rig.Device;
        await device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await device.BumpChannelAsync(7, pressed: true, CancellationToken.None);
        await WaitUntil(() => device.LastSession!.WritesSent == 2);
        await device.DisconnectAsync(CancellationToken.None);

        var transfers = Recorded(rig.Pipe);
        var afterStretch = transfers.FindIndex(t => t.ToConsole && t.At >= stretchedTo.Value);
        Assert.True(IsDataHeader(transfers[afterStretch]));   // the release's header, in that very slot
        Assert.Equal(E9Payload(19436, 0x01), transfers[afterStretch + 1].Data);   // SmartSoft's bump 7 release (E9 seq 19436), this session's second write
        Assert.Equal(0, device.LastSession!.WritesDropped);
        Assert.Null(device.LastSession.HeldKey);
    }

    [Theory]
    [InlineData((int)HspObjectId.LedStatus, 11636)]
    [InlineData((int)HspObjectId.FaderInfo, 11628)]
    public async Task In_a_live_session_a_key_LED_or_FaderInfo_report_that_does_not_decode_ends_the_session(int objectId, int e9bSeq)
    {
        // The first review of SC-2 item 10, major 2: the interlocks read these objects, and an older reading must never
        // stand for the console's state. The body is E9b's with its count byte lowered by one, so that it contradicts its
        // own length (F67): a shape no capture shows, made here on purpose. (ConsoleStatus cannot fail this way: its
        // decoder takes any 7 bytes, and a mode or bank value never observed already reads as unknown.)
        var broken = E9bBody(e9bSeq);
        broken[0]--;
        var rig = LiveConsole();
        var ended = WaitForStatus(rig.Device, ConnectionStatus.Error);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        rig.Console.Report(objectId, broken);
        var end = await ended;

        Assert.Contains($"{HspObjectIds.Name(objectId)} did not decode", end.Error, StringComparison.Ordinal);
        Assert.Equal(2, rig.Console.Served(objectId));
        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
    }

    [Fact]
    public async Task A_read_only_session_logs_a_key_LED_report_that_does_not_decode_and_goes_on()
    {
        // The read-only session stays as SC-1 reviewed it: nothing it sends depends on the LEDs.
        var broken = E9bBody(11636);
        broken[0]--;
        var rig = LiveConsole(liveControlDate: null);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        rig.Console.Report((int)HspObjectId.LedStatus, broken);
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 2);
        var seen = Recorded(rig.Pipe).Count;
        await WaitUntil(() => Recorded(rig.Pipe).Count > seen + 6);   // three more poll exchanges

        Assert.Equal(ConnectionStatus.Connected, rig.Device.Status);
        await rig.Device.DisconnectAsync(CancellationToken.None);
        Assert.Equal(ConnectionStatus.Disconnected, rig.Device.Status);
    }

    [Fact]
    public async Task A_write_is_refused_while_REC_MEM_is_lit_and_one_queued_before_it_lit_never_goes_out()
    {
        // SC-2 condition 5 with the console's own LED reports of E9b: seq 11636 (every record key off) and seq 83953 (REC MEM
        // lit after SmartSoft's REC MEM press). The MASTER write is queued while the loop waits inside a poll whose reply
        // then lists the new LEDs: the gate holds it while they are not fetched, and their arrival drops it.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetMasterAsync(0x80, CancellationToken.None);         // accepted: every record key still reads off
        rig.Console.Report((int)HspObjectId.LedStatus, E9bBody(83953));        // REC MEM lights on the panel
        rig.Console.Resume();
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 2 && rig.Device.LastSession!.QueuedWrites == 0);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetMasterAsync(0x40, CancellationToken.None));
        Assert.Contains("REC MEM is lit", refused.Message, StringComparison.Ordinal);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
        Assert.DoesNotContain(Recorded(rig.Pipe), IsDataHeader);
        Assert.Contains("write held back: the console listed new key LEDs that are not fetched yet (SC-2 condition 5)", rig.Log.Notes);
    }

    [Fact]
    public async Task A_write_waits_while_the_console_has_listed_key_LEDs_it_has_not_delivered_and_goes_after_them()
    {
        // SC-2 condition 5: listed LEDs not fetched yet are LEDs not known yet. The console lists LedStatus again (the same
        // E9b seq 11636 body); the MASTER write goes only after that object is in.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetMasterAsync(0x80, CancellationToken.None);
        rig.Console.Report((int)HspObjectId.LedStatus, E9bBody(11636));
        rig.Console.Resume();
        await rig.Console.DataWritten.Task.WaitAsync(Timeout);
        await rig.Console.PollAfterData.Task.WaitAsync(Timeout);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(1, rig.Device.LastSession!.WritesSent);
        Assert.Contains("write held back: the console listed new key LEDs that are not fetched yet (SC-2 condition 5)", rig.Log.Notes);
        var transfers = Recorded(rig.Pipe);
        var ledFetch = transfers.FindLastIndex(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)HspObjectId.LedStatus);
        var header = transfers.FindIndex(IsDataHeader);
        Assert.True(ledFetch > 0 && header > ledFetch, $"the write went at {header}, the LEDs were fetched at {ledFetch}");
    }

    [Fact]
    public async Task Live_control_that_expires_at_midnight_inside_a_session_drops_the_queued_write_and_refuses_the_next()
    {
        // SC-2 item 6: the switch names a day. The virtual clock (2026-10-30 12:00 UTC) moves twelve hours while a MASTER
        // write waits; at its slot the gate drops it, and the device is read-only from then on.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetMasterAsync(0x80, CancellationToken.None);
        rig.Clock.Advance(TimeSpan.FromHours(12));
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesDropped == 1);

        Assert.Contains("not confirmed for today", rig.Device.LastSession!.LastDropReason, StringComparison.Ordinal);
        Assert.Equal(DeviceCommands.None, rig.Device.SupportedCommands);
        Assert.Empty(rig.Device.WritableControls!);
        await Assert.ThrowsAsync<NotSupportedException>(() => rig.Device.SetMasterAsync(0x40, CancellationToken.None));
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
        Assert.DoesNotContain(Recorded(rig.Pipe), IsDataHeader);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task After_a_fault_the_release_of_a_pressed_key_goes_only_when_no_exchange_is_open(bool exchangeOpen)
    {
        // SC-2 condition 4 at a session's end. The stand-in console answers the poll after the bump 7 press in a way no
        // capture shows: a list read whole whose byte 1 is 0x01 (no exchange open: the release goes, then the session
        // ends), or a header announcing 793 bytes (its payload still due: nothing more is sent and the press is reported).
        var rig = LiveConsole(faultAfterWrite: exchangeOpen ? ConsoleFault.HeaderTooLong : ConsoleFault.PayloadByte1);
        var ended = WaitForStatus(rig.Device, ConnectionStatus.Error);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, pressed: true, CancellationToken.None);
        var end = await ended;

        // SmartSoft's bump 7 press and release (E9 seq 19055, 19436) with this session's counter
        var payloads = Recorded(rig.Pipe).Where(t => t.ToConsole && t.Data.Length != 12).Select(t => t.Data).ToArray();
        if (exchangeOpen)
        {
            Assert.Equal(new[] { E9Payload(19055, 0x00) }, payloads);
            Assert.Contains("Bump7 was pressed and its release could not be sent", end.Error, StringComparison.Ordinal);
            Assert.Contains("press Bump7 on the panel", end.Error, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(new[] { E9Payload(19055, 0x00), E9Payload(19436, 0x01) }, payloads);
            Assert.Contains("the release of Bump7 was sent as the session ended", end.Error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(1, "whether it reached the console is UNKNOWN")]   // the Data header's own transfer fails
    [InlineData(2, "payload was not")]                             // the payload fails after its header went out
    public async Task A_Data_write_that_does_not_complete_keeps_the_device_from_connecting_also_after_a_restart(int failingTransfer, string said)
    {
        // SC-2 item 7 and the first review of item 10 (minors: a failed header was assumed not to have reached the console;
        // the latch lived in memory only). The marker lives in a folder of this test only.
        var directory = Path.Combine(Path.GetTempPath(), "smartfade-tests", Guid.NewGuid().ToString("N"));
        var marker = new IncompleteWriteMarker(directory);
        try
        {
            var rig = LiveConsole(marker: marker, failDataTransfer: failingTransfer);
            var ended = WaitForStatus(rig.Device, ConnectionStatus.Error);
            await rig.Device.ConnectAsync(CancellationToken.None);
            await rig.KeysReported.WaitAsync(Timeout);
            await rig.Device.SetMasterAsync(0x80, CancellationToken.None);
            var end = await ended;

            Assert.Contains(said, end.Error, StringComparison.Ordinal);
            Assert.Contains("A Data write did not complete", marker.Read(), StringComparison.Ordinal);
            await rig.Device.DisconnectAsync(CancellationToken.None);   // waits for the session task's end

            // the same process: refused before anything is opened
            var states = new List<ConnectionState>();
            rig.Device.ConnectionChanged += (_, state) => states.Add(state);
            await rig.Device.ConnectAsync(CancellationToken.None);
            Assert.Contains("nothing connects again until the backend restarts", Assert.Single(states).Error, StringComparison.Ordinal);
            Assert.Equal(ConnectionStatus.Disconnected, rig.Device.Status);

            // a restart: a new device with the same marker refuses too, without opening the console, until the owner removes it
            var opened = 0;
            var restarted = Create(() =>
            {
                opened++;
                return new ScriptedPipe(replies: Enumerable.Repeat(Idle(), 100).ToArray());
            }, marker: marker);
            var refusals = new List<ConnectionState>();
            restarted.ConnectionChanged += (_, state) => refusals.Add(state);
            await restarted.ConnectAsync(CancellationToken.None);
            Assert.Contains("records a Data write that did not complete", Assert.Single(refusals).Error, StringComparison.Ordinal);
            Assert.Equal(0, opened);

            File.Delete(marker.FilePath);
            var connected = WaitForStatus(restarted, ConnectionStatus.Connected);
            await restarted.ConnectAsync(CancellationToken.None);
            await connected;
            Assert.Equal(1, opened);
            await restarted.DisconnectAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(1, "the press of Bump7 was being written")]     // the press's header fails
    [InlineData(2, "the press of Bump7 was being written")]     // the press's payload fails
    [InlineData(3, "Bump7 may still be held, press it on the panel")]   // the release's header fails
    [InlineData(4, "Bump7 may still be held, press it on the panel")]   // the release's payload fails
    public async Task A_key_event_that_does_not_complete_is_reported_for_what_it_was(int failingTransfer, string said)
    {
        // A press that did not complete may or may not have reached the console (UNKNOWN): pressing the key on the panel
        // would toggle or trigger it again if it did, so the report never asks for it. A release that did not complete
        // leaves the key possibly held: then a press on the panel is what releases it.
        var rig = LiveConsole(failDataTransfer: failingTransfer);
        var ended = WaitForStatus(rig.Device, ConnectionStatus.Error);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, pressed: true, CancellationToken.None);
        var end = await ended;

        Assert.Contains(said, end.Error, StringComparison.Ordinal);
        if (failingTransfer <= 2)
        {
            Assert.Contains("whether the console took it is UNKNOWN", end.Error, StringComparison.Ordinal);
            Assert.DoesNotContain("press Bump7 on the panel", end.Error, StringComparison.Ordinal);
            Assert.DoesNotContain("press it on the panel", end.Error, StringComparison.Ordinal);
        }

        var states = new List<ConnectionState>();
        await rig.Device.DisconnectAsync(CancellationToken.None);
        rig.Device.ConnectionChanged += (_, state) => states.Add(state);
        await rig.Device.ConnectAsync(CancellationToken.None);
        Assert.Contains("A Data write did not complete", Assert.Single(states).Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_SOLO_tap_goes_out_and_the_next_waits_until_the_console_has_reported_its_LED()
    {
        // SC-2 class B1 toggles (F133 / F134). SOLO reads off (E9b seq 11636), so a wish for SOLO on is one tap. The
        // stand-in console toggles nothing by itself, so the second wish waits until the test reports the LEDs SmartSoft's
        // SOLO press gave (E9b seq 22172, SOLO full); the tap counts as sent only once its press was taken for sending.
        var rig = LiveConsole();
        var soloLit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Device.StateChanged += (_, change) =>
        {
            if (change.Keys is { Solo.Light: KeyLight.Full })
            {
                soloLit.TrySetResult();
            }
        };
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetSoloAsync(true, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);   // the press and its release
        var waiting = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetSoloAsync(false, CancellationToken.None));
        Assert.Contains("has not been reported by the console yet", waiting.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.LedStatus, E9bBody(22172));
        await soloLit.Task.WaitAsync(Timeout);
        await rig.Device.SetSoloAsync(false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        // SmartSoft's SOLO press and release (E9 seq 36386, 36391), twice, with this session's counter
        var payloads = Recorded(rig.Pipe).Where(t => t.ToConsole && t.Data.Length != 12).Select(t => t.Data).ToArray();
        Assert.Equal(new[] { E9Payload(36386, 0x00), E9Payload(36391, 0x01), E9Payload(36386, 0x02), E9Payload(36391, 0x03) }, payloads);
    }

    [Fact]
    public void Live_control_needs_timings_no_slower_than_SmartSofts()
    {
        // The first review of SC-2 item 10, minor: the write timings had no upper bound. Faster settings end at the floors.
        Assert.Null(new UsbDeviceOptions().LiveControlTimingProblem());
        Assert.Null(new UsbDeviceOptions { PollIntervalMilliseconds = 1, WriteIntervalMilliseconds = 1 }.LiveControlTimingProblem());
        var slow = new UsbDeviceOptions { PollIntervalMilliseconds = 100, WriteIntervalMilliseconds = 500, ReplyTimeoutMilliseconds = 1000 };
        var problem = slow.LiveControlTimingProblem();
        Assert.Contains("PollIntervalMilliseconds=100, at most 31", problem, StringComparison.Ordinal);
        Assert.Contains("WriteIntervalMilliseconds=500, at most 46", problem, StringComparison.Ordinal);
        Assert.Contains("ReplyTimeoutMilliseconds=1000, at most 500", problem, StringComparison.Ordinal);   // the second pass of item 10, MINOR C

        var today = new DateOnly(2026, 10, 30);
        Assert.Throws<InvalidOperationException>(() => ServiceCollectionExtensions.ValidateLiveControlDate("2026-10-30", usbMode: true, today, slow));
        ServiceCollectionExtensions.ValidateLiveControlDate(null, usbMode: true, today, slow);   // read-only: any cadence above the floors

        var clock = new VirtualClock();
        slow.LiveControlDate = Today(clock);
        var device = Create(() => new BlockingPipe(), slow, clock: clock);
        Assert.Equal(DeviceCommands.None, device.SupportedCommands);   // a device built around the start-up check stays read-only
    }

    [Fact]
    public async Task A_write_waits_while_the_console_has_listed_its_fader_mode_it_has_not_delivered_and_goes_after_it()
    {
        // SC-2 condition 5 (the second pass of item 10, MINOR A): ConsoleStatus listed and not fetched is a fader mode or page
        // not known yet. The console lists ConsoleStatus again (the same E9b seq 796 body: channel mode, bank 1-24, page 1); the
        // MASTER write goes only after that object is in.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetMasterAsync(0x80, CancellationToken.None);
        rig.Console.Report((int)HspObjectId.ConsoleStatus, E9bBody(796));
        rig.Console.Resume();
        await rig.Console.DataWritten.Task.WaitAsync(Timeout);
        await rig.Console.PollAfterData.Task.WaitAsync(Timeout);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(1, rig.Device.LastSession!.WritesSent);
        Assert.Contains("write held back: the console listed a new fader mode or page (ConsoleStatus) that is not fetched yet", rig.Log.Notes);
        var transfers = Recorded(rig.Pipe);
        var statusFetch = transfers.FindLastIndex(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)HspObjectId.ConsoleStatus);
        var header = transfers.FindIndex(IsDataHeader);
        Assert.True(statusFetch > 0 && header > statusFetch, $"the write went at {header}, ConsoleStatus was fetched at {statusFetch}");
    }

    [Fact]
    public async Task Channel_fader_1_waits_while_the_console_has_listed_FaderInfo_it_has_not_delivered_and_goes_after_it()
    {
        // SC-2 condition 6 (the second pass of item 10, MINOR A): channel fader 1 is sent only with the MASTER at full, so a
        // listed FaderInfo (where the MASTER is reported) that is not fetched yet holds it, and with SC-5 its first event, the
        // matching event at its reported level, is held the same way. The console lists FaderInfo again (the same E9b seq 11628
        // body: MASTER and BUMPS at full); the events go only after that object is in.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(1, 0x40, CancellationToken.None);
        rig.Console.Report((int)HspObjectId.FaderInfo, E9bBody(11628));
        rig.Console.Resume();
        await rig.Console.DataWritten.Task.WaitAsync(Timeout);
        await rig.Console.PollAfterData.Task.WaitAsync(Timeout);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, rig.Device.LastSession!.WritesSent);   // the matching event, then the level
        Assert.Equal(0, rig.Device.LastSession.WritesDropped);
        Assert.Contains("write held back: the console listed FaderInfo or ChannelLevels (the level a re-assigned fader is matched at) and it is not fetched yet", rig.Log.Notes);
        var transfers = Recorded(rig.Pipe);
        var faderInfoFetch = transfers.FindLastIndex(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)HspObjectId.FaderInfo);
        var header = transfers.FindIndex(IsDataHeader);
        Assert.True(faderInfoFetch > 0 && header > faderInfoFetch, $"the event went at {header}, FaderInfo was fetched at {faderInfoFetch}");
    }

    /// <summary>
    /// A delay hook that stops the device (Disconnect, as the host does) inside the first write slot wait that
    /// <paramref name="which"/> selects: the stop arrives while the loop waits for that write's slot. The Disconnect task is
    /// handed back through <paramref name="disconnect"/> (Disconnect cancels the session's stop token before it first awaits).
    /// </summary>
    private static Func<VirtualClock, Func<TimeSpan, CancellationToken, Task>> StopDuringOneWriteSlot(Func<UsbSmartFadeDevice?> device, Func<HspReadSession, bool> which, StrongBox<Task?> disconnect)
    {
        var once = 1;
        return clock => (wait, _) =>
        {
            var dev = device();
            if (dev?.LastSession is { State: HspReadSessionState.ReadyToSend } session
                && session.PeekNextKind() == HspMessageType.Data && which(session) && Interlocked.Exchange(ref once, 0) == 1)
            {
                disconnect.Value = dev.DisconnectAsync(CancellationToken.None);
            }

            clock.Advance(wait);
            return Task.CompletedTask;
        };
    }

    [Fact]
    public async Task A_stop_during_a_writes_slot_wait_chooses_no_new_write_and_drops_the_queued_one()
    {
        // The first review of SC-2 item 10, minor (5), and the second pass, MINOR B: a stop that arrives while the loop waits
        // for a write's slot is handled before anything is chosen; the MASTER write queued for that slot never goes out.
        UsbSmartFadeDevice? device = null;
        var disconnect = new StrongBox<Task?>();
        var rig = LiveConsole(delay: StopDuringOneWriteSlot(() => device, session => session.HeldKey is null, disconnect));
        device = rig.Device;
        await device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await device.SetMasterAsync(0x80, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => disconnect.Value is not null);
        await disconnect.Value!.WaitAsync(Timeout);

        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.Equal(0, device.LastSession!.WritesSent);
        Assert.Equal(0, device.LastSession.QueuedWrites);
        Assert.Contains("stop: 1 queued write(s) dropped before they started", rig.Log.Notes);
        Assert.DoesNotContain(Recorded(rig.Pipe), IsDataHeader);
    }

    [Fact]
    public async Task A_stop_during_a_releases_slot_wait_still_sends_the_release()
    {
        // SC-2 condition 4 with the second pass's MINOR B: the stop arrives while the loop waits for the release's slot; the
        // release of the bump 7 press that already went is the one write a stop does not take away.
        UsbSmartFadeDevice? device = null;
        var disconnect = new StrongBox<Task?>();
        var rig = LiveConsole(delay: StopDuringOneWriteSlot(() => device, session => session.HeldKey is not null, disconnect));
        device = rig.Device;
        await device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await device.BumpChannelAsync(7, pressed: true, CancellationToken.None);
        await WaitUntil(() => disconnect.Value is not null);
        await disconnect.Value!.WaitAsync(Timeout);

        Assert.Equal(ConnectionStatus.Disconnected, device.Status);
        Assert.Equal(2, device.LastSession!.WritesSent);   // the press, then its release in the very slot the stop arrived in
        Assert.Null(device.LastSession.HeldKey);
        var transfers = Recorded(rig.Pipe);
        Assert.Equal(2, transfers.Count(IsDataHeader));
        var releaseHeader = transfers.FindLastIndex(IsDataHeader);
        Assert.Equal(E9Payload(19436, 0x01), transfers[releaseHeader + 1].Data);   // SmartSoft's bump 7 release (E9 seq 19436), this session's second write
        Assert.DoesNotContain(rig.Log.Notes, note => note.StartsWith("stop:", StringComparison.Ordinal) && note.Contains("dropped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_incomplete_write_marker_that_cannot_be_read_refuses_to_connect_until_it_can()
    {
        // The second pass of SC-2 item 10, MINOR B: a marker that exists but cannot be read refuses too, without opening the
        // console; readable again, it still records the incomplete write; only its removal lets the device connect.
        var directory = Path.Combine(Path.GetTempPath(), "smartfade-tests", Guid.NewGuid().ToString("N"));
        var marker = new IncompleteWriteMarker(directory);
        try
        {
            marker.Write(DateTimeOffset.UnixEpoch, "A Data write did not complete (test)");
            var opened = 0;
            var device = Create(() =>
            {
                opened++;
                return new ScriptedPipe(replies: Enumerable.Repeat(Idle(), 100).ToArray());
            }, marker: marker);
            var refusals = new List<ConnectionState>();
            device.ConnectionChanged += (_, state) => refusals.Add(state);

            using (new FileStream(marker.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await device.ConnectAsync(CancellationToken.None);
            }

            Assert.Contains("could not be read", Assert.Single(refusals).Error, StringComparison.Ordinal);
            Assert.Equal(0, opened);
            Assert.Equal(ConnectionStatus.Disconnected, device.Status);

            refusals.Clear();
            await device.ConnectAsync(CancellationToken.None);
            Assert.Contains("records a Data write that did not complete", Assert.Single(refusals).Error, StringComparison.Ordinal);
            Assert.Equal(0, opened);

            File.Delete(marker.FilePath);
            var connected = WaitForStatus(device, ConnectionStatus.Connected);
            await device.ConnectAsync(CancellationToken.None);
            await connected;
            Assert.Equal(1, opened);
            await device.DisconnectAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static readonly Lazy<CaptureFile> E9c = new(() => CaptureFile.Load(TestPaths.Capture("2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl")));

    private static readonly Lazy<CaptureFile> E9d = new(() => CaptureFile.Load(TestPaths.Capture("2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl")));

    private static readonly Lazy<CaptureFile> E9e = new(() => CaptureFile.Load(TestPaths.Capture("2026-11-02-e9e-smartsoft-channel-bumps-spot-check.jsonl")));

    /// <summary>The body of the object the console delivered in the capture's payload frame with that JSONL seq.</summary>
    private static byte[] Body(Lazy<CaptureFile> capture, int seq) => Frame(capture.Value, seq, CaptureDirection.In)[5..];

    /// <summary>SmartSoft's payload with that JSONL seq, with this session's counter (and a value and previous byte) in place of its own.</summary>
    private static byte[] WithCounter(Lazy<CaptureFile> capture, int seq, byte counter, byte? value = null, byte? previous = null)
    {
        var bytes = Frame(capture.Value, seq, CaptureDirection.Out).ToArray();   // a copy: the capture's bytes stay as recorded
        bytes[0] = counter;
        if (value is { } v)
        {
            bytes[6] = v;
        }

        if (previous is { } p)
        {
            bytes[7] = p;
        }

        return bytes;
    }

    private static byte[][] Payloads(TimedPipe pipe) =>
        Recorded(pipe).Where(t => t.ToConsole && t.Data.Length != 12).Select(t => t.Data).ToArray();

    /// <summary>The virtual time between the Data headers of the first two writes.</summary>
    private static TimeSpan FirstTwoHeadersApart(TimedPipe pipe)
    {
        var headers = Recorded(pipe).Where(IsDataHeader).Select(t => t.At).ToArray();
        return headers[1] - headers[0];
    }

    /// <summary>A task that completes when the device reports that its physical faders control <paramref name="bank"/>.</summary>
    private static Task BankReported(UsbSmartFadeDevice device, FaderBank bank)
    {
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.StateChanged += (_, change) =>
        {
            if (change.FaderBank == bank)
            {
                reported.TrySetResult();
            }
        };
        return reported.Task.WaitAsync(Timeout);
    }

    /// <summary>The stand-in console answers FADERS 25-48 as the 1248 answered SmartSoft's in E9d: ConsoleStatus seq 18715 (bank 25-48), LedStatus seq 18719 (block 56 lit).</summary>
    private static void AnswersFaders25To48(AnsweringPipe console) =>
        console.OnPress((byte)ClearedKey.Faders25To48, () =>
        {
            console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 18715));
            console.Report((int)HspObjectId.LedStatus, Body(E9d, 18719));
        });

    [Fact]
    public async Task A_channel_of_bank_25_48_switches_the_bank_first_then_matches_its_fader_then_sends_the_drag()
    {
        // SC-5 condition 1. The console shows bank 1-24 (E9b seq 796). A drag of channel 30 queues FADERS 25-48 as a tap
        // (a, b); nothing else goes until the console has reported bank 25-48 with its LEDs (c; the stand-in answers the press
        // with E9d's ConsoleStatus seq 18715 and LedStatus seq 18719, the console's own answer to SmartSoft's key there); then
        // physical fader 6's first event carries channel 30's reported level (d; ChannelLevels[30] = FaderInfo record 6 = 0x00),
        // and only then the UI's newest level. As SmartSoft's bytes: its FADERS 25-48 press and release (E9d seq 18706, 18711)
        // and its first event of index 0x05 in bank 25-48 (E9d seq 21637), with this session's counter, value and previous byte.
        var rig = LiveConsole();
        AnswersFaders25To48(rig.Console);
        var bank = BankReported(rig.Device, FaderBank.Channels25To48);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(30, 0x40, CancellationToken.None);
        await rig.Device.SetChannelLevelAsync(30, 0x48, CancellationToken.None);   // a newer level while the switch waits: the newest wins
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x10, CancellationToken.None));
        Assert.Contains("automatic bank switch to channel mode, bank 25-48 is in progress", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetMasterAsync(0x80, CancellationToken.None));
        rig.Console.Resume();
        await bank;
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(4, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[]
        {
            WithCounter(E9d, 18706, 0x00), WithCounter(E9d, 18711, 0x01),
            WithCounter(E9d, 21637, 0x02, value: 0x00, previous: 0x00), WithCounter(E9d, 21637, 0x03, value: 0x48, previous: 0x00),
        }, Payloads(rig.Pipe));

        // the matching event went only after the console's report was in: ConsoleStatus and LedStatus fetched before its header
        var transfers = Recorded(rig.Pipe);
        var headers = transfers.Select((t, i) => (t, i)).Where(x => IsDataHeader(x.t)).Select(x => x.i).ToArray();
        int LastFetch(HspObjectId id) => transfers.FindLastIndex(headers[2], t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)id);
        Assert.True(LastFetch(HspObjectId.ConsoleStatus) > headers[1] && LastFetch(HspObjectId.LedStatus) > headers[1], "the matching event went before the console's report");
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("automatic bank switch (SC-5 condition 1) for channel fader 30: FADERS 25-48 sent", StringComparison.Ordinal));
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("automatic bank switch done: the console reports channel mode, bank 25-48", StringComparison.Ordinal));
        Assert.Contains("matched channel fader 30 at its reported level 0x00 (SC-5 condition 1 d, F172)", rig.Log.Notes);
    }

    [Fact]
    public async Task An_event_queued_behind_our_bank_key_waits_for_the_consoles_report_and_never_reaches_the_new_bank()
    {
        // SC-5 condition 1 (c), the barrier. Channel 5's matching event goes first; its drag (0x20) is then queued behind
        // FADERS 25-48, which channel 30 asked for. The console answers the key late (the stand-in reports bank 25-48 three
        // polls after the press, slower than in any capture): the gate holds the drag until that report, and its own
        // condition then drops it, since index 0x04 would now move channel 29; channel 30 is matched and goes. As SmartSoft's
        // bytes: E9d seq 11362 (index 0x04 in bank 1-24), 18706 / 18711 (FADERS 25-48), 21637 (index 0x05 in bank 25-48).
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Faders25To48, () => rig.Console.ReportAfterPolls(
            3, ((int)HspObjectId.ConsoleStatus, Body(E9d, 18715)), ((int)HspObjectId.LedStatus, Body(E9d, 18719))));
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(5, 0x20, CancellationToken.None);
        await rig.Device.SetChannelLevelAsync(30, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 5);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 11362, 0x00, value: 0x00, previous: 0x00), WithCounter(E9d, 18706, 0x01), WithCounter(E9d, 18711, 0x02),
            WithCounter(E9d, 21637, 0x03, value: 0x00, previous: 0x00), WithCounter(E9d, 21637, 0x04, value: 0x40, previous: 0x00),
        }, Payloads(rig.Pipe));
        Assert.Contains("write held back: the console has not reported channel mode, bank 25-48 since FADERS 25-48 went (SC-5 condition 1 c)", rig.Log.Notes);
        Assert.Equal(1, rig.Device.LastSession!.WritesDropped);
        Assert.Contains("queued write dropped unsent: the console shows channel mode, bank 25-48: channel fader 5 needs channel mode, bank 1-24", rig.Log.Notes);
    }

    [Fact]
    public async Task An_automatic_switch_the_console_does_not_report_within_two_seconds_ends_the_session_as_a_deviation()
    {
        // SC-5 condition 1 (c): the stand-in console does not answer FADERS 25-48 (no ConsoleStatus, no LEDs). Two seconds
        // after the key's press, on the loop's clock, the session ends with that reason; nothing but the tap went out.
        var rig = LiveConsole();
        var ended = WaitForStatus(rig.Device, ConnectionStatus.Error);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetChannelLevelAsync(30, 0x40, CancellationToken.None);
        var state = await ended;

        Assert.Contains("did not report channel mode, bank 25-48 with its LEDs within 2 s of FADERS 25-48", state.Error, StringComparison.Ordinal);
        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[] { WithCounter(E9d, 18706, 0x00), WithCounter(E9d, 18711, 0x01) }, Payloads(rig.Pipe));
        var transfers = Recorded(rig.Pipe);
        Assert.True(transfers[^1].At - transfers.First(IsDataHeader).At >= UsbSmartFadeDevice.BankSwitchReportBound);
    }

    [Fact]
    public async Task A_memory_fader_switches_to_memory_mode_first_and_is_matched_at_its_FaderInfo_level()
    {
        // FADERS MEMS from bank 1-24 (E9d seq 29032 / 29041) and the console's answer there (ConsoleStatus seq 29039: memory
        // mode, page 1; LedStatus seq 29045). Memory fader 3 of page 1: FaderInfo record 3 bytes 0 and 1 read 0x00 (E9b seq
        // 11628, not listed again: the console lists what changed, F57), so its matching event carries 0x00, then the UI's
        // 0x50 (E9d seq 30522's layout: SmartSoft's first event of index 0x02 in memory mode).
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.FadersMems, () =>
        {
            rig.Console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 29039));
            rig.Console.Report((int)HspObjectId.LedStatus, Body(E9d, 29045));
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetMemoryLevelAsync(1, 3, 0x50, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 29032, 0x00), WithCounter(E9d, 29041, 0x01),
            WithCounter(E9d, 30522, 0x02, value: 0x00, previous: 0x00), WithCounter(E9d, 30522, 0x03, value: 0x50, previous: 0x00),
        }, Payloads(rig.Pipe));
        Assert.Contains("matched memory fader 3 of page 1 at its reported level 0x00 (SC-5 condition 1 d, F172)", rig.Log.Notes);
    }

    [Fact]
    public async Task A_memory_fader_goes_only_on_the_page_shown_and_only_while_its_physical_fader_holds_that_pages_memory()
    {
        // F113 and H69, with E9c S4c's state: memory mode, page 2 shown (ConsoleStatus seq 80286), and physical fader 3 still
        // holding memory 3 of page 1 at 0x7F (FaderInfo seq 80290: record 3 byte 3 = 0, the other records 1). Memory fader 3
        // of page 2 and its bump are refused, so is page 1; memory fader 4 of page 2 is matched at 0x00 and goes (E9d seq
        // 30916's layout, index 0x03 in memory mode).
        var rig = LiveConsole();
        var memories = BankReported(rig.Device, FaderBank.Memories);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        rig.Console.Report((int)HspObjectId.ConsoleStatus, Body(E9c, 80286));
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E9c, 80290));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9c, 80298));
        rig.Console.Resume();
        await memories;
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 2);

        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetMemoryLevelAsync(2, 3, 0x10, CancellationToken.None));
        Assert.Contains("physical fader 3 still holds memory 3 of page 1 (FaderInfo record 3 byte 3, H69)", held.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.BumpMemoryAsync(2, 3, true, CancellationToken.None));
        var page = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetMemoryLevelAsync(1, 4, 0x10, CancellationToken.None));
        Assert.Contains("the console shows memory page 2, not 1", page.Message, StringComparison.Ordinal);
        await rig.Device.SetMemoryLevelAsync(2, 4, 0x10, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9d, 30916, 0x00, value: 0x00, previous: 0x00), WithCounter(E9d, 30916, 0x01, value: 0x10, previous: 0x00) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_held_bump_goes_until_the_UI_lets_go_one_at_a_time_and_blocks_a_bank_switch()
    {
        // SC-5 class B3 and condition 2. Bump 7 pressed in the UI is held (SmartSoft held it 4.67 s, E9d seq 46589); while it
        // is held a second bump and a bank switch are refused; the release goes when the UI lets go (E9d seq 47004), once.
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump7, () => rig.Console.PauseAtNextPoll());
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // the press is out; the loop waits inside the next poll
        Assert.Equal(ClearedKey.Bump7, rig.Device.LastSession!.HeldKey);
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.BumpChannelAsync(10, true, CancellationToken.None));
        Assert.Contains("another bump is held", second.Message, StringComparison.Ordinal);
        var switching = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(30, 0x10, CancellationToken.None));
        Assert.Contains("a bump is held", switching.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.PressFadersMemsAsync(CancellationToken.None));
        await rig.Device.BumpChannelAsync(7, false, CancellationToken.None);
        await rig.Device.BumpChannelAsync(7, false, CancellationToken.None);   // once: a second release asks for nothing
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[] { WithCounter(E9d, 46589, 0x00), WithCounter(E9d, 47004, 0x01) }, Payloads(rig.Pipe));
        Assert.True(FirstTwoHeadersApart(rig.Pipe) < HspReadLoop.MaxHold - HspReadLoop.HoldReleaseLead);
        Assert.DoesNotContain(rig.Log.Notes, n => n.StartsWith("hold bound", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_held_bump_is_released_by_the_device_within_three_seconds_whatever_the_UI_does()
    {
        // SC-5 condition 2: the UI never lets go. On the loop's clock the release is asked for 2.75 s after the press header
        // and goes at the next write slot, so the press-to-release time stays within the 3 s bound; the UI's late release then
        // finds nothing to release.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.BumpChannelAsync(7, false, CancellationToken.None);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[] { WithCounter(E9d, 46589, 0x00), WithCounter(E9d, 47004, 0x01) }, Payloads(rig.Pipe));
        var held = FirstTwoHeadersApart(rig.Pipe);
        Assert.InRange(held, HspReadLoop.MaxHold - HspReadLoop.HoldReleaseLead, HspReadLoop.MaxHold);
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("hold bound: the release of Bump7 is due", StringComparison.Ordinal));
        Assert.DoesNotContain(rig.Log.Notes, n => n.StartsWith("deviation:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_held_bump_is_released_before_the_session_stops()
    {
        // SC-5 condition 2 ("and before any Disconnect", SC-2 condition 4's stop path): a Disconnect while bump 7 is held sends
        // its release first, then the session ends at an idle point.
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump7, () => rig.Console.PauseAtNextPoll());
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);
        var disconnect = rig.Device.DisconnectAsync(CancellationToken.None);   // the stop token is cancelled before it first awaits
        rig.Console.Resume();
        await disconnect.WaitAsync(Timeout);

        Assert.Equal(ConnectionStatus.Disconnected, rig.Device.Status);
        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Null(rig.Device.LastSession.HeldKey);
        Assert.Equal(new[] { WithCounter(E9d, 46589, 0x00), WithCounter(E9d, 47004, 0x01) }, Payloads(rig.Pipe));
        Assert.True(FirstTwoHeadersApart(rig.Pipe) < HspReadLoop.MaxHold - HspReadLoop.HoldReleaseLead);
    }

    [Fact]
    public async Task A_held_bump_is_released_at_once_when_the_console_reports_another_bank()
    {
        // SC-5 condition 2 ("a hold cannot span a bank switch"): bump 7 is held in bank 1-24 when FADERS 25-48 is pressed on the
        // panel (the stand-in reports E9d's ConsoleStatus seq 18715 and LedStatus seq 18719): the release goes at once.
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump7, () => rig.Console.PauseAtNextPoll());
        var bank = BankReported(rig.Device, FaderBank.Channels25To48);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);
        rig.Console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 18715));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9d, 18719));
        rig.Console.Resume();
        await bank;
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9d, 46589, 0x00), WithCounter(E9d, 47004, 0x01) }, Payloads(rig.Pipe));
        Assert.True(FirstTwoHeadersApart(rig.Pipe) < HspReadLoop.MaxHold - HspReadLoop.HoldReleaseLead);
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("bump 7 released at once: the console shows channel mode, bank 25-48", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_bump_of_bank_25_48_switches_first_and_is_held_once_the_console_has_reported_the_bank()
    {
        // Channel 48's bump from bank 1-24: FADERS 25-48 (E9e seq 19934 / 19939), the console's answer (E9d seq 18715 / 18719),
        // then physical bump 24 held (E9e seq 20709, the bump under the fader labelled 48) until the UI lets go (E9e seq 20835).
        var rig = LiveConsole();
        AnswersFaders25To48(rig.Console);
        rig.Console.OnPress((byte)ClearedKey.Bump24, () => rig.Console.PauseAtNextPoll());
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.BumpChannelAsync(48, true, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // the tap and the hold's press are out
        Assert.Equal(3, rig.Device.LastSession!.WritesSent);
        await rig.Device.BumpChannelAsync(48, false, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9e, 19934, 0x00), WithCounter(E9e, 19939, 0x01), WithCounter(E9e, 20709, 0x02), WithCounter(E9e, 20835, 0x03) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task FADERS_25_48_is_a_tap_from_bank_1_24_only_and_nothing_goes_in_NEXT_mode()
    {
        // SC-5 class B4: from bank 1-24 a tap (E9d seq 18706 / 18711); from bank 25-48 or memory mode refused (SmartSoft sent it
        // only from bank 1-24, E9b to E9e), so is a channel of bank 25-48 from memory mode; in NEXT mode (E9c seq 94604:
        // ConsoleStatus body[6] = 1, F173) every control is refused.
        var rig = LiveConsole();
        AnswersFaders25To48(rig.Console);
        var upper = BankReported(rig.Device, FaderBank.Channels25To48);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.PressFaders25To48Async(CancellationToken.None);
        await upper;
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2 && rig.Console.Served((int)HspObjectId.LedStatus) == 2);
        var again = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.PressFaders25To48Async(CancellationToken.None));
        Assert.Contains("FADERS 25-48 was sent from a PC only from channel mode, bank 1-24", again.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.PressFadersMemsAsync(CancellationToken.None));

        var memories = BankReported(rig.Device, FaderBank.Memories);
        rig.Console.Report((int)HspObjectId.ConsoleStatus, Body(E9d, 29039));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9d, 29045));
        await memories;
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 3);
        var fromMemories = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(30, 0x10, CancellationToken.None));
        Assert.Contains("the console shows memory mode: FADERS 25-48 was sent from a PC only from channel mode, bank 1-24", fromMemories.Message, StringComparison.Ordinal);

        var next = BankReported(rig.Device, FaderBank.Channels1To24);
        rig.Console.Report((int)HspObjectId.ConsoleStatus, Body(E9c, 94604));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9c, 94610));
        await next;
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 4);
        foreach (var refusal in new Func<Task>[]
        {
            () => rig.Device.SetMasterAsync(0x80, CancellationToken.None),
            () => rig.Device.SetChannelLevelAsync(1, 0x10, CancellationToken.None),
            () => rig.Device.BumpChannelAsync(7, true, CancellationToken.None),
            () => rig.Device.PressGoAsync(CancellationToken.None),
            () => rig.Device.SetSoloAsync(true, CancellationToken.None),
            () => rig.Device.PressFadersMemsAsync(CancellationToken.None),
        })
        {
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(refusal);
            Assert.Contains("NEXT mode is on (ConsoleStatus body[6], F173)", refused.Message, StringComparison.Ordinal);
        }

        await rig.Device.DisconnectAsync(CancellationToken.None);
        Assert.Equal(new[] { WithCounter(E9d, 18706, 0x00), WithCounter(E9d, 18711, 0x01) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task IND_1_is_a_tap_from_a_steady_LED_and_the_next_waits_until_the_console_has_reported_it()
    {
        // SC-5 class B5 (F115, F175): IND 1's LED reads off (E9b seq 11636, block 72), so a wish for on is one tap, SmartSoft's
        // press and release of E9c seq 90315 / 90324 with this session's counter; the second wish waits until the console has
        // reported the Independents object and the LEDs after IND 1's press (E9c seq 90322, 90328).
        var rig = LiveConsole();
        var indOn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Device.StateChanged += (_, change) =>
        {
            if (change.Masters is { Independent1: true } || change.Independents is { Independent1: true })
            {
                indOn.TrySetResult();
            }
        };
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetIndependentAsync(1, true, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        var waiting = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetIndependentAsync(1, false, CancellationToken.None));
        Assert.Contains("the previous IND 1 press has not been reported by the console yet", waiting.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.Independents, Body(E9c, 90322));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9c, 90328));
        await indOn.Task.WaitAsync(Timeout);
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.LedStatus) == 2);
        await rig.Device.SetIndependentAsync(1, false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9c, 90315, 0x00), WithCounter(E9c, 90324, 0x01), WithCounter(E9c, 90315, 0x02), WithCounter(E9c, 90324, 0x03) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task An_IND_whose_LED_and_Independents_object_disagree_is_refused()
    {
        // SC-5 class B5, the known state: the Independents object says IND 1 on (E9c seq 90322) while its LED reads off (E9b
        // seq 11636): neither wish is sent.
        var rig = LiveConsole();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Device.StateChanged += (_, change) =>
        {
            if (change.Independents is not null || change.Masters is { Independent1: true })
            {
                reported.TrySetResult();
            }
        };
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        rig.Console.Report((int)HspObjectId.Independents, Body(E9c, 90322));
        await reported.Task.WaitAsync(Timeout);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetIndependentAsync(1, true, CancellationToken.None));
        Assert.Contains("its state is not known (SC-5 class B5)", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetIndependentAsync(1, false, CancellationToken.None));
        await rig.Device.DisconnectAsync(CancellationToken.None);
        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
    }

    [Fact]
    public async Task A_channel_fader_needs_the_consoles_channel_levels_and_one_level_to_match()
    {
        // SC-5 condition 1 (d): without ChannelLevels in the session the level a fader's first event must carry is unknown:
        // refused. With ChannelLevels reading channel 1 at 0x17 (E9b seq 8063) while FaderInfo record 1 reads 0x00 (E9b seq
        // 11628), the two reports disagree (another source drives the channel): refused too. Nothing is sent.
        var rig = LiveConsole(withChannelLevels: false);
        var channels = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Device.StateChanged += (_, change) =>
        {
            if (change.ChannelLevels is { Count: > 0 })
            {
                channels.TrySetResult();
            }
        };
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(1, 0x10, CancellationToken.None));
        Assert.Contains("has not reported its channel levels in this session (ChannelLevels)", unknown.Message, StringComparison.Ordinal);
        rig.Console.Report((int)HspObjectId.ChannelLevels, E9bBody(8063));
        await channels.Task.WaitAsync(Timeout);
        var ambiguous = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(1, 0x10, CancellationToken.None));
        Assert.Contains("channel 1 reads 0x17 (ChannelLevels) and physical fader 1 0x00 (FaderInfo record 1): another source drives the channel", ambiguous.Message, StringComparison.Ordinal);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
    }

    /// <summary>The second read-only session of our software (E8b): BLACKOUT on and off with channel fader 5 at full (F125).</summary>
    private static readonly Lazy<CaptureFile> E8b = new(() => CaptureFile.Load(TestPaths.Capture("2026-10-30-e8b-second-read-only-session.jsonl")));

    /// <summary>A task that completes when the device reports key LEDs that satisfy <paramref name="condition"/>.</summary>
    private static Task KeysReportedAs(UsbSmartFadeDevice device, Func<PanelKeys, bool> condition)
    {
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.StateChanged += (_, change) =>
        {
            if (change.Keys is { } keys && condition(keys))
            {
                reported.TrySetResult();
            }
        };
        return reported.Task.WaitAsync(Timeout);
    }

    /// <summary>The stand-in console answers bump 2's press as the 1248 answered SmartSoft's in E9e: FaderInfo seq 17675 (record 2 `ff 00 00 00`) and ChannelLevels seq 17679 (channel 2 at 0xFF), F174.</summary>
    private static void AnswersBump2WithItsHold(AnsweringPipe console) =>
        console.OnPress((byte)ClearedKey.Bump2, () =>
        {
            console.Report((int)HspObjectId.FaderInfo, Body(E9e, 17675));
            console.Report((int)HspObjectId.ChannelLevels, Body(E9e, 17679));
        });

    /// <summary>A task that completes when the device reports channel <paramref name="channel"/> at <paramref name="level"/>.</summary>
    private static Task ChannelReported(UsbSmartFadeDevice device, int channel, byte level)
    {
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.StateChanged += (_, change) =>
        {
            if (change.ChannelLevels is { } levels && levels.TryGetValue(channel, out var now) && now == level)
            {
                reported.TrySetResult();
            }
        };
        return reported.Task.WaitAsync(Timeout);
    }

    [Fact]
    public async Task A_fader_waits_after_its_own_bump_until_the_console_has_reported_it_again_after_the_release()
    {
        // The first review of SC-5 item 10, major 1 (F174, VERIFIED): while bump 2 is held the console reports physical fader
        // 2's FaderInfo record as `ff 00 00 00` and channel 2 at 0xFF (the stand-in answers the press with E9e seq 17675 and
        // 17679, the console's answer to SmartSoft's press there), so the two agree and the matching level would be the bump's.
        // Channel fader 2 is refused while the bump is held; after the release while the console has listed and delivered
        // only PlaybackInfo (as E9e's first poll reply after the release, seq 17785, and its PlaybackInfo seq 17789), the
        // reports still reading the bump's 0xFF, not the 0x00 before the press; and still after FaderInfo seq 17796 alone
        // (record 2 at 0x00 again, ChannelLevels[2] still at 0xFF); once ChannelLevels seq 17800 (channel 2 at 0x00) is in too,
        // the drag goes: its matching event at 0x00, then 0x40 (the end rule of 2026-11-02, case a). As SmartSoft's bytes: its bump 2 press and release (E9e seq 17656,
        // 17782) and its event of index 0x01 in bank 1-24 (E9d seq 10006), with this session's counter and values.
        var rig = LiveConsole();
        AnswersBump2WithItsHold(rig.Console);
        rig.Console.OnRelease((byte)ClearedKey.Bump2, () => rig.Console.Report((int)HspObjectId.PlaybackInfo, Body(E9e, 17789)));
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        var inHold = ChannelReported(rig.Device, 2, 0xFF);
        await rig.Device.BumpChannelAsync(2, true, CancellationToken.None);
        await inHold;
        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(2, 0x40, CancellationToken.None));
        Assert.Contains("bump 2 is held: physical fader 2's reports carry the bump's level, not channel fader 2's", held.Message, StringComparison.Ordinal);

        const string Released = "bump 2 was released and the console's reports of physical fader 2 are not its content's again yet";
        await rig.Device.BumpChannelAsync(2, false, CancellationToken.None);
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.PlaybackInfo) == 1);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);   // PlaybackInfo is in, nothing else since the release
        var afterRelease = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(2, 0x40, CancellationToken.None));
        Assert.Contains(Released, afterRelease.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E9e, 17796));
        rig.Console.Resume();
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.FaderInfo) == 3);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);   // FaderInfo is in, ChannelLevels not yet
        var faderInfoOnly = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(2, 0x40, CancellationToken.None));
        Assert.Contains(Released, faderInfoOnly.Message, StringComparison.Ordinal);
        Assert.Equal(2, rig.Device.LastSession!.WritesSent);

        var after = ChannelReported(rig.Device, 2, 0x00);
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E9e, 17800));
        rig.Console.Resume();
        await after;
        await rig.Device.SetChannelLevelAsync(2, 0x40, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9e, 17656, 0x00), WithCounter(E9e, 17782, 0x01),
            WithCounter(E9d, 10006, 0x02, value: 0x00, previous: 0x00), WithCounter(E9d, 10006, 0x03, value: 0x40, previous: 0x00),
        }, Payloads(rig.Pipe));

        // the matching event went only after both objects were fetched again after the release
        var transfers = Recorded(rig.Pipe);
        var headers = transfers.Select((t, i) => (t, i)).Where(x => IsDataHeader(x.t)).Select(x => x.i).ToArray();
        int LastFetch(HspObjectId id) => transfers.FindLastIndex(headers[2], t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)id);
        Assert.True(LastFetch(HspObjectId.FaderInfo) > headers[1] && LastFetch(HspObjectId.ChannelLevels) > headers[1], "the matching event went before the console reported the fader again");
    }

    /// <summary>The owner's later E11b session (backend log, committed): bump 5's tests with channel fader 5 at 0x82 and at 0xFF (seq 4218 to 4609).</summary>
    private static readonly Lazy<CaptureFile> E11bLater = new(() => CaptureFile.Load(TestPaths.Capture("2026-11-02-e11b-second-live-control-session.backend-6.jsonl")));

    /// <summary>The stand-in console reports channel fader 5 at <paramref name="faderInfoSeq"/> / <paramref name="channelLevelsSeq"/> of the owner's later E11b session; the test waits until channel 5 reads <paramref name="level"/>.</summary>
    private static async Task ReportFader5(LiveRig rig, int faderInfoSeq, int channelLevelsSeq, byte level)
    {
        var reported = ChannelReported(rig.Device, 5, level);
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, faderInfoSeq));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, channelLevelsSeq));
        await reported;
    }

    [Fact]
    public async Task A_bump_that_changed_nothing_ends_its_shadow_after_two_quiet_poll_replies_and_the_drag_then_goes()
    {
        // The bump shadow's end rule of 2026-11-02, case (b), the owner's own test (E11b's later session, backend-6.jsonl): with
        // channel fader 5 at full (FaderInfo seq 4506, record 5 `ff 00 00 00` with BUMPS at 0xFF; ChannelLevels seq 4510,
        // channel 5 at 0xFF) bump 5 listed nothing at its press nor at its release (seq 4571 / 4609; OBSERVED once, log-only),
        // and under the first rule channel fader 5 stayed refused. The stand-in reports nothing either. Channel fader 5 is
        // refused until the release, and after it with one quiet poll reply; after the second it goes, its matching event at
        // 0xFF, the level before the press, then the drag. As SmartSoft's bytes: its code 0x04 press and release (E9d seq
        // 38764 / 38879, memory bump 5 there: the same physical bump in every bank, F171, F174) and its event of index 0x04
        // in bank 1-24 (E9d seq 11362).
        var rig = LiveConsole();
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () => rig.Console.PauseAtNextPoll());
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4506, 4510, 0xFF);

        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 1);
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // the release is out; the poll after it waits
        var released = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("bump 5 is held", released.Message, StringComparison.Ordinal);   // no poll reply since the release
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);   // one quiet reply
        var once = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("bump 5 was released and the console's reports of physical fader 5 are not its content's again yet", once.Message, StringComparison.Ordinal);
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);   // two
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01),
            WithCounter(E9d, 11362, 0x02, value: 0xFF, previous: 0x00), WithCounter(E9d, 11362, 0x03, value: 0x40, previous: 0xFF),
        }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_bump_on_a_fader_at_half_ends_its_shadow_when_the_level_before_the_press_is_reported_after_the_release()
    {
        // The end rule of 2026-11-02, case (a), the owner's other test of the same session: channel fader 5 at 0x82 (FaderInfo seq
        // 4146, ChannelLevels seq 4150); the console answered bump 5's press with record 5 `ff 00 00 00` and channel 5 at 0xFF
        // (seq 4227 / 4231) and its release with 0x82 again (seq 4296 / 4300). Refused during the hold; once both objects read
        // 0x82 after the release, the level before the press, the drag goes, matched at 0x82. As SmartSoft's bytes: E9d seq
        // 38764 / 38879 (bump 5) and 11362 (index 0x04).
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4227));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4231));
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4146, 4150, 0x82);

        var inHold = ChannelReported(rig.Device, 5, 0xFF);
        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        await inHold;
        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("bump 5 is held", held.Message, StringComparison.Ordinal);
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4296));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4300));
            rig.Console.PauseAfterServing((int)HspObjectId.ChannelLevels);
        });
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // right after the release's report: no quiet reply yet
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01),
            WithCounter(E9d, 11362, 0x02, value: 0x82, previous: 0x00), WithCounter(E9d, 11362, 0x03, value: 0x40, previous: 0x82),
        }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_taps_press_reported_after_its_release_keeps_the_shadow_until_the_level_before_the_press_is_reported()
    {
        // The end rule of 2026-11-02, a tap (F168: the press is listed even for a 47-77 ms tap, possibly after the release went
        // out): channel fader 5 at 0x82 (seq 4146 / 4150); the stand-in reports nothing at the press and the press's own report
        // (record 5 `ff 00 00 00`, channel 5 at 0xFF, seq 4227 / 4231) only after the release. That report reads the bump's level
        // and differs from the level before the press: refused. The release's report at 0x82 (seq 4296 / 4300) ends it: the drag
        // goes, matched at 0x82. As SmartSoft's bytes: E9d seq 38764 / 38879 (bump 5) and 11362 (index 0x04).
        var rig = LiveConsole();
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4227));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4231));
            rig.Console.PauseAfterServing((int)HspObjectId.ChannelLevels);
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4146, 4150, 0x82);

        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);   // a tap
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // the press's report is in, after the release
        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        var late = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("bump 5 was released and the console's reports of physical fader 5 are not its content's again yet", late.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4296));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4300));
        await rig.Console.ResumeAndPauseAfterServing((int)HspObjectId.ChannelLevels).WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01),
            WithCounter(E9d, 11362, 0x02, value: 0x82, previous: 0x00), WithCounter(E9d, 11362, 0x03, value: 0x40, previous: 0x82),
        }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_fader_moved_during_our_bump_ends_the_shadow_at_its_new_level()
    {
        // The end rule of 2026-11-02, case (a) with a level other than the bump's: channel fader 5 at 0x82 (seq 4146 / 4150), the
        // press answered at 0xFF (seq 4227 / 4231), and the fader moved on the panel during the hold, so the release's report
        // reads 0x54 (FaderInfo seq 4446, ChannelLevels seq 4450 of the same session): neither the bump's level nor the one before
        // the press, so the reports are the content's; the drag goes, matched at 0x54. As SmartSoft's bytes: E9d seq 38764 /
        // 38879 (bump 5) and 11362 (index 0x04).
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4227));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4231));
        });
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4446));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4450));
            rig.Console.PauseAfterServing((int)HspObjectId.ChannelLevels);
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4146, 4150, 0x82);

        var inHold = ChannelReported(rig.Device, 5, 0xFF);
        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        await inHold;
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // right after the release's report
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01),
            WithCounter(E9d, 11362, 0x02, value: 0x54, previous: 0x00), WithCounter(E9d, 11362, 0x03, value: 0x40, previous: 0x54),
        }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task Two_bumps_in_a_row_keep_the_level_before_the_first_press()
    {
        // The end rule of 2026-11-02 for two bumps of the same fader in a row: the second press is taken while the first bump's
        // shadow still waits and channel 5 still reads the first press's 0xFF, so the second shadow keeps the first one's level
        // before the press (0x82), not the 0xFF reported then. The stand-in answers the first press with 0xFF (seq 4227 / 4231)
        // and the second with 0x82 (seq 4296 / 4300: the first release's report, late), which ends the first shadow; the second
        // press's own 0xFF comes after its release and keeps the second shadow; 0x82 again ends it. As SmartSoft's bytes: E9d
        // seq 38764 / 38879 twice (code 0x04) and 11362 (index 0x04).
        var rig = LiveConsole();
        var presses = 0;
        rig.Console.OnPress((byte)ClearedKey.Bump5, () =>
        {
            var first = ++presses == 1;
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, first ? 4227 : 4296));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, first ? 4231 : 4300));
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4146, 4150, 0x82);

        var inHold = ChannelReported(rig.Device, 5, 0xFF);
        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        await inHold;
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);

        var restored = ChannelReported(rig.Device, 5, 0x82);
        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);   // channel 5 still reads 0xFF
        await restored;                                                        // the first release's report, at the second press
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4227));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4231));
            rig.Console.PauseAfterServing((int)HspObjectId.ChannelLevels);
        });
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await WaitUntil(() => rig.Console.Paused.Task.IsCompleted);   // the second press's report, after its release
        var late = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("bump 5 was released and the console's reports of physical fader 5 are not its content's again yet", late.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, 4296));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, 4300));
        await rig.Console.ResumeAndPauseAfterServing((int)HspObjectId.ChannelLevels).WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 6);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01), WithCounter(E9d, 38764, 0x02), WithCounter(E9d, 38879, 0x03),
            WithCounter(E9d, 11362, 0x04, value: 0x82, previous: 0x00), WithCounter(E9d, 11362, 0x05, value: 0x40, previous: 0x82),
        }, Payloads(rig.Pipe));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_bump_press_waits_for_its_faders_current_report(bool ledStatusFirst)
    {
        // The fourth review of SC-5 item 10, minor H: a write goes before pending fetches, so a press taken while FaderInfo and
        // ChannelLevels are listed and not fetched would keep the previous report as the content's level. Channel fader 5 stands
        // at 0x82 (seq 4146 / 4150 of the owner's later E11b session) and reaches full just as bump 5's press is queued, in one
        // of the two list orders that session shows. Without LedStatus (list seq 4019: FaderInfo seq 4023, record 5
        // `ff 00 00 00`; ChannelLevels seq 4027, channel 5 at 0xFF; most memory-mode lists carry no LedStatus either) the gate
        // passes at the slot right after the list, and only the press's own wait keeps it back: the transfer log notes it.
        // LedStatus first (list seq 9297, a move to full on the panel: LedStatus seq 9301, FaderInfo seq 9309, ChannelLevels seq
        // 9313): the gate waits for LedStatus and, after any object reply, finds the last poll reply older than two poll
        // periods by the write's slot (the fetch delay, the poll after it and the write after that poll come to at least 62 ms),
        // so the press waits there too. Either way the press goes after both objects, its shadow keeps 0xFF as the content's
        // level, the bump at full changes nothing (as in the owner's log, seq 4571 / 4609) and the shadow ends after two quiet
        // replies; the drag goes, matched at 0xFF. As SmartSoft's bytes: E9d seq 38764 / 38879 (code 0x04) and 11362 (index 0x04).
        var rig = LiveConsole();
        var afterRelease = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Console.OnRelease((byte)ClearedKey.Bump5, () => afterRelease.TrySetResult(rig.Console.PauseAtNextPoll()));
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        await ReportFader5(rig, 4146, 4150, 0x82);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        if (ledStatusFirst)
        {
            rig.Console.Report((int)HspObjectId.LedStatus, Body(E11bLater, 9301));
        }

        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E11bLater, ledStatusFirst ? 9309 : 4023));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E11bLater, ledStatusFirst ? 9313 : 4027));
        await rig.Device.BumpChannelAsync(5, true, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 1);
        await rig.Device.BumpChannelAsync(5, false, CancellationToken.None);
        await (await afterRelease.Task.WaitAsync(Timeout)).WaitAsync(Timeout);   // the release is out; the poll after it waits
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);         // one quiet reply
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);         // two
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9d, 38764, 0x00), WithCounter(E9d, 38879, 0x01),
            WithCounter(E9d, 11362, 0x02, value: 0xFF, previous: 0x00), WithCounter(E9d, 11362, 0x03, value: 0x40, previous: 0xFF),
        }, Payloads(rig.Pipe));

        // the press went only after the third FaderInfo and ChannelLevels of the session were fetched (the session's first, the
        // fader at 0x82, the fader at full)
        var transfers = Recorded(rig.Pipe);
        var press = transfers.FindIndex(IsDataHeader);
        int FetchesBefore(HspObjectId id) => transfers.Take(press).Count(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 2 && t.Data[4] == (int)id);
        Assert.Equal(3, FetchesBefore(HspObjectId.FaderInfo));
        Assert.Equal(3, FetchesBefore(HspObjectId.ChannelLevels));
        if (!ledStatusFirst)
        {
            Assert.Contains(rig.Log.Notes, n => n.Contains("bump 5's press waits for the level its fader reports now", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_fader_event_queued_behind_a_press_of_its_own_bump_is_dropped_unsent()
    {
        // The first review of SC-5 item 10, major 1, the queue: channel 2's matching event (0x00, read before the press) is
        // queued behind bump 2's press, which has not gone yet. The press goes, the event waits through the hold (nothing but
        // the release follows a press), and at its slot after the release it is dropped: the console has not reported physical
        // fader 2 again (its last reports carry the bump, E9e seq 17675 / 17679). Only the press and the release go (E9e seq
        // 17656, 17782); the UI's level is forgotten and the next drag matches again.
        var rig = LiveConsole();
        AnswersBump2WithItsHold(rig.Console);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.BumpChannelAsync(2, true, CancellationToken.None);
        await rig.Device.SetChannelLevelAsync(2, 0x40, CancellationToken.None);   // the press is queued, not sent: the reports are the fader's own
        Assert.Equal(2, rig.Device.LastSession!.QueuedWrites);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 1);
        await rig.Device.BumpChannelAsync(2, false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesDropped == 1);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[] { WithCounter(E9e, 17656, 0x00), WithCounter(E9e, 17782, 0x01) }, Payloads(rig.Pipe));
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("queued write dropped unsent: bump 2 ", StringComparison.Ordinal) && n.Contains("(F174)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_note_is_written_between_the_gate_and_a_writes_header_nor_inside_an_exchange()
    {
        // The first review of SC-5 item 10, major 2 (SC-2 condition 5 and its 62 ms analysis: nothing may wait between the gate
        // and the header, no I/O and no log note). The loop waits for a write's slot, then the session asks the gate and the
        // write's own condition, runs its sent callback and the header goes. The pipe, the transfer log and the loop's waits
        // are entered in one order: no note lies between a write's slot wait and its header, and the two notes the sent
        // callbacks leave (the bank key sent, the fader matched) come after the payload they describe. The re-check's minor B
        // (SC-1, the loop's own rule): nothing at all lies between a reply header that announces a payload and that payload,
        // although the console lists ConsoleStatus and LedStatus at the very poll after the bank key. The case is the
        // automatic switch for channel 30, with the bytes of A_channel_of_bank_25_48_switches_the_bank_first_then_matches_its_fader_then_sends_the_drag.
        var order = new List<string>();
        var rig = LiveConsole(order: order);
        AnswersFaders25To48(rig.Console);
        var bank = BankReported(rig.Device, FaderBank.Channels25To48);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetChannelLevelAsync(30, 0x48, CancellationToken.None);
        await bank;
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        string[] entries;
        lock (order)
        {
            entries = order.ToArray();
        }

        // a Data header: 12 bytes, type 1 (F131); its payload is the next entry
        var headers = Enumerable.Range(0, entries.Length).Where(i => entries[i].StartsWith("out 01", StringComparison.Ordinal) && entries[i].Length == "out ".Length + 24).ToArray();
        Assert.Equal(4, headers.Length);
        foreach (var header in headers)
        {
            var slotWait = Array.FindLastIndex(entries, header, e => e == "wait");
            var answer = Array.FindLastIndex(entries, header, e => e.StartsWith("in ", StringComparison.Ordinal));
            Assert.True(slotWait > answer, "the write's slot wait came before the console's last answer");
            Assert.DoesNotContain(entries[(slotWait + 1)..header], e => e.StartsWith("note ", StringComparison.Ordinal));
        }

        int NoteAt(string start) => Array.FindIndex(entries, e => e.StartsWith("note " + start, StringComparison.Ordinal));
        Assert.True(NoteAt("automatic bank switch (SC-5 condition 1) for channel fader 30: FADERS 25-48 sent") > headers[0] + 1, "the bank key's note came before its payload");
        Assert.True(NoteAt("matched channel fader 30 at its reported level 0x00") > headers[2] + 1, "the matching event's note came before its payload");

        // every reply header that announces a payload (its length word, F53) is followed by that payload and nothing else
        var payloadDue = false;
        var payloads = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (payloadDue)
            {
                Assert.True(entries[i].StartsWith("in ", StringComparison.Ordinal), $"\"{entries[i]}\" came between a reply header and its payload");
                payloadDue = false;
                payloads++;
                continue;
            }

            if (entries[i].StartsWith("in ", StringComparison.Ordinal))
            {
                var reply = Convert.FromHexString(entries[i]["in ".Length..]);
                payloadDue = (reply[2] | (reply[3] << 8)) > 0;
            }
        }

        Assert.True(payloads >= 8, $"only {payloads} payloads were read");
        var keyNote = NoteAt("automatic bank switch (SC-5 condition 1) for channel fader 30: FADERS 25-48 sent");
        Assert.StartsWith("in ", entries[keyNote - 1], StringComparison.Ordinal);   // written once the list after the key was read whole
    }

    [Fact]
    public async Task Channel_and_memory_faders_wait_while_BLACKOUT_is_on_and_go_once_it_reads_off_and_the_faders_are_reported()
    {
        // The re-check of SC-5 item 10, new major (F125, OBSERVED once, E8b): with channel fader 5 at full, BLACKOUT on made the
        // console report FaderInfo record 5 `00 00 00 00` with the MASTER at 0xFF (seq 26987), ChannelLevels[5] 0x00 (seq 26991)
        // and LED block 53 steady blue (seq 26999), in one list: the two reports agree at 0x00 and the MASTER is at full, so only
        // block 53 (F114) tells that the reported level is not the content's. Channel fader 5 and a memory fader are refused
        // while it reads so, and nothing is queued (no switch starts). BLACKOUT off, as E8b reported it in one list: block 53
        // `0A` x 6 (seq 27750), ChannelLevels[5] 0xFF (seq 27754), record 5 `FF 00 00 00` (seq 27762); then channel fader 5 goes,
        // its matching event at the content's 0xFF, then the drag. As SmartSoft's bytes: its event of index 0x04 in bank 1-24
        // (E9d seq 11362), with this session's counter and values.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        var on = KeysReportedAs(rig.Device, keys => keys.Blackout is { Light: KeyLight.Full, Blinking: false });
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E8b, 26987));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E8b, 26991));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E8b, 26999));
        await on;
        var channel = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("BLACKOUT's LED (block 53) reads Full: during a blackout the console reports every fader at zero with the MASTER at full (F125), so channel fader 5's reported level is not its own", channel.Message, StringComparison.Ordinal);
        var memory = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetMemoryLevelAsync(1, 1, 0x40, CancellationToken.None));
        Assert.Contains("BLACKOUT's LED (block 53) reads Full", memory.Message, StringComparison.Ordinal);
        Assert.Equal(0, rig.Device.LastSession!.QueuedWrites);

        rig.Console.Report((int)HspObjectId.LedStatus, Body(E8b, 27750));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E8b, 27754));
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E8b, 27762));
        await WaitUntil(() => rig.Console.Served((int)HspObjectId.FaderInfo) == 3);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);   // all three are in
        await rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9d, 11362, 0x00, value: 0xFF, previous: 0x00), WithCounter(E9d, 11362, 0x01, value: 0x40, previous: 0xFF) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_fader_event_queued_before_a_blackout_is_dropped_unsent()
    {
        // The re-check of SC-5 item 10, new major, the queue: channel fader 5 is matched (at 0x00, E9b seq 11632) and dragged
        // to 0x20; its next level, 0x30, is queued while the loop waits inside a poll, and the console then reports BLACKOUT on
        // (E8b seq 26987, 26991, 26999, one list). The gate holds the write while LedStatus is listed and unfetched; once it is
        // in, the event's own condition drops it, block 53 reading steady blue. The reported level reads 0x00 before and during
        // the blackout here, so only the BLACKOUT rule stops it. As SmartSoft's bytes: E9d seq 11362 (index 0x04).
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetChannelLevelAsync(5, 0x20, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(5, 0x30, CancellationToken.None);   // matched: queued as it is
        Assert.Equal(1, rig.Device.LastSession!.QueuedWrites);
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E8b, 26987));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E8b, 26991));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E8b, 26999));
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesDropped == 1);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(2, rig.Device.LastSession!.WritesSent);
        Assert.Equal(new[] { WithCounter(E9d, 11362, 0x00, value: 0x00, previous: 0x00), WithCounter(E9d, 11362, 0x01, value: 0x20, previous: 0x00) }, Payloads(rig.Pipe));
        Assert.Contains(rig.Log.Notes, n => n.StartsWith("queued write dropped unsent: BLACKOUT's LED (block 53) reads Full", StringComparison.Ordinal));
    }

    [Fact]
    public async Task After_a_BLACKOUT_tap_of_ours_the_faders_wait_for_the_consoles_report_of_its_LED()
    {
        // The re-check of SC-5 item 10, new major, our own trigger: from the press of a BLACKOUT tap of ours until the console has
        // reported block 53 again, the level objects may already read zero (F125; E8b listed FaderInfo and ChannelLevels before
        // LedStatus in one list, seq 26983), so channel fader 5 waits; once block 53 reads steady blue it waits for the blackout
        // itself. The stand-in toggles nothing by itself: the test reports BLACKOUT on (E8b seq 26987, 26991, 26999). As
        // SmartSoft's bytes: its BLACKOUT press and release (E9 seq 33542, 33547), with this session's counter.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Device.SetBlackoutAsync(true, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        var pressed = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("BLACKOUT was pressed from the PC and the console has not reported its LED (block 53) since", pressed.Message, StringComparison.Ordinal);

        var on = KeysReportedAs(rig.Device, keys => keys.Blackout is { Light: KeyLight.Full, Blinking: false });
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E8b, 26987));
        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E8b, 26991));
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E8b, 26999));
        await on;
        var blackout = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(5, 0x40, CancellationToken.None));
        Assert.Contains("BLACKOUT's LED (block 53) reads Full", blackout.Message, StringComparison.Ordinal);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { E9Payload(33542, 0x00), E9Payload(33547, 0x01) }, Payloads(rig.Pipe));
    }

    /// <summary>The stand-in console answers bump 7's press as the 1248 answered SmartSoft's held bump 7 with SOLO on in E9c: FaderInfo seq 88006 (record 7 `ff 00 00 00`), ChannelLevels seq 88010 (channel 7 at 0xFF).</summary>
    private static void AnswersBump7WithItsHold(AnsweringPipe console) =>
        console.OnPress((byte)ClearedKey.Bump7, () =>
        {
            console.Report((int)HspObjectId.FaderInfo, Body(E9c, 88006));
            console.Report((int)HspObjectId.ChannelLevels, Body(E9c, 88010));
        });

    [Fact]
    public async Task A_bump_of_ours_held_with_SOLO_on_keeps_every_fader_waiting_until_the_faders_are_reported_after_its_release()
    {
        // The re-check of SC-5 item 10, minor C: with SOLO on a bump replaces the output (F6, DOCUMENTED: the other channels are
        // muted), so every fader's reports may carry it (INFERRED, unobserved: FaderInfo of the other faders may drop as it does
        // for BLACKOUT, F125, and the MASTER, F176). The stand-in reports SOLO on as E9c did after SmartSoft's SOLO tap
        // (LedStatus seq 87881, block 52 `FF` x 6) and answers bump 7's press as the console answered SmartSoft's held bump 7
        // there. Channel fader 3, another fader, is refused during the hold, after the release, and after ChannelLevels (seq
        // 88133) and FaderInfo (seq 88141) are in, in E9c's order, until both have been quiet for two poll replies (with SOLO
        // on the other faders' reports must have settled too); then it goes, matched at 0x00. As SmartSoft's bytes: its
        // bump 7 press and release (E9c seq 87997, 88124) and its event of index 0x02 in bank 1-24 (E9d seq 10476).
        var rig = LiveConsole();
        AnswersBump7WithItsHold(rig.Console);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);
        var solo = KeysReportedAs(rig.Device, keys => keys.Solo is { Light: KeyLight.Full, Blinking: false });
        rig.Console.Report((int)HspObjectId.LedStatus, Body(E9c, 87881));
        await solo;

        var inHold = ChannelReported(rig.Device, 7, 0xFF);
        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await inHold;
        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(3, 0x40, CancellationToken.None));
        Assert.Contains("bump 7 is held with SOLO on: the bump replaces the output (F6), so every fader's reports may carry it, not channel fader 3's own level", held.Message, StringComparison.Ordinal);

        await rig.Device.BumpChannelAsync(7, false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);   // a poll after the release
        rig.Console.Resume();
        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);   // its idle reply is in
        var released = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(3, 0x40, CancellationToken.None));
        Assert.Contains("bump 7 was released with SOLO on and the console's reports have not settled since", released.Message, StringComparison.Ordinal);

        rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E9c, 88133));
        rig.Console.Report((int)HspObjectId.FaderInfo, Body(E9c, 88141));
        await rig.Console.ResumeAndPauseAfterServing((int)HspObjectId.FaderInfo).WaitAsync(Timeout);   // both are in, no quiet reply since
        var unsettled = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(3, 0x40, CancellationToken.None));
        Assert.Contains("bump 7 was released with SOLO on and the console's reports have not settled since", unsettled.Message, StringComparison.Ordinal);
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);   // one quiet reply
        await rig.Console.ResumeAndPauseAtNextPoll().WaitAsync(Timeout);   // two
        await rig.Device.SetChannelLevelAsync(3, 0x40, CancellationToken.None);
        rig.Console.Resume();
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 4);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[]
        {
            WithCounter(E9c, 87997, 0x00), WithCounter(E9c, 88124, 0x01),
            WithCounter(E9d, 10476, 0x02, value: 0x00, previous: 0x00), WithCounter(E9d, 10476, 0x03, value: 0x40, previous: 0x00),
        }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task SOLO_coming_on_during_a_bump_of_ours_widens_its_wait_to_every_fader()
    {
        // The re-check of SC-5 item 10, minor C, the panel's SOLO during our hold (the PC sends nothing between a press and its
        // release): bump 7 is pressed with SOLO off (E9b seq 11636), and the console reports SOLO on during the hold (E9c seq
        // 87881, block 52 `FF` x 6, with the hold's FaderInfo seq 88006 and ChannelLevels seq 88010). From then on channel
        // fader 3, another fader, waits too (F6; INFERRED, unobserved). Only the press and the release go (E9c seq 87997, 88124).
        var rig = LiveConsole();
        rig.Console.OnPress((byte)ClearedKey.Bump7, () =>
        {
            rig.Console.Report((int)HspObjectId.FaderInfo, Body(E9c, 88006));
            rig.Console.Report((int)HspObjectId.ChannelLevels, Body(E9c, 88010));
            rig.Console.Report((int)HspObjectId.LedStatus, Body(E9c, 87881));
        });
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        var solo = KeysReportedAs(rig.Device, keys => keys.Solo is { Light: KeyLight.Full, Blinking: false });
        await rig.Device.BumpChannelAsync(7, true, CancellationToken.None);
        await solo;
        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.SetChannelLevelAsync(3, 0x40, CancellationToken.None));
        Assert.Contains("bump 7 is held with SOLO on", held.Message, StringComparison.Ordinal);
        await rig.Device.BumpChannelAsync(7, false, CancellationToken.None);
        await WaitUntil(() => rig.Device.LastSession!.WritesSent == 2);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(new[] { WithCounter(E9c, 87997, 0x00), WithCounter(E9c, 88124, 0x01) }, Payloads(rig.Pipe));
    }

    [Fact]
    public async Task A_bump_needs_the_consoles_FaderInfo_as_a_fader_does()
    {
        // The first review of SC-5 item 10, minor 4: a bump's own checks read its physical fader's FaderInfo record (byte 1 for
        // a channel bump, byte 3 for a memory bump) and must not pass for want of it. The stand-in serves E9b's ConsoleStatus
        // seq 796, ChannelLevels seq 11632 and LedStatus seq 11636 but no FaderInfo: no bump goes, in the bank the console
        // shows or another, and nothing is sent.
        var rig = LiveConsole(withFaderInfo: false);
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        var channelBump = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.BumpChannelAsync(7, true, CancellationToken.None));
        Assert.Contains("the console has not reported its faders yet (FaderInfo", channelBump.Message, StringComparison.Ordinal);
        var memoryBump = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Device.BumpMemoryAsync(1, 1, true, CancellationToken.None));
        Assert.Contains("the console has not reported its faders yet (FaderInfo", memoryBump.Message, StringComparison.Ordinal);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(0, rig.Device.LastSession!.WritesSent);
        Assert.Equal(0, rig.Device.LastSession!.QueuedWrites);
    }

    [Fact]
    public void Every_channel_sits_on_the_bank_and_physical_fader_E9d_moved_it_by()
    {
        // The first review of SC-5 item 10, minor 3: ChannelPosition is an explicit 48-row table, each row checked here against
        // E9d's two SmartSoft passes (F171): a fader event of index i sent in channel mode (bank b from the console's latest
        // ConsoleStatus) names channel c's position (b, i + 1) when the next ChannelLevels the console delivered changed channel
        // c alone, to a level only events of that index and bank carried since the previous ChannelLevels. Every channel 1-48
        // is named, each by one position only, the table's.
        var capture = CaptureFile.Load(TestPaths.Capture("2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl"));
        var decoder = new HspSessionDecoder();
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        var named = Enumerable.Range(1, 48).ToDictionary(c => c, _ => new HashSet<(FaderBank, int)>());
        var recent = new List<(FaderBank Bank, int Index, byte Value)>();
        byte[]? levels = null;
        var payloadDue = false;
        foreach (var frame in capture.BulkFrames)
        {
            var toConsole = frame.Direction == CaptureDirection.Out;
            if (toConsole && payloadDue && frame.Data.Length == 8 && frame.Data[3] == 0x14 && frame.Data[4] == 0x00 && frame.Data[5] <= 0x17
                && assembler.FaderMode is { MemoryMode: false, NextMode: false } mode)
            {
                recent.Add((mode.Bank25To48 ? FaderBank.Channels25To48 : FaderBank.Channels1To24, frame.Data[5], frame.Data[6]));
            }

            if (toConsole)
            {
                payloadDue = frame.Data.Length == 12 && frame.Data[0] == 0x01;   // a Data header: its payload is the next transfer out (F131)
            }

            if (decoder.Feed(toConsole ? HspDirection.ToConsole : HspDirection.FromConsole, frame.Data) is not ObjectReceived received)
            {
                continue;
            }

            if (received.Payload.ObjectId == (int)HspObjectId.ChannelLevels)
            {
                var body = received.Payload.Body.ToArray();
                var changed = levels is null ? Array.Empty<int>() : Enumerable.Range(1, 48).Where(c => body[c] != levels[c]).ToArray();
                if (changed.Length == 1)
                {
                    var carriers = recent.Where(e => e.Value == body[changed[0]]).Select(e => (e.Bank, e.Index + 1)).Distinct().ToArray();
                    if (carriers.Length == 1)
                    {
                        named[changed[0]].Add(carriers[0]);
                    }
                }

                levels = body;
                recent.Clear();
            }

            assembler.Apply(received);
        }

        foreach (var channel in Enumerable.Range(1, 48))
        {
            Assert.Equal(new[] { UsbSmartFadeDevice.ChannelPosition(channel) }, named[channel].ToArray());
        }
    }

    /// <summary>A sink that keeps nothing: the tests above check the bytes through the pipes, not the log.</summary>
    private sealed class DiscardingSink : ITransferSink
    {
        public void Transfer(bool toConsole, ReadOnlySpan<byte> data, DateTimeOffset started, DateTimeOffset completed)
        {
        }

        public void Note(string text)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A pipe that answers each read with the next scripted reply; it records every write.</summary>
    private sealed class ScriptedPipe : IHspPipe
    {
        private readonly Queue<byte[]> _replies;
        private readonly Exception? _readFailure;

        public ScriptedPipe(byte[][] replies, Exception? readFailure = null)
        {
            _replies = new Queue<byte[]>(replies);
            _readFailure = readFailure;
        }

        public List<byte[]> Writes { get; } = new();

        public int Reads { get; private set; }

        public string Description => "scripted pipe";

        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Dispose() => Close();

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (Writes)
            {
                Writes.Add(data.ToArray());
            }
        }

        public void ReadExactly(Span<byte> buffer)
        {
            if (_replies.Count == 0)
            {
                throw _readFailure ?? new HspPipeException("no more scripted replies");
            }

            var reply = _replies.Dequeue();
            if (reply.Length != buffer.Length)
            {
                throw new HspPipeException($"scripted reply has {reply.Length} bytes, {buffer.Length} requested");
            }

            reply.CopyTo(buffer);
            Reads++;
        }
    }

    /// <summary>A pipe whose reads wait until the test releases them, then fail: the session stays connected meanwhile.</summary>
    private sealed class BlockingPipe : IHspPipe
    {
        private readonly ManualResetEventSlim _released = new(false);

        public List<byte[]> Writes { get; } = new();

        public TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Description => "blocking pipe";

        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Dispose()
        {
            Close();
            _released.Dispose();
        }

        public void Release() => _released.Set();

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (Writes)
            {
                Writes.Add(data.ToArray());
            }

            FirstWrite.TrySetResult();
        }

        public void ReadExactly(Span<byte> buffer)
        {
            _ = _released.Wait(Timeout);
            throw new HspPipeException("released by the test");
        }
    }

    /// <summary>
    /// A stand-in console for live control: the first poll is answered with a list of the objects it holds, a fetch with
    /// that object on the next poll, a poll after <see cref="Report"/> with a list of what was reported, every other poll
    /// idle; Data headers and payloads are taken without an answer (F131). The framing is hand-made (F53, F54, F57); the
    /// bodies come from a committed capture. A test can hold the loop inside a poll (<see cref="PauseAtNextPoll"/>), fail
    /// a Data transfer, or answer the first poll after a write in a way no capture shows (<see cref="FaultAfterWrite"/>).
    /// </summary>
    private sealed class AnsweringPipe : IHspPipe
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, byte[]> _bodies = new();
        private readonly List<int> _dirty = new();
        private readonly Dictionary<int, int> _served = new();
        private readonly Dictionary<byte, Action> _onPress = new();
        private readonly Dictionary<byte, Action> _onRelease = new();
        private readonly List<(int AtPoll, int Id, byte[] Body)> _scheduled = new();
        private int _polls;
        private readonly Queue<byte[]> _replies = new();
        private ManualResetEventSlim? _waiting;   // what the paused poll waits on
        private ManualResetEventSlim? _next;      // what the next pause will wait on, once one is asked for
        private int? _pauseAfterServing;
        private bool _pauseRequested;
        private bool _dataSeen;
        private bool _relist;
        private bool _payloadDue;
        private bool _faultArmed;
        private int _dataTransfers;
        private int? _fetched;
        private byte _sequence;

        public AnsweringPipe(params (int Id, byte[] Body)[] objects)
        {
            foreach (var (id, body) in objects)
            {
                _bodies[id] = body;
                _dirty.Add(id);   // the first poll lists them in this order
            }
        }

        /// <summary>After each Data payload, the next poll lists FaderInfo again, as the console lists the objects an event changed (F134).</summary>
        public bool RelistFaderInfoAfterWrites { get; init; }

        /// <summary>The Data transfer (1-based, headers and payloads counted together) whose write fails, or 0.</summary>
        public int FailDataTransfer { get; init; }

        /// <summary>How the first poll after the first Data payload is answered: as usual (None), or in a way no capture shows.</summary>
        public ConsoleFault FaultAfterWrite { get; init; }

        public TaskCompletionSource DataWritten { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PollAfterData { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Paused { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Description => "answering console";

        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Dispose()
        {
            Close();
            Resume();
        }

        /// <summary>The console's state changed: the next poll that follows no fetch lists the object, and its fetch delivers this body.</summary>
        public void Report(int id, byte[] body)
        {
            lock (_lock)
            {
                _bodies[id] = body;
                if (!_dirty.Contains(id))
                {
                    _dirty.Add(id);
                }
            }
        }

        /// <summary>
        /// When a key press with <paramref name="code"/> is written, the console reacts, on the loop's thread, before its next
        /// poll is answered: <paramref name="reaction"/> reports what changed (bodies from a committed capture, as the 1248
        /// answered SmartSoft's press there) or pauses at the next poll.
        /// </summary>
        public void OnPress(byte code, Action reaction)
        {
            lock (_lock)
            {
                _onPress[code] = reaction;
            }
        }

        /// <summary>As <see cref="OnPress"/>, for the release of the key with <paramref name="code"/> (kind 1, state 0; F133).</summary>
        public void OnRelease(byte code, Action reaction)
        {
            lock (_lock)
            {
                _onRelease[code] = reaction;
            }
        }

        /// <summary>
        /// The console's state changes <paramref name="polls"/> polls from now: that poll lists the objects (a late answer, as a
        /// console slower than any capture would give).
        /// </summary>
        public void ReportAfterPolls(int polls, params (int Id, byte[] Body)[] objects)
        {
            lock (_lock)
            {
                foreach (var (id, body) in objects)
                {
                    _scheduled.Add((_polls + polls, id, body));
                }
            }
        }

        /// <summary>How often the object was delivered.</summary>
        public int Served(int id)
        {
            lock (_lock)
            {
                return _served.TryGetValue(id, out var count) ? count : 0;
            }
        }

        /// <summary>The next poll waits inside the pipe (its reply not decided yet) until <see cref="Resume"/>; <see cref="Paused"/> says it is there.</summary>
        public Task PauseAtNextPoll()
        {
            lock (_lock)
            {
                return AskPause();
            }
        }

        /// <summary>The paused poll goes on and the one after it waits: exactly one poll exchange between the two pauses.</summary>
        public Task ResumeAndPauseAtNextPoll()
        {
            lock (_lock)
            {
                var paused = _waiting;
                var next = AskPause();
                paused?.Set();
                return next;
            }
        }

        /// <summary>The paused poll goes on, and the poll after the one that delivers <paramref name="objectId"/> waits: the test acts right after that delivery.</summary>
        public Task ResumeAndPauseAfterServing(int objectId)
        {
            lock (_lock)
            {
                var paused = _waiting;
                var next = AskPause();
                _pauseRequested = false;
                _pauseAfterServing = objectId;
                paused?.Set();
                return next;
            }
        }

        /// <summary>The poll after the one that delivers <paramref name="objectId"/> waits (asked for from a reaction, on the loop's thread, or while nothing is paused).</summary>
        public Task PauseAfterServing(int objectId)
        {
            lock (_lock)
            {
                var next = AskPause();
                _pauseRequested = false;
                _pauseAfterServing = objectId;
                return next;
            }
        }

        /// <summary>Everything goes on: the paused poll, and a pause asked for and not reached yet.</summary>
        public void Resume()
        {
            lock (_lock)
            {
                _waiting?.Set();
                _next?.Set();
            }
        }

        private Task AskPause()
        {
            Paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _next = new ManualResetEventSlim(false);
            _pauseRequested = true;
            return Paused.Task;
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            var isDataHeader = data.Length == 12 && data[0] == 1;
            if (isDataHeader || _payloadDue)
            {
                if (++_dataTransfers == FailDataTransfer)
                {
                    throw new HspPipeException($"Data transfer {_dataTransfers} failed (stand-in console)");
                }
            }

            if (_payloadDue)
            {
                _payloadDue = false;
                _dataSeen = true;
                _relist = RelistFaderInfoAfterWrites;
                _faultArmed |= FaultAfterWrite != ConsoleFault.None;
                Action? reaction = null;
                lock (_lock)
                {
                    // a key press (kind 1, state 1; F133)
                    if (data.Length == 7 && data[4] == 0x01 && data[6] == 0x01)
                    {
                        _onPress.TryGetValue(data[5], out reaction);
                    }
                    else if (data.Length == 7 && data[4] == 0x01 && data[6] == 0x00)
                    {
                        _onRelease.TryGetValue(data[5], out reaction);
                    }
                }

                reaction?.Invoke();
                DataWritten.TrySetResult();   // a Data payload: nothing answers it
                return;
            }

            if (isDataHeader)
            {
                _payloadDue = true;   // its payload follows
                return;
            }

            if (data[0] == 2)
            {
                _fetched = data[4] | (data[5] << 8);
                return;
            }

            if (_dataSeen)
            {
                PollAfterData.TrySetResult();
            }

            ManualResetEventSlim? resume = null;
            lock (_lock)
            {
                _polls++;
                foreach (var due in _scheduled.Where(s => s.AtPoll <= _polls).ToArray())
                {
                    _scheduled.Remove(due);
                    Report(due.Id, due.Body);
                }

                if (_pauseRequested)
                {
                    _pauseRequested = false;
                    resume = _next ?? new ManualResetEventSlim(true);
                    _next = null;
                    _waiting = resume;
                    Paused.TrySetResult();
                }
            }

            if (resume is not null)
            {
                _ = resume.Wait(Timeout);
                lock (_lock)
                {
                    if (ReferenceEquals(_waiting, resume))
                    {
                        _waiting = null;
                    }
                }
            }

            AnswerPoll();
        }

        public void ReadExactly(Span<byte> buffer)
        {
            var reply = _replies.Dequeue();
            if (reply.Length != buffer.Length)
            {
                throw new HspPipeException($"answer has {reply.Length} bytes, {buffer.Length} requested");
            }

            reply.CopyTo(buffer);
        }

        private void AnswerPoll()
        {
            if (_fetched is { } id)
            {
                byte[] body;
                lock (_lock)
                {
                    body = _bodies[id];
                    _served[id] = (_served.TryGetValue(id, out var count) ? count : 0) + 1;
                    if (_pauseAfterServing == id)
                    {
                        _pauseAfterServing = null;
                        _pauseRequested = true;   // the next poll waits, its pause asked for already
                    }
                }

                var payload = Payload(_sequence++, id, body);
                _replies.Enqueue(Header(3, (ushort)payload.Length, (ushort)id));
                _replies.Enqueue(payload);
                _fetched = null;
                return;
            }

            if (_faultArmed)
            {
                _faultArmed = false;
                if (FaultAfterWrite == ConsoleFault.PayloadByte1)
                {
                    // a list read whole whose byte 1 is 0x01 (0x00 in every observed payload, F55): the exchange is closed
                    var odd = ListPayload(_sequence++, new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
                    odd[1] = 0x01;
                    _replies.Enqueue(Header(4, (ushort)odd.Length));
                    _replies.Enqueue(odd);
                }
                else
                {
                    // a reply announcing more than any capture holds: the session stops with the payload still due
                    _replies.Enqueue(Header(4, HspReadSession.MaxObservedPayloadLength + 1));
                }

                return;
            }

            DirtyObjectRecord[] listed;
            lock (_lock)
            {
                if (_relist && !_dirty.Contains((int)HspObjectId.FaderInfo))
                {
                    _dirty.Add((int)HspObjectId.FaderInfo);
                }

                _relist = false;
                listed = _dirty.Select(o => new DirtyObjectRecord(o, 0, 0, 0)).ToArray();
                _dirty.Clear();
            }

            if (listed.Length > 0)
            {
                var list = ListPayload(_sequence++, listed);
                _replies.Enqueue(Header(4, (ushort)list.Length));
                _replies.Enqueue(list);
                return;
            }

            Thread.Sleep(1);   // idle: let the test's thread run, so the loop does not race far ahead on the virtual clock
            _replies.Enqueue(Idle());
        }
    }

    private enum ConsoleFault
    {
        None,

        /// <summary>A payload read whole whose byte 1 is 0x01: the session faults with no exchange open.</summary>
        PayloadByte1,

        /// <summary>A reply header announcing 793 bytes: the session faults with the payload still due.</summary>
        HeaderTooLong,
    }

    /// <summary>A sink that keeps the session's notes (the transfers are checked through the pipes), also in a shared order when given one.</summary>
    private sealed class RecordingSink : ITransferSink
    {
        private readonly List<string> _notes = new();

        public List<string>? Order { get; init; }

        public IReadOnlyList<string> Notes
        {
            get
            {
                lock (_notes)
                {
                    return _notes.ToArray();
                }
            }
        }

        public void Transfer(bool toConsole, ReadOnlySpan<byte> data, DateTimeOffset started, DateTimeOffset completed)
        {
        }

        public void Note(string text)
        {
            lock (_notes)
            {
                _notes.Add(text);
            }

            if (Order is { } order)
            {
                lock (order)
                {
                    order.Add("note " + text);
                }
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A clock that moves only when told to, in UTC, so that "today" never depends on the machine's time zone; the loop's thread and a test's may both move it.</summary>
    private sealed class VirtualClock : TimeProvider
    {
        private long _ticks;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref _ticks));

        public TimeSpan Now => TimeSpan.FromTicks(Interlocked.Read(ref _ticks));

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    /// <summary>Wraps a pipe, stamping every transfer with the virtual time and charging 1 ms per transfer.</summary>
    private sealed class TimedPipe : IHspPipe
    {
        private readonly IHspPipe _inner;
        private readonly VirtualClock _clock;

        public TimedPipe(IHspPipe inner, VirtualClock clock)
        {
            _inner = inner;
            _clock = clock;
        }

        public List<(TimeSpan At, bool ToConsole, byte[] Data)> Transfers { get; } = new();

        /// <summary>When given, every transfer is entered here too, as "out HEX" or "in HEX", in the order it happened.</summary>
        public List<string>? Order { get; init; }

        public string Description => _inner.Description;

        public bool IsOpen => _inner.IsOpen;

        public void Open() => _inner.Open();

        public void Close() => _inner.Close();

        public void Dispose() => _inner.Dispose();

        public void Write(ReadOnlySpan<byte> data)
        {
            _inner.Write(data);
            lock (Transfers)
            {
                Transfers.Add((_clock.Now, true, data.ToArray()));
            }

            Enter("out ", data);
            _clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        public void ReadExactly(Span<byte> buffer)
        {
            _inner.ReadExactly(buffer);
            _clock.Advance(TimeSpan.FromMilliseconds(1));
            lock (Transfers)
            {
                Transfers.Add((_clock.Now, false, buffer.ToArray()));
            }

            Enter("in ", buffer);
        }

        private void Enter(string direction, ReadOnlySpan<byte> data)
        {
            if (Order is { } order)
            {
                var entry = direction + Convert.ToHexString(data);
                lock (order)
                {
                    order.Add(entry);
                }
            }
        }
    }
}
