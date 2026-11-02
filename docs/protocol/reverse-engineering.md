# Reverse-engineering plan

## Safety rules (non-negotiable)

1. **Passive before active.** Observe SmartSoft ↔ console traffic before our software sends
   anything to the console.
2. **Never send captured or hand-built bytes to the console** until the message is understood
   and its effect is known to be harmless (e.g. a read/poll). Replaying edit messages can
   overwrite show data. *E2 note (2026-10-28): even polls and fetches have console-side
   effects (the console's sequence counter advances per payload, a listed dirty object is
   delivered once), so "read" is not automatically harmless; nothing is cleared for sending.* *(SC-1, 2026-10-30: the poll and the fetch of a listed record are cleared for a read-only session,
   [safety-checks.md](safety-checks.md); the note stands for everything else.)* *(SC-2, 2026-10-31: a conditional GO for building and reviewing a live-control encoder bounded to the fader indices 0x00 / 0x30 / 0x31 and eight live key codes, with the first write session E11 on the owner's go, and a NO-GO for the record keys from the PC (SC-3 later), ChannelText and Memory writes and the System pair; it clears nothing for sending until its conditions are met, [safety-checks.md](safety-checks.md).)*
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
| **E8b** | **Second read-only session of our own software (active, SC-1), with everything E8 lacked:** a named SD backup, USBPcap started before the WinUSB bind (H68), and a timed action log from `tools/usb-probe/e8b-guided-session.ps1`, which also starts the backend and the UI. It repeats E8's readings with times (memory faders, page change, BLACKOUT, IND 1 then IND 2 named, each record key entered and left with CLEAR, STACK) and adds the tests of H69, H71 and H73, BLACKOUT with a level up, the sequence on fader 21 started and stopped, optionally GO with STACK lit, optionally the power-on list (H65). | Low-to-medium, as E8: only the two cleared messages; no bump inside a record function | SC-1 conditions 3, 4, 8, 9, 10 met; the owner's go on the day (given 2026-10-30 evening) | F110-F120 to VERIFIED where reproduced; H68; H69, H71, H73; H72 (key to block); H65. **Done 2026-10-30 (evening)** → [captures/2026-10-30-e8b-second-read-only-session.md](captures/2026-10-30-e8b-second-read-only-session.md): 1 121 s over WinUSB on interface 1 after a power cycle, 27 484 polls and 2 315 fetches, 0 framing errors, no fault, stopped at an idle point; USBPcap ran from before the Zadig bind (the committed `.pcap` is the console-only split; the raw root-hub capture, which held other devices, was removed from the pushed history at the owner's request), a named backup (`SHOW06.ASC` = `SHOW05.ASC`) and a timed action log. Deviations from the runbook: Connect was pressed before the starting position was set, and the GO part (two GO presses, the crossfaders, STACK off, CLEAR) came after the scripted step, unlogged, timed by the console's data only. Results: **F110, F112, F113, F114 (the block 53 pattern), F115, F116, F119 and F120 VERIFIED and F117, F118 VERIFIED in part (VERIFIED count 49, never clearance for sending)**; **H68 CONFIRMED** (EP0 traffic only at the Zadig bind: nothing at the backend start, at Connect or at Disconnect), H66 CONFIRMED in two sessions, H67 same scope (floors untested), H69 and H73 CONFIRMED, H72 CONFIRMED in capture for blocks 60, 63, 64, 66, 67, 72 and 73 (the IND keys and BLACKOUT named by timed presses), H54 also at BUMPS 0x81; H71 narrowed, not separated; facts F122–F130 (the bus view and WinUSB's split reads, EP0 around a read-only session, the power-on list, BLACKOUT zeroing the level objects, memory-mode FaderInfo pre-MASTER, the page hold, the CLEAR series, a sequence that keeps running at fader zero, GO with STACK lit and the crossfaders), hypotheses H76–H80. E8b cleared nothing. Next: "Next capture (proposed by the E8b analysis, 2026-10-30)" below |
| **E9** | **SmartSoft writing (passive).** USBPcap records a SmartSoft Online session in which the owner moves faders and presses keys in SmartSoft's Console Simulator, renames a channel label, uses the console's own REC MEM, EDIT MEM and COPY through the Simulator keys on empty memories (owner decision 2026-10-30) and (optionally) edits one memory level; our code is not involved. Runbook below, from the static write-path analysis (`smartsoft-analysis.md` S11.15). | Low: SmartSoft's own messages, as for any user; the show backed up; no show load, erase, patch, settings or page action in the script | Interface 1 back on the ETC driver, Memory Integrity off, USBPcap, the action log | H5, H6, H24 (the PC → console data message and the ConsoleEvent framing), H40 (label byte order), the 1248 fader and button indices; the first OBSERVED PC → console write **Done 2026-10-30 (late evening)** → [captures/2026-10-30-e9-smartsoft-writes.md](captures/2026-10-30-e9-smartsoft-writes.md): SmartSoft Online driven by the owner through the guided script `tools/usb-probe/e9-guided-session.ps1` (6de4ddb), USBPcap passive (the committed `.pcap` is the console-only split of the root-hub capture, 214 494 frames, 22:19:59–22:48:44), a named backup (`SHOW07.ASC`, byte-identical to `SHOW04.ASC` … `SHOW06.ASC`) and a timed action log; **401 PC data writes** (the System pair, 332 ConsoleEvent fader events, 64 ConsoleEvent key events, three ChannelText labels); our software sent nothing. Results: **H5 (PC → console part), H6 (for the paths used) and H24 (kinds 0 and 1) CONFIRMED (OBSERVED once)**, H40 reading A (UTF-16BE) OBSERVED for SmartSoft's write (the console's side open), H72 extended, H51 untested; **F93, F94 (its structure), F97 (BUMPS 0xFF) and F103 VERIFIED (VERIFIED count 53, never clearance for sending)**; facts F131–F140 (the write framing and the outbound counter, four fader indices and 13 key codes, the console's answer to PC events, matching with PC faders, the record functions driven from the PC and what the console lists, the first DirtyObjects records with c ≠ 0, the ChannelText write and its bytes, E9's side effects on the show, the Simulator's opening and "all faders to zero" sending nothing), hypotheses H81–H86; F57's "c = 0 in every record" limited. Deviations from the runbook: the Mem Edit steps went through the console's own EDIT MEM (no Memory object was written), the "restore" step edited memory 7 instead of memory 1 (memory 7 was still changed: an open owner item, the owner on 2026-10-31: "Its possible"; **closed 2026-10-31**: `SHOW08.ASC` confirmed the change, the owner restored it by hand, and `SHOW09.ASC` is byte-identical to `SHOW07.ASC`), the guide's deletion step deleted nothing (script fixed in 6cb2c07), and the console was in memory mode at connect. **E9 cleared nothing.** Next: "Next capture (proposed by the E9 analysis, 2026-10-30)" below |
| **E9b** | **SmartSoft writing, the repeat (passive).** Three SmartSoft Online sessions in one USBPcap capture, driven by the owner through `tools/usb-probe/e9b-guided-session.ps1` (3ce3900) after a console power cycle: E9's live Simulator steps again, the H81 cases (a PC fader against a physical one), keys not yet seen (STACK, IND 1, IND 2, FADERS 25-48), the console's own EDIT MEM on pages 1 and 12, the label of channel 7 across two SmartSoft restarts, REC MEM into memory 9 with the MASTER at half and its deletion by REC MEM of an empty look, and (the owner's choice) one cell in SmartSoft's Mem Edit tab; our code is not involved. | Low: SmartSoft's own messages, as for any user; the show backed up first (`SHOW08.ASC`); no show load, erase, patch, settings or page action in SmartSoft | Interface 1 on the ETC driver, Memory Integrity off, USBPcap, the guided script's action log | The VERIFIED reproduction of F131–F140 and H81–H86 **Done 2026-10-31** → [captures/2026-10-31-e9b-smartsoft-writes-repeat.md](captures/2026-10-31-e9b-smartsoft-writes-repeat.md): USBPcap passive (the committed `.pcap` is the console-only split of the root-hub capture, 208 876 frames, 08:37:27–09:05:18), a named backup (`SHOW08.ASC`, saved at 08:35:31 before the capture) and a timed action log; **427 PC data writes** in three SmartSoft sessions (the System pair × 3, 366 ConsoleEvent fader events, 26 key press / release pairs, two ChannelText labels, one Memory (0) whole-object write); framing errors 0; our software sent nothing. Results: **F131, F132, F133, F134, F138 and F140 VERIFIED, F135 and F136 VERIFIED in part (VERIFIED count 61, as SmartSoft's and the console's behaviour, never clearance for sending)**; F141–F147 added (the last mover wins between a PC fader and a physical fader; the console's own EDIT MEM lists System (5, 5, 0); the Memory (0) whole-object write from the Mem Edit tab, a 90-byte body against the console's 138; ChannelText stored verbatim and returned across restarts and a power cycle; REC MEM stores the post-MASTER level; REC MEM of an empty look empties a memory; the Memory object listed after the console's own recording); fader index 0x04 and key codes 0x37, 0x3B, 0x47, 0x48 seen; **H84 and H86 CONFIRMED, H85 REJECTED, H81's first clause CONFIRMED for channel mode**, H82 and H83 supported, H78 contradicted in its prediction (cause UNKNOWN), H40 and H72 annotated, H87–H90 added. Deviations from the runbook: the H85 MASTER was moved on the console instead of the Simulator (INFERRED from the absence of any write) and its physical position stayed at about half until the owner set it to 100 %; the B7 recording and deletion used the console's REC MEM and bump 9; four Simulator key pairs and a playback check of memory 9 were unlogged. The owner's answers at the start were: the Mem Edit window of E9 = Simulator, no touch at 279 s, the Simulator's fader 1 moved at 290 s ("y"), and memory 7 "was in memory 1, not 7", which the download and `SHOW08.ASC` contradicted (restored by hand and closed 2026-10-31, `SHOW09.ASC` = `SHOW07.ASC`). **E9b cleared nothing:** SC-1's poll and listed fetch remain the only messages cleared, no type-1 header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*, and the safety check SC-2 is the gate for any write. Next: "Next capture (proposed by the E9b analysis, 2026-10-31)" below |
| **E11** | **First live-control session of our own software (active, SC-2; planned).** The backend in a separate write mode sends only SC-2's whitelist (fader indices 0x00, 0x30, 0x31; the eight live key codes) following the script in [safety-checks.md](safety-checks.md) SC-2: read-only first, one control at a time, press / release as one operation, no write while a record function is armed, no retries; USBPcap in parallel, backup first. Needs the encoder built with golden tests over E9 and E9b, the `protocol-safety-reviewer` pass and the owner's go on the day. | Medium: live output changes on the rig as from the panel (MASTER, BLACKOUT included); H66 for writes unknown (the console may ignore the events: the session then ends); no show data touched (record functions refused) | SC-2 conditions met; Zadig on interface 1; SD backup; the owner's written acceptance of the residual risks listed in SC-2 | H66 for writes; whether the console answers our events as it answers SmartSoft's (F134 for our host) | **Done 2026-11-01 in three attempts** → [attempt 1](captures/2026-11-01-e11-first-live-control-session-attempt-1.md) (no write sent: the console listed nothing and the interlock refused by design), [attempt 2](captures/2026-11-01-e11-first-live-control-session-attempt-2.md) (four channel fader 1 events, followed), [attempt 3](captures/2026-11-01-e11-first-live-control-session-attempt-3.md) (W1–W8, 116 writes, every control followed, the bus = the log, `SHOW22.ASC` = `SHOW21.ASC`; two sessions after the guide without USBPcap, a recorded deviation). H66 for writes CONFIRMED (OBSERVED, our own host); F167 VERIFIED in part; F168, F169, H111 added. **Nothing is cleared beyond SC-2's conditional GO; its decisions stand** |
| **E12** | **SmartSoft editing the patch and the stack / sequences (passive; planned and run 2026-10-31).** **Run 2026-10-31 15:39–16:32 local time: record `captures/2026-10-31-e12-smartsoft-patch-stack-edits.md` (provenance, completeness, structure: 70 PC → console writes by SmartSoft, 0 framing errors, three SmartSoft sessions, downloads of 157 / 161 / 157 records, `SHOW13.ASC` = `SHOW12.ASC` byte for byte). **The record holds provenance, completeness and structure only; the decoding of the 70 writes against H91–H101 is pending: the assistant's attempt was stopped by its safety system and is not repeated, the owner or a later pass does it (the record names the tools).** H91–H101 keep their status until then.** *(Superseded the same evening, text kept: the decoding was done by the `capture-analyst` agent on Opus 5.5 at the owner's request, with read-only tools, and applied by the curator; see "Analysed" in the Answers cell.)* *(The plan as written before the run:)* USBPcap records a SmartSoft Online session in which the owner, led by the guided script `tools/usb-probe/e12-guided-session.ps1` (written 2026-10-31 (`tools/usb-probe/e12-guided-session.ps1`, parse-checked)), edits test objects: channel 47 and IND 1 in the Patch tab, test cues in the stack (insert, a point cue, times, a label, a move, deletes), a step, the run mode and the label of KOTLIK, memory 9's content and name, optionally page 12's name and the console's own patch and step-timing menus. Every change is restored, then checked by two SmartSoft restarts (downloads) and by an SD save compared byte for byte with the backup. Our software is not involved and sends nothing. Runbook below ("E12 runbook"), from the static pass `smartsoft-analysis.md` S11.16. E9c and E9d are reserved by SC-2 for the live-control widening and E11 is the first live-control session of our software, hence the number | Low-to-medium: SmartSoft's own messages as for any user, but they **change show data** (the patch, the stack, one sequence, names, memory 9) and SmartSoft's Patch tab says "There is no UNDO for this!"; every change is restored in the same session and the SD backup is the fallback (the console's Load show, or Load patch from card for the patch alone); DMX 100–102 must be unconnected; no show load, erase, settings, wheel or page action in SmartSoft; the optional step H uses the physical wheel, which E3b showed can move values by accident | Interface 1 on the ETC driver (service ETCHSP), Memory Integrity off, USBPcap started before SmartSoft, the guided script `tools/usb-probe/e12-guided-session.ps1` and its action log, an SD backup to a free number and a second free number for the final save, nothing connected at DMX 100–102 (the rig is DMX 5–16), the owner's decisions listed in the runbook | H91–H101: the Patch, Independents, Page, Sequence, SequenceMain, StepContent and memory-name writes (their layouts against the console's own, `smartsoft-analysis.md` S11.16 row 5), the step commands 32 / 34 / 35 / 42 (bodies, `a` values, call sites; rows 1–4 and 6), InsertStep's two phases, the run-mode mapping (row 7), the label rule of row 8, the un-patch bin (row 10) and what the console lists after SmartSoft's edits and after its own patch and stack edits (H101). **All of it is INFERRED or DOCUMENTED today, nothing OBSERVED or VERIFIED; NOT CLEARED, never sent by our code; E12 is passive and has not been run** *(Superseded 2026-10-31, evening, text kept: run and analysed; the messages are OBSERVED once, F151–F159; still NOT CLEARED, never sent by our code.)* **Analysed 2026-10-31, evening** → [captures/2026-10-31-e12-smartsoft-patch-stack-edits.md](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md) (section "Analysis": the `capture-analyst` agent on Opus 5.5 at the owner's request, read-only tools `tools/protocol-analyzer/e12/`; applied by the curator the same evening, who re-checked the evidence behind every promotion). Results: **F143 and F147 VERIFIED, F144 and F146 VERIFIED in part (VERIFIED count 65; E9b + E12, as SmartSoft's and the console's behaviour, never clearance for sending)**; F151–F159 added (the step commands InsertStep / DeleteStep / MoveStep / id 42 and the console's lists after them; InsertStep's two phases with the step type in the second; the whole-object writes of the patch, the independents, steps, a sequence and pages in the console's layouts, the type-2 step excepted (100 bytes against 148); field readings (IND button mode and GM, run mode 0 Loop / 1 One Shot, the console's "W" and "State"); the console listing its own patch and step-timing edits at every wheel detent; Sequence records with c ≠ 0 and content-dependent lengths; the downloads 157 / 161 / 157; SmartSoft's unintended live keys and rewrites; the per-line label residue); **H91, H93, H97, H99 and H14 CONFIRMED, H92, H94, H95, H96, H101 and H87 PARTLY CONFIRMED, H98 CONFIRMED with its sub-reading "the step type travels in the command" REJECTED, H100 unchanged** (all OBSERVED once); H102–H106 added. Deviations from the runbook: A3 was not look-only (an unlogged GO, an Independents rewrite and a Direct cue inserted and deleted), D4 wrote memory 9's label (SmartSoft has no memory-step label) and left the console in memory mode, D5's label came only in I2, F3 renamed page 1 (corrected in I1), E1's step went before the old 7th step. **E12 cleared nothing**; for SC-1 (documentation only): Sequence records with c ≠ 0 and 144- / 148-byte bodies would stop a read-only session of ours with its fault, a safe stop (F156). Next: "Next capture (proposed by the E12 analysis, 2026-10-31)" below |
| **E12b** | **SmartSoft editing the patch and the stack / sequences, the repeat (passive; run 2026-10-31, late evening).** USBPcap recorded two SmartSoft Online sessions in which the owner, led by `tools/usb-probe/e12b-guided-session.ps1` (the trimmed version, 05b6e70) after a console power cycle, repeated E12's patch, IND 1, stack, KOTLIK and page edits and added a channel step, Manual, a labelled Direct cue, KOTLIK inserts at two other places and an SD save of the edited show on the console (`SHOW15.ASC`), restoring every change (`SHOW16.ASC` = `SHOW14.ASC` byte for byte); the optional console-side menus (step H) were declined. Our software was not involved and sent nothing | Low-to-medium, as E12: SmartSoft's own messages change show data; every change restored in the session; DMX 100 unconnected; no show load, erase, settings, wheel or page action in SmartSoft | Interface 1 on the ETC driver (service ETCHSP), Memory Integrity off, USBPcap before SmartSoft, the guided script's action log, a named SD backup (`SHOW14.ASC`) and two more free SD numbers (`SHOW15.ASC` edited, `SHOW16.ASC` restored) | The second, independent session for F151–F159 and H91–H106, and H12, H40 (the console's side), H44, H95 (Manual), H102, H103, H105. **Run 2026-10-31, 22:29–22:58 local time, and analysed the same late evening** → [captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md](captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md) (section "Analysis": the `capture-analyst` agent on Opus 5.5 at the owner's request, read-only tools `tools/protocol-analyzer/e12b/`; applied by the curator on 2026-11-01, who re-checked the evidence behind every promotion and new fact): 62 data writes, one framing error (the read cancelled during the SD save), downloads of 157 / 160 records. Results: **F151, F152, F156 and F157 VERIFIED, F153, F154 and F158 VERIFIED in part (VERIFIED count 72; E12 + E12b, as SmartSoft's and the console's behaviour, never clearance for sending)**; F160–F165 added (the channel step, the labelled State step stored verbatim and exported by the console as UTF-16BE, the SD file forms, the cancelled read and the pipe resets during the console's SD save, the live stack's positions as indices under step commands, the key codes 0x32 and 0x3A); **H91 (the channel drag), H93, H97, H98, H99 (the Page part), H102, H103 and H105 (as an order) CONFIRMED in two sessions, H92, H94, H95, H96 and H101 PARTLY CONFIRMED, H12 CONFIRMED, H40's byte order CONFIRMED for the console's side with reading B REJECTED for the 1248, H44 PARTLY CONFIRMED, H104 REJECTED, H106 untested; H107–H110 added**. Deviations from the guide: an unlogged playback from 22:40:05 to 22:43:16 (SmartSoft's next / PAUSE / PLAY, the console's STACK and PAUSE + PLAY) that left STACK lit; D5's label set only after it; the Direct cue got no level; SmartSoft kept 17 of the 18 label characters, the first a capital "Č"; the point cue moved to 13.3 (between 13.2 and 14.0). **E12b cleared nothing**; its lesson for our sessions: no SD save or load on the console while our software is connected (F163, H109; safety-checks.md "Conditions for E11"). Next: "Next capture (proposed by the E12b analysis, 2026-10-31)" below |
| **E9c** | **SmartSoft's Console Simulator driving the live controls in every bank (passive; run 2026-11-02, 00:41–01:20).** Guided by `tools/usb-probe/e9c-guided-session.ps1` (f87cb0e) after the owner's decision for an automatic bank switch and held bumps: every channel fader 2–24 of bank 1-24, the bank 25-48 (the key and two faders), memory faders on two pages with held memory bumps, the matching rule after a mode and a page switch with a PC level in force, held bumps with durations, IND 1 / 2, STACK without GO, PAUSE, NEXT, CLEAR, a fader with the MASTER at half, a SmartSoft restart; our software not involved. | Low: SmartSoft's own live controls; no record function; backup `SHOW24.ASC` | ETC's driver on interface 1, Memory Integrity off, USBPcap, the guided log | **Done 2026-11-02** → [captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md](captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md): 1 227 writes, 0 framing errors, `SHOW25.ASC` = `SHOW24.ASC`. **F171–F177 added** (the index names the physical fader in every bank and mode; a switch does not carry a level and a PC fader must match the new content's level first; NEXT = 0x3A with BlindLevels (18); held bumps; the live keys seen twice; the MASTER at 0x84; 24 records pending without a power cycle, a 158-record download); **H111 CONFIRMED in part, H79 / H81's matching clause CONFIRMED, H69 and H112 CONFIRMED in part, H82 / H107 / H108 supported, H65 widened; F165 VERIFIED in part (74 VERIFIED facts).** Deviations: no bump in bank 25-48 (the Simulator relabels its faders there); S4b first done in the Simulator. **E9c clears nothing: SC-5 (safety-checks.md) decides the classes, with E9d for what was seen once.** |
| **E9d** | **Every fader and bump of every bank from SmartSoft's Console Simulator, twice (passive; run 2026-11-02, 09:47–10:14, after a console power cycle).** Guided by `tools/usb-probe/e9d-guided-session.ps1` (1a7136c): in each of two SmartSoft sessions the channel faders 1–24, the bank 25-48 (every fader, one held bump), memory faders 1–24 and memory bumps 1–24 of page 1, bump 7 held about 5 s, E9c's mode-switch case; our software not involved. | Low: SmartSoft's own live controls; no record function; backup `SHOW26.ASC` | ETC's driver on interface 1, Memory Integrity off, USBPcap, the guided log | **Done 2026-11-02** → [captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md](captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md): 2 855 writes, 0 framing errors, `e9d/check.py` 228 steps as expected, `SHOW27.ASC` = `SHOW26.ASC`. **F171 and F174 VERIFIED, F172 VERIFIED in part, H111 CONFIRMED, H79 / H81's mode case CONFIRMED (77 VERIFIED facts).** Not covered: the channel bumps 2–6, 8, 9, 11–24 and the bumps 26–48 of bank 25-48 (E9e). **E9d clears nothing by itself: the SC-5 addendum (safety-checks.md) lifts the "after E9d" rows per class.** |
| **E9e** | **The four-bump spot check of SC-5 addendum 2 (passive; run 2026-11-02, 10:57–11:06).** Guided by `tools/usb-probe/e9e-guided-session.ps1` (e2c5dd4): in each of two SmartSoft sessions the bumps under faders 2, 12 and 24 of bank 1-24 and the bump under the fader labelled 48 of bank 25-48, held about 1 s; our software not involved. | Low: four key taps; backup `SHOW28.ASC` | ETC's driver on interface 1, USBPcap, the guided log | **Done 2026-11-02** → [captures/2026-11-02-e9e-smartsoft-channel-bumps-spot-check.md](captures/2026-11-02-e9e-smartsoft-channel-bumps-spot-check.md): 28 writes, 0 framing errors, every bump lit its own channel in both sessions (`e9d/check.py` 14 steps as expected), `SHOW29.ASC` = `SHOW28.ASC`. The inference of the addendum holds; F174 annotated; nothing cleared by the record. |
| **E11b** | **Second live-control session of our own software (active, SC-5; planned 2026-11-02, not run).** The backend with live control sends SC-2's whitelist plus SC-5's classes (every channel fader of both banks and every memory fader of the shown page through the automatic bank switch, the memory and channel bumps as holds bounded to 3 s, the bank keys, IND 1 / IND 2) following `tools/usb-probe/e11b-guided-session.ps1`: the SC-5 gate (the reviewer's pass closed, the written acceptances), read-only first, W0 from the panel, then one control at a time with 10 s between steps and the console's answer asked each time (channel faders 1 and 5; channel fader 30 with the switch to bank 25-48; channel fader 2 with the switch back; memory fader 3 of page 1 with the switch to memory mode; memory bump 1 and channel bump 7 held about 2 s; IND 1 / 2 and MASTER optional; the two bank keys from the UI), no retries; USBPcap in parallel, backup first. Needs the SC-5 implementation at the extension points (by an Opus 5.5 subagent at the owner's request, 2026-11-02), the `protocol-safety-reviewer` pass and the owner's go on the day. | Medium: live output changes on the rig as from the panel; a switch changes what the console's 24 physical faders control (visible on the FADERS LEDs); after a switch the console may ignore a PC fader whose first event is not the reported level (visible as a blinking bump LED; the session then ends); no show data touched (record functions refused) | SC-5 conditions met; Zadig on interface 1; SD backup; the owner's written acceptances SC-5 lists (the switch, the matching rule, the bounded hold with the lost-release risk accepted 2026-11-02, the last mover) | Whether the console follows our switch sequence and a first event at the reported level (F172 for our host), a held bump released by our host (F174 for our host), the bank keys and IND 1 / IND 2 from our host | **Planned, not run** |

**E1 and E2 are done (2026-10-28). Next: E3(a)** (see "Next capture" below). E2 turned the
static hypotheses (poll / dirty / fetch objects) into observed facts; the text below about E2
is the plan as written before it, kept for the record. **Update 2026-10-29: E3 (a), (b) and (c)
are done; the next steps are the owner questions and the passive captures listed under "Next
capture (proposed by the E3 analysis, 2026-10-29)" below.** **Update 2026-10-30: E3b is done (items B1–B4 of
that list); the next steps are listed under "Next capture (proposed by the E3b analysis, 2026-10-30)" below.** **Update 2026-10-30, later: E8 is done (the
first read-only session of our own software, see its row); the next steps are listed under "Next capture (proposed by the E8
analysis, 2026-10-30)" below, and E9 (the runbook below) is the only path to a write mode.** **Update 2026-10-30, evening: E8b is done too (the second read-only session, see its row); the next steps are listed under "Next capture (proposed by the E8b analysis, 2026-10-30)" below; E9 stays the only path to a write mode.** **Update 2026-10-30, late evening: E9 is done too (SmartSoft writing to the console, see its row); the next steps are listed under "Next capture (proposed by the E9 analysis, 2026-10-30)" below. E9 observed the first write messages and cleared none: E9b (the repeat) and a safety check SC-2 stand between those observations and any write of ours.** **Update 2026-10-31: E9b is done too (the repeat of E9, see its row); the next steps are listed under "Next capture (proposed by the E9b analysis, 2026-10-31)" below. E9b reproduced the write framing and the most-used ConsoleEvents (VERIFIED as behaviour) and cleared nothing: SC-2 still stands between those observations and any write of ours.** **Update 2026-10-31, later: E12 is planned** (a passive capture of SmartSoft editing the patch and the stack / sequences; see its row above and the "E12 runbook" below). It follows from a static pass, `smartsoft-analysis.md` S11.16, whose findings are INFERRED or DOCUMENTED and were never observed on the bus (hypotheses H91–H101). **E12 has not been run and clears nothing;** E9c / E9d (SC-2's widening) and E11 (the first live-control session of our software) keep their places in [safety-checks.md](safety-checks.md). **Update 2026-10-31, evening: E12 has run and is analysed** (see its row; the `capture-analyst` agent on Opus 5.5 at the owner's request); the next steps are listed under "Next capture (proposed by the E12 analysis, 2026-10-31)" below. E12 cleared nothing: the Patch and Independents writes stay SC-2 class F, the Memory write class D, and the stack, sequence, page and step-command writes are in no class. **Update 2026-11-01: E12b has run (2026-10-31, late evening) and is analysed** (see its row; the `capture-analyst` agent on Opus 5.5 at the owner's request): the repeat made E12's findings VERIFIED as SmartSoft's and the console's behaviour where it reproduced them and added F160–F165; the next steps are listed under "Next capture (proposed by the E12b analysis, 2026-10-31)" below (no repeat of E12b is needed). E12b cleared nothing; SC-2's E11 conditions gain "no SD save on the console while our software is connected" (safety-checks.md).
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

*(Run on 2026-10-30 evening, see the E8b row and the [E8b record](captures/2026-10-30-e8b-second-read-only-session.md). Deviations from
the steps below: Connect was pressed before the starting position was set, so the first mode switches (21.8–34.7 s) are inside the
session; the GO part (two GO presses, the crossfaders, STACK off, CLEAR) happened after the scripted "go with stack lit" step and is not in
the action log, timed by the console's data only; the raw root-hub USBPcap file was first committed unsplit (resolved: the committed `.pcap`
is the console-only split). The other steps were followed: the named backup, the power cycle, USBPcap before the bind, the bind check, the
timed log with 5 s between actions, Disconnect at the end.)*

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
- **Added 2026-11-02, after E11:** the UI keeps the console's layout (the right-hand columns FADERS,
  CROSSFADER, RECORD, MASTERS with INDEPENDENTS under it, the console's key colours), and **the PC
  drives memory faders and channels 25-48 through an automatic bank switch:** when a fader of another
  bank is moved in the UI, the backend presses the console's FADERS key for that bank first and then
  sends the fader event, as the Simulator's user would do by hand (the owner checked SmartSoft: it has
  no way to move a channel fader while MEMS is lit either). **Held bumps** from the UI are wanted. Both
  are protocol widenings, not UI shortcuts: E9c (the runbook below) observes them from SmartSoft first,
  a safety check extending SC-2 decides, the owner implements the sending code, a live session verifies.
  The owner also wants the console's state at Connect without touching the panel, as SmartSoft has it
  (its System pair, F58, F89): a candidate for its own safety check, SC-4, not decided.

### E9 runbook (passive capture of SmartSoft writing; proposed 2026-10-30 from S11.15)

**Guided script (2026-10-30 evening):** `powershell -NoProfile -ExecutionPolicy Bypass -File
tools/usb-probe/e9-guided-session.ps1 -LogFile e9-actions.txt` takes the owner through the setup gate
(backup, ETC driver on interface 1, Memory Integrity reading, USBPcap first), starts SmartSoft itself and
logs every step below with the PC clock; the record functions of steps 13–15 are in it, with the
deletion of memories 9 and 10 at the end. The owner ran it the same evening (E9, 22:19–22:47 local time).

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

### E12 runbook (patch and stack / sequence edits through SmartSoft, passive)

*(Planned 2026-10-31 from the static pass `smartsoft-analysis.md` S11.16; not run. Hypotheses H91–H101; DOCUMENTED context F148–F150.
Every "expected" cell below is an INFERRED prediction to compare with the bus, never an observed fact; a deviation is a finding, not an
error. Numbering: E9c and E9d are reserved by SC-2 for the live-control widening, E11 is the first live-control session of our software.)*

*(Run 2026-10-31, 15:39–16:32, and analysed the same evening: the [E12 record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md) compares every row of the table below with the
bus ("Hypotheses H91–H101", "Runbook deviations"). The "expected" cells are kept as written; where the bus differed (the step
type travels in the content write, not in InsertStep; D4 wrote a memory label; the stack has no run-mode popup), the record and
the hypotheses say so.)*

**Guided script:** `powershell -NoProfile -ExecutionPolicy Bypass -File tools/usb-probe/e12-guided-session.ps1 -LogFile e12-actions.txt`
(written 2026-10-31 (`tools/usb-probe/e12-guided-session.ps1`, parse-checked), not yet committed when this section was written; its step tags A1 … J2 follow the table below). It asks the
owner's decisions below first, takes him through the setup gate (the SD backup name, the driver, USBPcap first), starts SmartSoft itself
three times and logs every action with the PC clock; it refuses an existing log file. **Nothing in it touches the console, and our software
is not involved.**

SmartSoft does the writing and USBPcap records it, as in E9 and E9b. Every write is one SmartSoft makes for any user, but these are not
live controls and not one memory: they change the show's patch, the stack and a sequence, so every change is **restored in the same
session**, the restores are captured too (they are the DeleteStep, MoveStep and un-patch messages), and two restarts of SmartSoft plus an SD
save compared byte for byte with the backup prove the restore. Equal files prove it; a difference is a finding to examine, not
automatically a failure, but it does mean that the console exports something different, because saves of an unchanged show were
byte-identical (`SHOW04.ASC`, `SHOW05.ASC` and `SHOW06.ASC`; `SHOW09.ASC` equalled `SHOW07.ASC` after the E9 repair; F104, F139). No
encoder for these objects exists in the solution (checked 2026-10-31: the only data object `HspOutbound` encodes is
ConsoleEvent, SC-2 classes A and B1).

**Test objects:** channel 47 (no DMX address, F70), DMX 100–102 (unpatched; nothing connected there), IND 1 (channel 49, no address, F115),
memory 9 of page 1 (empty, so it can be written and emptied again, F146), the last cue of the stack, the sequence KOTLIK (page 1, fader 21,
7 steps), page 12's name.

**Setup (step 0, owner):** power-cycle the console (the pending list at connect depends on its history, H65); channel mode (FADERS 1-24 lit),
all channel faders at zero, MASTER and BUMPS full, STACK unlit, fader 21 (KOTLIK) at zero; save the show to the SD card to a **free number**
(safety rule 3) and note the name; interface 1 on the ETC driver (service ETCHSP; Memory Integrity off, as for E9b); USBPcap on the console's
root hub (`-A --inject-descriptors`) into a new file **before** SmartSoft starts. Then, with 15 s idle between actions and every action
logged by the guide:

| Step | Owner action | Expected on the bus (INFERRED, never observed) | Tests |
|---|---|---|---|
| A1 | Start SmartSoft Online; idle 60 s | System `09 00 00` and `07 00 00`, then the 157-record download 1 (F58, F94): the baseline of every test object | baseline |
| A2 | Open the Patch, Stack/Seq Edit, Page and Mem Edit tabs, 30 s each, touch nothing | nothing written (by analogy with opening the Simulator, F140) | — |
| A3 | (optional, looking only, touch nothing) Is there a patch-level control in the Patch tab? A run-mode popup for the stack? A "Direct cue" option in the Insert dialog? | the owner's answers (y / n / describe) | H100, H95, H98 |
| B1 | On the console: fader 5 to about half, REC MEM, bump 9, fader 5 to 0 | no PC write; the console lists Memory (0, 0, 8) by itself and SmartSoft fetches it (F147) | prepares memory 9 |
| C1 | Patch tab: drag channel 47 onto DMX 100 | one Patch (5) write, 514 bytes, entry 99 = 47, every other entry as in download 1 | H91 |
| C2 | Patch tab: drag IND 1 onto DMX 101 | a Patch (5) write with entry 100 = the IND's number (49 expected); an Independents (12) write may also follow | H91, H93 |
| C3 | (optional) IND 1: button mode changed and changed back; GM changed and changed back | Independents (12), 85 bytes, one write per change; which record byte changes (c or d by elimination, H93) shows where the mode and GM live | H93 |
| D1 | Stack: right-click the last cue, Insert step, Memory 01/09; log the cue number offered | InsertStep (32) with `a` = 48, then StepContent (4, new step), 13 bytes | H96, H97, H98 |
| D2 | Right-click the cue before the last original cue, Insert step, Memory 01/09 as a point cue between them | InsertStep (32) with `a` = 48 and `b` / `c` carrying the point cue (UNKNOWN), then StepContent (4); no id 33 | H97, H98 |
| D3 | On D1's step: Up, then Down, then Wait, one per action | one StepContent (4, step), 13 bytes, per edit | H94 |
| D4 | On D1's step: label "T" | StepContent (4), 13 bytes, no label block (S11.16 row 8) | H94 |
| D5 | (optional) Insert a Direct cue at the end, label "T" | InsertStep with step type 2, then StepContent (4) with levels and a label block | H94, H98, H12 |
| E1 | KOTLIK: insert after step 7, Memory 01/09 | InsertStep (32) with `a` = 0, then Sequence (2, 0, 0), whole, 144 bytes (8 steps) | H96, H98 |
| E2 | Change that step's Up time | Sequence (2, 0, 0), whole, 144 bytes | H94 |
| E3 | Run mode Loop → One Shot (in SmartSoft the mode is the bare word "Loop" in the header of the sequence's step list, no label; a click on it opens the popup Loop / One Shot / Manual: INFERRED from the binary, smartsoft-analysis.md S11.16 row 7, and found that way by the owner during E12) | Sequence (2, 0, 0): body[3] 0 → 1 | H95 |
| E4 | Label KOTLIK → KOTLIX | Sequence (2, 0, 0): the label block changes | H95 |
| F1 | Mem Edit: memory 01/09, channel 5 to 100 % | Memory (0, 0, 8), 90 bytes, channel 5 = 0xFF (the layout of F143) | H99 |
| F2 | Page tab: memory 01/09 name "T" | Memory (0, 0, 8), 90 bytes, label "T" | H99 |
| F3 | (optional) Page 12's name "T" | Page (8, 11), 39 bytes | H99 |
| G1 | Restart SmartSoft (download 2) | what the console stored of every test object (patch, stack, KOTLIX, memory 9, the names) | H91, H94, H95, H99 |
| H | (optional, console menu, Blind) Patch DMX 102 to channel 47, then Unpatch dimmer 102; Sequences > Stack > Step Timing > one step: D1's Up time | no PC write; does the console list Patch (5) or StepContent (4), and does SmartSoft follow? | H101 |
| I1 | Restores: page 12's name empty (if F3 was done), KOTLIK's label back, run mode Loop, Delete step on E1's step | Page (8, 11); Sequence (2, 0, 0) twice; DeleteStep (34) with `a` = 0 after "Are you sure?" | H95, H97 |
| I2 | Drag D2's step to between the last cue and D1's step (the Move dialog), then delete D2's, D1's (and D5's) steps | MoveStep (35) and id 42; DeleteStep (34) with `a` = 48, once per step | H96, H97 |
| I3 | Memory 9's name empty; then on the console REC MEM + bump 9 with nothing up | Memory (0, 0, 8) with an empty label; then the console lists the emptied memory (F146) | H99 |
| I4 | Drag the DMX 101 cell, then the DMX 100 cell, onto the un-patch bin | one Patch (5) write per drop, the entry zeroed | H92 |
| J1 | Restart SmartSoft (download 3) | equals download 1 | the restore |
| J2 | Close SmartSoft, stop USBPcap, SD save to a new free number, compare byte for byte with the backup | identical files | the restore |

The steps depend on one another: D2 and I2's move belong together (dropping D2 drops the move), D5 and its delete in I2, F3 and I1's
page-name restore; C3 has no separate restore step (each change is changed back in place); H restores itself (Unpatch dimmer 102; the
step-timing change goes with the deletion of D1's step in I2). The owner logs the text of any message box he sees (SmartSoft has the strings
"No space to insert a cue here" and "No space to move a cue here", S11.16 row 16).

**Never in E12:** the Simulator's wheel, Open showfile, New show, Save As, any Erase, Default times, REC SEQ, Tap mode, PAGE clicks in
SmartSoft (H51); no save inside SmartSoft; and never a *channel* dragged onto the un-patch bin (it zeroes every address of the channel,
S11.16 row 10): only the DMX cells I4 names. Abort rules as for E8: anything the console does that the owner did not cause ends the session
(note the time); an edit SmartSoft refuses is not retried; never power-cycle the console to recover mid-session.

**If the restore does not hold** (download 3 differs from download 1 or the J2 save from the backup): record first which object and which
bytes differ (that is a finding), close SmartSoft, then on the console Menu>Save/Load Show: Load show (the backup), or Load patch from card
if only the patch differs, and save again to compare. The console manual prints the menu path of "Load patch from card" as `[Save as]`, the
same as for saving (printed p.6, F148): read the entry off the console, do not trust the printed path.

**Evidence afterwards:** the action log, the USBPcap file (split to the console only, as for E9b) and the two SD files go into
`docs/protocol/captures/` (log, split pcap, normalized JSONL) and `smartfade-sd-card-backup/`; the capture analyst writes the E12 record
(`/record-experiment`), starting with the byte comparison; the curator then updates H91–H101 and the facts. OBSERVED needs bytes from this
capture, VERIFIED needs a second, independent session (an E12b), and neither clears anything for sending.

**Owner questions before E12** (the guide asks them first; the answers are statements and never promote):

1. Do you accept that E12 changes show data on purpose (a patch address for channel 47, IND 1's address, test cues in the stack, a test step
   in KOTLIK, its run mode and label, names, memory 9), restores it in the same session, and uses the SD backup (Load show, or Load patch from
   card) as the fallback?
2. Which optional steps do you want: C3 (IND 1's button mode and GM), D2 (the point cue; I2's move goes with it), D5 (a Direct cue), F3 (page
   12's name), H (the console's own Patch by dimmer, Unpatch dimmer and Step Timing, which use the physical wheel; E3b showed that the wheel
   can move values by accident)?
3. Which free SD numbers for the backup (before) and for the final save (after)?
4. Is nothing connected at DMX 100, 101 and 102 (the rig is DMX 5–16)?
5. A console power cycle beforehand?
6. An offline dry run of SmartSoft first, with the USB cable unplugged (zero risk), to find the Patch, Stack/Seq Edit, Page and Mem Edit
   controls, the Insert and Move dialogs and the answers to A3?

**What E12 cannot answer** (open unknowns, S11.16 "Open"): whether the console accepts these writes from a host that never sent the System
pair (H66 for writes; only sending can tell, and SmartSoft always sends the pair first); how it handles a malformed or short body; what id 42
means (E12 can show its bytes and its place next to MoveStep, not its meaning); which sequence step number the console's display shows (H43);
whether a proportional patch level exists anywhere (the Patch object has none, F70; A3 only shows whether SmartSoft offers one); what `b` and
`c` of InsertStep carry beyond what the logged dialog entries imply; what triggers phase 2 of InsertStep; whether label residue can be cleared
(F144).

**E12 clears no message:** SC-1's poll and listed fetch remain the only messages cleared for a read-only session, SC-2's classes are
unchanged (Patch and Independents stay class F, "Never"; the stack, sequence and page writes are in none of its classes), and a write of
these objects by our code would need its own safety check, a full body decode and a restore story first.

### E9c runbook (SmartSoft's live controls in every bank, held bumps, the keys seen once; passive; written 2026-11-02)

**Why:** after E11 the owner decided (2026-11-02, "Owner decisions on scope") to drive memory faders and
channels 25-48 from the PC through an **automatic bank switch** (the UI presses the console's FADERS key
before a fader of another bank is moved) and to have **held bumps** from the PC. Nothing of that has been
observed from SmartSoft: a fader event in memory mode or in bank 25-48, the FADERS 25-48 key a second time,
the console's matching rule after a mode or page switch with a PC level in force (H79 / H81, the risk the
automatic switch meets), and most channel fader indices of bank 1-24 (only 0x00, 0x04, 0x06 so far, H111).
E9c is the passive capture SC-2's "Path to a wider clearance" names, scoped for that decision; it clears
nothing by itself. After it: the record, a safety check that extends SC-2 (the classes for every fader
index in every bank, held bumps with a bounded hold and the release before any Disconnect, the keys seen
twice), the owner's implementation at the TODO-marked extension points, and a live session like E11.

**Guided script (written 2026-11-02, parse-checked; run the same night, 00:41–01:20, record `captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md`):** `powershell -NoProfile -ExecutionPolicy Bypass
-File tools/usb-probe/e9c-guided-session.ps1 -LogFile e9c-actions.txt` takes the owner through the setup gate
(backup, the E11 owner questions still open, which keys the Simulator has, ETC's driver on interface 1, the
Memory Integrity reading, USBPcap into `e9c-yyyyMMdd-HHmm.pcap` before SmartSoft, the starting position), starts
SmartSoft itself, logs every step with the PC clock and ends with the after-session SD save.

**Setup (owner):** show backed up to the SD card, name logged (safety rule 3); interface 1 on the ETC driver
and Memory Integrity off, as for E9b; USBPcapCMD on the console's root hub (`-A --inject-descriptors`) into a new
file; the starting position: channel mode 1-24, every channel fader at zero, MASTER and BUMPS at full, BLACKOUT,
SOLO, IND 1 / 2, STACK and NEXT off, page 1, nothing armed; memory 1 of page 1 recorded (channels 7 and 8), memory
9 of page 1 empty. The rig as before (DMX 5–16; IND 2 drives DMX 47).

**Actions, in order ("Simulator" = SmartSoft's Console Simulator; waits are in the script):**

- **S0.** Online, 60 s untouched (the System pair and the 157-record download once more, for a later SC-4);
  the Simulator opened, 20 s untouched.
- **S1. Every channel fader of bank 1-24:** Simulator faders 2 to 24, one at a time, up to about half, held 2 s,
  back to zero (fader 1 is VERIFIED, 5 and 7 OBSERVED once). Expected: index = fader − 1 for all 24 (H111's
  channel-mode clause; FaderInfo record N, ChannelLevels[N], DMX N, LED block N, the LCD cell).
- **S2. Bank 25-48:** the Simulator's FADERS 25-48 (code 0x37, the second observation), then its faders 1, 2 and
  24 moved and its bump 1 held 2 s there, then FADERS 1-24. Expected (H111): the same indices 0x00, 0x01, 0x17
  and code 0x00, the console answering with channels 25, 26, 48 (ChannelLevels[25] …, DMX 25 …, the LCD
  labels 25 / 37); the owner notes the LCD's channel numbers.
- **S3. Memory mode:** FADERS MEMS, then the Simulator's faders 1, 2 and 9 as memory faders of page 1 (memory 1
  = channels 7 and 8; memory 9 empty), memory bump 1 held 3 s, memory bump 9 tapped; a page change on the
  CONSOLE (hold MEMS, bump 2) and the Simulator's fader 1 as memory 1 of page 2; back to page 1 and FADERS
  1-24. Expected (H111, F112): the same index 0x00 moving FaderInfo record 1 as a memory fader, the memory's
  channels following; the page's memory on page 2.
- **S4. The matching rule with a PC level in force** (the automatic switch's risk, H79 / H81): (a) the
  Simulator's channel fader 1 left at half, then FADERS MEMS: what memory fader 1 reads and whether memory 1's
  light comes up; the Simulator's fader 1 to full and zero in memory mode; FADERS 1-24: what channel 1 reads
  after the round trip; cleanup. (b) The physical fader 2 at half, FADERS MEMS, the Simulator's fader 2 from
  zero (pick-up or at once?), FADERS 1-24, channel 2 afterwards; the physical fader to zero. (c) The
  Simulator's memory fader 3 at half, a page change on the console, what fader 3 does on page 2; cleanup.
- **S5. Held bumps with durations:** bump 7 held 3 s, bump 10 held 2 s, bump 7 held 2 s in SOLO mode, memory
  bump 1 held 3 s in memory mode (the release's timing and what a hold does to the objects, for the SC-2
  amendment on held bumps).
- **S6. The live keys seen once or never:** IND 1 and IND 2 on / off (codes 0x47 / 0x48, the second
  observation), STACK on / off without GO (0x3B), PAUSE twice with no fade running and NEXT on / fader 4 moved
  blind / NEXT off, each only if the Simulator has the key (H107, H112: the codes 0x32 / 0x3A expected), then
  CLEAR as one, two and three presses (0x38; levels only, F109).
- **S7. A channel fader with the MASTER at half:** the Simulator's MASTER to half, its fader 1 to full and
  back, the MASTER to full (F96: the post-MASTER values in FaderInfo and the level objects, the data for a UI
  whose MASTER is not at full).
- **S8. A SmartSoft restart:** close, start again (the System pair and the download, the counter from 0x00),
  the Simulator's fader 1 once more; then the end: 30 s idle, SmartSoft closed, USBPcap stopped, the physical
  positions described, the after-session SD save (must equal the backup byte for byte).

**Never in E9c:** REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT (no show data on purpose); RATE (its
setting is wheel-coupled) and the wheel; GO with STACK lit; Patch / Independents / Settings edits; page clicks
in SmartSoft; Open showfile, New show, Save As, any Erase. The only console-side actions are the page changes
(hold MEMS + a bump) and the physical fader 2 in S4b.

**Evidence and what follows:** the USBPcap file (console-only split), the action log and the two SD files into
`docs/protocol/captures/` with an E9c record (the analysis by the `capture-analyst` agent or the coordinator,
read-only tools); outcomes for H111 (every index and both banks), H79 / H81 (the matching rule with a PC
level), H82 (the new codes), H107, H112, F96; the held-bump timings; the System pair's count for SC-4. Then
the safety check that extends SC-2 (no derivation replaces a capture: an index or a mode seen once in E9c
needs E9d), the owner's implementation, a live session (E11b) under the same conditions as E11.

### E9d runbook (the passive repeat SC-5 names; written 2026-11-02, not run)

**Why:** SC-5 ([safety-checks.md](safety-checks.md)) clears a fader index or a bump only when it was seen in two sessions; E9c saw
most of them once (the channel faders 2–24 of bank 1-24, two faders of bank 25-48, four memory faders, channel bump 1) and some never
(faders 3–24 of bank 25-48, memory faders 4–8 and 10–24, most bumps). E9d gives the second session (and the first for the rest) in
one capture with **two SmartSoft sessions**, counted as E3b's sessions were for E2; it clears nothing by itself. The owner agreed on
2026-11-02.

**Guided script:** `powershell -NoProfile -ExecutionPolicy Bypass -File tools/usb-probe/e9d-guided-session.ps1 -LogFile e9d-actions.txt`
(the E9c gate; then per SmartSoft session one pass A–D below; about 20 minutes per session). **Run 2026-11-02, 09:47–10:14** (record `captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md`): both passes complete, every step as expected.

**Actions per session ("Simulator" = SmartSoft's Console Simulator; waits are in the script):**

- **A.** Every channel fader 1–24 once in bank 1-24 (up to about half, held 1 s, back to zero).
- **B.** FADERS 25-48, then every fader labelled 25–48 once (the Simulator relabels its faders there: E9c's deviation), a bump held 2 s
  under the fader labelled 25 (the bump in bank 25-48 E9c did not capture), FADERS 1-24.
- **C.** FADERS MEMS, then every fader 1–24 once as a memory fader of page 1 (an empty memory moves only its record, F171), every
  memory bump held 1 s, FADERS 1-24.
- **D.** Bump 7 held 5 s (SC-5 bounds a hold to 3 s; the longer hold shows the console's own behaviour beyond it), then E9c's S4a case
  once more (the Simulator's channel fader 1 at half, FADERS MEMS, the drag in memory mode, FADERS 1-24, the drag in channel mode).

Then SmartSoft is closed and started again and the pass is repeated (session 2); the end: 30 s idle, SmartSoft closed, USBPcap stopped,
the after-session SD save (must equal the backup).

**Never in E9d:** the record keys, RATE, the wheel, GO, STACK, NEXT, PAUSE, CLEAR, any SmartSoft edit, page clicks in SmartSoft, show
load or erase. **Evidence and what follows:** the split, the log and the two SD files into `docs/protocol/captures/` with an E9d record;
SC-5's "after E9d" rows are then decided by an SC-5 addendum, per index and bump, from the two sessions.

**E9e (written 2026-11-02 after E9d, cut to a spot check the same day, not run):** the E9d script left out the channel bumps of bank
1-24 (other than 1, 7, 10) and the bumps of bank 25-48 (other than the first). The first E9e script asked for all of them twice (96
presses); the owner refused a second long repetitive session, rightly, and SC-5's addendum 2 clears those bumps on the physical-control
rule instead (VERIFIED for every fader of three banks and every memory bump, F171 / F174). E9e is now the control of that inference:
`tools/usb-probe/e9e-guided-session.ps1`, two SmartSoft sessions, each the bumps under faders 2, 12 and 24 held about 1 s in bank 1-24, then
FADERS 25-48, the bump under the fader labelled 48, FADERS 1-24; about one minute per session; the same gate and end as E9d; nothing
else pressed. **Run 2026-11-02, 10:57–11:06** (record `captures/2026-11-02-e9e-smartsoft-channel-bumps-spot-check.md`): the four bumps as predicted in both sessions, 0 framing
errors, `SHOW29.ASC` = `SHOW28.ASC`; the control of SC-5 addendum 2 passed.

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

### Next capture (proposed by the E8b analysis, 2026-10-30)

Safety rules 1–7 above are unchanged. Everything in A is an owner question or console action that sends nothing; B is a third read-only
session of our software (E8c; SC-1's two messages only, nothing else is cleared); C is tooling; D points to E9. **E9 (the runbook above) is
the only path to a write mode; E8b clears no message.** Source: [E8b record](captures/2026-10-30-e8b-second-read-only-session.md) "Next
capture to take" and the owner's late statements ([captures/2026-10-30-e8b-owner-statements.txt](captures/2026-10-30-e8b-owner-statements.txt)).

**A. Owner, zero risk:**

1. ~~The raw capture's removal from the pushed history (Anomaly 1), the owner's decision.~~ **Done (reported by the coordinator,
   2026-10-30):** at the owner's request ("rewrite please") the raw USBPcap root-hub capture was removed from the pushed history; the
   committed `.pcap` is the console-only split (device address 10).
2. STACK: ~~what did you press at about 20:45:31 (448 s)?~~ **Answered in part (E8b statements, answer 1):** "I pressed it twice back
   then. Sorry for that." He dates it around 20:46:20, whereas the data's only unlogged STACK activity is at 20:45:31 (447.985 s), 49 s
   earlier, and the console listed nothing at all between 489.7 s and 504.7 s (20:46:12.8–20:46:27.8; curator check over the log), so nothing was
   reported at 20:46:20 (the time is his estimate or another action). **Still open:** does a short press switch the
   stack on and a second press off; when does "Jump to step" appear; what does holding STACK do with the stack on (F117)?
3. The sequence on fader 21 was probably still running on the console (fader at zero). **Answered in part:** the owner's way of stopping
   a sequence is the fader ("I am usually run the sequence by moving the fader up or down to stop it"); the data show that this does
   not stop the stepping (F129). **Still open:** how does one stop a running sequence (bump 21, CLEAR × 4 with the fader up, STACK, a
   page change), and what stopped it in E8 at about 16:29:26?
4. The crossfaders: **answered in part:** the left one is A and the right one is B (E8b statements, answer 2). **Still open:** where do A
   and B stand at rest, and which way was each moved first (H80)?
5. The `+` on the LCD right of channel 12 (H77): **answered: the owner does not know it** ("I am not sure what that is either"); the
   console manual's text has no such sign (text search, 2026-10-30).
6. The console's mode and bank before the power-off (it came up in memory mode with the 25-48 labels): **not answered.**
7. GO and PAUSE: the owner calls the key with the play icon under crossfader A the "play button" and does not know the word GO
   (E8b statements, answer 4); his account (two presses, step 14 to 1, then 1 to 2) stands. His description of the stack section (a
   statement, never a fact): with STACK lit the console is in stack mode; PLAY steps to the next step, is lit while the transition
   runs and dimmed when it is done; the crossfaders do the same by hand and PLAY blinks while a crossfader is between 0 and 100 % and
   both are not at 100 % (this matches block 50, H64); **PAUSE** pauses in the middle of a transition (PLAY and PAUSE lit), a second
   PAUSE goes back to the previous step and PLAY resumes. **No PAUSE press exists in any capture: PAUSE is an open item for a later
   capture (B below), not a fact.**

**B. E8c, a third read-only session of our software** (SC-1's two messages, the same setup as E8b, the pcap split to the console's address
before the commit), only for what still needs one:

- H71's separating case: memory fader 2 at full and BUMPS at a quarter, then the bump held (a bump below the fader's level); and CLEAR × 3
  with a bump held. Also BLACKOUT in memory mode with a memory fader up: F126 predicts that the record stays and only the level objects
  drop (a prediction, not an observation).
- The second half of the E8 plan's power-on item, not in the E8b script: Disconnect and Connect again without a power cycle (the read-only
  analogue of F94's sessions 2 and 3; nothing pending is expected).
- H76 / H63: STACK on, GO from step 3 to 4 and 5, a GOTO to a known step; GO with STACK off for comparison (E3b).
- **PAUSE (new, from the owner's description):** with STACK lit, GO, PAUSE in the middle of a transition, PAUSE again, then GO to resume,
  each press with a logged stamp and the lit keys noted, to see what the console reports (PlaybackInfo record 5, LED block 50 and the
  PAUSE LED, the LCD); the presses are the owner's own, nothing is sent by us.
- The running sequence: stop it the owner's way, logged (question 3).
- H77: a channel in bank 25-48 up, view switched to 1-24. H78: two power cycles in different modes, with H65 (b) and (c) (power on, move
  one fader / press SOLO, connect).
- From the E3b list, still open: SOLO with another output up, several stable MASTER positions (H60), BLACKOUT with the MASTER below full
  and a level up (F114's masking with levels).
- H74 (RATE / Fade) only if the owner wants it (its test uses the wheel, which the owner's scope decision above rules out for experiments);
  H75 cannot be forced read-only; the floors (H67) need a configuration change (owner decision).

**C. Tooling (zero risk):** commit the E8b ad-hoc scripts under `tools/protocol-analyzer/e8b/` (read-only; most import `hsp_summary.py` like
the E8 ones, take `<repo> <jsonl>` and print by seq): `xcheck.py` (the backend log against the usbpcap JSONL, URB by URB, with the split
reads and the clock offsets), `tshark_xcheck.py`, `census_usbpcap.py`, `objtable.py`, `cadence.py`, `pbinfo.py`, `pb_stats.py`,
`pb_rate.py`, `fader_bytes.py`, `fdr_at.py`, `cst_led.py`, `cmp_first.py`, `blk2548.py` and `logtimes.py`; add the normalization command of
the E8b record to the analyzer README. The `WinUsbHspPipe` comment that marks WinUSB's control requests at open as an ASSUMPTION (SC-1 item 5)
is answered for this PC (F123): a code-comment update for the coordinator, not a documentation change.

**D. E9** (passive USBPcap capture of SmartSoft writing) stays the only path to a write mode. For E9: put the ETC driver back on interface 1;
split the pcap to the console's address before committing; expect PlaybackInfo to be listed continuously while the sequence runs (it was
still stepping when E8b ended); the stack stands at cue 3.0 with STACK off. **E8b clears no message:** SC-1's clearance (poll and listed
fetch, read-only) is unchanged. *(Done 2026-10-30, late evening: E9 ran, see its row and the next section.)*

### Next capture (proposed by the E9 analysis, 2026-10-30)

Safety rules 1–7 above are unchanged. A is owner questions and console checks that send nothing; B is **E9b**, the repeat of E9 (passive,
SmartSoft, after the corrected guide), the second session that could make F131–F134 VERIFIED and that tests H81–H86; C is a code question
for the coordinator; D is tooling. **E9 clears no message: SC-1's poll and listed fetch remain the only messages cleared, and E9b and a
new safety check SC-2 stand between the observed write messages and any write of ours.** Source: [E9 record](captures/2026-10-30-e9-smartsoft-writes.md)
"Next capture to take" and the owner's answers of 2026-10-31 ([captures/2026-10-30-e9-owner-statements.txt](captures/2026-10-30-e9-owner-statements.txt);
statements, never promotions).

**A. Owner, zero risk:**

1. Memory 7 of page 1: is channel 7 at full? **Asked 2026-10-31: "Its possible." (a statement); not yet checked or restored. An open
   owner item.** Look at it on the console (EDIT MEM, bump 7, look at channel 7's bump, leave with CLEAR) or save `SHOW08.ASC` to the SD
   card and copy it into `smartfade-sd-card-backup/`, so that `GROUP 1`, `GROUP 7` and the absence of memories 9 and 10 can be compared
   with `SHOW07.ASC`, the pre-E9 state (E9 record "Side effects on the show", F139). **Done 2026-10-31:** `SHOW08.ASC` (the E9b
   pre-session backup) showed `GROUP 7` = `CHAN 7/100 15/8`, `GROUP 1` unchanged and no memories 9 and 10; the owner removed channel 7 from memory 7 by
   hand and `SHOW09.ASC`, saved afterwards, is byte-identical to `SHOW07.ASC`. **Closed.**
2. ~~Step 12: which SmartSoft window did you use, the Console Simulator or the Mem Edit tab?~~ **Answered in part (2026-10-31):** "Uuuff. I do
   not remember. Sorry. From what I remember I used console simulator for mem edit." This agrees with the capture (only Simulator key and
   fader events, no Memory object write), and a recollection promotes nothing. **Still open:** did you press the bump under fader 7 in the
   restore step, thinking of channel 7? (The capture shows key 0x06 after EDIT MEM and the console's LCD "Edit:" / "01/07".)
3. ~~At about 22:24:39 (279.5 s), while answering the fader-1 question: did you touch the physical fader 1 or another key?~~ **Answered
   (2026-10-31):** he did nothing and "it dropped by itself"; he guesses that "the panel registered the movement (most likely its a
   electrical resistance I guess) by accident" on an old device, or a software mistake. **The cause stays UNKNOWN** (his guesses are
   statements, not evidence; the H81 test below shows what a physical fader reading does to a PC-set level).
4. At about 22:24:50 (290.5 s): did you move the Simulator's fader 1 down and up again? **Not answered** (his reply to question 3 did not
   address it; the move is INFERRED from the timing).

**B. E9b, the repeat** (passive, SmartSoft, after the corrected guide 6cb2c07; the guided script is `tools/usb-probe/e9b-guided-session.ps1`,
committed 3ce3900 from this plan; no E9b capture exists yet), for VERIFIED and for H81–H86:

- The same live steps (fader 1, bump 7, MASTER, BUMPS, BLACKOUT, SOLO, MEMS with memory bump 1, 1-24, GO with STACK unlit) to reproduce
  F131–F134.
- H81: the Simulator's fader 1 to full, then the physical fader 1 touched; the physical fader 5 at half, then the Simulator's fader 5 from zero;
  MEMS with a Simulator fader up; note the bump LED.
- H82: Simulator presses of keys not yet seen and safe for the rig: STACK (then off), IND 1 and IND 2 (IND 2 drives DMX 47 only, IND 1
  nothing, F115), FADERS 25-48 and back.
- H83: EDIT MEM and a bump on the physical console (page 1), and once on page 12 (page changed on the console), left with CLEAR.
- H84: the "Čaj" label, then a SmartSoft restart (a new download), then the restore and another restart.
- H85, H86: REC MEM into memory 9 with channel 5 at a known level and the MASTER at half; delete it with REC MEM of an empty look (the
  owner's method); watch what the console lists.
- Optionally (owner's choice) the Mem Edit **tab** on memory 9 of page 1 (a cell edit), to see whether a Memory (0) write exists at all; then
  delete memory 9 again.
- As before: backup first (`SHOW08.ASC`), USBPcap before SmartSoft, the split before the commit, no wheel, no REC SEQ, no patch / settings /
  page / show / erase action in SmartSoft.

**C. Code questions for the coordinator (no change made here):** F137 (c ≠ 0 in a listed record) against `HspReadSession`'s fault rule in SC-1
sessions (a safe stop today, safety-checks.md "E9"); the Memory objects a read-only session would now receive after console-side recordings.
The Phase 2 sketch for live control (docs/architecture.md "Phase 2 proposal", not implemented) waits for E9b and SC-2.

**D. Tooling (zero risk):** commit the ad-hoc read-only E9 views (writes per step, kind-0 bursts with the previous-byte check, kind-1 press
/ release pairing, LED blocks per window, show objects after the download, list → fetch check) under `tools/protocol-analyzer/e9/`, as was
done for E8b; add the normalization command of the E9 record to the analyzer README.

**E9 clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no type-1 data header may be encoded
anywhere in the solution.

*(Done 2026-10-31: E9b ran, see its row and the next section; the owner item A1 was closed the same morning.)*

### Next capture (proposed by the E9b analysis, 2026-10-31)

Safety rules 1–7 above are unchanged. A is owner questions and checks that send nothing; B is the next passive capture (SmartSoft, guided) for
what E9b left open; C is code questions for the coordinator; D is tooling. **E9b clears no message: SC-1's poll and listed fetch remain the
only messages cleared, and the safety check SC-2 stands between the observed write messages and any write of ours.** Source: the
[E9b record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md) ("Next capture to take", "For the curator", "Input for a future safety check
(SC-2), updated after E9b") and the owner's statements of 2026-10-31 ([captures/2026-10-31-e9b-owner-statements.txt](captures/2026-10-31-e9b-owner-statements.txt)
and the action log [captures/2026-10-31-e9b-actions.txt](captures/2026-10-31-e9b-actions.txt); statements, never promotions).

**A. Owner, zero risk:**

1. ~~Memory 7 of page 1 still holds channel 7 at full.~~ **Done 2026-10-31 (about 09:45):** the owner restored it by hand; `SHOW09.ASC` is
   byte-identical to `SHOW07.ASC` (F139). **Closed.**
2. ~~The physical MASTER stands at about half while the console's MASTER level is full.~~ **Done** ("master is on 100% already", about 09:35;
   a statement).
3. The E9 questions were answered at E9b's start (action log 08:36:18–08:36:59, statements): the Mem Edit window of E9 was the Simulator; he did
   not touch the fader at 279 s ("n"); the Simulator's fader 1 was moved again at 290 s ("y"); and memory 7 "was in memory 1, not 7", which the
   download and `SHOW08.ASC` contradict (F139).
4. Questions from the E9b record: (a) In H85, did you move the console's MASTER (the data show no Simulator event)? (b) In B7, did you use the
   console's REC MEM and bump 9 for the recording and the deletion? (c) At about 08:53:59 (991.6 s) and in the two checks of memory 9 (08:59:04,
   09:00:19), did you use the Simulator's MEMS / FADERS 1-24 keys? (d) At 09:02:48–09:02:56, was the order of your check MEMS, memory fader 9 up,
   FADERS 1-24, MEMS, fader 9 down? (e) **H89: does SmartSoft's Simulator show a physical fader's movement on screen?**

**B. Next passive capture (SmartSoft, guided), for what E9b left open:**

- **H83 with c ≠ 0:** on the console, EDIT MEM and the bump of **memory 2** (page 1), then CLEAR; this decides whether a read-only session of
  ours stops when the owner edits a memory (expected System (5, 5, 1), F137, F142).
- **H87:** after the next SmartSoft start, is the emptied memory 9 named in the 157-record list? Optionally a second Mem Edit tab write followed
  by a SmartSoft restart, to see the console's stored body after a 48-level write (then delete the memory again).
- **H88 / H81:** a Simulator fader left up for several minutes with the physical fader untouched, watched for drops; then the physical MASTER
  touched while the console's MASTER is at a PC-set level; then MEMS with a Simulator fader up and a page change with one up (the matching-rule
  clause).
- **H82:** one bump in bank 25-48 from the Simulator (channel 25 is patched to DMX 25, outside the rig on DMX 5–16, F99).
- **H90:** a memory fader up across FADERS 1-24 and back, with every fader movement logged.
- **H111 (added 2026-11-01 after E11 attempt 3):** a Simulator fader moved in memory mode (page 1, then another page) and in bank 25-48, the
  Simulator's memory bump 1 held, and a mode switch with a Simulator fader up: the owner wants to drive memory faders from the PC without switching
  the console by hand, and nothing of it has been observed (see safety-checks.md "Path to a wider clearance").
- **H40 (console side):** while channel 7 is labelled "Čaj", save the show to the SD card and read `$CHANNEL 7` in the file; then restore the label.
- E9's PC-key record functions (EDIT MEM store, CLEAR, COPY) only if SC-2 needs them; the owner's write mode does not.
- As before: backup first, USBPcap before SmartSoft, the split before the commit, no wheel, no REC SEQ, no patch / settings / page / show / erase
  action in SmartSoft.

**C. Code questions for the coordinator (no change made here):** (1) the simulated device's REC MEM stored pre-MASTER levels (the INFERRED
reading of F107); E9b says post-MASTER (F145): **done 2026-10-31, commit ac00b9e**. (2) `ConsoleStateAssembler` reads FaderInfo records only in
memory mode (memory faders, F112) plus the masters (body[193], [194]), and its comment already treats a fader's physical position as
unreported; F141 supports that (after a PC event or a takeover FaderInfo carries the console's level, not the fader's position), and the H90
case does not touch its readings as built: no change suggested. (3) SC-1 sessions will see Memory objects with byte 2 = 0 and zero levels after
deletions (F146), which fits the assembler's "recorded = some level above zero" (INFERRED there; byte 2 may be the console's own flag, H87), and
System (5, 5, 0) on console-side EDIT MEM selections (F142); a decoder should not assume 96 levels in a Memory body that did not come from the
console (F143).

**D. Tooling (zero risk):** commit the ad-hoc read-only views used for E9b under `tools/protocol-analyzer/e9b/` (writes per step with the action
log, the merged live-field timeline, the MASTER byte over all FaderInfo payloads, the held-bump byte comparison of two captures, the non-live
objects outside the downloads, the three downloads compared); add the E9b normalization command to the analyzer README.

**E9b clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no type-1 data header may be encoded anywhere in
the solution.

### Next capture (proposed by the E12 analysis, 2026-10-31)

Safety rules 1–7 above are unchanged. A is owner questions that send nothing; B is the repeat E12b (passive, SmartSoft, guided); C is
documentation and code questions for the coordinator; D is tooling. **E12 clears no message: SC-1's poll and listed fetch remain the only
messages cleared, SC-2's classes are unchanged (the Patch and Independents writes class F, "Never"; the Memory write class D, NO-GO), and
the step commands and the Sequence, SequenceMain, StepContent and Page writes are in no class; our code never sends any of them.** Source:
the [E12 record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md) ("Owner questions", "Next capture to take", "Anomalies and notes") and the owner's statements
([captures/2026-10-31-e12-owner-statements.txt](captures/2026-10-31-e12-owner-statements.txt); statements, never promotions).

**A. Owner, zero risk** (the record's questions, kept there; the answers are statements and never promote):

1. A3 (15:44:03–15:47:44): did you open the IND panel, click PLAY in the stack view and insert and delete a Direct cue; on the console,
   did you move fader 1 and press STACK and the key that showed "Jump to step"? (F158)
2. A3: did you see a level control in the Patch tab, yes or no? (H100)
3. C3: what did SmartSoft show for IND 1's button mode and GM before and after each change, and why three writes for one change? (H93, H44)
4. D4 (16:00:08.4): where did you type "T", and what did you click just before (SmartSoft sent FADERS MEMS)? (F158)
5. 16:05:42.5: what did you click? SmartSoft sent key code 0x3A once; was it PAUSE? (H104)
6. D1, D5, E1: which row did you right-click? (where SmartSoft's Insert places the new step)
7. I2: did you type "Direct cue custom", or did SmartSoft offer "Direct cue"? (F159)
8. The console was left in memory mode (FADERS MEMS lit) and the stack's playback ran one crossfade in A3: set the console as you want it
   (FADERS 1-24). Not show data; the SD file is unchanged.

**B. E12b, the repeat** (passive, guided, the same safety frame as E12: a named SD backup first, USBPcap before SmartSoft, every change
restored in the same session, two restarts and an SD save compared byte for byte; never a channel onto the un-patch bin; no show, erase,
settings, wheel or page action in SmartSoft): a second capture after its own power cycle, to make H91–H99, H101 and F151–F159 VERIFIED as
SmartSoft's and the console's behaviour, with the A3 looks removed from the look-only steps (or logged as actions) and these additions,
each restored in place:

**E12b guide (written 2026-10-31, evening, parse-checked; trimmed the same evening at the owner's request; not run):** `tools/usb-probe/e12b-guided-session.ps1`, run from the repository root with `-LogFile e12b-actions.txt`, about 30 minutes. Nothing of E12 is repeated for its own sake: memory 9, the Mem Edit cell, the memory name and the console's REC MEM steps are gone (F143, F146, F147 are VERIFIED), the test steps reference memory 01/01, and the third SmartSoft session is gone (the after-session SD save is the proof of the restore). What stays sees E12's writes a second time (one Patch drag and its un-patch, the IND panel, InsertStep / StepContent / Sequence / Page, MoveStep, DeleteStep: H91–H99, H101, F151–F159 become VERIFIED as behaviour only with this) and adds, each restored in place: D5 a Direct cue inserted with channel fader 5 at half (a level in it) and labelled `čAJ123456789012345` (a "č" and a full third line, H40), D6 a channel step (channel 47, type 0) with one time edit, D2 a point cue later moved (MoveStep, id 42) and deleted, E1 / E1b inserts at step 3 and at the end of KOTLIK with the landing place logged (H102, H103), E3 / E3b One Shot then Manual, F3 page 12's name, S an SD save of the edited show (the file forms; a third SD number), C3 the IND values named by the owner before and after each change (H44, H93), and in the optional H the console's own Step Timing on the Direct cue and its Insert / Delete step menus in the stack and in KOTLIK (H106). E12's lessons are built in: no looks outside the steps and never the IND panel, PLAY or PAUSE outside their steps (a look is a write, F158), no memory-step label, the right-clicked row and the landing place logged at every insert, a "did you click anything else" question after every section, the USBPcap output path absolute, and the console returned to channel mode at the end. The owner questions A1–A8 above are asked in chat, not by the script. *(Superseded 2026-11-01, text kept: the guide was run on 2026-10-31, late evening (E12b, see its row) and the capture is analysed; the next steps are in "Next capture (proposed by the E12b analysis, 2026-10-31)" below. The owner questions A1–A8 above stay open where E12b did not answer them.)*

**E11 guide (written 2026-10-31, evening, parse-checked; not run):** `tools/usb-probe/e11-guided-session.ps1`, the first live-control session of our software as SC-2 conditions it (safety-checks.md "Conditions for E11"); it starts nothing before the re-check's open items are closed and the owner's written acceptances are given, and since the evening of 2026-10-31 it includes FADERS MEMS / FADERS 1-24 and GO from the UI as optional steps (their port methods and UI keys were added at the owner's wish). E11 and E12b are independent: E12b is passive (SmartSoft), E11 is ours.

**E11 attempt 1 (run 2026-11-01, morning; no write sent):** the owner ran the guide (7789a71) with the backend at 5416d7f; the console, power-cycled earlier and untouched since, listed nothing in 192 s (F166: no power-on list was pending after this history, H65 annotated), so the device refused the first control by condition 8 before any transfer, the guide mistook the refusal for the H66 stop, and the session ended at an idle point with 5 183 polls, 0 writes and SHOW18 = SHOW17. Record `captures/2026-11-01-e11-first-live-control-session-attempt-1.md`. The guide now makes the console report its state from the panel first (W0) and treats a refusal as "not sent"; attempt 2 is the owner's. E11's conditions (safety-checks.md) are unchanged.

**E11 attempt 2 (run 2026-11-01, afternoon; the first writes of our software):** the owner ran the guide at 85aa2f5 (W0 included); the console had LedStatus + DisplayContent pending at Connect (H65 annotated), W0 made it report its state (23 lists), and channel fader 1 dragged in the UI sent four class-A fader events that the console followed like a physical fader move (F167; H66 CONFIRMED in part for writes; record `captures/2026-11-01-e11-first-live-control-session-attempt-2.md`); the bus equals the backend's log byte for byte. The session ended on a mistyped guide answer (the case letter "b"), so W1's return to zero and W2–W8 are attempt 3's, with the guide now accepting the case letters a / b / c and confirming before an H66 stop. The owner's answers the same evening: the console was power-cycled again between the attempts (so the Connect-time pair is H65's power-on list; attempt 1's empty Connects stay unexplained), and after the Disconnect the console kept the PC value on channel 1 until the physical fader 1 was moved up and down (F141, a statement); `SHOW20.ASC` = `SHOW19.ASC`. E11's conditions (safety-checks.md) are unchanged; nothing further is cleared.

**E11 attempt 3 (run 2026-11-01, evening; E11 done):** the owner ran the guide at 5485ea3 to its end; the console had the power-on pair pending at Connect (H65 annotated), W0 made it report its state (24 lists), and the UI then sent 116 ConsoleEvents, W1 to W8 (channel fader 1 up and back to zero, bump 7, SOLO, BLACKOUT, BUMPS, FADERS MEMS / memory bump 1 × 3 / FADERS 1-24, MASTER, GO × 2 with STACK unlit), each 27–46 ms after a poll's reply; the console followed every control as it follows the panel and SmartSoft (F167 VERIFIED in part, F168, F169; H66 CONFIRMED for writes, OBSERVED once each; record `captures/2026-11-01-e11-first-live-control-session-attempt-3.md`); the bus equals the backend's log byte for byte; `SHOW22.ASC` = `SHOW21.ASC`. A bump tap from the PC (47–77 ms) leaves no trace in the fetched objects; the owner's hold of the console's own memory bump 1 showed memory 1 as in E9 / E9b. After the guide the owner ran the backend twice more by hand (a read-only session and a live-control session with 57 writes of the same classes) without USBPcap and after the SD save: a recorded deviation, log-only evidence; `SHOW23.ASC`, saved afterwards and copied 2026-11-02, = `SHOW22.ASC`. The owner's question of the same night (memory faders from the PC without switching the console's mode) is answered from the evidence in the record and in H111: the console's fader mode decides what a fader event drives; E9c (section B below) is the path. E11's conditions are unchanged; nothing further is cleared.

- a **channel step** (type 0) in the stack (H12, H98);
- **Manual** (run mode 2) on KOTLIK and back (H95);
- an insert and a delete at **another index** of KOTLIK (H102, H103);
- a **console-side** Insert step and Delete step in the stack and in KOTLIK, and a console-side Step Timing on a "State" step (H106: what a
  read-only session would see; SmartSoft Online, never our software);
- an **SD save while edited** (One Shot, 8 steps, a point cue, a State step with a level, a page and a memory label), then the restore and a
  second save, to learn the file forms (`$SEQUENCE … b`, `$$CUEPARAMS 2`, asc-format.md);
- the IND values with the owner naming what SmartSoft shows (H44, H93);
- a "č" in a Direct cue's label and a 6-character third line (H40, the console's side via the download).

**C. For the coordinator (documentation and code questions; no code was changed in this pass):** (1) the comment of
`HspReadSession.ObservedBodyLengths` says "one length per id in every capture"; E12 showed ids 2 and 4 in other lengths (144, 148) when
their content differs (F156), so the comment no longer describes the captures; the fault on another length is a safe stop and is not
questioned here. (2) A read-only session also faults on a listed Sequence record with c ≠ 0 (F156); whether to accept such records, or
other lengths for ids 2 and 4, is an SC-1 code and review question, not decided here. (3) A decoder of Sequence and StepContent bodies must
take the length from the object (10 + 38 + 12 n; 13 or 148) and must not assume c = 0.

**D. Tooling:** done with the analysis (`tools/protocol-analyzer/e12/`, read-only, one row each in the analyzer README).

**E12 clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no encoder for any of E12's messages may
exist in the solution.

### Next capture (proposed by the E12b analysis, 2026-10-31)

Safety rules 1–7 above are unchanged. A is owner questions that send nothing; B is optional passive captures (SmartSoft Online,
USBPcap first, a named backup first, our software not running; each one action with a wait; the owner decides); C is zero-risk static
checks; D is for E11 and the coordinator. **No repeat of E12b is needed for the reproduced findings.** **E12b clears no message:** SC-1's
poll and listed fetch remain the only messages cleared, SC-2's classes are unchanged, the key codes 0x32 and 0x3A are outside its
whitelist, and our code never sends any of E12b's messages. Source: the [E12b record](captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md) ("Owner questions",
"Next capture to take", "Anomalies and notes") and the owner's statements
([captures/2026-10-31-e12b-owner-statements.txt](captures/2026-10-31-e12b-owner-statements.txt); statements, never promotions). The E12 list's owner questions A1–A8
above stay open where E12b did not answer them.

**A. Owner, zero risk** (the record's questions, kept there; the answers are statements and never promote):

1. 22:40:05–22:40:30: SmartSoft sent key code 0x32 six times, GO four times and 0x3A four times (three times around 22:40:15, once at
   22:40:27). Which SmartSoft buttons are "next", "PAUSE" and "PLAY" (their labels or icons), and which did you click around 22:40:15? On
   the console, which key's LED lit light blue then (LED block 59) and went off at 22:40:27? (H107, H108)
2. D5 label: did you type a small "č" or a capital "Č" (was Caps Lock on)? SmartSoft wrote "Č" (U+010C); in E12 a step label kept its
   small letters. (F161)
3. D5: did SmartSoft's Insert step > Direct cue offer any way to give the cue levels? It was written with every level at zero although
   fader 5 stood at about half. (F161)
4. The console after the session: is STACK still lit? The capture ends with STACK lit (LED block 60) and the stack's live step 14.0 with
   no next step; please switch STACK off before E11 (its GO step needs STACK dim). Live state, not show data (SHOW16 = SHOW14).
5. (Minor) C3: "BUMP, double click" gave one write; in E12 one change gave three writes (00 → 01 → 00 → 01). How many clicks then? (H93)

**B. Optional passive captures** (none is needed for what E12b reproduced):

1. The console's own Insert / Delete step in the stack and in KOTLIK, and Step Timing on a "State" step (H106: what a read-only session of
   ours would see; SmartSoft Online, never our software).
2. One logged PAUSE press on the console during a GO fade with STACK dim and nothing up (H107), and one logged press of the key with the
   light-blue LED (H108).
3. One logged SD save on the console while SmartSoft is Online, with the confirm press logged (H109: the gap and its length again).

**C. Zero-risk static checks:** the Console Simulator's layout resource (smartsoft-analysis.md S11.15 row 4, its "Button" sections) for the
key codes 0x32 and 0x3A (H107, H108); the step-label editor's maximum length in `SmartSoft.exe` (H110); the insert dialog's accept path
(H105, S11.16 row 6).

**D. For E11 and the coordinator (no code was changed in this pass):** (1) no SD save or load on the console while our software is
connected (F163, H109): the E11 guide keeps its order (Disconnect, then the SD save) and, since commit 7789a71, says so; recorded as a
condition for E11 in safety-checks.md. (2) STACK dim before E11's GO step (owner question 4). (3) Code comments that cite the console's
reply times are the coordinator's (the E12b record's note on `UsbDeviceOptions`). (4) `HspReadSession.MaxObservedPayloadLength` (792 bytes)
is shorter than the full lists after edits (E12b 807 bytes, E12 812), which a read-only session never asks for (it sends no System
`07 00 00`; F157); no change proposed here.

**E12b clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged, and no encoder for any of E12b's messages may
exist in the solution.

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
