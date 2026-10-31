# 2026-10-31 E12b: the second SmartSoft session editing the patch and the stack / sequences (USBPcap, passive, guided script)

**Status of this record: complete.** Provenance, completeness and structure as committed in 9968ec2; the section "Analysis"
(capture-analyst pass of 2026-10-31, late evening, with read-only tools under `tools/protocol-analyzer/`, none of which can open a
device) decodes all 62 PC → console writes, the console's answers, the two downloads, the unlogged playback of 22:40:05–22:40:47 and
the failed transfer during the SD save, and judges H91–H106. **Where E12b reproduces E12 (a separate capture, each after its own
console power cycle), the analysis proposes VERIFIED for the behaviour, as SmartSoft's and the console's, never as clearance;
everything first seen here is OBSERVED once or weaker; the curator applies the statuses.** **Nothing in this capture is cleared for
sending; our software was not involved and sent nothing, and our code never sends any of these messages** (SC-1's poll and listed
fetch remain the only cleared messages).

- **Date:** 2026-10-31, late evening. USBPcap ran 22:29:47.631071–22:58:51.038318 local time (CET, UTC+1; the first and last
  frames of the raw root-hub capture). The committed console-only split runs from 22:29:47.631071 (f1) to 22:58:42.150538 (f218846).
- **What:** the owner ran `tools/usb-probe/e12b-guided-session.ps1` (the trimmed version, commit 05b6e70) from 22:26:27 to 22:59:44
  after a console power cycle: a second, independent session for the writes E12 saw once, plus what E12 did not show (a channel
  step, the Manual run mode, inserts at two places of KOTLIK, a Direct cue with a level and a label holding "č" and a full third
  line, the IND values named, an SD save of the edited show). The optional console-side menus (H) were declined ("opt h = n").
  SmartSoft 3.0.43 was Online in two sessions (started by the script at 22:29:49 and 22:52:53; closed at 22:52:43 and 22:58:44).
