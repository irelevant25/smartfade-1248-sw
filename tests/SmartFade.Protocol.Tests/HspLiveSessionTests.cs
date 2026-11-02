using SmartFade.Communication;
using SmartFade.Communication.Capture;

namespace SmartFade.Protocol.Tests;

/// <summary>
/// The write path of safety checks SC-2 (classes A and B1) and SC-5 (classes A2–A4, B3–B5) against SmartSoft's own writes
/// in E9 (docs/protocol/captures/2026-10-30-e9-smartsoft-writes.jsonl), E9b (2026-10-31-e9b-smartsoft-writes-repeat.jsonl),
/// E9c (2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl), E9d (2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl)
/// and E9e (2026-11-02-e9e-smartsoft-channel-bumps-spot-check.jsonl). The encoder reproduces SmartSoft's bytes for every
/// whitelisted index and code (every physical fader in each of the three banks, every bump, the new keys); the session,
/// replayed over the captures and handed SmartSoft's event at each of SmartSoft's whitelisted writes, reproduces header,
/// stale words, counter, previous byte (across bank switches, SC-5 condition 4) and placement, including SmartSoft's
/// switch sequences (FADERS MEMS then index 0x00 …, FADERS 25-48 then index 0x00 …; SC-2 condition 2, SC-5 condition 3).
/// The rules a replay cannot show (the whitelist typing, the read-only session, condition 8, the tap, the hold, the gate,
/// the stop, the release after a fault) are tested on hand-made replies below.
/// </summary>
public class HspLiveSessionTests
{
    private const string E9File = "2026-10-30-e9-smartsoft-writes.jsonl";
    private const string E9bFile = "2026-10-31-e9b-smartsoft-writes-repeat.jsonl";
    private const string E9cFile = "2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl";
    private const string E9dFile = "2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl";
    private const string E9eFile = "2026-11-02-e9e-smartsoft-channel-bumps-spot-check.jsonl";

    private static readonly Lazy<CaptureFile> E9 = new(() => CaptureFile.Load(TestPaths.Capture(E9File)));
    private static readonly Lazy<CaptureFile> E9b = new(() => CaptureFile.Load(TestPaths.Capture(E9bFile)));
    private static readonly Lazy<CaptureFile> E9c = new(() => CaptureFile.Load(TestPaths.Capture(E9cFile)));
    private static readonly Lazy<CaptureFile> E9d = new(() => CaptureFile.Load(TestPaths.Capture(E9dFile)));
    private static readonly Lazy<CaptureFile> E9e = new(() => CaptureFile.Load(TestPaths.Capture(E9eFile)));

    private byte _inboundSequence;

