# CLAUDE.md: SmartFade 1248 control software

## This project controls physical lighting hardware

It targets the **ETC SmartFade 1248** lighting console (the brief says "SmartFace"; the
official name is SmartFade). Wrong bytes can blackout a show, overwrite show data or damage a
console's firmware.

- **Never invent hardware protocol behaviour.** No guessed commands, byte sequences,
  checksums, IDs or timings, including in mocks, examples and tests.
- **All protocol assumptions must be marked as assumptions** (HYPOTHESIS / INFERRED /
  DOCUMENTED) in code comments and docs. Only **VERIFIED** protocol elements may be used by
  code that can talk to real hardware.
- Prefer evidence in this order:
  1. direct hardware tests on our console
  2. packet/USB/MIDI captures
  3. official ETC documentation
  4. reproducible experiments (e.g. saving show files on the console)
  5. other sources, clearly identified
- Documentation can be wrong: the MIDI spec v1.5 and console manual v3.0 contradict each
  other. "Documented" ≠ "verified".
- Never send unverified data to the real console. Default to simulation. Real hardware mode
  must be enabled deliberately and be visibly indicated in the UI.
- **VERIFIED is necessary, not sufficient.** A VERIFIED fact says the console or SmartSoft was
  seen behaving that way in two independent sessions; it never clears a message for sending.
  No PC → console message (poll, fetch or data) may be sent by our code until it has been
  observed from SmartSoft on the 1248, understood, shown to be harmless and cleared by
  `/hardware-safety-check`. As of 2026-10-31, SC-1 (`docs/protocol/safety-checks.md`) clears exactly
  two, for a read-only session: the poll and the fetch of a record the console listed. SC-2 (2026-10-31)
  is a conditional GO for building and reviewing a live-control encoder bounded to the fader indices
  0x00, 0x30, 0x31 and eight live key codes, with the first write session E11 on the owner's go; it
  clears nothing for sending until its conditions are met, and it is a NO-GO for the record keys, the
  label and memory writes and the System pair. Nothing else is cleared. The owner implemented SC-2's
  whitelist on 2026-10-31 (`HspOutbound`, `HspReadSession`, `UsbSmartFadeDevice`), sent only by a session
  with `Device:Usb:LiveControlDate` = today on top of the read-only mode; the first safety review (SC-2
  item 10) found changes, fixed the same day; the second pass (re-check, 2026-10-31, evening) found no blocker or
  major left, three minors and three nits open, and cleared nothing: nothing may be sent before SC-2's E11
  conditions are met and the owner gives the go for E11. Claude never writes that code (see "Current state").
  **2026-11-02: the owner instructed that SC-5's extension (every fader index of the three banks, the bounded
  holds, the bank keys, IND 1 / 2, the automatic bank-switch sequence) be implemented by an Opus 5.5 subagent at his
  request, under Claude's coordination, with the `protocol-safety-reviewer` pass before E11b; Claude (Fable) itself
  still writes none of the sending code.**
- Never touch the console bootloader / firmware update path.
- Never retry commands with side effects blindly.
- Golden protocol tests may only use bytes from a committed capture in `docs/protocol/captures/`.

## Source material

`smartsoft-source-materials/` is immutable proprietary research input. Never modify, rename
or copy its contents into source code. Derived extractions go to `analysis/extracted/`
(git-ignored). Hashes and inventory: `docs/source-materials.md`.

## Documentation map

| Topic | Location |
|---|---|
| Source inventory + hashes | `docs/source-materials.md` |
| Hardware facts | `docs/hardware/smartfade-1248.md` |
| Protocol knowledge base (status vocabulary, facts, hypotheses, evidence matrix) | `docs/protocol/` |
| Observed framing, commands, responses, session model (from E2, E3, E3b, E8, E8b, E9, E9b, E12 and E12b; VERIFIED = reproduced behaviour, never clearance to send; the write messages are VERIFIED only as SmartSoft's behaviour where E9b reproduced E9, E12 reproduced E9b or E12b reproduced E12 (E12's step commands and edit writes among them), the rest OBSERVED once, and none is cleared) | `docs/protocol/packet-format.md`, `commands.md`, `responses.md`, `state-model.md` |
| Third-party reference compared with our 1248 evidence (ConsoleLink, SmartFade ML, evidence class 5) | `docs/protocol/consolelink-analysis.md` |
| Capture records, evidence files, capture format | `docs/protocol/captures/`, `tools/protocol-analyzer/` |
| SmartSoft binary analysis (framing S11; write paths S11.15; GUI edits of the patch and the stack / sequences S11.16, INFERRED, each row annotated with E12's and E12b's outcomes) | `docs/protocol/smartsoft-analysis.md` |
| MIDI analysis | `docs/protocol/midi-spec-analysis.md` |
| Experiments, runbooks (E8, E8b, E9, E12), safety rules, capture format | `docs/protocol/reverse-engineering.md` |
| Hardware safety checks: what is cleared for sending, under which conditions | `docs/protocol/safety-checks.md` |
| ASC show file format | `docs/formats/asc-format.md` |
| Architecture (proposal + "Phase 1 as built") | `docs/architecture.md` |
| UI ↔ backend contract (SignalR, REST, DTOs; domain concepts only) | `docs/api.md` |

When protocol knowledge changes, update the relevant docs **in the same change**. Never
silently rewrite an earlier conclusion: mark it REJECTED and record the new evidence.

`docs/` **is** the project knowledge base. There is no separate knowledge-base folder.

## Claude agents and skills (project-local, `.claude/`)

Entry point for any task: skill `/sf` (routes to the items below).

| Need | Use |
|---|---|
| Answer a protocol question from docs, binaries or captures | agent `protocol-researcher` |
| Static analysis of SmartSoft binaries with the rea MCP tools | skill `/analyze-smartsoft` |
| Add a hypothesis with a test | skill `/add-hypothesis` |
| Plan or record an experiment E1..E7 | skill `/record-experiment` |
| Analyze a capture, write the experiment record | agent `capture-analyst` |
| Promote / reject / record a fact with evidence | skill `/protocol-status`, agent `knowledge-base-curator` |
| `.asc` show files | skill `/asc-probe`, agent `asc-format-analyst` |
| Before anything reaches the console | skill `/hardware-safety-check` |
| Review protocol / device code for invented data | agent `protocol-safety-reviewer` |
| Review code against the architecture | agent `architecture-reviewer` |
| Golden protocol test from a committed capture | skill `/golden-test` |

Generic Angular tooling (`/fe`, `fe-*` agents) stays in the user's global `~/.claude`.

## Architecture (summary; see docs/architecture.md)

Angular UI → SignalR/REST → ASP.NET Core (Api) → Application (desired vs actual state) →
`ISmartFadeDevice` → { Simulated device | USB device → Protocol → Communication transport }.
The UI never knows about transports or bytes. The ASC converter is a separate library + CLI
with no hardware dependencies.

## Current state