- **Console:** ETC SmartFade 1248, USB device address **30** on bus 1 (27 in E12: re-enumerated after the power cycle, "power
  cycled = y" at 22:27:17). Firmware not re-read (3.0.2.9.0.43 per the E8 statements and E12's ConsoleVersion object). Memory
  Integrity off (script reading 22:29:22), interface 1 on ETC's driver (service ETCHSP, 22:29:23), USBPcapCMD on root hub `USBPcap1`
  with the script's suggested command into the repository root (`-A --inject-descriptors`, INFERRED from the injected descriptors of
  addresses 1, 2, 3, 4, 18, 19 and 30 at frame 1's time).
  *(Analysis: the ConsoleVersion object reads "3.0.2.9.0.43", body `0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33`, in both sessions:
  seq 99 f198, seq 87218 f174436; F48 / F59 again.)*
- **Show backup (safety rule 3): taken.** `SHOW14.ASC`, logged at 22:27:44, before SmartSoft was started. The EDITED show was saved
  as `SHOW15.ASC` at 22:52:12 (every test change in place), and after the restores `SHOW16.ASC` at 22:59:24. All three are in
  `smartfade-sd-card-backup/` (git-ignored). `SHOW14` and `SHOW16` both have SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`: **byte-identical to each other and to SHOW13, SHOW12, SHOW09,
  SHOW07 and SHOW04.** The show data after E12b equals the show data before it. `SHOW15` (SHA-256
  `07f88be1da9c0a46692c7b3205ddb430866cd63e338ea17ef200917068bd4196`, 16 486 B) differs from SHOW14 in exactly the edited objects
  (listed in the owner statements; for the `asc-format-analyst`).
- **Rig:** the owner's rig on DMX 5–16; DMX 100 unconnected ("dmx 100 free = y"). Channel fader 5 was raised to about half on the
  console for the Direct cue (D5) and returned to zero.
- **Owner statements:** `2026-10-31-e12b-owner-statements.txt` (the typed answers quoted, the SD comparison, the one deviation:
  stack playback moved between 22:40 and 22:43 through SmartSoft's next / PAUSE / PLAY and the console's STACK and PAUSE + PLAY while
  the owner looked for the Direct cue's row; not logged as steps). Owner statements never promote a fact by themselves.

## Files

| File | SHA-256 | Size / span | Committed | Role |
|---|---|---|---|---|
| `2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.pcap` | `4a489a13d1810b1593060b526429ca3c3e96f4ec209104b5f5c6966b4e3886b3` | 10 773 103 B, 218 846 frames, 22:29:47.631071–22:58:42.150538 | yes, **the evidence** | Console-only split (device address 30) of the USBPcapCMD root-hub capture; classic pcap, link type 249, µs; made with tshark 4.0.3: `tshark -r <raw> -Y "usb.device_address == 30" -F pcap -w <split>`. **The raw root-hub capture is not committed** (addresses 1, 2, 3, 4, 18 and 19, among them the owner's keyboard at 19): `e12b.pcap` in the repository root (git-ignored), SHA-256 `e24b78c436cff98bfbcee13d9cd2d872058c4428a9b56686face93bcabd97388`, 1 101 137 231 B, 911 995 frames (tshark field census), 22:29:47.631071–22:58:51.038318. Completeness below |
| `2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.jsonl` | `6f5bcf776ccc30b8d34dd3e673b7202143372707c40e974b30eb20083a85f9ce` | 109 537 lines: 1 header, 109 423 `frame` records, 113 `note` records | yes (derived) | The split normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 30 --bus 1` (0 other-device frames), the action log merged as notes (`--notes-log`, `--notes-utc-offset +01:00`; the first lines have negative `t`) plus two analyst notes (the raw capture's provenance; its end). Command in `normalize_e12b.sh` of the session's scratchpad, reproduced below |
| `2026-10-31-e12b-actions.txt` | `f82ba2eddfe4b40734efe43d52a6124ab6b17141a01a4afc90ac8d7efdc29140` | 111 lines, 22:26:27.099–22:59:44.193 | yes | The guided script's action log: the owner's decisions and answers, every step and wait with the PC clock |
| `2026-10-31-e12b-owner-statements.txt` | `e8bff755eed3bd25ce69ded2cce83c2f49b1bed244cca34d5e7c474e87b53d13` | | yes | The owner's answers and the chat message, quoted, with the SD comparison and the assistant's reading for the analysis |
| `smartfade-sd-card-backup/SHOW14.ASC`, `SHOW16.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` (both) | 15 944 B each | no (git-ignored folder) | The backup before and the save after the session; equal to each other and to SHOW04 … SHOW13 |
| `smartfade-sd-card-backup/SHOW15.ASC` | `07f88be1da9c0a46692c7b3205ddb430866cd63e338ea17ef200917068bd4196` | 16 486 B | no (git-ignored folder) | The edited show as the console saved it: the file forms of a Direct cue ("State") step with a label, a channel step, a point cue, two added sequence steps, Manual, the patch entry and a page name |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py`, `hsp_summary.py`, `e9/cmp_records.py` | unchanged since E9b (hashes in the E9b record) | | yes | Normalizer version 2; `summary`, `sessions`; the raw-versus-split record comparison |
| tshark / capinfos 4.0.3 (Wireshark portable) | | | no | The split, the frame census by address and the frame counts and spans |
| `tools/protocol-analyzer/hsp_summary.py` (analysis) | `3f324e9f94732235d5cbaa0ea6ce76619db4b1357612de5828db1ccfcb0fd1ec` | | yes (unchanged) | `summary`, `sessions`, `log`, `lcd`, `timeline`, `payloads --id 9 / 36 / 37`, `startdiff --other` (the E12 JSONL) for the analysis |
| `tools/protocol-analyzer/e12/writes.py`, `lists.py`, `downloads.py`, `write_timing.py`, `cut_jsonl.py` | unchanged (hashes in the E12 record) | | yes | Run on the E12b JSONL as they are |
| `tools/protocol-analyzer/e2_asc_crosscheck.py` | `8c49aedb18ff7ee4179ad9baaa2bee3a0c286788734511649a1bb5b28c4bb314` | | yes (unchanged) | Downloads 1 and 2 against `SHOW14.ASC` / `SHOW15.ASC` (run on time slices made with `e12/cut_jsonl.py`) |
| `tools/protocol-analyzer/e8b/blk2548.py` | `b3b341721122ae43370672fad2d7e1b56dd80eac87d5a03b65b064601edfd58c` | | yes (unchanged) | LED blocks 25–48 (H78) |
| `tools/protocol-analyzer/e12b/write_rules.py` | `f0c9408034c6290c1507fa31da1297c3c468d355101e987a9ace143fa7d25f82` | 8 344 B | added with this analysis | Copy of `write_rules.py` (`ba799e37…a71a094`) changed in one place: a reply-header read shorter than 12 bytes is counted and reported instead of stopping the tool (the original stops with an IndexError at seq 85008); its output for the E12 JSONL equals the E12 record's figures |
| `tools/protocol-analyzer/e12b/cmp_writes.py` | `ea3937f4431674286a3b6fb3d9fe84b5d72755f83dbf7f33b9fd489855aadf9b` | 4 451 B | added | Copy of `e12/cmp_writes.py` (`eb316aa6…a7968cca`) that matches a Sequence write with the console's Sequence bodies under any qualifier c (E12b's console listed c = 2 and 7; the original knows c = 0 and 6) |
| `tools/protocol-analyzer/e12b/playback.py` | `38384110e1d7288e3e19d908040ef1c3ad54344ae3629c92dd2b2aa7214f0f97` | 3 072 B | added | Writes, lists, PlaybackInfo records 5 and 1, LED blocks 50, 51, 58, 59, 60, 69, 70, ConsoleStatus, SequenceMain and the LCD of a time window, merged |
| `tools/protocol-analyzer/e12b/reply_latency.py` | `9a3e442b8d9e0a46327600cd9a7c2ca427f8d97dcb83ee1e4f2c5c7719d66bd5` | 1 750 B | added | Reply-header reads above a threshold or failed, their distribution, poll gaps above 100 ms |
| `tools/protocol-analyzer/e12b/cmp_bodies.py` | `dc01ca5e4d34d942203e91da28747bf9e6d6d119205f5166535734f4741abd2a` | 2 043 B | added | Payloads compared by JSONL seq (A:B; SmartSoft's 48-level layouts expanded to the console's), bodies printed by seq |
| tshark 4.0.3, verbose decode of f170011–f170024 | | | no | The USBD status and URB function names of the failed read and of the two type-254 records |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py <split> -o <out>.jsonl --device-address 30 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E12b; E12 read it in the ConsoleVersion object)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 22:29:22.355)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 22:29:23.521; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-31-e12b-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (...) of the USBPcapCMD root-hub capture e12b.pcap (SHA-256 E24B78C4..., 1101137231 bytes, 911995 frames by a tshark field census, 22:29:47.631071-22:58:51.038318 local time), ... the console was power-cycled before the session (owner, log 22:27:17); our software was not involved" --note 1743.407247 "analyst: the raw capture's last frame (another device) is at 22:58:51.038318 local time; the console's last frame, frame 218846 of this split, is at 1734.519467 s (22:58:42.150538); the log stamps SmartSoft's second close at 22:58:39.142 (start) and 22:58:44.556 (closed) and USBPcap's stop at 22:58:53.109"
```

(The two notes are quoted in full in the JSONL's first records.)

Analysis commands (added with the analysis; from the repository root; every tool only reads, `-B` keeps `__pycache__` out of the
tree; `J` = the committed JSONL, `<tmp>` = a folder outside the repository; `-X utf8` where a note holding "Č" is printed):

```
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py J summary          (and the modes sessions, log, lcd, payloads --id 9 / 36 / 37)
py -3.13 -I -X utf8 -B tools/protocol-analyzer/hsp_summary.py J timeline --id 13 --id 14 --id 15 --id 17 --id 3 --id 22 --id 23 --id 36 --id 21 --max-changes 40
py -3.13 -I -X utf8 -B tools/protocol-analyzer/hsp_summary.py J startdiff --other docs/protocol/captures/2026-10-31-e12-smartsoft-patch-stack-edits.jsonl
py -3.13 -I -B tools/protocol-analyzer/e12/writes.py . J                 (likewise lists.py, downloads.py, write_timing.py)
py -3.13 -I -B tools/protocol-analyzer/e12b/write_rules.py J             (and the E12 JSONL, a regression check: the E12 record's figures)
py -3.13 -I -B tools/protocol-analyzer/e12b/cmp_writes.py . J
py -3.13 -I -B tools/protocol-analyzer/e12b/playback.py . J 600 700      (and 0 20, 850 1700, 1300 1800)
py -3.13 -I -B tools/protocol-analyzer/e12b/reply_latency.py . J         (and the E12 JSONL)
py -3.13 -I -B tools/protocol-analyzer/e12b/cmp_bodies.py . J 42344:61684 42344:87517 42344:100192 58030:87521 61676:87509 78698:87433 68207:94590 314:95757 107347:198 91835:282 19268:782 16292:782 6344:87317
py -3.13 -I -B tools/protocol-analyzer/e8b/blk2548.py . J
py -3.13 -I -B tools/protocol-analyzer/e12/cut_jsonl.py J <tmp>/e12b-d1.jsonl -1 90
py -3.13 -I -B tools/protocol-analyzer/e12/cut_jsonl.py J <tmp>/e12b-d2.jsonl 1386 1450
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py <tmp>/e12b-d1.jsonl smartfade-sd-card-backup/SHOW14.ASC
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py <tmp>/e12b-d2.jsonl smartfade-sd-card-backup/SHOW15.ASC
tshark -r docs/protocol/captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.pcap -Y "frame.number >= 170011 && frame.number <= 170024" -V
```

(Both cross-checks end with the IndexError of their part (7), "Live levels at session start", because neither download holds a
FaderInfo object, as in E12; parts (1)–(6) and the ChannelLevels / OutputLevels lines of (7) are complete. Every committed JSONL was
also read with `payloads --id 36` for the FileName changes cited below.)

**Completeness of the split:**
- Frame census of the raw capture by device address: address 30 (idVendor 14D5, idProduct 0200, the console): 218 830 bulk, 11 control,
  4 of USBPcap transfer type 0xFE, 1 injected descriptor = **218 846**, the split's frame count. Address 4 (idVendor 0D8C, an audio
  device): 566 627 isochronous and 6 others; address 19 (0B05): 126 354 interrupt and 6 others, the owner's keyboard; address 3 (0BDA):
  134; addresses 1, 2 and 18: control only (10, 6, 6). Total 911 995.
- The normalizer run on the raw capture with `--device-address 30 --bus 1` found 218 846 frames and skipped 693 149 frames of other
  devices unread; its 109 423 records equal the split's in every field except the pcap frame numbers (0 differing, `cmp_records.py`).
- The split's first frame is the raw file's first frame (same time stamp); its last console frame is 8.9 s before the raw file's last
  frame (another device).

## Structure (`hsp_summary.py summary` and `sessions`, read-only; counts, no decoding)

- 109 423 transfers: 54 699 bulk OUT (endpoint 0x04), 54 716 bulk IN (0x83), 1 control OUT, 5 control IN, and **two transfers of USBPcap
  type 254** (one OUT, one IN; what they are is for the analysis: USBPcap's own record of an aborted pipe, INFERRED, never seen in a
  capture before). **USBD status non-zero: 1** (seq 85008, f170015–170016, endpoint 0x83, status 0xC0010000, t = 1336.94 s =
  22:52:04.57, inside the edited-state SD save step logged 22:51:23–22:52:12). **Framing errors (H15 / H16): 1**, the same transfer: an
  IN of 0 bytes where a 12-byte reply header was expected (`in-unexpected`); everything else frames.
- Roles: 54 017 polls and 54 016 reply headers (53 317 idle replies, 141 lists, 558 type-3 replies), 558 fetches (each type-3 reply
  echoing its fetch's id, a, b, c: 558 ok, 0 differ), 699 IN payloads (payload sequence 0 → 186 with two wraps and 0 breaks), **62 Data
  headers with their 62 OUT payloads** (SmartSoft's writes). The stale-word rule (F52) holds in all 54 078 poll and data headers (0 differ).
- Two SmartSoft sessions in the one capture, as planned: first lists at f356 (t = 5.28 s, 157 records: download 1) and f174594
  (t = 1388.67 s, **160 records**: download 2, after the edits). The console's inbound payload sequence continues across the SmartSoft
  restart (244 → 245), as in E3b, E9b and E12 (H31).

*(Analysis: the two type-254 transfers are not an aborted pipe but two pipe resets, URB_FUNCTION_SYNC_RESET_PIPE_AND_CLEAR_STALL on
endpoints 0x04 and 0x83; section "The failed IN transfer during the SD save".)*

## Analysis

*(This section replaces the placeholder of 9968ec2, which named what the pass had to cover. The decoding below was made with
read-only analysis tools only (offline readers of the committed files; nothing that can open a device, encode a message or talk to
a console); the statuses it proposes are for the knowledge-base curator, who applies them.)*

**Conventions** (as in the E12 record): `seqN` is the record number in the normalized JSONL (notes take none); `fN` is a frame of
the console-only split, the data-carrying one (submission frame for OUT, completion frame for IN). A write is cited as **Wn**, the
n-th of the 62 data writes in capture order, with header seq / payload seq and the payload frame. `t` is seconds since the split's
first frame (22:29:47.631071 local; local time = 22:29:47.631 + t); log stamps are quoted with their t. `pseq` is a payload's own
counter (SmartSoft's from 0x00 in each SmartSoft session; the console's runs on). Sessions: **S1** seq 6 … 87128 (2.67–1371.01 s,
W1–W50), **S2** seq 87130 … 109423 (1386.10–1734.52 s, W51–W62) (seq 4, 5 and 87129 are SmartSoft's string-descriptor reads at its
starts). Stack steps are named by cue number and **index** (= StepContent `a`, 0-based). The show is E12's: the stack (`$SEQUENCE 48`,
download 1) has 19 steps, index 0–18 = cues 1.0–9.0, 10.1–10.6, 11.0, 12.0, 13.0, 14.0; KOTLIK (`$SEQUENCE 0`, page 1, fader 21) has 7,
records 1.0–7.0 = memories 1–7 of page 12 (index 0x0108–0x010E), 1.0 / 1.0 s. A step record is
`[u16 cue × 10][type][u16 memory index][x][u16 Up][u16 Down][u16 Wait]` (H41; times in tenths, BE). Every quoted byte comes from the
cited seq; readings carry their status (OBSERVED = seen in this capture; INFERRED; HYPOTHESIS; UNKNOWN).

The stack and KOTLIK during the session, from the console's own bodies:

| After | Index 17 | 18 | 19 | 20 | 21 | SequenceMain body[0] |
|---|---|---|---|---|---|---|
| download 1 (seq 390, 394) | 13.0 | 14.0 | — | — | — | 0x13 (19 steps, seq 318) |
| D5 insert (W8 / W9) | 13.0 | 13.1 "State" | 14.0 | — | — | 0x14 (seq 24958) |
| D6 insert (W39 / W40) | 13.0 | 13.1 | 13.2 channel 47 | 14.0 | — | 0x15 (seq 54415) |
| D2 insert (W42 / W43) = download 2 | 12.1 memory 1 | 13.0 | 13.1 | 13.2 | 14.0 | 0x16 (seq 61670, 87437) |
| I2 move + id 42 (W57 / W58) | 13.0 | 13.1 | 13.2 | 13.3 (the point cue) | 14.0 | 0x16 (seq 100184) |
| I2 delete (W59) | 13.0 | 13.1 | 13.3 | 14.0 | — | 0x15 (seq 103372) |
| I2 delete (W60) | 13.0 | 13.3 | 14.0 | — | — | 0x14 (seq 104525) |
| I2 delete (W61) | 13.0 | 14.0 | — | — | — | 0x13 (seq 105921) = download 1 |

KOTLIK: E1 (W44 / W45) made 8 steps (index 2 = the new 3.0, memory 1 of page 1 = 0x0000, 5.0 / 5.0; the old 3rd–7th renumbered
4.0–8.0), E1b (W46 / W47) 9 steps (index 7 = the new 8.0, the old last step 9.0), E3 / E3b One Shot then Manual (download 2), and I1
Loop, the delete at index 7 (back to W45's body) and the delete at index 2 (back to download 1's body).

### Inventory of the PC → console writes

**62 data writes** (type-1 header + payload, bulk 0x04, interface 1), all SmartSoft's, all completed with USBD status 0; framing
errors among them 0; w1 = body length + 4 in 62 of 62; SmartSoft's counter pseq 0–49 (S1) and 0–11 (S2), 0 gaps.

| Object (id) | Writes | w1 | Body | Wn | In E12 |
|---|---|---|---|---|---|
| System (39) | 4 (2 pairs) | 7 | `09 00 00`, `07 00 00` (F58) | W1, W2, W51, W52 | yes (3 pairs) |
| ConsoleEvent (20), kind 1 (key) | 28 (14 press / release pairs) | 7 | `01 32 01` / `00` × 6 (**code 0x32, never seen before**), `01 31 01` / `00` × 4 (GO), `01 3A 01` / `00` × 4 | W10–W37 | GO once, 0x3A once; 0x32 no |
| InsertStep (32) | 5 | 8 | `[a][b][u16 BE c]` | W8, W39, W42, W44, W46 | yes (5) |
| DeleteStep (34) | 5 | 8 | `[a][b][00 00]` | W55, W56, W59, W60, W61 | yes (5) |
| MoveStep (35) | 1 | 7 | `30 11 14`, E12's W58 byte for byte | W57 | yes (1) |
| id 42 | 1 | 8 | `30 14 00 85` | W58 | yes (1) |
| StepContent (4) | 5: 3 × 13 bytes, 2 × 100 bytes | 17 / 104 | a **channel step (type 0, new)**, its Up edit, a memory step; a Direct cue and its label | W40, W41, W43; W9, W38 | yes (types 1 and 2) |
| Sequence (2) | 5 | 148 / **160** | the whole object, 144 bytes (8 steps) and **156 bytes (9 steps, new)** | W45; W47, W48, W49, W54 | yes (144 only) |
| Page (8) | 2 | 43 | `[page − 1][03 06][36 label bytes]` | W50, W53 | yes |
| Independents (12) | 4 | 89 | the whole object, 85 bytes | W4–W7 | yes |
| Patch (5) | 2 | 518 | the whole object, `[02 00]` + 512 entries | W3, W62 | yes |

**Not seen:** InsertPointQStep (33); PatchDevice / UnpatchDevice / RepatchDevice (29–31) in either direction; Memory (0),
SequenceMain (3) or ChannelText (9) writes; ConsoleEvent kinds 0 and 2 (no fader event at all); any System write other than the pair;
anything on interface 0. Every payload is `[pseq][u16 LE len = w1 − 4][id][body]`. **A data payload carries no qualifier fields**
(the header's w2..w5 are stale, F52): W9's header `01 00 68 00 04 00 12 00 00 00 00 00` repeats the fetch (4, 18) before it, W45's
`01 00 94 00 02 00 00 00 00 00 02 00` and W47's `01 00 A0 00 02 00 00 00 00 00 07 00` the Sequence fetches with c = 2 and c = 7.

### Action log matched to the writes

"Offset" = the step's first write minus its log stamp (the guide logs the moment the owner starts the step). Every write of the
capture is in this table (62). "Nothing listed" means no DirtyObjects list at all came between the write and the next write (at most
15 s; `e12b/cmp_writes.py`).

| Log stamp (t) | Step | Writes (pseq; header / payload seq; payload frame; t) | Offset | The console's answer (seq) |
|---|---|---|---|---|
| 2.233 / 18.154 / 78.170 | A1 `smartsoft start #1`, online, 60 s | W1 pseq 0 `09 00 00` (seq 91 / 92, f183, 3.980); W2 pseq 1 `07 00 00` (seq 174 / 175, f349, 5.263), after reply headers #40 and #79 | +1.75 s | power-on list [22, 21] (seq 10, f20, pseq 0x00); [37] (seq 95) → ConsoleVersion (seq 99); **download 1**: 157-record list seq 178 (f356, 5.283), objects to 12.68 s; then nothing listed until 344.76 s |
| 94.980 / 101.840 | C1 channel 47 → DMX 100 | **W3** pseq 2 Patch: DMX 100 (body[101]) 0x00 → **0x2F (47)**, every other entry = download 1 (seq 6343 / 6344, f12687, 100.131) | +5.15 s | nothing listed |
| 135.753 | C3 IND 1 panel opened | **none** (no write from 100.13 s to 216.27 s) | | nothing listed |
| 202.278 | owner: "button mode is LATCH, GM is NO" | | | (record 1 = `00 FF 00 01`, download 1 seq 782) |
| 215.767 (answer 246.749: "BUMP, double click") | C3 button mode changed | **W4** pseq 3 Independents: record 1 byte 2 (body[3]) 00 → **01** (seq 13695 / 13696, f27391, 216.269) | +0.50 s | nothing listed |
| 255.732 | C3 button mode restored | **W5** pseq 4: byte 2 → 00 (seq 16291 / 16292, f32583, 257.260) | +1.53 s | nothing listed |
| 278.553 (answer 299.400: "YES") | C3 GM changed | **W6** pseq 5: record 1 byte 3 (body[4]) 01 → **00** (seq 17751 / 17752, f35503, 280.320) | +1.77 s | nothing listed |
| 302.378 | C3 GM restored, panel closed | **W7** pseq 6: byte 3 → 01, the body = download 1's (seq 19267 / 19268, f38535, 304.251) | +1.87 s | nothing listed; closing the panel wrote nothing more |
| 343.248 / 348.226 | D5 console fader 5 to about half | none | | seven lists [14, 15, 13, 22(, 21)] 344.76–346.28 s (seq 21831 … 21961): FaderInfo record 5 byte 0 (body[17]), ChannelLevels[5], DMX 5 (OutputLevels body[6]) and LED block 5 to **0x88** (seq 21946, 21950, 21954, 21958); LCD fader-5 bar `<3>` (seq 21939) |
| 388.136 / 401.770 (answer 529.717: "13.1, I click on the 14.0 …, content 'Cue'") | D5 Direct cue | **W8** pseq 7 InsertStep `30 12 00 83` (seq 24950 / 24951, f49901, 393.413); **W9** pseq 8 StepContent (4, 18), 100 bytes: type 2, **48 zero levels**, empty label (seq 24963 / 24964, f49927, 393.574) | +5.28 s | after W8: [3, 4:18, 4:19] (seq 24954, +18 ms), SequenceMain 0x14 (seq 24958), StepContent 18 `12 00 83 00 00 00 00 00 32 00 32 00 00` (seq 24962); then W9; then 19 = 14.0 (seq 24968). Channel 5 stood at 0x88 from 346.2 s to 535.6 s |
| 533.607 / 538.699 | D5 fader 5 to zero | none | | three lists 535.59–536.08 s: channel 5 0x88 → 0x00 (seq 34015), DMX 5 → 0 (seq 33999) |
| 603.695 (answer 808.939: the label shown and the playback) | D5 label `čAJ123456789012345` | W10–W37 (the unlogged playback, next section); **W38** pseq 37 StepContent (4, 18), 100 bytes: type 2, zero levels, label `ČAJ123` / `456789` / `01234`, 17 code units, the first `01 0C` (seq 42343 / 42344, f84687, 663.911) | +60.22 s | nothing listed after W38 |
| 830.017 / 857.713 (answer 893.047: "13.2, dropdown with all channels and I selected the 47") | D6 channel step | **W39** pseq 38 InsertStep `30 13 00 84` (seq 54407 / 54408, f108815, 854.395); **W40** pseq 39 StepContent (4, 19) `13 00 84 00 00 2E 00 00 32 00 32 00 00`: **type 0, bytes 3–4 = 0x002E** (seq 54420 / 54421, f108841, 854.556) | +24.38 s | after W39: [3, 4:19, 4:20, 21] (seq 54411, +19 ms), SequenceMain 0x15, StepContent 19 `13 00 84 00 00 00 00 00 32 00 32 00 00` (seq 54419); then W40; then 20 = 14.0 (seq 54425); LCD "Live:13.1 Next:13.2" / "State Ch:47" (seq 54429); [21] (seq 54432) |
| 908.207 | D6 Up 3.0 | **W41** pseq 40 StepContent (4, 19): Up `00 32` → `00 1E` (seq 58029 / 58030, f116059, 911.532) | +3.33 s | [17] (seq 58033): PlaybackInfo record 5 bytes 12–13 `00 32` → `00 1E` (seq 58037; the stack's next step was 13.2) |
| 955.810 / 976.779 (answer 1033.359: "12.1, I right clicked on 13.0, yes the dialog's cue number field was enabled") | D2 point cue, memory 01/01 | **W42** pseq 41 InsertStep `30 11 00 79` (seq 61662 / 61663, f123325, 968.883), E12's W19 byte for byte; **W43** pseq 42 StepContent (4, 17) `11 00 79 01 00 00 00 00 32 00 32 00 00`, memory index 0x0000 = memory 1 of page 1 (seq 61675 / 61676, f123351, 969.042) | +13.07 s | after W42: [3, 4:17, 4:18, 4:19, 4:20, 4:21, 17, 21] (seq 61666, +20 ms), SequenceMain 0x16, StepContent 17 `11 00 79 00 …` (seq 61674); then W43; then 18 = 13.0, **19 = 13.1, 148 bytes with the label** (seq 61684), 20 = 13.2 with Up 3.0 (seq 61688), 21 = 14.0, PlaybackInfo, LCD "Live:13.0 Next:13.1" (seq 61700) |
| 1058.561 / 1081.039 (answer 1142.800: "3, before, and it changed the number of step I clicked on … to 4 and all others increment by 1") | E1 KOTLIK insert at step 3, memory 01/01 | **W44** pseq 43 InsertStep `00 02 00 50` (seq 68197 / 68198, f136395, 1071.829); **W45** pseq 44 Sequence, 144 bytes: record 2 memory 0x010A → 0x0000 (seq 68206 / 68207, f136413, 1071.938) | +13.27 s | after W44: **[2:0/0/2, 17]** (seq 68201, +18 ms); **Sequence (2, 0, 0, 2), 144 bytes** (seq 68205, f136410): record 2 `00 1E 01 01 0A 00 00 32 00 32 00 00` (3.0, a copy of the displaced step's memory 0x010A, 5.0 / 5.0), records 3–7 the old 3rd–7th as 4.0–8.0; after W45 PlaybackInfo record 1 bytes 10–13 `00 0A 00 0A` → `00 32 00 32` (seq 68211) |
| 1154.669 / 1162.387 (answer 1206.081: "8, before, and it changed the number of step I clicked on … to 9") | E1b KOTLIK insert at the last step | **W46** pseq 45 InsertStep `00 07 00 5A` (seq 73704 / 73705, f147409, 1158.775); **W47** pseq 46 Sequence, **156 bytes**: record 7 memory 0x010E → 0x0000 (seq 73713 / 73714, f147427, 1158.887) | +4.11 s | after W46: **[2:0/0/7]** (seq 73708, +18 ms); **Sequence (2, 0, 0, 7), 156 bytes** (seq 73712, f147424): record 7 `00 50 01 01 0E 00 00 32 00 32 00 00` (8.0, a copy of 0x010E, 5.0 / 5.0), record 8 the old last step as 9.0 |
| 1211.659 | E3 run mode One Shot | **W48** pseq 47 Sequence: **body[3] 00 → 01** (seq 77753 / 77754, f155507, 1222.698) | +11.04 s | nothing listed |
| 1235.273 | E3b run mode Manual | **W49** pseq 48 Sequence: **body[3] 01 → 02** (seq 78697 / 78698, f157395, 1237.537) | +2.26 s | nothing listed |
| 1275.738 (answer 1295.431: "12") | F3 page 12 name T | **W50** pseq 49 Page: **body[0] = 0x0B = page 12**, label "T" (seq 81265 / 81266, f162531, 1278.105) | +2.37 s | nothing listed (E12's slip to page 1 not repeated) |
| (1308.30–1339.15; logged 1345.067) | S the edited-state save, on the console | none | | the console's menu on the LCD: "SmartFade Menu" → "Save/Load show" → "Save Show" → "Save>SHOW15.ASC" → "Save SHOW15.ASC / Confirm?" (seq 84952, 1335.946) → "Show file saved" (seq 85021, 1337.746); one reply-header read took 31.8 ms (seq 83531, 1313.652, the "Save>SHOW14.ASC*" screen); **the read after the poll of 1336.829 got no answer and was cancelled (seq 85008; section below)**; the first list after the gap [36, 21, 22] (seq 85013): **FileName "SHOW15.ASC"** (seq 85017, f170034) |
| 1368.487 / 1375.926 | close #1 | none | | S1's last transfer 1371.006 (seq 87128) |
| 1385.952 / 1399.240 / 1429.245 (answer "yes" 1446.358) | G1 start #2 | W51 pseq 0 `09 00 00` (seq 87210 / 87211, f174421, 1387.375); W52 pseq 1 `07 00 00` (seq 87293 / 87294, f174587, 1388.644), #40 / #79 | +1.42 s | [37] (seq 87214) → ConsoleVersion (seq 87218); **download 2: 160-record list seq 87297 (f174594, 1388.666)** |
| 1446.363–1446.367 | H3–H7 (optional) | skipped by the owner's decision | | |
| 1454.343 | I1 page 12 name cleared | **W53** pseq 2 Page 12 empty, = download 1's Page 11 (seq 91834 / 91835, f183669, 1457.700) | +3.36 s | nothing listed |
| 1473.045 | I1 run mode Loop | **W54** pseq 3 Sequence: body[3] 02 → 00 (seq 93148 / 93149, f186297, 1478.273) | +5.23 s | nothing listed |
| 1497.014 | I1 delete E1b's step | **W55** pseq 4 DeleteStep `00 07 00 00` (seq 94582 / 94583, f189165, 1500.875) | +3.86 s | **[2:0/0/7]** (seq 94586, +17 ms); Sequence (2, 0, 0, 7), 144 bytes (seq 94590) = W45's body byte for byte |
| 1516.617 | I1 delete E1's step | **W56** pseq 5 DeleteStep `00 02 00 00` (seq 95749 / 95750, f191499, 1519.201) | +2.58 s | **[2:0/0/2, 17]** (seq 95753, +17 ms); Sequence (2, 0, 0, 2), 132 bytes (seq 95757, f191514) = **download 1's Sequence byte for byte**; PlaybackInfo record 1 bytes 10–13 back to `00 0A 00 0A` (seq 95761) |
| 1577.294 (answer 1627.202: "13.3, this number was offered by the dialog, I move it between 13.2 and 14.0") | I2 move the point cue | **W57** pseq 6 MoveStep `30 11 14` (seq 100174 / 100175, f200349, 1589.011); **W58** pseq 7 id 42 `30 14 00 85` (seq 100179 / 100180, f200359, 1589.060), 48.8 ms later | +11.72 s | after W57: [3, 4:17 … 4:21, 17, 21] (seq 100178, +21 ms, before W58); fetched after W58: 17 = 13.0, 18 = 13.1 (148 bytes, seq 100192), 19 = 13.2, **20 = 13.3** `14 00 85 01 00 00 …` (seq 100200), 21 = 14.0; [3, 4:20, 4:21] (seq 100215) |
| 1635.957 | I2 delete the channel step | **W59** pseq 8 DeleteStep `30 13 00 00` (seq 103364 / 103365, f206729, 1639.246) | +3.29 s | [3, 4:19, 4:20, 17, 21] (seq 103368, +18 ms): SequenceMain 0x15, 19 = 13.3, 20 = 14.0 (seq 103376, 103380) |
| 1654.635 | I2 delete the Direct cue | **W60** pseq 9 DeleteStep `30 12 00 00` (seq 104517 / 104518, f209035, 1657.309) | +2.67 s | [3, 4:18, 4:19] (seq 104521, +16 ms): 0x14, 18 = 13.3, 19 = 14.0 |
| 1676.264 | I2 delete the point cue | **W61** pseq 10 DeleteStep `30 12 00 00` (seq 105913 / 105914, f211827, 1679.169) | +2.91 s | [3, 4:18, 17, 21] (seq 105917, +17 ms): 0x13, 18 = 14.0 (seq 105925) |
| 1699.128 (answer "yes" 1723.170) | I4 DMX 100 → bin | **W62** pseq 11 Patch: DMX 100 → 0, the whole body = download 1's (seq 107346 / 107347, f214693, 1701.733) | +2.60 s | nothing listed |
| 1731.511 / 1736.925 | close #2 | none | | last transfer 1734.519 (seq 109423, f218846) |

Totals: S1 50 writes, S2 12 = 62.

### The unlogged playback (617.6–659.2 s, 22:40:05–22:40:47)

The owner's account (statements): unable to reach the new row in SmartSoft's step list, he clicked SmartSoft's "next", PAUSE and PLAY
several times, then pressed STACK on the console and held PAUSE while pressing PLAY ("one step back"); the new row 13.1 then showed
and he set its label. `e12b/playback.py`:

| t (local) | Event | PlaybackInfo record 5 (body[65..80]) | LED blocks, ConsoleStatus, LCD |
|---|---|---|---|
| 617.578 (22:40:05.2) | SmartSoft 0x32 press / release (W10 / W11) | — | nothing listed |
| 618.051 | **GO** 0x31 (W12 / W13) | [3, 17, 22] (seq 39196, +19 ms); first PlaybackInfo of the capture `00 00 F9 06 00 00 01 00 01 00 00 32 00 32 00 00` (seq 39206): a fade running, bytes 2–3 moving | block 50 blue `00 00 FF 00 00 FF` (seq 39210); STACK (block 60) dim `46 46 00 46 46 00` |
| 618.887 | 0x32 (W14 / W15) | byte 5 → 01; bytes 2–3 **held** at `D1 2E` (seq 39277) | block 51 `FF FF FF 0A 0A 0A` (seq 39288) |
| 619.375 | GO (W16 / W17) | bytes 10–13 `00 32 00 32` → `00 28 00 28`, bytes 2–3 still held (seq 39319) | — |
| 620.075 | 0x32 (W18 / W19) | byte 4 → 01, byte 5 → 00, bytes 10–13 → 0; bytes 2–3 **run back** `D3 2B` → `FF 00` by 621.07 s (seq 39368 … 39442) | block 51 `FF` × 6 (seq 39372) |
| 622.000 | 0x32 (W20 / W21) | byte 5 → 01 (seq 39514) | block 51 `FF FF FF 0A 0A 0A` (seq 39518) |
| 622.474 | GO (W22 / W23) | bytes 10–13 → `00 32 00 32` (seq 39551); no fade | — |
| 623.555 | 0x32 (W24 / W25) | byte 5 → 00, bytes 10–13 → 0 (seq 39622) | block 51 `FF` × 6 (seq 39626) |
| 627.245, 627.721, 627.880 (22:40:14.9–15.5) | **0x3A** three times (W26–W31) | — | [23, 22] each (seq 39853, 39888, 39903): **ConsoleStatus body[6]** 00 → 01 → 00 → 01 (seq 39859, 39894, 39909); **block 59** `37 37 FF 0A 0A 0A` ↔ `0A` × 6 (seq 39863, 39898, 39913) |
| 639.519 (22:40:27.2) | 0x3A (W32 / W33) | — | [23, 22] (seq 40644): body[6] → 00 (seq 40648), block 59 `0A` × 6 (seq 40654) |
| 640.780 | 0x32 (W34 / W35) | byte 5 → 01 (seq 40739) | block 51 `FF FF FF 0A 0A 0A` (seq 40746) |
| 642.673 (22:40:30.3) | GO (W36 / W37) | bytes 10–13 → `00 32 00 32` (seq 40865) | — |
| 650.809 (22:40:38.4) | console: STACK | bytes 4–5 → 00 (seq 41376) | [17, 21, 22] (seq 41372): LCD "Live:14.0 Next: 1.0" (seq 41380); block 60 lit `FF FF 00 FF FF 00`, blocks 50 and 51 `0A` × 6 (seq 41384) |
| 651.48–651.68 | console | — | LCD "Jump to step:> 1" (seq 41424), STACK dim (seq 41433), the idle screen (seq 41437), as in E12's A3 |
| 652.978 | console: STACK | — | LCD "Live:14.0 Next: 1.0" (seq 41524), STACK lit (seq 41528) |
| 654.083 (22:40:41.7) | console: PAUSE held + PLAY (owner) | [3, 17, 22, 21] (seq 41593): **bytes 0–1 `00 00` → `12 12`**, a 5-s fade `F9 06` → `00 FF` (seq 41601 … 42033), bytes 10–13 counting `00 32` → `00 28` → `00 1E` → `00 14` → `00 0A` → `00 00` | block 50 blue (seq 41605); LCD "Live:13.0 Next:13.1" with the bars of "Mem:01/03" falling and "State" rising (seq 41609 … 41991) |
| 659.138 (22:40:46.8) | the fade ends | bytes 0–1 `13 13`, bytes 2–3 `FF 00`, bytes 10–13 `00 32 00 32` (seq 42040) | LCD "Live:13.1 Next:14.0" / "State Mem:01/01" (seq 42044); block 50 `0A` × 6, STACK lit (seq 42048) |
| 663.911 (22:40:51.5) | W38, the label | | |

- **No ChannelLevels, OutputLevels or FaderInfo object was listed from 536.3 s to the end of the capture:** the playback, and the step
  commands later made on the live stack, changed no channel or output level (OBSERVED). Why the stack's steps produced no output (the
  positions of the crossfaders) is not in the data (UNKNOWN).
- SmartSoft's keys moved no step index: record 5 bytes 0–1 stayed `00 00` with STACK dim (H76, as in E3b, E9, E9b, E12). The console's
  STACK and PAUSE + PLAY took the stack from Live 14.0 to Live 13.1 with a 5-s fade; record 5 bytes 0–1 went 0 → 18 → 19, each time the
  index of the step the LCD called "Next" (13.1 at index 18, 14.0 at index 19): H76 again.
- **Code 0x31** is GO again (block 50, a fade; F133). **Code 0x32 (new):** during a running fade it held the fade (byte 5 = 1, bytes 2–3
  frozen) and lit block 51 (= 0x32 + 1, H82); the next press ran the fade back to its start (byte 4 = 1); without a fade it toggled
  byte 5 and one half of block 51. That is what a PAUSE key would do (INFERRED; the console's crossfader keys are GO, PAUSE, RATE and
  CLEAR, `docs/hardware/smartfade-1248.md`, with CLEAR = 0x38 known, F133); H107. **Code 0x3A:** a toggle: ConsoleStatus body[6]
  (constant 0x00 in every capture before) and LED block 59 (`37 37 FF 0A 0A 0A`, the half-lit pattern that E8b read as blinking for
  block 50) changed with each press, nothing else (no PlaybackInfo, LCD or level change); in E12, with the console in memory mode, the
  same code changed nothing visible. Which key it is: UNKNOWN (owner question 1); H108. **H104's reading (0x3A = PAUSE) is
  contradicted** (section "Hypotheses H91–H106").
- These are the first PAUSE-like key events in any capture (the hardware doc: "PAUSE has not been seen in any capture"); the console's
  own PAUSE + PLAY is seen only through its effect (no PC write). PlaybackInfo record 1 (KOTLIK, fader 21 at zero) stayed
  `02 02 FF 00 …` (next index 2) throughout the playback.

### The step commands and what the console did with them (H96, H97, H98, H102, H103, H105)

| Wn | Step | Body | Reading (OBSERVED; E12 in brackets) | The console's list on the next poll | The console's step before phase 2 |
|---|---|---|---|---|---|
| W8 | D5 | InsertStep `30 12 00 83` | a = 48 (stack), b = 18 (14.0's index; the owner right-clicked 14.0), c = 0x0083 = **13.1** (logged 13.1) | [3, 4:18, 4:19] | `12 00 83 00 00 00 00 00 32 00 32 00 00`: 13.1, **type 0, memory index 0**, Up / Down 5.0 (as E12's four) |
| W39 | D6 | InsertStep `30 13 00 84` | b = 19 (14.0's index), c = 0x0084 = **13.2** (logged) | [3, 4:19, 4:20, 21] | `13 00 84 00 00 00 00 00 32 00 32 00 00` |
| W42 | D2 | InsertStep `30 11 00 79` | b = 17 (13.0's index; the owner right-clicked 13.0), c = 0x0079 = **12.1** (logged); E12's W19 byte for byte | [3, 4:17 … 4:21, 17, 21] | `11 00 79 00 00 00 00 00 32 00 32 00 00` |
| W44 | E1 | InsertStep `00 02 00 50` | a = 0 (KOTLIK), b = 2 (the old 3rd step; the owner clicked step 3), **c = 8.0 while the new step became 3.0** [E12: index 6, c 8.0, new 7.0] | [2:0/0/2, 17] | record 2: 3.0, **a copy of the displaced step's memory** (0x010A) with 5.0 / 5.0; the old 3rd–7th renumbered 4.0–8.0 |
| W46 | E1b | InsertStep `00 07 00 5A` | b = 7 (the old last step; the owner clicked step 8), **c = 9.0 while the new step became 8.0** | [2:0/0/7] | record 7: 8.0, a copy of 0x010E with 5.0 / 5.0; the old last step 9.0 |
| W55 | I1 | DeleteStep `00 07 00 00` | b = 7 (E1b's step), c = 0 | [2:0/0/7] | — (Sequence = W45's 144 bytes) |
| W56 | I1 | DeleteStep `00 02 00 00` | b = 2 (E1's step) | [2:0/0/2, 17] | — (Sequence = download 1's 132 bytes) |
| W57 | I2 | MoveStep `30 11 14` | b = 17 (the point cue, from), c = 20 (to: the index the step holds after the move, between 13.2 and 14.0) [E12: the same bytes, between 14.1 and 15.0] | [3, 4:17 … 4:21, 17, 21] | — |
| W58 | I2, 48.8 ms after W57 | id 42 `30 14 00 85` | index 20 gets cue **13.3** (the owner logged 13.3, "offered by the dialog") [E12: 14.2] | [3, 4:20, 4:21] (after the move's fetches) | — |
| W59 | I2 | DeleteStep `30 13 00 00` | b = 19 (13.2, the channel step) | [3, 4:19, 4:20, 17, 21] | — (19 = 13.3, 20 = 14.0) |
| W60 | I2 | DeleteStep `30 12 00 00` | b = 18 (13.1, the Direct cue) | [3, 4:18, 4:19] | — (18 = 13.3, 19 = 14.0) |
| W61 | I2 | DeleteStep `30 12 00 00` | b = 18 (13.3, the moved point cue) | [3, 4:18, 17, 21] | — (18 = 14.0) |

- **H96:** a = 48 in all 8 stack commands, a = 0 in all 4 KOTLIK commands (with E12: 18 and 6, two sessions). The formula is not
  separated by these two values, as before.
- **H97:** the bodies as in E12: InsertStep, DeleteStep and id 42 4 bytes `[a][b][u16 BE c]` (w1 = 8), MoveStep 3 bytes (w1 = 7);
  MoveStep first and id 42 48.8 ms later (E12 47 ms); **no id 33** (the point cue again an InsertStep with c = 12.1). Stack InsertStep:
  b = the index the new step takes and c = its cue number × 10 (3 of 3; 7 of 7 with E12). DeleteStep: b = the index, c = 0 (5 of 5; 10
  of 10). MoveStep: b = from, c = to after the move (2 of 2, both 17 → 20, one value pair). id 42: (a, index, new cue × 10), and the step
  at that index carried the new number in the next fetch (seq 100200; E12 seq 175936): the "renumber" reading is now supported in two
  sessions (INFERRED as a meaning). SmartSoft chose the stack cue numbers: a point number after the preceding cue (13.1, 13.2, 12.1;
  E12 12.1, 14.1, 14.2).
- **Where a new step lands:** before the right-clicked row, in the stack (D5, D6, D2) and in KOTLIK (E1, E1b), the following steps
  renumbered (owner, E1 / E1b); the empty row below the last appends after it (owner, D5 answer; E12's D1 15.0). E12's INFERRED reading
  is now supported by the data and by the owner's account.
- **H102:** the listed Sequence record's c = the changed step's index: (2, 0, 0, 2) after the insert and the delete at index 2 (seq
  68201, 95753), (2, 0, 0, 7) after the insert and the delete at index 7 (seq 73708, 94586); with E12's (2, 0, 0, 6) twice, 6 of 6
  records, three values, two sessions.
- **H103:** KOTLIK's new steps took their ordinal numbers (3.0 at index 2, 8.0 at index 7), never c (8.0, 9.0), and the console
  renumbered the following steps itself; with E12's insert (7.0 against c = 8.0), 3 of 3. c equals the step count after the insert × 10
  in 3 of 3 (8, 8, 9 steps): a reading (INFERRED); whether the console uses c at all is UNKNOWN.
- **What the console lists after a stack command, refined:** after an insert, SequenceMain and StepContent from the new index to the
  end; after a delete, from the deleted index to the new last index, i.e. every index whose content changed (W59: 19 and 20; W60: 18
  and 19; W61: 18; E12's deletes had at most one following step, so its "the deleted index" is the same rule); after the move, from the
  source index to the end. INFERRED as one rule ("from the lowest affected index to the last"), 24 of 24 commands in two sessions; no
  list names a command. With the stack live, PlaybackInfo and DisplayContent joined the lists (W39, W42, W57, W59, W61, and a list of
  DisplayContent alone after W60; the next section but one).
- **H98, two phases (5 of 5; 10 of 10 with E12):** the command carries no type; phase 2 set type 2 (W9), **0** (W40) and 1 (W43, W45,
  W47). The console's default step: stack type 0 with memory index 0 and 5.0 / 5.0 (seq 24962, 54419, 61674; 7 of 7 with E12); KOTLIK a
  copy of the displaced step's memory with 5.0 / 5.0 (seq 68205, 73712; 3 of 3).
- **H105, the order:** phase 2 was SmartSoft's next OUT transfer after the fetch of the object at the commanded index b, before any
  other listed record was fetched, in 5 of 5 (W9 before StepContent 19; W40 before 20 and 21; W43 before 18–21, 17 and 21; W45 before
  PlaybackInfo; W47 after the only listed record), 10 of 10 with E12, its header carrying that fetch's stale words (W9 (4, 18), W40
  (4, 19), W43 (4, 17), W45 (2, 0, 0, 2), W47 (2, 0, 0, 7)). Delays: stack 160.6, 161.0 and 159.0 ms (two fetches before phase 2,
  SequenceMain then the step), KOTLIK 109.1 and 111.2 ms (one fetch); E12 158.7–161.2 and 109.5 ms. Two delays one fetch cycle apart
  fit "after the object arrived" and not a fixed timer (INFERRED; the static check of the dialog's accept path, S11.16 row 6, would
  settle it).
- **Consequence (INFERRED, as in E12):** a lost phase 2 leaves the console's default step: in the stack a type-0 step with bytes 3–4 =
  0, which by F160's reading is a channel step of channel 1 (the LCD showed "Ch:1" for the empty next position after the last delete,
  seq 105933); in a sequence a copy of the displaced step's memory. A lost id 42 would leave a moved step out of cue order.

### Whole-object writes against the console's own bodies

`e12b/cmp_writes.py` compares each object write with the previous write of the object and with the console's last body before and
next body after it; `e12b/cmp_bodies.py` compares chosen pairs by seq (SmartSoft's 48-level bodies in the console's 96-level layout).

- **Patch (5), 2 writes, 514 bytes in the console's layout.** W3 changed only DMX 100 (body[101]) 0x00 → 0x2F; **download 2's Patch
  (seq 87317) is W3 byte for byte**; W62 is download 1's Patch (seq 198) byte for byte; SHOW15 has `100/47/100`, SHOW16 = SHOW14. As
  E12's W9 and W68 (H91, H92).
- **Independents (12), 4 writes, 85 bytes in the console's layout**, one write per change, one byte each: W4 record 1 byte 2 (body[3])
  00 → 01, W5 → 00, W6 byte 3 (body[4]) 01 → 00, W7 → 01; W5 and W7 equal download 1's body (seq 782), and download 2's (seq 87913)
  equals it too (restored in place). **The owner named the values SmartSoft showed:** byte 2 = 0x00 "LATCH", 0x01 "BUMP"; byte 3 = 0x01
  "GM: NO", 0x00 "GM: YES" (the show's `$INDEPENDENT n 0 255 0 1` is LATCH with GM NO). Opening the IND 1 panel wrote nothing (135.8–216.3
  s) and closing it nothing beyond W7: E12's unchanged W3 in A3 had another cause (INFERRED). The console's reaction to the changed
  values is again unobserved (nothing listed; restored before download 2). H93, H44.
- **StepContent (4), 5 writes.**
  - **W9** (D5, phase 2) has the form of E12's W7 and W29: `12 00 83 02 00 00 00 00 32 00 32 00 00` + `30` + **48 zero levels** + `03 06`
    + 36 zero bytes, written while channel 5 stood at 0x88 on the console (ChannelLevels[5] seq 21950 … 34015): SmartSoft's new Direct cue
    does not take the console's live look (OBSERVED once; that its levels come from SmartSoft's own state is INFERRED). The guide's aim
    "a Direct cue with a level" was not reached; SmartSoft's row showed the content "Cue" (owner).
  - **W38** (D5 label) is W9 with the label block `03 06` + `01 0C 00 41 00 4A 00 31 00 32 00 33` / `00 34 00 35 00 36 00 37 00 38 00 39` /
    `00 30 00 31 00 32 00 33 00 34 00 00` (reading A: "ČAJ123" / "456789" / "01234"; 17 code units, the 18th zero; 18 bytes differ from
    W9). The guide asked for 18 characters, `čAJ123456789012345`; SmartSoft kept 17 (the owner saw `ČAJ12345678901234`) and wrote U+010C
    "Č", not U+010D "č". Whether the owner typed the capital is owner question 2 (E12's step label W63 kept lower-case letters, so a
    SmartSoft upper-casing is not supported). **The console stored the block verbatim:** its 148-byte body of 13.1 (seq 61684 at index 19,
    seq 87517 in download 2, seq 100192 at index 18 after the move) equals W38 in the console's layout (`60` + 96 zero levels), except the
    index byte while the step sat at index 19 (`cmp_bodies.py`: 1 position, [0] 0x12 → 0x13; at seq 100192 0 positions). This is the
    read-back E12 could not get (its labelled step was deleted before any download).
  - **W40** (D6, phase 2): type 0, bytes 3–4 = 0x002E; **W41** (Up 3.0) = W40 with body[7..8] `00 32` → `00 1E`; download 2's StepContent 20
    (seq 87521) is W41 at the index shifted by D2's insert. The LCD named the step "Ch:47" (seq 54429, 100212): for a type-0 step bytes
    3–4 hold the channel − 1 (OBSERVED once, with SHOW15's `CHAN 47/100`; F160).
  - **W43** (D2, phase 2) = download 2's StepContent 17 (seq 87509) byte for byte.
- **Sequence (2), 5 writes:** W45 (phase 2: record 2's memory 0x010A → 0x0000), W47 (phase 2, 156 bytes: record 7's 0x010E → 0x0000), W48
  (**body[3] 0 → 1, One Shot**), W49 (**1 → 2, Manual**), W54 (**2 → 0, Loop**). **Download 2's Sequence (seq 87433) is W49 byte for byte**
  (9 steps, Manual); the console's Sequence after W55 (seq 94590) is W45 and after W56 (seq 95757) download 1's (seq 314). c, d, e stayed
  `00 00 01 00 01 00` in every write (H42).
- **Page (8), 2 writes:** W50 = download 2's Page 11 (seq 87401); W53 = download 1's Page 11 (seq 282).
- **No unchanged rewrite:** none of the 18 object writes repeated its object unchanged (E12: 10 of 46, all during "looks"; E12b's guide
  forbade looks).
- **Nothing of the written object was listed back after any of the 18** (no list naming it before the next write or within 15 s; with
  E12, 64 of 64). Two writes changed the live stack's next step and were followed by the live objects that show it: W40 by
  DisplayContent (seq 54432, the LCD's "Ch:47") and W41 by PlaybackInfo (seq 58033, record 5's `00 1E`).

### The stack's live and next step under the step commands

After the console's back fade the stack stood at Live 13.1 / Next 14.0 with STACK lit (LED block 60 `FF FF 00 FF FF 00` from 653.08 s to
the end of the capture: no LedStatus was listed after download 2's, seq 87929) and PlaybackInfo record 5 bytes 0–1 = `13 13` (next
index 19) to the end. SmartSoft's later commands changed the steps under these positions, without a GO and without a fade:

| t | Command | LCD (seq) | PlaybackInfo record 5 (seq) |
|---|---|---|---|
| 854.6 | D6 insert at 19 (W39 / W40) | "Live:13.1 Next:13.2" / "State Ch:47" (54429) | — |
| 911.5 | D6 Up 3.0 (W41) | — | bytes 12–13 `00 32` → `00 1E` (58037) |
| 969.3 | D2 insert at 17 (W42 / W43) | "Live:13.0 Next:13.1" / "Mem:01/03 State" (61700): **the live step changed from 13.1 to 13.0** | bytes 12–13 → `00 32` (61696) |
| 1339.2 | (after the save) | "Live:13.0 Next:13.1" (85112) | — |
| 1589.4 | move + id 42 (W57 / W58) | "Live:13.1 Next:13.2" / "State Ch:47" (100212) | bytes 12–13 → `00 1E` (100208) |
| 1639.5 | delete 13.2 at 19 (W59) | "Live:13.1 Next:13.3" / "State Mem:01/01" (103388) | → `00 32` (103384) |
| 1657.5 | delete 13.1 at 18, **the live step** (W60) | "Live:13.3 Next:14.0" / "Mem:01/01 Mem:01/01" (104540) | — |
| 1679.4 | delete 13.3 at 18, the live step again (W61) | "Live:14.0 Next:" / "Mem:01/01 Ch:1" (105933) | bytes 10–13 → `00 00 00 00` (105929) |

- OBSERVED once (F164): the console keeps the stack's live and next positions as step indices (18 and 19 throughout); an insert, move or
  delete at or before them changes which step is live or next at once, and deleting the live step made the following step live without a
  fade. No level object was listed, so no output changed (above). KOTLIK's record 1 behaved alike: its next index 2 got the inserted
  step's times (bytes 10–13 `00 0A 00 0A` → `00 32 00 32`, seq 68211) and lost them with the delete (seq 95761).
- Reading (INFERRED, from the values above): at rest record 5 bytes 10–11 and 12–13 hold the next step's Down and Up times (13.2's
  Down 5.0 / Up 3.0 at 911.5 s and 1589.4 s; zero when there is no next step, 1679.4 s); H50 had them as the fade's count-down.
- Relevance (INFERRED): on a running show, SmartSoft's step commands change what the next GO plays. None of these messages is cleared or
  encoded; any future editor would have to face it.

### The two downloads (157 / 160 records)

`downloads.py`: download 1's 157-record list (seq 178) is byte-identical to E9b's three and E12's two (body SHA-256 prefix
`8208d7647d400d48`; the twelfth such list). Download 2's list (seq 87297, f174594, 160 records, `9de5851c425b7511`) is the same list
with **three records inserted in list order: StepContent (4, 19), (4, 20), (4, 21) at positions 54–56** (after step 18): the three stack
steps the session had added and not yet deleted at G1 (12.1, 13.1 "State", 13.2 the channel step; the stack had 22 steps). KOTLIK's two
steps are inside its one Sequence record, and no memory was added; E12's download 2 was 157 + memory 9 + three steps = 161 (F157).

| Object | Download 1 (S1) | Download 2 (S2, after the edits) |
|---|---|---|
| Patch (5) | seq 198, f396: DMX 100 = 0 | seq 87317, f174634: DMX 100 = 47 (= W3) |
| Page (8, 11) | seq 282: empty | seq 87401: "T" (= W50) |
| Sequence (2) | seq 314, f628: 132 bytes, 7 steps, Loop, KOTLIK | seq 87433, f174866: **156 bytes, 9 steps, Manual** (= W49) |
| SequenceMain (3) | seq 318: 0x13 (19 steps) | seq 87437: 0x16 (22) |
| StepContent 17, 18 | 13.0, 14.0 (seq 390, 394) | 12.1 memory 1 (= W43), 13.0 (seq 87509, 87513) |
| StepContent 19, 20, 21 | not listed | **13.1 "State", 148 bytes, the label stored** (seq 87517, f175034, = W38); 13.2 channel 47, Up 3.0 (seq 87521, = W41); 14.0 (seq 87525) |
| FileName (36) | seq 790: "SHOW14.ASC" | seq 87921: "SHOW15.ASC" (the edited-state save) |
| LedStatus (22) | seq 798: block 60 (STACK) `46 46 00 46 46 00` | seq 87929: block 60 `FF FF 00 FF FF 00` (STACK lit since 653.1 s) |
| Independents (12) | `00 FF 00 01` × 2 (seq 782) | = download 1 (seq 87913) |
| ConsoleStatus (23) | `00 00 00 00 00 01 00` (seq 794) | = download 1 (seq 87925; channel mode throughout, body[6] back at 0) |
| ChannelText 6 (channel 7) | `06 03 06 00 00 00 61 00 6A` + zeros (seq 422) | = download 1 (seq 87553) |

Download 2 equals download 1 in 149 of the 157 shared objects (the eight above that differ). **There was no third SmartSoft session:**
the restores are shown by the console's last listed bodies (Sequence seq 95757 = download 1's; SequenceMain 0x13 at seq 105921;
StepContent 18 = 14.0 at seq 105925; W53 and W62 = download 1's Page 11 and Patch) and, from the file side, by SHOW16 = SHOW14.

### The failed IN transfer during the SD save (seq 85008)

| seq | Frames | t (local) | Transfer | Data / status |
|---|---|---|---|---|
| 85006 | f170011 / 12 | 1336.798 (22:52:04.430) | reply header (IN 0x83) to the poll seq 85005 | `04 00 00 …` (idle), 0.32 ms |
| 85007 | f170013 / 14 | 1336.829 (22:52:04.460) | poll (OUT 0x04) | `00` × 12 (the stale words of seq 85006) |
| 85008 | f170015 / 16 | submitted 1336.8295, completed 1336.9414 (22:52:04.572) | reply-header read (IN 0x83) | **0 bytes, USBD_STATUS_CANCELED (0xC0010000), after 111.9 ms** |
| 85009 | f170017 / 18 | 1336.9414–1337.6120 | USBPcap transfer type 0xFE ("USB IRP Info"): **URB_FUNCTION_SYNC_RESET_PIPE_AND_CLEAR_STALL (0x001E), endpoint 0x04** | success after **670.6 ms** |
| 85010 | f170019 / 20 | 1337.6121–1337.6210 | the same on **endpoint 0x83** | success after 9.0 ms |
| 85011 | f170021 / 22 | 1337.6507 (22:52:05.282) | poll | `00` × 12 |
| 85012 / 85013 | f170023 / 24, f170026 | 1337.6521 | reply header `04 00 16 00 …` (1.4 ms after the poll's submission) and the list **[36, 21, 22]**, pseq 0xEF | |
| 85017 | f170034 | 1337.6993 | FileName (36) | "SHOW15.ASC" |
| 85021 | f170042 | 1337.7463 | DisplayContent (21) | "Show file saved" |

- The names are Wireshark's decoding of the USBPcap header (tshark 4.0.3 `-V`: "IRP USBD_STATUS: USBD_STATUS_CANCELED (0xc0010000)",
  "URB Function: URB_FUNCTION_SYNC_RESET_PIPE_AND_CLEAR_STALL (0x001e)", "URB transfer type: USB IRP Info (0xfe)"). All five URBs carry IRP
  ID 0xffff80857ddefa60 and were submitted in the direction FDO → PDO, i.e. by the driver above USBPcap, ETC's etchsp.sys (INFERRED:
  whether on its own or at SmartSoft's request is not visible). The structure section's guess "an aborted pipe" is superseded: a
  cancelled read, then a reset of each pipe. The read was cancelled by the host, not failed by the device (no stall or transaction
  error status); a ~100-ms read timeout in etchsp.sys is INFERRED from this one case (S11.9 says the timeout comes from the driver,
  UNKNOWN until now).
- **No IN data for 853.5 ms** (seq 85006 at 1336.7985 → seq 85012 at 1337.6521); the poll-to-poll interval was 821.3 ms, the only one
  above 100 ms outside the SmartSoft restart. The console's last screen before the gap was "Save SHOW15.ASC / Confirm?" (seq 84952,
  1335.946; 27 idle replies came after it), the first payload after it the list with FileName: **the console stopped answering while
  it saved the show** (the cause is INFERRED from the timing; the owner's confirm press itself is not on the bus). The save menu also
  gave the capture's other slow reply, 31.8 ms (seq 83531, 1313.652, the "Save>SHOW14.ASC*" screen); every other reply-header read took
  at most 6.6 ms at the 99.9th percentile (median 1.9 ms); E12's longest was 7.4 ms (`reply_latency.py`).
- **Nothing was lost or repeated:** the console's payload counter went 238 (seq 84952) → 239 (seq 85013) without a break (H17 / F55);
  the poll after the gap carried the stale words of the last reply (F52; 54 078 of 54 078 headers); no fetch was outstanding and no
  write was near (W50 at 1278.1 s, W51 at 1387.4 s).
- **SmartSoft carried on without a restart:** its next poll came 29.7 ms after the second reset (SmartSoft's path for a short header
  read is "USB Retrying…", `msleep(20)` and a new poll, smartsoft-analysis.md S11.2 step C: INFERRED from the static analysis), was answered
  at once, and SmartSoft fetched the three listed objects and polled normally until it was closed 31 s later; no System pair, no
  counter reset (pseq 0–49 in S1 unbroken).
- **The first of its kind:** the only non-zero USBD status of any capture (the USBPcap captures E2 to E12: 0 in every transfer; E8's
  transfer log has no status field), and the first SD save made on the console while a host polled: FileName changed inside a
  session only here (`payloads --id 36` over every committed JSONL). One sample (H109).
- **What a session of ours would have done (INFERRED from the code at HEAD, never observed with our software):** `WinUsbHspPipe` sets
  the pipes' transfer timeout to the reply timeout (default 500 ms, `UsbDeviceOptions.Defaults.ReplyTimeout`; at most that with live
  control on); a read that times out throws `HspPipeException`, `HspReadLoop.RunAsync` rethrows it and the session ends: nothing is
  retried and, after a pipe error, nothing more is written, not even the release of a key that was held at that moment
  (`ReleaseAfterFault` names only a press or release in flight). Whether the console would have answered within 500 ms is UNKNOWN:
  the host gave up after 112 ms, and the pipe reset, which needs the console's control endpoint, took 671 ms, which suggests a silence of
  that order (INFERRED). The 31.8-ms reply would pass; the FileName list after a save would pass the guards (id 36 and 61 bytes are in
  `ObservedListedIds` / `ObservedBodyLengths`). **So an SD save on the console while a session of ours runs would most likely end that
  session (a safe stop).** The E11 guide puts its SD save after Disconnect; that order matters. The code comment "the console answered
  within 12 ms in every capture" (`UsbDeviceOptions`) no longer holds for E12b.

### H101: what the console lists after a write, a command or its own action

| Trigger | E12b | The console's answer | E12 |
|---|---|---|---|
| A SmartSoft object write (Patch 2, Independents 4, StepContent 5, Sequence 5, Page 2) | 18 | **the written object never listed** (18 of 18); after W40 and W41, which changed the live stack's next step, DisplayContent and PlaybackInfo | 46 of 46, nothing listed |
| InsertStep, stack | 3 | on the next poll (+18–21 ms): SequenceMain and StepContent from the new index to the end (with PlaybackInfo / DisplayContent once the stack was live) | 4 |
| InsertStep / DeleteStep, KOTLIK | 2 / 2 | on the next poll (+17–18 ms): **Sequence (2, 0, 0, c), c = the changed index** (2, 7); PlaybackInfo when index 2, KOTLIK's next step, changed | 1 / 1 (c = 6) |
| DeleteStep, stack | 3 | on the next poll (+16–18 ms): SequenceMain and StepContent from the deleted index to the new last index (+ 17, 21) | 4 |
| MoveStep / id 42 | 1 / 1 | [3, 4:17 … 4:21, 17, 21]; after id 42 [3, 4:20, 4:21] once the move's records were fetched | 1 / 1 |
| Key GO (0x31) | 4 | [3, 17] or [3, 17, 22]: a fade or new times in PlaybackInfo record 5, block 50 | 1 |
| Key 0x32 | 6 | [17, 22], or [17] then [22] (nothing for the first press, no fade had run): record 5 bytes 4–5, block 51 | — |
| Key 0x3A | 4 | [23, 22]: ConsoleStatus body[6], block 59 | 1 (nothing listed; memory mode) |
| The console's fader 5 (D5) | 2 moves | FaderInfo, ChannelLevels, OutputLevels, LedStatus, DisplayContent | (B1) |
| The console's STACK, PAUSE + PLAY | 3 + 1 | [17, 21, 22], [21], [22, 21], [21, 22]; [3, 17, 22, 21] and [17, 21] during the fade | (STACK in A3) |
| The console's SD save | 1 | DisplayContent at every menu screen; after the save [36, 21, 22]: **FileName listed** (outside a download for the first time) | — |
| The console's own menu edits | 0 (declined) | — | Patch 53 times, StepContent 20 times |

H101's first clause holds in two sessions for the written object (64 of 64), refined: a write that changes the live stack's next step
makes the console list the live objects that show it. The commands are answered by lists of the affected objects in two sessions (24
of 24). The console's own menu edits remain E12's alone.

**Relevance for SC-1 (INFERRED from the code, not observed with our software):** E12b again shows what `HspReadSession` faults on,
only after SmartSoft's commands or in download 2: Sequence records with c = 2 and c = 7, Sequence bodies of 144 and **156** bytes,
148-byte StepContents; download 2's 160-record list is a payload of 807 bytes, longer than `MaxObservedPayloadLength` (792; E12's
161-record list was 812), which a read-only session never triggers (it sends no System `07 00 00`). New for a read-only session: the
SD save above, and step commands on a live stack also list DisplayContent and PlaybackInfo (ids inside the guards).

### Write placement and framing (`e12b/write_rules.py`, `write_timing.py`)

| Check | E9 | E9b | E12 | **E12b** |
|---|---|---|---|---|
| Data writes; ids | 401; 39, 20, 9 | 427; 39, 20, 9, 0 | 70; 39, 20, 32, 34, 35, 42, 4, 2, 0, 8, 12, 5 | **62; 39, 20, 5, 12, 32, 4, 2, 8, 34, 35, 42** |
| Header w2..w5 = the last fetch or reply header (F52) | 401 / 401 | 427 / 427 | 70 / 70 | **62 / 62** (all poll and data headers: 54 078 / 54 078) |
| Counter from 0x00 per SmartSoft session, +1 per write (F131) | 1 session, 0 breaks | 3, 0 breaks | 3 (0–46, 0–20, 0–1), 0 breaks | **2 (0–49, 0–11), 0 breaks** |
| Placement: after an idle reply / a list / an object; writes since the last poll | 121 / 110 / 170; 0 | 80 / 101 / 246; 0 | 62 / 2 / 6; 0 | **45 / 7 / 10; 0 in 62 of 62** (never with a fetch outstanding) |
| With listed records still unfetched | 181 | 254 | 5 | **14** (W9, W40, W43, W45, W58 and nine key releases) |
| Next OUT transfer: poll / fetch | 220 / 181 | 173 / 254 | 65 / 5 | **48 / 14** |
| Header → payload submit (ms) | 0.05–0.32 | 0.067–0.254 | 0.077–0.230 | **0.073–0.272** (median 0.112) |
| Poll submit → write header submit (ms) | min 29.81 | min 29.88 | min 30.17 | **min 29.87**, median 32.12, max 48.57 |
| Write payload submit → next poll / fetch submit (ms) | 14.51–16.46 | 14.69–16.23 | 14.57–17.05, one at 30.60 | **14.65–17.07**, median 16.09 |
| … from the payload's completion (ms) | 14.46–16.38 | 13.63–16.20 | 10.96–24.99 | **10.42–16.99** |
| Write → next write, same session (ms) | min 44.77 | min 45.55 | min 46.89 | **min 45.71** |
| Key press → release (ms) | 45.63–5803 | 45.88–3355 | 46.89–93.64 (3 pairs) | **45.71–94.49 (14 pairs)** |
| Writes between a press and its release | 0 | 0 | 0 | **0** |

E12b keeps every rule E9, E9b and E12 showed (a fourth capture for the framing, the stale words, the per-session counter and the
placement): one write per loop slot in place of a poll, after a completed exchange, never two per poll. Key releases, phase-2 writes and
id 42 go before pending fetches (14 of 62; E12 5 of 70), which fits SmartSoft's loop writing a queued message before it fetches
(smartsoft-analysis.md S11.2, steps A and B; INFERRED). SmartSoft's idle poll interval reached 20.539 ms (6 intervals below 21.0 ms;
E12 20.446 ms, 11); a fetch's completion to the next poll 10.193 ms (E12 10.255; F62 ≥ 10.30). Our floors (`UsbDeviceOptions.Floors`:
21, 11, 21, 30, 15, 46 ms) stay above every SmartSoft minimum of the four captures; nothing here changes SC-1 or SC-2.
`e12b/write_rules.py` run on the E12 JSONL gives the E12 column above exactly.

### Cross-checks

- **ConsoleVersion** "3.0.2.9.0.43" in both sessions (seq 99, 87218), = the owner's Diagnostics reading (F48).
- **Download 1 against `SHOW14.ASC`** (`e2_asc_crosscheck.py` on the slice t < 90 s): Patch 512 of 512 entries (49 patched, 463 unpatched,
  0 mismatches); 16 of 16 memories, 768 of 768 levels; page labels 12 of 12, channel labels 96 of 96; SequenceMain 19 = 19 `CUE`; the
  Sequence header and its 7 records (the cue numbers differ as always, F75); StepContent 19 of 19 in all six fields; SystemSettings' `SET`
  values at body[6..11]; Independents `00 FF 00 01` × 2. The same results as E12's downloads 1 and 3.
- **Download 2 against `SHOW15.ASC`** (slice 1386–1450 s, before S2's first write): Patch 50 patched (DMX 100 = 47 on both sides), 462
  unpatched, 0 mismatches; memories, levels, page labels (page 12 "T") and channel labels all agree; SequenceMain 22 = 22 `CUE`; the
  Sequence's body[2] 9 = 9 `$CUE` and **body[3] = 2 = `$SEQUENCE 0 2 0 256 256`**, records 9 of 9 in memory, type, Up, Down and Wait (cue
  numbers: the wire's 3.0 and 8.0 against the file's 1.0 for `$$MEMORY 0 0`); StepContent 22 of 22 in cue, type, Up, Down and Wait and
  20 of 22 in memory: the two others are the State step (wire 0x0000, no `$$MEMORY` in the file) and the channel step (wire 0x002E, the
  file's `CHAN 47/100`), because the tool reads the memory from `$$MEMORY` only.
- **What the cross-check does not compare:** a step's label block, the State step's levels against its (absent) `CHAN` line, a channel
  step's `CHAN` line against its record, the `CHAN` lines the file repeats for memory steps, and the `Text` lines; these are compared by
  hand here:

| Object | On the bus (the console's body) | In `SHOW15.ASC` |
|---|---|---|
| State step 13.1 | seq 87517: `13 00 83 02 00 00 00 00 32 00 32 00 00` + `60` + 96 × `00` + `03 06 01 0C 00 41 …` | `CUE 13.1` / `$$CUEPARAMS 2` / `UP 5.0   0` / `DOWN 5.0   0` / `FOLLOWON 0` / `Text ?AJ12345678901234` / `$$Text 1 &#268;AJ123` / `$$Text 2 456789` / `$$Text 3 01234`, then two blank lines; no `$$MEMORY`, no `CHAN` (every level zero) |
| Channel step 13.2 | seq 87521: `14 00 84 00 00 2E 00 00 1E 00 32 00 00` | `CUE 13.2` / `$$CUEPARAMS 0` / `UP 3.0   0` / `DOWN 5.0   0` / `FOLLOWON 0` / `CHAN 47/100`; no `$$MEMORY` |
| Point cue 12.1 | seq 87509: `11 00 79 01 00 00 00 00 32 00 32 00 00` | `CUE 12.1` / `$$MEMORY 0 0` / `$$CUEPARAMS 1` / `UP 5.0` / `DOWN 5.0` / `FOLLOWON 0` / `CHAN 7/100 8/100` (memory 1's channels, as for every memory step), between `CUE 12.0` and `CUE 13.0` |
| KOTLIK | seq 87433: body[2] 9, body[3] 2; records 2 and 7 `00 1E 01 00 00 00 00 32 00 32 00 00`, `00 50 01 00 00 00 00 32 00 32 00 00` | `$SEQUENCE 0 2 0 256 256`; the new steps 3rd and 8th as `$CUE  1.0` / `$$MEMORY 0 0` / `$$CUEPARAMS 1` / `UP 5.0` / `DOWN 5.0` / `FOLLOWON 0` / `CHAN 7/100 8/100` (the file numbers a sequence step by its memory, F75) |
| Patch | seq 87317: DMX 100 = 0x2F | `PATCH 1 … 100/47/100` |
| Page 12 | seq 87401: `0B 03 06 00 54 …` | `Text T` / `$$Text 1 T` |

- **The State step's label in the file is the console's own reading of the block it stored:** `01 0C` became `&#268;` (U+010C) and the
  lines split 6 / 6 / 5 code units: **reading A of H40** (UTF-16 big-endian, 18 units in three lines of 6). Reading B (a pad byte, then
  UTF-16 little-endian, 12 / 12 / 11 bytes) would make the first character U+000C; the file contradicts it for the console's side. The
  derived `Text` line joins the lines and writes `?` for the character outside ASCII (`asc-format.md`, commit dcb67e6). OBSERVED once (one
  label, one save).
- **The State step has no `CHAN` line because its levels were zero** (W9, W38, seq 87517): `asc-format.md`'s new rule (dcb67e6) "no
  `CHAN` line although SmartSoft's write carried 48 levels" should say that the 48 levels were all zero; whether the console saves a
  State step's non-zero levels as a `CHAN` line is untested.
- **`startdiff` against E12's first bodies:** 158 of 167 identical; the others are E12's memory 9 and the Sequence records with c (2 and 7
  here, 6 there), StepContent 19–21 (fetched later in each session), FaderInfo, PlaybackInfo, LedStatus (blocks 25–48) and FileName
  ("SHOW14.ASC" against "SHOW12.ASC", F103: the owner's latest SD save, the backup).
- **LED blocks 25–48 read dim red `0A 00 00 0A 00 00` in every LedStatus payload from the first** (seq 14; `blk2548.py`), after a power-on
  in channel mode (ConsoleStatus body[0] = 0 in download 1, the owner's starting position): as E9b, unlike E12's dim green (H78 stays open).
- **ChannelText 6 still carries E9's residue** `06 03 06 00 00 00 61 00 6A` (seq 422, 87553), a fourth capture after another power cycle
  (F144).

### Hypotheses H91–H106

Proposed verdicts (the curator applies them; **none of these messages is cleared, and our code never sends any of them**). "Two
sessions" = E12 + E12b, separate captures after separate console power cycles.

| H | Verdict proposed | Evidence E12b (Wn, seq) | Reproduced in two sessions? |
|---|---|---|---|
| **H91** Patch write | **CONFIRMED in two sessions** for the channel-onto-cell drag: one 514-byte Patch write per drop, only the dragged entry changed, stored (download 2 = W3), saved as `100/47/100` | W3 (seq 6343 / 6344, f12687); seq 87317 | **yes** (E12 W9); the IND drag (entry 49) once, E12 W10 |
| **H92** un-patching | **DMX-cell half CONFIRMED in two sessions**; ids 29–31 absent in both; channel-drop half HYPOTHESIS (not tested, deliberately) | W62 (seq 107346 / 107347, f214693) = download 1's Patch; SHOW16 = SHOW14 | **yes** (E12 W67, W68) |
| **H93** Independents write | **CONFIRMED in two sessions:** whole 85 bytes, one write per change, record byte 2 = button mode, byte 3 = GM; **value names (OBSERVED once):** byte 2 0 LATCH / 1 BUMP, byte 3 1 GM NO / 0 GM YES | W4–W7 (seq 13695 … 19267, f27391 … f38535); owner 202.278, 246.749, 299.400 | **yes**; the console's reaction unobserved in both |
| **H94** step edits | **CONFIRMED in two sessions** for one 13-byte StepContent per time edit (E12 a memory step, E12b a channel step) and the 100-byte type-2 write with level and label blocks; **new:** the console stores a type-2 label verbatim (148 bytes) and saves it | W41; W9, W38; seq 61684, 87517, 100192; SHOW15 | **yes**; a sequence step's time edit once (E12 W32); the D4 clause untestable (SmartSoft has no memory-step label) |
| **H95** run mode and labels | **CONFIRMED for SmartSoft's side and the console's storage:** body[3] 0 Loop, 1 One Shot (two sessions), **2 Manual** (once, stored and saved as `$SEQUENCE 0 2 0 256 256`) | W48, W49, W54; seq 87433; SHOW15 | 0 / 1 **yes**, 2 once; the console's playback of One Shot / Manual and the meaning of the stack's b = 2 stay HYPOTHESIS |
| **H96** command addressing | **CONFIRMED for the two values in two sessions** (stack a = 48: 18 of 18; KOTLIK a = 0: 6 of 6) | 12 commands | **yes**; the formula stays open |
| **H97** command bodies and sources | **CONFIRMED in two sessions:** the bodies, MoveStep before id 42 (47–49 ms), no id 33; b and c as in F151 (stack InsertStep 7 of 7, DeleteStep 10 of 10) | W8 … W61 | **yes**; id 42 = renumber supported twice (INFERRED meaning); MoveStep one value pair (17 → 20 both times) |
| **H98** two phases | **CONFIRMED with the correction in two sessions** (10 of 10; the type in phase 2, now also type 0) | W8 / W9, W39 / W40, W42 / W43, W44 / W45, W46 / W47 | **yes** |
| **H99** page and memory names | **Page part CONFIRMED in two sessions** (39-byte write, stored, saved as `$PAGE 12` "T"); the Memory part once (E12) | W50, W53; seq 87401; SHOW15 | Page **yes** |
| **H100** absent features | Unchanged: supported by absence in two sessions (no other Patch message, no ids 29–31, no level in a Patch write) | the whole capture | — |
| **H101** lists after edits | **Object writes → the written object never listed: CONFIRMED in two sessions** (64 of 64), refined (with a live stack, PlaybackInfo / DisplayContent may follow); **commands → lists of the affected objects in two sessions** (24 of 24); the console's own menu edits once (E12) | section "H101" | first two parts **yes** |
| **H102** c of a listed Sequence record | **CONFIRMED in two sessions:** c = the changed step's index, 6 of 6 records, values 2, 6, 7 | seq 68201, 73708, 94586, 95753 | **yes**, with new values |
| **H103** sequence step numbers after InsertStep | **CONFIRMED in two sessions:** the new step takes its ordinal number, never c (3 of 3); c = the step count after the insert × 10 in 3 of 3 (INFERRED meaning) | W44, W46; seq 68205, 73712 | **yes** |
| **H104** 0x3A = PAUSE | **REJECTED (OBSERVED once):** the key that held a running fade was 0x32 (block 51); 0x3A had a visible effect without a fade (ConsoleStatus body[6], block 59); successors H107, H108 | W14 / W15, W18 / W19; W26–W33 | — |
| **H105** phase-2 trigger | **CONFIRMED as an order in two sessions** (10 of 10: phase 2 after the fetch of the commanded index's object, before other listed records); the delays (≈ 160 ms after two fetches, ≈ 110 ms after one) favour "the object's arrival" over a timer; the mechanism stays INFERRED | W9, W40, W43, W45, W47 | **yes** |
| **H106** console-side commands | **UNTESTED** (the optional console-side menus were declined) | — | — |

### Other hypotheses and facts touched

| Id | Verdict proposed (OBSERVED at most) | Evidence |
|---|---|---|
| **H12** `$$CUEPARAMS` = step type | **CONFIRMED** (types 0 and 2 now on the console and in a console-written file; type 0 once): 0 = channel step (record byte 2 = 0, LCD "Ch:47", SHOW15 `$$CUEPARAMS 0` + `CHAN 47/100`), 1 = memory step, 2 = the console's "State" (LCD in E12 and E12b, SHOW15 `$$CUEPARAMS 2`) | W40, W41, W38; seq 54429, 87517, 87521; SHOW15 |
| **H40** label byte order | **Reading A supported on the console's side** (OBSERVED once): the console returned the written block verbatim and its own SD export decoded `01 0C` as U+010C with lines of 6 / 6 / 5; reading B contradicted for the console's side. Proposed: CONFIRMED for reading A (SmartSoft's side in E9, E9b, E12, E12b; the console's side once): the curator's call. The 6-character third line is not testable through SmartSoft, which keeps 17 characters (H110) | W38; seq 61684, 87517, 100192; SHOW15 |
| H41 | Supported: a type-0 record's bytes 3–4 = the channel − 1 (one sample); Up and Down one field per write again | W40, W41 |
| H42 | Supported: a 156-byte Sequence (9 steps); body[3] = field b, 2 = Manual, stored and saved | W47–W49; seq 87433 |
| H43 | Supported: the wire numbers KOTLIK's steps ordinally (new 3.0 and 8.0, the rest renumbered), the file by memory (`$CUE  1.0`) | seq 68205, 73712, 87433; SHOW15 |
| **H44** Independents a–d | **PARTLY CONFIRMED:** a = the on state (E8, E8b), c = the button mode and d = the GM setting (E12, E12b), with SmartSoft's value names once; b = the level not exercised | W4–W7 |
| H50, H76 | Supported: record 5 bytes 0–1 = the next step's index (`00` "Next: 1.0", `12` "Next:13.1", `13` "Next:14.0"); GO with STACK dim kept the index; bytes 4 and 5 are new flags (H107); at rest bytes 10–13 follow the next step's Down / Up (INFERRED); record 1 = KOTLIK's next index 2, whose times followed the inserted step | seq 39206 … 42040; 58037, 105929; 68211, 95761 |
| H64 | Supported: block 50 blue during SmartSoft's GO fade and the console's back fade | seq 39210, 41605 |
| H65 | Supported: [22, 21] pending at the first poll after this power-on | seq 10 |
| H78 | Inconclusive: dim red after a channel-mode power-on (as E9b; E12 dim green) | `blk2548.py` |
| H82 | Supported: 0x32 lit block 51 (a new code), 0x3A lit block 59 (in channel mode; E12's 0x3A in memory mode lit nothing) | seq 39288, 39863 |
| H30, H31 | Supported: the System pair after reply headers #40 / #79 in both sessions; the console's counter continued 244 → 245 | `sessions` |
| H66, H83 | Untested (both sessions sent the pair; no EDIT MEM) | — |
| F144 | Annotate: E9's residue in ChannelText 6 returned in a fourth capture | seq 422, 87553 |
| F48, F59 | Annotate: ConsoleVersion again | seq 99, 87218 |
| F52, F131 | Annotate: a fourth capture of writes (62 of 62 stale; counter 0–49, 0–11; new w1 = 160) | `write_rules.py` |
| F57, F67 | Annotate: 558 records = 558 fetches in list order; four more Sequence records with c ≠ 0; Sequence 156 bytes; FileName listed after the console's SD save | lists |
| F62 | Annotate: a fetch's completion to the next poll min 10.193 ms (E12b; E12 10.255) | `summary` |
| F133, F134 | Annotate: 0x32 new (block 51); GO with STACK dim again changed no level | W10–W37 |

### Golden-test candidates

**Inbound (the console's bytes, for decoders):** seq 68205 (f136410) Sequence (2, 0, 0, 2), 144 bytes, the default step copied at index
2; seq 73712 (f147424) Sequence (2, 0, 0, 7), 156 bytes; seq 87433 (f174866) the 156-byte Sequence with body[3] = 2 (Manual), = SHOW15's
`$SEQUENCE 0 2`; seq 87517 (f175034) the 148-byte State step with the label `01 0C …`, = SHOW15's `CUE 13.1`; seq 87521 (f175042) the
channel step, = `CUE 13.2`; seq 54419 (f108838) the console's default step (type 0); seq 87297 (f174594) the 160-record list; seq 85013
(f170026) the list [36, 21, 22] after the SD save and seq 85017 (f170034) FileName "SHOW15.ASC"; seq 39859 (f79718) ConsoleStatus with
body[6] = 1; seq 39288 / 39863 LED blocks 51 and 59; seq 39277 and 39368 PlaybackInfo record 5 held and running back; **seq 85008 (f170016),
the cancelled 0-byte read, for a test of our session's fault path on a real failure** (a replay test, not a recorded success).

**Outbound (SmartSoft's bytes; decoder or replay tests only, never an encoder: none of these messages is cleared, and no encoder for them
may exist in the solution):** W3 (Patch), W4 / W6 (Independents c and d with their names), W8 / W9 (InsertStep + the 100-byte Direct
cue), W38 (a label with U+010C), W39 / W40 (the channel step), W44 / W45 and W46 / W47 (sequence inserts at indices 2 and 7), W49 (Manual),
W57 / W58 (MoveStep + id 42 → 13.3), W59–W61 (deletes, including the live step), W14 (0x32), W26 (0x3A).

### Input for the knowledge base (for the curator; nothing applied here)

**Status changes and annotations** (if the curator applies all seven promotions, F151, F152, F156 and F157 fully and F153, F154 and F158
in part, the VERIFIED count goes from 65 to 72, counted as after E12; none of them is clearance to send):

| Id | Current | Proposed | Evidence (E12 + E12b) |
|---|---|---|---|
| **F151** the step commands | OBSERVED (one capture) | **VERIFIED** (as SmartSoft's and the console's behaviour): the bodies, a = 48 / 0, InsertStep b / c for the stack, DeleteStep b / c = 0, MoveStep → id 42, no id 33, an answer on the next poll to every command (24 of 24). Annotate: the delete lists cover the deleted index to the new last index; KOTLIK's c = the step count after the insert × 10 (INFERRED, H103); id 42 = renumber supported twice (INFERRED meaning). **NOT CLEARED** | E12 W6 … W65; E12b W8, W39, W42, W44, W46, W55–W61 |
| **F152** two phases | OBSERVED | **VERIFIED:** 10 of 10 inserts; the default steps (stack 7 of 7, sequence 3 of 3); the type only in phase 2 (types 0, 1, 2); phase 2 after the fetch of the commanded index's object, 109–161 ms after the command (the trigger INFERRED, H105) | E12 W6 / W7 … W30 / W31; E12b W8 / W9 … W46 / W47 |
| **F153** whole-object writes | OBSERVED | **VERIFIED in part:** Patch 514, Independents 85, StepContent 13 and type-2 100 (console 148), Sequence 10 + 38 + 12 n (144 both, 156 E12b), Page 39; no qualifier fields; the written object never listed back (64 of 64); the console stores what was written (both downloads 2). **Still OBSERVED once:** the Memory part of this fact (F143 is VERIFIED separately) and the unchanged rewrites (E12 10 of 46; E12b 0 of 18 with no looks). Annotate: with a live stack two writes were followed by PlaybackInfo / DisplayContent | E12b W3 … W62; seq 87317, 87401, 87433, 87509, 87517, 87521 |
| **F154** field readings | OBSERVED | **VERIFIED in part:** (2) the positions of the button mode and GM bytes; (3) run mode 0 Loop, 1 One Shot; (5) type 2 = the console's "State". **OBSERVED once:** the IND value names (E12b), Manual = 2 (E12b), type 0 = "Ch:n" (E12b), (1) entry 49 = "i1" and (4) the "W" naming (E12) | W4–W7, W48, W49, W54; LCD seq 54429, 41609 |
| F155 | OBSERVED | Unchanged (no console-side menu edits in E12b) | — |
| **F156** c ≠ 0 records, content lengths | OBSERVED | **VERIFIED:** Sequence (2, 0, 0, c) with c = the changed index after SmartSoft's sequence commands (6 of 6, c = 2, 6, 7), Sequence bodies of 144 (both) and 156 (E12b) bytes, 148-byte type-2 StepContents (both); the SC-1 consequence stays INFERRED from the code | seq 68201 … 95757; 61684, 87517 |
| **F157** the downloads | OBSERVED | **VERIFIED:** after edits the full list is the first list plus the added records in list order (161 = 157 + 4; 160 = 157 + 3), the downloaded bodies equal the last writes; the 157-record list byte-identical for the twelfth time | seq 178, 87297 |
| **F158** live keys and rewrites from the edit tabs | OBSERVED | **VERIFIED in part:** SmartSoft's stack view sends live keys (GO in both, 0x3A in both, 0x32 in E12b; E12b's owner-explained). **OBSERVED once:** E12's unchanged rewrites and the FADERS MEMS of D4. Annotate: opening the IND panel wrote nothing (E12b) | W10–W37 |
| F159 | OBSERVED | Unchanged (no label clear in E12b); annotate the 17-character limit of SmartSoft's step label (H110) | W38 |
| F143, F146, F147 | VERIFIED (in part) | Unchanged (no memory write or recording in E12b) | — |
| F144 | VERIFIED in part | Annotate: the residue returned in a fourth capture | seq 422, 87553 |
| F73 | OBSERVED (text match; byte order HYPOTHESIS) | Curator's call: the console's own export decodes `01 0C` as U+010C (reading A) | SHOW15; seq 87517 |
| H91–H106 | as listed | As the table "Hypotheses H91–H106" | — |
| H12 | OPEN (supported) | **CONFIRMED** (0 and the file side of 2 OBSERVED once) | W40; SHOW15 |
| H40 | OPEN (byte order HYPOTHESIS for the console) | **Reading A CONFIRMED for the console's side, OBSERVED once** (curator's call) | W38; seq 87517; SHOW15 |
| H44 | OPEN (supported) | **PARTLY CONFIRMED** (a, c, d) | W4–W7 |
| H41, H42, H43, H50, H64, H65, H76, H78, H82, H30, H31 | as listed | Annotate as in the table above | — |
| docs/formats/asc-format.md (dcb67e6) | rule "no CHAN line although SmartSoft's write carried 48 levels" | Say the 48 levels were zero (for the `asc-format-analyst`) | W9, W38; seq 87517 |

**New facts (proposed IDs; all OBSERVED once in E12b; NOT CLEARED, never sent by our code):**

- **F160 The channel step (type 0).** D6: InsertStep `30 13 00 84` (W39), then StepContent (4, 19) `13 00 84 00 00 2E 00 00 32 00 32 00 00`
  (W40): record byte 2 = 0 and bytes 3–4 = 0x002E for channel 47 (chosen from SmartSoft's drop-down of all channels; no level field);
  the Up edit was one 13-byte write (W41). The console's LCD named the step "Ch:47" (seq 54429, 100212); download 2 holds it (seq 87521);
  SHOW15 saved it as `CUE 13.2` / `$$CUEPARAMS 0` / `UP 3.0` / `DOWN 5.0` / `FOLLOWON 0` / `CHAN 47/100`, no `$$MEMORY`. Reading: bytes 3–4
  of a type-0 record = the channel − 1 (one sample); the level is not in the record and the file writes 100 (INFERRED); the console's
  default step after InsertStep (type 0, bytes 3–4 = 0) would then be channel 1 (INFERRED; "Ch:1" on the LCD at seq 105933).
- **F161 A labelled Direct cue ("State") written, stored and saved.** D5: InsertStep `30 12 00 83` (W8) and phase 2 (W9) with 48 zero
  levels while channel 5 stood at 0x88: SmartSoft's new Direct cue does not hold the console's live levels. W38 wrote the label: of the
  18 characters asked for (`čAJ123456789012345`) SmartSoft wrote 17 code units, U+010C first (`01 0C 00 41 00 4A …`, the 18th unit zero).
  The console stored the block verbatim (its 148-byte body = W38 in its layout: seq 61684, 87517, 100192) and saved the step as `CUE 13.1`
  / `$$CUEPARAMS 2` / `UP 5.0` / `DOWN 5.0` / `FOLLOWON 0` / `Text ?AJ12345678901234` / `$$Text 1 &#268;AJ123` / `$$Text 2 456789` /
  `$$Text 3 01234`, no `$$MEMORY`, no `CHAN`: the console reads the block as three lines of six UTF-16BE code units (H40 reading A). The
  LCD shows the step as "State" (as in E12).
- **F162 The SD file forms of SmartSoft's other edits (SHOW15.ASC).** The point cue `CUE 12.1` with `$$MEMORY 0 0`, `$$CUEPARAMS 1` and
  memory 1's channels, in stack order; KOTLIK as `$SEQUENCE 0 2 0 256 256` (Manual = field b = Sequence body[3] = 2) with the two new
  steps at the 3rd and 8th place as `$CUE  1.0` / `$$MEMORY 0 0` / `$$CUEPARAMS 1` (numbered by their memory, F75; the wire 3.0 and 8.0);
  the patch entry `100/47/100`; `$PAGE 12` with `Text T` / `$$Text 1 T`. Download 2 against SHOW15: every compared field agrees.
- **F163 During the console's SD save a reply did not come; the host side cancelled the read and reset both pipes (ETC's driver,
  INFERRED); SmartSoft carried on.** During the
  edited-state save the reply-header read after the poll seq 85007 got no data and was cancelled after 111.9 ms (seq 85008, f170016,
  USBD_STATUS_CANCELED, 0 bytes); URB_FUNCTION_SYNC_RESET_PIPE_AND_CLEAR_STALL followed on 0x04 (670.6 ms) and 0x83 (9.0 ms) (seq 85009,
  85010, USBPcap type 0xFE); SmartSoft's next poll 29.7 ms later was answered at once with [36, 21, 22] and FileName "SHOW15.ASC". No IN
  data for 853.5 ms, between "Confirm?" and "Show file saved"; the payload counter and the stale words continuous; no restart. A 31.8-ms
  reply in the save menu (seq 83531); otherwise ≤ 6.6 ms (p99.9). The first non-zero USBD status and the first SD save during a polled
  session in any capture. The cause (the SD write) and the consequence for our code (a reply timeout ending the session, a safe stop)
  are INFERRED (H109).
- **F164 The stack's live and next positions are step indices; step commands on a live stack change them.** With STACK lit and Live 13.1 /
  Next 14.0 (record 5 bytes 0–1 = 19), D2's insert at index 17 made 13.0 live (LCD seq 61700), the move and the deletes changed the next
  step (seq 100212, 103388) and the deletes of the live step made the following step live at once (seq 104540, 105933), each without a GO
  or a fade; PlaybackInfo record 5's times followed the next step (seq 58037, 61696, 100208, 103384, 105929) and KOTLIK's record 1 its next
  step (seq 68211, 95761). No level object changed (the stack produced no output in this session; why UNKNOWN). STACK stayed lit to the
  end of the capture.
- **F165 Key codes 0x32 and 0x3A from SmartSoft's stack view.** 0x32 (new; six press / release pairs, W10–W35): during a GO fade it held
  the fade (PlaybackInfo record 5 byte 5 = 1, bytes 2–3 frozen, seq 39277) and lit block 51; a second press ran the fade back to its
  start (byte 4 = 1, seq 39368 … 39442); without a fade it toggled byte 5 and half of block 51. 0x3A (four pairs, W26–W33): toggled
  ConsoleStatus body[6] (seq 39859, 39894, 39909, 40648) and LED block 59 (`37 37 FF 0A 0A 0A`), nothing else; in E12 (memory mode) it
  changed nothing. The owner clicked "next", PAUSE and PLAY in SmartSoft's stack view (statement). Identities HYPOTHESIS (H107, H108);
  outside SC-2's whitelist; no code may be derived.

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H107 | Key code 0x32 is the console's PAUSE key (LED block 51): during a stack fade it holds the fade (PlaybackInfo record 5 byte 5 = 1, block 51 half-lit) and a second press runs it back (byte 4 = 1, block 51 fully lit) | F165; adjacency with GO = 0x31 / block 50; the manual's crossfader keys GO, PAUSE, RATE, CLEAR | Owner, zero risk: which SmartSoft button sent it; passive: one logged PAUSE press on the console during a GO fade with STACK dim and nothing up (a live action: the owner's decision) |
| H108 | Key code 0x3A is a toggle key whose state is ConsoleStatus body[6], shown on LED block 59; it acts in channel mode only | F165 (E12b channel mode: four toggles); E12 W36 / W37 (memory mode: nothing); RATE is the one crossfader key of the manual's list without a code (weak, INFERRED by elimination) | Owner, zero risk: which SmartSoft button sent it and which console key lights light blue; static: the Console Simulator's layout resource (smartsoft-analysis.md S11.15 row 4) |
| H109 | The console does not answer USB transfers while it writes its SD card; a host read pending then fails (SmartSoft's driver cancelled after about 112 ms), and a session of ours would end on its reply timeout (500 ms) | F163 (one save; 853.5 ms without IN data; the 671-ms reset of the OUT pipe) | Passive: one more logged SD save while SmartSoft is Online (the confirm press logged), to see the gap again and its length; never with our software running |
| H110 | SmartSoft's step-label field holds at most 17 characters (three lines of six less one), so SmartSoft never writes an 18th code unit; a full third line of a 1248 label cannot be produced through SmartSoft | E12b W38 (18 asked, 17 written; the owner saw 17); E12 W63 (17 characters) | Static, zero risk: the label editor's maximum length in `SmartSoft.exe` |

**Readings that stay INFERRED:** that the arrival of the new step's object triggers phase 2 (H105, now strongly supported as an order);
that id 42 means "renumber" (two samples); that c of a sequence InsertStep is the step count after the insert (three samples); that a
type-0 record's bytes 3–4 are the channel − 1 and the console's default step is channel 1; that SmartSoft fills a new Direct cue from its
own state; that the owner, not SmartSoft, wrote the capital "Č" (owner question 2); the identities of 0x32 and 0x3A; that the SD write
silenced the console (H109) and that a session of ours would have ended; that etchsp.sys has a read timeout of about 100 ms and issued
the resets; that the stack produced no output because of its crossfaders; record 5 bytes 10–13 = the next step's Down / Up; the SC-1
consequences (from the code).

**What E12b does not show:** the console's own Insert / Delete / Step Timing (H106, declined); a memory-step label; Memory writes; the IND
drag onto a cell (entry 49); a sequence step's time edit; a label clear (F159); the console's playback of One Shot or Manual; a State step
with non-zero levels (SmartSoft wrote none) and whether the console would save them as `CHAN`; a full third line of a label; what the
console does with the changed IND values; whether the console accepts any of these writes from a host that never sent the System pair; a
third SmartSoft session after the restores (SHOW16 = SHOW14 instead); DMX output effects (no level changed; DMX 100 unconnected). It shows
SmartSoft's messages and the console's answers, **never that any of them may be sent by us**.

### Owner questions (zero risk; statements never promote)

1. **22:40:05–22:40:30:** SmartSoft sent key code 0x32 six times, GO four times and 0x3A four times (around 22:40:15 three times and at
   22:40:27). Which SmartSoft buttons are "next", "PAUSE" and "PLAY" (their labels or icons), and which one did you click around 22:40:15?
   On the console, which key's LED lit light blue then (LED block 59) and went off at 22:40:27?
2. **D5 label:** did you type a small "č" or a capital "Č" (was Caps Lock on)? SmartSoft wrote "Č" (U+010C); in E12 a step label kept its
   small letters.
3. **D5:** did SmartSoft's Insert step > Direct cue offer any way to give the cue levels? It was written with every level at zero
   although fader 5 stood at about half.
4. **The console after the session:** is STACK still lit? The capture ends with STACK lit (LED block 60) and the stack's live step 14.0 with
   no next step; please switch STACK off before E11 (its GO step needs STACK dim). Live state, not show data (SHOW16 = SHOW14).
5. (Minor) **C3:** "BUMP, double click" gave one write; in E12 one change gave three writes (00 → 01 → 00 → 01). How many clicks then?

### Next capture to take

- **No repeat of E12b is needed for the reproduced findings.** For what stays open, optional and passive (SmartSoft Online, USBPcap first,
  backup first, our software not running): the console's own Insert / Delete step in the stack and in KOTLIK and Step Timing on a "State"
  step (H106); one logged PAUSE press on the console during a GO fade and one logged press of the key with the light-blue LED (H107,
  H108), STACK dim and nothing up; one logged SD save while SmartSoft is Online with the confirm press logged (H109). Each is one action
  with a wait; the owner decides.
- **Zero-risk static checks:** the Console Simulator's layout resource (smartsoft-analysis.md S11.15 row 4: its "Button" sections) for the
  key codes 0x32 and 0x3A; the step-label editor's maximum length (H110); the insert dialog's accept path (H105).
- **For E11 (our first live-control session):** no SD save while our session is connected (F163, INFERRED consequence); the guide's order
  (Disconnect, then the SD save) stays; STACK dim before the GO step (owner question 4).

## Anomalies and notes (provenance level)

1. One failed IN transfer (USBD status 0xC0010000, 0 bytes) at 22:52:04.57, while the console was saving the show to the SD card
   (the edited-state save, logged 22:51:23–22:52:12), followed by two USBPcap type-254 transfers: the console (or ETC's driver) did
   not answer one poll in time during the SD write. What SmartSoft did next is for the analysis (it stayed Online: the log and the
   following writes show the session going on).
2. The optional console-side menus (H3–H7) were declined, so the console's own Insert / Delete step (H106) and a console-side Step
   Timing on a "State" step remain untested.
3. Between 22:40:06 and 22:43:16 (after the Direct cue's label was typed) the owner used SmartSoft's next / PAUSE / PLAY and the
   console's STACK and PAUSE + PLAY to bring the new row into view (owner statement); not logged as steps.
4. The point cue was moved to 13.3, between the channel step 13.2 and 14.0, not between the last original cue and the Direct cue as
   the guide said (owner statement): a MoveStep sample all the same.
5. SmartSoft kept 17 of the 18 label characters and uppercased the "č" (the file holds `&#268;`, "Č"): the label block's form and
   length limits are for the analysis and the asc-format analyst.

*(Added with the analysis, 2026-10-31, late evening; notes 1–5 above are kept as written. Note 1's type-254 transfers are the two pipe
resets of the section "The failed IN transfer during the SD save"; note 5's "uppercased" is not established, see note 9.)*

6. **The only non-zero USBD status of any capture** (seq 85008): a reply-header read cancelled by the host after 111.9 ms during the SD
   save, then a reset of each pipe (670.6 and 9.0 ms); 853.5 ms without IN data; SmartSoft polled again 29.7 ms later and carried on (F163).
7. **The first SD save made while a host polled**, and the first FileName change inside a session; FileName (36) was listed by the console
   after its own save ([36, 21, 22], seq 85013). One more slow reply came in the save menu (31.8 ms, seq 83531).
8. **The playback deviation** (note 3) sent 28 key events from SmartSoft: code **0x32, never seen before** (six pairs), GO (four) and 0x3A
   (four); the console's STACK and PAUSE + PLAY moved the stack to Live 13.1. STACK stayed lit to the end of the capture, and the later step
   commands changed the stack's live and next steps under it (F164); no level object changed at any point after 536.3 s.
9. **D5's label:** the guide asked for `čAJ123456789012345` (18 characters, a small "č"); SmartSoft wrote 17 code units with U+010C ("Č").
   E12's step label kept small letters, so whether SmartSoft or the owner capitalised the "č" is owner question 2.
10. **D5's Direct cue has no level:** SmartSoft wrote 48 zero levels while channel 5 stood at 0x88; the console stored 96 zeros and SHOW15
    has no `CHAN` line for it. The guide's aim (a Direct cue with a level) was not reached.
11. **Opening the IND 1 panel wrote nothing** (C3, 135.8–216.3 s), unlike E12's unchanged Independents write in A3, whose cause stays
    open.
12. **Download 2's 160-record list (payload 807 bytes) is longer than `HspReadSession.MaxObservedPayloadLength` (792)**, like E12's
    161-record list (812): irrelevant to a read-only session, which never sends the System `07 00 00` (INFERRED from the code).
13. **No third SmartSoft session** (by design): the restores are shown by SHOW16 = SHOW14 and by the console's last listed bodies, not by a
    download.
14. **Both captures' MoveStep was `30 11 14`** (from 17 to 20), between different neighbours (E12 14.1 / 15.0, E12b 13.2 / 14.0): the
    reading "b = from, c = to after the move" rests on one value pair.
15. **The LED blocks 25–48 were dim red** after this channel-mode power-on (E12: dim green): H78 stays open.

**Runbook deviations** (besides notes 3, 4, 9 and 10): D5's label was set only after the unlogged playback (W38 at 22:40:51, 60 s after the
step's stamp); STACK was left lit. Otherwise the trimmed guide was followed: the power cycle, the named backup first (`SHOW14.ASC`), Memory
Integrity off, ETC's driver on interface 1, USBPcap before SmartSoft, the timed log, the waits, no look in the editors outside the steps
(no unchanged rewrite), no wheel, no show, erase, settings or page action in SmartSoft, the DMX 100 cell (not a channel) onto the un-patch
bin, the edited-state SD save, every change restored (`SHOW16.ASC` = `SHOW14.ASC`).

## Safety

Passive capture: SmartSoft drove every write, our software was not running on the console's interface and sent nothing. The show was
backed up before (SHOW14) and saved after (SHOW16), both kept and equal. **E12b clears nothing** (SC-1's poll and listed fetch remain the
only cleared messages; SC-2's decisions are unchanged; the stack, sequence, page and step-command writes are in no class, the Patch and
Independents writes class F "Never", the Memory write class D).

*(Added with the analysis:)* The analysis read the committed files only, with offline tools that cannot open a device. Where E12b
reproduces E12, SmartSoft's step commands (InsertStep 32, DeleteStep 34, MoveStep 35, id 42) and its Patch, Independents, StepContent,
Sequence and Page writes become **VERIFIED as SmartSoft's and the console's behaviour, never sendable**: they change show data, the console
acknowledges none of them, a lost second phase or renumber leaves the stack inconsistent, a command on a live stack changes what the next
GO plays (F164), and they are reversible only by another exact write or the SD backup. E12b adds two observations for our own code (no code
was changed here): **during an SD save on the console a reply did not come within the 112 ms SmartSoft's driver waited, and no IN data
came for 853 ms** (F163; the SD write as the cause is INFERRED, H109; a session of ours would most likely end on its reply timeout, a
safe stop, INFERRED: no SD save while our session is connected), and SmartSoft's stack view sends live keys (GO, 0x32, 0x3A) from an
editing tab (F165). Both are input for the coordinator.