    private static Lazy<CaptureFile> Capture(string file) => file switch
    {
        E9File => E9,
        E9bFile => E9b,
        E9cFile => E9c,
        E9dFile => E9d,
        E9eFile => E9e,
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, null),
    };

    /// <summary>The bulk OUT transfer on endpoint 0x04 with that JSONL seq: bytes SmartSoft sent.</summary>
    private static byte[] Out(Lazy<CaptureFile> capture, int seq) =>
        Assert.Single(capture.Value.Frames, f => f.Seq == seq && f.Direction == CaptureDirection.Out && f.IsBulk && f.Endpoint == "0x04" && !f.Synthetic).Data;

    [Fact]
    public void Encode_FaderEvent_MatchesCapture_E9_Seq9665()
    {
        // SmartSoft's first fader event in E9: header seq 9664 (f19327), payload seq 9665 (f19329), channel fader 1 to 0x05
        // from previous 0x00 with counter 0x02 (its System pair had 0x00 and 0x01; F131, F132).
        var smartSofts = Out(E9, 9665);
        var payload = HspOutbound.FaderEvent(smartSofts[0], ClearedFader.Fader1, smartSofts[6], smartSofts[7]);

        Assert.Equal(smartSofts, payload);
        Assert.Equal(Out(E9, 9664), HspOutbound.DataHeader(payload, 0, 0, 0, 0));

        // the same bytes spelled out, so a reader sees them without the capture (E9 seq 9664 / 9665)
        Assert.Equal(Convert.FromHexString("010008000000000000000000"), Out(E9, 9664));
        Assert.Equal(Convert.FromHexString("0204001400000500"), smartSofts);
    }

    [Theory]
    [InlineData(E9File, 9665, ClearedFader.Fader1)]
    [InlineData(E9File, 24115, ClearedFader.Master)]
    [InlineData(E9File, 29434, ClearedFader.Bumps)]
    [InlineData(E9bFile, 8038, ClearedFader.Fader1)]
    [InlineData(E9bFile, 13144, ClearedFader.Master)]
    [InlineData(E9bFile, 16306, ClearedFader.Bumps)]
    public void Encode_each_whitelisted_fader_MatchesCapture(string file, int payloadSeq, ClearedFader fader)
    {
        // The first event of each index in E9 and E9b (F132): only the typed index comes from the test.
        AssertEncodes(Capture(file), payloadSeq, payload => HspOutbound.FaderEvent(payload[0], fader, payload[6], payload[7]));
    }

    /// <summary>
    /// SC-5 condition 3: every physical fader in each of the three banks, SmartSoft's first event of that index there in E9d's
    /// first pass (parts A, B, C; F171), encoded from the typed whitelist through <see cref="PhysicalControls.Fader"/>'s table:
    /// the payload and its Data header byte for byte, and the console's last ConsoleStatus before it names the bank.
    /// </summary>
    [Theory]
    [InlineData(E9dFile, 9395, 1, "Channels1To24")]
    [InlineData(E9dFile, 10006, 2, "Channels1To24")]
    [InlineData(E9dFile, 10476, 3, "Channels1To24")]
    [InlineData(E9dFile, 10848, 4, "Channels1To24")]
    [InlineData(E9dFile, 11362, 5, "Channels1To24")]
    [InlineData(E9dFile, 11880, 6, "Channels1To24")]
    [InlineData(E9dFile, 12305, 7, "Channels1To24")]
    [InlineData(E9dFile, 12681, 8, "Channels1To24")]
    [InlineData(E9dFile, 13048, 9, "Channels1To24")]
    [InlineData(E9dFile, 13362, 10, "Channels1To24")]
    [InlineData(E9dFile, 13672, 11, "Channels1To24")]
    [InlineData(E9dFile, 14072, 12, "Channels1To24")]
    [InlineData(E9dFile, 14358, 13, "Channels1To24")]
    [InlineData(E9dFile, 14615, 14, "Channels1To24")]
    [InlineData(E9dFile, 14945, 15, "Channels1To24")]
    [InlineData(E9dFile, 15383, 16, "Channels1To24")]
    [InlineData(E9dFile, 15670, 17, "Channels1To24")]
    [InlineData(E9dFile, 15964, 18, "Channels1To24")]
    [InlineData(E9dFile, 16251, 19, "Channels1To24")]
    [InlineData(E9dFile, 16536, 20, "Channels1To24")]
    [InlineData(E9dFile, 16779, 21, "Channels1To24")]
    [InlineData(E9dFile, 17019, 22, "Channels1To24")]
    [InlineData(E9dFile, 17279, 23, "Channels1To24")]
    [InlineData(E9dFile, 17518, 24, "Channels1To24")]
    [InlineData(E9dFile, 20112, 1, "Channels25To48")]
    [InlineData(E9dFile, 20404, 2, "Channels25To48")]
    [InlineData(E9dFile, 20686, 3, "Channels25To48")]
    [InlineData(E9dFile, 21038, 4, "Channels25To48")]
    [InlineData(E9dFile, 21356, 5, "Channels25To48")]
    [InlineData(E9dFile, 21637, 6, "Channels25To48")]
    [InlineData(E9dFile, 21941, 7, "Channels25To48")]
    [InlineData(E9dFile, 22242, 8, "Channels25To48")]
    [InlineData(E9dFile, 22587, 9, "Channels25To48")]
    [InlineData(E9dFile, 22873, 10, "Channels25To48")]
    [InlineData(E9dFile, 23143, 11, "Channels25To48")]
    [InlineData(E9dFile, 23452, 12, "Channels25To48")]
    [InlineData(E9dFile, 23778, 13, "Channels25To48")]
    [InlineData(E9dFile, 24024, 14, "Channels25To48")]
    [InlineData(E9dFile, 24269, 15, "Channels25To48")]
    [InlineData(E9dFile, 24508, 16, "Channels25To48")]
    [InlineData(E9dFile, 24770, 17, "Channels25To48")]
    [InlineData(E9dFile, 24984, 18, "Channels25To48")]
    [InlineData(E9dFile, 25229, 19, "Channels25To48")]
    [InlineData(E9dFile, 25552, 20, "Channels25To48")]
    [InlineData(E9dFile, 25848, 21, "Channels25To48")]
    [InlineData(E9dFile, 26118, 22, "Channels25To48")]
    [InlineData(E9dFile, 26363, 23, "Channels25To48")]
    [InlineData(E9dFile, 26587, 24, "Channels25To48")]
    [InlineData(E9dFile, 29922, 1, "Memories")]
    [InlineData(E9dFile, 30238, 2, "Memories")]
    [InlineData(E9dFile, 30522, 3, "Memories")]
    [InlineData(E9dFile, 30916, 4, "Memories")]
    [InlineData(E9dFile, 31262, 5, "Memories")]
    [InlineData(E9dFile, 31582, 6, "Memories")]
    [InlineData(E9dFile, 31874, 7, "Memories")]
    [InlineData(E9dFile, 32149, 8, "Memories")]
    [InlineData(E9dFile, 32415, 9, "Memories")]
    [InlineData(E9dFile, 32683, 10, "Memories")]
    [InlineData(E9dFile, 32933, 11, "Memories")]
    [InlineData(E9dFile, 33172, 12, "Memories")]
    [InlineData(E9dFile, 33567, 13, "Memories")]
    [InlineData(E9dFile, 33809, 14, "Memories")]
    [InlineData(E9dFile, 34012, 15, "Memories")]
    [InlineData(E9dFile, 34254, 16, "Memories")]
    [InlineData(E9dFile, 34469, 17, "Memories")]
    [InlineData(E9dFile, 34718, 18, "Memories")]
    [InlineData(E9dFile, 34939, 19, "Memories")]
    [InlineData(E9dFile, 35213, 20, "Memories")]
    [InlineData(E9dFile, 35428, 21, "Memories")]
    [InlineData(E9dFile, 35842, 22, "Memories")]
    [InlineData(E9dFile, 36151, 23, "Memories")]
    [InlineData(E9dFile, 36465, 24, "Memories")]
    public void Encode_every_physical_fader_in_every_bank_MatchesCapture(string file, int payloadSeq, int physicalFader, string bank)
    {
        var capture = Capture(file);

        AssertEncodes(capture, payloadSeq, payload => HspOutbound.FaderEvent(payload[0], PhysicalControls.Fader(physicalFader), payload[6], payload[7]));
        Assert.Equal(bank, ReportedBankBefore(capture.Value, payloadSeq));
    }

    /// <summary>
    /// SC-5 condition 3, the bumps (class B3): every memory bump's press and release in E9d's first pass (part C, F174), the
    /// channel bumps from a PC (E9c's bump 1 in bank 1-24 and bump 10, E9d's bump 7 held 4.67 s and bank 25-48's first bump,
    /// E9e's bumps 2, 12 and 24 and the bump under the fader labelled 48), through <see cref="PhysicalControls.Bump"/>'s table.
    /// </summary>
    [Theory]
    [InlineData(E9dFile, 37119, 1, true, "Memories")]
    [InlineData(E9dFile, 37266, 1, false, "Memories")]
    [InlineData(E9dFile, 37609, 2, true, "Memories")]
    [InlineData(E9dFile, 37706, 2, false, "Memories")]
    [InlineData(E9dFile, 38054, 3, true, "Memories")]
    [InlineData(E9dFile, 38157, 3, false, "Memories")]
    [InlineData(E9dFile, 38426, 4, true, "Memories")]
    [InlineData(E9dFile, 38556, 4, false, "Memories")]
    [InlineData(E9dFile, 38764, 5, true, "Memories")]
    [InlineData(E9dFile, 38879, 5, false, "Memories")]
    [InlineData(E9dFile, 39077, 6, true, "Memories")]
    [InlineData(E9dFile, 39185, 6, false, "Memories")]
    [InlineData(E9dFile, 39447, 7, true, "Memories")]
    [InlineData(E9dFile, 39535, 7, false, "Memories")]
    [InlineData(E9dFile, 39722, 8, true, "Memories")]
    [InlineData(E9dFile, 39798, 8, false, "Memories")]
    [InlineData(E9dFile, 39980, 9, true, "Memories")]
    [InlineData(E9dFile, 40100, 9, false, "Memories")]
    [InlineData(E9dFile, 40271, 10, true, "Memories")]
    [InlineData(E9dFile, 40371, 10, false, "Memories")]
    [InlineData(E9dFile, 40555, 11, true, "Memories")]
    [InlineData(E9dFile, 40666, 11, false, "Memories")]
    [InlineData(E9dFile, 40865, 12, true, "Memories")]
    [InlineData(E9dFile, 40969, 12, false, "Memories")]
    [InlineData(E9dFile, 41170, 13, true, "Memories")]
    [InlineData(E9dFile, 41276, 13, false, "Memories")]
    [InlineData(E9dFile, 41459, 14, true, "Memories")]
    [InlineData(E9dFile, 41579, 14, false, "Memories")]
    [InlineData(E9dFile, 41749, 15, true, "Memories")]
    [InlineData(E9dFile, 41834, 15, false, "Memories")]
    [InlineData(E9dFile, 42045, 16, true, "Memories")]
    [InlineData(E9dFile, 42162, 16, false, "Memories")]
    [InlineData(E9dFile, 42317, 17, true, "Memories")]
    [InlineData(E9dFile, 42425, 17, false, "Memories")]
    [InlineData(E9dFile, 42596, 18, true, "Memories")]
    [InlineData(E9dFile, 42694, 18, false, "Memories")]
    [InlineData(E9dFile, 42877, 19, true, "Memories")]
    [InlineData(E9dFile, 42967, 19, false, "Memories")]
    [InlineData(E9dFile, 43152, 20, true, "Memories")]
    [InlineData(E9dFile, 43253, 20, false, "Memories")]
    [InlineData(E9dFile, 43410, 21, true, "Memories")]
    [InlineData(E9dFile, 43551, 21, false, "Memories")]
    [InlineData(E9dFile, 43776, 22, true, "Memories")]
    [InlineData(E9dFile, 43898, 22, false, "Memories")]
    [InlineData(E9dFile, 44086, 23, true, "Memories")]
    [InlineData(E9dFile, 44199, 23, false, "Memories")]
    [InlineData(E9dFile, 44369, 24, true, "Memories")]
    [InlineData(E9dFile, 44463, 24, false, "Memories")]
    [InlineData(E9cFile, 29263, 1, true, "Channels1To24")]
    [InlineData(E9cFile, 29430, 1, false, "Channels1To24")]
    [InlineData(E9cFile, 86728, 10, true, "Channels1To24")]
    [InlineData(E9cFile, 86903, 10, false, "Channels1To24")]
    [InlineData(E9dFile, 46589, 7, true, "Channels1To24")]
    [InlineData(E9dFile, 47004, 7, false, "Channels1To24")]
    [InlineData(E9dFile, 27255, 1, true, "Channels25To48")]
    [InlineData(E9dFile, 27410, 1, false, "Channels25To48")]
    [InlineData(E9eFile, 17656, 2, true, "Channels1To24")]
    [InlineData(E9eFile, 17782, 2, false, "Channels1To24")]
    [InlineData(E9eFile, 18353, 12, true, "Channels1To24")]
    [InlineData(E9eFile, 18491, 12, false, "Channels1To24")]
    [InlineData(E9eFile, 18883, 24, true, "Channels1To24")]
    [InlineData(E9eFile, 19005, 24, false, "Channels1To24")]
    [InlineData(E9eFile, 20709, 24, true, "Channels25To48")]
    [InlineData(E9eFile, 20835, 24, false, "Channels25To48")]
    public void Encode_every_physical_bump_MatchesCapture(string file, int payloadSeq, int physicalBump, bool pressed, string bank)
    {
        var capture = Capture(file);

        AssertEncodes(capture, payloadSeq, payload => HspOutbound.KeyEvent(payload[0], PhysicalControls.Bump(physicalBump), pressed));
        Assert.Equal(bank, ReportedBankBefore(capture.Value, payloadSeq));
    }

    [Theory]
    [InlineData(19055, ClearedKey.Bump7, true)]
    [InlineData(19436, ClearedKey.Bump7, false)]
    [InlineData(39596, ClearedKey.Bump1, true)]
    [InlineData(73996, ClearedKey.Bump9, true)]
    [InlineData(43870, ClearedKey.Go, true)]
    [InlineData(36386, ClearedKey.Solo, true)]
    [InlineData(33542, ClearedKey.Blackout, true)]
    [InlineData(41449, ClearedKey.Faders1To24, true)]
    [InlineData(39472, ClearedKey.FadersMems, true)]
    public void Encode_each_whitelisted_key_MatchesCapture_E9(int payloadSeq, ClearedKey key, bool pressed)
    {
        // E9's first press of each of the eight SC-2 codes, and bump 7's release (F133; the code tables of the E9 record).
        AssertEncodes(E9, payloadSeq, payload => HspOutbound.KeyEvent(payload[0], key, pressed));
    }

    /// <summary>SC-5 classes B4 and B5: FADERS 25-48 (E9b, E9d, E9e), IND 1 and IND 2 (E9b, E9c), presses and releases (F175).</summary>
    [Theory]
    [InlineData(E9dFile, 18706, ClearedKey.Faders25To48, true)]
    [InlineData(E9dFile, 18711, ClearedKey.Faders25To48, false)]
    [InlineData(E9eFile, 19934, ClearedKey.Faders25To48, true)]
    [InlineData(E9eFile, 19939, ClearedKey.Faders25To48, false)]
    [InlineData(E9cFile, 24528, ClearedKey.Faders25To48, true)]
    [InlineData(E9cFile, 90315, ClearedKey.Ind1, true)]
    [InlineData(E9cFile, 90324, ClearedKey.Ind1, false)]
    [InlineData(E9cFile, 91256, ClearedKey.Ind2, true)]
    [InlineData(E9cFile, 91265, ClearedKey.Ind2, false)]
    public void Encode_the_SC5_keys_MatchesCapture(string file, int payloadSeq, ClearedKey key, bool pressed)
    {
        AssertEncodes(Capture(file), payloadSeq, payload => HspOutbound.KeyEvent(payload[0], key, pressed));
    }

    [Fact]
    public void The_whitelist_is_exactly_SC2s_and_SC5s()
    {
        // SC-2 condition 1 and SC-5 condition 3: the 24 physical faders' indices (E9d's first pass, bank 1-24), the MASTER and
        // BUMPS (E9), the 24 physical bumps' codes (E9d's memory bumps), GO, SOLO, BLACKOUT, FADERS 1-24, FADERS MEMS (E9),
        // FADERS 25-48 (E9d), IND 1 and IND 2 (E9c), as SmartSoft's own events carry them (the payload's byte 5), and nothing
        // else: no CLEAR, STACK, PAUSE, NEXT or record key.
        var faders = new[] { 9395, 10006, 10476, 10848, 11362, 11880, 12305, 12681, 13048, 13362, 13672, 14072, 14358, 14615, 14945, 15383, 15670, 15964, 16251, 16536, 16779, 17019, 17279, 17518 };
        var bumps = new[] { 37119, 37609, 38054, 38426, 38764, 39077, 39447, 39722, 39980, 40271, 40555, 40865, 41170, 41459, 41749, 42045, 42317, 42596, 42877, 43152, 43410, 43776, 44086, 44369 };
        var indices = faders.Select(seq => Out(E9d, seq)[5]).Concat(new[] { 24115, 29434 }.Select(seq => Out(E9, seq)[5])).Order().ToArray();
        var codes = bumps.Select(seq => Out(E9d, seq)[5])
            .Concat(new[] { 43870, 36386, 33542, 41449, 39472 }.Select(seq => Out(E9, seq)[5]))
            .Append(Out(E9d, 18706)[5])
            .Concat(new[] { 90315, 91256 }.Select(seq => Out(E9c, seq)[5]))
            .Order().ToArray();

        Assert.Equal(indices, Enum.GetValues<ClearedFader>().Select(f => (byte)f).Order().ToArray());
        Assert.Equal(codes, Enum.GetValues<ClearedKey>().Select(k => (byte)k).Order().ToArray());
        Assert.Equal(Enumerable.Range(1, 24).Select(PhysicalControls.Fader).Select(f => (byte)f), faders.Select(seq => Out(E9d, seq)[5]));
        Assert.Equal(Enumerable.Range(1, 24).Select(PhysicalControls.Bump).Select(k => (byte)k), bumps.Select(seq => Out(E9d, seq)[5]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalControls.Fader(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalControls.Fader(25));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalControls.Bump(25));
    }

    [Fact]
    public void Refuses_every_index_code_and_payload_outside_the_whitelist()
    {
        // A value cast into the typed whitelist is refused at the boundary (SC-2 condition 1, SC-5 condition 3): STACK's code
        // 0x3B (E9b seq 47230) as a fader index, REC MEM's code 0x41 (E9 seq 73006, class B2), and the NO-GO keys of SC-5:
        // STACK (E9b 47230), PAUSE 0x32 (E9c 93437), NEXT 0x3A (E9c 94597), CLEAR 0x38 (E9c 102688).
        Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.FaderEvent(0, (ClearedFader)Out(E9b, 47230)[5], 1, 0));
        foreach (var payload in new[] { Out(E9, 73006), Out(E9b, 47230), Out(E9c, 93437), Out(E9c, 94597), Out(E9c, 102688) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HspOutbound.KeyEvent(0, (ClearedKey)payload[5], pressed: true));
        }

        // No Data header for anything SmartSoft wrote outside the whitelist: the System write (E9 seq 138), a ChannelText
        // label (seq 57881), REC MEM, E9b's Memory write (seq 92053), STACK, PAUSE, NEXT and CLEAR.
        foreach (var payload in new[] { Out(E9, 138), Out(E9, 57881), Out(E9, 73006), Out(E9b, 92053), Out(E9b, 47230), Out(E9c, 93437), Out(E9c, 94597), Out(E9c, 102688) })
        {
            Assert.False(HspOutbound.IsClearedConsoleEvent(payload), Convert.ToHexString(payload));
            Assert.Throws<ArgumentException>(() => HspOutbound.DataHeader(payload, 0, 0, 0, 0));
        }

        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9, 9665)));
        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9, 19436)));
        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9, 67236)));   // fader 7's index, cleared by SC-5 (sent inside EDIT MEM in E9: the device's interlock drops such writes)
        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9d, 20112)));
        Assert.True(HspOutbound.IsClearedConsoleEvent(Out(E9c, 90315)));
    }

    /// <summary>
    /// The golden replays of SC-2 condition 2 and SC-5 condition 3. Each range is one SmartSoft session with a session of ours:
    /// E9 had one (up to the poll at seq 68339, whose reply lists System (39, 5, 5, 6) with c = 6 after SmartSoft's EDIT MEM
    /// selection, F137: a session cleared for reads faults there by design); E9b had three (its record: S1 seq 4 … 64680, S2
    /// 64681 … 70067, S3 70068 …), each restarting SmartSoft's counter and previous bytes (F131, F132). With SC-5's whitelist
    /// E9's fader 7 events (index 0x06, inside EDIT MEM) and E9b's fader 5, FADERS 25-48 and IND events are reproduced too.
    /// </summary>
    [Theory]
    [InlineData(E9File, int.MinValue, 68339, 328, 11)]
    [InlineData(E9bFile, int.MinValue, 64681, 381, 7)]
    [InlineData(E9bFile, 64681, 70068, 0, 3)]
    [InlineData(E9bFile, 70068, int.MaxValue, 29, 7)]
    public void Sends_SmartSofts_whitelisted_events_byte_for_byte_where_SmartSoft_sent_them(string file, int firstSeq, int endSeq, int writes, int skipped)
    {
        var capture = Capture(file);
        var pipe = new ReplayHspPipe(capture.Value, payload => HspOutbound.IsClearedConsoleEvent(payload.Span), firstSeq, endSeq);
        if (file == E9File)
        {
            // F132's one exception in E9: pseq 0x44 (seq 16560) carries previous 0x5D, a value that never reached the wire
            // (INFERRED: SmartSoft's coalescing). This host's previous byte is the last value it sent (SC-2 condition 3):
            // 0x5F, the value of pseq 0x43 (seq 16554).
            DeclareCoalescing(pipe, capture, 16560, 16554);
        }

        var session = ReplayLive(pipe);

        Assert.Equal(writes, pipe.MatchedDataWrites);
        Assert.Equal(writes, session.WritesSent);
        Assert.Equal(skipped, pipe.SkippedSmartSoftWrites);
        Assert.Equal(0, pipe.Remaining);
        Assert.Null(session.Fault);
        Assert.Null(session.HeldKey);
        Assert.Equal(0, session.QueuedWrites);
        Assert.Equal(file == E9File ? new[] { 16560 } : Array.Empty<int>(), pipe.DeviationsMet);
    }

    /// <summary>
    /// SC-5 condition 3: the golden replays over E9c, E9d and E9e, one SmartSoft session each. Every range starts at the
    /// first poll with zero stale words after SmartSoft's show download (158 records, 802 bytes: longer than any list a
    /// session of ours accepts, so a session of ours could not have been there, F177), with SmartSoft's System pair counted
    /// before the range (pseq 0x00 and 0x01 of that session; F58, F131). Where SmartSoft's first event came before the
    /// console's first LedStatus after the download, a session of ours would have held it (SC-2 condition 8), so that first
    /// step is skipped (<paramref name="expectFrom"/>, the header of the second step's first write; each first step ended
    /// with its control back at 0x00, so SmartSoft's previous bytes stay this host's). The ranges hold every switch
    /// SmartSoft made: E9c S1 FADERS 25-48 (seq 24528) then index 0x00 and 0x01 in bank 25-48, FADERS MEMS (seq 36537) then
    /// the memory faders and bumps, the S4 mode cases, the held bumps, IND 1 / IND 2, up to the poll answered by NEXT mode's
    /// BlindLevels listing (seq 97137; an id no read session accepts, F173); E9d's two passes, every fader in three banks and
    /// every memory bump, each switch, bump 7 held 4.67 s and 3.42 s; E9e's channel bumps around FADERS 25-48. The three
    /// coalesced previous bytes of E9d (seq 21367, 78433, 81351; the record's "Writes" row, as E9's seq 16560) are declared:
    /// this host's previous byte is the value it last sent.
    /// </summary>
    [Theory]
    [InlineData(E9cFile, 17, 868, 97137, 10392, 1005, 61)]
    [InlineData(E9dFile, 6, 813, 57702, int.MinValue, 1476, 0)]
    [InlineData(E9dFile, 57702, 58796, int.MaxValue, 65833, 1358, 17)]
    [InlineData(E9eFile, 5, 994, 22552, 18352, 10, 2)]
    [InlineData(E9eFile, 22552, 23515, int.MaxValue, 28803, 10, 2)]
    public void Sends_SmartSofts_SC5_events_byte_for_byte_where_SmartSoft_sent_them(string file, int smartSoftSession, int firstSeq, int endSeq, int expectFrom, int writes, int skipped)
    {
        var capture = Capture(file);
        var pipe = new ReplayHspPipe(capture.Value, payload => HspOutbound.IsClearedConsoleEvent(payload.Span), firstSeq, endSeq, smartSoftSession, expectFrom);
        var coalesced = file == E9dFile ? new[] { (21367, 21361), (78433, 78428), (81351, 81345) }.Where(c => c.Item1 >= firstSeq && c.Item1 < endSeq).ToArray() : Array.Empty<(int, int)>();
        foreach (var (seq, previousEvent) in coalesced)
        {
            DeclareCoalescing(pipe, capture, seq, previousEvent);
        }

        var session = ReplayLive(pipe);

        Assert.Equal(2, pipe.PayloadsBeforeRange);   // SmartSoft's System pair, before the download
        Assert.Equal(writes, pipe.MatchedDataWrites);
        Assert.Equal(writes, session.WritesSent);
        Assert.Equal(skipped, pipe.SkippedSmartSoftWrites);
        Assert.Equal(0, pipe.Remaining);
        Assert.Null(session.Fault);
        Assert.Null(session.HeldKey);
        Assert.Equal(0, session.QueuedWrites);
        Assert.Equal(coalesced.Select(c => c.Item1).ToArray(), pipe.DeviationsMet);
    }

    [Fact]
    public void The_SC5_replays_cover_every_new_index_in_every_bank_every_bump_code_and_the_new_keys()
    {
        // What the replays above reproduce, read from the same ranges of the captures: every index 0x00-0x17 in each of the
        // three banks, every bump code 0x00-0x17 (memory bumps; channel bumps 1, 7, 10, 12, 24 and bank 25-48's 1 and 24),
        // FADERS 25-48 and FADERS MEMS each followed by index 0x00 before any other fader event (SmartSoft's switch sequences),
        // IND 1 and IND 2.
        var events = new[]
        {
            Expected(E9c, 868, 97137, 10392), Expected(E9d, 813, 57702, int.MinValue), Expected(E9d, 58796, int.MaxValue, 65833),
            Expected(E9e, 994, 22552, 18352), Expected(E9e, 23515, int.MaxValue, 28803),
        }.SelectMany(e => e).ToArray();

        foreach (var bank in new[] { "Channels1To24", "Channels25To48", "Memories" })
        {
            Assert.Equal(Enumerable.Range(0, 24).Select(i => (byte)i), events.Where(e => e.Payload[4] == 0x00 && e.Bank == bank && e.Payload[5] <= 0x17).Select(e => e.Payload[5]).Distinct().Order());
        }

        Assert.Equal(Enumerable.Range(0, 24).Select(i => (byte)i), events.Where(e => e.Payload[4] == 0x01 && e.Bank == "Memories" && e.Payload[5] <= 0x17).Select(e => e.Payload[5]).Distinct().Order());
        Assert.Equal(new byte[] { 0x00, 0x06, 0x09, 0x0B, 0x17 }, events.Where(e => e.Payload[4] == 0x01 && e.Bank == "Channels1To24" && e.Payload[5] <= 0x17).Select(e => e.Payload[5]).Distinct().Order());
        Assert.Equal(new byte[] { 0x00, 0x17 }, events.Where(e => e.Payload[4] == 0x01 && e.Bank == "Channels25To48" && e.Payload[5] <= 0x17).Select(e => e.Payload[5]).Distinct().Order());
        Assert.Contains(events, e => e.Payload[4] == 0x01 && e.Payload[5] == (byte)ClearedKey.Ind1);
        Assert.Contains(events, e => e.Payload[4] == 0x01 && e.Payload[5] == (byte)ClearedKey.Ind2);

        // the switch sequences: after each press of FADERS 25-48 or FADERS MEMS, the next fader event is index 0x00
        foreach (var key in new[] { ClearedKey.Faders25To48, ClearedKey.FadersMems })
        {
            var presses = events.Select((e, i) => (e, i)).Where(x => x.e.Payload[4] == 0x01 && x.e.Payload[5] == (byte)key && x.e.Payload[6] == 0x01).ToArray();
            Assert.NotEmpty(presses);
            Assert.Contains(presses, p => events.Skip(p.i + 1).FirstOrDefault(e => e.Payload[4] == 0x00).Payload is { } next && next[5] == 0x00);
        }
    }

    /// <summary>
    /// The converse of the golden replays (the first review of SC-2 item 10, minor: they show where SmartSoft wrote, not
    /// that the session writes nowhere else). With a write waiting at every moment and a gate that always sends, a
    /// session replayed over E9 and E9b writes at the first transfer after every completed poll exchange once the
    /// console's first list and LED state are in, and nowhere else: never between a fetch and its poll, never twice
    /// before the next poll. The console side is replayed as recorded (SmartSoft's own writes skipped); our writes are
    /// not answered (F131), so they are taken off the wire before the replay pipe.
    /// </summary>
    [Theory]
    [InlineData(E9File, int.MinValue, 68339)]
    [InlineData(E9bFile, int.MinValue, 64681)]
    public void With_a_write_always_waiting_the_session_writes_right_after_each_completed_poll_exchange_and_nowhere_else(string file, int firstSeq, int endSeq)
    {
        var capture = Capture(file);
        var pipe = new ReplayHspPipe(capture.Value, expectWrite: null, firstSeq, endSeq);
        var session = new HspReadSession(_ => HspWriteDecision.Send);
        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        var listed = false;
        var ledsIn = false;
        var exchangeJustCompleted = false;   // the last transfer completed a poll exchange (its reply read whole)
        var lastOut = HspMessageType.Poll;
        var writesSincePoll = 0;
        var slots = 0;
        var writes = 0;
        byte value = 0;
        pipe.Open();
        while (!pipe.AtEnd)
        {
            session.QueueFader(ClearedFader.Bumps, value++);   // always a write waiting; a newer value replaces the queued one
            if (exchangeJustCompleted && listed && ledsIn)
            {
                slots++;
            }

            var next = session.Next();
            if (next.IsWrite)
            {
                Assert.True(exchangeJustCompleted, $"a write not right after a completed poll exchange, capture position {pipe.Position}");
                Assert.Equal(HspMessageType.Poll, lastOut);   // never between a fetch and its poll
                Assert.Equal(0, writesSincePoll);             // at most one write per poll
                writesSincePoll++;
                writes++;
                exchangeJustCompleted = false;
                continue;   // not on the replayed wire: the console answers no write (F131)
            }

            exchangeJustCompleted = false;
            pipe.Write(next.Bytes);
            lastOut = next.Kind;
            if (next.Kind != HspMessageType.Poll)
            {
                continue;
            }

            writesSincePoll = 0;
            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                foreach (var evt in session.Receive(span.ToArray()))
                {
                    listed |= evt is DirtyListReceived;
                    ledsIn |= evt is ObjectReceived { Payload.ObjectId: (int)HspObjectId.LedStatus };
                }

                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }

            exchangeJustCompleted = true;
        }

        Assert.True(writes > 1000, $"{writes} writes");
        Assert.Equal(slots, writes);   // and every such slot carried one
        Assert.Equal(writes, session.WritesSent);
        Assert.Equal(0, pipe.Remaining);
    }

    [Fact]
    public void Peeking_asks_nothing_and_a_slot_that_was_not_waited_for_as_a_writes_carries_no_write()
    {
        // SC-2 condition 5 (the first review of item 10, major 1): the loop waits for the kind PeekNextKind names and only
        // then lets the session choose, so the gate is asked after the wait, right before the header.
        var (session, gate) = LiveSession();
        ReportLeds(session);
        session.QueueFader(ClearedFader.Master, 0x10);

        Assert.Equal(HspMessageType.Data, session.PeekNextKind());
        Assert.Equal(HspMessageType.Data, session.PeekNextKind());   // asking again changes nothing
        Assert.Equal(0, gate.Asked);

        var poll = session.Next(writeSlotMet: false);                // a caller that waited for a poll's slot only
        Assert.Equal(HspMessageType.Poll, poll.Kind);
        Assert.Equal(0, gate.Asked);
        Assert.Equal(1, session.QueuedWrites);
        Assert.Equal(0, session.WritesSent);
        session.Receive(Header(4, 0));

        Assert.Equal(HspMessageType.Data, session.PeekNextKind());
        var write = session.Next(writeSlotMet: true);

        Assert.True(write.IsWrite);
        Assert.Equal(1, gate.Asked);
        Assert.Equal(HspMessageType.Poll, session.PeekNextKind());   // one write per poll
    }

    [Fact]
    public void A_writes_sent_callback_runs_once_it_is_taken_for_sending_and_never_for_a_hold_or_a_drop()
    {
        // The first review of SC-2 item 10, minor: what follows a send (a SOLO / BLACKOUT tap waiting for its LED) must
        // not run when a condition merely allowed the write.
        var (session, gate) = LiveSession();
        ReportLeds(session);
        var sent = new List<string>();
        gate.Decision = HspWriteDecision.Hold("the console's last report is older than two poll periods");
        session.QueueKeyTap(ClearedKey.Solo, sent: () => sent.Add("SOLO"));

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);      // held
        Assert.Empty(sent);
        session.Receive(Header(4, 0));
        gate.Decision = HspWriteDecision.Drop("REC MEM is lit or blinking");
        session.QueueFader(ClearedFader.Bumps, 0x10, sent: () => sent.Add("BUMPS"));
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);      // the tap dropped unsent
        Assert.Empty(sent);
        session.Receive(Header(4, 0));
        gate.Decision = HspWriteDecision.Send;

        Assert.True(session.Next().IsWrite);                         // the BUMPS event
        Assert.Equal(new[] { "BUMPS" }, sent);
        Assert.Equal(1, session.WritesDropped);
    }

    [Fact]
    public void A_read_only_session_accepts_no_write()
    {
        var session = new HspReadSession();

        Assert.False(session.CanWrite);
        Assert.Throws<InvalidOperationException>(() => session.QueueFader(ClearedFader.Master, 0x80));
        Assert.Throws<InvalidOperationException>(() => session.QueueKeyTap(ClearedKey.Go));
        Assert.Throws<InvalidOperationException>(() => session.QueueKeyHold(ClearedKey.Bump7));
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
    }

    [Fact]
    public void Nothing_is_written_before_the_first_list_and_the_LED_state()
    {
        var (session, gate) = LiveSession();
        session.QueueFader(ClearedFader.Master, 0x80);

        IdleExchange(session);
        IdleExchange(session);   // SC-2 condition 8: no list, no LED state yet, so no write
        Assert.Contains("condition 8", session.WriteHeldReason, StringComparison.Ordinal);
        ReportLeds(session);     // the list and LedStatus arrive; the write slot after them carries the write
        Assert.Equal(0, session.WritesSent);
        Assert.Equal(0, gate.Asked);

        var write = session.Next();

        Assert.True(write.IsWrite);
        Assert.Equal(1, gate.Asked);
        Assert.Null(session.WriteHeldReason);
    }

    [Fact]
    public void A_write_goes_right_after_a_poll_exchange_before_a_pending_fetch_and_never_between_a_fetch_and_its_poll()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.FaderInfo, 0, 0, 0), new DirtyObjectRecord((int)HspObjectId.ChannelLevels, 0, 0, 0));
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);

        session.QueueFader(ClearedFader.Master, 0x80);
        Assert.True(session.Next().IsWrite);                          // before the pending fetches, as SmartSoft's writes (E9, E9b)
        session.QueueFader(ClearedFader.Bumps, 0x40);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);      // at most one write per poll
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // never between a fetch and its poll
        var faderInfo = Payload(NextInbound(), (int)HspObjectId.FaderInfo, new byte[195]);
        session.Receive(Header(3, (ushort)faderInfo.Length, (ushort)HspObjectId.FaderInfo));
        session.Receive(faderInfo);

        var second = session.Next();                                  // the slot after the object reply

        Assert.True(second.IsWrite);
        Assert.Equal((byte)ClearedFader.Bumps, second.Payload![5]);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);
    }

    [Fact]
    public void The_counter_starts_at_zero_and_the_previous_byte_is_this_hosts_last_value()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);

        session.QueueFader(ClearedFader.Master, 0x80);
        var first = session.Next();
        IdleExchange(session);
        session.QueueFader(ClearedFader.Master, 0x40);
        session.QueueFader(ClearedFader.Master, 0x20);                 // replaces the queued 0x40, which never goes out
        var second = session.Next();
        IdleExchange(session);
        session.QueueFader(ClearedFader.Fader1, 0x10);
        var third = session.Next();

        // SmartSoft's own events (E9 seq 24115, the MASTER's first, and seq 9665, channel fader 1's first) with this
        // session's counter, value and previous byte (F131, F132); after an idle reply the header carries zero stale
        // words, as SmartSoft's at seq 9664
        Assert.Equal(AsSent(24115, 0x00, value: 0x80, previous: 0x00), first.Payload);
        Assert.Equal(AsSent(24115, 0x01, value: 0x20, previous: 0x80), second.Payload);
        Assert.Equal(AsSent(9665, 0x02, value: 0x10, previous: 0x00), third.Payload);
        Assert.Equal(Out(E9, 9664), third.Bytes);
        Assert.Equal(3, session.WritesSent);
    }

    [Fact]
    public void The_counter_wraps_from_0xFF_to_0x00()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);

        byte last = 0;
        for (var i = 0; i < 257; i++)
        {
            session.QueueFader(ClearedFader.Bumps, (byte)i);
            last = session.Next().Payload![0];
            IdleExchange(session);
        }

        Assert.Equal(0x00, last);   // the 257th write (F131: E9b wrapped 0xFF -> 0x00 at seq 39860)
    }

    [Fact]
    public void A_tap_is_its_press_then_its_release_at_the_next_slot_with_nothing_between()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Go);
        session.QueueFader(ClearedFader.Master, 0x10);

        var press = session.Next();
        Assert.Equal(ClearedKey.Go, session.HeldKey);
        Assert.False(session.HoldInProgress);                         // a tap's release is due at once
        Assert.False(session.IsIdle);                                 // a press is never left without its release
        IdleExchange(session);
        var release = session.Next();
        IdleExchange(session);
        var fader = session.Next();

        // SmartSoft's GO press and release (E9 seq 43870, 43953) with this session's counter
        Assert.Equal(AsSent(43870, 0x00), press.Payload);
        Assert.Equal(AsSent(43953, 0x01), release.Payload);
        Assert.Equal((byte)ClearedFader.Master, fader.Payload![5]);
        Assert.Null(session.HeldKey);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
    }

    [Fact]
    public void A_held_bump_keeps_its_press_until_its_release_is_asked_for_and_nothing_goes_between()
    {
        // SC-5 class B3 and condition 2: the press, then nothing (a fader event waits) until the owner of the hold asks for
        // the release, which goes at the next write slot, once. SmartSoft's bump 7 press and release (E9d seq 46589 / 47004,
        // held 4.67 s in pass 1) with this session's counter.
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyHold(ClearedKey.Bump7);
        session.QueueFader(ClearedFader.Master, 0x10);
        Assert.True(session.HoldInProgress);

        var press = session.Next();
        for (var i = 0; i < 5; i++)
        {
            IdleExchange(session);                                    // held: polls only, the MASTER event waits
        }

        Assert.Equal(ClearedKey.Bump7, session.HeldKey);
        Assert.True(session.HoldInProgress);
        Assert.False(session.IsIdle);
        Assert.True(session.ReleaseKey(ClearedKey.Bump7));
        Assert.False(session.ReleaseKey(ClearedKey.Bump7));          // asked for once: a second ask changes nothing
        Assert.False(session.HoldInProgress);
        var release = session.Next();
        IdleExchange(session);
        var fader = session.Next();

        Assert.Equal(AsSentFrom(E9d, 46589, 0x00), press.Payload);
        Assert.Equal(AsSentFrom(E9d, 47004, 0x01), release.Payload);
        Assert.Equal((byte)ClearedFader.Master, fader.Payload![5]);
        Assert.False(session.ReleaseKey(ClearedKey.Bump7));          // released: nothing left to release
    }

    [Fact]
    public void A_hold_released_before_its_press_went_becomes_a_tap_and_only_a_bump_is_ever_held()
    {
        var (session, gate) = LiveSession();
        ReportLeds(session);
        gate.Decision = HspWriteDecision.Hold("the console listed new key LEDs that are not fetched yet");
        session.QueueKeyHold(ClearedKey.Bump12);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // the press waits
        session.Receive(Header(4, 0));
        Assert.True(session.ReleaseKey(ClearedKey.Bump12));          // the UI let go first: a tap
        Assert.False(session.HoldInProgress);
        gate.Decision = HspWriteDecision.Send;

        var press = session.Next();
        IdleExchange(session);
        var release = session.Next();

        // SmartSoft's channel bump 12 press and release (E9e seq 18353 / 18491) with this session's counter
        Assert.Equal(AsSentFrom(E9e, 18353, 0x00), press.Payload);
        Assert.Equal(AsSentFrom(E9e, 18491, 0x01), release.Payload);

        // every other key is a tap: a hold of GO, SOLO, a bank key or IND 1 is unrepresentable (SC-5 class B3)
        foreach (var key in new[] { ClearedKey.Go, ClearedKey.Solo, ClearedKey.Blackout, ClearedKey.Faders1To24, ClearedKey.Faders25To48, ClearedKey.FadersMems, ClearedKey.Ind1, ClearedKey.Ind2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => session.QueueKeyHold(key));
        }
    }

    [Fact]
    public void A_stop_makes_a_held_bumps_release_due_at_once()
    {
        // SC-5 condition 2: "and before any Disconnect" while the pipe is alive (the stop path of SC-2 condition 4).
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyHold(ClearedKey.Bump1);
        Assert.True(session.Next().IsWrite);
        IdleExchange(session);
        Assert.Equal(HspMessageType.Poll, session.PeekNextKind());   // held: nothing due in this write slot

        session.DiscardQueuedWrites(acceptNoMore: true);
        Assert.Equal(HspMessageType.Data, session.PeekNextKind());   // the release, in the same slot
        var release = session.Next();

        Assert.Equal(AsSentFrom(E9d, 37266, 0x01), release.Payload);  // SmartSoft's memory bump 1 release (E9d seq 37266)
        Assert.Null(session.HeldKey);
    }

    [Fact]
    public void The_gate_holds_a_new_write_back_but_never_a_release()
    {
        var (session, gate) = LiveSession();
        ReportLeds(session);
        gate.Decision = HspWriteDecision.Hold("REC MEM is lit");
        session.QueueFader(ClearedFader.Master, 0x10);

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        Assert.Equal("REC MEM is lit", session.WriteHeldReason);
        Assert.Equal(1, session.QueuedWrites);
        session.Receive(Header(4, 0));
        gate.Decision = HspWriteDecision.Send;
        Assert.True(session.Next().IsWrite);
        Assert.Null(session.WriteHeldReason);

        IdleExchange(session);
        session.QueueKeyTap(ClearedKey.Solo);
        Assert.True(session.Next().IsWrite);                          // the press
        IdleExchange(session);
        gate.Decision = HspWriteDecision.Drop("COPY is lit");
        var asked = gate.Asked;
        var release = session.Next();

        Assert.Equal(AsSent(36391, 0x02), release.Payload);           // SmartSoft's SOLO release (E9 seq 36391)
        Assert.Equal(asked, gate.Asked);                              // a release completes a press: nothing is asked
        Assert.Equal(0, session.WritesDropped);
    }

    [Fact]
    public void A_writes_own_condition_holds_or_drops_it_and_nothing_else_goes_in_that_slot()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        var condition = HspWriteDecision.Hold("the console listed FaderInfo");
        session.QueueFader(ClearedFader.Fader1, 0x10, () => condition);
        session.QueueFader(ClearedFader.Bumps, 0x20);

        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // held: the BUMPS write behind it waits too
        Assert.Equal("the console listed FaderInfo", session.WriteHeldReason);
        Assert.True(session.IsFaderQueued(ClearedFader.Fader1));
        session.Receive(Header(4, 0));
        condition = HspWriteDecision.Drop("the console is in memory mode");
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);       // dropped unsent; no second write in that slot
        Assert.Equal(1, session.WritesDropped);
        Assert.Equal("the console is in memory mode", session.LastDropReason);
        Assert.Equal(1, session.QueuedWrites);
        Assert.False(session.IsFaderQueued(ClearedFader.Fader1));
        session.Receive(Header(4, 0));

        var next = session.Next();

        Assert.True(next.IsWrite);
        Assert.Equal((byte)ClearedFader.Bumps, next.Payload![5]);
        Assert.Equal(0x00, next.Payload[0]);                          // the dropped write never took a counter value
        Assert.Equal(1, session.WritesSent);
    }

    [Fact]
    public void A_session_is_not_idle_before_the_poll_exchange_after_its_last_write()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueFader(ClearedFader.Master, 0x10);

        Assert.True(session.Next().IsWrite);
        Assert.False(session.IsIdle);                                 // SmartSoft polled 14.5-16.5 ms after every write
        IdleExchange(session);
        Assert.True(session.IsIdle);
    }

    [Fact]
    public void A_stop_drops_queued_writes_and_keeps_the_owed_release()
    {
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Bump7);
        Assert.True(session.Next().IsWrite);
        session.QueueFader(ClearedFader.Bumps, 0x20);

        var dropped = session.DiscardQueuedWrites(acceptNoMore: true);

        Assert.Equal(1, dropped);
        Assert.Equal(ClearedKey.Bump7, session.HeldKey);
        Assert.False(session.IsIdle);
        Assert.Throws<InvalidOperationException>(() => session.QueueFader(ClearedFader.Bumps, 0x20));
        IdleExchange(session);
        var release = session.Next();
        Assert.Equal(AsSent(19436, 0x01), release.Payload);           // SmartSoft's bump 7 release (E9 seq 19436)
        Assert.False(session.IsIdle);                                 // the poll after the release is still owed
        IdleExchange(session);
        Assert.True(session.IsIdle);
    }

    [Fact]
    public void A_release_at_the_end_is_offered_only_when_no_exchange_is_open()
    {
        // a fault on a payload read whole (byte 1 = 0x01, never observed): the console owes nothing more
        var (session, _) = LiveSession();
        ReportLeds(session);
        session.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(session.Next().IsWrite);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        list[1] = 0x01;
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);
        Assert.Equal(HspReadSessionState.Faulted, session.State);

        var release = session.TakeReleaseAtEnd();

        Assert.NotNull(release);
        Assert.Equal(AsSent(33547, 0x01), release!.Payload);          // SmartSoft's BLACKOUT release (E9 seq 33547)
        Assert.Null(session.HeldKey);

        // a fault on a header that announces a payload: the payload is still due, so no write may follow
        var (open, _) = LiveSession();
        ReportLeds(open);
        open.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(open.Next().IsWrite);
        Assert.Equal(HspMessageType.Poll, open.Next().Kind);
        open.Receive(Header(4, HspReadSession.MaxObservedPayloadLength + 1));
        Assert.Equal(HspReadSessionState.Faulted, open.State);
        Assert.Null(open.TakeReleaseAtEnd());
        Assert.Equal(ClearedKey.Blackout, open.HeldKey);

        // right after the press, before the poll that follows it: a second write in that poll cycle is never offered
        var (fresh, _) = LiveSession();
        ReportLeds(fresh);
        fresh.QueueKeyTap(ClearedKey.Blackout);
        Assert.True(fresh.Next().IsWrite);
        Assert.Null(fresh.TakeReleaseAtEnd());
    }

    /// <summary>The payload of SmartSoft's write at <paramref name="payloadSeq"/> and its Data header (the transfer before it), both as the encoder builds them from the typed event.</summary>
    private static void AssertEncodes(Lazy<CaptureFile> capture, int payloadSeq, Func<byte[], byte[]> encode)
    {
        var smartSofts = Out(capture, payloadSeq);
        var header = Out(capture, payloadSeq - 1);

        var payload = encode(smartSofts);

        Assert.Equal(smartSofts, payload);
        Assert.Equal(header, HspOutbound.DataHeader(payload, Word(header, 4), Word(header, 6), Word(header, 8), Word(header, 10)));
    }

    /// <summary>
    /// F132's coalescing exception at <paramref name="seq"/>: SmartSoft's previous byte names a value that never reached the
    /// wire (E9 seq 16560; E9d seq 21367, 78433, 81351). This host's previous byte is the value it last sent for that index:
    /// the value byte of SmartSoft's preceding event of the index (<paramref name="previousEvent"/>), taken from the capture.
    /// </summary>
    private static void DeclareCoalescing(ReplayHspPipe pipe, Lazy<CaptureFile> capture, int seq, int previousEvent)
    {
        var expected = Out(capture, seq).ToArray();   // a copy: the capture's own bytes stay as recorded
        Assert.Equal(expected[5], Out(capture, previousEvent)[5]);
        Assert.NotEqual(expected[7], Out(capture, previousEvent)[6]);
        expected[7] = Out(capture, previousEvent)[6];
        pipe.ExpectInstead(seq, expected);
    }

    /// <summary>
    /// The bank the console reported (its last ConsoleStatus, F98: body[0] memory mode, body[4] bank 25-48) before the
    /// transfer at <paramref name="seq"/>, read from the capture's replies with the header / payload pairing of F53.
    /// </summary>
    private static string ReportedBankBefore(CaptureFile capture, int seq)
    {
        string? bank = null;
        var due = 0;
        foreach (var frame in capture.BulkFrames)
        {
            if (frame.Seq >= seq)
            {
                break;
            }

            if (frame.Direction != CaptureDirection.In)
            {
                continue;
            }

            if (due > 0)
            {
                if (frame.Data.Length == due && frame.Data[4] == (byte)HspObjectId.ConsoleStatus && due == 5 + 7)
                {
                    bank = frame.Data[5] == 1 ? "Memories" : frame.Data[9] == 1 ? "Channels25To48" : "Channels1To24";
                }

                due = 0;
            }
            else if (frame.Data.Length == HspHeader.Size)
            {
                due = frame.Data[2] | (frame.Data[3] << 8);
            }
        }

        return bank ?? "unreported";
    }

    /// <summary>SmartSoft's whitelisted writes the replay above expects in the range (the payloads, with the bank the console reported before each), in one pass.</summary>
    private static List<(byte[] Payload, string Bank)> Expected(Lazy<CaptureFile> capture, int firstSeq, int endSeq, int expectFrom)
    {
        var events = new List<(byte[] Payload, string Bank)>();
        var frames = capture.Value.BulkFrames.ToArray();
        var bank = "unreported";
        var due = 0;
        for (var i = 0; i + 1 < frames.Length && frames[i].Seq < endSeq; i++)
        {
            var frame = frames[i];
            if (frame.Direction == CaptureDirection.In)
            {
                if (due > 0)
                {
                    if (frame.Data.Length == due && due == 5 + 7 && frame.Data[4] == (byte)HspObjectId.ConsoleStatus)
                    {
                        bank = frame.Data[5] == 1 ? "Memories" : frame.Data[9] == 1 ? "Channels25To48" : "Channels1To24";
                    }

                    due = 0;
                }
                else if (frame.Data.Length == HspHeader.Size)
                {
                    due = frame.Data[2] | (frame.Data[3] << 8);
                }

                continue;
            }

            if (frame.Seq >= firstSeq && frame.Seq >= expectFrom && frame.Data.Length == HspHeader.Size && frame.Data[0] == 1 && frame.Data[1] == 0
                && HspOutbound.IsClearedConsoleEvent(frames[i + 1].Data))
            {
                events.Add((frames[i + 1].Data, bank));
            }
        }

        return events;
    }

    /// <summary>SmartSoft's payload at that E9 seq with this session's counter (and a value and previous byte) in place of its own.</summary>
    private static byte[] AsSent(int seq, byte counter, byte? value = null, byte? previous = null)
    {
        var bytes = AsSentFrom(E9, seq, counter);
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

    /// <summary>SmartSoft's payload at that seq of the capture with this session's counter in place of its own.</summary>
    private static byte[] AsSentFrom(Lazy<CaptureFile> capture, int seq, byte counter)
    {
        var bytes = Out(capture, seq).ToArray();   // a copy: the capture's bytes stay as recorded
        bytes[0] = counter;
        return bytes;
    }

    /// <summary>
    /// Runs a session in write mode over the pipe, handing it SmartSoft's event at each write the pipe expects. The
    /// interlock of SC-2 condition 5 is the device's: here every point where SmartSoft wrote is a point where the session
    /// must write, so the gate stays open.
    /// </summary>
    private static HspReadSession ReplayLive(ReplayHspPipe pipe)
    {
        var session = new HspReadSession(_ => HspWriteDecision.Send);
        var buffer = new byte[HspReadSession.MaxObservedPayloadLength];
        pipe.Open();
        while (!pipe.AtEnd)
        {
            if (pipe.ExpectedWrite is { } smartSofts)
            {
                HandOver(session, smartSofts.Span);
            }

            var next = session.Next();
            pipe.Write(next.Bytes);
            if (next.Payload is { } payload)
            {
                pipe.Write(payload);
                continue;
            }

            while (session.ExpectedReadLength > 0)
            {
                var span = buffer.AsSpan(0, session.ExpectedReadLength);
                pipe.ReadExactly(span);
                session.Receive(span.ToArray());
                Assert.True(session.State != HspReadSessionState.Faulted, $"faulted at capture position {pipe.Position}: {session.Fault}");
            }
        }

        return session;
    }

    /// <summary>SmartSoft's event as the typed whitelist names it: what its user did, never its bytes.</summary>
    private static void HandOver(HspReadSession session, ReadOnlySpan<byte> payload)
    {
        // [pseq][u16 LE len][0x14][kind][index or code][value or state]([previous]) (F131)
        if (payload[4] == 0x00)
        {
            session.QueueFader((ClearedFader)payload[5], payload[6]);
        }
        else if (payload[6] == 0x01)
        {
            session.QueueKeyPress((ClearedKey)payload[5]);
        }
        else
        {
            Assert.True(session.ReleaseKey((ClearedKey)payload[5]), $"no press of 0x{payload[5]:X2} to release");
        }
    }

    private static ushort Word(byte[] header, int offset) => (ushort)(header[offset] | (header[offset + 1] << 8));

    private static (HspReadSession Session, TestGate Gate) LiveSession()
    {
        var gate = new TestGate();
        return (new HspReadSession(gate.Check), gate);
    }

    /// <summary>One completed poll exchange: the poll, answered idle.</summary>
    private static void IdleExchange(HspReadSession session)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        session.Receive(Header(4, 0));
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    /// <summary>The console's first list, naming LedStatus, and the LedStatus object it delivers (SC-2 condition 8).</summary>
    private void ReportLeds(HspReadSession session)
    {
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var list = ListPayload(NextInbound(), new DirtyObjectRecord((int)HspObjectId.LedStatus, 0, 0, 0));
        session.Receive(Header(4, (ushort)list.Length));
        session.Receive(list);
        Assert.Equal(HspMessageType.Fetch, session.Next().Kind);
        Assert.Equal(HspMessageType.Poll, session.Next().Kind);
        var leds = Payload(NextInbound(), (int)HspObjectId.LedStatus, new byte[451]);
        session.Receive(Header(3, (ushort)leds.Length, (ushort)HspObjectId.LedStatus));
        session.Receive(leds);
        Assert.Equal(HspReadSessionState.ReadyToSend, session.State);
    }

    private byte NextInbound() => _inboundSequence++;

    /*
     * The replies built by Header / Payload / ListPayload are hand-made inputs for the state machine, as in
     * HspReadSessionTests (a capture cannot show the console misbehaving, nor a write queued at a chosen point); their
     * shapes follow F53 (reply header), F54 (payload layout) and F57 (DirtyObjects body). Zero bodies stand in for an
     * object's content where the test is about the session, not the content (LedStatus 451 bytes, FaderInfo 195, F67).
     */

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

    /// <summary>A gate the test sets (send, hold or drop); it counts how often it was asked.</summary>
    private sealed class TestGate
    {
        public HspWriteDecision Decision { get; set; } = HspWriteDecision.Send;

        public int Asked { get; private set; }

        public HspWriteDecision Check(HspReadSession session)
        {
            Asked++;
            return Decision;
        }
    }
}