Phase 0 (discovery) done: source-material audit, plus static disassembly of the USB framing
in `sfEngine.dll` (`docs/protocol/smartsoft-analysis.md` S11, all INFERRED). **E1 done
2026-10-28** (`docs/protocol/captures/2026-10-28-e1-usb-identification.md`): the console is
`VID_14D5&PID_0200`, a full-speed composite device with HID interface 0 and vendor bulk
interface 1; firmware 3.0.2.9.0.43. ETC's `etchsp.sys` does not load while Memory Integrity
(HVCI) is on; the owner switched it off for the E2 session and wants it back on later, so our
backend must use a user-mode driver (WinUSB, H7). **E2 done 2026-10-28**
(`docs/protocol/captures/2026-10-28-e2-smartsoft-connect.md`, first USB capture, passive): the
poll / dirty-list / fetch protocol and its framing are **OBSERVED** (facts F51-F69, documents
`packet-format.md`, `commands.md`, `responses.md`, `state-model.md` in `docs/protocol/`);
SmartSoft wrote only two System messages and never used the HID interface. The cross-check with the console's own `SHOW04.ASC` matched the decoded Patch, Memory, label, step and Independents objects, and SystemSettings by position only (F70-F79). *(Until E3 only the product string and the firmware version, F41 and F48, were VERIFIED.)*
**E3 done 2026-10-29** (`docs/protocol/captures/2026-10-29-e3-console-actions.md`, second USB
capture after a console power cycle, passive; the owner's answers are in
`2026-10-29-e3-owner-statements.txt`): the independent reproduction of E2 promoted facts to
**VERIFIED**, **26** at that time (F41, F48, F51-F55 with F55 in part, F57-F65, F67 for the shared IDs, F68,
F70-F77 as "the console returns these bodies"; 35 after E3b, below); F40 and F42 stay OBSERVED (synthetic descriptor
frames). For F58 VERIFIED means that SmartSoft sends exactly those two System messages at those
points, **never that we may send them**. The console-side actions (faders 1 and 5, a memory bump, the
MASTER fader, a mode switch) were decoded (F80-F92, hypotheses H46-H59), E2's 0xAB is explained by
MASTER = 0xAA (INFERRED cause, F90), and the third-party ConsoleLink for the SmartFade ML (evidence class 5,
`docs/protocol/consolelink-analysis.md`) was compared: it changes no 1248 status by itself, but it
showed that the structural argument for the label byte order (UTF-16BE) also fits a second reading,
so that byte order is downgraded to HYPOTHESIS (F73).
**E3b done 2026-10-30** (`docs/protocol/captures/2026-10-30-e3b-live-objects.md`, third USB capture: three
SmartSoft sessions after another console power cycle, passive; the owner's answers are in
`2026-10-30-e3b-owner-statements.txt`): nine facts first seen in E3 (F80, F82-F87, F89, F92) are now
**VERIFIED in part** (the structural part only, never a live value; F92 like F58 only as SmartSoft's
behaviour), so **35** facts are VERIFIED; F93-F106 were added: the console's sequence counter is not reset by
a SmartSoft restart (H31 CONFIRMED), BUMPS = FaderInfo body[193] (H46), the live level objects are scaled by
the MASTER (H47), a held bump appears in them at the BUMPS level (H54, tested at BUMPS 0x00 and 0xFF only),
ConsoleStatus body[4] is the fader bank, FaderInfo and the LED blocks belong to the physical faders in every
mode and bank, LED block 52 is SOLO (INFERRED), a GO press shows as a PlaybackInfo crossfade (H50 layout) and
the default fade times are in SystemSettings and on the LCD (H45). H57 (an arithmetic formula for the MASTER
scaling) and H59 (FaderInfo records 25-48 = the 25-48 bank) were REJECTED; H60-H65 were added. The owner
explained the crossfade that his log lacked (he pressed GO once, STACK button unlit) and the 5.0 -> 5.1 s
default time (a wheel moved by accident); nothing is plugged into DMX IN (owner, 2026-10-30: only DMX OUT is
connected; H52's content reading stays untested).
**Safety check SC-1 (2026-10-30, `docs/protocol/safety-checks.md`) cleared the poll and the listed fetch for a
read-only session; `SmartFade.Devices.Usb` (WinUSB on interface 1, `Device:Mode=Usb`, UI badge HARDWARE, no
command accepted) is built and replay-tested against E2, E3 and E3b, and the first live session E8 waits for the
owner's go (backup, Zadig on interface 1, USBPcap in parallel; runbook in `reverse-engineering.md`). Nothing else
is cleared: no System write, no payload, no control command.** **Phase 1 is built
(2026-10-30, owner-approved):** .NET 10 solution (`SmartFade.sln`: Domain, Application with
desired-versus-actual state, simulated device, Protocol with decoders and, since SC-1, the poll and
listed-fetch encoder, with golden tests over the committed captures, Communication with a capture
replay and the WinUSB pipe, SignalR/REST API on port 5160) and the
Angular app (`src/frontend`); see `docs/architecture.md` "Phase 1 as built" and
`docs/api.md`. The same day the simulator and UI gained memories (REC MEM / EDIT MEM / COPY, 12
pages × 24), bump buttons, SOLO, the independents IND 1 / IND 2 (DMX 97 / 98 by default) and the
CLEAR key, modelled on the manual (F5, F6, F107–F109; the simulator's own readings are marked
INFERRED), plus the console's record and crossfader columns with the unmodelled keys inactive (since 2026-11-02 the right-hand
columns are FADERS (MEMORIES / 25-48 / 1-24 / NEXT, the bank a real console reports lit from the new `faderBank` state
field; MEMORIES and 1-24 as taps where accepted, 25-48 and NEXT inactive), CROSSFADER (RATE inactive, CLEAR two keys
wide), RECORD, MASTERS and INDEPENDENTS as its own section). Since
SC-1 the only code that can transmit to a console is the read-only USB device, and only the poll and
the listed fetch. Next: the owner questions and passive captures listed in
`docs/protocol/reverse-engineering.md` ("Next capture (proposed by the E3b analysis)": BUMPS at intermediate
levels and SOLO with another output up, several stable MASTER positions, a known DMX source on DMX IN, a page
change, the power-on pending list; optionally GO with the STACK button lit, and the SmartSoft-side "č" label
capture), the zero-risk static checks, and Phase 1 scaffolding
(simulator, decoder against captures) after owner approval.
**E8 done 2026-10-30** (`docs/protocol/captures/2026-10-30-e8-first-read-only-session.md` and its `.jsonl`, the backend's own
transfer log of the first live read-only session of our software: 861 s, 20 763 polls, 2 925 fetches, 0 framing
errors, no fault, stopped at an idle point; the owner's answers in `2026-10-30-e8-owner-statements.txt`). The console
kept reporting live changes (channels, outputs, faders, LEDs, LCD, status, independents, playback) to a host that never sent
SmartSoft's System writes, and tolerated our cadence: **H66, H67 CONFIRMED** for what was used (OBSERVED, one session); no
USBPcap ran, so **H68 stays UNTESTED**. **39 facts are VERIFIED** (F95 and, in part, F99, F100, F101 added; never clearance to
send); F110-F121 were added (F121: sending a show and the System erase commands are destructive paths our code must never use),
H62 was REJECTED and H69-H75 were added. **E8 cleared nothing beyond SC-1.** The owner states that SmartSoft Online changes the console
("both ways"), so **E9, a passive USBPcap capture of SmartSoft writing, is the next experiment and the only path
to a write mode; nothing is cleared for writing.** The static write-path analysis (`docs/protocol/smartsoft-analysis.md`
S11.15, INFERRED): SmartSoft's only live-control message is ConsoleEvent (id 20, from its Console Simulator), GUI
edits are whole-object writes, sending a show replaces the console's show and System (id 39) carries erase
commands, so our code must never send either of the last two; the E9 runbook (`reverse-engineering.md`) scripts the
Simulator actions, a label rename and an optional memory edit. After E8 the owner switched interface 1 back to the
ETC driver. **E10 steps 1 to 5 built 2026-10-31:** `SmartFade.ShowFile.Asc` (reader and writer, 33 of 33 files byte-identical
after a round trip), the `tools/asc-converter` CLI, and the UI's show-file page (page switch in the bar; `POST
/api/showfile/parse`; the patch and the stack / sequence steps editable (fade times, delays, follow-on and referenced
memory, in the forms the console's files use; steps inserted and removed, numbered as the console's files number them, a
memory step repeating its memory's channels, existing numbers never changed; memory channel lists and the labels of memories, channels and sequences editable
within the shapes the files show; `docs/formats/asc-format.md` "Step rules learned from the files" and "Label and memory
rules learned from the files") and saved as a new file through `POST /api/showfile/render`, the original never written; nothing reaches a
console). **The write path (SC-2 classes A and B1) is implemented by the owner at the TODO-marked extension points (commit
dcf48d6), not by Claude: Claude's attempt on 2026-10-31 was stopped by its safety system and must not be repeated; Claude
reviews the owner's code against SC-2 and the captures instead.** **Owner scope decisions (2026-10-30, `reverse-engineering.md` "Owner decisions on scope"):** no wheel
support and no settings edits, ever; a write mode means the live controls plus recording through the
console's own REC MEM / EDIT MEM / COPY as key presses (E9 captures both); E10 is imagined as editor pages
(patch, stack / sequences) with show save / load as `.asc` files on the PC (E6 round trip; the USB show
upload stays closed, F121, the SD card loads). The owner questions and E8b (a second read-only session) are listed under "Next capture (proposed by the E8
analysis, 2026-10-30)" in `reverse-engineering.md`.
**E8b done 2026-10-30, evening** (`docs/protocol/captures/2026-10-30-e8b-second-read-only-session.md`, the second read-only session of
our software: 1 121 s, polls and listed fetches only, with USBPcap from before the Zadig bind, a named backup and a timed action log):
the bus equals the backend's log byte for byte, and **H68 is answered** (EP0 traffic only at the Zadig bind; nothing at the backend start,
at Connect or at Disconnect). **49 facts are VERIFIED** (F110, F112-F116, F119, F120 added, F117 and F118 in part; never clearance to send).
The keys are tied to their LEDs with timed presses (IND 1 = Independents record 1 = LED block 72 = channel 49, IND 2 = record 2 = block 73 =
channel 50 -> DMX 47, BLACKOUT = block 53; H72 CONFIRMED in capture for seven blocks) and BLACKOUT zeroes the level objects; F122-F130 and
H76-H80 were added. The owner's descriptions of PLAY, PAUSE and the crossfaders are statements: no PAUSE press exists in any capture.
**E8b cleared nothing beyond SC-1.** Next: "Next capture (proposed by the E8b analysis, 2026-10-30)" in `reverse-engineering.md`.
**E9 done 2026-10-30, late evening** (`docs/protocol/captures/2026-10-30-e9-smartsoft-writes.md`, a SmartSoft Online session driven through its
Console Simulator and recorded passively; our software sent nothing): the **first observed PC -> console writes**, 401 in all (the System pair,
332 ConsoleEvent fader events, 64 key events, three ChannelText labels). H5 (PC -> console part), H6 (for the paths used) and H24 (kinds 0 and 1)
are CONFIRMED (OBSERVED once); F131-F140 and H81-H86 were added; **53 facts are VERIFIED** (F93, F94, F97, F103 added; never clearance to send).
Live control is the ConsoleEvent message (fader indices 0x00, 0x06, 0x30 = MASTER, 0x31 = BUMPS; 13 key codes, each its LED block number - 1,
H82); the console answers like for the panel and acknowledges nothing; the record functions driven from the PC change show data and are not
reversible by the protocol. **E9 cleared nothing: SC-1's poll and listed fetch remain the only messages cleared, no type-1 data header is encoded
anywhere in the solution, and the repeat E9b plus a safety check SC-2 are the gate for any write.** The owner item on memory 7 of page 1
(changed by E9, F139) was closed 2026-10-31: `SHOW08.ASC` confirmed the change, the owner restored it by hand, and `SHOW09.ASC` equals the
pre-E9 backup `SHOW07.ASC` byte for byte. Next: "Next capture (proposed by the E9 analysis, 2026-10-30)" in `reverse-engineering.md`.
**E9b done 2026-10-31** (`docs/protocol/captures/2026-10-31-e9b-smartsoft-writes-repeat.md`, the repeat of E9: three SmartSoft Online sessions driven through its
Console Simulator after a console power cycle and recorded passively; our software sent nothing): 427 writes (the System pair x 3, 366 ConsoleEvent fader events,
26 key press / release pairs, two ChannelText labels and **one Memory (0) whole-object write** from SmartSoft's Mem Edit tab, a 90-byte body against the console's
own 138), framing errors 0. It reproduced the write framing and the most-used ConsoleEvents: **61 facts are VERIFIED** (F131-F134, F138 and F140 added, F135 and
F136 in part; as SmartSoft's and the console's behaviour, never clearance to send). F141-F147 and H87-H90 were added: the last mover wins between a PC fader and a
physical fader (F141), the console's own EDIT MEM lists System (5, 5, 0) (F142), the console stores a label write verbatim (F144, OBSERVED), REC MEM stores the
post-MASTER level (F145, H85 REJECTED, the simulator changed accordingly) and, with nothing raised, empties the memory (F146, H86 CONFIRMED); fader index 0x04 and
the key codes 0x37 (FADERS 25-48), 0x3B (STACK), 0x47 / 0x48 (IND 1 / IND 2) were seen once each; H84 CONFIRMED, H81's first clause CONFIRMED for channel mode,
H78 contradicted in its prediction (cause UNKNOWN). **E9b cleared nothing: SC-1's poll and listed fetch remain the only messages cleared, no type-1 data header is
encoded anywhere in the solution, and the safety check SC-2 is the gate for any write** (its input: the E9b record's "Input for a future safety check (SC-2),
updated after E9b"). **SC-2 ran the same day** (`docs/protocol/safety-checks.md`): a conditional GO for class A (fader events, indices 0x00 / 0x30 / 0x31
only) and class B1 (eight live key codes: bumps 1 / 7 / 9, GO, SOLO, BLACKOUT, FADERS 1-24, MEMS) to build and review an encoder with golden tests over E9 and
E9b and to run the first write session E11 on the owner's go, under a separate write switch, a typed whitelist, press / release as one operation, no write while
a record function is armed, no retries; NO-GO for the record keys from the PC (SC-3, after the owner's written acceptance per key and a second capture E9c),
for ChannelText and Memory writes and for the System pair; never for the paths of F121. **The owner implemented classes A and B1 the same
day** (the SC-2 "Implementation" section): the first `protocol-safety-reviewer` pass (item 10) found NEEDS CHANGES (one blocker, fixed as tags
the same day; majors 3, 4, 5 fixed the same day, majors 1, 2 and 6 and the minors later that day: the write is chosen after its
slot wait, an undecodable LED / status / FaderInfo report ends a live session, the interlock is replayed over E9 and E9b, the
incomplete-write latch is kept on disk; commit bf1aa4e). **The second pass (re-check) of 2026-10-31, evening, passed:** majors 1, 2 and 6 and the
minors RESOLVED with file-and-line evidence, the 16 armed writes confirmed by the reviewer's own replay, SC-1's read-only session unchanged; three
minors (two untested holds, two untested paths, `ReplyTimeoutMilliseconds` unbounded) and three nits (wording) stay open on the owner's side, to be
fixed or accepted in the SC-2 review table. It cleared nothing: E11 needs SC-2's conditions (backup, rig decision, USBPcap, versions, the written
acceptances incl. the decode-failure release case, the script adjustments) and the owner's go on the day. Next: "Next capture (proposed by the
E9b analysis, 2026-10-31)" in `reverse-engineering.md`, and E9c / E11 as SC-2 lists them.
**E12 run 2026-10-31, afternoon** (a passive capture of SmartSoft Online editing the patch and the stack / sequences: SmartSoft drives, USBPcap
records, our software is not involved; E9c / E9d stay reserved by SC-2 and E11 is the first live-control session of our software; planned the same
day, runbook in `docs/protocol/reverse-engineering.md` "E12 runbook", guided script `tools/usb-probe/e12-guided-session.ps1`): the record
`docs/protocol/captures/2026-10-31-e12-smartsoft-patch-stack-edits.md` with the console-only split, the normalized JSONL, the action log and the
owner's statements; 70 PC -> console writes by SmartSoft, 0 framing errors, three SmartSoft sessions, every edit restored (`SHOW13.ASC` =
`SHOW12.ASC` byte for byte). **The record holds provenance, completeness and structure only: the decoding of those writes against H91-H101 is not
done by Claude (its attempt on 2026-10-31 was stopped by its safety system, like the encoder's, and must not be repeated); the owner or a later
pass evaluates them with the committed analyzer tools, the record names them.** The researcher's static findings stay as recorded
(`docs/protocol/smartsoft-analysis.md` S11.16; H91-H101 HYPOTHESIS, the test run and its outcome not recorded; the DOCUMENTED manual facts
F148-F150): none OBSERVED or VERIFIED, none cleared, and our code never sends any of these messages (SC-2 class F, "Never", for the Patch and
Independents writes; the VERIFIED count stays 61). *(Superseded in part the same evening, text kept: the decoding was then done by the
`capture-analyst` agent on Opus 5.5 at the owner's request, see "E12 analysed" below; the sentence on Claude's own stopped attempt stands.)*
**E12 analysed 2026-10-31, evening**, by the `capture-analyst` agent on Opus 5.5 at the owner's request (read-only tools
`tools/protocol-analyzer/e12/`; the record's section "Analysis"), and applied by the knowledge-base curator: SmartSoft's edits of the patch, the
independents, stack steps, a sequence, pages and memories are whole-object writes in the console's layouts (a type-2 "Direct cue" step excepted:
100 bytes against the console's 148), stored by the console and answered by no listing. Its step commands InsertStep (32), DeleteStep (34),
MoveStep (35) and id 42 carry `[a][b][c]` bodies (a = 48 for the stack, 0 for KOTLIK), the console answers each with a list of the affected
objects, and an insert runs in two phases with the step type in the second; the console also lists its own patch and step-timing menu edits at
every wheel detent. **65 facts are VERIFIED** (F143 and F147 added, F144 and F146 in part; E9b + E12, never clearance to send); F151-F159 and
H102-H106 were added, H91, H93, H97, H99 and H14 CONFIRMED, H92, H94-H96, H101 and H87 in part, H98's sub-reading "the step type travels in
the command" REJECTED (all OBSERVED once). **SC-1 relevance (documentation only; no code changed):** E12 shows list records and object lengths
that would stop a read-only session of ours with a fault, a safe stop (Sequence records with c ≠ 0, a 144-byte Sequence, a 148-byte type-2
StepContent; F156), seen only after SmartSoft's step commands and in its download; a console-side insert or delete is untested (H106). **E12
cleared nothing:** SC-1's poll and listed fetch remain the only messages cleared for a read-only session, SC-2's decisions are unchanged (Patch
and Independents class F, "Never"; Memory class D, NO-GO), the stack, sequence, page and step-command writes are in no class, and our code never
sends any of them. Next: "Next capture (proposed by the E12 analysis, 2026-10-31)" in `reverse-engineering.md` (the owner questions; E12b, the
repeat, whose guided script `tools/usb-probe/e12b-guided-session.ps1` is written and parse-checked, not run). *(Superseded in part, text kept:
E12b was run and analysed the same late evening, see below.)*
**E12b run and analysed 2026-10-31, late evening** (the repeat of E12, passive, after another console power cycle: two SmartSoft sessions, 62
PC -> console writes, an SD save of the edited show made on the console, every edit restored, `SHOW16.ASC` = `SHOW14.ASC`; record
`docs/protocol/captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md`). The analysis was done by the `capture-analyst` agent on Opus 5.5
at the owner's request (read-only tools `tools/protocol-analyzer/e12b/`) and applied by the knowledge-base curator on 2026-11-01. Where E12b
reproduced E12, SmartSoft's step commands, its two-phase insert, the console's lists after them (Sequence records with c = the changed step's
index) and the stored results are VERIFIED as SmartSoft's and the console's behaviour: **72 facts are VERIFIED** (F151, F152, F156, F157 added,
F153, F154, F158 in part; never clearance to send). New and OBSERVED once (F160-F165, H107-H110): a channel step; a labelled State step that the
console stored verbatim and exported as UTF-16BE (H40's byte order CONFIRMED for the console's side); the live stack addressed by index, so a
step command changes what is live or next without a GO; the key codes 0x32 (PAUSE-like) and 0x3A (a toggle of ConsoleStatus body[6]; H104
REJECTED). **The console stopped answering USB for about 0.85 s while it saved the show to its SD card** (one read cancelled, both pipes reset;
F163, H109), so no SD save or load is made on the console while our software is connected (an E11 condition in `safety-checks.md`; our reply
timeout would end the session, a safe stop). **E12b cleared nothing:** SC-1's poll and listed fetch remain the only messages cleared, SC-2's
classes are unchanged, 0x32 and 0x3A are outside its whitelist, and our code never sends any of these messages. Next: "Next capture (proposed
by the E12b analysis, 2026-10-31)" in `reverse-engineering.md` (owner questions, optional passive captures, static checks; no repeat needed).
**E11 attempt 1, 2026-11-01 morning: no write sent** (record `docs/protocol/captures/2026-11-01-e11-first-live-control-session-attempt-1.md`): the owner
ran the guided E11 session with the backend at 5416d7f and live control on; the console, power-cycled earlier and untouched since, listed nothing in
192 s (5 183 polls, idle replies only; the "power-on" pending list of E3b / E8b was not pending after this history, F166 OBSERVED once, H65 annotated),
so SC-2's interlock refused the first control by condition 8 before any transfer, the guide mistook the refusal for the H66 stop, the session ended
at an idle point and `SHOW18.ASC` = `SHOW17.ASC`; the bus equals the backend's logs byte for byte. The guide now makes the console report its state
from the panel before the first control (step W0) and treats a refusal as "not sent". **E11 is not done: its conditions and SC-2's decisions are
unchanged, the console's behaviour towards a PC event from our software is still unobserved, nothing is promoted and nothing is cleared.**
**E11 attempt 2, 2026-11-01 afternoon: the first PC → console writes of our own software** (record
`docs/protocol/captures/2026-11-01-e11-first-live-control-session-attempt-2.md`): with the backend at 85aa2f5 and live control on, the console had
LedStatus + DisplayContent pending at Connect (H65 annotated), the W0 panel actions made it report its state, and channel fader 1 dragged in the UI
sent **four ConsoleEvent fader events** (index 0x00, values 0x05, 0x17, 0x2F, 0x38, counter 0..3, F132's previous-byte chain), each after an idle reply
44–49 ms old (SC-2's 62 ms bound) and 15–30 ms before the next poll; **the console followed each one** like a physical fader move (FaderInfo record 1,
ChannelLevels channel 1, OutputLevels DMX 1 and LedStatus block 1 at the sent value, the LCD's bar glyph once; **F167 OBSERVED once, H66 CONFIRMED in
part for writes**), acknowledged nothing and listed no show object; the interlock held newer values eight times and released each within one poll
exchange; 0 framing errors, 0 USBD errors, the bus equals the backend's log byte for byte (15 351 transfers). The session ended on a mistyped guide
answer ("b" for the followed case), so W1's return to zero and W2–W8 are attempt 3's; the guide now accepts the case letters and confirms before an
H66 stop. **E11 is partly done; SC-2's decisions and conditions are unchanged; nothing further is cleared.** `SHOW20.ASC` = `SHOW19.ASC` byte for
byte (no show data changed); the owner states a power cycle between the attempts (so the Connect-time pair is H65's power-on list; attempt 1's
empty Connects stay unexplained) and that the console kept the PC value on channel 1 after the Disconnect until the physical fader 1 was moved
up and down (F141, a statement). Next: attempt 3 (W1 to zero, W2–W8) on the owner's go.
**E11 attempt 3, 2026-11-01 evening: E11 done** (record `docs/protocol/captures/2026-11-01-e11-first-live-control-session-attempt-3.md`):
with the backend at 5485ea3 and live control on, the guided session ran W1–W8 to the end and sent **116 ConsoleEvents** (channel fader 1 up and
back to zero, bump 7, SOLO, BLACKOUT, BUMPS, FADERS MEMS / memory bump 1 × 3 / FADERS 1-24, MASTER, GO × 2 with STACK unlit: 92 fader events
and 12 press / release pairs 47–77 ms apart), each 27–46 ms after a poll's reply (SC-2's 62 ms bound held 116 of 116), and **the console
followed every control** as it follows the panel and SmartSoft (the level objects, the LED blocks 50 / 52 / 53, the mode objects, the GO
crossfade): F167 → VERIFIED in part (channel fader 1 rising, attempts 2 + 3, our own host), F168 and F169 added, H66 CONFIRMED for writes
(OBSERVED once each); **73 facts are VERIFIED** (never clearance to send). A 47–78 ms bump tap from the PC leaves no trace in the fetched
objects (the console lists them for the press and the release), while the owner's hold of the console's own memory bump 1 showed memory 1 as in
E9 / E9b; 0 framing errors, the bus = the backend's log (49 426 transfers), `SHOW22.ASC` = `SHOW21.ASC`. After the guide the owner ran the
backend twice more by hand (a read-only session; a live-control session with 57 writes: memory bump 1 taps in memory mode, channel fader 1
drags, bump 7 taps, the panel's mode keys) **without USBPcap and after the SD save**: a recorded deviation, log-only evidence, no show data
changeable by those classes; `SHOW23.ASC`, saved afterwards and copied 2026-11-02, = `SHOW22.ASC` byte for byte. The owner's wish to drive memory faders from
the PC without switching the console's mode is answered from the evidence (the record's section; H111 added): the only live-control message
ever observed, ConsoleEvent, addresses the console's physical controls, so the console's fader mode decides what an event drives (F99, F112,
F134); memory faders and bank 25-48 from the PC need E9c (a passive capture of SmartSoft's Simulator in memory mode and in bank 25-48) and a
safety check, and the UI's FADERS 1-24 / MEMS keys are the only cleared way to change the console's bank. **E11 is done; SC-2's decisions
and conditions are unchanged; nothing further is cleared.** **Owner decisions 2026-11-02** (`reverse-engineering.md` "Owner decisions on
scope"): the UI keeps the console's layout (built the same day: the right-hand columns FADERS / CROSSFADER / RECORD / MASTERS +
INDEPENDENTS, the bank a real console reports lit from the new `faderBank` state field, RATE and NEXT inactive); the PC is to drive
memory faders and channels 25-48 through an **automatic bank switch** (the FADERS key sent before the fader event) and **held bumps**
are wanted; the console's state at Connect without touching the panel (SmartSoft's System pair, F58 / F89) is a candidate for a safety
check SC-4, not decided. **E9c done 2026-11-02, 00:41–01:20** (passive, SmartSoft's Console Simulator driven by the owner through
`tools/usb-probe/e9c-guided-session.ps1`; record `docs/protocol/captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md`; our software
not involved): 1 227 SmartSoft writes (the System pair twice, 1 149 fader events, 37 key pairs), 0 framing errors, `SHOW25.ASC` = `SHOW24.ASC`.
**The fader index names the physical fader in every bank and mode** (all 24 indices 0x00–0x17 moved faders 1–24 in bank 1-24; 0x00 / 0x01
moved channels 25 / 26 in bank 25-48 and memory faders 1 / 2 / 3 / 9 of the shown page in memory mode; F171, H111 CONFIRMED in part); **a
bank or page switch does not carry a level across, and a PC fader is ignored until one of its events equals the new content's level**
(the bump LED blinks meanwhile; the level left behind is kept; F172, H79 / H81's matching clause CONFIRMED, H69 in part); **NEXT is key
0x3A** and its blind levels go into the object BlindLevels (18), the live levels untouched (F173, H112 CONFIRMED in part, H108
explained); held bumps up to 2.85 s show and end cleanly (F174); FADERS 25-48, IND 1 / 2, STACK, PAUSE, CLEAR seen a second time (F175;
F165 VERIFIED in part, **74 facts VERIFIED**); a PC fader reads post-MASTER with the MASTER at 0x84 (F176); 24 records were pending at
SmartSoft's first poll without a power cycle and the download holds 158 records (F177; H65 widened; the owner: no power cycle between
E11's attempts 2 and 3 either). Deviations: the Simulator relabels its faders in bank 25-48, so fader 24 and the held bump were done in
bank 1-24 (no bump in bank 25-48); S4b's physical step was first done in the Simulator. **E9c clears nothing: SC-5
(`docs/protocol/safety-checks.md`) decides the classes for the automatic bank switch and held bumps (its rule from F172: after the switch
the first event must equal the target's reported level), with E9d for what E9c saw once;** then the owner's implementation and a live
session E11b. Still open: whether a held bump's lost-release case is accepted in writing.
**E9d done 2026-11-02, 09:47–10:14** (passive, after a console power cycle; the owner accepted the held-bump risk and agreed to E9d the same
morning; record `docs/protocol/captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md`): in two SmartSoft sessions every channel
fader 1–24, FADERS 25-48 with every fader there and one held bump, memory faders 1–24 and memory bumps 1–24 of page 1, bump 7 held up to
4.67 s and E9c's mode-switch case; 2 855 writes, 0 framing errors, `e9d/check.py` 228 steps as expected (channel 47 unpatched in this
show: no DMX), `SHOW27.ASC` = `SHOW26.ASC`. **F171 (the index names the physical fader, every bank and mode) and F174 (held bumps)
VERIFIED, F172 VERIFIED in part (the mode case), H111 CONFIRMED, H79 / H81's mode case CONFIRMED; 77 facts VERIFIED** (never clearance).
**The SC-5 addendum lifts the "after E9d" rows:** every channel fader of both banks and every memory fader of the shown page, the memory
bumps as holds bounded to 3 s and the bump of bank 25-48's first fader are GO (conditional) under SC-5's conditions (the switch sequence,
the bounded hold, one encoder and whitelist with golden tests over E9c and E9d); **the channel bumps 2–6, 8, 9, 11–24 and 26–48 are GO (conditional) on the
physical-control rule by addendum 2** (their codes VERIFIED as memory bumps; the rule held in 100 of 100 fader and bump cases; residual
risk a wrong channel flashing, live and visible), with **E9e cut to a four-bump spot check per session** (`tools/usb-probe/e9e-guided-session.ps1`,
about a minute each) as the control, which the owner may decline in writing: the first E9e script asked for 96 presses and the owner
refused a second long repetitive session, with reason (the omission was the analyst's); STACK, PAUSE, NEXT, CLEAR stay NO-GO.
**E9e run 2026-11-02, 10:57–11:06** (record `docs/protocol/captures/2026-11-02-e9e-smartsoft-channel-bumps-spot-check.md`): the four bumps
(2, 12, 24 of bank 1-24; the one labelled 48 of bank 25-48) lit their own channel in both sessions, 0 framing errors, `SHOW29.ASC` =
`SHOW28.ASC`: the control of addendum 2 passed, nothing changes. **All passive captures for the automatic bank switch and the holds are
done; the next step is the owner's implementation at the extension points, then the reviewer, then E11b.** The UI's
SOLO, BLACKOUT, IND 1 / 2 keys took the shared key look the same day. **Nothing is sent before the owner implements the switch sequence and
the holds at the extension points, the reviewer passes them and E11b runs with USBPcap.**
**SC-5 implemented 2026-11-02, midday (commit 98cb499), by an Opus 5.5 subagent at the owner's request, Claude coordinating and
writing none of it** (`safety-checks.md` SC-5 "Implementation" lists what was built, the golden tests over E9c / E9d / E9e and nine
readings where SC-5's text was narrowed or extended, for the reviewer; the run line is unchanged, "Commands" below): the whitelist as
explicit tables for the 24 physical faders and bumps in the three banks (F171), FADERS 25-48 (`pressFaders25To48`), IND 1 / 2, holds
bounded to 3 s and released before any Disconnect, the automatic bank switch as one device-layer sequence (the FADERS key as a tap,
every write but a release held until the console reports the bank within 2 s, else the session ends) and a fader's first event at its
reported level (F172); 421 backend and 304 frontend tests. **SC-5 addendum 3** clears channel bump 1 of bank 1-24, which the addendum
rows had left out (the analyst's omission; its evidence is stronger than the row's). The E11b guide
`tools/usb-probe/e11b-guided-session.ps1` is written and parse-checked, not run (the SC-5 gate and acceptances; W0 from the panel now
includes a move of channel fader 1, because the implementation refuses a channel fader until the console has listed ChannelLevels and
E11 attempt 3's W0 never made it; W1–W10 as "Conditions for E11b" in `safety-checks.md`; its row in the experiments table of
`reverse-engineering.md`). **The first `protocol-safety-reviewer` pass (SC-5 item 10, 2026-11-02, over 98cb499) was NEEDS CHANGES:** one
blocker (unmarked protocol readings in the matching-level comments), three majors (MAJOR 1: a held bump's level is the "reported level"
a matching event would carry, F174; MAJOR 2: a log note between the gate and the Data header; MAJOR 3: the console's unmatched blink
not shown), five minors, five nits; every byte from the whitelist, nothing NO-GO reachable, the replays reproduced by the reviewer's
own port. **The Opus subagent fixed its items the same afternoon** (a `BumpShadow` holds a fader's events from our own bump press until
FaderInfo and ChannelLevels were delivered after the release; the notes are written after the payload; `BlinkingFaders`, a display-only
mirror of the LED blocks whose phases differ, reaches the UI; an explicit 48-row channel table; bumps need FaderInfo; 428 backend and
308 frontend tests), the owner accepted the hold overrun with a slow console (MINOR 2, chat) and left the panel-bump case (MAJOR 1 (ii))
to Claude, which had the `capture-analyst` (Opus 5.5) compare a held bump against a fader at full in every object of ten captures: **no
object marks a press (F178, VERIFIED)**, so the case is accepted as a residual risk at the owner's delegation, recorded in the SC-5
review table and acknowledged at the E11b gate. **The re-check (second pass) resolved every first-pass item and found one new major:
during a BLACKOUT every fader reports 0x00 with the MASTER at full (F125, OBSERVED once; Claude had written VERIFIED, the subagent
corrected it), so a matching event would carry 0x00; the subagent made the device refuse channel and memory fader events while the
BLACKOUT LED (block 53) is not steady off and from a BLACKOUT tap of ours until the LED is reported (1eceabb → 0f2f1da; also the
transfer-log notes only at exchange ends, and a held bump of ours under SOLO shadows every fader, INFERRED from F6). The third pass
(2026-11-02, afternoon) is a PASS: nothing on the review side is open before E11b, one optional nit (the blackout residual, a
never-seen two-list split, worded for both directions; the hardening a follow-up), 433 backend and 308 frontend tests. The review
clears nothing for sending.** **What remains is the owner's:** the SD backup, USBPcap before the Zadig bind, the versions, the seven
acceptances and the rig answer at the E11b gate, no SD save or load while connected, and the go on the day; then E11b with
`tools/usb-probe/e11b-guided-session.ps1`, its record, and the SC-5 review table annotated with the outcome.
**E11b done 2026-11-02, 14:53–15:17** (record `docs/protocol/captures/2026-11-02-e11b-second-live-control-session.md` with the
console-only split, the normalized bus, the backend's log, the guide's log and the owner's statements): the backend at 4aa02b2 with
live control on, every acceptance answered y, the console power-cycled before, backup `SHOW30.ASC`; W0–W10 run to the end and
**117 ConsoleEvents sent** (93 fader events, 12 press / release pairs), each 25–51 ms after a poll's reply (SC-2's 62 ms bound 117 of
117), and **the console followed every control**: the automatic bank switch four times for a control and twice from the UI's keys
(the bank key as a tap, ConsoleStatus and the LEDs reported, the switch done 129–176 ms after the key, the matching event at the
reported level, then the drag applied to the new bank's content: channel 30 in bank 25-48, memory fader 3 in memory mode; **F179
OBSERVED once**), the five matching events at 0x00 listing nothing (**F180 OBSERVED once**), memory bump 1 and bump 7 held 2.48 and
2.26 s and released by the UI, IND 1 / IND 2 (DMX 47 for IND 2), the MASTER to half and back; 0 framing errors, the bus = the
backend's log byte for byte (51 357 transfers), `SHOW31.ASC` = `SHOW30.ASC`. F167, F168, F169 VERIFIED in part (two sessions of
our host), F171 / F174 / F175 / F115 annotated for our host, H66 CONFIRMED for SC-5's classes. Deviation (owner): two panel presses
(FADERS MEMS, FADERS 1-24) before W5 because the UI's memory hint ("Dimmed memories were not delivered") read as "not usable";
no data changed by it; a UI follow-up. **E11b clears nothing; SC-5's decisions stand.** Next: the follow-ups (the memory hint, the
optional blackout hardening), and any later live session with USBPcap makes F179 / F180 VERIFIED. **Five later backend sessions by
the owner (15:39–15:52, 183 writes, no USBPcap; he declined a further SD save: nothing changed, stated)** are recorded in the E11b record as log-only evidence
(`.backend-2` … `.backend-6.jsonl`): the matching event at a non-zero reported level followed by the drag (F172 annotated), and the
bump-shadow refusal after a bump that changed nothing (a fader at full): a usability defect with a proposed rule, to be implemented
by the subagent and reviewed before use. **The owner's feedback after playing (2026-11-02, evening)** is answered in
`reverse-engineering.md` "Owner decisions on scope": no panel action at Connect → **SC-4** (the System pair; research started, nothing
sent before the check); CLEAR and the record keys stay NO-GO (SC-5; SC-3 after his written acceptance per key); GO is cleared from
channel mode only (an addendum or a one-minute passive observation would lift it); the patch and stack editors are the UI's "Show file"
page (a discoverability follow-up). **The SC-4 groundwork was applied the same evening** (the `protocol-researcher` and the
`knowledge-base-curator`): F181–F186 (the pair byte-identical in 20 of 20 sessions, the console's version listing and the 157–161-record
download after it, nothing changed by it, the states it was sent in, what our sessions never receive without it), H113–H116,
`smartsoft-analysis.md` S11.17 (the countdowns, the version gate, every System value on the wire), `commands.md` "The System message";
**84 facts VERIFIED**; the pair never delivers FaderInfo, so the MASTER still needs one panel move per session under SC-2's condition. **The editor pages were rebuilt the same evening at the owner's request** (a general-purpose subagent, frontend only):
three views in the bar, LIVE / PATCH / STACK-SEQ, a Show menu (load from the card, save to the card, revert) instead of the show-file
page, one `ShowStore` with the edit rules ported (`show-rules.ts`), PATCH as a 32 × 16 matrix of the 512 DMX addresses (address on
top, channel below, IND 1 / IND 2 = channels 49 / 50 as targets), STACK / SEQ as a step list with a detail panel; the file stays the
only way out until a safety check clears the whole-object writes; 340 frontend tests. **SC-4 is drafted** in `safety-checks.md` as
a conditional decision for the owner (the two payloads once per Connect, the version gate, the download taken whole with no control
during it, a per-day switch `Device:Usb:StartupPairDate`, E13 as the first session); nothing is built or sent before his acceptances.
**The bump shadow's end rule was replaced at his request** (the Opus subagent; a bump that changed nothing frees its fader after two
quiet replies; the reviewer's fourth pass PASS, its minor and nits fixed and confirmed the same evening, 0909c84: the press waits for the
fader's current report; 442 backend tests). The Show menu's "Save to card" became "Save as file…" (the PC cannot write the console's
card); the owner's wish that an opened file replace the console's show while connected is SmartSoft's "send show" (F121, never
captured): a path of its own (E14 passive first), offered, not decided. **His answers later that evening** (`reverse-engineering.md`
"Owner decisions on scope"): "1. yes to SC-4"; a loaded file is to replace the console's show while connected (SmartSoft's "send
show", F121: wanted, so E14, a passive capture, then SC-7 with his written acceptance); every record key but SNAPSHOT from the UI
(SC-3 after E15, a passive capture of the four record flows from the Simulator). **SC-4 implemented 2026-11-02, evening (f282aa0),
by an Opus 5.5 agent at the owner's request, Claude writing none of it** (`safety-checks.md` SC-4 "Implementation": the two System
bodies as a typed whitelist, every other value unrepresentable; the placement at reply headers 40 / 79 and the version gate; the
download taken whole, its bounds raised only with the switch and replayed byte for byte over all 20 captured downloads; every control
refused until its last record; the console's state and show assembled for the UI, `GET /api/console/show`; the MASTER probe; the badge
STATE AT CONNECT). **Clause 4 amended the same late evening (d002705, the same agent, on the owner's "continue"):** the probe takes an
unpatched channel of either channel bank and switches there and back by SC-5's automatic switch (each transition one a committed capture
shows a PC host making, 60 presses; memory mode routed through bank 1-24; only from memory page 1; every control refused while it runs;
in the owner's show channel 47 = bump 23 of bank 25-48), because his show patches every channel of bank 1-24; 534 backend and 356
frontend tests. **Claude's own attempt to write the E14 / E15 guides was stopped by its safety system (f9c2802, not repeated); the E13,
E14 and E15 guided scripts were written by an Opus 5.5 agent on the owner's "continue" (d91bfa4; parse-checked, not run;
`tools/usb-probe/README.md` and the experiments table).** **The first `protocol-safety-reviewer` pass over SC-4 (item 10, the same
late evening, over f282aa0 + d002705) is NEEDS CHANGES:** no blocker; one major (the pair is still sent when the console already
reported a memory page other than 1, a state F185 never saw and "What SC-4 does not clear" excludes: the proposed fix refuses it);
three minors (the UI silent when the probe stops away from home with FaderInfo known; a window between the download's last record and
the probe's start in which a SOLO / BLACKOUT tap could be queued ahead of it; the probe's F112 residual, a bump on a physical fader
still holding a memory, to be named in the owner's acceptance); eight nits; every byte from the captures or SC-5's whitelist, no other
System value representable (a 2^32 sweep), the 20 downloads replayed byte for byte by the reviewer's own model, SC-1 / SC-2 / SC-5
sessions unchanged without the switch (`safety-checks.md` SC-4 "Review"). **The owner paused for the night before the report arrived:
the fixes (by the Opus 5.5 subagent), the decisions on MAJOR 1's alternative and MINOR 3, and the re-check wait for his "continue";
nothing is cleared and nothing is sent before a PASS is recorded there and he gives the go for E13 on the day; the E13 guide refuses
to start until then.**

## Commands

Source audit tooling (Python 3.10+; on the owner's PC use `py -3.13`, the bare `python` is
the Store stub). Read-only USB probes for E1 live in `tools/usb-probe/` (PowerShell, see its
README).

```
pip install -r tools/source-audit/requirements.txt
python tools/source-audit/extract_smartsoft_installer.py "smartsoft-source-materials/SmartSoft 3.0.2.9.0.43b.exe"
python tools/source-audit/pe_report.py analysis/extracted/SmartSoft_3.0.2.9.0.43b/files/sfEngine.dll
python tools/source-audit/strings_dump.py <file> > strings.txt
python -I tools/source-audit/x86_probe.py <pe> xref "Fetch object: %s (%d) %d %d %d"   # string xrefs
python -I tools/source-audit/x86_probe.py <pe> func 0x1001AB00                        # annotated disassembly
```

`x86_probe.py` (pefile + capstone, heuristic function boundaries) is the fallback when no
REA analysis backend (Ghidra / IDA) is available on the machine. Prefer REA when it works.

Backend (.NET 10, SDK pinned by `global.json`, warnings are errors):

```
dotnet build SmartFade.sln                       # restore + build every project
dotnet test SmartFade.sln                        # xUnit; the Protocol golden tests read docs/protocol/captures/*.jsonl
dotnet run --project src/backend/SmartFade.Api   # http://localhost:5160, simulated console (mock mode)
```

ASC show files (library `src/backend/SmartFade.ShowFile.Asc`, no hardware dependency; its tests also round-trip
every show in the git-ignored `smartfade-sd-card-backup/` when that folder exists):

```
dotnet run --project tools/asc-converter -- inspect "smartsoft-source-materials/example lighting output file - Ostrov - 16.09.2026.asc"
dotnet run --project tools/asc-converter -- roundtrip <file.asc>      # exit 0 = read and render give the same bytes
dotnet run --project tools/asc-converter -- render <file.asc> <new.asc>   # never overwrites
```

Frontend (Angular 22, Node 22):

```
cd src/frontend
npm install
npm start          # http://localhost:4200, proxies /api and /hubs to http://localhost:5160
npm test           # Vitest, headless, no backend needed
npm run build
```

Mock mode is the default: `Device:Mode=Simulated`. Simulator settings:
`Device:Simulation:CommandLatencyMilliseconds`, `ConnectDelayMilliseconds`, `FailCommands`.
**Real-hardware mode (read-only, SC-1):**
`dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=yyyy-MM-dd`
(today's date, or the backend refuses to start: the mode is confirmed per day, never left on) sends
only the cleared poll and listed fetch over WinUSB on the console's interface 1 (bound with Zadig
by the owner), logs every transfer to `Device:Usb:CaptureLogDirectory` (outside the repository by
default) and shows HARDWARE in the UI; it is never the default, any other `Device:Mode` value
refuses to start, and it is run only as the E8 runbook says (backup first). The golden tests locate the captures by walking up to
`SmartFade.sln`, so run them from a checkout. Port 5000 is taken by another application on the
owner's PC.
**Live control (SC-2 classes A and B1, the owner's implementation; not before the re-check and E11's go):**
`dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=yyyy-MM-dd --Device:Usb:LiveControlDate=yyyy-MM-dd`
(both dates today, or the backend refuses to start; set without `Device:Mode=Usb`, or with one of the five cadence settings
(poll interval, poll after fetch, fetch delay, poll after write, write interval) or the reply timeout slower than its default,
it refuses too) adds the whitelisted
ConsoleEvents (channel fader 1, MASTER, BUMPS, bump 7, memory bump 1, SOLO, BLACKOUT, and since 2026-10-31 evening the key
taps GO, FADERS MEMS and FADERS 1-24 through the port, the API and the UI: `pressGo`, `pressFadersMems`, `pressFaders1To24`,
`not_supported` on the simulator) behind the record-function
interlock, shows LIVE CONTROL beside HARDWARE and names the writable controls to the UI; everything else stays as
in the read-only mode. Run only as the SC-2 E11 script says (backup first, USBPcap in parallel; the guided script
`tools/usb-probe/e11-guided-session.ps1` refuses to start it before the re-check's items are closed and the acceptances given).
**SC-5's classes (implemented 2026-11-02 by an Opus 5.5 agent at the owner's request, commit 98cb499 with the review fixes 1eceabb and
0f2f1da; the `protocol-safety-reviewer` passed on its third pass the same afternoon, E11b's go pending; `safety-checks.md` SC-5
"Implementation" and "Review"):** the same switch also sends every channel fader and bump 1-48, the memory
faders and bumps of the page the console shows, IND 1 / IND 2 and the key tap FADERS 25-48 (`pressFaders25To48`), a bump held until the UI releases it or 2.75 s after the press on the
device's clock (later only with a slow console, logged); a
control of another bank first sends that bank's FADERS key and waits for the console's report (2 s, else the session ends), and a fader's first
event carries its reported level. After a Data write
that did not complete, the device refuses to connect until the backend restarts and `INCOMPLETE-WRITE.txt` in
`Device:Usb:CaptureLogDirectory` is removed by the owner, after reading the transfer log.
**State at Connect (SC-4 class E, implemented 2026-11-02, evening, by an Opus 5.5 agent at the owner's request; not before the
`protocol-safety-reviewer` pass and E13's go):** add `--Device:Usb:StartupPairDate=yyyy-MM-dd` to either run line above (today, or
the backend refuses to start; without `Device:Mode=Usb` too): every Connect then sends SmartSoft's start-up pair (System `09 00 00`,
then `07 00 00`) once, where SmartSoft sends it, unless the console already reported NEXT, BLACKOUT, a record function armed or bank
25-48, and downloads the console's state and show (STATE AT CONNECT in the UI); every control is refused until the download's last
record, then, with live control on, the MASTER probe: one bump tap of an unpatched channel of either channel bank, through SC-5's
automatic switch there and back where that channel's bank is not the shown one, every control refused while it runs (in the owner's
show bump 23 of bank 25-48, channel 47; `safety-checks.md` SC-4 "Implementation" and clause 4's amendment of 2026-11-02, late
evening). The reviewer's first pass over SC-4 was NEEDS CHANGES (2026-11-02, late evening; one major, the fixes pending): not
before a PASS and E13's go.

## Working style

Inspect → identify unknowns → state assumptions → smallest implementation → tests → docs.
Small, logical commits (`feat:`, `test:`, `docs:`). Never commit speculative protocol code
as if confirmed. Keep it simple: one local process, no distributed-system machinery.
