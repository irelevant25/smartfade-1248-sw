# 2026-10-30 E9: SmartSoft writing to the console (USBPcap, passive, guided script)

- **Date:** 2026-10-30, late evening. USBPcap ran 22:19:59.957–22:48:53.336 local time (CET, UTC+1; the first and last
  frames of the raw root-hub capture). The committed console-only split runs from 22:19:59.957156 (f1) to 22:48:44.112983
  (f214494). The guided script ran 22:17:35.374–22:48:55.092; it started SmartSoft at 22:21:16.491, and SmartSoft was
  closed at 22:48:39.464–22:48:47.799 (log).
- **Experiment:** **E9**, the first capture in which a PC **writes** to the SmartFade 1248: a SmartSoft Online session,
  driven by the owner through `tools/usb-probe/e9-guided-session.ps1` and recorded passively by USBPcap ("E9 runbook",
  [../reverse-engineering.md](../reverse-engineering.md)). **Our own software was not involved and sent nothing;** every
  write in this capture is SmartSoft's, made as for any user.
- **E9 clears nothing.** Every new statement below is OBSERVED (one session, one PC, one show) or weaker; none is
  VERIFIED, and none of it is clearance to send (CLAUDE.md; [../safety-checks.md](../safety-checks.md): SC-1 clears the
  poll and the listed fetch for a read-only session, nothing else). **E9 changed show data on the console** (section
  "Side effects on the show"), including one change nobody intended: **memory 7 of page 1 gained channel 7 at full.**
- **Console:** ETC SmartFade 1248, USB device address 10 on bus 1 (as in E8b).
  - Firmware: the ConsoleVersion object of this session reads "3.0.2.9.0.43" (f290, seq 145); the owner's Diagnostics
    reading of the same day is 3.0.2.9.0.43 (E8 statements; not re-read for E9).
  - **Not power-cycled since E8b** (log 22:18:36.635 "console powered off since E8b = n").
- **Host:** the owner's PC, Windows 11 Pro build 26300 (not restated); Memory Integrity **off** (read by the script,
  22:18:36.739); interface 1 on ETC's driver, service **ETCHSP** (read by the script, 22:18:37.719; the `etchsp.sys`
  version was not re-read, 2.00.000 in E2–E3b); SmartSoft 3.0.43 (package 3.0.2.9.0.43b), started by the script with
  `Start-Process`.
  - Driver entries: the owner sees two Device Manager entries named "ETC SmartFade (Interface 1)" (statements). The
    coordinator's finding after the session: one is a not-present (ghost) instance of the console's interface 1 on another
    USB path; the active interface 1 runs ETC's driver (consistent with the ETCHSP reading and with SmartSoft going
    Online). The statements file's first explanation (one Zadig package per bind) is the assistant's earlier guess; the
    ghost instance is the coordinator's later reading. Neither affects the capture.
- **Connection:** USB A-B cable straight into the PC (not restated). USBPcapCMD on root hub `USBPcap1` with `-A
  --inject-descriptors` (the script's suggested command; INFERRED from the injected descriptors f1–f6 and the device
  addresses in the raw file). USBPcapCMD started at 22:19:59 (process creation time, statements).
- **Show backup (safety rule 3): taken.** The script's gate logged "backup taken: SHOW07.ASC" at 22:18:26.804, before
  USBPcap and SmartSoft. `smartfade-sd-card-backup/SHOW07.ASC` (git-ignored folder; 15 944 B, SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`) is **byte-identical to `SHOW04.ASC`, `SHOW05.ASC`
  and `SHOW06.ASC`** (checked when this record was written), so the show was unchanged at the start of E9. The owner:
  "show equals SHOW06 = y" (22:18:32.192). The console reported FileName "SHOW07.ASC" at connect (f144, seq 72; F103).
- **Starting position** (script; owner 22:21:09.368): channel faders at zero, MASTER and BUMPS at full, channel mode,
  BLACKOUT, SOLO, IND 1 / IND 2 and STACK off, page 1; memories 9 and 10 of page 1 empty (owner, 22:21:14.756). The
  console was in fact still in **memory mode** at connect (ConsoleStatus `01 00 00 00 00 01 00`, seq 76) and switched to
  channel mode at 115.6 s during the baseline (console-side, unlogged; "Unlogged actions").
- **Rig:** the DOTZ BAR on DMX 5–16, nothing on DMX IN (owner, 2026-10-30; not restated).
- **Action log:** the guided script's timed log (106 lines, CRLF), PC clock, local time. Stamp semantics as in E8b: a
  `Step` line is stamped when the owner presses Enter "at the moment you START doing this", a `Confirm-Done` line after
  the action, a `wait … done` line after the wait. In E9 the first write of a step came 1.0–5.5 s after its stamp (one
  2.0 s before it); offsets per step in the action table.

**Conventions:**
- `seqN` is the record number in the normalized JSONL (`2026-10-30-e9-smartsoft-writes.jsonl`); `fN` is a frame of the
  committed console-only `.pcap` split: the data-carrying frame, i.e. the submission frame for OUT and the completion
  frame for IN. A write is cited as header / payload (for example seq 9664 / 9665, f19327 / f19329).
- `t` is seconds since the split's first frame (22:19:59.957156 local); local time = 22:19:59.957 + t. Log stamps are
  quoted with their t (for example "sim fader 1 up start" 22:23:38.581 = 218.624).
- `pseq` is a payload's own sequence byte (inbound: the console's counter; outbound: SmartSoft's counter).
- `body[n]`, LED block k (phase A RGB, phase B RGB; blinking = phase A ≠ phase B), FaderInfo record N (bytes 0–3) and
  the LCD cells as in the E8 and E8b records; ConsoleEvent bodies are written `[kind][index or code][value][previous]`.
- Every quoted byte comes from the cited seq or frame. Readings carry their status: OBSERVED (seen in this session),
  INFERRED, HYPOTHESIS, DOCUMENTED, UNKNOWN.

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-30-e9-smartsoft-writes.pcap` | `a5a24de9ef31b2b59de5f3ed72b1415129fd79c4be39bccb196b765eb7536d25` | 10 610 658 B, 214 494 frames, 22:19:59.957156–22:48:44.112983 | yes (73794ea), **the evidence** | Console-only split (device address 10) of the USBPcapCMD root-hub capture; classic pcap, link type 249, µs; made with `tshark -r <raw> -Y "usb.device_address == 10" -F pcap -w <split>`. **The raw root-hub capture is not committed** (it holds the traffic of other devices, addresses 1–6): `e9.pcap` in the repository root (git-ignored), SHA-256 `9955270a03c40fd23c5bfac6849fe7ed09ed9fff03ac1ae515dea2f0d4602278`, 1 094 446 614 B, 918 548 frames, 22:19:59.957156–22:48:53.335626. Completeness below |
| `2026-10-30-e9-smartsoft-writes.jsonl` | `79a1a2dc26e461f7fdd50917cf9713af613a17eaa5c6be393b575b77822f4edd` | 25 642 052 B, 107 356 lines: 1 header, 107 247 `frame` records, 108 `note` records | added with this record (derived) | The split normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 10 --bus 1` (0 other-device frames). Notes: the 106 action-log lines (`--notes-log`, `--notes-utc-offset +01:00`; the first six have negative `t`, they precede the capture) and two analyst notes (capture start with the raw capture's provenance; the raw capture's end). Two runs gave identical bytes. Command below |
| `2026-10-30-e9-actions.txt` | `aeddfdddecfca211bed5484f8d6d492a4dda12c6704a3ac83425c6921ec26ccd` | 6 453 B, 106 lines, CRLF | yes (73794ea) | The guided script's action log. Byte-identical to the untracked `e9-actions.txt` in the repository root (the script's `-LogFile`) |
| `2026-10-30-e9-owner-statements.txt` | `be5eb099803c23bf4bc11c8a7ef692dc04d77ed7f112110b092e5e1734a64ac2` | 3 394 B | yes (73794ea, 6cb2c07) | Context, the owner's statements in and after the session (the deletion steps, memories 9 and 10 deleted by hand afterwards, memory 1's channel 7 set back to full by hand, SHOW07.ASC copied, the two driver entries), transcribed by the assistant. Owner statements never promote by themselves |
| `tools/usb-probe/e9-guided-session.ps1` as run | blob `b3a8263f1dc583cb4a596386ae22d19a10e5db68` at 6de4ddb (content SHA-256 `2ca7aa6d5cf8ca75a717ef1415aa07724e93d7fb5599f8ed6d6ce6eb5e0f0210`) | 257 lines | yes (6de4ddb, 22:15:20, before the session) | The guided session that ran. It talks only to the operator and starts SmartSoft; it sends nothing to the console. 6cb2c07 (after the session; blob `273fb9d22dad2f4557ca6c313cc2eac128dd73a5`, SHA-256 `e1c19526e8226759de00d43413b5fafbf77f8d30a1ce9c29aa57d82319cc72e2`) replaced its deletion step |
| `smartfade-sd-card-backup/SHOW07.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` | 15 944 B | no (folder git-ignored) | The backup saved at 22:18:26, byte-identical to `SHOW04.ASC` … `SHOW06.ASC` |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `ea7627d7cdd0ff214f003f290ae7cb6b75b73149b4e753011c76b129efd20fe8` | | yes | Normalizer version 2, unchanged since E3 |
| `tools/protocol-analyzer/hsp_summary.py` | `3f324e9f94732235d5cbaa0ea6ce76619db4b1357612de5828db1ccfcb0fd1ec` | | yes | `summary`, `log`, `payloads`, `timeline`, `census`, `lcd`, `startdiff`, `sessions` |
| `tools/protocol-analyzer/poll_rules.py` | `b54cf6d24e01469b820312f75762bc7d1e873797d1e0c109fcda3ae244dc457e` | | yes | Stale-word rule and SmartSoft's cadence |
| `tools/protocol-analyzer/e8b/objtable.py`, `cst_led.py`, `blk2548.py`, `tshark_xcheck.py` | `285bdf8c83877ac5c67cb18d1d6ab6bc7c9930c0f5b93dd01126953830bf537b`, `8c83536f976bb92494d54ecf0902d5885b78ad315b2685bf6e70816b8849b00f`, `b3b341721122ae43370672fad2d7e1b56dd80eac87d5a03b65b064601edfd58c`, `d97668b353633cb69c38b5d3301a2f9efbfd88d8e0700df499bc839bf1541eef` | | yes | Run unchanged on the E9 JSONL |
| tshark 4.0.3 (Wireshark portable) | | | no | Frame counts by address in the raw capture, the reproduction of the split, the `usb.capdata` cross-check |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-30-e9-smartsoft-writes.pcap -o %TEMP%\e9.jsonl --device-address 10 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the ConsoleVersion object of this capture, f290; the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E9)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 22:18:36.739)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 22:18:37.719; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-30-e9-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (tshark -r <raw> -Y 'usb.device_address == 10' -F pcap -w <split>; re-run with tshark 4.0.3 it reproduces the committed split byte for byte) of the USBPcapCMD root-hub capture e9.pcap (SHA-256 9955270A03C40FD23C5BFAC6849FE7ED09ED9FFF03AC1AE515DEA2F0D4602278, 1094446614 bytes, 918548 frames, 22:19:59.957156-22:48:53.335626 local time), which the owner started in an admin window on root hub USBPcap1 with -A --inject-descriptors (guided script); the raw capture is not committed because it holds the traffic of other devices (addresses 1-6); frame 1 is at 22:19:59.957156 local time, the raw file's first frame" --note 1733.37847 "analyst: the raw capture's last frame (another device) is at 22:48:53.335626 local time; the console's last frame, frame 214494 of this split, is at 1724.155827 s (22:48:44.112983); the log stamps SmartSoft's close at 22:48:39.464 (start) and 22:48:47.799 (closed) and USBPcap's stop at 22:48:55.090"
```

Expected: source SHA-256 `A5A24DE9…6D25`, 107 247 transfers from 214 494 device frames (0 other-device frames skipped),
108 notes, output SHA-256 `79a1a2dc…4edd`.

**Completeness of the split:**
- tshark 4.0.3 re-run on the raw capture with the filter above writes a file **byte-identical** to the committed split
  (SHA-256 `a5a24de9…6d25` both).
- Frame counts in the raw capture by address (counted, not read): address 10: 214 494 (214 484 bulk, 10 control);
  address 4: 562 852 (562 846 isochronous); address 6: 141 064 (141 058 interrupt); address 3: 118; addresses 1, 2, 5: 8,
  6, 6 control. Total 918 548.
- The normalizer run on the raw capture with `--device-address 10` found 214 494 frames and skipped 704 054 frames of
  other devices unread; its 107 247 records equal the split's in every field except the pcap frame numbers (0 differing
  records).
