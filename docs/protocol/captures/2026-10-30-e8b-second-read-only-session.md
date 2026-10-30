# 2026-10-30 E8b: second read-only session of our own software (WinUSB, SC-1, USBPcap, timed action log)

- **Date:** 2026-10-30. The session ran 20:38:03.245–20:56:44.475 local time (CET, UTC+1). The backend log's
  `captureStart` is 2026-10-30T19:38:03.1471456Z; the first poll (seq 1) was submitted at t = 0.097813 s and the last
  transfer (seq 61112) completed at t = 1121.328003 s. USBPcap ran from 20:35:46.399930 to 20:57:15.924697 (the first
  and last frames of the raw root-hub capture); the committed console-only split runs from 20:35:46.399930 (f1) to
  20:56:44.475112 (f125932). The guided script ran 20:31:48.751–20:57:23.705.
- **Experiment:** **E8b**, the second read-only session of our own software, cleared by SC-1
  ([../safety-checks.md](../safety-checks.md)), run with `tools/usb-probe/e8b-guided-session.ps1` (commit c783223) after
  the "E8b runbook" ([../reverse-engineering.md](../reverse-engineering.md)) and item B of the E8 record's "Next capture to
  take". **Active:** our code sent the poll and the fetch of a listed record and nothing else: 29 799 OUT transfers, all
  12-byte headers (27 484 polls, 2 315 fetches). No System write, no data header, no payload, no control transfer and
  nothing on interface 0; the bus capture confirms it (section "USBPcap"). It was **not** a SmartSoft session.
- **Console:** ETC SmartFade 1248, USB device address 10 on bus 1 (E3b: 7).
  - Firmware **not re-read for E8b.** The owner's Diagnostics reading of the same evening is 3.0.2.9.0.43 (E8
    statements, answer 5; F48). A read-only session cannot fetch ConsoleVersion (F58, F59).
  - **Power-cycled before the first Connect** (log 20:32:52.483 "power cycle before E8b = y", 20:33:28.105 "console
    ready for E8b"). It had not been switched off between E8 and that power cycle (log 20:32:45.329 "console powered off
    since E8 = n").
- **Host:** the owner's PC (Windows 11 Pro build 26300 per the earlier records; not restated for E8b).
  - Backend `SmartFade.Api` at commit c783223, started by the script at 20:36:56.035 with `--Device:Mode=Usb
    --Device:Usb:ConfirmDate=2026-10-30`. The log header's `host.version` is
    `1.0.0+c783223cb9ad9c803ef240adcbf2cb49a79117e7`.
  - Cadence settings: the command line names only the mode and the date, so the defaults applied as far as known (poll
    interval 31 ms, poll after a fetch 15 ms, fetch delay 28 ms, floors 21 / 11 / 21 ms, reply timeout 500 ms).
  - Driver: WinUSB on interface 1, bound with Zadig **during the capture**. The script read the interface-1 service
    `ETCHSP` at 20:35:51.457 and `WinUSB` at 20:36:55.986. The pipe in the log header is
    `\\?\USB#VID_14D5&PID_0200&MI_01#7&3b1b95d&0&0001#{d222892d-f9ee-42e8-ae00-ece4a9d4055a}`; the interface GUID differs
    from E8's `{e54818a2-…}` (a new Zadig install, INFERRED). Interface 0 stayed on an HID driver (the HID class request
    SET_IDLE at the bind, f10, INFERRED).
  - Memory Integrity: not recorded. The UI (`npm start`, started by the script) showed the HARDWARE badge (owner,
    20:37:55.826).
