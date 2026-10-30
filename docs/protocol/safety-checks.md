# Hardware safety checks

Log of the `/hardware-safety-check` runs (checklist in `.claude/skills/hardware-safety-check`).
A check clears a message **for the stated use only**; everything it does not name stays
uncleared. CLAUDE.md: VERIFIED is necessary, not sufficient; a message may be sent only after it
was observed from SmartSoft on the 1248, understood, shown to be harmless and cleared here.

## SC-1 (2026-10-30): poll and listed fetch, read-only session

**Scope.** The two PC → console headers a read-only session needs, byte for byte as SmartSoft
sends them:

| Message | Bytes | Rule | Cleared for |
|---|---|---|---|
| Poll (type 0) | `00 00 00 00 w2 w3 w4 w5`, little-endian words; w2..w5 = the words of the most recent fetch or reply header, all zero before the first | F52 (38 517 polls in E2, E3, E3b; the copy rule holds in 38 517 of 38 517, re-counted with `tools/protocol-analyzer/poll_rules.py`) | polling at SmartSoft's cadence or slower (floors from the observed minimums, rounded up: polls never closer than 21 ms, never earlier than 11 ms after a fetch) |
| Fetch (type 2) | `02 00 00 00 id a b c` | F52, F57 (1 651 fetches: every one names a record of a DirtyObjects list the console sent in the same session, in list order, once; a poll always follows a fetch and is answered by the fetched object) | fetching **only** a record the console listed in this session, in list order, once, not earlier than 21 ms after the payload that listed it, and only an id SmartSoft was seen to fetch (`HspReadSession.ObservedListedIds`) |

