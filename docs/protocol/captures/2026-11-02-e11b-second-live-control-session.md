# E11b: the second live-control session of our own software, the first with SC-5's classes (2026-11-02, afternoon)

**Status:** OBSERVED once for our own host (every control followed; the automatic bank switch, the matching event, the holds,
the bank keys and IND 1 / IND 2 from our software seen in one session of ours). **E11b clears nothing by itself:** SC-5's
decisions and conditions stand (`docs/protocol/safety-checks.md`, SC-5 "Review" and "Conditions for E11b"); the facts below are
annotated for our host, F179 and F180 are new and OBSERVED once. The session was planned by SC-5 and its review (three passes,
PASS on 2026-11-02), run by the owner through `tools/usb-probe/e11b-guided-session.ps1` (4aa02b2) with every acceptance answered
y, and analysed by the coordinating assistant with the committed read-only tools (`tools/protocol-analyzer/e11/writes.py`,
`e11/bus_vs_log.py`, `hsp_summary.py`, `join_reads.py`, `usbpcap_to_jsonl.py`) plus two scratch extractions (not committed:
the per-write answer bytes and the ConsoleStatus timeline, both derivable from the files below).

## Provenance

| Item | Value |
|---|---|
| Raw capture | `e11b-20261102-1457.pcap`, USBPcapCMD on root hub USBPcap1, 741 135 480 bytes, SHA-256 `1e63a871c6edef147439dff7a6e43617c884e6f52560b416126d3cbe06b3ae6b`, 611 092 frames by a tshark field census over the device addresses 1, 2, 3, 42, 43, 44, 45, 50; first frame 14:57:15.343 local time (CET). Not committed: it holds the traffic of other devices. |
| Console-only split (committed) | `2026-11-02-e11b-second-live-control-session.pcap`, `tshark -r <raw> -Y 'usb.device_address == 50' -F pcap -w <split>`, 5 141 790 bytes, SHA-256 `92f6c813b90c47517664a57d96597f69a5fff4fdf171fc1d208b8fb789db9bcd` (the console's address 50, the one with bulk traffic on endpoints 0x04 / 0x83). |
| Normalized bus (committed) | `2026-11-02-e11b-second-live-control-session.jsonl`, `usbpcap_to_jsonl.py` version 2 over the split: 51 610 transfers from 103 216 device frames, 102 notes (the guide's log merged at UTC+01:00, one analyst note), SHA-256 `85444c89384b2ee69295f5b6a1ebcdafb360dd959ff23d14e606c602986fbf1d`, 12 322 650 bytes. |
| The backend's transfer log (committed) | `2026-11-02-e11b-second-live-control-session.backend.jsonl`, a copy of `2026-11-02-135919-usb-session.jsonl` from `Device:Usb:CaptureLogDirectory` (capture start 13:59:19.138 UTC = 14:59:19 local), 51 357 transfers, SHA-256 `705d72c1e55e443491ade2b3fc293561e5b47e91570ff0e952fef6740ddc087f`, 11 279 539 bytes; backend version `1.0.0+4aa02b2`. |
| The guide's log (committed) | `2026-11-02-e11b-actions.txt`, SHA-256 `1650653412d226d0e7b99e97579fdf8ed2d993767097ca7a913626af2bc1b577`: the gate answers, the versions, every step with the PC clock, the owner's answers. |
| Owner statements (committed) | `2026-11-02-e11b-second-live-control-session-owner-statements.txt` (the chat and the log quotes). |
| Show files | Backup `SHOW30.ASC` (14:56, before the session) and `SHOW31.ASC` (15:17, after it) in `smartfade-sd-card-backup/`, both 15 944 bytes, **byte-identical**, SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`: no show data changed. |
| Versions | Backend and guide at commit 4aa02b2 (the SC-5 implementation 98cb499 with the review fixes 1eceabb and 0f2f1da); console firmware 3.0.2.9.0.43 (owner); Windows 11 Pro 10.0.26300.0; "the A-B cable straight into the PC's rear port"; the console power-cycled before the session (owner, 14:57:04). |
| Driver | USBPcap started 14:57:16 with interface 1 on ETC's driver (service ETCHSP read by the guide at 14:57:17); Zadig Replace Driver to WinUSB at 14:57:42 (read back WinUSB); Memory Integrity off; interface 0 (HID) untouched by our software. |
| Software mode | `--Device:Mode=Usb --Device:Usb:ConfirmDate=2026-11-02 --Device:Usb:LiveControlDate=2026-11-02`: the poll, the listed fetch, SC-2's and SC-5's ConsoleEvents behind the interlocks; the UI showed HARDWARE and LIVE CONTROL, nothing but CONNECT enabled before Connect (owner, 14:58:49). |

## Completeness

- **The bus equals the backend's log byte for byte:** 51 357 bulk transfers on each side after joining the bus's 242 multi-URB
  reads (F122), 0 differing in order (`e11/bus_vs_log.py`). The bus holds in addition 7 control transfers on endpoint 0 and 4
  interrupt-IN URBs on endpoint 0x81 (interface 0): the six descriptor frames USBPcap injects at t = 0, and at t = 22.0 s (the
  Zadig bind, 14:57:37-42) two interrupt-IN URBs cancelled with USBD status 0xC0010000 and one control transfer with
  0xC0000004, as E8b's H68 answer (EP0 and the HID pipe only at the driver change). No other non-zero USBD status; nothing on
  endpoint 0 after the bind.
- **Framing:** 0 errors over 51 357 transfers (H15 / H16 ok 25 263 and 341); the console's IN payload counter 0 → 231 with no
  break (H17); 117 Data headers, 117 payloads, 341 fetches, 488 IN payloads (147 lists, 341 objects).
- **Fetched objects:** FaderInfo 105, LedStatus 73, ChannelLevels 62, OutputLevels 60, DisplayContent 27, ConsoleStatus 10,
  Independents 4; **no Memory (0), Patch, Sequence or step object was listed** (the console lists only what changed; no
  record function was used).
- **The guide's steps, all run:** W0 to W10 with the two optional steps (IND 1 / 2, MASTER) opted in; Disconnect at an idle point
  (15:15:48, the log's last transfer at t = 991.1 s); no end-of-session message, no `INCOMPLETE-WRITE.txt`; the end positions
  "everything as it was when I started this session" (owner).
- **Deviation (owner, recorded):** between W4 and W5 (t = 642.8 and 646.7 s, 15:10:00-04) the owner pressed FADERS MEMS and
  then FADERS 1-24 on the panel, because the UI's memory panel showed "Dimmed memories were not delivered by the console in
  this session"; see "What the UI showed" below. No deviation on the software's side: no hold reached the 2.75 s bound, no
  switch missed its 2 s bound, no write was dropped, no fault.
- **Deviation 2 (owner, recorded; log-only evidence as E11 attempt 3's later runs):** after the guide's END and the SD save the
  owner started the backend by hand five times with live control on, **without USBPcap and without an SD pair** (15:39:51, 10 s,
  0 writes; 15:40:03, 161 s, 48 writes: channel faders 5, 6 and 7, the MASTER, IND 1 / IND 2; 15:42:46, 153 s, 55 writes: channel
  fader 1, memory fader 1 through the automatic switch to memory mode and the switch back, the MASTER, FADERS MEMS from the UI;
  15:45:19, 56 s, 20 writes: the bank keys from the UI and channel fader 5 through the switch back; 15:46:18, 368 s, 60 writes:
  channel faders 1 and 5, bump 5 twice, FADERS MEMS), 183 writes behind the same interlocks, 0 framing errors, no deviation note,
  every session stopped at an idle point; the logs are committed as `.backend-2` to `.backend-6.jsonl`. Two observations worth
  keeping, log-only: (1) at 15:42:46 + 114.3 s the physical channel fader 1 sat at 0xFF, the device's matching event carried
  **0xFF** and the console followed the UI's drag from its next event (FaderInfo record 1, ChannelLevels[1], DMX 1 and LED block 1 at
  0xC5, 0x7F, 0x7C …): a PC drag started at the reported non-zero level was applied at once (F172 annotated for our host); (2) at
  15:46:18 the owner's bump 5 test: with channel fader 5 at about half the bump raised channel 5 to full and the release restored
  the fader's level; with the fader at full the bump changed nothing and, after its release, the UI refused channel fader 5 by the
  bump shadow ("the console has not reported physical fader 5 again since"), the fail-closed edge the implementation documents (a
  bump that changes neither report keeps its fader refused until both objects are listed again): a usability defect, listed as a
  follow-up with a proposed rule (the shadow ends at the release when the press itself was not followed by a FaderInfo or
  ChannelLevels listing, because then the release changes nothing either; the reviewer decides). **No SD save after these
  sessions:** asked, the owner declined on 2026-11-02 ("There is not need for another file in this case then I did not changed
  anything"); the classes used change no show data, so the five sessions stand as log-only evidence without an SD pair, by his
  statement.

## The session (the backend's clock, t = 0 at Connect 14:59:19 local)

991.1 s; 25 147 polls, 341 fetches, 147 lists, 24 659 idle replies, **117 writes** (all ConsoleEvent, id 20: 93 fader events
and 24 key events as 12 press / release pairs, two of them holds). The interlock held a newer value 124 times (82 "the console's last report
is older than two poll periods", 31 "new key LEDs listed and not fetched", 11 "FaderInfo (the MASTER) listed and not fetched"),
each released at the next exchange; the session notes name every switch, every matching event and every hold.

| Step (guide time) | Panel / UI action | Writes (pseq, index or code, values) | The console's answer (objects fetched after the write; the bytes that concern the control) |
|---|---|---|---|
| idle minute (14:59:26-15:00:26) | nothing | none | nothing listed; nothing changed by itself (owner) |
| W0 (15:01:04-15:02:09) | panel: FADERS MEMS, FADERS 1-24, SOLO on / off, MASTER down and back, channel fader 1 up and back | none | ConsoleStatus `01 00 00 00 00 01 00` at t = 106.6 (body[0] = 1, memory mode) and `00 00 00 00 00 01 00` at 121.5 (channel mode, bank 1-24); the key LEDs, the MASTER and, after the fader move, FaderInfo, ChannelLevels, OutputLevels, LedStatus and DisplayContent (the five objects the implementation needs; the UI then showed the state, owner 15:02:25) |
| W1 (15:02:42, 15:03:06) | UI: channel fader 1 to about 20 % and back | pseq 0 the matching event 0x00; 1-5 up 0x11, 0x1E, 0x2A, 0x30, 0x33; 6-9 down 0x30, 0x1B, 0x02, 0x00 (index 0x00) | after pseq 0 **nothing listed** (the console's level was 0x00); after each of 1-9 FaderInfo record 1, ChannelLevels[1], OutputLevels DMX 1, LED block 1 and (first) DisplayContent at the sent value, as E11 (F167) |
| W2 (15:03:36, 15:03:50) | UI: channel fader 5 to about 20 % and back | pseq 10 matching 0x00; 11-14 up 0x09, 0x1F, 0x2E, 0x33; 15-18 down 0x2F, 0x1E, 0x0B, 0x00 (index 0x04) | nothing after the matching event; record 5, ChannelLevels[5], DMX 5, block 5 at each value |
| W3 (15:07:24, 15:08:09) | UI: channel fader 30 to about 20 % and back: **the automatic switch to bank 25-48** | pseq 19 / 20 FADERS 25-48 (0x37) press / release 61.6 ms apart; 21 matching 0x00; 22-27 up 0x08, 0x13, 0x23, 0x25, 0x31, 0x32; 28-31 down 0x30, 0x20, 0x0C, 0x00 (index 0x05) | the first list 30 ms after the press: ConsoleStatus, LedStatus, DisplayContent; ConsoleStatus `00 00 00 00 01 01 00` (body[4] = 1, bank 25-48) delivered 112 ms after the press, LedStatus (block 56 `ff 00 00 ff 00 ff` lit, block 55 off) 174 ms, DisplayContent with the row labels "25" (0x32 0x35) 236 ms; **the device marked the switch done 176 ms after the key** and queued the matching event, which was sent at t = 489.240 (nothing listed after it); then **record 6, ChannelLevels[30], OutputLevels DMX 30 and LED block 6** at each sent value (0x08, 0x13 … 0x32, back to 0x00): the index named the physical fader and the console applied it to channel 30 (F171 for our host) |
| W4 (15:08:59, 15:09:21) | UI: channel fader 2 to about 20 % and back: **the switch back to bank 1-24** | pseq 32 / 33 FADERS 1-24 (0x36) 47.1 ms apart; 34 matching 0x00; 35-40 up 0x0D, 0x1A, 0x26, 0x2C, 0x32, 0x33; 41-44 down 0x32, 0x0F, 0x05, 0x00 (index 0x01) | the first list 17 ms after the press (ConsoleStatus, LedStatus); ConsoleStatus `00 00 00 00 00 01 00` (bank 1-24), block 55 lit; the switch done 131 ms after the key; record 2, ChannelLevels[2], DMX 2, block 2 followed |
| W5 (15:10:14, 15:11:57) | UI: memory fader 3 of page 1 to about 20 % and back: **the switch to memory mode** (after the owner's panel visit, see the deviation) | pseq 45 / 46 FADERS MEMS (0x39) 61.8 ms apart; 47 matching 0x00; 48-53 up 0x23, 0x2A, 0x30, 0x35, 0x34, 0x32; 54-59 down 0x28, 0x25, 0x1B, 0x0F, 0x07, 0x00 (index 0x02) | the first list 26 ms after the press; ConsoleStatus `01 00 00 00 00 01 00` (memory mode), LedStatus (block 57 `ff ff ff ff ff 0a`, the red bump LEDs); the switch done 141 ms after the key; then **FaderInfo record 3 bytes 0 and 1 = the level** (`23 23`, `2a 2a` …), the memory's channels in ChannelLevels and OutputLevels (channels 1-6 at `23 23 23 23 1d 22` for 0x23: memory 3's content scaled), LED block 3 red (`00 00 ff 00 00 46`), the LCD |
| W6 (15:12:51) | UI: memory bump 1 held about 2 s | pseq 60 press (0x00) at t = 815.588, 61 release at 818.072: **held 2.484 s** (released by the UI, before the 2.75 s bound) | the first list 32 ms after the press (FaderInfo, OutputLevels, ChannelLevels, LedStatus, DisplayContent): **record 1 `ff ff 00 00`**, ChannelLevels[7], [8] and DMX 7, 8 at 0xFF (memory 1's content), block 1, the LCD; after the release record 1 `00 00 00 00` (79 ms), LedStatus, then ChannelLevels, OutputLevels, DisplayContent back (F174 for our host) |
| W7 (15:13:17) | UI: channel bump 7 held about 2 s: **the switch back to bank 1-24 first** | pseq 62 / 63 FADERS 1-24 (0x36) 47.2 ms apart (the switch done 132 ms after the key, ConsoleStatus `00 … 00 01 00`); 64 press bump 7 (0x06) at 840.862, 65 release at 843.121: **held 2.259 s** | the first list 50 ms after the press: **record 7 `ff 00 00 00`**, ChannelLevels[7] 0xFF, DMX 7 0xFF, LED block 7 `ff 00 00 ff 00 00`, the LCD glyph 0x86 in cell body[9]; after the release ChannelLevels listed 21 ms after the payload (0x00), then OutputLevels, FaderInfo (`00 00 00 00`), LedStatus, DisplayContent (112 ms) |
| W8 (15:13:47, 15:14:06) | UI: IND 1 on, off; IND 2 on, off | pseq 66 / 67, 68 / 69 IND 1 (0x47) pairs 60.9 and 46.8 ms; 70 / 71, 72 / 73 IND 2 (0x48) pairs 61.4 and 61.3 ms | IND 1: the list 32 ms after the press (Independents, LedStatus, ChannelLevels); **Independents body[1] 0 → 1 → 0**, LED block 72 `ff 00 00 ff 00 00` → off; IND 2: **body[43] 0 → 1 → 0**, block 73 `ff 00 00 ff 00 ff` → off, **OutputLevels DMX 47 0xFF → 0x00** (F115 / F108 for our host; IND 1 unpatched, no DMX) |
| W9 (15:14:40) | UI: the FADERS 25-48 key, 5 s, the FADERS 1-24 key | pseq 74 / 75 (0x37) 62.2 ms apart; 76 / 77 (0x36) 47.1 ms | as W3 / W4 without a fader: ConsoleStatus body[4] 1 then 0, blocks 56 / 55, the LCD rows "25" / " 1"; the device's bound (2 s) held with 142 and 129 ms |
| W10 (15:15:08, 15:15:21) | UI: MASTER to about half and back | pseq 78-102 down 0xF9, 0xEB, 0xE7 … 0x81, 0x7F (25 events, the first with the previous byte 0x00: this host's first value for index 0x30, condition 4); 103-116 up 0x7F, 0x83 … 0xF7, 0xFF (14; index 0x30) | FaderInfo body[194] at each value (F169 reproduced), LED block 53 with the MASTER blink (`37 ff 0a 0a 0a 0a`, F85 / F90) from the first value below full, steady off (`0a` × 6) after 0xFF; every channel at zero, so no output changed |
| end (15:15:48) | UI: Disconnect | none | idle point; the session stopped by the application |

The writes by step: 10 + 9 + 2 + 11 + 2 + 11 + 2 + 13 + 2 + 2 + 2 + 4 + 4 + 2 + 2 + 39 = 117: **93 fader events** (W1 10, W2 9,
W3 11, W4 11, W5 13, W10 39) and **24 key events** (12 press / release pairs: 0x37 × 2, 0x36 × 3, 0x39, 0x00, 0x06, 0x47 × 2,
0x48 × 2).

## Timing (SC-2 MAJOR 1's bound and SC-5 condition 1 (c))

- Every one of the 117 Data headers was submitted **25.5-51.3 ms after the completion of a poll's reply** (SC-2's 62 ms bound
  held 117 of 117; the replies themselves took 0.05-6.0 ms), and the next poll followed the payload after 15.0-31.1 ms.
- A key's release followed its press after 46.8-62.2 ms with no write between them (as E11 attempt 3's 46.6-76.7 ms).
- The console's first list after a bank key came 17-31 ms after the press payload; ConsoleStatus was delivered 112-131 ms and
  the LedStatus 174-236 ms after it; **the device marked each switch done 129-176 ms after the key** (its 2 s bound held
  six of six: four switches for a control, two for the UI's keys). The matching event went 150-170 ms after the switch was
  done, the UI's first level 30-60 ms after the matching event.
- The two holds lasted 2.484 and 2.259 s, both ended by the UI's release; the device's own bound (the release asked for at
  2.75 s) was not reached and no overrun was logged.

## What the console did with the matching events (F172 for our host)

Five matching events were sent (pseq 0, 10, 21, 34, 47), one per fader before its first drag: each carried the console's
reported level, 0x00 in every case (every fader sat at zero), and **the console listed nothing after any of them** (one poll,
one idle reply, no fetch in the 1.5 s window); the drag that followed was answered from its first event. So the rule's cost is
one silent event per fader and the "ignored until matched" case (F172) was not exercised: no fader of this session had a level
to match other than zero. The PC values were taken as the physical faders' levels (FaderInfo record k byte 0 = the sent value)
until the next move, as F141 says; the physical faders stayed at zero throughout (owner, end positions).

## What the UI showed (the deviation before W5)

Before W5 the UI's memory panel still showed "Dimmed memories were not delivered by the console in this session": that hint
dims the memory cells whose **Memory objects (the contents, id 0)** the console has not delivered, and in a session of ours the
console lists a Memory object only when it changes (no record function was used; none was listed in 991 s). The owner, reading
it as "the memories cannot be changed", pressed FADERS MEMS and FADERS 1-24 on the panel (t = 642.8 and 646.7 s); the console
listed ConsoleStatus, LedStatus and once ChannelLevels, no FaderInfo and no Memory object, so the UI's data did not change by
that visit; W5 then ran through the automatic switch from channel mode as planned. Whether the memory faders were disabled in
the UI before the visit is not in the log (the device names the memory faders of the reported page as writable once FaderInfo
carries that page, F113 / H69, which W0's panel visit to memory mode and back had given). **Follow-up (UI, no protocol
change):** say in the hint that the memory contents are what is missing and that the faders stay usable, and show the
writable state of a memory fader independently of its dimming.

## Interlocks and refusals seen

124 "write held back" notes, each released within one exchange (82 for a report older than two poll periods, 31 for key LEDs
listed and unfetched, 11 for the MASTER's FaderInfo listed and unfetched; the same classes as E11 attempt 3); no refusal of a
control (every requested control was sent), no record function armed, no NEXT mode, no blackout, no shadow refusal (no fader was
moved during or right after a bump of the same physical fader; SOLO was off at every bump).

## What E11b answers, and what it does not

- **The automatic switch sequence (SC-5 condition 1 (a)-(e)) from our own host is followed by the console** exactly as
  SmartSoft's FADERS keys were: the bank key as a tap, ConsoleStatus body[4] / body[0] and the LEDs reported within 176 ms,
  the first fader event at the reported level, then the drag applied to the new bank's content (channel 30 in bank 25-48,
  memory fader 3 in memory mode). **F179 (new, OBSERVED once for our host).**
- **An event at the console's reported level lists nothing** (five of five matching events). **F180 (new, OBSERVED once).**
- The index names the physical fader for our host too (0x05 → channel 30 in bank 25-48; 0x02 → memory fader 3 in memory mode):
  **F171 annotated** (OBSERVED once for our host). A held bump from our host is answered as SmartSoft's (record k at 0xFF or
  `ff ff`, the levels, the LED, the LCD, back at the release): **F174 annotated**. The keys 0x37, 0x47, 0x48 from our host:
  **F175 annotated**; IND 2 → DMX 47: **F115 / F108 annotated**. The MASTER followed in FaderInfo with the blink on block 53:
  **F169 VERIFIED in part** (the MASTER case, attempts 3 and E11b); the channel faders followed in a second session of ours
  (fader 1 again, faders 2, 5, 30 and memory fader 3 new): **F167 VERIFIED in part** (channel fader 1 in two sessions, the
  others once); the key taps FADERS MEMS, FADERS 1-24 and memory bump 1 (as a hold here) answered again: **F168 VERIFIED in
  part** (those codes); **H66 CONFIRMED for SC-5's classes** (OBSERVED once).
- Not answered: the console's behaviour when a PC fader's first event does **not** match the content's level (every reported
  level was zero), the SOLO case of the review's MINOR C, the blackout refusal (no blackout), the hold bound (no hold reached
  2.75 s), a bump of bank 25-48 from our host, a memory bump of a page other than 1, FADERS 25-48 from memory mode (refused by
  design). **E11b clears nothing; SC-5's decisions and conditions are unchanged.**

## Files

| File | SHA-256 | Bytes |
|---|---|---|
| `2026-11-02-e11b-second-live-control-session.pcap` (the console-only split) | `92f6c813b90c47517664a57d96597f69a5fff4fdf171fc1d208b8fb789db9bcd` | 5 141 790 |
| `2026-11-02-e11b-second-live-control-session.jsonl` (the normalized bus) | `85444c89384b2ee69295f5b6a1ebcdafb360dd959ff23d14e606c602986fbf1d` | 12 322 650 |
| `2026-11-02-e11b-second-live-control-session.backend.jsonl` (the backend's transfer log) | `705d72c1e55e443491ade2b3fc293561e5b47e91570ff0e952fef6740ddc087f` | 11 279 539 |
| `2026-11-02-e11b-actions.txt` (the guide's log) | `1650653412d226d0e7b99e97579fdf8ed2d993767097ca7a913626af2bc1b577` | 6 887 |
| `2026-11-02-e11b-second-live-control-session-owner-statements.txt` | (text) | |
| `2026-11-02-e11b-second-live-control-session.backend-2.jsonl` (the backend's log of the later session 14:39:51  no USBPcap) | `ba702b259836ec764f3f06695b314e9207b99ae37adbb6467f42c9d85b3eb095` | 112 326 |
| `2026-11-02-e11b-second-live-control-session.backend-3.jsonl` (the backend's log of the later session 14:40:03  no USBPcap) | `3b04ad007bb6f26426a7b336e4b610fe7aca34b505967f74540407a6f8351256` | 2 002 378 |
| `2026-11-02-e11b-second-live-control-session.backend-4.jsonl` (the backend's log of the later session 14:42:46  no USBPcap) | `c2768eddb2e65e5f225f8f4bd96a31afdee6099def70948a550b0faaf7c7bdcd` | 2 073 869 |
| `2026-11-02-e11b-second-live-control-session.backend-5.jsonl` (the backend's log of the later session 14:45:19  no USBPcap) | `77db51cdfa5384233a1c415628edbae6b6d389dfc4eb48ef1f92f519edb7e499` | 699 054 |
| `2026-11-02-e11b-second-live-control-session.backend-6.jsonl` (the backend's log of the later session 14:46:18  no USBPcap) | `4fcf742e02a327a0d95c510b314c5571e98c01f962673bf120a020504dd92cde` | 4 349 510 |
| `smartfade-sd-card-backup/SHOW30.ASC` = `SHOW31.ASC` (git-ignored folder) | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` | 15 944 |
| raw `e11b-20261102-1457.pcap` (not committed) | `1e63a871c6edef147439dff7a6e43617c884e6f52560b416126d3cbe06b3ae6b` | 741 135 480 |
