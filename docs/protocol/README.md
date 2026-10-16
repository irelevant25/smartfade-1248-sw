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
| [reverse-engineering.md](reverse-engineering.md) | Experiment plan, tooling, safety rules, hardware evidence requested |
| [captures/](captures/) | Capture files and capture format |

Not yet created because nothing is known: `packet-format.md`, `commands.md`,
`responses.md`, `state-model.md`. Create them when the first USB capture exists.

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
| User's console firmware | **UNKNOWN** | Must be read from the Diagnostics menu |