- Independent check: tshark's `usb.capdata` equals the JSONL `data` in 107 242 of 107 242 data-carrying bulk records
  (0 mismatches, every capdata frame used).

The other figures come from small read-only scripts (importing the committed functions of `hsp_summary.py`, as the E8
and E8b scripts do), run with `py -3.13 -I` from the session scratchpad, outside the repository; they are not committed
with this record ("Next capture to take", D).

## Session health

| Check | Result | Source |
|---|---|---|
| Transfers | 107 247: 3 synthetic EP0 transfers (`--inject-descriptors`, f1–f6; the device descriptor `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01`, as in E3b and E8b), 2 real EP0 reads, 107 242 bulk on interface 1: **53 890 OUT** on 0x04 (52 458 polls, 630 fetches, 401 data headers, 401 data payloads) and **53 352 IN** on 0x83 (52 458 reply headers, 894 payloads) | `summary` |
| EP0 | GET_DESCRIPTOR STRING 0 (f7 / f8, `04 03 09 04`) and STRING 2 (f9 / f10, "ETC SmartFade") at 76.612–76.625, 1 ms before the first poll, as in E3b session 1 (F41, F56) | JSONL seq 4, 5 |
| Interface 0 | **No URB on 0x81 or 0x02** (the raw capture holds no interrupt frame of address 10): SmartSoft never used the HID interface, also while writing (F51, F63) | raw counts |
| USBD status | 0x00000000 in 107 247 of 107 247 transfers | JSONL |
| Framing | **0 errors.** Every OUT starts with a 12-byte header, w0 ∈ {0, 1, 2}; every type-1 header is followed by one OUT transfer of exactly w1 bytes (401 of 401); every IN follows a poll (headers) or its header (payloads) | `summary` (H15, H16) |
| Stale words (F52) | 52 458 of 52 458 polls carry the words of the most recent fetch or reply header (`poll_rules.py`), and so do **401 of 401 data headers** (52 858 of 52 858 poll and data headers after the first poll, `summary`); example: header seq 9679 `01 00 08 00 0D 00 …` after the OutputLevels reply (w2 = 13) | `poll_rules.py`, `summary` |
| Replies (F53) | 52 458 polls, 52 458 reply headers: 630 type 3, 264 type 4 with a list, 51 564 idle; type-3 echo 630 of 630; every poll after a fetch answered by the fetched object | `summary` |
| Lists and fetches (F57) | 264 lists; **630 records = 630 fetches, in list order**; list sizes 1 × 183, 2 × 33, 3 × 9, 4 × 14, 5 × 22, 6 × 1, 25 × 1 (connect), 157 × 1 (download). **c ≠ 0 in four records** (System (39) with qualifiers 5 / 5 / 6, 5 / 5 / 8 twice, 5 / 5 / 9; seq 68341, 77854, 91071, 95545): the first DirtyObjects records with c ≠ 0 in any capture (F137 below) | `listfetch` (ad hoc) |
| Inbound counter (F55) | 894 payloads, pseq **245 (0xF5, seq 8) … 114 (0x72), 0 breaks**, 4 wraps (f96 → f104, f33150 → f33162, f87888 → f87894, f151486 → f151494), byte 1 = 0x00 and LE length = payload − 5 in 894 of 894. **The first pseq continues E8b's last (244, 0xF4):** no power cycle in between, and the session of our host was followed by SmartSoft's (F93, H31) | `summary` |
| Outbound counter | **401 data payloads, pseq 0x00 (f275) … 0xFF (f63745), 0x00 (f63757) … 0x90 (f192149): +1 per data payload across all ids, one wrap, 0 gaps.** SmartSoft's counter starts at 0x00 in each session (F55, E3b); this is the first session with more than two writes | `summary` |
| Body lengths (F67) | Every fetched object has its F67 length (Memory 138, ChannelText 39, FaderInfo 195, LedStatus 451, …); System (39) 3 bytes also with a = 5 | `summary`, `objtable.py` |
| Latency | Reply header submit → completion max 16.68 ms (seq 39474, 679.678, at the MEMS press), payload max 4.31 ms (seq 16645), OUT max 0.56 ms (seq 30470) | `objtable.py` |
| End | The last exchange is an idle poll / reply (seq 107246 / 107247, f214491 / f214494, 1724.152 / 1724.156), 4.65 s after "smartsoft close start" (1719.507); no closing message; the raw capture has no console frame after f214494 up to its end (22:48:53.336) (F64) | log, raw |

**SmartSoft's cadence** (`poll_rules.py`; ms): poll → poll (submit) n = 52 456, min 29.01, median 31.08, max 77.37;
fetch → next poll n = 630, min 10.46, median 15.48, max 24.04; payload completion → fetch n = 449, min 20.41, median
28.72, max 34.55 (F62: reproduced). **Writes:** data header → its payload 0.05–0.32 ms (median 0.10); data payload → next
OUT (a poll in 220 cases, a fetch in 181) 14.51–16.46 ms (median 15.43; E3b: 14.58–15.82 after its six writes);
consecutive writes at least 44.8 ms apart (median 61.0 ms within a burst): one write per loop iteration (S11.2 step A,
INFERRED).

**Objects** (`objtable.py`; payloads, changed against the previous payload of the id): Memory 20 (16 in the download, 4
after stores), Sequence 1, SequenceMain 11 (9 in the connect list, 1 download, 1 at GO), StepContent 19, Patch 1, Page 12,
ChannelText 96, Independents 1, OutputLevels 32, FaderInfo 122, ChannelLevels 32, PlaybackInfo 65, DisplayContent 71,
LedStatus 101, ConsoleStatus 33 (23 unchanged), FileName 2, ConsoleVersion 1, SystemSizes 1, System 8, SystemSettings 1.
**No ConsoleEvent (20) and no other id was listed or fetched;** the console never echoed a write.

## The connect phase (SmartSoft's sixth session with the System pair)

- **First poll** seq 6 (f11, 76.626, all zero), answered at once by a type-4 header announcing 132 bytes: the **connect
  list** seq 8 (f16, pseq 0xF5) of **25 records**: PlaybackInfo, FaderInfo, LedStatus, ChannelLevels, OutputLevels,
  DisplayContent, SequenceMain × 9, FileName, ConsoleStatus × 9; all fetched (seq 9 … 108). E8b ended at 20:56:44 and E9
  connected at 22:21:16 without a power cycle; what happened in between is not in any capture. FileName ("SHOW07.ASC",
  seq 72) fits the owner's SD save at 22:18:26 (F103; INFERRED). The nine SequenceMain and nine ConsoleStatus records
  are UNKNOWN history (no information about the 85 minutes in between). H65.
- **System `09 00 00`** header seq 137 / payload seq 138 (f273 / f275, 78.262) `01 00 07 00 00 00 00 00 00 00 00 00` +
  `00 03 00 27 09 00 00` after reply header **#40** (after 14 idle replies: the 25-record list took the other
  iterations) → list [37] seq 141 (f282) → ConsoleVersion "3.0.2.9.0.43" seq 145 (f290).
- **System `07 00 00`** seq 220 / 221 (f439 / f441, 79.510) `01 03 00 27 07 00 00` after reply header **#79** (after 37
  idle replies) → the **157-record list** seq 224 (f448), **byte-identical to E3b's f356** (and so to E2's f384 and
  E3's f352) → the download of 157 objects, 79.53–86.78 s (last payload ChannelLevels seq 852, f1704).
- The download's first bodies equal E3b's for 154 object keys (`startdiff --other` E3b): the differences are the live
  state, FileName ("SHOW07" against "SHOW05"), SystemSettings body[13], [15] (`32` = 5.0 s against E3b's `33`, the
  accidental 5.1 s of E3b, F102) and the connect list. **The show at connect was the show of E2–E8b.**
- Reproduces F56 (string reads, all-zero first poll), F58 / F92 (the two System writes, byte-identical, after reply
  headers #40 and #79: sixth session; SmartSoft's behaviour only, never clearance), F59, F89 (the 157-record list once,
  at connect) and F94.
- **The 157-record list came only once** (after System `07 00 00`); none of the later writes produced it.

## Inventory of the PC → console writes

**401 data writes** (type-1 header + payload, bulk 0x04, interface 1), all from SmartSoft, all with USBD status 0:

| Kind | Count | Header (w0 w1, words LE) | Payload `[pseq][u16 LE len][id][body]` | Body |
|---|---|---|---|---|
| System (39) | 2 | `01 00 07 00` + stale words | `pseq 03 00 27 …` | `09 00 00`, `07 00 00` (the F58 pair) |
| ConsoleEvent (20), kind 0 (fader) | **332** | `01 00 08 00` + stale | `pseq 04 00 14 …` | `00 [index] [value] [previous]` |
| ConsoleEvent (20), kind 1 (key) | **64** (32 press / release pairs) | `01 00 07 00` + stale | `pseq 03 00 14 …` | `01 [code] [01 = press / 00 = release]` |
| ChannelText (9) | 3 | `01 00 2B 00` + stale | `pseq 27 00 09 …` | `[channel − 1] 03 06 [36 bytes]` (39 bytes) |

