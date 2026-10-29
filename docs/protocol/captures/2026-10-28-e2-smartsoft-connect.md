# 2026-10-28 E2: SmartSoft session capture (USBPcap)

- Date: 2026-10-28, capture 20:16:37–20:21:18 local time (CET, UTC+1); the pcap's first
  timestamp is 2026-10-28T19:16:37.470628Z, which matches the owner's start time.
- Experiment ID: **E2** (passive capture of a SmartSoft session; nothing was sent by us).
- Console: ETC SmartFade 1248, serial UNKNOWN (the device has no USB serial number, F40;
  rear panel not read). Firmware **3.0.2.9.0.43** (owner's Diagnostics reading, F48) and the
  console reported the same string over USB in this capture (f226, below). Operating mode
  not separately confirmed; SmartSoft went Online, which needs Normal mode (F16, INFERRED).
  USB device address 10 on bus 1 (root hub `USB\ROOT_HUB30\5&201A262C&0&0`, port 11).
- Host: owner's main PC, Windows 11 Pro build 26300 (x64), **Memory Integrity off** since the
  19:45 reboot (F49), `etchsp.sys` 2.00.000 loaded. SmartSoft 3.0.43 (package
  3.0.2.9.0.43b, binaries byte-identical to the installer, F47). USBPcap 1.5.4.0.
- Connection: USB A-B cable straight into the PC, as in E1.
- Show backup taken: **yes**, owner-reported: the show was saved to the SD card as
  `SHOW04.ASC` before SmartSoft was started. That file is not in the repository yet
  (`smartfade-sd-card-backup/` was copied at 20:10, before this backup, and has no
  `SHOW04.ASC`). **Update, same evening:** the owner added
  `smartfade-sd-card-backup/SHOW04.ASC` (SHA-256 `559636399c1d82e0…7a90`); see
  "Cross-check against SHOW04.ASC" at the end.
- Capture command (owner, elevated):
  `USBPcapCMD.exe -d \\.\USBPcap1 -o <path>\2026-10-28-e2-smartsoft-connect.pcap -A --inject-descriptors`,
  started 20:16:37, stopped at 20:21:18 by `Stop-Process` (buffered bytes at the very end
  may be missing; the file parses completely and every console URB in it has both its
  submission and its completion).
- Rig: as in E1 (owner's statement): 4 RGB fixtures, 3 channels each, DMX 5, 8, 11, 14.

**Conventions in this record.** `fN` = pcap frame number in the committed
`2026-10-28-e2-smartsoft-connect.pcap` (the frame that carries the bytes: the submission
frame for OUT, the completion frame for IN). `seqN` = record number in the normalized
`.jsonl`. Times are capture-relative seconds (`t` in the JSONL = URB completion time). Hex
bytes are quoted only from those frames. Field names (w0..w5, payload seq / id / body,
DirtyObjects records) are the S11 / H15–H20 vocabulary and are used as labels; whether the
labels fit is what this record evaluates.

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-28-e2-smartsoft-connect.pcap` | `8F350496438DE66E4AEA70B6BE5206ADBBFEC4D8A61F8C3782FA6A122580CB82` | 660 602 B, 12 966 frames, 0–125.252552 s | yes (**the evidence**) | Console-only split of the raw file, made by Claude in the same session (tshark 4.0.3 from the owner's portable Wireshark) with `tshark -r <raw> -Y "usb.device_address == 10" -F pcap -w <this>`. Classic pcap, little-endian, µs timestamps, link type 249 (USBPcap) |
| `2026-10-28-e2-smartsoft-connect.raw-all-devices.pcap` | `7ADCCDD8B379F6159B976108DD30031984897538DE104B71617667E06333531D` | 4 818 678 B, 51 028 frames, 0–280.540118 s | **no** (`.gitignore`: `*.raw-all-devices.pcap`) | Raw USBPcapCMD output for root hub `USBPcap1`: 7 device addresses, including the owner's keyboard and mouse, so it contains keystrokes. Kept locally only to prove the split complete (the raw file was deleted on 2026-10-29 after the completeness checks; its hash stays recorded) (below). Nothing from other devices was copied anywhere |
| `2026-10-28-e2-smartsoft-connect.jsonl` | `808834C94D42CEFB9B3F930F267C4D8714A4FCADE757B73A0CE5A12502AA4E8B` | 1 548 587 B, 6 486 lines | yes (derived) | Normalized capture: 1 header, 6 483 `frame` records (one per URB), 2 `note` records. Reproduce with the command in `tools/protocol-analyzer/README.md` (two runs gave identical bytes) |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `2540249B3A0179E13EBDAE381D768E1DE41D29AE52DECD83F57BFA4214E2BA7F` | | yes | Normalizer, stdlib only, parses pcap + USBPcap header itself |
| `tools/protocol-analyzer/hsp_summary.py` | `CC9727E76BC64362C92EA78F9BE87916FA4088A7C94E39E8BDD8C2EA61194FDC` | | yes | Structural checks, decoded log, payload listing and diff used for every number below (this revision only renames the printed payload sequence byte to `pseq=`; output otherwise identical to the revision `8CEF838F…E513` first used) |

**Completeness of the split.** The normalizer run on the raw file with `--device-address 10`
found 12 966 frames for address 10 (38 062 frames of other devices skipped, data discarded)
and produced 6 483 records identical to those from the split in every field except the pcap
frame numbers (e.g. split f16 = raw frame 4130; the FileName payload split f88 = raw frame
4346, 66 bytes on EP 0x83 at 28.899414 s). The raw file has no address-10 frames after
125.252552 s and none on EP 0x81 or 0x02 at any time. **Independent cross-check:** tshark
4.0.3's `usb.capdata` for all 6 478 data-carrying bulk frames equals the JSONL `data`
byte for byte (0 mismatches).

**Normalization choices** (details in `tools/protocol-analyzer/README.md`): one record per
URB, submission and completion merged by IRP ID, so the zero-length OUT completion and IN
submission frames are not separate records (their frame numbers and times are kept in
`frames` and `tSubmit`); the six frames with IRP ID 0 that `--inject-descriptors` writes at
t = 0 are kept and marked `"synthetic": true`.

## Action log (capture-relative)

Only two times come from the owner; everything else is read from the capture. The owner
kept no timed action log.

| t (s) | Source | Event |
|---|---|---|
| 0.000000 | owner + capture | USBPcapCMD started (20:16:37 local). f1–f6 (seq1–3): **synthetic** frames from `--inject-descriptors`: device descriptor f2 `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01`, the 64-byte configuration descriptor f4 (byte-identical to E1's, F42), SET_CONFIGURATION(1) f5/f6. Not bus traffic of the session |
| 0–28.47 | capture | No console traffic at all. Owner: SmartSoft launched after the capture started (exact time unknown) |
| 28.471968–28.478094 | capture | Two control transfers on EP0: GET_DESCRIPTOR(STRING 0) → f8 `04 03 09 04` (LANGID 0x0409); GET_DESCRIPTOR(STRING 2, LANGID 0x0409, setup `80 06 02 03 09 04 00 01`) → f10 `1C 03 45 00 54 00 43 00 20 00 53 00 6D 00 61 00 72 00 74 00 46 00 61 00 64 00 65 00` = "ETC SmartFade". INFERRED: the driver's answer to sfEngine's "product string" IOCTL 0x222008 (S11.10); the PID / bcdDevice IOCTLs caused no bus traffic |
| 28.478515 | capture | First bulk transfer: poll f11 `00 00 00 00 00 00 00 00 00 00 00 00` |
| 28.480263 | capture | First reply f14 `04 00 34 00 00 00 00 00 00 00 00 00` (type 4, 52 bytes follow) and payload f16 (DirtyObjects, 9 records, below) |
| 28.511–28.899 | capture | 9 Fetches (f17…f81), each answered on the next poll: PlaybackInfo, FaderInfo, LedStatus, OutputLevels, ChannelLevels, ConsoleStatus, ConsoleStatus, DisplayContent, FileName (f88 = "SHOW04.ASC") |
| 28.93–29.83 | capture | Idle polls, every reply `04 00 00 00 00 00 00 00 00 00 00 00` (first f92) |
| 29.872363 | capture | **First PC write**: header f209 `01 00 07 00 00 00 00 00 00 00 00 00`, payload f211 `00 03 00 27 09 00 00` (System, body `09 00 00`), sent after the 40th reply header of the session |
| 29.894249 | capture | Next poll's reply: DirtyObjects f218 `00 00 07 00 28 00 01 25 00 00 00 00` (1 record: ConsoleVersion) |
| 29.936725 | capture | Fetch f219 `02 00 00 00 25 00 00 00 00 00 00 00` → reply f224 `03 00 12 00 25 00 00 00 00 00 00 00`, payload f226 `01 00 0D 00 25 0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33` = ConsoleVersion "3.0.2.9.0.43" |
| 31.133470 | capture | **Second PC write**: header f375 `01 00 07 00 00 00 00 00 00 00 00 00`, payload f377 `01 03 00 27 07 00 00` (System, body `07 00 00`), after the 79th reply header (37 replies after the ConsoleVersion payload) |
| 31.153263 | capture | Next poll's reply: f382 `04 00 18 03 00 00 00 00 00 00 00 00`, payload f384 = DirtyObjects with **157** records |
| 31.179–38.441953 | capture | Full download: 157 Fetches f385…f1633, all answered; last payload ChannelLevels f1640 |
| 38.47–42.18 | capture | Idle polls |
| 42.216586–45.902536 | capture | 13 DirtyObjects lists (f2130 … f2714, 3–5 records each, 61 records) naming only OutputLevels, FaderInfo, ChannelLevels, LedStatus, DisplayContent; 61 Fetches. One level byte changes repeatedly in all five objects (see "Front-panel activity") |
| unknown | owner | "Moved ALL faders fully down at some point (to turn every light off)"; no address or memory changed. INFERRED: the 42.2–45.9 s activity is this action (only window with front-panel-related changes); not confirmed |
| unknown | owner | SmartSoft showed no dialog, went Online immediately and worked |
| 45.93–125.251168 | capture | 2 555 idle polls, every reply type 4 with w1 = 0 |
| 125.252552 | capture | Last transfer: reply f12966 `04 00 00 00 00 00 00 00 00 00 00 00` to poll f12963. No closing message of any kind. Owner: SmartSoft closed by the owner (time not reported); INFERRED from the capture: ≈ 125.25 s |
| 125.25–280.54 | capture (raw) | No console traffic |
| ≈ 280.5 | owner | USBPcapCMD stopped by `Stop-Process` (20:21:18 local) |

Nobody is known to have touched the console during the session other than the owner's fader
action (unconfirmed).

## Observed result

### Structure (no interpretation)

| Endpoint | Transfer | Direction | URBs | Sizes (bytes × count) |
|---|---|---|---|---|
| 0x04 (interface 1) | bulk | OUT | 3 233 | 12 × 3 231, 7 × 2 |
| 0x83 (interface 1) | bulk | IN | 3 245 | 12 × 3 005, then 44 × 108, 18 × 20, 519 × 16, 143 × 16, 56 × 15, 456 × 14, 200 × 14, 46 × 11, 32 × 10, 8 × 3, 66 × 2, 27 × 2, and 90, 86, 792, 52, 51, 23, 22, 137, 13 once each |
| 0x80 / 0x00 (EP0) | control | IN / OUT | 4 / 1 | 2 synthetic descriptor reads + 2 string reads / 1 synthetic SET_CONFIGURATION |
| **0x81, 0x02 (interface 0, HID)** | interrupt | | **0** | none in 0–125.25 s (split) and none in 0–280.54 s (raw) |

- Every URB completed with USBD status 0 (success).
- Bulk traffic runs from 28.478 s to 125.253 s without any gap > 100 ms between consecutive
  transfers.
- The OUT side is a strict pattern: 12-byte transfers, except twice a 12-byte transfer
  followed by a 7-byte one. Every IN 12-byte transfer directly follows an OUT 12-byte
  transfer whose first two bytes are `00 00`, and every longer (or second 12-byte) IN
  transfer directly follows such an IN 12-byte transfer (0 exceptions in 3 245). The
  console never sends an IN transfer without a preceding OUT.
- IN transfers larger than 64 bytes (up to 792) appear as single URBs, i.e. the driver issues
  one bulk URB per `ReadFile`, with the 12-byte header and the payload as separate URBs
  (OBSERVED at URB level; the split into 64-byte packets is below what USBPcap records).
  This settles the S11.0 limitation "whether the driver splits/aggregates".

### Decoded sequence (labels from S11)

Classification by `hsp_summary.py` with **0 framing errors**:
3 001 polls (OUT w0 = 0), 228 fetches (w0 = 2), 2 data headers (w0 = 1) each followed by
exactly w1 = 7 data bytes; 3 001 reply headers, 244 IN payloads. Reply types: 228 type 3
(all with payload), 16 type 4 with payload (all DirtyObjects), 2 757 type 4 with w1 = 0.

- **Fetch → object on the next poll.** All 228 fetches were answered by the reply to the
  very next poll (type 3), and both data writes were followed, at the next poll, by a
  type-4 DirtyObjects list. No fetch was ever re-sent.
- **Type-4 reply headers** have w2..w5 = 0 in all 2 773 cases; **type-3** reply headers
  echo the fetch's (id, a, b, c) in 228/228.
- **Stale words:** in all 3 002 poll/data headers after the first, w2..w5 equal the w2..w5
  of the most recent fetch or reply header (e.g. poll f19 `00 00 00 00 11 00 00 00 00 00 00 00`
  repeats fetch f17's id 0x11). The first poll f11 is all zero.
- **Inbound payload** `[seq][byte1][u16][id][body]`: byte1 = `00` in 244/244; the u16 at
  bytes 2–3 read **little-endian** equals payload length − 5 in 244/244 (e.g. f16
  `F6 00 2F 00 28 …`: 0x002F = 47 = 52 − 5); id (byte 4) always one of the IDs below.
- **Outbound payload** `[seq][u16 LE len][id][body]`: f211 `00 03 00 27 09 00 00` and f377
  `01 03 00 27 07 00 00`: seq 0 then 1, length 3 = w1 − 4, id 0x27 = 39 (System).
- **Sequence bytes:** IN payload byte 0 runs 0xF6 (f16) … 0xFF (f88), 0x00 (f218) … 0xE9
  (f2746), +1 per payload across type-3 and type-4 payloads, 244 payloads, no break. OUT:
  0x00 (f211), 0x01 (f377). The console's first value 0xF6 = 246 is unexplained (no earlier
  HSP session is known since the driver first loaded at 19:45).

### DirtyObjects (ID 40)

16 lists. In all 16 the payload length is 5 + 2 + 5 × count with count as a **big-endian**
u16 at body[0..1]; every record is `[id][u16 BE a][b][c]`; c = 0 in every record.

- f16 (first poll reply): count `00 09`: 17, 14, 22, 13, 15, 23, 23, 21, 36 (all a = b = c = 0).
  ConsoleStatus (23) is listed twice and fetched twice (f57, f65) with identical bodies.
- f218: count `00 01`: 37 (ConsoleVersion).
- f384: count `00 9D` = 157: 38 SystemSizes; 39 System a = 0; 39 System a = 6; 41
  SystemSettings; 5 Patch; 8 Page a = 0; 0 Memory (a = 0, b = 0..7 and 12); 8 Page a = 1..11;
  0 Memory (a = 11, b = 0..6); 2 Sequence (0, 0); 3 SequenceMain; 4 StepContent a = 0..18;
  9 ChannelText a = 0..95; 12 Independents; 39 System a = 1; 36 FileName; 23 ConsoleStatus;
  22 LedStatus; 13 OutputLevels; 15 ChannelLevels.
- f2130 … f2714: 13 lists of 3–5 records from {13, 14, 15, 21, 22}, all (0, 0, 0).
- **The 228 records, in order, are exactly the 228 fetches, in order** (w2..w5 = id, a, b, c),
  and every fetch comes after the list that named it.
- Qualifier echo in the body: Memory body[0..1] = (a, b) in 16/16; Sequence 1/1;
  StepContent body[0..1] = (a, 0) in 19/19 (refined later in this record: body[1..2] is the u16 BE cue number, F74 / H41, and body[1] is 0 only because every cue number here is below 25.6); Page body[0] = a in 12/12; ChannelText
  body[0] = a in 96/96. Memory a ∈ {0, 11}; Page a = 0..11.

### Object payloads seen (console → PC)

| ID | Name (S11.6) | Fetches | Body length | What the bytes show (OBSERVED; readings marked) |
|---|---|---|---|---|
| 0 | Memory | 16 | 138 | body[0..1] = (a, b); a text field preceded by `03 06` (f440 `… 03 06 00 4F 00 50 00 4F 00 4E 00 41 00 00 …`, "OPONA") |
| 2 | Sequence | 1 | 132 | body[0..1] = (0, 0); text after `03 06` (f656) |
| 3 | SequenceMain | 1 | 46 | f664 body `13 02 00 00 01 00 01 00 03 06 00 …`; body[0] = 0x13 = 19 = number of StepContent objects listed (HYPOTHESIS: step count) |
| 4 | StepContent | 19 | 13 | body[0] = a = step index |
| 5 | Patch | 1 | 514 | f424 body `02 00` (BE 512) + 512 bytes; entry i (DMX address i + 1) holds 1..46 for i = 0..45, then 0x32 (i = 46), 0x30 (i = 47), 0x18 (i = 96), else 0. HYPOTHESIS: 1-based channel number per DMX address, 0 = unpatched; check against `SHOW04.ASC` (**done: 512/512 match**, cross-check (1)) |
| 8 | Page | 12 | 39 | body[0] = page (a), then `03 06` |
| 9 | ChannelText | 96 | 39 | body[0] = a = 0..95 (96 objects although SystemSizes says 48 channels), then `03 06` |
| 12 | Independents | 1 | 85 | f1592 body `02 00 FF 00 01 03 06 …`; body[0] = 2, 85 = 1 + 2 × 42 (HYPOTHESIS: 2 records of 42 bytes) |
| 13 | OutputLevels | 15 | 514 | body `02 00` (BE 512) + 512 levels; f48 body[6] = 0xAB, all other level bytes 0 |
| 14 | FaderInfo | 14 | 195 | f32 body[0] = 0x30 = 48; 1 + 48 × 4 = 193, so **2 bytes beyond the records** (body[193] = 0x00, body[194] = 0xAA); non-zero: body[17] = 0xAB, body[83] = 0x01, body[99] = 0x01 |
| 15 | ChannelLevels | 15 | 51 | f56 `FB 00 33 00 0F 32 00 00 00 00 AB …`: body[0] = 0x32 = 50, payload 56 bytes; body[5] = 0xAB; body[49], body[50] = 0 in all 15 |
| 17 | PlaybackInfo | 1 | 81 | f24 body[0] = 5; 81 = 1 + 5 × 16 |
| 21 | DisplayContent | 11 | 41 | f80 body[0] = 0x28 = 40, then 40 bytes of ASCII-range text, `20 31 20 20 20 20 84 …` (" 1    \x84        U 5.0" + "13             D 5.0" if read as two 20-character lines, HYPOTHESIS: LCD contents) |
| 22 | LedStatus | 14 | 451 | f40 body[0] = 0x4B = 75; 451 = 1 + 75 × 6; body[26] = body[29] = 0xAB |
| 23 | ConsoleStatus | 3 | 7 | `00 00 00 00 00 01 00` each time (f64, f72, f1616) |
| 36 | FileName | 2 | 61 | f88 body `1E 53 00 48 00 4F 00 57 00 30 00 34 00 2E 00 41 00 53 00 43 00 00 …`: body[0] = 0x1E = 30, then 30 UTF-16 code units **little-endian**: "SHOW04.ASC" + zeros. Equals the owner's backup name. Same bytes again in f1608 |
| 37 | ConsoleVersion | 1 | 13 | f226 body[0] = 0x0C = 12, then ASCII "3.0.2.9.0.43" |
| 38 | SystemSizes | 1 | 8 | f392 body `30 02 00 18 00 0C 04 01`: 0x30 = 48, BE 0x0200 = 512, 0x18 = 24, then `00 0C 04 01` (unexplained; HYPOTHESIS: 0x0C = 12 pages and 4 = sequences per page, F3) |
| 39 | System | 3 | 3 | f400 `00 00 00`, f408 `06 00 00`, f1600 `01 00 00`: body[0] = the record's a |
| 40 | DirtyObjects | (never fetched) | 2 + 5n | above |
| 41 | SystemSettings | 1 | 18 | f416 body `00 64 5A 3C 00 00 00 00 00 01 7F 00 00 32 00 32 00 00`: **18** bytes, the length the S11.14.11 parser consumes (the serializer emits 19) |

IDs never seen in either direction: 1, 6, 7, 10, 11, 16, 18, 19, **20 (ConsoleEvent)**,
24–35, 42. The PC wrote only ID 39.

**Text encoding.** FileName is unambiguous: body[0] counts 30 code units and the first one,
body[1..2] = `53 00`, is "S" only when read little-endian. The labels in Memory, Sequence,
Page and ChannelText bodies follow two constant bytes `03 06` as `00 xx` pairs: big-endian
if the text starts right after `03 06`, little-endian if it starts one byte later. This
capture cannot tell which (HYPOTHESIS; the text helpers `sub_10013970` / `sub_100139F0`
named in S11 open lead 1 would decide). **Update:** settled structurally in favour of
big-endian by the SHOW04.ASC cross-check (section (3) at the end).

### Front-panel activity (42.2–45.9 s)

Across all 14 FaderInfo payloads exactly one byte changes, body[17] (record 4, byte 0 if
the records start at body[1]):

| f | t | FaderInfo body[17] |
|---|---|---|
| 32 | 28.575617 | AB (start of session) |
| 2146 / 2184 / 2222 | 42.308742 / 42.527727 / 42.746721 | 8A / 82 / 7F |
| 2308 / 2354 | 43.333098 / 43.595902 | 95 / AB |
| 2400 / 2438 | 43.866650 / 44.082052 | 08 / 00 |
| 2492 / 2530 / 2592 | 44.396037 / 44.613609 / 44.969583 | 66 / 9E / AB |
| 2646 / 2676 / 2722 | 45.326980 / 45.494968 / 45.764536 | 9E / 8A / 81 |

In the same window the only other changing bytes are ChannelLevels body[5] (AB → … → 00 →
… → AB → … → 81, last f2730 `E7 00 33 00 0F 32 00 00 00 00 81 …`), OutputLevels body[6]
(last f2738, 0x81), LedStatus body[26] and body[29] together (last f2746, both 0x81) and
DisplayContent body[7] (values 0x80, 0x82, 0x83, 0x84, and 0x20 while the level was 0x00).
The level starts at 0xAB, returns to exactly 0xAB twice and never exceeds it. After 45.9 s
nothing is dirty until the end; the last value in the capture is **0x81, not 0x00**.

INFERRED (consistent, not proven): one physical fader (fader 5: body[17] is 0-based record index 4, the fifth record, F66),
channel 5, DMX address 5 (Patch entry 4 = 5) moved down, up, down, up and down to a middle
position. All other FaderInfo, ChannelLevels and OutputLevels bytes were 0 for the whole
session, so "all faders down" (owner) may mean this one fader. Whether it was later moved
fully down after SmartSoft stopped polling, or whether "fully down" is imprecise, is
UNKNOWN: the console cannot report anything after 125.25 s because only polls bring data.

**Update 2026-10-29 (E3, [2026-10-29-e3-console-actions.md](2026-10-29-e3-console-actions.md)):**
- The layout reading above is confirmed for faders 1 and 5: FaderInfo body[1] / body[17],
  ChannelLevels body[1] / body[5], OutputLevels body[2] / body[6], LedStatus blocks 1 / 5.
- With the Grand Master at full, a fader at full reads **0xFF**, not 0xAB.
- In this E2 capture the Grand Master was below full: FaderInfo body[194] = 0xAA (f32), which
  E3 shows to move with the Grand Master. LedStatus block 53 (body[313..318]) = `37 37 FF 0A 0A
  0A` (f40), the pattern that E3 shows exactly while the Grand Master is below full.
- The 0xAB ceiling is therefore INFERRED to be a full fader scaled by the Grand Master. The
  exact arithmetic is HYPOTHESIS: 255 × 170 / 255 = 170, not 171.

### Timing

- Idle poll-to-poll interval (submission to submission, no fetch or payload in between,
  n = 2 754): median **31.10 ms**, 2 560 of 2 754 in the 30–32 ms bin, min 20.87 ms,
  max 45.30 ms. All polls: median 31.15 ms; the 46–48 ms bin (185) holds most iterations
  with a fetch.
- Fetch completion → next poll: 10.30–28.36 ms (n = 228). Data write completion → next
  poll: 15.59 ms and 15.05 ms.
- Reply-header IN URB, submission → completion: median 1.92 ms, max 6.97 ms.
- Full download of 157 objects: 31.18–38.44 s (≈ 7.3 s).
- INFERRED: Windows' default 15.625 ms timer tick turns `msleep(20)` into ≈ 31.25 ms and
  `msleep(10)` into ≈ 15.6 ms, which matches the peaks; the S11.9 "≈ 20 ms" ignored the
  tick.

## Hypotheses evaluated

Verdicts: **CONFIRMED-in-capture** (status at most OBSERVED: one capture, one PC, one
session), **REJECTED**, **UNTESTED**.

| H | Verdict | Evidence |
|---|---|---|
| H3 poll / dirty list / fetch | **CONFIRMED-in-capture** | 3 001 polls, every IN solicited; type-4 DirtyObjects lists (16) drive all 228 fetches by ID + 3 qualifiers (not by name); objects arrive as type-3 replies on the next poll. First list f16, fetch f17, object f22/f24 |
| H4 version exchange | **CONFIRMED-in-capture** for the exchange; **UNTESTED** for "refused on mismatch" (versions matched) | System `09 00 00` f211 → DirtyObjects [37] f218 → fetch f219 → ConsoleVersion "3.0.2.9.0.43" f226 → System `07 00 00` f377 → full list f384. Exactly the S11.14.11 order. System 9 after reply header 40, System 7 after reply header 79; INFERRED: timer- or count-driven, not sent on receipt of the version (H30) |
| H5 front panel mirrored as objects; PC control as ConsoleEvent | **CONFIRMED-in-capture** for the mirroring part (FaderInfo, LedStatus, DisplayContent plus ChannelLevels, OutputLevels changed with the level, f2130–f2746); **UNTESTED** for ConsoleEvent (no ID 20 anywhere) | Front-panel section above |
| H6 no direct "set level" message | UNTESTED | SmartSoft sent no control at all |
| H15 OUT 12-byte headers, stale w2–w5 | **CONFIRMED-in-capture** | 3 231 headers: w0 ∈ {0, 1, 2} only, w1 = 0 for types 0 and 2, both type-1 headers followed by exactly w1 = 7 bytes (f209/f211, f375/f377); stale words 3 002/3 002 (example f19) |
| H16 IN reply header, payload, echo | **CONFIRMED-in-capture** | 3 001/3 001 polls answered by one 12-byte header with w0 ∈ {3, 4}; payload of exactly w1 bytes after every w1 > 0 header (244/244); type-3 echo 228/228 (e.g. f22 `03 00 56 00 11 00 …` after fetch f17). Type 4 with w1 > 0 always carried DirtyObjects; type-4 w2..w5 always 0 |
| H17 per-direction sequence byte | **CONFIRMED-in-capture** | IN 0xF6 (f16) → 0xFF (f88) → 0x00 (f218) → 0xE9 (f2746), +1 per payload, 244 payloads, both reply types; OUT 0x00 (f211), 0x01 (f377). Start values: console 246 (unexplained), SmartSoft 0 |
| H18 payload layouts | **CONFIRMED-in-capture**; the open points resolved: inbound bytes 2–3 = **u16 LE** length − 5 (244/244), inbound byte 1 = 0x00 always (meaning UNKNOWN) | f16, f211, f377 and all 244 IN payloads |
| H19 object IDs = name-table index | **CONFIRMED-in-capture** for the 21 IDs seen (content matches the name for 5, 13, 15, 36, 37, 38, 39, 40, 41; structure matches S11.6 for 14, 17, 21, 22; qualifier semantics for 0, 2, 4, 8, 9). Sub-prediction "first fetches carry 37 / 38 / 41" **REJECTED**: the console's own first list decides (first fetch f17 is 17 PlaybackInfo; 37 only after System 9; 38 and 41 only in the full list) | Table above; version string f226 |
| H20 DirtyObjects body and fetch order | **CONFIRMED-in-capture** | 16/16 lengths = 7 + 5 n with BE count; 228/228 records = fetches in order; no deduplication (23 twice in f16 → f57, f65) |
| H21 (untested part: interface chosen by IOCTL PID) | No change (not visible on the bus). Side result: the string requests at 28.47 s fit IOCTL 0x222008 (INFERRED) | f8, f10 |
| H22 BE body fields; only LE = header words + outbound length | **REJECTED as worded**: numeric u16 fields in bodies are **BE** (DirtyObjects count and a, OutputLevels / Patch count `02 00`, SystemSizes `02 00`) — CONFIRMED-in-capture; but two more LE items exist: the **inbound** length word (bytes 2–3) and the **FileName** characters (UTF-16LE, f88). The FileName question of S11.6 is settled for console → PC in favour of the application parser (LE); the mirror parser and serializer read / write BE (S11.14.6), so SmartSoft's mirror copy and anything it sends back as FileName would be byte-swapped (INFERRED; SmartSoft sent no FileName) | f16, f48, f424, f392, f88 |
| H23 poll cadence | **REJECTED as worded** for the 20–25 ms peak (observed idle median 31.10 ms, 30–32 ms bin); **CONFIRMED-in-capture** for "next poll ≥ 10 ms after a fetch" (min 10.30 ms, n = 228); re-fetch after > 10 polls **UNTESTED** (never needed) | Timing section |
| H24 ConsoleEvent body | UNTESTED | No ID 20 in either direction; the console reported the fader through FaderInfo, not as an event |
| H25 Memory / Sequence qualifiers | UNTESTED as designed (no memory recorded); consistent: Memory body[0..1] = (a, b) 16/16, a ∈ {0, 11}, Page a = 0..11 (12 pages), b < 24 = SystemSizes body[3]. Mapping (a, b) to console page / fader needs `SHOW04.ASC` | f440–f464, f432 |
| H26 ChannelLevels `[n][n − 2 levels][IND1][IND2]` | **CONFIRMED-in-capture** for n = 50 and payload length 56 (15/15); the Independents part UNTESTED (body[49], body[50] = 0 throughout) | f56, f2154, f2730 |
| H27 SystemSizes `[48][02 00][24]…` | **CONFIRMED-in-capture** | f392 body `30 02 00 18 00 0C 04 01`; body[4..7] unexplained |
| H28 (rejected provisionally) event-driven HID | E2 **supports the rejection** with stronger evidence: the console's own FaderInfo shows a fader moving (42.3–45.8 s) and the HID IN endpoint delivered nothing | 0 frames on 0x81 |
| H29 host enables the HID stream when going Online | **REJECTED** (OBSERVED, one session) for SmartSoft with this 1248: SmartSoft went Online, did a full download and saw a fader move, and in 0–280.54 s there was **no** URB at all on EP 0x02 (no HID output report sent by anyone) and none on EP 0x81 (no input report delivered). The two HSP writes (System 9 / 7) did not start an HID stream either. INFERRED: the Windows HID class driver keeps interrupt-IN reads pending on a started HID device, so any report would have produced at least a completion or a re-submission frame. What interface 0 is for remains UNKNOWN (not used by SmartSoft in this session; possibly other products or the firmware path; no evidence either way) | Structure table |

Not touched: H1, H2, H7, H8, H9–H14.

## Unexpected or new findings

1. The very first poll reply is already a DirtyObjects list (f14/f16): the console had 9
   objects pending before the PC wrote anything, and the PC fetched them before the version
   handshake. **Reading the console needs no data (type-1) write** (polls and fetches are still transmissions whose console-side effects are UNKNOWN): polls and fetches alone returned
   PlaybackInfo, FaderInfo, LedStatus, OutputLevels, ChannelLevels, ConsoleStatus,
   DisplayContent and FileName (OBSERVED). ConsoleVersion and the full show content appeared
   only after the two System writes (INFERRED causal: each list came on the very next poll,
   after 30 and 37 consecutive empty replies respectively; one observation each).
2. **SmartSoft wrote only two 7-byte System messages in the whole session**; it did not
   upload a show or anything else to the console. Going Online here meant downloading
   console → PC. (OBSERVED for this session, in which nothing was edited in SmartSoft.)
3. The console's full list (f384) mirrors SmartSoft's own full-upload order of S11.14.11:
   System a = 0, System a = 6 at the start and System a = 1 near the end.
4. Session end is silent: the last transfer is an ordinary idle poll and reply; no
   goodbye message, no control transfer.
5. SystemSettings arrives with 18 body bytes (parser length), FaderInfo carries 2 bytes
   after its 48 records, ChannelText has 96 instances on a 48-channel console.
6. The device answers GET_DESCRIPTOR(STRING) normally on the bus (f8, f10, status 0); the
   E1 failure of string reads through the hub IOCTL was a host-side path problem (INFERRED).
7. USBPcap's injected descriptors (f2, f4; second enumeration after a reboot, device address
   10 instead of E1's 50) are byte-identical to E1's device and configuration descriptors.

## Golden-test candidates (this capture, by JSONL seq)

Decoder tests only. No encoder for a write message (System) until the curator has promoted
it, its effect on the console is understood and known to be harmless (safety rule 2 in
reverse-engineering.md), and `/hardware-safety-check` has passed. A byte-identical
reproduction in E3(a) verifies that SmartSoft sends these bytes, not that we may send them.

| seq | f | Content | Use |
|---|---|---|---|
| 6 | 11 | First poll, 12 bytes all zero | poll header shape |
| 7, 8 | 14, 16 | Type-4 header + DirtyObjects payload, 9 records, seq 0xF6 | reply header, payload header, DirtyObjects parser |
| 9, 10, 11, 12 | 17, 19, 22, 24 | Fetch PlaybackInfo, stale-word poll, type-3 echo, PlaybackInfo payload | fetch / echo matching |
| 43, 44 | 86, 88 | FileName "SHOW04.ASC" (UTF-16LE) | FileName parser, seq 0xFF |
| 108, 109 | 216, 218 | DirtyObjects [37], seq wrap to 0x00 | sequence wrap |
| 112, 113 | 224, 226 | ConsoleVersion "3.0.2.9.0.43" | version parser |
| 105, 106 and 188, 189 | 209, 211 and 375, 377 | System `09 00 00` and `07 00 00` as written by SmartSoft | decoder of outbound data; reference only, an encoder needs the message understood and harmless first (safety rule 2) |
| 191, 192 | 382, 384 | 792-byte DirtyObjects, 157 records | large payload, BE count |
| 195, 196 | 390, 392 | SystemSizes | sizes parser |
| 27, 28 and 1364, 1365 | 54, 56 and 2728, 2730 | ChannelLevels with 0xAB and later 0x81 at body[5] | level decode |
| 46 | 92 | Idle reply `04 00 00 …` | "nothing pending" |

## Conclusions and status proposals

The S11 static reading of the HSP framing is right in almost every detail: poll-driven,
12-byte LE headers, stale words, fetch by (id, a, b, c), type-3 echo, DirtyObjects with BE
records, per-direction sequence bytes, the System 9 → ConsoleVersion → System 7 handshake.
Corrections from the wire: inbound payload bytes 2–3 are a LE length and byte 1 is 0;
FileName text is UTF-16LE; the idle cadence is ≈ 31 ms; the first fetches are whatever the
console lists, not 37/38/41; SmartSoft never used the HID interface. Everything here is
**OBSERVED** at most (one session, one PC). A second capture of the same session type
(and E3) is what can promote it to VERIFIED.

### For the curator (proposed; not applied here)

Hypotheses (`hypotheses.md`):

| H | Proposed status |
|---|---|
| H3 | CONFIRMED 2026-10-28 (E2; OBSERVED) |
| H4 | CONFIRMED for the exchange (E2; OBSERVED); mismatch behaviour still OPEN, maybe split into a new hypothesis |
| H5 | Mirroring part CONFIRMED (E2; OBSERVED); ConsoleEvent part stays OPEN (or split off) |
| H15, H16, H17, H18, H20, H26 (length part), H27 | CONFIRMED 2026-10-28 (E2; OBSERVED). H18: record the LE inbound length and byte1 = 0. H26: Independents part stays OPEN |
| H19 | CONFIRMED for IDs 0, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 17, 21, 22, 23, 36, 37, 38, 39, 40, 41 (E2; OBSERVED); its test's "first fetches 37/38/41" REJECTED (text kept) |
| H22 | REJECTED as worded; successor: "numeric u16 body fields are BE; payload length words are LE in both directions; FileName (36) text is UTF-16LE with a code-unit count in body[0]; label text after `03 06` is UTF-16 of undetermined byte order" |
| H23 | REJECTED as worded (20–25 ms); successor: "SmartSoft polls every ≈ 31 ms idle (≈ 46 ms with a fetch), ≥ 10 ms after a fetch; re-fetch untested" |
| H28 | REJECTED (final), with E2 evidence (fader movement in FaderInfo, no HID frames) |
| H29 | REJECTED for SmartSoft + 1248 (E2; OBSERVED); keep the text; new open question: what interface 0 is for |
| H6, H24, H25 | stay OPEN (UNTESTED in E2) |

New hypotheses suggested (IDs to be assigned by the curator):

1. The console marks ConsoleVersion dirty in answer to System (39) `09 00 00` and its whole
   content in answer to `07 00 00`. Test: a second E2-type capture shows the same order.
2. The console's outbound sequence counter is not reset by a new session (first value 0xF6
   here). Test: the next capture, without a console power cycle, starts at 0xEA (0xE9 + 1);
   then once after a power cycle.
3. FaderInfo body = `[48][48 × 4-byte records][2 bytes]`, record k byte 0 = level of fader
   k + 1; ChannelLevels body[c] = channel c; OutputLevels body[1 + d] = DMX address d;
   LedStatus `[75][75 × 6]`; 0xAB is the top of fader 5's travel or a limit. Test: E3(a),
   fader 1 only, 0 → full → 0 with typed timestamps. **Update 2026-10-29 (E3):** the layout
   is confirmed for faders 1 and 5. "0xAB is the top" is rejected: full = 0xFF with the Grand
   Master at full, and E2's Grand Master was below full (FaderInfo body[194] = 0xAA).
4. SystemSizes body[4..7] `00 0C 04 01`: 12 pages, 4 sequences per page, rest UNKNOWN.
5. Patch body = `[u16 BE 512][512 × 1-based channel]`. Test: compare f424 with the
   `SHOW04.ASC` patch.
6. SequenceMain body[0] = number of StepContent steps (0x13 = 19, f664).

Known facts (`known-facts.md`), new OBSERVED rows proposed (source: this record):

- HSP framing as in H15/H16/H18 (with the LE inbound length), poll-only, one URB per
  `ReadFile` / `WriteFile`, every console transfer solicited.
- ConsoleVersion string over USB "3.0.2.9.0.43" (f226).
- FileName is UTF-16LE with a code-unit count (f88); current show "SHOW04.ASC".
- SystemSizes `30 02 00 18 00 0C 04 01` (f392).
- Session start sequence (string descriptor read, 9-object list, System 9, ConsoleVersion,
  System 7, 157-object list) and silent session end.
- SmartSoft wrote only System `09 00 00` and `07 00 00` in an edit-free session (no upload).
- SmartSoft did not touch the HID interface (no URB on 0x81 / 0x02 in 0–280.54 s).
- Idle cadence ≈ 31 ms; full download of 157 objects ≈ 7.3 s.

Promotion candidates (curator to judge whether a second session with a different tool
counts as the independent reproduction the README requires):

- **F48 → VERIFIED**: firmware 3.0.2.9.0.43 read by the owner on the display and reported by
  the console over USB (f226).
- **F41 → VERIFIED**: product string "ETC SmartFade" from the HID class driver in E1 and as
  the raw string descriptor on the bus in E2 (f10).
- **F40, F42 → VERIFIED**: the same descriptor bytes in a second session after a reboot, read
  by USBPcapCMD (f2, f4).
- **F49**: the HSP traffic in E2 runs through the loaded `etchsp.sys`; no change needed.

Docs the curator may want to touch: `reverse-engineering.md` (E2 row done; capture-format
keys added by the normalizer, see `tools/protocol-analyzer/README.md`; tooling row "Capture
importer" done), `smartsoft-analysis.md` S11.5 / S11.9 / S11.13 (inbound length LE, cadence,
driver URB behaviour), the evidence and capability matrices, `docs/protocol/README.md`
(time to create `packet-format.md`).

#### Addendum after the SHOW04.ASC cross-check (same evening)

Evidence: section "Cross-check against SHOW04.ASC" below (E2 capture + the console's own
show file saved just before the session; one show, one session, so OBSERVED at most).

| H | Proposed status | Why |
|---|---|---|
| H10 `MIDIChannel` 0-based | stays OPEN (undecidable) | `SET MIDIChannel 0` and the matching SystemSettings byte are both 0; that says nothing about 0- vs 1-based |
| H11 channels 49/50 = Independents | stays OPEN | No independent level was non-zero; this show patches channel 50 to DMX 47 and channel 49 nowhere. ChannelLevels n = 50 is consistent, not proof |
| H12 `$$CUEPARAMS` = step type | stays OPEN | All 26 steps have `$$CUEPARAMS 1` and record byte 2 = 0x01 on the wire; consistent with "1 = memory step", nothing about 0 and 2 |
| H13 `GROUP n` = p × 24 + f + 1 | **CONFIRMED (OBSERVED)** for the numbering relation | The 16 Memory objects fetched with (a, b) carry exactly the levels and labels of `GROUP 24a + b + 1` (16/16 memories, 768/768 level bytes, 16/16 label sets), and the 26 step records carry memory index 24p + f for `$$MEMORY p f` (26/26). That p / f are the console's page / fader is still to be seen on the console (E6) |
| H14 `FOLLOWON` = step Wait | **Supported**, stays INFERRED for the name | FOLLOWON is the third u16 BE time (tenths of a second) of every step record, after UP and DOWN: 26/26, including `1:30` → `03 84` (f728) and `58` → `02 44` (f736). That the console calls it "Wait" needs one look at a step on the console |
| H25 Memory / Sequence qualifiers | **CONFIRMED (OBSERVED)** for the qualifier mapping; the physical meaning (page / fader on the console) stays OPEN for the E6 test | Fetch (a, b) = the file's `$$MEMORY p f` = memory body[0..1]; memory number = 24a + b + 1 (16/16). Sequence (0, 0) = the file's only `$SEQUENCE 0` |
| H26 Independents part | stays OPEN | ChannelLevels body[49], body[50] are 0 throughout; the file stores no live levels |
| E2 suggestion 3 (0xAB = top of the fader) | **not supported** by the level scale; left OPEN for E3 | In memory data 0xFF = 100 % and 0xAB = 67 % (`GROUP 3` `9/67` → f456 body[12] = 0xAB). If FaderInfo uses the same scale, the fader stopped at 67 % three times; E3 decides |
| E2 suggestion 4 (SystemSizes body[4..7]) | partly consistent, stays OPEN | 12 `$PAGE` blocks fit body[5] = 0x0C; "4 sequences per page" untested |
| E2 suggestion 5 (Patch layout) | **CONFIRMED (OBSERVED)** | 512/512 entries = the file's channel per DMX address, 0 = unpatched. The proportional level (100 % everywhere here) is not in this object |
| E2 suggestion 6 (SequenceMain body[0] = step count) | **CONFIRMED (OBSERVED)** | 0x13 = 19 = the 19 `CUE` steps; Sequence body[2] = 7 = its 7 `$CUE` steps |

New hypotheses (IDs to be assigned by the curator), each OBSERVED once with SHOW04.ASC:

1. Memory (0) body = `[a][b][0x01][0x60 = 96][96 level bytes][text block]`; levels are
   0–255 with 0xFF = 100 %; the console stores ⌈pct × 255 / 100⌉ (53/53 non-zero levels) and
   the ASC writes pct = raw × 100 / 255 rounded (round and floor agree on all 53).
2. Text block = `[03][06][18 × u16 BE]` = 3 label lines × 6 characters (`$$Text 1..3`); used
   by Memory, Sequence, SequenceMain, Page, ChannelText, Independents. Big-endian follows from
   the block boundaries; a label with a character ≥ U+0100 would confirm it.
3. Step record (12 bytes, StepContent body[1..12] and the tail of Sequence) =
   `[u16 BE cue × 10][CUEPARAMS][u16 BE memory index 24p + f][x = 0][u16 BE UP × 10]
   [u16 BE DOWN × 10][u16 BE FOLLOWON × 10]`; all times in tenths of a second.
4. SequenceMain body = `[steps][b][u16 c][u16 d][u16 e][text]`, Sequence body =
   `[page][seq][steps][b][u16 c][u16 d][u16 e][text][steps × step record]`, with b..e = the
   `$SEQUENCE a b c d e` fields (2 0 256 256 and 0 0 256 256 here).
5. Sequence step numbers: the wire says 1.0 … 7.0, the file writes `$CUE 265.0 … 271.0` (the
   memory number). Which of the two the console shows is UNKNOWN.
6. Independents (12) body = `[2][2 × ([a][b][c][d][text block])]` = `$INDEPENDENT n a b c d`.
7. SystemSettings body[6..11] = the six `SET` values in file order (MIDIActive at body[9],
   MSCDeviceId at body[10]); body[12..17] = three u16 BE times, 5.0 s, 5.0 s, 0 (HYPOTHESIS:
   default Up / Down / Wait; LCD f80 shows "U 5.0" and "D 5.0").

For `asc-format.md` (asc-format-analyst / curator): SHOW04.ASC as copied into the
repository has **LF-only** line endings (0 CR, 1 153 LF), unlike the CRLF recorded for the
Ostrov and demo files (cause UNKNOWN: console writer or copy path). `$CUE` numbers in
sequences differ from the console's own step numbers (item 5). Labels are limited to
3 × 6 characters by the object layout (item 2). CHAN percentages come from 0–255 values
(item 1).

## Open follow-ups

1. ~~Owner: copy `SHOW04.ASC` from the SD card into `smartfade-sd-card-backup/` (zero risk) so
   Patch (f424), memory slots (H25) and labels can be compared with this capture.~~ **Done the
   same evening**; results in "Cross-check against SHOW04.ASC" below.
2. Next capture: **E3(a)**, fader 1 only, slowly 0 → full → 0, with the owner typing the
   time of each step; SmartSoft Online, no other action. Isolates fader numbering, the level
   scale (is 0xAB the top?), the ChannelLevels / OutputLevels index base, the LedStatus
   record and the DisplayContent glyph. The same capture repeats E2's start-up and so tests
   reproducibility and the sequence-counter question. **Done 2026-10-29 as E3**, with faders
   1 and 5, a memory bump and the Grand Master, after a console power cycle:
   [2026-10-29-e3-console-actions.md](2026-10-29-e3-console-actions.md).
   - The start-up reproduces, including the System writes after reply headers #40 and #79.
   - The sequence counter started at 0x00 after the power cycle, so the "no power cycle" test
     of H31 is still to do.
3. Owner: switch Memory Integrity back on when the capture sessions are over (E1 / F49
   follow-up). Our backend must not depend on `etchsp.sys` (H7).

## Cross-check against SHOW04.ASC

Added the same evening, after the owner copied the show file into the repository.

- **File:** `smartfade-sd-card-backup/SHOW04.ASC`, 15 944 bytes, SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` (recomputed, matches the
  owner's value). It starts `MANUFACTURER AVAB` / `CONSOLE SMARTFADE` /
  `$SOFTWAREVERSION 3.0.2 43`. Written by the console when the owner backed up the show to SD
  just before E2 (owner-reported). The file is read as data only. Line endings: **LF only**
  (0 CR, 1 153 LF).
- **Why the two should agree:** the file is the console's show as saved a few minutes before
  the capture, and the owner changed no memory or address (owner-reported). Live state
  (fader positions, current levels) is not in the file.
- **Tool:** `tools/protocol-analyzer/e2_asc_crosscheck.py` (SHA-256
  `8C49AEDB18FF7EE4179AD9BAAA2BEE3A0C286788734511649A1BB5B28C4BB314`). Every number below
  comes from:
  `py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl smartfade-sd-card-backup/SHOW04.ASC`.
- **Reproducibility caveat:** `smartfade-sd-card-backup/` is not tracked by git at the time of
  writing. Whether to commit `SHOW04.ASC` (the owner's show data) is the owner's call. Until
  then, this section can only be re-run on the owner's PC.
- Field layouts named below are the hypotheses this comparison tests. Every quoted byte comes
  from the cited pcap frame of the committed capture.

### Verdicts

| # | Comparison | Verdict |
|---|---|---|
| 1 | Patch object vs `PATCH` lines | **matches** (512/512 addresses). The proportional level is not in this object |
| 2 | Memory objects vs `GROUP` / `CHAN` / `$$MEMORY` | **matches**: same 16 memories, 768/768 channel levels, scale 0xFF = 100 %, slot (a, b) = `$$MEMORY p f` |
| 3 | Labels vs `Text` / `$$Text` | **matches** (16 memories, 1 sequence, 12 pages, 96 channels, 2 independents); big-endian text alignment settled structurally |
| 4 | SequenceMain / Sequence / StepContent vs `$SEQUENCE` / `CUE` / `$CUE` | **matches** in step counts, header fields, memory references, UP / DOWN / FOLLOWON (26/26 steps). **Contradicts** in one field: sequence step numbers 1.0–7.0 on the wire vs `$CUE 265.0–271.0` in the file |
| 5 | SystemSettings vs `SET` lines | **consistent, partly undecidable**: the six SET values occur in file order at body[6..11] (only position); the zero-valued keys cannot be placed one by one |
| 6 | Independents object vs `$INDEPENDENT`; ChannelLevels last two bytes | **matches** for the object; **undecidable** for the ChannelLevels bytes (0 on the wire, no live state in the file) |
| 7 | ChannelLevels / OutputLevels at session start vs the show | **undecidable** against the file (it stores no live levels); consistent with one channel fader raised |

### (1) Patch

f424 (seq212), body `02 00 01 02 03 … 2E 32 30 00 …`. The u16 BE count is 512, then 512
entries. For every DMX address d, entry d − 1 equals the channel in the file's `PATCH 1 d/c/l`:

- 49 patched addresses match: DMX 1–46 → 1–46, `47/50` → 0x32, `48/48` → 0x30,
  `97/24` → 0x18.
- 463 unpatched addresses match (`d/0/100` → 0x00).
- 0 mismatches.

Channels 47 and 49 have no address, and channel 24 has two (24 and 97). All 512 lines carry
level 100. The Patch body has exactly 2 + 512 bytes, all used by channel numbers, so the
proportional level is not in this object. Where it lives is UNKNOWN; finding out needs a show
with a level other than 100 %.

Side check: patch DMX 5 → channel 5 at 100 % is consistent with OutputLevels body[6] = 0xAB
(f48) equalling ChannelLevels body[5] = 0xAB (f56).

### (2) Memories

The 16 Memory fetches map one to one onto the 16 `GROUP` records (1–8, 13, 265–271) via
`GROUP n` with n = 24a + b + 1. None is missing and none is extra:

- f440, f448, f456, f464, f472, f480, f488, f496, f504: a = 0, b = 0–7 and 12.
- f600, f608, f616, f624, f632, f640, f648: a = 11, b = 0–6.

The file uses the same pairs as `$$MEMORY p f` (e.g. `CUE 1.0` `$$MEMORY 0 0`,
`$CUE 265.0` `$$MEMORY 11 0`).

Body layout:

- body[0..1] = (a, b), e.g. f600 body `0B 00 01 60`.
- body[2] = 0x01 in all 16 (meaning UNKNOWN).
- body[3] = 0x60 = 96.
- 96 level bytes follow (channels 1–96; 49–96 are 0 in all 16), then the text block (3).

For channels 1–48, **768/768** level bytes agree with the `CHAN` lists (0 ↔ channel not
listed). Example f448 (seq224), `GROUP 2` with `CHAN 1/17 2/25 3/33 4/39 5/38 6/35 9/38 10/43`
and `CHAN 13/7 14/32`: body[0..17] = `00 01 01 60 2C 40 55 64 61 5A 00 00 61 6E 00 00 12 52`.

**Scale:**

- 0xFF = 100 %: f440 body[10..11] `FF FF` = `GROUP 1` `7/100 8/100`.
- Across all 53 non-zero levels, raw = ⌈pct × 255 / 100⌉ holds 53/53. pct = raw × 100 / 255
  rounded and the same value truncated both hold 53/53 and cannot be told apart. round(pct ×
  2.55) fails 7/53 (e.g. 17 % is 0x2C = 44, not 43).
- **0xAB = 171 = 67 %**, not 100 %: f456 body[12] = 0xAB is `GROUP 3` `9/67`.
- Distinct pairs seen: 12=7 %, 15=8 %, 29=16 %, 2C=17 %, 31=19 %, 33=20 %, 40=25 %, 4A=29 %,
  4D=30 %, 52=32 %, 55=33 %, 5A=35 %, 61=38 %, 64=39 %, 6E=43 %, 94=58 %, 9C=61 %, AB=67 %,
  B0=69 %, CC=80 %, D4=83 %, F5=96 %, FF=100 %.

### (3) Labels

Every label-carrying body ends in a 38-byte block `03 06` + 36 bytes. Read as 3 lines × 6
code units of UTF-16BE, starting right after `03 06` and cut at the first 0, the labels equal
the file's `$$Text 1`, `$$Text 2` and `$$Text 3`:

- 16/16 memories (10 non-empty): OPONA, LES, RYBAR / DOM, DUCH, NOC 1 / MORE, NOC 2 / MORE,
  NOC 3 / MORE, NOC 1 / OPONA, NOC / BLESKY.
- The sequence: KOTLIK (f656).
- 12/12 pages and 96/96 channels: empty on both sides.
- 2/2 independents: empty on both sides.

`Text` equals `$$Text 1` in this file.

Example f504 (seq252, `GROUP 13`, `$$Text 1 NOC`, `$$Text 2 BLESKY`): body[100..137] =
`03 06 00 4E 00 4F 00 43 00 00 00 00 00 00 00 42 00 4C 00 45 00 53 00 4B 00 59 00 00 …`.
Line 2 starts at code unit 6, and "BLESKY" fills it exactly.

**Byte order, settled structurally.** With big-endian code units starting right after
`03 06`, the 36-byte area ends exactly where each body ends:

- Memory: 138 = 4 + 96 + 38.
- Page and ChannelText: 39 = 1 + 38.
- SequenceMain: 46 = 8 + 38.
- Sequence: the area ends exactly where the seven 12-byte step records begin, at body[48].

The alternative reading (little-endian, starting one byte later) would need one byte beyond
the body, or would eat the first byte of the first step record. So labels are UTF-16BE (the
match with the file is OBSERVED; the byte order is INFERRED from structure), unlike FileName,
which is UTF-16LE (f88). All characters here are below U+0100, so a label with, for example,
"č" would be the direct test.

### (4) Sequences and steps

- **SequenceMain** f664 (seq332), body `13 02 00 00 01 00 01 00 03 06 …`:
  - body[0] = 0x13 = 19 = the 19 `CUE` steps of `$SEQUENCE 48`.
  - body[1] = 2, u16 BE body[2..3] = 0, body[4..5] = 256, body[6..7] = 256: these equal the
    fields b, c, d, e of `$SEQUENCE 48 2 0 256 256`.
  - The label is empty.
- **Sequence** f656 (seq328), fetched with (0, 0):
  - body[0..1] = (0, 0).
  - body[2] = 7 = the 7 `$CUE` steps.
  - body[3] = 0, body[4..5] = 0, body[6..7] = 256, body[8..9] = 256: these equal
    `$SEQUENCE 0 0 0 256 256` b..e.
  - Label KOTLIK, then 84 bytes = 7 × 12-byte step records.
- **Step record** (hypothesis): `[u16 BE cue × 10][CUEPARAMS][u16 BE memory index]
  [x][u16 BE UP × 10][u16 BE DOWN × 10][u16 BE FOLLOWON × 10]`.
  - StepContent body[1..12] (19 objects, f672–f816) against the 19 `CUE` steps in order:
    cue 19/19, CUEPARAMS 19/19, memory 19/19, UP 19/19, DOWN 19/19, FOLLOWON 19/19.
    Examples:
    - f728 (seq364) `07 00 50 01 00 04 00 00 32 00 05 03 84` = `CUE 8.0`, `$$MEMORY 0 4`,
      `UP 5.0`, `DOWN 0.5`, `FOLLOWON 1:30` (0x0384 = 900 tenths).
    - f744 (seq372) `09 00 65 01 00 0C 00 00 00 00 00 00 0A` = `CUE 10.1`, `$$MEMORY 0 12`,
      `UP 0`, `DOWN 0`, `FOLLOWON 1.0`.
    - f720 `… 01 F4` = `FOLLOWON 50`.
    - f736 `… 02 44` = `FOLLOWON 58`.
  - Sequence records: memory index, CUEPARAMS, UP, DOWN and FOLLOWON agree 7/7. The first one
    is f656 body[48..59] `00 0A 01 01 08 00 00 0A 00 0A 00 00`: memory index 0x0108 = 264 =
    24 × 11 + 0 for `$$MEMORY 11 0`, `UP 1.0`, `DOWN 1.0`, `FOLLOWON 0`.
  - **Contradiction:** the sequence records' cue field is 1.0, 2.0 … 7.0 (0x000A … 0x0046),
    while the file writes `$CUE 265.0 … 271.0` (the memory number, as `asc-format.md` noted
    for the Ostrov file). Which number the console shows is UNKNOWN.
  - Record byte x (StepContent body[6]) is 0 in all 26 records, and every `UP` / `DOWN` delay
    in the file is 0, so "x is a delay" is undecidable. `$$CUEPARAMS` is 1 in all 26 and the
    byte is 0x01 in all 26, so the position fits but H12's values 0 and 2 are untested.

### (5) SystemSettings

f416 (seq208) body `00 64 5A 3C 00 00 00 00 00 01 7F 00 00 32 00 32 00 00`. The file's six
`SET` values in file order (Language 0, CrossfadeFadeMode 0, MIDIChannel 0, MIDIActive 1,
MSCDeviceId 127, MSCActive 0) occur as a contiguous run only once, at body[6..11].

Only MIDIActive (body[9] = 0x01) and MSCDeviceId (body[10] = 0x7F) are distinctive. The
three zero-valued keys before them cannot be placed individually, so H10 (MIDIChannel
0-based) is undecidable.

body[0..5] `00 64 5A 3C 00 00` and body[12..17] `00 32 00 32 00 00` have no `SET` line.
HYPOTHESIS: body[12..17] are three u16 BE times of 5.0 s, 5.0 s and 0 (default Up / Down /
Wait). The LCD text f80 shows "U 5.0" and "D 5.0".

### (6) Independents

- **Object:** f1592 (seq796) body `02 00 FF 00 01 03 06 …`: count 2, then two 42-byte
  records `00 FF 00 01` + an empty label block. This equals `$INDEPENDENT 1 0 255 0 1` and
  `$INDEPENDENT 2 0 255 0 1`, both with empty Text. The meaning of fields a–d stays UNKNOWN.
- **ChannelLevels:** body[49] and body[50] are `00 00` in all 15 payloads (first f56). The
  file stores no live independent level, so H26's Independents part and H11 stay untested.

### (7) Live levels at session start

The file holds no fader positions or live output. At session start:

- ChannelLevels f56: the only non-zero byte is body[5] = 0xAB (channel 5 per H32, INFERRED; 67 % only if the memory scale of F72 also applies to live levels, HYPOTHESIS).
- OutputLevels f48: the only non-zero byte is body[6] = 0xAB (DMX 5).
- FaderInfo f32: body[17] = 0xAB, besides the constant body[83] = 0x01, body[99] = 0x01 and
  body[194] = 0xAA.

The only memories containing channel 5 are `GROUP 2` (38 %) and `GROUP 3` (83 %), and both
also contain other channels, which are all 0 on the wire. INFERRED: the 67 % does not come
from a memory being played back (assuming a memory fader scales all of its channels). It is
consistent with channel fader 5 alone being up. The owner's "faders were up" therefore
amounts to one channel fader at 0xAB (67 % if the memory scale applies, HYPOTHESIS) in this capture.

### What the cross-check does not settle

- Whether (a, b) is the console's page and fader (E6 / H25 original test).
- Which step numbers the console displays for sequences.
- Where the patch proportional level is carried.
- The meaning of `$INDEPENDENT` a–d.
- The non-SET bytes of SystemSettings.
- The fader scale of FaderInfo (E3).

Status proposals are in "For the curator", addendum.
