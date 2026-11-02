using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>
/// The fader indices of ConsoleEvent kind 0 whitelisted by safety check SC-2 (2026-10-31, class A: 0x00, 0x30, 0x31) and
/// SC-5 with its addendum (2026-11-02, classes A2, A3, A4: every physical fader 0x00–0x17), and no other: every index
/// outside these 26 is unrepresentable here, and <see cref="HspOutbound"/> refuses a value cast into this type (SC-2
/// condition 1, SC-5 condition 3). <b>An index names the physical fader, not a channel or a memory</b>: the console
/// applies it to what that fader controls in the bank it reports (F171 VERIFIED: every index in bank 1-24 in E9c S1 and
/// both E9d passes (part A), in bank 25-48 in both E9d passes (part B), as a memory fader of the shown page in both E9d
/// passes (part C); H111 CONFIRMED). No index is computed from a fader number: <see cref="PhysicalControls.Fader"/> is an
/// explicit table of the 24 rows F171 found. VERIFIED (E9 to E9e) is no clearance by itself: SC-5's GO is conditional, and
/// nothing is sent before the reviewer passes the implementation and the owner gives the go for E11b.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F131, F132, F134, F171", "SC-2 class A (0x00, 0x30, 0x31) and SC-5 classes A2-A4 (0x00-0x17, the physical faders in every bank and mode)")]
public enum ClearedFader : byte
{
    /// <summary>Index 0x00, physical fader 1: channel 1 in bank 1-24 (E9, E9b, E11, E9c S4 / S8, E9d A; F132, F134, F167), channel 25 in bank 25-48 (E9c S2, E9d B), memory fader 1 of the shown page (E9c S3 pages 1 and 2, E9d C; F112).</summary>
    Fader1 = 0x00,

    /// <summary>Index 0x01, physical fader 2: channel 2 (E9c S1, E9d A), channel 26 (E9c S2, E9d B), memory fader 2 (E9c S3, E9d C).</summary>
    Fader2 = 0x01,

    /// <summary>Index 0x02, physical fader 3: channel 3 (E9c S1, E9d A), channel 27 (E9d B), memory fader 3 (E9c S4c, E9d C).</summary>
    Fader3 = 0x02,

    /// <summary>Index 0x03, physical fader 4: channel 4 (E9c S1, E9d A), channel 28 (E9d B), memory fader 4 (E9d C).</summary>
    Fader4 = 0x03,

    /// <summary>Index 0x04, physical fader 5: channel 5 (E9b, E9c S1, E9d A), channel 29 (E9d B), memory fader 5 (E9d C).</summary>
    Fader5 = 0x04,

    /// <summary>Index 0x05, physical fader 6: channel 6 (E9c S1, E9d A), channel 30 (E9d B), memory fader 6 (E9d C).</summary>
    Fader6 = 0x05,

    /// <summary>Index 0x06, physical fader 7: channel 7 (E9 inside and outside EDIT MEM, E9c S1, E9d A), channel 31 (E9d B), memory fader 7 (E9d C).</summary>
    Fader7 = 0x06,

    /// <summary>Index 0x07, physical fader 8: channel 8 (E9c S1, E9d A), channel 32 (E9d B), memory fader 8 (E9d C).</summary>
    Fader8 = 0x07,

    /// <summary>Index 0x08, physical fader 9: channel 9 (E9c S1, E9d A), channel 33 (E9d B), memory fader 9 (E9c S3, E9d C).</summary>
    Fader9 = 0x08,

    /// <summary>Index 0x09, physical fader 10: channel 10 (E9c S1, E9d A), channel 34 (E9d B), memory fader 10 (E9d C).</summary>
    Fader10 = 0x09,

    /// <summary>Index 0x0A, physical fader 11: channel 11 (E9c S1, E9d A), channel 35 (E9d B), memory fader 11 (E9d C).</summary>
    Fader11 = 0x0A,

    /// <summary>Index 0x0B, physical fader 12: channel 12 (E9c S1, E9d A), channel 36 (E9d B), memory fader 12 (E9d C).</summary>
    Fader12 = 0x0B,

    /// <summary>Index 0x0C, physical fader 13: channel 13 (E9c S1, E9d A), channel 37 (E9d B), memory fader 13 (E9d C).</summary>
    Fader13 = 0x0C,

