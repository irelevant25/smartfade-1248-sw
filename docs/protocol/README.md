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
| [smartsoft-analysis.md](smartsoft-analysis.md) | Static analysis of the SmartSoft installer, application, engine DLLs and USB driver; S11.15 (2026-10-30): the write paths from the GUI; S11.16 (2026-10-31): GUI edits of the patch, the independents, names, sequences and the stack, all INFERRED or DOCUMENTED and never seen on the bus *(since the E12 analysis of the same evening each row carries E12's outcome: most readings confirmed as OBSERVED once, two corrected: the step type travels in InsertStep's second phase, and SmartSoft writes a type-2 step in 100 bytes, not the console's 148)* *(since the E12b pass of 2026-11-01 each row also carries E12b's outcome: the reproduced readings are VERIFIED as SmartSoft's and the console's behaviour; S11.9 notes the driver's side of a timeout seen once)* |
| [midi-spec-analysis.md](midi-spec-analysis.md) | MIDI spec v1.5 vs console manual v3.0, message tables |
| [evidence-matrix.md](evidence-matrix.md) | Feature × source cross-reference (central document) |
| [capability-matrix.md](capability-matrix.md) | What the console / SmartSoft / our app supports |
| [reverse-engineering.md](reverse-engineering.md) | Experiment plan, tooling, safety rules, hardware evidence requested, capture format; runbooks for E8, E8b, E9 and **E12** (SmartSoft editing the patch and the stack / sequences, passive; planned 2026-10-31, run the same afternoon and analysed the same evening), and the next captures each analysis proposes (the latest: E12b, "Next capture (proposed by the E12 analysis, 2026-10-31)") *(E12b ran on 2026-10-31, late evening, and is analysed: its row, and the latest list "Next capture (proposed by the E12b analysis, 2026-10-31)", with owner questions, optional passive captures and static checks; no repeat of E12b is needed)* |
| [packet-format.md](packet-format.md) | HSP framing as observed in E2 (OBSERVED, nothing VERIFIED); since E3 (2026-10-29) the framing facts are VERIFIED as observed behaviour; E3b (2026-10-30) reproduced them a third time; E8 (2026-10-30, our own read-only session) reproduced them with another host program, the first evidence channel other than SmartSoft; E8b (2026-10-30 evening, our second read-only session, with the bus seen in parallel) reproduced them again and showed that WinUSB splits long IN reads into two URBs (host-side); E9 (2026-10-30, late evening, SmartSoft's first data writes, passive) added the type-1 data-write framing and the outbound counter over 401 writes (OBSERVED once, NOT CLEARED), reproduced the rest again and made F93 and F94 VERIFIED; E9b (2026-10-31, the repeat of E9: 427 data writes in three SmartSoft sessions, passive) reproduced the write framing and made it VERIFIED as SmartSoft's and the console's behaviour (F131, still NOT CLEARED) and added the Memory (0) write's w1 = 94; E12 (2026-10-31, SmartSoft editing the patch and the stack / sequences, passive) held the framing for 70 more writes of 12 ids and added the step commands' and the object writes' lengths and the fact that a data payload carries no qualifier fields (OBSERVED once, NOT CLEARED); E12b (2026-10-31, late evening, the repeat) held it for 62 more writes, made E12's shapes VERIFIED as behaviour, added w1 = 160 and the first non-zero USBD status on the bulk pipes (a read cancelled and both pipes reset during the console's SD save, F163; still NOT CLEARED) |
| [commands.md](commands.md) | PC → console messages SmartSoft sent in E2, E3 and E3b (a record, not a list of messages we may send; VERIFIED there means SmartSoft's behaviour only); E8 and E8b were our own read-only sessions (SC-1) and add none; **E9 (2026-10-30, late evening) adds a section with the exact bytes of SmartSoft's ConsoleEvent (fader, key) and ChannelText writes, with the fader indices and key codes seen, each OBSERVED (one session) and NOT CLEARED**; **E9b (2026-10-31) adds the Memory (0) whole-object write, fader index 0x04 and the key codes 0x37, 0x3B, 0x47 and 0x48, and marks the rows it repeated VERIFIED as SmartSoft's behaviour, all NOT CLEARED**; **E12 (2026-10-31, evening) adds a section with the step commands (InsertStep, DeleteStep, MoveStep, id 42) and the Patch, Independents, StepContent, Sequence and Page writes, each OBSERVED once and NOT CLEARED, the key code 0x3A, and the Memory write's layout VERIFIED as SmartSoft's behaviour**; **E12b (2026-10-31, late evening, the repeat) marks E12's rows VERIFIED as SmartSoft's behaviour where it reproduced them and adds a section with a channel step, a 156-byte Sequence, Manual, a label holding U+010C and the key codes 0x32 and 0x3A, all NOT CLEARED** |
| [responses.md](responses.md) | Reply types and object payloads seen in E2, E3, E3b, E8 and E8b (the live objects are decoded from E3, E3b, E8 and E8b); E9: the Memory objects the console lists after a recording, the first System records with c ≠ 0 and the live objects following PC events (section "Live objects (E9)"); E9b: the console's own EDIT MEM and recordings listed like the PC's, the post-MASTER stored level, the emptied memory, the label returned verbatim (section "Live objects (E9b)"); E12: the console's lists after SmartSoft's step commands (Sequence records with c ≠ 0), Sequence and StepContent bodies whose length follows the show, the 161-record download after edits and the console's own menu edits listed at every wheel detent (section "Objects after edits (E12)"); E12b: E12's answers again (VERIFIED as behaviour), Sequence records with c = the changed index, a 156-byte Sequence, a channel step, a label stored verbatim and read by the console as UTF-16BE, PlaybackInfo bytes 4–5 and ConsoleStatus body[6] with the new key codes, a reply that did not come during the console's SD save (section "Objects after edits, the repeat (E12b)") |
| [state-model.md](state-model.md) | Session phases and mirrored-state model as seen in E2, E3, E3b, E8 and E8b; E9: the session with PC writes and the console's reactions (no acknowledgement, the Memory object listed after a recording, c ≠ 0 records, matching of PC faders); E9b: the last mover wins between a PC fader and a physical fader, the Memory write's layout, REC MEM's post-MASTER level, the three-session phase table; E12: editing the stored show over USB (whole-object writes, step commands, the two-phase insert), steps keyed by a shifting index, content-dependent lengths, the console's own menu edits at every wheel detent, the E12 phase table; E12b: the editing model in a second capture, the live stack addressed by index, a write followed by live objects, the console silent on USB during its SD save, the E12b phase table |
| [safety-checks.md](safety-checks.md) | Hardware safety checks: what is cleared for sending and under which conditions (SC-1, 2026-10-30: the poll and the fetch of a listed record, read-only session; nothing else is cleared; E8 and E8b ran under it and cleared nothing further); **E9 (2026-10-30, late evening) observed SmartSoft's first write messages and cleared none; a future SC-2 is their gate; a note on the c ≠ 0 list records as a known safe stop of a read-only session**; **E9b (2026-10-31) repeated the writes and cleared none; the E9b record's updated SC-2 input is the starting point of SC-2**; **SC-2 (2026-10-31): a conditional GO for building and reviewing a live-control encoder bounded to the fader indices 0x00 / 0x30 / 0x31 and eight live key codes, with the first write session E11 on the owner's go, and a NO-GO for the record keys from the PC (SC-3 later), ChannelText and Memory writes and the System pair; it clears nothing for sending until its conditions are met**; **E12 (2026-10-31, run and analysed) cleared nothing: the stack, sequence, page, memory and step-command writes remain uncleared (a later check decides), a note under SC-2's "Path to a wider clearance"**; **E12b (2026-10-31, late evening, the repeat) cleared nothing either (a note under the same heading) and adds one condition for E11: no SD save or load on the console while our software is connected (F163, H109)** |
| [consolelink-analysis.md](consolelink-analysis.md) | ConsoleLink (third-party, SmartFade **ML**, evidence class 5) compared with our 1248 evidence; proposed annotations and the hypotheses N1–N6 (now H46–H51). Never changes a 1248 status by itself |
| [captures/](captures/) | Capture files, experiment records (E1, E2, E3, E3b, E8, E8b), the owner's statements and the capture format; E9 (SmartSoft writing to the console, 2026-10-30, late evening) added and E9b (the repeat, 2026-10-31) added; E12 (SmartSoft editing the patch and the stack / sequences, 2026-10-31, analysed the same evening) added; E12b (its repeat, 2026-10-31, late evening, analysed the same night) added |

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
promote; the one open owner item, memory 7 of page 1 changed by E9, was closed on 2026-10-31: the owner restored it by hand and `SHOW09.ASC` equals the pre-E9 backup `SHOW07.ASC` byte for byte (F139).
**Update 2026-10-31, E9b (the repeat of E9, passive, [record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md)):** three SmartSoft Online
sessions after a console power cycle, 427 data writes (the System pair × 3, 366 ConsoleEvent fader events, 26 key press / release pairs, two
ChannelText labels and one Memory (0) whole-object write); our software sent nothing. **61 facts are VERIFIED** (eight more than above: F131,
F132, F133, F134, F138 and F140 and, each in part, F135 and F136, each E9 + E9b; the scopes are in known-facts.md), as SmartSoft's and the
console's behaviour, the structural parts only, never a live value; F141–F147 were added (all OBSERVED: the last mover wins between a PC
fader and a physical fader, the console's own EDIT MEM listing System (5, 5, 0), the Memory (0) write from the Mem Edit tab, the ChannelText
round trips (F144 judged OBSERVED), REC MEM storing the post-MASTER level, REC MEM of an empty look emptying a memory, the Memory object
listed after the console's own recording); **H84 and H86 CONFIRMED, H85 REJECTED, H81's first clause CONFIRMED for channel mode**, H82 and
H83 supported, H78 contradicted in its prediction (cause UNKNOWN), H40 and H72 annotated, H87–H90 added. The sentence above stands
unchanged: VERIFIED is necessary, not sufficient, **E9b clears no message**, no type-1 data header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*,
SC-1's two messages remain the only ones cleared, and the safety check SC-2 ([safety-checks.md](safety-checks.md); its input is the E9b
record's "Input for a future safety check (SC-2), updated after E9b") is the gate for any write; **SC-2 ran the same day**: a conditional GO for building and reviewing a live-control encoder bounded to the fader indices 0x00 / 0x30 / 0x31 and eight live key codes, with the first write session E11 on the owner's go, and a NO-GO for the record keys from the PC (SC-3 later), ChannelText and Memory writes and the System pair; it clears nothing for sending until its conditions are met. The owner's statements never promote;
`SHOW08.ASC` is the E9b pre-session backup and `SHOW09.ASC` the owner's save after he restored memory 7.
**Update 2026-10-31, later (the static pass S11.16 and the plan of E12; no experiment was run):** the `protocol-researcher` mapped, from
`SmartSoft.exe` and `sfEngine.dll`, which messages SmartSoft would send when the owner edits the patch, the independents, page and memory
names, sequences and the stack ([smartsoft-analysis.md](smartsoft-analysis.md) S11.16, INFERRED), and the console and SmartSoft manuals
were re-checked for the matching functions (known-facts F148–F150, DOCUMENTED). None of these messages has ever been on the bus (no
patch, stack, sequence or page edit exists in E2–E9b), so nothing is OBSERVED or VERIFIED: hypotheses H91–H101 are HYPOTHESIS with the
passive capture **E12** as their test ([reverse-engineering.md](reverse-engineering.md) "E12 runbook"; SmartSoft Online drives the edits,
USBPcap records, our software is not involved; planned, not run). The VERIFIED count stays 61, **nothing is cleared for sending**, and our
code never sends any of these messages (SC-2 class F, "Never", for the Patch and Independents writes).
**Update 2026-10-31, evening (E12 run):** the E12 capture is committed ([captures/2026-10-31-e12-smartsoft-patch-stack-edits.md](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md): the console-only
split of device address 27, the normalized JSONL with the action log, the owner's statements; 70 PC → console writes by SmartSoft, 0 framing
errors, three SmartSoft sessions with downloads of 157 / 161 / 157 records, `SHOW13.ASC` = `SHOW12.ASC` byte for byte, so every edit was restored).
The record holds provenance, completeness and structure only: the decoding of the writes against H91–H101 is not recorded (the assistant's
attempt was stopped by its safety system and is not repeated; the owner or a later pass evaluates them with the committed analyzer tools). **No
status change: H91–H101 stay HYPOTHESIS, the VERIFIED count stays 61, nothing is cleared for sending.**
**Update 2026-10-31, evening (E12 analysed; the paragraph above is superseded in part, text kept):** the decoding was done by the
`capture-analyst` agent on Opus 5.5 at the owner's request, with read-only tools (`tools/protocol-analyzer/e12/`; the record's section
"Analysis"), and applied by the curator, who re-checked the evidence behind every promotion. SmartSoft's edits of the patch, the
independents, stack steps, a sequence, pages and memories are whole-object writes in the console's layouts (the type-2 "Direct cue" step
excepted: 100 bytes against the console's 148), stored by the console and answered by no listing; the step commands InsertStep (32),
DeleteStep (34), MoveStep (35) and id 42 carry `[a][b][c]` bodies (a = 48 for the stack, 0 for KOTLIK), the console answers each with a
list of the affected objects (among them Sequence (2, 0, 0, 6) with c ≠ 0), and InsertStep runs in two phases with the step type in the
second; the console lists its own patch and step-timing menu edits at every wheel detent. **65 facts are VERIFIED** (four more than
above: F143 and F147 and, each in part, F144 and F146; E9b + E12, as SmartSoft's and the console's behaviour, never clearance to send);
F151–F159 were added (OBSERVED once); H91, H93, H97, H99 and H14 CONFIRMED, H92, H94, H95, H96, H101 and H87 PARTLY CONFIRMED, H98
CONFIRMED with its sub-reading "the step type travels in the command" REJECTED, H100 unchanged, H102–H106 added. The sentence above
stands unchanged: VERIFIED is necessary, not sufficient, **E12 clears no message**, SC-1's two messages remain the only ones cleared, and
our code never sends any of E12's messages (the Patch and Independents writes SC-2 class F, "Never"; the Memory write class D, NO-GO; the
step commands and the Sequence, SequenceMain, StepContent and Page writes in no class). For SC-1 (documentation only, INFERRED from the
code): Sequence records with c ≠ 0 and 144- / 148-byte bodies, seen after SmartSoft's step commands and in its download, would end a
read-only session of ours with its fault, a safe stop (F156, H106).
**Update 2026-11-01 (E12b, the repeat of E12, run 2026-10-31, late evening, [record](captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md)):** two more
SmartSoft sessions after another console power cycle, 62 PC → console writes, an SD save of the edited show made on the console, every
edit restored (`SHOW16.ASC` = `SHOW14.ASC`); our software sent nothing; decoded by the `capture-analyst` agent on Opus 5.5 at the owner's
request and applied by the curator, who re-checked the evidence. Where E12b reproduced E12, SmartSoft's step commands, its two-phase
insert, the console's lists after them (Sequence records with c = the changed step's index) and the stored results are **VERIFIED as
SmartSoft's and the console's behaviour**: **72 facts are VERIFIED** (seven more than above: F151, F152, F156 and F157 and, each in part,
F153, F154 and F158; E12 + E12b); F160–F165 were added (OBSERVED once: the channel step, the labelled State step that the console stored
verbatim and exported as UTF-16BE, the console's SD file forms, a reply that did not come and two pipe resets during the console's SD
save, the live stack's positions as indices under step commands, the key codes 0x32 and 0x3A); H91, H93, H97, H98, H99 (Page), H102,
H103 and H105 CONFIRMED in two sessions, H92, H94, H95, H96 and H101 PARTLY CONFIRMED, H12 CONFIRMED, H40's byte order CONFIRMED for the
console's side (reading B rejected for the 1248), H44 PARTLY CONFIRMED, H104 REJECTED, H107–H110 added. The sentence above stands
unchanged: VERIFIED is necessary, not sufficient, **E12b clears no message**, SC-1's two messages remain the only ones cleared, and our
code never sends any of E12b's messages; its one lesson for our sessions is that no SD save or load is made on the console while our
software is connected (F163, H109; safety-checks.md "Conditions for E11").

**E11, the first live-control session of our own software, is done (2026-11-01, three attempts; records `captures/2026-11-01-e11-first-live-control-session-attempt-1.md`,
`captures/2026-11-01-e11-first-live-control-session-attempt-2.md`, `captures/2026-11-01-e11-first-live-control-session-attempt-3.md`).** Under SC-2's conditional GO the backend (the owner's implementation of classes A and B1 behind
the record-function interlock) sent, in all, 120 ConsoleEvents in the guided sessions (attempt 1 none, attempt 2 four, attempt 3 one hundred and sixteen:
channel fader 1 up and back to zero, bump 7, SOLO, BLACKOUT, BUMPS, FADERS MEMS, memory bump 1, FADERS 1-24, MASTER, GO with STACK unlit), each between a
poll's reply and the next poll or fetch with the reply at most 46 ms old, and the console followed every control as it follows the panel and SmartSoft's
Console Simulator, acknowledging nothing; the bus equals the backend's log byte for byte in every attempt and no show data changed. **73 facts are VERIFIED**
(one more than above: F167 in part, channel fader 1 rising from our software, attempts 2 + 3); F168 and F169 were added (OBSERVED once: the keys, a bump tap's
absence from the fetched objects, MASTER and BUMPS), H66 CONFIRMED for writes, H65 and F166 annotated, H111 added (a fader event addresses the physical
fader; untested in memory mode and bank 25-48). The sentence above stands unchanged: VERIFIED is necessary, not sufficient, **E11 clears nothing beyond
SC-2's conditional GO**, and the two backend sessions the owner ran after attempt 3's guide without USBPcap are a recorded deviation (log-only evidence).

