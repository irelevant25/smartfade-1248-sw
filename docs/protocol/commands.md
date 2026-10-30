# Commands (PC → console)

Created 2026-10-28 from E2. **Observed messages only, none VERIFIED.** This page records what
SmartSoft sent; it is **not** a list of messages our software may send. Before any of them is
sent to a console, it needs promotion in [known-facts.md](known-facts.md), a reproduction on
our unit, its effect on the console understood and known to be harmless (safety rule 2 in
reverse-engineering.md; a reproduction alone proves only that SmartSoft sends it), and
`/hardware-safety-check`. Framing: [packet-format.md](packet-format.md).
Evidence: [E2 record](captures/2026-10-28-e2-smartsoft-connect.md) and, since 2026-10-29,
the [E3 record](captures/2026-10-29-e3-console-actions.md) and, since 2026-10-30, the
[E3b record](captures/2026-10-30-e3b-live-objects.md) and, since the late evening of 2026-10-30, the
[E9 record](captures/2026-10-30-e9-smartsoft-writes.md) (SmartSoft's data writes beyond the System pair).

**Update 2026-10-29 (E3).** The second SmartSoft session, after a console power cycle, sent the same
four kinds of message (poll, fetch, the two System writes) and nothing else: no ConsoleEvent, no show
data, no System `[0x0A][page][0]`. The bytes of both System writes are identical and they came at the
same points (after reply headers #40 and #79). The rows below are therefore **VERIFIED as SmartSoft's
behaviour only**: "VERIFIED" here says that SmartSoft sends exactly these bytes at these points, and
that the console answered the same way in two sessions. **It does not say that we may send them.** The
page stays a record, not a list of messages we may send: every message still needs its effect on the
console understood and known to be harmless (safety rule 2) and `/hardware-safety-check`, and none has
passed. The "Created ... none VERIFIED" wording above is the 2026-10-28 text, kept.

**Update 2026-10-30 (E3b).** Three more SmartSoft sessions (one capture, another console power cycle before
the first) sent the same four kinds of message and nothing else: polls, fetches and **six** System writes,
two per session, byte-identical to E2's and E3's (`00 03 00 27 09 00 00`, `01 03 00 27 07 00 00`) and at the
same points (after reply headers #40 and #79 of each session; F92). No ConsoleEvent, no show data, no System
`[0x0A][page][0]` (no page change was made) and nothing on interface 0, although the owner operated bumps,
SOLO, the BUMPS and MASTER faders, modes, a bank switch and, once, the GO button (E3b-A1,
[owner statements](captures/2026-10-30-e3b-owner-statements.txt)). The rows below
are VERIFIED as SmartSoft's behaviour only, as before: **this does not say that we may send them**, and none
is cleared (safety rule 2; `/hardware-safety-check` has not passed).

**Update 2026-10-30 (SC-1).** Safety check SC-1 ([safety-checks.md](safety-checks.md)) cleared **two** of
the messages below for a read-only session of our own software: the poll, and the fetch of a record the
console listed in the same session (in list order, once, at SmartSoft's cadence or slower). The Poll and
Fetch rows are superseded in that respect; the two System writes stay uncleared, and every caution
about them stands. The sentences above are the earlier wording, kept.

**Update 2026-10-30 (E8).** E8 ([record](captures/2026-10-30-e8-first-read-only-session.md)) was not a SmartSoft
session: our own software sent 20 763 polls and 2 925 fetches of listed records (SC-1's two messages) and nothing else
(no System write, no data header, no payload, no control transfer, nothing on interface 0), so the rows below, which
record SmartSoft's messages, are not extended with E8's counts. The console answered all of them (F110). **E8 clears no
other message;** the two System writes stay uncleared.

**Update 2026-10-30 (E8b).** E8b ([record](captures/2026-10-30-e8b-second-read-only-session.md)) was a second session of our own software, again not
a SmartSoft session: it sent 27 484 polls and 2 315 fetches of listed records (SC-1's two messages) and nothing else, and this time the
bus confirms it: 29 799 OUT transfers on 0x04, all 12-byte headers, equal to the backend's log byte for byte, no System write, no data
header, no payload and no control transfer of ours, nothing on interface 0 (F122; the EP0 burst at the Zadig bind preceded the backend by
11 s and was not ours, F123). The rows below, which record SmartSoft's messages, are not extended with E8b's counts. The console answered
every poll and fetch (F110) and no ConsoleEvent occurred in either direction although the owner used memory faders, a page change,
BLACKOUT, the IND keys, STACK, the record keys, GO, the crossfaders and CLEAR (F112–F130); the owner's description of PAUSE is a
statement and no PAUSE press exists in any capture. **E8b clears no other message;** the two System writes stay uncleared.

**Update 2026-10-30, late evening (E9).** E9 ([record](captures/2026-10-30-e9-smartsoft-writes.md)) was a SmartSoft session
driven by the owner through its Console Simulator and recorded passively (our software sent nothing): the first capture in which
SmartSoft sent more than the two System messages. Its 401 data writes were the System pair (byte-identical again, a sixth
session), 332 ConsoleEvent kind 0 (fader) writes, 64 ConsoleEvent kind 1 (key) writes (32 press / release pairs) and three
ChannelText (9) label writes. The new messages are recorded with their exact bytes in the section "PC → console data writes seen
in E9" below, each **OBSERVED (one session) and NOT CLEARED**. **E9 clears no message:** SC-1's poll and listed fetch remain the
only messages cleared, and the System writes and every message of the E9 section stay uncleared; the statements further down that
no set-level, GO or record message was observed are superseded by E9 (text kept, annotated where they stand).

| Message | Bytes as seen | Count in E2 (E3) | Effect observed | Status | Facts |
|---|---|---|---|---|---|
| Poll | 12-byte header, w0 = 0 (first poll f11: all zero) | 3 001 (E3: 9 692) (E3b: 25 824) | Console answers with a type-3 or type-4 reply header | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED) | F52, F53 |
| Fetch | 12-byte header, w0 = 2, w2 = object ID, w3..w5 = a, b, c (example f17: ID 17; E3 f21: ID 19) | 228 (E3: 423) (E3b: 1 000) | Object delivered in the reply to the next poll; treated as a read by SmartSoft's code (S11, INFERRED), console-side effects UNKNOWN (the console's sequence counter advances per payload and a listed object is delivered once), not established as safe to send; *(superseded for the listed fetch by SC-1, 2026-10-30, read-only session: the effects named here are the known ones, no other was observed in 1 651 fetches)* every fetch named by a DirtyObjects record | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED); console-side effects stay UNKNOWN | F52, F57 |
| Data: System (ID 39), body `09 00 00` | header f209 `01 00 07 00 …` + payload f211 `00 03 00 27 09 00 00` (E3: f177 + f179, byte-identical) (E3b: f181 + f183, f5547 + f5549, f10245 + f10247, byte-identical) | 1 (29.872 s) (E3: 1, 7.515 s) (E3b: 3, at 172.005 / 226.142 / 272.745 s) | Next poll: DirtyObjects list with ConsoleVersion (ID 37) | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #40 in both sessions (count-driven, INFERRED) **E3b:** reproduced in three more sessions (five in all), after reply header #40 of each session; still SmartSoft's behaviour only | F58, F92 |
| Data: System (ID 39), body `07 00 00` | header f375 + payload f377 `01 03 00 27 07 00 00` (E3: f343 + f345, byte-identical) (E3b: f347 + f349, f5713 + f5715, f10411 + f10413, byte-identical) | 1 (31.133 s) (E3: 1, 8.766 s) (E3b: 3, at 173.255 / 227.396 / 273.984 s) | Next poll: 157-record DirtyObjects list, full download | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #79 in both sessions (count-driven, INFERRED) **E3b:** reproduced in three more sessions (five in all), after reply header #79 of each session; still SmartSoft's behaviour only | F58, F92 |

## PC → console data writes seen in E9 (2026-10-30, late evening; OBSERVED, one session; NOT CLEARED)

Source: the [E9 record](captures/2026-10-30-e9-smartsoft-writes.md) ("Inventory of the PC → console writes", "ConsoleEvent kind 0: the
faders", "ConsoleEvent kind 1: the keys", "Whole-object writes"), the committed console-only capture
`captures/2026-10-30-e9-smartsoft-writes.pcap` and its normalization `.jsonl` (`seq` = the JSONL record number, `fN` = the frame
of the split that carries the data). The 401 data writes are **SmartSoft's own** (the owner drove its Console Simulator); our
software sent nothing. **Every row below is OBSERVED (one session, one PC, one console) and NOT CLEARED: no type-1 data header is
encoded anywhere in the solution, SC-1 clears only the poll and the listed fetch, and a future safety check SC-2 (after the repeat
E9b) is the gate for any of these messages.** The record's table "Input for a future safety check (SC-2)" gives, per class, the
exact pattern, the effect on the console, whether it can be undone and what must be verified first; it is the starting point of
SC-2 and not an approval. Framing: header `01 00 [w1 LE] [w2 … w5]` with w2..w5 stale (F52), payload
`[pseq][u16 LE len = w1 − 4][id][body]`, SmartSoft's sequence byte +1 per data payload across all ids ([packet-format.md](packet-format.md)
"Data writes", F131). `[pseq]` stands for that counter byte; the examples carry its value at that point of the session.

| Message | Bytes as seen | Count in E9 | Effect observed | Status | Facts |
|---|---|---|---|---|---|
| Data: ConsoleEvent (ID 20), kind 0 (fader) | header `01 00 08 00` + stale words; payload `[pseq] 04 00 14 00 [index] [value] [previous]`. First event: header seq 9664 (f19327) `01 00 08 00 00 00 00 00 00 00 00 00`, payload seq 9665 (f19329) `02 04 00 14 00 00 05 00` | 332 (index 0x00: 84, 0x06: 70, 0x30: 86, 0x31: 92) | Sets the control with that index at once, from the first event: FaderInfo, ChannelLevels, OutputLevels, an LED block and an LCD cell follow as for the panel; inside EDIT MEM it edits the selected memory's level (stored by the next EDIT MEM); no acknowledgement | OBSERVED (one session), NOT CLEARED | F131, F132, F134, F135 |
| Data: ConsoleEvent (ID 20), kind 1 (key), press | header `01 00 07 00` + stale words; payload `[pseq] 03 00 14 01 [code] 01`. Example: bump 7, header seq 19054 (f38107) `01 00 07 00 00 00 00 00 00 00 00 00`, payload seq 19055 (f38109) `56 03 00 14 01 06 01` | 32 | The console acts on the press (bumps flash, BLACKOUT and SOLO toggle, GO starts a fade, FADERS keys switch mode, the record keys open their function) | OBSERVED (one session), NOT CLEARED | F133, F134 |
| Data: ConsoleEvent (ID 20), kind 1 (key), release | the same header; payload `[pseq] 03 00 14 01 [code] 00`, a separate write 46 ms to 5.80 s after the press. Example: seq 19435 / 19436 (f38871) `57 03 00 14 01 06 00` | 32 | Ends a held bump (its output goes off); a session that ends between a press and its release is untested | OBSERVED (one session), NOT CLEARED | F133 |
| Data: ChannelText (ID 9), a label | header `01 00 2B 00` + stale words; payload `[pseq] 27 00 09 [channel − 1] 03 06 [36 bytes]` (the whole 39-byte object). Channel 7: seq 57437 (f114873) `1A 27 00 09 06 03 06` + 36 × `00` (the unchanged empty label); seq 57881 (f115761) `1B 27 00 09 06 03 06 01 0C 00 61 00 6A` + 30 × `00` ("Čaj", UTF-16BE); seq 60605 (f121209) `1C 27 00 09 06 03 06 00 00 00 61 00 6A` + 30 × `00` (the "restore", residue behind a zeroed first unit) | 3 | Replaces the channel's label (show data); nothing was listed back | OBSERVED (one session), NOT CLEARED | F138 |
| Data: System (ID 39), body `09 00 00` / `07 00 00` | as in the table above; E9: header seq 137 (f273) + payload seq 138 (f275) `00 03 00 27 09 00 00`, header seq 220 (f439) + payload seq 221 (f441) `01 03 00 27 07 00 00` | 2 (78.262 s, 79.510 s) | as above (list [37] / the 157-record list) | **VERIFIED as SmartSoft's behaviour only** (sixth session; never clearance) | F58, F92 |

Fader indices seen (kind 0; no other index was observed, and none may be derived):

| Index | Control (Simulator) | Events | First event (payload) | What the console followed with |
|---|---|---|---|---|
| 0x00 | channel fader 1 | 84 | seq 9665 `02 04 00 14 00 00 05 00` | FaderInfo record 1 byte 0 (body[1]), ChannelLevels[1], OutputLevels[2] (DMX 1), LED block 1 G, LCD cell body[3] |
| 0x06 | channel fader 7, used inside EDIT MEM | 70 | seq 67236 `25 04 00 14 00 06 02 00` | LED block 7 G and LCD cell body[9] (the edited memory's channel 7, blind) and the stored Memory body[10]; FaderInfo record 7 never moved with it |
| 0x30 (48) | MASTER | 86 | seq 24115 `58 04 00 14 00 30 FD 00` | FaderInfo body[194]; LED block 53 blinking below full |
| 0x31 (49) | BUMPS | 92 | seq 29434 `AE 04 00 14 00 31 FD 00` | FaderInfo body[193] |

The value byte is the console's level byte (0x00–0xFF, unconverted); the previous byte is SmartSoft's own last value of that index
(0x00 for the first event of the session, also for the MASTER and BUMPS, which stood at 0xFF on the console; F132). The indices of
the other 22 channel faders, the memory faders and the crossfaders are UNKNOWN, and "channel fader k = index k − 1" rests on two
points (HYPOTHESIS).

Key codes seen (kind 1; each named by a logged Simulator step and a timed press; header / payload seq of the first press, payload frame in brackets):

| Code | Key (Simulator) | Pairs | First press | LED block that changed |
|---|---|---|---|---|
| 0x00 | bump 1 (memory bump 1 in memory mode) | 2 | seq 39595 / 39596 (f79191) `14 03 00 14 01 00 01` | 1 |
| 0x06 | bump 7 | 2 | seq 19054 / 19055 (f38109) `56 03 00 14 01 06 01` | 7 |
| 0x08 | bump 9 | 4 | seq 73995 / 73996 (f147991) `75 03 00 14 01 08 01` | 9 |
| 0x09 | bump 10 | 2 | seq 84654 / 84655 (f169309) `83 03 00 14 01 09 01` | 10 |
| 0x31 | GO | 1 | seq 43869 / 43870 (f87739) `18 03 00 14 01 31 01` | 50 |
| 0x33 | SOLO | 2 | seq 36385 / 36386 (f72771) `0E 03 00 14 01 33 01` | 52 |
| 0x34 | BLACKOUT | 2 | seq 33541 / 33542 (f67083) `0A 03 00 14 01 34 01` | 53 |
| 0x36 | FADERS 1-24 | 2 | seq 41448 / 41449 (f82897) `16 03 00 14 01 36 01` | 55 |
| 0x38 | CLEAR | 3 | seq 80937 / 80938 (f161875) `7D 03 00 14 01 38 01` | 57 |
| 0x39 | FADERS MEMS | 3 | seq 39471 / 39472 (f78943) `12 03 00 14 01 39 01` | 58 |
| 0x3E | COPY | 1 | seq 82233 / 82234 (f164467) `7F 03 00 14 01 3E 01` | 63 |
| 0x41 | REC MEM | 1 | seq 73005 / 73006 (f146011) `73 03 00 14 01 41 01` | 66 |
| 0x42 | EDIT MEM | 7 | seq 66989 / 66990 (f133979; the `sim edit mem` step's own first press, the record's choice: seq 76628, f153257) `21 03 00 14 01 42 01` | 67 |

For all 13 codes, code = LED block number − 1 (H82, HYPOTHESIS for any other key: no code may be derived from it). **Not observed, and
never to be tried:** any other index or code, ConsoleEvent kind 2 (the wheel, excluded by the owner's decision), a Memory
whole-object write, any other object write, System `[0x0A][page][0]`, the erase commands and a show upload (F121). **What a sender
learns:** only from the state objects the console then lists (FaderInfo, LED blocks, ConsoleStatus, Memory); ConsoleEvent was never
listed or fetched and an object write (ChannelText) produced no listing at all. The console accepted these events from a host that
had sent the System pair (every SmartSoft session does); whether it accepts them from a host that never sent it (our read-only
session, H66) is UNKNOWN and can only be learned by sending, which nothing clears.

Observations that matter for safety:

- Reading the console needs only polls and fetches (F56). Nothing in E2 shows a write is needed
  to read state. *Annotation 2026-10-29 (E3):* this holds for what the console lists on its own
  at connect; the full show appeared only after SmartSoft's System `07 00 00` write (H30, F58),
  and the live objects FaderInfo, DisplayContent and PlaybackInfo are not in that list either
  (F89), so a complete read as SmartSoft performs it involves those two writes.
- SmartSoft sent nothing else: no ConsoleEvent (ID 20), no show data, no upload (F58, F67). What
  the two System bodies mean (`09 00 00`, `07 00 00`) is UNKNOWN beyond "session start-up
  handshake" (H4, H30); they must not be replayed to explore. *(E2 and E3 wording, kept. E9, 2026-10-30, late evening: in its sixth
  session SmartSoft also sent ConsoleEvent and ChannelText writes, see the E9 section above; it still sent no show data and no upload.)*
- There is no observed message for "set channel level", GO, record, load or save (H6 open). *(Superseded 2026-10-30, late evening,
  by E9, text kept: SmartSoft's Console Simulator sends channel levels, the MASTER, BUMPS, GO and the record keys as ConsoleEvent
  writes, see the E9 section above; H6 CONFIRMED for the paths used, OBSERVED once. Load and save were not observed, F121.)*
- No message on interface 0 (HID) was seen from anyone (F63; E3 again: 0 URBs on EP 0x81 / 0x02).
- **E3 (2026-10-29):** the owner operated two channel faders, a memory bump, the MASTER fader and a
  mode switch on the console while SmartSoft was Online, and SmartSoft still sent nothing but polls,
  fetches and its two start-up System messages. Console-side actions reach the PC only as state
  objects (F82–F88); no ConsoleEvent (ID 20) occurred in either direction (H5, H24).
- **E3b (2026-10-30):** three more sessions again sent only polls, fetches and the two start-up System
  messages (six in all), although the owner pressed bumps, SOLO and GO and moved the BUMPS and MASTER faders
  and switched modes and banks. Console-side actions reach the PC as state objects only (F95–F101); no
  ConsoleEvent (H5, H24). Nothing observed so far shows what a GO, a bump or a fader write from the PC
  would look like, so no such message can be built from these observations. *(Superseded in part by E9, text kept: SmartSoft's own GO,
  bump and fader writes are now recorded above. They are OBSERVED in one session and not cleared, so no such message may be built
  into code that talks to a console.)*
- **E8 (2026-10-30) and the static write-path analysis.** E8 sent only polls and listed fetches and saw no ConsoleEvent in
  either direction although the owner used memory faders, BLACKOUT, the IND keys, STACK and the record keys (F112–F117). Static
  analysis ([smartsoft-analysis.md](smartsoft-analysis.md) S11.15, INFERRED; **E9 is the test**) says that SmartSoft's only
  live-control message is ConsoleEvent (id 20), sent by its Console Simulator, and that GUI edits of stored data are
  whole-object writes. **Two paths are known to be destructive and our code must never use them:** sending a show (it replaces the
  console's show, DOCUMENTED by SmartSoft manual p. 24) and the System erase commands (a = 11, 12, 13, 14, 16; INFERRED from the
  confirmation dialogs; F121). Neither was seen on the bus. *(E9, 2026-10-30, late evening: the live-control half of the static
  reading is confirmed on the bus for the paths used, H5, H6, H24; ChannelText is a whole-object write, as predicted; neither
  destructive path was seen among E9's 401 writes.)*
- **ConsoleLink (class 5).** The third-party ML client sends ConsoleEvent kinds 0 / 1, System
  `[0x0A][page][0]` and a System `07` with sequence 1 at every connect, outside its own write gate
  ([consolelink-analysis.md](consolelink-analysis.md) W1–W5, §4). None of these was seen from SmartSoft
  on the 1248, so none is listed in the table above, and a "read-only" label on a third-party tool is
  no evidence that a message is harmless. *(E9: ConsoleEvent kinds 0 and 1 were seen from SmartSoft on the 1248 and are recorded in
  the E9 section above, with 1248 indices and codes that differ from ConsoleLink's ML numbers (24 MASTER, 25 BUMPS); System
  `[0x0A][page][0]` was not seen.)*
