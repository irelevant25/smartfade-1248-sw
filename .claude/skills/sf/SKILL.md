---
name: sf
description: >
  SmartFade 1248 project orchestrator. Main entry point for any task in this repository: protocol
  research, captures, experiments, .asc show files, knowledge-base updates, safety checks, backend or
  frontend work. Trigger on: "research", "investigate", "capture", "experiment", "hypothesis",
  "asc", "show file", "safety", "golden test", "update docs", "scaffold the backend", "SmartFade",
  "SmartSoft", "console". Reads CLAUDE.md and routes to the right skill or agent.
---

# SmartFade 1248 orchestrator

This project controls physical lighting hardware. Wrong bytes can blackout a show or corrupt show
data. Every route below preserves two invariants: **never invent protocol behaviour** and
**every protocol claim carries a status** (VERIFIED / OBSERVED / DOCUMENTED / INFERRED /
HYPOTHESIS / UNKNOWN).

## Step 1: Read context

1. `CLAUDE.md` (rules, documentation map, current phase).
2. The knowledge base is `docs/`. Start at `docs/protocol/README.md` for protocol work,
   `docs/formats/asc-format.md` for show files, `docs/architecture.md` for application code,
   `docs/protocol/reverse-engineering.md` for experiments and safety rules.

## Step 2: Route

### Protocol research and evidence

| User wants | Use |
|---|---|
| Answer a protocol question from docs / binaries / captures | **Agent `protocol-researcher`** |
| Analyze a USB/MIDI capture, write an experiment record | **Agent `capture-analyst`** |
| Plan or record an experiment (E1..E7) | **Skill `/record-experiment`** |
| Add a hypothesis with a test plan | **Skill `/add-hypothesis`** |
| Promote / reject / record a fact with evidence | **Skill `/protocol-status`** or **Agent `knowledge-base-curator`** |
| Static analysis of SmartSoft binaries | **Skill `/analyze-smartsoft`** (then `protocol-researcher` for interpretation) |

### Show files

| User wants | Use |
|---|---|
| Analyze or diff `.asc` files, E6 round-trip probes | **Agent `asc-format-analyst`** or **Skill `/asc-probe`** |
| Design the ASC parser / converter | `asc-format-analyst` for the spec, then normal implementation with tests |

### Safety and review

| User wants | Use |
|---|---|
| Anything before real-hardware mode or sending bytes | **Skill `/hardware-safety-check`** |
| Review protocol/device code or docs for invented data | **Agent `protocol-safety-reviewer`** |
| Review backend/frontend against the architecture | **Agent `architecture-reviewer`** |
| Write a golden protocol test | **Skill `/golden-test`** |

### Application code (Phase 1 onwards)

| User wants | Use |
|---|---|
| Angular UI work | Skill `/fe` and the `fe-*` agents (generic Angular tooling from `~/.claude`) |
| .NET backend work | Normal implementation following `docs/architecture.md`; finish with `architecture-reviewer` |

## Step 3: Finish every task the same way

1. Docs updated in the same change (`docs/protocol/`, `docs/formats/`, `docs/architecture.md`, CLAUDE.md).
2. Protocol assumptions marked in code comments and docs.
3. Small conventional commit (`feat:`, `test:`, `docs:`, `chore:`), only when the user asked to commit.

## Current phase

Phase 0 (discovery) is done. Next: hardware evidence **E1** (USB identification, read-only) and
**E2** (SmartSoft session capture), then Phase 1 scaffolding after owner approval. Do not start
Phase 1 code without that approval.
