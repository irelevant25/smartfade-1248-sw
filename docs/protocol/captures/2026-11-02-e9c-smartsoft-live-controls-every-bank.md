# 2026-11-02 E9c: SmartSoft's Console Simulator driving the live controls in every bank, the matching rule, held bumps and the keys seen once (USBPcap, passive, guided script)

- **Date:** 2026-11-02, 00:41–01:20 local time (CET, UTC+1). USBPcap ran 00:43:33.175–01:19:11.965 (the raw root-hub
  capture's first and last frames); the committed console-only split runs from 00:43:33.175462 (f1, the injected descriptors)
  to 01:19:03.243 (f259738). The guided script `tools/usb-probe/e9c-guided-session.ps1` (f87cb0e) ran 00:41:42.119–01:19:39.938,
  started SmartSoft twice (00:44:58.722, 01:13:08.573) and logged every step; SmartSoft was closed at 01:12:52–01:12:57 and
  01:18:59–01:19:05.
- **Experiment:** **E9c**, the passive capture SC-2's "Path to a wider clearance" names, scoped on 2026-11-02 for the owner's
  decision to drive memory faders and channels 25-48 from the PC through an automatic bank switch, with held bumps
  ([../reverse-engineering.md](../reverse-engineering.md) "E9c runbook"). SmartSoft Online, its Console Simulator driven by the
  owner; **our own software was not involved and sent nothing;** every write in this capture is SmartSoft's, made as for any
  user. **E9c clears nothing.** Everything first seen here is OBSERVED (one session); what repeats E9, E9b or E12b is proposed
  VERIFIED as SmartSoft's and the console's behaviour, never as clearance to send.
- **Console:** ETC SmartFade 1248, USB device address **38** on bus 1 (as in E11 attempts 2 and 3: no re-enumeration since; the
  owner is unsure whether the console was switched off since E11). Firmware: ConsoleVersion "3.0.2.9.0.43" in both sessions.
  The inbound counter stood at 25 at the first payload (f34; not a power-on, F80 / H31).
- **Host:** the owner's PC, Windows 11 Pro build 26300; Memory Integrity **off** (read by the script); interface 1 on ETC's
  driver, service **ETCHSP** (read by the script); SmartSoft 3.0.43 (package 3.0.2.9.0.43b), started by the script. Four
  non-zero USBD statuses at the start (seq 5–8: two interrupt reads on the HID interface's EP 0x81 and two EP0 controls,
  0xC0010000 / 0xC0000004, at the driver's bind, before any bulk transfer; the console's bulk pipes: 0 errors). SmartSoft never
  used the HID interface (F63 holds).
- **Connection:** USB A-B cable straight into the PC. USBPcapCMD on root hub `USBPcap1` with `-A --inject-descriptors` into
  `e9c-20261102-0042.pcap` (the script's suggested name). Other devices in the raw file: a hub, the owner's keyboard, an
  isochronous device with most of the 1.37 GB, and a bulk device at address 41 (not the console).
- **Show backup (safety rule 3): taken.** "backup taken: SHOW24.ASC" (00:41:58), before USBPcap and SmartSoft. After the session
  `SHOW25.ASC` (01:19:39). Both copied into the git-ignored `smartfade-sd-card-backup/`: **byte-identical** (15 944 bytes, SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, the same bytes as `SHOW23.ASC` and the E11 files): **no
  show data changed**, as planned (no record function, no edit).
- **Starting position** (script; owner 00:43:55): channel mode 1-24, every channel fader at zero, MASTER and BUMPS at full,
  BLACKOUT, SOLO, IND 1 / 2, STACK and NEXT off, page 1, nothing armed; memory 1 of page 1 recorded (channels 7 and 8), memory 2
  recorded, memory 9 empty, memory 1 of page 2 empty (owner, 00:44:20–00:44:53). The Simulator has PAUSE, NEXT and RATE keys
  (owner, 00:42:45–00:42:54; RATE not pressed).
- **Rig:** the DOTZ BAR on DMX 5–16 (one three-cell fixture driven from channel 12, owner 01:11:22); nothing on DMX IN.
- **Action log:** the guided script's timed log (184 lines, CRLF), PC clock, local time; the owner's answers to the open E11
  questions open it. Stamp semantics as in E9b: a `Step` line when the owner presses Enter at the start of an action, a
  `Confirm-Done` line after it, a `wait … done` line after each wait.
- **Owner statements** (`2026-11-02-e9c-owner-statements.txt`): the typed answers with readings, and the chat statement about
  S4b (the physical fader step first done in the Simulator, then corrected).

**Conventions:** `seqN` is the record number in the normalized JSONL (`2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl`),
`fN` a frame of the committed `.pcap` split; `t` is seconds since the split's first frame (00:43:33.175 local; local time =
00:43:33.175 + t; the action log's stamps are merged as notes, so a step's `t` is its stamp). `pseq` is a payload's sequence byte
(outbound: SmartSoft's counter from 0x00 per session). `body[n]`, LED block k = body[1 + 6(k − 1) .. 6k] (phase A RGB, phase B
RGB), FaderInfo record N = body[1 + 4(N − 1) .. 4N], ChannelLevels[c] = body[c], OutputLevels DMX d = body[1 + d], LCD cells as
in the E9 record. ConsoleEvent bodies are written `[kind][index or code][value][previous]`. Sessions: **S1** (seq 17 … 106 846,
111.2–1763.5 s), **S2** (seq 106 847 … 129 871, 1775.4–2130.1 s). Readings carry their status.

## Evidence files

| File | SHA-256 | Notes |
|---|---|---|
| `2026-11-02-e9c-smartsoft-live-controls-every-bank.pcap` | `32e3959348a1352fe036fe08151211283cbf05615493ba378da83ca827057117` | console-only split of device address 38 (`tshark -r <raw> -Y "usb.device_address == 38" -F pcap -w <split>`), 259 738 frames, 13 058 903 bytes |
| `2026-11-02-e9c-smartsoft-live-controls-every-bank.jsonl` | `1a52038c4efc45181a7aaf9e861fb5f6c521e0a04910707c4a642c997527cd00` | the split normalized (`usbpcap_to_jsonl.py` v2) with the action log merged as 185 notes: 129 871 transfers (65 966 bulk OUT, 63 888 bulk IN, 13 control, 4 interrupt); no multi-URB reads (ETC's driver reads whole transfers, unlike WinUSB in E11: `join_reads.py` joined 0) |
| `2026-11-02-e9c-actions.txt` | `4ce050b7e56600df4fcce7d1f330a8c13bfa32691c383a5986bb057909ce710a` | the guided script's log, 184 lines |
| `2026-11-02-e9c-owner-statements.txt` | (hash in `README.md`) | the owner's answers and his S4b statement, with readings |
| not committed | `cff84517339674b34924daaec84b29021e5a8c80391f00830d0601a93f457b20` | the raw root-hub capture `e9c-20261102-0042.pcap`, 1 367 954 545 bytes, 1 461 898 frames (other devices) |
| `smartfade-sd-card-backup/SHOW24.ASC`, `SHOW25.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` both | git-ignored; the backup and the after-session save, byte-identical |

Tools (read-only): `hsp_summary.py` (summary, timeline), `write_rules.py`, `e11/writes.py`, `e9c/steps.py` (every step's writes
and the console's answers), `join_reads.py`.

## Session health

| Check | Result |
|---|---|
| Framing | 0 errors over 129 871 transfers; H15 stale words 63 096 of 63 096; H16 type-3 echoes 1 642 of 1 642; H17 the inbound counter 25 … 250 with seven wraps and no break |
| Writes | **1 227 Data writes: 4 System (the pair, twice) and 1 223 ConsoleEvents** (1 149 fader events, 37 key press / release pairs); the header's w2..w5 the most recent reply's 1 227 of 1 227 (F52); the counter from 0x00 in each session (F131); every fader event's previous byte the last wire value of its index (1 149 of 1 149, F132); no write between a press and its release |
| Placement (`write_rules.py`) | After an idle reply 285, after a list reply 225, after a fetched object 717 (757 with listed records still unfetched; SmartSoft's own, looser placement, as E9 / E9b); poll submit → write header 29.4–49.3 ms; the last inbound completion → header 21.2–48.6 ms; payload → next poll or fetch 12.7–18.7 ms; header to header ≥ 44.8 ms (median 62.9); press → release 45.8 ms … 2.85 s |
| Cadence | 61 870 polls, idle poll-to-poll median 31.6 ms (as E2 / E9b); 1 642 fetches; 376 lists; one gap of 13.4 s between bulk transfers (SmartSoft's restart, 1763.5–1776.9) |
| USBD status | 4 non-zero, all at the driver's bind before the first bulk transfer (above); 0 on the bulk pipes |

## The connect phases (SmartSoft's tenth and eleventh sessions with the System pair)

Both sessions began as every SmartSoft session since E2 (F58): System `09 00 00` (pseq 0; S1 t = 112.860, S2 1776.763) answered by
a one-record list naming ConsoleVersion, then System `07 00 00` (pseq 1; 114.143, 1778.049) answered by the show download. **The
download listed 158 records** in both sessions (seq 233, seq 107 413): Memory ×17, Sequence (2) ×1, SequenceMain ×1, StepContent
(4) ×19, Patch, Page (8) ×12, ChannelText ×96, Independents, OutputLevels, ChannelLevels, LedStatus, ConsoleStatus, FileName,
SystemSizes, System ×3 ((0, 0, 0), (0, 6, 0), (0, 1, 0)), SystemSettings: the same ids as E2's and E3's 157-record list (F89), one
record more (which one is not identified here; the show is byte-identical to the one E12b left). As in E2–E12b the download holds
no FaderInfo, DisplayContent or PlaybackInfo (F89).

**The list pending at S1's first poll (t = 111.208, seq 19, before any write): 24 records** naming ConsoleStatus ×19, LedStatus,
DisplayContent, FaderInfo, PlaybackInfo, OutputLevels and ChannelLevels: what changed on the console since our backend's last
session fetched it (2026-11-01, 23:10: the owner's mode switches and fader moves of the setup, 19 ConsoleStatus changes among
them), with no power cycle (the counter did not restart). The owner also states that the console was **not** power-cycled
between E11's attempts 2 and 3 (log 00:42:03), when the pair LedStatus + DisplayContent was pending at attempt 3's Connect: the
pending list names what changed since a host last fetched it, after a power-on or not (H65 annotated). S2's first poll
(1776.0) found nothing pending: S1 had fetched everything and nothing moved between the sessions.

## S1: every channel fader of bank 1-24 (the Simulator's faders 2 to 24)

Each Simulator fader was dragged to about half (0x85–0xA5), held and brought back; 15–51 fader events per fader (median 23),
one list of the five live objects per two to four events. **The index was the fader number − 1 for all 23 faders**, and the
console answered each with the objects of that physical fader:

| Simulator fader N | Index | FaderInfo record N (body[1 + 4(N − 1)]) | ChannelLevels[N] | OutputLevels DMX N (body[1 + N]) | LED block N (body[2 + 6(N − 1)], [5 + 6(N − 1)]) | LCD cell |
|---|---|---|---|---|---|---|
| 2 | 0x01 | body[5] 00 → 0F … 8A … 00 (seq 9 217 …) | body[2] | body[3] | body[8], [11] | body[4] |
| 3 | 0x02 | body[9] | body[3] | body[4] | body[14], [17] | body[5] |
| 4 … 12 | 0x03 … 0x0B | body[13] … body[45] | body[4] … [12] | body[5] … [13] | blocks 4 … 12 | body[6] … [14] |
| 13 … 24 | 0x0C … 0x17 | body[49] … body[93] | body[13] … [24] | body[14] … [25] | blocks 13 … 24 | line 2: body[23] … [34] |

Fader 24 also moved OutputLevels body[98] (DMX 97) with DMX 24: the show's patch has DMX 97 on channel 24 too (OBSERVED; the
patch, not the protocol). With fader 1 (0x00, VERIFIED since E9b) and this session, **every index 0x00–0x17 is now observed in
bank 1-24**, each with the objects the layouts of `responses.md` predict for that fader (F82–F84, F86); 0x04 and 0x06 are now
seen in two sessions (E9b / E9 and E9c), 0x01–0x03, 0x05 and 0x07–0x17 once. "Index = fader − 1" rests on 24 points.

## S2: bank 25-48 (with a deviation)

The Simulator's FADERS 25-48 (code **0x37**, seq 24 522, t = 475.137, the second observation after E9b): ConsoleStatus body[4]
0 → 1 (seq 24 535), LED block 55 off and 56 on (body[326], [329] FF → 0A; [332], [335] 0A → FF), the LCD row labels " 1" / "13"
→ "25" / "37" (body[1], [2], [21], [22]), as the panel's key (F98). Then:

| Simulator control, bank 25-48 | Index / code | The console's answer | Reading |
|---|---|---|---|
| fader 1, 0 → 0x9D → 0 (20 events, 489.3–490.9) | **0x00** | FaderInfo record 1 (body[1]), **ChannelLevels[25]**, **OutputLevels DMX 25** (body[26]), LED block 1, LCD cell body[3] | the same index as channel fader 1; the console applied it to channel 25 (H111) |
| fader 2 (27 events, 496.4–498.9) | **0x01** | record 2 (body[5]), ChannelLevels[26], DMX 26 (body[27]), block 2, cell body[4] | likewise, channel 26 |

**Deviation (owner, 00:54:08):** in bank 25-48 the Simulator labels its faders 25–48, so the owner found no "fader 24", pressed
FADERS 1-24 (code 0x36, t = 531.389: ConsoleStatus body[4] 1 → 0, blocks 55 / 56, the LCD rows back) and did the remaining two steps
in bank 1-24: fader 24 (index 0x17 → channel 24, DMX 24 and 97, record 24, block 24) and **bump 1 held 2.54 s** (code 0x00:
ChannelLevels[1], DMX 1, FaderInfo record 1 and block 1 at 0xFF from the press to the release, the LCD glyph 0x86; the first
PC-held channel bump 1). **A bump in bank 25-48 was not captured** (E9d). The script's later "FADERS 1-24" step (639.6 s) sent
nothing: the console was already there.

## S3: memory mode (page 1 and page 2)

FADERS MEMS (0x39, 662.066): ConsoleStatus body[0] 0 → 1, the memory LED map (`46` recorded, `0A` empty), DisplayContent. Then:

| Simulator control, memory mode | Index / code | The console's answer | Reading |
|---|---|---|---|
| fader 1 (memory 1 of page 1, channels 7 and 8), 0 → 0x8F → 0 (31 events) | **0x00** | FaderInfo record 1 **bytes 0 and 1** (body[1], [2] = the memory fader's level, F112), block 1 46 → FF, OutputLevels DMX 7, 8 (body[8], [9]), ChannelLevels[7], [8], LCD cells 9, 10 | the index of physical fader 1 moved memory fader 1 of the shown page |
| fader 2 (memory 2) | 0x01 | record 2 bytes 0 and 1 (body[5], [6]), block 2, ten or more channels and DMX addresses (memory 2's content), the LCD | memory 2 |
| fader 9 (memory 9, empty) | 0x08 | record 9 bytes 0 and 1 (body[33], [34]) only; **12 lists of FaderInfo alone**, no channel, no DMX, no LCD | an empty memory's fader moves the record and nothing else |
| memory bump 1 held 2.28 s | 0x00 | record 1 `FF FF`, block 1 FF, channels 7 and 8, DMX 7, 8, LCD 0x86 from the press to the release (seq 39 545 …) | as E9 and E9b (third session) |
| memory bump 9 tapped 160 ms (empty) | 0x08 | FaderInfo record 9 bytes 0, 1 00 → FF → 00 only | the bump of an empty memory shows in the record alone |
| page 2 on the CONSOLE (hold MEMS, bump 2; 748 s) | — (no write) | the LCD "Memory page:1" / "Bump 1-12 to change" then the page screen, ConsoleStatus body[2] 0 → 1 (seq 42 230), the page's LED map, **FaderInfo byte 3 of records 1–24 → 1** (H69), PlaybackInfo body[12], [14] 0A → 00 | the page select; the page is per fader (H69) |
| fader 1 (memory 1 of page 2, empty) | 0x00 | record 1 bytes 0, 1 only, 17 lists of FaderInfo alone | the shown page's memory, empty |
| page 1 on the console (835 s), FADERS 1-24 (0x36, 851.1) | | body[2] 1 → 0, byte 3 of the records → 0; body[0] 1 → 0, the channel-mode map | back to the start |

## S4: the matching rule with a PC level in force (the automatic switch's risk)

| Case | What was done | What the console did (OBSERVED once) |
|---|---|---|
| **S4a** a mode switch with a PC channel level | The Simulator's fader 1 to **0x87** (channel 1 at 0x87, physical fader 1 at zero; 873 s). FADERS MEMS (0x39, 892.8). | At the switch FaderInfo record 1 read **0x87 → 0x00** (seq 51 466; memory fader 1 at zero) and block 1 went `46 00 00 / 0A 00 00` (**blinking**: unmatched); memory 1's light did not come up (owner). The Simulator's drag of fader 1 in memory mode, 0x8A → 0xFF → 0x00 (56 events, 965.9–969.7): **no FaderInfo, level or output change**, one LedStatus at the end (block 1 phase B 0A → 46: the blink stopped when the value reached 0x00). FADERS 1-24 (978.6): record 1 **0x00 → 0x87** (channel 1's level kept, owner: "half as before"), block 1 G steady 0x87. The Simulator's next drag of fader 1 from 0x02 (1019.3): block 1 blinking green (`87 → 46 / 0A`) and nothing moved until the value passed 0x87 (0x9D at 1019.86: block 1 → 9D steady), then record 1, channel 1 and DMX 1 followed up to 0xFF and down to 0. |
| **S4b** a mode switch with a physical channel level | (The owner first dragged the Simulator's fader 2 to 0x97 and back to 0, 40 events 1035.0–1057.6, followed as in S1; then) the **physical** fader 2 to 0x7E (record 2 1D … 7E with no write, 1059.9–1060.9). FADERS MEMS (1136.7). | record 2 **0x7E → 0x00**, block 2 blinking red (memory 2 unmatched; owner: blinking, level zero). The Simulator's drag of fader 2 in memory mode 0x02 → 0xB2 → 0x00 (63 events): **nothing moved**, the blink stopped at 0x00 (1232.7). FADERS 1-24 (1249.4): record 2 **0x00 → 0x7E** (channel 2 back, owner: "about 50 %"). The physical fader 2 to zero afterwards (1285–1304: the LED blinked while it moved, channel 2 followed it down). |
| **S4c** a page change with a PC memory level | FADERS MEMS, the Simulator's fader 3 to **0x7F** (memory 3 of page 1: record 3 bytes 0, 1, block 3 FF, its ten channels up; 1318–1319). Page 2 on the console (1341.7). | **Record 3 byte 3 stayed 0 while records 1, 2 and 4–24 took 1** (seq 80 290: fader 3 kept memory 3 of page 1, H69), block 3 blinking (the page's slot 3 is empty, `0A`), the memory still up. The Simulator's fader 3 to zero (1404.1–1405.0): record 3 bytes 0, 1 7F → 6F → 3A → … → 00 and the ten channels down **at once, no pick-up**: the PC still controlled the content it had. Page 1 (1409.7), FADERS 1-24 (1413.4). |

**Reading (OBSERVED once per case; H79 / H81's matching clause):** a bank or page switch does not carry a level across. The
console remembers the last position it was given for a physical fader (a PC value counts as a position) and, when the fader's
content changes, compares that position with the new content's level: unequal means unmatched (the bump LED blinks, red in
memory mode, green in channel mode) and **PC fader events are ignored until one of them equals the content's level**, from which
the fader follows again ("last mover wins" holds only once matched, F141). A fader that keeps its content across a page change
(H69) needs no pick-up. The level left behind (channel 1 at 0x87, channel 2 at 0x7E) is kept and shown again in its own bank.

## S5: held bumps, with durations

| Bump (code) | Mode | Held (press → release headers) | The console (the five live objects) |
|---|---|---|---|
| bump 7 (0x06) | channel | 2.19 s (1434.254) | ChannelLevels[7], DMX 7, FaderInfo record 7 and block 7 at **0xFF** 67 ms after the press, LCD 0x86; back to 0 at the release (seq 86 316 …) |
| bump 10 (0x09) | channel | 2.67 s (1442.939) | channel 10, DMX 10, record 10, block 10 at 0xFF; back at the release (the second observation of 0x09 after E9) |
| bump 7 in SOLO mode | channel, SOLO on (0x33, block 52 FF ×6) | 1.91 s (1462.777) | as above (nothing else was up to be suppressed); SOLO off (1470.8) |
| memory bump 1 (0x00) | memory (0x39 before, 0x36 after) | 2.85 s (1486.099) | record 1 `FF FF`, block 1, channels 7 and 8, DMX 7, 8, LCD; back at the release |
| bump 1 (0x00), S2 | channel | 2.54 s (547.014) | channel 1, DMX 1, record 1, block 1 at 0xFF; back at the release |

The output stands at the BUMPS level (0xFF) from the press to the release, in every case within one poll of each (F97, F134);
no bump "stuck" after a release. SmartSoft sends the release as its own write 46 ms … 2.85 s after the press (E9b: up to 3.36 s).

## S6: the live keys seen once or never

| Key (code) | Sent | The console's answer | Status after E9c |
|---|---|---|---|
| IND 1 (0x47) | on / off (1498.8, 1503.2) | Independents body[1] 0 ↔ 1, ChannelLevels[49] 0 ↔ FF, LED block 72 (body[428], [431]) | second observation (E9b) |
| IND 2 (0x48) | on / off (1513.5, 1518.2) | Independents body[43], ChannelLevels[50], **OutputLevels DMX 47** (body[48]) 0 ↔ FF, block 73 (body[434], [437]) | second observation (E9b); IND 2 → DMX 47 in this patch, as E8b / E9b |
| STACK (0x3B) | on / off (1529.7, 1535.3), no GO | PlaybackInfo record 5 body[65], [66] `13 13` → `00 00` and body[76], [78] 00 → 0x32, the LCD "Live:14.0 Next: 1.0" / "█Mem:01/01 Mem:01/01" (F130), block 60 lit (body[355], [356], [358], [359] 46 → FF); off: back, the LCD to the main screen | second observation (E9b) |
| PAUSE (0x32) | twice, no fade running (1547.6, 1553.2) | **nothing listed** | second observation of the code (E12b); without a running fade it changes nothing visible (H107 annotated) |
| NEXT (0x3A) | on (1565.9); fader 4 moved with NEXT on; off (1676.1) | **ConsoleStatus body[6] 0 → 1** (seq 94 604), **LED block 59 `00 00 FF 00 00 FF`** (steady blue; body[349..354]); the owner: "the NEXT bump is lit and nothing else changed". With NEXT on, the Simulator's fader 4 (index 0x03, 35 events 0 → 0x87 → 0) made the console list **BlindLevels (18)** and LedStatus eleven times each: BlindLevels body = `[0x60][96 bytes]` (97 bytes) with **body[4] = channel 4 following the fader** (07 → 32 → … → 87 → … → 00), block 4 G following too; **ChannelLevels, OutputLevels and the LCD did not change**. Off: body[6] 1 → 0, block 59 `0A` ×6 | **0x3A is the NEXT key** (the manual's blind NEXT mode; H112 CONFIRMED in part: the LED the owner saw, the body[6] toggle of E12b reproduced, and the blind levels in their own object); BlindLevels (18) first seen (its name from sfEngine's table, S11.6) |
| CLEAR (0x38) | ×1, ×2, ×3 (1692.9–1705.2; nothing up) | six DisplayContent listings (the LCD's message), nothing else | second observation of the code (E9); with nothing up only the LCD reacts (F109) |

## S7: a channel fader with the MASTER at 0x84

The Simulator's MASTER to 0x84 (index 0x30, 20 events; FaderInfo body[194] FF → 84, block 53 `37 37 FF / 0A 0A 0A`, the MASTER
blink of F85), then its fader 1 to 0xFF and back: **FaderInfo record 1 read 0x85 at the fader's 0xFF**, as did ChannelLevels[1],
DMX 1 and block 1 (LCD 0x83): the live objects, FaderInfo included, carry the post-MASTER value in channel mode (F96 reproduced
for a PC fader; 0x84 × 0xFF / 0xFF = 0x84, read 0x85). The MASTER back to 0xFF (block 53 steady again).

## S8: the restart

SmartSoft closed (1759–1763) and started again (1775.4): the System pair (pseq 0, 1), the 158-record download, nothing pending
before it; the Simulator's fader 1 (index 0x00, 31 events from pseq 2) followed as in S1. The session ended after 30 s idle;
USBPcap stopped at 2140.6 (01:19:13.8).

## Reproduction of earlier facts and hypotheses touched (proposals for the curator; nothing is cleared)

- **F131, F132, F133, F134** (the write framing, the counter, the previous byte, the key layout, the console's answer as to the
  panel): reproduced in a fourth capture, 1 227 writes, no exception.
- **F58** (the System pair at every SmartSoft start): two more sessions (eleven since E2); the download now 158 records.
- **H111 → CONFIRMED in part (OBSERVED once per bank):** the fader index names the physical fader (0x00–0x17 = faders 1–24, all
  24 seen in bank 1-24; 0x00 and 0x01 in bank 25-48 moved channels 25 and 26; 0x00, 0x01, 0x02 and 0x08 in memory mode moved memory
  faders 1, 2, 3 and 9 of the shown page). No message that names a channel of the other bank or a memory directly was sent by
  SmartSoft (it switched banks with 0x37 / 0x36 / 0x39).
- **H79 / H81 (the matching clause) → CONFIRMED (OBSERVED once per case, S4):** after a mode switch with a PC or physical level in
  force the new content's fader is unmatched (LED blinking) and PC events are ignored until one equals the content's level; a
  page change leaves a raised fader with its content (H69 → CONFIRMED in part).
- **H82:** the codes 0x37, 0x3B, 0x47, 0x48 (E9b) and 0x32, 0x3A (E12b) seen again; 0x09 (E9) seen again; no new code; the rule holds
  for all 19 codes seen (still HYPOTHESIS for any key not seen).
- **H112 → CONFIRMED in part:** 0x3A is the NEXT key (the owner's eyes, the LED block 59 blue, ConsoleStatus body[6], the blind
  levels in BlindLevels (18)); H108's "toggle of body[6], visible in channel mode only" reproduced and explained.
- **H107 (0x32 = PAUSE):** supported by the Simulator's PAUSE key sending 0x32 (the owner pressed the key the script named);
  without a running fade nothing visible happens.
- **H65 / F166:** annotated: a pending list without a power cycle (24 records at S1's first poll); the owner's "no power cycle
  between E11's attempts 2 and 3".
- **F96:** reproduced for a PC fader (FaderInfo post-MASTER in channel mode).
- **F97 / F134 (held bumps):** bump 7 in three sessions, memory bump 1 in three, bump 10 in two, channel bump 1 once; a hold of
  up to 2.85 s shows and ends cleanly.
- **New (OBSERVED once):** BlindLevels (18) and NEXT mode's effect (F173 proposed); the matching rule's cases (F172); the pending
  list of 24 records (F177); the index rule for all 24 faders and the other banks (F171); the live keys' second observations
  (F175); the MASTER-at-0x84 figures (F176); held bumps with durations (F174).

## Input for the safety check that follows (SC-5)

1. **Classes to decide:** every fader index 0x00–0x17 in bank 1-24 (0x00 VERIFIED; 0x04 and 0x06 twice; the rest once in E9c); the
   bank 25-48 (0x37 twice; indices 0x00 and 0x01 there once; no bump there yet); memory mode (0x39 / 0x36 many times; indices
   0x00, 0x01, 0x02, 0x08 once as memory faders; memory bump 1 held three times; memory bump 9 once); held bumps (bump 7 ×3,
   memory bump 1 ×3, bump 10 ×2, bump 1 ×1); the keys IND 1 / IND 2 / STACK (twice), PAUSE / NEXT (twice as codes, once with a
   known effect), CLEAR (twice).
2. **The automatic bank switch's rule, from S4:** after the switch the console ignores PC events for that fader until one equals
   the new content's current level. A device that switches the bank and then starts the drag **from the console's reported level
   of the target** (ChannelLevels[c] or the memory's FaderInfo record) is matched by its first event; a drag that starts elsewhere
   moves nothing and leaves the LED blinking. The level left in the previous bank stays (F172) and the physical fader's position
   is not involved while the PC is the last mover.
3. **What stays unobserved:** a bump in bank 25-48; a fader event with the MASTER below full in memory mode; a held bump longer
   than 2.85 s; any index or code not listed above; a mode switch while a held bump is down.
4. **The System pair (SC-4 candidate):** eleven SmartSoft sessions, byte-identical, always answered by the version listing and the
   download; the console reports its state (the 158 records) within about 7 s.

## Surprises and anomalies

- The Simulator relabels its faders in bank 25-48 (the owner's deviation): the record's S2 covers faders 25 and 26 only.
- The download has 158 records, E2's and E3's had 157: the same ids, one record more; not identified here.
- Four USBD errors at the driver's bind (the HID interface's EP 0x81 and EP0), before any bulk transfer; SmartSoft's own driver
  stack, not the console's pipes.
- BlindLevels (18) was never listed in any earlier capture; the manual's NEXT mode is the only way seen to make the console use it.

## Next capture to take (E9d)

A bump in bank 25-48 from the Simulator; a second observation of the indices seen once (0x01–0x03, 0x05, 0x07–0x17 in bank 1-24;
0x00, 0x01 in bank 25-48; 0x00, 0x01, 0x02, 0x08 as memory faders), of the matching-rule cases and of NEXT mode with a patched
channel; a held bump of 5 s; optionally a fader event in memory mode with the MASTER at half. Passive, as E9c; the SC-5 check
decides which of these it needs before a class is cleared.