**Not seen:** no Memory (0) write (the "Mem Edit" step went through the console's EDIT MEM, see below), no other object
write, no ConsoleEvent kind 2 (the wheel was excluded), no System other than the pair (no page select: H51; no erase:
F121), nothing on interface 0, no control transfer of SmartSoft's besides the two string reads.

### Action log matched to the writes

"Offset" = the first write of the step minus the log stamp. Every write of the session is in this table.

| Log stamp (t) | Step | Writes (pseq; header seq / payload frame) | Offset | The console's answer (first list; seq) |
|---|---|---|---|---|
| 76.534 | `smartsoft start` | System pair pseq 0, 1 (seq 137, 220) | +1.73 s | the connect phase above |
| 95.720 / 155.732 | `smartsoft online` / 60-s baseline | none | | one list at 115.573 (seq 2705): the console-side switch to channel mode ("Unlogged actions") |
| 174.885 / 204.893 | `simulator opened` / 30 s untouched | **none: opening the Simulator wrote nothing** | | none |
| 218.624 / 228.217 | `sim fader 1 up start` / `at full` | kind 0, index 0x00, pseq 2–30 (29), seq 9664 … 9826, f19329 … f19651, 223.750–225.403: `00 00 05 00` … `00 00 FF FF` | +5.13 s | list [13, 14, 15, 22, 21] at 223.815 (seq 9672), 65 ms after the first write |
| (unlogged, during the owner's answer at 306.100) | — | kind 0, index 0x00, pseq 31–51 (21), seq 14028 … 14225, f28057 … f28449, 290.502–292.919: `FC FF` → … → `00 00` … `1D 00` → … → `FF FF` | | channel 1 down and up again ("Unlogged actions") |
| 326.034 / 331.950 | `sim fader 1 down start` / `at zero` | kind 0, index 0x00, pseq 52–85 (34), seq 16466 … 16659, f32933 … f33317, 327.708–329.659: `FC FF` … `00 00` | +1.67 s | list at 327.726 (seq 16470) |
| 364.911 / 377.173 | `sim bump 7 press` / `release` | key 0x06: press pseq 86 (seq 19054, f38109) 368.452, release pseq 87 (seq 19435, f38871) 374.255, held 5.80 s | +3.54 s | list at 368.474 (seq 19058) |
| 444.151 / 454.783 | `sim master to half start` / `at half` | kind 0, index 0x30, pseq 88–132 (45), seq 24114 … 24450, f48229 … f48899, 446.828–450.514: `00 30 FD 00` … `00 30 7E 80` | +2.68 s | list [14, 22] at 446.844 (seq 24118) |
| 474.879 / 484.301 | `sim master to full start` / `at full` | kind 0, index 0x30, pseq 133–173 (41), seq 26352 … 26580, f52705 … f53159, 480.048–482.343: `82 7E` … `FF FF` | +5.17 s | FaderInfo listed from 480.067 |
| 524.464 / 531.124 | `sim bumps to zero start` / `at zero` | kind 0, index 0x31, pseq 174–227 (54), seq 29433 … 29715, f58867 … f59429, 526.648–529.489: `00 31 FD 00` … `00 31 00 00` | +2.18 s | list [14] at 526.666 (seq 29437) |
| 558.544 / 564.289 | `sim bumps to full start` / `at full` | kind 0, index 0x31, pseq 228–255, 0–9 (38), seq 31724 … 31924, f63449 … f63847, 560.697–562.676: `06 00` … `FF FF` | +2.15 s | FaderInfo from 560.779 (seq 31734) |
| 586.094 | `sim blackout on` | key 0x34: pseq 10 / 11 (seq 33541 / 33546; f67083 / f67093), 587.785 / 587.831 | +1.69 s | list [22] at 587.804 (seq 33545), **between press and release** |
| 612.403 | `sim blackout off` | key 0x34: pseq 12 / 13 (f70457 / f70467), 613.953 / 614.000 | +1.55 s | list [22] at 613.972 (seq 35232) |
| 630.706 | `sim solo on` | key 0x33: pseq 14 / 15 (f72771 / f72781), 631.858 / 631.904 | +1.15 s | list [22] at 631.876 (seq 36389) |
| 651.272 | `sim solo off` | key 0x33: pseq 16 / 17 (f75453 / f75463), 652.634 / 652.680 | +1.36 s | list [22] at 652.652 (seq 37730) |
| 678.016 / 687.731 | `sim mems and memory bump 1 press` / `release` | key 0x39: pseq 18 / 19 (f78943 / f78961) 679.647 / 679.770; key 0x00: pseq 20 / 21 (f79191 / f79675) 681.470 / 685.106, held 3.64 s | +1.63 s | list [23, 22, 21] at 679.678 (seq 39475); [14, 22] at 681.488 (seq 39599) |
| 708.610 | `sim channel mode again` | key 0x36: pseq 22 / 23 (f82897 / f82907), 710.005 / 710.052 | +1.40 s | list [23, 22] at 710.023 (seq 41452) |
| 743.926 / 759.298 | `sim go stack unlit` / `go done` | key 0x31: pseq 24 (seq 43869, f87739) 747.524, release pseq 25 (seq 43952, f87905) 748.443, held 0.92 s | +3.60 s | list [3, 22] at 747.543 (seq 43873) |
| 866.081 | `playback all faders to zero` | **none** | | **no list** (nothing changed on the bus) |
| 900.445 | answer: channel 7 label before = "-" | | | |
| 958.257 | `channel 7 label to Caj with caron` | ChannelText pseq 26 (seq 57436 / 57437, f114873) at 956.232: the **unchanged empty label**, 2.0 s before the stamp; ChannelText pseq 27 (seq 57880 / 57881, f115761) at 963.130: "Čaj" | −2.02 / +4.87 s | **no list** after either write |
| 1003.159 | `channel 7 label restored` | ChannelText pseq 28 (seq 60604 / 60605, f121209) at 1005.427 | +2.27 s | **no list** |
| 1052.051 | answer: mem edit decision = y | | | |
| 1098.981 / 1113.990 | `mem edit memory 1 channel 7 to 50` | keys 0x39 (pseq 29 / 30, 1101.512), 0x36 (31 / 32, 1102.826), 0x42 (33 / 34, 1104.403), **0x00** (35 / 36, 1105.877); kind 0 index 0x06 pseq 37–76 (40), seq 67235 … 67479, 1107.974–1110.823: `02 00` … `FF FF` … `7F 82`; key 0x42 (77 / 78, 1111.775) | +2.53 s | System (5, 5, 0) listed at 1105.971 (seq 67101); **Memory (0, 0, 0) listed at 1111.793 (seq 67548)** |
| 1120.626 / 1135.640 | `mem edit memory 1 channel 7 restored` | key 0x42 (pseq 79 / 80, 1122.579), **0x06** (81 / 82, 1123.823); kind 0 index 0x06 pseq 83–103 (21), seq 68605 … 68731, 1127.910–1129.398: `6A 7F` … `00 00` … `FF FF`; key 0x42 (104 / 105, 1130.841); kind 0 index 0x06 pseq 106–114 (9), seq 69068 … 69107, 1134.489–1134.892: `EF FF` … `00 00` | +1.95 s | **System (5, 5, 6)** at 1123.889 (seq 68341); **Memory (0, 6, 0) listed at 1130.859 (seq 68828): memory 7, not memory 1** |
| 1145.540 / 1153.688 | `console fader 5 to half` / `at half` | none (physical fader) | | FaderInfo record 5 up to 0x79 (seq 69978 … 70152) |
| 1191.734 | `sim rec mem` | key 0x41: pseq 115 / 116 (seq 73005, f146011), 1194.744 | +3.01 s | list [21, 22] at 1194.763 (seq 73009) |
| 1208.636 | `sim rec mem bump 9` | key 0x08: pseq 117 / 118 (seq 73995, f147991), 1210.036 | +1.40 s | **Memory (0, 0, 8) listed at 1210.100 (seq 74003)** |
| (unlogged, 13 s after the wait, before the answer at 1240.167) | — | key 0x39: pseq 119 / 120 (seq 75722, f151445), 1236.782 | | memory mode (seq 75730); the owner looked at memory 9's key |
| 1249.530 | `sim edit mem` | key 0x42: pseq 121 / 122 (f153257), 1250.739 | +1.21 s | list [21, 22] at 1250.755 (seq 76632) |
| 1268.175 | `sim edit mem bump 9` | key 0x08: pseq 123 / 124 (f155685), 1269.501 | +1.33 s | System (5, 5, 8) at 1269.630 (seq 77854) |
| 1294.273 / 1300.637 | `console fader 5 to full in edit` | none (physical; the moves came at 1285.8–1286.3, before the stamp, and 1296.9–1297.3) | | LED block 5 and the LCD only (seq 78905 … 79676) |
| 1315.856 | `sim clear leaves edit mem` | key 0x38: pseq 125 / 126 (f161875), 1316.890 | +1.03 s | list [23, 14, 21, 22] at 1316.906 (seq 80941): **no Memory** |
| 1335.816 | `sim copy` | key 0x3E: pseq 127 / 128 (f164467), 1336.939 | +1.12 s | list [23, 22, 21] at 1336.958 (seq 82237) |
| 1350.033 | `sim copy source 9` | key 0x08: pseq 129 / 130 (f166369), 1351.618 | +1.58 s | list [21] at 1351.712 (seq 83194) |
| 1373.129 | `sim copy target 10` | key 0x09: pseq 131 / 132 (f169309), 1374.407 | +1.28 s | **Memory (0, 0, 9) listed at 1374.505 (seq 84664)** |
| 1418.637 / 1423.300 | `console fader 5 to zero` | none (physical; FADERS 1-24 pressed on the console first, 1420.42) | | ConsoleStatus seq 87625; record 5 → 0 (seq 87690) |
| 1463.736 / 1507.936 | `delete memory 9` / `memory 9 deleted` | keys 0x42 (pseq 133 / 134, 1469.218), 0x08 (135 / 136, 1473.521), 0x38 (137 / 138, 1500.249) | +5.48 s | System (5, 5, 8) at 1473.617 (seq 91071); after CLEAR [23, 21, 22] (seq 92792): **no Memory** |
| 1538.540 / 1557.355 | `delete memory 10` / `memory 10 deleted` | keys 0x42 (pseq 139 / 140, 1541.440), 0x09 (141 / 142, 1542.666), 0x38 (143 / 144, 1550.751) | +2.90 s | System (5, 5, 9) at 1542.761 (seq 95545); after CLEAR [23, 21, 22] (seq 96069): **no Memory** |
| 1676.700 | answer: memories 9 and 10 empty again = "no, they are not …" | | | matches the data |
| 1719.507 / 1727.842 | `smartsoft close start` / `closed` | none | | last transfer 1724.156 (f214494) |

Totals: kind 0 = 29 + 21 + 34 + 45 + 41 + 54 + 38 + 40 + 21 + 9 = 332; key pairs = 32; with the System pair and the
three ChannelText writes, 401.

### Unlogged actions

| t (s) | Seq | Data | Reading |
|---|---|---|---|
| 115.573–115.668 | 2705, 2709, 2713 | ConsoleStatus body[0] 0x01 → 0x00, LED blocks 1–24, 55, 58 to the channel view; no PC write between pseq 1 (79.5) and pseq 2 (223.8) | A mode switch on the console during the baseline (the owner set channel mode, which the starting position asked for; INFERRED) |
| 279.506–279.737 | 13315 … 13335 | Channel 1 FF → 00 in FaderInfo record 1, ChannelLevels[1], OutputLevels[2], LED block 1 G and the LCD cell, in one step; **no PC write** between pseq 30 (225.4) and pseq 31 (290.5) | Cause **UNKNOWN** (the physical fader 1 touched? a console-side reset?); question to the owner |
| 290.502–292.919 | 14028 … 14237 | SmartSoft's fader 1 from its full position down to 0 and back to full; the console followed at once (record 1 `85` seq 14048, OutputLevels[2] `B7` seq 14042, … record 1 `FF` seq 14229) | The owner moved the Simulator fader again while answering (INFERRED from the timing) |
| 1236.782 | 75722 | Key 0x39 (MEMS) from the PC; memory mode; memory 9's key `46 00 00 46 00 00` (recorded) in the map (seq 75740) | The owner checked memory 9 before answering "memory 9 recorded = y" |
| 1285.776–1290.214 | 78901 … 79209 | Physical fader 5 up to full and back to about 0x82 inside EDIT MEM, 8.5 s before the step's stamp | Part of the edit step, early |
| 1420.424 | 87621 … 87633 | ConsoleStatus body[0] 0x01 → 0x00 (channel mode) with no PC write | FADERS 1-24 pressed on the console before pulling fader 5 down (INFERRED) |

## ConsoleEvent kind 0: the faders

**Layout (OBSERVED, 332 of 332):** header `01 00 08 00 [w2 … w5 stale]`, payload `[pseq] 04 00 14 00 [index] [value]
[previous]` (len 4, w1 = 8). Example, the first event: seq 9664 / 9665 (f19327 / f19329) `01 00 08 00 00 00 00 00 00 00
00 00` + `02 04 00 14 00 00 05 00`.

**Indices and what they moved (OBSERVED):**

| Index | Simulator control (log) | The console's object that followed | Evidence |
|---|---|---|---|
| 0x00 | channel fader 1, bank 1-24 | FaderInfo record 1 byte 0 (body[1]), ChannelLevels[1], OutputLevels[2] (DMX 1), LED block 1 G, LCD cell body[3] | seq 9672 … 9834, 16470 … 16667 |
| 0x06 | channel fader 7 (in the Mem Edit steps, inside EDIT MEM) | LED block 7 G and LCD cell body[9] (the edited memory's channel 7, blind) and the stored Memory body[10] = channel 7; FaderInfo record 7 never moved with it | seq 67357 … 67486, 68683 … 68717, 67552, 68832 |
| 0x30 (48) | MASTER | FaderInfo body[194]; LED block 53 blinking `37 37 FF 0A 0A 0A` below full (F85) | seq 24124 … 24457, 26362 … 26560 |
| 0x31 (49) | BUMPS | FaderInfo body[193] | seq 29443 … 29674, 31734 … 31899 |

Note the order: the MASTER is index 48 and BUMPS 49, while in FaderInfo body[193] is BUMPS and body[194] the MASTER.
ConsoleLink's ML numbers (24 MASTER, 25 BUMPS, class 5) do not apply to the 1248 (S11.15 row 10 warned of this). The
indices of the other 22 channel faders, the memory faders and the crossfaders were not observed (that channel fader k
is index k − 1 rests on two points, 0x00 and 0x06: HYPOTHESIS, and no index may be derived from it).

**Level scale (OBSERVED):** the value byte is the console's level byte, 0x00–0xFF, unconverted: FaderInfo record 1 took
the values of the events (0x1F, 0x5D, 0xAA, 0xE7, 0xFF at seq 9684, 9725, 9754, 9789, 9830 = the values of pseq 5, 12,
17, 23, 25), and so did ChannelLevels[1] with the MASTER at full (0x25 at seq 9690 = pseq 6). The MASTER's "about half"
was 0x7E (pseq 132, FaderInfo body[194] = 0x7E at seq 24457).

**The previous byte (OBSERVED):**
- The first event of each index in the session carries previous = 0x00 (4 of 4: `00 00 05 00`, `00 30 FD 00`, `00 31
  FD 00`, `00 06 02 00`), also for the MASTER and BUMPS, which stood at 0xFF on the console (FaderInfo seq 16, `… FF
  FF`). So the byte is SmartSoft's own last value of that index, 0x00 at session start, not read back from the console
  (S11.14.4: `DAT_1009ce84[index]`, INFERRED, agrees).
- In 327 of the 328 later events it equals the value of the preceding event of the same index on the wire. The
  exception: pseq 0x44 (seq 16560, f33119) `00 00 52 5D`, while the preceding event of index 0 on the wire was pseq 0x43
  `00 00 5F 6D` (f33107): the value 0x5D never reached the wire. INFERRED: a GUI event overwrote or dropped a queued
  element (S11.14.4's coalescing, which keeps the first previous byte, or a race with the thread's copy of the element).
- The console applied the MASTER's and BUMPS's first events although their previous byte (0x00) did not match the
  console's level (0xFF): FaderInfo body[194] read 0xF9 at seq 24124 and body[193] 0xF7 at seq 29443. So the previous
  byte is no pick-up condition for these controls. Whether the console reads it at all is UNKNOWN.

**Pacing (OBSERVED):** at most one write per loop iteration, 44.8–172 ms apart within a burst (median 61 ms); 56 of 332
events repeat their predecessor (`FF FF`, `00 00` at the end stops), and SmartSoft sent them anyway.

## ConsoleEvent kind 1: the keys

**Layout (OBSERVED, 64 of 64):** header `01 00 07 00 [stale]`, payload `[pseq] 03 00 14 01 [code] [state]`, state 0x01
on press and 0x00 on release, as **two separate writes** (a mouse click: 46–124 ms apart; held: 0.92 s GO, 3.64 s
memory bump 1, 5.80 s bump 7). Example: bump 7 press seq 19054 / 19055 (f38107 / f38109) `01 00 07 00 00 00 00 00 00
00 00 00` + `56 03 00 14 01 06 01`; release seq 19435 / 19436 (f38871) `57 03 00 14 01 06 00`.

**The 1248's key codes (OBSERVED, one session; each named by its log step):**

| Code | Key (Simulator) | Pairs | Proving step and first press (header seq, payload frame) | LED block that changed with it |
|---|---|---|---|---|
| 0x00 | bump 1 (memory bump 1 in memory mode) | 2 | `sim mems and memory bump 1 press` (seq 39595, f79191); also selected memory 1 in EDIT MEM (seq 67091) | 1 |
| 0x06 | bump 7 | 2 | `sim bump 7 press` (seq 19054, f38109); also selected memory 7 in EDIT MEM (seq 68333) | 7 |
| 0x08 | bump 9 | 4 | `sim rec mem bump 9` (seq 73995, f147991), `sim edit mem bump 9`, `sim copy source 9`, `delete memory 9` | 9 |
| 0x09 | bump 10 | 2 | `sim copy target 10` (seq 84654, f169309), `delete memory 10` | 10 |
| 0x31 | GO | 1 | `sim go stack unlit` (seq 43869, f87739) | 50 (blue during the fade, H64) |
| 0x33 | SOLO | 2 | `sim solo on` / `off` (seq 36385, f72771; seq 37726) | 52 (`FF`×6 on, F100) |
| 0x34 | BLACKOUT | 2 | `sim blackout on` / `off` (seq 33541, f67083; seq 35228) | 53 (`00 00 FF 00 00 FF` on, F114) |
| 0x36 | FADERS 1-24 | 2 | `sim channel mode again` (seq 41448, f82897) | 55 (`00 FF 00 …` in channel mode) |
| 0x38 | CLEAR | 3 | `sim clear leaves edit mem` (seq 80937, f161875), both delete steps | 57 (blinking in a function → steady when it was left) |
| 0x39 | FADERS MEMS | 3 | `sim mems …` (seq 39471, f78943) | 58 (`FF 00 00 …` in memory mode) |
| 0x3E | COPY | 1 | `sim copy` (seq 82233, f164467) | 63 (`FF`×6) |
| 0x41 | REC MEM | 1 | `sim rec mem` (seq 73005, f146011) | 66 (`FF 00 00 FF 00 00`) |
| 0x42 | EDIT MEM | 7 | `sim edit mem` (seq 76628, f153257); the Mem Edit and delete steps | 67 (`FF 37 37 0A 0A 0A`) |

- **For all 13 codes, code = (LED block number − 1)**, i.e. the 0-based index of the key's LED block (bumps 1, 7, 9, 10 →
  blocks 1, 7, 9, 10; GO → 50; SOLO → 52; BLACKOUT → 53; 1-24 → 55; CLEAR → 57; MEMS → 58; COPY → 63; REC MEM → 66;
  EDIT MEM → 67). OBSERVED for these 13; for any other key it is a HYPOTHESIS (H82) and **no code may be derived from
  it**.
- **The console acts on the press:** the first list came between press and release for BLACKOUT (587.804 between 587.785
  and 587.831), SOLO (631.876), GO (747.543 vs. release 748.443) and EDIT MEM. BLACKOUT and SOLO are toggles (two
  clicks: on, off). The bumps flash while held (output on press, off on release).

## The console's answer to the events

| Control (code / index) | What the console reported (first payloads) | Matches the physical control? |
|---|---|---|
| Channel fader 1 (0x00) | FaderInfo record 1 byte 0, ChannelLevels[1], OutputLevels[2] (DMX 1), LED block 1 G **steady** (`00 FF 00 00 FF 00`, seq 9812), LCD cell `<6>` at full (seq 9801) | Yes, as E3's physical fader 1 (F82, F83, F86). The rig on DMX 5–16 showed nothing; the owner saw bump 1 lit |
| Bump 7 (0x06), held | OutputLevels[8] (DMX 7), FaderInfo record 7 (body[25]), ChannelLevels[7] and LED block 7 G → 0xFF and the LCD cell body[9] → 0x86 while held; back to 0x00 / 0x20 at release (seq 19062 … 19078, 19445 … 19461) | **The same body bytes changed to the same values as at E3b's physical channel bump 7** (f40360 … f40392, F97), at BUMPS 0xFF; the fixture lit and dimmed (owner) |
| MASTER (0x30) | FaderInfo body[194] 0xFF → 0x7E → 0xFF; LED block 53 `37 37 FF 0A 0A 0A` from seq 24130 to seq 26560 | As the physical MASTER (F82, F85); all levels were 0, so the scaling (F96) was not exercised |
| BUMPS (0x31) | FaderInfo body[193] 0xFF → 0x00 → 0xFF; nothing else | As the physical BUMPS (F95) |
| BLACKOUT (0x34) | LED block 53 `00 00 FF 00 00 FF` (seq 33551) and back (seq 35238); nothing else (all levels 0) | As F114; the owner saw the console's BLACKOUT key lit |
| SOLO (0x33) | LED block 52 `FF`×6 (seq 36395) and back (seq 37736) | As F100 |
| MEMS (0x39) / 1-24 (0x36) | ConsoleStatus body[0] 0x00 ↔ 0x01 with the memory map, blocks 55 / 58 (seq 39479 / 39485, 41458 / 41462) | As F87, F98 |
| Memory bump 1 (0x00) in memory mode, held | FaderInfo record 1 `FF FF`, LED block 1 `FF 00 00 FF 00 00`, OutputLevels[8], [9], ChannelLevels[7], [8] (DMX 7, 8), LCD cells 9, 10 (seq 39603 … 39622); back at release (seq 39845 … 39861) | **The same body bytes changed to the same values as at E3b's physical memory bump 1** (f60332 … f60370, F97) |
| GO (0x31), STACK unlit | List [3, 22]: SequenceMain (unchanged, seq 43877), block 50 blue (seq 43881); PlaybackInfo record 5 Live / Next `FF 00` → `F7 08` … `02 FD` → `FF 00` with the times 5.0 s counting down (seq 43888 … 44324); **record 5 bytes 0–1 stayed `02 02`**; block 50 off (seq 44335); no level object changed | As E3b's GO with STACK unlit (F101); the owner: "lights nothing and console nothing" |

- **No acknowledgement and no echo:** the reply to the poll after a write is an ordinary idle reply or list; ConsoleEvent
  (20) was never listed or fetched; nothing names the PC as the source of a change.
- **The physical faders did not follow (they are not motorised: they stay where the hand left them, F120, F128;
  OBSERVED via the data):** FaderInfo record 1 reported
  the PC's level 0xFF with the physical fader 1 at zero (starting position), and bump 1 stayed **steady**, not blinking
  (seq 9812 … 13331): the console did not treat the physical fader as unmatched.
- **Matching and pick-up (OBSERVED, small cases; HYPOTHESIS H81 for the rule):**
  - The PC's channel-fader-1, MASTER and BUMPS events were applied at once, from the first event, whatever the console's
    level and the previous byte (above), also after channel 1 had dropped to 0 at 279.5 s (`FC FF`, `E2 FC`, `B7 E2` …
    → record 1 `85` at seq 14048).
  - Inside EDIT MEM (blind edit) the PC's fader 7 changed the edited level only **after reaching the stored level**:
    memory 1's channel 7 (stored 0xFF) stayed 0xFF on LED block 7 while the PC fader rose `02 … FC FF`, then followed it
    down (`EA` seq 67357 … `7F` seq 67486); memory 7's channel 7 (stored 0x00) stayed dark while the PC fader fell `6A …
    00`, then followed it up (`4D`, `CF`, `FF`, seq 68683 … 68717). This is the manual's matching rule (F8, DOCUMENTED)
    applied to the PC's fader.
  - After the second EDIT MEM store (channel mode again), the PC's fader 7 moving `EF FF` … `00 00` changed **no** level
    object; LED block 7 blinked dim green `00 46 00 00 0A 00` (seq 69078) and went dark when the PC fader reached 0
    (seq 69101): the unmatched-fader blink (H79), here for a PC fader.
  - The physical fader 5 inside the edit of memory 9 moved the edited level at once (it stood at the stored 0x79) and
    FaderInfo was **not** listed during the edit (LED block 5 and the LCD only, seq 78905 … 79676).

## Whole-object writes

### ChannelText (9): the label of channel 7, and H40

| Write | Seq / frame | Payload (43 bytes) | Reading |
|---|---|---|---|
| pseq 26, 956.232 | 57436 / 57437, f114873 | `1A 27 00 09` + `06 03 06` + 36 × `00` | Channel index 6 (channel 7), the empty label, **byte-identical to the console's own ChannelText 6 body of the download** (seq 468, f936). Sent 2.0 s before the rename's stamp: SmartSoft wrote the unchanged label when the cell was opened or committed (INFERRED) |
| pseq 27, 963.130 | 57880 / 57881, f115761 | `1B 27 00 09` + `06 03 06` + **`01 0C 00 61 00 6A`** + 30 × `00` | "Čaj": U+010C (capital C with caron, as the owner typed it), U+0061, U+006A, **UTF-16 big-endian right after `03 06`** |
| pseq 28, 1005.427 | 60604 / 60605, f121209 | `1C 27 00 09` + `06 03 06` + `00 00 00 61 00 6A` + 30 × `00` | The "restore": only the first code unit was zeroed; 'a', 'j' stay behind it. Read up to the first 0 the label is empty, but the block is **not byte-identical** to the original |

- **H40 for SmartSoft's write: reading A.** Reading A (18 UTF-16BE code units after `03 06`) gives "Čaj"; reading B
  (CL-ML: a pad byte `00`, then UTF-16LE lines) would need body[3] = 0x00 and `0C 01`, and gives a non-zero "pad" byte
  0x01 and U+000C, 'a', 'j'. The H40 test predicted `01 0D` for "č" under A; the owner typed the capital "Č", U+010C,
  `01 0C`: the same test. OBSERVED once.
- **The console side stays open:** the console listed nothing after any of the three writes (no ChannelText, no
  DisplayContent change; the owner thinks the 1248 shows labels nowhere), and the label was restored before any second download.
  That the console stores and returns the block as written is HYPOTHESIS H84.
- The write uses the same 39-byte body as the console's ChannelText (F67): `[a = channel − 1][03][06][36 bytes]`, the
  whole object.

### No Memory (0) write; what "Mem Edit" did

The script asked for SmartSoft's MEM EDIT (step 12), and S11.15 row 6 expects a whole-object Memory (0, page, slot) write
from that tab (INFERRED). **No Memory write occurred.** Both Mem Edit steps went through the console's own EDIT MEM
function by key and fader events, the keys 1.2–1.6 s apart with 46–124 ms clicks (human pace): MEMS, 1-24, EDIT MEM, bump 1,
fader 7 to 0x7F, EDIT MEM; then EDIT MEM, **bump 7**, fader 7 to 0xFF, EDIT MEM. Which SmartSoft window produced them
(the Simulator or the Mem Edit tab) is not visible in the bytes; the pacing suggests the Simulator (INFERRED; question to
the owner). The Memory whole-object write of S11.15 stays untested.

### Playback tab, "all faders to zero"

The click (866.081) produced **no transfer besides polls and no list** up to the next write (956.2). All Simulator
channel faders stood at zero then (fader 1 since 329.7 s). Whether the button sends anything when a fader is up is
UNKNOWN.

## The record functions driven from the PC

| Step | Keys (PC) | What the console showed and listed | Reading (OBSERVED unless marked) |
|---|---|---|---|
| REC MEM (channel 5 at 0x79 on the physical fader, channel mode) | 0x41 | LCD "Record Memory Pg:1" / "Select Bump or Stack" (seq 73013); LED: recorded 1–8, 13 `46 00 00 0A 00 00`, empty 9–12 and 14–24 (21 included) `C8 00 00 0A 00 00`, block 60 `C8 C8 00 0A 0A 00`, 65 `FF FF FF 0A 0A 0A`, 66 `FF 00 00 FF 00 00`, channel 5's G off (seq 73019) | The F116 REC MEM screen, from a PC key |
| + bump 9 | 0x08 | List [0:0/8/0, 22, 21] (seq 74003); **Memory (0, 0, 8)** body `00 08 01 60` + channel 5 = **0x79** + all other levels 0 + an empty label block (seq 74007, f148014); back to the channel view (seq 74011, 74015) | REC MEM stored the live level (0x79 = FaderInfo record 5, ChannelLevels[5], OutputLevels[6]; MASTER at full, so pre- or post-MASTER is open, H85) and **the console listed the new Memory object by itself** |
| (MEMS, unlogged) | 0x39 | Memory map: block 9 `46 00 00 46 00 00` (recorded); block 5 `46 00 00 0A 00 00` blinking (channel fader 5 up in memory mode, H79), record 5 → 0 (seq 75736, 75740) | The owner's "memory 9 recorded = y" |
| EDIT MEM (memory mode) | 0x42 | LCD "Edit Memory" / "Select Bump or Stack"; recorded memories (now 1–9, 13) `C8 00 00 0A 00 00` blinking, empty `46 00 00 46 00 00`, block 67 `FF 37 37 0A 0A 0A` (seq 76636, 76642) | F116's EDIT MEM screen |
| + bump 9 | 0x08 | List [23, 39:5/5/8, 23, 14, 22, 21] (seq 77854): **ConsoleStatus body[0] → 0x00** (channel mode for the edit, seq 77858), **System (39) (5, 5, 8)** body `05 05 08` (seq 77862), FaderInfo record 5 → 0x79, LED block 5 G = 0x79 (memory 9's content), LCD "Edit:" / "01/09" (seq 77878) | Memory 9 selected; the edit shows the memory's levels on the channel bumps |
| fader 5 (physical) to full | — | LED block 5 G and the LCD cell follow (0x82 … 0xFF, seq 78905 … 79676); **no FaderInfo, ChannelLevels or OutputLevels** | A blind edit |
| CLEAR | 0x38 | List [23, 14, 21, 22] (seq 80941): body[0] → 0x01 (memory mode, the mode before EDIT MEM), record 5 → 0, LCD back, memory map (seq 80947 … 80959); **no Memory listed** | **CLEAR cancelled the edit** (manual F107, DOCUMENTED: "If you press CLEAR instead, changes are aborted"); memory 9 unchanged (0x79, confirmed by the copy below) |
| COPY | 0x3E | LCD "Copy Memory/Seq" / "Select source"; recorded `C8 00 00 0A 00 00` blinking, 21 `C8 C8 00 0A 0A 00`, empty `0A 00 00 0A 00 00`, block 63 `FF`×6 (seq 82247, 82251); ConsoleStatus re-listed unchanged (seq 82241) | F116's COPY screen |
| + bump 9 (source) | 0x08 | LCD "Copy 01/09" / "Select target" (seq 83198); LED: recorded dim `46 00 00 0A 00 00`, empty and 21 bright `C8 00 00 0A 00 00` (seq 83205) | The target-selection screen (new) |
| + bump 10 (target) | 0x09 | List [0:0/9/0, 22, 21] (seq 84664); **Memory (0, 0, 9)** = memory 9's levels and label (channel 5 = 0x79; seq 84668, f169336); map with block 10 recorded (seq 84672) | COPY wrote memory 10 and the console listed it |
| "delete memory 9": EDIT MEM, bump 9, CLEAR (fader 5 at zero) | 0x42, 0x08, 0x38 | System (5, 5, 8) again (seq 91079, unchanged body); LED block 5 G = **0x79** (the stored level, not the fader's 0) and LCD "Edit: 01/09" (seq 91087, 91091); after CLEAR (seq 92792 … 92806) **no Memory listed** | Not deleted: the edit started from the stored level and CLEAR cancelled it |
| "delete memory 10": the same | 0x42, 0x09, 0x38 | System (5, 5, 9) (seq 95553), LED block 5 G = 0x79, LCD "Edit: 01/10" (seq 95568); after CLEAR (seq 96069 … 96083) no Memory | Not deleted |
| The two Mem Edit steps | see the action table | System (5, 5, 0) (seq 67109) and (5, 5, 6) (seq 68349); LCD "Edit:" / "01/01" (seq 67124) and **"Edit:" / "01/07"** (seq 68364); at each EDIT MEM store the LCD read "SmartFade Menu" on line 2 for about 1.4 s (seq 67562 → 67655, 68842 → 68935) before the fader view; **Memory (0, 0, 0)** listed with channel 7 = 0x7F (seq 67552, f135104) and **Memory (0, 6, 0)** with channel 7 = 0xFF (seq 68832, f137664) | The second edit went into **memory 7**, the console's own LCD named it |

What this tells our UI (OBSERVED once): after a recording the console **lists the Memory object (0, page − 1, slot − 1)
by itself** within 18–98 ms of the storing key (REC MEM + bump, EDIT MEM store, COPY target), with the full 138-byte body;
selecting a memory in EDIT MEM lists System (39) (5, 5, slot − 1); CLEAR inside a function lists no Memory. The
157-record list never recurred. A client that only follows the lists (SC-1) therefore learns a new or changed memory
from the console's own listing.

## Side effects on the show

| Show object | Before (download, seq) | Last state in the capture | After the capture (owner statements, not in the capture) |
|---|---|---|---|
| Memory 1, page 1 (`GROUP 1` "OPONA") | channels 7, 8 = 0xFF (seq 252, f504) | channel 7 = **0x7F**, channel 8 = 0xFF (seq 67552) | The owner set channel 7 back to 100 % by hand |
| **Memory 7, page 1 (`GROUP 7` "NOC 3 / MORE")** | channel 15 = 0x15 only (seq 276, f552; `SHOW07.ASC` `CHAN 15/8`) | channel 7 = **0xFF** added, channel 15 = 0x15 (seq 68832) | **Not mentioned by the owner: probably still changed on the console** |
| Memory 9, page 1 | empty (not in the download) | channel 5 = 0x79, label empty (seq 74007) | Deleted by the owner |
| Memory 10, page 1 | empty | channel 5 = 0x79, label empty (seq 84668) | Deleted by the owner |
| ChannelText 6 (channel 7's label) | empty, all zero (seq 468) | written as `00 00 00 61 00 6A` + zeros (pseq 28): reads empty, not byte-identical (UNKNOWN whether the console stored it so) | — |

- **Owner action needed (zero risk): check memory 7 of page 1.** Its channel 7 should not be at full (`GROUP 7` of
  `SHOW07.ASC` holds only `CHAN 15/8`). Either look at it on the console (EDIT MEM, bump 7: channel 7's bump would light;
  leave with CLEAR) or save the show to the SD card and compare `GROUP 7` with `SHOW07.ASC`. The same save would show
  whether memories 9 and 10 are gone and memory 1 is back to `CHAN 7/100 8/100`. **Resolved 2026-10-31 (after E9b):**
  `SHOW08.ASC` confirmed the change (`GROUP 7` = `CHAN 7/100 15/8`, `GROUP 1` = `CHAN 7/100 8/100`, no `GROUP 9` or `GROUP 10`,
  nothing else different from `SHOW07.ASC` besides the three stack cues that carry memory 7); the owner removed channel 7 from
  memory 7 by hand and `SHOW09.ASC`, saved afterwards, is byte-identical to `SHOW07.ASC`.
- The memory 1 complaint is explained: the restore step selected bump 7 (key 0x06) after EDIT MEM, so it edited memory 7
  and left memory 1's channel 7 at 0x7F (the console's LCD read "Edit: 01/07", seq 68364).

## Hypotheses touched

| H | Verdict (OBSERVED at most) | Key evidence (E9 seq / frame) |
|---|---|---|
| **H5** PC-side control is sent as ConsoleEvent | **PC → console part CONFIRMED (OBSERVED once):** every Simulator control used (channel faders 1 and 7, MASTER, BUMPS, bumps 1, 7, 9, 10, BLACKOUT, SOLO, MEMS, 1-24, GO, REC MEM, EDIT MEM, COPY, CLEAR) left the PC as ConsoleEvent (id 20) type-1 writes, 396 in all; the console answered with its state objects. The mirroring part was already CONFIRMED | 396 writes; seq 9664 … 96075 |
| **H6** no "set channel level" message | **Supported, CONFIRMED for the paths used (OBSERVED once):** channel 1 and 7 levels, the MASTER and BUMPS were set only by kind-0 events; no level object (13, 14, 15, 17) and no Memory object was written; the only object writes were ChannelText | inventory |
| **H24** ConsoleEvent body | **CONFIRMED (OBSERVED once) for kinds 0 and 1:** kind 0 `[00][index][value][previous]` (len 4), kind 1 `[01][code][1 / 0]` (len 3), press and release as separate writes; previous = SmartSoft's last value of the index (0 at start; 327 of 328, one exception consistent with the coalescing of S11.14.4, INFERRED). Kind 2 not exercised (the wheel was excluded by the owner's decision) | seq 9665, 19055, 16560 |
| **H40** label byte order | **SmartSoft's write: reading A (UTF-16BE), OBSERVED once; reading B contradicted for the write.** The console's stored / returned body after a non-ASCII write was not observed (successor H84); the 6-character third line was not tested | f115761 |
| H51 page select System `[0A][page][00]` | **Untested:** no page change, no System write besides the pair | inventory |
| **H72** key LEDs | **Supported and extended:** the PC's named key codes changed blocks 50 (GO), 52 (SOLO), 53 (BLACKOUT), 55 (1-24), 57 (CLEAR: blinking in a function, steady when CLEAR left it, as in E8b), 58 (MEMS), 63 (COPY), 66 (REC MEM), 67 (EDIT MEM); every code is the block number − 1 (H82). Block 57's identity as the CLEAR LED gains the code relation but CLEAR still never changed it outside a function: stays INFERRED; 60 (STACK), 64 (REC SEQ), 65 (MAGIC), 72, 73 not pressed | the codes table |
| H64 block 50 = GO LED | Supports (blue during the GO fade from a PC key) | seq 43881, 44335 |
| H76 PlaybackInfo bytes 0–1 | Supports (GO with STACK unlit: record 5 bytes 0–1 stayed `02 02` through the fade, as E3b's `06 06`) | seq 43888 … 44331 |
| H78 blocks 25–48 colour | Supports: `0A 00 00 0A 00 00` (dim red) in all 101 LedStatus payloads, no power cycle since E8b's memory-mode power-on | `blk2548.py` |
| H79 bump blink while unmatched | Supports: block 5 `46 00 00 0A 00 00` in memory mode with channel fader 5 up (seq 75740, 80959, 84672); block 7 `00 46 00 00 0A 00` while the PC's fader 7 came down unmatched after the edit (seq 69078 → 69101); block 5 `00 46 00 00 0A 00` briefly while fader 5 was pulled down after the mode switch (seq 87676) | as cited |
| H65 connect list | Supports (another history: 25 records with repeats; FileName after the SD save, INFERRED) | seq 8 |
| H31 / F93 counter | Supports: not reset across a change of host (E8b → E9, 244 → 245) | seq 8 |
| H30 / F58 / F92 System pair | Reproduced in a sixth session (#40, #79, byte-identical; the 157-record list byte-identical); causality still INFERRED; SmartSoft's behaviour only | seq 137 … 224 |
| H58 show objects only when listed | Extended: the console lists Memory objects after stores and System (5, 5, slot) at an EDIT MEM selection by itself; SequenceMain at GO again | seq 74003, 67101, 43873 |
| H53 ConsoleStatus body[0] | Supports with named keys (MEMS → 1, 1-24 → 0; EDIT MEM + bump → 0; CLEAR out of the edit → the previous mode) | `cst_led.py` |
| H75 ZLP | Untested (no IN length a multiple of 64; largest list 157 records, 792-byte payload) | — |
| H77, H80 | Untested (no output outside the bank; no crossfader move) | — |

## Golden-test candidates

**Outbound (SmartSoft's own bytes; the only admissible golden evidence for any future encoder, none of which is cleared):**

| seq (frame) | Bytes | Content |
|---|---|---|
| 137 / 138 (f273 / f275), 220 / 221 (f439 / f441) | `01 00 07 00 00 …` + `00 03 00 27 09 00 00`; + `01 03 00 27 07 00 00` | The System pair, sixth session (decoder or replay tests only) |
| 9664 / 9665 (f19327 / f19329) | `01 00 08 00 00 00 00 00 00 00 00 00` + `02 04 00 14 00 00 05 00` | First kind-0 event, channel fader 1 |
| 24114 / 24115 (f48227 / f48229) | `01 00 08 00 …` + `58 04 00 14 00 30 FD 00` | MASTER, previous 0x00 |
| 29433 / 29434 (f58865 / f58867) | `01 00 08 00 …` + `AE 04 00 14 00 31 FD 00` | BUMPS |
| 16559 / 16560 (f33117 / f33119) | `… 44 04 00 14 00 00 52 5D` | The one previous-byte exception |
| 19054 / 19055, 19435 / 19436 (f38109, f38871) | `56 03 00 14 01 06 01`, `57 03 00 14 01 06 00` | Key press / release (bump 7) |
| 33541 / 33542 (f67083) | `0A 03 00 14 01 34 01` | BLACKOUT press |
| 57880 / 57881 (f115761) | `1B 27 00 09 06 03 06 01 0C 00 61 00 6A …` | ChannelText "Čaj" (UTF-16BE) |
| 60604 / 60605 (f121209) | `1C 27 00 09 06 03 06 00 00 00 61 00 6A …` | The restore with residue |
| 31873, 31879 (f63745, f63757) | pseq `FF` then `00` | The outbound counter's wrap |

**Inbound (the console's bytes, for decoders):** seq 8 (f16) the 25-record connect list (pseq 0xF5); seq 74003 / 74007
(f148006 / f148014) list naming Memory (0, 0, 8) and its body; seq 84664 / 84668 (f169328 / f169336) Memory (0, 0, 9);
seq 67548 / 67552 (f135096 / f135104) Memory (0, 0, 0) with 0x7F; seq 68828 / 68832 (f137656 / f137664) Memory (0, 6, 0);
**seq 68341 (f136682) the list `[23, 39:5/5/6, 23, 22]` with c = 6** and seq 68349 (f136698) System body `05 05 06`;
seq 9684 … 9830 FaderInfo record 1 following the PC; seq 24457 MASTER 0x7E; seq 39603 … 39622 memory bump 1 (the same
changes as E3b f60332 …); seq 19062 … 19078 bump 7 (the same changes as E3b f40360 …); seq 68364 the LCD "Edit:" / "01/07"; seq 67562 the
LCD "SmartFade Menu"; seq 83198 "Copy 01/09" / "Select target".

## For the curator (proposed; nothing applied here)

**Status changes and annotations:**

| Id | Current | Proposed | Evidence (E9) |
|---|---|---|---|
| H5 | Mirroring CONFIRMED; PC → console part HYPOTHESIS | **PC → console part CONFIRMED (OBSERVED once):** SmartSoft's virtual controls are ConsoleEvent writes | inventory; F131–F134 |
| H6 | OPEN (supported statically) | **CONFIRMED (OBSERVED once) for the paths used**; the Memory whole-object write of S11.15 row 6 untested | inventory |
| H24 | OPEN, low | **CONFIRMED (OBSERVED once) for kinds 0 and 1**; kind 2 untested; the 1248 indices and codes OBSERVED for those named (F132, F133) | seq 9665, 19055 |
| H40 | HYPOTHESIS (A or B) | **Reading A OBSERVED for SmartSoft's write (UTF-16BE); B contradicted for the write**; keep HYPOTHESIS for the console's stored body until a re-download shows it (H84); the 6-character third line untested | f115761 |
| H72 | CONFIRMED in capture for 60, 63, 64, 66, 67, 72, 73; 57, 65 INFERRED | Add: 50, 52, 53, 55, 58 tied to named PC key codes; the code = block − 1 relation (H82); 57 stays INFERRED (supported) | codes table |
| H64, H76, H78, H79, H65, H53, H58 | as listed | Supported again (annotations) | Hypotheses table |
| H51 | UNTESTED | Unchanged | — |
| F52 | VERIFIED | Annotate: 401 data headers carry the stale words too (52 858 of 52 858 poll and data headers after the first) | `summary` |
| F54 | VERIFIED | Annotate: the outbound layout for ids 20 and 9 (401 payloads, len = w1 − 4, w1 = 7, 8, 43); OBSERVED once for those ids | inventory |
| F55 | VERIFIED in part | Annotate: the outbound counter +1 per data payload over 401 writes and all ids, wrapping 0xFF → 0x00 (f63745 → f63757); inbound 245 … 114 continuing E8b (no power cycle) | `summary` |
| **F57** | VERIFIED ("c = 0 in every record") | **Limit, not reject:** c = 0 in every record **of E2–E8b**; E9 has four records with c ≠ 0 (System (5, 5, 6 / 8 / 9)); the list format and the list → fetch correspondence hold (630 of 630) | seq 68341, 77854, 91071, 95545 |
| F58, F92 | VERIFIED as SmartSoft's behaviour | Annotate: sixth session, byte-identical, #40 / #79; still never clearance | seq 137, 220 |
| F59, F89, F94 | VERIFIED / VERIFIED in part / OBSERVED | Annotate (ConsoleVersion, the 157-record list once, byte-identical; a sixth whole start-up). **F94: VERIFIED proposed** for the structure (every SmartSoft session repeats the whole start-up; the connect list varies: E3b, E9) | seq 145, 224, 8 |
| F67 | VERIFIED for the ids seen | Annotate: ConsoleEvent (20) now seen PC → console (bodies 3 and 4 bytes), never console → PC; System (39) also with a = 5 | inventory |
| **F93** | OBSERVED | **VERIFIED proposed** (E3b + E9): the console's counter is not reset by a new session; E9 continued E8b's 244 with 245 across a change of host program | seq 8 |
| **F97** | OBSERVED (BUMPS 0xFF) | **VERIFIED proposed** for BUMPS 0xFF (E3b + E9): channel bump 7 and memory bump 1 changed the same body bytes to the same values (E3b f40360 … f40392, f60332 … f60370; E9 seq 19062 … 19078, 39603 … 39622), here from PC keys | as cited |
| **F103** | OBSERVED | **VERIFIED proposed** (E3b + E9): FileName = the name of the last SD save ("SHOW07.ASC" after the owner's save at 22:18:26) | seq 72 |
| F100 | VERIFIED in part (pattern) | Annotate: block 52 toggled with the PC's SOLO key (code 0x33, named, timed): the SOLO identity OBSERVED | seq 36395, 37736 |
| F101, F114, F116, F119, F130 | VERIFIED / VERIFIED in part / OBSERVED | Annotate: GO with STACK unlit reproduced from a PC key (record 5 `02 02` kept); block 53 with the PC's BLACKOUT; the REC MEM, EDIT MEM and COPY screens from PC keys; ConsoleStatus re-listed unchanged at every EDIT MEM selection and store (selections seq 67105 … 67113, 68345 … 68353, 77866, 91075 … 91083, 95549 … 95557; stores seq 67558, 68838); SequenceMain listed at a GO with STACK unlit (E3b, E8b, E9) | as cited |
| F107, F109 | DOCUMENTED | Annotate (OBSERVED once): REC MEM + bump stores the current look and returns to the previous mode; EDIT MEM + bump switches to channel mode and shows the memory blind; EDIT MEM stores; CLEAR aborts; COPY writes the target | record functions |
| F121 | INFERRED / DOCUMENTED | Annotate: none of the destructive paths occurred in a writing session (no show upload, no System erase, no FileName write) | inventory |

**New facts (proposed IDs; all OBSERVED, one session, unless stated):**

- **F131 The PC → console data write as SmartSoft sends it.** A type-1 header `01 00 [w1 LE] [w2 … w5 stale, F52]` is
  followed 0.05–0.32 ms later by one OUT transfer of w1 bytes `[pseq][u16 LE len = w1 − 4][id][body]` (w1 = 7, 8, 43 for
  3-, 4- and 39-byte bodies); SmartSoft's sequence byte rises by one per data payload across all ids, from 0x00 at session
  start, wrapping 0xFF → 0x00 (401 writes, 0 gaps); a write replaces no poll: the loop's next poll or fetch follows
  14.5–16.5 ms later, and consecutive writes are at least 44.8 ms apart (one per loop iteration, INFERRED from
  S11.2). **No acknowledgement:** the reply to the next poll is an ordinary idle reply or DirtyObjects list, ConsoleEvent
  is never listed, and an object write is not listed back (ChannelText). USBD status 0 in all. Evidence: inventory;
  seq 9664 / 9665, 57880 / 57881; `summary`.
- **F132 ConsoleEvent kind 0 on the 1248.** Body `[00][index][value][previous]`, 4 bytes, 332 events. Index 0x00 = channel
  fader 1 (FaderInfo record 1, ChannelLevels[1], OutputLevels[2]), 0x06 = channel fader 7 (inside EDIT MEM: the edited
  channel 7), 0x30 = MASTER (FaderInfo body[194]), 0x31 = BUMPS (body[193]). The value is the console's level byte
  unconverted (0x00–0xFF). Previous = SmartSoft's own last value of the index, 0x00 for the first event of the session
  (4 of 4, also for the MASTER and BUMPS at 0xFF on the console), equal to the preceding wire value of the index in 327 of
  328 later events (exception pseq 0x44, seq 16560: a value that never reached the wire). Identical consecutive events are
  sent (56 of 332). Evidence: kind-0 bursts in the action table.
- **F133 ConsoleEvent kind 1 on the 1248: key codes.** Body `[01][code][state]`, 3 bytes, press (01) and release (00) as
  separate writes; 32 pairs. Codes: 0x00 bump 1, 0x06 bump 7, 0x08 bump 9, 0x09 bump 10, 0x31 GO, 0x33 SOLO, 0x34
  BLACKOUT, 0x36 FADERS 1-24, 0x38 CLEAR, 0x39 FADERS MEMS, 0x3E COPY, 0x41 REC MEM, 0x42 EDIT MEM, each named by a logged
  step; for all 13, code = LED block number − 1. The console acts on the press (BLACKOUT, SOLO, GO, EDIT MEM listed
  before the release); bumps flash while held; BLACKOUT and SOLO toggle per click. Evidence: the codes table.
- **F134 The console answers PC events as if the panel had been used.** The state objects follow the PC's controls with
  the same bytes as for the physical controls (bump 7 and memory bump 1: the same body bytes changed to the same values
  as at E3b's physical presses; channel fader 1 as E3's fader; MASTER, BUMPS, BLACKOUT, SOLO, MEMS, 1-24, GO with STACK
  unlit as F82, F85, F87, F95, F100, F101, F114), within 16–65 ms (first list after the event). The physical faders do
  not move and are not reported: FaderInfo record 1 read the PC's 0xFF with the physical fader 1 at zero, its bump LED
  steady. The DMX output followed (DMX 7 from bump 7: the fixture lit, owner). Evidence: "The console's answer to the
  events".
- **F135 Matching and pick-up with PC faders (small cases).** Channel fader 1, MASTER and BUMPS events took effect at once
  from the first event; inside EDIT MEM the PC's fader 7 changed the edited level only after reaching the stored level
  (0xFF in memory 1, 0x00 in memory 7); after leaving the edit, the PC's fader 7 (at 0xFF in SmartSoft) changed no level
  object while coming down and its bump blinked dim green until it reached 0. One unexplained drop of channel 1 to 0 at
  279.5 s without a PC write. Evidence: seq 14048, 67357 … 67486, 68683 … 68717, 69078, 69101, 13315 … 13335. H81.
- **F136 Record functions from the PC, and what the console lists.** REC MEM (0x41) + bump 9 stored the live look
  (channel 5 = 0x79) into memory 9 and the console listed Memory (0, 0, 8) at once; EDIT MEM (0x42) + bump k switched the
  console to channel mode, listed System (39) (5, 5, k − 1) with body `05 05 k−1`, showed the memory's levels on the
  channel bumps and the LCD "Edit:" / "page/memory" ("01/09", blind: no level object); a second EDIT MEM stored (Memory (0, 0, k − 1)
  listed, LCD "SmartFade Menu" for about 1.4 s); CLEAR cancelled (no Memory listed) and returned to the previous mode;
  COPY (0x3E) + bump 9 + bump 10 showed "Copy 01/09" / "Select target" and listed Memory (0, 0, 9) with memory 9's levels.
  An EDIT MEM session started from the stored levels whatever the faders' positions, so "EDIT MEM, bump, CLEAR with all
  faders at zero" deleted nothing. Evidence: "The record functions driven from the PC".
- **F137 The first DirtyObjects records with c ≠ 0.** System (39) records (5, 5, 6), (5, 5, 8) twice and (5, 5, 9) (seq
  68341, 77854, 91071, 95545; and (5, 5, 0), seq 67101); each fetched with its qualifiers and answered with a 3-byte body
  equal to them (seq 68349 … 95553). In E2–E8b c was 0 in every record (F57). **Consequence for SC-1:** `HspReadSession`
  ends a read-only session on a listed record with c ≠ 0; if an EDIT MEM selection on the console itself lists the same
  records (H83), selecting any memory other than memory 1 during a session of ours ends it with a fault (a safe stop, but
  a stop), while System (5, 5, 0) and the Memory objects listed after a recording would be fetched (ids 39 and 0 are in
  `ObservedListedIds`, their body lengths 3 and 138 in `ObservedBodyLengths`; the qualifier ranges are not enforced).
- **F138 The ChannelText write and the label encoding.** SmartSoft writes a label as the whole 39-byte ChannelText body
  `[channel − 1][03][06][18 UTF-16BE code units]` ("Čaj" = `01 0C 00 61 00 6A`, f115761); the unchanged empty label was
  written byte-identical to the console's own body; the "restore" zeroed only the first code unit (`00 00 00 61 00 6A`,
  f121209). The console listed nothing after any of the three writes. H40, H84.
- **F139 E9's side effects on the show (last state in the capture).** Memory 1 channel 7 = 0x7F (was 0xFF); memory 7
  channel 7 = 0xFF added (was absent; `GROUP 7` `CHAN 15/8`); memories 9 and 10 = channel 5 at 0x79; channel 7's label
  block with residue. The owner restored memory 1 and deleted memories 9 and 10 afterwards (statements); memory 7 is not
  mentioned. Evidence: "Side effects on the show".
- **F140 Opening the Simulator and the Playback tab's "all faders to zero" sent nothing** (174.885–204.893 and
  866.081–956.2 without a write or a list; all Simulator channel faders at zero at the click).

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H81 | A kind-0 ConsoleEvent sets the position of the fader with that index exactly like a physical move ("last mover wins"); the console's matching rule (F8, H79) applies to it after a change of what the fader controls (EDIT MEM entered or left, a mode or page change, CLEAR), and inside EDIT MEM the fader picks up the stored level when it reaches it | F134, F135 | E9b, logged: Simulator fader 1 to full, then touch the physical fader 1; physical fader 5 at half, then the Simulator fader 5 from 0 up; MEMS with a Simulator fader up; note the bump LED |
| H82 | Every 1248 key's ConsoleEvent code is its LED block number − 1 | F133 (13 of 13) | E9b: press, in the Simulator, keys not yet seen (STACK, IND 1, IND 2, FADERS 25-48, a bump in bank 25-48); each code must be observed, none derived |
| H83 | The console lists System (39) (5, 5, slot − 1) whenever EDIT MEM selects a memory, whatever the key's source (a = b = 5 in all five records, meaning UNKNOWN; whether a page other than 1 changes a or b is open) | F136, F137 (from PC keys only) | E9b: EDIT MEM + bump on the physical console (page 1), and once on page 12 (a page change on the console, not in SmartSoft), each left with CLEAR |
| H84 | The console stores a ChannelText write verbatim and returns it in the next download (the console side of H40) | F138 | E9b: label channel 7 "Čaj", close and restart SmartSoft (a new download) and read ChannelText 6; then restore and download again to see whether the residue persists |
| H85 | REC MEM records each channel's level before the MASTER (F107's simulator reading, INFERRED) | memory 9 = 0x79 with the MASTER at full (F136) | E9b: REC MEM into an empty memory with a channel at a known level and the MASTER at half; delete it afterwards with REC MEM of an empty look |
| H86 | REC MEM of an empty look (nothing raised) deletes a memory, and the console lists the memory with all levels 0 (or stops listing it) | Owner statement (log 22:47:56.657); manual F107 ("To delete a single memory edit it so all intensities are at zero", DOCUMENTED) | E9b: the corrected deletion step (script 6cb2c07) |

## Input for a future safety check (SC-2)

**E9 clears nothing.** It is the first evidence of what SmartSoft sends when it writes; it is one session; it changed show
data, one change unintended. Only a second session (E9b) can make any of the following VERIFIED, and VERIFIED would
still be necessary, not sufficient (CLAUDE.md). The record functions and the object writes change show data and must not
be considered for an encoder of ours on the strength of E9.

| Write class | Exact pattern seen (SmartSoft) | Effect on the console (E9) | Reversible? | To verify before any consideration |
|---|---|---|---|---|
| ConsoleEvent kind 0, fader | header `01 00 08 00` + stale words; payload `[pseq] 04 00 14 00 [index] [value] [previous]`; indices 0x00, 0x06, 0x30, 0x31 only | Sets the fader's level at once (channel 1 → DMX 1; the MASTER and BUMPS moved FaderInfo body[194] / body[193], whose effect on the outputs is F96 / F97, not exercised in E9 with all levels at 0); inside EDIT MEM it edits a memory's level, which the next EDIT MEM stores (show data) | Live state: by another event or by moving the physical fader (H81). Inside a record function: not by the fader alone | A second session; the indices of every fader to be whitelisted (only four seen); the previous byte's role; the matching rule (H81); behaviour in memory mode and bank 25-48; the console's state machine (a fader event must never be sent while a record function is open) |
| ConsoleEvent kind 1, key | header `01 00 07 00` + stale; payload `[pseq] 03 00 14 01 [code] [01 / 00]`; 13 codes seen | Live keys: bumps flash while held (outputs at the BUMPS level), BLACKOUT toggles (block 53; all levels were 0 in E9, and with a level up the physical key zeroes every intensity, F125), SOLO toggles, GO starts a 5-s fade, MEMS / 1-24 switch the fader mode. Record keys: REC MEM + bump stores the current look, EDIT MEM + bump + EDIT MEM stores an edit, COPY + bump + bump overwrites the target memory; CLEAR aborts | Live toggles by a second press; a bump by its release (**a lost release, i.e. a session ending between press and release, is untested**, ConsoleLink W5 for the ML). Record functions: **not reversible by the protocol** (re-record, or restore from the SD backup); a wrong bump edits another memory (E9's memory 7) | A second session; whitelist only live keys; the press / release pairing and a dead session between them; what an unknown code does (never to be tried); that no record key can be sent |
| ChannelText (9) | header `01 00 2B 00`; payload `[pseq] 27 00 09 [ch − 1] 03 06 [36 bytes]` | Replaces the channel's label (show data); nothing is listed back | Only by writing the exact old 36 bytes, which a read-only session never receives (ChannelText comes only in the 157-record download after the uncleared System `07 00 00`) or the SD file | A second session; the console's stored bytes (H84); length and character rules; out of scope for the owner's write mode (live controls and recording through the console's own keys) |
| System (39) `09 00 00`, `07 00 00` | As F58 | Starts the ConsoleVersion / 157-record download; meaning UNKNOWN (H30) | — | Not needed by a read-only session (H66); not part of live control |
| Memory (0) whole-object write | **Not observed** | — | — | A capture of the Mem Edit tab itself (E9b, owner's choice) |
| Show upload, System erase, page select, wheel, FileName | **Not observed** | — | — | Never (F121; the owner's decisions of 2026-10-30) |

Further observations a safety check needs:
- **No acknowledgement:** a sender learns a write's effect only from the state objects the console then lists (FaderInfo,
  LED, ConsoleStatus, Memory); an object write (ChannelText) produced no listing at all.
- **The console accepted events from a host that had sent the System pair** (SmartSoft's sessions always do). Whether it
  accepts ConsoleEvents from a host that never sent it (our read-only session, H66) is UNKNOWN and can only be learned by
  sending, which nothing clears.
- **The console never complained** about SmartSoft's sequence bytes or previous bytes; whether it checks them is UNKNOWN.
- **SC-1 itself is affected** (F137): an EDIT MEM selection from the PC listed System records with c ≠ 0, which our
  read-only session treats as a fault and stops on; whether the console's own EDIT MEM key does the same is H83.

## Surprises and anomalies

1. **The "restore" step edited memory 7, not memory 1** (key 0x06 after EDIT MEM; LCD "Edit: 01/07", seq 68364; Memory
   (0, 6, 0) with channel 7 = 0xFF, seq 68832). Memory 1 was left at 0x7F (the owner noticed and fixed it by hand);
   **memory 7 probably still holds channel 7 at full** (to be checked by the owner, "Side effects"). **Resolved 2026-10-31: it did
   (`SHOW08.ASC`); restored by hand, `SHOW09.ASC` equals `SHOW07.ASC` byte for byte.**
2. **The guide's deletion step deleted nothing:** EDIT MEM + bump starts from the stored levels and CLEAR aborts the edit
   (no Memory listed, seq 92792, 96069), as the owner said and as the manual says (F107). Script fixed in 6cb2c07.
3. **No Memory whole-object write:** both "Mem Edit" steps went through the console's EDIT MEM by key and fader events;
   S11.15's Memory write path is still untested.
4. **The first records with c ≠ 0** (System (5, 5, slot)), which SC-1's session would stop on (F137).
5. **An unexplained drop of channel 1** at 279.5 s with no PC write (seq 13315 … 13335).
6. **The restored label is not byte-identical** to the original (residual 'a', 'j' after a leading 0, f121209).
7. **The console was in memory mode at connect** although the starting position asked for channel mode; it was switched
   on the console during the baseline (115.6 s), not by a write.
8. **The previous byte once named a value that never reached the wire** (pseq 0x44): SmartSoft's coalescing or a race,
   INFERRED.
9. Unlogged: SmartSoft's fader 1 moved again during the owner's answer (290.5 s), a MEMS press before the "memory 9
   recorded" answer (1236.8 s), the physical fader 5 moved 8.5 s before its step (1285.8 s), FADERS 1-24 pressed on the
   console before "fader 5 to zero" (1420.4 s).
10. The `etchsp.sys` version and the firmware were not re-read; the Windows build was not restated.

**Runbook deviations:** the Mem Edit steps used the console's EDIT MEM (not a Memory write) and the restore picked memory
7 (1, 3); the deletion instruction was wrong (2); the console was not in channel mode at connect (7). Otherwise the E9
runbook was followed: the named backup (`SHOW07.ASC`, copied, byte-identical to SHOW04–06), ETC's driver on interface 1,
Memory Integrity off, USBPcap before SmartSoft, the timed log, the waits between actions, no wheel, no REC SEQ, no patch,
settings, page, show or erase action.

## Next capture to take

**A. Owner, zero risk:**
1. Memory 7 of page 1: is channel 7 at full? (EDIT MEM, bump 7, look at channel 7's bump, CLEAR; or save SHOW08.ASC to the
   SD card and copy it into `smartfade-sd-card-backup/`, so that `GROUP 1`, `GROUP 7` and the absence of memories 9 and 10
   can be compared with `SHOW07.ASC`.)
2. Step 12: which SmartSoft window did you use, the Console Simulator or the Mem Edit tab? Did you press the bump under
   fader 7 in the restore step, thinking of channel 7?
3. At about 22:24:39 (279.5 s), while answering the fader-1 question: did you touch the physical fader 1 or another key?
4. At about 22:24:50 (290.5 s): did you move the Simulator's fader 1 down and up again?

**B. E9b, the repeat (passive, SmartSoft, after the corrected guide 6cb2c07), for VERIFIED and for H81–H86:**
- The same live steps (fader 1, bump 7, MASTER, BUMPS, BLACKOUT, SOLO, MEMS with memory bump 1, 1-24, GO with STACK
  unlit) to reproduce F131–F134.
- H81: Simulator fader 1 to full, then the physical fader 1 touched; the physical fader 5 at half, then the Simulator
  fader 5 from zero; MEMS with a Simulator fader up.
- H82: Simulator presses of keys not yet seen and safe for the rig: STACK (then off), IND 1 and IND 2 (IND 2 drives DMX 47
  only, IND 1 nothing, F115), FADERS 25-48 and back.
- H83: EDIT MEM + bump on the physical console (page 1), and once on page 12 (page changed on the console), left with
  CLEAR.
- H84: the "Čaj" label, then a SmartSoft restart (new download), then the restore and another restart.
- H85, H86: REC MEM into memory 9 with channel 5 at a known level and the MASTER at half; delete it with REC MEM of an
  empty look (the owner's method); watch what the console lists.
- Optionally (owner's choice) the Mem Edit **tab** on memory 9 of page 1 (a cell edit), to see whether a Memory (0) write
  exists at all; then delete memory 9 again.
- As before: backup first (SHOW08.ASC), USBPcap before SmartSoft, the split before the commit, no wheel, no REC SEQ, no
  patch / settings / page / show / erase action in SmartSoft.

**C. Code questions for the coordinator (no change made here):** F137 (c ≠ 0) against `HspReadSession`'s fault rule in
SC-1 sessions; the Memory objects a read-only session would now receive after console-side recordings.

**D. Tooling (zero risk):** commit the ad-hoc read-only E9 views (writes per step, kind-0 bursts with the previous-byte
check, kind-1 press / release pairing, LED blocks per window, show objects after the download, list → fetch check) under
`tools/protocol-analyzer/e9/`, as was done for E8b; add the normalization command above to the analyzer README.

**E9 clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no type-1 data header may
be encoded anywhere in the solution.