    /// <summary>Index 0x0D, physical fader 14: channel 14 (E9c S1, E9d A), channel 38 (E9d B), memory fader 14 (E9d C).</summary>
    Fader14 = 0x0D,

    /// <summary>Index 0x0E, physical fader 15: channel 15 (E9c S1, E9d A), channel 39 (E9d B), memory fader 15 (E9d C).</summary>
    Fader15 = 0x0E,

    /// <summary>Index 0x0F, physical fader 16: channel 16 (E9c S1, E9d A), channel 40 (E9d B), memory fader 16 (E9d C).</summary>
    Fader16 = 0x0F,

    /// <summary>Index 0x10, physical fader 17: channel 17 (E9c S1, E9d A), channel 41 (E9d B), memory fader 17 (E9d C).</summary>
    Fader17 = 0x10,

    /// <summary>Index 0x11, physical fader 18: channel 18 (E9c S1, E9d A), channel 42 (E9d B), memory fader 18 (E9d C).</summary>
    Fader18 = 0x11,

    /// <summary>Index 0x12, physical fader 19: channel 19 (E9c S1, E9d A), channel 43 (E9d B), memory fader 19 (E9d C).</summary>
    Fader19 = 0x12,

    /// <summary>Index 0x13, physical fader 20: channel 20 (E9c S1, E9d A), channel 44 (E9d B), memory fader 20 (E9d C).</summary>
    Fader20 = 0x13,

    /// <summary>Index 0x14, physical fader 21: channel 21 (E9c S1, E9d A), channel 45 (E9d B), memory fader 21 (E9d C).</summary>
    Fader21 = 0x14,

    /// <summary>Index 0x15, physical fader 22: channel 22 (E9c S1, E9d A), channel 46 (E9d B), memory fader 22 (E9d C).</summary>
    Fader22 = 0x15,

    /// <summary>Index 0x16, physical fader 23: channel 23 (E9c S1, E9d A), channel 47 (E9d B; unpatched in this show, no DMX), memory fader 23 (E9d C).</summary>
    Fader23 = 0x16,

    /// <summary>Index 0x17, physical fader 24: channel 24 (E9c S1 and S2, E9d A; DMX 24 and 97 in this patch), channel 48 (E9d B), memory fader 24 (E9d C).</summary>
    Fader24 = 0x17,

    /// <summary>Index 0x30, the MASTER: FaderInfo body[194] followed (F132).</summary>
    Master = 0x30,

    /// <summary>Index 0x31, BUMPS: FaderInfo body[193] followed (F132).</summary>
    Bumps = 0x31,
}

/// <summary>
/// The key codes of ConsoleEvent kind 1 whitelisted by SC-2 (2026-10-31, class B1) and SC-5 with its two addenda
/// (2026-11-02, classes B3, B4, B5), and no other. A bump code names the physical bump (F174, SC-5 addendum 2): it is
/// sent as a press and a release from one code path, a tap or a hold bounded to 3 s (SC-5 condition 2); every other key
/// is a tap. NO-GO and absent: the record keys (REC MEM 0x41, EDIT MEM 0x42, COPY 0x3E; SC-3), CLEAR (0x38), STACK (0x3B),
/// PAUSE (0x32), NEXT (0x3A). No code is computed from a bump number (<see cref="PhysicalControls.Bump"/> is an explicit
/// table) or from an LED block number (H82 stays HYPOTHESIS); the LED block named for each code is the one that changed at
/// its presses (F133).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F131, F133, F134, F174, F175", "SC-2 class B1 and SC-5 classes B3 (the 24 bumps), B4 (FADERS 25-48), B5 (IND 1 / IND 2); the channel bumps other than 1, 2, 7, 10, 12, 24 and bank 25-48's 1 and 24 on the physical-control rule (SC-5 addendum 2, INFERRED for channel mode)")]
public enum ClearedKey : byte
{
    /// <summary>Code 0x00, physical bump 1, LED block 1: memory bump 1 (E9, E9b, E9c S3 / S5, E9d C; F134, F174), channel bump 1 (E9c S2, held 2.54 s, once), channel 25's bump in bank 25-48 (E9d B, both passes).</summary>
    Bump1 = 0x00,

    /// <summary>Code 0x01, physical bump 2: memory bump 2 (E9d C), channel bump 2 (E9e, both sessions).</summary>
    Bump2 = 0x01,

