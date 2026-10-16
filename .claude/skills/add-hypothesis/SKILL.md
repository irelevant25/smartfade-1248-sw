---
name: add-hypothesis
description: >
  Add a new protocol or file-format hypothesis to docs/protocol/hypotheses.md with evidence and a
  concrete test. Use when the user says "add a hypothesis", "I think the console does X",
  "we should test whether", "new theory about", or when research produces a claim that cannot be
  verified from existing material.
---

# Add a hypothesis

A hypothesis is a claim plus the experiment that would confirm or reject it. Without a test it is
not a hypothesis, it is speculation, and it does not go into the knowledge base.

## Procedure

1. Read `docs/protocol/hypotheses.md`. Check the claim is not already covered (H1..H14 at the time
   of writing). If it refines an existing one, extend that row instead.
2. Fill the row:

   | Field | Content |
   |---|---|
   | ID | next free `H<n>` |
   | Hypothesis | one precise, falsifiable sentence, no bytes unless they come from cited evidence |
   | Evidence for | file / strings / document / capture that suggested it, with location |
   | Test | which experiment (existing E1..E7 from `docs/protocol/reverse-engineering.md`, or a new E-ID with risk, needs, and what it answers) |
   | Status | OPEN |

3. If a new experiment is needed, add it to the experiments table in
   `docs/protocol/reverse-engineering.md` with: risk level, prerequisites, what it answers, and
   the safety rules that apply (passive before active, backup first, never firmware path).
4. Add the feature row to `docs/protocol/evidence-matrix.md` if it introduces a new feature.

## Quality bar

- Falsifiable: someone with the console can perform the test and get a yes/no.
- Statused evidence: INFERRED/DOCUMENTED sources only; never "it seems likely".
- Safe test: read-only or passive observation preferred. Active tests state the risk and the backup.
