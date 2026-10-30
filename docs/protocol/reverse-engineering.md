# Reverse-engineering plan

## Safety rules (non-negotiable)

1. **Passive before active.** Observe SmartSoft ↔ console traffic before our software sends
   anything to the console.
2. **Never send captured or hand-built bytes to the console** until the message is understood
   and its effect is known to be harmless (e.g. a read/poll). Replaying edit messages can
   overwrite show data. *E2 note (2026-10-28): even polls and fetches have console-side
   effects (the console's sequence counter advances per payload, a listed dirty object is
   delivered once), so "read" is not automatically harmless; nothing is cleared for sending.* *(SC-1, 2026-10-30: the poll and the fetch of a listed record are cleared for a read-only session,
   [safety-checks.md](safety-checks.md); the note stands for everything else.)*
3. **Back up the show to SD card before every session** involving SmartSoft or our tools.
   SmartSoft "Load show" pushes a show onto the console.
4. **Never touch the firmware/bootloader path** (self-test "Firmware Upgrade", SFDownloader).
5. Keep dimmers/fixtures on a safe rig (or DMX output disconnected / monitored) during active tests.
6. Record evidence for every experiment: date, console firmware version, SmartSoft version,
   OS, cable/adapter, capture file name, observed result.
7. SmartSoft installation on a production PC is at the owner's discretion. A VM or a spare PC is
   preferred (the installer adds a kernel driver and a firewall rule).

## Experiments (ordered by uncertainty removed per unit of risk)

