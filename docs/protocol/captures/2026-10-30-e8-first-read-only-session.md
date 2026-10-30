# 2026-10-30 E8: first read-only session of our own software (WinUSB, SC-1)

- **Date:** 2026-10-30. The session ran 16:25:28.39–16:39:49.68 local time (CET, UTC+1): the
  log header's `captureStart` is 2026-10-30T15:25:28.3880753Z and the last transfer completes at
  t = 861.296184 s. The original log in `%LOCALAPPDATA%\SmartFade\captures\` has the same SHA-256
  as the committed copy (checked 2026-10-30) and is the only file in that folder, so the owner's
  failed first attempt ("No SmartFade 1248 with WinUSB on interface 1 was found") wrote no log.
- **Experiment:** **E8**, the first read-only session of our own software, cleared by SC-1
  ([../safety-checks.md](../safety-checks.md)) and run after the E8 runbook
  ([../reverse-engineering.md](../reverse-engineering.md), "E8 runbook"). **Active:** our code
  sent the two cleared messages, the poll and the fetch of a listed record, and nothing else. It
  never sent SmartSoft's System writes `09 00 00` / `07 00 00`, no data header, no payload, no
  control transfer and nothing on interface 0. It was **not** a SmartSoft session.
- **Console:** ETC SmartFade 1248.
  - Firmware **not re-read for E8.** The owner gave no Diagnostics reading. A read-only session
    cannot fetch ConsoleVersion, because the console lists it only after the uncleared System
    `09 00 00` (F58, F59). Last reading: 3.0.2.9.0.43 (F48; ConsoleVersion in E2, E3 and E3b).
  - Power cycles since E3b: UNKNOWN. SmartSoft sessions since E3b: the owner says SmartSoft Online
    "works in both ways" (statements file), so at least one is likely; no capture of it exists.
- **Host:** the owner's PC, as in E1–E3b (Windows 11 Pro build 26300 per the earlier records; not
  restated for E8).
  - Backend `SmartFade.Api` in `--Device:Mode=Usb` with `--Device:Usb:ConfirmDate=2026-10-30`, at
    commit 08d1284. The log header's `host.version` is
    `1.0.0+08d1284558ac9fe408dadb066f79d02c824176be`.
  - Cadence settings: the owner's command line (statements file) names only the mode and the date,
    so the defaults applied as far as known: poll interval 31 ms, poll after a fetch 15 ms, fetch
    delay 28 ms, floors 21 / 11 / 21 ms, reply timeout 500 ms (`UsbDeviceOptions`).
  - Driver: WinUSB, bound by the owner with Zadig to the console's interface 1. The pipe in the log
    header is `\\?\USB#VID_14D5&PID_0200&MI_01#7&3b1b95d&0&0001#{e54818a2-…}`. Interface 0 stayed on
    the inbox HID driver (owner check after the session: MI_00 "USB Input Device", HIDClass; MI_01
    "ETC SmartFade", USBDevice).
  - Memory Integrity: not stated (WinUSB is an inbox driver; the runbook allowed it on).
  - SmartSoft (3.0.43, installed): not part of E8. The runbook notes that SmartSoft cannot see
    the console while WinUSB is bound to interface 1.
  - After the session the owner put the ETC driver back on interface 1 ("I already swapped the
    drivers").
- **Connection:** USB A-B cable straight into the PC (as in E1–E3b; not restated).
- **Show backup (safety rule 3, SC-1 condition 3): not recorded as specified.** The owner says
  "everything is backuped on the panel, so do not worry about that". No new file was named and
  none was copied: `smartfade-sd-card-backup/` holds SHOW01–SHOW05.ASC and nothing newer. Deviation
  from runbook step 1.
- **Parallel USBPcap (runbook step 3, SC-1 item 8): not run** ("I am not aware I ran it"). There is
  no bus capture, so the control-endpoint traffic at the Zadig bind, at the backend start and at
  Connect is unobserved (H68).
- **Action log: none.** `tools/usb-probe/action-log.ps1` was not used (runbook step 6). The owner
  described his actions afterwards, in his own order and without times
  ([2026-10-30-e8-owner-statements.txt](2026-10-30-e8-owner-statements.txt)). Every time below
  comes from the console's own data. The LCD texts and the object changes are the anchors.
- **Rig:** one ADJ DOTZ BAR 1.4 on DMX 5–16 (E3b-A3); nothing on DMX IN (owner, 2026-10-30).
- **What the owner did** (statements file, his order): channel faders; memory faders in a memory
  mode; SOLO; the BUMPS fader; BLACKOUT on; the MASTER fader; IND 1 and IND 2 on; CLEAR; STACK;
  the record keys EDIT MEM, REC MEM, REC SEQ and COPY. The runbook's plan (60 s idle, fader 1, a
  channel bump with BUMPS up, MASTER to half and back) was not followed. The first 12.6 s were
  idle.

**Conventions** (as in the E3b record, adapted to a log without pcap frames):
- `seqN` is the JSONL record number. There are **no pcap frame numbers**: the log is the
  backend's own record (`SmartFade.Devices.Usb.TransferLog`, format version 1), not a USBPcap
  capture.
- `t` is the completion time and `tSubmit` the submission time, in seconds since `captureStart`.
  `status` is `null` in every frame (header `statusNote`: the backend sees only success or
  failure; a failed transfer would be a note, and there is none).
- `pseq` is a payload's own sequence byte. `body[n]` is byte n after the 5-byte inbound payload
  header.
- **LED block k** = LedStatus body[1 + 6(k − 1) .. 6k], numbered from 1 as in F84 / F100. Phase A
  is the first three bytes and phase B the last three. "Steady" means phase A = phase B;
  "blinking" means phase A ≠ phase B.
- **FaderInfo record N** = body[1 + 4(N − 1) .. 4N]. Its bytes are numbered 0–3, so byte 0 of
  record 1 is body[1] and byte 3 is body[4].
- Every quoted byte comes from the cited seq. Readings carry their status: OBSERVED (seen in this
  session), INFERRED, HYPOTHESIS, DOCUMENTED (console manual v3.0, PDF page = printed page + 6, F5).

## Evidence files

| File | SHA-256 | Size | In git | What it is |
|---|---|---|---|---|
| `2026-10-30-e8-first-read-only-session.jsonl` | `f7d9d55f12e5cd1cd89ab83f217e3be82b43c9133b88a48b76aeefedfca17f60` | 11 326 190 B, 49 850 lines: 1 header, 49 846 `frame` records, 3 `note` records | yes (5ef67c7), **the evidence** | The backend's transfer log `2026-10-30-152528-usb-session.jsonl`, byte-identical to the original in `%LOCALAPPDATA%\SmartFade\captures\` (same SHA-256). Header `source` = `smartfade-backend`, `normalizer` = `SmartFade.Devices.Usb.TransferLog` version 1; `sourceSha256` is empty and there is no `console.firmware` (the backend has no source file and cannot read the firmware read-only). Notes: session start (t = 0.009285), "session stopped by the application at an idle point" (861.296555) and the counts "polls 20763, fetches 2925, lists 2470, objects 2925" (861.29659) |
| `2026-10-30-e8-owner-statements.txt` | `950607d04edc9377c75f684a72d82bd26e293631986bbdbc9881a1996afd9996` | 4 390 B, 57 lines | yes (5ef67c7) | The owner's observations and answers, transcribed by the assistant the same afternoon. Owner statements never promote by themselves |
| USBPcap capture | — | — | — | **None.** USBPcap did not run |
| Action log | — | — | — | **None** |
| Show backup | — | — | — | **None recorded** (see above) |
| `tools/protocol-analyzer/hsp_summary.py` | `84600ed39c66a9ee42a9f6faef4b8a508291d59a28cf6e2cfa09cd62cb2a63d3` | | yes (bc93144) | Modes `log`, `sessions`, `census`, `lcd`, `payloads` and `diff` work on the log (they print `fNone` or `f     -` for the frame). **`summary` stopped with a TypeError after its H20 lines and `timeline` failed at its first line** when this record was written: both compared or formatted the pcap frame number, which is `None` here (lines 365 and 496); SC-1's re-check had covered only the loader and `log`. Fixed in the commit that adds this record (both modes print `f-` for the frame) |
| `tools/protocol-analyzer/poll_rules.py` | `b54cf6d24e01469b820312f75762bc7d1e873797d1e0c109fcda3ae244dc457e` | | yes (5d4c5e5) | Cadence figures and the stale-word rule; works on the log |

The per-object timelines, LED block views and cadence histograms below were computed by small
read-only scripts that import the committed decoding functions of `hsp_summary.py` (`load`,
`classify`, `pairs`, `decode_payload`, `dirty_records`) and print by JSONL seq instead of pcap
frame. They were run with `py -3.13 -I` from outside the repository while the record was written
and were committed afterwards under `tools/protocol-analyzer/e8/` (`e8_health.py`, `e8_timeline.py`,
`e8_leds.py`, `e8_ledstate.py`, `manual_grep.py`), unchanged but for that note.

## Session health against SC-1

| Check | Result | Source |
|---|---|---|
| Transfers | 49 846, all bulk on interface 1: 23 688 OUT on 0x04 (all 12 bytes: 20 763 polls, 2 925 fetches), 26 158 IN on 0x83 (20 763 reply headers, 5 395 payloads) | `summary` (first part), ad-hoc |
| Framing | **0 errors**; every OUT is a 12-byte header with w0 ∈ {0, 2} and w1 = 0; no type-1 header, no data | `summary` (H15/H16) |
| Stale-word rule (F52) | **20 762 of 20 762** polls after the first carry the words of the most recent fetch or reply header; the first poll (seq 1) is 12 zero bytes (`poll_rules.py` counts it in: 20 763 of 20 763) | `summary`, `poll_rules.py` |
| Replies (F53) | 20 763 polls, 20 763 reply headers: 2 925 type 3, 2 470 type 4 with a list, 15 368 type 4 idle. **Type-3 echo 2 925 of 2 925**; every poll after a fetch was answered by the fetched object | `summary` (H16) |
| Lists and fetches (F57) | 2 470 DirtyObjects lists, payload length 5 + 2 + 5n in all; 2 925 records, c = 0 and a = b = 0 in all; **records = fetches 2 925 of 2 925, in list order, no re-fetch** | `summary` (H20) |
| Listed IDs | 12, 13, 14, 15, 17, 21, 22, 23 only, **all in `HspReadSession.ObservedListedIds`** (0, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 17, 19, 21, 22, 23, 36–39, 41); a listed id outside it would have ended the session | ad-hoc |
| Body lengths (F67) | Independents 85, OutputLevels 514, FaderInfo 195, ChannelLevels 51, PlaybackInfo 81, DisplayContent 41, LedStatus 451, ConsoleStatus 7: the F67 length for every id; DirtyObjects 2 + 5n. Largest IN transfer 519 bytes (bound 792) | ad-hoc |
| Inbound sequence (F55) | 5 395 payloads, pseq **123 (0x7B) … 141, 0 breaks**, 21 wraps, byte 1 = 0x00 in 5 395 of 5 395, LE length = payload − 5 in 5 395 of 5 395 | `summary` (H17, H18) |
| Faults, timeouts, errors | None. No failure note; reply-header IN completion max 21.57 ms (seq 17959, t = 241.645; median 1.99 ms), payload IN max 4.99 ms, OUT max 1.19 ms, against the 500-ms timeout. The header's `statusNote` expects the USBD health counts from the parallel USBPcap capture, which did not run, so these figures rest on the log alone | ad-hoc |
| End | The last exchange is an idle poll and reply (seq 49845 / 49846, 861.294 / 861.296 s), then the note "session stopped by the application at an idle point" (the owner pressed Disconnect). The last list was fetched to its end (records = fetches). Like SmartSoft's (F64), the end is silent: nothing closing was sent | log |

