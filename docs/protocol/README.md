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
| [smartsoft-analysis.md](smartsoft-analysis.md) | Static analysis of the SmartSoft installer, application, engine DLLs and USB driver |
| [midi-spec-analysis.md](midi-spec-analysis.md) | MIDI spec v1.5 vs console manual v3.0, message tables |
| [evidence-matrix.md](evidence-matrix.md) | Feature × source cross-reference (central document) |
| [capability-matrix.md](capability-matrix.md) | What the console / SmartSoft / our app supports |
| [reverse-engineering.md](reverse-engineering.md) | Experiment plan, tooling, safety rules, hardware evidence requested, capture format |
| [packet-format.md](packet-format.md) | HSP framing as observed in E2 (OBSERVED, nothing VERIFIED); since E3 (2026-10-29) the framing facts are VERIFIED as observed behaviour |
| [commands.md](commands.md) | PC → console messages SmartSoft sent in E2 and E3 (a record, not a list of messages we may send; VERIFIED there means SmartSoft's behaviour only) |
| [responses.md](responses.md) | Reply types and object payloads seen in E2 and E3 (the live objects are decoded from E3) |
| [state-model.md](state-model.md) | Session phases and mirrored-state model as seen in E2 and E3 |
| [consolelink-analysis.md](consolelink-analysis.md) | ConsoleLink (third-party, SmartFade **ML**, evidence class 5) compared with our 1248 evidence; proposed annotations and the hypotheses N1–N6 (now H46–H51). Never changes a 1248 status by itself |
| [captures/](captures/) | Capture files, experiment records (E1, E2, E3), the owner's statements and the capture format |

The four deferred documents (`packet-format.md`, `commands.md`, `responses.md`,
`state-model.md`) were created 2026-10-28, when the first USB capture (E2) existed. They carry
OBSERVED statuses at most; only VERIFIED elements may be used by code that talks to a console,
and no protocol element is VERIFIED yet. *(2026-10-28 wording, kept.)* **Update 2026-10-29:** after
the independent E3 session many elements are VERIFIED (26 facts, see [known-facts.md](known-facts.md)),
but VERIFIED means reproduced behaviour, never clearance for sending (safety rule 2 in
[reverse-engineering.md](reverse-engineering.md)): "only VERIFIED elements may be used by code that
talks to a console" is a necessary condition, not a sufficient one, and **no message is cleared:
no code may send anything to a console yet.**

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
