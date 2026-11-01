# E11, attempt 2 (2026-11-01, afternoon): the first PC → console writes of our own software

**Status of this record:** complete for what was run. Attempt 2 covered the guide's step W1 only (channel fader 1 raised from the UI);
the session ended on a mistyped answer in the guide, not on anything the console did. **Four ConsoleEvent fader events (SC-2 class A,
index 0x00) went out and the console followed each one** as it follows SmartSoft's events and a physical fader. The controls W2 to W8
(bump 7, SOLO, BLACKOUT, BUMPS, the optional key taps, MASTER, the optional GO) and the return of channel fader 1 to zero were not
tried; they are attempt 3's. **Nothing is cleared by this record** (SC-2's conditional GO and its conditions are unchanged); the status
proposals are at the end, for the curator.

Evidence class 1 (our console, two independent recorders): the backend's own transfer log and a parallel USBPcap capture of the bus,
equal byte for byte. The decoding of the console's answers uses the documented layouts of `responses.md` (FaderInfo, ChannelLevels,
OutputLevels, LedStatus, DisplayContent) and names the fact behind each index.

## Provenance

| Item | Value |
|---|---|
| Session | The guided E11 session, `tools/usb-probe/e11-guided-session.ps1` at commit 85aa2f5 (the W0 version with the per-attempt pcap name), run by the owner from 15:37:52 to 15:56:16 local time (CET, UTC+1). Action log `2026-11-01-e11-attempt-2-actions.txt` (SHA-256 `60f2206829a475a039e56c283b645838a40823c55138e807e86b9da44b02b397`, 55 lines). Owner statements `2026-11-01-e11-attempt-2-owner-statements.txt` (`55d691ec49b6afcb46613c9720388b4e349450f438bbb96dd51836bafd637728`) |
| Gates (from the log) | The SC-2 re-check items closed = y (15:41:21); the acceptances H66 unknown, last mover, lost release, live output, counter 0x00, release after fault = y (15:42:07–15:46:25); rig decision = y; the optional memory bump and GO = y; backup `SHOW19.ASC` saved on the console before the backend started (15:47:11); backend commit 85aa2f5; Windows build 10.0.26300.0; firmware 3.0.2.9.0.43 (the owner's Diagnostics reading); cable: the A-B cable straight into the PC's rear port; "power cycled today before this session" = y (the morning's cycle before attempt 1 counts, so whether the console was cycled again between the attempts is an open owner question, below) |
| Driver | Interface 1 on WinUSB since the morning's attempt 1 (the guide read the service WinUSB before and after its Zadig step; no Replace Driver was run; the bus shows no control transfer to the console but the six descriptor frames USBPcap injects). Interface 0 (HID) untouched by us |
| Backend session | One session, 15:50:08.194–15:55:07.347 local (299.15 s): `2026-11-01-145008-usb-session.jsonl` from `%LOCALAPPDATA%\SmartFade\captures`, committed as `2026-11-01-e11-first-live-control-session-attempt-2.backend.jsonl` (SHA-256 `3db9a4555ecd876e60976fb3ddb604ca83c2855e5c620f7760e41985601955ac`, 15 364 lines: 15 351 transfers, 12 notes). Its first notes: "live control on (SC-2): only the whitelisted ConsoleEvents may be sent, behind the record-function interlock". Its last: "session stopped by the application at an idle point; polls 7610, fetches 47, lists 29, objects 47, writes 4" |
| Bus | USBPcapCMD on root hub USBPcap1, started by the owner at 15:48:17 into `e11-20261101-1547.pcap` (the guide's suggested name; SHA-256 `f3efd904c624b51ba2e10822c58bcd1543d51a06851d81ad9614159cb9f6bb94`, 274 436 990 bytes, 208 951 frames, 15:48:17.439149–15:55:33.442129). **Not committed:** it holds other devices (addresses 1, 2, 3, 32, 33, 34: an isochronous device with 269 MB of the file, a hub, the owner's keyboard). Census by `tshark -T fields`: the console is **device address 37** (30 778 bulk frames, 6 control frames = the injected descriptors). Console-only split `2026-11-01-e11-first-live-control-session-attempt-2.pcap` (`tshark -r <raw> -Y "usb.device_address == 37" -F pcap -w <split>`; SHA-256 `75c5ec90ed816e7ea3ac8939380c0f46f3fba1511685a1a3a850798473135acf`, 1 519 904 bytes, 30 784 frames, 15:48:17.439149–15:55:07.346823) |
| Normalized bus | `2026-11-01-e11-first-live-control-session-attempt-2.usbpcap.jsonl` (SHA-256 `dacfb8c93bf4d6e90d8bf7588e4bc73d2025a2c8adfa618d26e4f8dd4e9bdc2d`; `usbpcap_to_jsonl.py` v2 with the action log merged as notes at UTC+1, 56 notes): 15 392 transfers, 7 665 bulk OUT, 7 724 bulk IN, 3 control (the descriptors), **non-zero USBD status 0**. `hsp_summary.py` reports 76 "framing errors" on the bus file: all 38 reads longer than 64 bytes split into two URBs (200 = 192 + 8, 456 = 448 + 8, 519 = 512 + 7; F122), each counted twice; the backend's log, which reads whole transfers, has 0 |
| Bus = log | `tools/protocol-analyzer/e11/bus_vs_log.py` (the multi-URB reads joined): **15 351 bulk transfers on the bus = 15 351 in the backend's log, 0 differing in direction or bytes, 38 joins.** Every transfer our software logged is on the bus and nothing else went to or came from the console |
| Show files | Backup `SHOW19.ASC` before the backend started; after-session save `SHOW20.ASC` after the backend and USBPcap had stopped (the guide's order; no SD operation while connected). **Comparison pending:** the owner copies both into the git-ignored `smartfade-sd-card-backup/`; the expectation is byte-identical files (a fader event changes live state only) |
| Tools (read-only) | `tools/protocol-analyzer/hsp_summary.py` (summary, timeline), `write_rules.py` (placement and timings), `tools/protocol-analyzer/e11/writes.py` (each write with its neighbours and the console's answer), `e11/bus_vs_log.py` |

## What happened, in session time (the backend's clock; local time = 15:50:08.194 + t)

| t (s) | Local | Event (the log's notes and the owner's action log) | On the bus / in the log |
|---|---|---|---|
| 0.023 | 15:50:08 | Connect; the first poll | The first reply lists **LedStatus and DisplayContent** (payload seq 0, 0.059 s), the pair that E3b and E8b saw pending after a power-on (H65 case (a)); both fetched. The LCD read ` 1             U 5.0` / `13             D 5.0`, the main channel screen, not a menu |
| 0.06–60 | | The idle minute ("nothing changed by itself": owner, 15:51:35) | Idle replies only |
| 120.0 | 15:52:08 | **W0 on the panel:** FADERS MEMS | Lists at 120.955 (ConsoleStatus, LedStatus) and 121.080 (DisplayContent, LedStatus): memory mode |
| 135.4 | 15:52:23 | W0: FADERS 1-24 | List at 137.310 (ConsoleStatus, LedStatus): channel mode |
| 151.0 | 15:52:39 | W0: SOLO on, then off | Lists at 151.511 and 154.046 (LedStatus) |
| 171.2 | 15:52:59 | W0: MASTER down a little and back to full | Lists at 173.422 (FaderInfo, LedStatus) and on, FaderInfo 22 times in all |
| 201.4 | 15:53:29 | Owner: the UI shows the LEDs, the MASTER and channel mode; the controls are enabled | The interlocks are satisfied (conditions 5, 6, 8) |
| 212.5 | 15:53:40 | **W1: channel fader 1 dragged slowly to about 20 % in the UI** | **Four Data writes**, 214.373–215.549 s (15:53:42.567–15:53:43.743), the table below; eight "write held back" notes between them (the interlock holding newer fader values while a list was unfetched or the last report was older than two poll periods, SC-2 condition 5), each released by the next poll exchange |
| 222.5–263.9 | 15:53:50–15:54:32 | The owner answers **b** (case (b): the fader went out and the UI's channel 1 followed within a second); the guide reads the letter as "not followed" and prints the H66 stop | Idle replies; no further write |
| 263.9–297.1 | 15:54:32–15:55:05 | The guide's end steps; the owner presses DISCONNECT (15:55:05) | 299.153: "session stopped by the application at an idle point" |

The guide's W1 question offered three lettered cases but accepted only y / yes / followed for case (b); the owner's "b" fell into the
stop branch. The guide now accepts a / b / c and asks a confirmation before an H66 stop (commit after this session). The physical
faders were never moved; the owner reports the end positions "as it should, default / starting value" and no end-of-session message
(no held key, no unsent release, no incomplete write; `INCOMPLETE-WRITE.txt` does not exist).

## The four writes and the console's answer

Every write is a ConsoleEvent (id 20) kind 0 fader event for index 0x00 (channel fader 1), 8-byte payload `[pseq][04 00][14][00][00][value][previous]`
after the 12-byte type-1 header `01 00 08 00 00 00 00 00 00 00 00 00` (w2..w5 = the last reply header's, 4 of 4, F52; the header's
length word 8 = the payload). The counter started at 0x00 and rose by one (F131); each event's previous byte is the value of the event
before it, 0x00 before the first (F132: 4 of 4, no exception). The values are the UI's fader positions the device had queued when
its slot came (a newer value replaces a queued one, `HspReadSession`; the UI's intermediate positions between them were never sent).

| # | pseq | Header submit (t) | Local | Payload (8 bytes) | value | previous | After the idle reply (ms) | After the poll's submit (ms) | Next poll after the payload (ms) |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 0 | 214.372721 | 15:53:42.567 | `00 04 00 14 00 00 05 00` | 0x05 = 5 (2 %) | 0x00 | 49.36 | 51.14 | 26.44 |
| 2 | 1 | 214.789422 | 15:53:42.983 | `01 04 00 14 00 00 17 05` | 0x17 = 23 (9 %) | 0x05 | 45.50 | 46.42 | 22.78 |
| 3 | 2 | 215.191830 | 15:53:43.386 | `02 04 00 14 00 00 2F 17` | 0x2F = 47 (18 %) | 0x17 | 44.52 | 46.31 | 30.25 |
| 4 | 3 | 215.548982 | 15:53:43.743 | `03 04 00 14 00 00 38 2F` | 0x38 = 56 (22 %) | 0x2F | 43.68 | 46.21 | 15.41 |

Placement (`write_rules.py`): all four after an idle reply with no write since that poll and no listed record unfetched, followed by a
poll; header submit to completion 0.11–0.15 ms, payload 0.09–0.12 ms; header to the next header 357 / 402 / 417 ms (min / median / max).
**SC-2 MAJOR 1's E11 analysis:** the time from the reply's completion to the write header's submit is 43.7–49.4 ms for all four, at most
62 ms as required (the defaults: a 30 ms slot after the poll, seen as 46–51 ms because the host's timer wakes on its tick; a late timer
can only lengthen the hold). The next poll went 15.4–30.3 ms after the payload (the floor is 15 ms). The idle replies before the writes
had come 0.8–2.4 ms after their poll.

**The console's answer to each write, at its next poll** (reply 18.6–33.3 ms after the payload's completion), then fetched one object
per poll cycle (the fetch 59–63 ms after the payload, the object 79–91 ms after it). The byte that moved is the one the documented layout
assigns to fader 1, channel 1, DMX 1 and LED block 1:

| Object (id) | Byte, per `responses.md` | After write 1 | 2 | 3 | 4 | Reading |
|---|---|---|---|---|---|---|
| FaderInfo (14), 200-byte read | body[1] = record 1 byte 0 = the level of fader 1 (F82) | 00 → 05 | 05 → 17 | 17 → 2F | 2F → 38 | The console's fader 1 took the PC value while the physical fader stayed at zero (as for SmartSoft's events, F141) |
| ChannelLevels (15), 56-byte read | body[1] = channel 1 (F83) | 05 | 17 | 2F | 38 | Channel 1 at the sent value (post-MASTER, the MASTER at full) |
| OutputLevels (13), 519-byte read | body[2] = DMX 1 (F83; channel 1 patched 1:1) | 05 | 17 | 2F | 38 | DMX 1 followed |
| LedStatus (22), 456-byte read | body[2] and body[5] = block 1, G of phase A and B (F84: channel mode G = fader level) | 05 | 17 | 2F | 38 | Fader 1's LED followed |
| DisplayContent (21), 46-byte read | body[3] = fader 1's cell (F86) | 0x20 → 0x80 | not listed | not listed | not listed | The LCD drew the lowest bar glyph in fader 1's cell after the first event and had nothing new to list for the next three |
| ConsoleStatus, PlaybackInfo, Independents, SequenceMain, System | | not listed | | | | No show object, no download and no System record was listed; the console acknowledged nothing beyond these lists, as for SmartSoft (E9, E9b) |

The lists named five records after write 1 (reply header `04 00 20 00`), three after write 2 (`04 00 16 00`) with LedStatus alone at the
following poll (215.053 s), four after writes 3 and 4 (`04 00 1B 00`). Between the writes the device fetched every listed object before
it chose the next write (the eight hold notes), so a fader event went out about every 400 ms while each event made the console list four
or five objects; SmartSoft's median write-to-write interval in E9 / E9b was 62 ms under its own, looser placement (it wrote with listed
records still unfetched). This is a measured property of our host under SC-2 condition 5, not of the console; nothing is proposed here.

## Health

| Check | Result |
|---|---|
| Framing (backend) | 0 errors over 15 351 transfers; H15 stale words 7 613 of 7 613 ok; H16 type-3 echoes 47 of 47; H17 IN payload counter 0..75 without a break; the four OUT payloads 0..3 |
| USBD status (bus) | 0 non-zero over 15 392 transfers; no cancelled read, no pipe reset |
| Reply timeout, fault, incomplete write | None; `INCOMPLETE-WRITE.txt` absent; the session ended by the application at an idle point |
| Cadence | 7 610 polls in 299 s (an idle poll-to-poll of about 31 ms as in E8 / E8b); 47 fetches; 29 lists (1 at Connect, 23 for W0's panel actions, 5 for the four writes) |

## What this attempt shows and does not show

- **Shown (OBSERVED once, our own host, one session):** the console accepts a ConsoleEvent fader event from our software, framed and
  placed as SmartSoft's are, and answers it like a physical fader move: the same four live objects listed at the next poll, with the
  sent value in them, and no acknowledgement. H66's open question for writes ("the console may ignore the events") is answered for
  channel fader 1 rising: it did not ignore them. The bus equals the log, so the encoder sent exactly the bytes the golden tests hold.
- **Not shown:** a fader event back to zero (the UI's fader 1 stayed at 0x38 when the session ended); any key event (press / release
  as one operation), MASTER, BUMPS, SOLO, BLACKOUT, GO, the mode keys; the console's state after a Disconnect with a PC value in force
  (F141 says the console keeps the last mover's value; the owner's "end positions" answer describes the physical faders, not the
  console's channel 1 level); a second session (nothing here is VERIFIED).
- **Not changed:** SC-2's decisions and conditions, the whitelist, the interlocks (they held eight times and released each time within one
  poll exchange), the read-only mode.

## Open owner questions (attempt 2)

1. Was the console power-cycled again between attempt 1 (10:15) and this session, or only this morning before attempt 1? (For H65 and
   F166: at this Connect the console had LedStatus and DisplayContent pending, with the LCD on the main screen; at attempt 1's two
   Connects nothing was pending. The console's own menu was used for an SD save before the backend in both attempts.)
2. After the session: did the console's channel 1 stay at the PC value (its LED bar lit at a low level, the bar glyph in the LCD's fader 1
   cell), and did you move the physical fader 1 up and down to take it back? (F141: the last mover wins; nothing in our software can
   release a fader value after a Disconnect.)
3. The UI during W1: did the channel 1 level in the UI follow within a second after each movement, and did anything else in the UI change
   (an LED reading, a message)?

## Status proposals (for the curator; nothing here is clearance to send)

- **New fact F167 (OBSERVED once):** a ConsoleEvent fader event from our own software (index 0x00, values 0x05, 0x17, 0x2F, 0x38, the
  previous-byte chain of F132) is followed by the console like a physical fader move: at the next poll it lists FaderInfo, ChannelLevels,
  OutputLevels and LedStatus (and DisplayContent once) with the sent value at fader 1 / channel 1 / DMX 1 / block 1, and acknowledges
  nothing; evidence: this record, the backend log seq 10999–11095, the bus split, `e11/writes.py`.
- **H66 (for writes): CONFIRMED in part (OBSERVED once)** for channel fader 1 rising; the keys, MASTER, BUMPS and the zero event are untested.
- **F141 (last mover):** supported once more, with our host: FaderInfo record 1 took the PC value while the physical fader stayed at zero.
- **H65 / F166:** annotate: at this Connect, after a history close to attempt 1's (an SD save from the console's menu minutes before, the
  interface on WinUSB throughout), the pair LedStatus + DisplayContent was pending; which difference decides it stays UNKNOWN (owner question 1).
- **SC-2:** E11 is partly done (W1); attempt 3 covers W1's return to zero and W2–W8 with the corrected guide; the conditions and decisions are
  unchanged; nothing further is cleared.