**Cadence (our loop, `poll_rules.py` and ad-hoc; ms, submit times unless stated):**

| Interval | E8 (our host) | Floor (SC-1) | SmartSoft (F62, `poll_rules.py` over E2, E3, E3b) |
|---|---|---|---|
| Poll → next poll, every pair (n = 20 762) | min **31.08**, median 45.84, max 92.17 | 21 | min 20.59, medians 30.8–31.2 |
| Idle poll → next poll (n = 15 367) | min 31.08, median 45.47, max 52.40; **two clusters**: 7 124 in 30–40 ms, 8 243 in 40–53 ms | 21 | median ≈ 31 (E3b 30.79; 20 348 of 24 547 in 30–32) |
| Payload completion → fetch (n = 2 925) | min **28.01**, median 30.00, max 46.58; 1 959 in 28–32, 3 in 32–34, 955 in 36–44, 8 in 44–47 | 21 | min 20.53, medians 28.3–28.8 |
| Fetch → next poll (n = 2 925) | min **15.12**, median 15.60, max 31.88; 2 532 in 14–18, 393 in 28–32 | 11 | min 10.35, medians 15.3–15.5 |

- **Every interval kept its floor**: 0 polls closer than 21 ms, 0 polls earlier than 11 ms after a
  fetch, 0 fetches earlier than 21 ms after their list.
- **Our cadence was SmartSoft's or slower**, as SC-1 clears. The idle polls fall into two clusters,
  near 31 ms and near 47 ms. The fetch delays fall near 28–31 and 38–44 ms, and the poll after a
  fetch near 15–17 and 28–32 ms. INFERRED: the Windows timer tick (15.625 ms, F62's explanation
  for SmartSoft) rounds our 31 / 28 / 15-ms waits up by one tick about half the time.
- The upper ends exceed the envelope that H67 quotes from SmartSoft: idle 20–64 ms holds (max
  52.40), but 263 fetches came more than 43 ms after their list (max 46.58) and 393 polls more than
  28 ms after a fetch (max 31.88). The console answered every one of them normally.
- **The floors themselves were never exercised.** The fastest intervals were 31.08 / 15.12 / 28.01
  ms, so a host running at the floors (21 / 11 / 21 ms) remains untested.

