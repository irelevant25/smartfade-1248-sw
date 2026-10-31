# 2026-10-31 E12: SmartSoft editing the patch and the stack / sequences (USBPcap, passive, guided script)

**Status of this record: provenance, completeness and structure only. The decoding of SmartSoft's writes is not in it (see
"Analysis", below).** Nothing in this capture is cleared for sending; our software was not involved and sent nothing.

- **Date:** 2026-10-31, afternoon. USBPcap ran 15:39:43.890770–16:32:26.220864 local time (CET, UTC+1; the first and last frames
  of the raw root-hub capture). The committed console-only split runs from 15:39:43.890770 (f1) to 16:32:14.523094 (f397536).
- **What:** the owner ran `tools/usb-probe/e12-guided-session.ps1` (commit 7f91015; fe7b91b later changed only the wording of
  steps E3 and I1) from 15:35:54 to 16:32:51 and followed every step, with all optional steps (C3, D2, D5, F3, H) accepted.
  SmartSoft 3.0.43 was Online in three sessions (started by the script at 15:39:48, 16:16:27 and 16:31:03; closed at 16:16:15,
  16:30:51 and 16:32:19) and made every edit; the console's own menus were used in step H. The runbook is in
  `docs/protocol/reverse-engineering.md` ("E12 runbook"); the step list with the PC clock is the action log.
