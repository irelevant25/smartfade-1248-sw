# 2026-10-30 E3b: live objects, three SmartSoft sessions (USBPcap, passive)

- **Date:** 2026-10-30. The capture ran 08:54:52–09:13:35 local time (CET, UTC+1). The pcap's
  first timestamp is 2026-10-30T07:54:52.222585Z = 08:54:52.222585 on the PC clock that wrote
  the action log.
- **Experiment:** **E3b**, the passive session proposed by the E3 analysis
  ([../reverse-engineering.md](../reverse-engineering.md), "Next capture (proposed by the E3
  analysis)", items B1–B4). Nothing was sent by us.
  - B1: three SmartSoft sessions without a power cycle (H31, H58).
  - B2: a channel at full while the MASTER moves (H47, H57).
  - B3: the BUMPS fader, then a channel bump and memory bumps with SOLO off and on (H46, H54).
  - B4: the 25-48 fader bank (H59, H53, H56).
  - The optional GO (B5) was declined by the owner (log `owner decision: GO step = n`).
    Coordinator: the owner said the STACK button was not lit, so GO would not have run the
    stack in this state.
- **Console:** ETC SmartFade 1248, firmware 3.0.2.9.0.43; the console sent "3.0.2.9.0.43" again
  in every session (f198, f5564, f10262).
  - It was switched off since E3 (owner, log 08:56:03).
  - USB device address 7 on bus 1, the same root hub as E3.
- **Host:** the owner's main PC, as in E2 and E3. Windows 11 Pro build 26300, Memory Integrity
  off, `etchsp.sys` 2.00.000 loaded, SmartSoft 3.0.43 (package 3.0.2.9.0.43b), USBPcap 1.5.4.0,
  tshark 4.0.3.
- **Connection:** USB A-B cable straight into the PC.
- **DMX IN:** connected (owner answer, log 08:56:13). What was connected and whether it was
  transmitting is UNKNOWN.
