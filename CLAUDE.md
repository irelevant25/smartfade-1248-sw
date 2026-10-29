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
  `/hardware-safety-check`. As of 2026-10-29 none is cleared.
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
| Observed framing, commands, responses, session model (from E2 and E3; VERIFIED = reproduced behaviour, never clearance to send) | `docs/protocol/packet-format.md`, `commands.md`, `responses.md`, `state-model.md` |
| Third-party reference compared with our 1248 evidence (ConsoleLink, SmartFade ML, evidence class 5) | `docs/protocol/consolelink-analysis.md` |
| Capture records, evidence files, capture format | `docs/protocol/captures/`, `tools/protocol-analyzer/` |
| SmartSoft binary analysis | `docs/protocol/smartsoft-analysis.md` |
| MIDI analysis | `docs/protocol/midi-spec-analysis.md` |
| Experiments, safety rules, capture format | `docs/protocol/reverse-engineering.md` |
| ASC show file format | `docs/formats/asc-format.md` |
| Architecture proposal | `docs/architecture.md` |

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
**VERIFIED**, now **26** (F41, F48, F51-F55 with F55 in part, F57-F65, F67 for the shared IDs, F68,
F70-F77 as "the console returns these bodies"); F40 and F42 stay OBSERVED (synthetic descriptor
frames). For F58 VERIFIED means that SmartSoft sends exactly those two System messages at those
points, **never that we may send them**. The console-side actions (faders 1 and 5, a memory bump, the
MASTER fader, a mode switch) were decoded (F80-F92, hypotheses H46-H59), E2's 0xAB is explained by
MASTER = 0xAA (INFERRED cause, F90), and the third-party ConsoleLink for the SmartFade ML (evidence class 5,
`docs/protocol/consolelink-analysis.md`) was compared: it changes no 1248 status by itself, but it
showed that the structural argument for the label byte order (UTF-16BE) also fits a second reading,
so that byte order is downgraded to HYPOTHESIS (F73). **No protocol message is cleared for sending
(`/hardware-safety-check` has not passed), so no code may send anything to a console yet.** **No
application code exists yet.** Next: the owner questions and passive captures listed in
`docs/protocol/reverse-engineering.md` ("Next capture (proposed by the E3 analysis)": H31 restart test,
a channel at full during a MASTER move, the BUMPS fader and bumps, the 25-48 fader mode, optionally GO,
and the SmartSoft-side "č" label capture), the zero-risk static checks, and Phase 1 scaffolding
(simulator, decoder against captures) after owner approval.

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

Build / test / run commands for the backend, frontend, mock mode and real-hardware mode
are added here once Phase 1 is scaffolded.

## Working style

Inspect → identify unknowns → state assumptions → smallest implementation → tests → docs.
Small, logical commits (`feat:`, `test:`, `docs:`). Never commit speculative protocol code
as if confirmed. Keep it simple: one local process, no distributed-system machinery.