**This is the first evidence channel other than SmartSoft for F52, F53 and F57** (and F51, F54,
F55). Until now every observation of the poll / reply / list / fetch protocol came from SmartSoft's
own sessions, seen by USBPcap. In E8 another host program built the headers (our encoder) and
recorded the console's answers in its own log: 20 763 polls answered as F53 describes, 2 470
lists in the F57 format, each record fetched and answered by its object.
- For F53 and F57 (console behaviour) this is an independent reproduction by a different host.
- For F52 (SmartSoft's header rule) it shows that the console accepts F52-conformant headers from
  another host, not anything new about SmartSoft.
- The outbound bytes are our encoder's output, so they cannot serve as golden evidence for that
  encoder (see "Golden-test candidates").

### The connect-time list (seq 3)

The first poll (seq 1, t = 0.102) was answered at once by a type-4 header `04 00 C4 01 …`
(seq 2). It announced a 452-byte payload: a DirtyObjects list, pseq 0x7B, of **89 records**
(seq 3, body `00 59` + 89 × 5 bytes, 447 bytes). In list order:

`23 22 23 21 23 23 23 23 23 23 14 17 23 23 15 13` + `23` × ~~26~~ 25 + `12` × 44 + `23` × 4 *(the run of
ConsoleStatus records corrected by the curator's re-decode of seq 3 on 2026-10-30: 16 + 25 + 44 + 4 = 89)*

| ID | Records | Fetched (first … last) |
|---|---|---|
| ConsoleStatus (23) | **39** | seq 7 (0.187 s) …; all 39 bodies `00 00 00 00 00 01 00` |
| Independents (12) | **44** | seq 171 (2.441 s) …; all 44 bodies identical, both independents off |
| LedStatus (22), DisplayContent (21), FaderInfo (14), PlaybackInfo (17), ChannelLevels (15), OutputLevels (13) | 1 each | seq 11, 19, 47, 51, 63, 67 |

- Our loop fetched all 89 in list order, one per poll cycle, from seq 4 (0.155 s) to seq 356
  (4.902 s). No new list could come before the last of them was fetched, so the next list
  arrived only at 4.953 s.
- **Same structure as E2's connect list** (F56: 9 records, PlaybackInfo, FaderInfo, LedStatus,
  OutputLevels, ChannelLevels, ConsoleStatus × 2, DisplayContent, FileName): the same live
  objects, with repeats. Differences: no FileName here, and Independents appears for the first
  time. E3 (InputLevels alone) and E3b (LedStatus and DisplayContent, or nothing after a restart,
  F94) had short lists.
- **No de-duplication, at scale.** ConsoleStatus is listed 39 times and Independents 44 times,
  each fetched every time, all identical within the id. E2's "ConsoleStatus listed twice"
  (F57's no-de-duplication detail, resting on E2 only) is reproduced in a second session.
  HYPOTHESIS: the pending list is a log with one record per change event since the last delivery,
  so 39 / 44 change events of these objects happened on the console after the last session
  that fetched them (H65).
- **Our session saw only what was pending.** No list ever named the 157-record "full show" objects
  (Patch, Memory, Page, ChannelText, Sequence, StepContent, SystemSettings, FileName, …), because
  the console lists those only after SmartSoft's System `07 00 00` (F58, F89, H58).
- Size: 89 records is not one of the 37 / 101 / 165-record sizes (a multiple of 64 bytes) that
  SC-1's accepted leftover names. E8 shows, however, that the size of a pending list depends on
  the console's history: those sizes are reachable in practice.

### Objects (2 925 payloads besides the lists)

| Object | Payloads | Changed vs. previous of the id | Identical to previous | First … last (seq, t) |
|---|---|---|---|---|
| ConsoleStatus (23) | 64 | 5 | 58 | 7 (0.187) … 43714 (740.792) |
| LedStatus (22) | 125 | 117 | 7 | 11 (0.234) … 43882 (743.919) |
| DisplayContent (21) | 139 | 90 | **48** | 19 (0.326) … 43886 (743.965) |
| FaderInfo (14) | 79 | 71 | 7 | 47 (0.730) … 40311 (675.843) |
| PlaybackInfo (17) | **2 340** | 2 257 | 82 | 51 (0.791) … 24963 (377.334) |
| ChannelLevels (15) | 64 | 53 | 10 | 63 (0.932) … 40319 (675.953) |
| OutputLevels (13) | 58 | 46 | 11 | 67 (0.981) … 40334 (676.127) |
| Independents (12) | 56 | 12 | 43 | 171 (2.441) … 23097 (341.221) |

- **Being listed does not mean having changed.** 48 of 139 DisplayContent payloads equal the
  previous one. Of the 25 ConsoleStatus payloads after the connect list, 20 equal the previous
  one: 7 around the page prompts and 13 at record-function transitions ("Record keys" below).
  The repeats inside the connect list account for the other 38 identical ConsoleStatus and all
  43 identical Independents payloads.
- Lists during the session: 2 469 after the connect list. 2 305 had one record (2 261 of them
  PlaybackInfo alone), 78 had 2, 25 had 3, 21 had 4, 24 had 5 and 16 had 6. The last list came
  at 743.871 s; the remaining 117 s up to Disconnect were idle.

## Action reconstruction (from the console's data)

No action log exists, so this table is built from the data alone. The right-hand column matches
each event to the owner's account where one fits. The matching is INFERRED unless the bytes
name the action themselves (an LCD text).

| t (s) | seq | Data | Reading |
|---|---|---|---|
| 0.102 – 4.902 | 1 – 356 | All-zero first poll, the 89-record list, 89 fetches | Connect |
| 0.791 – 237.519 | 51 … 17739 | PlaybackInfo record 1 moving all the time (2 336 payloads; from 4.95 s about 10 per second, median interval 93 ms) | A sequence was running **before our first poll** (PlaybackInfo was in the connect list); not in the owner's account. See "PlaybackInfo" |
| 12.746 – 18.419 | 950 – 1358 | FaderInfo record 5 byte 0 (body[17]), ChannelLevels[5], OutputLevels[6] (DMX 5), LED block 5 G, LCD body[7]: up to 0xFF, down to 0, up to 0xFF | Channel fader 5 (the owner: "channel faders") |
| 53.766 – 54.293 | 4020 – 4059 | The same objects back to 0 | Fader 5 down |
| 54.775 / 54.911 | 4095 / 4103 | ConsoleStatus body[0] 0 → 1; LedStatus 58 bytes (the memory-mode map of page 1, F84) | Memory mode |
| 55.826 – 57.458 | 4168 – 4289 | FaderInfo record 1 bytes 0 **and** 1 `B0 B0` → `BE BE` → `89 89` → 0; channels 7, 8 and DMX 7, 8 up to 0xBE; LED block 1 full red | Memory fader 1 (the owner: "memory faders") |
| 57.691 – 61.837 | 4307 – 4613 | FaderInfo record 2 bytes 0 and 1 up to `FF FF` (seq 4478) and back; ten channels at the levels of `GROUP 2` | Memory fader 2 |
| 77.956 | 5820 | LED block 53 `0A`×6 → **`00 00 FF 00 00 FF`** (steady blue) | BLACKOUT on |
| 79.892 – 80.155 | 5971 – 5993 | MASTER (body[194]) FF → E2 → D2 → **88**, block 53 stays steady blue | MASTER down, BLACKOUT still on |
| 82.507 | 6165 | Block 53 → `37 37 FF 0A 0A 0A` (the MASTER-below-full blink, F85) | BLACKOUT off, MASTER still at 0x88 |
| 82.803 – 83.079 | 6186 – 6205 | MASTER → CD → FF; block 53 → `0A`×6 | MASTER back to full |
| 84.400 – 92.234 | 6300 – 6895 | Independents body[43] / body[1] toggling, LED blocks 73 / 72, ChannelLevels[50] / [49], OutputLevels[48] (DMX 47) with body[43] only | IND 2 on, IND 1 on, IND 1 off, IND 2 off, IND 1 on, IND 2 on, IND 1 off, IND 2 off (reading record 1 = IND 1, record 2 = IND 2; INFERRED) |
| 95.745 – 97.329 | 7158 – 7278 | BUMPS (body[193]) 00 → FF → 00, ten changes | BUMPS fader |
| 98.698 / 99.502 | 7377 / 7437 | LED block 52 `FF`×6 and back to `0A`×6 | SOLO on and off |
| 100.111 / 100.701 | 7483 / 7529 | Block 53 steady blue and back | BLACKOUT on and off |
| 200.305 – 202.978 | 14974 – 15164 | Memory fader 1 up to `FF FF` and down | Memory fader 1 |
| 237.369 – 244.460 | 17728 – 18121 | LCD "Memory page:1 / Bump 1-12 to change" (seq 17732), page-select LED view, ConsoleStatus body[2] → **0x0B** (seq 18003), FaderInfo byte 3 of records 1–24 → 0x0B, LCD "Memory page:12" (seq 18087), back to page 1 (seq 18102) | **A page change to page 12 and back**, not in the owner's account. The running sequence stopped at 237.519 |
| 244.816 – 247.906 | 18142 – 18325 | Channel mode, fader 5 up and down, memory mode again | |
| 248.440 – 249.299 | 18356 – 18421 | Memory fader 1 up and down | |
| 295.169 / 296.435 | 20754 / 20819 | Block 53 steady blue and back | BLACKOUT on and off |
| 296.915 – 298.318 | 20846 – 20929 | MASTER FF → E9 → D6 → D4 → E4 → FF; block 53 blinking from 296.979 to 298.318 | MASTER |
| 339.598 – 341.378 | 22996 – 23108 | IND 1 on, IND 2 on, IND 2 off, IND 1 off | IND keys |
| 368.906 – 369.341 | 24509 – 24540 | Memory fader 1 up to `FF FF` | |
| 372.420 – 372.639 | 24706 – 24722 | Record 1 `FF FF` → `00 00` **in one step**, channels 7, 8 to 0, LED block 1 → `46 00 00 0A 00 00` (blinking) | Memory level zeroed with the fader still up: CLEAR (INFERRED; see "CLEAR") |
| 377.334 – 379.202 | 24963 – 25068 | PlaybackInfo record 5 bytes 0–1 `01 01` → `00 00`; LCD "Live:14.0 Next: 1.0"; LED block 60 → `FF FF 00 FF FF 00`; LCD "Jump to step:> 1 / Mem:01" (seq 25057); back at 379.150 / 379.202 | STACK |
| 414.298 – 743.919 | 26861 – 43882 | 28 record-function screens: "Record Memory Pg:1" × 9, "Edit Memory" × 8, "Record Sequence" × 6, "Copy Memory/Seq" × 5, each with its LED pattern | The record keys REC MEM, EDIT MEM, REC SEQ, COPY |
| 672.345 | 40108 | LED block 1 blinking → steady `46 00 00 46 00 00`, no FaderInfo change | Memory fader 1 brought to zero (matched; INFERRED) |
| 675.285 – 676.127 | 40273 – 40334 | Memory fader 1 up to `FF FF` and down within 0.6 s | |
| 743.965 – 861.296 | 43886 – 49846 | One last unchanged DisplayContent (743.965), then idle polls only | |
| 861.296 | 49846 | Stop at an idle point | Disconnect |

## H66, H67, H68: the three unobserved points of SC-1

- **H66 (the console keeps reporting to a host that never sends the System writes): CONFIRMED
  (OBSERVED, one session).**
  - For 861 s the console answered all 20 763 polls.
  - It sent 2 469 lists after the connect list, naming FaderInfo, ChannelLevels, OutputLevels,
    LedStatus, DisplayContent, ConsoleStatus, Independents and PlaybackInfo. Every front-panel
    action the owner reports appears in them (sections below).
  - Changes were reported at once: fader 5's first movement was listed at 12.638 s (seq 942)
    and its FaderInfo arrived at 12.746 s.
  - Our session never sent `09 00 00` or `07 00 00`. Accordingly it never received ConsoleVersion,
    the 157-record list or any show object (F58, F89).
  - H66's side prediction holds: LedStatus (seq 11, 0.234 s) arrived before FaderInfo (seq 47,
    0.730 s).
  - The owner saw the UI follow the channel faders, SOLO, BUMPS and the MASTER (statements file).
- **H67 (the console tolerates our cadence): CONFIRMED (OBSERVED, one session) for the cadence
  actually used.**
  - Ranges used: idle polls 31.08–52.40 ms apart, fetches 28.01–46.58 ms after their list, polls
    15.12–31.88 ms after a fetch.
  - All of it is SmartSoft's cadence or slower, partly beyond SmartSoft's upper values (263
    fetches > 43 ms, 393 polls > 28 ms after a fetch).
  - Result: 0 framing errors, 0 unexpected replies, 0 timeouts and an unbroken inbound sequence.
  - **Not tested:** the floors 21 / 11 / 21 ms, which the loop never reached, and any other PC.
- **H68 (the console does not need the string-descriptor reads): stays OPEN (UNTESTED).**
  - Our code issued no control transfer, and the console answered the all-zero first poll within
    2.3 ms with a normal type-4 list header (seq 2), as E2's first reply did. This is what H68
    predicts.
  - However, the Zadig bind re-enumerates the device and Windows reads descriptors then, and
    whether `WinUsb_Initialize` issues control requests is unknown.
  - Without the USBPcap capture the EP0 history before our first poll is unobserved, so this is
    no test.

## The owner's report matched to the objects

The owner's sentences are quoted from the statements file. "UI" means our Angular UI in hardware
mode. At commit 08d1284 the backend's `ConsoleStateAssembler` takes from the live objects only:
- ChannelLevels and OutputLevels;
- FaderInfo body[193] / [194] (BUMPS, MASTER);
- LedStatus block 52 (SOLO);
- the DisplayContent lines, for the log, not the UI.

It also decodes FileName, ConsoleVersion and Memory, which a read-only session never receives.
It never claims BLACKOUT or the independents (shown as unknown, SC-1 item 6).

### Channel faders ("the CHANNELS works")

- **Physical fader 5 in channel mode** (12.746–18.419 s, 53.766–54.293 s and 246.208–247.048 s)
  moved the same five objects in step as in E3 and E3b (F82, F83):
  - FaderInfo record 5 byte 0 (body[17], up to 0xFF at seq 1169);
  - ChannelLevels[5] and OutputLevels[6] (DMX 5);
  - LED block 5 G (body[26], [29]);
  - the LCD cell body[7] (glyphs 0x80–0x86).
- FaderInfo record 5 byte 1 stayed 0 in channel mode, as in every channel-mode sample with a
  non-zero record (0 of 15).
- This is a third session for F82 / F83 / F86 (structure).

### Memory faders ("the MEMORIES do not")

- **Memory mode** (ConsoleStatus body[0] = 1 from 54.775 s, seq 4095) changed LED blocks 1–24 to
  page 1's memory map, as in E3 and E3b (F84):
  - blocks 1–8 and 13 `46 00 00 46 00 00`;
  - block 21 `46 46 00 46 46 00`;
  - the others `0A 00 00 0A 00 00`;
  - block 55 dim, block 58 red.
- **The memory faders are in FaderInfo:** physical fader k is record k in memory mode too (F99).
  - Memory fader 1 at 55.826–57.037 s: record 1 `B0 B0 00 00` → `BE BE 00 00` → `89 89 00 00` →
    `00 00 00 00` (seq 4168, 4195, 4239, 4258).
  - Memory fader 2 at 57.691–61.431 s: record 2 up to `FF FF 00 00` (seq 4478) and back.
  - Memory fader 1 again at 200.305–202.497, 248.440–249.091, 368.906–372.420 and
    675.285–675.843 s.
  - **Bytes 0 and 1 are equal in all 26 non-zero memory-mode samples**, and byte 1 is 0 in every
    channel-mode sample.
- **The channel and output levels carry the memory's content, scaled by the fader:**
  - Memory 1 = `GROUP 1` = channels 7 and 8 at 100 % (Memory (0, 0) in E3b, F71). Channels 7 / 8
    (ChannelLevels[7], [8]) and DMX 7 / 8 (OutputLevels[8], [9]) follow record 1:
    `BE` at seq 4176 / 4180, `FF` at seq 15069 / 15073.
  - With memory fader 2 at full (seq 4478), ChannelLevels (seq 4463) and OutputLevels (seq 4467)
    carry exactly the stored levels of `GROUP 2`, Memory (0, 1) "LES" (E3b, body[4..51]):
    channels 1–6 `2C 40 55 64 61 5A`, 9–10 `61 6E`, 13–14 `12 52`, all other channels 0.
    That is 48 of 48 channel bytes.
  - At intermediate fader values the levels fit "stored level × fader / 255". The fader byte and
    the levels are sampled at different moments, though, so the rounding rule is not determinable
    here (INFERRED, F107: "proportional playback" was INFERRED for the simulator).
  - LED block k reads `FF 00 00 FF 00 00` (full red) while memory fader k is above zero. This
    matches the manual: a memory's bump button "lights up at full if the fader is above zero"
    (F8, DOCUMENTED).
  - The LCD shows the channel output (cells body[9], [10] for channels 7, 8; all ten channels of
    `GROUP 2` at seq 4471).
- **Why the UI showed nothing:** the memories panel waits for Memory objects. The console lists
  those only in the 157-record list after the uncleared System `07 00 00`, so a read-only session
  never receives them (F89, H58). The memory fader levels, however, were in FaderInfo records
  1–24 (bytes 0 and 1) all along, and the page in ConsoleStatus body[2] (next section).
- **H62 is contradicted.** H62 says byte 1 "is non-zero only while a memory bump is held" and
  predicts that "moving the fader changes byte 0 only". Here memory faders 1 and 2 moved bytes 0
  and 1 together with no bump.
  - No bump was involved: BUMPS stood at 0x00 until 95.745 s, and a bump at BUMPS 0 adds no output
    (F88). The values moved through intermediate steps.
  - Successor HYPOTHESIS H71 below.

### The memory page (not in the owner's account)

- At 237.369–244.460 s the LED blocks and the LCD show the **page-select view** twice on page 1
  and once on page 12, as the manual describes it for the held MEMS button (printed p.57,
  DOCUMENTED): "As long as the button is held, the current fader page is indicated with a lit
  bump 1-12. Bumps for pages with recorded information will blink. To select a new page, press
  bump 1-12 and let go of the MEMS button."
  - Page 1 (seq 17728, 17968): block 1 steady `FF 00 00 FF 00 00`, block 12 blinking
    `C8 00 00 0A 00 00`, blocks 2–11 `0A 00 00 0A 00 00`. LCD "Memory page:1 / Bump 1-12 to
    change" (seq 17732, 17972).
  - Page 12 (seq 18083): block 12 steady full, block 1 blinking `C8 00 00 0A 00 00`. LCD "Memory
    page:12" (seq 18087).
  - The show has memories only on pages 1 and 12 (Memory qualifiers a ∈ {0, 11}, H25), and
    exactly those two blocks show "recorded information".
- **ConsoleStatus body[2] = page − 1.** Body `01 00 0B 00 00 01 00` at seq 18003 (242.437 s), back
  to `01 00 00 00 00 01 00` at seq 18102 (244.157 s).
  - The LCD's "Memory page:12" (seq 18087) was shown while body[2] = 0x0B.
  - OBSERVED once (two transitions). This fits CL-ML's "data[2] = 0-based page".
  - No list named System (39) at the page change; the page-select write of H51 is a PC → console
    message and stays untested.
- **FaderInfo byte 3 of records 1–24 = page − 1.**
  - At seq 18007 (242.493 s) all 24 records read `00 00 00 0B`, and back to `00 00 00 00` at seq
    18106. Records 25–48 did not change.
  - So the byte is 0 in every earlier capture only because the page was always 1.
  - HYPOTHESIS H69: a per-fader page byte. Per the manual, a fader above zero keeps its old page's
    content until it is brought to zero (F107), and all 24 faders stood at zero here.
- **FaderInfo record 21 byte 2 (body[83]) = 0x01 on page 1 and 0x00 on page 12** (seq 18007,
  18106).
  - In E2–E3b body[83] = 1 was a constant (F82).
  - Record 21 is fader 21, page 1's sequence slot (block 21 yellow, H55). On page 12 fader 21 holds
    no sequence: block 21 reads `0A 00 00 0A 00 00` in the page-12 map (seq 18015).
  - HYPOTHESIS H70: byte 2 = "this fader's slot holds a sequence". Record 25's constant 0x01
    (body[99]) is unexplained and did not change.
- **The page-12 memory map** (seq 18015): blocks 1–7 `46 00 00 46 00 00` (recorded), blocks 8–24
  `0A 00 00 0A 00 00`.
  - This matches the show's Memory objects with qualifiers a = 11, b = 0–6 (`GROUP` 265–271,
    fetched in E2, E3 and E3b, H25).
  - Page 1's map already matched a = 0 (F84). This is the first observation on a second page with
    the page number shown (LCD "Memory page:12", ConsoleStatus body[2] = 0x0B), and it ties
    qualifier a to the page and b to the fader (a = page − 1, b = fader − 1). It assumes that the
    show did not change since E3b, which E8 cannot check (no show object is fetched read-only).