- **Show backup (safety rule 3, enforced by the script):** `backup taken: SHOW05.ASC` at
  08:55:48.809, before the first SmartSoft start (08:57:42.237).
  - The owner copied the file into `smartfade-sd-card-backup/SHOW05.ASC` (15 944 bytes,
    SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`). The folder is
    git-ignored; cite the file by name and hash.
  - It is **byte-identical to `SHOW04.ASC`** (same SHA-256, `cmp` finds no difference).
- **Starting position** (owner, log 08:57:40): channel faders at zero, MASTER full, BUMPS zero,
  channel mode 1-24, BLACKOUT off; SOLO not lit (08:56:24). The fader left of MASTER is BUMPS
  (08:56:46).
- **Capture command (coordinator):** `USBPcapCMD.exe -d \\.\USBPcap1 -o <raw> -A --inject-descriptors`.
  - Started elevated by Claude at 08:54:52.
  - Stopped with `Stop-Process` at about 09:13:20; the raw file's last frame is at 1122.644005 s
    = 09:13:34.87. Either way this is long after the last console frame (09:11:41.20).
  - The console-only split was made by Claude with
    `tshark -r <raw> -Y "usb.device_address == 7" -F pcap -w <split>`.
- **SmartSoft was started three times by the script itself** via `Start-Process` (log lines
  `smartsoft start #n (Start-Process by the script)`). The script is
  `tools/usb-probe/e3b-guided-session.ps1`, SHA-256
  `EF95D9C1D5EA4D86F6953042E6B4FC1F978564441F86F0265F9838543F3F7855`.

**Conventions** (as in the E2 and E3 records):
- `fN` is the pcap frame number in the committed split: the submission frame for OUT, the
  completion frame for IN.
- `seqN` is the JSONL record number; `pseq` is a payload's own sequence byte.
- `body[n]` is byte n after the 5-byte inbound payload header.
- LED block k = LedStatus body[1 + 6(k − 1) .. 6k], numbered from 1.
- FaderInfo record N = body[1 + 4(N − 1) .. 4N], numbered from 1.
- Times are capture-relative seconds; `t` in the JSONL is the URB completion time.
- Every quoted byte comes from the cited frame. "CL-ML" marks a ConsoleLink claim about the
  SmartFade ML (evidence class 5, [../consolelink-analysis.md](../consolelink-analysis.md)).

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-30-e3b-live-objects.pcap` | `E470719DBCF2B723EEB1A8E5CE4177B8A6BB0689D62A56C5652ACCDBD4E1B6A2` | 5 426 940 B, 107 870 frames, 0–1008.974036 s | yes (**the evidence**) | Console-only split (address 7), classic pcap, LE, µs, link type 249 |
| `2026-10-30-e3b-live-objects.raw-all-devices.pcap` | `3F3CA4E83C57699F441074D1E494160A90EA0DE95F109CADD0FA3868C394A6D7` | 705 089 085 B, 503 370 frames, 0–1122.644005 s | **no** (`.gitignore`: `*.raw-all-devices.pcap`) | Raw USBPcapCMD output for root hub `USBPcap1` (deleted on 2026-10-30 after the completeness checks below; the hash stays recorded): every device on the hub, including keyboard and mouse (keystrokes) and a heavily streaming address 4. Used only for the completeness check below, other devices discarded unread; deleted on 2026-10-30 after the completeness check, like the E2 and E3 raw files |
| `2026-10-30-e3b-live-objects.log` | `E69ECA64C6FF32633754E1147E49FA4054E4EF4D63E15BAE80483A8A223097A1` | 4 967 B, 86 lines, CRLF | yes | The owner's action log from `tools/usb-probe/e3b-guided-session.ps1`. Same stamping rules as E3: `Step` = start of an action, `Confirm-Done` = after it, `wait … done` = end of a wait. New: `owner answer:` lines and the script's own `smartsoft start #n` lines |
| `2026-10-30-e3b-live-objects.jsonl` | `0EA97056712F1656026AE42831E766B0B1F53662F5C2A07879C5CF0D5D944594` | 12 973 616 B, 54 024 lines | yes (derived) | 1 header, 53 935 `frame` records, 88 `note` records: the 86 log lines plus capture start and stop (coordinator). Normalizer version 2, `--notes-utc-offset +01:00`. Command in `tools/protocol-analyzer/README.md`; two runs and the README command gave identical bytes |
| `tools/usb-probe/e3b-guided-session.ps1` | `EF95D9C1D5EA4D86F6953042E6B4FC1F978564441F86F0265F9838543F3F7855` | 9 263 B | yes | The guided session. It refuses an existing log file, asks for the backup before starting SmartSoft, starts SmartSoft three times and asks the zero-risk questions. It talks only to the operator and starts SmartSoft; it sends nothing to the console itself |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `EA7627D7CDD0FF214F003F290AE7CB6B75B73149B4E753011C76B129EFD20FE8` | | yes | Unchanged since E3 (version 2) |
| `tools/protocol-analyzer/hsp_summary.py` | `DFD289DE3470232974797F2C8A4AA7B4EA9345E3F62B505581B80D64D699794B` | | yes | New mode `sessions` (per SmartSoft session); the other modes are unchanged |
| `tools/protocol-analyzer/e2_asc_crosscheck.py` | `8C49AEDB18FF7EE4179AD9BAAA2BEE3A0C286788734511649A1BB5B28C4BB314` | | yes | Unchanged; run on the E3b JSONL against `SHOW05.ASC` |
| `smartfade-sd-card-backup/SHOW05.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` | 15 944 B | no (folder git-ignored) | The backup made at 08:55, byte-identical to `SHOW04.ASC` |

**Completeness of the split.** The normalizer run on the raw file with `--device-address 7`
found **107 870** frames for address 7 and skipped 395 500 frames of other devices unread. It
produced 53 935 records, identical to the split's in every field except the pcap frame numbers
(0 differing records).
- The raw file has no address-7 frames after 1008.974036 s and none on EP 0x81 or 0x02.
- **Independent cross-check:** tshark 4.0.3's `usb.capdata` equals the JSONL `data` for all
  53 928 data-carrying bulk frames (0 mismatches).

## Action log (capture-relative)

Sources:
- **log**: the owner's action log, as a `note`.
- **capture**: the console's data.
- **coord.**: the coordinator.

The 86 log lines are all in the JSONL; the table keeps the ones that matter for the evaluation.

| t (s) | Source | Event |
|---|---|---|
| 0.000000 | coord. + capture | Capture starts. f1–f6 are **synthetic** `--inject-descriptors` frames, byte-identical to E2's and E3's (device descriptor f2, configuration descriptor f4, SET_CONFIGURATION f5 / f6) |
| 4.86–168.21 | log | `START`; `backup taken: SHOW05.ASC` (56.59); owner answers: powered off since E3 = y, DMX IN connected = y, SOLO lit at start = n, fader left of MASTER = BUMPS; `starting position set (owner)` (168.21) |
| 170.014415 | log | `smartsoft start #1 (Start-Process by the script)` (08:57:42.237) |
| 170.735–170.738 | capture | GET_DESCRIPTOR(STRING 0) → f8 `04 03 09 04`; GET_DESCRIPTOR(STRING 2, 0x0409) → f10 "ETC SmartFade" |
| 170.738514 | capture | **Session 1** first poll f11 (12 zero bytes). The first reply f14 `04 00 00 00 …` is idle. The second reply f18 `04 00 11 00 …` brings f20 `00 00 0C 00 28 00 02 16 00 00 00 00 15 00 00 00 00`: pseq **0x00**, a DirtyObjects list of **2 records: LedStatus (22), DisplayContent (21)**. They are fetched (f28, f36) |
| 172.005314 / 173.254598 | capture | System `09 00 00` (f183) after reply header #40 → list [37] (f190) → ConsoleVersion (f198); System `07 00 00` (f349) after reply header #79 → the 157-record list (f356) → download; the last payload of the download is ChannelLevels f1612 (pseq 0xA2 = 162) |
| 179.091 | log | `smartsoft online #1 (owner)` |
| 208.065 / 209.449888 / 212.700 | log / capture / log | `smartsoft close #1 start`; the last transfer of session 1 is an idle poll / reply (f5381 / f5384); `smartsoft closed #1` |
| 224.815 | log | `smartsoft start #2` (08:58:37.038) |
| 224.888 | capture | **Session 2**: GET_DESCRIPTOR(STRING 2) only (f5386; no STRING 0); first poll f5387 (12 zero bytes); replies idle (first f5390); **nothing pending at connect** |
| 226.141853 / 227.396078 | capture | System `09 00 00` (f5549) after reply header #40 → [37] (f5556, pseq **0xA3 = 163**) → ConsoleVersion f5564; System `07 00 00` (f5715) after #79 → the 157-record list f5722 → download (last payload f6978, pseq 0x42) |
| 258.487374 | capture | Last transfer of session 2: idle poll / reply (f10079 / f10082) |
| 271.433 | log | `smartsoft start #3` (08:59:23.656) |
| 271.507 | capture | **Session 3**: STRING 2 only (f10084), first poll f10085 (12 zero bytes), replies idle, **nothing pending at connect**. System `09 00 00` (f10247) after #40 → [37] (f10254, pseq **0x43 = 67**) → ConsoleVersion f10262; System `07 00 00` (f10413) after #79 → the 157-record list f10420 → download |
| 312.405 / 319.238 | log | `fader5 up start` / `fader5 at full` |
| 313.57–316.38 | capture | Fader 5 rises to 0xFF in ChannelLevels[5], OutputLevels[6], FaderInfo[17], LED block 5 G, LCD cell body[7] (FaderInfo f16384 `[17] = FF`, MASTER `[194] = FF`) |
| 344.773 / 354.490 | log | `master to half start` / `master at half` |
| 346.32–350.31 | capture | MASTER down: FaderInfo body[194] 0xFF → **0x7F** (f20302 … f20944). Fader 5's output follows it in all level objects (see "MASTER with a channel at full"). LED block 53 blinks from f20310 |
| 363.640 / 369.046 | log | `master to full start` / `master at full` |
| 365.24–366.94 | capture | MASTER up: body[194] → 0xFF (f22914 … f23188); fader 5 back to 0xFF; block 53 stops blinking at f23174 |
| 390.551 | log | owner answer: `fixture 1 during MASTER move = dimmed and came back` |
| 397.271 / 402.763 | log | `fader5 down start` / `fader5 at zero` (capture 398.87–401.41) |
| 416.736–461.396 | log | BUMPS up (416.74), at full (422.72), down (432.95), at zero (438.61), up again (456.30), at full again (461.40) |
| 418.07–420.37, 434.55–436.55, 457.52–… | capture | FaderInfo body[193] 0x00 → 0xFF (f30010 … f30430), → 0x00 (f32290 … f32654), → 0xFF (from f35394); nothing else changes |
| 493.717 / 504.068 | log | `channel bump 7 press` / `channel bump 7 release` |
| 494.90–502.95 | capture | Channel 7 = 0xFF while held: OutputLevels[8] (f40360), FaderInfo record 7 body[25] (f40368), ChannelLevels[7] (f40376), LED block 7 G (f40384), LCD cell body[9] = 0x86 (f40392); back to 0 at 502.77–502.95 (f41394 … f41426) |
| 516.039 | log | owner answer: `lights during channel bump 7 = fixture 1 bluie came on` |
| 542.983 / 547.616 | log | `memory mode start` / `memory mode on` |
| 544.930–545.143 | capture | ConsoleStatus body[0] 0x00 → **0x01** (f46896); LedStatus changes 58 bytes (f46904, same map as E3, F84); the LCD is listed but unchanged |
| 634.387 | log | owner answer: `LED under 21 is yellowish and LED under fader 1 is red (high intesity, because something is saved there, LED under not saved faders are red as well but low intensity)` |
| 647.168 / 653.690 | log | `memory bump 1 press, SOLO off` / `memory bump 1 release` |
| 648.09–652.49 | capture | Channels 7 and 8 = 0xFF while held: FaderInfo record 1 bytes 0 **and 1** = `FF FF` (f60332), OutputLevels[8], [9] (f60346), ChannelLevels[7], [8] (f60354), LED block 1 = `FF 00 00 FF 00 00` (f60362), LCD cells 7 and 8 = 0x86 (f60370); back at 652.28–652.49 (block 1 → `46 00 00 …`, f60900) |
| **664.75–665.94** | capture | **A third memory bump, not in the log**: the same changes (f62528 … f62702), about 1 s long, at 09:05:56.97–09:05:58.16 local |
| 724.050 | log | owner answer: `lights during memory bump 1, SOLO off = First one came BLUE and second one came RED and then, after release, both came down` |
| 734.462 / 739.154 | log | `SOLO press` / `SOLO on (owner)` |
| 735.497 | capture | LED block 52 = `FF FF FF FF FF FF` (f71756); ConsoleStatus not listed |
| 754.294 / 763.552 | log | `memory bump 1 press, SOLO on` / release |
| 754.66–762.50 | capture | The same changes as with SOLO off (f74254 … f75286) |
| 775.762 | log | owner answer: SOLO on = same lights as SOLO off |
| 791.411 / 795.847 | log | `SOLO release` / `SOLO off (owner)`; capture: block 52 back to `0A`×6 (f79228, 792.687) |
| 828.090 / 833.678 | log | `bumps down final start` / `bumps at zero final`; capture: body[193] 0xFF → 0x00 (f83970 … f84292) |
| **842.731 / 846.596** | capture | **Two mode switches, not in the log.** ConsoleStatus body[0] 0x01 → 0x00 (f85826, 09:08:54.95) and back to 0x01 (f86336, 09:08:58.82), each with the matching LED change (blocks 1–24, 55, 58) |
| 850.457 / 853.864 | log | `channel mode 1-24 start` / `on`; capture: body[0] → 0x00 at f86984 (851.488), LED f86992 |
| 861.269 / 864.505 | log | `bank 25-48 press` / `on`; capture: ConsoleStatus body[4] 0x00 → **0x01** (f88374, body `00 00 00 00 01 01 00`); LED blocks 55 / 56 swap (f88382); LCD row labels " 1" / "13" → "25" / "37" (f88390) |
| 889.189 / 898.850 | log | `fader1 in bank 25-48 move start` / `… at zero` |
| 890.30–897.90 | capture | Physical fader 1 in bank 25-48: **FaderInfo record 1 (body[1])** 0x07 … 0xFF … 0x00 (f92020 … f93190), **ChannelLevels[25]** and **OutputLevels[26]** (DMX 25) 0x00 → 0xFF → 0x00, LED block 1 G, LCD cell body[3]; body[97] (record 25), ChannelLevels[1] and OutputLevels[2] stay 0x00 |
| 907.088 / 910.411 | log | `bank 1-24 press` / `on`; capture: body[4] → 0x00 (f94520), LED f94528, LCD back to " 1" / "13" (f94542) |
| **975.699–980.955** | capture | **A playback crossfade, not in the log**, at 09:11:07.92–09:11:13.18 local. List f103304 (SequenceMain, PlaybackInfo, LedStatus); PlaybackInfo Live / Next (body[67] / [68]) cross from `FA 05` (f103320) to `00 FF` (f104224) and reset to `FF 00` (f104238); body[75..78] count down 5.0 s in 1-s steps; LED block 50 blue (f103328 … f104254); SequenceMain listed at start and end, unchanged; **no level object changed** |
| 999.334 | log | `owner decision: GO step = n` |
| 1007.339 | log | `smartsoft close #3 start` (09:11:39.562) |
| 1008.974036 | capture | Last transfer: idle poll / reply (f107867 / f107870), 1.63 s after the close started. No closing message |
| 1010.890 … 1015.903 | log | `smartsoft closed #3`, `END` |
| 1107.8 | coord. | USBPcapCMD stopped (about 09:13:20) |

## Observed result

### Structure

| Endpoint | Transfer | Direction | URBs |
|---|---|---|---|
| 0x04 (interface 1) | bulk | OUT | 26 836 (12 B × 26 830, 7 B × 6) |
| 0x83 (interface 1) | bulk | IN | 27 092 (25 824 reply headers, 1 268 payloads) |
| 0x80 / 0x00 | control | IN / OUT | 6 / 1 (2 + 1 synthetic; STRING 0 once; STRING 2 three times) |
| **0x81, 0x02 (HID)** | interrupt | | **0** (split and raw) |

- All 53 935 URBs completed with status 0, and there were 0 framing errors.
- Counts: 25 824 polls, 1 000 fetches, 6 data headers, each followed by exactly 7 bytes, 25 824
  reply headers, 1 268 payloads.
- Replies: 1 000 type 3, all echoing their fetch; 268 type 4 with payload, all DirtyObjects;
  24 556 idle.
- Stale w2..w5 in 25 829 / 25 829 poll and data headers.
- The 1 000 list records are, in order, the 1 000 fetches. No fetch was re-sent.
- Every IN transfer was solicited.
- **Inbound sequence:** 1 268 payloads, pseq 0x00 (f20) … 0xF3, **0 breaks across the three
  sessions**. Four wraps, each keeping byte 1 at 0x00: f6442 `FF 00 27 00 09` → f6450
  `00 00 27 00 09`; f16108 `FF 00 1B 00 28` → f16116 `00 00 C3 00 0E`; f30304 `FF 00 C3 00 0E`
  → f30310 `00 00 07 00 28`; f92058 `FF 00 02 02 0D` → f92066 `00 00 C3 00 0E`. Byte 1 is 0x00
  in 1 268 / 1 268, and the LE length word is payload − 5 in 1 268 / 1 268.
- **Outbound sequence** (`sessions` mode): 0x00, 0x01 in each session (f183 / f349, f5549 /
  f5715, f10247 / f10413).
- **Timing:** idle poll interval (within sessions, n = 24 547) median **30.79 ms**, 20 348 in
  30–32 ms, min 20.59 ms, max 42.29 ms. Per session the medians are 30.73 / 30.80 / 30.80 ms.
  The next poll comes ≥ 10.33 ms after a fetch (n = 1 000, max 25.32 ms) and 14.58–15.82 ms
  after the 6 data writes. The reply-header IN median is 1.92 ms (max 8.02 ms).
- **Lists per action window** (all 268 lists fall into one; ID: count):

| Window | Lists | Records | IDs listed |
|---|---|---|---|
| S1 / S2 / S3 start-up | 3 / 2 / 2 | 160 / 158 / 158 | S1 adds 21, 22 to the [37] + 157 lists |
| Fader 5 + MASTER, 312–402 s | 46 | 195 | 13: 42, 14: 45, 15: 44, 21: 22, 22: 42 |
| BUMPS moves, 416–462 s / 828–834 s | 83 / 24 | 83 / 24 | 14 only |
| Channel bump 7 | 2 | 10 | 13, 14, 15, 21, 22 (2 each) |
| Memory mode on | 2 | 4 | 21: 1, 22: 2, 23: 1 |
| Memory bump 1, SOLO off / unlogged bump / SOLO on | 4 / 2 / 3 | 10 / 10 / 10 | 13, 14, 15, 21, 22 (2 each) |
| SOLO on / SOLO off | 1 / 1 | 1 / 1 | 22 |
| Unlogged mode switches / channel mode / bank 25-48 / bank 1-24 | 3 / 1 / 1 / 2 | 7 / 2 / 3 / 3 | 21, 22, 23 |
| Fader 1 in bank 25-48 | 20 | 91 | 13: 20, 14: 20, 15: 20, 21: 13, 22: 18 |
| Unlogged crossfade, 975–982 s | 66 | 70 | 17: 66, 3: 2, 22: 2 |

### Three sessions without a power cycle (B1; H31, H58, H30)

`sessions` mode (sessions split at pauses > 2 s):

| Session | Span (first OUT … last IN) | Connect-time pending list | System 9 / 7 | First / last inbound pseq | 157-record list |
|---|---|---|---|---|---|
| 1 (after the power cycle) | f11 170.738 … f5384 209.450 | **LedStatus, DisplayContent** (f20, pseq 0x00) | f183 after reply header **#40** (36 idle replies before), f349 after **#79** (37) | 0 (f20) … 162 (f1612) | f356 |
| 2 | f5387 224.888 … f10082 258.487 | **none** | f5549 after **#40** (40 idle), f5715 after **#79** (37) | **163** (f5556) … 66 (f6978) | f5722, identical to f356 |
| 3 | f10085 271.507 … f107870 1008.974 | **none** | f10247 after **#40** (40 idle), f10413 after **#79** (37) | **67** (f10254) … 243 (f104254) | f10420, identical to f356 |

- **The console's sequence counter is not reset by a new SmartSoft session:** 162 → 163 and
  66 → 67. After the power cycle it started at 0x00 again (f20), as in E3.
- **Every session repeats the full start-up:** STRING 2 read, all-zero first poll, System 9 →
  [37] → ConsoleVersion, System 7 → the byte-identical 157-record list → a full download of
  157 objects.
- **The 157-record list is byte-identical in all three sessions and to E3's f352 and E2's
  f384**: all five 787-byte bodies are equal (direct comparison of the list bodies; body
  SHA-256 prefix `8208d7647d400d48`).
- **Connect-time pending list:**
  - Session 1, after the power cycle and the owner's set-up: LedStatus + DisplayContent (E3,
    after its power cycle: InputLevels).
  - Sessions 2 and 3, after a close and restart: **nothing**, so neither session received
    FaderInfo, DisplayContent or PlaybackInfo until something changed (H58).
