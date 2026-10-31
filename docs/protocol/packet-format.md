# Packet format (High-Speed-Pipe, interface 1)

Created 2026-10-28, the day the first USB capture (E2) existed. **Everything here is OBSERVED
at most** (one SmartSoft session, one PC, firmware 3.0.2.9.0.43, SmartSoft 3.0.43); nothing is
VERIFIED, so no code that can talk to a real console may use it yet *(2026-10-28 wording; F51–F57 are
VERIFIED since E3 / E3b, and SC-1 cleared the poll and the listed fetch on 2026-10-30, see the update at
the end of this section)*. Evidence: facts F51–F57,
[E2 record](captures/2026-10-28-e2-smartsoft-connect.md), `captures/2026-10-28-e2-smartsoft-connect.pcap`
and `.jsonl`. Field names (w0..w5, seq, id, body, a / b / c) are our labels from
[smartsoft-analysis.md](smartsoft-analysis.md) S11, not ETC's.

**Update 2026-10-29 (E3,** [record](captures/2026-10-29-e3-console-actions.md)**).** A second SmartSoft
session, after a console power cycle (USB address 7 instead of 10), reproduced the framing below:
F51–F54, F57–F65 are now **VERIFIED**, F55 in part (the start value of the console's counter
differed between the two sessions); the sections below say which parts. VERIFIED means that the console and SmartSoft
were seen to behave this way in two sessions; **it does not clear any message for sending**
(safety rule 2): no code may send anything to a console yet. Evidence added:
`captures/2026-10-29-e3-console-actions.pcap` and `.jsonl`. "CL-ML" marks a claim of the third-party
ConsoleLink program about the SmartFade **ML** (evidence class 5, [consolelink-analysis.md](consolelink-analysis.md)),
never a 1248 fact. The sentence "nothing is VERIFIED" above is the 2026-10-28 wording, kept.

**Update 2026-10-30 (E3b,** [record](captures/2026-10-30-e3b-live-objects.md)**).** Three more SmartSoft
sessions (one capture, another console power cycle before the first, none between them) reproduced the
framing again: 25 824 polls, 1 000 fetches, 268 DirtyObjects lists, 1 268 payloads, 0 framing errors, all
URBs with status 0 (F51–F54, F57, F65: third reproduction). New: the console's sequence counter is **not
reset by a new SmartSoft session** (F93), and F80 (start value 0x00 after a power cycle) is VERIFIED in
part. VERIFIED still does not clear any message for sending: no code may send anything to a console yet.

**Update 2026-10-30, later the same day (SC-1).** Safety check SC-1 ([safety-checks.md](safety-checks.md))
cleared the poll and the fetch of a listed record, byte for byte as described below, for a read-only session of
our own software; the three "no code may send anything" sentences above are superseded for those two messages
only. The stale-word rule was re-counted for it (38 517 of 38 517 polls, `tools/protocol-analyzer/poll_rules.py`).

**Update 2026-10-30, E8 (**[record](captures/2026-10-30-e8-first-read-only-session.md)**).** Our own read-only session
(a second host program on the same PC; polls and listed fetches built by our encoder; 861 s) reproduced the framing from
the host side: 20 763 polls and 2 925 fetches with w0 in {0, 2} only and the stale words of F52 (20 762 of 20 762 after the
first poll), every poll answered by one 12-byte header (type-3 echo 2 925 of 2 925), 2 470 lists of payload 5 + 2 + 5n, and
5 395 payloads with byte 1 = 0x00 and the length word = payload length − 5, 0 framing errors (F51–F55, F57, F110): the first
evidence channel other than SmartSoft, with no status change. Our code sent no data header, so the outbound payload layout was
not re-tested, and without USBPcap the bus view (packets, USBD status, EP0) is missing (H68, H75).