| ID | Experiment | Risk | Needs | Answers |
|---|---|---|---|---|
| **E1** | **USB identification.** Power the console in Normal mode, connect USB to a Windows PC *without* SmartSoft installed. Record Device Manager entry, `pnputil /enum-devices /connected /ids` output, and a descriptor dump with USBTreeView (Uwe Sieber). Repeat on the PC where SmartSoft is installed. | None (read-only) | USB A-B cable, PC | H1, H2. VID/PID, number of interfaces, endpoint types and sizes, HID report descriptor, serial string, bcdDevice (firmware?). **Done 2026-10-28** → [captures/2026-10-28-e1-usb-identification.md](captures/2026-10-28-e1-usb-identification.md): H1 and H21 confirmed; raw HID report descriptor not obtained (structure from `HidP_GetCaps` instead); `etchsp.sys` does not load under HVCI |
| **E2** | **SmartSoft session capture.** Install Wireshark + USBPcap. Start capture, launch SmartSoft with the console connected, wait until "Online", idle 30 s, close SmartSoft. Save as pcapng. | None to the rig. SmartSoft may sync the show, so back up first | E1 done, SmartSoft installed, matching firmware. **Blocker found in E1:** `etchsp.sys` is refused while Memory Integrity (HVCI) is on (F46), so SmartSoft cannot go Online on the owner's PC as configured. Options: the owner turns Memory Integrity off for the session (Windows Security > Device security > Core isolation; reboot), or a VM / spare PC with it off. **Resolved 2026-10-28 evening:** the owner switched Memory Integrity off (reboot 19:45), the driver loads (F49), USBPcap 1.5.4.0 is installed in `C:\Program Files\USBPcap`. The owner wants Memory Integrity back on afterwards, so our own backend must never depend on `etchsp.sys` (H7) | H3, H4. Framing, poll cadence, version exchange, bulk vs HID usage. Also H29 (what enables the HID stream). **Done 2026-10-28** → [captures/2026-10-28-e2-smartsoft-connect.md](captures/2026-10-28-e2-smartsoft-connect.md): USBPcap 1.5.4.0, 125 s of SmartSoft traffic, 6 483 URBs. H3, H15–H20, H26 (length part), H27 confirmed as OBSERVED; H4 and H5 partly; H22, H23, H28, H29 rejected (as worded); facts F51–F69; F41 (product string) and F48 promoted to VERIFIED. Format caveat: the capture is classic pcap (link type 249), not pcapng. SmartSoft wrote only two System messages and the HID interface was never used |
| **E3** | **Single-action captures.** One capture per action, each with a spoken/typed timestamp log: (a) move channel fader 1 from 0 → full → 0 slowly on the console; (b) press one memory bump; (c) move GM; (d) press GO on an empty/safe stack; (e) in SmartSoft rename one channel label. | Low (backup first) | E2 | H5, H6. Which objects change for which action, level encoding. **E3 (a), (b) and (c) done 2026-10-29** → [captures/2026-10-29-e3-console-actions.md](captures/2026-10-29-e3-console-actions.md): one guided passive capture after a console power cycle (USBPcap 1.5.4.0, 313 s, 20 326 URBs): channel faders 1 and 5, the bump of memory fader 1 in memory mode, the MASTER fader (Grand Master), plus an unlogged switch to memory mode; the owner's answers of the same evening are archived in [captures/2026-10-29-e3-owner-statements.txt](captures/2026-10-29-e3-owner-statements.txt). Results: the mirrored objects and their layouts (F80–F92), the **second independent reproduction of E2** (F41, F51–F55, F57–F65, F67, F68, F70–F77 promoted to VERIFIED as observed behaviour, never as clearance for sending), H30 partly confirmed (causality INFERRED), H32 layout confirmed and its "0xAB is the top" sub-reading rejected, hypotheses H46–H59 added. **(d) GO on a safe stack and (e) the SmartSoft-side label rename were not run and stay open**; (e) is also E6 probe 5 and needs an SD save |
| **E3b** | **Live objects, three SmartSoft sessions (passive).** The proposals B1–B4 of the E3 analysis ("Next capture (proposed by the E3 analysis, 2026-10-29)" below), run with the guided script `tools/usb-probe/e3b-guided-session.ps1` (backup first, SmartSoft started three times by the script, owner actions with logged times): B1 restart SmartSoft twice without a power cycle, B2 a channel at full while the MASTER moves, B3 the BUMPS fader, a channel bump and memory bumps with SOLO off and on, B4 the 25-48 bank and the mode switches. | Low (backup first; nothing sent by us) | E3 done, a console power cycle, the owner at the console | H31, H46, H47, H54, H57, H59, H53, H56, H58. **Done 2026-10-30** → [captures/2026-10-30-e3b-live-objects.md](captures/2026-10-30-e3b-live-objects.md): three SmartSoft sessions after another power cycle (USBPcap 1.5.4.0, 53 935 URBs, console frames 0–1008.97 s); B1–B4 done; **B5 (GO)** was declined in the script, but the owner pressed GO once, unlogged, with the STACK button unlit (a 5-s crossfade, no output change; owner statement E3b-A1); the owner's answers are archived in [captures/2026-10-30-e3b-owner-statements.txt](captures/2026-10-30-e3b-owner-statements.txt). Results: F93–F106; F80, F82–F87, F89 and F92 VERIFIED in part (VERIFIED count 35, never clearance for sending); H31, H46 (BUMPS part), H47 (scaling part), H54 and H50 (layout) CONFIRMED; H57 and H59 REJECTED; H60–H65 added |
| **E4** | **MIDI observation.** Console MIDI OUT → USB-MIDI interface → PC. Record with a MIDI monitor (e.g. MIDI-OX or our tool). Move GM, bumps master, crossfaders, a memory fader, press a bump, GO, PAUSE, blackout, change page. | None | USB-MIDI interface, MIDI cable | H9. Resolves MIDI doc contradictions for TX |
| E5 | MIDI control test. PC → console MIDI IN, one message at a time: GM CC127 0 → 127, then PC 125/124, observe console + DMX out | Low-medium (blackout/GO) | E4 done | Verified MIDI write path |
| E6 | ASC round-trip probes. On console: record a memory on page 2 fader 5, a channel step, a stack state step, set MIDI channel 3, patch IND1. Save to SD. Diff with previous file | None | SD card | H10–H14 |
| *(E6 probes added 2026-10-28 from the SHOW04.ASC cross-check; each is a console-menu action followed by an SD save and, where noted, an E2-type SmartSoft capture; nothing is sent by us)* | (1) **Page / fader meaning (H13, H25):** record a memory on page 2 fader 5, expect fetch qualifiers (1, 4) and `$$MEMORY 1 4`, `GROUP 29` (24 × 1 + 4 + 1). (2) **Sequence step numbering (H43):** step through a sequence on the console and note the number shown; compare with wire 1.0-7.0 and file `$CUE`. (3) **"Wait" mapping (H14):** set a known Wait on a step, expect it in the third time of the step record (F74). (4) **A patch level other than 100 % (H34):** change a proportional level, save, and find which object carries it (not the Patch body, F70); no SD show has one. (5) **A label with a character >= U+0100, such as "č" (H40):** settles the UTF-16 byte order (BE is INFERRED, F73). Also: step types CUEPARAMS 0 / 2 (H12), Independents fields (H44), SystemSettings keys and default times (H45), memory levels at 1 / 50 / 99 % (H39). | None | SD card; SmartSoft + USBPcap for the capture parts | H10-H14, H25, H34, H36, H39-H45 |
| E7 | DMX output observation (optional). USB-DMX input or DMX analyser on console DMX OUT | None | DMX interface | Ground truth for "actual state" of outputs |
| **E8** | **First read-only session of our own software (active, SC-1).** The backend in `--Device:Mode=Usb` polls the console over WinUSB and fetches what the console lists, as SmartSoft does, but sends neither System write; USBPcap records the bus in parallel. Steps in the runbook below: backup, Zadig on interface 1, idle 60 s, then fader 1, a bump, the MASTER, Disconnect. | Low-to-medium: the first bytes our code ever sends; only the two cleared messages (`safety-checks.md` SC-1), the session ends on the first unobserved reply, nothing is retried | SC-1 conditions 3, 4, 8, 9, 10 met; the owner's go on the day | H66, H67, H68; a second evidence channel for F52, F53, F57 (our own host instead of SmartSoft). **Done 2026-10-30** → [captures/2026-10-30-e8-first-read-only-session.md](captures/2026-10-30-e8-first-read-only-session.md): 861 s over WinUSB on interface 1, 20 763 polls and 2 925 fetches, 0 framing errors, no fault, stopped at an idle point (the backend's own transfer log; **no USBPcap ran, no action log, no named backup**: deviations from runbook steps 1, 3 and 6; the planned actions were replaced by free exploration, the owner's account is untimed, [captures/2026-10-30-e8-owner-statements.txt](captures/2026-10-30-e8-owner-statements.txt)). Results: **H66 and H67 CONFIRMED** (OBSERVED, one session) for what was used (idle polls 31–52 ms apart, fetches 28–47 ms after their list, polls 15–32 ms after a fetch; the SC-1 floors were never reached), **H68 stays UNTESTED** (no USBPcap), **H62 REJECTED** (successor H71), H63 (record 1) and H26 (Independents part) CONFIRMED in capture; F95 VERIFIED and F99, F100, F101 VERIFIED in part (VERIFIED count 39, never clearance for sending), F57's no-de-duplication detail VERIFIED; facts F110–F120 (the first evidence channel other than SmartSoft for F51–F55 and F57; the 89-record connect list; memory faders, the memory page, BLACKOUT, the independents, the record functions, STACK, a running sequence), hypotheses H69–H75. E8 cleared nothing. Next: "Next capture (proposed by the E8 analysis, 2026-10-30)" below |
| **E8b** | **Second read-only session of our own software (active, SC-1), with everything E8 lacked:** a named SD backup, USBPcap started before the WinUSB bind (H68), and a timed action log from `tools/usb-probe/e8b-guided-session.ps1`, which also starts the backend and the UI. It repeats E8's readings with times (memory faders, page change, BLACKOUT, IND 1 then IND 2 named, each record key entered and left with CLEAR, STACK) and adds the tests of H69, H71 and H73, BLACKOUT with a level up, the sequence on fader 21 started and stopped, optionally GO with STACK lit, optionally the power-on list (H65). | Low-to-medium, as E8: only the two cleared messages; no bump inside a record function | SC-1 conditions 3, 4, 8, 9, 10 met; the owner's go on the day (given 2026-10-30 evening) | F110-F120 to VERIFIED where reproduced; H68; H69, H71, H73; H72 (key to block); H65 |
| **E9** | **SmartSoft writing (passive).** USBPcap records a SmartSoft Online session in which the owner moves faders and presses keys in SmartSoft's Console Simulator, renames a channel label, uses the console's own REC MEM, EDIT MEM and COPY through the Simulator keys on empty memories (owner decision 2026-10-30) and (optionally) edits one memory level; our code is not involved. Runbook below, from the static write-path analysis (`smartsoft-analysis.md` S11.15). | Low: SmartSoft's own messages, as for any user; the show backed up; no show load, erase, patch, settings or page action in the script | Interface 1 back on the ETC driver, Memory Integrity off, USBPcap, the action log | H5, H6, H24 (the PC → console data message and the ConsoleEvent framing), H40 (label byte order), the 1248 fader and button indices; the first OBSERVED PC → console write |