- STRING 0 (`04 03 09 04`) was read only in session 1; each session read STRING 2 (f10, f5386,
  f10084). INFERRED: Windows caches the LANGID list per enumeration.

### MASTER with a channel at full (B2; H47, H57)

Fader 5 stood at full (log 319.24–397.27; FaderInfo f16384 `[17] = FF`) while the MASTER went
full → half → full. Same-payload FaderInfo pairs (fader-5 output body[17], MASTER body[194]),
all 23 between the rise and the fall of fader 5:

- **Down (346.32–350.22 s):** `FC/FB`, `E8/E7`, `D7/D2`, `C8/C7`, `B9/B6`, `AD/AC`, `A5/A2`,
  `9A/97`, `8F/8C`, `85/84`, `82/81`, `82/81`, `81/80`, **`7F/7F`** (f20302 … f20944).
- **Up (365.24–366.79 s):** `81/82`, `86/85`, `9D/9F`, `BF/C4`, `D5/D8`, `E9/E8`, `FF/FE`,
  **`FF/FF`** (f22914 … f23188).

Readings:
- **Every live level object follows the MASTER:** FaderInfo record 5, ChannelLevels[5],
  OutputLevels[6], LED block 5 G and LCD cell body[7] (0x86 → 0x82 at half → 0x86).