    /// <summary>Code 0x02, physical bump 3: memory bump 3 (E9d C); as a channel bump on the physical-control rule (SC-5 addendum 2).</summary>
    Bump3 = 0x02,

    /// <summary>Code 0x03, physical bump 4: memory bump 4 (E9d C); channel bump by the addendum's rule.</summary>
    Bump4 = 0x03,

    /// <summary>Code 0x04, physical bump 5: memory bump 5 (E9d C); channel bump by the addendum's rule.</summary>
    Bump5 = 0x04,

    /// <summary>Code 0x05, physical bump 6: memory bump 6 (E9d C); channel bump by the addendum's rule.</summary>
    Bump6 = 0x05,

    /// <summary>Code 0x06, physical bump 7, LED block 7: channel bump 7 (E9, E9b, E9c S5, E9d D held 4.67 s and 3.42 s; F174), memory bump 7 (E9d C).</summary>
    Bump7 = 0x06,

    /// <summary>Code 0x07, physical bump 8: memory bump 8 (E9d C); channel bump by the addendum's rule.</summary>
    Bump8 = 0x07,

    /// <summary>Code 0x08, physical bump 9, LED block 9: memory bump 9 (E9c S3, E9d C); from a PC in channel mode only inside record functions (E9, E9b), so channel bump 9 rests on the addendum's rule.</summary>
    Bump9 = 0x08,

    /// <summary>Code 0x09, physical bump 10: channel bump 10 (E9 once in channel mode inside EDIT MEM, header seq 95535; E9c S5 held 2.67 s), memory bump 10 (E9 once in memory mode inside COPY, header seq 84654; E9d C).</summary>
    Bump10 = 0x09,

    /// <summary>Code 0x0A, physical bump 11: memory bump 11 (E9d C); channel bump by the addendum's rule.</summary>
    Bump11 = 0x0A,

    /// <summary>Code 0x0B, physical bump 12: memory bump 12 (E9d C), channel bump 12 (E9e, both sessions).</summary>
    Bump12 = 0x0B,

    /// <summary>Code 0x0C, physical bump 13: memory bump 13 (E9d C); channel bump by the addendum's rule.</summary>
    Bump13 = 0x0C,

    /// <summary>Code 0x0D, physical bump 14: memory bump 14 (E9d C); channel bump by the addendum's rule.</summary>
    Bump14 = 0x0D,

    /// <summary>Code 0x0E, physical bump 15: memory bump 15 (E9d C); channel bump by the addendum's rule.</summary>
    Bump15 = 0x0E,

    /// <summary>Code 0x0F, physical bump 16: memory bump 16 (E9d C); channel bump by the addendum's rule.</summary>
    Bump16 = 0x0F,

    /// <summary>Code 0x10, physical bump 17: memory bump 17 (E9d C); channel bump by the addendum's rule.</summary>
    Bump17 = 0x10,

    /// <summary>Code 0x11, physical bump 18: memory bump 18 (E9d C); channel bump by the addendum's rule.</summary>
    Bump18 = 0x11,

    /// <summary>Code 0x12, physical bump 19: memory bump 19 (E9d C); channel bump by the addendum's rule.</summary>
    Bump19 = 0x12,

    /// <summary>Code 0x13, physical bump 20: memory bump 20 (E9d C); channel bump by the addendum's rule.</summary>
    Bump20 = 0x13,

    /// <summary>Code 0x14, physical bump 21: memory bump 21 (E9d C); channel bump by the addendum's rule.</summary>
    Bump21 = 0x14,

    /// <summary>Code 0x15, physical bump 22: memory bump 22 (E9d C); channel bump by the addendum's rule.</summary>
    Bump22 = 0x15,

    /// <summary>Code 0x16, physical bump 23: memory bump 23 (E9d C); channel bump by the addendum's rule.</summary>
    Bump23 = 0x16,

    /// <summary>Code 0x17, physical bump 24: memory bump 24 (E9d C), channel bump 24 and channel 48's bump in bank 25-48 (E9e, both sessions).</summary>
    Bump24 = 0x17,

    /// <summary>Code 0x31, GO, LED block 50 (seen from the PC only with STACK unlit, F134).</summary>
    Go = 0x31,

    /// <summary>Code 0x33, SOLO, LED block 52.</summary>
    Solo = 0x33,

    /// <summary>Code 0x34, BLACKOUT, LED block 53.</summary>
    Blackout = 0x34,

