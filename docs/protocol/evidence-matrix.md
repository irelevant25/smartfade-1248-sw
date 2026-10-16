# Evidence matrix

Central cross-reference. One row per feature. Cells say what each source contains.
`—` = not addressed by that source. Status uses the vocabulary in [README.md](README.md).

Sources: **SS-Man** = SmartSoft 3.0.1 manual · **Con-Man** = SmartFade console manual v3.0 ·
**Spec** = spec sheet + RTF · **MIDI** = MIDI spec v1.5 · **Exe** = SmartSoft.exe / sfEngine.dll
static analysis · **SFML** = ML library files · **ASC** = Ostrov + demo show files ·
**Capture** / **HW test** = our own experiments.

| Feature | SS-Man | Con-Man | Spec | MIDI | Exe | SFML | ASC | Capture | HW test | Status |
|---|---|---|---|---|---|---|---|---|---|---|
| USB link to PC | Yes, driver "USB High-Speed-Pipe" | USB for firmware update | USB Series B, SmartSoft over USB | — | etchsp.inf VID 14D5 MI_01. sfEngine HighSpeedPipeline | — | — | ? | ? | DOCUMENTED + INFERRED |
| USB message framing | — | — | — | — | Poll / Fetch / DirtyObjects / seq no. / retries (strings) | — | — | ? | ? | HYPOTHESIS (H3) |
| Version handshake | "console upgraded" required | — | — | — | ConsoleVersion object, mismatch message | — | `$SOFTWAREVERSION 3.0.2 43` | ? | ? | HYPOTHESIS (H4) |
| Channel level (write) | Live tab edits LTP params (ML) | Channel faders 48 | 48 ch | **none** | `ChannelLevels` object, `ConsoleEvent` | — | `CHAN n/%` in memories | ? | ? | UNKNOWN |
| Channel / output level readback | Output mimic zone | LCD output levels | — | none | `ChannelLevels`, `OutputLevels`, `InputLevels` objects | — | — | ? | ? | HYPOTHESIS (H5) |
| Fader positions | "fader positions… level matching" | — | — | CC 1–48 master TX | `FaderInfo` object | — | — | ? | ? | DOCUMENTED + HYPOTHESIS |
| Grand master | GM status | GM fader, BO button | Yes | CC 127 (both docs) | (not named) | — | — | ? | ? | DOCUMENTED |
| Blackout | — | BO button | Yes | PC 126/127 **contradictory** | — | — | — | ? | ? | DOCUMENTED (conflict) |
| Bumps master | — | Yes | Yes | CC 126 | — | — | — | ? | ? | DOCUMENTED |
| Crossfader faders | — | Live/Next | Yes | CC 124/125 (XF down/up vs Live/Next) | — | — | — | ? | ? | DOCUMENTED (naming conflict) |
| Cue/stack GO / PAUSE | — | Yes | Yes | PC 124/125 **swapped** between docs. MSC Go/Stop/Resume (fw3) | `PlaybackInfo` | — | Main `$SEQUENCE 48` | ? | ? | DOCUMENTED (conflict) |
| Stack step jump | — | Jump to step | — | PC 0–99 | — | — | `CUE n.n` | ? | ? | DOCUMENTED |
| Cue record / edit | Stack/Seq Edit tab | Record/Edit chapters | 199 steps | none | `InsertStep`, `DeleteStep`, `MoveStep`, `InsertPointQStep`, `StepContent` | — | `CUE`, `UP`, `DOWN`, `FOLLOWON`, `$$CUEPARAMS` | ? | ? | DOCUMENTED + INFERRED |
| Memories (288) | Page / Mem Edit tabs | 12 × 24 | 288 | CC 1–48, Note 0–47 bumps, PC 100–111 page | `Console::Memory` cache | — | `GROUP n` + `$$MEMORY p f` | ? | ? | DOCUMENTED + INFERRED (H13) |
| Sequences (48) | Page tab (yellow) | 12 × 4, 24 steps | 48 | "not sequence stepping" | `Console::Sequence` | — | `$SEQUENCE s …` + `$CUE` | ? | ? | DOCUMENTED |
| Patch | Patch tab | Patch by dimmer/channel | 512 → 48 | none | `PatchDevice` etc. (ML?), `consoleCachePatchUpdated` | Personalities (ML only) | `PATCH 1 dmx/ch/%` | ? | ? | DOCUMENTED + INFERRED |
| Independents | Patch tab | IND1/IND2 | Yes | none | `consoleCacheIndependentsUpdated` | — | `$INDEPENDENT n a b c d` | ? | ? | DOCUMENTED, ASC fields UNKNOWN |
| Labels / text | Any object can have text (SmartSoft only) | — | — | none | `ChannelText` | — | `Text`, `$$Text 1..3` | ? | ? | DOCUMENTED |
| LEDs / LCD mirror | Console simulator | — | — | — | `LedStatus`, `DisplayContent` | — | — | ? | ? | HYPOTHESIS |
| MIDI settings | — | MIDI menu | — | channel | "MIDI MSC on / i.d." | — | `SET MIDIChannel/MIDIActive/MSCDeviceId/MSCActive` | ? | ? | DOCUMENTED |
| Load/save show | System tab, console loads same show | SD card save/load | SD backup | none | `FileName`, `SHOW%d.ASC` | — | whole file | ? | ? | DOCUMENTED |
| DMX backup looks | — | 24 × 512 | Yes | none | `$BACKUPLOOK` keyword | — | not in samples | ? | ? | DOCUMENTED |
| Fixtures / personalities | Devices (ML) | — (not on 1248) | — | — | `Personality*`, `Device` objects | PERSLIB, FullReference | ML dialect only | n/a | n/a | Not applicable to 1248 |
