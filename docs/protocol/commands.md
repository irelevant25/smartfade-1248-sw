# Commands (PC → console)

Created 2026-10-28 from E2. **Observed messages only, none VERIFIED.** This page records what
SmartSoft sent; it is **not** a list of messages our software may send. Before any of them is
sent to a console, it needs promotion in [known-facts.md](known-facts.md), a reproduction on
our unit, its effect on the console understood and known to be harmless (safety rule 2 in
reverse-engineering.md; a reproduction alone proves only that SmartSoft sends it), and
`/hardware-safety-check`. Framing: [packet-format.md](packet-format.md).
Evidence: [E2 record](captures/2026-10-28-e2-smartsoft-connect.md) and, since 2026-10-29,
the [E3 record](captures/2026-10-29-e3-console-actions.md).

**Update 2026-10-29 (E3).** The second SmartSoft session, after a console power cycle, sent the same
four kinds of message (poll, fetch, the two System writes) and nothing else: no ConsoleEvent, no show
data, no System `[0x0A][page][0]`. The bytes of both System writes are identical and they came at the
same points (after reply headers #40 and #79). The rows below are therefore **VERIFIED as SmartSoft's
behaviour only**: "VERIFIED" here says that SmartSoft sends exactly these bytes at these points, and
that the console answered the same way in two sessions. **It does not say that we may send them.** The
page stays a record, not a list of messages we may send: every message still needs its effect on the
console understood and known to be harmless (safety rule 2) and `/hardware-safety-check`, and none has
passed. The "Created ... none VERIFIED" wording above is the 2026-10-28 text, kept.

| Message | Bytes as seen | Count in E2 (E3) | Effect observed | Status | Facts |
|---|---|---|---|---|---|
| Poll | 12-byte header, w0 = 0 (first poll f11: all zero) | 3 001 (E3: 9 692) | Console answers with a type-3 or type-4 reply header | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED) | F52, F53 |
| Fetch | 12-byte header, w0 = 2, w2 = object ID, w3..w5 = a, b, c (example f17: ID 17; E3 f21: ID 19) | 228 (E3: 423) | Object delivered in the reply to the next poll; treated as a read by SmartSoft's code (S11, INFERRED), console-side effects UNKNOWN (the console's sequence counter advances per payload and a listed object is delivered once), not established as safe to send; every fetch named by a DirtyObjects record | **VERIFIED 2026-10-29** as SmartSoft's behaviour (E2 + E3; was OBSERVED); console-side effects stay UNKNOWN | F52, F57 |
| Data: System (ID 39), body `09 00 00` | header f209 `01 00 07 00 …` + payload f211 `00 03 00 27 09 00 00` (E3: f177 + f179, byte-identical) | 1 (29.872 s) (E3: 1, 7.515 s) | Next poll: DirtyObjects list with ConsoleVersion (ID 37) | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #40 in both sessions (count-driven, INFERRED) | F58, F92 |
| Data: System (ID 39), body `07 00 00` | header f375 + payload f377 `01 03 00 27 07 00 00` (E3: f343 + f345, byte-identical) | 1 (31.133 s) (E3: 1, 8.766 s) | Next poll: 157-record DirtyObjects list, full download | **VERIFIED 2026-10-29 as SmartSoft's behaviour only** (E2 + E3; was OBSERVED); effect causality OBSERVED twice, still INFERRED; sent after reply header #79 in both sessions (count-driven, INFERRED) | F58, F92 |

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
- **ConsoleLink (class 5).** The third-party ML client sends ConsoleEvent kinds 0 / 1, System
  `[0x0A][page][0]` and a System `07` with sequence 1 at every connect, outside its own write gate
  ([consolelink-analysis.md](consolelink-analysis.md) W1–W5, §4). None of these was seen from SmartSoft
  on the 1248, so none is listed in the table above, and a "read-only" label on a third-party tool is
  no evidence that a message is harmless.