- **At rest at "half" all read 0x7F for 15 s:** FaderInfo f20944 (body[17] = 0x7F,
  body[193] = 0x00, body[194] = 0x7F), ChannelLevels f20952, OutputLevels f20928 and LED
  f20936. The next FaderInfo comes only at 365.24 s.
- The owner saw fixture 1 dim and come back (log 390.55).
- During motion the fader output lags the MASTER byte: +1 … +5 on the way down, −1 … −5 on
  the way up. INFERRED: the two bytes are sampled at slightly different moments.
- **Formula tests:**
  - Plain product (output = MASTER byte for a full fader): 3 / 23 pairs, including both rest
    points (0x7F, 0xFF).
  - H57 formula ⌈255 × round(M × 100 / 255) / 100⌉: 5 / 23 pairs. **It predicts 0x80 at the
    stable rest point M = 0x7F, where 0x7F is observed.**
  - "Live level = fader level, only the output scaled": contradicted, because ChannelLevels
    and FaderInfo fall too.
- E2's static pair (fader full, MASTER 0xAA → 0xAB, F90) is +1 and E3b's static pair is 0, so
  no formula of the two bytes alone fits both. HYPOTHESIS: the console works with finer
  internal values; the MASTER byte is truncated and the output rounded.

### BUMPS and bumps (B3; H46, H54)

