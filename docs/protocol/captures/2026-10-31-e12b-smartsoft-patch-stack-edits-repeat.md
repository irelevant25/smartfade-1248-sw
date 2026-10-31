# 2026-10-31 E12b: the second SmartSoft session editing the patch and the stack / sequences (USBPcap, passive, guided script)

**Status of this record: provenance, completeness and structure. The decoding of SmartSoft's writes and the per-hypothesis
verdicts follow in the section "Analysis" (a capture-analyst pass).** Nothing in this capture is cleared for sending; our software
was not involved and sent nothing.

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

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py <split> -o <out>.jsonl --device-address 30 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E12b; E12 read it in the ConsoleVersion object)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 22:29:22.355)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 22:29:23.521; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-31-e12b-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (...) of the USBPcapCMD root-hub capture e12b.pcap (SHA-256 E24B78C4..., 1101137231 bytes, 911995 frames by a tshark field census, 22:29:47.631071-22:58:51.038318 local time), ... the console was power-cycled before the session (owner, log 22:27:17); our software was not involved" --note 1743.407247 "analyst: the raw capture's last frame (another device) is at 22:58:51.038318 local time; the console's last frame, frame 218846 of this split, is at 1734.519467 s (22:58:42.150538); the log stamps SmartSoft's second close at 22:58:39.142 (start) and 22:58:44.556 (closed) and USBPcap's stop at 22:58:53.109"
```

(The two notes are quoted in full in the JSONL's first records.)

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

## Analysis

*(To be written by the capture-analyst pass: the 62 writes matched to the action log, the console's answers and lists, the two
downloads (157 / 160 records), the repeat of E12's findings for H91–H99, H101 and F151–F159 (VERIFIED needs this second session), the
new items (H102 / H103 inserts at two places of KOTLIK, the channel step, Manual, the labelled Direct cue with a level, the IND values
LATCH / BUMP and NO / YES against bytes 2 and 3 of the Independents record, H44 / H93), the failed transfer at seq 85008 during the SD
save, the unlogged playback of 22:40–22:43, and the input for the knowledge base.)*

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

## Safety

Passive capture: SmartSoft drove every write, our software was not running on the console's interface and sent nothing. The show was
backed up before (SHOW14) and saved after (SHOW16), both kept and equal. **E12b clears nothing** (SC-1's poll and listed fetch remain the
only cleared messages; SC-2's decisions are unchanged; the stack, sequence, page and step-command writes are in no class, the Patch and
Independents writes class F "Never", the Memory write class D).