    /// <summary>Code 0x36, FADERS 1-24, LED block 55.</summary>
    Faders1To24 = 0x36,

    /// <summary>Code 0x37, FADERS 25-48, LED block 56: ConsoleStatus body[4] 0 → 1 (F98, F175; E9b, E9c S2, E9d B, E9e; SC-5 class B4).</summary>
    Faders25To48 = 0x37,

    /// <summary>Code 0x39, FADERS MEMS, LED block 58.</summary>
    FadersMems = 0x39,

    /// <summary>Code 0x47, IND 1, LED block 72: Independents record 1 byte 0 toggles (F115, F175; E9b, E9c S6; SC-5 class B5).</summary>
    Ind1 = 0x47,

    /// <summary>Code 0x48, IND 2, LED block 73: Independents record 2 byte 0 toggles (F115, F175; E9b, E9c S6; SC-5 class B5).</summary>
    Ind2 = 0x48,
}

/// <summary>
/// The System (id 39) bodies whitelisted by safety check SC-4 (2026-11-02, evening, class E): SmartSoft's start-up pair,
/// verbatim, and nothing else. Each member is the body's first byte a of `[a][00][00]` (b = c = 0, F181) and stands for one
/// exact payload (<see cref="HspOutbound.SystemMessage"/>): `00 03 00 27 09 00 00` and `01 03 00 27 07 00 00`, pseq 0 and 1,
/// the session's first two data writes, as in all 20 SmartSoft sessions of the ten captures E2 to E9e (F181, VERIFIED as
/// SmartSoft's behaviour: 40 of 40 payloads byte-identical). Every other System value SmartSoft's code can put on the wire
/// is unrepresentable here: the show-upload framing 0, 1, 6 and the erase commands 11, 12, 13, 14, 16 (F121: never), the
/// page select 10 (no clearance) (S11.17 row 7; commands.md "The System message (ID 39)"): a wrong first body byte would
/// select one of them, so the encoder knows these two bytes only. What 0x09 and 0x07 mean to the console's firmware is
/// UNKNOWN beyond the listings that follow them (F182; the causal link INFERRED, H30); ConsoleLink (evidence class 5) calls
/// them "version query" and "catalog request". SC-4 is a conditional GO: nothing is sent before the reviewer's pass and the
/// owner's go for E13, and then only behind `Device:Usb:StartupPairDate`.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F181, F182", "SC-4 class E: SmartSoft's start-up pair, 40 of 40 payloads byte-identical in 20 sessions, and the console's answers to it (the ConsoleVersion listing, the download) as SmartSoft's and the console's behaviour; the meaning of the two bodies UNKNOWN (S11.17, H30); nothing sent before SC-4's review and E13")]
public enum ClearedSystemMessage : byte
{
    /// <summary>
    /// Body `09 00 00`, payload `00 03 00 27 09 00 00` (pseq 0): SmartSoft's first data write of every session, after reply
    /// header #40 (F181; S11.17 rows 1–2: countdown 1, `0x10018232`). The console lists ConsoleVersion at the next free reply,
    /// 16.2–21.9 ms after it, and delivers `0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33` ("3.0.2.9.0.43") when fetched (F182 a,
    /// VERIFIED, 20 of 20; F59). E2 seq 105 / 106 is SmartSoft's first one on the bus.
    /// </summary>
    ListVersion = 0x09,

    /// <summary>
    /// Body `07 00 00`, payload `01 03 00 27 07 00 00` (pseq 1): SmartSoft's second data write, after reply header #79 and only
    /// when the received ConsoleVersion equals its own "3.0.2.9.0.43" (F181; S11.17 rows 2–3, `0x10018269`, INFERRED high). The
    /// console lists its whole state and show at the next free reply: 157–161 records in a fixed order, payloads of 792–812
    /// bytes, fetched in 7.2–7.6 s, no other list and no write during it (F182 b–e, VERIFIED, 20 of 20). E2 seq 188 / 189.
    /// </summary>
    ListShow = 0x07,
}

