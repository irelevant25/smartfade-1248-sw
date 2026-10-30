# Capability matrix

"SmartFade supports?" and "SmartSoft supports?" are **documented** claims. "Protocol known?"
means *we* know the bytes. Today that is **No** for every row except console identification
(E1, 2026-10-28) and the read-only parts that experiment E2 (2026-10-28, passive capture of a
SmartSoft session, [record](captures/2026-10-28-e2-smartsoft-connect.md)) observed. **Nothing
is VERIFIED for writing**, and nothing is implemented or tested yet. E2 observed no PC -> console
control at all; its only writes were two System messages (F58).

**Update 2026-10-29 (E3,** [record](captures/2026-10-29-e3-console-actions.md)**).** The independent
second session (after a console power cycle) reproduced most of the read-side objects and the
start-up; the facts concerned are now VERIFIED as observed console / SmartSoft behaviour
(known-facts.md: F51–F55, F57–F65, F67, F68, F70–F77), and new read-side objects were observed
(F80–F89). **VERIFIED never clears a message for sending:** every write column below stays **No**,
E3 observed no PC -> console control either (the same two System messages, F58), and nothing is
implemented or tested.

**Update 2026-10-30 (E3b,** [record](captures/2026-10-30-e3b-live-objects.md)**).** Three more SmartSoft
sessions after another console power cycle reproduced the start-up and the show objects (known-facts.md:
F80, F82–F87, F89 and F92 are now VERIFIED in part, 35 VERIFIED facts in all) and added read-side facts
about the BUMPS fader, bump output, the MASTER scaling, SOLO, the fader banks, a GO crossfade and the
default fade times (F93–F106). **VERIFIED never clears a message for sending:** every write column below
stays **No**; E3b observed no PC -> console control either (the same two System messages in each
session, F58), and nothing is implemented or tested.

**Update 2026-10-30, later the same day (SC-1).** Safety check SC-1 ([safety-checks.md](safety-checks.md))
cleared the poll and the fetch of a listed record for a **read-only** session of our own software
(`SmartFade.Devices.Usb`, `Device:Mode=Usb` confirmed per day, no command accepted). The read columns can
therefore be served by our code for the live objects the console lists on its own, once the first live session
**E8** ([reverse-engineering.md](reverse-engineering.md), hypotheses H66–H68) has run; every write column still
stays **No**, and the full show download (which SmartSoft obtains with its System writes) is not available to
our code.

Protocol-known values: `No` · `Documented (MIDI)` · `Partially observed` · `Observed` · `Verified`.

