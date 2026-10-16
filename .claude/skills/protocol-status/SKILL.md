---
name: protocol-status
description: >
  Record, promote, downgrade or reject a SmartFade 1248 protocol fact or hypothesis in
  docs/protocol/ with evidence. Use when the user says "mark H3 confirmed", "add a known fact",
  "this is now verified", "reject hypothesis", "downgrade to hypothesis", "record evidence",
  "update the evidence matrix". Enforces the status vocabulary and the no-silent-rewrite rule.
---

# Protocol status change

## Vocabulary (docs/protocol/README.md)

| Status | Requires |
|---|---|
| VERIFIED | Reproduced on *our* SmartFade 1248; capture file + seq or experiment record in `docs/protocol/captures/` |
| OBSERVED | Seen once in a capture / on hardware; capture reference |
| DOCUMENTED | Official ETC document: name, version, page/section |
| INFERRED | Static analysis or data pattern: file, offset/line, tool |
| HYPOTHESIS | A theory with a concrete test (experiment ID) |
| UNKNOWN | Nothing known |
| REJECTED | Previous entry proven wrong; old text kept, new evidence added |

## Procedure

1. Collect the evidence first. Required fields: source (file / capture + seq / document + page),
   date, console firmware version (if hardware), SmartSoft version (if SmartSoft involved).
   **No evidence, no status change.** Offer to record it as HYPOTHESIS instead.
2. Open the affected entries:
   - `docs/protocol/hypotheses.md` (H-IDs)
   - `docs/protocol/known-facts.md` (F-IDs: hardware F1+, link F10+, MIDI F20+, files F30+)
   - `docs/protocol/evidence-matrix.md` (feature × source grid)
   - `docs/protocol/capability-matrix.md` if a capability changed
   - `docs/formats/asc-format.md` for H10..H14
3. Apply:
   - **Confirm a hypothesis**: hypotheses.md status → CONFIRMED (keep row), add fact in known-facts.md
     with evidence, update evidence-matrix row.
   - **Reject**: status → REJECTED, keep the text, add "Rejected because: … (evidence, date)". Move
     facts to the "Rejected / superseded" section of known-facts.md.
   - **New fact**: next free F-ID in the right group, Status, Source.
   - **Promote to VERIFIED**: only with a capture/experiment record; increment the verified count
     in known-facts.md.
4. Update "Last updated" in known-facts.md, and the CLAUDE.md "Current state" section if the phase
   changed.
5. Show the diff summary: file, entry, old status, new status, evidence.

## Never

- Delete a previous conclusion.
- Mark VERIFIED from documentation alone ("documented" is not "verified").
- Add bytes, IDs or timings that do not appear in the cited evidence.