**Update 2026-10-30, E8b (**[record](captures/2026-10-30-e8b-second-read-only-session.md)**).** A second session of the same host
program (1 121 s, after a power cycle) reproduced the framing from the host side and, for the first time for our host, **on the bus**
(USBPcap from before the Zadig bind): 27 484 polls and 2 315 fetches with w0 in {0, 2} only and the stale words of F52 (27 483 of
27 483 after the first poll), every poll answered by one 12-byte header (type-3 echo 2 315 of 2 315), 1 514 lists of payload
5 + 2 + 5n and 3 829 payloads with byte 1 = 0x00 and the length word = payload length − 5, 0 framing errors (F51–F55, F57).
**The backend's transfer log equals the bus byte for byte** (61 112 of 61 112 transfers, USBD status 0 in all 62 957 bulk URBs, the
log's times within 0.75 ms of the bus; F122), which checks the log (`TransferLog`) but not the encoder, whose outbound bytes stay
circular as evidence for itself. **Host-side only:** under WinUSB an IN read longer than 64 bytes and not a multiple of 64 is
**two URBs** on the bus, a whole-packet part and the remainder (1 845 reads: 86, 90, 200, 456 and 519 bytes; INFERRED as WinUSB
behaviour, SmartSoft's ETC driver having read every payload in one URB in E2–E3b); the console sees IN tokens either way, so the
framing below is unchanged and the "one URB per transfer" wording of the Transport table describes the ETC driver. No zero-length
packet and no read whose length is a multiple of 64 occurred (H75 stays untested). The EP0 traffic was one burst at the driver change
and nothing at the host's open, at Connect or at Disconnect (H68 CONFIRMED, F123).

**Update 2026-10-30, late evening (E9,** [record](captures/2026-10-30-e9-smartsoft-writes.md)**).** A SmartSoft session driven through
its Console Simulator and recorded passively (our software sent nothing) is the first capture with PC **data writes beyond the System
pair**: 401 writes, each a type-1 header and one payload, for the ids 39, 20 (ConsoleEvent) and 9 (ChannelText). The framing below
held for all of them: header `01 00 [w1 LE] [w2 … w5]` with the stale words in 401 of 401, payload `[pseq][u16 LE len = w1 − 4][id][body]`,
SmartSoft's sequence byte +1 per data payload across all ids with one wrap and 0 gaps, USBD status 0, 0 framing errors; the inbound
framing held again (894 payloads, byte 1 = 0x00, four wraps, the first pseq 245 continuing E8b's 244). The write shapes are
**OBSERVED once** (F131) and **NOT CLEARED**: nothing here clears a message, no type-1 header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*, and
the messages with their exact bytes are in [commands.md](commands.md) "PC → console data writes seen in E9". The first records with
c ≠ 0 appeared in E9's DirtyObjects lists (four System records, F137).

**Update 2026-10-31 (E9b,** [record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md)**).** The repeat of E9 (three SmartSoft sessions in one passive
capture after a console power cycle; our software sent nothing) held the framing again for **427 data writes** (the ids 39, 20, 9 and now
**0, Memory**): header `01 00 [w1 LE] [w2 … w5]` with the stale words in 427 of 427, payload `[pseq][u16 LE len = w1 − 4][id][body]` of
exactly w1 bytes 0.067–0.254 ms later, w1 = 7, 8, 43 and **94** (the Memory write), the outbound counter from 0x00 in each session, +1
per data payload across all ids, one wrap (seq 39860 → 39866), 0 gaps, USBD status 0, 0 framing errors; the inbound framing held (1 604
payloads, six wraps, byte 1 = 0x00, the first pseq 0x00 after the power cycle and continuous across both restarts). The write framing is
now **VERIFIED as SmartSoft's and the console's behaviour** (F131, E9 + E9b), which is **not clearance**: nothing here clears a message,
no type-1 header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*, and the messages with their exact bytes are in [commands.md](commands.md)
"PC → console data writes seen in E9" (the section keeps its heading; it holds the E9b rows too).

**Update 2026-10-31, evening (E12,** [record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md)**).** A third capture of SmartSoft writes (three sessions after a console
power cycle, passive; our software sent nothing) held the framing for **70 data writes** of 12 ids, among them the first step
commands (32, 34, 35, 42) and the object writes 2, 4, 5, 8 and 12: header `01 00 [w1 LE] [w2 … w5]` with the stale words in 70 of
70 (and in all 98 253 poll and data headers), payload `[pseq][u16 LE len = w1 − 4][id][body]` 0.077–0.230 ms later, w1 = 7, 8, 17,
43, 89, 94, 104, 148, 518, the counter from 0x00 in each session (0–46, 0–20, 0–1), 0 gaps, USBD status 0, 0 framing errors; the
inbound framing held (1 260 payloads, byte 1 = 0x00 in all, pseq 0x00 after the power cycle, four wraps, 0 breaks). **A data payload
carries no qualifier fields**: the object's identity is in its body (F153). The new shapes are OBSERVED once (F151, F153), NOT
CLEARED, and listed with their bytes in [commands.md](commands.md) "PC → console data writes seen in E12".

**Update 2026-11-01 (E12b,** [record](captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md)**).** A fourth capture of SmartSoft writes (2026-10-31, late
evening: the repeat of E12, two sessions after another console power cycle, passive; our software sent nothing) held the framing for
**62 data writes** of 11 ids, the stale words in all 54 078 poll and data headers, the counter from 0x00 per SmartSoft session, and
the inbound framing; the shapes E12 saw once are now **VERIFIED as SmartSoft's and the console's behaviour** (F151, F153 in part), a
156-byte Sequence (w1 = 160) is new and OBSERVED once; NOT CLEARED. It also holds **the first non-zero USBD status on the console's
bulk pipes in any capture**: during the console's own SD save one reply-header read got no data, was cancelled by the host side after
111.9 ms (USBD_STATUS_CANCELED, 0 bytes, the classifier's one `in-unexpected` framing error) and was followed by a reset of both pipes
(USBPcap transfer type 0xFE, URB_FUNCTION_SYNC_RESET_PIPE_AND_CLEAR_STALL on 0x04 and 0x83); SmartSoft's next poll was answered at once,
with nothing lost, repeated or restarted (F163, H109; section "Not known" below).

## Transport

| Item | Observed | Fact |
|---|---|---|
| Interface / endpoints | Interface 1 (vendor class), bulk OUT 0x04, bulk IN 0x83, 64-byte packets. Interface 0 (HID) unused | F42, F51, F63 |
| URB granularity | One bulk URB per `WriteFile` / `ReadFile` of the driver; header and payload are separate transfers. **E8b 2026-10-30 (WinUSB, our host, on the bus):** this is the ETC driver's behaviour; under WinUSB every OUT and every IN of up to 64 bytes is one URB, but an IN longer than 64 bytes and not a multiple of 64 is two URBs (a whole-packet part and the remainder; host-side, INFERRED); header and payload are still separate transfers | F51, F122 |
| Direction rule | Strictly host-driven: every reply-header IN follows a poll OUT, and every payload IN follows its own reply header; the console never sends unsolicited | F51 |
| Byte order | Little-endian for header words and length words (OBSERVED); big-endian for numeric u16 fields inside bodies (OBSERVED); FileName characters UTF-16LE (OBSERVED); label text blocks UTF-16BE (INFERRED from structure, H40). **Update 2026-10-29:** the first three items are **VERIFIED** (E2 + E3). The label byte order is **downgraded INFERRED → HYPOTHESIS** (not rejected): two readings fit the same 38-byte block and give the same text for every label seen: **A** 18 UTF-16BE code units after `03 06`, **B** (CL-ML, class 5) one pad byte then UTF-16LE lines of 12 / 12 / 11 bytes; decided by a "č" label and a 6-character third line (consolelink-analysis.md §2.6) | F54, F60, F73, H36, H40 |

## PC → console

Every OUT transfer starts as a 12-byte **header**, six u16 LE words:

| Word | Poll (w0 = 0) | Data (w0 = 1) | Fetch (w0 = 2) |
|---|---|---|---|
| w0 | 0 | 1 | 2 |
| w1 | 0 | payload length (7 in both observed cases, E2 / E3 / E3b; E9: 7, 8 or 43, F131; E9b adds 94 for the Memory write, F143; E12 adds 8 and 7 for the step commands and 17, 43, 89, 104, 148 and 518 for the object writes, F151, F153; E12b adds 160 for a 156-byte Sequence, F153) | 0 |
| w2 | stale | stale | object ID |
| w3, w4, w5 | stale | stale | qualifiers a, b, c |

"Stale": equal to w2..w5 of the most recent fetch or reply header (the first poll: all zero).
A Data header is followed by one OUT transfer of exactly w1 bytes (F52):

`[seq][u16 LE len][id][body]`, len = w1 − 4. Example (f211): `00 03 00 27 09 00 00`.

**Data writes seen in E9 (OBSERVED, one session; NOT CLEARED; F131).** All 401 data writes of SmartSoft's E9 session have this shape:
a type-1 header `01 00 [w1 LE] [w2 … w5 stale]`, then 0.05–0.32 ms later (median 0.10 ms) one OUT transfer of exactly w1 bytes. The next
OUT transfer (a poll in 220 cases, a fetch in 181) follows 14.51–16.46 ms after the payload, and consecutive writes are at least 44.8 ms
apart (one write per loop iteration, INFERRED from smartsoft-analysis.md S11.2 step A). Nothing acknowledges a write:

| Id | Body | w1 | Writes | Example payload (SmartSoft's own bytes) |
|---|---|---|---|---|
| 39 System | 3 bytes `09 00 00` or `07 00 00` | 7 | 2 | seq 138 `00 03 00 27 09 00 00` |
| 20 ConsoleEvent, kind 0 (fader) | 4 bytes `[00][index][value][previous]` | 8 | 332 | seq 9665 `02 04 00 14 00 00 05 00` |
| 20 ConsoleEvent, kind 1 (key) | 3 bytes `[01][code][state]`, state 01 on press and 00 on release | 7 | 64 | seq 19055 `56 03 00 14 01 06 01` |
| 9 ChannelText | 39 bytes `[channel − 1][03][06][36 bytes]` | 43 | 3 | seq 57881 `1B 27 00 09 06 03 06 01 0C 00 61 00 6A` + 30 × `00` |
| 0 Memory (E9b) | 90 bytes `[page − 1][slot − 1] 01 30` + 48 level bytes + `03 06` + 36 label bytes | 94 | 0 (E9b: 1) | seq 92053 `23 5A 00 00 00 08 01 30 00 00 00 00 FF` + 43 × `00` + `03 06` + 36 × `00` |
| 32 InsertStep, 34 DeleteStep, 42 (E12) | 4 bytes `[a][b][u16 BE c]` | 8 | 0 (E12: 5, 5, 1) | seq 30121 `05 04 00 20 30 13 00 96` |
| 35 MoveStep (E12) | 3 bytes `[a][b][c]` | 7 | 0 (E12: 1) | seq 175911 `0A 03 00 23 30 11 14` |
| 4 StepContent (E12) | 13 bytes `[index][12-byte record]`, or 100 bytes with `[30][48 levels][03 06][36 bytes]` for a type-2 step | 17 / 104 | 0 (E12: 6 + 5) | seq 61504 `11 0D 00 04 13 00 96 01 00 08 00 00 32 00 32 00 00` |
| 2 Sequence (E12) | the whole object, 10 + 38 + 12 n bytes (144 for 8 steps) | 148 | 0 (E12: 13) | seq 123369 `28 90 00 02 00 00 08 01 …` |
| 8 Page (E12) | 39 bytes `[page − 1][03 06][36 bytes]` | 43 | 0 (E12: 6) | seq 137545 `2E 27 00 08 00 03 06 00 54` + 34 × `00` |
| 12 Independents (E12) | 85 bytes `[02]` + 2 × `[a][b][c][d][03 06][36]` | 89 | 0 (E12: 7) | seq 54129 `0A 55 00 0C 02 00 FF 01 01 03 06 …` |
| 5 Patch (E12) | 514 bytes `02 00` + 512 entries | 518 | 0 (E12: 4) | seq 43919 `08 02 02 05 02 00 01 02 …` |

The ConsoleEvent bodies are single bytes (no 16-bit fields); the ChannelText text block follows the byte order discussed under
"Byte order" above (UTF-16BE for SmartSoft's write, H40; the console's side is open, H84). The meaning of every index, code and byte is
in [commands.md](commands.md), not here.

*(E9b, 2026-10-31: the same shape held for 427 of 427 writes; E9b counts per id: 39 System 6, 20 ConsoleEvent 418 (kind 0: 366, kind 1: 52), 9
ChannelText 2, 0 Memory 1. The next poll or fetch followed the payload 14.69–16.23 ms later (median 15.44 ms), consecutive writes were at
least 45.55 ms apart (median 61.81 ms within a burst). The framing is VERIFIED as behaviour (F131); the Memory write's 90-byte body is
not the console's own 138-byte layout (F143). NOT CLEARED.)*

*(E12, 2026-10-31: the same shape held for 70 of 70 writes of 12 ids (39 System 6, 20 ConsoleEvent 6, 32 InsertStep 5, 34
DeleteStep 5, 35 MoveStep 1, 42 1, 4 StepContent 11, 2 Sequence 13, 0 Memory 5, 8 Page 6, 12 Independents 7, 5 Patch 4). The next
poll or fetch followed the payload 14.57–17.05 ms later, once 30.60 ms (W67, a 518-byte Patch; a skipped loop slot, INFERRED); writes
were at least 46.89 ms apart. A data payload carries no qualifier fields: the identity is in the body (Page body[0], StepContent
body[0], Sequence body[0..1], Memory body[0..1]; Patch and Independents have none). The Memory layout is VERIFIED as SmartSoft's
behaviour (F143, E9b + E12); the other new shapes are OBSERVED once. NOT CLEARED.)*

*(E12b, 2026-10-31, late evening: the same shape held for 62 of 62 writes of 11 ids (39 System 4, 20 ConsoleEvent 28 (key pairs, among
them the new code 0x32), 32 InsertStep 5, 34 DeleteStep 5, 35 MoveStep 1, 42 1, 4 StepContent 5, 2 Sequence 5 (one of 144 bytes, four
of 156 bytes with w1 = 160), 8 Page 2, 12 Independents 4, 5 Patch 2); header → payload 0.073–0.272 ms (median 0.112), the next poll or
fetch 14.65–17.07 ms after the payload, writes at least 45.71 ms apart, never two per poll. The step-command and object-write shapes of
E12 are now VERIFIED as SmartSoft's behaviour (F151; F153 in part); the 156-byte Sequence is OBSERVED once. NOT CLEARED.)*

## Console → PC

Each poll or fetch is answered by one 12-byte **reply header** (IN); if w1 > 0, the next IN
transfer is the **payload** of w1 bytes (F53):

| Word | Type 3 (object, answer to a fetch) | Type 4 (poll reply) |
|---|---|---|
| w0 | 3 | 4 |
| w1 | payload length (> 0) | 0 = nothing pending; > 0 = DirtyObjects list follows |
| w2..w5 | echo of the fetch's (id, a, b, c) | 0 |

Payload: `[seq][0x00][u16 LE len][id][body]`, len = payload length − 5 (F54). Byte 1 was 0x00 in
244 of 244 payloads; its meaning is UNKNOWN. A fetch is answered by the reply to the **next**
poll, not to the fetch itself (F53). **E3:** byte 1 = 0x00 in 510 of 510 payloads, also across the
sequence wrap (f10942 `FF 00 C3 00 0E` → f10950 `00 00 33 00 0F`), so byte 1 is not the high byte of a
16-bit counter; length word = payload length − 5 in 510 of 510. Status: VERIFIED (F53, F54, E2 + E3). **E3b:** byte 1 =
0x00 in 1 268 of 1 268 payloads across four more wraps and the length word = payload length − 5 in 1 268
of 1 268 (third reproduction).

## Sequence bytes

One 8-bit counter per direction in payload byte 0, +1 per payload, wrapping 0xFF → 0x00 (F55).
Console → PC started at 0xF6 in E2 (unexplained, H31); PC → console started at 0x00. **E3 (2026-10-29):**
after a console power cycle the console's counter started at **0x00** (f20; F80, OBSERVED once; the
start value differed from E2's, so it is not promoted); the +1 rule across type-3 and type-4 payloads (510
payloads, 0x00 … 0xFD, no break), the wrap with byte 1 staying 0x00 and the outbound 0x00, 0x01 reproduced:
**VERIFIED** (F55 in part). CL-ML (class 5) reads inbound bytes 0–1 as a 16-bit sequence and never uses
it; both wraps (E2 f88 → f218, E3 f10942 → f10950) contradict a carrying 16-bit counter.

**E3b (2026-10-30):** three SmartSoft sessions without a console power cycle between them: the console's
counter **continued** across the restarts (last pseq of session 1 = 0xA2 = 162 at f1612, first of session 2
= 0xA3 = 163 at f5556; 0x42 = 66 at f6978, then 0x43 = 67 at f10254; F93, H31 CONFIRMED), while the PC's
own counter restarted at 0x00 in every session (outbound 0x00, 0x01 per session: f183 / f349, f5549 /
f5715, f10247 / f10413). After the second power cycle the first payload (f20) again carried 0x00 (F80
VERIFIED in part). The wrap rule held four more times with byte 1 = 0x00 (f6442 → f6450, f16108 → f16116,
f30304 → f30310, f92058 → f92066); 1 268 payloads, 0 breaks. CL-ML's 16-bit reading of bytes 0–1 is
contradicted six times in all.

**E9 (2026-10-30, late evening):** SmartSoft's own counter now runs over a real write sequence: **401 data payloads, pseq 0x00 (f275)
… 0xFF (f63745), 0x00 (f63757) … 0x90 (f192149), +1 per data payload across all ids (System, ConsoleEvent, ChannelText), one wrap, 0
gaps** (OBSERVED once, F55, F131); it starts at 0x00 in each SmartSoft session. The console's counter: 894 payloads, pseq 245 (0xF5,
seq 8) … 114, 0 breaks, four wraps with byte 1 = 0x00 (f96 → f104, f33150 → f33162, f87888 → f87894, f151486 → f151494); **the first
pseq continues E8b's last (244)** across a change of host program with no power cycle, which makes F93 VERIFIED (E3b + E9). Whether the
console checks SmartSoft's outbound sequence byte is UNKNOWN (it never complained).

**E9b (2026-10-31):** the outbound counter started at 0x00 in each of the three SmartSoft sessions and rose by one per data payload across
all ids, 0 gaps: session 1 388 writes, 0x00 (seq 94, f187) … 0x83 (seq 63422, f126843) with one wrap 0xFF (seq 39860, f79719) → 0x00
(seq 39866, f79731); session 2 3 writes, 0x00 … 0x02; session 3 36 writes, 0x00 … 0x23 (seq 92053, f184105); the rule is VERIFIED as
behaviour (F55, F131). The console's counter: 1 604 payloads, **0x00 at the first payload after the power cycle** (seq 10, f20; the fourth
power-on seen, F80) … 0x43, 0 breaks, six wraps with byte 1 = 0x00 (f26416 → f26428, f57688 → f57696, f86180 → f86192, f130026 → f130034,
f141568 → f141576, f189664 → f189670), and **continuous across both SmartSoft restarts** (211 → 212 at seq 64766, 115 → 116 at seq 70153;
F93). Whether the console checks SmartSoft's outbound sequence byte is still UNKNOWN.

**E12 (2026-10-31):** the outbound counter started at 0x00 in each of the three SmartSoft sessions and rose by one per data payload
across all ids, 0 gaps (47, 21 and 2 writes: 0x00 … 0x2E, 0x00 … 0x14, 0x00 … 0x01). The console's counter: 1 260 payloads, 0x00 at
the first payload after the power cycle (seq 10, f20; the fifth power-on seen, F80), four wraps, 0 breaks, continuous across both
SmartSoft restarts (174 → 175, 75 → 76; F93).

**E12b (2026-10-31, late evening):** the outbound counter started at 0x00 in both SmartSoft sessions and rose by one per data payload
across all ids, 0 gaps (50 and 12 writes: 0x00 … 0x31, 0x00 … 0x0B). The console's counter: 699 payloads, 0x00 at the first payload
after the power cycle (seq 10, f20; the sixth power-on seen, F80), two wraps, 0 breaks, continuous across the SmartSoft restart (244 →
245, seq 85112 → 87214; F93) and across the 853.5 ms without IN data during the console's SD save (238 → 239, seq 84952 → 85013; F163).

## DirtyObjects (ID 40)

Body: `[u16 BE count]` + count × `[id][u16 BE a][b][c]` (5 bytes each). Each record is fetched
afterwards with w2 = id, w3 = a, w4 = b, w5 = c, in list order (F57). **E3:** 87 lists, all of payload
length 5 + 2 + 5n, c = 0 in every record, the 423 records are exactly the 423 fetches in order; status
VERIFIED for the format and the list → fetch correspondence (F57). CL-ML (class 5): ConsoleLink's
`[type][u16 BE][u16 LE]` equals this while c = 0 and reads the count as one byte (same values below 256). **E3b:** 268 lists, the 1 000 records are the 1 000
fetches in order, c = 0 in every record; the 157-record list is byte-identical in five sessions (F57, F94).
**E9 (2026-10-30, late evening):** 264 lists of payload 5 + 2 + 5n with a BE count (sizes 1 × 183, 2 × 33, 3 × 9, 4 × 14, 5 × 22, 6 × 1,
25 × 1 at connect, 157 × 1 for the download), **630 records = 630 fetches in order**, no re-fetch, ID 40 never fetched; the 157-record list
is byte-identical to E3b's (six sessions, F94 VERIFIED for its structure). **The first records with c ≠ 0:** four System (39) records,
(a, b, c) = (5, 5, 6) at seq 68341, (5, 5, 8) at seq 77854 and again at seq 91071, (5, 5, 9) at seq 95545, each listed when the EDIT
MEM key selected a memory (`27 00 05 05 06` in the list body at seq 68341); the record format is unchanged, so F57's "c = 0 in every
record" is limited to E2–E8b, not the format (F137, H83).
**E9b (2026-10-31):** 315 lists of payload 5 + 2 + 5n with a BE count (sizes 1 × 138, 2 × 33, 3 × 20, 4 × 51, 5 × 70, 157 × 3), **1 289
records = 1 289 fetches in order**, no re-fetch, ID 40 never fetched, and **c = 0 in all 1 289 records** (E9b selected no memory other
than memory 1 for editing; the console's own EDIT MEM listed System (5, 5, 0) with c = 0, F142); the 157-record list is byte-identical in
nine sessions (F57, F94).
**E12 (2026-10-31):** 287 lists, **973 records = 973 fetches in order** (curator's re-check), no re-fetch; the full list 157 / 161 /
157 records (F157); **two non-System records with c ≠ 0, Sequence (2, 0, 0, 6)** (list body `00 01 02 00 00 00 06`, seq 87587 and
169123), listed after SmartSoft's InsertStep and DeleteStep on KOTLIK and fetched with their qualifiers (F156); the record format is
unchanged; no de-duplication (Patch up to seven times in one list, seq 148220, F155).
**E12b (2026-10-31, late evening):** 141 lists, **558 records = 558 fetches in order**, no re-fetch; the full list 157 / 160 records (the
160-record list is an 807-byte payload; F157); **four Sequence records with c ≠ 0**, (2, 0, 0, 2) twice and (2, 0, 0, 7) twice (seq
68201, 73708, 94586, 95753), each c the index of the step SmartSoft's command changed (F156; H102 CONFIRMED in two sessions); FileName
(36) listed by the console after its own SD save, in [36, 21, 22] (seq 85013, F163); the record format unchanged.

## Timing seen from SmartSoft

Idle poll about every 31 ms, next poll at least 10 ms after a fetch (F62, H37). These are
SmartSoft's numbers, not limits proven for the console. **E3:** idle median 31.00 ms (n = 9 179), next
poll ≥ 10.35 ms after a fetch (n = 423): the qualitative cadence is VERIFIED on this PC (F62), the
figures are per-session OBSERVED values, and all of it describes SmartSoft's host-side timing, not a
console requirement and not a timing for our code to transmit with; a second PC is untested. **E3b:** idle
median 30.79 ms within sessions (n = 24 547), next poll ≥ 10.33 ms after a fetch (n = 1 000) and 14.58–15.82 ms
after the six data writes: reproduced on the same PC (F62). **E8 (our own host's cadence, not SmartSoft's):** idle poll to
next poll 31.08–52.40 ms (median 45.47), payload completion to fetch 28.01–46.58 ms (median 30.00), fetch to next poll
15.12–31.88 ms (median 15.60): all SmartSoft's cadence or slower, and the console answered every interval normally (F62
annotation, F110, H67); the SC-1 floors were never reached. **E8b (our own host's cadence again, with the bus):** idle poll to next poll
31.10–47.76 ms (median 45.66), payload completion to fetch 28.01–44.47 ms (median 30.03), fetch to next poll 15.11–31.68 ms
(median 15.56); the same ranges, the console answered every interval normally and the floors were again never reached; the backend's
times and the bus agree within 0.75 ms (F62 annotation, F122, H67). **E9 (SmartSoft again, 2026-10-30, late evening):** poll to next poll
median 31.08 ms (n = 52 456, min 29.01, max 77.37), fetch to next poll median 15.48 ms (min 10.46), payload completion to fetch median
28.72 ms (min 20.41): the cadence of E2–E3b reproduced (F62). **Writes:** data header to its payload 0.05–0.32 ms (median 0.10), payload
to the next OUT transfer 14.51–16.46 ms (median 15.43; E3b 14.58–15.82 after its six writes), consecutive writes at least 44.8 ms apart
(median 61.0 ms within a burst): SmartSoft's host-side behaviour, OBSERVED once, not a console limit and not a timing for our code
(F131). **E9b (SmartSoft again, 2026-10-31):** poll to next poll median 31.12 ms (n = 50 339, min 21.60, max 79.00), fetch to next poll median 15.48 ms (min 10.49), payload completion to fetch median 28.72 ms (min 20.39): the cadence reproduced (F62); 17 idle intervals were below 29 ms, the lowest 21.60 ms (seq 90886 → 90888) while the owner worked in SmartSoft's Mem Edit tab, closer to SC-1's floor for our sender (21 ms) than in any earlier capture, which does not change that floor (it is ours, never derived as SmartSoft's minimum). **Writes:** data header to payload 0.067–0.254 ms (median 0.118), payload to the next OUT transfer 14.69–16.23 ms (median 15.44), consecutive writes at least 45.55 ms apart (median 61.81 ms within a burst): SmartSoft's host-side behaviour, VERIFIED as behaviour (F131), not a console limit and not a timing for our code. **E12 (SmartSoft again, 2026-10-31):** idle poll intervals down to 20.446 ms (11 below 21.0 ms, 34 below 21.6 ms, at 19 moments in all three sessions; seq 122076 … 122080), below E2–E3b's 20.59 and E9b's 21.60 ms, and a fetch's completion to the next poll down to 10.255 ms (F62: ≥ 10.30 ms): SmartSoft's host-side timing, cause UNKNOWN; SC-1's floors for our sender (21 ms, 11 ms) are ours, stay above both, and nothing here changes SC-1 or SC-2. **Writes:** header to payload 0.077–0.230 ms (median 0.129), payload to the next OUT transfer 14.57–17.05 ms (median 15.79; once 30.60 ms after W67), writes at least 46.89 ms apart (OBSERVED, SmartSoft's behaviour, not a timing for our code). **E12b (SmartSoft again, 2026-10-31, late evening):** idle poll intervals down to 20.539 ms (6 below 21.0 ms) and a fetch's completion to the next poll down to 10.193 ms: SmartSoft's host-side timing again, SC-1's floors for our sender (21 ms, 11 ms) stay above both. **Writes:** header to payload 0.073–0.272 ms (median 0.112), payload to the next OUT transfer 14.65–17.07 ms (median 16.09), writes at least 45.71 ms apart; key press to release 45.71–94.49 ms (14 pairs), no write between a press and its release. **Replies:** reply-header reads took a median 1.9 ms and at most 6.6 ms at the 99.9th percentile, except two in the console's save menu: 31.8 ms (seq 83531) and the read cancelled after 111.9 ms (seq 85008), with a poll-to-poll interval of 821.3 ms around it (F163). None of this is a timing for our code; SC-1's reply timeout (500 ms) is ours.

## Not known

Meaning of inbound byte 1; what the console does with a malformed or unexpected header; behaviour
on timeout or lost payload (never occurred, F65); packet-level (64-byte) behaviour below the URB
level; whether any field differs on other firmware versions. *(Added 2026-10-29:)* the rule behind the
connect-time pending list (9 records in E2, 1 record in E3, F56, F80); why FaderInfo, DisplayContent and
PlaybackInfo are not in the 157-record list (H58); whether the console's sequence counter is reset by a
new SmartSoft session (H31); what the console does with an unsolicited fetch (never to be tried, CL-ML's
technique, consolelink-analysis.md S4). *(Update 2026-10-30:)* the counter question (H31) is answered: it is
not reset by a new SmartSoft session (F93). The connect-time list had 2 records in E3b session 1 and none in
sessions 2 and 3 (F94, H65); the rule behind it is still UNKNOWN. *(Update 2026-10-30, E8:)* a fourth history gave an
89-record connect list with repeats (F111); whether a zero-length packet follows a payload whose length is a multiple of
64 bytes, as a pending list of 37 or 101 records would be (H75); the EP0 traffic at the host's open (H68). *(Update 2026-10-30, E8b: the
EP0 question is answered, none at the host's open, F123, H68 CONFIRMED; the 64-byte question is still open, no such length occurred; USBPcap shows URBs, not packets, so
packet-level behaviour below the URB level stays unknown.)* *(Update 2026-10-30, late evening, E9:)* whether the console checks
SmartSoft's outbound sequence byte or the previous byte of a fader event (it never complained, F131, F132); whether it accepts data
writes from a host that never sent the System pair (our read-only session sends neither, H66; the answer can only be learned by
sending, which nothing clears); what it does with a key press whose release never comes (a session ending between the two is
untested); whether a page other than 1 changes the System (5, 5, c) records (H83). *(Update 2026-10-31, E9b:)* a page other than 1 does not change the System (5, 5, 0) record for memory 1 (page 12 gave the same record, F142); still open: c ≠ 0 from the console's own EDIT MEM key, what the console stored for the Memory body's channels 49–96 after a 48-level write (F143, H87), and whether the console reads the previous byte of a fader event at all (F132). *(Update 2026-10-31, E12:)* what the console does with a lost phase 2 of InsertStep or a lost id 42 (INFERRED: the default step stays, a moved step stays out of cue order); what InsertStep's c means for a sequence (H103) and id 42 beyond one renumber; whether the console lists c ≠ 0 records after its own step commands (H102, H106); how it handles a malformed or short body of the new object writes. *(Update 2026-11-01, E12b:)* "behaviour on timeout" above is no longer unseen: during the console's own SD save a pending reply-header read got no data, the host side (ETC's driver, INFERRED) cancelled it after 111.9 ms and reset both pipes, and the next poll was answered at once with nothing lost or repeated (F163). Still UNKNOWN: how long the console stays silent while it writes its SD card and what it does with a poll that arrives meanwhile (H109); whether a session of ours, which would end on its 500 ms reply timeout, could have waited it out (INFERRED from the code: most likely not).

## CL-ML cross-check (class 5, not 1248 evidence)

ConsoleLink 0.1.8 for the SmartFade **ML** uses the same framing: a 12-byte header of six u16 LE words
("msgType" = w0, "payloadLen" = w1, "state[0..3]" = w2..w5), types 0 / 1 / 2 out and 3 in (type 4 is not
described), the same OUT payload `[seq][u16 LE len][type][data]` and big-endian counts; its terms
"announce" and "ack" are our DirtyObjects list and Fetch. Differences: it reads the count as one byte, it
ignores the reply type and the echo (SmartSoft checks the echo, so a decoder of ours should), and it
reads inbound bytes 0–1 as a 16-bit sequence (contradicted by the wraps above). Details:
[consolelink-analysis.md](consolelink-analysis.md) T1–T13, S1–S8. Nothing from ConsoleLink is copied into this
repository, and none of its behaviour (unsolicited fetches, idle polls with zeroed state words, skipping
System 9, back-to-back polling) was seen from SmartSoft, so none of it may be tried on the console.
