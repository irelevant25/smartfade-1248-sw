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
public class UsbSmartFadeDeviceTests
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
        VirtualClock? clock = null)
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
            (wait, _) =>
            {
                clock.Advance(wait);
                return Task.CompletedTask;
            });
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
        | DeviceCommands.SetBlackout | DeviceCommands.SetSolo | DeviceCommands.BumpChannel | DeviceCommands.BumpMemory;

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
        Assert.DoesNotContain(DeviceControls.ChannelFader(2), live.WritableControls!);
        Assert.DoesNotContain(DeviceControls.ChannelBump(9), live.WritableControls!);
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

        // outside the whitelist (SC-2 condition 1, item 6)
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetChannelLevelAsync(2, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpChannelAsync(10, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.RecordMemoryAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.SetIndependentAsync(1, true, CancellationToken.None));

        // whitelisted codes never sent from a PC in that state with the record keys off (E9, E9b): bump 9 anywhere,
        // channel bump 1, memory bump 7
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpChannelAsync(9, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpChannelAsync(1, true, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => device.BumpMemoryAsync(1, 7, true, CancellationToken.None));

        // inside it, but the console has reported nothing yet (SC-2 conditions 5, 6, 8)
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetMasterAsync(1, CancellationToken.None));
        Assert.Contains("key LEDs", refused.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetChannelLevelAsync(1, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.SetSoloAsync(true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.BumpChannelAsync(7, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressGoAsync(CancellationToken.None));

        var ended = WaitForStatus(device, ConnectionStatus.Error);
        pipe.Release();
        await ended;

        Assert.Equal(new byte[12], Assert.Single(pipe.Writes));   // the one poll: no Data header ever left
        Assert.Equal(0, device.LastSession!.WritesSent);
    }

    /// <summary>
    /// A live device on a stand-in console that reports the state SmartSoft saw before its first MASTER event of E9b
    /// (header seq 13143, payload 13144): FaderInfo seq 11628 (MASTER and BUMPS at full), ConsoleStatus seq 796 (channel
    /// mode, bank 1-24, page 1) and LedStatus seq 11636 (REC MEM, EDIT MEM, COPY, REC SEQ, SOLO and BLACKOUT off, STACK
    /// dim), served in that order, so the key LEDs are reported last.
    /// </summary>
    private static (UsbSmartFadeDevice Device, AnsweringPipe Console, TimedPipe Pipe, UsbDeviceOptions Options, Task KeysReported) LiveConsole(bool relistAfterWrites = false)
    {
        var e9b = CaptureFile.Load(TestPaths.Capture("2026-10-31-e9b-smartsoft-writes-repeat.jsonl"));
        byte[] Body(int seq) => Frame(e9b, seq, CaptureDirection.In)[5..];
        var console = new AnsweringPipe(
            ((int)HspObjectId.FaderInfo, Body(11628)),
            ((int)HspObjectId.ConsoleStatus, Body(796)),
            ((int)HspObjectId.LedStatus, Body(11636)))
        {
            RelistFaderInfoAfterWrites = relistAfterWrites,
        };
        var clock = new VirtualClock();
        var options = new UsbDeviceOptions { LiveControlDate = Today(clock) };
        var pipe = new TimedPipe(console, clock);
        var device = Create(() => pipe, options, clock: clock);
        var keysReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.StateChanged += (_, change) =>
        {
            if (change.Keys is not null)
            {
                keysReported.TrySetResult();
            }
        };

        return (device, console, pipe, options, keysReported.Task);
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
        var (device, console, pipe, _, keysReported) = LiveConsole();
        await device.ConnectAsync(CancellationToken.None);
        await keysReported.WaitAsync(Timeout);

        // channel mode, bank 1-24: memory bump 1 needs memory mode, FADERS 1-24 is sent only from memory mode or bank 25-48
        var memoryBump = await Assert.ThrowsAsync<InvalidOperationException>(() => device.BumpMemoryAsync(1, 1, true, CancellationToken.None));
        Assert.Contains("channel mode", memoryBump.Message, StringComparison.Ordinal);
        var faders = await Assert.ThrowsAsync<InvalidOperationException>(() => device.PressFaders1To24Async(CancellationToken.None));
        Assert.Contains("bank 1-24", faders.Message, StringComparison.Ordinal);

        // SOLO and BLACKOUT already read off, and a bump's release alone: nothing to send
        await device.SetSoloAsync(false, CancellationToken.None);
        await device.SetBlackoutAsync(false, CancellationToken.None);
        await device.BumpChannelAsync(7, pressed: false, CancellationToken.None);

        // channel fader 1 with the MASTER at full: sent, as SmartSoft's first channel fader 1 event of E9b (seq 8038)
        // but for this session's counter, the level and the previous byte
        await device.SetChannelLevelAsync(1, 0x10, CancellationToken.None);
        await console.DataWritten.Task.WaitAsync(Timeout);
        await console.PollAfterData.Task.WaitAsync(Timeout);
        await device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(1, device.LastSession!.WritesSent);
        var transfers = Recorded(pipe);
        var header = transfers.FindIndex(t => t.ToConsole && t.Data.Length == 12 && t.Data[0] == 1);
        var e9b = CaptureFile.Load(TestPaths.Capture("2026-10-31-e9b-smartsoft-writes-repeat.jsonl"));
        var expected = Frame(e9b, 8038, CaptureDirection.Out).ToArray();
        expected[0] = 0x00;
        expected[6] = 0x10;
        expected[7] = 0x00;
        Assert.Equal(expected, transfers[header + 1].Data);
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
        await device.SetChannelLevelAsync(1, 0x20, CancellationToken.None); // held while FaderInfo is listed and not fetched
        await WaitUntil(() => device.LastSession!.WritesSent == 5);
        await device.DisconnectAsync(CancellationToken.None);               // returns after the poll exchange that follows the last write

        Assert.Equal(0, device.LastSession!.WritesDropped);
        TimeSpan? lastPoll = null, lastFetch = null, lastPayloadIn = null, lastWriteHeader = null, lastWritePayload = null;
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
                }

                continue;
            }

            if (outboundPayloadDue)
            {
                outboundPayloadDue = false;
                lastWritePayload = at;
                writes++;
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

        Assert.Equal(5, writes);
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
    /// that object on the next poll, every other poll idle; Data headers and payloads are taken without an answer (F131).
    /// The framing is hand-made (F53, F54, F57); the bodies come from a committed capture.
    /// </summary>
    private sealed class AnsweringPipe : IHspPipe
    {
        private readonly (int Id, byte[] Body)[] _objects;
        private readonly Queue<byte[]> _replies = new();
        private bool _listed;
        private bool _dataSeen;
        private bool _relist;
        private int? _fetched;
        private byte _sequence;

        public AnsweringPipe(params (int Id, byte[] Body)[] objects)
        {
            _objects = objects;
        }

        /// <summary>After each Data payload, the next poll lists FaderInfo again, as the console lists the objects an event changed (F134).</summary>
        public bool RelistFaderInfoAfterWrites { get; init; }

        public TaskCompletionSource DataWritten { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PollAfterData { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Description => "answering console";

        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Dispose() => Close();

        public void Write(ReadOnlySpan<byte> data)
        {
            if (data.Length != 12)
            {
                _dataSeen = true;
                _relist = RelistFaderInfoAfterWrites;
                DataWritten.TrySetResult();   // a Data payload: nothing answers it
                return;
            }

            if (data[0] == 0 && _dataSeen)
            {
                PollAfterData.TrySetResult();
            }

            switch (data[0])
            {
                case 0 when _fetched is { } id:
                    var body = _objects.First(o => o.Id == id).Body;
                    var payload = Payload(_sequence++, id, body);
                    _replies.Enqueue(Header(3, (ushort)payload.Length, (ushort)id));
                    _replies.Enqueue(payload);
                    _fetched = null;
                    break;
                case 0 when !_listed:
                    _listed = true;
                    var list = ListPayload(_sequence++, _objects.Select(o => new DirtyObjectRecord(o.Id, 0, 0, 0)).ToArray());
                    _replies.Enqueue(Header(4, (ushort)list.Length));
                    _replies.Enqueue(list);
                    break;
                case 0 when _relist:
                    _relist = false;
                    var again = ListPayload(_sequence++, new DirtyObjectRecord((int)HspObjectId.FaderInfo, 0, 0, 0));
                    _replies.Enqueue(Header(4, (ushort)again.Length));
                    _replies.Enqueue(again);
                    break;
                case 0:
                    Thread.Sleep(1);   // idle: let the test's thread run, so the loop does not race far ahead on the virtual clock
                    _replies.Enqueue(Idle());
                    break;
                case 2:
                    _fetched = data[4] | (data[5] << 8);
                    break;
                default:
                    break;   // a Data header: its payload follows
            }
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
    }

    /// <summary>A clock that moves only when told to, in UTC, so that "today" never depends on the machine's time zone.</summary>
    private sealed class VirtualClock : TimeProvider
    {
        private long _ticks;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

        public TimeSpan Now => TimeSpan.FromTicks(_ticks);

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
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
        }
    }
}
