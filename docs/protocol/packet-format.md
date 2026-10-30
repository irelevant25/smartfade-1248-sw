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
**OBSERVED once** (F131) and **NOT CLEARED**: nothing here clears a message, no type-1 header is encoded anywhere in the solution, and
the messages with their exact bytes are in [commands.md](commands.md) "PC → console data writes seen in E9". The first records with
c ≠ 0 appeared in E9's DirtyObjects lists (four System records, F137).

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
| w1 | 0 | payload length (7 in both observed cases, E2 / E3 / E3b; E9: 7, 8 or 43, F131) | 0 |
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

The ConsoleEvent bodies are single bytes (no 16-bit fields); the ChannelText text block follows the byte order discussed under
"Byte order" above (UTF-16BE for SmartSoft's write, H40; the console's side is open, H84). The meaning of every index, code and byte is
in [commands.md](commands.md), not here.

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
(F131).

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
untested); whether a page other than 1 changes the System (5, 5, c) records (H83).

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