/// <summary>
/// The physical faders and bumps by number, 1–24, as the whitelist names them: one explicit row per physical control,
/// the table F171 and F174 found (index and code 0x00–0x17 = fader and bump 1–24 in every bank and mode; VERIFIED for
/// every fader in three banks and every bump in memory mode, the channel bumps on SC-5 addendum 2's rule). Nothing here
/// computes an index or a code from a number; a number outside 1–24 has no row and is refused.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F171, F174", "the 24 rows observed in E9c, E9d and E9e; the channel bumps not pressed from a PC rest on SC-5 addendum 2 (INFERRED)")]
public static class PhysicalControls
{
    /// <summary>The fader index of physical fader <paramref name="physicalFader"/> (1–24).</summary>
    public static ClearedFader Fader(int physicalFader) => physicalFader switch
    {
        1 => ClearedFader.Fader1,
        2 => ClearedFader.Fader2,
        3 => ClearedFader.Fader3,
        4 => ClearedFader.Fader4,
        5 => ClearedFader.Fader5,
        6 => ClearedFader.Fader6,
        7 => ClearedFader.Fader7,
        8 => ClearedFader.Fader8,
        9 => ClearedFader.Fader9,
        10 => ClearedFader.Fader10,
        11 => ClearedFader.Fader11,
        12 => ClearedFader.Fader12,
        13 => ClearedFader.Fader13,
        14 => ClearedFader.Fader14,
        15 => ClearedFader.Fader15,
        16 => ClearedFader.Fader16,
        17 => ClearedFader.Fader17,
        18 => ClearedFader.Fader18,
        19 => ClearedFader.Fader19,
        20 => ClearedFader.Fader20,
        21 => ClearedFader.Fader21,
        22 => ClearedFader.Fader22,
        23 => ClearedFader.Fader23,
        24 => ClearedFader.Fader24,
        _ => throw new ArgumentOutOfRangeException(nameof(physicalFader), physicalFader, "The 1248 has physical faders 1-24 (F171)."),
    };

    /// <summary>The key code of physical bump <paramref name="physicalBump"/> (1–24), the bump under that fader.</summary>
    public static ClearedKey Bump(int physicalBump) => physicalBump switch
    {
        1 => ClearedKey.Bump1,
        2 => ClearedKey.Bump2,
        3 => ClearedKey.Bump3,
        4 => ClearedKey.Bump4,
        5 => ClearedKey.Bump5,
        6 => ClearedKey.Bump6,
        7 => ClearedKey.Bump7,
        8 => ClearedKey.Bump8,
        9 => ClearedKey.Bump9,
        10 => ClearedKey.Bump10,
        11 => ClearedKey.Bump11,
        12 => ClearedKey.Bump12,
        13 => ClearedKey.Bump13,
        14 => ClearedKey.Bump14,
        15 => ClearedKey.Bump15,
        16 => ClearedKey.Bump16,
        17 => ClearedKey.Bump17,
        18 => ClearedKey.Bump18,
        19 => ClearedKey.Bump19,
        20 => ClearedKey.Bump20,
        21 => ClearedKey.Bump21,
        22 => ClearedKey.Bump22,
        23 => ClearedKey.Bump23,
        24 => ClearedKey.Bump24,
        _ => throw new ArgumentOutOfRangeException(nameof(physicalBump), physicalBump, "The 1248 has physical bumps 1-24 (F174)."),
    };

    /// <summary>True for the 24 bump codes of the table above (a key held for up to 3 s, SC-5 class B3); false for the other keys, which are taps.</summary>
    public static bool IsBump(ClearedKey key) => BumpCodes.Contains(key);

    private static readonly IReadOnlySet<ClearedKey> BumpCodes = Enumerable.Range(1, 24).Select(Bump).ToHashSet();
}

