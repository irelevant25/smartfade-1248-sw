# 2026-10-31 E9b: SmartSoft writing to the console, the repeat of E9 (USBPcap, passive, guided script)

- **Date:** 2026-10-31, morning. USBPcap ran 08:37:27.235–09:05:24.043 local time (CET, UTC+1; the first and last frames of
  the raw root-hub capture). The committed console-only split runs from 08:37:27.234654 (f1) to 09:05:18.307077 (f208876).
  The guided script ran 08:34:59.192–09:05:28.999; it started SmartSoft three times (08:38:13.104, 08:54:54.070,
  08:56:30.848), and SmartSoft was closed at 08:54:34.904–08:54:42.153, 08:56:13.123–08:56:17.518 and 09:05:15.383–09:05:20.204
  (log).
- **Experiment:** **E9b**, the repeat of E9 ([E9 record](2026-10-30-e9-smartsoft-writes.md), "Next capture to take" B): three
  SmartSoft Online sessions in one capture, driven by the owner through `tools/usb-probe/e9b-guided-session.ps1` (3ce3900) and
  recorded passively by USBPcap. It repeats E9's live Simulator steps, tests H81–H86, renames channel 7's label across two
  SmartSoft restarts and, at the owner's choice, changes one cell in SmartSoft's Mem Edit tab (B7). **Our own software was not
  involved and sent nothing;** every write in this capture is SmartSoft's, made as for any user.
- **E9b clears nothing.** It is the second session of SmartSoft's writes; where it reproduces E9 the statements below propose
  VERIFIED, which means "SmartSoft and the console were seen to behave so in two sessions", **never that we may send the
  message** (CLAUDE.md; [../safety-checks.md](../safety-checks.md): SC-1 clears the poll and the listed fetch for a read-only
  session, nothing else; no type-1 data header is encoded anywhere in the solution). Everything first seen here is OBSERVED
  (one session) or weaker.
- **Console:** ETC SmartFade 1248, USB device address **21** on bus 1 (10 in E8b and E9: the console re-enumerated after its
  power cycle).
  - Firmware: the ConsoleVersion object reads "3.0.2.9.0.43" in each session (seq 101 f202, seq 64770 f129540, seq 70157
    f140314); the owner's Diagnostics reading of 2026-10-30 is 3.0.2.9.0.43 (E8 statements; not re-read for E9b).
  - **Powered off since E9** (log 08:36:59.587 "console powered off since E9 = y"). The inbound counter starts at 0x00 (f20,
    F80).
- **Host:** the owner's PC, Windows 11 Pro build 26300 (not restated); Memory Integrity **off** (read by the script,
  08:36:59.695); interface 1 on ETC's driver, service **ETCHSP** (read by the script, 08:37:00.650; the `etchsp.sys` version
  was not re-read, 2.00.000 in E2–E3b); SmartSoft 3.0.43 (package 3.0.2.9.0.43b), started by the script with `Start-Process`.
- **Connection:** USB A-B cable straight into the PC (not restated). USBPcapCMD on root hub `USBPcap1`, with the script's
  suggested `-A --inject-descriptors` (INFERRED from the injected descriptors of addresses 1, 2, 18 and 21 at frame 1's time
  stamp). The owner logged "usbpcap running" at 08:37:30.577.
- **Show backup (safety rule 3): taken.** The script's gate logged "backup taken: SHOW08.ASC" at 08:35:31.383, before USBPcap
  and SmartSoft. `smartfade-sd-card-backup/SHOW08.ASC` (git-ignored folder; 15 968 B, SHA-256
  `079dc11502e048c895243d4727b75871ef5e4692861b874c9fbcf81524ccb8dc`) differs from `SHOW07.ASC` (the pre-E9 show, byte-identical
  to SHOW04–06) in exactly four lines (`diff`, checked when this record was written): `GROUP 7` (page 1, memory 7, "NOC 3")
  reads `CHAN 7/100 15/8` instead of `CHAN 15/8`, and the three stack cues that use memory 7 (`CUE 10.2`, `10.4`, `10.6`,
  `$$MEMORY 0 6`) repeat the extra `7/100`. That is E9's mistaken edit (F139). **Restored by the owner after the session:** his
  next SD save, `SHOW09.ASC` (about 09:45), is byte-identical to `SHOW07.ASC` (coordinator's `cmp`; hashes equal here). The console reported FileName "SHOW08.ASC" in all three downloads (seq 792 f1584, seq 65461
  f130922, seq 70848 f141696; F103).
- **Starting position** (script; owner 08:38:04.138): channel faders at zero, MASTER and BUMPS at full, channel mode, BLACKOUT,
  SOLO, IND 1 / IND 2 and STACK off, page 1; memory 9 of page 1 empty (owner, 08:38:11.407). The console was in channel mode
  at connect (ConsoleStatus `00 00 00 00 00 01 00`, seq 796 f1592).
- **Rig:** the DOTZ BAR on DMX 5–16, nothing on DMX IN (owner, 2026-10-30; not restated).
- **Action log:** the guided script's timed log (138 lines, CRLF), PC clock, local time; the owner's answers to E9's four
  questions open it (08:36:18–08:36:59). Stamp semantics as in E8b and E9: a `Step` line is stamped when the owner presses Enter
  "at the moment you START doing this", a `Confirm-Done` line after the action, a `wait … done` line after the wait. In E9b the
  first write of a Simulator step came 0.82–3.68 s after its stamp, except the fader-5 step (+8.89 s: the owner first set the
  physical fader, as asked) and the label (+7.06 s, typing); offsets per step in the action table.
- **The owner's statements** (`2026-10-31-e9b-owner-statements.txt`): the backup, his habit of pulling fader 5 to zero right
  after the B7 recording (unlogged), two notes added after the session about the MASTER ending at about 50 % (section "The
  MASTER at the end of E9b"), and, later the same morning, "master is on 100% already" (about 09:35) and the restore of memory
  7 with a new SD save `SHOW09.ASC` (about 09:45), which is **byte-identical to `SHOW07.ASC`** (SHA-256 `559636399c1d…7a90`
  both, checked when this record was written). Owner statements never promote by themselves.

**Conventions** (as in the E9 record):
- `seqN` is the record number in the normalized JSONL (`2026-10-31-e9b-smartsoft-writes-repeat.jsonl`; the notes do not take
  numbers); `fN` is a frame of the committed console-only `.pcap` split: the data-carrying frame, i.e. the submission frame for
  OUT and the completion frame for IN. A write is cited as header / payload (for example seq 92052 / 92053, f184103 /
  f184105).
- `t` is seconds since the split's first frame (08:37:27.234654 local); local time = 08:37:27.235 + t. Log stamps are quoted
  with their t (for example "h85 reset" 08:59:31.328 = 1324.093).
- `pseq` is a payload's own sequence byte (inbound: the console's counter; outbound: SmartSoft's counter, from 0x00 in each
  SmartSoft session).
- `body[n]`, LED block k (phase A RGB, phase B RGB; blinking = phase A ≠ phase B), FaderInfo record N (body[1 + 4(N − 1)],
  bytes 0–3), ChannelLevels[c] = body[c], OutputLevels DMX d = body[1 + d], and the LCD cells as in the E8b and E9 records;
  ConsoleEvent bodies are written `[kind][index or code][value][previous]`.
- Sessions: **S1** (seq 4 … 64680, 45.95–1031.50 s), **S2** (seq 64681 … 70067, 1046.91–1128.26 s), **S3** (seq 70068 …
  104438, 1143.76–1671.07 s).