### SOLO ("the SOLO button ... works")

- LED block 52 went `0A`×6 → `FF`×6 at seq 7377 (98.698 s) and back at seq 7437 (99.502 s). Both
  lists named LedStatus and PlaybackInfo only; nothing else changed.
- This is a second session after E3b's logged SOLO press / release (F100). Here the time comes
  from the owner's untimed account plus the UI's SOLO (decoded from block 52), which he saw
  follow the panel.

### BUMPS ("the ... BUMPS fader works")

- FaderInfo body[193] 0x00 → 0xFF → 0x00 in ten changes, 95.745–97.329 s (seq 7158 … 7278; 0xFF
  at seq 7202). No other FaderInfo byte changed, and no level object changed (no bump was
  pressed).
- Second session for F95 (E3b, four logged moves).

### MASTER ("If I change the MASTER fader, it works")

- FaderInfo body[194]: FF → E2 → D2 → **88** → CD → FF (79.892–82.942 s, seq 5971 … 6197) and
  FF → E9 → D6 → D4 → E4 → FF (296.915–298.177 s, seq 20846 … 20918).
- No level was up during either move, so F96's post-MASTER scaling was not re-tested.

### BLACKOUT ("If is turn it on, the UI doesnt show it" / "the BLACKOUT button does not flashing as it does ont the panel")

- **LED block 53 = `00 00 FF 00 00 FF` (steady blue) three times:** 77.956–82.507 s (seq 5820 →
  6165), 100.111–100.701 s (seq 7483 → 7529) and 295.169–296.435 s (seq 20754 → 20819).
  - Before and after it read `0A`×6 or the MASTER blink.
  - Reading: BLACKOUT active (INFERRED). The owner switched BLACKOUT on, and the manual says "As
    soon as it is activated the button will light up" (F5, DOCUMENTED).
- **While BLACKOUT was active the MASTER blink did not show.**
  - The MASTER went to 0x88 at 80.155 s and block 53 stayed steady blue until 82.507 s.
  - Then it changed to the blink `37 37 FF 0A 0A 0A` with the MASTER still at 0x88 (seq 6165).
    Reading: BLACKOUT switched off, MASTER still below full.
  - It went off at 83.079 s, after the MASTER was back at 0xFF (82.942 s).
  - The second MASTER move (296.9–298.2 s, BLACKOUT off) shows the plain blink (seq 20850 →
    20929), as in E3 / E3b (F85).
- **What the owner calls flashing** is this blink. It is carried in one LedStatus payload with
  phase A ≠ phase B (F85), and the hardware-mode UI deliberately does not blink (SC-1 second
  pass). The data carried it.
- **No other object carried BLACKOUT.**
  - The lists in the three windows named only LedStatus, PlaybackInfo and (with the MASTER)
    FaderInfo: 77.832 "17,22", 82.399 "17,22", 99.999 / 100.603 "17,22", 295.120 / 296.384 "22".
  - ConsoleStatus did not change.
  - All channel and output levels were 0 in all three windows, so whether ChannelLevels /
    OutputLevels / FaderInfo show a blackout (outputs to zero) is **untested**.

### IND 1 / IND 2 ("I turned them on but in UI they are still off")

Twelve changes, 84.400–92.234 s and 339.598–341.378 s. In each row the objects named changed
together, within 0.27 s of each other (sample seqs in the action table):

| Event | Independents (ID 12) | LED | ChannelLevels | OutputLevels |
|---|---|---|---|---|
| Record 1 on / off (6 changes) | **body[1]** 0x00 ↔ 0x01 (record 1 byte 0; seq 6421 on) | **block 72** `00 0A 00 00 0A 00` ↔ `00 FF 00 00 FF 00` (seq 6425) | **body[49]** 0x00 ↔ 0xFF (seq 6429) | **none** |
| Record 2 on / off (6 changes) | **body[43]** 0x00 ↔ 0x01 (record 2 byte 0; seq 6300 on) | **block 73**, same values (seq 6304) | **body[50]** 0x00 ↔ 0xFF (seq 6308) | **body[48] = DMX 47** 0x00 ↔ 0xFF (seq 6312) |

- **Independents record byte 0 = the independent's on state** (OBSERVED, 6 + 6 changes).
  - Bytes 1–3 stayed `FF 00 01`, with byte 1 = 0xFF matching the 0xFF level in ChannelLevels.
  - This fits CL-ML's `[latched][level][00 01]` (H44).
- **ChannelLevels body[49] / body[50] are the two independents' levels.** That is H26's
  Independents part, untested until now, now OBSERVED.
- **OutputLevels follows the show's patch, not the manual's default.**
  - OutputLevels body[48] is **DMX 47** (body[1 + d] = DMX d, F83), not DMX 48.
  - The patch of E2–E3b maps DMX 47 → channel 50 and gives channel 49 no DMX address (F70), so
    record 2 / channel 50 drove DMX 47 and record 1 / channel 49 drove nothing.
  - This show does not use F108's default (IND 1 = DMX 97, IND 2 = DMX 98; DMX 97 is patched to
    channel 24 here, F70). It is INFERRED that the patch was unchanged since E3b (Patch is not
    fetched read-only).
  - The rig (DMX 5–16) shows neither.
- **Which key is which is INFERRED.** Record 1 / channel 49 / block 72 is read as IND 1 and record
  2 / channel 50 / block 73 as IND 2, by order and by the patch's "channels i1 and i2" (F108,
  H11). The owner's account names no order; the first change (84.400 s) was record 2.

### CLEAR ("CLEAR works")

- At 372.420 s (seq 24706) FaderInfo record 1 went from `FF FF 00 00` to `00 00 00 00` **in one
  step**.
  - Channels 7 / 8 and DMX 7 / 8 fell from 0xFF to 0 at the same time (seq 24710, 24718), and the
    LCD cells cleared (seq 24714).
  - LED block 1 became `46 00 00 0A 00 00` (seq 24722): dim red, blinking.
  - It **kept blinking for 300 s** and turned steady `46 00 00 46 00 00` at 672.345 s (seq 40108)
    **without any FaderInfo change**. Memory fader 1 was then moved up and down again at
    675.285–675.843 s.
- **Reading (INFERRED): CLEAR set memory fader 1 to zero while the physical fader stayed at full.**
  The bump LED then blinked "unmatched" until the owner pulled the fader down.
  - Manual: CLEAR "set[s] memory faders to zero (press 2-4 times)", 3 presses for memory faders
    (F109); a bump button "will blink until the actual level is matched by that of the fader"
    (F8, printed for intensity faders).
  - The time fits the owner's order: after the IND keys (339–341 s), before STACK (377 s).
  - The UI showed channels 7 and 8 fall, which is what "CLEAR works" would mean there.
- **Alternative:** a very fast pull-down with no intermediate sample. The other pull-downs of
  memory fader 1 had one or two intermediate samples, and this alternative leaves the 300-s blink
  unexplained.
- Three DisplayContent re-listings (lists at 371.517 / 371.879 / 371.968 s, each fetched with an
  unchanged body) precede the drop. They are consistent with three key presses but weak evidence,
  because 48 unchanged re-listings occur in the session.
- HYPOTHESIS H73; test in "Next capture".

### STACK ("When I pressed STACK on panel, it does not display on state of that button on UI")

- 377.334 s (seq 24963): PlaybackInfo record 5 (body[65..80]) bytes 0–1 changed `01 01` → `00 00`;
  the rest stayed `FF 00 00 00 01 00 01 00 00 32 00 32 00 00`.
- 377.392 s (seq 24967): LCD "Live:14.0 Next: 1.0 " / byte 0x86 then "Mem:01/01 Mem:01/01".
- 377.440 s (seq 24971): **LED block 60 `46 46 00 46 46 00` → `FF FF 00 FF FF 00`** (yellow,
  full).
