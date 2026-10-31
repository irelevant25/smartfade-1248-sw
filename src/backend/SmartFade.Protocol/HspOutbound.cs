using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>
/// The fader indices of ConsoleEvent kind 0 whitelisted by safety check SC-2 (2026-10-31, class A), and no other:
/// every index outside these three is unrepresentable here, and <see cref="HspOutbound"/> refuses a value cast into
/// this type (SC-2 condition 1). No index is derived from a fader number: "channel fader k = index k − 1" stays
/// HYPOTHESIS and is not used. VERIFIED (E9 + E9b) is no clearance by itself: SC-2's GO is conditional, and nothing is
/// sent before E11 on the owner's go.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F131, F132, F134", "SC-2 class A whitelist: indices 0x00, 0x30, 0x31 only")]
public enum ClearedFader : byte
{
    /// <summary>Index 0x00, channel fader 1: FaderInfo record 1, ChannelLevels[1] and OutputLevels[2] (DMX 1) followed (F132, F134); every PC event of E9 and E9b came in channel mode, bank 1-24.</summary>
    ChannelFader1 = 0x00,

    /// <summary>Index 0x30, the MASTER: FaderInfo body[194] followed (F132).</summary>
    Master = 0x30,

    /// <summary>Index 0x31, BUMPS: FaderInfo body[193] followed (F132).</summary>
    Bumps = 0x31,
}

/// <summary>
/// The key codes of ConsoleEvent kind 1 whitelisted by SC-2 (2026-10-31, class B1) as live keys, sent only as a press
/// followed by its release (SC-2 condition 4), and no other. The record keys (REC MEM 0x41, EDIT MEM 0x42, COPY 0x3E,
/// CLEAR 0x38) are NO-GO (class B2, deferred to SC-3) and absent; no code is derived from an LED block number (H82
/// stays HYPOTHESIS). The LED block named for each code is the one that changed at its presses (F133).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F131, F133; F134 for every code but bump 9", "SC-2 class B1 whitelist: eight live codes only, press / release pairs; bump 9 (0x08) was seen from a PC only inside record functions, so its effect outside them is unobserved and the device never sends it")]
public enum ClearedKey : byte
{
    /// <summary>Code 0x00, bump 1 (memory bump 1 in memory mode), LED block 1.</summary>
    Bump1 = 0x00,

    /// <summary>Code 0x06, bump 7, LED block 7.</summary>
    Bump7 = 0x06,

    /// <summary>Code 0x08, bump 9, LED block 9.</summary>
    Bump9 = 0x08,

    /// <summary>Code 0x31, GO, LED block 50 (seen from the PC only with STACK unlit, F134).</summary>
    Go = 0x31,

    /// <summary>Code 0x33, SOLO, LED block 52.</summary>
    Solo = 0x33,

    /// <summary>Code 0x34, BLACKOUT, LED block 53.</summary>
    Blackout = 0x34,

    /// <summary>Code 0x36, FADERS 1-24, LED block 55.</summary>
    Faders1To24 = 0x36,

    /// <summary>Code 0x39, FADERS MEMS, LED block 58.</summary>
    FadersMems = 0x39,
}

/// <summary>
/// Every PC → console transfer our software can produce, byte for byte as SmartSoft sends it, and nothing else.
/// <list type="bullet">
/// <item>The poll and the fetch (VERIFIED, F52: 38 517 polls and 1 651 fetches in E2, E3 and E3b, the stale-word rule
/// re-checked on 2026-10-30 with `tools/protocol-analyzer/poll_rules.py`), cleared by safety check SC-1
/// (docs/protocol/safety-checks.md) for a read-only session: a fetch only for a record that the console itself listed
/// in a DirtyObjects list of the same session, in list order, once.</item>
/// <item>The Data header and the two ConsoleEvent payloads, fader event (kind 0) and key event (kind 1) (VERIFIED as
/// SmartSoft's behaviour, F131, F132, F133: 828 of 828 writes in E9 and E9b follow the framing), built only from the
/// typed whitelist <see cref="ClearedFader"/> / <see cref="ClearedKey"/> (SC-2 condition 1). SC-2 (2026-10-31) is a
/// conditional GO for building and reviewing this encoder: it clears no byte for sending until its conditions are met
/// and the owner gives the go for E11, and only a session created with a write gate (live control, behind
/// `Device:Usb:LiveControlDate`) ever asks for these.</item>
/// </list>
/// No System write, no object write (ChannelText, Memory), no record key and no index or code outside the whitelist
/// can be encoded here (SC-2 condition 8; classes B2, C, D, E NO-GO, F never).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F52, F53, F57", "cleared by SC-1 (2026-10-30) for a read-only session: poll and listed fetch")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133", "SC-2 (2026-10-31) classes A and B1: conditional GO to build and review; nothing sent before E11 on the owner's go")]
public static class HspOutbound
{
    /// <summary>Object id of ConsoleEvent, the live-control message (F131; id → name S11.6).</summary>
    private const byte ConsoleEventId = (byte)HspObjectId.ConsoleEvent;

    /// <summary>Body byte 0 of a fader event (F132).</summary>
    private const byte FaderKind = 0x00;

    /// <summary>Body byte 0 of a key event (F133).</summary>
    private const byte KeyKind = 0x01;

    /// <summary>Payload length of a fader event: [seq][u16 LE 4][0x14] + 4 body bytes (w1 = 8, F131).</summary>
    private const int FaderEventLength = HspOutboundPayload.HeaderSize + 4;

    /// <summary>Payload length of a key event: [seq][u16 LE 3][0x14] + 3 body bytes (w1 = 7, F131).</summary>
    private const int KeyEventLength = HspOutboundPayload.HeaderSize + 3;