Implementation: `SmartFade.Protocol.HspOutbound` (the only encoder), `HspReadSession` (the state
machine, which faults on any reply that no capture has shown), `SmartFade.Devices.Usb.HspReadLoop`
(cadence, re-checked against the clock after every delay), `WinUsbHspPipe` (interface 1 of
VID 14D5 / PID 0200 only, whatever the path's source). **Not cleared, not encoded anywhere:** Data
headers (type 1), the System writes `09 00 00` / `07 00 00`, any payload, any transfer on
interface 0, any control transfer, an unsolicited or repeated fetch, a fetch of an id never seen
in a list, any message to a device other than VID 14D5 / PID 0200 interface 1.

### Decision: GO (conditional)

GO for building and reviewing the code and for the first read-only session, **E8**, once the
conditions below are met and the owner gives the go on the day. The session itself is an
experiment: three things in it are unobserved (items 2 and 11) and are written down as hypotheses
H66, H67 and H68, with the session ending on the first deviation.

| # | Item | Status | Evidence |
|---|---|---|---|
| 1 | Evidence status | **Yes for the bytes; timing proceeds at OBSERVED with the owner's go** | Framing F51–F54 VERIFIED (three sessions); stale-word rule F52 (38 517 of 38 517); replies F53 (every poll answered by exactly one 12-byte header of type 3 or 4; the poll after a fetch answered by the fetched object in 1 651 of 1 651; type 4 carries zeros); DirtyObjects F57; sequence rule F55 (in part). The cadence is SmartSoft's measured host behaviour (F62 and its 2026-10-30 annotation: OBSERVED per-session figures, not console limits), used as floors rounded up; the checklist allows a poll at OBSERVED timing with the owner's explicit go-ahead, which E8 asks for. Runs of all-zero polls answered idle: 40 in a row at the start of two E3b sessions, 2 in E3, 1 in E2 (`poll_rules.py`). |
| 2 | Side effects understood | **Yes for the messages; the session shape is new** | A poll is answered by one header and changes nothing seen (0 anomalies in 38 517, no state object ever changed on a poll). A listed fetch delivers the object once and advances the console's payload counter (F53, F55, F57). **Unobserved:** a session that never sends SmartSoft's two System writes (does the console keep reporting live changes? H66), our own timer jitter (H67), and a session that starts without the string-descriptor reads every SmartSoft session issued on EP0 before its first poll (F56 step 1; H68). Mitigation: the session reproduces SmartSoft's cadence, sends nothing else, and stops on the first reply that differs from observed behaviour. *(E8, 2026-10-30: H66 and H67 CONFIRMED for what was used; E8b, the same evening: H66 and H67 in a second session and H68 CONFIRMED, see "Afterwards" below.)* |
| 3 | Backup | **Condition** | The owner saves the show to the SD card before E8 and the guided script records the name (expected SHOW06.ASC). |
| 4 | Rig | **Condition** | The DOTZ BAR 1.4 on DMX 5–16 (owner statement 2026-10-30, nothing on DMX IN); no dimmers. The MASTER down for the idle part is the owner's choice; nothing we send can change an output as far as three captures show. |
| 5 | Firmware path untouched | **Yes** | Bulk transfers on interface 1 only; our code issues no control transfer, no pipe reset, no stall clearing; no SFDownloader, no "Firmware Upgrade", no HID. ASSUMPTION, not observed: WinUSB itself may issue standard control requests when the handle is opened; E8's USBPcap records EP0 at plug-in, at the Zadig bind and at Connect to settle it (H68). Binding WinUSB with Zadig changes the host's driver for interface 1 only: an owner action, picked by the USB ID `14D5 0200 01` (never by Zadig's label), reversible (Device Manager → the WinUSB device → uninstall and reinstall the ETC driver, or "Update driver" → pick "USB High-Speed-Pipe"); that nothing on the console changes at the rebind is INFERRED (a driver swap on the host) until E8's USBPcap at the bind shows what the host sends (H68). **Answered for this PC (E8b, 2026-10-30; the text above is kept):** USBPcap ran from before the Zadig bind and recorded EP0 at the bind, at the backend start, at Connect and at Disconnect. The console's only EP0 traffic besides the injected descriptors was one burst at the driver change (20:36:45.016–.094, f7–f18: two GET_DESCRIPTOR STRING 2 reads, a HID class SET_IDLE to interface 0 that the console stalled, the HID interrupt reads cancelled and resubmitted, GET_STATUS), 11 s before the backend started and not issued by our code; **nothing at the backend start, at Connect or at Disconnect**, so the ASSUMPTION that WinUSB itself may issue standard control requests when the handle is opened is answered for this PC: none reached the bus (F123, H68 CONFIRMED). The rebind re-enumerated nothing (address 10 throughout) and the first poll, 78 s after the last string read, was answered normally; that nothing on the console changes at the rebind stays INFERRED in the sense that no change of console state was seen. Limit (an ASSUMPTION about the USB stack): USBPcap shows the URBs that pass its filter. |
| 6 | Mode gating | **Yes, built** | `Device:Mode=Usb` plus `Device:Usb:ConfirmDate=<today>` must both be given, or the backend refuses to start: a setting left in a shell, a launch profile or appsettings expires by itself ("this run only"). `Simulated` stays the default; any other value refuses to start; the UI badge shows HARDWARE in the alert colour; `supportedCommands` is empty, so every control is read-only and the backend refuses every command (`not_supported`); the masters are shown dimmed as "not reported" until FaderInfo arrives (an earlier LedStatus only keeps its SOLO reading for then), the channels, outputs and memories carry the same "not reported" state until the console lists them, and BLACKOUT / IND 1 / IND 2, which no observed object carries, are marked unknown. "This day only": the confirmation is a date, read from any configuration source, checked at start-up and again at every Connect against the clock, so a backend left running past midnight refuses to connect until it is restarted with the new date (a session already running is not interrupted). Tests: `HostingTests` (every test host pins its mode: `Simulated`, or `Usb` with the pipe factory replaced and a device path the pipe refuses). |
| 7 | No blind retries | **Yes, built** | `HspReadSession` never re-sends; a transfer timeout (500 ms, the console answered within 12 ms in every capture), a short transfer, an announced length above the observed maximum (792 bytes), an object body of a length never seen for its id (F67) or any other unobserved reply ends the session in the Error state with its reason, whatever the exception; a stop request is honoured only at an idle point (no fetch is left without its poll; a drain bound of 2 000 exchanges exists but is unreachable with the observed lists); host shutdown stops the session; Connect is refused while a session is alive; a session that outlives its stop reports its real end. Tests: `HspReadSessionTests`, `UsbSmartFadeDeviceTests`, `ConsoleStateAssemblerTests`. |
| 8 | Logging on | **Yes, built; plus a condition** | Every transfer of a session is written to a capture-format JSONL (`Device:Usb:CaptureLogDirectory`, outside the repository by default, readable by `hsp_summary.py` and `CaptureFile`; written after each exchange, never between a poll and its reply; the log is created before the console is even opened, so without a log the console is never touched). E8 also runs USBPcap in parallel for independent evidence. **Cross-checked against the bus (E8b, 2026-10-30):** the parallel USBPcap capture ran this time (the console-only split is committed) and the frame-by-frame comparison found **61 112 of 61 112 backend transfers equal to the bus byte for byte and in order**, USBD status 0 in all 62 957 bulk URBs and the log's times within 0.75 ms of the bus (the log's own `status` stays null, as its header says); the log is therefore a faithful record of what our code sent and received, with one host-side difference: WinUSB splits an IN read longer than 64 bytes and not a multiple of 64 into two URBs, which the log records as one read (F122). |
| 9 | Versions recorded | **Condition** | Firmware (Menu > Diagnostics), Windows build, cable, backend version and git commit in the E8 record. |
| 10 | Review done | **Yes: two passes and a re-check of their fixes (PASS)** | `protocol-safety-reviewer` over the exact code, see "Review" below: the first pass (2026-10-30) found two blockers and a set of majors, the second pass one blocker and three majors, all fixed the same day; the re-check of those fixes passed, with eleven minor leftovers fixed or accepted as recorded there, commit hashes included. |
| 11 | Passive first | **Yes** | E2, E3 and E3b (2026-10-28 to 2026-10-30) show SmartSoft producing both messages and the console answering them. |

### Golden evidence behind the clearance

`HspReadSessionTests` replays the three committed captures through `ReplayHspPipe`: the session
engine is handed the console's transfers in capture order and every transfer it decides to send
is compared byte for byte with SmartSoft's at the same point. Result on 2026-10-30: E2 3 001 polls
+ 228 fetches, E3 9 692 + 423, E3b 25 824 + 1 000, all identical; the only transfers skipped are
SmartSoft's ten System writes (2 + 2 + 6); no reply failed a check. `UsbSmartFadeDeviceTests` runs the whole
device the same way and checks the assembled state against the records (firmware string, show
file names, MASTER / BUMPS / SOLO at the end of E3b, memory OPONA with channels 7 and 8 at full),
and, on a virtual clock, that every transfer of the three replays keeps the cadence floors (poll
to poll, fetch to poll, payload to fetch) on every single transfer.

### Owner decisions recorded with SC-1

- **Inbound decoding for display at OBSERVED or INFERRED.** CLAUDE.md allows only VERIFIED
  elements in code that talks to hardware. No transmitted byte depends on anything below
  VERIFIED (the session uses F51–F57 only), but the state shown in hardware mode rests on
  OBSERVED readings (post-MASTER levels F96, BUMPS F95, the OutputLevels index F83) and INFERRED
  ones (SOLO from its LED F100, the recorded flag of a memory from the manual's deletion rule
  F107) and on hypotheses (label byte order H40, memory page / fader H25). Proposed reading:
  **display-only decoding may use any status, marked as such in the UI and in
  `ConsoleStateAssembler`; nothing sent may.** The owner is asked to confirm this exception; until
  then it is applied as written here. Values the console never reports (Blackout, IND 1 / IND 2)
  are shown as unknown, never as off.
- **Timing at OBSERVED.** The cadence floors are SmartSoft's measured minimums (F62 annotation);
  the owner's go for E8 is the explicit go-ahead the checklist asks for.

### Conditions and abort rules for E8

- Before: backup saved and named; USBPcap capture started; Zadig applied to the entry with USB ID
  `14D5 0200 01` only; Memory Integrity may stay on (WinUSB is an inbox driver).
- Start the backend with `--Device:Mode=Usb --Device:Usb:ConfirmDate=<today>`; the UI must show the
  HARDWARE badge and "Not reported by the console yet" on the masters before Connect.
- The session ends by itself on the first deviation (Error in the UI with the reason). Do not
  press Connect again before the log has been read; never power-cycle the console to "fix" it.
- Stop (Disconnect) if the console's front panel, LCD or outputs do anything the owner did not
  cause. Record the time. Disconnect waits for an idle point (the current list is fetched to its
  end, a few seconds at most).
- Expected during E8, not a deviation: the channels, outputs and memories panels say "not
  reported" until the console lists those objects (it does so only on a change; a memory only
  when it is listed, which a read-only session may never see), and the masters stay "not
  reported" until both a FaderInfo and a LedStatus have arrived.
