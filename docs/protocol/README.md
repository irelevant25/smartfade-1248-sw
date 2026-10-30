# Protocol knowledge base

This folder records what we **know**, **believe** and **suspect** about how software talks
to the ETC SmartFade 1248. It is the single place where protocol knowledge is recorded.
Application code may only rely on elements marked **VERIFIED**.

## Three separate things. Do not conflate them.

1. **SmartFade ↔ PC communication** (this folder): USB "High-Speed-Pipe" used by SmartSoft,
   plus the MIDI implementation.
2. **SmartSoft / console show file format (.asc)**: see [../formats/asc-format.md](../formats/asc-format.md).
3. **Our application's native show format**: not designed yet.

## Status vocabulary

| Status | Meaning |
|---|---|
| **VERIFIED** | Reproduced against *our* physical SmartFade 1248 (experiment or capture), with a recorded evidence entry |
| **OBSERVED** | Seen in a capture or on hardware at least once, but not yet reproduced or fully understood |
| **DOCUMENTED** | Stated in official ETC documentation. Not yet checked on our unit (docs can be outdated: see the MIDI contradictions) |
| **INFERRED** | Derived from static analysis (binaries, strings, file structure) or consistent patterns in data |
| **HYPOTHESIS** | A theory that needs a specific test |
| **UNKNOWN** | No information |

Rules:
- Never promote a status without adding evidence (file, capture, date, firmware version, SmartSoft version).
- OBSERVED becomes VERIFIED only by an independent reproduction on our unit (a second session
  or second PC, or a genuinely different evidence channel such as bus traffic versus an API
  reading) with its own recorded evidence entry. A verbal confirmation never promotes. A
  reproduction verifies that the console or SmartSoft behaves that way, never that we may
  send the message (safety rule 2 in reverse-engineering.md).
- When something turns out wrong, don't delete it. Mark it **REJECTED**, keep the old text,
  and add *new evidence / what changed / why*.
- Golden protocol tests may only use bytes from a committed capture file.

## Documents