**E9c (2026-11-02, passive; record `captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md`):** after the owner's decision to drive memory faders and channels 25-48 from the PC
through an automatic bank switch, SmartSoft's Console Simulator was driven through every channel fader of bank 1-24, two faders of bank
25-48, memory faders on two pages, a mode and a page switch with a PC level in force, held bumps, the keys seen once and a restart (1 227
writes, 0 framing errors, no show data changed). The fader index names the physical fader in every bank and mode (F171, H111 CONFIRMED in
part); a switch does not carry a level across and a PC fader must match the new content's level before it moves it (F172, H79 / H81
CONFIRMED); NEXT is key 0x3A and its blind levels go into BlindLevels (18) (F173, H112); held bumps show cleanly up to 2.85 s (F174); the
keys seen once before were seen again (F175); a PC fader reads post-MASTER (F176); 24 records were pending without a power cycle and the
download holds 158 records (F177). **74 facts are VERIFIED** (F165 in part); E9c clears nothing: the safety check SC-5 decides the classes
for the automatic switch and the held bumps, with E9d for what E9c saw once.

**E9d (2026-11-02, morning, passive; record `captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md`):** after a console power cycle, SmartSoft's Simulator was driven
through every fader and bump of every bank in two sessions (2 855 writes, 0 framing errors, no show data changed), and every step
matched the physical-fader reading: **F171 (the index names the physical fader, in every bank and mode) and F174 (held bumps) are
VERIFIED, F172 (a switch does not carry a level; a PC fader must match first) VERIFIED in part, H111 CONFIRMED; 77 facts are
VERIFIED** (never clearance). The SC-5 addendum lifts the "after E9d" rows: every channel fader of both banks and every memory fader of
the shown page, the memory bumps as bounded holds and the bump of bank 25-48's first fader are now GO (conditional) under SC-5's
conditions; the channel bumps 2–6, 8, 9, 11–24 and 26–48 wait for E9e. Nothing is sent before the owner implements, the reviewer
passes and E11b runs.

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