- **BUMPS = FaderInfo body[193]:** 0x00 → 0xFF (f30010 … f30430, where body[193..194] =
  `FF FF`), → 0x00 (f32290 … f32654), → 0xFF (from f35394), → 0x00 at the end (f83970 …
  f84292). That is 104 changes, each during a logged BUMPS move; no other object changed.
- **Channel-mode bump of channel 7, with BUMPS at 0xFF:**
  - Channel 7 = 0xFF in ChannelLevels[7] (f40376), OutputLevels[8] = DMX 7 (f40360) and
    FaderInfo record 7 byte 0 = body[25] (f40368; byte 1, body[26], stays 0).
  - LED block 7 G = 0xFF (f40384) and LCD cell body[9] = 0x86 (f40392).
  - Everything returns to 0 at release.
  - The owner saw fixture 1's blue light, and DMX 7 is fixture 1's third channel (rig at
    DMX 5, 8, 11, 14).
- **Memory bump 1 (GROUP 1 = channels 7 and 8 at 100 %), BUMPS 0xFF, SOLO off:**
  - Channels 7 and 8 = 0xFF in ChannelLevels (f60354) and OutputLevels (DMX 7, 8, f60346).
  - FaderInfo record 1 bytes 0 and 1 = `FF FF` (f60332; body[3..4] stay 0).
  - LED block 1 = `FF 00 00 FF 00 00` (f60362) and LCD cells 7, 8 = 0x86 (f60370).
  - At release everything returns, with LED block 1 back to `46 00 00 46 00 00` (f60900).
  - The owner saw "first one BLUE, second one RED": DMX 7 and DMX 8.
- **With SOLO on** (LED block 52 white, f71756) the bump gave exactly the same changes
  (f74254 … f75286), and the owner saw the same. No other output was active, so "SOLO
  replaces" and "SOLO off adds" (manual, F6) must look alike here (INFERRED). The test could
  not separate them.
- **The unlogged third bump** (664.75–665.94 s) shows the same pattern.
- Together with E3 (BUMPS at 0 % → no level change, F88) this fits H54: bump output = BUMPS
  level, carried in ChannelLevels / OutputLevels / FaderInfo while the bump is held. Only the
  values 0x00 and 0xFF were tested.

### Modes and the 25-48 bank (B4; H53, H59, H56)

| Moment | ConsoleStatus body (f) | LED blocks 55 / 56 / 58 | LCD row labels |
|---|---|---|---|
| Channel mode 1-24 (S1 full list) | `00 00 00 00 00 01 00` (f1588) | `00 FF 00 …` / `00 0A 00 …` / `0A`×6 | " 1" / "13" |
| Memory mode | `01 00 00 00 00 01 00` (f46896) | `00 0A 00 …` / `00 0A 00 …` / `FF 00 00 FF 00 00` | unchanged |
| Channel mode 1-24 again | `00 00 00 00 00 01 00` (f86984) | as at the start | " 1" / "13" |
| Bank 25-48 | `00 00 00 00 01 01 00` (f88374) | `00 0A 00 …` / `00 FF 00 …` / `0A`×6 (f88382) | "25" / "37" (f88390) |
| Bank 1-24 again | `00 00 00 00 00 01 00` (f94520) | as at the start (f94528) | " 1" / "13" (f94542) |

- **ConsoleStatus:** body[0] = 0 in channel mode and 1 in memory mode (5 transitions across E3
  and E3b, including the 2 unlogged ones). body[4] = 0 in bank 1-24 and 1 in bank 25-48
  (2 transitions). Both agree with CL-ML's `data[0]` = fader-mode family and `data[4]` =
  intensity sub-mode (INT A / INT B).
- **LEDs:**
  - Block 55 is bright green for bank 1-24 and block 56 for bank 25-48 (they swap as H56
    predicted).
  - Block 58 is red in memory mode.
  - INFERRED: the 1-24, 25-48 and MEMS buttons' LEDs, not yet identified physically.
- **In bank 25-48, physical fader 1 drives FaderInfo record 1 (body[1]):** `07` … `FF` … `00`,
  f92020 … f93190. Record 25 (body[97]) stays 0x00.
  - Channel 25 (ChannelLevels[25]) and DMX 25 (OutputLevels[26]; Patch DMX 25 → 25, F70) go
    0x00 → 0xFF → 0x00; ChannelLevels[1] and OutputLevels[2] stay 0x00.
  - LED block 1 G (not block 25) and LCD cell body[3] (under the row label "25") follow.
- **FaderInfo records and LED blocks 1–24 belong to the 24 physical faders in every mode**:
  channel 1-24, channel 25-48, memory.
  - Records 25–48 never changed in E2, E3 or E3b.
  - LED blocks 25–48 stayed `00 0A 00 00 0A 00` in every state, including bank 25-48.
  - HYPOTHESIS: these halves are sized for a 48-fader console (the engine is shared with the
    SmartFade 2496, S11.14.11) and are unused on the 1248.

### LED colours versus the owner (H55, H48)

- **Memory mode, page 1** (f46904, as E3 f32328):
  - Recorded memories (blocks 1–8, 13) read `46 00 00 46 00 00`; the owner says "red, high
    intensity, because something is saved there".
  - Empty slots read `0A 00 00 0A 00 00`; the owner says "red as well but low intensity".
  - Block 21 reads `46 46 00 46 46 00` (R + G); the owner says "yellowish" (the sequence slot).
- **SOLO = block 52**, white `FF`×6, on at f71756 after `SOLO press` and off at f79228 after
  `SOLO release`. E2's block 52 was `FF`×6 (f40), so **SOLO was on during E2** (INFERRED).
- **Block 53** (Blackout LED, F85) blinks `37 37 FF 0A 0A 0A` while the MASTER is below full:
  f20310 … f23174, reproduced.
- **Block 50** is blue `00 00 FF 00 00 FF` during the unlogged crossfade only (f103328 …
  f104254). HYPOTHESIS: the GO (or crossfade) button LED.

### PlaybackInfo (ID 17) during the unlogged crossfade (H50)

66 payloads, all in 975.79–980.86 s; PlaybackInfo is never listed at connect and is not in the
157 list (H58).
- Record 5 (body[65..80]) at the first fetch, f103320:
  `06 06 FA 05 00 00 01 00 01 00 00 32 00 32 00 00`.
