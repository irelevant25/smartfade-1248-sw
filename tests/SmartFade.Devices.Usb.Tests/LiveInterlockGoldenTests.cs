using System.Runtime.InteropServices;
using SmartFade.Communication;
using SmartFade.Communication.Capture;
using SmartFade.Domain;
using SmartFade.Protocol;

namespace SmartFade.Devices.Usb.Tests;

/// <summary>
/// SC-2 condition 5 against SmartSoft's own writes (the first review of SC-2 item 10, major 6). E9
/// (docs/protocol/captures/2026-10-30-e9-smartsoft-writes.jsonl) and E9b
/// (docs/protocol/captures/2026-10-31-e9b-smartsoft-writes-repeat.jsonl) are replayed through a session and the
/// device's <see cref="ConsoleStateAssembler"/>, and at each of SmartSoft's 605 whitelisted ConsoleEvents the device's
/// interlock (<see cref="UsbSmartFadeDevice.Interlock"/>, what its gate applies once the session-end and expiry checks
/// pass) is asked what it would do with a write of ours at that point. SmartSoft made 16 of them inside a record
/// function (8 press / release pairs of bumps 1, 7 and 9 under EDIT MEM, REC MEM or COPY): exactly those read armed
/// and are dropped. Of the other 589, 436 would go and 153 would be held, every one of them because the console had
/// listed a LedStatus that was not fetched yet (SmartSoft wrote before its fetches; the gate does not, F57 / F116).
/// The age of the console's report is not judged here: the captures carry SmartSoft's timing, not ours (that rule has
/// its own test on the virtual clock). Counted beforehand with a port of these rules over the same ranges.
/// </summary>
public class LiveInterlockGoldenTests
{
    private const string E9File = "2026-10-30-e9-smartsoft-writes.jsonl";
    private const string E9bFile = "2026-10-31-e9b-smartsoft-writes-repeat.jsonl";

    private static readonly Lazy<CaptureFile> E9 = new(() => CaptureFile.Load(TestPaths.Capture(E9File)));
    private static readonly Lazy<CaptureFile> E9b = new(() => CaptureFile.Load(TestPaths.Capture(E9bFile)));

    /// <summary>
    /// One range per session of ours, each starting at a poll with zero stale words after an idle reply with nothing
    /// listed: E9 up to the poll at seq 68339 (as the golden write replay), then 68367 … 77851 and 77881 … 91068, each
    /// ending at the poll whose reply lists System with c ≠ 0 after an EDIT MEM selection (seq 68341, 77854, 91071; F137:
    /// a session faults there by design); E9's later ranges hold no whitelisted write. E9b per SmartSoft session (S2,
    /// 64681 … 70067, holds none).
    /// </summary>
    [Theory]
    [InlineData(E9File, int.MinValue, 68339, 228, 56, new[] { 67092, 67098, 68334, 68338 }, new[] { "EDIT MEM", "EDIT MEM", "EDIT MEM", "EDIT MEM" })]
    [InlineData(E9File, 68367, 77852, 1, 1, new[] { 73996, 74000, 77843, 77851 }, new[] { "REC MEM", "REC MEM", "EDIT MEM", "EDIT MEM" })]
    [InlineData(E9File, 77881, 91069, 0, 0, new[] { 83185, 83191, 91062, 91068 }, new[] { "COPY", "COPY", "EDIT MEM", "EDIT MEM" })]
    [InlineData(E9bFile, int.MinValue, 64681, 196, 82, new int[0], new string[0])]
    [InlineData(E9bFile, 70068, int.MaxValue, 11, 14, new[] { 79053, 79059, 84063, 84069 }, new[] { "REC MEM", "REC MEM", "REC MEM", "REC MEM" })]
    public void The_interlock_drops_exactly_SmartSofts_writes_inside_a_record_function(
        string file, int firstSeq, int endSeq, int sends, int holds, int[] drops, string[] armedKeys)
    {
        var capture = file == E9File ? E9 : E9b;

        var decisions = ReplayInterlock(capture.Value, firstSeq, endSeq);

        Assert.Equal(Whitelisted(capture.Value, firstSeq, endSeq), decisions.Select(d => d.PayloadSeq).ToArray());
        var dropped = decisions.Where(d => d.Decision.Verdict == HspWriteVerdict.Drop).ToArray();
        Assert.Equal(drops, dropped.Select(d => d.PayloadSeq).ToArray());
        for (var i = 0; i < dropped.Length; i++)
        {
            // the record function named is the one lit (blocks 66 REC MEM, 67 EDIT MEM, 63 COPY; H72), SC-2 condition 5
            Assert.StartsWith($"{armedKeys[i]} is lit or blinking", dropped[i].Decision.Reason, StringComparison.Ordinal);
        }

        Assert.Equal(sends, decisions.Count(d => d.Decision.Verdict == HspWriteVerdict.Send));
        var held = decisions.Where(d => d.Decision.Verdict == HspWriteVerdict.Hold).ToArray();
        Assert.Equal(holds, held.Length);
        Assert.All(held, d => Assert.Equal("the console listed new key LEDs that are not fetched yet (SC-2 condition 5)", d.Decision.Reason));
    }

