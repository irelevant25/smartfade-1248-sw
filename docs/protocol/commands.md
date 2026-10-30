# Commands (PC → console)

Created 2026-10-28 from E2. **Observed messages only, none VERIFIED.** This page records what
SmartSoft sent; it is **not** a list of messages our software may send. Before any of them is
sent to a console, it needs promotion in [known-facts.md](known-facts.md), a reproduction on
our unit, its effect on the console understood and known to be harmless (safety rule 2 in
reverse-engineering.md; a reproduction alone proves only that SmartSoft sends it), and
`/hardware-safety-check`. Framing: [packet-format.md](packet-format.md).
Evidence: [E2 record](captures/2026-10-28-e2-smartsoft-connect.md) and, since 2026-10-29,
the [E3 record](captures/2026-10-29-e3-console-actions.md) and, since 2026-10-30, the
[E3b record](captures/2026-10-30-e3b-live-objects.md).

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

| Message | Bytes as seen | Count in E2 (E3) | Effect observed | Status | Facts |
|---|---|---|---|---|---|
| Poll | 12-byte header, w0 = 0 (first poll f11: all zero) | 3 001 (E3: 9 692) (E3b: 25 824) | Console answers with a type-3 or type-4 reply header | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED) | F52, F53 |
| Fetch | 12-byte header, w0 = 2, w2 = object ID, w3..w5 = a, b, c (example f17: ID 17; E3 f21: ID 19) | 228 (E3: 423) (E3b: 1 000) | Object delivered in the reply to the next poll; treated as a read by SmartSoft's code (S11, INFERRED), console-side effects UNKNOWN (the console's sequence counter advances per payload and a listed object is delivered once), not established as safe to send; *(superseded for the listed fetch by SC-1, 2026-10-30, read-only session: the effects named here are the known ones, no other was observed in 1 651 fetches)* every fetch named by a DirtyObjects record | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED); console-side effects stay UNKNOWN | F52, F57 |
| Data: System (ID 39), body `09 00 00` | header f209 `01 00 07 00 …` + payload f211 `00 03 00 27 09 00 00` (E3: f177 + f179, byte-identical) (E3b: f181 + f183, f5547 + f5549, f10245 + f10247, byte-identical) | 1 (29.872 s) (E3: 1, 7.515 s) (E3b: 3, at 172.005 / 226.142 / 272.745 s) | Next poll: DirtyObjects list with ConsoleVersion (ID 37) | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #40 in both sessions (count-driven, INFERRED) **E3b:** reproduced in three more sessions (five in all), after reply header #40 of each session; still SmartSoft's behaviour only | F58, F92 |
| Data: System (ID 39), body `07 00 00` | header f375 + payload f377 `01 03 00 27 07 00 00` (E3: f343 + f345, byte-identical) (E3b: f347 + f349, f5713 + f5715, f10411 + f10413, byte-identical) | 1 (31.133 s) (E3: 1, 8.766 s) (E3b: 3, at 173.255 / 227.396 / 273.984 s) | Next poll: 157-record DirtyObjects list, full download | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #79 in both sessions (count-driven, INFERRED) **E3b:** reproduced in three more sessions (five in all), after reply header #79 of each session; still SmartSoft's behaviour only | F58, F92 |

Observations that matter for safety:

- Reading the console needs only polls and fetches (F56). Nothing in E2 shows a write is needed
  to read state. *Annotation 2026-10-29 (E3):* this holds for what the console lists on its own
  at connect; the full show appeared only after SmartSoft's System `07 00 00` write (H30, F58),
  and the live objects FaderInfo, DisplayContent and PlaybackInfo are not in that list either
  (F89), so a complete read as SmartSoft performs it involves those two writes.
- SmartSoft sent nothing else: no ConsoleEvent (ID 20), no show data, no upload (F58, F67). What
  the two System bodies mean (`09 00 00`, `07 00 00`) is UNKNOWN beyond "session start-up
  handshake" (H4, H30); they must not be replayed to explore.
- There is no observed message for "set channel level", GO, record, load or save (H6 open).
- No message on interface 0 (HID) was seen from anyone (F63; E3 again: 0 URBs on EP 0x81 / 0x02).
- **E3 (2026-10-29):** the owner operated two channel faders, a memory bump, the MASTER fader and a
  mode switch on the console while SmartSoft was Online, and SmartSoft still sent nothing but polls,
  fetches and its two start-up System messages. Console-side actions reach the PC only as state
  objects (F82–F88); no ConsoleEvent (ID 20) occurred in either direction (H5, H24).
- **E3b (2026-10-30):** three more sessions again sent only polls, fetches and the two start-up System
  messages (six in all), although the owner pressed bumps, SOLO and GO and moved the BUMPS and MASTER faders
  and switched modes and banks. Console-side actions reach the PC as state objects only (F95–F101); no
  ConsoleEvent (H5, H24). Nothing observed so far shows what a GO, a bump or a fader write from the PC
  would look like, so no such message can be built from these observations.
- **E8 (2026-10-30) and the static write-path analysis.** E8 sent only polls and listed fetches and saw no ConsoleEvent in
  either direction although the owner used memory faders, BLACKOUT, the IND keys, STACK and the record keys (F112–F117). Static
  analysis ([smartsoft-analysis.md](smartsoft-analysis.md) S11.15, INFERRED; **E9 is the test**) says that SmartSoft's only
  live-control message is ConsoleEvent (id 20), sent by its Console Simulator, and that GUI edits of stored data are
  whole-object writes. **Two paths are known to be destructive and our code must never use them:** sending a show (it replaces the
  console's show, DOCUMENTED by SmartSoft manual p. 24) and the System erase commands (a = 11, 12, 13, 14, 16; INFERRED from the
  confirmation dialogs; F121). Neither was seen on the bus.
- **ConsoleLink (class 5).** The third-party ML client sends ConsoleEvent kinds 0 / 1, System
  `[0x0A][page][0]` and a System `07` with sequence 1 at every connect, outside its own write gate
  ([consolelink-analysis.md](consolelink-analysis.md) W1–W5, §4). None of these was seen from SmartSoft
  on the 1248, so none is listed in the table above, and a "read-only" label on a third-party tool is
  no evidence that a message is harmless.
