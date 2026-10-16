---
name: record-experiment
description: >
  Plan or record a hardware experiment (E1 USB identification, E2 SmartSoft capture, E3 single-action
  captures, E4/E5 MIDI, E6 ASC round-trip, E7 DMX) for the SmartFade 1248. Use when the user says
  "prepare E1", "I ran the capture", "here is the pnputil output", "write the experiment record",
  "what do I need for E2", "checklist for the capture session".
---

# Plan or record an experiment

Experiments are defined in `docs/protocol/reverse-engineering.md` (table E1..E7, safety rules,
capture format, hardware evidence requested). Records live in `docs/protocol/captures/` using the
template in its README.

## Planning mode (before the session)

Produce a one-page checklist for the owner:

1. Experiment ID, what it answers (hypotheses), risk level.
2. Safety rules that apply: show backed up to SD, safe rig / DMX monitored, passive before active,
   never the firmware path, SmartSoft on a spare PC/VM preferred.
3. Materials: cable/adapter, software (USBTreeView, Wireshark + USBPcap, MIDI monitor), console mode.
4. Exact steps with the action log format (`mm:ss action`) so notes can be correlated with frames.
5. What to send back: files, screenshots, console firmware version from Menu > Diagnostics,
   SmartSoft version, OS.

## Recording mode (after the session)

1. Store raw files unmodified in `docs/protocol/captures/YYYY-MM-DD-<slug>.<ext>`.
   Record SHA-256 of each.
2. Create `docs/protocol/captures/YYYY-MM-DD-<slug>.md` from the template:
   date, experiment ID, console serial/firmware, host OS/SmartSoft/driver, connection, backup
   taken, action log, observed result, conclusions with status changes.
3. For captures, hand over to the `capture-analyst` agent for normalization to `.jsonl`
   and analysis. For E1 text output (Device Manager, `pnputil`, USBTreeView), transcribe the
   relevant values (VID, PID, interfaces, endpoints, strings, bcdDevice) into the record.
4. Apply status changes through `/protocol-status`. Hardware-derived facts are OBSERVED on first
   sight; reproduce or get owner confirmation before VERIFIED.
5. Update `docs/protocol/captures/README.md` (remove "No captures yet" once the first exists) and
   the "Hardware evidence requested" list in `docs/protocol/reverse-engineering.md`.

## Never

- Suggest sending anything to the console that is not covered by the experiment's defined steps.
- Reorder experiments so an active test precedes its passive prerequisite (E5 needs E4, E3 needs E2).
