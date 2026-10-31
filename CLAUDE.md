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
  label and memory writes and the System pair. Nothing else is cleared, and no type-1 header is
  encoded anywhere in the solution yet.
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
| Observed framing, commands, responses, session model (from E2, E3, E3b, E8, E8b, E9 and E9b; VERIFIED = reproduced behaviour, never clearance to send; the write messages are VERIFIED only as SmartSoft's behaviour where E9b reproduced E9, the rest OBSERVED once, and none is cleared) | `docs/protocol/packet-format.md`, `commands.md`, `responses.md`, `state-model.md` |
| Third-party reference compared with our 1248 evidence (ConsoleLink, SmartFade ML, evidence class 5) | `docs/protocol/consolelink-analysis.md` |
| Capture records, evidence files, capture format | `docs/protocol/captures/`, `tools/protocol-analyzer/` |
| SmartSoft binary analysis | `docs/protocol/smartsoft-analysis.md` |
| MIDI analysis | `docs/protocol/midi-spec-analysis.md` |
| Experiments, safety rules, capture format | `docs/protocol/reverse-engineering.md` |
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
INFERRED), plus the console's record and crossfader columns with the unmodelled keys inactive. Since
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
ETC driver. **E10 steps 1 to 4 built 2026-10-31:** `SmartFade.ShowFile.Asc` (reader and writer, 33 of 33 files byte-identical
after a round trip), the `tools/asc-converter` CLI, and the UI's show-file page (page switch in the bar; `POST
/api/showfile/parse`; the patch and the stack / sequence steps editable (fade times, delays, follow-on and referenced
memory, in the forms the console's files use; steps inserted and removed, numbered as the console's files number them, a
memory step repeating its memory's channels, existing numbers never changed; `docs/formats/asc-format.md` "Step rules learned
from the files") and saved as a new file through `POST /api/showfile/render`, the original never written; nothing reaches a
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
for ChannelText and Memory writes and for the System pair; never for the paths of F121. Nothing is encoded or sent yet. Next: "Next capture (proposed by the
E9b analysis, 2026-10-31)" in `reverse-engineering.md`, and E9c / E11 as SC-2 lists them.

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

## Working style

Inspect → identify unknowns → state assumptions → smallest implementation → tests → docs.
Small, logical commits (`feat:`, `test:`, `docs:`). Never commit speculative protocol code
as if confirmed. Keep it simple: one local process, no distributed-system machinery.
