# 2026-11-01 E11, attempt 1: the first live-control session of our own software, no write sent (SC-2 classes A and B1)

**Result in one line: our software sent 5 183 polls over two sessions and nothing else; the first control was refused by our own
interlock, as designed, because the console never reported its state (0 lists in 192 s); no PC → console event was sent, the
console's behaviour towards one is still unobserved (H66 for writes), and the show is unchanged.** The guide was corrected the same
morning (a wake-up step from the panel, a refusal handled as "not sent"); attempt 2 is the owner's.

- **Date:** 2026-11-01, morning. The guided script `tools/usb-probe/e11-guided-session.ps1` (commit 7789a71) ran 10:00:12–10:15:05
  local time (CET, UTC+1). USBPcap ran 10:04:32.743519–10:14:02.690062 (the raw root-hub capture's first and last frames), from
  before the Zadig bind. The committed console-only split runs from 10:04:32.743519 (f1) to 10:12:51.507004 (f20750).
- **What:** the backend at commit 5416d7f, started by the script with `--Device:Mode=Usb --Device:Usb:ConfirmDate=2026-11-01
  --Device:Usb:LiveControlDate=2026-11-01` (10:06:10) after the Zadig bind of interface 1 to WinUSB (10:06:09; ETCHSP before), and the
  UI (`npm start`). Two sessions: **10:07:55–10:08:12** (17 s, 425 polls; a Connect pressed before the guide's Connect step, then a
  Disconnect; not in the action log) and **10:09:39–10:12:51** (192 s, 4 758 polls; the guide's Connect at 10:09:49, the idle minute,
  then W1 at 10:12:01). Both sessions had live control on (the log's second note). In both the console answered every poll with the
  idle reply: **0 lists, 0 fetches, 0 objects, 0 writes, 0 framing errors.**
- **The first control (W1, 10:12:01):** channel fader 1 dragged to about 20 % in the UI. The device refused it before any transfer:
  "setChannelLevel(1): Refused: the console has not reported its key LEDs in this session yet (SC-2 conditions 5 and 8)" (the owner's
  transcription of the UI's message). The guide's H66 question did not foresee a refusal, read the owner's "no" as "the console did
  not follow a sent event" and ended the session; the owner pressed Disconnect at an idle point (10:13:04). Correct behaviour of the
  device (condition 8: nothing before the console's first list and its LED state), a wrong reading by the guide.
- **Why the console reported nothing:** it lists an object only when it changes (F57, F94) or when a host's System pair makes it list
  everything (F58; our software never sends it, SC-2 class E). Between the power cycle and the first Connect the owner touched no
  control, so nothing changed; and the "power-on" pending list that E3b and E8b saw after a power cycle (LedStatus and
  DisplayContent, H65 case (a)) **did not appear**: this console had been on for minutes, had saved `SHOW17.ASC` from its own menu
  (10:03) and had ETC's driver bound until the Zadig bind, none of which E8b's history had. Which of these cleared or prevented the
  pending list is UNKNOWN (no capture covers the interval before 10:04:32). OBSERVED once; H65 is annotated, not changed.
- **Console:** ETC SmartFade 1248, USB device address **35** on bus 1 after the bind (the bind re-enumerated it; the addresses 32, 33,
  34 of the audio device, a hub and the keyboard were given in the same minute), firmware 3.0.2.9.0.43 (typed from Diagnostics),
  "the A-B cable straight into the PC's rear port", Windows 10.0.26300.0, backend 5416d7f.
- **Show backup (safety rule 3): taken.** `SHOW17.ASC` at 10:03:05 before anything else; `SHOW18.ASC` saved after the session
  (10:15:05). Both SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, byte-identical to each other and to
  SHOW04 … SHOW16: **no show data changed**, as nothing was sent.
- **Rig:** the owner's rig on DMX 5–16; the owner accepted the live-output steps (the log's acceptances, all y); none was reached.
- **Owner statements:** `2026-11-01-e11-owner-statements.txt` (the chat message, the typed answers quoted, the assistant's reading).
- **The acceptances SC-2 asks for** were given in writing in the log at 10:00:31–10:02:25 (the re-check items closed; H66 unknown; the
  last mover; the lost release; the live output; the counter 0x00; the release after a fault with the decode-failure case; the rig).

## Files

| File | SHA-256 | Size / span | Committed | Role |
|---|---|---|---|---|
| `2026-11-01-e11-first-live-control-session-attempt-1.backend-1.jsonl` | `ad66b7fc753ec0af198e4f462c1d807caabf2b279ad52cf17be52b5c1feabf3a` | 855 lines, 850 transfers, 10:07:55.474 + 0.02 … 17.42 s | yes, **the evidence of our transfers** | The backend's own transfer log of the first (unplanned) session: `2026-11-01-090755-usb-session.jsonl` from `%LOCALAPPDATA%\SmartFade\captures`, copied; `status` = `null` (no USBD status in the backend's log), the notes "live control on (SC-2)" and "polls 425, fetches 0, lists 0, objects 0, writes 0" |
| `2026-11-01-e11-first-live-control-session-attempt-1.backend-2.jsonl` | `058d18029dffcadfc7d692f6fe4ed79d8d3f3c2525e2353d552a68404dee6ff7` | 9 521 lines, 9 516 transfers, 10:09:39.771 + 0.00 … 191.74 s | yes, **the evidence of our transfers** | The backend's log of the main session: `2026-11-01-090939-usb-session.jsonl`, copied; "polls 4758, fetches 0, lists 0, objects 0, writes 0", "session stopped by the application at an idle point" |
| `2026-11-01-e11-first-live-control-session-attempt-1.pcap` | `6649baba6c2b79317e414ce4f280aea9fbcea50cdbf966204d7732b05d32d2cf` | 1 016 852 B, 20 750 frames, 10:04:32.743519–10:12:51.507004 | yes, **the evidence for the bus** | Console-only split (device address 35) of the USBPcapCMD root-hub capture; tshark 4.0.3: `tshark -r <raw> -Y "usb.device_address == 35" -F pcap -w <split>`. **The raw root-hub capture is not committed** (addresses 1, 2, 3, 32, 33, 34; the owner's keyboard at 34): `e11.pcap` in the repository root (git-ignored), SHA-256 `24fc0f315520092b997ae3a5079cd081801bf5b6c3a855a677063b3d87bc946b`, 4 558 945 B, 80 341 frames (tshark field census), 10:04:32.743519–10:14:02.690062 |
| `2026-11-01-e11-first-live-control-session-attempt-1.usbpcap.jsonl` | `a2b00419960dd47ad3edda5ff33ec4475db45341d06bdf92b1e6e703da971b66` | 10 424 lines: 1 header, 10 377 `frame` records, 46 `note` records | yes (derived) | The split normalized with `usbpcap_to_jsonl.py` version 2, `--device-address 35 --bus 1`, the action log merged as notes plus two analyst notes (provenance; the end) |
| `2026-11-01-e11-actions.txt` | `0365c9b84d9860f287f6aac2f20c792f5f95aa620f163a91da0b934358659805` | 44 lines, 10:00:12.008–10:15:05.514 | yes | The guided script's action log: the gates and acceptances, the versions, every step with the PC clock, the owner's answers |
| `2026-11-01-e11-owner-statements.txt` | `6d1b7b59ccdcdd39b935e1862ebabe91e3eafa097f762e35310990b1e6cb1ca0` | | yes | The owner's statements, quoted, with the reading for this record |
| `smartfade-sd-card-backup/SHOW17.ASC`, `SHOW18.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` (both) | 15 944 B each | no (git-ignored folder) | The backup before and the save after; equal to each other and to SHOW04 … SHOW16 |
| `tools/protocol-analyzer/usbpcap_to_jsonl.py`, `hsp_summary.py` | unchanged since E9b | | yes | Normalizer version 2; `summary` |
| `cmp_bus_log.py` (session scratchpad, 30 lines, not committed) | | | no | The bulk transfers of the usbpcap JSONL against the two backend logs, direction and bytes in order |
| tshark / capinfos 4.0.3 (Wireshark portable) | | | no | The split, the frame census by address, the spans |

**Completeness and the bus against our log:** the split holds every frame of address 35 in the raw file (20 732 bulk, 7 control, 4
interrupt, 4 of USBPcap type 0xFE, 4 of type 0xFF, 1 injected descriptor = 20 750 by the census; the raw file's other 59 591 frames
belong to addresses 1, 2, 3, 32, 33, 34). **The 10 366 bulk transfers on the bus equal the two backend logs byte for byte, in order
(0 differing), the first session's 850 then the main session's 9 516:** 5 183 polls, 5 183 idle replies, nothing else from our
software. The three non-zero USBD statuses are at the Zadig bind (10:05:20, t = 47.5 s): two cancelled reads on the HID interface's
interrupt pipe 0x81 (0xC0010000) and one STALL on the control pipe (0xC0000004), the re-enumeration's own traffic as in E8b (F123);
none on the bulk pipes. Stale words (F52) ok in 5 182 of 5 182 polls after the first; framing errors 0.

## Structure

- Our cadence: poll-to-poll median 45.7 ms (bins: 30–32 ms 1 640, 44–47 ms 2 743, the rest spread to 60.8 ms), reply header
  completion median 1.9 ms, max 7.1 ms. Live control's write slot timing (`WriteAfterPoll` 31 ms) stretched the idle cadence from the
  read-only sessions' ≈31 ms towards 46 ms whenever the loop peeked a possible write slot (the queue was empty: no write was ever
  queued; the loop waits for the kind `PeekNextKind` names, Poll here, with the poll floors; the 44–47 ms bins are for the analysis:
  INFERRED from the code, the owner's E8b read-only session had a 31 ms median).
- The console: idle replies only, in both sessions, from the first poll to the last (seq 1 … 9 516 of the main log).

## What this attempt shows and what it does not

- **Shows (OBSERVED, our software):** the live-control session starts, polls, is read-only in effect while the console reports nothing,
  refuses a command with its reason before any transfer, stops at an idle point, and leaves no marker (no `INCOMPLETE-WRITE.txt`); the
  bus equals its log byte for byte (as E8b). The UI showed HARDWARE and LIVE CONTROL before Connect (owner, 10:07:45).
- **Shows (OBSERVED once, the console):** a console that has nothing pending answers a host without the System pair with idle replies
  only, here for 192 s; the power-on pending list of H65 (a) was not pending after this history (see above).
- **Does not show:** anything about a PC → console event from our software (none was sent): H66 for writes, the counter 0x00, the
  console's answer to our ConsoleEvent, the write cadence, the release path. **E11 is not done; SC-2's conditions for it are unchanged;
  nothing is promoted and nothing is cleared.**

## The correction (same morning, before attempt 2)

- **The guide** (`e11-guided-session.ps1`): a new step **W0** after the idle minute makes the console report its state from the panel
  (FADERS MEMS, FADERS 1-24, SOLO on and off, the MASTER down a little and back to full: ConsoleStatus, LedStatus and FaderInfo get
  listed and fetched), asks whether the UI shows the LEDs, the MASTER and channel mode and has the live controls enabled, repeats the
  four actions once if not, and stops before any control if the state is still not reported. **W1** now distinguishes a refusal
  (nothing sent: repeat W0 once, then W1 once; a second refusal ends the attempt without a write) from a sent event that the console did
  not follow (H66: the session ends, nothing is retried). The end-of-session question names the UI's messages precisely.
- **SC-2's "Conditions for E11"** gets the same condition (the console's state reported from the panel before the first control).
- **Nothing in the device changed**: the refusal is right. A read-only session cannot ask for an unlisted object (SC-1 clears the fetch
  of a listed record only), so the panel is the only way to make the console report its state without SmartSoft's System pair.

## Anomalies and notes

1. A Connect at 10:07:55 (17 s, 425 polls) before the guide's Connect step, not in the action log (the owner tried the UI's button
   after the badges appeared); harmless, the console reported nothing to it either.
2. The guide's "which controls are enabled" question was answered with the bar's items ("CONSOLE, SHOW FILE, CONNECT"): before Connect
   every fader and key is disabled, so the question was mistimed; reworded.
3. "end-of-session report = yes" against a log that reports nothing pending: read as a misreading of the question; reworded.
4. The guide logged "E11 controls covered: …" on the stop path although none was: the line is a fixed summary; it now follows the
   optional GO step only when the session ran (the stop paths say what stopped them).
5. No `INCOMPLETE-WRITE.txt` in the capture folder after the session (checked by the guide and by hand).

## Safety

Nothing but the poll was sent (5 183 polls, 0 fetches, 0 writes); the device's interlock held; the show is unchanged (SHOW18 =
SHOW17). E11 attempt 1 clears nothing and changes no decision of SC-1 or SC-2.