- 379.076 s (seq 25057): LCD "Jump to step:> 1" / "Mem:01".
- 379.150 / 379.202 s: the LCD back to the fader view, block 60 back to `46 46 00 …`.
- Manual (printed pp.60–61, DOCUMENTED):
  - "By pressing STACK, the Stack is activated (lit at full) … The cue stack is loaded onto the
    Crossfader at step 0 (a non-existent step before step one) so that pressing GO … will fade to
    step 1. The LCDs will show the current state of the Stack."
  - "If there is nothing stored in the Stack, the button will be off completely when not
    activated. otherwise it will be dimmed."
  - GOTO mode "(held): As long as the button is held, the display will allow you to scroll to any
    step."
- Readings, INFERRED:
  - block 60 is the STACK button's LED: dimmed yellow when inactive with a stored stack (19 steps,
    F74), full when active;
  - record 5 bytes 0–1 = 0 is "loaded at step 0" (the LCD's "Next: 1.0" agrees);
  - "Jump to step" is the GOTO display of a second, held press, after which the stack was
    inactive again (block 60 dimmed).
- "Live:14.0" is not explained (the stack's previous live step?). In the REC MEM and REC SEQ
  screens block 60 blinks `C8 C8 00 0A 0A 00` (next section), which matches the owner's "STACK is
  flashing".

### The record keys EDIT MEM, REC MEM, REC SEQ, COPY

Twenty-eight record-function screens between 414.298 and 743.919 s. Each was opened and closed
with exactly one LedStatus payload (2 per screen in 28 of 28), with no LedStatus in between, also
over REC MEM's longest screen, 711.14–726.95 s. The LED state on screen was **identical every
time**:

| LCD (count, first seq) | Bump LEDs 1–24 | Indicator blocks that changed (baseline in brackets) |
|---|---|---|
| "Record Memory Pg:1 / Select Bump or Stack" (9, seq 26861) | recorded 1–8, 13: `46 00 00 0A 00 00` (dim, blinking); empty 9–12, 14–24 (21 included): `C8 00 00 0A 00 00` (bright, blinking) | 57 `FF FF FF 0A 0A 0A` [`FF`×6]; 60 `C8 C8 00 0A 0A 00` [`46 46 00 …`]; **65** `FF FF FF 0A 0A 0A` [`0A`×6]; **66** `FF 00 00 FF 00 00` [`0A`×6] |
| "Edit Memory / Select Bump or Stack" (8, seq 27116) | recorded: `C8 00 00 0A 00 00` (blinking); empty and 21: `46 00 00 46 00 00` (steady) | 57 blinking; **67** `FF 37 37 0A 0A 0A` [`0A`×6] |
| "Record Sequence / Select Sequence" (6, seq 29110) | 1–20 `0A 00 00 0A 00 00`; 21 `46 46 00 0A 0A 00` (dim yellow, blinking); **22–24** `C8 C8 00 0A 0A 00` (bright yellow, blinking) | 57 blinking; 60 blinking; **64** `FF FF 00 FF FF 00` [`0A 0A 00 …`] |
| "Copy Memory/Seq / Select source" (5, seq 29266) | recorded: `C8 00 00 0A 00 00`; 21 `C8 C8 00 0A 0A 00` (both blinking); empty `0A 00 00 0A 00 00` | 57 blinking; **63** `FF FF FF FF FF FF` [`0A`×6] |

- **Blinking travels as phase A ≠ phase B inside one payload, never as alternating payloads**
  (OBSERVED in all 28 screens; F85's convention).
- **The bump LEDs match the owner's account and the manual:**
  - REC SEQ makes only 21–24 blink ("only they can store the sequence");
  - EDIT MEM and COPY make the recorded memories blink (COPY also the sequence on 21);
  - REC MEM makes the recordable buttons "blink at full" and the recorded ones blink "dimmed"
    (F107, DOCUMENTED).
  - The REC SEQ pattern is the first wire evidence that blocks 22–24 are sequence slots (H55's
    INFERRED extension).
- **Key LEDs** (INFERRED; the owner has not identified them on the panel). Manual, printed p.11:
  "All buttons have LED's … They light up when active, and blink when they can be selected for
  the current function." Readings:
  - **57 = CLEAR**: the only block that blinks in all four functions; the owner says CLEAR
    flashes with every record key.
  - **60 = STACK**: blinks in REC MEM and REC SEQ, the two functions the owner says make STACK
    flash; lit at full when STACK is active.
  - **65 = MAGIC**: blinks only in REC MEM, where the owner sees MAGIC flash.
  - **66 = REC MEM**: steady red; **64 = REC SEQ**: steady yellow; **63 = COPY**: steady white.
    Each changes in its own function only (63 in COPY, 64 in REC SEQ, 66 together with 65 in REC
    MEM) and is lit steadily, as the manual describes the active key.
  - **67 = EDIT MEM**: blinking `FF 37 37 / 0A 0A 0A`. The manual's EDIT MEM text says "EDIT MEM
    and CLEAR will blink".
  - The owner says that REC MEM, REC SEQ and COPY flash too. The data show them steady, so either
    his "flashing" is loose or these three identities are wrong. The order 63–67 = COPY, REC SEQ,
    MAGIC, REC MEM, EDIT MEM equals the order of the key labels in the manual's panel figure
    (printed p.13: "RATE | COPY | REC SEQ | MAGIC | REC MEM | EDIT MEM | SNAP SHOT", extracted
    text). That is weak support, since the extraction order of figure labels is not proof of the
    physical order.
  - HYPOTHESIS H72.
- **ConsoleStatus is re-listed with an unchanged body at every EDIT MEM exit and every COPY
  entry** (13 times: 423.112, 461.099, 518.073, 555.523, 557.155, 636.753, 640.873, 668.663,
  680.087, 690.819, 710.625, 733.317, 740.792 s). It is also re-listed unchanged around each
  opening and closing of the page-select view (7 times, 237.243–244.508 s). Meaning UNKNOWN.

## FaderInfo record layout (E8 adds bytes 1–3)

| Byte of record N (N = 1–24) | E8 reading | Status |
|---|---|---|
| 0 (body[4N − 3]) | Output level of physical fader N: the channel fader in channel mode (record 5 = channel 5), the memory fader in memory mode (records 1, 2) | OBSERVED (F82, F99; memory mode new) |
| 1 (body[4N − 2]) | Equal to byte 0 in memory mode (26 of 26 non-zero samples), 0 in channel mode (0 of 15 non-zero samples). E3b: `FF FF` with a held memory bump (F97) | OBSERVED; meaning HYPOTHESIS (H71); H62 REJECTED |
| 2 (body[4N − 1]) | 0x01 only in record 21 on page 1; 0x00 on page 12 | OBSERVED once; HYPOTHESIS (H70) |
| 3 (body[4N]) | Page − 1: 0x00 on page 1, 0x0B on page 12, in all 24 records | OBSERVED once; HYPOTHESIS per-fader page (H69) |

- **Records 25–48 never changed** in E8 (record 25 stays `00 00 01 00`). That covers channel mode,
  memory mode and page 12, and also the LED blocks 25–48: H61's "unused" part is supported again.
- **body[193] = BUMPS, body[194] = MASTER**, as in F95 and F82.
- The coordinator's first look called the page byte "byte 4 of each record" (counting from 1).
  In the 0-based numbering used here it is byte 3: body[4], [8], …, [96]. **body[83] is record 21
  byte 2**, not a page byte.

## ConsoleStatus (64 payloads)

| Body | Payloads | When |
|---|---|---|
| `00 00 00 00 00 01 00` | 39 | The connect list: channel mode, bank 1-24 (body[4] = 0), page 1 |
| `01 00 00 00 00 01 00` | 5 + 2 + 14 | Memory mode (54.775 s; again from 244.157 and from 247.766 s) |
| `01 00 0B 00 00 01 00` | 3 | Memory mode, page 12 (242.437–244.157 s) |
| `00 00 00 00 00 01 00` | 1 | Channel mode (244.816 s) |

- **body[0]:** three more transitions (0 → 1 at 54.775, 1 → 0 at 244.816, 0 → 1 at 247.766 s), each
  with the LED change of blocks 1–24, 55 and 58 (F87, F98). That makes 8 transitions in E3, E3b
  and E8.
- **body[2] = memory page − 1** (new, above).
- **body[4]** stayed 0, because the 25-48 bank was not used.
- body[1], [3], [5] and [6] never changed.

## PlaybackInfo (2 340 payloads)

- **What drives the stream: a running sequence in record 1** (body[1..16]).
  - The record was pending at our first poll and moving at its first fetch (seq 51, 0.791 s:
    `02 02 46 B9 00 00 01 00 01 00 00 0A 00 0A 00 0A`).
  - From 4.953 to 237.463 s, 2 261 lists named PlaybackInfo alone and 74 more included it.
  - Our loop fetched it about every 93 ms (median), so the rate is our cadence, not the
    console's.
- **Record 1 layout in motion** (OBSERVED, 2 340 payloads):
  - Bytes 0–1 (both equal in 2 340 of 2 340) step through **0, 1, 2, 3, 4, 5, 6, 0, …**: 207
    changes, a 7-step cycle, median 1.125 s per step (0.73–1.58 s, apart from the 4.2-s gap while
    the connect list was fetched).
  - Within each step byte 2 falls from its first sample (0x79–0xFF, depending on when it was
    sampled) to 0x00 while byte 3 rises to 0xFF, **summing to 255 in 2 340 of 2 340** (Live /
    Next as in record 5 during E3b's GO, F101).
  - Bytes 10–15 (three u16 BE) are `00 0A` × 3 (1.0 s) during a step's fade and `00 00` × 3 when
    it completes (280 payloads), then the next step starts.
  - Bytes 4–9 are constant `00 00 01 00 01 00`.
- **INFERRED: this is page 1's sequence on fader 21** (`$SEQUENCE 0`, "KOTLIK"). The Sequence
  object (0, 0) of E2–E3b has 7 steps, each UP 1.0 s and DOWN 1.0 s (E3b, records at body[48..131]).
  - Record 1's times changed at the page change and back: `00 00 00 00 00 0A` on page 12 (seq
    18011) and `00 0A 00 0A 00 0A` again on page 1 (seq 18110).
  - That supports H50's untested sub-reading "records 1–4 are the four sequence faders of the
    page" for record 1 (HYPOTHESIS).
  - The third u16 is 1.0 s while the file's FOLLOWON is 0, so it is not FOLLOWON (UNKNOWN).
- **Output:** none. No level object showed the sequence, so its fader (21) was at zero (INFERRED).
- **What started it is unknown.** It ran before our first poll, so our session did not start it
  (our session sends nothing that could). The owner did not mention it.
- **It stopped at 237.519 s** (seq 17739): bytes 0–1 `03 03`, Live / Next reset to `FF 00`, times
  `00 0A` × 3. After that only the times changed, at the page change (seq 18011, 18110).
  - This was 0.150 s after the first page-select view opened (LED seq 17728, 237.369 s). That the
    MEMS press, or something pressed with it, stopped the sequence is INFERRED from the timing
    alone.
  - Ask the owner.
- **Record 5 (the crossfader, H50)** changed only at the STACK press: bytes 0–1 `01 01` → `00 00`
  (seq 24963). Read together with the manual ("loaded onto the Crossfader at step 0"), bytes 0–1
  are the stack position.
- **H63: CONFIRMED in capture for record 1.** In record 1, bytes 0–1 are the 0-based step index of
  the running sequence (207 changes in step order, OBSERVED once). Record 5 is consistent (one
  transition).
  - E2's `00 00`, E3b's `04 04` / `06 06` and E8's initial `01 01` are positions left from earlier
    use.
- **Bytes 4–9 `00 00 01 00 01 00`** in all five records equal the Sequence / SequenceMain header
  fields b..e = 0, 0, 256, 256 (`$SEQUENCE 0 0 0 256 256`, F74). HYPOTHESIS H74: the last two are
  the Rate and Fade override (u16 BE, 256 = 100 %; manual printed p.61 "Rate & Fade for the
  Stack").

## The LCD

- 139 DisplayContent payloads, 91 with a change. Layout as in F86.
- New texts (OBSERVED; bytes in the cited seq):
  - "Memory page:1" / "Memory page:12" with "Bump 1-12 to change" (seq 17732, 18087);
  - "Live:14.0 Next: 1.0" / 0x86 "Mem:01/01 Mem:01/01" (seq 24967);
  - "Jump to step:> 1" / "Mem:01" (seq 25057);
  - "Record Memory Pg:1" / "Select Bump or Stack" (seq 26861);
  - "Edit Memory" / "Select Bump or Stack" (seq 27116);
  - "Record Sequence" / "Select Sequence" (seq 29110);
  - "Copy Memory/Seq" / "Select source" (seq 29266).
- **The default times read "U 5.0" / "D 5.0" in all 139 frames** (first seq 19). E3b read
  "U 5.1" / "D 5.1" (F102). The value changed back between the two sessions, by a cause UNKNOWN
  (wheel, power cycle, show load). SystemSettings is not fetched read-only, so its bytes are
  unknown for E8.

## Reproduction of earlier facts (first session of a host other than SmartSoft)

| Facts | Reproduced in E8? | Evidence |
|---|---|---|
| F51 transport | Bulk 0x04 / 0x83 on interface 1 only, every IN solicited. HID: our code never opened interface 0; no bus capture confirms it | Structure |
| F52 PC → console header | The console accepted 20 763 polls and 2 925 fetches built to F52 by our encoder (stale rule 20 762 of 20 762). This describes our encoder; it adds nothing about SmartSoft | Structure |
| F53 replies | 20 763 of 20 763 polls answered by one 12-byte header; type-3 echo 2 925 of 2 925; type 4 with a payload = DirtyObjects 2 470 of 2 470; each fetch answered on the next poll | Structure |
| F54 inbound layout, F55 inbound counter | byte 1 = 0 and LE length = payload − 5 in 5 395 of 5 395; +1 per payload, 0 breaks, 21 wraps | Structure |
| F56 reading without writes | The connect-time list on the first reply (89 records), fetched with polls and fetches only | seq 2, 3 |
| F57 lists | Format and list → fetch correspondence 2 925 of 2 925. **The no-de-duplication detail, which rested on E2 only, is reproduced** (ConsoleStatus × 39, Independents × 44) | seq 3 |
| F64 silent end | The session ended after an idle exchange; nothing closing was sent | seq 49845, 49846 |
| F67 body lengths | 8 ids, each with its F67 length | `payloads` |
| F82 FaderInfo layout, MASTER = body[194] | Third session | seq 5971 … 6197, 20846 … 20918 |
| F83 level mapping | Channel 5 / DMX 5; channels 1–14 / DMX 1–14 (`GROUP 2`); channels 7, 8 / DMX 7, 8; channels 49, 50 and DMX 47 (new) | above |
| F84 LED layout, page-1 memory map | Third session (seq 4103); page-12 map new (seq 18015) | above |
| F85 block 53 MASTER blink | Third session (seq 6165, 20850); steady blue for BLACKOUT new | above |
| F86 LCD layout | Third session; new texts | `lcd` |
| F87 / F98 body[0] = mode | Three more transitions, each with the LED change of blocks 1–24, 55 and 58 | seq 4095, 18142, 18314 |
| F89, H58 | The 157-list objects never arrived (no System write) | lists |
| **F95 BUMPS = body[193]** | **Second session** (E3b timed; E8 untimed, the owner saw the UI's BUMPS follow the panel) | seq 7158 … 7278 |
| F96 post-MASTER levels | Not exercised (no level up during the MASTER moves) | — |
| F97 bump output | Not exercised (no bump) | — |
| **F99 records 1–24 = physical faders** | **Second session for memory mode** (memory faders 1 and 2 = records 1 and 2); records 25–48 unchanged in every state; the 25-48 bank not exercised | seq 4168 … 4613 |
| **F100 block 52 = SOLO** | **Second session** for the pattern (`FF`×6 while on); identity still INFERRED (the owner saw the UI's SOLO, decoded from block 52, follow the panel) | seq 7377, 7437 |
| **F101 PlaybackInfo record layout** | **Second session** for the record structure: record 1 (a sequence) has the same Live / Next pair summing to 255, the same reset to `FF 00` and the same three u16 times counting down as record 5 in E3b's GO crossfade | seq 366, 415, 424, 17739 |
| F102 default times | The LCD reads 5.0 again (E3b: 5.1); SystemSettings not fetched | seq 19 |

## Hypotheses evaluated

| H | Verdict (OBSERVED at most) | Key evidence |
|---|---|---|
| **H66** console reports to a host without the System writes | **CONFIRMED in capture** (861 s, 2 469 lists, every reported action seen) | lists; owner |
| **H67** cadence tolerated | **CONFIRMED in capture for the cadence used** (idle 31–52 ms, fetch 28–47 ms after the list, poll 15–32 ms after a fetch); the floors were never reached | Cadence table |
| **H68** no string-descriptor reads needed | **UNTESTED** (no USBPcap); the first reply was normal, but the EP0 history is unknown | seq 1, 2 |
| **H62** FaderInfo byte 1 = memory-bump component | **REJECTED**: memory faders 1 and 2 moved bytes 0 and 1 together without a bump, BUMPS at 0 | seq 4168 … 4258, 4307 … 4582 |
| **H63** PlaybackInfo bytes 0–1 = step position | **CONFIRMED in capture for record 1**: the 0-based step index of a running 7-step sequence (207 changes in order). Record 5: 01 → 00 when STACK loads the stack at step 0 (manual), consistent | seq 51 … 17739, 24963 |
| **H26** ChannelLevels body[49], [50] = IND levels | **Independents part CONFIRMED in capture** (6 + 6 changes in step with the Independents object and LED blocks 72 / 73) | seq 6308, 6429 |
| H11 channels 49 / 50 = Independents | **Channel part supported**; the DMX 97 / 98 default does not apply to this show (channel 50 → DMX 47, channel 49 unpatched, F70) | seq 6312 |
| H44 Independents fields | **a = on state supported** (0 / 1 with the keys); b = 0xFF = the level read in ChannelLevels; c, d unchanged | seq 6300, 6421 |
| H25 qualifier a = page, b = fader | **Physical meaning supported** for a second page: page 12's view (LCD "Memory page:12", body[2] = 0x0B) shows faders 1–7 recorded = Memory (11, 0–6) | seq 18003, 18015, 18087 |
| H50 PlaybackInfo | Pair layout reproduced in record 1; **"records 1–4 = the page's sequence faders" supported for record 1** (a 7-step sequence whose times follow the page) | seq 18011, 18110 |
| H51 System page select | **UNTESTED** (a PC → console message). New: a console-side page change shows in ConsoleStatus body[2] and FaderInfo byte 3, and no System record is listed | seq 18003 |
| H53 ConsoleStatus | Extended: **body[2] = memory page − 1** (one change and back); body[0] three more transitions | seq 18003, 18102 |
| H55 sequence slots | **Extension to blocks 22–24 supported**: REC SEQ makes 22–24 blink in bright yellow as recordable sequence slots (owner: "only they can store the sequence") | seq 29114 |
| H56 indicator LEDs | **Block 53 steady blue (`00 00 FF 00 00 FF`) while BLACKOUT is active**, three times, over the MASTER blink; the other identities go to H72 | seq 5820, 7483, 20754 |
| H61 records / blocks 25–48 unused | Supported again (no change in any E8 state, page 12 included) | `census` |
| H65 connect-time list | Supported in part: 89 records of live objects with repeats, i.e. what changed and was not yet delivered. The power-on part is untestable (no power-cycle record) | seq 3 |
| H58 | Supported (live objects only when listed) | lists |
| H5 / H24 | Mirroring extended (BLACKOUT, IND keys, page, STACK, record functions); no ConsoleEvent (ID 20) in either direction | — |
| H31 | No information (E8's first pseq 0x7B; the console's history since E3b is unknown) | seq 3 |
| H45, H52, H54, H57, H60, H64 | Not exercised | — |

## Golden-test candidates (E8, by JSONL seq; decoders only)

**The outbound transfers of E8 (polls, fetches) are our own encoder's output.** They record what
our code sent, but they cannot be golden evidence for that encoder: that would be circular. The
encoder's golden bytes stay SmartSoft's (E2 seq 6, 9, 10, 14). The inbound bytes below are the
console's and may be used by decoder tests.

| seq | Content |
|---|---|
| 2, 3 | Type-4 header `04 00 C4 01 …` and the 89-record connect list with repeats (pseq 0x7B, body 447 bytes) |
| 4478, 4463, 4467 | Memory fader 2 at full: FaderInfo record 2 `FF FF 00 00`; ChannelLevels and OutputLevels = the stored levels of `GROUP 2` |
| 18003, 18007 | ConsoleStatus `01 00 0B 00 00 01 00` (page 12); FaderInfo records 1–24 `00 00 00 0B`, record 21 byte 2 = 0 |
| 17732, 18087 | DisplayContent "Memory page:1" / "Memory page:12" with "Bump 1-12 to change" |
| 17728, 18083, 18015 | LedStatus: the page-select views of pages 1 and 12; the page-12 memory map |
| 5820, 6165 | LedStatus block 53 steady blue (BLACKOUT) and blinking `37 37 FF 0A 0A 0A` (MASTER below full) |
| 6300, 6304, 6308, 6312 | IND record 2 on: Independents body[43] = 1, LED block 73 G, ChannelLevels[50], OutputLevels[48] = DMX 47 |
| 6421, 6425, 6429 | IND record 1 on: Independents body[1] = 1, LED block 72 G, ChannelLevels[49] |
| 7202 | FaderInfo BUMPS body[193] = 0xFF |
| 7377 | LedStatus block 52 `FF`×6 (SOLO) |
| 24963, 24967, 24971, 25057 | STACK: PlaybackInfo record 5 bytes 0–1 = 00; LCD "Live:14.0 Next: 1.0"; LED block 60 full yellow; LCD "Jump to step:> 1" |
| 26861 / 26865, 27116 / 27120, 29110 / 29114, 29266 / 29270 | The four record-function screens (LCD and LED) |
| 366, 415, 424, 17739 | PlaybackInfo record 1: step 6 mid-fade, a fade's end (times 0), the next step's start (step 0), the stop with `FF 00` at step 3 |
| 24706, 24722 | FaderInfo record 1 `FF FF` → `00 00` in one step; LED block 1 blinking `46 00 00 0A 00 00` |

## For the curator (proposed; nothing applied here)

**Status changes.**

| Id | Current | Proposed | Evidence |
|---|---|---|---|
| H66 | HYPOTHESIS (untested) | **CONFIRMED (OBSERVED, one session)** | E8 lists and objects throughout 861 s without any System write; "H66, H67, H68" section |
| H67 | HYPOTHESIS (untested) | **CONFIRMED (OBSERVED, one session) for idle 31–52 ms, fetch 28–47 ms after its list, poll 15–32 ms after a fetch**; the floors 21 / 11 / 21 ms stay untested | Cadence table |
| H68 | HYPOTHESIS (untested) | **unchanged (UNTESTED)**: no USBPcap ran | owner statement |
| H62 | HYPOTHESIS | **REJECTED** (successor H71) | seq 4168, 4195, 4239, 4258 (memory fader 1), 4307 … 4582 (memory fader 2), BUMPS 0 |
| H63 | HYPOTHESIS | **CONFIRMED in capture for record 1 (OBSERVED once)**; record 5 consistent | seq 51 … 17739 (207 step changes), 24963 |
| H26 | Length part CONFIRMED, Independents part UNTESTED | **Independents part CONFIRMED (OBSERVED once)** | seq 6308, 6429 and the IND table |
| H11 | OPEN | Channel part supported (OBSERVED); "default DMX 97 / 98" not this show's patch (F70, seq 6312) | IND table |
| H44 | OPEN (supported) | Byte a = on state supported (OBSERVED once); b, c, d unchanged | seq 6300, 6421 |
| H25 | Qualifier mapping CONFIRMED, physical meaning OPEN | Physical meaning supported (OBSERVED once, page 12); the E6 test stays the decisive one | seq 18003, 18015, 18087 |
| H50 | Layout CONFIRMED (E3b) | Add: record 1 = a sequence playback record; "records 1–4 = sequence faders of the page" supported for record 1 | PlaybackInfo section |
| H53 | HYPOTHESIS, strongly supported | Extend with body[2] = memory page − 1 (OBSERVED once) | seq 18003, 18102 |
| H55 | CONFIRMED for block 21 | Extension to blocks 22–24 supported (OBSERVED, REC SEQ 6 of 6; owner) | seq 29114 |
| H56 | Block 53 supported; others partly | Add: block 53 `00 00 FF 00 00 FF` steady while BLACKOUT is active, over the MASTER blink (OBSERVED, 3 times) | seq 5820, 6165, 7483, 20754 |
| H61 | HYPOTHESIS | Supported again (no change of records / blocks 25–48 in any E8 state) | `census` |
| H65 | HYPOTHESIS | Annotate: E8's 89-record list with repeats (power-on history unknown) | seq 3 |
| H51 | UNTESTED | Unchanged; annotate: no System record at a console-side page change | lists at 242.370 |
| F57 (no-de-duplication detail) | VERIFIED except that detail (E2 only) | **VERIFIED for the detail too** (E2 + E8) | seq 3 |
| F95 | OBSERVED | **VERIFIED** (E3b + E8; E8 without a timed log) | seq 7158 … 7278 |
| F100 | OBSERVED (pattern); INFERRED (SOLO) | **VERIFIED in part** (block 52 `FF`×6 / `0A`×6 toggles with the SOLO key, second session); identity INFERRED | seq 7377, 7437 |
| F99 | OBSERVED | **VERIFIED in part** (memory mode: physical fader k = record k; records 25–48 unchanged); the bank part stays OBSERVED | seq 4168 … 4613 |
| F101 | OBSERVED | **VERIFIED in part** (the 16-byte record structure: pair summing to 255, reset `FF 00`, three u16 times counting down; E3b record 5, E8 record 1) | seq 366 … 17739 |
| F51–F55, F57 | VERIFIED | No change; annotate "E8: reproduced by our own host, the first evidence channel other than SmartSoft" | Session health |
| F84, F85, F86, F87 | VERIFIED in part | No change; annotate the third session and the new values (page-12 map, BLACKOUT blue, new LCD texts, body[2]) | above |
| F102 | OBSERVED | Annotate: the LCD reads "U 5.0 / D 5.0" in E8 (139 of 139 frames); cause unknown | seq 19 |
| F108 | DOCUMENTED | Annotate: this show does not use the default (IND 2 = channel 50 → DMX 47, IND 1 = channel 49 unpatched; F70, E8) | seq 6312 |

**New facts (proposed IDs; all OBSERVED, one session, unless stated):**

- **F110 Read-only session of our software (E8).** 861 s. Our host sent only polls and listed
  fetches, never a System write.
  - The console answered 20 763 of 20 763 polls (15 368 idle, 2 470 lists, 2 925 fetched
    objects).
  - Lists named 12, 13, 14, 15, 17, 21, 22, 23, all fetched in order; 5 395 payloads with an
    unbroken counter; 0 framing errors; no fault; stopped at an idle point.
  - Cadence: idle 31–52 ms, fetch 28–47 ms after its list, poll 15–32 ms after a fetch, all
    above the SC-1 floors.
  - The console kept reporting every front-panel change (H66, H67).
- **F111 A long connect-time list with repeats.** The first reply carried 89 records: ConsoleStatus
  × 39, Independents × 44, one each of LedStatus, DisplayContent, FaderInfo, PlaybackInfo,
  ChannelLevels, OutputLevels. Each record was fetched in order; the bodies were identical within
  an id (seq 3).
- **F112 Memory faders.** In memory mode physical fader k is FaderInfo record k with bytes 0 = 1 =
  the fader's level (26 of 26). ChannelLevels / OutputLevels carry the memory's stored channel
  levels scaled by the fader (`GROUP 2` at full: 48 of 48 channel bytes). LED block k is full red
  while the fader is up (seq 4168 … 4613, 14974 … 15164).
- **F113 Memory page.**
  - ConsoleStatus body[2] = page − 1 (0x0B with the LCD's "Memory page:12").
  - FaderInfo records 1–24 byte 3 = page − 1; record 21 byte 2 = 1 on page 1, 0 on page 12.
  - The MEMS-held page view: blocks 1–12 = pages, current page steady full red, pages with
    content blinking `C8/0A`, matching the manual p.57 (DOCUMENTED); LCD "Memory page:n / Bump
    1-12 to change".
  - The page-12 map shows faders 1–7 recorded (seq 17728 … 18121).
- **F114 BLACKOUT LED.** LED block 53 = `00 00 FF 00 00 FF` (steady blue) three times, read as
  BLACKOUT active (INFERRED; manual: "As soon as it is activated the button will light up").
  - It masks the MASTER blink: steady while the MASTER stood at 0x88, then the blink once
    BLACKOUT was off.
  - No other object was listed; the effect on level objects is untested (seq 5820 … 7529,
    20754 … 20929).
- **F115 Independents live state.** The Independents record byte 0 (body[1], body[43]) is 0 / 1
  with the key. ChannelLevels body[49] / [50] = 0x00 / 0xFF and LED blocks 72 / 73 G
  `0A` / `FF` follow it. OutputLevels follows the show's patch: DMX 47 for channel 50, nothing for
  channel 49 (6 + 6 changes; seq 6300 … 6895, 22996 … 23108). Key identities INFERRED (H72).
- **F116 Record functions.**
  - The LCD texts "Record Memory Pg:1", "Edit Memory", "Record Sequence", "Copy Memory/Seq" and
    their LED states are identical in 9 / 8 / 6 / 5 screens.
  - Each screen is opened and closed by one LedStatus payload.
  - Blinking is carried as phase A ≠ phase B, never as alternating payloads (seq 26861 …
    43882). Key identities INFERRED (H72).
- **F117 STACK.**
  - LED block 60 is `46 46 00` (dim yellow), `FF FF 00` at the STACK press (with the LCD "Live:14.0
    Next: 1.0") and `C8 C8 00 / 0A 0A 00` blinking in REC MEM and REC SEQ.
  - PlaybackInfo record 5 bytes 0–1 changed `01 01` → `00 00` at the press (manual: "loaded …
    at step 0", DOCUMENTED).
  - The LCD read "Jump to step:> 1 / Mem:01" before the stack went inactive (seq 24963 … 25068).
- **F118 A running sequence in PlaybackInfo record 1.**
  - Step index (bytes 0–1) 0 … 6 cyclic, 207 changes, about 1.125 s per step.
  - Live / Next summing to 255 in 2 340 of 2 340; times 1.0 s counting to 0 in every step.
  - Listed about every 93 ms by our loop for 232 s; it was running before our first poll and
    stopped at 237.519 s with `FF 00` at step 3.
  - Identification with `$SEQUENCE 0` "KOTLIK" (7 steps of UP / DOWN 1.0 s) INFERRED (seq 51 …
    17739).
- **F119 Listed is not changed.** 48 of 139 DisplayContent, 82 of 2 340 PlaybackInfo and 20
  ConsoleStatus payloads after the connect list equal their predecessor. ConsoleStatus is
  re-listed unchanged at every EDIT MEM exit and COPY entry (13 times) and around the page-select
  view (7 times).
- **F120 A memory level zeroed with the fader up.** At 372.420 s memory fader 1's record went
  `FF FF` → `00 00` in one step together with its channels. Its bump LED blinked `46/0A` for 300 s
  and turned steady without a FaderInfo change at 672.345 s. CLEAR is INFERRED as the cause (H73).

**New hypotheses (proposed IDs):**

| H | Hypothesis | Evidence | Test |
|---|---|---|---|
| H69 | FaderInfo record byte 3 (body[4N]) is the page (0-based) of the content that physical fader N controls: per fader, so a fader held above zero across a page change keeps its old page until brought to zero (manual p.57, F107), while ConsoleStatus body[2] is the selected page | E8: all 24 records 0x0B on page 12 with all faders at zero (seq 18007) | Passive, logged: memory fader 1 at full, change to page 12, then bring fader 1 to zero. Predictions of the hypothesis, not observations: record 1 byte 3 stays 0x00 until the fader reaches zero, records 2–24 read 0x0B at once |
| H70 | FaderInfo record byte 2 = 1 when the fader's slot on its page holds a sequence | E8: record 21 byte 2 = 1 on page 1 (KOTLIK on fader 21), 0 on page 12 (seq 18007, 18106); E2–E3b constant 1 on page 1 (F82). Record 25's constant 1 is unexplained | Passive: a page whose sequence sits on fader 22, 23 or 24 (or record one there: owner decision, it changes show data, backup first); expect byte 2 of that record = 1 |
| H71 | *(successor of the REJECTED H62)* In memory mode FaderInfo record bytes 0 and 1 both carry the memory fader's level; in channel mode byte 1 is 0. What distinguishes them (e.g. fader output versus content level, or one of them post-MASTER) is UNKNOWN | E8: 26 of 26 equal (memory faders 1, 2), 0 of 15 non-zero in channel mode; E3b: `FF FF` with a held memory bump at BUMPS 0xFF (F97) | Passive, logged: memory fader 1 at about 50 % with the MASTER at half; then a memory bump on fader 2 with BUMPS at about 50 % while fader 2 stands at 25 %. A difference between bytes 0 and 1 in either case separates them |
| H72 | LedStatus blocks 57 = CLEAR, 60 = STACK, 63 = COPY, 64 = REC SEQ, 65 = MAGIC, 66 = REC MEM, 67 = EDIT MEM, 72 = IND 1, 73 = IND 2; active keys lit steadily, selectable keys blinking (manual p.11) | E8: each block changes only in its function (9 / 8 / 6 / 5 identical screens) or with its key; owner: CLEAR flashes with every record key, STACK with REC SEQ and REC MEM, MAGIC with REC MEM | Owner, zero risk: press each key on the console and say which key LEDs light or blink; a logged passive session confirms the block per key. The owner's "REC MEM / REC SEQ / COPY flash" against the steady 66 / 64 / 63 is the first point to check |
| H73 | CLEAR, pressed three times, sets the memory faders' levels to zero (FaderInfo bytes 0 and 1, the channels) while the faders stay up; the bump LED then blinks dim red until the fader is brought to zero | E8 seq 24706 … 24722 and 40108; manual pp.11, 30, 56 (F8, F109); the owner's "CLEAR works" | Passive, logged: CLEAR × 1, × 2, × 3 and × 4, each with one channel fader and one memory fader up; note which objects change per press |
| H74 | PlaybackInfo record bytes 6–7 and 8–9 (`01 00 01 00`) and the Sequence / SequenceMain header fields d, e (256, 256) are the Rate and Fade overrides as u16 BE with 256 = 100 % | E8: `00 00 01 00 01 00` at bytes 4–9 of all five records in 2 340 payloads; F74 headers b..e = 0, 0, 256, 256; other SD shows have e = 54 (asc-format.md); manual printed p.61 "Rate & Fade for the Stack" | Passive, owner decision: RATE + STACK, change the Fade % with the wheel, capture; prediction of the hypothesis, not an observation: record 5 bytes 6–9 change. Reset afterwards as the manual describes ("Reset Rate & Fade", printed p.61) |

## Surprises and anomalies

1. **A sequence was running before the session started** (PlaybackInfo pending at our first poll),
   and it stopped at 237.519 s, at the page-select view. The owner did not mention it.
   PlaybackInfo was named in 2 340 of the 2 470 lists, and 2 261 of them named nothing else. Our
   session cannot have started it: the session sends nothing that could.
2. **The connect-time list held 89 records** with heavy repetition. A pending list's size depends
   on the console's history, so the 37 / 101 / 165-record sizes of SC-1's accepted leftover
   (a zero-length packet after a 64-byte multiple) are reachable in practice.
3. **Runbook deviations:**
   - no USBPcap (H68 untestable);
   - no action log;
   - no named backup copied (no SHOW06.ASC in `smartfade-sd-card-backup/`);
   - the planned actions were replaced by free exploration, including a page change, record
     functions (REC MEM, EDIT MEM, REC SEQ, COPY) and STACK.
   The record functions store show data if a bump is pressed in them (F107).
   - After every one of the 28 screens the page-1 LED map returned to the map before it
     (recorded 1–8 and 13, sequence on 21, the others empty), so no new memory or sequence
     appeared on page 1 (INFERRED).
   - An edit of existing content would not show in that map, and no Memory object can be fetched
     read-only to check it.
   - Ask the owner, and save a backup before the next session.
4. **`hsp_summary.py` `summary` (after its H20 header lines) and `timeline` fail on the backend's
   log** (pcap frame `None`). The SC-1 re-check fixed only the loader and `log`.
5. **The default times read 5.0 again** (E3b: 5.1). Something changed them between the sessions.
6. **Our cadence was bimodal and slower than SmartSoft's** (INFERRED: timer granularity), within
   the clearance. The maximum reply-header latency, 21.57 ms (seq 17959, during the page-select
   view), is above SmartSoft's maxima (8.02 / 11.62 ms) and far below the 500-ms timeout.
7. **The firmware was not re-read.** The log header carries none, because a read-only session
   cannot fetch ConsoleVersion.

## Next capture to take

**A. Owner, zero risk:**
1. Which key LEDs light or blink when you press CLEAR, STACK, COPY, REC SEQ, MAGIC, REC MEM, EDIT
   MEM, IND 1, IND 2? Do REC MEM, REC SEQ and COPY blink, or stay lit (H72)? Which IND key did
   you press first at about 16:26:52?
2. Was a sequence running on memory fader 21 before you connected, and how was it started?
   Did holding MEMS stop it at about 16:29:26 (237.5 s)?
3. Did you press CLEAR three times at about 16:31:40 (372.4 s) with memory fader 1 up, and pull
   fader 1 down at about 16:36:40 (672.3 s) (H73)?
4. Did any record function store or change anything (a bump pressed inside REC MEM, EDIT MEM, REC
   SEQ or COPY)? Was the console power-cycled since E3b? How did the default times return to 5.0?
   What is the name of the backup taken before E8?
5. The console's firmware from Menu > Diagnostics, for the record.

**B. E8b, a second read-only session of our software** (the same SC-1 conditions, this time all of
them).
- Setup:
  - a named SD backup copied into `smartfade-sd-card-backup/`;
  - USBPcap from before the Zadig bind;
  - `action-log.ps1` with a line per action;
  - actions at least 5 s apart.
- Each item turns an E8 reading into a second-session observation (VERIFIED needs the
  reproduction). Each needs logged times.
  1. **Repeat E8's readings with times:**
     - memory faders 1 and 2 (F112);
     - a page change to 12 and back (F113);
     - BLACKOUT on and off (F114);
     - IND 1, then IND 2, named (F115, H72);
     - each record key entered and left with CLEAR only, one at a time (F116, H72); no bump inside
       a record function, since that would record;
     - STACK pressed, held and released (F117).
  2. **New tests:**
     - BLACKOUT with channel fader 5 at full (does any level object show the blackout?);
     - a page change with memory fader 1 at full (H69);
     - memory fader 1 at 50 % with the MASTER at half, and a bump at BUMPS about 50 % (H71);
     - CLEAR × 1 to × 4 with a channel and a memory fader up (H73);
     - start and stop the sequence on fader 21 (F118, H50);
     - GO with STACK lit on a safe stack (H63 record 5, H64; optional, owner decision, safe rig).
  3. **The power-on list (H65):** power on, touch nothing, connect; then disconnect and connect
     again (the read-only analogue of F94).
- **What the USBPcap capture adds:**
  - **H68:** EP0 at the Zadig bind, at the backend's start and at Connect, i.e. whether Windows or
    WinUSB read string descriptors before our first poll.
  - USBD status for every transfer, which the backend's log does not have.
  - A frame-by-frame cross-check of the backend's log against the bus, as E3b's tshark
    cross-check did for the normalizer. That would verify `TransferLog` itself.

**C. Tooling (zero risk):** make `hsp_summary.py`'s `summary` and `timeline` modes accept a log
without pcap frames (print the seq instead), so that the E8 figures can be reproduced with the
committed tool.

**D. Not E8's business, recorded for completeness:** E9, the passive USBPcap capture of SmartSoft
writing (CLAUDE.md), is the only path to a write mode. Nothing in E8 clears any other message:
SC-1's clearance (poll and listed fetch, read-only) is unchanged.

## Owner answers to section A (2026-10-30, evening; statements file)

Owner statements never promote by themselves; they are matched against the data here.

1. **Key LEDs:** the owner's account, key by key, equals the block readings of H72 in every item:
   CLEAR lit always and blinking as the cancel key in COPY, REC SEQ, REC MEM and EDIT MEM (block 57);
   STACK lit when active and blinking in REC SEQ and REC MEM (block 60); MAGIC blinking in REC MEM
   only (block 65); COPY, REC SEQ and REC MEM **lit steadily** when on and EDIT MEM **blinking** when
   on (blocks 63, 64, 66, 67), which settles the earlier "they flash" in favour of the data; IND 1 and
   IND 2 lit when on (blocks 72, 73). The memory keys: REC MEM blinks the stored ones low and the
   empty ones high, COPY and EDIT MEM blink the stored ones, REC SEQ blinks 21–24; a slot holding a
   sequence does not count as stored for REC MEM, EDIT MEM and COPY (block 21: bright blink in REC
   MEM, steady dim in EDIT MEM). H72 stays INFERRED until a logged press ties a key to its block (E8b).
2. **The sequence:** the owner first wrote "page 12", then corrected himself the same evening: the
   sequence sits in **memory 21 of page 1** and its seven steps **use memories 1–7 of page 12**. That
   agrees with the data (block 21 yellow on page 1's map, record 21 byte 2 = 1 on page 1, H70) and
   explains page 12's map (faders 1–7 recorded = Memory (11, 0–6), the step memories) and the Sequence
   object's seven steps; no discrepancy remains. Whether it was running before the connection is
   unresolved ("maybe I was playing with it"); holding MEMS does **not** stop a sequence, so the stop
   at 237.519 s has another cause (UNKNOWN).
3. **CLEAR:** "I think yes" to three presses with memory fader 1 up and the later pull-down: supports
   H73 and F120's reading.
4. **"Yes to all":** a record function **did store or change something** during E8, so the show
   differs from E3b's in an unknown way (no show object is fetched read-only; the page-1 LED map was
   unchanged, so the change is an edit of existing content, another page, or the stack / a sequence);
   the console **was power-cycled** since E3b; the default times **did** return to 5.0 (cause still
   UNKNOWN). A named backup before E8b and E9 is therefore required, not optional.
5. **Firmware:** 3.0.2.9.0.43, the third reading (F48); unchanged.