    [Fact]
    public void The_ranges_hold_every_whitelisted_write_of_both_captures()
    {
        // 298 in E9 and 307 in E9b (16 inside a record function, 589 not); none falls between the ranges above
        var e9 = Whitelisted(E9.Value, int.MinValue, int.MaxValue);
        var e9b = Whitelisted(E9b.Value, int.MinValue, int.MaxValue);
        var e9Ranges = new[] { (int.MinValue, 68339), (68367, 77852), (77881, 91069) };
        var e9bRanges = new[] { (int.MinValue, 64681), (70068, int.MaxValue) };

        Assert.Equal(298, e9.Length);
        Assert.Equal(307, e9b.Length);
        Assert.All(e9, seq => Assert.Contains(e9Ranges, r => seq >= r.Item1 && seq < r.Item2));
        Assert.All(e9b, seq => Assert.Contains(e9bRanges, r => seq >= r.Item1 && seq < r.Item2));
    }

    /// <summary>
    /// A read-only session replays the range with SmartSoft's writes skipped; every object it delivers goes to the
    /// assembler, and at each skipped whitelisted ConsoleEvent (the pipe reaches it right after the exchange it followed,
    /// before the session's next transfer) the interlock is asked, with the session's list state and the assembler's key
    /// LEDs as they stand there.
    /// </summary>
    private static List<(int PayloadSeq, HspWriteDecision Decision)> ReplayInterlock(CaptureFile capture, int firstSeq, int endSeq)
    {
        var seqOf = new Dictionary<byte[], int>(ReferenceEqualityComparer.Instance);
        foreach (var frame in capture.BulkFrames)
        {
            seqOf[frame.Data] = frame.Seq;
        }

        var session = new HspReadSession();
        var assembler = new ConsoleStateAssembler(ConsoleCapabilities.SmartFade1248);
        var decisions = new List<(int PayloadSeq, HspWriteDecision Decision)>();
        var pipe = new ReplayHspPipe(capture, payload =>
        {
            if (HspOutbound.IsClearedConsoleEvent(payload.Span))
            {
                Assert.True(MemoryMarshal.TryGetArray(payload, out var segment) && segment.Array is not null);
                decisions.Add((seqOf[segment.Array!], UsbSmartFadeDevice.Interlock(session, assembler.Keys, reportFresh: true)));
            }

            return false;   // SmartSoft's write is skipped: a session of ours never sent it
        }, firstSeq, endSeq);

        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        pipe.Open();
        while (!pipe.AtEnd)
        {
            var next = session.Next();
            pipe.Write(next.Bytes);
            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                foreach (var evt in session.Receive(span.ToArray()))
                {
                    if (evt is ObjectReceived received)
                    {
                        Apply(assembler, received);
                    }
                }

                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }
        }

        return decisions;
    }

    /// <summary>As the device applies an object: one the assembler cannot decode is skipped, but none of the objects the interlock reads may be such.</summary>
    private static void Apply(ConsoleStateAssembler assembler, ObjectReceived received)
    {
        try
        {
            assembler.Apply(received);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            Assert.DoesNotContain(received.Payload.ObjectId, new[] { (int)HspObjectId.LedStatus, (int)HspObjectId.ConsoleStatus, (int)HspObjectId.FaderInfo });
        }
    }

    /// <summary>The payload seq of every whitelisted ConsoleEvent SmartSoft wrote in the range (a Data header, then its payload).</summary>
    private static int[] Whitelisted(CaptureFile capture, int firstSeq, int endSeq)
    {
        var frames = capture.BulkFrames.Where(f => f.Seq >= firstSeq && f.Seq < endSeq).ToArray();
        var seqs = new List<int>();
        for (var i = 0; i + 1 < frames.Length; i++)
        {
            var header = frames[i].Data;
            var payload = frames[i + 1];
            if (frames[i].Direction == CaptureDirection.Out && header.Length == 12 && header[0] == 1 && header[1] == 0
                && payload.Direction == CaptureDirection.Out && HspOutbound.IsClearedConsoleEvent(payload.Data))
            {
                seqs.Add(payload.Seq);
            }
        }

        return seqs.ToArray();
    }
}
