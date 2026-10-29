# 2026-10-29 E3: console actions while SmartSoft is Online (USBPcap, passive)

- Date: 2026-10-29. The capture ran 20:40:21–20:46:33 local time (CET, UTC+1). The pcap's
  first timestamp is 2026-10-29T19:40:21.625362Z, which is 20:40:21.625362 on the same PC clock
  that wrote the action log.
- Experiment ID: **E3**, run as one guided capture with single actions in sequence:
  - E3(a): channel fader 1, 0 → full → 0, plus the same for channel fader 5.
  - E3(b): bump of memory fader 1 on page 1, in memory mode.
  - E3(c): Grand Master to about half and back to full.
  - E3(d) and E3(e) of the plan were not run.
  - Passive: nothing was sent by us.
- Console: ETC SmartFade 1248, firmware 3.0.2.9.0.43 (F48; the console reported the same
  string again, f194).
  - The owner answered "y" to "was the console switched off since yesterday's capture"
    (log 20:40:56.293), so this session follows a **power cycle**.
  - The console re-enumerated at USB device address **7** on bus 1, the same root hub as E2
    (E2: address 10; E1: address 50).
- Host: the owner's main PC, as in E2.
  - Windows 11 Pro build 26300, Memory Integrity off, `etchsp.sys` 2.00.000 loaded.
  - SmartSoft 3.0.43 (package 3.0.2.9.0.43b).
  - USBPcap 1.5.4.0, tshark 4.0.3.
- Connection: USB A-B cable straight into the PC, as in E1 and E2.
- Show backup: no new backup was reported for E3. The show on the console is still
  `SHOW04.ASC`:
  - FileName f1576 carries "SHOW04.ASC".
  - Every show object fetched in E3 is byte-identical to E2 (section "Reproduction of E2").
  - The E2 backup in `smartfade-sd-card-backup/SHOW04.ASC` therefore still matches the
    console's show.
