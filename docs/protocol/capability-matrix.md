# Capability matrix

"SmartFade supports?" and "SmartSoft supports?" are **documented** claims. "Protocol known?"
means *we* know the bytes, and today that is **No** for every row. Nothing is implemented
or tested yet.

Protocol-known values: `No` · `Documented (MIDI)` · `Partially observed` · `Verified`.

| Capability | SmartFade 1248 supports? | SmartSoft supports? | Protocol known? (USB) | Protocol known? (MIDI) | Implemented? | Tested? |
|---|---|---|---|---|---|---|
| Connect / identify console | n/a | Yes (Online/Offline) | No | n/a | No | No |
| Firmware version readback | Yes (Diagnostics menu) | Yes (version check) | No | none | No | No |
| Channel levels (live control) | Yes (48 ch) | Probably (live views; editing documented for LTP params on ML) | No | none | No | No |
| Output level readback | Yes (LCD) | Yes (output mimic, DMX Outputs tab) | No | none | No | No |
| Cue (stack) playback GO/PAUSE | Yes | Yes (playback tab) | No | Documented (conflicting values) | No | No |
| Stack step jump | Yes | Yes | No | Documented (PC 0–99) | No | No |
| Cue recording / editing | Yes | Yes (edit tabs, insert/move/delete) | No | none | No | No |
| Grand master | Yes | Shows GM status | No | Documented (CC 127) | No | No |
| Blackout | Yes | ? | No | Documented (conflicting polarity) | No | No |
| Bumps master | Yes | ? | No | Documented (CC 126) | No | No |
| Submasters | **No such term on 1248.** Closest: memory faders 1–24 × 12 pages | Yes | No | Documented (CC 1–48, MEM mode) | No | No |
| Memory bumps | Yes | ? | No | Documented (Note 0–47) | No | No |
| Memories (store/edit) | Yes (288) | Yes | No | none | No | No |
| Memory page select | Yes (12) | Yes | No | Documented (PC 100–111) | No | No |
| Sequences | Yes (48 × 24) | Yes | No | none (no stepping) | No | No |
| Patch | Yes | Yes (drag & drop) | No | none | No | No |
| Independents | Yes (2) | Yes | No | none | No | No |
| Labels (text) | Stored in show file | Yes | No | none | No | No |
| State readback (full show) | n/a | Yes (loads show from console) | No | none | No | No |
| Load show to console | SD card | Yes (System tab) | No | none | No | No |
| Snapshots | Yes (10) | ? | No | none | No | No |
| DMX backup mode | Yes | No (Normal mode required) | n/a | none | No | No |
| MIDI Show Control | Yes (fw 3.0: Go/Stop/Resume…) | Settings only | n/a | Documented (MSC standard, SmartFade subset) | No | No |
| ASC show file read | Yes (SD) | Yes | n/a (file format) | n/a | No | No |