    /// <summary>
    /// A poll: w0 = 0, w1 = 0, w2..w5 = the "stale" words, which SmartSoft copies from the most
    /// recent fetch or reply header (all zero before the first; F52).
    /// </summary>
    public static byte[] Poll(ushort w2, ushort w3, ushort w4, ushort w5) =>
        Header((ushort)HspMessageType.Poll, 0, w2, w3, w4, w5);

    /// <summary>A fetch of one listed record: w0 = 2, w1 = 0, w2 = object id, w3..w5 = qualifiers a, b, c (F52, F57).</summary>
    public static byte[] Fetch(DirtyObjectRecord record)
    {
        if (record.ObjectId is < 0 or > ushort.MaxValue || record.A is < 0 or > ushort.MaxValue
            || record.B is < 0 or > ushort.MaxValue || record.C is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(record), record, "Fetch words must fit in u16.");
        }

        return Header((ushort)HspMessageType.Fetch, 0, (ushort)record.ObjectId, (ushort)record.A, (ushort)record.B, (ushort)record.C);
    }

    /// <summary>
    /// The Data header of a whitelisted ConsoleEvent: w0 = 1, w1 = the payload's length, w2..w5 = the stale words, the
    /// same words a poll would carry at that point (F52; E9 and E9b: 828 of 828 Data headers). Produced only for a
    /// payload that <see cref="IsClearedConsoleEvent"/> accepts, so no Data header can announce anything else.
    /// SmartSoft's first one, E9 seq 9664: `01 00 08 00 00 00 00 00 00 00 00 00`.
    /// </summary>
    public static byte[] DataHeader(ReadOnlySpan<byte> consoleEventPayload, ushort w2, ushort w3, ushort w4, ushort w5)
    {
        if (!IsClearedConsoleEvent(consoleEventPayload))
        {
            throw new ArgumentException(
                $"A Data header is produced only for a whitelisted ConsoleEvent payload (SC-2 condition 1), not for {Convert.ToHexString(consoleEventPayload)}.",
                nameof(consoleEventPayload));
        }

        return Header((ushort)HspMessageType.Data, (ushort)consoleEventPayload.Length, w2, w3, w4, w5);
    }

    /// <summary>
    /// ConsoleEvent kind 0, a fader event: `[seq] 04 00 14 00 [index] [value] [previous]` (F131, F132). The value is the
    /// console's level byte, unconverted; <paramref name="previous"/> is this host's own last value of that index, 0x00
    /// for its first event of the session, never a value read from the console (SC-2 condition 3).
    /// SmartSoft's first one, E9 seq 9665: `02 04 00 14 00 00 05 00`.
    /// </summary>
    public static byte[] FaderEvent(byte sequence, ClearedFader fader, byte value, byte previous)
    {
        if (!Enum.IsDefined(fader))
        {
            throw new ArgumentOutOfRangeException(nameof(fader), fader, "Not a fader index of the SC-2 whitelist (0x00, 0x30, 0x31).");
        }

        return ConsoleEvent(sequence, new[] { FaderKind, (byte)fader, value, previous });
    }

    /// <summary>
    /// ConsoleEvent kind 1, a key event: `[seq] 03 00 14 01 [code] [01 press / 00 release]` (F131, F133). A key is
    /// always sent as a press followed by its release (SC-2 condition 4; <see cref="HspReadSession"/> sees to it).
    /// SmartSoft's bump 7, E9 seq 19055 / 19436: `56 03 00 14 01 06 01`, `57 03 00 14 01 06 00`.
    /// </summary>
    public static byte[] KeyEvent(byte sequence, ClearedKey key, bool pressed)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key code of the SC-2 whitelist (0x00, 0x06, 0x08, 0x31, 0x33, 0x34, 0x36, 0x39).");
        }

        return ConsoleEvent(sequence, new[] { KeyKind, (byte)key, pressed ? (byte)0x01 : (byte)0x00 });
    }

    /// <summary>
    /// True for exactly the payloads <see cref="FaderEvent"/> and <see cref="KeyEvent"/> produce, whatever the
    /// sequence byte: a ConsoleEvent with a whitelisted index or code (and a press or release state). False for every
    /// other payload SmartSoft was seen to send (the System pair, ChannelText, Memory, record keys, indices 0x04 / 0x06,
    /// the other codes).
    /// </summary>
    public static bool IsClearedConsoleEvent(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HspOutboundPayload.HeaderSize + 1
            || payload[3] != ConsoleEventId
            || BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]) != payload.Length - HspOutboundPayload.HeaderSize)
        {
            return false;
        }

        return payload[4] switch
        {
            FaderKind => payload.Length == FaderEventLength && Enum.IsDefined((ClearedFader)payload[5]),
            KeyKind => payload.Length == KeyEventLength && Enum.IsDefined((ClearedKey)payload[5]) && payload[6] <= 0x01,
            _ => false,
        };
    }

    /// <summary>`[seq][u16 LE body length][0x14][body]`, the outbound payload layout (F54 for SmartSoft's writes, F131).</summary>
    private static byte[] ConsoleEvent(byte sequence, byte[] body)
    {
        var payload = new byte[HspOutboundPayload.HeaderSize + body.Length];
        payload[0] = sequence;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)body.Length);
        payload[3] = ConsoleEventId;
        body.CopyTo(payload, HspOutboundPayload.HeaderSize);
        return payload;
    }

    private static byte[] Header(ushort w0, ushort w1, ushort w2, ushort w3, ushort w4, ushort w5)
    {
        var bytes = new byte[HspHeader.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, w0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), w1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), w2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), w3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), w4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), w5);
        return bytes;
    }
}