- Rig (owner): 4 RGB fixtures at DMX 5, 8, 11, 14. Channel fader 5 was expected by the guided script (assuming an R,G,B channel order, which is UNKNOWN) to light
  fixture 1 red (owner's expectation, not confirmed in writing).
- Capture (coordinator): `USBPcapCMD.exe -d \\.\USBPcap1 -o <raw> -A --inject-descriptors`,
  started elevated by Claude at 20:40:21 and stopped with `Stop-Process` at 20:46:33. That is
  58 s after the last console frame (20:45:34.96), so nothing of the session is missing.
  - The console-only split was made by Claude with
    `tshark -r <raw> -Y "usb.device_address == 7" -F pcap -w <split>`.
  - SmartSoft was started by Claude via `Start-Process` at 20:40:27.376.
- **Deviation from safety rule 3 (show backup before SmartSoft is started):** no SD backup was
  taken immediately before this session; it relied on the E2 backup `SHOW04.ASC`, and the
  guided script had no backup gate. The capture shows after the fact that nothing had changed
  (every show object byte-identical to E2). Consequence for later sessions: the guided script
  now asks for the backup file name before anything else and logs "backup taken", and
  SmartSoft is started only after that line exists.
  - The owner followed `tools/usb-probe/e3-guided-session.ps1`.

**Conventions** (as in the E2 record):
- `fN` is the pcap frame number in the committed split: the submission frame for OUT, the
  completion frame for IN.
- `seqN` is the JSONL record number.
- `pseq` is a payload's own sequence byte.
- `body[n]` is byte n after the 5-byte inbound payload header.
- Times are capture-relative seconds; `t` in the JSONL is the URB completion time.
- Every quoted byte comes from the cited frame.
- **LED blocks are numbered from 1:** block k = LedStatus body[1 + 6(k − 1) .. 6k], as in
  [../consolelink-analysis.md](../consolelink-analysis.md), so fader N's LED is block N.
- "CL-ML" marks a claim of the third-party ConsoleLink program about the SmartFade **ML**
  (evidence class 5, never a 1248 fact; see that document).

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-29-e3-console-actions.pcap` | `0DCC3E1DCA277F97F471D6DDF12F5A39AADF7898A126471606A2579888058340` | 2 067 417 B, 40 652 frames, 0–313.336854 s | yes (**the evidence**) | Console-only split (address 7), classic pcap, LE, µs, link type 249 |
| `2026-10-29-e3-console-actions.raw-all-devices.pcap` | `E11B5AA5E32D1E2BCC51F7E3A505CAC91D651E3EE23D7F1BE7C2067857E26D04` | 234 903 216 B, 188 988 frames, 0–372.253081 s | **no** (`.gitignore`: `*.raw-all-devices.pcap`) | Raw USBPcapCMD output for root hub `USBPcap1` (deleted on 2026-10-29 after the completeness checks below; the hash stays recorded): every device on the hub, including the owner's keyboard and mouse (keystrokes), plus two busy unrelated devices (per the coordinator, address 4 with 120 996 frames and address 6 with 27 290). Used only for the completeness check below; other devices' data was discarded unread |
| `2026-10-29-e3-console-actions.log` | `EF10EBC2F3949A37B3F76EC881D43EE2C2C3B0FF37A61CEE2A79AB69A2B1645B` | 1 315 B, 27 lines, CRLF | yes | The owner's action log, written by `tools/usb-probe/e3-guided-session.ps1`. Stamps are PC local time. `Step` lines are stamped when the owner presses Enter at the **start** of an action. `Confirm-Done` lines ("at full", "at zero", "release", "online", "closed") are stamped when the owner confirms **afterwards**. `wait … done` lines are stamped at the **end** of a scripted wait |
| `2026-10-29-e3-console-actions.jsonl` | `3B2B6861C25CE7B90BDF4212E20445F7B53EF8DE1A44E308F75B2C43E9AE59CF` | 4 903 008 B, 20 357 lines | yes (derived) | 1 header, 20 326 `frame` records (one per URB), 30 `note` records: the 27 log lines (with `source` and `logTime`) plus 3 coordinator facts (capture start, SmartSoft start at t = 5.750638, capture stop). Command in `tools/protocol-analyzer/README.md`; two runs and a verbatim run of the README command gave identical bytes |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `EA7627D7CDD0FF214F003F290AE7CB6B75B73149B4E753011C76B129EFD20FE8` | | yes | Normalizer **version 2**, which adds `--notes-log` / `--notes-utc-offset`. Re-run on E2 it is byte-identical to the committed E2 file except the header's normalizer version (checked). The revision before this change (`48E761CD…ABC8F`, version 1 with source comments added after E2) reproduced the E2 file exactly |
| `tools/protocol-analyzer/hsp_summary.py` | `E183704E274D307894F65AE6EB1B99711C31CA22FE215DEFBD19926AD0E9EA35` | | yes | New modes `timeline`, `census`, `lcd`, `startdiff`. `summary` now also prints the sequence wraps and the reply-header position of each data write. The E2 summary output is unchanged apart from those two new line types (checked) |
| `2026-10-29-e3-owner-statements.txt` | `d3e0ee5b995e3e419d2fa96568644d94cb4a9599309a16f7b73f6c4d5dbde328` | | yes | The owner's chat answers after the capture, transcribed by Claude (see "Owner answers"); Claude's readings inside it do not govern, the knowledge base does |
| `tools/usb-probe/e3-guided-session.ps1` | E3 revision `fa5d5ff1c570da80c61df799e346c3e68521245158eacb1cc815495217e62e12` | | yes | The guided operator script that wrote the action log and defines the meaning of its time stamps (Enter = start of an action). Revised after the safety review (backup gate, mandatory new log file), so the current file hashes differently |
| `tools/usb-probe/action-log.ps1` | E3 revision `561e525d719d76baf28c60f2ae2724b14d44e208fb02e462f1907d8c2b670cc5` | | yes | Free-form action log script (not used for E3's log, which came from the guided script); revised after the safety review |
| `tools/protocol-analyzer/e2_asc_crosscheck.py` | `8C49AEDB18FF7EE4179AD9BAAA2BEE3A0C286788734511649A1BB5B28C4BB314` | | yes | Unchanged; run on the E3 JSONL against `SHOW04.ASC` (section "Reproduction of E2") |

**Completeness of the split.** The normalizer run on the raw file with `--device-address 7`
found **40 652** frames for address 7 and skipped 148 336 frames of other devices unread. It
produced 20 326 records, identical to the split's in every field except the pcap frame numbers
(0 differing records; e.g. split f20 = raw frame 2860).
- The raw file has no address-7 frames after 313.336854 s and none on EP 0x81 or 0x02 at all.
- **Independent cross-check:** tshark 4.0.3's `usb.capdata` equals the JSONL `data` for all
  20 321 data-carrying bulk frames (0 mismatches).

**Normalization** is as for E2 (one record per URB, merged by IRP ID), plus the action log
merged as notes. The log is converted with `--notes-utc-offset +01:00`. That offset is not
read from the machine: it is the stated clock relationship (frame 1 = 20:40:21.625362 local =
19:40:21.625362Z) and agrees with E2's capture start (19:16:37.470628Z = 20:16:37 local).

## Action log (capture-relative)

Sources:
- **log**: a line of the owner's action log, as a `note` with its `logTime`.
- **coord.**: a fact from the coordinator.
- **capture**: read from the console's data.

| t (s) | Source | Event |
|---|---|---|
| 0.000000 | coord. + capture | Capture starts. f1–f6 are **synthetic** `--inject-descriptors` frames, byte-identical to E2's: device descriptor f2 `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01`, the 64-byte configuration descriptor f4, SET_CONFIGURATION f5 / f6. They are not bus traffic of the session |
| 5.750638 | coord. | SmartSoft started via `Start-Process` (20:40:27.376) |
| 6.244772–6.249838 | capture | GET_DESCRIPTOR(STRING 0) → f8 `04 03 09 04`; GET_DESCRIPTOR(STRING 2, 0x0409) → f10 `1C 03 45 00 54 00 43 00 20 00 53 00 6D 00 61 00 72 00 74 00 46 00 61 00 64 00 65 00` = "ETC SmartFade" (as in E2) |
| 6.250309 | capture | First poll f11 = 12 zero bytes; the first reply f14 `04 00 00 00 00 00 00 00 00 00 00 00` is **idle** |
| 6.283064 | capture | Second reply f18 `04 00 0C 00 …` with payload f20 `00 00 07 00 28 00 01 13 00 00 00 00`: pseq **0x00**, DirtyObjects with **1 record, ID 0x13 = 19 InputLevels** |
| 6.313131–6.330059 | capture | Fetch f21 `02 00 00 00 13 00 00 00 00 00 00 00`; reply f26 `03 00 08 02 13 00 …` (520 bytes follow), payload f28 `01 00 03 02 13 00 02 00 …` = InputLevels, 515-byte body |
| 7.514813 | capture | **System `09 00 00`**: header f177 `01 00 07 00 00 00 00 00 00 00 00 00`, payload f179 `00 03 00 27 09 00 00`, after the 40th reply header |
| 7.531994–7.578468 | capture | List f186 `02 00 07 00 28 00 01 25 00 00 00 00` (ID 37), fetch f187, ConsoleVersion f194 `03 00 0D 00 25 0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33` = "3.0.2.9.0.43" |
| 8.525638 | log | `START guided E3 session` (20:40:30.151) |
| 8.765690 | capture | **System `07 00 00`**: header f343, payload f345 `01 03 00 27 07 00 00`, after the 79th reply header |
| 8.788986 | capture | Reply f350 `04 00 18 03 …`, list f352 = **157 records**, body byte-identical to E2's f384 |
| 8.84–16.07 | capture | Full download, 157 fetches f353…f1601; the last payload is ChannelLevels f1608 |
| 34.667638 | log | `owner answer: console powered off since E2 = y` (20:40:56.293) |
| 39.758638 | log | `smartsoft online (owner)` (20:41:01.384). The coordinator notes that Online appeared some seconds earlier; the download had finished at 16.07 s |
| 59.768638 | log | `wait 20 s done (idle after Online)` |
| 76.541638 | log | `fader1 up start` (20:41:38.167) |
| 77.80–79.79 | capture | Fader 1 rises. First list f9578; FaderInfo body[1] 0x0B (f9594) … 0xFF (f9916); ChannelLevels body[1] 0xFF at f9924 |
| 81.803638 | log | `fader1 at full` (confirmed 2.1 s after the capture shows 0xFF) |
| 83.62–91.35 | capture | **Not in the log:** level 1 falls to 0x00 (83.62–85.25), rises to 0xFF (86.93–87.60), falls to 0x00 (89.12–89.43) and rises to 0xFF again (90.87–91.35). The change is consistent across FaderInfo body[1], ChannelLevels body[1], OutputLevels body[2], LedStatus body[2] / [5] and DisplayContent body[3] (f10432 … f11624). The log says fader 1 was at full from 81.80 s until `fader1 down start` at 95.55 s. These are four unlogged movements (two falls, two rises) of fader 1 (or of something else driving channel 1 and FaderInfo record 1; INFERRED: the fader; the owner later confirmed accidental movements, A2) |
| 86.808638 | log | `wait 5 s done (fader 1 at full)` |
| 95.549638 | log | `fader1 down start` (20:41:57.175) |
| 97.81–99.18 | capture | Fader 1 falls to 0x00 (FaderInfo f12438 … f12668, ChannelLevels f12676) |
| 100.068638 | log | `fader1 at zero` |
| 105.087638 | log | `wait 5 s done (fader 1 at zero)` |
| 125.776638 | log | `fader5 up start` (20:42:27.402) |
| 126.64–128.28 | capture | Fader 5 rises; FaderInfo body[17] reaches 0xFF at f16506 (128.279829) |
| 131.639638 | log | `fader5 at full` |
| 136.644638 | log | `wait 5 s done (fader 5 at full)` |
| 140.729638 | log | `fader5 down start` |
| 141.93–144.07 | capture | Fader 5 falls to 0x00 (FaderInfo f18282 … f18642; LCD cell blank at f18658) |
| 145.762638 | log | `fader5 at zero` |
| 150.769638 | log | `wait 5 s done (fader 5 at zero)` (20:42:52.395) |
| 249.985664–250.089884 | capture | **Memory-mode switch, not logged.** The coordinator says it happened "at an unlogged moment between 20:42:52 and 20:44:36"; the capture places it at 20:44:31.61 local. List f32312 (ConsoleStatus, LedStatus); ConsoleStatus f32320 body[0] 0x00 → **0x01**; LedStatus f32328 changes 58 bytes |
| 255.291638 | log | `bump1 press` (20:44:36.917) |
| 255.489752–255.538628 | capture | List f33032 (LedStatus); LedStatus f33040 block 1 = `FF 00 00 FF 00 00` |
| 263.078302–263.126350 | capture | List f34018; LedStatus f34026 block 1 = `46 00 00 46 00 00` (bump released) |
| 264.122638 | log | `bump1 release` (a Confirm-Done stamp, 1.0 s after the console reported the release) |
| 269.130638 | log | `wait 5 s done (after bump)` |
| 286.979638 | log | `gm to half start` (20:45:08.605) |
| 287.32–289.12 | capture | FaderInfo body[194] 0xFF → 0x83 (f37168 … f37484, 23 steps). LedStatus block 53 becomes `37 37 FF 0A 0A 0A` at f37176 (287.418) |
| 289.995638 | log | `wait 3 s done (GM at half)` |
| 294.498638 | log | `gm to full start` (20:45:16.124) |
| 294.83–295.49 | capture | body[194] 0x83 → 0xFF (f38228 … f38326, 8 steps); block 53 back to `0A 0A 0A 0A 0A 0A` at f38348 (295.490) |
| 299.503638 | log | `wait 5 s done (GM at full)` |
| 311.714638 | log | `smartsoft close start` (20:45:33.340) |
| 313.336854 | capture | Last transfer: idle reply f40652 `04 00 00 00 00 00 00 00 00 00 00 00` to poll f40649 (12 zero bytes), 1.62 s after the close started. No closing message |
| 316.349638 / 321.359638 / 321.361638 | log | `smartsoft closed`, `wait 5 s done (after close)`, `END guided E3 session` |
| 371.4 | coord. | USBPcapCMD stopped (20:46:33); the raw file ends at 372.253081 s |

The console was probably left in memory mode at the end (coordinator).

## Observed result

### Structure

| Endpoint | Transfer | Direction | URBs | Notes |
|---|---|---|---|---|
| 0x04 (interface 1) | bulk | OUT | 10 119 | 12 B × 10 117, 7 B × 2 |
| 0x83 (interface 1) | bulk | IN | 10 202 | 9 692 reply headers, 510 payloads |
| 0x80 / 0x00 (EP0) | control | IN / OUT | 4 / 1 | 2 synthetic descriptor reads + 2 string reads / 1 synthetic SET_CONFIGURATION |
| **0x81, 0x02 (interface 0, HID)** | interrupt | | **0** | none in 0–313.34 s (split) and none in 0–372.25 s (raw) |

- All 20 326 URBs completed with USBD status 0.
- Bulk traffic runs 6.250–313.337 s without a gap above 100 ms.
- `hsp_summary.py` classified the bulk traffic with **0 framing errors**: 9 692 polls, 423
  fetches, 2 data headers each followed by exactly 7 bytes, 9 692 reply headers and 510
  payloads.
- Reply types: 423 type 3 (all with payload, all echoing the fetch, 423 / 423); 87 type 4 with
  payload (all DirtyObjects); 9 182 type 4 idle. Type-4 w2..w5 = 0 in 9 269 / 9 269.
- Stale w2..w5 in poll and data headers: 9 693 / 9 693.
- Every IN transfer directly follows a poll or its own reply header (0 exceptions in 10 202).
- Every fetch is answered by the reply to the very next poll (423 / 423), and no fetch was ever
  re-sent. Both data writes were followed at the next poll by a DirtyObjects list.
- **Inbound sequence:** 510 payloads, pseq 0x00 (f20) … 0xFD, +1 per payload, 0 breaks, one
  wrap: f10942 starts `FF 00 C3 00 0E`, f10950 starts `00 00 33 00 0F`. Byte 1 is 0x00 in
  510 / 510. The length word (bytes 2–3, LE) is payload length − 5 in 510 / 510.
- **Outbound sequence:** 0x00 (f179), 0x01 (f345).
- **DirtyObjects:** 87 lists, all of length 7 + 5n with a BE count, c = 0 in every record. The
  423 records are, in order, exactly the 423 fetches, and every fetch follows the list that
  named it. Lists per action window (all 87 fall into one):

| Window | Lists | Records | Objects listed (ID: count) |
|---|---|---|---|
| Start-up, 6–17 s | 3 (f20, f186, f352) | 159 | the 1 + 1 + 157 records above |
| Fader 1, 76–101 s | 30 (f9578 … f12652) | 143 | 13: 30, 14: 30, 15: 30, 21: 23, 22: 30 |
| Fader 5, 125–146 s | 17 (f16214 … f18618) | 80 | 13: 17, 14: 17, 15: 17, 21: 12, 22: 17 |
| Memory-mode switch, ≈ 250 s | 2 (f32312, f32334) | 4 | 21: 1, 22: 2, 23: 1 |
| Bump 1, 255–265 s | 2 (f33032, f34018) | 2 | 22: 2 |
| GM, 286–300 s | 33 (f37160 … f38332) | 35 | 14: 33, 22: 2 |

### Objects: what changed where (`census`)

Only these body positions changed during the session. Every other byte of every object stayed
as first fetched.

| Object | Position | Range | Moved with |
|---|---|---|---|
| FaderInfo (14), 80 payloads | body[1] | 0x00–0xFF | fader 1 (26 changes, f9640 … f12668) |
| | body[17] | 0x00–0xFF | fader 5 (16 changes, f16238 … f18642) |
| | body[194] | 0x83–0xFF | Grand Master (31 changes, f37168 … f38326) |
| ChannelLevels (15), 48 payloads | body[1] / body[5] | 0x00–0xFF | fader 1 / fader 5 |
| OutputLevels (13), 48 payloads | body[2] / body[6] | 0x00–0xFF | fader 1 (DMX 1) / fader 5 (DMX 5) |
| DisplayContent (21), 36 payloads | body[3] / body[7] | 0x20, 0x80–0x86 | fader 1 / fader 5 |
| LedStatus (22), 54 payloads | G bytes of block 1 (body[2], [5]) and block 5 (body[26], [29]) | 0x00–0xFF | fader 1 / fader 5 |
| | R bytes of blocks 1–24, R + G of block 21, block 55 G, block 58 | see "LED blocks" | memory-mode switch (f32328) |
| | block 1 R | 0x46 → 0xFF → 0x46 | bump 1 (f33040, f34026) |
| | block 53 phase A | `0A 0A 0A` → `37 37 FF` → `0A 0A 0A` | GM below full (f37176, f38348) |
| ConsoleStatus (23), 2 payloads | body[0] | 0x00 → 0x01 | memory-mode switch (f1584 → f32320) |

- FaderInfo body[193] is 0x00 in all 80 payloads (E2: 0x00 in all 14). The constant bytes
  body[83] = body[99] = 0x01 (byte 2 of records 21 and 25) are the same as in E2.
- Record bytes 1–3 of records 1 and 5 never changed.
- At full, with the GM at full, all three level objects read **0xFF**: FaderInfo f9916 body[1];
  f16506 body[17]; ChannelLevels f9924; OutputLevels reached 0xFF too (census maximum).
- Index base and scale:
  - fader 1 = FaderInfo body[1] = record 1 at 1 + 4(N − 1);
  - channel 1 = ChannelLevels body[1];
  - DMX 1 = OutputLevels body[2] = body[1 + d];
  - fader 5 = body[17], channel 5 = body[5], DMX 5 = body[6].
  - Patch maps DMX 1 → 1 and DMX 5 → 5 (F70).
  - Fader 5 example: f16222 ChannelLevels body[0..6] `32 00 00 00 00 07 00`; f16230
    OutputLevels body[0..7] `02 00 00 00 00 00 10 00`; f16246 LedStatus body[25..30]
    `00 1C 00 00 1C 00`.
- **The bump changed no level object.** ChannelLevels, OutputLevels and FaderInfo were not even
  listed in 255–265 s; GROUP 1 = `CHAN 7/100 8/100`, so channels 7 / 8 and DMX 7 / 8 stayed as
  they were. Only LedStatus block 1 changed. Whether the fixtures (assuming an R,G,B channel order, UNKNOWN: DMX 7 = fixture 1 blue,
  DMX 8 = fixture 2 red) lit during the bump was not reported.
- **During the GM move no level byte changed**: every channel was at 0, so a GM scaling of the
  live levels cannot be seen in this capture.
- **FaderInfo is not part of the start-up.** It was in neither the pending list nor the
  157-record list; its first fetch in E3 is f9594 at 77.90 s, during the first fader move.
  DisplayContent (first f9624) behaves the same way. PlaybackInfo (17) was never listed in E3.

### InputLevels (19), new

f28: body = `00 02 00` + 512 zero bytes = 515 bytes; payload length 520, length word 0x0203.
- HYPOTHESIS: `[flag?][u16 BE 512][512 DMX-input levels]`, by analogy with OutputLevels; all
  zero here. Whether anything was connected to DMX IN is UNKNOWN.
- Fetched once: it is the only record of the list pending at connect (f20).

### LCD (DisplayContent, ID 21; `lcd` mode)

body[0] = 0x28 = 40, then two 20-character lines. The line layout is the same as in E2.
Selected payloads, rendered the way the `lcd` mode renders them. `lcd` prints changes only, so
it omits f16522 (unchanged since f16452); that line was rendered from the bytes quoted below:

```
f9624  | 1<0>            U 5.0|13             D 5.0|     fader 1 just above 0
f9878  | 1<6>            U 5.0|13             D 5.0|     fader 1 near full
f10732 | 1             U 5.0|13             D 5.0|     fader 1 at 0 (cell = 0x20)
f16522 | 1    <6>        U 5.0|13             D 5.0|     fader 5 at 0xFF, GM 0xFF
```

`<k>` is byte 0x80 + k, shown as a placeholder. Line 1 is " 1" plus 12 cells plus "U 5.0";
line 2 is "13" plus 12 cells plus "D 5.0".
- Fader 1's cell is body[3] (line 1, column 2), e.g. f9624 body `28 20 31 80 20 …`.
- Fader 5's cell is body[7] (column 6), e.g. f16522 body `28 20 31 20 20 20 20 86 20 …`.
- Values seen: 0x20 at 0x00, then 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, rising with the
  level. **0x87 never appears.**
- f16522 was fetched at 128.37 s, after ChannelLevels (128.18 s) and FaderInfo (128.28 s) had
  reached 0xFF with the GM at 0xFF. It shows 0x86.
- The LCD did not change at the memory-mode switch (f32342 "unchanged"), at the bump, or during
  the GM move (not listed).

### LED blocks (LedStatus, ID 22)

body[0] = 0x4B = 75 blocks of 6 bytes; each block holds two RGB triples. Block view (from the
helper used for this record; the bytes are in the frames cited):

| Blocks | Channel mode, E3 start f1592 | Memory mode f32328 | Bump held f33040 | GM below full f37176 | E2 start f40 |
|---|---|---|---|---|---|
| 1–24 | `00 00 00 00 00 00` (faders at 0; G = level while a fader is up) | 1–8, 13: `46 00 00 46 00 00`; 9–12, 14–20, 22–24: `0A 00 00 0A 00 00`; **21: `46 46 00 46 46 00`** | block 1 `FF 00 00 FF 00 00` | as memory mode | block 5 `00 AB 00 00 AB 00`, others `00` |
| 25–48 | `00 0A 00 00 0A 00` | same | same | same | same |
| 52 | `0A 0A 0A 0A 0A 0A` | same | same | same | **`FF FF FF FF FF FF`** |
| 53 | `0A 0A 0A 0A 0A 0A` | same | same | **`37 37 FF 0A 0A 0A`** | **`37 37 FF 0A 0A 0A`** |
| 55 | `00 FF 00 00 FF 00` | `00 0A 00 00 0A 00` | same | same | `00 FF 00 00 FF 00` |
| 58 | `0A 0A 0A 0A 0A 0A` | `FF 00 00 FF 00 00` | same | same | `0A 0A 0A 0A 0A 0A` |
| 49, 64 / 50–51, 54, 59, 63, 65–67, 69–70 / 56, 72–73 / 57, 62, 71, 74–75 / 60 / 61 / 68 | `0A 0A 00 …` / `0A …` / `00 0A 00 …` / `FF FF FF …` / `46 46 00 46 46 00` / `00 00 FF 00 00 FF` / `FF 00 00 FF 00 00` | unchanged | unchanged | unchanged | unchanged |

Readings:
- **Memory mode, blocks 1–24 (OBSERVED match with SHOW04.ASC page 1):** the 9 blocks with
  `46` in R are exactly the recorded memories 1–8 and 13 of page 1 (`GROUP 1`–`8`, `13`). All
  other blocks of 1–24 read `0A` in R, except block 21.
- **Block 21:** memory 21 does not exist in the file, yet the block shows `46 46 00`, the
  colour of the constant block 60. The file's only sequence is `$SEQUENCE 0` (page 1, label
  KOTLIK) (HYPOTHESIS below).
- **Mode buttons (INFERRED):** blocks 55 (green, dim in memory mode) and 58 (red in memory mode)
  look like the channel-mode and memory-mode buttons' LEDs.
- **E2's block 52** (white) is unexplained.

### Writes and ConsoleEvent

- SmartSoft wrote **only** the two System messages, byte-identical to E2: f179
  `00 03 00 27 09 00 00` and f345 `01 03 00 27 07 00 00`. There was no other type-1 OUT and
  no System `[0x0A][page][0]` write (N6).
- **No ConsoleEvent (ID 20)** appears in either direction: no OUT data with ID 20, and no
  DirtyObjects record with ID 20. The console-side actions were all reported as state objects.

### Timing

- Idle poll-to-poll interval (submission to submission, n = 9 179): median **31.00 ms**,
  8 359 of 9 179 in the 30–32 ms bin, min 20.84 ms, max 40.42 ms.
- Next poll ≥ **10.35 ms** after a fetch (n = 423, max 22.62 ms); 15.39 ms and 15.84 ms after
  the two data writes; fetch iterations mostly 46–48 ms (311).
- Reply-header IN: median 1.90 ms, max 11.62 ms.
- Full download of 157 objects: 8.84–16.07 s, about 7.2 s.

## Reproduction of E2 (for the VERIFIED decision)

E3 is an independent session after a console power cycle and a re-enumeration (address 7 instead
of 10), on the same PC with the same SmartSoft and tools. "Byte-identical" means the same
bytes in the cited E3 and E2 frames. "Semantic" means every stated property holds but the counts
or values depend on the session.

| Fact | Reproduced? | E3 evidence |
|---|---|---|
| F40, F42 descriptors | **Byte-identical** (synthetic copies again, now after a power cycle). Not promotable: `--inject-descriptors` frames come from the hub cache, the same mechanism as E1, so the curator kept F40/F42 OBSERVED | f2, f4 |
| F41 product string | Byte-identical | f10 |
| F51 transport | Semantic: interface 1 only, one URB per transfer, every IN solicited (0 / 10 202), no gap > 100 ms | Structure |
| F52 OUT header | Semantic: w0 ∈ {0, 1, 2} only, w1 = 0 / 7, stale words 9 693 / 9 693; first poll byte-identical (12 zero bytes) | f11, f21, f177 |
| F53 reply header | Semantic: 9 692 / 9 692 polls answered, type-3 echo 423 / 423, type-4 w2..w5 = 0, idle reply byte-identical, type-4 payload always DirtyObjects (87 / 87), answer on the next poll 423 / 423, no re-fetch | f14, f26, f40652 |
| F54 payload layouts | Byte-identical outbound payloads; inbound: byte 1 = 0 (510 / 510), LE length (510 / 510), BE counts | f179, f345, f20, f352 |
| F55 sequence bytes | OUT byte-identical (0x00, 0x01). IN: the +1 rule holds (510, no break) and the wrap keeps byte 1 = 0x00 (f10942 → f10950). **Start value differs**: 0x00 after the power cycle vs E2's 0xF6 | f20, f10942, f10950 |
| F56 start-up order | Semantic with one **difference**: string reads byte-identical, then polls; the pending list was fetched before any write, and the first write came after reply header #40. **But** the first reply was idle and the pending list had 1 record (InputLevels) on the second reply, not 9 records on the first. The first fetch is ID 19, not 17 | f14, f20, f21, f179 |
| F57 DirtyObjects | Semantic (87 lists, format, records = fetches 423 / 423); **157-record list body byte-identical** | f352 vs E2 f384 |
| F58 the two writes | **Byte-identical**, at the **same positions**: after reply header #40 and #79 (E2: #40 / #79). Same answers on the next poll: [37] and the 157-record list. Idle replies just before: 37 / 37 (E2: 30 / 37). Download about 7.2 s (E2 7.3 s) | f177–f194, f343–f352 |
| F59 ConsoleVersion | Byte-identical body | f194 |
| F60 FileName | Byte-identical body | f1576 |
| F61 SystemSizes | Byte-identical body | f360 |
| F62 timing | Semantic: idle median 31.00 ms (E2 31.10), ≥ 10.35 ms after a fetch (E2 10.30), 15.4 / 15.8 ms after writes (E2 15.6 / 15.0) | Timing |
| F63 HID unused | Yes: 0 URBs on 0x81 / 0x02 in split and raw | Structure |
| F64 silent end | Yes: the last transfer is an idle poll / reply pair | f40649, f40652 |
| F65 health | Yes: 20 326 URBs status 0, 0 framing errors, no re-fetch | Structure |
| F66 mirroring | Semantic, extended to fader 1, fader 5 and the GM; same five objects per fader list | Census |
| F67 IDs and lengths | Same body length for every ID seen in both. New: InputLevels (19), 515 bytes. PlaybackInfo (17) not fetched in E3 | `startdiff` |
| F68 Patch bytes | Byte-identical body | f392 |
| F69 Online | Semantic: Online confirmed by the owner (39.76 s); download complete at 16.07 s | Action log |
| F70–F77 (bodies vs SHOW04.ASC) | **The bodies are byte-identical to E2**: `startdiff` finds 155 of 161 shared object instances identical. The 6 that differ are live state only: OutputLevels, FaderInfo, ChannelLevels, DisplayContent, LedStatus, and the first DirtyObjects list. `e2_asc_crosscheck.py` on the E3 JSONL repeats every result: Patch 512 / 512, Memory 16 / 16 with 768 / 768 levels, the scale rule 53 / 53, labels (12 pages, 96 channels), steps 19 / 19, the Sequence header, 7 records differing only in the cue number (H43), SystemSettings at body[6..11], Independents `00 FF 00 01` × 2 | f392, f408 ff., f624, f632, f384, f1560 |
| F78 live levels at start | Differs by design: all live levels 0 at the start of E3 (ChannelLevels f1608 and OutputLevels f1600 all zero) | f1600, f1608 |

## Hypotheses evaluated

Verdicts are at most OBSERVED; "twice" means E2 and E3.

| H | Verdict | Evidence |
|---|---|---|
| H4 version exchange | **Reproduced** (exchange; mismatch still UNTESTED) | f179 → f186 → f187 → f194 → f345 |
| H5 mirroring / ConsoleEvent | Mirroring **reproduced and extended** (faders 1 and 5, GM, mode switch, bump LED). ConsoleEvent: none in either direction again; console-side actions produce state objects, not events (OBSERVED twice). The PC → console part (SmartSoft virtual controls) stays UNTESTED | Census; Writes |
| H24 ConsoleEvent body | **n/a**: no ConsoleEvent in E3 (no virtual SmartSoft control was used) | — |
| H26 ChannelLevels Independents part | **UNTESTED** again: body[49], body[50] = 0 in all 48 payloads; the Independents were not touched | ChannelLevels payloads |
| H30 System 9 / 7 triggers | **Reproduced** (OBSERVED twice): same bodies, both after reply headers #40 and #79 in both sessions, answered by [37] and by the byte-identical 157-record list. A count rule (S11.14.11 countdowns) fits both sessions; a wall-clock timer does not (1.39 s after the first poll in E2, 1.26 s in E3) | f177–f352 |
| H31 console sequence counter | The start value **after a power cycle is 0x00** (f20; OBSERVED once). The H31 prediction ("0xEA without a power cycle") was **not testable**, because the console had been switched off. The "not reset by a new session" part stays UNTESTED. INFERRED, assuming every power-on starts at 0x00: before E2's first payload (0xF6) the counter had been advanced 246 times since that power-on, by something unknown | f20 |
| H32 FaderInfo / ChannelLevels / OutputLevels / LedStatus layout | **Confirmed in capture** for faders 1 and 5. FaderInfo record N byte 0 at body[1 + 4(N − 1)]; ChannelLevels body[c]; OutputLevels body[1 + d]; LedStatus `[75][75 × 6]` with fader N = block N (G bytes in channel mode). Full = **0xFF** with the GM at 0xFF. The sub-reading "0xAB is the top of fader travel" is **REJECTED**: E2's 0xAB coincided with FaderInfo body[194] = 0xAA, i.e. the GM below full (INFERRED cause, N2) | f9916, f16506, f9924, census |
| H33 SystemSizes body[4..7] | Bytes **reproduced** (f360 `… 00 0C 04 01`); meaning still OPEN. Possibly related: memory-mode LED block 21 and the page's sequence (new hypothesis below) | f360 |
| H37 cadence | **Reproduced** (same PC): idle median 31.00 ms, ≥ 10.35 ms after a fetch, no re-fetch | Timing |
| H38 HID interface unused | Supported again: 0 URBs on 0x81 / 0x02 | Structure |
| H36 / H40 label byte order | No new evidence: no label changed, all labels are ASCII. The researcher's "UNDECIDED" (consolelink-analysis.md §2.6) stands | — |
| H43 sequence step numbers | Contradiction **reproduced**: the Sequence records carry 1.0 … 7.0 again, the file says 265.0 … 271.0 | f624 |
| H39, H41, H42, H44, H45 | Bytes reproduced (cross-check identical); no new meaning | `e2_asc_crosscheck.py` on E3 |

**ConsoleLink-derived predictions** (consolelink-analysis.md §3.2):

| N | Prediction | Verdict | Evidence |
|---|---|---|---|
| N1 | FaderInfo `[48][48 × 4][Bumps][Master]`, body[193] = Bumps master, body[194] = GM | **body[194] = GM: CONFIRMED in capture.** It moves only during the logged GM steps: 0xFF → 0x83 at "half" (f37484, 51.4 % if 0xFF = 100 %), back to 0xFF at "full" (f38326); 31 changes, nothing else in FaderInfo changed then. **body[193] = Bumps master: UNTESTED**: 0x00 in all 80 payloads (and E2's 14), because the Bumps master was not moved | f37168 … f38326 |
| N2 | Live levels are post-GM outputs; E2's 0xAB = fader at full × GM ≈ 67 %; block 53 blinks while the GM is below full | **LED part CONFIRMED in capture:** block 53 = `37 37 FF 0A 0A 0A` from the first GM step (f37176, 47 ms after FaderInfo f37168 showed 0xF9) and `0A` × 6 again just after body[194] reached 0xFF (f38348 after f38326). E2 had the same pattern with body[194] = 0xAA. **Scaling part: consistent, not tested.** At GM 0xFF a full fader reads 0xFF in all three level objects (E2: 0xAB at GM 0xAA), but no channel was up while the GM moved. Arithmetic: 255 × 170 / 255 = 170, not 171. One fit is HYPOTHESIS only: GM 0xAA → 67 % (round), and 255 × 67 % rounded up (the ceil rule of F72) = 171 = 0xAB. That the 1248 LED is the BlackOut LED (the ML name) is unverified | f37176, f38348, f9916, E2 f32 / f40 |
| N3 | LedStatus block map: fader N → block N (G bytes), blocks 25–48 dim, 49–75 indicators | **CONFIRMED in capture** for blocks 1 and 5 (G bytes body[2] / [5] and body[26] / [29] track the level, 0x00–0xFF). Blocks 25–48 = `00 0A 00 00 0A 00` in every state seen. Memory mode turns blocks 1–24 into the page's memory slots with ConsoleLink's ML colour values: 0x46 recorded, 0x0A empty, 0xFF while the bump is held, but in **R** (red). Indicators: 53 (GM below full), 55 / 58 (mode, INFERRED), 52 (E2 only, unexplained) | f9610, f16246, f32328, f33040 |
| N4 | DisplayContent `[40][2 × 20]`, bar glyphs 0x80–0x87, fader 1's cell = body[3], "0x87 at full only if the GM is at full" | Layout **CONFIRMED in capture**: count 0x28; fader 1 = body[3], fader 5 = body[7]; 0x20 at 0; rising glyphs from 0x80. **"0x87 at full" CONTRADICTED**: with the GM at 0xFF and the level at 0xFF the cell is **0x86** (f16522); 0x87 never appears in E2 or E3. The cell has 8 states: blank plus 0x80–0x86 | f9624, f9878, f16522 |
| N5 | PlaybackInfo `[5][5 × 16]`, record 5 = crossfader pair at body[67] / [68], times at body[75..80] | **UNTESTED**: PlaybackInfo (17) was never listed or fetched in E3 (0 payloads); E2's f24 remains the only sample | — |
| N6 | System `[0x0A][page][0]` = page select | **No such write and no such object**: SmartSoft wrote only `09 00 00` / `07 00 00`; the console listed System only in the 157-record list (a = 0, 6, 1). The owner used page 1, and ConsoleStatus body[2] = 0x00 is consistent with CL-ML's "data[2] = 0-based page", but nothing tests N6 | f179, f345, f32320 |

**Other ConsoleLink claims touched:**

| Claim | Verdict | Evidence |
|---|---|---|
| T10, inbound bytes 0–1 as a 16-bit sequence | **Contradicted again**: E3's only wrap keeps byte 1 at 0x00 | f10942 `FF 00 …` → f10950 `00 00 …` |
| O10, ConsoleStatus byte 0 = fader-mode family (0 intensity, 1 memories) | **Consistent**: 0x00 in channel mode, 0x01 after the switch to memory mode (ML family values; on the 1248 "channel mode" corresponds to the ML intensity family, INFERRED) | f1584 `9E 00 07 00 17 00 00 00 00 00 01 00` → f32320 `B1 00 07 00 17 01 00 00 00 00 01 00` |
| S3, a pending list at connect | **Consistent**, but the content differs from E2: 1 record (InputLevels) on the second reply after a power cycle | f20 |

## Surprises

1. **Two unlogged events:**
   - Four extra fader-1 movements, two falls and two rises (83.62–91.35 s), while the log says fader 1 sat at full (owner: accidental, A2).
   - The memory-mode switch at 249.99 s, which the coordinator had placed in the window 20:42:52–20:44:36.

   Both are in the console's own data. For the other steps the log and the capture agree:
   - Step stamps come 0.2–2.2 s **before** the console's first change (the owner pressed Enter,
     then moved).
   - Confirm-Done stamps come 0.9–3.4 s **after** the console reached the end state ("fader5 at
     full" is the 3.4 s case).
2. **The bump in memory mode changed no level.** Only bump 1's LED changed. Two readings stay
   open:
   - (a) The bump is scaled by the Bumps master, which N1 puts at body[193] = 0x00.
   - (b) The live level objects do not show bump output.

   The owner's memory of the fixtures during the bump (20:44:37–20:44:45) would decide between
   them at zero risk.
3. **After a power cycle the console's sequence starts at 0x00**, and the pending list is a
   single InputLevels record, an object E2 never saw.
4. **FaderInfo, DisplayContent and PlaybackInfo are not in the 157-record list.** SmartSoft had
   no FaderInfo until the first fader move (77.90 s). A client that only follows the lists does
   not know the fader / GM state until something moves.
5. **0xAB was not a hardware limit.** E2's GM was below full (body[194] = 0xAA, LED block 53
   blinking).
6. **Memory-mode LED block 21** shows the colour `46 46 00` on a slot with no memory.

## Golden-test candidates (E3, by JSONL seq; decoders only)

| seq | f | Content |
|---|---|---|
| 9, 10 | 18, 20 | First payload after a power cycle: pseq 0x00, 1-record list (ID 19) |
| 13, 14 | 26, 28 | InputLevels, 515-byte body |
| 5471, 5475 | 10942, 10950 | Sequence wrap 0xFF → 0x00 with byte 1 = 0x00 |
| 4958 | 9916 | FaderInfo with fader 1 = 0xFF |
| 18742, 19163 | 37484, 38326 | FaderInfo body[194] = 0x83 / 0xFF (GM) |
| 792, 16160 | 1584, 32320 | ConsoleStatus channel mode / memory mode |
| 16164, 16520, 17013 | 32328, 33040, 34026 | LedStatus memory mode; bump held; released |
| 18588, 19174 | 37176, 38348 | LedStatus block 53 blinking / idle |
| 8261 | 16522 | DisplayContent with glyph 0x86 at full |
| 90, 173 | 179, 345 | The two System writes again (decoder only) |

## For the curator (proposed; nothing applied here)

**Promotion candidates (VERIFIED by an independent second session after a power cycle; same
PC):**
- ~~F40, F42~~ (not promoted by the curator: synthetic descriptor frames), F41 (already
  VERIFIED since E2; third read), F51, F52, F53, F54, F57, F58, F59, F60, F61, F62 (promoted
  for the qualitative cadence only), F63, F64, F65, F67 (for the shared IDs) and F68.
- F55's outbound part and its +1 / no-carry rule.
- F70–F77 as "the console returns these bodies".

The interpretation against the file is still one show. For F58 the promotion verifies what
SmartSoft sends, not that we may send it (safety rule 2). **Not** promotable:
- F55's start value (state-dependent).
- F56's pending-list content (differs).
- F66 / F78 (live state).
- F69 (owner statement).

Hypotheses:
- **H30** → CONFIRMED (OBSERVED twice). Add the reply-header count (#40 / #79) as the
  best-fitting trigger; causality stays INFERRED.
- **H31** → annotate: after a power cycle the counter starts at 0x00 (f20); the session-reset
  question needs two sessions without a power cycle.
- **H32** → CONFIRMED for the layout (OBSERVED, faders 1 and 5); the "0xAB top" sub-reading
  REJECTED (E3 full = 0xFF at GM 0xFF), keep the text.
- **H5** → annotate mirroring reproduced and extended; the ConsoleEvent part stays OPEN.
- **H26** (Independents part), **H33**, H24 → unchanged.
- **H37, H38** → supported by a second session.
- **H43** → reproduced.
- **N1** → body[194] = GM CONFIRMED (OBSERVED); body[193] = Bumps master stays HYPOTHESIS.
- **N2** → LED part CONFIRMED (OBSERVED); the scaling part stays HYPOTHESIS (E2 consistent).
- **N3** → CONFIRMED for blocks 1 and 5, blocks 25–48, and the memory-mode colours of blocks
  1–24.
- **N4** → layout CONFIRMED; "0x87 at full" REJECTED (0x86 at full).
- **N5** → untested.
- **N6** → nothing observed.

New hypotheses (IDs to be assigned):
1. **InputLevels (19)** body = `[00][u16 BE 512][512 DMX-input levels]`; it is the pending
   record after power-on. Test: a DMX source on DMX IN (owner decision; passive).
2. **ConsoleStatus (23)** body[0] = 0 channel mode, 1 memory mode (once each). Test: switch
   both ways with logged times.
3. **Bump level is scaled by the Bumps master** (FaderInfo body[193]). Test: the owner says
   whether the lights flashed in E3; or raise the Bumps master and bump again with logged
   times.
4. **Memory-mode LED block 21 = the page's sequence**: SHOW04.ASC's only sequence, page 1,
   KOTLIK, on memory fader 21. Connected to H33's "4 sequences per page". Test (zero risk):
   in memory mode, page 1, the owner looks at fader 21's bump LED colour and what fader 21
   plays.
5. **LED indicators:** block 53 blinks `37 37 FF / 0A 0A 0A` while the GM is below full;
   blocks 55 / 58 are the channel / memory mode LEDs. Test: the owner identifies the physical
   LEDs.
6. **GM arithmetic:** with a channel at full, the level bytes equal
   ⌈255 × round(GM × 100 / 255) / 100⌉ (fits E2's 0xAA → 0xAB). Test: E3(c) with one channel
   fader at full during the GM move.
7. **The 157-record list omits live objects** (FaderInfo, DisplayContent, PlaybackInfo). A
   list-following client learns them only when they change. Relevant to our design.

Docs for the curator:
- `reverse-engineering.md`:
  - Mark E3(a) / (b) / (c) done (E3(d) and (e) not run).
  - Add normalizer version 2 and its `note` keys `source` / `logTime` and header `notesLog` to
    "Capture format as implemented".
  - Mark the tooling row "diff of two captures" done (`startdiff`, `timeline`, `census`, `lcd`).
- `responses.md`: add the layouts above, InputLevels, the ConsoleStatus change.
- `state-model.md`: live objects are not in the full list.
- `consolelink-analysis.md`: the N1–N6 outcomes.

## Owner answers (2026-10-29, after the capture; added by Claude)

Archived verbatim in `2026-10-29-e3-owner-statements.txt` (SHA-256
`d3e0ee5b995e3e419d2fa96568644d94cb4a9599309a16f7b73f6c4d5dbde328`, after the owner's
follow-up Q5 about the panel labels), with Claude's reading marked INFERRED there. In short:

- The BUMPS fader ("solo bumps fader") was at **0 %** at the time of the bump (owner); nothing
  suggests it moved during the session (FaderInfo body[193] = 0x00 in all 80 payloads, but
  FaderInfo was not fetched during 255–265 s). A bump then drives the memory to the BUMPS level,
  so the memory-1 bump could not change any level while the memory fader was at zero. Supports
  reading (a) "bump level scaled by the BUMPS master" (H54) over reading (b), without excluding
  (b): the bump LED still reached 0xFF (F88).
- The unlogged fader-1 movements at 83.6–91.4 s (two falls to 0x00 and two rises to 0xFF, F91)
  were accidental operator movements.
- The fader moved in the "gm" steps is the **rightmost** fader, labelled **MASTER** on the panel
  (owner's follow-up Q5; the fader next to it is labelled **BUMPS**, with the INDEPENDENTS
  section below). Its movement landed in FaderInfo body[194] and the Blackout LED blinked
  while it was below full. The manual (printed p.56, "Master & Black Out function") states:
  "The Master fader proportionally limits all intensity outputs. As soon as the Master is
  faded under 100% the Blackout button will blink." and (printed p.56, "Bump Master") "The
  Bump Master sets the level to which bump buttons will flash."; printed p.12: bump playback
  modes "both use the level of the BUMPS fader". So body[194] = MASTER (Grand Master) level is
  OBSERVED (wire position, one session, time-correlated with the logged moves and the owner's
  panel label; the manual does not say that *only* the Master makes Blackout blink, it documents
  the behaviour), and BUMPS at 0 % is consistent with body[193] = 0x00 (wire position of BUMPS
  still HYPOTHESIS until it is seen moving). Where the parenthetical readings inside the
  statements file differ from the knowledge base, the knowledge base governs: block 53 =
  Blackout LED is INFERRED (F85, H56), the MASTER name is settled by the owner's Q5 (F82).
- The blinking LED was the **Blackout** button's LED, so LedStatus block 53 is the Blackout LED
  (OBSERVED pattern, owner identification).
- Memory fader 21 on page 1 holds a **sequence**, so LED block 21 lit for a sequence
  (new hypothesis 4 supported by the owner).

## Open follow-ups

1. ~~Ask the owner (zero risk), each one decides a hypothesis above:~~ **Answered, see
   "Owner answers" above.** Still open from that list: which physical LEDs are blocks 55 and 58.
   - Did fixture 1 light blue and fixture 2 red during the bump (20:44:37–20:44:45)?
   - Where is the Bumps master fader?
   - Which physical LEDs are blocks 53, 55 and 58?
   - What colour is memory fader 21's LED on page 1, and what does that fader play?
2. Next capture:
   - (i) H31: close and restart SmartSoft twice **without** a console power cycle; the second
     session's first pseq should continue from the first session's last one if the counter
     is not reset per session.
   - (ii) E3(c'): one channel fader at full while the GM moves (N2 scaling, new hypothesis 6).
   - (iii) The Bumps master moved, then a bump in channel mode and in memory mode.
   - (iv) E3(d) GO on a safe stack for N5.

   Same safety rules: backup first, passive capture, nothing sent by us.
3. Owner: switch Memory Integrity back on when the capture sessions are over (H7).
