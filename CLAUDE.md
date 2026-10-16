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

Phase 0 (discovery) done: source-material audit. **No application code exists yet.**
Next: hardware evidence (experiments E1/E2), then Phase 1 scaffolding after owner approval.

## Commands

Source audit tooling (Python 3.10+):

```
pip install -r tools/source-audit/requirements.txt
python tools/source-audit/extract_smartsoft_installer.py "smartsoft-source-materials/SmartSoft 3.0.2.9.0.43b.exe"
python tools/source-audit/pe_report.py analysis/extracted/SmartSoft_3.0.2.9.0.43b/files/sfEngine.dll
python tools/source-audit/strings_dump.py <file> > strings.txt
```

Build / test / run commands for the backend, frontend, mock mode and real-hardware mode
are added here once Phase 1 is scaffolded.

## Working style

Inspect → identify unknowns → state assumptions → smallest implementation → tests → docs.
Small, logical commits (`feat:`, `test:`, `docs:`). Never commit speculative protocol code
as if confirmed. Keep it simple: one local process, no distributed-system machinery.
