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
    /// (header seq 13143, payload 13144): FaderInfo seq 11628 (MASTER and BUMPS at full), ConsoleStatus seq 796 (channel
    /// mode, bank 1-24, page 1) and LedStatus seq 11636 (REC MEM, EDIT MEM, COPY, REC SEQ, SOLO and BLACKOUT off, STACK
    /// dim), served in that order, so the key LEDs are reported last. <paramref name="delay"/> replaces the virtual
    /// clock's plain delay; the transfer log's notes are kept.
    /// </summary>
    private static LiveRig LiveConsole(
        bool relistAfterWrites = false,
        Func<VirtualClock, Func<TimeSpan, CancellationToken, Task>>? delay = null,
        IncompleteWriteMarker? marker = null,
        string? liveControlDate = "today",
        int failDataTransfer = 0,
        ConsoleFault faultAfterWrite = ConsoleFault.None)
    {
        var console = new AnsweringPipe(
            ((int)HspObjectId.FaderInfo, E9bBody(11628)),
            ((int)HspObjectId.ConsoleStatus, E9bBody(796)),
            ((int)HspObjectId.LedStatus, E9bBody(11636)))
        {
            RelistFaderInfoAfterWrites = relistAfterWrites,
            FailDataTransfer = failDataTransfer,
            FaultAfterWrite = faultAfterWrite,
        };
        var clock = new VirtualClock();
        var options = new UsbDeviceOptions { LiveControlDate = liveControlDate == "today" ? Today(clock) : liveControlDate };
        var pipe = new TimedPipe(console, clock);
        var log = new RecordingSink();
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

        Assert.Equal(5, writes);
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
        // listed FaderInfo (where the MASTER is reported) that is not fetched yet holds it. The console lists FaderInfo again
        // (the same E9b seq 11628 body: MASTER and BUMPS at full); the event goes only after that object is in.
        var rig = LiveConsole();
        await rig.Device.ConnectAsync(CancellationToken.None);
        await rig.KeysReported.WaitAsync(Timeout);

        await rig.Console.PauseAtNextPoll().WaitAsync(Timeout);
        await rig.Device.SetChannelLevelAsync(1, 0x40, CancellationToken.None);
        rig.Console.Report((int)HspObjectId.FaderInfo, E9bBody(11628));
        rig.Console.Resume();
        await rig.Console.DataWritten.Task.WaitAsync(Timeout);
        await rig.Console.PollAfterData.Task.WaitAsync(Timeout);
        await rig.Device.DisconnectAsync(CancellationToken.None);

        Assert.Equal(1, rig.Device.LastSession!.WritesSent);
        Assert.Equal(0, rig.Device.LastSession.WritesDropped);
        Assert.Contains("write held back: the console listed FaderInfo (the MASTER) and it is not fetched yet", rig.Log.Notes);
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
        private readonly Queue<byte[]> _replies = new();
        private readonly ManualResetEventSlim _resume = new(true);
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
            _resume.Set();
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
                Paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _resume.Reset();
                _pauseRequested = true;
                return Paused.Task;
            }
        }

        public void Resume() => _resume.Set();

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

            bool pause;
            lock (_lock)
            {
                pause = _pauseRequested;
                _pauseRequested = false;
                if (pause)
                {
                    Paused.TrySetResult();
                }
            }

            if (pause)
            {
                _ = _resume.Wait(Timeout);
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

    /// <summary>A sink that keeps the session's notes (the transfers are checked through the pipes).</summary>
    private sealed class RecordingSink : ITransferSink
    {
        private readonly List<string> _notes = new();

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