- Every quoted byte comes from the cited seq or frame. Readings carry their status: OBSERVED (seen in this session),
  INFERRED, HYPOTHESIS, DOCUMENTED, UNKNOWN. "Reproduced" means seen again in E9b with a timed action.

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-31-e9b-smartsoft-writes-repeat.pcap` | `7faceb355a8b0df2b44eca13c9d0b097e9a69175622a40a18a5960421ce77c7c` | 10 446 141 B, 208 876 frames, 08:37:27.234654–09:05:18.307077 | yes (b5027c5), **the evidence** | Console-only split (device address 21) of the USBPcapCMD root-hub capture; classic pcap, link type 249, µs; made with `tshark -r <raw> -Y "usb.device_address == 21" -F pcap -w <split>`. **The raw root-hub capture is not committed** (it holds the traffic of other devices, addresses 1, 2, 3, 4, 18, 19): `e9b.pcap` in the repository root (git-ignored), SHA-256 `0a07109d2add9639fd00f72200e3fae46834a8741cc74319f11e58fe74262ef8`, 27 895 136 B, 316 092 frames, 08:37:27.234654–09:05:24.043032. Completeness below |
| `2026-10-31-e9b-smartsoft-writes-repeat.jsonl` | `d485ec3eeacb1f352365ef7b8575b70bba2445f0f885b647cf87ff4984629822` | 25 184 777 B, 104 579 lines: 1 header, 104 438 `frame` records, 140 `note` records | added with this record (derived) | The split normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 21 --bus 1` (0 other-device frames). Notes: the 138 action-log lines (`--notes-log`, `--notes-utc-offset +01:00`; the first nine have negative `t`, they precede the capture) and two analyst notes (capture start with the raw capture's provenance; the raw capture's end). Two runs gave identical bytes. Command below |
| `2026-10-31-e9b-actions.txt` | `98ff4b1a532cea01219320d9889a30a8bf72a9a4d44e2acf07c10dc5a6008d86` | 7 984 B, 138 lines, CRLF | yes (b5027c5) | The guided script's action log, with the owner's answers to E9's questions at the start and his typed answers during the session. Byte-identical to the untracked `e9b-actions.txt` in the repository root (the script's `-LogFile`) |
| `2026-10-31-e9b-owner-statements.txt` | `b248c6cd0adfe400c5ee4ff5dc09b6b356caf7cb536af1faba38dff62ce32588` (the version committed with this record, with the SHOW08 timing corrected; earlier versions: b5027c5, 65fffc3, b8ceab4 `0373044c…8666`, 4850ee5 `552b9f9f…f5eb`, cf9fa24 `2bdcefa7…57ef9`) | 5 505 B | yes (b5027c5, 65fffc3, b8ceab4, 4850ee5, cf9fa24, and with this record) | Context, the backup and its difference from SHOW07, the owner's statements after the session (fader 5 pulled to zero after the B7 recording; the MASTER at about 50 % at the end, his clarification, "master is on 100% already"; memory 7 restored and SHOW09.ASC saved), transcribed by the assistant. Owner statements never promote by themselves. On when SHOW08.ASC was saved see "Surprises and anomalies", 12 |
| `smartfade-sd-card-backup/SHOW09.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` | 15 944 B | no (folder git-ignored) | Saved by the owner after the session (about 09:45) after restoring memory 7: **byte-identical to `SHOW07.ASC`** (and SHOW04–06): no `GROUP 9` or `GROUP 10`, `GROUP 7` `CHAN 15/8` again |
| `tools/usb-probe/e9b-guided-session.ps1` as run | blob `34aa76a13964a3d42207b54f5cc710c2d76c72ff` at 3ce3900 (content SHA-256 `14f2a91bd2159fdae5ac8f11efaeb0dbd64da19af7c8d5ea933c912f9bf9c64a`) | 265 lines | yes (3ce3900, 2026-10-31 00:11:22, before the session; unchanged at b5027c5) | The guided session that ran. It talks only to the operator and starts SmartSoft; it sends nothing to the console |
| `smartfade-sd-card-backup/SHOW08.ASC` | `079dc11502e048c895243d4727b75871ef5e4692861b874c9fbcf81524ccb8dc` | 15 968 B | no (folder git-ignored) | The backup saved at 08:35:31, before the session: SHOW07 plus E9's memory-7 edit (four lines) |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `ea7627d7cdd0ff214f003f290ae7cb6b75b73149b4e753011c76b129efd20fe8` | | yes | Normalizer version 2, unchanged since E3 |
| `tools/protocol-analyzer/hsp_summary.py` | `3f324e9f94732235d5cbaa0ea6ce76619db4b1357612de5828db1ccfcb0fd1ec` | | yes | `summary`, `sessions`, `lcd`, `startdiff` |
| `tools/protocol-analyzer/poll_rules.py` | `b54cf6d24e01469b820312f75762bc7d1e873797d1e0c109fcda3ae244dc457e` | | yes | Stale-word rule and SmartSoft's cadence |
| `tools/protocol-analyzer/e9/kind0.py`, `kind1.py`, `listfetch.py`, `biglist.py`, `cmp_records.py`, `writestats.py` | `2861564b514f64753be95729e0a65879f4b4099d80d5ce083fc93ec8f1c6fb3a`, `3107ed282e854ba83f68afc4b6e8af7ce0978145c321ed10b436612a8ab732be`, `22083e1ed9736f1e8728b4996fe1fc234af7b88bbd52ab2fa9d0d0c225bd4237`, `7c388a785b0dcc542598bca60751e1e4c3df39b05bfa350349569abc5df8979f`, `66582f6f069189a5dfdb5f95cd030ce44e1d338919c222387ab83402c983d3eb`, `88d144030e866550872132fa333cc4d38ca07ecaff2bd002f4821020d0ecaf28` | | yes | Run unchanged on the E9b JSONL |
| `tools/protocol-analyzer/e8b/objtable.py`, `cst_led.py`, `blk2548.py`, `tshark_xcheck.py` | `285bdf8c83877ac5c67cb18d1d6ab6bc7c9930c0f5b93dd01126953830bf537b`, `8c83536f976bb92494d54ecf0902d5885b78ad315b2685bf6e70816b8849b00f`, `b3b341721122ae43370672fad2d7e1b56dd80eac87d5a03b65b064601edfd58c`, `d97668b353633cb69c38b5d3301a2f9efbfd88d8e0700df499bc839bf1541eef` | | yes | Run unchanged on the E9b JSONL |
| tshark 4.0.3 (Wireshark portable) | | | no | Frame counts by address in the raw capture, the reproduction of the split, the `usb.capdata` cross-check |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-31-e9b-smartsoft-writes-repeat.pcap -o %TEMP%\e9b.jsonl --device-address 21 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the ConsoleVersion object of this capture, f202, f129540, f140314; the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E9b)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 08:36:59.695)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 08:37:00.650; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-31-e9b-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (tshark -r <raw> -Y 'usb.device_address == 21' -F pcap -w <split>; re-run with tshark 4.0.3 it reproduces the committed split byte for byte) of the USBPcapCMD root-hub capture e9b.pcap (SHA-256 0A07109D2ADD9639FD00F72200E3FAE46834A8741CC74319F11E58FE74262EF8, 27895136 bytes, 316092 frames, 08:37:27.234654-09:05:24.043032 local time), which the owner started on root hub USBPcap1 with the guided script's suggested command (-A --inject-descriptors, INFERRED from the injected descriptors of several devices at frame 1's time stamp); the raw capture is not committed because it holds the traffic of other devices (addresses 1, 2, 3, 4, 18, 19); frame 1 is at 08:37:27.234654 local time, the raw file's first frame; the console was powered off between E9 and E9b (owner, log 08:36:59.587)" --note 1676.808378 "analyst: the raw capture's last frame (another device) is at 09:05:24.043032 local time; the console's last frame, frame 208876 of this split, is at 1671.072423 s (09:05:18.307077); the log stamps SmartSoft's third close at 09:05:15.383 (start) and 09:05:20.204 (closed) and USBPcap's stop at 09:05:28.997"
```

Expected: source SHA-256 `7FACEB35…7C7C`, 104 438 transfers from 208 876 device frames (0 other-device frames skipped), 140
notes, output SHA-256 `d485ec3e…9822`.

**Completeness of the split:**
- tshark 4.0.3 re-run on the raw capture with the filter above writes a file **byte-identical** to the committed split
  (SHA-256 `7faceb35…7c7c` both).
- Frame counts in the raw capture by address (counted, not read): address 21: 208 876 (208 862 bulk, 14 control); address 19:
  100 356 (100 350 interrupt); address 4: 6 680 (6 578 isochronous); address 3: 154; addresses 1, 2, 18: 14, 6, 6 control.
  Total 316 092.
- The normalizer run on the raw capture with `--device-address 21` found 208 876 frames and skipped 107 216 frames of other
  devices unread; its 104 438 records equal the split's in every field except the pcap frame numbers (0 differing records,
  `cmp_records.py`).
- Independent check: tshark's `usb.capdata` equals the JSONL `data` in 104 431 of 104 431 data-carrying bulk records (0
  mismatches, every capdata frame used; `tshark_xcheck.py`).

The other figures come from small read-only scripts that import the committed functions of `hsp_summary.py` (writes per step
with the action log, a merged timeline of the live fields, the MASTER byte over all FaderInfo payloads, the changed bytes of a
held bump in two captures, the non-live objects outside the downloads, the three downloads compared), run with `py -3.13 -I`
from the session scratchpad, outside the repository and outside the capture directory; they are not committed with this record
(this task allowed writes under `docs/protocol/captures/` only; "Next capture to take", D).

## Session health

| Check | Result | Source |
|---|---|---|
| Transfers | 104 438: 3 synthetic EP0 transfers (`--inject-descriptors`, f1–f6; the device descriptor `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01`, as in E3b, E8b and E9), 4 real EP0 reads, 104 431 bulk on interface 1: **52 485 OUT** on 0x04 (50 342 polls, 1 289 fetches, 427 data headers, 427 data payloads) and **51 946 IN** on 0x83 (50 342 reply headers, 1 604 payloads) | `summary` |
| Three SmartSoft sessions | S1: polls 31 090, fetches 739, data writes 388, payloads 980; S2: 2 531, 158, 3, 160; S3: 16 721, 392, 36, 464 | `sessions` |
| EP0 | S1: GET_DESCRIPTOR STRING 0 (seq 4, f7 / f8, `04 03 09 04`) and STRING 2 (seq 5, f9 / f10, "ETC SmartFade") at 45.951–45.954, 1 ms before the first poll; S2 and S3: STRING 2 only (seq 64681, f129361 / f129362, 1046.908; seq 70068, f140135 / f140136, 1143.760), as E3b's sessions 2 and 3 (F94) | JSONL |
| Interface 0 | **No URB on 0x81 or 0x02** (the raw capture holds no interrupt frame of address 21): SmartSoft never used the HID interface, also while writing (F51, F63) | raw counts |
| USBD status | 0x00000000 in 104 438 of 104 438 transfers | `summary` |
| Framing | **0 errors.** Every OUT starts with a 12-byte header, w0 ∈ {0, 1, 2}; every type-1 header is followed by one OUT transfer of exactly w1 bytes (427 of 427); every IN follows a poll (headers) or its header (payloads) | `summary` (H15, H16) |
| Stale words (F52) | 50 342 of 50 342 polls carry the words of the most recent fetch or reply header (`poll_rules.py`), and so do **427 of 427 data headers** (50 768 of 50 768 poll and data headers after the first poll, `summary`) | `poll_rules.py`, `summary` |
| Replies (F53) | 50 342 polls, 50 342 reply headers: 1 289 type 3, 315 type 4 with a list, 48 738 idle; type-3 echo 1 289 of 1 289 | `summary` |
| Lists and fetches (F57) | 315 lists (S1 241, S2 2, S3 72); **1 289 records = 1 289 fetches, in list order**; list sizes 1 × 138, 2 × 33, 3 × 20, 4 × 51, 5 × 70, 157 × 3. **c = 0 in all 1 289 records** (E9's c ≠ 0 records did not recur: no memory other than memory 1 was selected for editing, section H83) | `listfetch.py` |
| Inbound counter (F55, F80, F93) | 1 604 payloads, pseq **0 (0x00, seq 10, f20) … 67 (0x43), 0 breaks**, 6 wraps (f26416 → f26428, f57688 → f57696, f86180 → f86192, f130026 → f130034, f141568 → f141576, f189664 → f189670), byte 1 = 0x00 and LE length = payload − 5 in 1 604 of 1 604. **0x00 at the first payload after the power cycle** (the fourth power-on seen: E3, E3b, E8b, E9b) and **continuous across both SmartSoft restarts**: S1's last payload pseq 211 (seq 62126) → S2's first 212 (seq 64766, f129532); S2's last 115 (seq 65477) → S3's first 116 (seq 70153, f140306) | `summary`, `sessions` |
| Outbound counter (F55, F131) | **From 0x00 in each session, +1 per data payload across all ids, 0 gaps:** S1 388 writes, pseq 0x00 (seq 94, f187) … 0x83 (seq 63422, f126843), one wrap 0xFF (seq 39860, f79719) → 0x00 (seq 39866, f79731); S2 3 writes, 0x00 … 0x02 (seq 68753, f137505); S3 36 writes, 0x00 … 0x23 (seq 92053, f184105). The counters prove that no write is missing between the ones cited (S3: pseq 0x01 at 1146.283, 0x02 at 1278.642) | ad hoc |
| Body lengths (F67) | Every fetched object has its F67 length (Memory 138, ChannelText 39, FaderInfo 195, LedStatus 451, PlaybackInfo 81, System 3, …). Outbound: System 3, ConsoleEvent 3 and 4, ChannelText 39, and **Memory 90** (new, the B7 write) | ad hoc |
| Latency | Reply header submit → completion max 20.62 ms (seq 58395, 934.260, at the page change on the console), payload max 4.96 ms (seq 45530), OUT max 1.93 ms (seq 81153) | `objtable.py` |
| Session ends | Each session's last exchange is an idle poll / reply (S1 seq 64679 / 64680, f129357 / f129360, 1031.501; S2 seq 70066 / 70067, f140131 / f140134, 1128.263; S3 seq 104437 / 104438, f208873 / f208876, 1671.072), 2.4–3.8 s after the log's "close … start"; no closing message (F64); the raw capture has no console frame after f208876 up to its end (09:05:24.043) | log, raw |

**SmartSoft's cadence** (`poll_rules.py`; ms): poll → poll (submit) n = 50 339, min **21.60**, median 31.12, max 79.00;
fetch → next poll n = 1 289, min 10.49, median 15.48, max 17.81; payload completion → fetch n = 1 035, min 20.39, median 28.72,
max 37.06 (F62 reproduced). 17 idle poll → poll intervals were below 29 ms (E9's minimum: 29.01 ms): one at each session start
(45.981, 1046.931, 1143.787), one at 301.457 and **13 at 1460.495–1461.450** (seq 90826 … 90894, minimum 21.60 ms at seq 90886
→ 90888), while the owner worked in SmartSoft's Mem Edit tab (B7) and nothing else happened on the bus. SmartSoft's own
interval came closer to SC-1's floor for our sender (21 ms) than in any earlier capture; the floor is ours and is not changed by
this (it was never derived as SmartSoft's minimum). **Writes:** data header → its payload 0.067–0.254 ms (median 0.118, submit
to submit); data payload → next OUT 14.69–16.23 ms (median 15.44); consecutive writes at least 45.55 ms apart (median 61.81 ms
within a burst, 372 gaps under 200 ms): one write per loop iteration (S11.2 step A, INFERRED), as in E9.

**Objects** (`objtable.py`; payloads): Memory 52 (48 in the three downloads, 4 listed after recordings), Sequence 3,
SequenceMain 4 (3 downloads, 1 at GO), StepContent 57, Patch 3, Page 36, ChannelText 288, Independents 7, OutputLevels 125,
FaderInfo 186, ChannelLevels 129, PlaybackInfo 68, DisplayContent 113, LedStatus 168, ConsoleStatus 27, FileName 3,
ConsoleVersion 3, SystemSizes 3, System 11 (9 in the downloads, 2 × (5, 5, 0) at EDIT MEM selections), SystemSettings 3. **No
ConsoleEvent (20) and no other id was listed or fetched;** the console never echoed a write.

## The connect phases (SmartSoft's seventh, eighth and ninth sessions with the System pair)

| | S1 (after the power cycle) | S2 (restart) | S3 (restart) |
|---|---|---|---|
| STRING read, first poll | STRING 0 + 2 (seq 4, 5); first poll seq 6 (f11, 45.954), all zero | STRING 2 (seq 64681); seq 64682 (f129363, 1046.909), all zero | STRING 2 (seq 70068); seq 70069 (f140137, 1143.761), all zero |
| Pending at connect | **3 records, pseq 0x00** (seq 10, f20, 45.984): `00 00 11 00 28 00 03` + PlaybackInfo (17), LedStatus (22), DisplayContent (21); first reply idle (f14), second announced 22 bytes (f18) | **nothing**: 40 idle replies until the System write | **nothing**: 40 idle replies |
| System `09 00 00` | header / payload seq 93 / 94 (f185 / f187, 47.260) after reply header **#40** | seq 64762 / 64763 (f129523 / f129525, 1048.164) after **#40** | seq 70149 / 70150 (f140297 / f140299, 1145.018) after **#40** |
| → list [37], ConsoleVersion | seq 97 (f194) → seq 101 (f202) "3.0.2.9.0.43" | seq 64766 (f129532) → seq 64770 (f129540) | seq 70153 (f140306) → seq 70157 (f140314) |
| System `07 00 00` | seq 176 / 177 (f351 / f353, 48.525) after **#79** | seq 64845 / 64846 (f129689 / f129691, 1049.431) after **#79** | seq 70232 / 70233 (f140463 / f140465, 1146.283) after **#79** |
| 157-record list and download | seq 180 (f360); download 48.54–55.82 s (last payload seq 808, f1616) | seq 64849 (f129698); 1049.45–1056.77 s (seq 65477) | seq 70236 (f140472); 1146.30–1153.59 s (seq 70864) |

- The System pair is **byte-identical** in all three sessions (`01 00 07 00 00 …` + `00 03 00 27 09 00 00` and `01 03 00 27 07
  00 00`), each after reply headers #40 and #79 of its session: the seventh, eighth and ninth SmartSoft sessions with the pair
  (F58, F92; SmartSoft's behaviour only, never clearance).
- **The 157-record list is byte-identical in all three sessions and to E9's** (`biglist.py`; 787-byte body, SHA-256 prefix
  `8208d7647d400d48` in all three, the prefix F94 records for E2, E3, E3b and E9): nine sessions with the same list. It came
  once per session, after System `07 00 00` (F89).
- **The three downloads are identical except one object:** 156 of 157 bodies are byte-identical across S1, S2 and S3; the
  exception is ChannelText (9, a = 6), channel 7's label, which the owner renamed and restored in between (H84 below).
- **Against E9's first download** (`startdiff --other` E9): 155 first bodies identical; the differences are the live state,
  FileName ("SHOW08" against "SHOW07"), and two show objects that E9 changed: **Memory (0, 6, 0) = memory 7 with channel 7 =
  0xFF** (body[10], seq 232, f464; E9's first download had 0x00) and **ChannelText 6 = `06 03 06 00 00 00 61 00 6A` + zeros**
  (seq 424, f848; E9's first had all zeros). Memory 1 (seq 208, f416: channels 7 and 8 = `FF FF`) equals its pre-E9 body:
  the owner's restore by hand after E9 holds. Memories 9 and 10 are not in the list (the list is unchanged), so they are
  empty, as the owner deleted them after E9.
- **The pending list after the power-on** held PlaybackInfo in addition to the LedStatus / DisplayContent pair that E3b
  session 1 and E8b saw after their power-ons (F124). The owner set the starting position between the power-on and the
  SmartSoft start (log 36.903, contents unlogged); something in it changed PlaybackInfo (INFERRED). No ConsoleStatus was
  pending, so no mode switch happened before the connect (INFERRED from F124's evidence that a mode switch lists ConsoleStatus):
  the console powered on in channel mode. Supports H65 (the list names what changed since the power-on), no test.
- Reproduces F56 (string reads, all-zero first poll), F58 / F92 (the pair, sessions 7–9), F59, F80 (0x00 at power-on), F89
  (the 157-record list once per session), F93 (the counter continues across restarts), F94 (each session repeats the whole
  start-up; nothing pending after a restart; STRING 0 only in the first session after enumeration) and F103 (FileName =
  "SHOW08.ASC", the last SD save).

## Inventory of the PC → console writes

**427 data writes** (type-1 header + payload, bulk 0x04, interface 1), all from SmartSoft, all with USBD status 0:

| Kind | Count | Header (w0 w1, words LE) | Payload `[pseq][u16 LE len][id][body]` | Body | E9 |
|---|---|---|---|---|---|
| System (39) | 6 (3 pairs) | `01 00 07 00` + stale words | `pseq 03 00 27 …` | `09 00 00`, `07 00 00` (the F58 pair) | 2 |
| ConsoleEvent (20), kind 0 (fader) | **366** (index 0x00: 135, 0x04: 93, 0x30: 73, 0x31: 65) | `01 00 08 00` + stale | `pseq 04 00 14 …` | `00 [index] [value] [previous]` | 332 |
| ConsoleEvent (20), kind 1 (key) | **52** (26 press / release pairs) | `01 00 07 00` + stale | `pseq 03 00 14 …` | `01 [code] [01 = press / 00 = release]` | 64 |
| ChannelText (9) | 2 | `01 00 2B 00` + stale | `pseq 27 00 09 …` | `[channel − 1] 03 06 [36 bytes]` (39 bytes) | 3 |
| **Memory (0)** | **1** | **`01 00 5E 00`** + stale (w1 = 94) | **`pseq 5A 00 00 …`** | **`[page − 1][slot − 1] 01 30` + 48 level bytes + `03 06` + 36 label bytes (90 bytes)** | **0** |

**Not seen:** no other object write, no ConsoleEvent kind 2 (the wheel was excluded), no System other than the pair (no page
select: H51; no erase: F121), nothing on interface 0, no control transfer of SmartSoft's besides the string reads.

### Action log matched to the writes

"Offset" = the first write of the step minus the log stamp. Every write of the capture is in this table (427).

| Log stamp (t) | Step | Writes (pseq; header seq / payload frame) | Offset | The console's answer (first list; seq) |
|---|---|---|---|---|
| 45.869 | `smartsoft start #1` | System pair pseq 0, 1 (seq 93, 176) | +1.39 s | the connect phase above |
| 63.453 / 123.470 | `smartsoft online #1` / 60-s baseline | none | | none |
| 134.193 / 154.199 | `simulator opened` / 20 s untouched | **none: opening the Simulator wrote nothing** | | none |
| 166.767 / 171.909 | `sim fader 1 up start` / `at full` | kind 0, index 0x00, pseq 2–28 (27), seq 8037 … 8186, f16075 … f16373, 168.363–169.892: `00 00 00 00`, `00 00 05 00` … `00 00 FF FF` | +1.60 s | [13, 14, 15] at 168.425 (seq 8045), 62 ms after the first write |
| 190.863 / 195.683 | `sim fader 1 down start` / `at zero` | kind 0, index 0x00, pseq 29–60 (32), seq 9643 … 9821, f19287 … f19643, 192.439–194.257: `FC FF` … `00 00` | +1.58 s | seq 9651 (192.502) |
| 216.606 / 224.027 | `sim bump 7 press` / `release` | key 0x06: press pseq 61 (seq 11391, f22783) 218.608, release pseq 62 (seq 11614, f23229) 221.959, held 3.35 s | +2.00 s | [14, 15, 13, 22, 21] at 218.658 (seq 11397) |
| 244.019 / 250.534 | `sim master to half start` / `at half` | kind 0, index 0x30, pseq 63–89 (27), seq 13143 … 13351, f26287 … f26703, 245.638–248.016: `30 FD 00` … `30 80 7E` | +1.62 s | [14, 22] at 245.656 (seq 13147) |
| 268.447 / 272.725 | `sim master to full start` / `at full` | kind 0, index 0x30, pseq 90–118 (29), seq 14762 … 14919, f29525 … f29839, 269.912–271.568: `82 80` … `FF FF` | +1.46 s | [14] at 269.930 (seq 14766) |
| 291.759 / 296.497 | `sim bumps to zero start` / `at zero` | kind 0, index 0x31, pseq 119–153 (35), seq 16305 … 16486, f32611 … f32973, 293.090–294.912: `31 FD 00` … `31 00 00` | +1.33 s | [14] at 293.108 (seq 16309) |
| 318.000 / 322.772 | `sim bumps to full start` / `at full` | kind 0, index 0x31, pseq 154–183 (30), seq 18062 … 18220, f36125 … f36441, 319.401–320.976: `02 00` … `FF FF` | +1.40 s | [14] at 319.417 (seq 18066) |
| 343.253 | `sim blackout on` | key 0x34: pseq 184 / 185 (seq 19740 / 19745; f39481 / f39491), 344.615 / 344.661 | +1.36 s | [22] at 344.632 (seq 19744), **between press and release** |
| 361.247 | `sim blackout off` | key 0x34: pseq 186 / 187 (f41767 / f41785), 362.361 / 362.454 | +1.11 s | [22] at 362.378 (seq 20887) |
| 381.097 | `sim solo on` | key 0x33: pseq 188 / 189 (f44329 / f44347), 382.252 / 382.346 | +1.15 s | [22] at 382.270 (seq 22168) |
| 397.997 | `sim solo off` | key 0x33: pseq 190 / 191 (f46527 / f46537), 399.301 / 399.348 | +1.30 s | [22] at 399.318 (seq 23267) |
| 421.906 / 429.504 | `sim mems and memory bump 1 press` / `release` | key 0x39: pseq 192 / 193 (f49629 / f49639) 423.374 / 423.436; key 0x00: pseq 194 / 195 (f49777 / f50225) 424.400 / 427.755, held 3.36 s | +1.47 s | [23, 21, 22] at 423.405 (seq 24818); [14, 22, 15] at 424.417 (seq 24892) |
| 454.155 | `sim channel mode again` | key 0x36: pseq 196 / 197 (f53833 / f53851), 455.685 / 455.778 | +1.53 s | [23, 22] at 455.703 (seq 26920) |
| 477.701 / 487.701 | `sim go stack unlit` / `go done` | key 0x31: pseq 198 / 199 (seq 28407 / 28412; f56815 / f56825), 478.819 / 478.867 | +1.12 s | [3, 22] at 478.839 (seq 28411) |
| 518.171 / 524.078 | `h81 sim fader 1 full` / `at full` | kind 0, index 0x00, pseq 200–231 (32), seq 31162 … 31344, f62325 … f62689, 519.606–521.468: `05 00` … `FF FF` | +1.43 s | seq 31170 (519.671) |
| 542.784 / 548.597 | `h81 physical fader 1 to half` | **none** (physical fader) | | FaderInfo record 1 FF → 0x0D … 0x82 (seq 32850 … 32999) |
| 623.885 / 631.638 | `h81 physical fader 1 full then zero` | **none** | | record 1 → FF → 00 (seq 38166 … 38409) |
| 648.451 / 657.766 | `h81 sim fader 1 to zero` | kind 0, index 0x00, pseq 232–255, 0–19 (44), seq 39727 … 39973, f79455 … f79947, 650.294–652.893: `FC FF` … `00 00` | +1.84 s | seq 39735 (650.359) |
| 689.639 / 704.200 | `h81 physical 5 half, sim 5 from zero` / `sim fader 5 at full` | physical fader 5 first (693.0–694.8, no write); then kind 0, **index 0x04**, pseq 20–73 (54), seq 42954 … 43276, f85909 … f86553, 698.526–701.844: `04 02 00` … `04 FF FF` | +8.89 s | seq 42962 (698.592) |
| 728.115 / 738.974 | `h81 cleanup` / `done` | kind 0, index 0x04, pseq 74–112 (39), seq 45094 … 45313, f90189 … f90627, 730.134–732.368: `FF FF` … `00 00`; then the physical fader 5 (734.8–736.2, no write) | +2.02 s | [15] at 730.198 (seq 45102) |
| 760.115 | `sim stack on` | key **0x3B**: pseq 113 / 114 (seq 47229 / 47233; f94459 / f94467), 761.508 / 761.554 | +1.39 s | [17, 21, 22] at 761.572 (seq 47237), 18 ms after the release |
| 775.249 | `sim stack off` | key 0x3B: pseq 115 / 116 (f96383 / f96393), 776.367 / 776.412 | +1.12 s | [21] at 776.384 (seq 48195) |
| 793.150 | `sim ind 1 on off` | key **0x47**: pseq 117 / 118 (f98715 / f98733) 794.415 / 794.508; pseq 119 / 120 (f99345 / f99371) 799.234 / 799.375 | +1.26 s | [12, 22, 15] at 794.432 (seq 49361) and 799.252 (seq 49676) |
| 806.118 | `sim ind 2 on off` | key **0x48**: pseq 121 / 122 (f100347 / f100365) 806.941 / 807.035; pseq 123 / 124 (f100917 / f100935) 811.277 / 811.369 | +0.82 s | [12, 13, 15, 22] at 806.959 (seq 50177); [12, 22] at 811.294 (seq 50462) |
| 828.103 | `sim bank 25-48 and back` | key **0x37**: pseq 125 / 126 (f103233 / f103243) 829.200 / 829.247; key 0x36: pseq 127 / 128 (f103955 / f103973) 834.716 / 834.810 | +1.10 s | [23, 21, 22] at 829.219 (seq 51620); [23, 22, 21] at 834.732 (seq 51981) |
| 874.019 / 887.253 | `console edit mem select 1 clear` / `left` | **none** (console keys) | | **System (5, 5, 0)** listed at 878.269 (seq 54790), section H83 |
| 932.191 / 947.631 | `console page 12 edit mem select 1 clear` / `left page 12` | **none** (console keys) | | page 12 at 934.414 (seq 58404); **System (5, 5, 0)** at 939.285 (seq 58731) |
| 965.280 | `console page 1` | **none** | | page 1 at 966.801 (seq 60512) |
| (unlogged, 11.3 s after the wait, before the answer at 995.276) | — | key 0x36: pseq 129 / 130 (seq 62112, f124225 / f124243), 991.624 / 991.717 | | [23, 22] at 991.644 (seq 62116): channel mode again ("Unlogged actions") |
| 1004.871 | `channel 7 label to Caj with caron` | ChannelText pseq 131 (seq 63421 / 63422, f126841 / f126843) at 1011.931: "Čaj" | +7.06 s | **no list** up to S1's end (1031.5) |
| 1027.669 / 1034.918 | `smartsoft close #1 start` / `closed` | none | | S1 ends at 1031.501 |
| 1046.835 | `smartsoft start #2` | System pair pseq 0, 1 (seq 64762, 64845) | +1.33 s | S2's connect phase |
| 1099.874 | answer: label after restart = "Čaj" | | | matches the download (seq 65093) |
| 1105.318 | `channel 7 label restored` | ChannelText pseq 2 (seq 68752 / 68753, f137503 / f137505) at 1107.821 | +2.50 s | **no list** up to S2's end (1128.3) |
| 1125.888 / 1130.283 | `smartsoft close #2 start` / `closed` | none | | S2 ends at 1128.263 |
| 1143.613 | `smartsoft start #3` | System pair pseq 0, 1 (seq 70149, 70232) | +1.41 s | S3's connect phase |
| 1192.869 | answer: label after second restart = "-" | | | matches the download (seq 70480: the label reads empty) |
| 1198.023 / 1213.026 | `simulator opened again` / 15 s untouched | **none** | | none |
| 1236.248 / 1245.454 | `h85 fader 5 half, master half` / `set` | **none**: the fader 5 and the MASTER moved on the console (section H85) | | record 5 → 0x7C (seq 76489), MASTER → 0x86 (seq 76687) |
| 1275.195 | `h85 rec mem bump 9` | key 0x41: pseq 2 / 3 (seq 78966, f157933 / f157951) 1278.642 / 1278.736; key 0x08: pseq 4 / 5 (seq 79052, f158105 / f158117) 1279.867 / 1279.945 | +3.45 s | [21, 22] at 1278.661 (seq 78970); **Memory (0, 0, 8) at 1279.962 (seq 79062)** |
| (unlogged, 6.5 s after the wait, before the answer at 1305.492) | — | key 0x39: pseq 6 / 7 (f160281 / f160307) 1296.710 / 1296.865; key 0x36: pseq 8 / 9 (f160517 / f160535) 1298.424 / 1298.517 | | memory mode and back: the owner looked at memory 9's key |
| 1324.093 / 1335.006 | `h85 reset` / `done` | kind 0, index 0x30, pseq 10–26 (17), seq 82061 … 82180, f164123 … f164361, 1326.412–1327.660: `30 FD 00` … `30 FF FF`; then the physical fader 5 to zero (1330.3–1330.9, no write) | +2.32 s | [14] at 1326.429 (seq 82065) |
| 1352.491 | `h86 delete 9 by rec mem` | key 0x41: pseq 27 / 28 (f167879 / f167897) 1354.587 / 1354.680; key 0x08: pseq 29 / 30 (f168125 / f168137) 1356.428 / 1356.506 | +2.10 s | [21, 22] at 1354.607 (seq 83943); **Memory (0, 0, 8) at 1356.522 (seq 84072)** |
| (unlogged, 4.2 s after the wait, before the answer at 1374.427) | — | key 0x39: pseq 31 / 32 (f170095 / f170105) 1371.671 / 1371.733; key 0x36: pseq 33 / 34 (f170239 / f170249) 1372.680 / 1372.728 | | memory mode and back: memory 9's key checked |
| 1389.325 | answer: mem edit tab decision = y | | | |
| 1402.903 / 1417.911 | `b7 record memory 9` | **none**: fader 5 up and REC MEM + bump 9 **on the console** (section B7) | | Memory (0, 0, 8) at 1408.731 (seq 87480) |
| 1475.804 / 1490.812 | `b7 mem edit tab cell change` | **Memory (0) pseq 35** (seq 92052 / 92053, f184103 / f184105) at 1479.485 | +3.68 s | **no list** until the owner's next console key (1521.016, 41.5 s later) |
| 1564.608 | answer: b7 console reaction | | | the owner's memory-9 check on the console, 1521.0–1528.9 ("Unlogged actions") |
| 1579.415 / 1594.418 | `b7 delete 9` | **none**: REC MEM + bump 9 **on the console** | | Memory (0, 0, 8) at 1581.607 (seq 98688) |
| 1601.192 | answer: b7 memory 9 empty = y | | | matches the data |
| 1668.148 / 1672.969 | `smartsoft close #3 start` / `closed` | none | | last transfer 1671.072 (f208876) |

