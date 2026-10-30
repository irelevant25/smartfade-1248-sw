# State model (session and mirrored console state)

Created 2026-10-28 from E2. **OBSERVED sequence, INFERRED causality, nothing VERIFIED.**
Evidence: [E2 record](captures/2026-10-28-e2-smartsoft-connect.md), facts F56–F58, F62–F66.
This page describes what SmartSoft's session looked like on the bus, as input to the
architecture's "desired vs actual" model ([../architecture.md](../architecture.md)); it is not
a design for our client.

**Update 2026-10-29 (E3,** [record](captures/2026-10-29-e3-console-actions.md)**).** A second session,
after a console power cycle, reproduced the phases below (F51–F54, F57–F65 VERIFIED as observed behaviour;
"nothing VERIFIED" above is the 2026-10-28 wording, kept) and showed three things the E2 view could not:
the connect-time pending list differed between the sessions (F56, F80; INFERRED: it depends on the console's state, the cause is not isolated); the live objects FaderInfo,
DisplayContent and PlaybackInfo are not in the full list (F89, H58); every kind of console-side action
(faders, a mode switch, a bump, the MASTER fader) arrives as state objects only (F82–F88). VERIFIED never
clears a message for sending; the causality statements remain INFERRED.

**Update 2026-10-30 (E3b,** [record](captures/2026-10-30-e3b-live-objects.md)**).** Three more sessions in
one capture (another console power cycle before the first, **no** power cycle between them) repeated the
phases below in full each time (F94) and showed four more things: the console's sequence counter is not
reset by a SmartSoft restart (F93); after a restart nothing is pending at connect, while after a power-on
the pending list varies (E2 9 records, E3 InputLevels, E3b LedStatus and DisplayContent; F94, H65); the
FaderInfo records and the LED blocks belong to the physical faders in every mode and bank (F99); a held
bump, the MASTER fader and the BUMPS fader all show in the level objects (F95–F97). The 157-record list
is byte-identical in five sessions. VERIFIED never clears a message for sending; the causality
statements remain INFERRED.

**Update 2026-10-30 (E8,** [record](captures/2026-10-30-e8-first-read-only-session.md)**).** The first live
session of our own software (a second host program on the same PC, polls and listed fetches only, **no
USBPcap**) ran the polling and fetching phases for 861 s *without* the two System writes: the console kept
listing and delivering its live objects (H66 CONFIRMED, OBSERVED once; F110), the connect-time pending list
was the longest yet (89 records with repeats, F111), and being listed turned out not to mean having changed
(F119). Console-side actions that the SmartSoft sessions had not covered (memory faders, a page change,
BLACKOUT, the IND keys, STACK, the record functions and CLEAR) all arrived as state objects (F112–F117, F120),
and a running sequence appeared as a stream of PlaybackInfo (F118). VERIFIED never clears a message for sending;
the causality statements remain INFERRED.

**Update 2026-10-30 (E8b,** [record](captures/2026-10-30-e8b-second-read-only-session.md)**).** The second live session of
our own software (1 121 s, polls and listed fetches only, after a power cycle, **with USBPcap from before the Zadig bind**
and a timed action log) ran the polling and fetching phases again without the System writes (H66 CONFIRMED in two
sessions) and added the bus view: the backend's log equals the bus byte for byte (F122); the EP0 traffic is one burst at
the driver change and nothing at the backend start, at Connect or at Disconnect (H68 CONFIRMED, F123); the pending list
after a power cycle was the 2-record list of E3b session 1 (F124). New readings of the mirrored state: BLACKOUT zeroes the
level objects (F125); in memory mode the FaderInfo records are pre-MASTER while the level objects are post-MASTER (F126); a
fader held across a page change keeps its page (F127); the CLEAR series (F128); a sequence started by its fader keeps
running at fader zero (F129); GO with STACK lit and the manual crossfaders in PlaybackInfo record 5, with SequenceMain
listed at each GO (F130). VERIFIED never clears a message for sending; the causality statements remain INFERRED.

## Session phases seen (SmartSoft, firmware 3.0.2.9.0.43)

| Phase | What happened | When (E2) | Fact |
|---|---|---|---|
| 0. Open | Host reads string descriptors on EP0 (LANGID, product string) | 28.47 s | F41, F56 |
| 1. First poll | Console already has a 9-object DirtyObjects list pending; SmartSoft fetches all 9 (PlaybackInfo, FaderInfo, LedStatus, OutputLevels, ChannelLevels, ConsoleStatus ×2, DisplayContent, FileName) | 28.48–28.90 s | F56 |
| 2. Idle | Empty type-4 replies | 28.93–29.87 s | F53 |
| 3. Handshake A | PC writes System `09 00 00`; next poll: list [ConsoleVersion]; PC fetches it; console answers "3.0.2.9.0.43" | 29.87–29.94 s | F58, F59 |
| 4. Handshake B | PC writes System `07 00 00`; next poll: 157-record list | 31.13 s | F58 |
| 5. Download | PC fetches all 157 objects in list order (≈ 7.3 s): System, SystemSizes, SystemSettings, Patch, Pages, Memories, Sequences, steps, ChannelText, Independents, FileName, status objects | 31.18–38.44 s | F57, F58, F67 |
| 6. Steady state | Poll about every 31 ms; the console lists only objects that changed; SmartSoft fetches exactly those | 38.47–125.25 s | F57, F62 |
| 7. End | No closing message; polling just stops | 125.25 s | F64 |

The same phases in E3 (2026-10-29, after a console power cycle, USB address 7):

| Phase | When (E3) | Difference from E2 |
|---|---|---|
| 0. Open | 6.24 s: string 0 and string 2 on EP0 | none (F41) |
| 1. First poll | 6.250 s first poll; the first reply is idle; the second reply (6.283 s) carries a list of **1 record, InputLevels (19)**, fetched at once (f21, f28) | E2: the first reply was a 9-record list; the connect-time list differed between the sessions (F56, F80, H58) |
| 2. Idle | 37 idle replies before the first write | E2: 30 |
| 3. Handshake A | 7.515 s System `09 00 00` after reply header #40 → list [37] → "3.0.2.9.0.43" (7.578 s) | same bytes, same position (F58, F92) |
| 4. Handshake B | 8.766 s System `07 00 00` after reply header #79 → 157-record list | same, and the list is byte-identical |
| 5. Download | 8.84–16.07 s (about 7.2 s), 157 fetches, last ChannelLevels f1608 | same |
| 6. Steady state | 16.07–313.34 s: polls about every 31 ms; owner actions produce lists of 1–5 records (fader 1, fader 5, the switch to memory mode, a bump, the MASTER fader; 87 lists in all) | E2: one fader movement; FaderInfo and DisplayContent first appear at 77.90 s, with the first fader move (F89) |
| 7. End | 313.34 s, 1.62 s after SmartSoft's close started: the last transfer is an idle poll / reply | same (F64) |

The same phases in E3b (2026-10-30; session 1 after a console power cycle, sessions 2 and 3 after a close
and a restart of SmartSoft without a power cycle; `hsp_summary.py sessions`):

| Phase | Session 1 (170.7–209.5 s) | Sessions 2 (224.9–258.5 s) and 3 (271.5–1009.0 s) |
|---|---|---|
| 0. Open | string 0 and string 2 on EP0 (f8, f10) | string 2 only (f5386, f10084) |
| 1. First poll | all-zero first poll (f11); the first reply is idle; the second reply (f18) carries a list of 2 records, LedStatus and DisplayContent, fetched at once (f20, f28, f36) | all-zero first poll (f5387, f10085); the replies stay idle: **nothing pending** |
| 2. Idle | 36 idle replies before the first write | 40 idle replies |
| 3. Handshake A | 172.005 s System `09 00 00` after reply header #40 → list [37] → "3.0.2.9.0.43" (f198) | 226.142 s and 272.745 s: same bytes, same position (#40 of the session) |
| 4. Handshake B | 173.255 s System `07 00 00` after reply header #79 → 157-record list (f356) | 227.396 s and 273.984 s: same, byte-identical list (f5722, f10420) |
| 5. Download | 160 fetches, last payload f1612 (pseq 162) | 158 fetches each; the counter continues (first pseq 163 and 67, F93) |
| 6. Steady state | idle | session 2 idle; session 3 holds all owner actions (fader 5, MASTER, BUMPS, bumps, SOLO, modes, bank, a GO crossfade): 268 lists in the capture |
| 7. End | 209.45 s: idle poll / reply, no closing message | 258.49 s and 1008.97 s likewise (F64) |

The same phases in E8 (2026-10-30; our own host, polls and listed fetches only, **no System writes**; the
backend's transfer log, JSONL `seq`, `hsp_summary.py log` and the E8 scripts under `tools/protocol-analyzer/e8/`):

| Phase | When (E8) | Difference from the SmartSoft sessions |
|---|---|---|
| 0. Open | not observed: no USBPcap ran, and our code issued no control transfer | H68 stays untested |
| 1. First poll | 0.102 s all-zero first poll (seq 1); the first reply (seq 2, 2.3 ms later) announces a list of **89 records** (seq 3), fetched one per poll cycle, 0.155–4.902 s (seq 4 … 356) | E2 held 9 records, E3 one, E3b two or none: the pending list depends on the console's history (F111, F94, H65) |
| 2. Idle | idle replies (15 368 of 20 763) between the console's lists | no handshake count to wait for |
| 3. Handshake A | **not sent**; no ConsoleVersion arrived | SmartSoft writes System `09 00 00` after reply header #40 (F58) |
| 4. Handshake B | **not sent**; no 157-record list, no show object | SmartSoft writes `07 00 00` after #79 (F58, F89) |
| 5. Download | none | 157 fetches in the SmartSoft sessions |
| 6. Steady state | 4.953–743.871 s: 2 469 lists of 1–6 records (2 305 with one record, 2 261 of them PlaybackInfo alone), then 117 s idle; every front-panel action the owner reports shows in them | as in the SmartSoft sessions, except that PlaybackInfo dominates here because a sequence was running (F118) |
| 7. End | 861.296 s: an idle poll / reply, then "session stopped by the application at an idle point" | silent end, as SmartSoft's (F64) |

The same phases in E8b (2026-10-30 evening; our own host, polls and listed fetches only, **no System writes**, after a power
cycle; the backend's log, JSONL `seq`, and the committed console-only pcap split, `fN`):

| Phase | When (E8b) | Difference from E8 and the SmartSoft sessions |
|---|---|---|
| 0. Open | EP0 burst at the Zadig bind, 20:36:45 (f7–f18: string reads, a stalled HID SET_IDLE, GET_STATUS, the HID interrupt reads cancelled and resubmitted), 78 s before the first poll; **nothing at the backend start (20:36:56) or at Connect (20:38:03)** | SmartSoft's string reads came right before its first poll; E8 had no bus view (H68 CONFIRMED, F123) |
| 1. First poll | 0.098 s all-zero first poll (seq 1, f19); the first reply is idle (seq 2, f22); the second (seq 4, f26) announces the 17-byte list [LedStatus, DisplayContent] (seq 5, f28), fetched (seq 6 … 13) | the same list as E3b session 1 after a power cycle; E8 held 89 records after a history unknown to us (F111, F124) |
| 2. Idle | idle replies; no list from 34.7 to 141.2 s (106.5 s) | no handshake count to wait for |
| 3. Handshake A, 4. Handshake B, 5. Download | **not sent**, no ConsoleVersion, no 157-record list, no show object (SequenceMain, listed by the console at each GO, excepted) | SmartSoft writes `09 00 00` after reply header #40 and `07 00 00` after #79 (F58) |
| 6. Steady state | 1 513 lists of 1–6 records after the power-on list, each object first listed with the first action that changes it (ConsoleStatus 21.9 s, FaderInfo and the levels 165.5 s, PlaybackInfo 232.6 s, Independents 304.0 s, SequenceMain 1088.4 s) | as E8, with a timed log |
| 7. End | 1121.328 s: an idle poll / reply (f125929 / f125932), then "session stopped by the application at an idle point"; no frame of the console after it | silent end (F64); a sequence was still stepping on the console (F129) |

## Model implied by the observations

- **The PC is the only initiator.** State reaches the PC only as the answer to a poll: a
  DirtyObjects list names changed objects (id, a, b, c), the PC fetches each, the console sends
  the object. (F51, F53, F57)
- **Mirrored front-panel state is just objects**: while a fader moved, FaderInfo, ChannelLevels,
  OutputLevels, LedStatus and DisplayContent were re-listed and re-fetched, several times in a
  few seconds, each fetch carrying the then-current value. Intermediate values can be missed
  because only polls bring data; the last value in E2 (0x81) was reported only because a poll
  followed. (F66, F64)
- **A start without any data (type-1) write is possible in what was seen** (polls and fetches are still transmissions, console-side effects UNKNOWN): the first nine objects arrived without any
  write (F56). The full show appeared after the two System writes (F58); whether a client that
  never writes would ever get the full list is UNKNOWN (H30).
- **Nothing was observed that changes console state.** The two System bodies are the only
  PC → console data; their meaning is not understood. The architecture's write path
  (`SetChannelLevel`, `Go`, …) has no observed protocol element behind it (H6, H24).
- **The HID interface plays no role in this session** (F63, H38).
- **Live objects are not in the full list (E3, F89, H58).** The 157-record list names OutputLevels,
  ChannelLevels, LedStatus and ConsoleStatus but not FaderInfo, DisplayContent or PlaybackInfo (nor
  InputLevels). SmartSoft had no FaderInfo until the first fader moved (77.90 s in E3). A client that only
  follows the lists does not know the fader / MASTER state, the LCD or the playback state until the console
  reports a change or has them pending at connect; this matters for the "actual state" side of our
  desired-vs-actual store. (Unsolicited fetches, the ML client's way round this, were never seen from
  SmartSoft and must not be tried: consolelink-analysis.md S4.)
- **The connect-time pending list differed between the sessions** (E2: 9 records, E3 after a power cycle:
  1 record, F56, F80); INFERRED: it depends on the console's state, the cause is not isolated. Do not
  design for a fixed start-up list.
- **Console-side actions are reported as state objects only** (E3, F82–F88): a fader move re-lists
  OutputLevels, FaderInfo, ChannelLevels and LedStatus (and DisplayContent when its glyph changes); the
  MASTER fader moves FaderInfo body[194] and the Blackout LED (LedStatus block 53); a switch to memory mode
  changes ConsoleStatus body[0] and LedStatus; a memory bump changed only its LED (with the BUMPS fader at
  0 %). Intermediate values are missed in the same way as in E2 (only polls bring data). No ConsoleEvent
  occurred in either direction (H5).
- **Restarting SmartSoft repeats the whole start-up and does not reset the console's counter** (E3b, F93,
  F94): a client that reconnects without a power-on finds nothing pending and receives FaderInfo,
  DisplayContent and PlaybackInfo only when they change, so after a reconnect its 'actual' state for the
  faders, MASTER, BUMPS, LCD and playback is unknown until something moves (H58). Do not design for a fixed
  start-up list (E2 9 records, E3 1, E3b 2 or none).
- **FaderInfo is indexed by physical fader, not by channel** (E3b, F99): in bank 25-48 physical fader 1 still
  moves record 1, and the channel it controls (25) shows in ChannelLevels and OutputLevels. The mode and the
  bank (ConsoleStatus body[0], body[4], F98) are needed to interpret a fader move.
- **Bump output and the MASTER scaling are visible in the level objects** (E3b, F96, F97): the live levels are
  post-MASTER and a held bump appears at the BUMPS level (tested at 0xFF). A model of 'desired versus actual'
  levels must treat the console's levels as outputs, not as fader positions.
- **A GO press shows as PlaybackInfo and an LED, not as an event** (E3b, F101): with the STACK button unlit it
  changed no output; ConsoleEvent never occurred (H5).
- **A client that never sends the System writes still receives live state** (E8, F110, H66 CONFIRMED, OBSERVED
  once): the console listed FaderInfo, ChannelLevels, OutputLevels, LedStatus, DisplayContent, ConsoleStatus,
  Independents and PlaybackInfo for 861 s and reported changes at once. Such a client receives no ConsoleVersion
  and no show object (F58, F89).
- **A pending list can be long and can repeat** (E8, F111): 89 records, ConsoleStatus × 39 and Independents × 44,
  each fetched in list order. Do not design for a fixed or short start-up list (E2 9 records, E3 one, E3b two or
  none, E8 89). Pending lists whose payload is a multiple of 64 bytes (37, 101 or 165 records) are reachable and
  untested (H75).
- **Listed is not changed** (E8, F119): 48 of 139 DisplayContent payloads, 82 of 2 340 PlaybackInfo payloads and
  20 of 25 later ConsoleStatus payloads equal their predecessor; ConsoleStatus is re-listed unchanged at every
  EDIT MEM exit, COPY entry and page-view opening. A mirror must compare bodies, not trust a listing.
- **More console-side actions arrive as state objects only** (E8, F112–F117, F120): memory faders (FaderInfo
  records 1–24, ChannelLevels, OutputLevels), the memory page (ConsoleStatus body[2], FaderInfo byte 3),
  BLACKOUT (LED block 53), the IND keys (Independents record byte 0, ChannelLevels body[49] / [50], LED blocks
  72 / 73), STACK (LED block 60, PlaybackInfo record 5), the record functions (LCD and LED blocks 57, 63–67) and
  CLEAR (a FaderInfo record dropped to zero in one step). No ConsoleEvent occurred in either direction (H5).
- **Blinking is carried inside one payload** (E8, F85, F116): a blinking LED has phase A different from phase B
  in a single LedStatus payload; each of the 28 record-function screens was opened and closed by exactly one
  LedStatus payload. A mirror cannot detect blinking by comparing successive payloads.
- **A running sequence makes PlaybackInfo the busiest object** (E8, F118): 2 340 of the 2 925 fetched objects, the
  rate being our fetch cadence (about 93 ms), not the console's.
- **The host's log is not the bus, and WinUSB splits long reads** (E8b, F122): the 61 112 transfers of the backend's log equal
  the bus byte for byte, but an IN read longer than 64 bytes and not a multiple of 64 is two URBs on the bus (1 845 reads;
  INFERRED host-side behaviour). A replay or pipe test must accept the split; the decoders see the concatenated record.
- **A bind is not a session, and the first poll needs no preceding EP0 traffic** (E8b, F123, H68 CONFIRMED): the driver change
  produced one EP0 burst (string reads, a stalled HID SET_IDLE, GET_STATUS) and no re-enumeration; nothing followed at the
  backend start, at Connect or at Disconnect, and the first poll, 78 s after the last string read, was answered normally.
- **After a power cycle the pending list can be the minimal two records** (E8b, F124): LedStatus and DisplayContent, byte-identical
  to E3b session 1's; every other object arrives only with the first action that changes it, so a client that connects right
  after a power-on knows nothing of faders, masters, status, playback or independents until they change (H58, H65). The console
  powered on in memory mode (INFERRED) and LED blocks 25–48 had a different constant colour (H78).
- **BLACKOUT acts on the level objects** (E8b, F125): with a channel at full, FaderInfo record byte 0, ChannelLevels, OutputLevels,
  the LED G byte and the LCD cell of the channel go to zero within 0.3 s and come back with BLACKOUT off, the MASTER staying at
  full; an "actual" level in a mirror is the post-MASTER, post-BLACKOUT output, not the fader position.
- **In memory mode the FaderInfo record is the fader's level before the MASTER; the level objects are after it** (E8b, F126):
  a mirror must not read an output level from a FaderInfo record in memory mode (record `94 94` at MASTER 0x76 against levels 0x45);
  in channel mode record byte 0 is post-MASTER and post-BLACKOUT. A held bump shows at the BUMPS level in both.
- **A memory fader keeps its page across a page change** (E8b, F127): the content of a fader above zero is the old page's until
  the fader reaches zero, FaderInfo byte 3 being per fader and ConsoleStatus body[2] the selected page; the bump LED blinks
  (H69 CONFIRMED, H79).
- **Key presses that change nothing visible still produce listings** (E8b, F128, F119): every CLEAR series re-listed DisplayContent
  unchanged; two presses clear the channel intensities, three the memory faders (the faders stay up and their bump LEDs blink
  until they are brought to zero).
- **A sequence is started by its fader and keeps running at fader zero** (E8b, F129): record 21, the output and block 21 fall back
  but PlaybackInfo record 1 keeps stepping (listed about every 93 ms by our cadence) until something unknown stops it; a mirror
  must not infer "stopped" from the fader or from the absence of output.
- **GO with STACK lit and the manual crossfaders live in PlaybackInfo record 5; SequenceMain is listed at each GO** (E8b, F130): a
  timed fade moves Live and Next in opposite directions (sum 255), the manual crossfaders move the two bytes independently
  (sums 0 … 510), the step counter (bytes 0–1, H76) advances when a fade completes, and the LED block 50 is steady in a timed fade
  and blinking at intermediate manual positions (H64). PAUSE has not been seen (the owner's description is a statement only).

## Show content seen in the download (decoded, `SHOW04.ASC`)

OBSERVED once, matching the console's own saved file (E2 record "Cross-check against SHOW04.ASC",
F70-F79). Object layouts: [responses.md](responses.md) "Decoded object bodies". What the 157-object
download carries, in terms of the show model:

| Show data | Object(s) | Match with the file | Not carried / open |
|---|---|---|---|
| Patch (DMX -> channel) | Patch (5) | 512/512 | proportional level (UNKNOWN where) |
| 16 memories with levels and labels | Memory (0), slot (a, b), number 24a + b + 1 | 16/16, 768/768 levels | page / fader meaning (E6) |
| 1 sequence + the main stack | Sequence (2), SequenceMain (3), StepContent (4) | steps 26/26 incl. Up / Down / third time (FOLLOWON) | sequence step numbers differ wire vs file (H43) |
| 2 Independents | Independents (12) | 2/2 | field meaning; live levels (ChannelLevels body[49], [50] were 0) *(E8 2026-10-30: record byte 0 = the on state and ChannelLevels body[49], [50] = the levels moved with the IND keys, F115; the other fields are still open)* |
| MIDI and other settings | SystemSettings (41) | run of six `SET` values at body[6..11] | per-key mapping, default times (H45) *(E3b: body[12..15] = the default Up / Down times shown on the LCD, F102; not in the file as far as F104 shows)* |
| Labels (pages, channels, memories, sequences, independents) | text block in each object | all compared objects | byte order HYPOTHESIS, two readings fit the same bytes (H40, F73) |
| Live state (fader positions, levels) | FaderInfo, ChannelLevels, OutputLevels | **not in the file**; at session start only channel 5 at 0xAB (INFERRED a channel fader, F78) | scale of live objects (E3(a)) *(E3 2026-10-29: 0x00–0xFF, full = 0xFF with the MASTER at 0xFF, F83; all levels 0 at the start of E3; scaling by the MASTER of live levels H47 / H57 untested)* *(E3b 2026-10-30: the live levels are MASTER-scaled, F96; BUMPS is FaderInfo body[193], F95; a held bump appears in them, F97; FaderInfo is per physical fader, F99)* *(E8 2026-10-30: the memory faders are FaderInfo records 1–24 and drive the levels with the memory's stored content, F112; the memory page is ConsoleStatus body[2], F113)* *(E8b 2026-10-30: BLACKOUT zeroes the level objects, F125; in memory mode the FaderInfo records are pre-MASTER while ChannelLevels and OutputLevels are post-MASTER, F126; the stack's crossfade state is PlaybackInfo record 5, F130)* |

Consequence for "desired vs actual": the file is a faithful snapshot of the stored show, the live
objects are separate state. A diff between a saved `.asc` and a download would be possible for the
parts above; nothing here is VERIFIED.

## Open

Behaviour when a poll is not answered, reconnect after an unclean stop, a second host (*E8, 2026-10-30: a
second host program ran on the same PC for 861 s, F110; a second PC is untested; E8b, 2026-10-30 evening: a second session of the same host, 1 121 s, with the bus seen in parallel, F122*), a console
in another mode, console power-up while connected, whether the 157-object list is always full or
depends on a show-changed flag; whether the initial pending list (F56) depends on what happened
before the session. *(E3 2026-10-29: it differed after a console power cycle, F80, so it depends on something about the
console's state or history; the cause is not isolated. The 157-record list was byte-identical in both
sessions, but both sessions had the same show.)* *(E3b 2026-10-30: the pending list differed again after a third history, F94; after a restart nothing was pending, H65. The 157-record list was byte-identical in five sessions, all with the same show.)*