- **Console:** ETC SmartFade 1248, USB device address **27** on bus 1 (21 in E9b, 10 in E8b and E9: the console re-enumerated
  after its power cycle; "power cycled = y" in the log at 15:36:49). Firmware not re-read (3.0.2.9.0.43 per the E8 statements;
  the ConsoleVersion object of this capture is for the analysis to compare). Memory Integrity off (script reading 15:39:26),
  interface 1 on ETC's driver (service ETCHSP, 15:39:28), USBPcapCMD on root hub `USBPcap1` with the script's suggested command
  (`-A --inject-descriptors`, INFERRED from the injected descriptors of addresses 1, 2, 3, 4, 18, 19 and 27 at frame 1's time).
- **Show backup (safety rule 3): taken.** `SHOW12.ASC`, logged at 15:37:24, before SmartSoft was started. After the session the
  owner saved `SHOW13.ASC` (16:32:51). Both are in `smartfade-sd-card-backup/` (git-ignored) and both have SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`: **byte-identical to each other and to `SHOW09.ASC`,
  `SHOW07.ASC` and `SHOW04.ASC`.** The show data after E12 equals the show data before it, so every test change of the session
  (patch, stack, KOTLIK, memory 9, the page names) was restored. The owner's slip in step F3 (page 1 renamed instead of page 12,
  corrected in I1) left no trace: page 1's name was empty before and is empty after.
- **Rig:** the owner's rig on DMX 5–16 (4 RGB fixtures); DMX 100–102 unconnected ("dmx 100-102 free = y", 15:37:34). The
  session changed live output only through the test patch of channel 47 and IND 1 to DMX 100 / 101 and the console's own
  "Patch by dimmer" of DMX 102, all restored.
- **Owner statements:** `2026-10-31-e12-owner-statements.txt` (the mid-session question about the run mode's place, the
  after-session notes: the page 1 slip in F3 / I1 and the Direct cue's label set and cleared inside I2, not D5; the SD copies;
  transcribed by the assistant, quoted verbatim). Owner statements never promote a fact by themselves.

## Files

| File | SHA-256 | Size / span | Committed | Role |
|---|---|---|---|---|
| `2026-10-31-e12-smartsoft-patch-stack-edits.pcap` | `63b32fc153a9a85b7c327e60efb217b42ae6a1dc552093527cb16672fe421094` | 19 580 904 B, 397 536 frames, 15:39:43.890770–16:32:14.523094 | yes, **the evidence** | Console-only split (device address 27) of the USBPcapCMD root-hub capture; classic pcap, link type 249, µs; made with tshark 4.0.3: `tshark -r <raw> -Y "usb.device_address == 27" -F pcap -w <split>`. **The raw root-hub capture is not committed** (it holds the traffic of other devices, addresses 1, 2, 3, 4, 18 and 19, among them the owner's keyboard at 19): `e12.pcap`, SHA-256 `92c13b05b1fc14dce40963484aeb6f4dfe362d5e603712b85edcccb9e39c68ae`, 2 003 901 408 B, 1 789 464 frames (tshark field census), 15:39:43.890770–16:32:26.220864. It was written to `C:\Windows\System32\e12.pcap` (the working folder of the owner's elevated PowerShell) and copied byte for byte into the repository root (git-ignored), same hash. Completeness below |
| `2026-10-31-e12-smartsoft-patch-stack-edits.jsonl` | `e8a91f7451a640f4f1ae5470aab7366c3479cfd9a2a0e3425ca56e2cfde41685` | 198 916 lines: 1 header, 198 768 `frame` records, 147 `note` records | yes (derived) | The split normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 27 --bus 1` (0 other-device frames), the action log merged as notes (`--notes-log`, `--notes-utc-offset +01:00`; the first lines have negative `t`, they precede the capture) plus two analyst notes (capture start with the raw capture's provenance; the raw capture's end). Command in `normalize_e12.sh` of the session's scratchpad, reproduced below |
| `2026-10-31-e12-actions.txt` | `2ae5e4045f5834deadd9c5a3549b82d276cfb6ab14f67a8586b384cabb136800` | 145 lines, 15:35:54.650–16:32:51.585 | yes | The guided script's action log: the owner's decisions and answers, every step and wait with the PC clock (CRLF, ASCII) |
| `2026-10-31-e12-owner-statements.txt` | see `README.md` | | yes | The owner's statements of the session and after it, quoted, with the assistant's reading for the analysis |
| `smartfade-sd-card-backup/SHOW12.ASC`, `SHOW13.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` (both) | 15 944 B each | no (git-ignored folder) | The backup before and the save after the session; equal to each other and to SHOW09 / SHOW07 / SHOW04 |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py`, `hsp_summary.py`, `e9/cmp_records.py` | unchanged since E9b (hashes in the E9b record) | | yes | Normalizer version 2; `summary`, `sessions`; the raw-versus-split record comparison |
| tshark / capinfos 4.0.3 (Wireshark portable) | | | no | The split, the frame census by address and the frame counts and spans |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py <split> -o <out>.jsonl --device-address 27 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E12; the ConsoleVersion object of this capture is for the analyst to compare)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 15:39:26.998)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 15:39:28.313; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-31-e12-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (...) of the USBPcapCMD root-hub capture e12.pcap (SHA-256 92C13B05..., 2003901408 bytes, 1789464 frames by a tshark field census, 15:39:43.890770-16:32:26.220864 local time), ... the console was powered off before the session (owner, log 15:36:49); our software was not involved" --note 3162.330094 "analyst: the raw capture's last frame (another device) is at 16:32:26.220864 local time; the console's last frame, frame 397536 of this split, is at 3150.632324 s (16:32:14.523094); the log stamps SmartSoft's third close at 16:32:15.357 (start) and 16:32:19.450 (closed) and USBPcap's stop at 16:32:27.659"
```

(The two notes are quoted in full in the JSONL's first records.)

**Completeness of the split:**
- Frame census of the raw capture by device address (tshark fields `usb.device_address`, `usb.transfer_type`, `usb.idVendor`):
  address 27 (idVendor 14D5, idProduct 0200, the console): 397 482 bulk, 53 control, 1 injected descriptor = **397 536**, the
  split's frame count. Address 4 (idVendor 0D8C, an audio device): 1 027 794 isochronous and 46 others, the bulk of the 2 GB
  file; address 19 (idVendor 0B05): 363 548 interrupt and 166 others, the owner's keyboard; address 3 (0BDA): 266; addresses
  1, 2 and 18: control only (96, 6, 6). Total 1 789 464.
- The normalizer run on the raw capture with `--device-address 27 --bus 1` found 397 536 frames and skipped 1 391 928 frames of
  other devices unread; its 198 768 records equal the split's in every field except the pcap frame numbers (0 differing records,
  `cmp_records.py`).
- The split's first frame is the raw file's first frame (the injected descriptors; same time stamp); its last console frame is
  at 3150.632324 s, 11.7 s before the raw file's last frame (another device).

## Structure (`hsp_summary.py summary` and `sessions`, read-only; counts, no decoding)

- 198 768 transfers: 99 297 bulk OUT (endpoint 0x04), 99 444 bulk IN (0x83), 1 control OUT, 26 control IN. USBD status
  non-zero: 0. **Framing errors (H15 / H16): 0.**
- Roles: 98 184 polls and 98 184 reply headers (96 924 idle replies, 287 lists, 973 type-3 replies), 973 fetches (each type-3
  reply echoing its fetch's id, a, b, c: 973 ok, 0 differ), 1 260 IN payloads (payload sequence 0 → 235 with four wraps and 0
  breaks), **70 Data headers with their 70 OUT payloads** (SmartSoft's writes). The stale-word rule (F52) holds in all 98 253
  poll and data headers (0 differ).
- Three SmartSoft sessions in the one capture, as planned: first lists at f356 (t = 7.23 s, 157 records: download 1), f278024
  (t = 2205.80 s, **161 records**: download 2, after the edits) and f388544 (t = 3082.11 s, 157 records: download 3, after the
  restores). The console's inbound payload sequence continues across both SmartSoft restarts (174 → 175, 75 → 76), as in E3b
  and E9b (H31). The time stamps of the action log line up with the frames through the merged notes (the log's `t` is the PC
  clock, the frames USBPcap's; both the PC's).

## Analysis

**Not done in this record.** What this capture was taken for (the E12 runbook and H91–H101: which objects SmartSoft writes for
a patch drag, an un-patch, an IND setting, a stack step's times and label, an inserted, moved and deleted step, a sequence's
step, run mode and label, a memory's content and name, a page's name; the layouts and the step-command bodies; what the console
lists after SmartSoft's edits and after its own menu edits, H101; the differences between the three downloads, 157 / 161 / 157
records) is the decoding of the 70 PC → console writes and of the console's answers around them. **The assistant's attempt at
that decoding on 2026-10-31 was stopped by its safety system and is not repeated**, as with the live-control encoder (CLAUDE.md,
"Current state"). The owner can do it with the committed tools (`hsp_summary.py log`, `payloads --id N`, `diff --id N`,
`timeline`, `startdiff --other <E9b jsonl>`; `write_rules.py` for the placement and timing of the writes) and record the findings
here, or a later pass may; until then **H91–H101 keep their status (HYPOTHESIS; the test was run, its outcome is not recorded),
nothing is promoted, nothing is REJECTED, nothing is cleared, and the VERIFIED count stays 61.**

What is already established without decoding:
- The session changed show data through SmartSoft and the console restored it exactly (the SD comparison above): E12's edits are
  reversible in SmartSoft, which the runbook required.
- SmartSoft's writes kept the framing and the stale-word rule of E9 / E9b (0 framing errors, 98 253 / 98 253), and the console's
  download after the edits listed 4 records more than the downloads before and after (161 against 157): what they are is for the
  analysis.

## Anomalies and notes

1. The raw USBPcap file was written into `C:\Windows\System32` (the elevated PowerShell's working folder); copied out with its
   hash checked. The guide should name an absolute output path next time.
2. Step F3 renamed **page 1** instead of page 12 (owner statement); corrected in I1 by renaming page 12 to T and back twice and
   clearing page 1's name. Page 1's name was empty in SHOW12, so the Page writes of this session concern pages 1 and 12 and the
   end state equals the start state (SHOW13 = SHOW12).
3. Step D5's Direct cue (14.1) was inserted without a label; the owner found how to label a Direct cue only in I2 and there set
   the label, cleared it and deleted the step (owner statement). The guide's I2 prompt quoted the owner's D5 answer where it
   expected a cue number (the script interpolated the typed answer).
4. Step D2's point cue was inserted after cue 12.0 as 12.1 ("I clicked on 13.0, insert it and the 12.1 was added"), not between
   the last two original cues as the guide said; I2 moved it to 14.2 before deleting it.
5. Step E3 took 8.5 min (16:03:43 → 16:12:07): the owner asked where the run mode is changed; the answer (the bare word Loop in
   the header of the step list, a click opens the popup) is in the owner statements and in `smartsoft-analysis.md` S11.16 row 7.
6. A3: no run-mode popup for the stack ("I see only PLAY and PAUSE buttons/bumps there"); the Direct cue's "Cue number" field was
   disabled in the insert dialog; the patch tab shows channels above a 512-cell matrix with patched cells green, no level control
   seen (H100's question).
7. The console's last frame (16:32:14.523) precedes the log's "smartsoft close #3 start" stamp (16:32:15.357) by 0.8 s: the owner
   confirmed the close after SmartSoft had already stopped polling.

## Safety

Passive capture: SmartSoft drove every write, our software was not running on the console's interface and sent nothing. The
show was backed up before (SHOW12) and saved after (SHOW13), both kept. **E12 clears nothing** (SC-1's poll and listed fetch
remain the only cleared messages; SC-2's conditional GO is unchanged; the Patch and Independents writes stay class F, "Never",
and the stack, sequence, page and memory writes are in no class).