Totals: kind 0 = 27 + 32 + 27 + 29 + 35 + 30 + 32 + 44 + 54 + 39 + 17 = 366; key pairs = 26; with the three System pairs, the
two ChannelText writes and the Memory write, 427.

### Unlogged actions

| t (s) | Seq | Data | Reading |
|---|---|---|---|
| 991.624 | 62112 … 62126 | Key 0x36 (FADERS 1-24) from the PC; ConsoleStatus `01 …` → `00 …` | After "console page 1" (hold MEMS, bump 1) the console was in memory mode (seq 60516); the owner returned to channel mode with the Simulator before the label question (INFERRED from the timing) |
| 1236.248–1243.168 | 76367 … 76687 | FaderInfo record 5 0x0F … 0x7C (MASTER 0xFF), then **body[194] (MASTER) 0xF8 … 0x86 with no write** (S3's outbound counter: pseq 0x01 at 1146.283, 0x02 at 1278.642) | The step asked for the Simulator's MASTER; the MASTER that moved was the console's own (INFERRED: the only other source; the first Simulator MASTER event of S3, at 1326.412, carries previous 0x00, so SmartSoft had not moved its MASTER in S3 before). The owner's clarification says he moved the Simulator's MASTER: the data contradict that for the move to half |
| 1296.710–1298.517 | 80140 … 80276 | Keys 0x39 and 0x36 from the PC: memory mode, LED block 9 `46 00 00 46 00 00` (recorded), then channel mode | The owner checked memory 9's key before "memory 9 recorded = y" (as in E9) |
| 1371.671–1372.728 | 85047 … 85133 | Keys 0x39 and 0x36 from the PC: memory mode, LED block 9 `0A 00 00 0A 00 00` (empty), then channel mode | The check before "memory 9 empty again = y" |
| 1405.049–1408.731 | 87204 … 87495 | Physical fader 5 up to 0x7F; LCD "Record Memory Pg:1" (seq 87401, f174802) and LED block 66 lit **with no write** (pseq 0x22 at 1372.728, 0x23 at 1479.485); Memory (0, 0, 8) listed | The B7 recording used the console's REC MEM and bump 9, not the Simulator's (INFERRED from the absence of writes) |
| 1448.404–1449.123 | 90034 … 90096 | Record 5 0x7A … 0x00 | The owner pulled fader 5 to zero after the recording, his habit (statements) |
| 1521.016–1528.954 | 94722 … 95297 | ConsoleStatus `01 …` (memory mode) with no write; FaderInfo record 9 (memory fader 9) 0x14 → 0xFF with ChannelLevels[5] / DMX 5 → **0xFF** (seq 94919 / 94923); ConsoleStatus `00 …` (seq 95010) and back `01 …` (seq 95177); record 9 → 0 | The owner's playback check of memory 9 on the console ("the channel 5 is stored there at 100%", log 1564.608): MEMS, memory fader 9 up, FADERS 1-24, MEMS, fader 9 down (INFERRED order from the data) |
| 1580.568–1581.747 | 98616 … 98700 | LCD "Record Memory Pg:1" (seq 98620, f197240) with no write; Memory (0, 0, 8) listed empty | The B7 deletion used the console's REC MEM and bump 9 |

## ConsoleEvent kind 0: the faders (F132 reproduced)

**Layout (OBSERVED in both sessions, 366 of 366 here):** header `01 00 08 00 [w2 … w5 stale]`, payload `[pseq] 04 00 14 00
[index] [value] [previous]` (len 4, w1 = 8). Example, the first event of E9b: seq 8037 / 8038 (f16073 / f16075) `01 00 08 00 00
00 00 00 00 00 00 00` + `02 04 00 14 00 00 00 00` (value 0x00: the drag's first sample at the bottom).

**Indices and what they moved:**

| Index | Simulator control (log) | The console's object that followed | Evidence (E9b) | E9 |
|---|---|---|---|---|
| 0x00 | channel fader 1, bank 1-24 | FaderInfo record 1 byte 0, ChannelLevels[1], OutputLevels DMX 1, LED block 1 G, LCD cell body[3] | seq 8057 … 8195, 9657 … 9815, 31176 … 31353, 39741 … 39930 | same (seq 9672 … 9834) |
| **0x04** | **channel fader 5** (`h81 … sim 5 from zero`, `h81 cleanup`) | FaderInfo record 5 byte 0 (body[17]), ChannelLevels[5], DMX 5, LED block 5 G, LCD cell body[7] | seq 42968 … 43271, 45108 … 45312 | not seen |
| 0x30 (48) | MASTER | FaderInfo body[194]; LED block 53 blinking `37 37 FF 0A 0A 0A` below full | seq 13153 … 14888, 82071 … 82179 | same |
| 0x31 (49) | BUMPS | FaderInfo body[193] | seq 16315 … 16458, 18072 … 18204 | same |
| 0x06 | channel fader 7 | — | not used in E9b | inside EDIT MEM only |

- Indices 0x00, 0x30 and 0x31 are now seen in two sessions with named, timed steps; 0x06 (E9) and 0x04 (E9b) once each.
  "Channel fader k = index k − 1" now rests on three points (0x00, 0x04, 0x06): still HYPOTHESIS, and no other index may be
  derived from it.
- **Level scale (reproduced):** the value byte is the console's level byte, unconverted: FaderInfo record 1 took 0x0F, 0x37,
  0x75, 0xCC and 0xFF (seq 8057, 8086, 8115, 8150, 8185 = the values of pseq 5, 10, 15, 21, 25); the MASTER's "about half" was
  0x80 (pseq 89, body[194] = 0x80 at seq 13359).
- **The previous byte (reproduced, no exception this time):** the first event of each index in each SmartSoft session carries
  previous 0x00 (5 of 5: S1 `00 00 00 00`, `30 FD 00`, `31 FD 00`, `04 02 00`; S3 `30 FD 00` at seq 82062, while the
  console's MASTER stood at 0x86), and every later event carries the value of the preceding event of the same index on the
  wire (361 of 361 within a session; E9: 327 of 328, its one exception stays INFERRED as SmartSoft's coalescing). The console
  applied first events whose previous byte did not match its level (MASTER and BUMPS at 0xFF, MASTER at 0x86, fader 5 at 0x84):
  the previous byte is no pick-up condition; whether the console reads it at all is UNKNOWN.
- **Pacing (reproduced):** one write per loop iteration, 45.55–185.65 ms apart within a burst (median 61.81); 65 of 366 events
  repeat their predecessor (`FF FF`, `00 00` at the end stops), and SmartSoft sent them anyway.

## ConsoleEvent kind 1: the keys (F133 reproduced, four new codes)

**Layout (OBSERVED in both sessions, 52 of 52 here):** header `01 00 07 00 [stale]`, payload `[pseq] 03 00 14 01 [code]
[state]`, state 0x01 on press and 0x00 on release, as **two separate writes** (a click: 46–155 ms apart; held: 3.35 s bump 7,
3.36 s memory bump 1). Example: STACK press seq 47229 / 47230 (f94457 / f94459) `01 00 07 00 00 00 00 00 00 00 00 00` + `71 03
00 14 01 3B 01`; release seq 47233 / 47234 (f94467) `72 03 00 14 01 3B 00`.

| Code | Key (Simulator) | Pairs | First press in E9b (header seq, payload frame) | LED block that changed | E9 |
|---|---|---|---|---|---|
| 0x00 | bump 1 (memory bump 1 in memory mode) | 1 | `sim mems and memory bump 1 press` (seq 24888, f49777) | 1 (`FF 00 00 FF 00 00` while held) | reproduced |
| 0x06 | bump 7 | 1 | `sim bump 7 press` (seq 11391, f22783) | 7 | reproduced |
| 0x08 | bump 9 (in REC MEM) | 2 | `h85 rec mem bump 9` (seq 79052, f158105), `h86 delete 9 by rec mem` | (REC MEM target) | reproduced |
| 0x31 | GO | 1 | `sim go stack unlit` (seq 28407, f56815) | 50 (blue during the fade) | reproduced |
| 0x33 | SOLO | 2 | `sim solo on` / `off` (seq 22164, f44329; seq 23263) | 52 (`FF`×6 on) | reproduced |
| 0x34 | BLACKOUT | 2 | `sim blackout on` / `off` (seq 19740, f39481; seq 20883) | 53 (`00 00 FF 00 00 FF` on) | reproduced |
| 0x36 | FADERS 1-24 | 5 | `sim channel mode again` (seq 26916, f53833); the bank step; three unlogged | 55 | reproduced |
| 0x39 | FADERS MEMS | 3 | `sim mems …` (seq 24814, f49629); two unlogged | 58 | reproduced |
| 0x41 | REC MEM | 2 | `h85 rec mem bump 9` (seq 78966, f157933), `h86 …` | 66 (`FF 00 00 FF 00 00`) | reproduced |
| **0x37** | **FADERS 25-48** | 1 | `sim bank 25-48 and back` (seq 51616, f103233) | **56** `00 0A 00 …` → `00 FF 00 00 FF 00`; 55 → `00 0A 00 …` (seq 51634) | new |
| **0x3B** | **STACK** | 2 | `sim stack on` / `off` (seq 47229, f94459; seq 48191) | **60** `46 46 00 46 46 00` → `FF FF 00 FF FF 00` (seq 47249) and back (seq 48208) | new |
| **0x47** | **IND 1** | 2 | `sim ind 1 on off` (seq 49357, f98715; off seq 49672) | **72** `00 0A 00 …` → `00 FF 00 00 FF 00` (seq 49371) and back (seq 49684) | new |
| **0x48** | **IND 2** | 2 | `sim ind 2 on off` (seq 50173, f100347; off seq 50458) | **73** → `00 FF 00 00 FF 00` (seq 50195) and back (seq 50472) | new |
| 0x09, 0x38, 0x3E, 0x42 | bump 10, CLEAR, COPY, EDIT MEM | 0 | not pressed from the PC in E9b | | E9 only |

- **Code = LED block number − 1 holds for all 17 codes seen in E9 and E9b.** For STACK (60), IND 1 (72) and IND 2 (73) the
  blocks were known before E9b (H72, F115, F117) and the hypothesis predicted 0x3B, 0x47 and 0x48: three correct
  predictions. For FADERS 25-48 the block was not known; E9b shows block 56 lit with the key (the first identification of
  block 56). H82 stays HYPOTHESIS for any key not seen, and **no code may be derived from it.**
- **The console acts on the press:** the first list came between press and release in 23 of the 26 pairs (BLACKOUT on 344.632
  between 344.615 and 344.661, SOLO, MEMS, 1-24, GO 478.839, STACK off, IND 1, IND 2, 25-48, REC MEM, …). In three pairs it came
  16–18 ms after the release (STACK on, 761.572 after 761.554; bump 9 inside REC MEM twice, 1279.962 and 1356.522), 64–95 ms
  after the press: the timing does not decide those three. Bumps flash while held (output on press, off on release); BLACKOUT,
  SOLO, STACK, IND 1 and IND 2 toggle per click.

## The console's answer to the events (F134 reproduced)

| Control (code / index) | What the console reported (first payloads) | Same as E9? |
|---|---|---|
| Channel fader 1 (0x00) | FaderInfo record 1 byte 0, ChannelLevels[1], DMX 1, LED block 1 G **steady**, LCD cell (seq 8051 … 8195) | Yes (and as E3's physical fader 1) |
| Bump 7 (0x06), held | **The same six bytes of five objects changed to the same values as in E9** (`holddiff`: OutputLevels body[8] 00 → FF, FaderInfo body[25] 00 → FF, ChannelLevels body[7] 00 → FF, DisplayContent body[9] 20 → 86, LedStatus body[38], [41] 00 → FF; seq 11401 … 11417) and back at release (seq 11624 … 11640) | Yes, byte for byte (E9 seq 19062 … 19078; E9 had matched E3b's physical bump 7) |
| Memory bump 1 (0x00) in memory mode, held | **Byte for byte as in E9** (OutputLevels [8], [9], FaderInfo [1], [2], ChannelLevels [7], [8] 00 → FF; DisplayContent [9], [10] 20 → 86; LedStatus [1], [4] 46 → FF; seq 24896 … 24915) | Yes (E9 seq 39603 … 39622) |
| MASTER (0x30) | body[194] 0xFF → 0x80 → 0xFF; LED block 53 `37 37 FF 0A 0A 0A` from seq 13159 to seq 14888 | Yes |
| BUMPS (0x31) | body[193] 0xFF → 0x00 → 0xFF; nothing else | Yes |
| BLACKOUT (0x34) | LED block 53 `00 00 FF 00 00 FF` (seq 19750) and back (seq 20891); nothing else (all levels 0) | Yes |
| SOLO (0x33) | LED block 52 `FF`×6 (seq 22172) and back (seq 23273) | Yes |
| MEMS (0x39) / 1-24 (0x36) | ConsoleStatus body[0] 0x00 ↔ 0x01 with the memory map, blocks 55 / 58 (seq 24824 / 24832, 26924 / 26930) | Yes |
| GO (0x31), STACK unlit | List [3, 22]: SequenceMain **unchanged** (seq 28417, f56834 = the download's seq 320), block 50 blue (seq 28421); PlaybackInfo record 5 `02 02 F5 0A` … `02 02 00 FF` → `02 02 FF 00` (seq 28428 … 28869), **bytes 0–1 stayed `02 02`**; block 50 off (seq 28873); no level object changed | Yes (GO with STACK unlit for the third time: E3b from the panel, E9 and E9b from the PC; F101, H76) |
| **Channel fader 5 (0x04)** | FaderInfo record 5, ChannelLevels[5], DMX 5, LED block 5 G, LCD cell body[7] | new |
| **STACK (0x3B)** | List [17, 21, 22]: PlaybackInfo record 5 bytes 0–1 **`02 02` → `00 00`** (seq 47241, f94482; they stayed `00 00` after STACK off), the LCD stack view "Live:14.0 Next: 1.0" / "<6>Mem:01/01 Mem:01/01" (seq 47245, f94490; as E8b's STACK, F130), block 60 lit (seq 47249); off: the channel view (seq 48201), block 60 back (seq 48208) | new; `00` with "Next: 1.0" fits H76 |
| **IND 1 (0x47), IND 2 (0x48)** | Independents record byte 0 (body[1] for IND 1, seq 49365; body[43] for IND 2, seq 50181), ChannelLevels body[49] / body[50] 00 ↔ FF, **DMX 47 = 0xFF with IND 2 only** (seq 50187), blocks 72 / 73: the same objects as the console's own IND keys in E8 and E8b (F115) | new |
| **FADERS 25-48 (0x37)** | ConsoleStatus body[4] 0 → 1 (`00 00 00 00 01 01 00`, seq 51626), LCD rows "25" / "37" (seq 51630), blocks 55 dim / 56 lit (seq 51634); back with 0x36 (seq 51985 … 51995) | new; as the panel's bank key (F98) |

- **No acknowledgement and no echo** (reproduced): the reply to the poll after a write is an ordinary idle reply or list;
  ConsoleEvent (20) was never listed or fetched; nothing names the PC as the source of a change.
- **The physical faders did not follow** (not motorised): FaderInfo record 1 reported the PC's 0xFF from 521.5 s to 544.7 s
  with the physical fader 1 at zero and LED block 1 steady `00 FF 00 00 FF 00` (seq 31328 … 32846), as in E9.

## H81: the PC's fader against the physical fader

All in channel mode, bank 1-24, MASTER and BUMPS at 0xFF unless stated; FaderInfo record N byte 0 is the level the console
uses (post-MASTER, F96).

| Case | Before | What happened | Evidence |
|---|---|---|---|
| Physical after PC (fader 1) | PC's fader 1 at 0xFF (pseq 231, 521.468), physical fader 1 at zero | The owner moved the physical fader up: at the first registered movement record 1 dropped **0xFF → 0x0D** (DMX 1 = 0x09) and then followed the hand to 0x82 ("about half"), with no write. **The physical fader took over at once, from its own position, without waiting to reach the PC's level** | seq 32846 (DMX 1 0x09, 544.756), 32850 (record 1 0x0D, f65700, 544.800) … 32999 (0x82, 546.512) |
| PC after physical (fader 1) | Physical fader 1 back at zero (record 1 0x00, seq 38409); SmartSoft's fader 1 still at its 0xFF | The Simulator drag down started at `FC FF`: the first samples after it read **0xF2** (DMX 1, seq 39741, 650.423) and **0xEC** (record 1, seq 39747), the values of the third and fourth events: the level jumped from 0x00 to the PC's value and followed the PC down to 0 | seq 39727 … 39973 (writes), record 1 0xEC at seq 39747 (f79494, 650.483) |
| PC after physical (fader 5) | Physical fader 5 at 0x84 (seq 42704, 694.740) | The Simulator's fader 5 from zero: record 5 **0x84 → 0x0D** at once (DMX 5 0x0A at seq 42968), then followed the PC up to 0xFF. Owner: "h81 pickup = at once" | seq 42954 (first write `04 02 00`), 42974 (record 5 0x0D, f85948, 698.716) … 43236 (0xFF) |
| Physical after PC (fader 5) | PC's fader 5 brought to 0x00 (record 5 0x00, seq 45294, 732.155); physical fader 5 still at about half | The owner pulled the physical fader 5 down: record 5 **0x00 → 0x83** at the first registered movement, then followed it down to 0 | seq 45477 (record 5 0x83, f90954, 734.839) … 45595 (0x00, 736.190) |
| PC after physical (MASTER) | MASTER at 0x86 from the console's own MASTER (seq 76687, 1243.168) | The Simulator's MASTER drag (`30 FD 00`, previous 0x00): body[194] **0x86 → 0xF3** at once, then 0xD9, 0xE9 and 0xFF following the PC; LED block 53 steady again | seq 82062 (first write), 82071 (0xF3, f164142, 1326.494) … 82161 (0xFF, f164322, 1327.445), 82179 (block 53) |

- **Reading (OBSERVED, one session): in channel mode the last mover wins, in both directions, with no pick-up.** A PC event
  sets the control to its value at once, whatever the physical fader's position; a physical fader takes over at once, at its
  own position, as soon as the console registers a movement of it. In none of these cases did the bump LED blink (no
  unmatched-fader state in channel mode). The owner's answers agree ("instantly go to the value of the physical fader", "at
  once"; statements). E9's other cases (the pick-up inside EDIT MEM and the blink after leaving the edit, F135) were not
  repeated.
- **SmartSoft's Simulator does not follow the console** (INFERRED, three cases): its drags started from its own last position
  (fader 1 from 0xFF at 650.3 s while the console was at 0x00; fader 5 from 0x00 at 698.5 s while the console was at 0x84; the
  MASTER from about full at 1326.4 s while the console was at 0x86), so the first event of a drag can make the output jump.
- **E9's unexplained drop** (channel 1 FF → 00 at 279.5 s with no write, F135; the owner touched nothing: log 08:36:44.975 "e9
  drop at 279 s = n"): a takeover by the physical fader at its zero position would produce exactly that drop (record 1 to 0x00
  in one step, as at 544.8 s here from 0xFF to 0x0D). Whether the console registered a movement that nobody made is UNKNOWN
  (H88). Nothing of the kind happened in E9b: record 1 stayed at the PC's 0xFF for 23 s (521.5–544.7) until the owner moved
  the fader.

### The MASTER at the end of E9b (the owner's notes)

The owner noticed after the session that the MASTER fader stood at about 50 %, and thinks he moved the Simulator's MASTER back
to full in H85 (statements). What the capture shows:

1. **The move to "half" in H85 was the console's own MASTER, not a PC event** (INFERRED): body[194] went 0xFF → 0xF8 → … →
   0x86 at 1241.641–1243.168 (seq 76558 … 76687) with no write at all (S3's outbound counter goes from pseq 0x01 at 1146.283 to
   0x02 at 1278.642), and the first Simulator MASTER event of S3 (1326.412) carries previous 0x00, so SmartSoft had not moved
   its MASTER in S3 before. LED block 53 blinked from seq 76562.
2. **A PC event set the console's MASTER to full:** the "h85 reset" burst (index 0x30, pseq 10–26, seq 82061 … 82180,
   1326.412–1327.660) took body[194] from 0x86 to 0xF3 at once and to **0xFF at 1327.445 (seq 82161)**; LED block 53 steady
   `0A`×6 (seq 82179). Channel 5 rose with it (record 5 0x41 → 0x7C) until the owner pulled fader 5 down (1330.3 s).
3. **Nothing brought it back to about 0x80 within the capture:** body[194] = 0xFF in all 29 FaderInfo payloads from seq 82161
   (1327.445) to the last FaderInfo of the capture (seq 95285, 1528.812), and no FaderInfo was listed after that up to the last
   frame (1671.072). There is no MASTER value near 0x80 late in the capture.
4. Reading: the physical MASTER stayed where the owner had left it in H85 (about half, 0x86), because the faders are not
   motorised, while the console's MASTER level was the PC's 0xFF from 1327.4 s to the end (OBSERVED for the level; the physical
   position INFERRED). The "50 %" the owner saw is the fader's position. By the takeover behaviour above (OBSERVED for faders 1
   and 5, not for the MASTER in that direction), the console will take the physical position, about half, as soon as the
   physical MASTER is moved; what the console does with the physical position at a power-on is UNKNOWN. No PC event and no
   "re-registration" of the panel lowered the MASTER during the capture; it affected live levels only, not show data. After
   the session the owner reported "master is on 100% already" (statements, about 09:35): the physical fader and the console's
   level agree again (statement; not in any capture).

## H82: keys not yet seen from the PC

See the key table: STACK = 0x3B (block 60), IND 1 = 0x47 (block 72), IND 2 = 0x48 (block 73), FADERS 25-48 = 0x37 (block 56,
new). **Supports H82** (17 of 17 codes = block − 1; three predicted correctly). The script did not include a bump in bank 25-48,
so that part of H82's test is open. IND 2 drove DMX 47 only, as the patch says (channel 50 → DMX 47, F115); the rig on DMX 5–16
showed nothing for the IND keys.

## H83: the console's own EDIT MEM

The owner pressed EDIT MEM, the bump under memory 1, waited and pressed CLEAR **on the console** (no write in 834.8–991.6 s:
pseq 0x80 at 834.810, 0x81 at 991.624), on page 1 and then on page 12:

| | Page 1 (874.019 `console edit mem select 1 clear`) | Page 12 (932.191 `console page 12 edit mem select 1 clear`) |
|---|---|---|
| Page change | — | Hold MEMS + bump 12: ConsoleStatus `01 00 00 …` (seq 58374) → **`01 00 0B 00 00 01 00`** (seq 58408, f116816), FaderInfo byte 3 = 0x0B in records 1–24 (seq 58412), LCD "Memory page:12" / "Bump 1-12 to change" (seq 58420); no System record listed (F113) |
| EDIT MEM | LCD "Edit Memory" / "Select Bump or Stack" (seq 54604, f109208); recorded memories `C8 00 00 0A 00 00` blinking, empty `46 00 00 46 00 00`, block 57 blinking, block 67 `FF 37 37 0A 0A 0A` (seq 54611) | The same screen (seq 58578, f117156) with page 12's map (memories 1–7 recorded) |
| Bump 1 | **List [23, 39:5/5/0, 23, 22, 21]** (seq 54790, f109580); **System (39) (5, 5, 0) body `05 05 00`** (seq 54798, f109596); ConsoleStatus re-listed unchanged (channel mode already); LED blocks 7 and 8 G = 0xFF (memory 1's content, `GROUP 1` channels 7 and 8); LCD "Edit:" / "01/01" (seq 54810, f109620) | **List [23, 39:5/5/0, 23, 21, 22]** (seq 58731, f117462); ConsoleStatus **`00 00 0B 00 00 01 00`** (channel mode for the edit, seq 58735); **System (39) (5, 5, 0) body `05 05 00`** (seq 58739, f117478); LCD "Edit:" / **"12/01"** (seq 58747, f117494) |
| CLEAR | List [23, 21, 22] (seq 55241): the channel view, block 57 steady, block 67 off (seq 55249, 55253); **no Memory listed** | ConsoleStatus `01 00 0B …` (memory mode again, seq 59070); **no Memory listed** |

- **The console's own EDIT MEM selection lists System (39) exactly as the PC's did in E9** (E9: (5, 5, 0) at seq 67101 / 67109
  for memory 1 from PC keys). **Supports H83's "whatever the key's source"**, for memory 1.
- **The page is not in the record:** page 12, memory 1 gave the same (5, 5, 0) and the same body as page 1, memory 1; the LCD
  ("12/01") and ConsoleStatus body[2] = 0x0B carried the page. a = b = 5 stay UNKNOWN in meaning; c = slot − 1 rests on E9's
  PC-key records (6, 8, 9 for memories 7, 9, 10).
- **The c ≠ 0 case from the console's own key is still untested**: the script selected memory 1 on both pages, so c = 0 and
  every record of E9b had c = 0. **For SC-1 this means (INFERRED, not observed):** an owner's EDIT MEM selection of memory 1 on
  any page lists System (5, 5, 0), which a read-only session of ours fetches (id 39 and body length 3 are inside SC-1 as built,
  F137); a selection of any other memory would, by E9's c = slot − 1 and E9b's same-record behaviour, list c ≠ 0 and end our
  session with its fault (a safe stop). One console-side selection of memory 2 or higher decides it (next capture, B).

## Whole-object writes

### ChannelText (9): the label of channel 7, and H84

| Write | Seq / frame | Payload (43 bytes) | The console's next download |
|---|---|---|---|
| (E9, pseq 28, 1005.427: the "restore" with residue) | E9 seq 60604 / 60605, E9 f121209 | `1C 27 00 09 06 03 06 00 00 00 61 00 6A` + 30 × `00` | **E9b S1, after a power cycle: `06 03 06 00 00 00 61 00 6A` + zeros (seq 424, f848)**: the residue was stored and returned |
| S1 pseq 131, 1011.931: "Čaj" | seq 63421 / 63422, f126841 / f126843 | `83 27 00 09 06 03 06 01 0C 00 61 00 6A` + 30 × `00` | **S2: `06 03 06 01 0C 00 61 00 6A` + zeros (seq 65093, f130186)**, byte-identical to the written body; SmartSoft showed "Čaj" (owner, 1099.874) |
| S2 pseq 2, 1107.821: the "restore" | seq 68752 / 68753, f137503 / f137505 | `02 27 00 09 06 03 06 00 00 00 61 00 6A` + 30 × `00` | **S3: `06 03 06 00 00 00 61 00 6A` + zeros (seq 70480, f140960)**, byte-identical; SmartSoft showed no label (owner, 1192.869) |

- **The written bodies are byte-identical to E9's** (after the 4-byte payload head): "Čaj" = E9 pseq 27, the restore = E9 pseq
  28; SmartSoft's restore again zeroed only the first code unit. E9's extra write of the unchanged label 2 s before the rename
  did not recur (one write per rename here); what triggered it in E9 is UNKNOWN.
- **H84 CONFIRMED (OBSERVED, three round trips):** the console stores the 39-byte ChannelText body as written and returns it
  verbatim in the next download, across SmartSoft restarts and across a power cycle (E9's residue in E9b's first download).
  Nothing was listed after either write (no ChannelText, no DisplayContent change), as in E9.
- **H40:** SmartSoft writes U+010C as `01 0C` (UTF-16BE, reading A; two sessions) and decodes the returned bytes as "Čaj" (the
  owner's reading of SmartSoft's display, a statement); reading B would give a pad byte 0x01 and U+000C, 'a', 'j'. So
  SmartSoft's codec is reading A both ways. **The console's own reading of the bytes is still untested:** it stores them
  verbatim and the 1248 shows labels nowhere (owner); `SHOW08.ASC`, saved with the residue on the console, has an empty
  `$CHANNEL 7` text (`Text ` / `$$Text 1 `), and `SHOW09.ASC`, saved after E9b with the same residue on the console (S3's
  download, seq 70480), is byte-identical to `SHOW07.ASC`, saved when the block was all zeros: the export reads the block up
  to its first zero unit, which fits both readings (the residue's first unit is zero under both).
- **For a writer:** the exact old bytes of a label block are not in the SD file (the residue does not appear there); only a
  download after SmartSoft's System `07 00 00` delivers them.

### Memory (0): the Mem Edit tab's cell change (B7)

The owner chose to run B7 (log 1389.325). Memory 9 of page 1 was recorded on the console with fader 5 at 0x7F (Memory (0, 0, 8)
listed at seq 87480 / 87484, f174960 / f174968: `00 08 01 60` + channel 5 = **0x7F** + zeros + an empty label block), fader 5 was
pulled back to zero (1449.1 s, the owner's habit), and in SmartSoft's **Mem Edit tab** the owner set channel 5 of memory 9 to
100 %:

| | Bytes |
|---|---|
| Header seq 92052 (f184103, 1479.485) | `01 00 5E 00 00 00 00 00 00 00 00 00` (w1 = 94; w2..w5 = the idle reply's zeros, F52) |
| Payload seq 92053 (f184105) | `23 5A 00 00` (pseq 0x23, len 90, id 0 = Memory) + body: **`00 08 01 30`** + **`00 00 00 00 FF`** + 43 × `00` (levels of channels 1–48: channel 5 = 0xFF) + **`03 06`** + 36 × `00` (the empty label block) |

- **A Memory (0) whole-object write exists and SmartSoft's Mem Edit tab uses it** (S11.15 row 6: "GUI edits are whole-object
  writes … Memory(0, page, slot)", INFERRED, now OBSERVED once). The qualifiers are in the body: body[0] = page − 1 (0), body[1]
  = slot − 1 (8), as in the console's own Memory bodies (88 of 88 in E2, E9 and E9b have body[0..1] = the fetch's a, b).
- **The written body is not the console's layout:** 90 bytes, body[3] = **0x30** and **48** level bytes, against the console's
  138 bytes, body[3] = **0x60** and **96** level bytes (88 of 88 console Memory bodies; channels 49–96 always zero). body[3] is
  the number of level bytes in both (INFERRED from the two lengths). SmartSoft's 1248 engine keeps 48 channels per memory
  (INFERRED).
- **No answer:** nothing was listed after the write (the next list came at 1521.016, 41.5 s later, from the owner's console
  key), as for ChannelText.
- **The console applied it:** in the owner's check, memory fader 9 raised to full in memory mode drove ChannelLevels[5] and DMX 5
  to **0xFF** (seq 94919 / 94923, f189838 / f189846, with FaderInfo record 9 = `FF FF 00 00` at seq 94927 and the MASTER at
  0xFF); with the recorded 0x7F they would have stopped at 0x7F. The owner: "the channel 5 is stored there at 100% not about
  50%" (log 1564.608). OBSERVED.
- **What the console stored for channels 49–96 is UNKNOWN** (the write carried 48 levels; the memory was deleted before any
  download could show the stored body).
- Memory 9 was then deleted on the console (REC MEM + bump 9 with nothing up): Memory (0, 0, 8) listed with **`00 08 00 60`** +
  96 zeros (seq 98688 / 98692, f197376 / f197384).

### Playback tab

Not used in E9b.

## The record functions (H85, H86, B7)

| Step | Keys | What the console showed and listed | Reading |
|---|---|---|---|
| H85 set (1236.2–1243.2) | none (console) | Fader 5 up: record 5, ChannelLevels[5], DMX 5 = **0x7C** with the MASTER at 0xFF (seq 76489, 76493, 76497, f152978 …); the console's MASTER down to **0x86**: record 5 and ChannelLevels[5] and DMX 5 = **0x41** (seq 76687 f153374, 76676, 76672) | 0x7C × 0x86 / 255 = 65.2: the plain product (F96; one more point for H60) |
| PC REC MEM (0x41) | pseq 2 / 3 | LCD "Record Memory Pg:1" / "Select Bump or Stack" (seq 78974, f157948); recorded memories `46 00 00 0A 00 00`, empty (9 included) `C8 00 00 0A 00 00`, blocks 57, 60, 65 blinking, 66 lit (seq 78980) | The F116 screen from a PC key (as in E9) |
| + PC bump 9 (0x08) | pseq 4 / 5 | List [0:0/8/0, 22] (seq 79062, f158124); **Memory (0, 0, 8) = `00 08 01 60` + channel 5 = 0x41** + zeros + empty label (seq 79066, f158132); the channel view (seq 79070, 79077) | **REC MEM stored the post-MASTER level 0x41, not the fader's 0x7C** |
| Proof that the fader stood at 0x7C | — | When the PC's MASTER returned to full, record 5 rose 0x41 → 0x76 → 0x6A → 0x71 → **0x7C** with body[194] 0xF3, 0xD9, 0xE9, 0xFF (seq 82071 … 82161): 0x7C × M / 255 at each step | The fader did not move between the recording and 1330 s |
| PC MEMS / 1-24 (unlogged) | pseq 6–9 | Memory map: block 9 `46 00 00 46 00 00` (recorded); block 5 `46 00 00 0A 00 00` blinking and record 5 = 0 (fader 5 up in memory mode, H79) (seq 80152, 80158) | The owner's check; H79 again |
| H86: PC REC MEM + bump 9, nothing raised | pseq 27–30 | REC MEM screen (seq 83947, 83953: block 9 now `46 00 00 0A 00 00`, recorded); list [0:0/8/0, 22, 21] (seq 84072, f168144); **Memory (0, 0, 8) = `00 08 00 60` + 96 zeros + an empty label** (seq 84076, f168152) | **REC MEM of an empty look emptied memory 9**: byte 2 went 0x01 → 0x00 |
| PC MEMS / 1-24 (unlogged) | pseq 31–34 | Memory map: block 9 **`0A 00 00 0A 00 00`** (empty) (seq 85061) | The owner: "memory 9 empty again = y" |
| B7 record, console keys | none | LCD "Record Memory Pg:1" (seq 87401), block 66 lit; Memory (0, 0, 8) = `00 08 01 60` + channel 5 = 0x7F (seq 87484) | The console lists the Memory object after a recording made **with its own keys** too |
| B7 delete, console keys | none | LCD "Record Memory Pg:1" (seq 98620); Memory (0, 0, 8) = `00 08 00 60` + zeros (seq 98692) | H86 again, from the console's keys |

- **H85 REJECTED (OBSERVED once):** REC MEM recorded channel 5 at the level the live objects reported after the MASTER (0x41),
  not the fader's level before it (0x7C). F107's "simulator reading" (pre-MASTER, INFERRED) is contradicted; what the manual says
  (F107, DOCUMENTED: REC MEM records "the current look") fits. With the MASTER at full (E9's memory 9, B7) the two cannot be told
  apart.
- **H86 CONFIRMED (OBSERVED twice, PC keys and console keys):** REC MEM + bump with nothing raised empties the memory; the
  console lists the Memory object at once with body[2] = 0x00 and all 96 levels 0 (the console's own Memory bodies otherwise
  have body[2] = 0x01: 86 of 86 in E2, E9 and E9b); its key shows empty (`0A 00 00 0A 00 00`) in memory mode. Whether the
  emptied memory is absent from the next 157-record list, like a never-recorded one, is open (H87).
- **The console lists the Memory object after any recording**, from PC keys (E9, E9b) and from its own keys (E9b, twice): in
  E9b 94–95 ms after the PC's bump press (16–17 ms after its release; E9: 18–98 ms after the storing key); the 157-record list
  never recurred.

## Side effects on the show

| Show object | Before E9b | Last state in the capture | Note |
|---|---|---|---|
| **Memory 7, page 1** | channel 7 = 0xFF, channel 15 = 0x15 (seq 232, f464; `SHOW08.ASC` `CHAN 7/100 15/8`) | unchanged | **E9's mistaken edit was still on the console during E9b** (E9b did not touch it); the owner's answer at the start ("It was in memory 1 … not memory 7") is contradicted by the download and by SHOW08.ASC. **Restored by the owner after the session:** `SHOW09.ASC` (about 09:45) is byte-identical to `SHOW07.ASC` (statements; file hash checked) |
| Memory 1, page 1 | channels 7, 8 = 0xFF (seq 208, f416) = pre-E9 | unchanged | The owner's restore after E9 holds |
| Memory 9, page 1 | empty (not in the list) | recorded (0x41), emptied, recorded (0x7F), written (0xFF), emptied: last body `00 08 00 60` + zeros (seq 98692) | Byte 2 = 0 and zero levels; whether the console now treats it as never recorded is open (H87) |
| ChannelText 6 (channel 7's label) | `00 00 00 61 00 6A` residue from E9 (seq 424) | the same residue (S3's download, seq 70480) | "Čaj" in between; the residue reads empty |
| MASTER (live, not show data) | 0xFF | console level 0xFF (PC), physical fader at about half (INFERRED) | Section "The MASTER at the end of E9b"; at 100 % after the session (owner, about 09:35) |

No other show object changed: the three downloads are identical except channel 7's label. **After the session the show data
equal the pre-E9 show again** as far as the SD file can show: `SHOW09.ASC` = `SHOW07.ASC` byte for byte (memory 7 restored,
memory 1 as before, no memory 9 or 10). The only known remaining trace of E9 and E9b is the residue in channel 7's label block
(`00 00 00 61 00 6A`, reads empty), which the ASC file cannot show (F139, F144); only a download after SmartSoft's System `07 00
00` can.

## Reproduction of E9's facts (F131–F140)

| Fact | E9 (one session) | E9b (second session, timed) | Verdict proposed |
|---|---|---|---|
| **F131** write framing, outbound counter, no acknowledgement | 401 writes | 427 writes in three sessions: type-1 header `01 00 [w1]` + stale words (427 of 427), payload of exactly w1 bytes 0.067–0.254 ms later; w1 = 7, 8, 43 and **94** (Memory, new); pseq from 0x00 per session, +1 per data payload across ids, wrap 0xFF → 0x00 (seq 39860 → 39866), 0 gaps; next OUT 14.69–16.23 ms later; writes ≥ 45.55 ms apart; no list after an object write; ConsoleEvent never listed; USBD 0 | **Reproduced: VERIFIED** (structure and behaviour; "one write per loop iteration" stays INFERRED; w1 = 94 OBSERVED once). NOT CLEARED |
| **F132** kind 0 | indices 0x00, 0x06, 0x30, 0x31; previous rule 327 of 328 | indices 0x00, 0x30, 0x31 again (seq 8038, 13144, 16306 first events), **0x04 = channel fader 5** new; value = level byte; first event previous 0x00 per session (5 of 5), then the last wire value (361 of 361); 65 repeats sent | **Reproduced: VERIFIED** for the layout, the value scale, the previous rule and indices 0x00, 0x30, 0x31; 0x06 and 0x04 OBSERVED (one session each). NOT CLEARED |
| **F133** kind 1 | 13 codes | 9 of them again with named steps (0x00, 0x06, 0x08, 0x31, 0x33, 0x34, 0x36, 0x39, 0x41); **0x37, 0x3B, 0x47, 0x48 new**; press and release separate; the console acts on the press (23 of 26 pairs) | **Reproduced: VERIFIED** for the layout and those 9 codes; 0x09, 0x38, 0x3E, 0x42 stay OBSERVED (E9); the 4 new codes OBSERVED. NOT CLEARED |
| **F134** the console answers as for the panel | | fader 1, bump 7 and memory bump 1 (byte for byte as E9), MASTER, BUMPS, BLACKOUT, SOLO, MEMS, 1-24, GO with STACK unlit; new: fader 5, STACK, IND 1, IND 2, 25-48 | **Reproduced: VERIFIED** for the controls of both sessions; the new ones OBSERVED |
| **F135** matching with PC faders | PC events at once; EDIT MEM pick-up; post-edit blink; one drop | PC events took effect at once from the first event (fader 1 twice, fader 5, MASTER, BUMPS); **new: a physical fader takes over at once from a PC-set level** (F141); EDIT MEM cases not repeated; no drop | **VERIFIED in part** ("a PC fader event takes effect at once, whatever the console's level and the previous byte"); the EDIT MEM pick-up and blink stay OBSERVED; the drop UNKNOWN (H88) |
| **F136** record functions from the PC | REC MEM, EDIT MEM, CLEAR, COPY | REC MEM + bump 9 from the PC stored the look and the console listed Memory (0, 0, 8) at once (seq 79062), twice (H85, H86); the console's own REC MEM did the same (B7); EDIT MEM + bump 1 listed System (5, 5, 0) (from the console's keys, H83); EDIT MEM store, CLEAR, COPY from the PC not repeated | **VERIFIED in part**: "REC MEM + bump stores the live look and the console lists Memory (0, page − 1, slot − 1) by itself" and "an EDIT MEM selection of memory 1 lists System (5, 5, 0), body `05 05 00`" (E9 from PC keys, E9b from the console's keys). The rest stays OBSERVED. NOT CLEARED |
| **F137** c ≠ 0 records | four records | **none** (memory 1 only; c = 0 in 1 289 of 1 289) | **Not reproduced (not tested)**: stays OBSERVED; H83 supported for the key source |
| **F138** ChannelText write | three writes | two writes, bodies byte-identical to E9's "Čaj" and restore; the console's side: H84 CONFIRMED (F144) | **Reproduced: VERIFIED** for the write (layout and bytes). NOT CLEARED |
| **F139** E9's side effects | memory 1, 7, 9, 10; label | memory 7 still changed at capture time (seq 232 + SHOW08.ASC), memory 1 restored (seq 208), 9 and 10 empty (list), label residue stored (seq 424) | Annotate: the open owner item was answered by evidence (memory 7 held channel 7 at full during E9b) and closed after the session (2026-10-31, about 09:45: the owner restored memory 7; SHOW09.ASC = SHOW07.ASC byte for byte) |
| **F140** nothing sent by opening the Simulator | once (+ the Playback tab) | twice (134.2–168.4 s in S1, 1198.0–1278.6 s in S3) | **Reproduced: VERIFIED** for the Simulator; the Playback tab's button stays OBSERVED (not used in E9b) |

## Hypotheses touched

| H | Verdict (OBSERVED at most) | Key evidence (E9b seq / frame) |
|---|---|---|
| **H81** PC fader = a physical move ("last mover wins") | **First clause supported in both directions (channel mode)**: PC events take over at once (fader 1, fader 5, MASTER) and the physical fader takes over at once from a PC-set level, at its own position (faders 1 and 5); no bump blink. The matching-rule clause (after EDIT MEM, a mode or page change) was not tested in E9b | seq 32850, 39747, 42974, 45477, 82071 |
| **H82** code = LED block − 1 | **Supported:** 17 of 17; STACK 0x3B, IND 1 0x47, IND 2 0x48 predicted and observed; FADERS 25-48 0x37 with block 56 (new). Bank-25-48 bump untested | seq 47230, 49358, 50174, 51617 |
| **H83** System (5, 5, slot − 1) whatever the key's source | **Supported for the source:** the console's own EDIT MEM + bump 1 listed System (5, 5, 0), body `05 05 00`, on page 1 and on page 12; **the page is not in the record** (page 12 gave (5, 5, 0)). c ≠ 0 from the console's key untested (memory 1 only) | seq 54790 / 54798, 58731 / 58739 |
| **H84** ChannelText stored verbatim | **CONFIRMED (OBSERVED, three round trips, across a power cycle)** | seq 424, 63422 → 65093, 68753 → 70480 |
| **H85** REC MEM records pre-MASTER | **REJECTED (OBSERVED once):** 0x41 stored = 0x7C × 0x86 / 255, the post-MASTER level | seq 76489, 76687, 79066, 82161 |
| **H86** REC MEM of an empty look deletes | **CONFIRMED (OBSERVED twice):** Memory listed with body[2] = 0 and zero levels; key shown empty | seq 84076, 98692, 85061 |
| **H40** label byte order | SmartSoft: reading A both ways (write in two sessions; display after a restart, owner); console: stores verbatim, own reading untested | seq 63422, 65093 |
| **H72** key LEDs | Extended: blocks 60, 72, 73 tied to named PC key codes; **block 56 = FADERS 25-48** (OBSERVED once) | seq 47249, 49371, 50195, 51634 |
| H53 ConsoleStatus | Supports: body[0] (MEMS / 1-24, EDIT MEM), body[2] = page − 1 (page 12), body[4] = bank (25-48 from a PC key) | seq 24824, 58408, 51626 |
| H64 block 50 = GO | Supports (blue during the GO fade, PC key) | seq 28421, 28873 |
| H76 PlaybackInfo bytes 0–1 | Supports twice: GO with STACK unlit kept `02 02`; STACK on set record 5 to `00 00` with the LCD "Next: 1.0" (E9 had left `02 02`, and E9b's first PlaybackInfo after the power cycle still read `02 02`, seq 14) | seq 28428 … 28869; 47241, 47245 |
| **H78** blocks 25–48 colour from the power-on mode | **Contradicted (OBSERVED once; the power-on mode INFERRED):** dim red `0A 00 00 0A 00 00` in every LedStatus payload from the first (seq 18) after a power-on in which the console was in channel mode (no ConsoleStatus pending, body[0] = 0 in the download); E8b, E9 and E9b all dim red, E2–E8 dim green: the cause is UNKNOWN | `blk2548.py`; seq 10, 796 |
| H79 bump blink while unmatched | Supports (block 5 blinking in memory mode with fader 5 up, record 5 = 0) | seq 80152, 80158 |
| H65 the pending list after a power-on | Supports in a new history (3 records with PlaybackInfo after the owner's unlogged setup); nothing pending after the two restarts | seq 10 |
| H58 show objects only when listed | Extended: the console's own REC MEM and EDIT MEM produce the same listings as the PC's keys | seq 87480, 54790 |
| H31 / F93 | Supports: the counter continued across two SmartSoft restarts | seq 64766, 70153 |
| H30 / F58 / F92 | Reproduced in sessions 7–9 (#40, #79, byte-identical); SmartSoft's behaviour only | seq 93 … 70233 |
| H60 MASTER scaling | One more rest point that fits the plain product (0x7C at M 0x86 → 0x41) | seq 76687 |
| H51 page select System `[0A][page][00]` | **Untested:** the page change was made on the console; no System write, no System record at the page change | inventory |
| H75 zero-length packet | Untested (no IN length a multiple of 64; largest the 157-record list, 792 bytes) | — |
| H66, H77, H80 | Untested (every session sent the pair; no output outside the bank; no crossfader move) | — |

## Golden-test candidates

**Outbound (SmartSoft's own bytes; the only admissible golden evidence for any future encoder, none of which is cleared):**

| seq (frame) | Bytes | Content |
|---|---|---|
| 93 / 94 (f185 / f187), 176 / 177 (f351 / f353) | `01 00 07 00 00 …` + `00 03 00 27 09 00 00`; + `01 03 00 27 07 00 00` | The System pair, session 7 (sessions 8, 9: seq 64762 …, 70149 …); decoder or replay tests only |
| 8037 / 8038 (f16073 / f16075) | `01 00 08 00 00 00 00 00 00 00 00 00` + `02 04 00 14 00 00 00 00` | Kind 0, value 0 at the start of a drag |
| 42954 / 42955 (f85907 / f85909) | `01 00 08 00 …` + `14 04 00 14 00 04 02 00` | Index 0x04 (channel fader 5), first event |
| 82061 / 82062 (f164121 / f164123) | `01 00 08 00 …` + `0A 04 00 14 00 30 FD 00` | S3's first MASTER event: previous 0x00 with the console at 0x86 |
| 47229 / 47230 (f94457 / f94459), 49357 / 49358 (f98713 / f98715), 50173 / 50174 (f100345 / f100347), 51616 / 51617 (f103231 / f103233) | `71 03 00 14 01 3B 01`, `75 03 00 14 01 47 01`, `79 03 00 14 01 48 01`, `7D 03 00 14 01 37 01` | STACK, IND 1, IND 2, FADERS 25-48 presses |
| 63421 / 63422 (f126841 / f126843) | `01 00 2B 00 …` + `83 27 00 09 06 03 06 01 0C 00 61 00 6A …` | ChannelText "Čaj" (body byte-identical to E9's) |
| 92052 / 92053 (f184103 / f184105) | `01 00 5E 00 00 …` + `23 5A 00 00 00 08 01 30 00 00 00 00 FF …` | **The Memory (0) write** (90-byte body) |
| 39860, 39866 (f79719, f79731) | pseq `FF` then `00` | The outbound counter's wrap |

**Inbound (the console's bytes, for decoders):** seq 10 (f20) the power-on list with PlaybackInfo (pseq 0x00); seq 54790 /
54798 (f109580 / f109596) and seq 58731 / 58739 (f117462 / f117478) System (5, 5, 0) from the console's own EDIT MEM on pages 1
and 12; seq 79062 / 79066 (f158124 / f158132) Memory (0, 0, 8) with the post-MASTER 0x41; seq 84072 / 84076 (f168144 /
f168152) the emptied memory (`00 08 00 60`); seq 87480 / 87484 (f174960 / f174968) a recording from the console's keys; seq
424 (f848), 65093 (f130186), 70480 (f140960) the ChannelText round trips; seq 232 (f464) memory 7 with E9's edit; seq 32850
(f65700), 45477 (f90954) physical takeovers; seq 42974 (f85948), 82071 (f164142) PC takeovers; seq 47245 (f94490) the stack
LCD; seq 54810 and 58747 the LCD "Edit:" "01/01" and "12/01"; seq 51626 ConsoleStatus with body[4] = 1.

## For the curator (proposed; nothing applied here)

**Status changes and annotations:**

| Id | Current | Proposed | Evidence (E9b) |
|---|---|---|---|
| **F131** | OBSERVED (E9) | **VERIFIED** (E9 + E9b): the type-1 framing, w1 = payload length, the stale words, the per-session outbound counter (+1 per data payload across ids, from 0x00, wrapping), one write per loop iteration (the INFERRED part stays INFERRED), no acknowledgement; annotate w1 = 94 for the Memory write (OBSERVED once). **NOT CLEARED** | inventory; `summary`; seq 39860 / 39866 |
| **F132** | OBSERVED | **VERIFIED** for the layout, the unconverted level byte, the previous-byte rule (0x00 for the first event of each index per session; the last wire value afterwards) and indices 0x00, 0x30, 0x31; 0x06 stays OBSERVED; **add index 0x04 = channel fader 5 (OBSERVED once)**; "index = fader − 1" stays HYPOTHESIS (three points). **NOT CLEARED** | kind-0 bursts; seq 42955 |
| **F133** | OBSERVED | **VERIFIED** for the layout and codes 0x00, 0x06, 0x08, 0x31, 0x33, 0x34, 0x36, 0x39, 0x41; 0x09, 0x38, 0x3E, 0x42 stay OBSERVED; **add 0x37 FADERS 25-48, 0x3B STACK, 0x47 IND 1, 0x48 IND 2 (OBSERVED once)**. **NOT CLEARED** | key table |
| **F134** | OBSERVED | **VERIFIED** for fader 1, bump 7, memory bump 1 (byte for byte), MASTER, BUMPS, BLACKOUT, SOLO, MEMS, 1-24, GO with STACK unlit; annotate fader 5, STACK, IND 1, IND 2, 25-48 (OBSERVED once) | the answer table |
| **F135** | OBSERVED | **VERIFIED in part:** a PC fader event takes effect at once, whatever the console's level and the previous byte (fader 1, MASTER, BUMPS in both sessions); the EDIT MEM pick-up and the post-edit blink stay OBSERVED; annotate F141 and H88 (the drop) | seq 8051, 13153, 16315, 39747 |
| **F136** | OBSERVED | **VERIFIED in part:** REC MEM + bump stores the live look and the console lists Memory (0, page − 1, slot − 1) by itself (PC keys in E9 and E9b; also console keys in E9b); an EDIT MEM selection of memory 1 lists System (5, 5, 0) with body `05 05 00` (E9 PC keys, E9b console keys, pages 1 and 12). EDIT MEM store, CLEAR, COPY from the PC stay OBSERVED. **NOT CLEARED, not reversible by the protocol** | seq 79062, 84072, 87480, 54790, 58731 |
| F137 | OBSERVED | Unchanged (no c ≠ 0 record in E9b); annotate H83's partial result and the page finding | seq 54790, 58731 |
| **F138** | OBSERVED | **VERIFIED** for the write (layout; "Čaj" and the restore byte-identical in two sessions); the console's side is F144 (H84). **NOT CLEARED** | seq 63422, 68753 |
| F139 | OBSERVED | Annotate: the open owner item is answered: memory 7 still held channel 7 at full during E9b (E9b seq 232, `SHOW08.ASC`); memory 1 restored (seq 208); memories 9, 10 empty (list unchanged); the label residue stored and returned (seq 424). The owner's answer at the start of E9b (memory 1, not 7) is contradicted by both. After E9b the owner restored memory 7: `SHOW09.ASC` is byte-identical to `SHOW07.ASC` (statements; file hash). Remaining: only the label block's residue, which the SD file cannot show | seq 208, 232, 424; SHOW08.ASC, SHOW09.ASC |
| **F140** | OBSERVED | **VERIFIED** for "opening the Console Simulator sends nothing" (E9 once, E9b twice); the Playback tab's button stays OBSERVED | action table |
| F58, F92 | VERIFIED as SmartSoft's behaviour | Annotate: sessions 7–9, byte-identical, #40 / #79; still never clearance | seq 93 … 70233 |
| F80 | VERIFIED in part | Annotate: pseq 0x00 at the first payload after a power-on, the fourth time (E3, E3b, E8b, E9b) | seq 10 |
| F89, F94 | VERIFIED in part / VERIFIED (structure) | Annotate: the 157-record list once per session, byte-identical (prefix `8208d7647d400d48`) in three more sessions (nine in all); STRING 0 only in the first session; nothing pending after the restarts | seq 180, 64849, 70236 |
| F93 | VERIFIED | Annotate: continued across two more restarts (211 → 212, 115 → 116) | seq 64766, 70153 |
| F96 | VERIFIED in part | Annotate: at rest, fader 0x7C and MASTER 0x86 gave 0x41 in record 5, ChannelLevels[5] and DMX 5 (the plain product); REC MEM stores that value (F145) | seq 76672 … 76687 |
| F97, F100, F101, F103, F113, F114, F115, F116, F98 | as listed | Annotate (reproduced from PC keys or the console in E9b: bumps at BUMPS 0xFF, SOLO, GO, FileName "SHOW08.ASC", page 12, BLACKOUT, the IND keys from the PC, the REC MEM and EDIT MEM screens, the bank key) | as cited |
| F107 | DOCUMENTED (simulator reading INFERRED) | **The simulator's pre-MASTER reading is contradicted** (H85 REJECTED, F145); the manual's "current look" stands | seq 79066 |
| F112 | VERIFIED in part ("byte 1 is 0 in channel mode") | Annotate (limit, not reject): E9b agrees in 103 of 104 non-zero channel-mode records (byte 1 = 0) and 11 of 11 memory-mode records (byte 0 = byte 1); the exception: with memory fader 9 up across FADERS 1-24, channel mode showed record 9 `00 FF 00 00` and record 5 `FF 00 00 00` (channel 5 held at 0xFF by memory 9, physical fader 5 at zero; seq 95014), one unlogged case, H90 | `fader_bytes.py`; seq 95014, 95018 |
| F57 | VERIFIED (limited) | Annotate: c = 0 in all 1 289 records of E9b | `listfetch.py` |
| F67 | VERIFIED for the ids seen | Annotate: Memory (0) seen PC → console with a 90-byte body (48 levels, body[3] = 0x30) against 138 (96 levels, 0x60) from the console | seq 92053 |
| H81 | HYPOTHESIS | **First clause CONFIRMED for channel mode (OBSERVED once, both directions)**; the matching-rule clause untested in E9b | F141 |
| H82 | HYPOTHESIS | Supported (17 of 17, three predictions met); stays HYPOTHESIS, no code derived | key table |
| H83 | HYPOTHESIS | **Supported for the key source and the page (OBSERVED once each)**; c ≠ 0 from the console's own key untested | F142 |
| **H84** | HYPOTHESIS | **CONFIRMED** (OBSERVED, three round trips) | F144 |
| **H85** | HYPOTHESIS | **REJECTED** (OBSERVED once) | F145 |
| **H86** | HYPOTHESIS | **CONFIRMED** (OBSERVED twice, PC and console keys) | F146 |
| H40 | HYPOTHESIS (byte order) | SmartSoft's codec: reading A both ways (OBSERVED; the display part rests on the owner's reading); the console's own reading untested | seq 63422, 65093 |
| H72 | as listed | Add block 56 = FADERS 25-48 (OBSERVED once); blocks 60, 72, 73 tied to PC key codes | seq 51634 |
| **H78** | HYPOTHESIS | **Contradicted in its prediction** for a channel-mode power-on (dim red seen; the power-on mode INFERRED); cause UNKNOWN | `blk2548.py` |
| H53, H58, H64, H65, H76, H79, H60 | as listed | Supported again (annotations) | Hypotheses table |

**New facts (proposed IDs; all OBSERVED in E9b unless stated):**

- **F141 The last mover wins between a PC fader and the physical fader (channel mode).** A kind-0 event sets the control at once
  whatever the physical fader's position (fader 1: 0x00 → 0xEC at seq 39747; fader 5: 0x84 → 0x0D at seq 42974; MASTER: 0x86 →
  0xF3 at seq 82071), and a physical fader takes over at once, at its own position, as soon as the console registers a movement
  of it (fader 1: 0xFF → 0x0D at seq 32850; fader 5: 0x00 → 0x83 at seq 45477); no bump LED blinked. SmartSoft's Simulator kept
  its own fader positions (its drags started from them, INFERRED). At the end of the capture the console's MASTER level was 0xFF
  (PC, seq 82161) while the physical MASTER stood at about half (owner; INFERRED from the H85 move); the owner set it to 100 %
  afterwards (statement). Owner statements agree. Relevance: a PC
  event and a hand on the fader override each other without warning; the first event of a drag can make the output jump.
  H81, H88.
- **F142 The console's own EDIT MEM lists System (39) like the PC's.** EDIT MEM + bump 1 on the console listed [23,
  39:5/5/0, 23, …] with System (5, 5, 0) body `05 05 00` on page 1 (seq 54790 / 54798) and on page 12 (seq 58731 / 58739, LCD
  "Edit:" / "12/01", ConsoleStatus body[2] = 0x0B): the record does not carry the page. CLEAR left the edit with no Memory
  listed (seq 55241, 59066). c ≠ 0 from the console's key untested. Relevance: SC-1 sessions see these records when the owner
  edits; c ≠ 0 would stop them (F137). H83.
- **F143 The Memory (0) whole-object write (SmartSoft's Mem Edit tab).** Header `01 00 5E 00` + stale words, payload `[pseq] 5A
  00 00` + 90-byte body `[page − 1][slot − 1] 01 30` + 48 level bytes + `03 06` + 36 label bytes (seq 92052 / 92053, f184103 /
  f184105: memory 9 of page 1, channel 5 = 0xFF). Nothing was listed back; the console applied it (memory fader 9 to full gave
  channel 5 = 0xFF, seq 94919 / 94923; owner's check). The written layout differs from the console's own Memory body (138
  bytes, body[3] = 0x60, 96 levels; 88 of 88 bodies in E2, E9, E9b): the console accepted 48 levels; channels 49–96 after such a
  write UNKNOWN. Show data, **not reversible by the protocol** without the exact old body. OBSERVED once. **NOT CLEARED.** H87.
- **F144 The console stores a ChannelText write verbatim and returns it in the next download (H84).** Three round trips: E9's
  restore (E9 seq 60605) → E9b's first download after a power cycle (seq 424); "Čaj" (seq 63422) → S2's download (seq 65093);
  the restore (seq 68753) → S3's download (seq 70480); 156 other download objects unchanged. Nothing is listed after the write.
  `SHOW08.ASC` and `SHOW09.ASC` (both saved with the residue on the console) export the label as empty. OBSERVED (the curator may judge the E9 → E9b
  round trip and the E9b round trips as independent reproductions: **VERIFIED proposed**).
- **F145 REC MEM records the post-MASTER level (H85 REJECTED).** Fader 5 at 0x7C (record 5 with the MASTER at 0xFF, seq 76489;
  again 0x7C when the MASTER returned to 0xFF, seq 82161), the console's MASTER at 0x86 (seq 76687): record 5, ChannelLevels[5]
  and DMX 5 = 0x41; PC REC MEM + bump 9 stored channel 5 = **0x41** (seq 79066). OBSERVED once.
- **F146 REC MEM of an empty look empties a memory (H86).** PC keys (seq 84072 / 84076) and console keys (seq 98688 / 98692):
  Memory (0, 0, 8) listed at once with body `00 08 00 60` + 96 zero levels + an empty label block; byte 2 is 0x01 in every other
  console Memory body (86 of 86); the memory's key shows empty in memory mode (`0A 00 00 0A 00 00`, seq 85061). OBSERVED (twice,
  one session). H87.
- **F147 The console lists the Memory object after a recording made with its own keys.** B7: the console's REC MEM + bump 9
  listed Memory (0, 0, 8) with channel 5 = 0x7F (seq 87480 / 87484) and, at the deletion, the emptied memory (seq 98688 /
  98692), with no PC write in between. Relevance: a read-only session receives Memory objects when the owner records on the
  console (id 0, 138 bytes, inside SC-1 as built). OBSERVED (twice, one session).

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H87 | Memory body byte 2 = 1 for a recorded memory and 0 for an empty one, byte 3 = the number of level bytes that follow (0x60 in the console's objects, 0x30 in SmartSoft's write); an emptied memory (byte 2 = 0) is not named in the next 157-record list | F143, F146; 88 + 2 console bodies, one write; `SHOW09.ASC`, saved after memory 9 was emptied, has no `GROUP 9` (the SD export treats the emptied memory as never recorded) | Passive: the next SmartSoft session's 157-record list (memory 9 named or not); zero-risk static: the sfEngine Memory serializer (S11.15 row 6) |
| H88 | The console takes over a fader's level from the physical fader whenever it registers a movement of it, also a movement nobody made: E9's unexplained drop of channel 1 (FF → 00, no write) was such a registration at the physical fader's zero position | F141; E9 F135 (the drop), owner: he did nothing | Passive: a PC fader left up for several minutes with the physical fader untouched, watched for drops; then the owner moving the physical MASTER while the console's MASTER is at a PC-set level (the split state at the end of E9b) and a session watches body[194] |
| H89 | SmartSoft's Console Simulator never takes the console's fader positions: its controls keep their own last value, and a drag starts there (so the first event can jump the output) | F141: drags from 0xFF with the console at 0x00 (fader 1) and from about full with the console at 0x86 (MASTER); F132 (previous byte = SmartSoft's own value) | Owner, zero risk: move a physical fader with SmartSoft's Simulator open and say whether the on-screen fader follows |
| H90 | After FADERS 1-24 with a memory fader up, the memory's levels are held and FaderInfo reports them in channel mode: record k byte 0 = channel k's held level, and the memory fader's record keeps its level in byte 1 | One unlogged case (seq 95014: record 5 `FF 00 00 00`, record 9 `00 FF 00 00`; ChannelLevels[5] stayed 0xFF; LED block 5 steady `00 FF 00 00 FF 00`, block 9 dark, seq 95018) against F112 (byte 1 = 0 in 15 of 15 channel-mode samples in E8; 103 of 104 in E9b) | Passive, logged: memory fader up, FADERS 1-24, then MEMS, with every fader movement logged |

## Input for a future safety check (SC-2), updated after E9b

**E9b clears nothing.** It is the second session of SmartSoft's writes; it makes the framing and the most-used ConsoleEvents
VERIFIED as SmartSoft's and the console's behaviour, which is necessary and not sufficient (CLAUDE.md). No message is cleared, no
type-1 data header is encoded anywhere in the solution, and SC-1's two messages remain the only ones cleared. E9b also adds the
first evidence of a Memory write and of the last-mover behaviour, both of which a safety check must weigh.

| Write class | Exact pattern seen (SmartSoft) | Sessions | Effect on the console | Reversible? | To verify before any consideration |
|---|---|---|---|---|---|
| ConsoleEvent kind 0, fader | header `01 00 08 00` + stale words; payload `[pseq] 04 00 14 00 [index] [value] [previous]`; indices 0x00, 0x30, 0x31 (E9 + E9b), 0x06 (E9), 0x04 (E9b) | **2** (332 + 366 events) | Sets the control at once to the value, whatever the physical fader (F141): channel levels, the MASTER (scales all outputs, F96) and BUMPS; inside EDIT MEM it edits a memory (E9) | Live state: by another event, or by touching the physical fader, which takes over at once at its own position (F141), so the console's state and the faders disagree until someone moves them (E9b's MASTER) | The index of every fader to be whitelisted (five seen); the first-event jump (a drag starts from the sender's own position, H89); the previous byte's role (no pick-up condition, read or not UNKNOWN); the matching rule after mode / page changes and inside record functions; never while a record function is open |
| ConsoleEvent kind 1, key | header `01 00 07 00` + stale; payload `[pseq] 03 00 14 01 [code] [01 / 00]`; 9 codes in both sessions, 4 in E9 only, 4 in E9b only | **2** (64 + 52 events) | Live keys: bumps flash while held, BLACKOUT / SOLO / STACK / IND 1 / IND 2 toggle, GO starts a fade, MEMS / 1-24 / 25-48 switch modes and banks; record keys: REC MEM + bump stores (post-MASTER, F145), REC MEM of an empty look deletes (F146), EDIT MEM / COPY (E9) | Toggles by a second press; a bump by its release (a lost release untested); record functions not reversible by the protocol (re-record or restore from SD) | Whitelist only live keys; the press / release pairing and a session dying between them; no record key ever; what an unknown code does (never to be tried) |
| ChannelText (9) | header `01 00 2B 00`; payload `[pseq] 27 00 09 [ch − 1] 03 06 [36 bytes]` | **2** (3 + 2 writes) | Replaces the label; stored and returned verbatim, persistent across power cycles (F144); nothing listed | Only by writing the exact old 36 bytes, which only a download after System `07 00 00` delivers (the SD file does not carry the residue) | Out of scope for live control; the console's own text decoding (H40) |
| **Memory (0)** | header `01 00 5E 00`; payload `[pseq] 5A 00 00 [page − 1][slot − 1] 01 30 [48 levels] 03 06 [36 bytes]` | **1** (E9b) | Replaces a memory's levels and label (show data); nothing listed; applied (F143) | Only by another write of the full old body (which the console reports in a different, 96-level layout) or by re-recording; channels 49–96 after a write UNKNOWN | A second session; the stored body after a write (download); out of scope for the owner's write mode |
| System (39) `09 00 00`, `07 00 00` | As F58 | 9 sessions | Starts the ConsoleVersion / 157-record download; meaning UNKNOWN (H30) | — | Not needed by a read-only session (H66); not part of live control |
| Show upload, System erase, page select, wheel, FileName, Patch, Independents, SystemSettings | **Not observed** | — | — | — | Never (F121; the owner's decisions of 2026-10-30) |

Further observations a safety check needs:
- **Last mover wins (F141):** neither side is told when the other overrides it; a sender learns the console's level only from
  FaderInfo, and the physical fader's position is not reported until it is moved. A UI must show the console's reported level,
  never assume the physical position.
- **No acknowledgement** (VERIFIED proposed, F131): object writes (ChannelText, Memory) produce no listing at all.
- **The console accepted events and object writes only from hosts that had sent the System pair** (every SmartSoft session).
  Whether it accepts them from a host that never sent it (our read-only session, H66) is UNKNOWN and can only be learned by
  sending, which nothing clears.
- **SC-1 itself:** console-side recordings and EDIT MEM selections are listed to any client (F142, F147): Memory objects and
  System (5, 5, 0) are fetched by SC-1 as built; a selection of memory 2–24 would list c ≠ 0 (INFERRED) and stop the session.

## Surprises and anomalies

1. **The MASTER ended in a split state** (owner's note): the console's level 0xFF (PC, 1327.4 s) and the physical fader at about
   half, because the H85 move to half was made on the console, not in the Simulator (INFERRED from the absence of any write and
   the previous byte 0x00 of S3's first MASTER event). No value near 0x80 occurred after 1327.4 s.
2. **A Memory whole-object write appeared** (B7), with a body that differs from the console's own Memory layout (48 levels,
   byte 3 = 0x30, against 96 and 0x60) and was accepted.
3. **REC MEM stored the post-MASTER level** (0x41), against the simulator's pre-MASTER reading (H85 REJECTED).
4. **E9's mistaken edit of memory 7 was still on the console during E9b** (seq 232, `SHOW08.ASC`), against the owner's answer
   at the start. *(2026-10-31, about 09:45, after the session: restored by the owner; `SHOW09.ASC` = `SHOW07.ASC` byte for
   byte.)*
5. **Blocks 25–48 were dim red after a channel-mode power-on** (INFERRED mode), against H78's prediction.
6. **SmartSoft's idle poll interval fell to 21.60 ms** (seq 90886 → 90888) during the Mem Edit tab work, the lowest in any
   capture (SC-1's floor for our sender is 21 ms).
7. **FaderInfo record 5 read 0xFF in channel mode with the physical fader 5 at zero** (seq 95014), while memory fader 9 was up
   across FADERS 1-24 (one unlogged case, H90).
8. The power-on list held PlaybackInfo too (the owner's unlogged setup before the connect).
9. The console's USB address changed to 21 after the power cycle (10 before); no effect on the analysis.
10. E9's extra ChannelText write of the unchanged label did not recur.
11. The firmware, the `etchsp.sys` version and the Windows build were not re-read.
12. When SHOW08.ASC was saved: the statements' later paragraph (4850ee5) and the coordinator's note call it "saved after E9b";
    the action log ("backup taken: SHOW08.ASC" at 08:35:31.383, before USBPcap and SmartSoft), the statements' first paragraph
    and the console's FileName "SHOW08.ASC" at 55.6 s of S1 (seq 792, F103) say a SHOW08.ASC was saved before the session. The
    file's content cannot decide it (E9b left no net change an ASC file can show), and nothing in this record depends on it:
    the end-of-capture state is decoded from the capture itself (memory 7 at seq 232, the label at seq 70480). For the
    coordinator to settle with the owner.
    **Settled 2026-10-31 (coordinator):** the action log and the FileName are right. SHOW08.ASC is the E9b pre-session backup; the "saved after E9b" wording was the assistant's error in the statements file, known-facts.md (F139) and reverse-engineering.md, corrected in the commit that adds this record. The memory 7 conclusion is unchanged: SHOW08 shows E9's edit outlasting the owner's hand fixes, and SHOW09.ASC = SHOW07.ASC shows the restore and that E9b itself left nothing in the show data.

**Runbook deviations:** the H85 MASTER was moved on the console instead of the Simulator, and its physical position was not
returned to full (1); the B7 recording and deletion used the console's REC MEM and bump 9 instead of the Simulator's (no effect
on the Memory write, which came from the Mem Edit tab as asked); fader 5 was pulled to zero after the B7 recording (statements);
four unlogged Simulator key pairs (FADERS 1-24 at 991.6 s, MEMS / 1-24 at 1296.7 s and 1371.7 s) and the owner's unlogged
playback check of memory 9 (1521.0–1528.9 s); the bank-25-48 bump of H82's test and "MEMS with a Simulator fader up" of H81's
test were not in the script. Otherwise the E9b script was followed: the named backup first (`SHOW08.ASC`, copied, hashed),
Memory Integrity off, ETC's driver on interface 1, USBPcap before SmartSoft, the timed log, the waits, no wheel, no REC SEQ, no
patch, settings, page, show or erase action in SmartSoft.

## Next capture to take

**A. Owner, zero risk:**
1. ~~Memory 7 of page 1 still holds channel 7 at full~~ **Done after the session** (statements, about 09:45): memory 7
   restored, `SHOW09.ASC` byte-identical to `SHOW07.ASC`.
2. ~~The physical MASTER stands at about half while the console's MASTER level is full~~ **Done** ("master is on 100%
   already", about 09:35).
3. Questions: (a) In H85, did you move the console's MASTER (the data show no Simulator event)? (b) In B7, did you use the
   console's REC MEM and bump 9 for the recording and the deletion? (c) At about 08:53:59 (991.6 s) and in the two checks of
   memory 9 (08:59:04, 09:00:19), did you use the Simulator's MEMS / FADERS 1-24 keys? (d) At 09:02:48–09:02:56, the order of
   your check (MEMS, memory fader 9 up, FADERS 1-24, MEMS, fader 9 down)? (e) Does SmartSoft's Simulator show a physical fader's
   movement on screen (H89)?

**B. Next passive capture (SmartSoft, guided), for what E9b left open:**
- H83 with c ≠ 0: on the console EDIT MEM + the bump of **memory 2** (page 1), CLEAR; this decides whether a read-only session of
  ours stops when the owner edits a memory (expect System (5, 5, 1)).
- H87: after the next SmartSoft start, is memory 9 named in the 157-record list? Optionally a second Mem Edit tab write followed
  by a SmartSoft restart, to see the console's stored body after a 48-level write (then delete the memory again).
- H88 / H81: a Simulator fader left up for several minutes with the physical fader untouched; then the physical MASTER touched;
  then MEMS with a Simulator fader up, and a page change with one up (the matching-rule clause).
- H82: one bump in bank 25-48 from the Simulator (channel 25 is patched to DMX 25, outside the rig on DMX 5–16, F99).
- H90: a memory fader up across FADERS 1-24 and back, logged.
- H40 (console side): while channel 7 is labelled "Čaj", save the show to the SD card and read `$CHANNEL 7` in the file; then
  restore.
- E9's PC-key record functions (EDIT MEM store, CLEAR, COPY) only if SC-2 needs them; the owner's write mode does not.
- As before: backup first, USBPcap before SmartSoft, the split before the commit, no wheel, no REC SEQ, no patch / settings /
  page / show / erase action in SmartSoft.

**C. Code questions for the coordinator (no change made here):** (1) the simulated device's REC MEM stores pre-MASTER levels
(the INFERRED reading of F107): E9b says post-MASTER (F145). (2) `ConsoleStateAssembler` reads FaderInfo records only in memory
mode (memory faders, F112) plus the masters (body[193], [194]), and its comment already treats a fader's physical position as
unreported: F141 supports that (after a PC event or a takeover FaderInfo carries the console's level, not the fader's
position); the H90 case (byte 1 non-zero in channel mode) does not touch its readings as built. For information, no change
suggested. (3) SC-1 sessions will see Memory objects with byte 2 = 0 and zero levels after deletions (F146), which fits the
assembler's "recorded = some level above zero" (INFERRED there; byte 2 may be the console's own flag, H87), and System (5, 5, 0)
on console-side EDIT MEM selections (F142); a decoder should not assume 96 levels in a Memory body that did not come from the
console (F143).

**D. Tooling (zero risk):** commit the ad-hoc read-only views used here under `tools/protocol-analyzer/e9b/` (writes per step with
the action log, the merged live-field timeline, the MASTER byte over all FaderInfo payloads, the held-bump byte comparison of two
captures, the non-live objects outside the downloads, the three downloads compared); add the normalization command above to the
analyzer README.

**E9b clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no type-1 data header may be
encoded anywhere in the solution.