| Document | Content |
|---|---|
| [known-facts.md](known-facts.md) | Facts with sources |
| [hypotheses.md](hypotheses.md) | Open hypotheses with test plans |
| [smartsoft-analysis.md](smartsoft-analysis.md) | Static analysis of the SmartSoft installer, application, engine DLLs and USB driver |
| [midi-spec-analysis.md](midi-spec-analysis.md) | MIDI spec v1.5 vs console manual v3.0, message tables |
| [evidence-matrix.md](evidence-matrix.md) | Feature × source cross-reference (central document) |
| [capability-matrix.md](capability-matrix.md) | What the console / SmartSoft / our app supports |
| [reverse-engineering.md](reverse-engineering.md) | Experiment plan, tooling, safety rules, hardware evidence requested, capture format |
| [packet-format.md](packet-format.md) | HSP framing as observed in E2 (OBSERVED, nothing VERIFIED); since E3 (2026-10-29) the framing facts are VERIFIED as observed behaviour; E3b (2026-10-30) reproduced them a third time; E8 (2026-10-30, our own read-only session) reproduced them with another host program, the first evidence channel other than SmartSoft; E8b (2026-10-30 evening, our second read-only session, with the bus seen in parallel) reproduced them again and showed that WinUSB splits long IN reads into two URBs (host-side); E9 (2026-10-30, late evening, SmartSoft's first data writes, passive) added the type-1 data-write framing and the outbound counter over 401 writes (OBSERVED once, NOT CLEARED), reproduced the rest again and made F93 and F94 VERIFIED |
| [commands.md](commands.md) | PC → console messages SmartSoft sent in E2, E3 and E3b (a record, not a list of messages we may send; VERIFIED there means SmartSoft's behaviour only); E8 and E8b were our own read-only sessions (SC-1) and add none; **E9 (2026-10-30, late evening) adds a section with the exact bytes of SmartSoft's ConsoleEvent (fader, key) and ChannelText writes, with the fader indices and key codes seen, each OBSERVED (one session) and NOT CLEARED** |
| [responses.md](responses.md) | Reply types and object payloads seen in E2, E3, E3b, E8 and E8b (the live objects are decoded from E3, E3b, E8 and E8b); E9: the Memory objects the console lists after a recording, the first System records with c ≠ 0 and the live objects following PC events (section "Live objects (E9)") |
| [state-model.md](state-model.md) | Session phases and mirrored-state model as seen in E2, E3, E3b, E8 and E8b; E9: the session with PC writes and the console's reactions (no acknowledgement, the Memory object listed after a recording, c ≠ 0 records, matching of PC faders) |
| [safety-checks.md](safety-checks.md) | Hardware safety checks: what is cleared for sending and under which conditions (SC-1, 2026-10-30: the poll and the fetch of a listed record, read-only session; nothing else is cleared; E8 and E8b ran under it and cleared nothing further); **E9 (2026-10-30, late evening) observed SmartSoft's first write messages and cleared none; a future SC-2 is their gate; a note on the c ≠ 0 list records as a known safe stop of a read-only session** |
| [consolelink-analysis.md](consolelink-analysis.md) | ConsoleLink (third-party, SmartFade **ML**, evidence class 5) compared with our 1248 evidence; proposed annotations and the hypotheses N1–N6 (now H46–H51). Never changes a 1248 status by itself |
| [captures/](captures/) | Capture files, experiment records (E1, E2, E3, E3b, E8, E8b), the owner's statements and the capture format; E9 (SmartSoft writing to the console, 2026-10-30, late evening) added |

The four deferred documents (`packet-format.md`, `commands.md`, `responses.md`,
`state-model.md`) were created 2026-10-28, when the first USB capture (E2) existed. They carry
OBSERVED statuses at most; only VERIFIED elements may be used by code that talks to a console,
and no protocol element is VERIFIED yet. *(2026-10-28 wording, kept.)* **Update 2026-10-29:** after
the independent E3 session many elements are VERIFIED (26 facts, see [known-facts.md](known-facts.md)),
but VERIFIED means reproduced behaviour, never clearance for sending (safety rule 2 in
[reverse-engineering.md](reverse-engineering.md)): "only VERIFIED elements may be used by code that
talks to a console" is a necessary condition, not a sufficient one, and **no message is cleared:
no code may send anything to a console yet.** **Update 2026-10-30:** after the E3b sessions (three more
sessions after another power cycle) 35 facts are VERIFIED, nine more than above, each in part (the structural
part of facts first seen in E3, never a live value); the sentence above stands unchanged: VERIFIED is
necessary, not sufficient, and **no message is cleared; no code may send anything to a console yet.** **Update 2026-10-30, later the same day (SC-1):** safety check SC-1
([safety-checks.md](safety-checks.md)) cleared the poll and the listed fetch for a read-only session of our
own software (`SmartFade.Devices.Usb`, behind `Device:Mode=Usb` confirmed per day); the sentences above are
superseded for those two messages only, and nothing else is cleared. **Update 2026-10-30, E8 (our own read-only session,
[record](captures/2026-10-30-e8-first-read-only-session.md)):** the first live session of that software ran 861 s over WinUSB on
interface 1, sent only the poll and the fetch of a listed record, and the console kept reporting its live state to a host that
never sent SmartSoft's System writes (H66 and H67 CONFIRMED for what was used, H68 untested, H62 REJECTED). **39 facts are VERIFIED**
(four more than above: F95 and, each in part, F99, F100 and F101; F57's no-de-duplication detail now too), the structural parts only,
never a live value; F110–F121 were added. The sentence above stands unchanged: VERIFIED is necessary, not sufficient, **E8 clears no
message**, and SC-1's two messages remain the only ones cleared. SmartSoft's static write paths (smartsoft-analysis.md S11.15) add
INFERRED context for E9 and the destructive paths our code must never use (F121). **Update 2026-10-30, evening, E8b (our second read-only
session, [record](captures/2026-10-30-e8b-second-read-only-session.md)):** 1 121 s over WinUSB on interface 1 after a power cycle, polls and
listed fetches only, with USBPcap from before the Zadig bind, a named backup and a timed action log. **49 facts are VERIFIED** (ten more than
above: F110, F112, F113, F114 (the block 53 pattern), F115, F116, F119, F120 and, each in part, F117 and F118), the structural parts only,
never a live value; F122–F130 were added; **H68 CONFIRMED** (EP0 traffic only at the Zadig bind: nothing at the backend start, at Connect or at
Disconnect), H66 CONFIRMED in two sessions, H69 and H73 CONFIRMED, H72 CONFIRMED in capture for seven key LEDs (the IND keys and BLACKOUT are
now OBSERVED with named, timed keys); H76–H80 were added. The sentence above stands unchanged: VERIFIED is necessary, not sufficient, **E8b
clears no message**, and SC-1's two messages remain the only ones cleared. The owner's descriptions (PLAY, PAUSE, the crossfaders) are
statements and never promote; PAUSE has not been seen in any capture. **Update 2026-10-30, late evening, E9 (SmartSoft writing to the
console, passive, [record](captures/2026-10-30-e9-smartsoft-writes.md)):** the first capture in which a PC **writes** to the console:
SmartSoft Online driven through its Console Simulator, 28 minutes, 401 data writes (the System pair, 332 ConsoleEvent fader events, 64
ConsoleEvent key events, three ChannelText labels); our software sent nothing. **53 facts are VERIFIED** (four more than above: F93,
F94 for its structure, F97 for BUMPS 0xFF and F103, each E3b + E9), the structural parts only, never a live value; F131–F140 were added
(all OBSERVED, one session: the write framing and the outbound counter, ConsoleEvent kind 0 and kind 1 with the four fader indices and
13 key codes seen, the console's answer, matching with PC faders, the record functions driven from the PC, the first DirtyObjects
records with c ≠ 0, the ChannelText write, E9's side effects on the show, and the Simulator's opening sending nothing); **H5 (PC →
console part), H6 (for the paths used) and H24 (kinds 0 and 1) CONFIRMED (OBSERVED once)**, H40 reading A OBSERVED for SmartSoft's write
(the console's side open), H72 extended, H81–H86 added; F57's "c = 0 in every record" limited, not rejected. The sentence above stands
unchanged and now applies to the write messages too: VERIFIED is necessary, not sufficient, **E9 clears no message**, no type-1 data
header is encoded anywhere in the solution, and SC-1's two messages remain the only ones cleared; a future SC-2, after the repeat E9b,
is the gate for the write messages ([safety-checks.md](safety-checks.md)). The owner's answers of 2026-10-31 are statements and never
promote; one open owner item remains: memory 7 of page 1 was probably changed by E9 and has not been checked (F139).

## Version landscape

| Artifact | Version | Relationship |
|---|---|---|
| SmartSoft installer | 3.0.2.9.0.43b (MSI ProductVersion 3.0.43) | Bundles SmartSoft, SmartFade firmware and ML firmware, all **3.0.2.9.0.43** |
| SmartSoft.exe / sfEngine.dll | built 2011-11-30 | Part of the 3.0.2.43 package |
| SmartSoft user manual | 3.0.1 Rev A (2011-10) | Slightly older than the package. Content still matches |
| SmartFade console manual | 3.0 Rev A (2010-11) | Bundled in installer. Newest official MIDI description we have |
| SmartFade MIDI spec | doc 0.12 (2005), firmware **1.50** | **Outdated** relative to firmware 3.x. Contradicts console manual 3.0 |
| SFML library | 11.2.2 (2015) | SmartFade **ML** personalities. Not applicable to the 1248 |
| Ostrov .asc | `$SOFTWAREVERSION 3.0.2 43`, `IDENT 3.0` | Written by 3.0.2 build 43 engine (SmartSoft or console) |
| User's console firmware | **3.0.2.9.0.43** (owner's reading of the Diagnostics menu, 2026-10-28, known-facts F48) | Same version as the SmartSoft 3.0.2b package |
| ConsoleLink (third-party reference, https://github.com/gaeljaffrain/consolelink) | 0.1.8, commit `09b3c266757c4d4a614151fa9326e25143caaa1f` (2026-10-20), MIT licence (Gaël Jaffrain); local checkout `analysis/extracted/consolelink-09b3c26/` (git-ignored, read-only) | Python web / Art-Net client for the SmartFade **ML** (USB PID 0x0201), tested by its author only with an ML. **Evidence class 5** of CLAUDE.md ("other sources, clearly identified"): read as data, never run, nothing copied; compared in [consolelink-analysis.md](consolelink-analysis.md). Not 1248 evidence: it can corroborate, contradict or extend our hypotheses but changes no 1248 status by itself |