**E1 and E2 are done (2026-10-28). Next: E3(a)** (see "Next capture" below). E2 turned the
static hypotheses (poll / dirty / fetch objects) into observed facts; the text below about E2
is the plan as written before it, kept for the record. **Update 2026-10-29: E3 (a), (b) and (c)
are done; the next steps are the owner questions and the passive captures listed under "Next
capture (proposed by the E3 analysis, 2026-10-29)" below.** **Update 2026-10-30: E3b is done (items B1–B4 of
that list); the next steps are listed under "Next capture (proposed by the E3b analysis, 2026-10-30)" below.** **Update 2026-10-30, later: E8 is done (the
first read-only session of our own software, see its row); the next steps are listed under "Next capture (proposed by the E8
analysis, 2026-10-30)" below, and E9 (the runbook below) is the only path to a write mode.**
*(Plan text before E2:)* **Next: E2**, the single capture that turns the static hypotheses
(poll / dirty / fetch objects) into observed facts. Before E2 the owner must decide how to get
`etchsp.sys` loaded (see the E2 row; resolved the same evening). The zero-risk HID listen was
done the same evening (H28 rejected, H29 opened). **Now that the driver loads, SmartSoft can go
Online and push a show as soon as anyone starts it: back up the show to SD before SmartSoft is
launched at all.** Follow-up after the capture session: the owner switches Memory Integrity
back on (still pending after E2, per the E2 record; our backend must not depend on
`etchsp.sys`, H7).

### E8 runbook (first read-only session of our software; SC-1)

*(Run on 2026-10-30, see the E8 row and the [E8 record](captures/2026-10-30-e8-first-read-only-session.md). Deviations from
the steps below: step 1, no named backup was recorded (the owner says everything is backed up on the console); step 3,
USBPcap did not run; step 6, no action log, and the planned actions were replaced by free exploration. The runbook text
is kept as written.)*

Owner actions, in order; nothing in it sends anything but the cleared poll and listed fetch.

1. **Backup (safety rule 3):** save the show to the SD card (SHOW06.ASC or the next free name) and
   note the name; copy the file into `smartfade-sd-card-backup/`.
2. **Driver (once, reversible, owner action):** run Zadig, *Options → List All Devices*, and pick
   the entry by its **USB ID `14D5 0200 01`** (vendor 14D5, product 0200, interface 01: the
   console's interface 1, `USB\VID_14D5&PID_0200&MI_01`; the name Zadig shows for it is not
   evidence, E1 lists "ETC SmartFade" and "USB High-Speed-Pipe"); never the entry with interface
   `00`. Driver **WinUSB**, *Replace Driver*. Memory Integrity can stay on. SmartSoft will not see the console
   until the ETC driver is put back (Device Manager → the WinUSB device → *Uninstall device* with
   "delete the driver software", then *Scan for hardware changes*, or *Update driver* → *Browse my
   computer* → *Let me pick* → "USB High-Speed-Pipe").
3. **Capture in parallel:** start USBPcapCMD on the console's root hub as for E2/E3
   (`-A --inject-descriptors`), into a new file.
4. **Backend and UI:** `dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb
   --Device:Usb:ConfirmDate=<today, yyyy-MM-dd>` (the backend refuses any other date: hardware
   mode is confirmed per day; `Device:Usb:CaptureLogDirectory` defaults to
   `%LOCALAPPDATA%\SmartFade\captures`), then `npm start` and open http://localhost:4200. The
   badge must read **HARDWARE**, the masters must say "Not reported by the console yet" and every
   fader must be read-only before you press Connect.
5. **Connect**, then leave everything alone for 60 s. Expect: Connected, the LCD text in the backend
   log, no change on the console. If the status turns to Error, stop: read the reason, keep the logs.
6. **Actions, 10 s apart, each announced in the action log** (`tools/usb-probe/action-log.ps1`):
   fader 1 up to full and back; bump of channel 1 (BUMPS fader up first, then down again); MASTER
   to half and back to full. Expect the channel, output, FaderInfo and LED changes in the UI.
7. **Disconnect**, stop the backend, stop USBPcap.
8. **Evidence:** copy the backend's JSONL and the USBPcap file (console-only split, as for E3b)
   into `docs/protocol/captures/` with an E8 record (`/record-experiment`); versions as in SC-1
   item 9; update H66 / H67 / H68 and SC-1's "Review" section.

Abort rules: any Error from the backend ends the session (do not reconnect before reading the log);
anything the console does that the owner did not cause ends it (note the time); never power-cycle
the console to recover, just Disconnect.