/// <summary>
/// Every PC → console transfer our software can produce, byte for byte as SmartSoft sends it, and nothing else.
/// <list type="bullet">
/// <item>The poll and the fetch (VERIFIED, F52: 38 517 polls and 1 651 fetches in E2, E3 and E3b, the stale-word rule
/// re-checked on 2026-10-30 with `tools/protocol-analyzer/poll_rules.py`), cleared by safety check SC-1
/// (docs/protocol/safety-checks.md) for a read-only session: a fetch only for a record that the console itself listed
/// in a DirtyObjects list of the same session, in list order, once.</item>
/// <item>The Data header and the two ConsoleEvent payloads, fader event (kind 0) and key event (kind 1) (VERIFIED as
/// SmartSoft's behaviour, F131, F132, F133: every write of E9 to E9e follows the framing), built only from the typed
/// whitelist <see cref="ClearedFader"/> / <see cref="ClearedKey"/> (SC-2 condition 1, SC-5 condition 3). SC-2 (2026-10-31)
/// and SC-5 (2026-11-02) are conditional GOs for building and reviewing this encoder: they clear no byte for sending until
/// their conditions are met and the owner gives the go for the session (E11, E11b), and only a session created with a
/// write gate (live control, behind `Device:Usb:LiveControlDate`) ever asks for these.</item>
/// <item>SmartSoft's start-up pair, System `09 00 00` then `07 00 00`, as the two exact payloads of F181 (SC-4, 2026-11-02,
/// evening, class E: a conditional GO to build and review; nothing sent before the review and the owner's go for E13), built
/// only from <see cref="ClearedSystemMessage"/>; only a session created with the pair's gate (behind
/// `Device:Usb:StartupPairDate`) asks for them, once, as its first two data writes.</item>
/// </list>
/// No other System value (the upload framing 0 / 1 / 6, the page select 10, the erase commands 11–16), no object write
/// (ChannelText, Memory), no record key, no CLEAR, STACK, PAUSE or NEXT and no index or code outside the whitelist can be
/// encoded here (SC-2 condition 8, SC-5 condition 6, SC-4 clause 6; classes B2, C, D NO-GO, F never).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F52, F53, F57", "cleared by SC-1 (2026-10-30) for a read-only session: poll and listed fetch")]
[Evidence(EvidenceStatus.Verified, "F131, F132, F133, F171, F174, F175", "SC-2 (2026-10-31) classes A and B1, SC-5 (2026-11-02) classes A2-A4, B3-B5: conditional GO to build and review; nothing sent before the review and the owner's go")]
[Evidence(EvidenceStatus.Verified, "F181, F182", "SC-4 (2026-11-02, evening) class E: SmartSoft's start-up pair verbatim, once per session; conditional GO to build and review; nothing sent before the review and the owner's go for E13")]
public static class HspOutbound
{
    /// <summary>Object id of System (F58, F181; id → name S11.6).</summary>
    private const byte SystemId = (byte)HspObjectId.System;

    /// <summary>The System body's length: `[a][b][c]`, 3 bytes (F181; S11.17 row 9).</summary>
    private const int SystemBodyLength = 3;

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
    /// The Data header of a whitelisted write: w0 = 1, w1 = the payload's length, w2..w5 = the stale words, the same words a
    /// poll would carry at that point (F52; every Data header of E9 to E9e, and SmartSoft's 40 start-up headers, F181: w2 = 0
    /// in 36 and 0x11, the last reply header's PlaybackInfo id, in 4, answered alike). Produced only for a payload that
    /// <see cref="IsClearedConsoleEvent"/> or <see cref="IsClearedSystemMessage"/> accepts, so no Data header can announce
    /// anything else. SmartSoft's first ConsoleEvent header, E9 seq 9664: `01 00 08 00 00 00 00 00 00 00 00 00`; its first
    /// System header, E2 seq 105: `01 00 07 00 00 00 00 00 00 00 00 00`.
    /// </summary>
    public static byte[] DataHeader(ReadOnlySpan<byte> payload, ushort w2, ushort w3, ushort w4, ushort w5)
    {
        if (!IsClearedConsoleEvent(payload) && !IsClearedSystemMessage(payload))
        {
            throw new ArgumentException(
                $"A Data header is produced only for a whitelisted ConsoleEvent payload (SC-2 condition 1, SC-5 condition 3) or one of SC-4's two System payloads, not for {Convert.ToHexString(payload)}.",
                nameof(payload));
        }

        return Header((ushort)HspMessageType.Data, (ushort)payload.Length, w2, w3, w4, w5);
    }

