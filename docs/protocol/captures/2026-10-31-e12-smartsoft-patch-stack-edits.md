# 2026-10-31 E12: SmartSoft editing the patch and the stack / sequences (USBPcap, passive, guided script)

**Status of this record: complete.** Provenance, completeness and structure as committed in dde1c23; the section "Analysis"
(capture-analyst pass of 2026-10-31, evening, with read-only tools under `tools/protocol-analyzer/`, none of which can open a
device) decodes all 70 PC → console writes, the console's answers, the three downloads and the console's own menu edits, and
tests H91–H101. **Everything first seen here is OBSERVED (one capture, three SmartSoft sessions) or weaker; E12 alone makes
nothing VERIFIED** (where it reproduces an earlier capture, the curator decides: "Input for the knowledge base"). **Nothing in
this capture is cleared for sending; our software was not involved and sent nothing, and our code never sends any of these
messages** (SC-1's poll and listed fetch remain the only cleared messages).

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
  *(Analysis: the ConsoleVersion object reads "3.0.2.9.0.43", body `0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33`, in all three
  sessions: seq 99 f198, seq 138933 f277866, seq 194193 f388386; F48 / F59 again.)*
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
| `tools/protocol-analyzer/hsp_summary.py` (analysis) | `3f324e9f94732235d5cbaa0ea6ce76619db4b1357612de5828db1ccfcb0fd1ec` | | yes (unchanged) | `summary`, `sessions`, `log`, `timeline`, `payloads --id`, `lcd`, `startdiff --other` (E9b JSONL) for the analysis |
| `tools/protocol-analyzer/write_rules.py` | `ba799e37785e3ea12f8da61d42d764c490acf9e47bdf72f6990ccaacaa71a094` | | yes (unchanged since 518789f) | Placement, stale words, counter and timing of the 70 writes (section "Write placement and framing") |
| `tools/protocol-analyzer/e2_asc_crosscheck.py` | `8c49aedb18ff7ee4179ad9baaa2bee3a0c286788734511649a1bb5b28c4bb314` | | yes (unchanged) | Downloads 1 and 3 against `SHOW12.ASC` / `SHOW13.ASC` (run on time slices made with `e12/cut_jsonl.py`) |
| `tools/protocol-analyzer/e8b/blk2548.py` | `b3b341721122ae43370672fad2d7e1b56dd80eac87d5a03b65b064601edfd58c` | | yes (unchanged) | LED blocks 25–48 (H78) |
| `tools/protocol-analyzer/e12/writes.py` | `87889ed96b4aaf8d796be035f1190094dd5b5e434624dbf94a73e2f12a11f91b` | 5 593 B | added with this analysis | Every data write: seq / frames, times, header words, decoded body, the log note before it, the console's lists and objects until the next write |
| `tools/protocol-analyzer/e12/cmp_writes.py` | `eb316aa6ed881b41acdcae663df8d2db7baeae9b4ff3f96a202b6ac5a7968cca` | 4 090 B | added | Each object write against the previous write, the console's last body before and its next body after (48-level bodies expanded to the console's layout), and the lists until the next write |
| `tools/protocol-analyzer/e12/downloads.py` | `ebd8366beaba7138641618e1e428df1556dcf70922eec55626ee0008b81269a2` | 3 914 B | added | The three downloads compared object by object, list hashes and extra records; the show-object bodies outside the downloads |
| `tools/protocol-analyzer/e12/lists.py` | `3b0242c861b38de72e0a4f93b478704fb6aa043d5980ee4359881162b3b1cbc6` | 1 928 B | added | Lists, writes and log notes merged in time order |
| `tools/protocol-analyzer/e12/write_timing.py` | `44aff45166dd1889af55e8b695db21e7f4de4c381ccbae24f38db5cc9f539b6d` | 3 035 B | added | Per-write timing and SmartSoft's idle poll intervals below a threshold |
| `tools/protocol-analyzer/e12/cut_jsonl.py` | `4607d9e64cf03bd9bdba15d33d9181b70d126a670aee8f0ce1fb7abee76ec632` | 1 303 B | added | Writes a time slice of a JSONL (header + frames) to a new file outside `docs/protocol/captures/`, for the cross-check of one download |
| tshark / capinfos 4.0.3 (Wireshark portable) | | | no | The split, the frame census by address and the frame counts and spans |

