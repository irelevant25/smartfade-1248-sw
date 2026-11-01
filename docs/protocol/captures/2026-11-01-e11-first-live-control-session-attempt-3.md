# E11, attempt 3 (2026-11-01, evening): the full script W1–W8 of the first live-control session, and two sessions after the guide

**Status of this record:** complete for what was run. The guided session ran SC-2's E11 script to its end: channel fader 1 up and
back to zero (W1), bump 7 (W2), SOLO on and off (W3), BLACKOUT on and off (W4), BUMPS to half and back (W5), FADERS MEMS, memory
bump 1 three times and FADERS 1-24 from the UI (W6), MASTER to half and back (W7) and GO twice with STACK unlit (W8): **116
ConsoleEvents of SC-2's classes A and B1 went out and the console followed every control** as it follows the panel and SmartSoft's
Console Simulator (F134). **E11 is done.** After the guide had ended the owner ran the backend twice more on his own (a read-only
session of 3 s and a live-control session of 242 s with 57 more writes), without USBPcap and after the after-session SD save: those
two sessions are recorded here as a deviation from E11's conditions, evidenced by the backend's log alone. **Nothing is cleared by
this record** (SC-2's conditional GO and its conditions are unchanged); the status proposals are at the end, for the curator.

Evidence class 1 for the guided session (our console, two independent recorders: the backend's own transfer log and a parallel
USBPcap capture of the bus, equal byte for byte); the two later sessions: the backend's log only (one recorder). The decoding of the
console's answers uses the documented layouts of `responses.md` and names the fact behind each index.

## Provenance

| Item | Value |
|---|---|
| Session | The guided E11 session, `tools/usb-probe/e11-guided-session.ps1` at commit 5485ea3 (the W0 version with the three-case W1 answer), run by the owner from 22:04:27 to 23:04:08 local time (CET, UTC+1). Action log `2026-11-01-e11-attempt-3-actions.txt` (SHA-256 `c50d72143140423e74111b171000caa2296e67b0e192def849a9c98b7278692e`, 94 lines). Owner statements `2026-11-01-e11-attempt-3-owner-statements.txt` (`3a9be5789f95ab7594be1d3b5ae8371398178b2386eae8d8564a6a606f157616`): the guide's typed answers with readings, and the owner's chat message of the same night (the fader-mode question) |
| Gates (from the log) | The SC-2 re-check items closed = y (22:04:36); the acceptances H66 unknown, last mover, lost release, live output, counter 0x00, release after fault = y (22:04:38–22:04:48); rig decision = y; the optional memory bump and GO = y; backup `SHOW21.ASC` saved on the console before the backend started (22:11:34); backend commit 5485ea3; Windows build 10.0.26300.0; firmware 3.0.2.9.0.43 (the owner's Diagnostics reading); cable: the A-B cable straight into the PC's rear port; "power cycled today before this session" = y (the day's earlier cycles count; whether the console was cycled again after attempt 2 is not answered by it, owner question 2 below) |
| Driver | Interface 1 on WinUSB since the morning's attempt 1 (the guide read the service WinUSB before and after its Zadig step at 22:45:17 / 22:45:20; no Replace Driver was run; the bus shows no control transfer to the console but the six descriptor frames USBPcap injects). Interface 0 (HID) untouched by us |
| Backend session 1 (guided) | 22:46:29.244–23:02:21.992 local (952.75 s): `2026-11-01-214629-usb-session.jsonl` from `%LOCALAPPDATA%\SmartFade\captures`, committed as `2026-11-01-e11-first-live-control-session-attempt-3.backend.jsonl` (SHA-256 `dc2c9bd3fbfd91df930c5c1905a732e07311eb631d3af8a79c6d09ce25a0aa06`, 49 509 lines: 49 426 transfers, 82 notes). First notes: "live control on (SC-2): only the whitelisted ConsoleEvents may be sent, behind the record-function interlock". Last: "session stopped by the application at an idle point; polls 24148, fetches 325, lists 248, objects 325, writes 116" |
| Backend session 2 (after the guide) | 23:04:35.879 local + 2.97 s, **read-only (SC-1)**, started by the owner by hand after the guide's END (23:04:08) and the SD save `SHOW22.ASC`: `2026-11-01-220435-usb-session.jsonl`, committed as `…attempt-3.backend-2.jsonl` (`c97c43d78f71f73191c5d930e3f1655453dfc41dbe830c27f9ba681e145fd7b6`, 157 transfers: 75 polls, 3 fetches, 1 list, 0 writes). The list pending at its Connect named DisplayContent, LedStatus and **FileName**, and FileName read `SHOW22.ASC` (UTF-16LE, 30 code units, F60): the name of the SD save made a minute earlier (F103) |
| Backend session 3 (after the guide) | 23:06:59.118–23:10:40.840 local (241.72 s), **live control on**, started by the owner by hand: `2026-11-01-220659-usb-session.jsonl`, committed as `…attempt-3.backend-3.jsonl` (`1355740e73efc0f2609f6705761af54c2824af82a06026b2b1d43031350d9959`, 12 910 lines: 12 852 transfers, 57 notes); "polls 6026, fetches 302, lists 82, objects 302, **writes 57**", stopped by the application at an idle point. **No USBPcap ran** (it stopped at 23:02:56 with the guide) and **no SD save followed it**: see "The two sessions after the guide" below |
| Bus (guided session) | USBPcapCMD on root hub USBPcap1, started by the owner at 22:45:02 into `e11-20261101-2244.pcap` (the guide's suggested name; SHA-256 `3e6a4cab3f888dffb71bdac2c271db4ab9da972e87737f776d519198c9355d07`, 677 079 318 bytes, 537 066 frames, 22:45:02.377096–23:02:56.266977). **Not committed:** it holds other devices (addresses 1, 2, 3, 32, 33, 34: an isochronous device with most of the file, a hub, the owner's keyboard). Census by `tshark -T fields`: the console is **device address 38** (49 410 bulk OUT, 50 016 bulk IN, 6 control frames = the injected descriptors). Console-only split `2026-11-01-e11-first-live-control-session-attempt-3.pcap` (`tshark -r <raw> -Y "usb.device_address == 38" -F pcap -w <split>`; SHA-256 `fac35487d425864ab6a3ea2661cb1218efabd370837b9ca1645483b3289cf5ae`, 4 927 465 bytes, 99 432 frames, 22:45:02.377096–23:02:21.991288) |
| Normalized bus | `2026-11-01-e11-first-live-control-session-attempt-3.usbpcap.jsonl` (SHA-256 `74fcfb67ea50b3732a81eb6c98f6c20ba5c7aabbc08b2b06dd14306505a25d4c`; `usbpcap_to_jsonl.py` v2 with the action log merged as notes at UTC+1, 96 notes): 49 716 transfers, 24 705 bulk OUT, 25 008 bulk IN, 3 control (the descriptors), **non-zero USBD status 0**. `hsp_summary.py` reports 574 "framing errors" on the bus file: the 287 reads longer than 64 bytes split into two URBs (200 = 192 + 8, 456 = 448 + 8, 519 = 512 + 7; F122), each counted twice; the backend's log, which reads whole transfers, has 0 |
| Bus = log | `tools/protocol-analyzer/e11/bus_vs_log.py` (the multi-URB reads joined): **49 426 bulk transfers on the bus = 49 426 in the backend's log of session 1, 0 differing in direction or bytes, 287 joins.** Every transfer our software logged in the guided session is on the bus and nothing else went to or came from the console |
| Show files | Backup `SHOW21.ASC` (22:11, before the backend started); after-session save `SHOW22.ASC` (23:04, after the backend and USBPcap had stopped; the guide's order, no SD operation while connected). **`SHOW22.ASC` = `SHOW21.ASC` byte for byte** (both 15 944 bytes, SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, in the git-ignored `smartfade-sd-card-backup/`; equal also to `SHOW17.ASC`–`SHOW20.ASC` of attempts 1 and 2): no show data changed in the guided session, as expected for live events. **The pair does not cover sessions 2 and 3**; the owner's later save `SHOW23.ASC` (copied 2026-11-02) equals `SHOW22.ASC` byte for byte (SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`), so nothing changed in them either |
| Tools (read-only) | `tools/protocol-analyzer/hsp_summary.py` (summary, timeline), `write_rules.py` (placement and timings), `tools/protocol-analyzer/e11/writes.py` (each write with its neighbours and the console's answer; key events and the list layout added for this attempt), `e11/bus_vs_log.py` |

## The guided session, in session time (the backend's clock; local time = 22:46:29.244 + t)

| t (s) | Local | Event (the log's notes and the owner's action log) | On the bus / in the log |
|---|---|---|---|
| 0.06 | 22:46:29 | Connect; the first poll | The first reply lists **LedStatus and DisplayContent** (payload seq 0), the pair E3b, E8b and attempt 2 saw pending after a power-on (H65 case (a)); both fetched |
| 0.2–80 | | The idle minute ("nothing changed by itself": owner, 22:47:41) | Idle replies only |
| 80.5 | 22:47:49 | **W0 on the panel:** FADERS MEMS | ConsoleStatus body[0] 0 → 1, LedStatus blocks 1–24 = the memory map (`46` = recorded, `0A` = empty; F84), DisplayContent |
| 98.8 | 22:48:08 | W0: FADERS 1-24 | ConsoleStatus body[0] 1 → 0, the LED map back to channel mode |
| 119.3 / 121.8 | 22:48:28 | W0: SOLO on, then off | Block 52 (body[307..312]) `0A` → `FF` → `0A` (F100) |
| 132.2–134.4 | 22:48:41 | W0: MASTER down a little and back to full | FaderInfo body[194] 0xFE → 0xF8 … 0xDB → 0xFF (22 lists); block 53 phase A `0A 0A 0A` → `37 37 FF` while below full and back (F85, the MASTER blink). W0: 24 lists, 28 fetches in all |
| 153.8 | 22:49:03 | Owner: the UI shows the LEDs, the MASTER and channel mode (conditions 5, 6, 8 satisfied) | |
| 162.1–163.5 | 22:49:11 | **W1: channel fader 1 dragged to about 20 % in the UI** | **5 fader events** 0x05, 0x11, 0x22, 0x23, 0x34 (pseq 0–4), the table below |
| 197.1–197.9 | 22:49:46 | **W1: channel fader 1 back to zero** | **4 fader events** 0x35, 0x1B, 0x01, 0x00 (pseq 5–8; the first a one-step rise as the hand took the UI fader) |
| 242.8 | 22:50:32 | **W2: bump 7 tap** in the UI | Key 0x06 press / release 77 ms apart (pseq 9, 10) |
| 271.5 / 298.0 | 22:51:00 / 22:51:27 | **W3: SOLO on, then off** | Key 0x33 press / release twice (pseq 11–14) |
| 321.5 / 345.9 | 22:51:50 / 22:52:15 | **W4: BLACKOUT on, then off** | Key 0x34 press / release twice (pseq 15–18) |
| 371.5–390.1 | 22:52:40 | **W5: BUMPS to half and back to full** | **47 fader events** index 0x31: 0xFB … 0x7E (29) then 0x81 … 0xFF (18) (pseq 19–65); 47 lists |
| 452.1 | 22:54:01 | **W6: FADERS MEMS from the UI** | Key 0x39 press / release (pseq 66, 67) |
| 484.7, 491.2, 496.4 | 22:54:33–22:54:45 | **W6: memory bump 1 clicked three times** in the UI (owner: "I clicked on it 3 times … when I click and hold it's just blink") | Key 0x00 press / release three times, 47–48 ms apart (pseq 68–73) |
| 549.2–550.1 | 22:55:38 | The owner held the console's **own** memory bump 1 ("I also tried to click and hold the bump on the CONSOLE as well") | **No write**; memory 1's content shown for 0.85 s (below) |
| 624.7 | 22:56:53 | **W6: FADERS 1-24 from the UI** | Key 0x36 press / release (pseq 74, 75) |
| 647.3–662.0 | 22:57:16 | **W7: MASTER to half and back to full** | **36 fader events** index 0x30: 0xFB … 0x7F (24) then 0x81 … 0xFF (12) (pseq 76–111); 36 lists |
| 863.6 / 878.5 | 23:00:52 / 23:01:07 | **W8: GO from the UI, twice** (STACK unlit; owner: "I press it twice, sorry, I was experimenting") | Key 0x31 press / release twice (pseq 112–115); two 5.0 s crossfades, 110 lists |
| 952.7 | 23:02:21 | The owner presses DISCONNECT at an idle point | "session stopped by the application at an idle point" |

The physical faders were not moved during the guided session (owner: "everything is as it was at the beginning"); no end-of-session
message (no held key, no unsent release, no incomplete write; `INCOMPLETE-WRITE.txt` does not exist).

## The 116 writes

Every write is a ConsoleEvent (id 20) after the 12-byte type-1 header (w2..w5 = the last reply header's, 116 of 116, F52). Fader
events (kind 0, 92 of them) carry `[pseq][04 00][14][00][index][value][previous]`; key events (kind 1, 24 = 12 press / release pairs)
carry `[pseq][03 00][14][01][code][state]` with state 1 = press, 0 = release (H82's codes). The counter started at 0x00 and rose by one
per write to 0x73 (F131); every fader event's previous byte is this host's last value for that index, 0x00 before the first (F132: 92
of 92). A press and its release were one operation 46.6–76.7 ms apart (median 61.7 ms; no write between them, SC-2 condition 4).

| Step | Control (index / code) | Events (pseq) | Values sent | Reply age at the header (ms) | Next poll or fetch after the payload (ms) |
|---|---|---|---|---|---|
| W1 | channel fader 1 (0x00) | 9 (0–8) | 05 11 22 23 34, then 35 1B 01 00 | 27.1–43.8 | 15.0–30.3 |
| W2 | bump 7 (0x06) | 2 (9, 10) | press, release | 44.1–44.7 | 16.0–29.6 |
| W3 | SOLO (0x33) | 4 (11–14) | press, release × 2 | 28.0–45.9 | 15.1–29.7 |
| W4 | BLACKOUT (0x34) | 4 (15–18) | press, release × 2 | 28.7–44.0 | 15.3–15.8 |
| W5 | BUMPS (0x31) | 47 (19–65) | FB F7 EF … 80 7E, then 81 86 … FB FF | 28.0–44.9 | 15.1–31.1 |
| W6 | FADERS MEMS (0x39) | 2 (66, 67) | press, release | 32.9–43.3 | 15.1–15.6 |
| W6 | memory bump 1 (0x00) | 6 (68–73) | press, release × 3 | 27.7–44.8 | 15.1–15.8 |
| W6 | FADERS 1-24 (0x36) | 2 (74, 75) | press, release | 28.1–30.2 | 15.1–15.8 |
| W7 | MASTER (0x30) | 36 (76–111) | FB E3 DA … 80 7F, then 81 85 … FD FF | 27.8–45.9 | 15.0–30.9 |
| W8 | GO (0x31) | 4 (112–115) | press, release × 2 | 30.1–44.0 | 15.1–30.1 |

Placement (`write_rules.py`): all 116 between a poll's reply and the next poll or fetch, at most one per loop iteration (SC-2's cleared
placement): 34 after an idle reply and 82 after a list reply whose records were not yet fetched (the drags of BUMPS and MASTER, where
the console lists FaderInfo after every event; SmartSoft wrote in the same position in E9 / E9b). **SC-2 MAJOR 1's E11 analysis:** the
time from the reply's completion to the write header's submit is **27.1–45.9 ms for all 116**, at most 62 ms as required; the next poll
or fetch went 15.0–31.1 ms after the payload (floor 15 ms); the header-to-header interval was at least 46.6 ms (floor 46 ms); header
submit to completion 0.1–0.2 ms, payload 0.1 ms. The interlock held newer values 78 times ("write held back": a listed object not yet
fetched or a report older than two poll periods, SC-2 condition 5) and released each hold within one poll exchange; a drag therefore
reached the console as 9 to 47 events at about 110–130 ms intervals during W5 and W7 (one event per fetch cycle) and about 400 ms
during W1 (four or five objects listed and fetched per event).

## The console's answer, per control

Every answer is a DirtyObjects list at the next poll, then the listed objects fetched one per poll cycle; the bytes that changed are
the ones `responses.md` assigns to the control. The console acknowledged nothing, listed no show object, no System record and no download.

| Control | What the console listed and what changed | Reading |
|---|---|---|
| **W1 channel fader 1 up** (5 events) | After each event FaderInfo body[1], ChannelLevels body[1], OutputLevels body[2] and LedStatus body[2] / body[5] (block 1, G of both phases) = the sent value; DisplayContent body[3] 0x20 → 0x80 after the first (the lowest bar glyph in fader 1's cell, F86). The third and fourth events (0x22, 0x23) were 47 ms apart: one list covered both and the fetched objects carried 0x23; the console listed the four objects once more (unchanged). 11 lists, 38 fetches for the nine events | As attempt 2 (F167): followed like a physical fader move, the physical fader 1 at zero (F141) |
| **W1 channel fader 1 to zero** (4 events) | FaderInfo body[1] 0x34 → 0x35 → 0x1B → 0x00 (the 0x01 and 0x00 events 47 ms apart, one list, the objects at 0x00); the same bytes in ChannelLevels, OutputLevels, LedStatus; DisplayContent body[3] 0x80 → 0x20 (the bar glyph cleared) | **The zero event is followed** (new in this attempt); the console's channel 1 ended at 0, as before the session |
| **W2 bump 7 tap** (77 ms) | Two lists of the five live objects (OutputLevels, FaderInfo, ChannelLevels, LedStatus, DisplayContent): one at the press (31.8 ms after the payload), one at the release; **every fetched object unchanged** (the first fetch came 35 ms after the release) | The console reacted to the press and to the release; a tap this short leaves no trace in the fetched objects (F97 showed bump output while a bump is **held**). Whether channel 7 rose for those 77 ms is not shown by this capture |
| **W3 SOLO** on / off | One list each (LedStatus): block 52 (body[307..312]) `0A` × 6 → `FF` × 6 → `0A` × 6 | SOLO toggled from the PC as from the panel in W0 and as SmartSoft's key 0x33 (F100, F134) |
| **W4 BLACKOUT** on / off | One list each (LedStatus): block 53 (body[313..318]) `0A` × 6 → `00 00 FF 00 00 FF` → `0A` × 6; no level object listed (every level was at zero) | The steady-blue BLACKOUT pattern of F114 from the PC's key 0x34; its effect on the levels was not exercised (nothing was up) |
| **W5 BUMPS** (47 events) | One list (FaderInfo) per event, body[193] = the sent value (0xFF → 0x7E → 0xFF; F95); nothing else listed | Followed; invisible on the console (owner: "I can't tell"), since bump output appears at the BUMPS level only while a bump is held (F97) |
| **W6 FADERS MEMS** from the UI | Lists [ConsoleStatus, LedStatus, DisplayContent] then [LedStatus, DisplayContent]: ConsoleStatus body[0] 0 → 1, the LED map of memory mode (F87, F98) | The PC's key 0x39 switched the console to memory mode as the panel's key did in W0 and as SmartSoft's did (F134) |
| **W6 memory bump 1** (3 taps, 47–48 ms) | Two lists of five objects per tap (press and release); **every fetched object unchanged** | As W2: a tap leaves no trace in the fetched objects |
| The console's **own memory bump 1 held** (0.85 s, no write) | Lists at 549.22 and 550.07: FaderInfo body[1], body[2] 0x00 → 0xFF → 0x00 (memory fader 1, F112), LedStatus block 1 `46` → `FF` → `46`, ChannelLevels body[7], body[8] and OutputLevels body[8], body[9] 0x00 → 0xFF → 0x00 (memory 1 = channels 7 and 8 at full), DisplayContent body[9], body[10] 0x20 → 0x86 → 0x20 | Byte for byte the answer to SmartSoft's held memory bump 1 in E9 / E9b (F134): what a **held** bump shows and a tap does not |
| **W6 FADERS 1-24** from the UI | List [ConsoleStatus, LedStatus]: body[0] 1 → 0, the channel-mode LED map | Back to channel mode from the PC's key 0x36 |
| **W7 MASTER** (36 events) | One list (FaderInfo) per event, body[194] = the sent value (0xFF → 0x7F → 0xFF); block 53 phase A `0A 0A 0A` → `37 37 FF` at the first value below full and back to `0A` after 0xFF (the MASTER blink of F85 / F90); no level object listed (nothing was up) | Followed; the MASTER's effect on the outputs (F96) not exercised with every level at zero |
| **W8 GO × 2** (STACK unlit) | Per press the list [SequenceMain, PlaybackInfo, LedStatus] then PlaybackInfo alone every 75–110 ms for 5.0 s (55 lists per crossfade): PlaybackInfo body[67] / body[68] `F7 08` → … → `01 FE` → `FF 00` and body[76] = body[78] 0x32 → 0x28 → 0x1E → 0x14 → 0x0A → 0x00 at one-second steps; block 50 (body[295..300]) `0A` × 6 → `00 00 FF 00 00 FF` during the crossfade and back after; SequenceMain (46 bytes) unchanged | The crossfade of E9's PC GO with STACK unlit (F101, F134: record 5 Live / Next and a 5.0 s time, the default of F109 / H45, counting down in tenths: INFERRED); the stack's step did not move (owner statement; F130) |

**Two independent sessions for F167's core.** Channel fader 1 rising from our software was followed in attempt 2 (15:53, four events)
and in this guided session (22:49, five events), both with the bus and the log in agreement; the return to zero was followed in this
guided session and again in session 3 (below, log only).

## Health (guided session)

| Check | Result |
|---|---|
| Framing (backend) | 0 errors over 49 426 transfers; H15 stale words 24 263 of 24 263 ok; H16 type-3 echoes 325 of 325; H17 IN payload counter 0 … 60 with two wraps and no break; the 116 OUT payloads 0x00 … 0x73 |
| USBD status (bus) | 0 non-zero over 49 716 transfers; no cancelled read, no pipe reset |
| Reply timeout, fault, incomplete write | None; `INCOMPLETE-WRITE.txt` absent; the session ended by the application at an idle point |
| Cadence | 24 148 polls in 952.7 s (poll-to-poll about 31 ms idle, 45 ms around fetches, as E8 / E8b); 325 fetches; 248 lists: 1 at Connect, 24 for W0, 11 for W1, 2 each for W2, W3 and W4, 47 for W5, 2 + 6 + 1 for W6, 4 for the console's own bump, 36 for W7, 110 for W8 |

## The two sessions after the guide (23:04–23:10; a deviation from E11's conditions)

After the guide's END (23:04:08) and the SD save `SHOW22.ASC`, the owner started the backend again by hand, twice (statement 1 in the
owner-statements file: "I tried to change the channels fader 1 when the console was in memories mode"). USBPcap had been stopped at
23:02:56 and no SD save followed, so E11's conditions (USBPcap in parallel, the SD pair) were **not met** for these sessions; they are
evidence of the backend's log alone and are kept in full (`…backend-2.jsonl`, `…backend-3.jsonl`).

- **Session 2 (23:04:35, read-only, 2.97 s):** the pending list [DisplayContent, LedStatus, FileName] fetched, FileName = `SHOW22.ASC` (the
  save made a minute before, F103), then idle replies; 0 writes.
- **Session 3 (23:06:59, live control, 241.7 s, 57 writes):** nothing pending at Connect (session 2 had fetched it; 12.6 s of idle replies,
  F166); at 12.6–13.8 s the physical fader 1 moved (FaderInfo body[1] 0x10 → 0x20 → 0x00) and at 15.3 s the panel's FADERS MEMS put the
  console in memory mode (ConsoleStatus body[0] = 1), where the physical fader 1 was raised to 0x1A and back (memory fader 1 with channels
  7 and 8, F112). The UI's channel fader 1 was **refused** by the interlock in memory mode (the owner's quoted message; a refusal is no
  transfer and is not in the log). Then: **three memory bump 1 taps** from the UI (pseq 0–5, 20.6–23.1 s, press / release 48–74 ms;
  lists only, the fetched objects unchanged, as in W6); FADERS 1-24 on the panel (27.4 s); **17 channel fader 1 events** (pseq 6–22,
  38.8–44.5 s and 65.1–66.7 s: 0x09 … 0xC6, 0xFF, 0x00, 0x0E, 0x60, 0x00, then 0x76 … 0x03, 0x00), each followed in FaderInfo, ChannelLevels,
  OutputLevels, LedStatus and the LCD's bar glyph (DisplayContent body[3] 0x20 → 0x80 → 0x81 → 0x82 → 0x85 → 0x86 → 0x20 as the level rose
  to full and fell); the panel's mode keys toured (ConsoleStatus body[0] 0 → 1 → 0 at 47.0 / 53.5 s; **body[4] 0 → 1 at 55.8 s = bank 25-48**,
  with LED block 55 off, block 56 on and the LCD row labels ` 1` / `13` → `25` / `37`, F98; body[0] 1 at 56.3 s with body[4] still 1;
  both 0 at 56.9 s; memory mode again 69.8–174.8 s); **17 bump 7 taps** from the UI in channel mode (pseq 23–56, 176.3–195.9 s, 47–78 ms;
  lists only, the fetched objects unchanged); Disconnect at an idle point. Placement as in the guided session: reply age 26.3–49.1 ms (57
  of 57 at most 62 ms), the next poll or fetch 15.1–31.0 ms after the payload, 0 framing errors, no fault.
- **What can have changed:** only live state (the whitelisted classes under the same interlocks; no record key LED changed in the log, no
  Memory or System object was listed); no show data by the messages used. **Verified afterwards:** `SHOW23.ASC`, saved by the owner and copied
  2026-11-02, equals `SHOW22.ASC` byte for byte. The guide now ends with a reminder that a later backend run is outside the session's conditions unless USBPcap
  runs and an SD save pairs it.

## The owner's question: can a memory fader be driven without switching the console to memory mode?

Asked in the chat the same night (statement 1). The answer from the evidence, in short (the full text is in the chat and in
`safety-checks.md`'s "Path to a wider clearance"):

- The only PC → console live-control message ever observed is ConsoleEvent, which carries the number of a **physical** control: a fader
  index 0x00–0x17, 0x30, 0x31 or a key code = LED block − 1 (E9, E9b, F131–F134, H82; S11.15 found no other live-control message in
  SmartSoft). What the console does with it depends on its own fader mode, as for the panel: FaderInfo records and LED blocks 1–24
  belong to the 24 physical faders in every mode and bank (F99), in memory mode physical fader k is memory fader k of the shown page
  (F112), and the key code 0x00 is channel bump 1 in channel mode and memory bump 1 in memory mode (F134, E9 / E9b).
- SmartSoft's Console Simulator, the only PC sender seen, pressed FADERS MEMS (0x39) before touching a memory bump and FADERS 1-24
  (0x36) to return (E9 seq 39471, E9b), and sent fader events only in channel mode, bank 1-24 (all 215 with the MASTER known).
- So the refusal is **both**: SC-2's gate (channel fader 1 is cleared in the state SmartSoft sent it in) and, underneath, the console's
  model: a fader event in memory mode was never observed, and by F99 / F112 / H82 it would most likely move the **memory** fader of that
  physical position, not the channel (H111, untested). A message that sets "memory fader k of page p" or "channel 25" directly is not known
  to exist; our code will never guess one.
- Consequence for the UI: the console's 24 faders are one bank at a time; the FADERS 1-24 / MEMS keys in the UI are the PC's only way to
  change it (both worked in W6 and are the only mode controls cleared). Memory faders and bank 25-48 from the PC need a passive capture of
  SmartSoft doing it first (E9c) and a safety check; nothing of that is cleared today.

## Owner questions (attempt 3)

1. The sessions after the guide are not covered by the `SHOW21` / `SHOW22` pair: at the next opportunity, save the show to the SD card
   (`SHOW23.ASC`) and copy it; it should equal `SHOW22.ASC` byte for byte. **Answered 2026-11-02: `SHOW23.ASC` = `SHOW22.ASC` byte for byte.**
2. Was the console power-cycled between attempt 2 (15:56) and this session (22:46)? (H65 / F166: the pair LedStatus + DisplayContent was
   pending at the guided session's Connect, as after a power-on.)
3. During session 3 (23:07–23:10): besides the refused channel fader 1 in memory mode, the three memory bump clicks, the fader 1 drags and
   the 17 bump 7 clicks, did you try anything else in the UI (a refused control, a memory fader)?
4. The UI's bumps are taps by SC-2's condition 4 (press and release as one operation, never a held bump), so "click and hold" flashes once;
   should the UI say so on the button (a tap), or do you want a held bump considered in a later safety check (it needs SmartSoft's held
   bump, seen in E9 / E9b, and a decision on a lost release)?

## Status proposals (for the curator; nothing here is clearance to send)

- **F167 → VERIFIED in part** (channel fader 1 rising from our software is followed like a physical fader move: attempt 2 and this guided
  session, two sessions of our own host, each with the bus and the log; never clearance to send); the return to zero OBSERVED (the guided
  session and session 3 of the same evening, the latter in the log only).
- **New fact F168 (OBSERVED once):** key events from our software (bump 7 tap, SOLO, BLACKOUT, FADERS MEMS, memory bump 1 tap, FADERS 1-24,
  GO with STACK unlit) are answered by the console as the panel's keys and SmartSoft's are (the LED blocks 52, 53, 50, the mode objects, the
  GO crossfade); a 47–78 ms tap of a bump leaves no trace in the fetched level objects although the console lists them for the press and the
  release.
- **New fact F169 (OBSERVED once):** MASTER and BUMPS fader events from our software are followed in FaderInfo body[194] / body[193], with
  the block 53 MASTER blink appearing below full and clearing at 0xFF; the outputs were not exercised (every level at zero).
- **H66 (for writes): CONFIRMED** for every control of SC-2's classes A and B1 that the device sends (OBSERVED once each, our own host, one
  guided session; channel fader 1 in two).
- **H65 / F166:** annotate: the power-on pair pending at the guided session's Connect (owner question 2); nothing pending at session 3's
  Connect after session 2 had fetched [DisplayContent, LedStatus, FileName] (FileName = the last SD save, F103).
- **New hypothesis H111:** a ConsoleEvent fader event addresses the physical fader at that index, so in memory mode index 0x00 would move
  memory fader 1 of the shown page and in bank 25-48 channel 25 (as F99, F112 and the key code 0x00 suggest); test: E9c, SmartSoft's
  Simulator fader 1 moved in memory mode and in bank 25-48, passive.
- **SC-2:** E11 is done (W1–W8, the bus = the log, 0 errors, no show data changed in the guided session); its decisions and conditions are
  unchanged; nothing further is cleared; the two sessions after the guide are a recorded deviation (log-only evidence; `SHOW23.ASC` = `SHOW22.ASC`,
  2026-11-02); the owner's wish (memory faders and bank 25-48 from the PC) goes to the E9c list of "Path to a wider clearance".