- **body[67] (Live) and body[68] (Next)** move in opposite directions, `FA/05` → … → `00/FF`
  (f104224), and reset to **`FF/00`** at completion (f104238). This is the CL-ML crossfader
  behaviour.
- **body[75..76] and body[77..78]** count down `00 32` → `00 28` (f103482) → `00 1E` (f103664)
  → `00 14` (f103846) → `00 0A` (f104028) → `00 00` (f104210) in exact 1-s steps, then return
  to `00 32` (f104238). INFERRED: fade time remaining in tenths of a second, starting from 5.0 s
  (the stack steps' UP / DOWN time, F74), not from the 5.1 s default below. body[79..80] stays 0.
- Record bytes 0–1 read `06 06` (record 5) and `04 04` (record 1, body[1..2]), against `00 00`
  in E2's f24. Their meaning is UNKNOWN; they did not change during the fade.
- No level object changed, consistent with the owner's "STACK button not lit" (coordinator).
- What started the fade is not logged; the log's "GO step = n" came 23.6 s later.

### SystemSettings and the LCD's fade times (H45)

- SystemSettings is `00 64 5A 3C 00 00 00 00 00 01 7F 00 00 **33** 00 **33** 00 00` in all
  three sessions (f388, f5754, f10452). E2 and E3 had `… 00 32 00 32 00 00` (F77).
- Every E3b LCD frame reads **"U 5.1" / "D 5.1"** (e.g. f36, body `28 20 31 20 … 55 20 35 2E 31
  31 33 20 … 44 20 35 2E 31`). E2 and E3 read "U 5.0" / "D 5.0".
- So body[12..13] and body[14..15] are u16 BE times in tenths of a second, 0x33 = 5.1 s: the
  default Up / Down times shown on the LCD (OBSERVED correlation across sessions).
- They are **not stored in the show file**: SHOW05.ASC = SHOW04.ASC byte for byte while the
  value differs.
- What changed them between E3 (29 Oct, 20:45) and E3b is UNKNOWN; ask the owner.

### FileName (ID 36)

All three sessions send "SHOW05.ASC": f1580, f6946 and f11644, body
`1E 53 00 48 00 4F 00 57 00 30 00 35 00 2E 00 41 00 53 00 43 00 …`. E2 and E3 sent
"SHOW04.ASC". The object tracks the console's last SD save (OBSERVED; the save was the owner's
backup at 08:55).

### InputLevels (ID 19) and DMX IN (H52)

- **InputLevels was never listed or fetched in E3b**: 0 payloads, although DMX IN was connected
  (owner). After this power cycle the pending list held LedStatus + DisplayContent instead (E3:
  InputLevels).
- H52 cannot be tested from E3b. Whether a transmitting source was connected is UNKNOWN.

### Writes and ConsoleEvent

- SmartSoft wrote only the six System messages, two per session, byte-identical to E2 and E3
  (`00 03 00 27 09 00 00`, `01 03 00 27 07 00 00`).
- No other type-1 OUT, no System `[0x0A][page][0]` (H51; no page change was made) and no
  ConsoleEvent (ID 20) in either direction.

### Comparison with SHOW05.ASC (coordinator addendum)

- **SHOW05.ASC (backup of 2026-10-30) vs SHOW04.ASC (backup of 2026-10-28):** byte-identical,
  15 944 bytes, SHA-256 `559636399c1d…7a90` for both. Between the two saves the console was
  power-cycled twice and its live state changed: MASTER, BUMPS, SOLO, modes, and the default
  fade times 5.0 → 5.1 s. **E6-type observation:** two saves of the same show produce
  identical files, and no live state or default fade time is written (OBSERVED, one pair).
- **`e2_asc_crosscheck.py` on the E3b JSONL against SHOW05.ASC**, three sessions:

| Check | Result |
|---|---|
| Patch | 3 × 512 / 512 (49 patched, 463 unpatched, 0 mismatches) |
| Memory | 16 GROUPs = 16 fetched slots; levels 2 304 / 2 304 (3 × 768); ceil rule 159 / 159 |
| Labels | 36 / 36 pages, 288 / 288 channels |
| Steps | StepContent 57 / 57 in all six fields |
| Sequence | Header 3 × matching; 21 / 21 records differ only in the cue number (H43) |
| SequenceMain | 5 fetches, all body[0] = 19 |
| SystemSettings | The SET values at body[6..11] in all 3 |
| Independents | `00 FF 00 01` × 6 |

## Reproduction of E2 / E3 facts (third session set, after another power cycle)

E3b holds three independent SmartSoft sessions on the same PC. The table lists what they
reproduce; promotion rules are the curator's.

| Facts | Reproduced in E3b? | Evidence |
|---|---|---|
| F40, F42 descriptors | Byte-identical, synthetic again | f2, f4 |
| F41 product string | Byte-identical, 3 sessions | f10, f5386, f10084 |
| F51–F54 transport, headers, replies, payload layouts | Yes, 3 sessions: 0 framing errors, stale words 25 829 / 25 829, echo 1 000 / 1 000, byte 1 = 0 and LE length 1 268 / 1 268 | Structure |
| F55 sequence bytes | Outbound 0, 1 per session; inbound +1 with no break **across** sessions; the wrap rule holds 4 more times | Structure, sessions |
| F56 start-up order | Order yes, 3 times; the pending list depends on state (S1: LedStatus + DisplayContent; S2 / S3: none) | sessions |
| F57 DirtyObjects | Yes: 268 lists, records = fetches 1 000 / 1 000, the 157 list byte-identical 3 times | sessions |
| F58 / F92 System writes | Byte-identical, after reply headers #40 / #79 in all three sessions (now 5 sessions in total) | sessions |
| F59 ConsoleVersion | Byte-identical, 3 times | f198, f5564, f10262 |
| F60 FileName | Same layout; value now "SHOW05.ASC" (follows the last save) | f1580 |
| F61 SystemSizes, F68 Patch | Byte-identical, 3 times | `startdiff`, cross-check |
| F62 timing | Idle median 30.79 ms; ≥ 10.33 ms after fetches | Structure |
| F63 HID unused | 0 URBs | Structure |
| F64 silent end | 3 times | f5384, f10082, f107870 |
| F65 health | All status 0 | Structure |
| F67 lengths | Same for every ID seen; PlaybackInfo 81; no InputLevels | summary |
| F70–F77 bodies vs the show file | Yes, against SHOW05.ASC (= SHOW04.ASC). SystemSettings body[13], [15] changed (fade-time setting, not show data) | cross-check |
| F80 power-on start 0x00 | Yes (f20), 2nd observation | f20 |
| F82, F83 fader / level mapping | Fader 5 again; plus channel 7 (bump), channel 25 (bank 25-48), MASTER, BUMPS | census |
| F84 LED blocks 1–48, memory-mode map | Identical map (f46904) | f46904 |
| F85 block 53 Blackout blink | Yes | f20310, f23174 |
| F86 LCD layout | Yes; plus row labels 25 / 37 in bank 25-48 | lcd |
| F87 ConsoleStatus body[0] | 4 more transitions; plus body[4] = bank | ConsoleStatus table |
| F89 live objects not in the 157 list | Yes, 3 times; after a restart nothing is pending | sessions |