    /// <summary>
    /// One of SmartSoft's two start-up System payloads, byte for byte (F181): `00 03 00 27 09 00 00` for
    /// <see cref="ClearedSystemMessage.ListVersion"/>, `01 03 00 27 07 00 00` for <see cref="ClearedSystemMessage.ListShow"/>
    /// (`[pseq][u16 LE 3][0x27][a][00][00]`, F54, S11.17 row 9). <paramref name="sequence"/> is the session's counter (F131:
    /// 0x00 at its first data payload, +1 per payload) and must be the pseq SmartSoft's pair always had, 0 for `09` and 1 for
    /// `07`: the pair is the session's first two data writes or it is not sent (SC-4 clauses 1 and 6). Any other value, or a
    /// member cast from another byte, is refused: the upload, page-select and erase values (S11.17 row 7) are unrepresentable.
    /// </summary>
    public static byte[] SystemMessage(byte sequence, ClearedSystemMessage message)
    {
        var pseq = message switch
        {
            ClearedSystemMessage.ListVersion => (byte)0x00,
            ClearedSystemMessage.ListShow => (byte)0x01,
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Not one of SC-4's two System bodies (09 00 00, 07 00 00); every other System value is unrepresentable (S11.17 row 7, F121)."),
        };

        if (sequence != pseq)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"System {(byte)message:X2} 00 00 goes with pseq {pseq}, as the session's {(pseq == 0 ? "first" : "second")} data write (F181: 40 of 40 payloads); the pair is first or not at all (SC-4 clause 6).");
        }

        var payload = new byte[HspOutboundPayload.HeaderSize + SystemBodyLength];
        payload[0] = pseq;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), SystemBodyLength);
        payload[3] = SystemId;
        payload[4] = (byte)message;
        return payload;   // body bytes b and c stay 0x00 (F181)
    }

    /// <summary>
    /// True for exactly the two payloads <see cref="SystemMessage"/> produces, pseq included (`00 03 00 27 09 00 00`,
    /// `01 03 00 27 07 00 00`, F181), false for every other payload: another first body byte (the upload framing, the page
    /// select, an erase), a b or c other than 0, another pseq, another length or id.
    /// </summary>
    public static bool IsClearedSystemMessage(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != HspOutboundPayload.HeaderSize + SystemBodyLength
            || BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]) != SystemBodyLength
            || payload[3] != SystemId
            || payload[5] != 0x00
            || payload[6] != 0x00)
        {
            return false;
        }

        return (payload[4], payload[0]) switch
        {
            ((byte)ClearedSystemMessage.ListVersion, 0x00) => true,
            ((byte)ClearedSystemMessage.ListShow, 0x01) => true,
            _ => false,
        };
    }

    /// <summary>
    /// ConsoleEvent kind 0, a fader event: `[seq] 04 00 14 00 [index] [value] [previous]` (F131, F132). The value is the
    /// console's level byte, unconverted; <paramref name="previous"/> is this host's own last value of that index, 0x00
    /// for its first event of the session, never a value read from the console (SC-2 condition 3; across a bank switch
    /// too, SC-5 condition 4).
    /// SmartSoft's first one, E9 seq 9665: `02 04 00 14 00 00 05 00`.
    /// </summary>
    public static byte[] FaderEvent(byte sequence, ClearedFader fader, byte value, byte previous)
    {
        if (!Enum.IsDefined(fader))
        {
            throw new ArgumentOutOfRangeException(nameof(fader), fader, "Not a fader index of the SC-2 / SC-5 whitelist (0x00-0x17, 0x30, 0x31).");
        }

        return ConsoleEvent(sequence, new[] { FaderKind, (byte)fader, value, previous });
    }

    /// <summary>
    /// ConsoleEvent kind 1, a key event: `[seq] 03 00 14 01 [code] [01 press / 00 release]` (F131, F133). A key is
    /// always sent as a press followed by its release, a tap or a bounded hold (SC-2 condition 4, SC-5 condition 2;
    /// <see cref="HspReadSession"/> sees to it).
    /// SmartSoft's bump 7, E9 seq 19055 / 19436: `56 03 00 14 01 06 01`, `57 03 00 14 01 06 00`.
    /// </summary>
    public static byte[] KeyEvent(byte sequence, ClearedKey key, bool pressed)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key code of the SC-2 / SC-5 whitelist (0x00-0x17, 0x31, 0x33, 0x34, 0x36, 0x37, 0x39, 0x47, 0x48).");
        }

        return ConsoleEvent(sequence, new[] { KeyKind, (byte)key, pressed ? (byte)0x01 : (byte)0x00 });
    }

    /// <summary>
    /// True for exactly the payloads <see cref="FaderEvent"/> and <see cref="KeyEvent"/> produce, whatever the
    /// sequence byte: a ConsoleEvent with a whitelisted index or code (and a press or release state). False for every
    /// other payload SmartSoft was seen to send (the System pair, ChannelText, Memory, the step commands, the record keys,
    /// CLEAR, STACK, PAUSE, NEXT).
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