### E8b runbook (second read-only session of our software; SC-1)

Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools\usb-probe\e8b-guided-session.ps1 -LogFile e8b-actions.txt`
from the repository root and follow it. In order it does: the SD backup gate (name required; the owner
states on 2026-10-30 that the show equals `smartfade-sd-card-backup/SHOW05.ASC`, a fresh save is still
taken), the optional power cycle (H65), USBPcap started by the owner in an admin window **before** the
Zadig bind (H68), the bind check (interface 1 must report the WinUSB service), the backend in hardware
mode for today's date and the UI (`npm start`) in their own windows, the HARDWARE badge check, the
starting position, Connect and 60 s idle, then the actions of the E8 record's item B with 5 s between
them, each stamped when the owner presses Enter, then Disconnect. Abort rules as for E8. Afterwards:
the action log, the backend's JSONL, the USBPcap file and the backup go into `docs/protocol/captures/`
and `smartfade-sd-card-backup/`, and the capture analyst writes the E8b record (`/record-experiment`).

### Owner decisions on scope (2026-10-30)

- **No wheel support and no settings edits, ever.** The wheel (ConsoleEvent kind 2) changes menu
  values and the settings are whole-object writes; neither is wanted, so no experiment captures them
  on purpose and no safety check will consider them.
- **A write mode means the live controls** (channel and memory faders, bumps, MASTER, BUMPS,
  BLACKOUT, SOLO, GO) **and recording through the console's own functions**: REC MEM, EDIT MEM and
  COPY as key presses, so that the console keeps control of its show data. E9 captures both.
- **E10, as the owner imagines it:** editor pages for the patch and for the stack / sequences, and
  saving and loading shows as `.asc` files on the PC. The ASC part needs the E6 round trip (save on
  the console, change in our app, load back from the SD card) and no capture; live editing of the
  patch or the stack over USB needs one capture per object type, like E9 for a memory. Loading a
  show into the console over USB stays closed (SmartSoft's upload replaces the console's show,
  F121); the SD card does that job.

### E9 runbook (passive capture of SmartSoft writing; proposed 2026-10-30 from S11.15)

The first capture of a PC changing the console. SmartSoft does the writing; our code is not
involved and sends nothing. Every write SmartSoft makes is one it makes for any user, but a show
load or an erase is irreversible, so those stay out of the script.

**Setup (owner):** show backed up to the SD card, name noted (safety rule 3); interface 1 on the
ETC driver (switched back after E8) and Memory Integrity off, as for E3b; USBPcapCMD on the
console's root hub (`-A --inject-descriptors`) into a new file; the action log
(`tools/usb-probe/action-log.ps1`): one entry per action, at least 15 s idle between actions, and
after each action a note of the LCD, the LEDs and the bar; the rig as in E3b, BUMPS at full before
step 3.

**Actions, in order (SmartSoft Online; "Simulator" = SmartSoft's Console Simulator, manual p. 10):**

0. Go Online and leave everything alone for 60 s: the baseline (polls, fetches, the two System writes).
1. Open the Console Simulator and touch nothing for 30 s: does opening it write anything?
2. Simulator channel fader 1 (bank 1-24): 0 → full → 0, slowly. Expected: type-1 headers with id 20
   kind 0 (fader index, scale, coalescing) and the console's FaderInfo / ChannelLevels / OutputLevels
   replies; note whether bump 1 blinks (matching, F8).
3. Simulator channel bump 7: press, hold 2 s, release. Expected: kind 1 press and release, the button code.
4. Simulator MASTER: full → about half → full (the MASTER fader index, S11.15 row 10).
5. Simulator BUMPS: full → 0 → full (the BUMPS fader index).
6. Simulator BLACKOUT: on, wait 5 s, off (its code; whether any object reports it).
7. Simulator SOLO: on, then off.
8. Simulator FADERS MEMS, press and release memory bump 1, then FADERS 1-24 again.
9. Simulator GO once with the STACK button unlit (in E3b this changed no output): the GO code.
10. Playback tab: the "all faders to zero" arrow, to identify that path.
11. Rename channel 7's label to a text containing "č", then rename it back: a ChannelText (9)
    whole-object write, and the test of H40 (label byte order).
12. Optional, the owner's choice: in Mem Edit change channel 7's level in memory 1, then restore it:
    a Memory (0, page, slot) whole-object write.
13. **Record functions through the Simulator keys** (owner decision 2026-10-30; backup first; only the
    empty memories 9 and 10 of page 1, empty per the E8 LED map): channel fader 5 at about 50 %, then
    Simulator REC MEM, then memory bump 9 (records the look into memory 9). Expected: the key events,
    the console's "Record Memory" LCD and LED screen (F116), and whether the console lists the Memory
    object afterwards (never observed in a session without the download, F89).
14. Simulator EDIT MEM, memory bump 9, channel fader 5 to full, CLEAR to leave (manual: EDIT MEM and
    CLEAR blink). Expected: the edit screen, then the changed content if the console lists it.
15. Simulator COPY, bump 9 (source), bump 10 (target). Afterwards delete both: EDIT MEM, bump 9, all
    channel faders to zero, CLEAR; the same for 10 (all-zero content deletes the memory, F107).

**Never in E9:** the wheel (kind 2 changes menu values, see E3b's accidental 5.0 → 5.1 s change);
REC SEQ (a sequence slot would be written); Patch, Independents or Settings edits; page clicks;
Open showfile, New show, Save As, any Erase. Steps 13–15 write show data through the console's own
functions and only into memories 9 and 10 of page 1, which are deleted again at the end.

**Evidence:** the USBPcap file (console-only split, as for E3b) and the action log into
`docs/protocol/captures/` with an E9 record (`/record-experiment`); outcomes for H5, H6, H24, H40
and the first OBSERVED PC → console data messages. Repeat the session once (E9b) so that the
behaviour can be VERIFIED. Only after E9 and E9b can a safety check (SC-2) consider a type-1
encoder limited to the whitelisted ConsoleEvents.

### Next capture (proposed by the E2 analysis, 2026-10-28)

*(Superseded 2026-10-29: item 2 was done as E3, see the E3 row above; the list is kept for the
record. The current list follows the next heading.)*

1. ~~**Owner, zero risk:** copy `SHOW04.ASC` (saved to the SD card before E2) into
   `smartfade-sd-card-backup/`.~~ **Done the same evening, see item 9 below.** It lets us compare Patch (F68, H34), memory slots (H25),
   the step count (H35), page / sequence counts (H33) and the labels (H36) with the E2 bytes.
2. **E3(a), fader 1 only:** SmartSoft Online, nothing else; the owner moves channel fader 1
   slowly 0 → full → 0 and types the time of each step. Isolates fader numbering, the level
   scale (is 0xAB the top?), the index base of ChannelLevels / OutputLevels, the LedStatus
   record and the DisplayContent glyph (H32). The same capture repeats E2's start-up, so it is
   also the **independent reproduction that can promote E2's OBSERVED facts to VERIFIED**
   (F51–F69; for F58 that verifies that SmartSoft sends those System bytes, not that we may
   send them: safety rule 2 still applies), and it tests the sequence-counter question (H31: first inbound payload byte 0
   should be 0xEA without a console power cycle). Same safety rules: back up first, passive
   capture only, no bytes sent by us.

### Next capture (proposed by the E3 analysis, 2026-10-29)

**Before every capture session (safety rule 3):** save the show to the SD card on the console and
record the file name in the action log. `tools/usb-probe/e3-guided-session.ps1` asks for it as its
first step and logs "backup taken: <file>"; SmartSoft is started only after that line exists. E3
deviated from this (it relied on the E2 backup `SHOW04.ASC`; the capture later showed the show
unchanged), which is why the gate is now in the script.

Safety rules 1–7 above are unchanged: backup first, passive capture only, **nothing is sent by
us**, the rig stays safe. Every item below is an owner action on the console or a static read; none
needs a byte from our software.

**A. Owner questions (zero risk).** Each decides a hypothesis. Answers of 2026-10-29 are archived in
[captures/2026-10-29-e3-owner-statements.txt](captures/2026-10-29-e3-owner-statements.txt)
(SHA-256 `d3e0ee5b995e3e419d2fa96568644d94cb4a9599309a16f7b73f6c4d5dbde328`); what is still open is marked.

1. Did fixture 1 / fixture 2 (channels 7 / 8) light during the memory-1 bump? **Answered (A1):** no,
   the BUMPS fader stood at 0 % (H54).
2. Where is the Bumps master fader? **Answered (Q5):** the fader labelled BUMPS, next to the one
   labelled MASTER (rightmost), with INDEPENDENTS below both; left or right of MASTER was not stated.
3. Which physical LEDs are blocks 53, 55 and 58? **Block 53 answered (A4):** the Blackout button's
   LED. **Blocks 55 and 58 are still open** (H56; the manual suggests mode-button LEDs).
4. What colour is memory fader 21's LED on page 1, and what does that fader play? **Partly answered
   (A4):** it holds a sequence and its LED lit; the colour was not reported (H55: expected dim yellow).
5. *(new)* Was the SOLO button on during the memory-1 bump? Was anything connected to DMX IN (H52)?

**B. Passive captures** (SmartSoft Online, console-only USBPcap split, the guided script
`tools/usb-probe/e3-guided-session.ps1` as the timed action log, memory-mode and fader-mode switches
logged this time):

1. **H31:** close and restart SmartSoft twice **without** a console power cycle. The second session's
   first pseq should continue from the first session's last one if the counter is not reset per
   session. It also shows what the connect-time list holds without a power cycle (H58).
2. **E3(c'), H47 / H57:** one channel fader at full (NEXT off, BLACKOUT off) while the MASTER moves
   full → half → full; compare that channel's level bytes with the MASTER byte and with the ceil
   formula of H57.
3. **H46 / H54:** move the BUMPS fader 0 → full → 0, then press a memory bump (a memory with known
   levels, `GROUP 1` = channels 7 / 8) in channel mode and in memory mode, with SOLO off and on (log
   the SOLO state); ask the owner what the lights did.
4. **H59 / H53 / H56:** press the 25-48 FADERS button and move physical fader 1; switch 1-24 →
   MEMS → 25-48 → MEMS → 1-24 with logged times.
5. *(optional)* **E3(d), H50:** GO on a safe stack, watching PlaybackInfo (17).

*(Update 2026-10-30: B1–B4 were run as E3b. B5 was declined in the script (log `owner decision: GO step = n`),
but the owner pressed the GO button once, unlogged, with the STACK button unlit (E3b-A1). A.5 was answered
unclearly: the DMX IN question got a description of the rig's LED bar (E3b-A3). The current list follows the
next heading.)*

**C. Still open from E3:** the **SmartSoft-side capture** with a "č" (U+010D) label rename (E3(e) =
E6 probe 5, H40), with a 6-character third line and an SD save, backup first. It is a SmartSoft
write, so it is an owner decision, and it would be the first capture of SmartSoft sending anything
besides the two System messages.

**D. Static, zero risk:** the initial values of the two start-up countdowns in `sfEngine.dll`
(S11.14.11; the observations suggest 40 and 39, H30), the text helpers `sub_10013970` /
`sub_100139F0` (H40), the `sfmlEngine.dll` PID check (S11 open lead 5).

**E. Owner:** switch Memory Integrity back on when the capture sessions are over (H7).

### Next capture (proposed by the E3b analysis, 2026-10-30)

Safety rules 1–7 above are unchanged: back up the show first (the guided script asks for it), passive
capture only, **nothing is sent by us**, the rig stays safe. Every item below is an owner action on the
console or a static read; none needs a byte from our software.

**A. Owner questions (zero risk).**

1. ~~Was GO or another playback button pressed around 09:11:08?~~ **Answered (E3b-A1):** the owner pressed the
   GO ('play') button once, with the STACK button unlit, to see what it does; a 5-s crossfade, no output change
   (F101, F106).
2. ~~How did the default times become 5.1 s?~~ **Answered (E3b-A2):** the owner moved the (sensitive) wheel by
   accident (H45). **Still open:** was that before or after the `SHOW05.ASC` save at 08:55:48? It decides
   whether the identical files show that the times are not stored in the file (F102, F104).
3. ~~What is connected to DMX IN?~~ **Answered unclearly (E3b-A3):** the owner described the rig's LED bar
   (DOTZ BAR 1.4 at address 5, 12-channel mode), almost certainly on DMX OUT. **Still open:** is anything
   plugged into the DMX IN socket, and does it send (H52, F105)? **Answered 2026-10-30:** nothing is plugged into DMX IN (owner: "Only DMX out is connected (the light)").
4. Which physical buttons carry LedStatus blocks 55, 56, 58 and 50 (and is 52 the SOLO button's LED)? Which
   button LED lit blue during the GO crossfade (H56, H64)?
5. Were the third memory bump (664.75 s) and the two mode switches (842.73 s, 846.60 s) the owner's actions
   (F106)? Which step is the main stack on and which sequences are running (H63)?

**B. Passive captures** (SmartSoft Online, console-only USBPcap split, the guided script as the timed action
log; log the SOLO and GO states too):

1. **H54 scaling:** BUMPS at about 50 % (also about 25 % and 75 %) with a channel bump and a memory bump
   (`GROUP 1` = channels 7 and 8); prediction of the hypothesis, not an observation: the bumped channels read
   about the BUMPS byte.
2. **H54 SOLO add versus replace:** SOLO on and off with another fader up (a second output active), then a
   memory bump; this separates 'replaces' from 'adds'.
3. **H60:** several stable MASTER positions with one channel at full (about 25 %, 50 %, 75 % and the E2
   position 0xAA), each held about 2 s.
4. **H52:** a known DMX source on the DMX IN socket (the owner decides; nothing is sent by us), after the owner
   has confirmed what is plugged in now.
5. **H51:** a memory page change on the console (ConsoleStatus body[2]).
6. **H62 / H61:** in memory mode bump another memory (for example `GROUP 2`) and move a memory fader without a
   bump; in bank 25-48 move physical faders 12 and 24 (DMX 36 and 48 lie outside the rig).
7. **H65:** power on and connect with the console untouched; power on, move one fader, connect; power on,
   press SOLO, connect.
8. *(optional, owner decision, safe rig)* **H50 / H63 / H64:** GO with the STACK button lit on a safe stack,
   logged, with the step shown on the console noted before and after.

**C. Still open from earlier:** the SmartSoft-side capture with a "č" (U+010D) label rename (E3(e), E6 probe
5, H40), which is a SmartSoft write and an owner decision (unchanged).

**D. Static, zero risk:** the initial values of the two start-up countdowns in `sfEngine.dll` (S11.14.11; the
observations suggest 40 and 39, H30), the text helpers `sub_10013970` / `sub_100139F0` (H40), the
`sfmlEngine.dll` PID check (S11 open lead 5).

**E. Owner:** switch Memory Integrity back on when the capture sessions are over (H7).

### Next capture (proposed by the E8 analysis, 2026-10-30)

Safety rules 1–7 above are unchanged. Everything in A is an owner question or console action that sends nothing; B is a
second read-only session of our software (SC-1's two messages only, nothing else is cleared); C and D are tooling and a
pointer. **E9 (the runbook above) is the only path to a write mode; nothing in E8 clears any other message.** Source:
[E8 record](captures/2026-10-30-e8-first-read-only-session.md) "Next capture to take".

**A. Owner, zero risk:**

1. Which key LEDs light or blink when you press CLEAR, STACK, COPY, REC SEQ, MAGIC, REC MEM, EDIT MEM, IND 1, IND 2? Do REC MEM,
   REC SEQ and COPY blink, or stay lit (H72)? Which IND key did you press first at about 16:26:52?
2. Was a sequence running on memory fader 21 before you connected, and how was it started? Did holding MEMS stop it at about
   16:29:26 (237.5 s)?
3. Did you press CLEAR three times at about 16:31:40 (372.4 s) with memory fader 1 up, and pull fader 1 down at about 16:36:40
   (672.3 s) (H73)?
4. Did any record function store or change anything (a bump pressed inside REC MEM, EDIT MEM, REC SEQ or COPY)? Was the console
   power-cycled since E3b? How did the default times return to 5.0? What is the name of the backup taken before E8?
5. The console's firmware from Menu > Diagnostics, for the record.

**B. E8b, a second read-only session of our software** (the same SC-1 conditions, this time all of them).

- Setup:
  - a named SD backup copied into `smartfade-sd-card-backup/`;
  - USBPcap from before the Zadig bind;
  - `action-log.ps1` with a line per action;
  - actions at least 5 s apart.
- Each item turns an E8 reading into a second-session observation (VERIFIED needs the reproduction). Each needs logged times.
  1. **Repeat E8's readings with times:**
     - memory faders 1 and 2 (F112);
     - a page change to 12 and back (F113);
     - BLACKOUT on and off (F114);
     - IND 1, then IND 2, named (F115, H72);
     - each record key entered and left with CLEAR only, one at a time (F116, H72); no bump inside a record function, since that
       would record;
     - STACK pressed, held and released (F117).
  2. **New tests:**
     - BLACKOUT with channel fader 5 at full (does any level object show the blackout?);
     - a page change with memory fader 1 at full (H69);
     - memory fader 1 at 50 % with the MASTER at half, and a bump at BUMPS about 50 % (H71);
     - CLEAR × 1 to × 4 with a channel and a memory fader up (H73);
     - start and stop the sequence on fader 21 (F118, H50);
     - GO with STACK lit on a safe stack (H63 record 5, H64; optional, owner decision, safe rig).
  3. **The power-on list (H65):** power on, touch nothing, connect; then disconnect and connect again (the read-only analogue
     of F94). Watch also for a pending list of 37 or 101 records (H75; added by the curator, not in the E8 record).
- **What the USBPcap capture adds:**
  - **H68:** EP0 at the Zadig bind, at the backend's start and at Connect, i.e. whether Windows or WinUSB read string
    descriptors before our first poll.
  - USBD status for every transfer, which the backend's log does not have, and (added by the curator from H75) whether a
    zero-length packet ever follows a transfer.
  - A frame-by-frame cross-check of the backend's log against the bus, as E3b's tshark cross-check did for the normalizer. That
    would verify `TransferLog` itself.

**C. Tooling (zero risk):** `hsp_summary.py`'s `summary` and `timeline` modes had to accept a log without pcap frames (print the
seq instead). *Done in the commit that added the E8 record (both modes print `f-` for the frame; re-run by the curator on the
E8 log on 2026-10-30, both run without an error).*

**D. Not E8's business, recorded for completeness:** E9, the passive USBPcap capture of SmartSoft writing (the runbook above,
CLAUDE.md), is the only path to a write mode. Nothing in E8 clears any other message: SC-1's clearance (poll and listed fetch,
read-only) is unchanged.

## Hardware evidence requested

Things that cannot be determined from the supplied materials:

1. Console firmware version (*Menu > Diagnostics > Software version*) and serial number / rear-panel photo.
2. Whether SmartSoft has ever connected to this console "Online", on which PC/OS, with which SmartSoft version. *Partly answered 2026-10-28:* SmartSoft 3.0.43 is installed on the owner's main PC (2026-02-03), but its HSP driver has never loaded there under HVCI (F46) and the user settings key is empty, so it was probably never Online on that PC. Still open: was it Online anywhere else? **Answered 2026-10-28 (E2):** SmartSoft 3.0.43 went Online immediately on the owner's main PC (Windows 11 build 26300, Memory Integrity off, `etchsp.sys` 2.00.000, console firmware 3.0.2.9.0.43), showed no dialog and downloaded the show (owner-reported, F69; the download is in the capture, F58). Whether it was ever Online before E2 stays unknown.
3. ~~E1 output: Device Manager name + hardware IDs, `pnputil /enum-devices /connected /ids` (or USBTreeView report) with the console plugged in.~~ **Done 2026-10-28** (E1 record).
4. Whether a USB-MIDI interface and MIDI cables are available (for E4).
5. Whether a spare PC or VM is available for SmartSoft + USBPcap. *Now required for E2 unless Memory Integrity is switched off on the main PC (F46).* **2026-10-28: owner switched it off on the main PC for the E2 session (F49); not needed.**
6. Whether a USB-DMX interface is available (optional, E7).
7. More `.asc` files, ideally made deliberately for E6.
8. *(new 2026-10-28)* Console firmware version and serial number: E1 cannot read them; bcdDevice 0x0103 is not obviously the firmware version. **Firmware answered the same day: 3.0.2.9.0.43 (F48).** Serial number still open.
9. ~~*(new 2026-10-28, from E2)* `SHOW04.ASC` copied from the SD card into `smartfade-sd-card-backup/` (it was saved before the E2 session and is not in the repository yet), see "Next capture".~~ **Done 2026-10-28** (the same evening; SHA-256 559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90). Results: E2 record "Cross-check against SHOW04.ASC", facts F70-F79, H13 / H25 / H34 / H35 confirmed, H39-H45 added. The folder is untracked in git; committing the owner's show data is the owner's decision.
10. *(new 2026-10-28, from E2)* The owner's timed account of the E2 fader action (the log was untimed, so the 42.2–45.9 s movement is only INFERRED to be it, F66), and the time SmartSoft was closed (about 125.25 s is inferred from the capture). **Answered for E3 on 2026-10-29** by the guided session: the fader moves, the MASTER move, the bump and the SmartSoft close are timed in the owner's action log (PC clock, merged into the capture); the E2 movement itself stays untimed.
11. *(new 2026-10-29, from E3)* Still missing from the owner: which physical LEDs LedStatus blocks 55 and 58 are; the colour of memory fader 21's bump LED in memory mode on page 1; whether SOLO was on during the memory-1 bump; whether anything was connected to DMX IN (see "Next capture (proposed by the E3 analysis, 2026-10-29)"). Answered the same day: the lights did not come on during the bump because the BUMPS fader stood at 0 %; the panel labels are BUMPS and MASTER; the Blackout LED blinked while the MASTER was below full; fader 21 holds a sequence; the unlogged fader-1 moves were the owner's ([captures/2026-10-29-e3-owner-statements.txt](captures/2026-10-29-e3-owner-statements.txt)).
12. *(new 2026-10-30, from E3b)* Answered: the owner pressed GO once at about 09:11:08 (E3b-A1); the default times changed by a wheel move (E3b-A2); the colour of memory fader 21's LED is yellowish and recorded / empty memory LEDs are red bright / dim (log 634.39 s); SOLO was off at the start and on for the second memory-bump test (log); the rig is one DOTZ BAR 1.4 LED bar at address 5 in its 12-channel mode (E3b-A3; [captures/2026-10-30-e3b-owner-statements.txt](captures/2026-10-30-e3b-owner-statements.txt)). Still missing: whether anything is plugged into DMX IN; which physical buttons carry LedStatus blocks 50, 55, 56 and 58; whether the wheel move happened before the SHOW05 save; the cause of the third memory bump and of the two mode switches.

## Tooling plan

| Tool | Location | Status |
|---|---|---|
| Source audit scripts (installer extraction, PE report, strings) | `tools/source-audit/` | Done |
| USB identification probes (read-only): descriptor dump through the hub driver, HID capabilities + passive input-report listen | `tools/usb-probe/` | Done (E1) |
| Operator action log for capture sessions (`action-log.ps1`: PC-clock-stamped notes typed at the moment of each console / SmartSoft action, same clock as USBPcap) | `tools/usb-probe/` | Done 2026-10-29 (for E3: `action-log.ps1` and the guided script `e3-guided-session.ps1`; the log is merged into the capture as `note` records by normalizer version 2, **action log merge done**) **2026-10-30:** `e3b-guided-session.ps1` (refuses an existing log, asks for the backup before it starts SmartSoft, starts SmartSoft three times itself and asks the zero-risk questions; it sends nothing to the console) used for E3b. |
| Capture importer: USBPcap pcap → normalized capture JSONL (`usbpcap_to_jsonl.py`; classic pcap only, pcapng must be converted with tshark first) | `tools/protocol-analyzer/` | **Done 2026-10-28** (normalizer version 1, used for E2; two runs gave identical bytes; ~~Planned after E2~~). **Version 2 done 2026-10-29** (used for E3): adds `--notes-log` / `--notes-utc-offset` (action log merged as `note` records); re-run on E2 it is byte-identical except the normalizer version number |
| Hex/record viewer, diff of two captures, object-name annotator | `tools/protocol-analyzer/` | Partly done 2026-10-28: `hsp_summary.py` (`summary`, `log`, `payloads --id N`, `diff --id N` within one capture). **Diff of two captures done 2026-10-29** (needed for E3): `startdiff --other` (first body of every object compared with another capture), plus `timeline` (per-object change timeline interleaved with the notes), `census` (the body positions that ever changed) and `lcd` (DisplayContent as two text lines); `summary` also prints the sequence wraps and the reply-header position of each data write. Hex/record viewer and object-name annotator: the `log` mode only **2026-10-30:** `sessions` mode added to `hsp_summary.py` (per SmartSoft session: span, first replies and lists, position of each data write within its session, inbound sequence continuity, repetition of the large list); the `summary` mode counts reply headers from the start of the capture, so for sessions 2 and 3 it prints #1220 / #1259 and #2233 / #2272 where `sessions` prints #40 / #79. |
| E2 vs show-file cross-check (`e2_asc_crosscheck.py`: Patch, Memory, labels, sequences and steps, SystemSettings, Independents, live levels against `SHOW04.ASC`; specific to E2, copy and adapt) | `tools/protocol-analyzer/` | **Done 2026-10-28** (SHA-256 8C49AEDB…B314; run: `py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py <jsonl> <asc>`; re-runnable only where `smartfade-sd-card-backup/` exists) Ran unchanged on the E3 and E3b JSONL (E3b against `SHOW05.ASC`). |
| MIDI recorder (Windows MIDI → JSONL) | `tools/protocol-analyzer/` | Planned for E4 |
| Replay (to mock transport only) | backend `ReplayTransport` | Done 2026-10-30 (`SmartFade.Communication.ReplayTransport` plays the console side of a normalized capture, `SendAsync` only records; a `ReplayDevice` that drives the application from it is the next step) |

## Capture format (proposal)

*Annotated 2026-10-28: the proposal below is kept as written. What `tools/protocol-analyzer/usbpcap_to_jsonl.py` actually writes (normalizer version 1, used for E2) is documented in the next section, "Capture format as implemented". Differences: the source is a classic pcap (the example says pcapng), and several keys were added.*

Raw captures (`.pcapng`) are kept unmodified as evidence. They are normalized into
**JSON Lines**: one JSON object per line, deterministic key order, diff-friendly, streamable,
usable by xUnit tests. File: `docs/protocol/captures/YYYY-MM-DD-<slug>.jsonl`, with
`<slug>.md` describing the experiment.

First line = header:

```json
{"type":"header","format":"smartfade-capture","version":1,"transport":"usb","source":"usbpcap","sourceFile":"2026-09-20-smartsoft-connect.pcapng","sourceSha256":"…","console":{"model":"SF1248","firmware":"3.0.2.9.0.43"},"host":{"app":"SmartSoft","version":"3.0.2.9.0.43b","os":"Windows 11"},"device":{"vid":"14D5","pid":"????"}}
```

Records:

```json
{"type":"frame","seq":17,"t":1.234567,"ts":"2026-09-20T10:30:00.123456Z","dir":"out","iface":1,"ep":"0x04","xfer":"bulk","len":12,"data":"<hex copied from the capture>"}
{"type":"note","t":5.000000,"text":"moved channel fader 1 to full"}
```

- The examples above are **illustrative shapes only**: `data` must always be copied from a real
  capture, never typed in. (Endpoint numbers per F42: interface 1 bulk OUT 0x04 / IN 0x83,
  interface 0 interrupt OUT 0x02 / IN 0x81.)
- `t` = seconds since capture start (µs precision), `dir` = `out` (host→console) / `in`.
- MIDI captures use the same format with `"transport":"midi"` and no `iface/ep/xfer`.
- Notes are inserted from the operator's action log so the decoder can correlate.

### Capture format as implemented (normalizer version 1, E2, 2026-10-28)

Source: `tools/protocol-analyzer/README.md` and the header and records of
`captures/2026-10-28-e2-smartsoft-connect.jsonl`. Deterministic: uppercase hex, fixed key order,
numbers rounded to 6 decimals, LF line endings; the same input and arguments give the same bytes.
The proposal's `data`-copied-from-capture rule holds: `data` is the transfer payload as USBPcap
recorded it.

Header (first line), keys in this order: `type` = `header`, `format` = `smartfade-capture`,
`version` = 1, `transport` = `usb`, `source` = `usbpcap`, `sourceFile`, `sourceSha256`,
**`sourceFrames`** (pcap frames), **`captureStart`** (UTC ISO 8601 of the first frame),
`console` {`model`, `firmware`}, `host` {`app`, `version`, `os`, **`driver`**}, `device` {`vid`, `pid`,
**`bus`**, **`address`**, **`descriptorChecked`**: VID/PID matched the first device descriptor in the capture},
**`normalizer`** {`name`, `version`}, **`deviceFrames`**, **`otherDeviceFramesSkipped`** (frames of other
devices on the bus are counted, their data discarded), **`transfers`** (records). Keys in bold are new
relative to the proposal.

Records: one `frame` record **per USB transfer (URB)**, not per pcap frame. USBPcap writes a
submission and a completion frame; they are merged by IRP ID, so zero-length OUT completions and
IN submissions are not records of their own. Order: by completion frame number. Keys: `type` =
`frame`, `seq` (1-based record number), `t` (completion time, seconds since the first frame),
`ts` (completion time, UTC), `dir` (`out` / `in`), `iface` (from the configuration descriptor found in the
capture, `null` for endpoint 0), `ep` (e.g. `0x04`), `xfer` (`bulk`, `control`, …), `len`, `data` (hex),
and the new keys **`setup`** (control transfers only, 8-byte SETUP packet as hex), **`status`** (USBD status
of the completion, `0x00000000` = success), **`tSubmit`** (submission time), **`frames`**
(`[submission frame, completion frame]`, 1-based pcap frame numbers: the data-carrying frame is
`frames[0]` for OUT and `frames[1]` for IN), **`synthetic`** (present, `true`, only for frames with IRP ID 0
that `USBPcapCMD --inject-descriptors` writes at t = 0: copies of descriptors, not session traffic).
`note` records (`type`, `t`, `text`) come from `--note <t> <text>` on the command line and sort
before frames with the same `t`.

Reproduce the E2 normalization and the numbers in its record: command line in
`tools/protocol-analyzer/README.md` (expected output SHA-256 `808834C9…4E8B`).

### Capture format as implemented, version 2 additions (normalizer version 2, E3, 2026-10-29)

Source: `tools/protocol-analyzer/README.md` ("How `usbpcap_to_jsonl.py` works") and the header and notes of
`captures/2026-10-29-e3-console-actions.jsonl`. Everything in the version-1 section above still holds. A run
without `--notes-log` writes the same bytes as version 1 except the normalizer version number (checked on
E2, recorded in the tool README). The header key `version` (the **format** version, value 1) is unchanged;
the **normalizer** version is `normalizer.version` = 2.

New keys, written only when `--notes-log` is used:

- Header **`notesLog`** = {`file` (the log's file name), `sha256` (of the log), `utcOffset` (for example
  `+01:00`), `notes` (number of notes taken from the log)}. E3: `{"file":"2026-10-29-e3-console-actions.log","sha256":"EF10EBC2F3949A37B3F76EC881D43EE2C2C3B0FF37A61CEE2A79AB69A2B1645B","utcOffset":"+01:00","notes":27}`.
- On every `note` record merged from the log: **`source`** (the log's file name) and **`logTime`** (the
  log's own time stamp, verbatim, local PC time, for example `2026-10-29 20:40:30.151`); `t` is the
  capture-relative time computed with the offset. Notes added with `--note T TEXT` (coordinator facts)
  carry neither key.
- Log format: `YYYY-MM-DD HH:MM:SS.fff text`, as written by `tools/usb-probe/action-log.ps1` and
  `e3-guided-session.ps1`; a line in another format aborts the run instead of being dropped.
  `--notes-utc-offset +HH:MM` converts the log's clock to UTC; it is an argument and is never read from
  the machine that runs the normalizer.

E3 JSONL: 1 header, 20 326 `frame` records, 30 `note` records (27 from the log, 3 coordinator notes: capture
start, SmartSoft start, capture stop); two runs and a verbatim run of the README command gave identical
bytes (output SHA-256 `3B2B6861C25CE7B90BDF4212E20445F7B53EF8DE1A44E308F75B2C43E9AE59CF`). The log's stamps are
Enter-key times at the start of an action (`Step` lines) or confirmation times after it (`Confirm-Done`
lines), so a note is not the instant the console changed; the console's own data is the reference
([E3 record](captures/2026-10-29-e3-console-actions.md) "Action log"). Reproduce the E3 normalization: the
command line in `tools/protocol-analyzer/README.md` ("Reproduce the E3 normalization").

E3b JSONL: 1 header, 53 935 `frame` records, 88 `note` records (86 from the log, which now also holds
`owner answer:` lines and the script's own `smartsoft start #n` lines, plus the coordinator's capture start
and stop); output SHA-256 `0EA97056712F1656026AE42831E766B0B1F53662F5C2A07879C5CF0D5D944594`; two runs and
the README command gave identical bytes (command line: `tools/protocol-analyzer/README.md`, "Reproduce the E3b
normalization"; record: [E3b record](captures/2026-10-30-e3b-live-objects.md)).