- **Connection:** USB A-B cable straight into the PC (not restated). USBPcap root hub `USBPcap1` (INFERRED from the
  script's suggested command and the device addresses in the file).
- **Show backup (safety rule 3, SC-1 condition 3): taken.** The script's gate logged "backup taken: SHOW06.ASC" at
  20:32:30.275, before the power cycle, USBPcap and the bind.
  - `smartfade-sd-card-backup/SHOW06.ASC` is present (git-ignored folder; 15 944 B, SHA-256
    `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`) and **byte-identical to `SHOW04.ASC` and
    `SHOW05.ASC`** (checked when this record was written).
  - The owner: "show equals SHOW05 = y" (log 20:32:38.031); what was recorded during E8 was removed again (statements).
- **Parallel USBPcap (SC-1 item 8): ran**, started by the owner in an admin window before the Zadig bind (log 20:35:50.334
  "usbpcap running (owner)"; the capture's first frame is 20:35:46.40). The committed `.pcap` is the **console-only
  split** (device address 10) of that root-hub capture; the raw capture, which also holds device addresses 1–6, is not
  committed (Anomaly 1). This record reads only address 10.
- **Action log:** the guided script's timed log (133 lines). It runs on the PC clock in local time, the same clock as
  the backend's `ts` and as USBPcap (the backend and the bus agree within 0.75 ms, section "USBPcap").
  - Stamp semantics (script): a `Step` line is stamped when the owner presses Enter "at the moment you START doing this",
    a `Confirm-Done` line after the action, a `wait … done` line after the wait.
  - The data put most `Step` actions 0.35–2.6 s after their stamp. Four came **before** it: Connect (47.5 s before),
    memory mode (7.1 s), BLACKOUT off (0.4 s) and Disconnect (5.2 s). Offsets per step in the action table.
- **Rig:** the DOTZ BAR on DMX 5–16; nothing on DMX IN (owner, 2026-10-30; not restated).
- **Actions outside the script** (owner statements; their only clock is the console's data):
  - after the scripted "go with stack lit" (20:55:45.183): GO twice, crossfader A up and down, B up and down, both together,
    STACK off, CLEAR several times;
  - also seen in the data: the starting-position setup after Connect (21.8–34.7 s) and a short STACK action at
    447.99–448.67 s while the owner answered the STACK question.

**Conventions** (as in the E8 record, plus the bus):
- `seqN` is the record number in the backend's transfer log
  (`2026-10-30-e8b-second-read-only-session.jsonl`), the primary evidence of our session.
- `fN` is a frame number of the committed `.pcap`, the console-only split: the data-carrying frame, i.e. the submission
  frame for OUT and the completion frame for IN. The same numbers are in the `frames` field of the usbpcap JSONL. The
  split keeps the order of the raw capture, so frame k of the split is the k-th console frame of the raw capture.
- `t` is seconds since the backend's `captureStart` (20:38:03.147 local); local time = 20:38:03.147 + t. Log stamps are
  quoted with their t (for example "memory mode on" 20:40:31.479 = 148.332 s). The usbpcap JSONL counts its own t from
  its first frame (20:35:46.399930): t(usbpcap) = t + 136.747216.
- `body[n]`, LED block k (phase A / phase B, steady / blinking) and FaderInfo record N (bytes 0–3) as in the E8 record.
- Every quoted byte comes from the cited seq or frame. Readings carry their status: OBSERVED (seen in this session),
  INFERRED, HYPOTHESIS, DOCUMENTED.

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-30-e8b-second-read-only-session.jsonl` | `fa7c2e0a3f63b5122265035aa806f50135aa87e02e641c2574b72c68ac37cc8d` | 13 885 602 B, 61 116 lines: 1 header, 61 112 `frame` records, 3 `note` records | yes (0de6d17), **the evidence** | The backend's transfer log `2026-10-30-193803-usb-session.jsonl` (`SmartFade.Devices.Usb.TransferLog` version 1, `status` = `null`, no pcap frames). Notes: session start (t = 0.008802), "session stopped by the application at an idle point" (1121.328427), "polls 27484, fetches 2315, lists 1514, objects 2315" (1121.328471) |
| `2026-10-30-e8b-second-read-only-session.pcap` | `f653db8065cd5c8f809becafcf8f4a19a736c11c7e6e46226bec220a5a393ec2` | 6 485 015 B, 125 932 frames, 20:35:46.399930–20:56:44.475112 | yes, **the evidence for the bus** | Console-only split (device address 10) of the USBPcapCMD root-hub capture, classic pcap, link type 249, µs; made by the coordinator with `tshark -r <raw> -Y "usb.device_address == 10" -F pcap -w <split>`. **The raw root-hub capture is not committed**, because it holds the traffic of other devices (addresses 1–6, possibly keyboard or mouse input): SHA-256 `fcfa1a2511d8084141f0ad90b4a15fce5c2b676f13f949757d8a76e5eedc4dbf`, 13 525 304 B, 180 806 frames, 20:35:46.399930–20:57:15.924697 (1 289.524767 s), written by USBPcapCMD to `C:/Program Files/USBPcap/e8b.pcap` (statements). It was first committed under this name by mistake in 0de6d17 (Anomaly 1). Completeness of the split below |
| `2026-10-30-e8b-second-read-only-session.usbpcap.jsonl` | `0c317164720dc49adb39c1e4d54a59ddba38a75b02cfdde863aa62f9dad9b1f4` | 15 503 072 B, 63 104 lines: 1 header, 62 968 `frame` records, 135 `note` records | added with this record (derived) | The committed split, normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 10 --bus 1` (0 other-device frames). Notes: the 133 action-log lines (`--notes-log`, `--notes-utc-offset +01:00`; the first seven have negative t, they precede the capture) and two analyst notes (capture start with the raw capture's provenance, capture end). Two runs gave identical bytes. Command below |
| `2026-10-30-e8b-actions.txt` | `bdc3b0b8382b1df3ab4e5e49c1e475bbee47a2726e4ddc5e80d5dad494ad38c7` | 7 195 B, 133 lines, CRLF | yes (0de6d17) | The guided script's action log. Byte-identical to the untracked `e8b-actions.txt` in the repository root (the script's `-LogFile`) |
| `2026-10-30-e8b-owner-statements.txt` | `894eb5ad36f02bbf5caed840478b1760e47a9ceeea7b652245082aa4a13d66d7` | 3 106 B, 41 lines | yes (0de6d17, 311eda6, ed9364d) | Context (since ed9364d also the SHOW06.ASC hash and its identity with SHOW05.ASC), the owner's account of the unlogged actions after "go with stack lit", the typed answers. Owner statements never promote by themselves |
| `tools/usb-probe/e8b-guided-session.ps1` | `30aa0334b00dfd7764690993c636b3486d968f79952c8db811f74dc3ba4da163` | | yes (c783223) | The guided session. It talks only to the operator and starts the backend and the UI; it sends nothing to the console itself |
| `smartfade-sd-card-backup/SHOW06.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` | 15 944 B | no (folder git-ignored) | The backup saved at 20:32:30, byte-identical to `SHOW04.ASC` and `SHOW05.ASC` |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py` | `ea7627d7cdd0ff214f003f290ae7cb6b75b73149b4e753011c76b129efd20fe8` | | yes | Normalizer version 2, unchanged since E3 |
| `tools/protocol-analyzer/hsp_summary.py` | `3f324e9f94732235d5cbaa0ea6ce76619db4b1357612de5828db1ccfcb0fd1ec` | | yes | All modes used here run on the backend's log (`summary`, `census`, `lcd`, `payloads`); `payloads` piped into `head` stops with an `OSError` on Windows (broken pipe, not a decoding error) |
| `tools/protocol-analyzer/poll_rules.py` | `b54cf6d24e01469b820312f75762bc7d1e873797d1e0c109fcda3ae244dc457e` | | yes | Stale-word rule and cadence |
| `tools/protocol-analyzer/e8/e8_health.py`, `e8_leds.py`, `e8_ledstate.py`, `e8_timeline.py` | `45a09a6401c511f385d00a2f053680bc1aca019631ab28871eb7b3c3d9118c88`, `30e5c5e42c36b25978126803751c4e9288ae50236614ce4ae39b918b26616e3c`, `8be06f773e869de9062f5de61a3816e97f03d06deb684133169e2fda45c4bb95`, `1410690153850d208bc2bff5cfe1c786599c4aeba161f4a998ba3e4c66b56854` | | yes | The E8 ad-hoc views, run unchanged on the E8b log (`<repo> <jsonl>`) |
| tshark 4.0.3 (Wireshark portable) | | | no | Inventory of the raw capture by device address (counts only), the completeness check of the split, the EP0 decode and the independent `usb.capdata` check |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-30-e8b-second-read-only-session.pcap -o %TEMP%\e8b.usbpcap.jsonl --device-address 10 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (owner's Diagnostics reading 2026-10-30 evening, E8 statements; not re-read for E8b)" --host-app "SmartFade backend (read-only USB device, SC-1)" --host-version "1.0.0+c783223cb9ad9c803ef240adcbf2cb49a79117e7" --host-os "Windows 11 Pro build 26300 (not restated for E8b; Memory Integrity not recorded)" --host-driver "WinUSB on interface 1, bound with Zadig during this capture (service ETCHSP before the bind); interface 0 HID (INFERRED from the HID SET_IDLE in the capture)" --notes-log docs/protocol/captures/2026-10-30-e8b-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (tshark -r <raw> -Y 'usb.device_address == 10' -F pcap -w <split>) of the USBPcapCMD capture that the owner started in an admin window on root hub USBPcap1 with -A --inject-descriptors (INFERRED from the injected descriptors and the device addresses 1-6 and 10 in the raw file); the raw root-hub capture (SHA-256 FCFA1A2511D8084141F0AD90B4A15FCE5C2B676F13F949757D8A76E5EEDC4DBF, 180806 frames) is not committed because it holds the traffic of other devices; frame 1 is at 20:35:46.399930 local time, the time of the raw file's first frame" --note 1289.524767 "analyst: the raw capture's last frame (another device) is at 20:57:15.924697 local time; the owner stopped USBPcap and the backend before 20:57:23.703 (log); the console's last frame, frame 125932 of this split, is at 1258.075182 s (20:56:44.475112)"
```

Expected: source SHA-256 `F653DB80…393EC2`, 62 968 transfers from 125 932 device frames (0 other-device frames skipped),
135 notes, output SHA-256 `0c317164…b1f4`.

**Completeness of the split** (the E3b method):
- The normalizer run on the raw capture with `--device-address 10` found 125 932 frames for address 10 and skipped 54 874
  frames of other devices unread. Its 62 968 records are identical to the split's in every field except the pcap frame
  numbers (0 differing records); only the header's source fields and the two analyst notes differ, by design.
- tshark lists 125 932 address-10 frames in the raw capture, and the k-th of them is frame k of the split for every k
  (so a raw-capture frame N maps to the split by its rank among the console's frames: for example raw 13861 → f7, raw
  30189 → f19, raw 172512 → f125932).
- Spot checks in the split itself: the injected descriptors f1–f6, the EP0 burst f7–f18, the first poll f19
  (`00`×12, 20:38:03.245712), the list f28, the split read f36 / f38 (448 + 8 bytes) and the last poll and reply
  f125929 / f125932.

The other figures come from small read-only scripts that import the committed functions of `hsp_summary.py` (as the E8
scripts do) and from tshark 4.0.3. They were run with `py -3.13 -I` from the session scratchpad, outside the repository,
and are not committed with this record (listed under "Next capture to take", C).

## Session health against SC-1

| Check | Result | Source |
|---|---|---|
| Transfers | 61 112, all bulk on interface 1: 29 799 OUT on 0x04 (all 12 bytes: 27 484 polls, 2 315 fetches), 31 313 IN on 0x83 (27 484 reply headers, 3 829 payloads) | `summary`, `e8_health.py` |
| Framing | **0 errors**; every OUT is a 12-byte header with w0 ∈ {0, 2} and w1 = 0 | `summary` (H15/H16) |
| Stale-word rule (F52) | **27 483 of 27 483** polls after the first carry the words of the most recent fetch or reply header (`poll_rules.py`, counting the first: 27 484 of 27 484). The first two polls (seq 1, 3) are all zero: the first reply (seq 2) was idle | `summary`, `poll_rules.py` |
| Replies (F53) | 27 484 polls, 27 484 reply headers: 2 315 type 3, 1 514 type 4 with a list, 23 655 idle. **Type-3 echo 2 315 of 2 315**; every poll after a fetch was answered by the fetched object | `summary` (H16) |
| Lists and fetches (F57) | 1 514 DirtyObjects lists, payload length 5 + 2 + 5n in all; 2 315 records, c = 0 and a = b = 0 in all; **records = fetches 2 315 of 2 315, in list order, no re-fetch**. List sizes: 1 record × 1 172, 2 × 38, 3 × 190, 4 × 77, 5 × 33, 6 × 4 (largest list payload 37 bytes) | `summary` (H20), `e8_health.py` |
| Listed IDs | 3, 12, 13, 14, 15, 17, 21, 22, 23, **all in `HspReadSession.ObservedListedIds`**. SequenceMain (3) is listed for the first time in a session of our host (twice, at the two GO presses, seq 58654 and 59236; "GO with STACK lit") | `e8_health.py` |
| Body lengths (F67) | SequenceMain 46, Independents 85, OutputLevels 514, FaderInfo 195, ChannelLevels 51, PlaybackInfo 81, DisplayContent 41, LedStatus 451, ConsoleStatus 7: the F67 length for every id; DirtyObjects 2 + 5n. Largest IN transfer 519 bytes (bound 792) | `e8_health.py` |
| Inbound sequence (F55) | 3 829 payloads, pseq **0x00 (seq 5) … 244 (0xF4), 0 breaks**, 14 wraps, byte 1 = 0x00 in 3 829 of 3 829, LE length = payload − 5 in 3 829 of 3 829 | `summary` (H17, H18) |
| Faults, timeouts, errors | None. No failure note. Reply-header IN completion max 24.27 ms (seq 1644, t = 32.312, during the owner's mode switch while setting the starting position; median 1.98 ms), payload IN max 5.10 ms (seq 34843), OUT max 4.09 ms (seq 34823), against the 500-ms timeout. **USBD status 0x00000000 in all 62 957 bulk URBs** (USBPcap; the log itself has `status` = `null`) | `e8_health.py`, ad-hoc, usbpcap JSONL |
| End | The last exchange is an idle poll and reply (seq 61111 / 61112, 1121.322 / 1121.328 s; f125929 / f125932), then the note "session stopped by the application at an idle point" (the owner pressed Disconnect). The last list (seq 61106, 1121.231 s, PlaybackInfo) was fetched to its end (seq 61110). PlaybackInfo was still being listed about every 93 ms (a sequence was running, "The sequence on fader 21"), so the stop came at an idle reply between two listings, as SC-1 item 7 designs it. Nothing closing was sent: f125932 is the console's last frame, and the raw capture has no console frame after it up to its end (20:57:15.92) | log, pcap |

**Cadence (our loop; ms, submit times unless stated):**

| Interval | E8b | E8 (our host) | Floor (SC-1) | SmartSoft (F62, `poll_rules.py` over E2, E3, E3b) |
|---|---|---|---|---|
| Poll → next poll, every pair | n = 27 483: min **31.10**, median 45.86, max 78.45 | min 31.08, median 45.84, max 92.17 | 21 | min 20.59, medians 30.8–31.2 |
| Idle poll → next poll | n = 23 654: min 31.10, median 45.66, max 47.76; **10 800 in 30–40 ms, 12 854 in 40–53 ms**, none outside | min 31.08, median 45.47, max 52.40; 7 124 / 8 243 | 21 | median ≈ 31 (E3b 30.79) |
| Payload completion → fetch | n = 2 315: min **28.01**, median 30.03, max 44.47; 1 471 in 28–32, 5 in 32–36, 607 in 36–43, 232 above 43 | min 28.01, median 30.00, max 46.58; 263 above 43 | 21 | min 20.53, medians 28.3–28.8 |
| Fetch → next poll | n = 2 315: min **15.11**, median 15.56, max 31.68; 2 114 in 14–18, 201 in 28–32 | min 15.12, median 15.60, max 31.88; 393 above 28 | 11 | min 10.35, medians 15.3–15.5 |

- **Every interval kept its floor:** 0 polls closer than 21 ms, 0 polls earlier than 11 ms after a fetch, 0 fetches
  earlier than 21 ms after their list.
- The same two clusters as in E8 (near 31 and near 47 ms between idle polls; INFERRED: the 15.625-ms timer tick). The
  upper ends again exceed the envelope H67 quotes from SmartSoft (232 fetches more than 43 ms after their list, 201 polls
  more than 28 ms after a fetch), and the console answered every one normally.
- **The floors were never exercised** (fastest 31.10 / 15.11 / 28.01 ms), as in E8.

**Objects (2 315 payloads besides the lists; "changed" against the previous payload of the id):**

| Object | Payloads | Changed | Identical | First … last (seq, t) |
|---|---|---|---|---|
| SequenceMain (3) | 2 | 0 | 1 | 58662 (1088.471) … 59240 (1096.099) |
| Independents (12) | 4 | 3 | 0 | 15616 (304.037) … 17115 (333.352) |
| OutputLevels (13) | 288 | 275 | 12 | 8425 (165.687) … 60849 (1117.954) |
| FaderInfo (14) | 151 | 137 | 13 | 8417 (165.579) … 52073 (1000.753) |
| ChannelLevels (15) | 292 | 278 | 13 | 8429 (165.733) … 60845 (1117.878) |
| PlaybackInfo (17) | 1 305 | 1 269 | 35 | 11984 (232.794) … 61110 (1121.280) |
| DisplayContent (21) | 150 | 111 | 38 | 13 (0.279) … 60841 (1117.816) |
| LedStatus (22) | 97 | 89 | 7 | 9 (0.220) … 60595 (1114.482) |
| ConsoleStatus (23) | 26 | 13 | 12 | 1118 (21.889) … 50073 (974.051) |

- **Each object arrived only when the console listed it** (F94, H58). After the power-on list (LedStatus, DisplayContent)
  ConsoleStatus came at the owner's first mode switch (21.8 s), FaderInfo, ChannelLevels and OutputLevels at the first
  fader move (165.5 s), PlaybackInfo at the first page change (232.6 s), Independents at the first IND press (304.0 s) and
  SequenceMain at the first GO (1088.4 s). The UI therefore showed the masters as "not reported" for the first 165 s,
  as SC-1's expectations say.

## The power-on list (seq 1 … 13)

- seq 1 (t = 0.098, f19): the first poll, 12 zero bytes. seq 2 (f22): an **idle** reply `04 00 00 00 …`.
- seq 3 (f23): the second poll, again all zero (it copies the idle reply's zero words). seq 4 (f26): `04 00 11 00 …`,
  announcing 17 bytes.
- seq 5 (f28): `00 00 0C 00 28 00 02 16 00 00 00 00 15 00 00 00 00`: pseq **0x00**, a DirtyObjects list of **2
  records, LedStatus (22) and DisplayContent (21)**. Both fetched (seq 6 → payload seq 9, seq 10 → seq 13).
- **This list is byte-identical to E3b session 1's power-on list** (E3b f20, the same 17 bytes), and the shape is the
  same as after the power cycles of E3 and E3b: the first reply idle, the second carrying the list with pseq 0 (F80, F94).
  This is the first time with our host, and the first power cycle followed by WinUSB instead of the ETC driver.
- What the console showed at power-on (OBSERVED; the mode itself is INFERRED, since ConsoleStatus was first delivered at
  21.889 s):
  - LED blocks 1–24 the page-1 memory map (blocks 1–8 and 13 `46 00 00 46 00 00`, block 21 `46 46 00 46 46 00`, the others
    `0A 00 00 0A 00 00`), block 58 `FF 00 00 FF 00 00`, blocks 55 and 56 `00 0A 00 00 0A 00` (seq 9): **memory mode**
    (F84, F98). E3b's power-on LedStatus showed channel mode (block 55 bright green, blocks 1–24 dark).
  - LCD "25 … U 5.0" / "37 … D 5.0" (seq 13): the row labels of the 25-48 view (F98). E3b's power-on LCD read " 1" / "13"
    and 5.1.
  - LED blocks 25–48 `0A 00 00 0A 00 00` (dim red) in every payload of the session, where E2, E3, E3b and E8 have
    `00 0A 00 00 0A 00` (dim green) in every payload (HYPOTHESIS H78 below).
- No list before Connect could be observed (a list waits for a poll). Nothing pending besides the two records: the owner
  touched nothing between the power-on and Connect (script step "console ready"), which is H65's case (a).
- **Never more than 6 records in a list** in the whole session, so H75's 37 / 101-record lists did not occur.

## USBPcap: the bus view of our session

**Inventory (console, address 10).** 125 932 frames, 62 968 transfers (normalizer):
- 62 957 bulk on interface 1 (29 799 OUT on 0x04, 33 158 IN on 0x83);
- 7 control on EP0: three synthetic (`--inject-descriptors`: device and configuration descriptor, SET_CONFIGURATION;
  f1–f6, byte-identical to E3b's injected device descriptor) and four real ones at the bind (below);
- 4 interrupt on 0x81 (interface 0): two completions CANCELED and two resubmissions that never completed (below);
- **nothing on 0x02**, and **no interrupt IN data at all**: the HID interface carried nothing, as in E2–E3b (F51).

**USBD status:** 0x00000000 in all 62 957 bulk URBs and in 6 of the 7 control transfers; the HID SET_IDLE was stalled
(0xC0000004, f13); the two cancelled interrupt reads carry USBD_STATUS_CANCELED (0xC0010000, f7, f8).

**Frame-by-frame cross-check of the backend's log (this verifies `TransferLog`):**
- **61 112 of 61 112 backend transfers equal the bus byte for byte, in order**, every one with USBD status 0. Every OUT is
  one URB; every IN of up to 64 bytes is one URB.
- **Every IN read longer than 64 bytes and not a multiple of 64 is two URBs on the bus**: 1 845 reads (PlaybackInfo 86
  bytes × 1 305, Independents 90 × 4, FaderInfo 200 × 151, LedStatus 456 × 97, OutputLevels 519 × 288). The first URB
  carries a whole number of 64-byte packets (64, 64, 192, 448, 512 bytes) and the second the rest (22, 26, 8, 8, 7). Example:
  the LedStatus read seq 9 (456 bytes) is f36 (448 bytes) + f38 (8 bytes). The concatenation equals the backend's
  record in 1 845 of 1 845.
  - INFERRED: WinUSB splits a read whose length is not a multiple of the endpoint's 64-byte packet size into a whole-packet
    part and a remainder read (host-side behaviour; SmartSoft's ETC driver read every payload in one URB in E2–E3b). The
    console sees IN tokens either way; no console-side difference is expected (INFERRED). This explains the 33 158 bulk IN
    URBs against the log's 31 313 reads.
- **One clock.** Backend submission time minus USBPcap submission: median −0.022 ms (−0.753 … −0.005); backend completion
  `ts` minus USBPcap completion: median +0.022 ms (+0.007 … +3.934). The action log, the backend log and the pcap can be
  compared directly.
- **Independent check of the normalizer:** tshark 4.0.3's `usb.capdata` equals the usbpcap JSONL's `data` for 62 957 of
  62 957 data-carrying bulk records (0 mismatches), as E3b's tshark cross-check did.
- Zero-length packets: none on 0x83. No backend read had a length that is a multiple of 64; the 1 845 whole-packet URBs are
  the host-side splits above, each followed by the console's remainder. **H75 is not tested** by this.

### H68: EP0 at the bind, at the backend start and at Connect

All console EP0 traffic in the capture besides the injected descriptors is one burst, **f7–f18, 20:36:45.016–
20:36:45.094**. That is 9.9 s before the "zadig bind start" stamp (20:36:54.994) and 10.9 s before the script read the
WinUSB service (20:36:55.986):

| Frames | Transfer | Result |
|---|---|---|
| f7, f8 | The two pending interrupt-IN reads on 0x81 (interface 0) complete | USBD_STATUS_CANCELED, no data |
| f9 → f11 | GET_DESCRIPTOR STRING 2, LANGID 0x0409, wLength 4 (setup `80 06 02 03 09 04 04 00`) | `1C 03 45 00` |
| f10 → f13 | HID class SET_IDLE to interface 0 (setup `21 0A 00 00 00 00 00 00`) | **STALL** (0xC0000004) |
| f14, f15 | Two interrupt-IN reads on 0x81 resubmitted (same IRP IDs) | never completed |
| f12 → f16 | GET_DESCRIPTOR STRING 2, wLength 28 | "ETC SmartFade" (`1C 03 45 00 54 00 43 00 …`, F41) |
| f17 → f18 | GET_STATUS (device) | `01 00` (self-powered) |

- No SET_ADDRESS, no device or configuration descriptor read, no SET_CONFIGURATION or SET_INTERFACE: **the device was not
  re-enumerated** (address 10 throughout). No STRING 0 was read.
- INFERRED: the driver change restarted the function drivers of the device (the HID reads of interface 0 were cancelled
  and SET_IDLE was sent again, so interface 0 was restarted too); the capture does not show which component issued the
  string and status reads. That this burst is the bind is INFERRED from its timing (the only EP0 activity between the
  capture start and our first poll, inside the window in which the service changed from ETCHSP to WinUSB).
- **At the backend start (20:36:56) and at Connect (20:38:03.1–.25): no console frame of any kind.** The split's f18 (20:36:45.094) and
  f19, our first poll (20:38:03.245712), are consecutive: 78.15 s without any frame of the console's address. Neither the WinUSB open nor
  our session issued a control transfer that reached the bus.
- The console answered the first poll at once (f22, 1.2 ms after the submission; idle) and the second with the power-on
  list (f26, f28), the same answers as after E3's and E3b's power cycles, where SmartSoft's string reads came right
  before the first poll.
- **After the session:** f125932 is the console's last frame; the raw capture has no console frame after it up to its end
  (20:57:15.92), so Disconnect issued nothing on the bus. The backend process was stopped later (stamp 20:57:23.703), possibly after USBPcap, so its exit is not covered.
- Limit (ASSUMPTION about the USB stack, not observed): USBPcap shows the URBs that pass its filter; a request the stack
  answered from its own descriptor cache would not appear, but it would not reach the console either.
- **H68 result: CONFIRMED (OBSERVED once).** A host that issues no string-descriptor read before its first poll (the last
  one, Windows' read at the bind, was 78 s earlier) is answered normally. SC-1 item 5's ASSUMPTION "WinUSB itself may issue
  standard control requests when the handle is opened" is answered for this PC: none reached the bus.

## Action log matched to the data

"Offset" is the time of the first list (or object) the action caused minus the log stamp. A positive offset means the
console reported the action after the owner pressed Enter (as the script asks for `Step` lines). The matching of an
unlogged event to the owner's account is INFERRED unless the bytes name the action (an LCD text).

| Log stamp (local), t | Script line | Data (seq, t) | Offset | Reading |
|---|---|---|---|---|
| 20:36:54.994, −68.153 / 20:36:55.986, −67.161 | `zadig bind start` / service WinUSB | EP0 burst f7–f18 at 20:36:45.02–.09 | −9.9 s | The bind (INFERRED), section H68 |
| 20:37:55.826, −7.321 | `ui ready, hardware badge (owner)` | — | | |
| 20:38:50.704, 47.557 | `connect pressed` | first poll seq 1 at 0.098 (20:38:03.245) | **−47.46 s** | The owner pressed Connect before this step, 7.3 s after the UI was ready and before the starting position was set |
| (before 20:38:41.525, 38.378 `starting position set`) | — | ConsoleStatus seq 1118 (21.889) `00 00 00 00 00 01 00` with the channel-mode LEDs (seq 1122) and the LCD labels " 1" / "13" (seq 1126); memory mode seq 1649 (32.370); the MEMS page view of page 1 (LED seq 1695, LCD "Memory page:1" seq 1702, 33.135–33.811); channel mode seq 1782 (34.652) | | The owner set the starting position while connected: from the power-on state (memory mode, LCD 25 / 37) to channel mode 1-24, a look at the page (page 1), channel mode |
| 20:38:56.285, 53.138 … 20:40:05.980, 122.833 | `connected`, 60-s idle, `idle observation = n` | no list from 34.7 to 141.2 s (106.5 s of idle replies) | | Idle, as the owner says |
| 20:40:31.479, 148.332 | `memory mode on` | list seq 7173 (141.203), ConsoleStatus body[0] 0 → 1 seq 7177, LED seq 7181 | **−7.13 s** | Memory mode, done before the Enter |
| 20:40:46.082, 162.935 / 20:40:52.962, 169.815 | `memory fader 1 up start` / `at full` | FaderInfo record 1 `0B 0B 00 00` seq 8417 (165.579) … `FF FF 00 00` seq 8535 (167.154); LED block 1 full red seq 8421; channels 7, 8 to 0xFF (ChannelLevels seq 8531, OutputLevels seq 8546) | +2.6 s | Memory fader 1 (F112) |
| 20:41:11.795, 188.648 / 20:41:17.485, 194.338 | `memory fader 1 down start` / `at zero` | record 1 `F3 F3` seq 9721 (190.701) … 0 seq 9820 (192.036) | +2.0 s | |
| 20:41:27.288, 204.141 / 20:41:38.491, 215.344 | `memory fader 2 up start` / `at zero` | record 2 `14 14` seq 10570 (206.517) … `FF FF 00 00` seq 10699 (208.273) … 0 seq 10994 (213.389); LED block 2 full red seq 10574 … seq 11021 | +2.4 s | Memory fader 2 (F112) |
| 20:41:53.895, 230.748 / 20:41:59.616, 236.469 | `page 12 start` / `selected` | LCD "Memory page:1" seq 11934 (231.926), page view seq 11941; ConsoleStatus `01 00 0B 00 00 01 00` seq 11976 (232.674); FaderInfo records 1–24 `00 00 00 0B` seq 11980; first PlaybackInfo seq 11984; page-12 map seq 12003 (233.050) | +1.1 s | Page 12 (F113) |
| 20:42:28.193, 265.046 / 20:42:33.001, 269.854 | `page 1 start` / `selected` | LCD "Memory page:12" seq 13712 (266.584); ConsoleStatus body[2] → 0 seq 13733 (266.945); byte 3 → 0 seq 13737; page-1 map seq 13760 (267.303) | +1.4 s | Page 1 (F113) |
| 20:42:44.322, 281.175 | `blackout on` | list seq 14508 (282.091); LED block 53 `00 00 FF 00 00 FF` seq 14512 | +0.92 s | BLACKOUT (F114) |
| 20:42:57.067, 293.920 | `blackout off` | block 53 `0A`×6 seq 15089 (293.574) | −0.35 s | |
| 20:43:06.788, 303.641 | `ind 1 on` | list seq 15612 (303.991); Independents body[1] = 1 seq 15616; LED block 72 G seq 15620; ChannelLevels[49] seq 15624 | +0.35 s | **IND 1 = record 1, block 72, channel 49** (F115) |
| 20:43:21.536, 318.389 | `ind 2 on` | list seq 16379 (318.927); body[43] = 1 seq 16383; OutputLevels[48] (DMX 47) seq 16387; ChannelLevels[50] seq 16391; block 73 seq 16395 | +0.54 s | **IND 2 = record 2, block 73, channel 50 → DMX 47** |
| 20:43:28.658, 325.511 / 20:43:35.951, 332.804 | `ind 1 off` / `ind 2 off` | seq 16746 … 16758 (326.046) / seq 17111 … 17130 (333.303) | +0.54 / +0.50 s | |
| 20:43:52.325, 349.178 / 20:44:01.759, 358.612 | `rec mem on` / `left with clear` | LCD "Record Memory Pg:1" seq 17957 (349.752), LED seq 17961; back seq 18444, 18448 (359.313) | +0.53 / +0.59 s | REC MEM screen (F116, H72) |
| 20:44:10.280, 367.133 / 20:44:19.514, 376.367 | `edit mem on` / `left with clear` | LCD "Edit Memory" seq 18919 (368.408), LED seq 18923; ConsoleStatus re-listed unchanged seq 19358 (377.104); back seq 19362, 19366 | +1.22 / +0.68 s | EDIT MEM screen |
| 20:44:31.005, 387.858 / 20:44:40.210, 397.063 | `rec seq on` / `left with clear` | LCD "Record Sequence" seq 19939 (388.378), LED seq 19943; back seq 20426, 20430 | +0.47 / +0.83 s | REC SEQ screen |
| 20:44:48.645, 405.498 / 20:44:55.795, 412.648 | `copy on` / `left with clear` | ConsoleStatus re-listed unchanged seq 20863 (406.409); LCD "Copy Memory/Seq" seq 20867, LED seq 20871; back seq 21210, 21214 (413.332) | +0.86 / +0.57 s | COPY screen |
| 20:45:07.225, 424.078 / 20:45:15.001, 431.854 | `stack press` / `stack release` | **no list at all from 413.3 to 447.9 s** | | The logged press-and-hold changed nothing the console reports |
| (unlogged, during the answer 436.9–481.7) | — | LCD "Live:14.0 Next: 1.0" seq 22973 (447.985), block 60 full seq 22977 (448.045), LCD "Jump to step:> 1" / "Mem:01" seq 23004 (448.517), block 60 dim seq 23011 (448.609), fader view seq 23015 (448.672) | | A short STACK action (owner: "if I just press it and release right after I press it it lit"); E8's three-state sequence within 0.68 s |
| 20:46:11.996, 488.849 | `channel mode on` | ConsoleStatus body[0] → 0 seq 25086 (489.602), LED seq 25090 | +0.69 s | |
| 20:46:27.027, 503.880 / 20:46:31.320, 508.173 | `channel fader 5 up start` / `at full` | record 5 `09 00 00 00` seq 25855 (504.754) … `FF 00 00 00` seq 25989 (506.540); LED block 5 G; LCD cell body[7] | +0.81 s | Channel fader 5 (F82, F83) |
| 20:46:48.210, 525.063 | `blackout on with level` | list seq 26983 (525.998); FaderInfo body[17] FF → 00 seq 26987; ChannelLevels[5] seq 26991; OutputLevels[6] seq 26995; LED block 5 G → 0 and block 53 blue seq 26999; LCD cell seq 27003 (526.262) | +0.94 s | **BLACKOUT zeroes the level objects** (new) |
| 20:47:00.432, 537.285 | `fixture at blackout = went dark` | | | Owner |
| 20:47:03.297, 540.150 | `blackout off with level` | list seq 27746 (540.970); LED seq 27750; ChannelLevels seq 27754; OutputLevels seq 27758; FaderInfo seq 27762; LCD seq 27766 (541.248) | +0.82 s | All five restored |
| 20:47:11.418, 548.271 / 20:47:15.624, 552.477 | `channel fader 5 down start` / `at zero` | record 5 `E7 00 00 00` seq 28169 (549.303) … 0 seq 28261 (550.536) | +1.0 s | |
| 20:47:19.574, 556.427 | `memory mode on again` | ConsoleStatus seq 28609 (557.100), LED seq 28613 | +0.67 s | |
| 20:47:31.380, 568.233 / 20:47:35.559, 572.412 | `h69 memory fader 1 up` / `at full` | record 1 `17 17` seq 29233 (569.278) … `FF FF 00 00` seq 29309 (570.298) | +1.0 s | |
| 20:47:45.648, 582.501 / 20:47:50.111, 586.964 | `h69 page 12 with fader up` / `selected` | ConsoleStatus `01 00 0B …` seq 29992 (583.736); FaderInfo record 1 `FF FF 00 00`, records 2–24 `00 00 00 0B` seq 29996; LED block 1 blinking `C8 00 00 0A 00 00` over the page-12 map seq 30022 (584.181) | +0.54 s | **H69** |
| 20:48:49.455, 646.308 | `h69 bump 1 blinking = yes it is blinking …` | | | Owner |
| 20:48:59.376, 656.229 / 20:49:09.039, 665.892 | `h69 memory fader 1 down` / `at zero` | record 1 `ED ED 00 00` seq 33677 (656.730) … `00 00 00 0B` seq 33741 (657.633); block 1 → `46 00 00 46 00 00` seq 33757 | +0.44 s | **H69**: byte 3 follows the page once the fader is at zero |
| 20:49:21.417, 678.270 / 20:49:26.538, 683.391 | `page 1 again` / `selected again` | ConsoleStatus body[2] → 0 seq 34858 (679.737); byte 3 → 0 seq 34862 | +1.07 s | |
| 20:49:45.629, 702.482 / 20:49:57.551, 714.404 | `h71 master half, fader 1 half` / `h71 set` | MASTER body[194] FF → 0x76 seq 36132 (704.573) … seq 36264 (706.333), block 53 blinking seq 36136; record 1 `06 06` seq 36385 (708.685) … `94 94 00 00` seq 36521 (710.504); ChannelLevels[7], [8] 0x45 seq 36536 | +2.0 s | **H71 (1)** |
| 20:50:12.841, 729.694 / 20:50:19.934, 736.787 | `h71 reset` / `reset done` | MASTER → FF seq 37531 (730.600) … seq 37595 (731.517), channels 7, 8 → 0x94 (seq 37591) with record 1 still `94 94`; record 1 → 0 seq 37663 … 37708 (733.297) | +0.91 s | |
| 20:51:02.018, 778.871 / 20:51:14.740, 791.593 | `h71 bump 2 press` / `release` | BUMPS body[193] → 0x81 seq 40074 (780.434) … seq 40144 (781.416); record 2 → `49 49 00 00` seq 40275 (783.570); bump held: record 2 `81 81 00 00` seq 40318 (784.315), levels seq 40322, 40326; released: `49 49 00 00` seq 40651 (790.836) | +1.6 s | **H71 (2)**, H54 |
| 20:51:25.551, 802.404 / 20:51:33.159, 810.012 | `h71 bumps and fader 2 to zero` / `cleanup done` | BUMPS → 0 seq 41316 … 41365 (804.685); record 2 → 0 seq 41437 … 41475 (806.552) | +1.6 s | |
| 20:51:58.308, 835.161 / 20:52:11.828, 848.681 | `h73 setup` / `setup done` | channel mode seq 42976 (836.056); record 5 → `FF 00 00 00` seq 43142 (838.803); memory mode seq 43205 (839.980); FaderInfo record 5 → `00 00 00 00` seq 43209 (840.041) and LED block 5 blinking `46 00 00 0A 00 00` seq 43217 (840.147); record 1 → `FF FF 00 00` seq 43344 (842.065) | +0.85 s | H73 setup; fader 5 is up but not in control in memory mode |
| 20:52:23.718, 860.571 | `clear x1` | one unchanged DisplayContent re-listing seq 44344 (861.295); no level change | +0.72 s | **H73**: 1 press, nothing |
| 20:52:31.239, 868.092 | `clear x2` | DisplayContent re-listed seq 44707, 44714; LCD cell body[7] → blank seq 44735 (869.018); ChannelLevels[5] FF → 00 seq 44739; OutputLevels[6] FF → 00 seq 44746 | +0.44 s | **H73**: 2 presses clear the channel intensity |
| 20:52:37.701, 874.554 | `clear x3` | DisplayContent re-listed 4 times (seq 45051 … 45082); FaderInfo record 1 `FF FF` → `00 00` seq 45105 (875.804); LED block 1 → `46 00 00 0A 00 00` seq 45109; LCD seq 45113; ChannelLevels[7], [8] → 0 seq 45117; OutputLevels[8], [9] → 0 seq 45121 | +0.41 s | **H73**: 3 presses set the memory fader to zero (F120's pattern) |
| 20:52:44.650, 881.503 | `clear x4` | DisplayContent re-listed 4 times (seq 45413 … 45482); no other change | +0.43 s | 4 presses: nothing (no sequence fader up) |
| 20:53:22.324, 919.177 | `h73 result = 1 and 5 blinks and 1 and 5 are up` | blocks 1 and 5 blinking `46 00 00 0A 00 00` | | Owner, matches |
| 20:53:36.617, 933.470 / 20:53:45.312, 942.165 | `h73 cleanup` / `done` | block 1 → steady `46 00 00 46 00 00` seq 48097 (934.895), no FaderInfo change; channel mode seq 48158 (936.060); block 5 green blink `00 46 00 00 0A 00` seq 48211 (937.086), dark seq 48236 (937.522) | +1.36 s | The blinks end when each fader reaches zero |
| 20:54:16.115, 972.968 / 20:54:22.664, 979.517 | `sequence start` / `running` | memory mode seq 50073 (974.051); FaderInfo record 21 `16 16 01 00` seq 50142 (975.233) … `FF FF 01 00` seq 50200; PlaybackInfo record 1 step 0 seq 50146 (975.281); block 21 `FF FF 00 FF FF 00` seq 50150; channels 37, 45 (seq 50154, 50158); LCD body[15] '+' seq 50162 (975.500) | +1.0 s | Fader 21 up starts page 1's sequence |
| 20:54:41.728, 998.581 / 20:54:46.051, 1002.904 | `sequence stop` / `stopped` | record 21 `BC BC 01 00` seq 51978 (999.506) … 0 seq 52054 (1000.489); output to 0 (seq 52061, 52065); block 21 dim seq 52077; '+' gone seq 52081 (1000.874); **record 1 keeps stepping to the end of the session** (last seq 61110, 1121.280) | +0.9 s | Fader 21 down: output 0, sequence still running |
| 20:55:21.050, 1037.903 / 20:55:32.663, 1049.516 | answers: start / stop method; `go with stack decision = y` | | | Owner |
| 20:55:45.183, 1062.036 / 20:55:53.362, 1070.215 | `go with stack lit` / `go with stack done` | list seq 56716 (1062.688); LCD "Live:14.0 Next: 1.0" seq 56724; block 60 full seq 56728 (1062.830); **no GO in this window** (PlaybackInfo record 5 unchanged) | +0.65 s | STACK activated only |
| (unlogged, after `wait 10 s done` 1080.232) | — | GO #1 at 1088.363 (seq 58654) … 1093.697; GO #2 at 1096.050 (seq 59236) … 1101.350; crossfader A 1104.752–1106.889, B 1107.093–1109.165, both 1109.664–1110.613; STACK off 1114.182–1114.482; CLEAR 1117.349–1117.954 | | The owner's account, step by step in "GO with STACK lit and the crossfaders" |
| 20:56:49.648, 1126.501 / 20:56:53.344, 1130.197 | `disconnect pressed` / `disconnected` | last transfer seq 61112 (1121.328) | −5.17 s | Disconnect, done before the Enter |

## E8's readings, repeated with times

### F110: the read-only session as a whole

Reproduced (section "Session health"): only polls and listed fetches; every poll answered by one 12-byte header;
every list fetched in order; an unbroken inbound counter; no fault; cadence above the floors; stopped at an idle point;
the console reported every front-panel action in the script (and the unlogged ones). Second session of our host, with a
timed action log and the bus seen in parallel: **VERIFIED proposed** for the structure; the counts and intervals are per
session.

### F111: the long connect-time list

**Not reproduced, by design:** after a power cycle the pending list held 2 records (seq 5), not E8's 89 with repeats.
F111 records one history (E8's) and stays OBSERVED. The power-on list is in "The power-on list" above (F80, F94, H65).

### F112: memory faders

- Memory mode (ConsoleStatus body[0] = 1, seq 7177) with timed moves of memory faders 1 and 2: physical fader k is
  FaderInfo record k, bytes 0 and 1 equal (record 1 `0B 0B` … `FF FF`, seq 8417 … 8535; record 2 up to `FF FF 00 00`, seq
  10699). **Bytes 0 = 1 in 87 of 87 non-zero memory-mode samples** of the session (records 1, 2, 21); byte 1 = 0 in 16 of
  16 non-zero channel-mode samples.
- The level objects carry the memory's stored levels scaled by the fader: with memory fader 2 at full, ChannelLevels (seq
  10703) and OutputLevels (seq 10707) read channels 1–6 `2C 40 55 64 61 5A`, 9–10 `61 6E`, 13–14 `12 52`, the other
  channels 0, **the same 48 of 48 channel bytes as E8** (seq 4463 / 4467). They equal `GROUP 2` "LES" of `SHOW06.ASC`
  (`CHAN 1/17 2/25 3/33 4/39 5/38 6/35 9/38 10/43`, `CHAN 13/7 14/32`); memory fader 1 at full gives channels 7, 8 at 0xFF
  (`GROUP 1` "OPONA", `CHAN 7/100 8/100`; seq 8531, 8546).
- LED block k is `FF 00 00 FF 00 00` while memory fader k is up (block 1 seq 8421 … 9843, block 2 seq 10574 … 11021).
- **VERIFIED proposed** for the structure (E8 + E8b, timed). Proportional playback stays INFERRED (F107).

### F113: the memory page

- Timed page changes to 12 and back, twice (230.7–269.9 s and, with fader 1 up, 582.5–683.4 s):
  - ConsoleStatus body[2] = page − 1: `01 00 0B 00 00 01 00` (seq 11976, 29992) and back to `… 00 …` (seq 13733, 34858);
  - FaderInfo byte 3 of records 1–24 = page − 1 (`00 00 00 0B`, seq 11980; back, seq 13737);
  - record 21 `00 00 01 00` on page 1 and `00 00 00 0B` on page 12 (byte 2 = 0 there; seq 11980, 13737, 29996, 34862);
  - records 25–48 unchanged (record 25 `00 00 01 00`).
- The held-MEMS page view: page 1 view with block 1 steady full red and block 12 blinking `C8 00 00 0A 00 00` (seq 11941),
  block 12 full and block 1 blinking once bump 12 is pressed (seq 11988); LCD "Memory page:1" / "Bump 1-12 to change"
  (seq 11934) and "Memory page:12" (seq 13712); the page-12 map blocks 1–7 `46 00 00 46 00 00`, 8–24 `0A 00 00 0A 00 00`
  (seq 12003), as in E8. With `SHOW06.ASC` = `SHOW05.ASC` the map equals the show's Memory qualifiers a = 11, b = 0–6
  (`GROUP` 265–271), so the E8 caveat "assumes that the show did not change since E3b" is settled for E8b.
- ConsoleStatus is re-listed unchanged around each opening and closing of the page view (10 payloads, e.g. seq 11930,
  11999, 13708, 13756), as in E8.
- **VERIFIED proposed** for the structure (two sessions, the second timed). H69 and H70 are evaluated separately.

### F114: BLACKOUT

- Timed: `blackout on` (281.175) → block 53 `00 00 FF 00 00 FF` (seq 14512, 282.151); `blackout off` (293.920) → `0A`×6
  (seq 15089). The lists named LedStatus alone (seq 14508, 15085), and all levels were 0: as in E8.
- **With channel fader 5 at full** (`blackout on with level` 525.063, `off` 540.150), BLACKOUT **set every level-carrying
  place of channel 5 to zero** within 0.27 s and restored it within 0.28 s:
  - FaderInfo body[17] (record 5 byte 0) FF → 00 (seq 26987) and back (seq 27762);
  - ChannelLevels[5] (seq 26991 / 27754) and OutputLevels[6] = DMX 5 (seq 26995 / 27758);
  - LED block 5 G (body[26], [29]) together with block 53 going blue (seq 26999 / 27750);
  - the LCD cell body[7] 0x86 → 0x20 (seq 27003 / 27766).
  The owner: the fixture "went dark". MASTER (body[194]) stayed 0xFF. This answers F114's open point: the level objects
  do show a blackout, as they show the MASTER (F96).
- The masking of the MASTER blink (E8) was not re-tested: the MASTER stood at full during both BLACKOUT tests.
- **VERIFIED proposed** for block 53 steady blue while BLACKOUT is on (E8 + E8b, timed key); with the timed key the reading
  "block 53 = BLACKOUT active" is OBSERVED, no longer only INFERRED. New fact F125 for the level objects.

### F115: the independents, named

- `ind 1 on` → the session's first Independents payload, body[1] (record 1 byte 0) = 1 and body[43] = 0 (seq 15616; the
  object had not been listed before, so its earlier state is not in the log), LED block 72 G `0A` → `FF` (seq 15620),
  ChannelLevels[49] 0 → 0xFF (seq 15624), no OutputLevels change.
- `ind 2 on` → body[43] (record 2 byte 0) 0 → 1 (seq 16383), OutputLevels[48] = DMX 47 (seq 16387), ChannelLevels[50]
  (seq 16391), block 73 (seq 16395).
- `ind 1 off` and `ind 2 off` reverse exactly these (seq 16750 … 16758, 17115 … 17130). Bytes 1–3 stayed `FF 00 01`.
- **E8's INFERRED identities are now OBSERVED with named, timed keys:** IND 1 = Independents record 1 = LED block 72 =
  channel 49 (unpatched); IND 2 = record 2 = block 73 = channel 50 → DMX 47 (the show's patch, F70). **VERIFIED proposed**
  for the structure.

### F116: the record keys, one at a time

- Each key entered and left with CLEAR, timed (349.2–412.6 s). The LCD texts and the LED states are **identical to E8's**
  (28 screens there, 4 here):
  - REC MEM (seq 17957 / 17961): bumps 1–8 and 13 `46 00 00 0A 00 00`, the others (21 included) `C8 00 00 0A 00 00`;
    block 57 `FF FF FF 0A 0A 0A`, 60 `C8 C8 00 0A 0A 00`, 65 `FF FF FF 0A 0A 0A`, **66 `FF 00 00 FF 00 00`**;
  - EDIT MEM (seq 18919 / 18923): recorded `C8 00 00 0A 00 00`, empty and 21 `46 00 00 46 00 00`; 57 blinking, **67
    `FF 37 37 0A 0A 0A`**;
  - REC SEQ (seq 19939 / 19943): 1–20 `0A 00 00 0A 00 00`, 21 `46 46 00 0A 0A 00`, 22–24 `C8 C8 00 0A 0A 00`; 57 and 60
    blinking, **64 `FF FF 00 FF FF 00`**;
  - COPY (seq 20867 / 20871): recorded `C8 00 00 0A 00 00`, 21 `C8 C8 00 0A 0A 00`, empty `0A 00 00 0A 00 00`; 57 blinking,
    **63 `FF FF FF FF FF FF`**.
- Each screen opened and closed with exactly one LedStatus payload (2 per screen in 4 of 4); blinking travels as phase A ≠
  phase B. ConsoleStatus is re-listed unchanged at the EDIT MEM exit (seq 19358) and at the COPY entry (seq 20863), as in
  E8.
- After every screen the page-1 map returned unchanged (seq 18448, 19366, 20430, 21214); no bump was pressed inside a
  record function (log), so nothing was recorded.
- **VERIFIED proposed** for the screens; the key ties are H72 below.

### F117: STACK

- **The logged press-and-hold** (`stack press` 424.078 to `stack release` 431.854) **produced no list at all**: nothing the
  console reports changed between 413.3 and 447.9 s. The owner: "if I hold it it does nothing I think".
- **An unlogged short action at 447.99–448.67 s** (while the owner typed his STACK answer) reproduced E8's three states in
  0.68 s: LCD "Live:14.0 Next: 1.0" / 0x86 "Mem:01/01 Mem:01/01" (seq 22973), block 60 `FF FF 00 FF FF 00` (seq 22977),
  LCD "Jump to step:> 1" / "Mem:01" (seq 23004), block 60 back to `46 46 00 46 46 00` (seq 23011), fader view (seq 23015).
  E8 took 1.8 s for the same sequence. Which presses produce it is UNKNOWN.
- **The scripted STACK press** (`go with stack lit` 1062.036): LCD "Live:14.0 Next: 1.0" (seq 56724) and block 60 full
  (seq 56728, 1062.830), **steady for 51.7 s** until the owner switched STACK off (seq 60595, 1114.482). No "Jump to step".
- PlaybackInfo record 5 bytes 0–1 were `00 00` from the first PlaybackInfo of the session (seq 11984) on, so E8's
  `01 01` → `00 00` at the press could not recur (nothing to reset after the power cycle).
- **"Live:14.0" is the stack's last cue** (INFERRED): `$SEQUENCE 48` of `SHOW06.ASC` has 19 cues, 1.0 … 9.0, 10.1 …
  10.6, 11.0 … 14.0, and cue 14.0 is `$$MEMORY 0 0`, i.e. "Mem:01/01". At step 0 the Live field shows the last cue and the
  Next field cue 1.0 (`$$MEMORY 0 0`, "Mem:01/01"). The later steps match the file too ("GO with STACK lit").
- "Jump to step" is the GOTO display: manual printed p.61 (DOCUMENTED), "Activate GOTO mode (held): As long as the button
  is held, the display will allow you to scroll to any step. Let go of STACK to transport the whole sequence to this step
  in Next - ready for the next crossfade." E8's further reading "of a second, held press" is **not supported** by E8b: the
  logged press-and-hold (stamps 7.8 s apart; the script asked for a 3-s hold), from an inactive stack (block 60 dim), did
  nothing, and the three states came within 0.68 s. INFERRED: GOTO needs the stack to be active; which presses produced
  the 448-s sequence is UNKNOWN.
- **VERIFIED in part proposed:** block 60 `46 46 00 …` → `FF FF 00 FF FF 00` with the LCD "Live:14.0 Next: 1.0" when STACK
  is activated (E8 + E8b, timed in E8b). Not reproduced: record 5 `01 01` → `00 00` (already 0).

### F118: the sequence on fader 21

- **Started by a timed fader move:** fader 21 up in memory mode on page 1 (`sequence start` 972.968): FaderInfo record 21
  `16 16 01 00` … `FF FF 01 00` (seq 50142 … 50200), PlaybackInfo record 1 from `05 05 FF 00 …` (at rest at step 5 from
  its first fetch, seq 11984, 232.794, on) to step 0 with a fade `00 00 E0 1F …` (seq 50146, 975.281), LED block 21 `FF FF 00 FF FF 00` (seq 50150).
- **Its output is page 12's memories 1–7:** channels 37, 38 and 45 (ChannelLevels[37], [38], [45]; OutputLevels[38], [39],
  [46] = DMX 37, 38, 45; first seq 50154, 50158), step by step as `GROUP 265`–`271` of `SHOW06.ASC` (`CHAN 37/100 45/100`,
  `CHAN 37/100 38/100 45/100`, … `CHAN 37/58 38/61 45/100`). That ties record 1 to the sequence "KOTLIK" on page 1's fader
  21 with steps = memories 1–7 of page 12, as the owner says (E8 statements, correction). The rig (DMX 5–16) shows none of
  it.
- Record 1 structure, as in E8: bytes 0 = 1 and Live + Next = 255 in 1 305 of 1 305 PlaybackInfo payloads; from 975 s,
  129 step changes, all in order 0 → 1 → … → 6 → 0, median 1.124 s per step (0.90–1.24 s); times `00 0A` × 3 during a fade
  and `00 00` × 3 at its end; bytes 4–9 `00 00 01 00 01 00` throughout (H74). The times follow the page (`00 00 00 00 00 0A`
  on page 12, seq 30000; `00 0A 00 0A 00 0A` on page 1, seq 13741, 34866), as in E8.
- **Moving fader 21 down does not stop the sequence:** record 21 and the output went to 0 (seq 52054, 52061, 52065), block
  21 back to `46 46 00 46 46 00` (seq 52077), but record 1 kept stepping until the end of the session (last seq 61110,
  1121.280, step 3). The owner: "moving fader 21 down to stop". **This explains E8's sequence running without output**
  (F118: pending at the first poll, no level object showing it): it had been started with fader 21 and its fader brought
  back to zero. What stopped it at E8's 237.519 s stays UNKNOWN (the owner: holding MEMS does not stop it).
- An LCD sign appeared while the output was on: body[15] (line 1, the position after channel 12's cell) 0x20 → `+` (0x2B)
  at 975.500 (seq 50162) and back at 1000.874 (seq 52081). HYPOTHESIS H77.
- **VERIFIED in part proposed:** record 1 = the playback of page 1's sequence on fader 21 (structure, E8 + E8b, start timed
  in E8b); new fact F129 for the start / "stop" behaviour.

### F119: listed is not changed

Reproduced: 38 of 150 DisplayContent, 35 of 1 305 PlaybackInfo, 12 of 26 ConsoleStatus, 7 of 97 LedStatus, 13 of 151
FaderInfo, 13 of 292 ChannelLevels, 12 of 288 OutputLevels payloads and 1 of 2 SequenceMain payloads equal their
predecessor (first payloads excluded). The unchanged ConsoleStatus re-listings fall exactly where E8 put them (page view,
EDIT MEM exit, COPY entry). New: every CLEAR series re-listed DisplayContent with an unchanged body (x1: 1, x2: 2, x3: 4 + 1,
x4: 4 listings; seq 44344 … 45482), so key presses that change nothing visible still show as listings; the counts do not
match the press counts one to one. **VERIFIED proposed.**

### F120: a memory level zeroed with the fader up

Reproduced by the timed CLEAR × 3 (seq 45105 … 45121): record 1 `FF FF 00 00` → `00 00 00 00` in one step, channels 7, 8
and DMX 7, 8 to 0, the LCD cells cleared, LED block 1 `46 00 00 0A 00 00` blinking until the fader reached zero (seq 48097,
no FaderInfo change), the same bytes as E8 (seq 24706 … 24722, 40108). **VERIFIED proposed;** the cause, INFERRED in E8, is
OBSERVED here (H73).

## New tests

### H69: a fader held across a page change

- Memory fader 1 at full (`FF FF 00 00`, seq 29309), then page 12 (`h69 page 12 with fader up` 582.501):
  - ConsoleStatus body[2] = 0x0B (seq 29992);
  - **FaderInfo record 1 stayed `FF FF 00 00` (byte 3 = 0x00, page 1) while records 2–24 read `00 00 00 0B` at once** (seq
    29996);
  - channels 7 and 8 stayed at full (page 1's memory 1, `GROUP 1`; LCD seq 30026);
  - LED block 1 `C8 00 00 0A 00 00` (blinking) over the page-12 map, blocks 2–7 `46 00 00 46 00 00` (seq 30022). The owner:
    blinking, "the lights are from a different source not the current selected which is mem 1 from page 12".
- Fader 1 to zero (`h69 memory fader 1 down` 656.229): record 1 `ED ED 00 00` … `25 25 00 00` with byte 3 still 0 (seq 33677
  … 33730), then **`00 00 00 0B`** (seq 33741, 657.633), and block 1 steady `46 00 00 46 00 00` (seq 33757), page 12's
  recorded memory 1.
- **H69: CONFIRMED (OBSERVED once), exactly as predicted.** It matches the manual's page rule (F107, DOCUMENTED).

### H71: what separates FaderInfo bytes 0 and 1

- **MASTER at half, memory fader 1 at half:** MASTER 0x76 (seq 36264), record 1 `94 94 00 00` (seq 36521), while
  ChannelLevels[7], [8] and OutputLevels[8], [9] read **0x45** (seq 36536, 36517) = 0x94 × 0x76 / 255 (68.5). When the
  MASTER returned to 0xFF the level objects rose to 0x94 (seq 37587, 37591) and the record did not change. **In memory mode
  both bytes are pre-MASTER**; the level objects are post-MASTER (F96). In channel mode byte 0 is post-MASTER (F96) and
  post-BLACKOUT (F125).
- **A bump at an intermediate BUMPS level:** BUMPS 0x81 (body[193], seq 40144), memory fader 2 at 0x49 (`49 49 00 00`, seq
  40275, levels ≈ stored × 0x49 / 255, seq 40283). Bump 2 held: **record 2 `81 81 00 00`** (seq 40318) and the `GROUP 2`
  levels ≈ stored × 0x81 / 255 (seq 40326: channels 1–6 `17 21 2B 33 31 2E`, 9–10 `31 38`, 13–14 `0A 2A`); released: `49 49
  00 00` and the 0x49 levels again (seq 40651 … 40659).
- **Bytes 0 and 1 were equal in both cases** and in all 87 non-zero memory-mode samples. Nothing separated them; H71 stays
  HYPOTHESIS, narrowed: both carry the memory's level before the MASTER, and a held bump at the BUMPS level shows in both
  (whether as the maximum of fader and bump or as a replacement is undecided: the bump level 0x81 exceeded the fader's
  0x49).
- H54 / F97 at an intermediate BUMPS level: the bump output follows the BUMPS level (0x81) as at 0xFF. F95: BUMPS read 0x81
  at rest.

### H73: CLEAR × 1 … × 4

Setup: channel fader 5 at full in channel mode, then memory mode and memory fader 1 at full (both physically up). In
memory mode FaderInfo record 5 read 0 and LED block 5 blinked `46 00 00 0A 00 00` from the mode switch on (seq 43209,
43217): fader 5 was up but not in control of memory 5.

| Series | Lists | Changes | Reading |
|---|---|---|---|
| × 1 (860.571) | [21] seq 44344 | none | One press does nothing (outside a record function) |
| × 2 (868.092) | [21] × 2, then [21, 15], [13, 21] | channel 5's output: LCD body[7] → blank (seq 44735), ChannelLevels[5] FF → 00 (seq 44739), OutputLevels[6] FF → 00 (seq 44746); no FaderInfo change | Two presses clear the channel (intensity) level |
| × 3 (874.554) | [21] × 4, then [14, 22, 21, 15, 13] | memory fader 1: FaderInfo record 1 `FF FF` → `00 00` (seq 45105), LED block 1 → `46 00 00 0A 00 00` (seq 45109), LCD cells 7, 8 (seq 45113), ChannelLevels[7], [8] (seq 45117), OutputLevels[8], [9] (seq 45121) → 0 | Three presses set the memory faders to zero |
| × 4 (881.503) | [21] × 4 | none | Four presses: nothing visible (no sequence fader was up) |

- The physical faders stayed up (owner). Block 1 blinked until the owner pulled fader 1 down (seq 48097, 934.895, steady
  `46 00 00 46 00 00`, no FaderInfo change); in channel mode block 5 blinked green `00 46 00 00 0A 00` (seq 48211) until fader
  5 reached zero (seq 48236).
- This is the manual's count rule (F109, DOCUMENTED: 2 presses channel faders and Stack, 3 memory faders, 4 sequence
  faders), checked here for 1, 2 and 3; 4 had nothing to clear. Each series came 6.5–7.5 s after the previous one, so the
  series did not add up.
- **H73: CONFIRMED (OBSERVED once)** for three presses, refined: two presses clear the channel intensities and leave the
  memory faders.

### BLACKOUT with a level

See F114 above and new fact F125: BLACKOUT zeroes FaderInfo record byte 0 (channel mode), ChannelLevels, OutputLevels, the
LED G byte and the LCD cell of the channel.

### The power-on list (H65) and the counter after a power cycle (F80, H31)

See "The power-on list": two records, LedStatus and DisplayContent, pseq 0x00, after an idle first reply; byte-identical
to E3b session 1. H65 (a) supported twice (E3b with SmartSoft, E8b with our host); F80's pseq 0x00 after a power cycle
seen for the third time.

## GO with STACK lit and the crossfaders

The scripted step (`go with stack lit` 1062.036 … `go with stack done` 1070.215) only activated STACK. The GO presses, the
crossfader moves, STACK off and the CLEAR presses came later and are unlogged (owner statements); the data time them.

| t (s) | seq | Data | Reading |
|---|---|---|---|
| 1062.688 | 56716, 56724, 56728 | List [17, 21, 22]; LCD "Live:14.0 Next: 1.0" / 0x86 "Mem:01/01 Mem:01/01"; block 60 `FF FF 00 FF FF 00` | STACK active, stack at step 0 (record 5 `00 00 FF 00 …`, unchanged since 232.8 s) |
| 1088.363 | 58654 | **List [17, 3, 22, 13, 15, 21]**: SequenceMain listed | GO #1 (the owner: "I press play button once") |
| 1088.422 | 58658 | Record 5 `00 00 FA 05 00 00 01 00 01 00 00 32 00 32 00 00` | The crossfade starts: Live 0xFA, Next 0x05, times 5.0 / 5.0 |
| 1088.471 | 58662 | SequenceMain body `13 02 00 00 01 00 01 00 03 06` + 36 zero bytes | Byte-identical to E2's, E3's and E3b's SequenceMain (19 cues; header 2, 0, 256, 256; F74) |
| 1088.533 | 58666 | Block 50 `00 00 FF 00 00 FF` | GO LED blue (H64) |
| 1088.6–1093.4 | 58670 … 59032 | Channels 7, 8 (ChannelLevels[7], [8]; OutputLevels[8], [9]) rise 0x0E … 0xFF; LCD line 2 bars body[21] 0x86 → 0x80 (Live) and body[31] 0x80 → 0x86 (Next) | **The stack's output appears in the level objects** (cue 1.0 = `$$MEMORY 0 0` = `GROUP 1`, channels 7, 8 at 100 %) |
| 1089.4–1092.4 | 58727 … 58953 | Record 5 times `00 32` → `00 28` → `00 1E` → `00 14` → `00 0A` in 1-s steps, Live + Next = 255 | Timed fade, as F101 |
| 1093.478 | 59036 | Record 5 `01 01 FF 00 00 00 01 00 01 00 00 32 00 32 00 00` | Fade complete: bytes 0–1 = 1, pair reset to `FF 00`, times back to 5.0 |
| 1093.574 / 1093.697 | 59043 / 59051 | LCD "Live: 1.0 Next: 2.0" / "Mem:01/01 Mem:01/02"; block 50 back to `0A`×6 | Step 1 live (the owner: "the step goes from 14 to 1") |
| 1096.050 | 59236, 59240 | List [3, 17, 22, 15, 13, 21]; SequenceMain again (identical) | GO #2 |
| 1096.2–1101.4 | 59248 … 59626 | Block 50 blue; `GROUP 2` fades in and channels 7, 8 out; record 5 completes `02 02 FF 00 …` (seq 59603); LCD "Live: 2.0 Next: 3.0" / "Mem:01/02 Mem:01/04" (seq 59622); block 50 off (seq 59626); the levels end at `GROUP 2`'s stored values | Step 2 live ("from step 1 to 2"); cue 2.0 = `$$MEMORY 0 1` |
| 1104.752–1106.499 | 59880 … 60009 | Record 5 byte 2 (Live, body[67]) `FF` → `EA` → `DB` → `5A` → **`00`** (seq 59933) → `04` → `21` → `9D` → `FF`; byte 3 stays `00`; `GROUP 2` levels fall to 0 (seq 59929) and return (seq 60024, 60028); block 50 `37 37 FF 0A 0A 0A` blinking (seq 59891) until seq 60036 | Crossfader A ("A … up, then down") |
| 1107.093–1108.835 | 60050 … 60174 | Byte 3 (Next, body[68]) `00` → `02` → `32` → `AD` → **`FF`** (seq 60107) → `E5` → `CD` → `5C` → `00`; byte 2 stays `FF`; channel 2 rises from 0x40 to 0x4D and back (cue 3.0 = `GROUP 4` "DUCH", `CHAN 2/30`); block 50 blinking (seq 60061 … 60201) | Crossfader B |
| 1109.664–1110.381 | 60240 … 60289 | Byte 2 down and byte 3 up together (`FF 06`, `EB 1B`, `5A AB`), then **`03 03 FF 00 …`** (seq 60289); LCD "Live: 3.0 Next: 4.0" / "Mem:01/04 Mem:01/02" (seq 60293); `GROUP 2` to 0, channel 2 at 0x4D (seq 60297, 60301); block 50 off (seq 60305) | Both crossfaders: the manual fade completes to step 3 |
| 1114.182–1114.482 | 60572 … 60595 | Fader view with channel 2's cell `0x81` (seq 60580); block 60 → `46 46 00 46 46 00` (seq 60595); record 5 stays `03 03 FF 00` | STACK off; the stack's output (channel 2) stays |
| 1117.349–1117.954 | 60808 … 60849 | DisplayContent re-listed (seq 60808), then channel 2's LCD cell (seq 60841), ChannelLevels[2] and OutputLevels[3] 0x4D → 0 (seq 60845, 60849) | CLEAR presses (the manual: two presses clear channel faders and the Stack, F109; presses not countable) |

Readings:
- **PlaybackInfo record 5 is the stack's crossfader (H50), now with STACK lit:** a timed GO fade moves Live / Next in
  opposite directions summing to 255 (as F101); **the manual crossfaders move the two bytes independently** (18 samples
  with sums from 0 to 510, seq 59880 … 60270): byte 2 = the Live (outgoing) level, byte 3 = the Next (incoming) level.
  Which physical fader moves which byte (A = byte 2, B = byte 3) rests on the owner's order of moves (INFERRED); the manual
  (printed pp.60, 67, DOCUMENTED) names the Live fader A and the Next fader B, does a crossfade "by moving the crossfaders
  from the down position to the up position", and has A at 100 % when down. "To create a blackout between two steps, move
  only the Live fader to it's end position" (printed p.60) matches A alone: the outgoing levels went to 0 (seq 59929).
  **F101's "always sums to 255" holds for timed fades only.**
- **Record 5 bytes 0–1 count the stack steps:** `00 00` with "Next: 1.0", `01 01` with "Live: 1.0 Next: 2.0", `02 02` with
  "Next: 3.0", `03 03` with "Next: 4.0", changing when a fade completes (seq 59036, 59603, 60289). HYPOTHESIS H76: bytes 0–1
  are the 0-based index of the Next cue (equivalently the 1-based index of the Live cue, 0 = step 0).
- **The LCD names the cues and memories of `SHOW06.ASC`'s stack:** cue 1.0 `$$MEMORY 0 0` = "Mem:01/01", 2.0 `0 1` =
  "Mem:01/02", 3.0 `0 3` = "Mem:01/04", 4.0 `0 1` = "Mem:01/02". The level objects carry the cue's memory (`GROUP 1`, `GROUP
  2`, `GROUP 4`).
- **GO with STACK lit changes the output** (channels 7, 8; then `GROUP 2`; then channel 2), unlike E3b's GO with STACK unlit
  (F101: no level object changed).
- **Block 50** is blue `00 00 FF 00 00 FF` during both timed fades (seq 58666 … 59051, 59248 … 59626) and blinks `37 37 FF
  0A 0A 0A` while a manual fade is incomplete (three windows), `0A`×6 otherwise: supports H64 (GO LED). Discrepancy: the
  manual says of GO "Starts a crossfade. The button will blink during the fade" (printed p.60, DOCUMENTED), whereas block 50
  is steady during the GO fades and blinks only during the manual ones (UNKNOWN whether block 50 is another LED or the firmware differs from the manual here).
- **SequenceMain is listed at each GO** (seq 58654, 59236), unchanged. E3b listed it at the start and the end of its GO fade;
  E8b at the start only (2 of 2). Our session fetched it as any listed record (it is in `ObservedListedIds`).
- No FaderInfo change during the crossfader moves: FaderInfo carries no crossfader position (the A / B faders are not among
  its 48 records or bytes 193 / 194, OBSERVED once).

## FaderInfo, LED and LCD details (E8b)

| Item | E8b | Status |
|---|---|---|
| FaderInfo bytes 0 / 1, memory mode | Equal in 87 of 87 non-zero samples (records 1, 2, 21); pre-MASTER (0x94 at MASTER 0x76) | OBSERVED (H71) |
| FaderInfo byte 0, channel mode | Post-MASTER (F96) and post-BLACKOUT (seq 26987); byte 1 = 0 in 16 of 16 | OBSERVED |
| FaderInfo byte 3 | Page − 1 per fader; a fader above zero keeps its page until it reaches zero | OBSERVED (H69 CONFIRMED) |
| FaderInfo record 21 byte 2 | 1 on page 1, 0 on page 12; stays 1 while fader 21 moves (`16 16 01 00` … `FF FF 01 00`) | OBSERVED (H70) |
| FaderInfo records 25–48 | Never changed (record 25 `00 00 01 00`) | OBSERVED (H61) |
| A physical fader that is up when memory mode is entered (fader 5, raised in channel mode) | Its record reads 0 and its bump LED blinks dim red (record 5, block 5, seq 43209, 43217) until it is matched | OBSERVED once (H79) |
| LED blocks 25–48 | `0A 00 00 0A 00 00` in all 97 payloads (E2, E3, E3b, E8: `00 0A 00 00 0A 00` in all) | OBSERVED (H78) |
| LCD body[15] | `+` while fader 21's sequence put out channels 37, 38, 45 | OBSERVED once (H77) |
| LCD default times | "U 5.0" / "D 5.0" in every fader view of the session (as E8; E3b 5.1) | OBSERVED (F102) |
| ConsoleStatus body[0] | Nine transitions, each with the LED change of blocks 1–24, 55, 58 (seq 1649 … 50073) | OBSERVED (F87, F98) |

## Reproduction of earlier facts

| Facts | Reproduced in E8b? | Evidence |
|---|---|---|
| F41 product string | Windows read STRING 2 "ETC SmartFade" at the bind (f16) | f9 … f16 |
| F42 descriptors | The injected device descriptor `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01` equals E3b's; configuration descriptor with the HID interface (0x81 / 0x02) and the bulk interface (0x04 / 0x83) | f1 … f6 (synthetic) |
| F51 transport | Bulk 0x04 / 0x83 on interface 1 only; **no data on the HID interface**, now seen on the bus for our host (0x81 reads cancelled and resubmitted at the bind, never completed; nothing on 0x02) | usbpcap JSONL |
| F52–F55, F57 | As in E8, a second session of our host (stale rule 27 483 of 27 483, echo 2 315 of 2 315, lists = fetches 2 315 of 2 315, counter 0 … 244 unbroken, byte 1 = 0 and LE length in 3 829 of 3 829) | Session health |
| F56 / F94 reading without writes | The power-on list on the second reply, fetched with polls and fetches only | seq 2 … 13 |
| F64 silent end | The last exchange is an idle poll / reply; **no frame of any kind follows on the bus** | seq 61111, 61112; f125929 … f125932 (the split's last frame) |
| F67 body lengths | 9 ids incl. SequenceMain (46), each with its F67 length | Session health |
| F80 / H31 counter after a power cycle | pseq **0x00** in the first payload after the owner's power cycle, the third power cycle observed (E3, E3b, E8b) and the first with our host | seq 5 |
| F82 FaderInfo, MASTER = body[194], F95 BUMPS = body[193] | MASTER 0xFF → 0x76 → 0xFF (seq 36132 … 37595); BUMPS 0x00 → 0x81 → 0x00 (seq 40074 … 41365), the first intermediate BUMPS level at rest | above |
| F83 level mapping | Channel 5 / DMX 5; `GROUP 2` on channels 1–6, 9–10, 13–14; channels 7, 8; 49, 50 / DMX 47; **channels 37, 38, 45 / DMX 37, 38, 45** (new, the sequence) | above |
| F84 page-1 memory map, F85 MASTER blink | Page-1 map (seq 7181 …), page-12 map (seq 12003); block 53 `37 37 FF 0A 0A 0A` while the MASTER was below full (seq 36136 … 37595) | above |
| F86 LCD | Layout and glyphs; E8's texts seen again; new: the stack steps "Live: 1.0 Next: 2.0", "Live: 2.0 Next: 3.0", "Live: 3.0 Next: 4.0" with "Mem:01/02", "Mem:01/04", the crossfade bars in line 2 (body[21], body[31]) and the sign `+` at body[15] | `lcd` |
| F87 / F98 body[0] = mode | Nine more transitions with the LED change of blocks 1–24, 55, 58 | seq 1649 … 50073 |
| F96 post-MASTER levels | In memory mode: levels 0x45 at fader 0x94 and MASTER 0x76, 0x94 at MASTER 0xFF (the record itself is pre-MASTER, F126) | seq 36536, 37591 |
| F97 / H54 bump output | At BUMPS 0x81: levels ≈ stored × 0x81 / 255, record `81 81` | seq 40318 … 40326 |
| F99 records 1–24 = physical faders, 25–48 unused | Records 1, 2, 5, 21 move with their physical faders; records 25–48 never change | `census` |
| F101 PlaybackInfo record | Record 1 (sequence) and record 5 (stack) in timed fades: pair sum 255, reset, times counting; **with the manual crossfaders the pair does not sum to 255** (seq 59880 … 60270) | above |
| F102 default times | "U 5.0" / "D 5.0" in every fader view (E8 too; E3b 5.1) | `lcd` |
| F108 independents | Not the default patch: IND 2 → DMX 47, IND 1 unpatched (as E8), now named | seq 16387 |
| F109 CLEAR | The count rule for 1, 2 and 3 presses | H73 |

## Hypotheses touched

| H | Verdict (OBSERVED at most) | Key evidence (E8b seq / frame) |
|---|---|---|
| **H66** console reports to a host without the System writes | **Supports; second session** (1 121 s, 1 513 lists after the power-on list, every scripted and unlogged action seen) | lists; action table |
| **H67** cadence tolerated | **Supports for the cadence used** (idle 31.10–47.76 ms, fetch 28.01–44.47 ms after its list, poll 15.11–31.68 ms after a fetch); the floors still never reached | Cadence table |
| **H68** no string-descriptor reads needed | **CONFIRMED (OBSERVED once):** no EP0 frame at the backend start, at Connect or at Disconnect; the last string read (at the bind) was 78 s before the first poll; the console answered normally | f7 … f18, f19 … f28 |
| **H69** per-fader page byte | **CONFIRMED (OBSERVED once), as predicted** | seq 29992, 29996, 33741, 33757 |
| H70 record byte 2 = sequence slot | **Supports** (record 21: 1 on page 1, 0 on page 12, four transitions; 1 while fader 21 moves and runs the sequence); 22–24 untested | seq 11980, 13737, 29996, 34862, 50142 … 50200 |
| **H71** bytes 0 / 1 | **Inconclusive, narrowed:** equal in 87 of 87 memory-mode samples, including MASTER at half (both pre-MASTER) and a bump at BUMPS 0x81 (both 0x81) | seq 36521, 36536, 40318 |
| **H72** key LEDs | **Supports; CONFIRMED in capture for 60 (STACK), 63 (COPY), 64 (REC SEQ), 66 (REC MEM), 67 (EDIT MEM), 72 (IND 1), 73 (IND 2)** by timed presses of single keys; 57 (CLEAR) and 65 (MAGIC) stay INFERRED (the CLEAR key never changed block 57 by itself; MAGIC was not pressed) | seq 15620, 16395, 17961, 18923, 19943, 20871, 56728 |
| **H73** CLEAR | **CONFIRMED (OBSERVED once)** for three presses; refined: two presses clear the channel intensities, one and four did nothing here | seq 44344 … 45482, 48097 |
| H74 Rate / Fade overrides | **Inconclusive** (not exercised; bytes 4–9 `00 00 01 00 01 00` in all five records of 1 305 payloads) | PlaybackInfo |
| H75 ZLP after a 64-multiple list | **Untested** (largest list 6 records; no 64-multiple IN read; no zero-length IN on the bus) | usbpcap JSONL |
| H31 counter not reset by a session, 0 after power-on | Supports (pseq 0 after the third power cycle) | seq 5 |
| H50 PlaybackInfo layout | **Supports:** record 5 = the stack on the crossfader (GO and manual fades with STACK lit); record 1 = page 1's sequence on fader 21 (timed start) | seq 58658 … 60289; 50146 |
| H54 bump at the BUMPS level | **Supports at 0x81** (first intermediate level) | seq 40318 … 40326 |
| H63 PlaybackInfo bytes 0–1 = position | **Supports for record 5:** four stack positions matched to the LCD (`00` Next 1.0, `01` 2.0, `02` 3.0, `03` 4.0); record 1 as in E8 (129 changes in order); successor reading H76 | seq 59036, 59603, 60289; 50146 … 61110 |
| H64 block 50 = GO LED | **Supports:** blue during both GO fades, blinking during incomplete manual fades; discrepancy with the manual's "will blink during the fade" (printed p.60) for GO fades | seq 58666, 59051, 59248, 59626, 59891 … 60305 |
| H65 power-on list | **Supports (a):** power on, nothing touched, connect → LedStatus and DisplayContent, byte-identical to E3b session 1 | seq 5 |
| H53 ConsoleStatus | Supports (body[0] mode, nine transitions; body[2] page, four) | seq 1649 … 50073 |
| H55 sequence slots | Supports (block 21 full yellow while fader 21 runs the sequence; REC SEQ 22–24) | seq 50150, 19943 |
| H56 indicator LEDs | Block 53 tied to the BLACKOUT key with times | seq 14512, 15089, 26999, 27750 |
| H58 live objects only when listed | Supports; SequenceMain was listed (not a 157-list object fetch: listed by the console at each GO) | seq 58654, 59236 |
| H61 records / blocks 25–48 unused | Supports (no change within E8b); new: their constant colour differs between sessions (H78) | `census`, blocks 25–48 |
| H11 / H26 / H44 independents | Supports with named keys (record 1 = IND 1 = channel 49; record 2 = IND 2 = channel 50) | seq 15616 … 17130 |
| H5 / H6 | Not E8b's business (no write) | — |

## Golden-test candidates (E8b; inbound bytes for decoders only)

The outbound transfers are our encoder's output: the bus shows they were sent as logged, but they stay circular as evidence
for the encoder, whose golden bytes remain SmartSoft's (E2 seq 6, 9, 10, 14). The inbound bytes below are the console's.

| seq (frame) | Content |
|---|---|
| 2, 4, 5 (f22, f26, f28) | Idle first reply; type-4 header `04 00 11 00 …`; the power-on list `00 00 0C 00 28 00 02 16 00 00 00 00 15 00 00 00 00` (byte-identical to E3b f20) |
| 26987, 26991, 26995, 26999, 27003 | BLACKOUT with channel 5 at full: FaderInfo body[17] = 0, ChannelLevels[5] = 0, OutputLevels[6] = 0, LED block 5 G = 0 with block 53 blue, LCD cell blank |
| 29992, 29996, 30022, 33741 | H69: page 12 with fader 1 up (record 1 `FF FF 00 00`, records 2–24 `00 00 00 0B`), block 1 blinking `C8/0A`, record 1 `00 00 00 0B` at zero |
| 36521, 36536, 37591 | Memory-mode record `94 94 00 00` at MASTER 0x76 against levels 0x45; levels 0x94 at MASTER 0xFF |
| 40318, 40326 | Bump 2 at BUMPS 0x81: record `81 81 00 00`, scaled `GROUP 2` |
| 44735, 44739, 44746 / 45105, 45109, 45117, 45121 | CLEAR × 2 (channel 5 cleared) / CLEAR × 3 (memory fader 1 zeroed, block 1 blinking) |
| 15616, 15620, 15624 / 16383, 16387, 16391, 16395 | IND 1 on (named) / IND 2 on (named) |
| 17957 / 17961, 18919 / 18923, 19939 / 19943, 20867 / 20871 | The four record-function screens, each key pressed alone and timed |
| 56724, 56728 | STACK active: LCD "Live:14.0 Next: 1.0", block 60 full |
| 58654, 58662 | List naming SequenceMain at GO; SequenceMain body |
| 58658, 59036, 59043, 59603, 59622 | Record 5 during and after the two GO fades; the LCD step texts |
| 59933, 60107, 60270, 60289, 60293 | Manual crossfade: Live 0 / Next 0, Live FF / Next FF, `5A AB`, completion `03 03 FF 00`, LCD "Live: 3.0 Next: 4.0" |
| 58666, 59891 | Block 50 blue (GO fade) and blinking (manual fade) |
| 50142, 50146, 50150, 50154, 50158, 50162 | Fader 21 starts the sequence: record 21 `16 16 01 00`, record 1 step 0, block 21 full yellow, channels 37 / 45, LCD `+` |
| 52054, 52077, 61110 | Fader 21 at zero, block 21 dim, record 1 still stepping at the end |
| 9 = f36 + f38 | A 456-byte read that is two URBs on the bus (448 + 8): for a replay or pipe test that has to accept WinUSB's split, not for a decoder |

## For the curator (proposed; nothing applied here)

**Status changes.**

| Id | Current | Proposed | Evidence (E8b) |
|---|---|---|---|
| F110 | OBSERVED (one session) | **VERIFIED** (E8 + E8b, our host): a read-only session of polls and listed fetches only, every poll answered, lists fetched in order, unbroken counter, no fault, cadence above the floors, every front-panel action reported. Counts and intervals stay per session | Session health; F122 |
| F111 | OBSERVED | **Unchanged**; annotate: not reproducible by design (history-specific); after a power cycle the pending list held 2 records (F124 below) | seq 5 |
| F112 | OBSERVED | **VERIFIED** (structure: memory fader k = record k with bytes 0 = 1; levels = stored levels scaled by the fader, 48 of 48 `GROUP 2` bytes again; LED k full red while up); proportional playback stays INFERRED | seq 8417 … 11021, 10703, 10707 |
| F113 | OBSERVED | **VERIFIED** (structure: body[2] = page − 1, byte 3 = page − 1, record 21 byte 2 = 1 / 0, the MEMS page view, the page-12 map; the show equals `SHOW05.ASC`); per-fader reading → H69 CONFIRMED | seq 11934 … 13760, 29992, 34858 |
| F114 | OBSERVED (pattern); BLACKOUT identity INFERRED | **VERIFIED** for block 53 `00 00 FF 00 00 FF` steady while BLACKOUT is on, toggling with the key; the identity is OBSERVED with a timed key. The masking of the MASTER blink stays OBSERVED (E8 only); the level-object part is F125 | seq 14512, 15089, 26999, 27750 |
| F115 | OBSERVED; key identities INFERRED | **VERIFIED** (structure), key identities OBSERVED (named, timed): IND 1 = record 1 / block 72 / channel 49; IND 2 = record 2 / block 73 / channel 50 → DMX 47 | seq 15616 … 17130 |
| F116 | OBSERVED; key identities INFERRED | **VERIFIED** (the four screens' LCD texts and LED states, one LedStatus to open and one to close, blinking as phase A ≠ B); key identities: H72 | seq 17957 … 21214 |
| F117 | OBSERVED; readings INFERRED | **VERIFIED in part**: block 60 → `FF FF 00 FF FF 00` and the LCD "Live:14.0 Next: 1.0" when STACK is activated. Not reproduced: record 5 `01 01` → `00 00` (already 0). Annotate: "Live:14.0" = the stack's last cue (INFERRED from `SHOW06.ASC`); "Jump to step" = the GOTO display of a held STACK (manual printed p.61, DOCUMENTED), but the "second, held press" reading is not supported (a logged press-and-hold from an inactive stack did nothing) | seq 56724, 56728; 22973 … 23015 |
| F118 | OBSERVED | **VERIFIED in part**: record 1 is the playback of page 1's sequence on fader 21 (7-step cycle, pair sum 255, times following the page; start timed; output = page-12 memories 1–7). Annotate: moving the fader down does not stop it (F129), which explains E8's sequence without output; E8's stop at 237.519 s stays UNKNOWN | seq 50142 … 61110, 13741, 30000, 34866 |
| F119 | OBSERVED | **VERIFIED** (listed is not changed; unchanged ConsoleStatus re-listings at the page view, the EDIT MEM exit and the COPY entry again); annotate: CLEAR series re-list DisplayContent unchanged | object table; seq 19358, 20863, 44344 … 45482 |
| F120 | OBSERVED; cause INFERRED | **VERIFIED** (the one-step drop and the dim-red blink until the fader reaches zero); the cause CLEAR × 3 is OBSERVED in E8b (H73) | seq 45105 … 45121, 48097 |
| F101 | VERIFIED in part ("a Live / Next pair that always sums to 255") | **Limit the wording:** the pair sums to 255 at rest and in timed fades (GO, running sequence: 1 287 of 1 305 record-5 samples, all but the 18 below, and 1 305 of 1 305 record-1 samples); with the manual crossfaders the two bytes move independently (18 samples, sums 0 … 510). The observations stand; "always" is wrong | seq 59880 … 60270 |
| F80 | VERIFIED in part | Annotate: third power cycle, again an idle first reply and pseq 0x00 in the first payload, the first time with our host; the generalisation stays INFERRED (3 of 3) | seq 2, 5 |
| F94 | OBSERVED | Annotate: the power-on list after the third observed power cycle is byte-identical to E3b session 1's (F124) | seq 5 |
| F95 | VERIFIED | Annotate: an intermediate level at rest, 0x81 (timed) | seq 40144 |
| F96 | OBSERVED | Annotate: holds in memory mode (levels 0x45 at fader 0x94 / MASTER 0x76); the FaderInfo record is pre-MASTER there (F126) | seq 36536, 37591 |
| F97 | OBSERVED | Annotate: the bump output at BUMPS 0x81 is the stored level × 0x81 / 255 and the record reads `81 81` | seq 40318 … 40326 |
| F41, F51, F64 | VERIFIED | Annotate: seen on the bus for our host (STRING 2 at the bind; no HID data; silent end) | f16; usbpcap JSONL |
| F102 | OBSERVED | Annotate: "U 5.0 / D 5.0" in E8b too | `lcd` |
| H66 | CONFIRMED (one session) | **CONFIRMED (two sessions, E8 + E8b)** | lists |
| H67 | CONFIRMED for the cadence used | Unchanged scope; second session in the same ranges; floors untested | Cadence |
| H68 | HYPOTHESIS (untested) | **CONFIRMED (OBSERVED once)**; SC-1 item 5's WinUSB ASSUMPTION answered for this PC (no control transfer at open or Connect) | f7 … f18, f19 … f28 |
| H69 | HYPOTHESIS | **CONFIRMED (OBSERVED once)** | seq 29996, 33741 |
| H70 | HYPOTHESIS | Supported again (record 21); unchanged status | seq 11980 … 50200 |
| H71 | HYPOTHESIS | **Unchanged, narrowed:** both bytes pre-MASTER and equal in 87 of 87 memory-mode samples (MASTER at half, bump at 0x81); the separating case is still missing | seq 36521, 40318 |
| H72 | HYPOTHESIS | **CONFIRMED in capture for blocks 60, 63, 64, 66, 67, 72, 73** (timed single-key presses; identity as the key's own LED by uniqueness, the owner's account and the manual: INFERRED for the four blocks of the record keys, OBSERVED for 72, 73 and 60); **57 and 65 stay INFERRED** | seq 15620, 16395, 17961, 18923, 19943, 20871, 56728 |
| H73 | HYPOTHESIS | **CONFIRMED (OBSERVED once)**, refined: × 2 clears channel intensities, × 3 the memory faders | seq 44735 … 45121 |
| H74, H75 | HYPOTHESIS | Unchanged (not exercised) | — |
| H54 | CONFIRMED for BUMPS 0 / 0xFF | **CONFIRMED also at 0x81** | seq 40318 … 40326 |
| H63 | CONFIRMED in capture for record 1 | Add: record 5 supported with four positions matched to the LCD; successor reading H76 | seq 59036, 59603, 60289 |
| H64 | HYPOTHESIS (supported) | **Supported** (two GO fades blue, manual fades blinking) | seq 58666 … 60305 |
| H65 | HYPOTHESIS (supported in part) | Supported for (a), twice byte-identical | seq 5 |
| H50 | Layout CONFIRMED | Add: record 5 = the stack with STACK lit, under GO and the manual crossfaders | seq 58658 … 60289 |
| H61 | HYPOTHESIS | Supported again; see H78 | — |

**New facts (proposed IDs; all OBSERVED, one session, unless stated):**

- **F122 The bus view of a read-only session of our software (E8b).** USBPcap from before the bind: the 61 112 transfers of
  the backend's log equal the bus byte for byte, in order (62 957 bulk URBs, USBD status 0 in all); WinUSB issues every IN
  read longer than 64 bytes and not a multiple of 64 as two URBs, a whole-packet part and the remainder (1 845 reads: 86,
  90, 200, 456 and 519 bytes); the log's times and the bus agree within 0.75 ms (median 0.022 ms); tshark's `usb.capdata`
  equals the normalized data in 62 957 of 62 957 records. The HID interface carried no data (its two interrupt reads were
  cancelled and resubmitted at the bind and never completed); nothing on 0x02. Evidence: section "USBPcap"; f19 …
  f125932; seq 9 = f36 + f38.
- **F123 EP0 around a read-only session (H68).** The only console EP0 traffic in the capture besides the injected
  descriptors is a burst at the Zadig bind (20:36:45.016–.094, f7 … f18): GET_DESCRIPTOR STRING 2 (wLength 4, then
  28: "ETC SmartFade"), HID SET_IDLE to interface 0 (stalled), GET_STATUS (`01 00`), and the HID interrupt reads cancelled
  and resubmitted; no re-enumeration (address 10 throughout). Nothing at the backend start, at Connect (20:38:03) or at
  Disconnect; the console answered our first poll (78 s after the last string read) with an idle reply and the second with
  the power-on list. Which component issued the burst is not visible (INFERRED: the restart after the driver change).
  H68 CONFIRMED.
- **F124 The power-on list, byte-identical a second time.** After the owner's power cycle (the script asked him to touch
  nothing until Connect) the first reply was idle and the second carried `00 00 0C 00 28 00 02 16 00 00 00 00 15 00 00 00 00` (pseq 0x00;
  LedStatus, DisplayContent; seq 5), the same 17 bytes as E3b session 1 (f20). The other live objects came only with the
  first action that changed them (ConsoleStatus 21.8 s, FaderInfo and the levels 165.5 s, PlaybackInfo 232.6 s,
  Independents 304.0 s, SequenceMain 1088.4 s). At power-on the LEDs showed memory mode (page-1 map, block 58 red) and the
  LCD the "25" / "37" row labels (seq 9, 13; the mode is INFERRED, ConsoleStatus came later); E3b's power-on showed channel
  mode 1-24. F80, F94, H65.
- **F125 BLACKOUT zeroes the live level objects.** With channel fader 5 at full, BLACKOUT on set FaderInfo body[17],
  ChannelLevels[5], OutputLevels[6] (DMX 5), LED block 5 G and the LCD cell body[7] to 0 within 0.27 s, with block 53 steady
  blue, and BLACKOUT off restored all five (seq 26987 … 27003, 27750 … 27766; MASTER 0xFF throughout). The owner: the
  fixture went dark. Without a level only block 53 changes (seq 14512, 15089; as E8). Extends F96 (post-MASTER) to
  BLACKOUT; answers F114's open point.
- **F126 Memory-mode FaderInfo records are pre-MASTER; a held bump shows at the BUMPS level.** Record 1 read `94 94 00 00`
  with the MASTER at 0x76 while channels 7, 8 read 0x45 (= 0x94 × 0x76 / 255), and the MASTER back at 0xFF changed the
  levels to 0x94, not the record (seq 36521, 36536, 37591). Bump 2 held at BUMPS 0x81 with fader 2 at 0x49: record 2 `81 81
  00 00` and `GROUP 2` ≈ stored × 0x81 / 255; released `49 49 00 00` (seq 40318 … 40659). Bytes 0 = 1 in 87 of 87 non-zero
  memory-mode samples, byte 1 = 0 in 16 of 16 channel-mode samples. Contrast: in channel mode byte 0 is post-MASTER (F96)
  and post-BLACKOUT (F125). H71, H54.
- **F127 A fader held across a page change keeps its page (H69).** Memory fader 1 at full, page 12 selected: ConsoleStatus
  body[2] = 0x0B, FaderInfo record 1 `FF FF 00 00` while records 2–24 read `00 00 00 0B`; channels 7, 8 stayed at full;
  LED block 1 blinked `C8 00 00 0A 00 00` over the page-12 map; at zero the record read `00 00 00 0B` and the block steady
  `46 00 00 46 00 00` (seq 29992, 29996, 30022, 33741, 33757). The owner saw the blink and read it as "a different source".
  Manual F107 (DOCUMENTED).
- **F128 CLEAR series (H73).** In memory mode with channel fader 5 (raised in channel mode) and memory fader 1 up: CLEAR ×
  1 changed nothing; × 2 cleared channel 5's output (ChannelLevels[5], OutputLevels[6], LCD) and left memory fader 1;
  × 3 set memory fader 1 to zero in one step (record `FF FF` → `00 00`, channels 7, 8 / DMX 7, 8, LCD) with LED block 1
  blinking `46 00 00 0A 00 00` until the fader reached zero; × 4 changed nothing (no sequence fader up). Every series
  re-listed DisplayContent unchanged (seq 44344 … 45482, 48097). Matches the manual's count rule (F109) for 1–3 presses.
- **F129 A sequence is started by its fader and keeps running at fader zero.** Fader 21 up on page 1 (memory mode)
  started PlaybackInfo record 1 at step 0 with FaderInfo record 21 `FF FF 01 00`, LED block 21 `FF FF 00 FF FF 00` and output
  on channels 37, 38, 45 (DMX 37, 38, 45) exactly as page 12's memories 1–7 (`GROUP 265`–`271` of `SHOW06.ASC`); an LCD `+`
  at body[15] while the output was on. Fader 21 down took record 21, the output and block 21 back, but record 1 kept
  stepping (129 steps in order, median 1.124 s) until the end of the session (seq 50142 … 52081, 61110). The owner calls
  fader 21 down "stop". Explains E8's running sequence without output (F118).
- **F130 GO with STACK lit, and the manual crossfaders, in PlaybackInfo record 5.** STACK active (block 60 full, LCD
  "Live:14.0 Next: 1.0"); each GO (unlogged, owner-reported) listed SequenceMain (unchanged body) with PlaybackInfo, turned
  block 50 blue and ran a 5-s fade in record 5 (pair summing to 255, times 5.0 → 1.0), with the cue's memory fading in the
  level objects; at completion record 5 read `01 01 FF 00 …` and then `02 02 FF 00 …`, with the LCD "Live: 1.0 Next: 2.0" and
  "Live: 2.0 Next: 3.0" (cues and memories as `$SEQUENCE 48` of `SHOW06.ASC`). The manual crossfaders moved byte 2 (Live) and
  byte 3 (Next) independently (sums 0 … 510; block 50 blinking `37 37 FF / 0A 0A 0A`) and completed the fade to `03 03 FF 00`
  ("Live: 3.0 Next: 4.0"). STACK off left record 5 and the output (channel 2 at 0x4D) until CLEAR cleared the output. No
  FaderInfo change throughout (seq 56716 … 60849). E3b's GO with STACK unlit changed no output (F101).

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H76 | PlaybackInfo record bytes 0–1 are the 0-based index of the Next step (the step being faded in, or the one the next GO fades in): a stack (record 5) advances when a fade completes and resets the pair to `FF 00`; a running sequence (record 1) keeps `k k 00 FF` at the end of a fade and advances when the next fade starts | E8b record 5: `00` with "Next: 1.0", `01` with "Next: 2.0", `02` with "Next: 3.0", `03` with "Next: 4.0" (seq 59036, 59603, 60289); record 1 in E8 and E8b (successor reading of H63). E3b's GO with STACK unlit left record 5 at `06 06` before and after its fade (F101), so a GO advances the index only with the stack loaded (OBSERVED in two sessions, one case each); manual printed p.60: "If STACK is off the Crossfader operates only the Single Scene Crossfading function" (DOCUMENTED) | Passive, logged: GO from a later step, and a GOTO (held STACK, if that is what it does) to a known step; compare bytes 0–1 with the LCD's Next |
| H77 | The LCD sign `+` at body[15] (line 1, after channel 12's cell) marks output on channels outside the displayed bank (25–48 while 1–24 are shown) | E8b: `+` exactly while fader 21's sequence put out channels 37, 38, 45 (seq 50162 … 52081). The console manual's extracted text has no `+` besides telephone numbers and a list of illegal file-name characters (text search, 2026-10-30) | Passive, logged: in bank 25-48 raise a channel fader (channel 25–48), switch the bank to 1-24 and back; expect `+` while 1-24 is shown |
| H78 | LED blocks 25–48 take a colour from the console's state at power-on (dim green `00 0A 00` after a channel-mode power-on, dim red `0A 00 00` after a memory-mode power-on) and keep it for the session | E2, E3, E3b, E8: `00 0A 00 00 0A 00` in every payload; E8b (power-on in memory mode, F124): `0A 00 00 0A 00 00` in all 97 payloads | Passive: two power cycles, one in channel mode and one in memory mode; compare blocks 25–48 in the first LedStatus |
| H79 | A bump LED blinks while its physical fader is out of step with the level it controls (after CLEAR, after a mode change with the fader up, or after a page change with the fader up) and stops when the fader is brought to zero; red in memory mode, green in channel mode | E8 F120; E8b block 1 after CLEAR × 3 (seq 45109 … 48097), block 5 after entering memory mode with fader 5 up (seq 43217), and green in channel mode after CLEAR × 2 had cleared channel 5 (seq 48211 … 48236), block 1 on page 12 with fader 1 up (seq 30022 … 33757); manual F8 ("will blink until the actual level is matched by that of the fader") | Passive, logged: each case once more with a single fader, noting the LED and the moment it stops |
| H80 | In PlaybackInfo record 5, byte 2 follows crossfader A (Live) and byte 3 crossfader B (Next); A gives 100 % Live when down, B 0 % Next when down, so moving A up lowers byte 2 | E8b: the owner's order (A, then B, then both) against the bytes (seq 59880 … 60289); manual printed p.60 ("Live (A) and Next (B) fader", a crossfade moves them "from the down position to the up position") and p.67 ("Crossfader A is at 100%" when down, B "at 0%"), DOCUMENTED | Owner, zero risk: say where A and B stand before and after a manual crossfade; then a logged single move of A |

## Surprises and anomalies

1. **The pcap was first committed unsplit; resolved by the console-only split.** Commit 0de6d17 (on `origin/main`) added
   the raw root-hub capture under the name `2026-10-30-e8b-second-read-only-session.pcap` (SHA-256
   `fcfa1a2511d8084141f0ad90b4a15fce5c2b676f13f949757d8a76e5eedc4dbf`, 13 525 304 B, 180 806 frames). Besides the console
   (address 10, 125 932 frames) it held 54 874 frames of six other devices: address 6 52 330 (almost all interrupt IN),
   address 4 2 406 (mostly isochronous), address 3 118 (interrupt, control, bulk), addresses 1, 2 and 5 a few control
   frames; they were counted, not read, and may include keyboard or mouse input. The captures README allows only the
   console-only split. The coordinator made the split (`tshark -r <raw> -Y "usb.device_address == 10" -F pcap -w
   <split>`, SHA-256 `f653db8065cd5c8f809becafcf8f4a19a736c11c7e6e46226bec220a5a393ec2`), which replaces the raw file
   under the same name; this record, the usbpcap JSONL and the README row cite the split (completeness check in
   "Evidence files"). Removing the raw file from the pushed history is the owner's decision.
2. **Connect came 47.5 s before the script's step**, before the starting position was set: the owner's mode switches and the
   MEMS page view at 21.8–34.7 s are inside the session. Harmless (nothing outside SC-1 was sent), but the "60 s idle after
   Connect" started at 34.7 s and lasted 106.5 s.
3. **The logged STACK press-and-hold changed nothing**, and the STACK states came from an unlogged action during the
   owner's answer (447.99–448.67 s). The STACK key's behaviour is still UNKNOWN in detail.
4. **The GO part is unlogged:** the scripted "go with stack lit" only activated STACK; the two GO presses, the crossfaders,
   STACK off and CLEAR followed after the last wait (1088–1118 s). The data time them; the owner's account names them.
5. **The sequence on fader 21 was still running when the session ended** (record 1 at step 3, seq 61110): moving fader 21
   down did not stop it. The console was left with a running sequence (no output) and the stack at cue 3.0 with STACK off;
   the next session will see PlaybackInfo listed about ten times a second from its first fetch.
6. **F101's "always sums to 255" is wrong for manual crossfades** (18 samples, seq 59880 … 60270); the VERIFIED part needs
   its wording limited (curator table).
7. **E8's reading of "Jump to step" as the display of a second, held press is not supported** (F117 row); that it is the
   GOTO display of a held STACK is DOCUMENTED (manual printed p.61). Also the manual's "[GO] will blink during the fade"
   against block 50 steady during the GO fades (H64).
8. **The console powered on in memory mode with the 25-48 row labels**, and LED blocks 25–48 were dim red for the whole
   session (H78). Cause UNKNOWN (state kept from before the power-off?).
9. **SequenceMain was listed and fetched** at each GO: a show object that E8 never received (E8 record: no list named the
   157-record "full show" objects). The console lists it by itself at a GO (E3b too), and its id is in
   `ObservedListedIds` (SmartSoft fetched it in the 157-record list), so the fetch is within SC-1.
10. The reply-header latency peaked at 24.27 ms (seq 1644) during a mode switch (E8: 21.57 ms during the page view); far
    below the 500-ms timeout.
11. The firmware was not re-read and Memory Integrity was not recorded (as in E8). The pcap was written to `C:/Program
    Files/USBPcap/` (the admin window's working directory).
12. `hsp_summary.py payloads` piped into `head` ends with an `OSError` on Windows (broken pipe); cosmetic.
13. The first seven action-log notes precede the capture and carry negative `t` in the usbpcap JSONL.

**Runbook deviations:** Connect before the starting position (2); the end of the session unlogged (4); the pcap first committed
without the console-only split (1, resolved). Otherwise the E8b runbook was followed: the named backup (`SHOW06.ASC`, copied), the optional
power cycle, USBPcap before the bind, the bind check, the timed log, 5 s between actions, Disconnect at the end.

## Next capture to take

**A. Owner, zero risk:**
1. The raw capture's removal from the pushed history (Anomaly 1), the owner's decision.
2. STACK: what did you press at about 20:45:31 (448 s)? Does a short press switch the stack on and a second press switch
   it off? When does "Jump to step" appear? What does holding STACK do with the stack on?
3. The sequence on fader 21 is probably still running on the console (fader at zero). How do you stop a running sequence
   (bump 21, CLEAR × 4 with the fader up, STACK, a page change)? What stopped it in E8 at about 16:29:26?
4. The crossfaders: where do A and B stand at rest, and which way did you move them first (H80)?
5. The `+` on the LCD right of channel 12 (H77): do you know it? (A manual search may answer it too.)
6. The console's mode and bank before the power-off (it came up in memory mode with the 25-48 labels).

**B. A third read-only session (E8c), only for what still needs one** (SC-1's two messages, the same setup as E8b, the pcap
split before the commit):
- H71's separating case: memory fader 2 at full and BUMPS at a quarter, then the bump held (a bump below the fader level);
  and CLEAR × 3 with a bump held. Also BLACKOUT in memory mode with a memory fader up: F126 predicts that the record stays
  and only the level objects drop (a prediction, not an observation).
- The second half of the E8 plan's power-on item, not in the E8b script: Disconnect and Connect again without a power cycle
  (the read-only analogue of F94's sessions 2 and 3; nothing pending is expected).
- H76 / H63: STACK on, GO from step 3 to 4 and 5, a GOTO to a known step; GO with STACK off for comparison (E3b).
- The running sequence: stop it the owner's way, logged (question 3).
- H77: a channel in bank 25-48 up, view switched to 1-24. H78: two power cycles in different modes, with H65 (b) and (c)
  (power on, move one fader / press SOLO, connect).
- From the E3b list, still open: SOLO with another output up, several stable MASTER positions (H60), BLACKOUT with the
  MASTER below full and a level up (F114's masking with levels).
- H74 (RATE / Fade) only if the owner wants it; H75 cannot be forced read-only; the floors (H67) need a configuration change
  (owner decision).

**C. Tooling (zero risk):** commit the E8b ad-hoc scripts under `tools/protocol-analyzer/e8b/` (read-only; most import `hsp_summary.py`
like the E8 ones, take `<repo> <jsonl>` and print by seq): `xcheck.py` (the backend log against the usbpcap JSONL, URB by
URB, with the split reads and the clock offsets), `tshark_xcheck.py` (tshark `usb.capdata` against the usbpcap JSONL),
`census_usbpcap.py` (endpoints, USBD status, lengths), `objtable.py` (objects and latency maxima), `cadence.py` (interval
bins), `pbinfo.py`, `pb_stats.py` and `pb_rate.py` (PlaybackInfo records), `fader_bytes.py` and `fdr_at.py` (FaderInfo),
`cst_led.py` (ConsoleStatus and LedStatus per window), `cmp_first.py` (first bodies of two captures), `blk2548.py` (LED blocks
25–48 per capture) and `logtimes.py` (action-log stamps to backend t). Add the normalization command above to the analyzer
README.

**D. E9** (passive USBPcap capture of SmartSoft writing) stays the only path to a write mode. For E9: put the ETC driver
back on interface 1; split the pcap to the console's address before committing; expect PlaybackInfo to be listed
continuously while the sequence runs; the stack stands at cue 3.0 with STACK off. **E8b clears no message:** SC-1's
clearance (poll and listed fetch, read-only) is unchanged.