| Capability | SmartFade 1248 supports? | SmartSoft supports? | Protocol known? (USB) | Protocol known? (MIDI) | Implemented? | Tested? |
|---|---|---|---|---|---|---|
| Connect / identify console | n/a | Yes (Online/Offline) | **Partially observed**: USB identity (VID/PID, interfaces, endpoints, strings; E1 2026-10-28) and, from E2 2026-10-28, the SmartSoft start-up (string reads, poll / fetch framing, System `09 00 00` then `07 00 00`, F56, F58). OBSERVED once, not VERIFIED; the two System writes must not be sent by our code. **Update 2026-10-29 (E3):** the start-up sequence, the poll / fetch framing and the two System bodies are VERIFIED as what SmartSoft does and how the console answers (F41, F51–F54, F57–F59, F62, F65); the descriptors (F40, F42) stay OBSERVED and the connect-time list differed between the two sessions (F80). VERIFIED is no clearance: the two System writes still must not be sent by our code **Update 2026-10-30 (E3b):** every SmartSoft session repeats the start-up in full (three more sessions, F94); the console's counter is not reset by a restart (F93); the connect-time pending list differs with the console's state (F56, F80, F94). Still the read side; the two System writes must not be sent by our code. | n/a | No | No |
| Firmware version readback | Yes (Diagnostics menu) | Yes (version check) | **Observed** (E2 2026-10-28): ConsoleVersion object (ID 37) fetched after System `09 00 00`, string "3.0.2.9.0.43" (F59, f226). The value is VERIFIED (F48); the request (a fetch) is OBSERVED once. **Update 2026-10-29 (E3):** reproduced after a console power cycle (f194): the ConsoleVersion object (F59) and the sequence System `09 00 00` -> list [37] -> fetch -> string (F58) are VERIFIED as SmartSoft's and the console's behaviour; read side only, not cleared for sending **Update 2026-10-30 (E3b):** byte-identical in three more sessions (f198, f5564, f10262; F59). | none | No | No |
| Channel levels (live control) | Yes (48 ch) | Probably (live views; editing documented for LTP params on ML) | No | none | No | No |
| Output level readback | Yes (LCD) | Yes (output mimic, DMX Outputs tab) | **Partially observed** (E2): OutputLevels (13) and ChannelLevels (15) objects fetched and changing with a fader (F66, F67); index and scale mapping INFERRED / HYPOTHESIS (H32). **Update 2026-10-29 (E3):** the object formats (F67) are VERIFIED; the index mapping for channels 1 and 5 (ChannelLevels body[c], OutputLevels body[1 + d]) and the 0x00–0xFF scale at MASTER 0xFF are OBSERVED with logged owner actions (F83); scaling of live levels by the MASTER is HYPOTHESIS (H47, H57). Read side only **Update 2026-10-30 (E3b):** the index mapping (F83) is VERIFIED in part and the scaling of live levels by the MASTER is OBSERVED (F96; the exact arithmetic is open, H60); bump output appears in the level objects at the BUMPS level (F97). Read side only. | none | No | No |
| Cue (stack) playback GO/PAUSE | Yes | Yes (playback tab) | No for any write. **Read side (E3b 2026-10-30):** one GO press by the owner, with the STACK button unlit, appeared as a 5-s crossfade in PlaybackInfo (Live / Next, remaining time) and LedStatus block 50 and changed no output (F101, F106; H50 layout CONFIRMED); GO with the STACK button lit is untested | Documented (conflicting values) | No | No |
| Stack step jump | Yes | Yes | No | Documented (PC 0–99) | No | No |
| Cue recording / editing | Yes | Yes (edit tabs, insert/move/delete) | **No** (read side observed, E2 + SHOW04.ASC): 12-byte step records read from the console, 26/26 match the show file (F74); read-only, nothing recorded or edited, no write message known | none | No | No |
| Grand master | Yes | Shows GM status | **Partially observed (read side only)** (E3 2026-10-29): FaderInfo body[194] = the MASTER fader level, 0xFF -> 0x83 -> 0xFF (F82; OBSERVED, supported by the DOCUMENTED manual p.56 and the owner's panel label). **No** for any write **Update 2026-10-30 (E3b):** MASTER = body[194] VERIFIED in part (F82); the live level objects are scaled by it (F96). | Documented (CC 127) | No | No |
| Blackout | Yes | ? | No (read side, E3 2026-10-29: LedStatus block 53 = the Blackout LED, blinking while the MASTER is below full; F85, H56; the button itself was not pressed) **Update 2026-10-30 (E3b):** the block 53 blink reproduced (F85, VERIFIED in part). | Documented (conflicting polarity) | No | No |
| Bumps master | Yes | ? | No (read side, E3 2026-10-29: FaderInfo body[193] = 0x00 while the BUMPS fader stood at 0 %; HYPOTHESIS H46, not yet seen moving) **Update 2026-10-30 (E3b): Partially observed (read side only):** FaderInfo body[193] = the BUMPS fader level, 0x00 ↔ 0xFF in four logged moves (F95; H46 BUMPS part CONFIRMED); bump output follows the BUMPS level (F97, tested at 0x00 and 0xFF). **No** for any write. | Documented (CC 126) | No | No |
| Submasters | **No such term on 1248.** Closest: memory faders 1–24 × 12 pages | Yes | No | Documented (CC 1–48, MEM mode) | No | No |
| Memory bumps | Yes | ? | **Partially observed (read side only)** (E3 2026-10-29): in memory mode the bump LED (LedStatus block k) went to full while the bump was held and back (F84, F88); no level object changed (BUMPS fader at 0 %, readings a / b, H54). **No** for any write **Update 2026-10-30 (E3b):** with BUMPS at 0xFF the held bump appears in the level objects (F97); SOLO on and off looked alike; read side only. | Documented (Note 0–47) | No | No |
| SOLO (bump mode) | Yes (Pile-on / Solo bump modes, manual) | ? | **Partially observed (read side only)** (E3b 2026-10-30): LedStatus block 52 is white while SOLO is on (F100; INFERRED); the effect of SOLO on the level objects could not be separated (F97). **No** for any write | none | No | No |
| Fader modes and banks (FADERS buttons: 1-24, 25-48, MEMS) | Yes (24 physical faders, F7) | ? | **Partially observed (read side only)** (E3 / E3b): ConsoleStatus body[0] (channel 0 / memory 1) and body[4] (bank 1-24 = 0 / 25-48 = 1), the indicator LEDs 55 / 56 / 58 and the LCD row labels follow the mode (F87, F98; meaning HYPOTHESIS, H53); FaderInfo and LED blocks 1–24 stay with the physical faders (F99). **No** for any write | none | No | No |
| Memories (store/edit) | Yes (288) | Yes | **No** (read side observed, E2 + SHOW04.ASC): Memory objects read (16 of 16 match the file's `GROUP`/`CHAN`, scale 0xFF = 100 %, F71, F72); no store / edit message observed. **Update 2026-10-29:** the 16 Memory bodies were reproduced byte for byte in E3 (VERIFIED as "the console returns these bodies", F71) | none | No | No |
| Memory page select | Yes (12) | Yes | No | Documented (PC 100–111) | No | No |
| Sequences | Yes (48 × 24) | Yes | **Partially observed** (E2 + SHOW04.ASC): Sequence and SequenceMain headers and steps match the file (F74); step-number contradiction open (F75). **Update 2026-10-29:** E3 reproduced the bodies and the contradiction (F74, F75 VERIFIED as "the console returns these bodies") | none (no stepping) | No | No |
| Patch | Yes | Yes (drag & drop) | **Partially observed** (E2 + SHOW04.ASC): Patch (5) body = 2 + 512 bytes, 512/512 DMX addresses equal the show file's patch (F70, H34 CONFIRMED, OBSERVED); proportional level not in the object, UNKNOWN. **Update 2026-10-29:** body byte-identical in E3 (F70, F68 VERIFIED as "the console returns this body") | none | No | No |
| Independents | Yes (2) | Yes | **Partially observed** (E2 + SHOW04.ASC): Independents record read, matches `$INDEPENDENT` (F76); meaning of the fields and live levels UNKNOWN. **Update 2026-10-29:** body byte-identical in E3 (F76 VERIFIED as "the console returns this body") | none | No | No |
| Labels (text) | Stored in show file | Yes | **Partially observed** (E2 + SHOW04.ASC): labels = 3 x 6 UTF-16BE characters, equal to the file in all objects compared; byte order INFERRED (F73, H40). **Update 2026-10-29:** the label blocks are byte-identical in E3 (VERIFIED as "the console returns these blocks"), but the byte order is downgraded to HYPOTHESIS (two readings, A: UTF-16BE, B: pad byte + UTF-16LE, CL-ML class 5) | none | No | No |
| State readback (full show) | n/a | Yes (loads show from console) | **Partially observed** (E2): after System `07 00 00` the console listed 157 objects and SmartSoft fetched them all in ≈ 7.3 s (F58, F67); most bodies not decoded *Update (SHOW04.ASC cross-check): the bodies of Patch, Memory, Sequence / SequenceMain / StepContent, Independents, SystemSettings and the labels are decoded to the extent that they match the show file (F70-F77); other objects remain undecoded.* **Update 2026-10-29 (E3):** the same 157-record list and the same bodies in a second session (F57, F70–F77 VERIFIED as behaviour). The live objects FaderInfo, DisplayContent and PlaybackInfo are not in that list (F89, H58): they arrive when pending at connect or when they change **Update 2026-10-30 (E3b):** the same 157-record list and bodies in three more sessions (F94); after a restart nothing is pending, so FaderInfo, DisplayContent and PlaybackInfo arrive only when they change (F89 VERIFIED in part, H58). | none | No | No |
| Load show to console | SD card | Yes (System tab) | No (E2: SmartSoft uploaded nothing; its Online step is a download, F58) | none | No | No |
| Snapshots | Yes (10) | ? | No | none | No | No |
| DMX backup mode | Yes | No (Normal mode required) | n/a | none | No | No |
| MIDI Show Control | Yes (fw 3.0: Go/Stop/Resume…) | Settings only | n/a | Documented (MSC standard, SmartFade subset) | No | No |
| ASC show file read | Yes (SD) | Yes | n/a (file format) | n/a | No | No |
