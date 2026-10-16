---
name: asc-probe
description: >
  Work with SmartFade / SmartSoft .asc show files: inventory keywords, diff two saves, design E6
  round-trip probes, and update docs/formats/asc-format.md. Use when the user says "look at this
  asc", "diff the show files", "what is $$MEMORY", "plan the E6 probes", "asc parser rules".
---

# ASC show-file probe

The `.asc` format is "ASCII Text Representation for Lighting Console Data" (USITT) with ETC
`$$` extensions. Knowledge lives in `docs/formats/asc-format.md`; open questions are hypotheses
H10..H14 in `docs/protocol/hypotheses.md`. This is file-format work, independent from USB/MIDI.

## Inventory a file

1. Report encoding facts: size, SHA-256, CRLF, non-ASCII bytes, line count.
2. Keyword table: keyword, count, arity, value ranges, first line number. Separate USITT keywords
   (DOCUMENTED semantics) from `$`/`$$` ETC extensions (INFERRED).
3. Cross-check against `docs/formats/asc-format.md`; add rows for anything new with Status.

## Diff two saves (E6)

1. `diff` the files; group changes by block (`PATCH`, `CUE`, `$$MEMORY`, `SET`, ...).
2. Map each change to the single console action the owner performed (from the action log).
3. One action per probe. Suggested probes (from `docs/protocol/reverse-engineering.md` E6):
   record a memory on a known page/fader (H13), add a channel step and a stack-state step
   (H12), set MIDI channel 3 (H10), patch an independent (H11), set a known step Wait (H14).
4. A probe that reproduces the expected change makes the hypothesis CONFIRMED; record it with
   `/protocol-status` and keep the two files as evidence under `docs/protocol/captures/`
   (they are reproducible experiments, evidence level 4).

## Parser rules to carry into `SmartFade.ShowFile.Asc`

- Preserve unknown keywords and their raw lines; warn, never drop.
- Keep original line numbers for diagnostics.
- Round-trip test: parse → write → byte-identical for files we have (CRLF, ordering, numeric format).

Delegate heavy analysis to the `asc-format-analyst` agent.