- A memory reported by the console in this session would be new (never observed outside
  SmartSoft's download, which follows an uncleared System write): note the time. The grid shows
  the delivered memory and dims the others as undelivered.
- Afterwards: copy the backend's JSONL and the USBPcap file into `docs/protocol/captures/` with
  an E8 record; update H66 / H67 / H68; add the second review to this file. *(Done 2026-10-30 for the
  JSONL and the record; no USBPcap ran, so H68 stays untested; H66 and H67 CONFIRMED for what was used.)*
- **E8b (2026-10-30, evening; [record](captures/2026-10-30-e8b-second-read-only-session.md)): no deviation from SC-1, nothing
  further cleared.** The second read-only session ran 1 121 s with the conditions E8 lacked: a named backup (`SHOW06.ASC`, saved
  before the power cycle and byte-identical to `SHOW05.ASC`), USBPcap from before the Zadig bind (item 8), a timed action log and
  the HARDWARE badge seen before Connect (owner, 20:37:55). Item 9 was met in part: the backend commit (c783223) is in the log header;
  the firmware was not re-read (the owner's Diagnostics reading of the same evening, 3.0.2.9.0.43, is the E8 answer), and the Windows
  build, the cable and Memory Integrity were not restated. Result against the checklist: 29 799 OUT transfers, all 12-byte polls
  (27 484) and listed fetches (2 315), **confirmed on the bus** (no System write, no data header, no control transfer of ours, nothing
  on interface 0); 0 framing errors, no fault, timeout or failure note, USBD status 0 in all 62 957 bulk URBs, every interval above the
  SC-1 floors (the floors themselves never reached), a stop at an idle point; H66 and H67 CONFIRMED in a second session and **H68
  CONFIRMED** (item 5: no control transfer reached the bus at the backend start, at Connect or at Disconnect). **SequenceMain (3) was
  listed by the console and fetched by our host for the first time** (at the two GO presses, seq 58654 and 59236): its id is in
  `HspReadSession.ObservedListedIds` (SmartSoft fetched it in the 157-record download and at E3b's GO crossfade), so the fetch is
  inside SC-1. H75 stayed untested (the largest list had 6 records; no read of a multiple of 64 bytes; no zero-length packet). Item 6's
  remark that BLACKOUT, IND 1 and IND 2 are carried by no observed object is superseded since E8 (F114, F115): the key identities are
  now OBSERVED with named, timed keys (E8b), which concerns what the UI may *display* (display-only decoding, the owner decisions
  above), never what may be sent. **E8b clears no message:** SC-1's clearance (poll and listed fetch, read-only) is unchanged and the
  E9 runbook stays the only path to a write mode. A third read-only session (E8c) is proposed in
  [reverse-engineering.md](reverse-engineering.md) ("Next capture (proposed by the E8b analysis, 2026-10-30)").

### Review

**First pass (2026-10-30, `protocol-safety-reviewer`): not ready, two blockers.** Findings and
what was changed the same day:

| Finding | Fix |
|---|---|
| **BLOCKER** The loop told a payload from a header by its length, so after a 12-byte payload (a one-record list, a ConsoleStatus object) the next fetch went out with no delay: 37 of 423 fetches in the E3 replay, 190 of 1 000 in E3b, outside the cleared use. | The payload stamp now comes from the session state, not the length; a virtual-clock test replays E2, E3 and E3b and asserts the three floors on every transfer. |
| **BLOCKER** Evidence tags missing or wrong in `ConsoleStateAssembler` and `UsbDeviceOptions` (BUMPS called VERIFIED, the recorded flag and "position not reported" unmarked, no class attributes, timing figures without status). | Tags corrected (BUMPS OBSERVED F95; recorded flag INFERRED from F107; position not reported INFERRED; F62 figures OBSERVED), `[Evidence]` on both classes, F62 annotated in known-facts with the poll_rules.py figures and their definitions. |
| MAJOR A type-4 reply to the poll after a fetch, and an empty list, were accepted. | Both fault; so do records with c ≠ 0, the list's own id and ids never seen in a list. |
| MAJOR Exceptions outside a narrow filter ended the session silently (status Connected, no reason). | Any exception ends in Error with its reason; payloads under 5 bytes and announced lengths above 1 024 fault cleanly; a session without a log does not start. |
| MAJOR Disconnect could stop between a fetch and its poll or with records unfetched; host shutdown never stopped the session. | Stops only at an idle point (bounded drain of 2 000 transfers, noted as a deviation); a hosted service stops the session on shutdown; Connect is refused while a session is alive; Disconnected is reported only once the session has ended. |
| MAJOR `Device:Usb:DevicePath` bypassed the VID / PID filter; shared open. | The pipe refuses any path without `vid_14d5&pid_0200&mi_01`; the handle is exclusive. |
| MAJOR Hardware mode could come from any configuration source and stay on; test hosts relied on statement order. | `Device:Usb:ConfirmDate` must equal today's date; every test host pins `Simulated`; the Usb test has two independent guards. |
| MAJOR The handle lacked FILE_FLAG_OVERLAPPED, which `WinUsb_Initialize` requires. | Added. |
| MAJOR Values the console never sent were shown as console state (masters before the first report; Blackout and the independents). | `mastersReported` in the state and the contract; the UI dims the masters and says "Not reported by the console yet"; BLACKOUT / IND keys marked unknown in hardware mode; SOLO's key says it is an LED reading. |
| MAJOR The knowledge base still said "nothing is cleared"; F62 said "not a transmit timing"; H66–H68, SC-1 and E8 absent from the matrices; `poll_rules.py` untracked. | Superseded sentences annotated in commands.md, the protocol README, known-facts (header and F62), the capability and evidence matrices; H68 added; `poll_rules.py` committed with a docstring. |
| MAJOR The WinUSB descriptor-read statement was unmarked; the EP0 string reads of every SmartSoft session were missing from the session-shape analysis. | Marked as an ASSUMPTION; H68 added; E8 records EP0. |
| MINOR Floors just below the observed minimums; no re-check after a delay; stamps before the write; Connect filter too narrow; Zadig label unverified; log writes between poll and reply; relative log folder; device path in the UI. | Floors 21 / 11 / 21 ms; the wait re-checks the clock; stamps after the write; any exception at open fails with its reason; Zadig by USB ID; log written after each exchange; log folder under `%LOCALAPPDATA%`; the UI gets "SmartFade 1248, read-only". |

**Second pass (2026-10-30, `protocol-safety-reviewer`): needs changes, one blocker.** Findings and
what was changed the same day:

| Finding | Fix |
|---|---|
| **BLOCKER** The golden outbound bytes in `HspReadSessionTests` were typed by hand and cited by pcap frame, never cross-checked against the committed capture. | The bytes are loaded from the E2 capture (JSONL seq 6, 9, 10, 14) and compared with the encoder's output; the literals stay beside them, cited. |
| MAJOR LedStatus before FaderInfo (the order of every E3b session) built a placeholder masters record and reported it as console state. | No masters change until FaderInfo; the LED reading of SOLO is remembered and travels with the first FaderInfo; `ConsoleStateAssemblerTests` replays that order from E3b. |
| MAJOR The channels, the outputs and the memories were placeholders shown as console state. | `channelsReported`, `outputsReported`, `memoriesReported` in the state and the contract; the channel, output and memory panels dim and say "not reported" (memories: "not delivered in this read-only session") until the console lists them; the runbook says what to expect. |
| MAJOR Statements saying "nothing is cleared", "decoders only" or "no code can transmit" were still unannotated (CLAUDE.md, README, architecture.md, packet-format.md, safety rule 2's note, `Evidence.cs`). | Each annotated as superseded by SC-1 for the two messages only; `Evidence.cs` reworded. |
| MINOR H67 said floors 20 / 20 / 10; H66's evidence cell contradicted F56 and F94; "six System writes" (ten); "every test host pinned to Simulated"; the 2 000-transfer drain described as reachable; the E8 row and runbook step 8 lacked H68; the LCD expectation had no log line; a 1 024-byte bound above the 792 observed and no body-length check; `IsIdle` equated with SmartSoft's session end; a late session end and a refused Connect unreported; file name and firmware never pushed to the UI and never cleared; device paths in error texts; the blackout blink in hardware mode; `status` = 0 claimed in the transfer log; ALLOW_PARTIAL_READS left on; the Zadig label in the locator; "nothing on the console changes" unmarked; a null log sink tolerated; Connected raised after the task started. | All changed: H66 and H67 reworded; the wording fixed here; H68 added to E8; the LCD is logged at Information; bound 792 and a per-id body-length table from F67 (checked in the replays); comments fixed; the late end and the refused Connect raise connection events; file name and firmware are pushed with the state and cleared on disconnect; error texts name no path; no blink in hardware mode; `status` is null with a header note; partial reads refused on the IN pipe; locator documented by USB ID; INFERRED marked; a missing sink stops the session (tested); Connected is raised before the first transfer. |
| MINOR, accepted and recorded: the fetch guard is per id, not per qualifier range (consistent with F57); only the poll stamp crosses sessions; a stalled pipe needs a replug; a reply queued after a timeout faults the next session at its first fetch; a list of exactly 37, 101 or 165 records (a multiple of 64 bytes) would end the session if the console then sent a zero-length packet. | Accepted for E8: each ends in the safe direction (no transfer, or a session end with its reason); to be revisited with the E8 capture. *(E8, 2026-10-30: the connect list had 89 records, so a 37- or 101-record list is reachable in practice; recorded as H75. No bulk transfer in E2, E3, E3b or E8 was a multiple of 64 bytes, and a 165-record list exceeds the 792-byte bound anyway.)* |

**Re-check of the second-pass changes (2026-10-30, `protocol-safety-reviewer`, over commits 8a08765 /
be66a4d / 5d4c5e5 and the frontend move 69b90ed): PASS.** Every second-pass finding was confirmed fixed
in the committed code and docs, the test suites pass with the expected counts, and the fixes opened no
route for an uncleared byte, weakened no gate and added no retry. Eleven MINOR leftovers; what was done
the same day, in commit bc93144:

| Finding | Fix |
|---|---|
| One poll literal in `HspReadSessionTests` was still typed by hand, and the golden filter checked only seq and direction. | Every golden byte comes from `E2Out(seq)`: a bulk OUT transfer on endpoint 0x04 of the E2 capture, not synthetic; the literal stays in a comment. |
| FaderInfo before LedStatus (E2's order, JSONL seq 16 then 20) reported SOLO = false inside a masters record marked as reported. | The masters are one reading from both objects: nothing is claimed until FaderInfo (MASTER, BUMPS) and LedStatus (SOLO) have both arrived, and an unchanged reading is no change. `ConsoleStateAssemblerTests` replays both orders (E2 and E3b). |
| Outputs held back for the 33 ms broadcast tick could reach the UI after the state reset of a connection change and set `outputsReported` while Disconnected or Error. | The broadcaster drops the held outputs on every connection change. |
| No test of the reported-flag transitions. | `ConsoleStateStoreTests`: each part sets only its own flag; the end of the connection clears all four, the file name and the firmware version. |
| The E2 section of known-facts.md still said that no `/hardware-safety-check` had passed. | Annotated with SC-1. |
| `hsp_summary.py` could not read the backend's transfer log (no pcap `frames`), which SC-1 item 8 and `TransferLog` claimed. | The loader and the `log` mode tolerate a missing pcap frame number; checked on a file in the log's shape and on E2 / E3b. |
| `ConfirmDate` was checked at start-up only, so a backend left running past midnight could still connect; item 6 overstated the gate. | `UsbSmartFadeDevice.ConnectAsync` re-checks the date against its clock and refuses with the exact restart command; item 6 reworded; tested on the virtual clock. |
| The pipe was opened before the log existed, so whatever the open puts on the bus (H68) could happen outside a logged session. | The log is created first; without a log the console is never opened. Tests assert that the pipe stays closed when the log fails and that the log exists before the open. |
| "(host-side policy, no bus traffic)" at the ALLOW_PARTIAL_READS call carried no status. | Marked ASSUMPTION, covered by H68's USBPcap at Connect. |
| Trivial: `WithoutDevicePath` kept the first backslash of a device path; `IHspPipe.Description` said "for the UI". | Both corrected. |
| **Accepted, recorded:** one `memoriesReported` flag covers 288 memories that arrive one object at a time; after the first Memory object the other 287 would show as empty at level 0, a value no object reports. A Memory object outside SmartSoft's download (which follows the uncleared System write `07 00 00`) was never observed in E2, E3 or E3b, and E8 has no memory action. | Not changed for E8 (the runbook says to note the time if it happens). To be fixed with a reported flag per memory before hardware mode is used beyond E8. **Fixed later the same day (commit 91e452f):** `MemoryState.Reported` per memory (false for a placeholder, true once the console delivered it, cleared with the connection), carried as `MemoryStateDto.reported`; the memory grid dims undelivered memories and says so; tests in `ConsoleStateStoreTests` and the memory-grid spec. |