## Hypotheses evaluated

| H | Verdict (OBSERVED at most) | Key evidence |
|---|---|---|
| H31 console counter not reset by a new session | **CONFIRMED in capture**: 162 → 163, 66 → 67 across restarts; power-on start 0x00 (2nd time) | f1612 / f5556, f6978 / f10254, f20 |
| H58 live objects only when pending or changed | **Supported**: after restarts nothing was pending, so FaderInfo, DisplayContent and PlaybackInfo were absent until they changed. The pending content after a power-on varies (E3 InputLevels, E3b LedStatus + DisplayContent): rule UNKNOWN | sessions |
| H30 System 9 / 7 | Reproduced 3 more times (#40 / #79) | sessions |
| H46 BUMPS at body[193] | **BUMPS part CONFIRMED in capture** (follows 4 logged moves, 0x00–0xFF, nothing else changes) | f30010 … f84292 |
| H47 post-MASTER live levels | **Scaling part CONFIRMED in capture**: FaderInfo, ChannelLevels, OutputLevels, LED G and LCD all follow the MASTER with the fader at full; owner saw the dimming | f20302 … f23188 |
| H57 ⌈255 × round(M %) / 100⌉ | **REJECTED**: predicts 0x80 at M = 0x7F, stable 0x7F observed in all level objects (f20944, f20952, f20928). The plain product fits E3b's rest points but not E2's 0xAA → 0xAB | MASTER section |
| H54 bump output = BUMPS level, in the level objects | **CONFIRMED in capture** for BUMPS = 0xFF (channel bump and memory bump), consistent with E3 at BUMPS = 0. Intermediate BUMPS levels untested; SOLO on / off indistinguishable here | bump section |
| H53 ConsoleStatus | body[0] mode (channel 0 / memory 1) **supported**, 5 transitions; **new: body[4] = fader bank** (1-24 → 0, 25-48 → 1) | ConsoleStatus table |
| H59 records 25–48 = the 25-48 bank | **REJECTED**: in bank 25-48 physical fader 1 moves record 1 (body[1]); record 25 (body[97]) stays 0; channel 25 / DMX 25 move | f92020 … f93190 |
| H48 LED blocks | Extended: blocks 1–24 = physical faders in every mode (block 1 shows channel 25 in bank 25-48); blocks 25–48 constant even in bank 25-48 | LED section |
| H55 sequence slot block 21 | **Supported by the owner's colour** ("yellowish"), 2nd observation of `46 46 00` | f46904 |
| H56 indicator LEDs | Block 53 reproduced. Block 55 / 56 swap **CONFIRMED in capture** (1-24 / 25-48 bank buttons, INFERRED identity). Block 58 red in memory mode (5 transitions). **New: block 52 = SOLO** (white), block 50 = blue during a crossfade | LED section |
| H45 SystemSettings body[12..17] | **Strongly supported**: body[12..15] 0x32 → 0x33 while the LCD's "U / D" times read 5.0 → 5.1 | f388, f36 |
| H50 PlaybackInfo | **Layout CONFIRMED in capture** (one unlogged crossfade): Live / Next at body[67] / [68], reset to FF / 00; body[75..78] count down in 1-s steps | f103320 … f104238 |
| H51 page select | UNTESTED (no page change, no such write) | — |
| H52 InputLevels with DMX IN | UNTESTED: InputLevels was never listed in E3b | — |
| H49 LCD | Reproduced; new: bank row labels "25" / "37"; channel cells 7 and 8 show bump output | lcd |
| H5 / H24 / H26 | Mirroring extended (bumps, BUMPS, bank). No ConsoleEvent either way (H24 n/a). H26 Independents part untested | — |
| H37, H38 | Supported again | Structure |
| H13, H25, H34, H35, H39, H41, H42, H43, H44 | Bytes reproduced 3 times (cross-check vs SHOW05.ASC) | cross-check |
| H36 / H40 label byte order | No new evidence (no label changes) | — |

**ConsoleLink (ML) claims touched:**
- "Fader values are output levels incl. Master" fits (FaderInfo record 5 follows the MASTER).
- "Mode-agnostic FaderInfo" fits (record 1 in bank 25-48 and in memory mode).
- "Bump LED red in MEMS, 0xFF while the memory is up, 0x46 recorded, 0x0a empty" reproduced.
- "Solo on = white" fits block 52.
- "data[4] = intensity sub-mode" fits ConsoleStatus body[4].
- "Live / Next reset to 255 / 0 after a crossfade" fits PlaybackInfo.
- The 16-bit sequence reading is contradicted again (four wraps).

## Surprises

1. **Four unlogged events, all in the console's own data:**
   - a third memory bump (664.75–665.94 s);
   - two mode switches (842.73 s memory → channel, 846.60 s back);
   - a playback crossfade at 975.70–980.86 s (09:11:07.9–09:11:13.2), although the owner
     declined GO and says GO would not have run the stack.

   The crossfade changed no output. Ask the owner whether GO or another playback button was
   pressed around 09:11:08.
2. **The default fade times changed from 5.0 s to 5.1 s** (SystemSettings, LCD) between E3 and
   E3b without being in the show file.
3. **FaderInfo is per physical fader**, not per channel bank (H59 rejected).
4. **FaderInfo record byte 1 moves only in the memory bump** (record 1 = `FF FF`); the channel
   bump moved only byte 0. Meaning UNKNOWN.
5. **InputLevels did not appear at all** although DMX IN was connected.
6. **SOLO was probably on during E2** (block 52 white in E2).

## Golden-test candidates (E3b, by JSONL seq; decoders only)

| seq | f | Content |
|---|---|---|
| 806, 2778, 3489, 5127 | 1612, 5556, 6978, 10254 | pseq continuity across SmartSoft restarts (162 → 163, 66 → 67) |
| 9, 10 | 18, 20 | Pending list after power-on: LedStatus + DisplayContent, pseq 0 |
| 10472, 10476, 10464 | 20944, 20952, 20928 | MASTER 0x7F at rest, fader 5 output 0x7F in FaderInfo / ChannelLevels / OutputLevels |
| 15215 | 30430 | FaderInfo body[193..194] = `FF FF` (BUMPS, MASTER) |
| 20180, 20184, 20188 | 40360, 40368, 40376 | Channel bump 7 at BUMPS 0xFF |
| 30166, 30173, 30177, 30181 | 60332, 60346, 60354, 60362 | Memory bump 1: FaderInfo record 1 `FF FF`, channels 7 / 8, LED block 1 |
| 44187, 44195 | 88374, 88390 | ConsoleStatus body[4] = 1 (bank 25-48), LCD "25" / "37" |
| 46033 | 92066 | FaderInfo record 1 driven by physical fader 1 in bank 25-48 |
| 35878 | 71756 | LED block 52 = SOLO on |
| 51660, 52112, 52119 | 103320, 104224, 104238 | PlaybackInfo crossfade start, end, reset |
| 194 | 388 | SystemSettings with 5.1-s default times |

## For the curator (proposed; nothing applied here)

**Facts.** E3b reproduces F40–F42, F51–F54, F57–F59, F61–F65, F67, F68 and F70–F77 (bodies)
in three more sessions after another power cycle (list above). It reproduces F80 (start 0x00)
and F82–F87, F89 and F92 a second time.

New OBSERVED facts:
- The console sequence counter continues across SmartSoft restarts.
- After a restart nothing is pending.
- BUMPS = body[193].
- The live levels are post-MASTER (all objects).
- Bump output appears in the level objects at the BUMPS level.
- ConsoleStatus body[4] = fader bank.
- FaderInfo / LED blocks are per physical fader in every mode.
- LED block 52 = SOLO, blocks 55 / 56 = bank buttons, block 58 red in memory mode.
- PlaybackInfo crossfade behaviour.
- SystemSettings body[12..15] = the LCD's default fade times (5.1 s), not stored in the show
  file.
- FileName follows the last SD save.
- Two saves of the same show are byte-identical (SHOW05 = SHOW04).

**Hypotheses.**

| H | Proposed status |
|---|---|
| H31 | CONFIRMED (OBSERVED) |
| H46, BUMPS part | CONFIRMED (OBSERVED) |
| H47, scaling part | CONFIRMED (OBSERVED) |
| H54 | CONFIRMED for BUMPS 0 / 0xFF; intermediate levels and SOLO add / replace stay OPEN |
| H57 | **REJECTED** (counterexample f20944 / f20952 / f20928). Successor HYPOTHESIS: at rest a full fader reads the MASTER byte or one more (E2 +1, E3b 0); no byte formula fits both; finer internal values |
| H59 | **REJECTED** (record 1 moves in bank 25-48). Successor HYPOTHESIS: FaderInfo records 1–24 = physical faders in every mode; records 25–48 and LED blocks 25–48 unused on the 1248 (sized for 48 faders) |
| H53 | Extend with body[4] = bank (supported) |
| H56 | Blocks 55 / 56 swap confirmed; add block 52 = SOLO, block 50 = blue during a crossfade (HYPOTHESIS: GO LED) |
| H45 | Body[12..15] = default Up / Down times, strongly supported (5.0 → 5.1 with the LCD) |
| H50 | Layout CONFIRMED (OBSERVED once; trigger unlogged) |
| H55 | Supported by the owner's colour |
| H58 | Supported (restart part) |
| H51, H52 | Unchanged (UNTESTED) |
| H30, H37, H38 | Further reproductions |

**New hypotheses** (IDs by the curator):
1. FaderInfo record byte 1 = a memory-flash component (moves only with the memory bump).
2. PlaybackInfo record bytes 0–1 (`04 04`, `06 06` here, `00 00` in E2) = stack or step
   position.
3. LED block 50 = GO / crossfade LED.
4. The post-power-on pending list reflects what changed since power-on (E3 InputLevels, E3b
   LedStatus + DisplayContent).

**Docs:**
- `reverse-engineering.md`: B1–B4 done; B5 not done (declined).
- `responses.md` / `state-model.md`: the session-restart behaviour, the per-physical-fader
  FaderInfo, the bump representation, PlaybackInfo, ConsoleStatus body[4].
- `consolelink-analysis.md`: N1 BUMPS confirmed, N2 scaling confirmed, N5 confirmed.
- `asc-format.md`: two saves identical; default fade times not in the file.

## Open follow-ups

1. Owner (zero risk):
   - Was GO or another playback button pressed around 09:11:08?
   - How did the Up / Down default times become 5.1 s, and is that intended?
   - What was connected to DMX IN, and was it sending?
   - Which physical buttons carry LED blocks 55, 56, 58 and 50?
2. Next passive capture:
   - BUMPS at about 50 % with a channel bump (H54 scaling).
   - SOLO on while another fader is up (add vs replace).
   - A known DMX source on DMX IN (H52).
   - Several stable MASTER positions with a channel at full (H57 successor).
   - A memory page change (H51, ConsoleStatus body[2]).

   Same safety rules.

## Owner answers (2026-10-30, after the analysis; added by Claude)

Archived verbatim in `2026-10-30-e3b-owner-statements.txt` (SHA-256
`eb764fa7f576c4979d26725187abeb328dab0f3c5da1fc61e2d9b4e7eb4466c4`); Claude's readings are
marked INFERRED there and the knowledge base governs where they differ.

- **The crossfade at 975.70–980.86 s (09:11:08)** was the owner pressing the GO ("play")
  button once, with the STACK button unlit, while deciding how to answer the GO prompt
  (answered "n" 23 s later). Owner action, not a console artefact; GO with STACK unlit ran a
  crossfade that changed no output.
- **Default fade times 5.0 → 5.1 s:** the owner accidentally moved the LCD wheel ("pretty
  sensitive"). Supports H45: SystemSettings body[12..15] and the LCD "U 5.1 / D 5.1" follow the
  wheel, and the value is not stored in the show file.
- **Rig:** one ADJ **DOTZ BAR 1.4** LED bar in its 12-channel mode at start address 5, i.e. four
  cells of three channels at DMX 5, 8, 11, 14 (what the owner earlier called "4 RGB lights").
  The owner gave this when asked about DMX IN, so it most likely hangs on DMX OUT; whether
  anything is plugged into the DMX IN socket stays UNKNOWN (H52 untested; the owner is asked).