Normalization command (single line, from the repository root; output written outside the repository and copied):

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py <split> -o <out>.jsonl --device-address 27 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware "3.0.2.9.0.43 (the owner's Diagnostics reading of 2026-10-30, E8 statements; not re-read for E12; the ConsoleVersion object of this capture is for the analyst to compare)" --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off, read by the guided script at 15:39:26.998)" --host-driver "etchsp.sys, service ETCHSP on interface 1 (read by the guided script at 15:39:28.313; version not re-read, 2.00.000 in E2-E3b); interface 0 HID" --notes-log docs/protocol/captures/2026-10-31-e12-actions.txt --notes-utc-offset +01:00 --note 0 "analyst: console-only split (...) of the USBPcapCMD root-hub capture e12.pcap (SHA-256 92C13B05..., 2003901408 bytes, 1789464 frames by a tshark field census, 15:39:43.890770-16:32:26.220864 local time), ... the console was powered off before the session (owner, log 15:36:49); our software was not involved" --note 3162.330094 "analyst: the raw capture's last frame (another device) is at 16:32:26.220864 local time; the console's last frame, frame 397536 of this split, is at 3150.632324 s (16:32:14.523094); the log stamps SmartSoft's third close at 16:32:15.357 (start) and 16:32:19.450 (closed) and USBPcap's stop at 16:32:27.659"
```

(The two notes are quoted in full in the JSONL's first records.)

Analysis commands (added with the analysis; from the repository root; every tool only reads, `-B` keeps `__pycache__` out of
the tree; `J` = the committed JSONL, `<tmp>` = a folder outside the repository):

```
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py J summary        (and the modes sessions, log, lcd, payloads --id 17)
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py J timeline --id 0 --id 2 --id 3 --id 4 --id 5 --id 8 --id 12 --id 39 --id 23 --max-changes 40
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py J startdiff --other docs/protocol/captures/2026-10-31-e9b-smartsoft-writes-repeat.jsonl
py -3.13 -I -B tools/protocol-analyzer/write_rules.py J
py -3.13 -I -B tools/protocol-analyzer/e12/writes.py . J          (likewise cmp_writes.py, downloads.py, lists.py, write_timing.py)
py -3.13 -I -B tools/protocol-analyzer/e8b/blk2548.py . J
py -3.13 -I -B tools/protocol-analyzer/e12/cut_jsonl.py J <tmp>/e12-d1.jsonl -1 280
py -3.13 -I -B tools/protocol-analyzer/e12/cut_jsonl.py J <tmp>/e12-d3.jsonl 3079 4000
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py <tmp>/e12-d1.jsonl smartfade-sd-card-backup/SHOW12.ASC
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py <tmp>/e12-d3.jsonl smartfade-sd-card-backup/SHOW13.ASC
```

(The second cross-check ends with an IndexError in its last part, "Live levels at session start", because session 3 fetched no
FaderInfo; parts (1)–(6) and the ChannelLevels / OutputLevels lines of (7) are complete.)

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

*(This section replaces the placeholder of dde1c23, which said that the decoding was pending, that the assistant's first attempt
had been stopped by its safety system and that H91–H101 kept their status. The decoding below was made on the coordinator's
request with read-only analysis tools only (offline readers of the committed files; nothing that can open a device, encode a
message or talk to a console); the statuses it proposes are for the knowledge-base curator, who applies them. The two statements
of the placeholder that needed no decoding hold: the show data after E12 equal those before it (`SHOW13.ASC` = `SHOW12.ASC`), and
the writes kept the framing and the stale-word rule (0 framing errors, 98 253 / 98 253).)*

**Conventions** (as in the E9b record): `seqN` is the record number in the normalized JSONL (notes take none); `fN` is a frame of
the console-only split, the data-carrying one (submission frame for OUT, completion frame for IN). A write is cited as **Wn**, the
n-th of the 70 data writes in capture order, with header seq / payload seq and the payload frame (for example W9, seq 43918 / 43919,
f87837). `t` is seconds since the split's first frame (15:39:43.890770 local; local time = 15:39:43.891 + t); log stamps are
quoted with their t. `pseq` is a payload's own counter (SmartSoft's from 0x00 in each SmartSoft session; the console's runs on).
Sessions: **S1** seq 6 … 138843 (4.63–2189.83 s, W1–W47), **S2** seq 138845 … 194103 (2203.22–3066.00 s, W48–W68), **S3** seq
194105 … 198768 (3079.52–3150.63 s, W69–W70). Stack steps are named by cue number and **index** (= StepContent `a`, 0-based; it
shifts when a step is inserted or deleted before it). The show's stack (`$SEQUENCE 48`, download 1) has 19 steps, index 0–18 =
cues 1.0–9.0, 10.1–10.6, 11.0, 12.0, 13.0, 14.0; KOTLIK (`$SEQUENCE 0`, page 1, fader 21) has 7. `body[n]` is a byte of the object
body; a step record is `[u16 cue × 10][type][u16 memory index][x][u16 Up][u16 Down][u16 Wait]` (H41; times in tenths, BE). Every
quoted byte comes from the cited seq; readings carry their status (OBSERVED = seen in this capture; INFERRED; HYPOTHESIS; UNKNOWN).

### Inventory of the PC → console writes

**70 data writes** (type-1 header + payload, bulk 0x04, interface 1), all SmartSoft's, all completed with USBD status 0; framing
errors 0; w1 = body length + 4 in 70 of 70; SmartSoft's counter pseq 0–46 (S1), 0–20 (S2), 0–1 (S3), 0 gaps.

| Object (id) | Writes | w1 | Body | First PC → console write | Wn |
|---|---|---|---|---|---|
| System (39) | 6 (3 pairs) | 7 | `09 00 00`, `07 00 00` (F58) | E2 | W1, W2, W48, W49, W69, W70 |
| ConsoleEvent (20), kind 1 (key) | 6 (3 press / release pairs) | 7 | `01 31 01` / `00` (GO), `01 39 01` / `00` (FADERS MEMS), **`01 3A 01` / `00` (a code never seen before)** | E9 | W4 / W5, W25 / W26, W36 / W37 |
| **InsertStep (32)** | 5 | 8 | `[a][b][u16 BE c]` | **E12** | W6, W17, W19, W28, W30 |
| **DeleteStep (34)** | 5 | 8 | `[a][b][00 00]` | **E12** | W8, W57, W60, W61, W65 |
| **MoveStep (35)** | 1 | 7 | `[a][b][c]` | **E12** | W58 |
| **id 42** (no entry in the engine's name table, S11.16 row 1) | 1 | 8 | `[a][b][u16 BE c]` | **E12** | W59 |
| **StepContent (4)** | 11: 6 × 13 bytes, 5 × 100 bytes | 17 / 104 | a memory step: `[index]` + the 12-byte record; a "Direct cue": `[index]` + record + `[30][48 levels][03 06][36 label bytes]` | **E12** | W18, W20–W24; W7, W29, W62–W64 |
| **Sequence (2)** | 13 | 148 | the whole object, 144 bytes (8 steps) | **E12** | W31–W35, W38–W43, W55, W56 |
| Memory (0) | 5 | 94 | 90 bytes, F143's layout `[page − 1][slot − 1] 01 30` + 48 levels + `03 06` + 36 label bytes | E9b (once) | W27, W44–W46, W66 |
| **Page (8)** | 6 | 43 | `[page − 1][03 06][36 label bytes]` (39) | **E12** | W47, W50–W54 |
| **Independents (12)** | 7 | 89 | the whole object, 85 bytes, `[02]` + 2 × `[a][b][c][d][03 06][36]` | **E12** | W3, W11–W16 |
| **Patch (5)** | 4 | 518 | the whole object, `[02 00]` + 512 entries (514) | **E12** | W9, W10, W67, W68 |

**Not seen:** InsertPointQStep (33); PatchDevice / UnpatchDevice / RepatchDevice (29–31) in either direction (no write, no listed
record); SequenceMain (3) or ChannelText (9) writes; ConsoleEvent kinds 0 and 2; any System write other than the pair; anything on
interface 0. Every payload is `[pseq][u16 LE len = w1 − 4][id][body]`. **A data payload carries no qualifier fields** (the
header's w2..w5 are stale, F52; W31's header `01 00 94 00 02 00 00 00 00 00 06 00` repeats the fetch (2, 0, 0, 6) before it): an
object's identity is in its body (Memory body[0..1], Page body[0], StepContent body[0], Sequence body[0..1]; Patch and
Independents have none), a step command's in its `a`.

### Action log matched to the writes

"Offset" = the step's first write minus its log stamp. Every write of the capture is in this table (70). "Nothing listed" means no
DirtyObjects list at all came between the write and the next write (at most 15 s; `cmp_writes.py`).

| Log stamp (t) | Step | Writes (pseq; header / payload seq; payload frame; t) | Offset | The console's answer (seq) |
|---|---|---|---|---|
| 4.503 / 16.867 / 76.883 | A1 `smartsoft start #1`, online, 60 s | W1 pseq 0 `09 00 00` (seq 91 / 92, f183, 5.934); W2 pseq 1 `07 00 00` (seq 174 / 175, f349, 7.210), after reply headers #40 and #79 | +1.43 s | power-on list [22, 21] (seq 10, f20, pseq 0x00); [37] (seq 95) → ConsoleVersion (seq 99); **download 1**: 157-record list seq 178 (f356, 7.227), objects to 14.48 s |
| 84.502–229.971 | A2: Patch, Stack/Seq Edit, Page, Mem Edit tabs open 30 s each | **none** | | none (no list from 14.6 s to 277.7 s) |
| (229.971–557.470; answers at 320.047, 394.577, 557.470) | A3 "looking only" | **W3** pseq 2 Independents, 85 bytes = the console's body byte for byte (seq 16307 / 16308, f32615, 259.643) | unlogged | nothing listed |
| | (console, no write) | | | physical fader 1 0x15 → 0x7D → 0x00 at 277.7–279.4 s: FaderInfo record 1, ChannelLevels[1], DMX 1, LED block 1, LCD cell (seq 17457 … 17599) |
| | (console, no write) | | | STACK on: LED block 60 `FF FF 00 FF FF 00`, LCD "Live:14.0 Next: 1.0" (seq 21134, 21141, 335.3 s); "Jump to step:> 1" (seq 21352); STACK off (seq 21363, 338.8 s) |
| | A3, SmartSoft | **W4 / W5** pseq 3 / 4 key **0x31 (GO)** press / release (seq 22438 / 22447, f44877 / f44895, 355.832 / 355.926) | unlogged | [3, 22] (seq 22442, +18 ms): SequenceMain unchanged (seq 22446), LED block 50 blue `00 00 FF 00 00 FF` (seq 22452); PlaybackInfo listed and fetched 63 times, 356.04–360.95 s: record 5 bytes 2–3 `F5 0A` → `FF 00`, bytes 0–1 `00 00` throughout (seq 22459 … 22893); [17, 22] at 360.896 (seq 22889), block 50 back (seq 22897). **No level object listed**: GO with STACK unlit, as in E9b (F134) |
| | A3, SmartSoft | **W6** pseq 5 InsertStep `30 13 00 96` (seq 30120 / 30121, f60241, 475.154); **W7** pseq 6 StepContent (4, 19), 100 bytes, type 2, levels 0, label empty (seq 30133 / 30134, f60267, 475.313); **W8** pseq 7 DeleteStep `30 13 00 00` (seq 30465 / 30466, f60931, 480.480) | unlogged | after W6: [3, 4:19] (seq 30124, +19 ms), SequenceMain 0x14 (seq 30128), StepContent 19 `13 00 96 00 00 00 00 00 32 00 32 00 00` (seq 30132); after W8: [3, 4:19] (seq 30469), SequenceMain 0x13 (seq 30473), StepContent 19 `13` + 12 × `00` (seq 30477). **A Direct cue was inserted as 15.0 and deleted 5.3 s later** (the A3 answer at 557.470 describes the dialog) |
| 575.472–653.714 | B1 fader 5 up, console REC MEM + bump 9, fader 5 down | **none** | | LCD fader 5 cell (577.2–577.9 s); "Record Memory Pg:1" (seq 38631, 608.9 s); [0:0/8/0, 22, 21] (seq 38715) → **Memory (0, 0, 8) `00 08 01 60` + channel 5 = 0x85 + zeros + empty label** (seq 38719, f77438, 610.22); fader 5 back 627.0–628.0 s. F147 again (console keys) |
| 684.420 | C1 channel 47 → DMX 100 | **W9** pseq 8 Patch: entry DMX 100 = 0x2F (47), every other entry = download 1 (seq 43918 / 43919, f87837, 692.156) | +7.74 s | nothing listed; owner: nothing on the LCD (757.790) |
| 764.974 | C2 IND 1 → DMX 101 | **W10** pseq 9 Patch: DMX 100 = 47 kept, **DMX 101 = 0x31 (49)** (seq 48804 / 48805, f97609, 769.513) | +4.54 s | nothing listed; **no Independents write** (the next write is W11, 84 s later) |
| 851.728 | C3 IND 1 button mode changed | **W11 / W12 / W13** pseq 10–12 Independents: record 1 byte 2 (body[3]) 00 → 01, → 00, → 01 (seq 54128, 54206, 54254; f108257, f108413, f108509; 853.824, 855.040, 855.782) | +2.10 s | nothing listed |
| 872.087 | C3 button mode restored | **W14** pseq 13: byte 2 → 00 (seq 55372 / 55373, f110745, 873.499) | +1.41 s | nothing listed |
| 899.559 | C3 GM changed | **W15** pseq 14: record 1 byte 3 (body[4]) 01 → 00 (seq 57224 / 57225, f114449, 902.773) | +3.21 s | nothing listed |
| 918.660 | C3 GM restored | **W16** pseq 15: byte 3 → 01 (seq 58318 / 58319, f116637, 920.094) | +1.43 s | nothing listed |
| 956.501 (inserted 973.172; answer "15.0" 1000.077) | D1 insert after the last cue, Memory 01/09 | **W17** pseq 16 InsertStep `30 13 00 96` (seq 61490 / 61491, f122981, 970.206); **W18** pseq 17 StepContent (4, 19) `13 00 96 01 00 08 00 00 32 00 32 00 00` (seq 61503 / 61504, f123007, 970.367) | +13.71 s | after W17: [3, 4:19] (seq 61494, +19 ms), SequenceMain 0x14 (seq 61498), StepContent 19 `13 00 96 00 00 00 00 00 32 00 32 00 00` (seq 61502); after W18 nothing listed |
| 1029.259 (inserted 1050.845; answer 1096.157: "I clicked on 13.0 … 12.1 was added") | D2 point cue, Memory 01/09 | **W19** pseq 18 InsertStep `30 11 00 79` (seq 65955 / 65956, f131911, 1040.716); **W20** pseq 19 StepContent (4, 17) `11 00 79 01 00 08 00 00 32 00 32 00 00` (seq 65968 / 65969, f131937, 1040.876) | +11.46 s | after W19: [3, 4:17, 4:18, 4:19, 4:20] (seq 65959), SequenceMain 0x15 (seq 65963), StepContent 17 `11 00 79 00 00 00 00 00 32 00 32 00 00` (seq 65967); then W20; then 18 = 13.0, 19 = 14.0, 20 = 15.0 (D1's step) (seq 65973, 65977, 65981) |
| (unlogged, 2.8 s before "d3 up time") | — | **W21** pseq 20 StepContent (4, 20) = 15.0 unchanged (seq 70294 / 70295, f140589, 1109.309) | | nothing listed |
| 1112.101 | D3 Up | **W22** pseq 21 StepContent (4, 20): Up `00 32` → `00 1E` (5.0 → 3.0 s) (seq 70844 / 70845, f141689, 1117.993) | +5.89 s | nothing listed |
| 1134.157 | D3 Down | **W23** pseq 22: Down → `00 28` (4.0 s) (seq 72038 / 72039, f144077, 1136.858) | +2.70 s | nothing listed |
| 1160.927 (answer "Wait" 1193.459) | D3 third time | **W24** pseq 23: third time `00 00` → `00 14` (2.0 s) (seq 73728 / 73729, f147457, 1163.583) | +2.66 s | nothing listed |
| 1220.920 | D4 step label "T" | **W25 / W26** pseq 24 / 25 key **0x39 (FADERS MEMS)** press / release (seq 77586 / 77591, f155173 / f155183, 1224.541 / 1224.588); **W27** pseq 26 **Memory (0, 0, 8)**: channel 5 = 0x85 (SmartSoft's copy of B1's recording), **label "T"** (seq 77708 / 77709, f155417, 1226.354) | +3.62 s | after W25: [23, 22] (seq 77590, +23 ms): **ConsoleStatus `01 00 00 00 00 01 00`** (seq 77596, memory mode), LED block 58 `FF 00 00 FF 00 00`, block 9 `46 00 00 46 00 00` (seq 77600); [21, 22] (seq 77603); after W27 nothing listed. **No StepContent write** |
| 1249.032 (answer 1318.875: no label possible; "14.1") | D5 Direct cue | **W28** pseq 27 InsertStep `30 14 00 8D` (seq 79654 / 79655, f159309, 1257.107); **W29** pseq 28 StepContent (4, 20), 100 bytes: `14 00 8D 02 00 00 00 00 32 00 32 00 00` + `30` + 48 × `00` + `03 06` + 36 × `00` (seq 79667 / 79668, f159335, 1257.265) | +8.07 s | after W28: [3, 4:20, 4:21] (seq 79658), SequenceMain 0x16 (seq 79662), StepContent 20 `14 00 8D 00 00 00 00 00 32 00 32 00 00` (seq 79666); then W29; then 21 = 15.0 with D3's times (seq 79672) |
| 1359.878 (inserted 1385.001; answer "7" 1409.995) | E1 KOTLIK, insert, Memory 01/09 | **W30** pseq 29 InsertStep `00 06 00 50` (seq 87583 / 87584, f175167, 1382.062); **W31** pseq 30 Sequence, 144 bytes: 8 steps, record 6 = `00 46 01 00 08 00 00 32 00 32 00 00` (7.0, memory 9 of page 1, 5.0 / 5.0), record 7 = the old 7th step as 8.0 (seq 87592 / 87593, f175185, 1382.171) | +22.18 s | after W30: **[2:0/0/6]** (seq 87587, +19 ms), **Sequence (2, 0, 0, 6), 144 bytes** (seq 87591, f175182): record 6 `00 46 01 01 0E 00 00 32 00 32 00 00`, record 7 `00 50 01 01 0E 00 00 0A 00 0A 00 00`; after W31 nothing listed |
| 1424.469 | E2 that step's Up | **W32** pseq 31 Sequence: record 6 Up → `00 14` (2.0 s), body[127] (seq 90450 / 90451, f180901, 1427.346) | +2.88 s | nothing listed |
| (unlogged, 1439.5–1943.2: the owner's search for the run mode, statements) | — | **W33–W35** pseq 32–34 Sequence unchanged (seq 92124, 92160, 92850; 1453.829, 1454.384, 1465.253); **W36 / W37** pseq 35 / 36 key **0x3A** press / release (seq 98766 / 98772, f197533 / f197545, 1558.580 / 1558.660); **W38–W40** pseq 37–39 Sequence unchanged (seq 121628, 121764, 121822; 1918.444, 1920.578, 1921.477) | | **nothing listed** (no list at all from 1382.08 s to the end of S1) |
| 1943.172 | E3 run mode Loop → One Shot | **W41** pseq 40 Sequence: **body[3] 00 → 01** (seq 123368 / 123369, f246737, 1945.646) | +2.47 s | nothing listed |
| 1982.757 (answer 2056.204: "allows only 6 characters … KOTLIX") | E4 label | **W42** pseq 41 Sequence: label KOTLIK → KOTLIX (body[23] 0x4B → 0x58) (seq 126250 / 126251, f252501, 1990.998); **W43** pseq 42 unchanged (seq 130230, f260461, 2053.734, unlogged) | +8.24 s | nothing listed; owner: nothing on the LCD |
| 2079.515 | F1 Mem Edit, channel 5 → 100 % | **W44** pseq 43 Memory (0, 0, 8): channel 5 0x85 → **0xFF**, label "T" kept (seq 132380 / 132381, f264761, 2087.612) | +8.10 s | nothing listed |
| 2122.993 | F2 memory 9 name "T" | **W45** pseq 44 Memory: label empty (seq 134898, f269797, 2127.447); **W46** pseq 45 Memory: label "T" (seq 135014, f270029, 2129.263) | +4.45 s | nothing listed (the memory had been named "T" since D4) |
| 2166.916 | F3 "page 12" name "T" | **W47** pseq 46 **Page: body[0] = 0x00 = page 1**, label "T" (seq 137544 / 137545, f275089, 2169.305) | +2.39 s | nothing listed; the owner's slip (statements) |
| 2184.992 / 2191.414 | close #1 | none | | S1's last transfer 2189.828 (seq 138843) |
| 2203.119 / 2217.669 / 2247.686 (answer "yes" 2276.955) | G1 start #2 | W48 pseq 0 `09 00 00` (seq 138925 / 138926, f277851, 2204.502); W49 pseq 1 `07 00 00` (seq 139008 / 139009, f278017, 2205.779), #40 / #79 | +1.38 s | [37] (seq 138929) → ConsoleVersion (seq 138933); **download 2: 161-record list seq 139012 (f278024, 2205.797)** |
| 2297.992 | H Patch by dimmer, DMX 102 → channel 47, Blind | **none** | | section "Step H"; Patch listed 53 times in 22 lists, 2310.5–2365.1 s |
| 2375.220 | H Unpatch dimmer 102 | **none** | | [5, 21] (seq 151663, 2398.505): Patch DMX 102 = 0 (seq 151667, f303334) |
| 2412.935 (answer 2524.503: "looks like it follow it") | H Step Timing, D1's step Up | **none** | | six lists [3, 3, 4:21, 21, …] 2461.4–2464.5 s: StepContent 21 Up 0x1E → 0x32 (3.0 → 5.0 s) |
| 2552.907 | I1 page names (statements: page 12 → T, → empty, page 1 → empty, page 12 → T, → empty) | **W50–W54** pseq 2–6 Page: page 12 "T", page 12 "", page 1 "", page 12 "T", page 12 "" (seq 161751, 161865, 162005, 162195, 162279; f323503, f323731, f324011, f324391, f324559; 2555.274–2563.567) | +2.37 s | nothing listed; exactly the owner's account |
| 2628.017 | I1 label back | **W55** pseq 7 Sequence: label KOTLIK (seq 166559, f333119, 2631.298) | +3.28 s | nothing listed |
| 2648.492 | I1 run mode Loop | **W56** pseq 8 Sequence: body[3] → 00 (seq 167887, f335775, 2652.194) | +3.70 s | nothing listed |
| 2668.275 | I1 delete E1's step | **W57** pseq 9 DeleteStep `00 06 00 00` (seq 169119 / 169120, f338239, 2671.590) | +3.31 s | **[2:0/0/6]** (seq 169123, +15 ms); Sequence (2, 0, 0, 6), 132 bytes (seq 169127, f338254) = **download 1's Sequence byte for byte** |
| 2773.585 (answer "14.2" 2794.440) | I2 move the point cue | **W58** pseq 10 MoveStep `30 11 14` (seq 175910 / 175911, f351821, 2778.865); **W59** pseq 11 **id 42 `30 14 00 8E`** (seq 175915 / 175916, f351831, 2778.912) | +5.28 s | after W58: [3, 4:17, 4:18, 4:19, 4:20, 4:21] (seq 175914, +16 ms, before W59); fetched after W59: 17 = 13.0, 18 = 14.0, **19 = 14.1 "State", 148 bytes**, 20 = **14.2**, memory 9, 21 = 15.0 (seq 175924 … 175940); [3, 4:20, 4:21] (seq 175943) |
| 2799.207 | I2 delete the point cue | **W60** pseq 12 DeleteStep `30 14 00 00` (seq 177420, f354841, 2802.522) | +3.31 s | [3, 4:20] (seq 177424): SequenceMain 0x15, StepContent 20 = 15.0 (seq 177432) |
| 2819.368 | I2 delete D1's step | **W61** pseq 13 DeleteStep `30 14 00 00` (seq 178655, f357311, 2821.927) | +2.56 s | [3, 4:20] (seq 178659): SequenceMain 0x14, StepContent 20 = `14` + 12 × `00` (seq 178667) |
| (unlogged, before "i2 delete direct cue"; statements) | I2 label of the Direct cue set and cleared | **W62** pseq 14 StepContent (4, 19), 100 bytes, unchanged (seq 180508, f361017, 2851.169); **W63** pseq 15 label **"Direct cue custom"** (seq 182456, f364913, 2881.927); **W64** pseq 16 label "cleared": code units 0, 6 and 12 zeroed, the rest left (seq 182926, f365853, 2889.360) | | nothing listed |
| 2896.203 | I2 delete the Direct cue | **W65** pseq 17 DeleteStep `30 13 00 00` (seq 183448, f366897, 2897.514) | +1.31 s | [3, 4:19] (seq 183452): SequenceMain 0x13, StepContent 19 = `13` + 12 × `00` (seq 183460) |
| 2948.778 | I3 memory 9 name cleared | **W66** pseq 18 Memory: channel 5 0xFF, label empty (seq 186817, f373635, 2950.811) | +2.03 s | nothing listed |
| 2973.797 (answer "y" 2993.223) | I3 console REC MEM + bump 9, nothing up | **none** | | "Record Memory Pg:1" (seq 188425, 2976.2 s); [0:0/8/0, 22] (seq 188540) → **Memory (0, 0, 8) `00 08 00 60` + 96 zeros + empty label** (seq 188544, f377088, 2978.06); LED block 9 `0A 00 00 0A 00 00` (seq 188548). F146 again (console keys) |
| 3008.158 | I4 DMX 101 → bin | **W67** pseq 19 Patch: DMX 101 → 0 (seq 190618, f381237, 3010.845) | +2.69 s | nothing listed |
| 3030.233 (answer "I think so" 3057.018) | I4 DMX 100 → bin | **W68** pseq 20 Patch: DMX 100 → 0, the whole body = download 1's (seq 191988, f383977, 3032.529) | +2.30 s | nothing listed |
| 3061.092 / 3067.600 | close #2 | none | | S2's last transfer 3065.997 (seq 194103) |
| 3079.425 / 3093.245 / 3123.256 (answer "y" 3146.212) | J1 start #3 | W69 pseq 0 `09 00 00` (seq 194185, f388371, 3080.798); W70 pseq 1 `07 00 00` (seq 194268, f388537, 3082.090), #40 / #79 | +1.37 s | **download 3: 157-record list seq 194272 (f388544, 3082.111)**, byte-identical to download 1's |
| 3151.466 / 3155.559 | close #3 | none | | last transfer 3150.632 (seq 198768, f397536) |

Totals: S1 47 writes, S2 21, S3 2 = 70. The console's state after each logged step follows from the listed objects and the
downloads below.

### The step commands and what the console did with them (H96, H97, H98)

| Wn | Step | Body | Reading (OBSERVED once unless marked) | The console's list on the next poll | The console's new step before phase 2 |
|---|---|---|---|---|---|
| W6 | A3 (unlogged) | InsertStep `30 13 00 96` | a = 48 (stack), b = 19 (the index the new step takes: after the last, 14.0 at 18), c = 0x0096 = 15.0 | [3, 4:19] | `13 00 96 00 00 00 00 00 32 00 32 00 00`: cue 15.0, **type 0, memory index 0, Up / Down 5.0** |
| W17 | D1 | InsertStep `30 13 00 96` | the same; the owner logged 15.0 | [3, 4:19] | the same |
| W19 | D2 | InsertStep `30 11 00 79` | b = 17 (13.0's index before the insert), c = 0x0079 = **12.1**; the owner logged 12.1 | [3, 4:17, 4:18, 4:19, 4:20] | `11 00 79 00 00 00 00 00 32 00 32 00 00` |
| W28 | D5 | InsertStep `30 14 00 8D` | b = 20 (D1's 15.0 at 20 before the insert), c = 0x008D = **14.1**; the owner logged 14.1 | [3, 4:20, 4:21] | `14 00 8D 00 00 00 00 00 32 00 32 00 00` |
| W30 | E1 | InsertStep `00 06 00 50` | a = 0 (KOTLIK), b = 6 (the old 7th step's index), **c = 0x0050 = 8.0 although the new step became 7.0** (meaning of c for a sequence UNKNOWN) | [2:0/0/6] | record 6 `00 46 01 01 0E 00 00 32 00 32 00 00`: 7.0, **a copy of the displaced step's memory (0x010E = page 12, memory 7)**, Up / Down 5.0; the old step renumbered 8.0 |
| W8 | A3 (unlogged) | DeleteStep `30 13 00 00` | b = 19 (the Direct cue of W6 / W7), c = 0 | [3, 4:19] | — (StepContent 19 then `13` + 12 × `00`) |
| W57 | I1 | DeleteStep `00 06 00 00` | a = 0, b = 6 (E1's step) | [2:0/0/6] | — (Sequence back to download 1's 132 bytes) |
| W60 | I2 | DeleteStep `30 14 00 00` | b = 20 (the moved point cue, 14.2) | [3, 4:20] | — (20 now = 15.0) |
| W61 | I2 | DeleteStep `30 14 00 00` | b = 20 (D1's 15.0) | [3, 4:20] | — (20 = `14` + 12 × `00`, the old last index) |
| W65 | I2 | DeleteStep `30 13 00 00` | b = 19 (the Direct cue 14.1) | [3, 4:19] | — (19 = `13` + 12 × `00`) |
| W58 | I2 | MoveStep `30 11 14` | b = 17 (12.1, from), c = 20 (to: the index the step holds after the move, between 14.1 and 15.0) | [3, 4:17 … 4:21] | — |
| W59 | I2, 47 ms after W58 | id 42 `30 14 00 8E` | index 20 gets **cue 14.2** (the owner logged 14.2): id 42 renumbers a step (INFERRED from this one sample) | [3, 4:20, 4:21] (after the move's fetches) | — |

- **H96:** `a` = 48 in all 10 stack commands and 0 in both KOTLIK commands. The two formulas of S11.16 row 3 (page × 4 + slot and
  a·12 + b) are not separated by these two values, as the hypothesis says.
- **H97:** InsertStep, DeleteStep and id 42 are 4 bytes `[a][b][u16 BE c]`, MoveStep 3 bytes `[a][b][c]`; no id 33: the point
  cue 12.1 came from InsertStep with c = 12.1. InsertStep: b = the index the new step takes, c = its cue number × 10 (stack, 4 of
  4; for KOTLIK c = 8.0 while the step became 7.0). DeleteStep: b = the index, c = 0. MoveStep: b = from, c = to. id 42: b = the
  index, c = the new cue number × 10. The cue numbers were chosen by SmartSoft (the dialog's "Cue number" field was disabled,
  owner A3): a whole number after the last cue (15.0), a point number after the preceding whole cue otherwise (12.1, 14.1, 14.2).
  The owner logged no message box.
- **Where a new step lands (GUI, INFERRED):** D2 put 12.1 before the clicked 13.0 (owner), D5 put 14.1 before D1's 15.0, E1 put the
  new KOTLIK step before the old 7th step; D1's step went after the last cue. That fits "Insert step places the new step before the
  right-clicked step" with D1 clicked below the list; the owner's clicks decide (question 6).
- **H98, two phases (5 of 5):** the command, then the content: StepContent (4, b) for the stack (W7, W18, W20, W29), the whole
  Sequence for KOTLIK (W31). **The step type is not in the command** (it has no type field): it is in phase 2, record byte 2 =
  CUEPARAMS 1 (memory: W18, W20, W31) or 2 (Direct cue: W7, W29). Between the phases the console holds a default step (column
  above), lists it, and SmartSoft fetches it. **Phase 2 was SmartSoft's next OUT transfer after the fetched object of the new step**
  in 5 of 5 (before any other listed record was fetched: W20 before StepContent 18–20, W29 before 21), 109–161 ms after the command
  (W6 → W7 159.6 ms, W17 → W18 161.2, W19 → W20 159.8, W28 → W29 158.7, W30 → W31 109.5); the header of each phase-2 write carries the
  stale words of that fetch (W7 `01 00 68 00 04 00 13 00 …`, W31 `… 02 00 00 00 00 00 06 00`). That the arrival of the object
  triggers phase 2 is INFERRED from this order (H105 proposed); a timer or the dialog's end is not excluded by one session.
- **Consequence (INFERRED):** a lost phase 2 would leave the console's default step (stack: type 0, memory index 0; sequence: a
  copy of the displaced step's memory, 5.0 / 5.0); a lost id 42 would leave a moved step out of cue order. Neither is reversible
  by the protocol except by the inverse command with exact knowledge of the console's state.

### Whole-object writes against the console's own bodies

`cmp_writes.py` compares each write with the previous write of the object, with the console's last body before it and with the
console's next body after it (download 2 or 3, or a listed fetch); SmartSoft's 48-level bodies are compared in the console's
96-level layout. **No list followed any of the 46 object writes** (46 of 46, until the next write or 15 s).

- **Patch (5), 4 writes, 514 bytes in the console's layout.** One write per drop (C1, C2, I4 twice), each changing exactly the
  dragged entry: W9 DMX 100 (body[101]) 0x00 → 0x2F (47); W10 adds DMX 101 (body[102]) = **0x31 = 49, SmartSoft's value for
  IND 1** (the console's own patch screen later named this entry "C: i1", seq 146853); W67 DMX 101 → 0; W68 DMX 100 → 0, the whole
  body = download 1's. **Download 2's Patch (seq 139032) is W10 byte for byte; download 3's (seq 194292) is download 1's.** H91,
  H92 (cell drop), H34.
- **Independents (12), 7 writes, 85 bytes in the console's layout.** W3 = the console's body unchanged (A3). C3: **record 1 byte 2
  (`$INDEPENDENT 1 a b c d` field c) carries the button mode** (W11–W14: 00 → 01 → 00 → 01, restored 00) and **record 1 byte 3
  (field d) carries the GM setting** (W15 01 → 00, W16 back); which value means toggle or bump, GM on or off, is UNKNOWN (the
  default is `00 FF 00 01`); the console's reaction is unobserved (nothing listed, restored before download 2, which equals
  download 1). H93, H44.
- **StepContent (4), 11 writes.** Memory step, 13 bytes in the console's layout: W18, W20 (phase 2), W21 (unchanged), W22–W24
  (one field each: Up, Down, the third time). Download 2 holds W20 as StepContent 17 byte for byte and W24's content as StepContent
  21 (index re-numbered by D5's insert). **Direct cue, 100 bytes: `[index][record with type 2][30][48 levels][03 06][36 label
  bytes]`** (W7, W29, W62–W64), against **the console's 148 bytes `[index][record][60][96 levels][03 06][36]`** (download 2
  StepContent 20, seq 139240, = W29 in the console's layout; seq 175932 after the move) — the same 0x30 / 0x60 pattern as the
  Memory object (F143). The console's own LCD calls this step **"State"** ("XF Step:>14.1 State", seq 155246). The Direct cue's
  label is serialized (W63 "Direct cue custom", 17 characters over the three 6-character lines); SmartSoft's "clear" (W64) zeroed
  only the first code unit of each line (units 0, 6, 12), leaving "irect", "cue c", "stom" behind them: the per-line form of F138's
  residue. What the console stored of W63 / W64 is unobserved (the step was deleted before any download, nothing listed).
- **Sequence (2), 13 writes, 144 bytes (8 steps).** W31 (phase 2: record 6's memory 0x010E → 0x0008), W32 (record 6 Up 5.0 → 2.0),
  W41 (**body[3] 0 → 1: One Shot**), W42 (label KOTLIX), W55 (label KOTLIK), W56 (**body[3] → 0: Loop**); W33–W35, W38–W40 and W43
  unchanged. **Download 2's Sequence (seq 139152) is W42 / W43 byte for byte** (8 steps, body[3] = 1, KOTLIX, record 6 = memory 9,
  Up 2.0, Down 5.0). The run mode is body[3] on SmartSoft's side and stored by the console; whether the console *plays* One Shot is
  untested. H95, H42.
- **Memory (0), 5 writes, 90 bytes (F143's layout again):** W27 (label "T", levels = SmartSoft's copy of the console's recording,
  channel 5 = 0x85), W44 (channel 5 → 0xFF, label kept), W45 (label empty), W46 (label "T"), W66 (label empty). Every name edit
  carried the levels and every level edit the name. **Download 2's Memory (0, 0, 8) (seq 139072, 138 bytes, `00 08 01 60`) equals
  W46 in the console's layout** (channel 5 = 0xFF, label "T", channels 49–96 zero, as they were before). H99, F143.
- **Page (8), 6 writes, 39 bytes in the console's layout:** W47 page 1 "T" (the slip), W50 / W53 page 12 "T", W51 / W54 page 12
  empty, W52 page 1 empty. Download 2's Page 0 (seq 139036) = W47; download 3's Pages 0 and 11 = download 1's. H99.
- **Unchanged rewrites:** 10 of the 46 object writes repeat the object unchanged (W3, W21, W33–W35, W38–W40, W43, W62), each
  outside a logged change; INFERRED: SmartSoft writes an object when a cell or panel is opened or left without a change (cf. E9's
  extra ChannelText write, F138).

### The three downloads (157 / 161 / 157 records)

`downloads.py`: the 157-record lists of downloads 1 and 3 are **byte-identical to each other and to E9b's three** (body SHA-256
prefix `8208d7647d400d48`; nine such lists through E9b, these are the tenth and eleventh). Download 2's list (161 records, `9c821df060fea7e0`) is
the same list with **four records inserted in list order: Memory (0, 0, 8) at position 14 (after memory 8) and StepContent (4, 19),
(4, 20), (4, 21) at positions 55–57 (after step 18)**: memory 9, recorded in B1, and the three stack steps the session had added
and not yet deleted at G1 (the point cue 12.1, the Direct cue 14.1 and D1's 15.0; the stack had 22 steps). KOTLIK's eighth step is
inside the one Sequence record.

| Object | Download 1 (S1) | Download 2 (S2, after the edits) | Download 3 (S3, after the restores) |
|---|---|---|---|
| Patch (5) | seq 198, f396: DMX 100–102 = 0 | seq 139032, f278064: DMX 100 = 47, DMX 101 = 49 (= W10) | seq 194292, f388584: = download 1 |
| Page (8, 0) | seq 202: empty | seq 139036: "T" (= W47) | = download 1 |
| Page (8, 11) | seq 282: empty | = download 1 (F3 had renamed page 1) | = download 1 |
| Memory (0, 0, 8) | not listed (never recorded) | seq 139072: `00 08 01 60`, channel 5 = 0xFF, label "T" (= W46) | **not listed** (emptied in I3) |
| Sequence (2) | seq 314, f628: 132 bytes, 7 steps, Loop, KOTLIK | seq 139152, f278304: 144 bytes, 8 steps, One Shot, KOTLIX (= W42 / W43) | seq 194408: = download 1 |
| SequenceMain (3) | seq 318: 0x13 (19 steps) | seq 139156: 0x16 (22) | = download 1 |
| StepContent 17, 18 | 13.0, 14.0 | 12.1 memory 9 5.0 / 5.0 (= W20); 13.0 | = download 1 |
| StepContent 19, 20, 21 | not listed | 14.0; **14.1 "State", 148 bytes** (type 2, `60` + 96 zero levels, empty label; = W29); 15.0 memory 9, Up 3.0, Down 4.0, third time 2.0 | not listed |
| Independents (12) | `00 FF 00 01` × 2 | = download 1 (C3 restored in place) | = download 1 |
| ChannelText 6 (channel 7) | `06 03 06 00 00 00 61 00 6A` + zeros (seq 422) | = download 1 | = download 1 |
| ConsoleStatus (23) | `00 00 00 00 00 01 00` (channel mode) | `01 …` (memory mode, since D4) | `01 …` |
| LedStatus (22) | channel-mode map | memory-mode map (block 58 lit, block 9 recorded) | memory-mode map (block 9 empty) |

Download 2 equals download 1 in 149 of the 157 shared objects, download 3 in **155 of 157**: the two that differ are ConsoleStatus
and LedStatus, the console's live mode (memory mode since W25, never switched back; not show data, not in the SD file). **Every
show object is restored**, which the SD comparison (`SHOW13.ASC` = `SHOW12.ASC`) confirms from the other side. ChannelText 6
still carries E9's residue (F144) in all three downloads, after another power cycle; the SD files show it as an empty label.

### Step H: the console's own menu edits (H101)

No PC write from W49 (2205.779) to W50 (2555.274); SmartSoft only polled and fetched what the console listed (the owner: "looks like
it follow it"). The console's LCD (`lcd`) shows the menus as the manual names them (F148, F149).

| Console action (LCD, seq) | What the console listed | Bodies |
|---|---|---|
| Menu → Patch → "Patch by Dimmer", "Patch mode:>Blind" (seq 145414, 145495; 2304.0–2305.3 s) | [21, 22] at the menu screens | — |
| D/C screen opens, "D:> 1 C: 1" (seq 145834, 2310.6) | [5, 21] (seq 145826, 2310.515) | Patch unchanged (seq 145830) |
| The wheel scrolls D 1 → 110 → 102 (2320.4–2327.2): "D:> 97 C: 24" (seq 146839), "D:>101 C: i1" (seq 146853), "D:>102 C:" (seq 147017) | [21] only | — (choosing a dimmer changes nothing) |
| The wheel scrolls C to 47 (2343.8–2348.8): "C:>47" (seq 148466); the screen is left 2349.8–2366.2 | Patch (5) **52 times in 21 lists**, 2343.7–2365.1 s (with the one at 2310.5: 53 in 22), up to 7 × id 5 in one list ([5, 21, 5, 5, 5, 5, 5, 5], seq 148220); no de-duplication (F57) | **DMX 102 took 39 channel values in 4.9 s** (1, 3, 4, 5, 8, 9, 12 … 46, 47; seq 148137 f296274 … seq 148462 f296924): in Blind mode the patch changed at every wheel detent, not at a confirm |
| Patch - special → "Unpatch dimmer", "Dimmer:>102", "Unpatch?" (seq 150931, 151357, 151492) | [5, 21] (seq 151663, 2398.505) | DMX 102 = 0 (seq 151667, f303334) |
| Sequences → "Playback:>XF" → "Step Timing" (after "Modify Steps", scrolled past) → "One step" (seq 152970, 153962, 154049); "XF Step:>12.1 01/09", "XF Step:>14.1 State", "XF Step:>15 01/09 U 3.0 D 4.0 W 2.0" (seq 155197, 155246, 155277) | [21] at each screen | — |
| The wheel moves U 3.0 → 5.0 (2460.1–2464.7; "U> 5.0 D 4.0 W 2.0", seq 156014) | six lists [3, 3, 4:21, 21, (3, 3, 4:21) × k] (seq 155735 … 155998): **StepContent (4, 21) listed 20 times, each time with SequenceMain twice** | StepContent 21 body[8] 0x1E → 0x20 → 0x22 → 0x25 → 0x28 → 0x2A → 0x2B → 0x2E → 0x2F → 0x30 → 0x31 → 0x32, 11 changes (seq 155747 f311494 … seq 155935 f311870); SequenceMain unchanged |

- **H101, second half, CONFIRMED (OBSERVED once):** after its own Patch by dimmer, Unpatch dimmer and Step Timing edits the console
  listed the changed object at once, as after its own record keys (F142, F147). Every record had c = 0, the ids were 3, 4, 5 (and
  21, 22) and the body lengths 46, 13, 514: **inside SC-1's guards as built** (`HspReadSession.ObservedListedIds`,
  `ObservedBodyLengths`; INFERRED from the code, not tested with our software). The edited step was a memory step; the console's
  Step Timing on the "State" step (148 bytes) was not tried.
- **The console's own screens tie the fields** (OBSERVED once): the third time is "W" on the console, "Wait" in SmartSoft (owner
  D3) and the record's last field, which the file writes as FOLLOWON (F74): H14. The patch screen shows a Patch entry 49 as "i1" and
  the type-2 step as "State".

### H101: what the console lists after a write, a command or its own edit

| Trigger | Count | The console's answer |
|---|---|---|
| A SmartSoft object write (Patch 4, Independents 7, StepContent 11, Sequence 13, Memory 5, Page 6) | 46 | **nothing**, 46 of 46 (no list of any kind before the next write or 15 s) |
| InsertStep, stack | 4 | on the next poll (+18–19 ms): SequenceMain and StepContent from the new index to the end ([3, 4:19]; [3, 4:17 … 4:20]; [3, 4:20, 4:21]) |
| InsertStep / DeleteStep, KOTLIK | 2 | on the next poll (+15–19 ms): **Sequence (2, 0, 0, 6)**, c = 6 both times (the index of the changed step in both cases; one value) |
| DeleteStep, stack | 4 | on the next poll (+16–20 ms): SequenceMain and StepContent of the deleted index only (a record of zeros when it was the last) |
| MoveStep / id 42 | 1 / 1 | [3, 4:17 … 4:21] on the next poll; after id 42, [3, 4:20, 4:21] once the move's records were fetched |
| Key GO (0x31) / FADERS MEMS (0x39) | 1 / 1 | [3, 22] and the crossfade's PlaybackInfo; [23, 22] and [21, 22] |
| Key 0x3A | 1 | **nothing** (no list for the remaining 631 s of S1) |
| The console's Patch by dimmer / Unpatch dimmer / Step Timing | 39 channel values / 1 / 11 Up values | Patch (5) 53 + 1 times; StepContent (4, 21) 20 times, each with SequenceMain (3) twice |
| The console's REC MEM + bump 9 (record in B1, empty in I3) | 2 | Memory (0, 0, 8) (F147) |

So the first clause of H101 holds for **object writes** and fails for **step commands**, which are answered by lists of the
affected objects (a reply to the command's effect, not an acknowledgement: no list names the command itself). **Relevance for SC-1
(INFERRED from the code, not observed with our software):** E12 shows the three things H101 predicted would stop a read-only
session: a listed record with **c ≠ 0** (Sequence (2, 0, 0, 6)), a **Sequence of another length** (144 bytes, 8 steps) and a
**type-2 StepContent** (148 bytes). E12 saw them only after SmartSoft's step commands and in download 2 (which a session of ours
never triggers); after the console's own edits it saw only ids 3, 4, 5 with c = 0 and the known lengths. A console-side insert,
delete or move, or a console-side edit of a "State" step, during a read-only session is untested (INFERRED: it would list the same
objects and fault the session, a safe stop). Body lengths of ids 2 and 4 depend on the show's content, so one length per id
(`ObservedBodyLengths`) holds only for the show of E2–E12.

### Write placement and framing (`write_rules.py`, `write_timing.py`)

| Check | E9 | E9b | **E12** |
|---|---|---|---|
| Data writes; ids | 401; 39, 20, 9 | 427; 39, 20, 9, 0 | **70; 39, 20, 32, 34, 35, 42, 4, 2, 0, 8, 12, 5** |
| Header w2..w5 = the last fetch or reply header (F52) | 401 / 401 | 427 / 427 | **70 / 70** (all poll and data headers: 98 253 / 98 253) |
| Counter from 0x00 per SmartSoft session, +1 per write (F131) | 1 session, 0 breaks | 3, 0 breaks | **3 (0–46, 0–20, 0–1), 0 breaks** |
| Placement: after an idle reply / a list / an object; writes since the last poll | 121 / 110 / 170; 0 | 80 / 101 / 246; 0 | **62 / 2 / 6; 0 in 70 of 70** (never with a fetch outstanding) |
| With listed records still unfetched | 181 | 254 | **5** (W5, W20, W26, W29, W59) |
| Next OUT transfer: poll / fetch | 220 / 181 | 173 / 254 | **65 / 5** |
| Header → payload submit (ms) | 0.05–0.32 | 0.067–0.254 | **0.077–0.230** (median 0.129) |
| Poll submit → write header submit (ms) | min 29.81 | min 29.88 | **min 30.17**, median 31.89, max 48.60 |
| Write payload submit → next poll / fetch submit (ms) | 14.51–16.46 | 14.69–16.23 | **14.57–17.05, one at 30.60 (W67)**; median 15.79 |
| … from the payload's completion (ms) | 14.46–16.38 | 13.63–16.20 | **10.96–24.99** (the 85–518-byte payloads take 0.4–5.6 ms to complete on the bus) |
| Write → next write, same session (ms) | min 44.77 | min 45.55 | **min 46.89** |
| Key press → release (ms) | 45.63–5803 | 45.88–3355 | **46.89–93.64** (3 pairs) |
| Writes between a press and its release | 0 | 0 | **0** |

E12 keeps every rule E9 and E9b showed (OBSERVED a third time for the framing, the stale words, the per-session counter and the
placement): one write per loop iteration in place of a poll, after a completed exchange, never two per poll; a step command or
object write is placed exactly like a ConsoleEvent. **New figures (SmartSoft's, OBSERVED once):** the one 30.60 ms gap after W67
(a skipped loop slot, INFERRED); SmartSoft's idle poll interval fell to **20.446 ms** (seq 122076 … 122080, 1925.4 s; 11 idle
intervals below 21.0 ms, 34 below 21.6 ms, at 19 moments spread over all three sessions; cause UNKNOWN, PC-side timing),
below the 20.59 ms of E2–E3b behind `Floors.PollInterval` = 21 ms and E9b's 21.60 ms; a fetch's completion to the next poll fell
to 10.255 ms (F62: ≥ 10.30 ms; `Floors.PollAfterFetch` = 11 ms). Our floors stay above SmartSoft's minimums; nothing here
changes SC-1 or SC-2.

### Cross-checks

- **ConsoleVersion** "3.0.2.9.0.43" in all three sessions (seq 99, 138933, 194193), = the owner's Diagnostics reading (F48).
- **Download 1 against `SHOW12.ASC`** (`e2_asc_crosscheck.py` on the slice t < 280 s): Patch 512 of 512 entries (49 patched, 463
  unpatched, 0 mismatches; DMX 100–102 = `d/0/100`); 16 of 16 memories (`GROUP 9` absent, memory 9 empty), 768 of 768 levels; page
  labels 12 of 12 (`$PAGE 1` and `$PAGE 12` empty), channel labels 96 of 96; SequenceMain = `$SEQUENCE 48 2 0 256 256`, 19 of 19
  steps in all six fields; the Sequence header = `$SEQUENCE 0 0 0 256 256`, label KOTLIK, 7 records (the cue number differs as
  always, F75); SystemSettings' `SET` values at body[6..11]; Independents `00 FF 00 01` × 2 = `$INDEPENDENT 1 / 2 0 255 0 1`.
- **Download 3 against `SHOW13.ASC`** (slice t ≥ 3079 s): the same results in every part. Both files have SHA-256
  `559636399c1d…7a90` (re-checked).
- **`startdiff` against E9b's first bodies:** 156 identical; Memory (0, 0, 6) body[10] = 0x00 in E12 against 0xFF in E9b (memory 7's
  channel 7: E9's mistaken edit, still present in E9b, is gone, F139), FileName "SHOW12.ASC" (F103: the owner's latest SD save, the backup),
  and the live objects. **LED blocks 25–48 read dim green `00 0A 00 00 0A 00` in every LedStatus payload of E12** (`blk2548.py`,
  from seq 14), after a power-on in channel mode (INFERRED: ConsoleStatus body[0] = 0 in download 1); E9b read dim red after a
  channel-mode power-on (H78).
- **What the ASC files say about the edited objects:** only their unedited state. No SD save was made while the edits were on the
  console (SHOW13 after the restores), so the file forms of One Shot (`$SEQUENCE 0 1 …`?), an 8-step KOTLIK, a point cue 12.1
  inserted by SmartSoft, a type-2 ("State") step with its levels, a Page or memory label "T" and the patch entries 100 / 101 were
  not observed in E12.

### Hypotheses H91–H101

Proposed verdicts (OBSERVED at most; the curator applies them; **none of these messages is cleared, and our code never sends
any of them**).

| H | Verdict proposed | Evidence (Wn, seq, frame) | Runbook expectation against the capture |
|---|---|---|---|
| **H91** Patch write | **CONFIRMED (OBSERVED once)** | W9 (seq 43918 / 43919, f87837), W10 (seq 48804 / 48805, f97609); download 2 seq 139032 = W10; nothing listed | As expected: one 514-byte write per drag, only the dragged entry changed, DMX 100 = 47 and DMX 101 = **49** for IND 1; **no Independents write after the IND drag**. "a = b = c = 0" does not apply: a data payload has no qualifier fields |
| **H92** un-patching | **CONFIRMED for the DMX-cell drop (OBSERVED once, two drops)**; channel drop UNTESTED (deliberately); ids 29–31 absent in both directions | W67 (seq 190618, f381237), W68 (seq 191988, f383977); download 3 seq 194292 = download 1 | As expected |
| **H93** Independents write | **CONFIRMED (OBSERVED once)**: whole, 85 bytes, the console's layout; **button mode = record byte 2 (field c), GM = record byte 3 (field d)** | W11–W16 (seq 54128 … 58318, f108257 … f116637); W3 unchanged (seq 16307) | As expected; the values' meaning and the console's reaction UNKNOWN (restored before any download, nothing listed); label and level not exercised |
| **H94** step edits | **PARTLY CONFIRMED (OBSERVED once):** Up / Down / third time of a memory step = one 13-byte StepContent each; a type-2 step = 100 bytes with levels and a label block (label serialized); a sequence step's time = the whole Sequence (144 bytes). **The D4 clause did not occur as written** | W22–W24 (seq 70844, 72038, 73728); W29, W63, W64; W32 (seq 90450); D4: W25–W27 | D4: SmartSoft has no label for a memory step (owner: "The labels are from selected memory label or channel label"); the owner's D4 edit wrote **Memory (0, 0, 8) with the label "T"** (W27), which the console stored (download 2): no StepContent write at all. D5's label was set only in I2 (W63) |
| **H95** run mode and labels | **CONFIRMED for SmartSoft's side (OBSERVED once):** whole Sequence for the popup and the label; **body[3] 0 = Loop, 1 = One Shot** (W41, W56); stored by the console (download 2 body[3] = 1). No stack run-mode popup (owner A3) and **no SequenceMain write** | W41 (seq 123368, f246737), W56 (seq 167887, f335775), W42 / W55; download 2 seq 139152 | As expected for E3, E4, I1; Manual = 2, the stack's `b` = 2 and the console's playback of One Shot stay untested (HYPOTHESIS for the console side) |
| **H96** command addressing | **CONFIRMED for the two values (OBSERVED once):** stack a = 48 (10 of 10), KOTLIK a = 0 (2 of 2); formula not separable | W6 … W65 (table above) | As expected |
| **H97** command bodies and sources | **CONFIRMED (OBSERVED once):** 4-byte `[a][b][u16 c]` for 32, 34, 42; 3-byte `[a][b][c]` for 35; MoveStep then id 42 (47 ms); **no id 33**; b and c read as in the table above (id 42 = renumber, INFERRED) | W6, W8, W17, W19, W28, W30, W57–W61, W65 | As expected; c of InsertStep for a sequence (8.0 for a new 7.0) UNKNOWN |
| **H98** two phases | **CONFIRMED with a correction (OBSERVED once, 5 of 5):** the type is in phase 2 (record byte 2), not in the command; phase 2 = the next OUT after the fetch of the console's default step, 109–161 ms after the command | W6 / W7, W17 / W18, W19 / W20, W28 / W29, W30 / W31 | The runbook expected "InsertStep with step type …": the command carries none. Type 0 (channel step) not exercised |
| **H99** page and memory names | **CONFIRMED (OBSERVED once):** Page 39 bytes `[page − 1][03 06][36]`; Memory 90 bytes for a cell edit and for name edits, every one with SmartSoft's levels | W47, W50–W54 (seq 137544, 161751 … 162279); W27, W44–W46, W66; downloads 2 and 3 | F3 renamed page 1 (owner's slip), corrected in I1 |
| **H100** absent features | **Supported by absence (one session), stays HYPOTHESIS:** no Patch message other than the 514-byte object, no ids 29–31, no level anywhere | the whole capture | The owner's A3 answer "Yes, On top there are channels and under it is output matrix …" describes the matrix, not a level control: the "Yes" is ambiguous (owner question 2) |
| **H101** lists after edits | **PARTLY CONFIRMED / PARTLY REJECTED (OBSERVED once):** object writes → nothing listed (46 of 46): CONFIRMED; step commands → the affected objects listed (12 of 12: 11 on the next poll, id 42's after the move's pending fetches): the "lists nothing back" clause is REJECTED for commands; the console's own menu edits → the changed object listed at every detent (Patch, SequenceMain + StepContent), c = 0, lengths 514 / 46 / 13: CONFIRMED | section "Step H", table above | The SC-1 hazards H101 named exist on the 1248 (c ≠ 0 Sequence records, 144- and 148-byte bodies), seen after SmartSoft's commands and in download 2 only |

### Other hypotheses and facts touched

| Id | Verdict proposed (OBSERVED at most) | Evidence |
|---|---|---|
| **H87** emptied memory | **List clause CONFIRMED (OBSERVED once):** memory 9, emptied by the console's REC MEM in I3 (`00 08 00 60`, seq 188544), is **not named in download 3's list** (seq 194272), as a never-recorded memory; the count-byte reading of byte 3 is supported by a second object (type-2 StepContent: SmartSoft 0x30 + 48 levels, the console 0x60 + 96) | seq 188544, 194272; W29 / seq 139240 |
| H12 `$$CUEPARAMS` = step type | Supported for 1 (memory) and **2 = the stack-only state** (SmartSoft's "Direct cue", the console's LCD "State", seq 155246); 0 untested (the console's transient default step after InsertStep has type 0 with memory index 0, seq 30132, 61502, 65967, 79666; never displayed, never saved) | W7, W29; seq 155246 |
| **H14** FOLLOWON = the console's Wait | **CONFIRMED (OBSERVED once):** the console's Step Timing screen names the third time "W" (seq 155277: "U 3.0 D 4.0 W 2.0" for the value W24 wrote), SmartSoft names it "Wait" (owner D3), and it is the record field the file writes as FOLLOWON (F74) | W24 (seq 73728), seq 155277 |
| H34 Patch = channel per DMX address | Supported again, from both sides: SmartSoft's writes and the console's own patch screen ("D:> 97 C: 24" = entry 0x18, "D:>101 C: i1" = 0x31, "D: 102 C:>47" = 0x2F) | seq 146839, 146853, 148466 |
| H35, H41, H42 | Supported: SequenceMain body[0] and Sequence body[2] follow the step count (19 → 22 → 19; 7 → 8 → 7); Up, Down and the third time are record bytes 6–7, 8–9 and 10–11 (body[7..12] of StepContent), one per edit | W22–W24; seq 61498 … 183456 |
| H43 sequence step numbers | The console renumbers a sequence's steps ordinally itself after an insert (seq 87591: 7.0 new, 8.0 the shifted step) and back after the delete; which number its display shows stays UNKNOWN | seq 87591, 169127 |
| H44 Independents a–d | Fields c = button mode and d = GM (OBSERVED once, SmartSoft's writes); value meanings UNKNOWN | W11–W16 |
| H40 label byte order | SmartSoft's codec A again, for a 17-character label over three lines (W63); its clear zeroes the first unit of each line (W64). The console's own reading untested; no "č" and no 6-character third line | W63, W64 |
| H64, H76 | Supported: block 50 blue during the A3 GO fade; record 5 bytes 0–1 unchanged (`00 00`) with STACK unlit | seq 22452 … 22897 |
| H65 | Supported: the list pending at the first poll after this power-on was [22, 21] (seq 10) | seq 10 |
| H78 | Inconclusive: dim green after a channel-mode power-on here, dim red in E9b; cause UNKNOWN | `blk2548.py` |
| H82 | Untested: code 0x3A appeared once with no visible effect and an unidentified key; LED block 59 stayed `0A` × 6 in every payload | W36 / W37 |
| H30, H31 | Supported: the System pair after reply headers #40 / #79 in S1–S3 (SmartSoft's sessions 10–12 with the pair); the console's counter continued across both restarts (174 → 175, 75 → 76) | `sessions` |
| H66, H83 | Untested (every SmartSoft session sent the pair; no EDIT MEM) | — |

### Golden-test candidates

**Inbound (the console's bytes, for decoders):** seq 87591 (f175182) Sequence (2, 0, 0, 6), 144 bytes, listed with c = 6; seq
175932 (f351864) and seq 139240 (f278480) the 148-byte type-2 StepContent; seq 30477 (f60954) a StepContent of zeros after the last
step was deleted; seq 139012 (f278024) the 161-record list; seq 148462 (f296924) the Patch from the console's own Patch by dimmer;
seq 155935 (f311870) StepContent after the console's own Step Timing; seq 188544 (f377088) the emptied memory from the console's
keys; seq 38719 (f77438) a recording from the console's keys; seq 156014 / 155246 the LCD "U> 5.0 D 4.0 W 2.0" and "State".

**Outbound (SmartSoft's bytes; decoder or replay tests only, never an encoder: none of these messages is cleared, and no encoder
for them may exist in the solution):** W9 / W10 (Patch), W11 / W15 (Independents c and d), W17 / W18 (InsertStep + phase 2), W19
(point cue), W28 / W29 (InsertStep + the 100-byte Direct cue), W30 / W31 (sequence insert), W41 (One Shot), W47 (Page), W57
(DeleteStep, KOTLIK), W58 / W59 (MoveStep + id 42), W63 / W64 (a three-line label and its residue), W36 (key 0x3A).

### Input for the knowledge base (for the curator; nothing applied here)

**Status changes and annotations:**

| Id | Current | Proposed | Evidence (E12) |
|---|---|---|---|
| H91 | HYPOTHESIS | **CONFIRMED** (OBSERVED once) | W9, W10; download 2 |
| H92 | HYPOTHESIS | **CONFIRMED for the DMX-cell drop** (OBSERVED once); the channel-drop half stays HYPOTHESIS | W67, W68; download 3 |
| H93 | HYPOTHESIS | **CONFIRMED** (OBSERVED once), with c = button mode and d = GM; value meanings UNKNOWN | W11–W16 |
| H94 | HYPOTHESIS | **PARTLY CONFIRMED** (OBSERVED once); D4's clause not testable as written (no memory-step label in SmartSoft; the memory's label is written instead) | W22–W24, W29, W63, W32; W27 |
| H95 | HYPOTHESIS | **CONFIRMED for SmartSoft's side** (OBSERVED once; 0 Loop, 1 One Shot); the console's meaning and value 2 stay HYPOTHESIS | W41, W56; download 2 |
| H96 | HYPOTHESIS | **CONFIRMED for a = 48 (stack) and a = 0 (KOTLIK)** (OBSERVED once); the formula stays open | 12 commands |
| H97 | HYPOTHESIS | **CONFIRMED** (OBSERVED once), with the b / c readings above | 12 commands |
| H98 | HYPOTHESIS | **CONFIRMED with a correction** (OBSERVED once): type in phase 2, not in the command; trigger INFERRED (H105) | 5 inserts |
| H99 | HYPOTHESIS | **CONFIRMED** (OBSERVED once) | W27, W44–W47, W50–W54, W66 |
| H100 | HYPOTHESIS | Unchanged (supported by absence); owner question on A3 | — |
| H101 | HYPOTHESIS | **Split:** object writes → nothing listed CONFIRMED; commands → lists (the clause REJECTED for commands); own menu edits → listed CONFIRMED (OBSERVED once each) | tables above |
| H87 | HYPOTHESIS | **List clause CONFIRMED** (OBSERVED once); the byte-2 / byte-3 readings supported | seq 188544, 194272 |
| H14 | OPEN (supported) | **CONFIRMED** (OBSERVED once) | seq 155277, W24 |
| H12, H34, H35, H41, H42, H43, H44, H40, H64, H65, H76, H78, H30, H31 | as listed | Annotate as in the table above | — |
| **F143** Memory write layout | OBSERVED (once, E9b) | **VERIFIED for the write** (E9b + E12: 5 more writes with the same 90-byte layout, w1 = 94, nothing listed back); annotate: the console stores it in its 138-byte layout (download 2 = W46: channel 5, label, byte 2 = 1, channels 49–96 zero as before — not decisive for zero-fill). NOT CLEARED | W27, W44–W46, W66; seq 139072 |
| **F146** REC MEM of an empty look | OBSERVED (twice, one session) | **VERIFIED for the console-key path** (E9b + E12 I3: `00 08 00 60` + zeros, listed at once); annotate H87's list clause | seq 188544 |
| **F147** the console lists its own recordings | OBSERVED (twice, one session) | **VERIFIED** (E9b + E12: B1 recording seq 38719, I3 deletion seq 188544) | seq 38719, 188544 |
| F144 ChannelText stored verbatim | OBSERVED (three round trips, one capture) | **Curator's call:** E12, a separate capture after another power cycle, returns E9's residue `06 03 06 00 00 00 61 00 6A` in all three downloads (seq 422 and the two later downloads): persistence and read-back of a written label reproduced in a second capture; the write itself was not repeated | downloads 1–3 |
| F52, F131 | VERIFIED | Annotate: a third capture of writes (70 of 70 stale headers; w1 = 8, 17, 43, 89, 94, 104, 148, 518 new or repeated; counter per session) | `write_rules.py` |
| F57 | VERIFIED (limited) | Annotate: 973 records = 973 fetches in list order; **two non-System records with c ≠ 0: Sequence (2, 0, 0, 6)** (seq 87587, 169123); no de-duplication (Patch up to 7 times, SequenceMain twice per list) | lists |
| F67 | VERIFIED for the ids seen | Annotate: **ids 2 and 4 come in more than one length** (Sequence 132 / 144 with 7 / 8 steps; StepContent 13 / 148 for a memory / "State" step); PC → console now also ids 2, 4, 5, 8, 12, 32, 34, 35, 42 | downloads, writes |
| F58 / F92, F80, F89 / F94, F93, F103, F104 / F139 | as listed | Annotate: the pair in sessions 10–12 (#40 / #79); pseq 0x00 after the power-on; the 157-record list twice more (`8208d7647d400d48`) and the 161-record list; the counter across two restarts; FileName "SHOW12.ASC"; SHOW13 = SHOW12 = SHOW09 = SHOW07 = SHOW04 | — |
| F133, F134 | VERIFIED (codes, behaviour) | Annotate: GO (0x31) and FADERS MEMS (0x39) from SmartSoft's edit tabs (unlogged); **0x3A seen once (OBSERVED), no visible effect, key UNKNOWN**; GO with STACK unlit again without a level change | W4–W5, W25–W26, W36–W37 |
| F148, F149 | DOCUMENTED | Annotate: the console's LCD shows these menus ("Patch by Dimmer", "Patch mode: Blind", "Patch - special", "Unpatch dimmer", "Unpatch?", "Step Timing", "One step", "XF Step") and calls a stack-only state "State"; Patch by dimmer in Blind writes the patch at every wheel detent (OBSERVED once) | seq 145414 … 156014 |

**New facts (proposed IDs; all OBSERVED once in E12; NOT CLEARED, show data, never sent by our code):**

- **F151 The step commands as SmartSoft sends them.** InsertStep (32), DeleteStep (34), id 42: header `01 00 08 00` + stale words,
  4-byte body `[a][b][u16 BE c]`; MoveStep (35): `01 00 07 00`, `[a][b][c]`. a = 48 for the stack, 0 for KOTLIK; InsertStep b =
  the new step's index, c = its cue number × 10 (stack); DeleteStep b = the index, c = 0; MoveStep b = from, c = to; id 42 = (a,
  index, cue × 10), sent 47 ms after MoveStep, renumbered the moved step (12.1 → 14.2). No id 33: a point cue is an InsertStep.
  The console answers each on the next poll with a list of the affected objects (SequenceMain + StepContent, or Sequence (2, 0, 0,
  6)); no list names a command. W6 … W65.
- **F152 InsertStep's two phases.** The console creates a default step (stack: the given cue number, type 0, memory index 0, Up /
  Down 5.0; sequence: a copy of the displaced step's memory with 5.0 / 5.0, steps renumbered ordinally), lists it, SmartSoft fetches
  it and writes the content as its next OUT transfer (StepContent for the stack, the whole Sequence for a sequence; type 1 memory,
  type 2 Direct cue), 109–161 ms after the command; 5 of 5. W6 / W7 … W30 / W31; seq 30132, 61502, 65967, 79666, 87591.
- **F153 SmartSoft's whole-object writes of the patch, the independents, the stack, a sequence and the pages** are the console's
  layouts (Patch 514, Independents 85, StepContent 13, Sequence 10 + 38 + 12 n, Page 39) except the type-2 StepContent (100 bytes,
  `[30]` + 48 levels, against the console's 148 with `[60]` + 96); the payload carries no qualifier fields; nothing is listed back
  (46 of 46 object writes, Memory included); the console stores the content (download 2 equals the last writes, in its own layout,
  for Patch, Page, Memory, Sequence and StepContent); 10 of the 46 were unchanged rewrites. W3 … W68; seq 139032 … 139240.
- **F154 Field readings from E12's writes:** Patch entry 49 = IND 1 (SmartSoft's value; the console's patch screen shows "i1");
  Independents record byte 2 = the button mode, byte 3 = the GM setting (values' meaning UNKNOWN); Sequence body[3] = the run mode,
  0 Loop, 1 One Shot; the step record's third time is the console's "W" and SmartSoft's "Wait" (H14); record byte 2 = 2 is the
  console's "State" (SmartSoft's Direct cue), with its own level block. W10, W11–W16, W41 / W56, W24, W29; seq 146853, 155246,
  155277.
- **F155 The console lists its own patch and step-timing edits at every wheel detent:** Patch (5) 53 times while "Patch by Dimmer"
  (Blind) scrolled DMX 102 through 39 channel values to 47, once at "Unpatch dimmer"; StepContent (4, 21) 20 times, each with
  SequenceMain twice, while a Step Timing Up change went through 11 values (3.0 → 5.0 s); c = 0, lengths 514 / 46 / 13. In Blind,
  the patch itself changes at every detent, not at a confirm. Section "Step H".
- **F156 Records with c ≠ 0 for a Sequence, and content-dependent lengths.** After InsertStep and DeleteStep on KOTLIK the console
  listed Sequence (2, 0, 0, 6) (c = 6, the changed step's index in both cases: a reading, H102) and answered the fetch with 144
  and 132 bytes; the stack's type-2 step is 148 bytes. Relevance (INFERRED): each would end a read-only session of ours with a
  fault (c ≠ 0; length ≠ 132 / 13), a safe stop; seen only after SmartSoft's commands and in a download. seq 87587 / 87591,
  169123 / 169127, 175932, 139240.
- **F157 The downloads around an edit session:** after the edits the console's full list grew by exactly the added objects, in
  list order (161 = 157 + memory 9 + three stack steps), and after the restores it was byte-identical to the first (and to E9b's);
  the downloaded bodies equal the last writes; an emptied memory is absent (H87). seq 178, 139012, 194272.
- **F158 SmartSoft's edit tabs send live keys and rewrites the owner may not intend.** During the "looking only" step A3 SmartSoft
  sent GO (INFERRED: the stack view's PLAY, named by the owner), an unchanged Independents write and an InsertStep / DeleteStep pair
  (a Direct cue inserted and deleted); in D4 a FADERS MEMS key left the console in memory mode to the end of the capture; key 0x3A
  (no visible effect) came during the run-mode search. W3–W8, W25 / W26, W36 / W37; downloads 2 and 3 ConsoleStatus `01 …`.
- **F159 SmartSoft's label clear leaves residue in every line:** clearing the three-line label "Direct cue custom" zeroed only code
  units 0, 6 and 12 (W64), as F138's one-line "restore"; a residue the SD file does not show (F144). SmartSoft's sequence-label
  field took 6 characters (owner, E4), a step label 17 (W63).

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H102 | A Sequence (2) DirtyObjects record after a step command carries the changed step's index in c | (2, 0, 0, 6) after an insert and a delete at index 6 (one value) | E12b: an insert / delete at another index of KOTLIK; a console-side Insert / Delete in a sequence (does the console list c ≠ 0 by itself?) |
| H103 | The console ignores InsertStep's c for a sequence and numbers sequence steps ordinally (SmartSoft sent 8.0, the step became 7.0) | W30, seq 87591 | E12b: an insert at another index (does c change, does the result?) |
| H104 | Key code 0x3A is the console's PAUSE key (SmartSoft's stack-view PAUSE; LED block 59 by H82), with no visible effect without a running fade | W36 / W37 (no list); the owner's A3 answer names PLAY and PAUSE; PLAY sent GO (W4) | Owner question 5; a logged PAUSE click during a fade only with the owner's decision (GO is a live action) |
| H105 | SmartSoft sends InsertStep's phase 2 when the console's object of the new step has arrived (list → fetch → write) | 5 of 5 orders | E12b order; static: the insert dialog's accept path (S11.16 row 6) |
| H106 | A console-side Insert / Delete / Move in the stack or a sequence produces the same lists as SmartSoft's commands (including Sequence c ≠ 0 and 148-byte State steps) and would therefore stop a read-only session | F155 (own edits list objects at once), F156 | E12b (passive, the console's own menu), never with our software running |

**Readings that stay INFERRED:** that the fetched object triggers phase 2 (H105); that id 42 means "renumber" (one sample); where the
GUI inserts (before the clicked step); that SmartSoft rewrites unchanged objects when a cell is opened; the source of the GO (the
stack view's PLAY) and of the FADERS MEMS key in D4; that the Blind patch's transient values reached no output (DMX 102 unconnected,
no levels up); the SC-1 consequences above (from the code, not from a run of our software).

**What E12 does not show:** it is **one session** (all new findings OBSERVED once; VERIFIED needs E12b or another independent
capture); it shows SmartSoft's messages and the console's answers, **never that any of them may be sent by us**: nothing is cleared
(SC-1's poll and listed fetch remain the only cleared messages; SC-2's classes are unchanged; Patch and Independents stay class F
"Never"; the step commands and the Sequence, StepContent, Page and Memory writes are in no class). Not shown: whether the console
accepts these writes from a host that never sent the System pair (H66 for writes); malformed or short bodies; what the console does
with channels 49–96 of a 48-level write (zero before and after); the meaning of the Independents values, of InsertStep's c for a
sequence, of id 42 beyond one renumber; Manual (run mode 2), a channel step (type 0) and the console's playback of One Shot; whether
the console stores a type-2 label (deleted before a download); the console's own insert / delete / move; DMX output effects (DMX
100–102 unconnected, no level raised during the patch edits); the SD file form of any edited object.

*(Applied by the curator on 2026-10-31, evening: `known-facts.md` (F143, F147 VERIFIED; F144, F146 VERIFIED in part; F151–F159), `hypotheses.md` (H91–H101 verdicts; H102–H106).)*

### Owner questions (zero risk; statements never promote)

1. **A3** (15:44:03–15:47:44): did you open the IND panel (an unchanged Independents write at 15:44:03.5), click **PLAY** in the
   stack view (GO at 15:45:39.7: a 5-s stack crossfade on the console, no output change), and insert a **Direct cue** and delete it
   again (15:47:39–44)? On the console: did you move **fader 1** (15:44:21–23, to about half and back) and press **STACK** and the
   key that showed "Jump to step" (15:45:19–23)?
2. **A3 "patch level control = Yes, …":** did you see a level control in the Patch tab, yes or no?
3. **C3:** what did SmartSoft show for IND 1's button mode and GM before and after each change (the names of the values)? Why three
   writes for "button mode changed" (15:53:57.7–59.7)?
4. **D4** (16:00:08.4): where did you type the label "T", and what did you click just before (SmartSoft sent FADERS MEMS: the
   console has been in memory mode since)?
5. **16:05:42.5** (during the run-mode search): what did you click? SmartSoft sent key code 0x3A once; was it PAUSE?
6. **D1, D5, E1:** which row did you right-click (the new step went after the last cue in D1, before the clicked step in D2)?
7. **I2:** did you type "Direct cue custom", or did SmartSoft offer "Direct cue"?
8. The console was left in **memory mode** (FADERS MEMS lit, downloads 2 and 3) and the stack's playback had run one crossfade in
   A3: set the console as you want it (FADERS 1-24). Not show data; the SD file is unchanged.

### Next capture to take

- **E12b, the repeat** (passive, guided, the same safety frame: backup, USBPcap first, every change restored, SD save compared), to
  make H91–H99, H101 and F151–F159 VERIFIED as SmartSoft's and the console's behaviour, with the A3 questions removed from the
  "look-only" steps (or logged as actions) and these additions, each restored in place: a **channel step** (type 0); **Manual** (run
  mode 2) on KOTLIK; an insert and a delete at **another index** of KOTLIK (H102, H103); a **console-side** Insert step and Delete
  step in the stack and in KOTLIK (H106, what a read-only session would see) and a console-side Step Timing on a "State" step (148
  bytes listed?); an **SD save while edited** (One Shot, 8 steps, a point cue, a State step with a level, a page / memory label) to
  learn the file forms, then the restore and a second save; the IND values with the owner naming what SmartSoft shows (H44, H93); a
  "č" in a Direct cue's label and a 6-character third line (H40, console side via the download).
- Owner, zero risk: the questions above.
- Tooling: done here (`tools/protocol-analyzer/e12/`, hashes in the files table).

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

*(Added with the analysis, 2026-10-31, evening; notes 1–7 above are kept as written. Note 6's "no level control seen" is the
assistant's reading of an answer that begins "Yes": see note 17.)*

8. **A3 was not look-only.** Between 259.6 s and 480.5 s SmartSoft sent an unchanged Independents write (W3), GO press / release
   (W4 / W5: a 5-s stack crossfade in PlaybackInfo, LED block 50 blue, no level object changed) and a Direct cue's InsertStep,
   StepContent and DeleteStep (W6–W8: a step 15.0 existed on the console for 5.3 s); on the console, fader 1 went to about half and
   back (277.7–279.4 s, DMX 1, outside the rig) and STACK was pressed with "Jump to step" on the LCD (335.3–338.9 s). None of it is
   in the log; owner question 1. The show was not changed by it in the end (download 2 shows no trace; the step was deleted).
9. **D4 sent FADERS MEMS** (W25 / W26) and wrote memory 9's label (W27) instead of a step label; **the console stayed in memory mode
   to the end of the capture** (ConsoleStatus `01 …` in downloads 2 and 3, LED block 58 lit). Download 3 therefore differs from
   download 1 in ConsoleStatus and LedStatus (live state, not show data; not in the SD file). Owner questions 4 and 8.
10. **A key code never seen before, 0x3A** (W36 / W37, 16:05:42.5, during the run-mode search), with no list at all afterwards and
    LED block 59 unchanged (`0A` × 6 in every payload). Which key it is: UNKNOWN (H104 proposes PAUSE); no code may be derived.
11. **Ten unchanged rewrites** (W3, W21, W33–W35, W38–W40, W43, W62), all outside logged changes: SmartSoft writes whole objects
    also when nothing changed (INFERRED: on opening or leaving a cell), so a "look" in its editors is a write.
12. **E1's new KOTLIK step went before the old 7th step** (index 6, number 7.0), not after it as the runbook said; InsertStep's c
    was 8.0 for it. The restore (W57 DeleteStep at index 6) returned the Sequence to download 1's bytes exactly.
13. **The first non-System DirtyObjects records with c ≠ 0** (Sequence (2, 0, 0, 6), twice) and **content-dependent body lengths**
    (Sequence 144, StepContent 148): three things `HspReadSession` faults on (section H101; F156 proposed).
14. **SmartSoft's idle poll interval reached 20.446 ms** (11 idle intervals below 21.0 ms), below every earlier capture's minimum
    (E3b 20.59 ms, E9b 21.60, E9 29.01); a fetch's completion to the next poll 10.255 ms (E3b 10.33, E9b 10.39, E9 10.42). Our
    floors (21 / 11 ms) stay above; nothing changes.
15. **One write was followed by the next poll after 30.60 ms** (W67, a 518-byte Patch) instead of 14.6–17.1 ms (INFERRED: one
    skipped loop slot).
16. **The console's Blind "Patch by Dimmer" changed the patch at every wheel detent**: DMX 102 was patched to 39 channels in turn
    (1 … 47) within 4.9 s before it settled on 47. DMX 102 was unconnected and no channel level was up, so nothing was output
    (INFERRED); with a fixture there and a channel up, scrolling would light it in passing (the manual's "Blind" promises no output
    change only "until changes in the patch override any active channels", F148).
17. **The A3 answer "patch level control = Yes, On top there are channels and under it is output matrix …"** describes the
    matrix, not a level control; the "Yes" is ambiguous (owner question 2; H100 stays HYPOTHESIS).
18. **E9's label residue in ChannelText 6** (`00 00 00 61 00 6A` behind a zeroed first unit) is still on the console, in all three
    downloads, after another power cycle (F144); and W64 left a new residue of the same kind in three lines of the Direct cue's
    label, which was deleted with the step before any download.
19. **LED blocks 25–48 were dim green** after this channel-mode power-on (E9b: dim red after one): H78 stays open.
20. **The console-side edits of step H came 23–51 s after their log stamps** (menu navigation with the wheel): the stamps mark the
    start of the step, the data mark the change.

**Runbook deviations** (besides notes 2–4 and 8–12): D4 produced a Memory label instead of a StepContent write (SmartSoft has no
memory-step label); D5's label came only in I2; H's Step Timing set D1's Up back to 5.0 before I2 deleted the step (no effect on
the restore). Otherwise the E12 script was followed: the named backup first (`SHOW12.ASC`), Memory Integrity off, ETC's driver on
interface 1, USBPcap before SmartSoft, the timed log, the waits, no wheel in SmartSoft, no show, erase, settings or page action in
SmartSoft, no channel dragged onto the un-patch bin, every change restored (download 3 and `SHOW13.ASC`).

## Safety

Passive capture: SmartSoft drove every write, our software was not running on the console's interface and sent nothing. The
show was backed up before (SHOW12) and saved after (SHOW13), both kept. **E12 clears nothing** (SC-1's poll and listed fetch
remain the only cleared messages; SC-2's conditional GO is unchanged; the Patch and Independents writes stay class F, "Never",
and the stack, sequence, page and memory writes are in no class).

*(Added with the analysis:)* The analysis read the committed files only, with offline tools that cannot open a device. Its
findings make SmartSoft's step commands (InsertStep 32, DeleteStep 34, MoveStep 35, id 42) and its Sequence, StepContent, Page,
Memory, Patch and Independents writes **OBSERVED once as SmartSoft's and the console's behaviour, never sendable**: they change show
data, the console acknowledges none of them, a lost second phase or renumber leaves the stack inconsistent (INFERRED), and they are
reversible only by another exact write or by the SD backup. Any use of them by our code would need, at least, a second capture
(E12b), its own safety check, a complete body decode and a restore story first (S11.16 "Assessment"). The SC-1-relevant findings
(c ≠ 0 Sequence records, 144- and 148-byte bodies; the console listing its own patch and step edits) change no code here; they are
input for the coordinator.
