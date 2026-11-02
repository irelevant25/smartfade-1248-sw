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
| 6 | Mode gating | **Yes, built** | `Device:Mode=Usb` plus `Device:Usb:ConfirmDate=<today>` must both be given, or the backend refuses to start: a setting left in a shell, a launch profile or appsettings expires by itself ("this run only"). `Simulated` stays the default; any other value refuses to start; the UI badge shows HARDWARE in the alert colour; `supportedCommands` is empty, so every control is read-only and the backend refuses every command (`not_supported`); the masters are shown dimmed as "not reported" until FaderInfo arrives (an earlier LedStatus only keeps its SOLO reading for then), the channels, outputs and memories carry the same "not reported" state until the console lists them, and BLACKOUT / IND 1 / IND 2, which no observed object carries, are marked unknown. *(Superseded 2026-10-30 evening by E8 / E8b: BLACKOUT is read from LED block 53 (F114, VERIFIED pattern, identity OBSERVED), IND 1 / IND 2 from the Independents object (F115, identities OBSERVED by timed presses), and the key LEDs from their blocks (F116, F117, H72); the UI marks each as a reading, display only.)* "This day only": the confirmation is a date, read from any configuration source, checked at start-up and again at every Connect against the clock, so a backend left running past midnight refuses to connect until it is restarted with the new date (a session already running is not interrupted). Tests: `HostingTests` (every test host pins its mode: `Simulated`, or `Usb` with the pipe factory replaced and a device path the pipe refuses). |
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

### E9 (2026-10-30, late evening): the first write messages observed; nothing cleared

E9 ([record](captures/2026-10-30-e9-smartsoft-writes.md)) was a **SmartSoft** session recorded passively (our software sent
nothing): the first capture of a PC writing to the console. It observed, in one session, SmartSoft's ConsoleEvent faders (kind 0)
and keys (kind 1) and three ChannelText label writes, with their exact bytes ([commands.md](commands.md) "PC → console data writes
seen in E9"; F131–F138). **E9 clears no message:** SC-1's clearance (the poll and the fetch of a listed record, read-only) is
unchanged, and no type-1 data header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*. The record's table "Input for a future safety check
(SC-2)" is the starting point of a future **SC-2**, whose gate is: a second session that reproduces the writes (E9b); each message
class understood and shown harmless (the record functions and the object writes change show data and are not reversible by the
protocol, and a wrong bump already edited another memory in E9, F139); a whitelist of indices and codes (only four fader indices and
13 key codes were seen, none may be derived, H82); the press / release pairing and a session that dies between them (untested); and
`/hardware-safety-check` for each class, with `protocol-safety-reviewer` on the encoder. Whether the console accepts events from a host
that never sent SmartSoft's System pair (our read-only session sends neither, H66) can be learned only by sending, which nothing
clears.

**A known way a read-only session ends in the safe direction (F137, H83).** In E9 the console listed System (39) records with c ≠ 0
((5, 5, 6), (5, 5, 8), (5, 5, 9)) when the EDIT MEM key selected a memory. `HspReadSession` faults on any listed record with c ≠ 0
(first review pass, below), so a read-only session of ours that is running while a memory other than memory 1 (memories 7, 9
and 10 were seen) is selected for editing would end in the Error state with its reason, without any further transfer: the abort rule working as designed,
a stop and not a deviation. Whether the console's own EDIT MEM key lists these records is untested (H83, E9b; E9's records came from
key events sent by SmartSoft). The Memory objects the console listed after a recording (id 0, 138 bytes) are inside SC-1 as built
(id 0 is in `ObservedListedIds` and 138 in `ObservedBodyLengths`; the qualifier ranges are not enforced), so a read-only session could
receive a Memory object outside SmartSoft's download for the first time (note the time, as the E8 conditions say). Whether to accept
c ≠ 0 records is a code and review question for the coordinator and is not decided here.

### E9b (2026-10-31): the repeat of the writes; nothing cleared

E9b ([record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md)) repeated E9: three SmartSoft sessions recorded passively (our software sent
nothing), 427 data writes. **E9b clears no message:** SC-1's clearance (the poll and the fetch of a listed record, read-only) is unchanged, no
type-1 data header is encoded anywhere in the solution, and the safety check SC-2 has not been run. What E9b changes for SC-2's gate (the E9
section above): the item "a second session that reproduces the writes" is met for the framing and the most-used ConsoleEvents as SmartSoft's and
the console's behaviour (F131–F134 and F138 VERIFIED, F135 and F136 in part), which is necessary and not sufficient. The other items are not met:
each message class understood and shown harmless (E9b adds a Memory (0) whole-object write with a layout unlike the console's own and not
reversible by the protocol, F143, and the finding that a PC fader and a physical fader override each other without warning, F141); a whitelist
of indices and codes (five fader indices and 17 key codes were seen, none may be derived, H82); the press / release pairing and a session that
dies between them (untested); and `/hardware-safety-check` for each class, with `protocol-safety-reviewer` on the encoder. **The starting point
of SC-2 is the E9b record's section "Input for a future safety check (SC-2), updated after E9b"**, which supersedes the E9 record's table of the
same name (the E9 text stays). Whether the console accepts events and object writes from a host that never sent SmartSoft's System pair (H66) can
still be learned only by sending, which nothing clears.

**The c ≠ 0 safe stop, updated (F142, F137).** E9b answered half of the question left open above: the console's own EDIT MEM key lists System (39)
(5, 5, 0), c = 0, for memory 1, on page 1 and on page 12, which `HspReadSession` fetches (id 39 and body length 3 are inside SC-1 as built). Still
untested, and INFERRED from E9's c = slot − 1: the selection of any memory other than memory 1 lists c ≠ 0 and ends a read-only session with its
fault (a safe stop, not a deviation); one console-side selection of memory 2 or higher decides it (E9b record, next capture B). The Memory objects
the console lists after a recording made with its own keys (id 0, 138 bytes; byte 2 = 0 and zero levels after a deletion) are inside SC-1 as built
(F147).

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

## SC-2 (2026-10-31): PC → console writes, per message class

**Scope.** The five write classes SmartSoft was seen to send in E9 and E9b
([E9 record](captures/2026-10-30-e9-smartsoft-writes.md), [E9b record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md),
each with a section "Input for a future safety check (SC-2)"), judged for a **future write mode of
`SmartFade.Devices.Usb`** whose purpose is the owner's: the live controls and, later, recording through the
console's own REC MEM / EDIT MEM / COPY keys (owner decisions of 2026-10-30, `reverse-engineering.md`). Nothing
is encoded yet. SC-1's poll and listed fetch are the only messages cleared at the time of this check.

| Class | Message | Status in `known-facts.md` | Decision |
|---|---|---|---|
| A | ConsoleEvent (20) kind 0, fader event: header `01 00 08 00` + stale words, payload `[pseq] 04 00 14 00 [index] [value] [previous]` | F131 VERIFIED (framing, per-session counter, no acknowledgement); F132 VERIFIED for the layout, the raw level and the previous-byte rule, **for indices 0x00 (channel fader 1), 0x30 (MASTER), 0x31 (BUMPS)**; 0x06 (fader 7, E9 only) and 0x04 (fader 5, E9b only) OBSERVED once; every other index never seen ("index = fader − 1" HYPOTHESIS, three points); F134 VERIFIED (the console answers as for the panel) for fader 1, MASTER, BUMPS; F135 in part (takes effect at once); F141 (last mover wins) OBSERVED | **GO (conditional) for indices 0x00, 0x30, 0x31 only**: build and review an encoder, then a first write session **E11** on the owner's go. NO-GO for every other index |
| B1 | ConsoleEvent kind 1, key event, **live keys**: header `01 00 07 00` + stale words, payload `[pseq] 03 00 14 01 [code] [01 press / 00 release]` | F133 VERIFIED for the layout and for codes **0x00 bump 1, 0x06 bump 7, 0x08 bump 9, 0x31 GO, 0x33 SOLO, 0x34 BLACKOUT, 0x36 FADERS 1-24, 0x39 MEMS** (and 0x41 REC MEM, class B2); 0x09 bump 10, 0x38 CLEAR, 0x3E COPY, 0x42 EDIT MEM OBSERVED (E9 only); 0x37 FADERS 25-48, 0x3B STACK, 0x47 IND 1, 0x48 IND 2 OBSERVED once (E9b); F134 VERIFIED for bump 7, memory bump 1, BLACKOUT, SOLO, MEMS, 1-24, GO with STACK unlit | **GO (conditional) for the eight live codes above only**, as press / release pairs; NO-GO for every other code |
| B2 | ConsoleEvent kind 1, **record keys**: REC MEM 0x41, EDIT MEM 0x42, COPY 0x3E, CLEAR 0x38 | 0x41 VERIFIED as SmartSoft's behaviour; the other three OBSERVED once; effects F136 (in part), F145, F146: REC MEM + bump stores the post-MASTER look, REC MEM of an empty look empties the memory, EDIT MEM / COPY change show data; **not reversible by the protocol** (re-record, or restore from the SD card); E9 changed memory 7 by a mistaken bump (F139) | **NO-GO** now. Deferred to **SC-3**: it needs the owner's explicit written acceptance per key, a second capture for 0x42 / 0x3E / 0x38 (and for 0x09 if bump 10 is to be used), and the conditions under "Path to a wider clearance" |
| C | ChannelText (9) write: header `01 00 2B 00`, payload `[pseq] 27 00 09 [ch − 1] 03 06 [36 bytes]` | F138 VERIFIED (the write); F144 OBSERVED (stored verbatim, returned after a power cycle); show data; reversible only by writing the exact old 36 bytes, which only the download after the uncleared System `07 00 00` delivers | **NO-GO** (out of the owner's scope; not reversible from a read-only session) |
| D | Memory (0) whole-object write: header `01 00 5E 00`, payload `[pseq] 5A 00 00 [page − 1][slot − 1] 01 30 [48 levels] 03 06 [36 bytes]` | F143 OBSERVED once; layout unlike the console's own 138-byte body (96 levels, 0x60); channels 49–96 after such a write UNKNOWN; show data; not reversible | **NO-GO**, and outside the owner's write mode by decision (recording goes through the console's keys) |
| E | System (39) pair `09 00 00`, `07 00 00` | F58 VERIFIED as SmartSoft's behaviour only; meaning UNKNOWN (H30); System (39) is the carrier of the erase commands (F121, S11.15); starts the 157-record download; not needed by a session (H66 CONFIRMED for reads) | **NO-GO** |
| F | Show upload, System erase, page select, wheel, FileName, Patch, Independents, SystemSettings | Never observed from a PC except as listed in S11.15; destructive or out of scope (F121, owner decisions 2026-10-30) | **Never** |

### Decision: GO (conditional) for classes A and B1 as bounded above; NO-GO for B2, C, D, E; never for F

The GO is for **building and reviewing** an encoder limited to the three fader indices and the eight live key
codes, and for **one first write session, E11**, once every condition below is met and the owner gives the go
on the day. **Until then nothing may be sent**: this check clears no byte for sending today, and no type-1
header is encoded anywhere in the solution at the time of writing. E11 is an experiment like E8 was: two
things in it are unobserved (items 2 and 11 below) and the session ends on the first deviation.

| # | Item | Status (classes A and B1) | Evidence |
|---|---|---|---|
| 1 | Evidence status | **Yes for the bytes of the whitelisted indices and codes; the placement in the loop proceeds at INFERRED / OBSERVED with the owner's go** | F131 (427 of 427 writes in E9b follow the framing, the counter from 0x00 per session, +1 per data payload; no acknowledgement in 2 + 3 sessions), F132 (layout and the previous-byte rule in 332 + 366 fader events; indices 0x00, 0x30, 0x31 in both captures), F133 (116 key events; the eight codes in both captures), F134 (the console's answer equals the panel's for those controls, byte for byte for bump 7 and memory bump 1), F135 in part (seq 8051, 13153, 16315, 39747). "One write per loop iteration, between a poll reply and the next poll" is INFERRED from the captures, like SC-1's cadence was. Everything OBSERVED once (0x04, 0x06, 0x09, 0x37, 0x38, 0x3B, 0x3E, 0x42, 0x47, 0x48) is outside the whitelist |
| 2 | Side effects understood | **Yes for the messages; the host context is new** | A fader event sets the control at once to the value (F135, F141: fader 1 0x00 → 0xEC at seq 39747, MASTER 0x86 → 0xF3 at seq 82071); the outputs follow through the patch and the MASTER (F96), BUMPS sets the bump level (F97). The eight live keys toggle or switch exactly as the panel's keys do (F134); BLACKOUT zeroes every intensity (F125); GO with STACK lit was seen only from the panel (E8b), from a PC event only with STACK unlit. **Unobserved:** whether the console accepts events from a host that never sent SmartSoft's System pair (H66 for writes) can only be learned by sending; the first event of E11 answers it, and a console that ignores it ends the session without a retry. A fader event inside an armed record function edits a memory (E9): guarded by condition 6 |
| 3 | Backup | **Condition** | The owner saves the show to the SD card right before E11 and the guided script records the name (SHOW10.ASC or the next free name). |
| 4 | Rig | **Condition** | The DOTZ BAR 1.4 on DMX 5–16 (owner, 2026-10-30), nothing on DMX IN, no dimmers; the owner's live-output decision for the MASTER and BLACKOUT events (both change the whole output, as the panel's controls do). |
| 5 | Firmware path untouched | **Yes** | Bulk transfers on interface 1 only, as in SC-1; no control transfer, no HID, no SFDownloader, no "Firmware Upgrade"; the Zadig bind is the owner's action (H68 answered in E8b: EP0 traffic only at the bind). |
| 6 | Mode gating | **Condition (to build)** | A **separate write mode** on top of the read-only one: `Device:Mode=Usb`, `Device:Usb:ConfirmDate=<today>` **and** a new write switch with its own date (`Device:Usb:LiveControl=<today>` or equivalent), all three or the backend refuses to start; `Simulated` stays the default; any other value refuses to start; the UI badge reads HARDWARE and a second badge in the alert colour says LIVE CONTROL; `supportedCommands` names only the whitelisted controls and the contract gains a per-control list so the UI enables fader 1, MASTER, BUMPS, bumps 1 / 7 / 9, GO, SOLO, BLACKOUT, MEMS and FADERS 1-24 and nothing else. The read-only mode stays exactly as reviewed for SC-1. |
| 7 | No blind retries | **Condition (to build)** | A write is sent once. A transfer failure, a timeout or a reply the session has not seen ends the session in the Error state with its reason; no write is ever re-sent; the UI shows the fault and the console's last reported state. A key press whose release could not be sent is reported as such (condition 5 below). |
| 8 | Logging on | **Yes, built; plus a condition** | Every OUT transfer, writes included, goes to the session's capture-format JSONL as today (`Device:Usb:CaptureLogDirectory`), written before the console is opened; E11 also runs USBPcap in parallel, bound before Zadig as in E8b, and its console-only split is committed with the record. |
| 9 | Versions recorded | **Condition** | Firmware (Menu > Diagnostics), Windows build, cable, backend version and git commit in the E11 record. |
| 10 | Review done | **Condition** | `protocol-safety-reviewer` over the exact encoder, the whitelist, the gating and the golden tests before E11; its findings fixed or recorded here, as for SC-1. |
| 11 | Passive first | **Yes** | E9 and E9b (2026-10-30 and 2026-10-31) show SmartSoft producing every whitelisted event and the console answering it; our software has never produced one. |

### Conditions for building the encoder (classes A and B1)

1. **One encoder, one whitelist.** The type-1 header and the two payload layouts are produced by `HspOutbound`
   only, from typed inputs: a fader index drawn from an enum of exactly {0x00 channel fader 1, 0x30 MASTER,
   0x31 BUMPS} and a key code from an enum of exactly {0x00, 0x06, 0x08, 0x31, 0x33, 0x34, 0x36, 0x39}. No
   arithmetic derives an index or a code from a fader or key number (H82 and "index = fader − 1" stay
   hypotheses and are not used); every other value is unrepresentable, and the boundary refuses it.
2. **Golden tests over the committed captures.** As `HspReadSessionTests` does for SC-1, the E9 and E9b replays
   hand the encoder SmartSoft's event (index or code, value, press / release) at each write and compare the
   produced transfer byte for byte with the capture: header, stale words, `pseq` (0x00 at the first data payload
   of a session, +1 per data payload), the previous byte (0x00 for the first event of an index in the session,
   then the last value this host sent for that index), and the placement (between a poll reply and the next
   poll, never between a fetch and its poll, at most one write per loop iteration). Only bytes from
   `docs/protocol/captures/` may appear in these tests.
3. **The previous byte is this host's own last value**, never a value read from the console (F132: SmartSoft's
   rule; the console's use of the byte is UNKNOWN and nothing else was observed).
4. **Press and release are one operation.** A key event is sent as a press followed by its release from the same
   code path; a bump is a tap in E11 (no held bump). The release is the device layer's duty, not the UI's: it is
   sent even if the UI stops, and before the pipe closes on a session end when the pipe is alive. Residual risk,
   stated for the owner: a process killed between a press and its release leaves the console with a held key
   until the key is pressed on the panel (INFERRED, untested; the E9b record's SC-2 input lists it as untested).
5. **No write while a record function is armed or unknown.** A write is sent only when the console's most recent
   PanelKeys report shows REC MEM, EDIT MEM and COPY off (LED blocks 66, 67, 63 as decoded today) and that
   report is younger than two poll periods; otherwise the write is refused and the UI says why. Residual race,
   stated: a panel EDIT MEM plus a bump within one poll period of a PC fader event could edit a memory (E9's
   mistaken edit was this path, from the PC side).
6. **The UI shows the console's reported level, never an assumed position** (F141: last mover wins without
   notice; H89: SmartSoft's own fader jumped the output because its control started from its own position).
   A PC fader starts from the reported level before it may move, so the first event cannot jump the output
   by more than the user's own drag.
   *(Annotated 2026-11-02 after SC-5's first review pass, MAJOR 1: while a bump of that physical fader is held, the reported level
   is the bump's level (F174 VERIFIED: FaderInfo record k byte 0 and ChannelLevels[c] read 0xFF from the press to the release), so a
   drag started then would carry the bump's level, not the content's; SC-5's device holds a fader's events across its own bump hold
   until both objects were reported after the release, and a bump held on the panel meanwhile is the owner's accepted residual risk
   at the E11b gate; see SC-5 condition 1 (d) and its review table. The same holds during a BLACKOUT, when every fader reports 0x00
   with the MASTER at full (F125, OBSERVED once; F114 VERIFIED for the LED): SC-5's device refuses fader events while LED block 53 is
   not steady off, re-check 2026-11-02.)*
7. **Cadence as observed.** Writes are placed where SmartSoft placed them and the SC-1 floors hold for every
   transfer; the read loop keeps fetching what the console lists after an event (FaderInfo, LedStatus,
   ConsoleStatus, PlaybackInfo: F134), so the UI shows the effect from the console, not from the command.
8. **Nothing else.** No System pair, no ChannelText, no Memory, no record key, no index or code outside the
   whitelist, no write of any kind in the read-only mode, no write before the session has seen the console's
   first list and the LED state.

### Conditions for E11 (the first write session) and what to record

- Backup to the SD card first, the name in the log; USBPcap from before the Zadig bind; the guided script
  logs every owner action with the time; firmware, Windows build, cable, backend commit recorded.
- The owner gives the go on the day after reading this section, and accepts in writing: H66 for writes is
  unknown (the console may ignore the events: the session then ends and nothing is retried); the last-mover
  behaviour (the console's state and a physical fader can disagree until someone moves it); the lost-release
  residual risk (condition 4); MASTER and BLACKOUT events change the whole live output like the panel does.
- Script, read-only first: connect, idle 60 s (the read side as in E8). Then, one at a time, with the console's
  answer watched in the UI and 10 s between steps: channel fader 1 to a low level and back to zero (H66
  answered here: no FaderInfo change within two poll periods means stop); bump 1 tap; SOLO on and off; MEMS
  and FADERS 1-24; BLACKOUT on and off; BUMPS to a mid level and back to full; MASTER to a mid level and
  back to full (the owner's choice to include it); GO with STACK unlit once; disconnect at an idle point.
- Record: every write with its `pseq` and the console's answer (FaderInfo record 1, body[193], body[194], the
  LED blocks, ConsoleStatus, PlaybackInfo), the bus against the backend's log byte for byte (as E8b), any
  deviation and the time it ended the session, and the physical fader positions at the end. Afterwards: an
  SD save, compared with the backup byte for byte (no show data may have changed).
- **No SD save (and no SD load) on the console while our software is connected** (added 2026-11-01 from E12b, F163 / H109; no decision
  changed): during E12b's SD save the console answered no USB transfer for about 0.85 s, and our reply timeout (500 ms, at most that with
  live control on) would end the session with a fault, a safe stop; the script's order (Disconnect, then the SD save) stays, and the
  script forbids a save or load while connected (commit 7789a71). The same holds for a read-only session.
- **Guided script (written 2026-10-31, evening, parse-checked; not run):** `tools/usb-probe/e11-guided-session.ps1` takes the owner
  through the above in that order and refuses to start the backend unless the re-check's open items are closed and every
  acceptance is answered y; it follows the "E11 script adjustments" below (bump 7 instead of bump 1, MASTER last, SOLO and BLACKOUT
  from a steady LED, memory bump 1 optional in memory mode between FADERS MEMS and FADERS 1-24 from the UI, GO optional and last).
- **The console's state reported before the first control (added 2026-11-01 after attempt 1):** the interlocks refuse every control until
  the console has reported its key LEDs, its fader mode and the MASTER (conditions 5, 6, 8), and a console with nothing pending reports
  nothing to a host without the System pair (F166). So the operator makes it report from the PANEL after the idle minute (the guide's
  step W0: FADERS MEMS, FADERS 1-24, SOLO on and off, the MASTER down and back to full) and checks the UI shows the LEDs, the MASTER and
  channel mode before the first control; a refused control is "not sent" (repeat W0 once, then the control once; a second refusal ends
  the attempt), only a sent event the console does not follow is the H66 stop.
- **Attempt 1 (2026-11-01, backend 5416d7f, guide 7789a71): no write sent.** 5 183 polls in two sessions, 0 lists from the console, the
  first control refused by condition 8 as designed, the guide read the refusal as the H66 stop, Disconnect at an idle point, SHOW18 =
  SHOW17; the bus equals the backend's logs byte for byte. Record `captures/2026-11-01-e11-first-live-control-session-attempt-1.md`. **E11 is not done: its conditions are
  unchanged, nothing is promoted, nothing is cleared.** Attempt 2 runs the corrected guide on the owner's go.
- **Attempt 2 (2026-11-01, afternoon, backend and guide 85aa2f5): four class-A writes sent, the console followed; W1 only.** After the W0
  panel actions (23 lists), channel fader 1 dragged in the UI sent four ConsoleEvent fader events (index 0x00, values 0x05, 0x17, 0x2F,
  0x38, counter 0..3, F132's chain), each after an idle reply with nothing outstanding, the reply 43.7–49.4 ms old at the header (MAJOR 1's
  E11 analysis: at most 62 ms, held for 4 of 4), the next poll 15.4–30.3 ms after the payload, the next write 357–417 ms later; the
  interlock held newer values eight times (a listed object unfetched, a report older than two poll periods) and released each within one
  poll exchange; the console listed FaderInfo, ChannelLevels, OutputLevels and LedStatus at the next poll with the sent value in each and
  acknowledged nothing (F167; H66 CONFIRMED in part for writes); 0 framing errors, 0 USBD errors, the bus = the backend's log (15 351
  transfers); no end-of-session message, no marker; `SHOW20.ASC` = `SHOW19.ASC` byte for byte (no show data changed). The session ended on the
  guide's W1 question: the owner typed the case letter "b" (followed) and the guide, accepting only y / yes / followed, took the H66
  stop; the guide now accepts a / b / c and confirms before a stop. Record
  `captures/2026-11-01-e11-first-live-control-session-attempt-2.md`. **E11 is partly done (W1 up only): attempt 3 covers W1's return to zero and
  W2–W8; the conditions and decisions above are unchanged; nothing further is cleared.**
- **Attempt 3 (2026-11-01, evening, backend and guide 5485ea3): W1–W8 run to the end, 116 writes, the console followed every control; E11 done.**
  After the W0 panel actions (24 lists) the guided session sent 92 class-A fader events (channel fader 1 up to 0x34 and back to 0x00, BUMPS 0xFF → 0x7E → 0xFF,
  MASTER 0xFF → 0x7F → 0xFF) and 12 class-B1 press / release pairs (bump 7, SOLO on / off, BLACKOUT on / off, FADERS MEMS, memory bump 1 × 3, FADERS 1-24,
  GO × 2 with STACK unlit), each 27.1–45.9 ms after a poll's reply (MAJOR 1's bound of 62 ms held 116 of 116), the next poll or fetch 15.0–31.1 ms after the
  payload, the press and its release 46.6–76.7 ms apart with no write between, the interlock holding newer values 78 times and releasing each within one poll
  exchange; the console answered every control at the next poll as it answers the panel and SmartSoft (F167 VERIFIED in part, F168, F169; H66 CONFIRMED for
  writes, OBSERVED once each) and acknowledged nothing; 0 framing errors, 0 USBD errors, the bus = the backend's log (49 426 transfers); no end-of-session
  message, no marker; `SHOW22.ASC` = `SHOW21.ASC` byte for byte. A bump tap from the PC (47–77 ms) left no trace in the fetched objects (the console listed them
  for the press and the release); a held bump from the PC stays unsent (condition 4). **Deviation, recorded:** after the guide's END and the SD save the owner
  started the backend twice more by hand (23:04 read-only, 3 s; 23:07 live control, 242 s, 57 writes of the same classes behind the same interlocks: memory
  bump 1 taps in memory mode, channel fader 1 drags, bump 7 taps, the panel's mode keys between), **without USBPcap and without an SD pair**: evidence of the
  backend's log alone, no show data changeable by those classes, and `SHOW23.ASC`, saved by the owner afterwards and copied 2026-11-02, equals `SHOW22.ASC` byte for byte; the guide now ends with a reminder
  that a later backend run needs USBPcap and an SD pair. Record `captures/2026-11-01-e11-first-live-control-session-attempt-3.md`. **E11 is done: the
  conditions and decisions above are unchanged; nothing further is cleared** (every index and code outside the whitelist, a held bump, a fader event in memory
  mode or in bank 25-48, the record keys, ChannelText, Memory, the System pair and class F stay as decided).

### Review (SC-2 item 10)

**First pass (2026-10-31, `protocol-safety-reviewer`, over the owner's uncommitted implementation): NEEDS CHANGES, one
blocker and six majors; the bytes, the whitelist, the golden tests and the read-only mode passed** (build 0 warnings,
198 tests green at the time). Findings and what was done:

| # | Finding | Status |
|---|---|---|
| BLOCKER | Protocol assumptions in the write path carried no status word and the device class no `[Evidence]` tag (toggle per click, the freshness reading of condition 5, F96 used as fact, capture counts without status). | Fixed the same day, tags and status words only: toggle per click VERIFIED (F133 / F134), the freshness reading INFERRED from F57 and F116, F96 OBSERVED, the counts OBSERVED (`write_rules.py`), four `[Evidence]` attributes on the device. |
| MAJOR 1 | Condition 5's "report younger than two poll periods" is checked when the write is chosen, not when it is sent: after the slot wait the report can be about 75 ms old with the default settings, unbounded with larger ones. | **Fixed 2026-10-31, waits for the re-check:** a write is taken only from a slot whose timing is already met. The loop asks `HspReadSession.PeekNextKind`, waits for that kind's slot, and only then lets the session choose (`Next(writeSlotMet)`), so the gate and the write's own condition answer right before the header, with nothing waiting in between; a stop that comes during the wait is handled before anything is chosen; a release is still never held back (condition 4). Tests on the virtual clock: the MASTER write's slot wait stretched by three poll periods is held and a poll goes in that slot, the write following the fresh report; the same stretch before a release does not stop it; the floors test checks the report's age at every header but a release's. Consequence for E11, by the freshness reading: the poll after a fetch is answered by the object, not by a report, so a write never goes in the slot after the last object of a download; it waits for the next poll reply. **Re-check 2026-10-31, evening: RESOLVED** (evidence by file and line in the second pass below). |
| MAJOR 2 | The interlock fails open: a LedStatus, ConsoleStatus or FaderInfo object that does not decode is logged and skipped, the old reading stays and counts as current. | **Fixed 2026-10-31, waits for the re-check:** in a live session such an object ends the session (`HspSessionFaultException`, item 7; an owed release still goes if no exchange is open, as after any fault); the read-only session logs it and goes on, as SC-1 reviewed it. Tests with E9b's LedStatus (seq 11636) and FaderInfo (seq 11628) bodies whose count byte was lowered on purpose (a shape no capture shows); ConsoleStatus cannot fail that way (any 7 bytes decode, and a mode or bank value never observed already reads as unknown). **Re-check 2026-10-31, evening: RESOLVED** (evidence by file and line in the second pass below). |
| MAJOR 3 | Condition 6 not met for channel fader 1: the thumb and the drag started from the UI's remembered value, not from the reported level (the H89 / F141 jump). | Fixed the same day in the UI: on a real console a writable fader's thumb sits at the reported level while nothing is pending and a drag starts there (`channel-grid.component.spec.ts`). |
| MAJOR 4 | Item 6's per-control list was not connected: the device named no `WritableControls`, so the UI enabled every channel fader and bump while the device sends three faders and a few keys. | Fixed the same day: `UsbSmartFadeDevice.WritableControls` names exactly channel fader 1, MASTER, BUMPS, bump 7, memory bump 1 of each page, SOLO and BLACKOUT (device, hosting and UI tests). |
| MAJOR 5 | Docs not updated in the same change: "no type-1 header is encoded anywhere" and "not implemented" statements, the switch and the run line nowhere. | Fixed the same day: every such statement annotated as superseded, the switch and the run line in CLAUDE.md, this section. |
| MAJOR 6 | Condition 5 and the end-of-session paths had no tests (a write refused or dropped while a record key is lit, the holds, the discard after an LED report, expiry inside a session, the release after a fault, an in-flight release, the incomplete write, a SOLO / BLACKOUT tap that goes out); the golden replays use a gate that always says Send, although the captures can prove the interlock (the 16 whitelisted SmartSoft writes made inside a record function must read armed: E9 payload seq 67098, 68338, 74000, 77851, 83191, 91068 and their headers; E9b 79059, 84069; the other 589 read off). | **Fixed 2026-10-31, waits for the re-check.** `LiveInterlockGoldenTests` replays E9 (seq … 68338, 68367 … 77851, 77881 … 91068, each range ending at the poll whose reply lists System with c ≠ 0, F137) and E9b (S1, S3) through a session and the device's `ConsoleStateAssembler`, and asks `UsbSmartFadeDevice.Interlock` (what the gate applies after its session-end and expiry checks) at each of the 605 whitelisted writes. It drops exactly the 16 inside a record function, which are 8 press / release pairs (E9 payload seq 67092 / 67098, 68334 / 68338, 77843 / 77851, 91062 / 91068 EDIT MEM; 73996 / 74000 REC MEM; 83185 / 83191 COPY; E9b 79053 / 79059, 84063 / 84069 REC MEM), and none of the other 589. Of those, 436 go and 153 are held, every one because the console had listed a LedStatus that was not fetched yet: SmartSoft wrote before its fetches, the gate does not (F57, F116). The report's age is not judged there (the captures carry SmartSoft's timing, not ours). Device tests on a stand-in console with E9b's LED bodies: refused while REC MEM is lit (seq 83953) and a write queued just before it lit discarded unsent; held while LedStatus is listed and not fetched; live control expiring at midnight inside a session; the release after a fault with no exchange open (sent) and with one open (reported); the incomplete write (header and payload); a release whose payload fails; a SOLO tap that goes out and the next toggle waiting for its LED. **Re-check 2026-10-31, evening: RESOLVED** (evidence by file and line in the second pass below). |
| MINOR | A failed Data header write is assumed not to have reached the console (unmarked); the incomplete-write marker lives in memory only; the write timings have no upper bound; `onSend` runs before the write is committed; a chosen write still goes out if a stop arrives during the slot wait; the replays prove "where SmartSoft wrote", not "nowhere else". | **Fixed 2026-10-31, waits for the re-check.** (1) A header whose own transfer fails is an incomplete write too: whether it reached the console is UNKNOWN, so the worse case is assumed. (2) The latch is also written to `INCOMPLETE-WRITE.txt` beside the transfer logs (`IncompleteWriteMarker`), and Connect refuses while it exists, also after a restart; only the owner removes it, after reading the log. (3) With live control on, each of the five cadence settings (poll interval, poll after fetch, fetch delay, poll after write, write interval) must be at or below its default (SmartSoft's median; for the write interval the observed write-to-write minimum rounded up, 46 ms); otherwise the backend refuses to start and the device stays read-only. The reply timeout is bounded too, at most 500 ms, its default (second pass, MINOR C). A write then goes about 31 ms after its poll on a report at most 62 ms old, and a tap lasts about 46 ms. (4) A write's `sent` callback runs only when the session commits it, and conditions change nothing. (5) A stop during the slot wait is handled before anything is chosen. (6) A replay of E9 (up to seq 68339) and of E9b's first session (up to seq 64681) with a write always waiting shows the session writing right after each completed poll exchange and nowhere else. (7) Found while fixing these: a press whose own transfer failed was reported as "pressed, press it on the panel", although whether the console took it is UNKNOWN and a panel press would toggle SOLO / BLACKOUT or fire GO again; it is now reported as a press that did not complete, with that warning. The incomplete-write latch, in memory as before and now on disk, refuses every Connect, read-only ones too: the console may still wait for the payload. A stop during a poll's or fetch's wait still sends that transfer, as SC-1 reviewed it. **Re-check 2026-10-31, evening: RESOLVED** (evidence by file and line in the second pass below). |
| MINOR | The HARDWARE hint said read-only beside the LIVE CONTROL badge; the command lists were read once at start-up. | Fixed the same day: the hint, and the lists are refreshed on every connection change. |
| MINOR | `ClearedKey`'s attribute cited F134 for bump 9; `HspOutboundPayload`'s lacked F131; no hosting test of the switch. | Fixed the same day. |

**Two points for the owner's E11 acceptance, from the review:** our first ConsoleEvent carries the counter 0x00, whereas
every SmartSoft session's first ConsoleEvent carried 0x02 or more (its System pair took 0x00 and 0x01); whether the console
checks the counter is UNKNOWN. And after a session fault the code still sends an owed key release when no exchange is open
(condition 4 as written), a transfer into a console state the session has just judged unobserved. *(Second pass, 2026-10-31,
evening: the mechanism is unchanged, but it now also covers major 2's fault, a release sent to a console whose LED, status or
FaderInfo report has just failed to decode, and it no longer covers a press whose own transfer failed, which gets no release
and no "press it on the panel" advice; the owner's written acceptance of this point should name the decode-failure case.)*

**Second pass (re-check, 2026-10-31, evening, `protocol-safety-reviewer`, over commit bf1aa4e): PASS. No blocker or major is left;
three minors and three nits remain, to be fixed or accepted by the owner in this table. The re-check clears nothing for sending:
E11 still needs SC-2's own conditions and the owner's go on the day.** How it was checked: every touched file read in full and the
diff fe7b91b..bf1aa4e; `dotnet test SmartFade.sln` once (229 passed); the reviewer's own Python replay of the raw E9 and E9b JSONL
(`armed_check.py`, `holds_check.py`, session scratchpad, not committed) found the same 16 armed writes as the golden interlock test
(E9 67092 / 67098 and 68334 / 68338 EDIT MEM, 73996 / 74000 REC MEM, 77843 / 77851 EDIT MEM, 83185 / 83191 COPY, 91062 / 91068
EDIT MEM; E9b 79053 / 79059 and 84063 / 84069 REC MEM), 153 holds for a listed LedStatus (57 + 96), 436 sends (229 + 207) and no
ConsoleStatus listed without LedStatus. The first pass's "the 16 … and their headers" was imprecise: the 16 are 8 press / release
pairs. Findings of the second pass:

| # | Finding | Verdict | Evidence |
|---|---|---|---|
| MAJOR 1 | Freshness checked at the choice, not at the header. | **RESOLVED** | `HspReadLoop` asks the next kind, waits for that kind's slot, handles a stop, then lets the session choose with `Next(writeSlotMet)`; nothing waits between the choice and the header (no await, I/O or log note); the gate reads the report's age inside `TakeWrite`; a write queued during a poll's or fetch's wait cannot use that slot (`Next(false)` closes it without `TakeWrite`); a stop during a write's wait chooses nothing new and an owed release still goes (the release branch is not gated). Virtual-clock tests: the report's age at every header but a release's; the MASTER write's slot stretched by three poll periods is held, a poll goes, the write follows a reply at most 62 ms old; the same stretch does not stop a release; peeking asks nothing. **Worst case with the defaults:** the gate allows 62 ms; the slot opens at least 31 ms after the poll whose reply (within 12 ms in every capture) is the report, so a write normally sees a report 19–31 ms old; a late Windows timer can only cause a hold; from the gate to the header only CPU work remains (interlock, `Recheck`, commit, encoder, `ToggleSent`), normally microseconds, stretched only by a preemption or GC pause landing inside it. **E11 analysis:** for every write that is not a release, compute the time from the poll reply header to the write header in the backend's log and confirm it is at most 62 ms. |
| MAJOR 2 | The interlock failed open on a decode failure. | **RESOLVED** | `InterlockObjects` (LedStatus, ConsoleStatus, FaderInfo); in a live session a failed decode throws `HspSessionFaultException`, caught by the loop, the owed release handled as after any fault (the payload was read whole, so `TakeReleaseAtEnd` gives it back); the read-only session logs and skips, as SC-1 reviewed it (test). The ConsoleStatus claim holds: `Decode` accepts any non-empty body, the session accepts 7-byte bodies only, a mode or bank byte above 1 makes `FaderMode` null and null refuses or drops every control. No partial update can leave a stale reading: `LedStatusObject.Decode` and `FaderInfoObject.Decode` require the count to match the body length (451 → 75, 195 → 48), so the assembler's early returns cannot happen in a session. The test bodies are E9b seq 11636 (LedStatus) and 11628 (FaderInfo) with the count lowered by one, labelled as a shape no capture shows. |
| MAJOR 6 | Missing tests. | **RESOLVED**, one gap (MINOR A) | `LiveInterlockGoldenTests` reads only the two committed JSONL files, states and justifies `reportFresh: true`, and a second test checks that every one of the 605 whitelisted writes lies in a replayed range; the 16 drops match (confirmed independently, above). Every device test the first row names exists and passes: REC MEM lit (seq 83953), the hold while LedStatus is listed, expiry at midnight, the release after a fault with and without an open exchange, the incomplete header and payload with the marker and a restart, a failed release header or payload, the SOLO tap and the next toggle waiting. |
| MINOR (1)–(7) | Header failure; marker on disk; timing bound; `sent` at commit; stop during the wait; "nowhere else"; the press in flight. | **RESOLVED** (5 in code, untested: MINOR B; 6 for the ranges replayed: NIT E) | A failed header is UNKNOWN to have reached the console and counts as incomplete (test); `IncompleteWriteMarker` beside the transfer logs, Connect refused while it exists, also for read-only sessions, also after a restart (test with the same process, a restart and the file removed); the timings checked at start-up and at every check of the switch (test); `sent` runs after the commit and never for a hold or a drop (test); a stop during a write's wait chooses nothing new, and `DisconnectAsync` clears the live session before it cancels, so the gate would drop a new write anyway; the "nowhere else" replay; a press whose own transfer failed is reported as not completed, never "press it on the panel" (tests). |
| **MINOR A (new)** | Two holds have no test that would fail if they were removed: the ConsoleStatus-listed hold of the gate and the FaderInfo-listed hold of channel fader 1 (`Recheck`). No capture replay reaches the ConsoleStatus branch (never listed without LedStatus in the 605 writes); the floors test lists FaderInfo after each write but checks for no hold. One-line calls to the function the LedStatus test covers, hence MINOR. | **Fixed 2026-10-31, evening (Claude, tests only; the owner's code unchanged):** `A_write_waits_while_the_console_has_listed_its_fader_mode_it_has_not_delivered_and_goes_after_it` and `Channel_fader_1_waits_while_the_console_has_listed_FaderInfo_it_has_not_delivered_and_goes_after_it` in `UsbSmartFadeDeviceTests`, each with E9b's body (seq 796 / 11628) listed again while the write waits, the hold note asserted and the header after the fetch. | Two tests modelled on the LedStatus-listed hold: one reports ConsoleStatus (E9b body seq 796, header 795) before `SetMasterAsync`, the other FaderInfo (E9b body seq 11628) before `SetChannelLevelAsync(1, …)`; each asserts the hold note and that the header comes after the fetch. |
| **MINOR B (new)** | Two fixed paths have no test: a stop during a write's slot wait (`HspReadLoop`), and the refusal when the marker exists but cannot be read (`IncompleteWriteRefusal`). | **Fixed 2026-10-31, evening (Claude, tests only):** `A_stop_during_a_writes_slot_wait_chooses_no_new_write_and_drops_the_queued_one` and `A_stop_during_a_releases_slot_wait_still_sends_the_release` (a delay hook that calls Disconnect inside the write's or the release's slot wait; no Data header for the write, the release's payload byte for byte), and `An_incomplete_write_marker_that_cannot_be_read_refuses_to_connect_until_it_can` (the marker held open with `FileShare.None`, then readable, then removed). | A loop-level test that cancels from the delay hook while `PeekNextKind() == Data`, asserting no Data header goes out and an owed release still does; a device test with the marker file held open with `FileShare.None`. |
| **MINOR C (new, docs)** | Item (3) above and the CLAUDE.md run line say "every timing setting" must be at or below its default, but `ReplyTimeoutMilliseconds` (default 500 ms, allowed 50–10 000) is not checked by `LiveControlTimingProblem`. It does not age the report (stamped when the reply arrives) but bounds how long a tap's press can stay unreleased if the console is slow to answer the poll between press and release (never observed: within 12 ms in every capture). | **Fixed 2026-10-31, evening:** the docs name the five cadence settings, and `UsbDeviceOptions.LiveControlTimingProblem` now bounds the reply timeout too (at most 500 ms, its default, `Defaults.ReplyTimeout`; asserted in `Live_control_needs_timings_no_slower_than_SmartSofts`). Options validation only, no transmit code touched. | Either add it to `LiveControlTimingProblem` (at most 500) or reword to "the five cadence settings". |
| NIT D (new) | `UsbDeviceOptions` and item (3) call the 46 ms `WriteInterval` default "SmartSoft's median"; `write_rules.py` gives a write-to-write minimum of 44.77 / 45.55 ms and a median of 61.74 / 62.14 ms (E9 / E9b): 46 is the observed minimum rounded up, right as a floor. | **Fixed 2026-10-31, evening** (`UsbDeviceOptions.Defaults` comment and item (3) above: the observed minimum rounded up, OBSERVED). | "the observed minimum (OBSERVED)". |
| NIT E (new) | Item (6) says "a replay of E9 and E9b"; the test replays E9 up to seq 68339 and E9b's first session only. | **Fixed 2026-10-31, evening** (item (6) above names the ranges). | Name the ranges. |
| NIT F (new) | A comment in `HspReadLoop` says "21 ms after the payload"; 21 ms is the `FetchDelay` floor, the default is 28 ms. | **Fixed 2026-10-31, evening** (the comment now says 28 ms, the default, with 21 ms as the floor). | Reword. |

Checked and found in order by the second pass: no new byte sequence in `src/` (the press check in `WriteData` is F133's layout and cites
it); the stand-in console's bodies come from committed E9b frames (seqs 11636, 22172, 83953 LedStatus; 11628 FaderInfo; 795 / 796
ConsoleStatus; outbound 13143, 13144, 8038) and its framing from F53, F54 and F57 as the first pass accepted; every deliberate deviation
is labelled as a shape no capture shows; the new assumptions carry status words (UNKNOWN: whether a failed transfer reached the console
and what the console does with a header whose payload never came; INFERRED: the freshness reading, with its `[Evidence]` attribute;
OBSERVED: the timing figures, matching `write_rules.py`: poll-to-write 29.81 ms minimum and 47.99 ms maximum, press-to-release
45.63 / 45.88 ms minimum, so the 46 ms tap is never faster than SmartSoft's); no retries (the owed release is sent once and a failure is
reported); the firmware path untouched; locks always `_gate` then `_writeLock`, the session never calls the device under `_writeLock`;
nothing between the commit and the header throws in practice; **SC-1's read-only session is unchanged in behaviour** (`PeekNextKind`
never says Data without a gate, so the loop waits for the same kind with the same floors; the E2 and E3b replays and the cadence test
pass), and stricter in one respect: the incomplete-write latch and marker now also refuse read-only Connects. **A note for E11:** the
marker lives in `Device:Usb:CaptureLogDirectory`; changing that setting between sessions lifts the latch, so it must stay the same
through E11. The docs (CLAUDE.md, this file) describe the code as it is, apart from MINOR C and the nits.

**What stands between this pass and E11:** the owner fixes or accepts MINOR A–C and NIT D–F here; SC-2's own conditions (the SD backup
and its name, item 3; the rig and the live-output decision for MASTER and BLACKOUT, item 4; USBPcap bound before the Zadig bind with
the console-only split committed, item 8; the versions, item 9; the written acceptances: H66 for writes is unknown, the last mover
wins, a release can be lost, MASTER and BLACKOUT change the whole output, and the two review points above with the decode-failure
case named; the script adjustments: bump 7 instead of bump 1, the MASTER step last, GO only with STACK dim, MEMS and FADERS 1-24 left
out); the E11 record includes the report-age check of MAJOR 1; and **the owner's go on the day.** Until then nothing is sent.

### Implementation (owner, 2026-10-31)

The owner implemented classes A and B1 at the marked extension points: `HspOutbound` (the typed whitelist, the Data header
and the two payloads), `HspReadSession` (the write queue, the counter, the previous byte, the placement, taps, the gate),
`HspReadLoop` (header then payload, both logged, the floors from `write_rules.py`), `UsbSmartFadeDevice` (the commands
behind the interlocks of conditions 5, 6 and 8, each control only in the console state SmartSoft sent it in: channel fader
1 in channel mode, bank 1-24, with the MASTER at full; bump 7 in channel mode; memory bump 1 on the page shown; SOLO and
BLACKOUT from a steady LED; GO with STACK dim), the switch `Device:Usb:LiveControlDate` (checked at start-up, at Connect
and at every command), `ReplayHspPipe`'s write mode and the golden tests `HspLiveSessionTests` (E9 up to seq 68339 and the
three E9b sessions, byte for byte, the counter less the skipped SmartSoft writes, F132's seq 16560 exception declared).
Bump 9, channel bump 1 and memory bump 7 are whitelisted codes the device never sends (seen from a PC only inside record
functions, or only in the other fader mode). GO, MEMS and FADERS 1-24 had device methods but no port method until 2026-10-31, evening, when
`ISmartFadeDevice.PressGoAsync` / `PressFadersMemsAsync` / `PressFaders1To24Async`, the hub methods `pressGo` / `pressFadersMems` /
`pressFaders1To24` and the UI's GO key (crossfader panel) and Faders 1-24 / Mems keys (memory panel head) were added at the owner's
wish, behind the same interlocks (`DeviceCommands.PressGo` etc., outside the simulator's set).

Run line (E11, on the owner's go only, after the re-check): `dotnet run --project src/backend/SmartFade.Api --
--Device:Mode=Usb --Device:Usb:ConfirmDate=<today> --Device:Usb:LiveControlDate=<today>`.

**E11 script adjustments to the implementation:** "bump 1 tap" becomes bump 7 (the device sends channel bump 7, never
bump 1); the MASTER step goes last, since channel fader 1 is sent only with the MASTER at full; GO only with STACK dim;
MEMS and FADERS 1-24 from the UI (optional step W6 of the guide, around the memory bump) and GO from the UI (optional W8, last, with STACK dim; the stack's next cue crossfades on the console); SOLO and BLACKOUT only from a steady LED. A write queued while the console downloads what changed goes only after the next poll reply (the freshness reading of condition 5), so a drag reaches the console in steps; no timing setting may be slower than its default (the backend refuses to start).

### What SC-2 does not clear

Every fader index except 0x00, 0x30, 0x31 (so channel faders 2–24, bank 25-48 and every memory fader);
every key code except the eight live codes (so bump 10, CLEAR, COPY, EDIT MEM, REC MEM, FADERS 25-48, STACK,
IND 1, IND 2 and anything never seen); the record functions from the PC (SC-3); ChannelText and Memory
writes; the System pair and any System write; anything in class F. A held bump, a write in the read-only
mode, a write outside the observed placement, a retry.

### Path to a wider clearance

- **E9c, passive (SmartSoft's Console Simulator, USBPcap, our software not involved):** every channel fader of
  bank 1-24 moved once (seen so far: faders 1, 5, 7), every bump pressed, the keys seen once (CLEAR, COPY,
  EDIT MEM on an empty memory and left with CLEAR, FADERS 25-48, STACK, IND 1, IND 2, bump 10) and a bump in
  bank 25-48. Indices and codes already OBSERVED once become VERIFIED with E9c as the second session; those
  first seen in E9c need one more capture (E9d). No derivation replaces a capture. **Added 2026-11-01 after E11 attempt 3 (the owner's wish to
  drive memory faders and bank 25-48 from the PC without switching the console by hand):** E9c also moves a Simulator fader in memory mode (memory
  fader 1 of page 1, then of another page) and in bank 25-48, holds the Simulator's memory bump 1, and switches modes with a Simulator fader up,
  so that H111 (the index addresses the physical fader; its effect is the console's mode's) and the matching rule (H79, H81) are observed before
  any of it is classed. Until then every fader index in memory mode and in bank 25-48 stays uncleared, the UI's FADERS 1-24 / MEMS keys (class
  B1, sent in E11) are the only cleared way to change what the console's 24 faders control, and no message that sets a memory fader or a
  channel of the other bank directly is known to exist (our code never guesses one). **Owner decision 2026-11-02:** the
  automatic bank switch is the way to go (the UI presses the console's FADERS key before a fader of another bank is moved) and
  held bumps are wanted; the E9c runbook and guided script (`reverse-engineering.md` "E9c runbook",
  `tools/usb-probe/e9c-guided-session.ps1`) are scoped for it: every channel fader of bank 1-24, the bank 25-48, memory faders on
  two pages, the matching rule with a PC level in force, held bumps with durations, IND 1 / 2, STACK, PAUSE, NEXT, CLEAR, a fader
  with the MASTER at half, a SmartSoft restart. The check that follows E9c (SC-5, with E9d for anything seen once) decides the
  classes; until then nothing of it is cleared. **E9c run 2026-11-02, 00:41–01:20** (record `captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md`): 1 227
  SmartSoft writes, every channel fader index of bank 1-24 once, two faders and the FADERS 25-48 key in bank 25-48, four memory faders on
  two pages, the matching rule after a mode and a page switch with a PC level in force (F172: the console ignores PC events for a
  re-assigned fader until one equals the new content's level), held bumps up to 2.85 s, IND 1 / 2, STACK, PAUSE, NEXT (= 0x3A, with the
  blind levels in BlindLevels (18)), CLEAR, a fader with the MASTER at 0x84, a SmartSoft restart; 0 framing errors; `SHOW25.ASC` =
  `SHOW24.ASC`. Deviations: the Simulator relabels its faders in bank 25-48, so fader 24 and the bump were done in bank 1-24 (no bump
  in bank 25-48); S4b's physical step was first done in the Simulator. **E9c clears nothing; SC-5 below decides.**
- **SC-3 (record keys from the PC):** after E9c, and only with the owner's written acceptance of each key
  (REC MEM, EDIT MEM, COPY, CLEAR), an SD backup immediately before every session that enables them, a UI
  confirmation step before a record key is armed, and the same guard against a stray bump as the panel has
  (a record function is left with CLEAR when nothing is selected within a bound). F136, F145, F146 describe
  the effects; none is reversible by the protocol.
- **E12, passive (planned 2026-10-31; runbook in `reverse-engineering.md`, guide `tools/usb-probe/e12-guided-session.ps1`):**
  SmartSoft edits the patch, the stack and a sequence while USBPcap records, so that the Patch, Independents, Page,
  Sequence, SequenceMain and StepContent writes and the step commands (H91–H101, INFERRED from the binaries) are seen
  at least once. Nothing of it is cleared by E12; a later check, SC-3 or its own, judges those writes per class, and the
  "never" of class F for Patch and Independents below stands until then (the show upload, the erase commands, the
  wheel, FileName and the settings stay never by the owner's decisions).
  *(Note 2026-10-31, evening, no decision changed: E12 was run that afternoon and analysed that evening by the
  `capture-analyst` agent on Opus 5.5 at the owner's request ([record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md),
  known-facts F151–F159). It observed SmartSoft's Patch, Independents, StepContent, Sequence, Page and Memory writes and the
  step commands InsertStep, DeleteStep, MoveStep and id 42 once (the Memory write's layout is now VERIFIED as SmartSoft's
  behaviour, F143, E9b + E12) and cleared none of them: the stack, sequence, page, memory and step-command writes remain
  uncleared, and a later check, after the repeat E12b, decides. Class D (Memory, NO-GO) and class F (Patch and Independents,
  "Never") stand; the step commands and the Sequence, SequenceMain, StepContent and Page writes are still in no class. For SC-1,
  documentation only and INFERRED from the code: E12 showed Sequence records with c ≠ 0 and 144- / 148-byte Sequence /
  StepContent bodies after SmartSoft's step commands and in its download; each would end a read-only session of ours with its
  fault, a safe stop like the one described for F137 (F156, H106).)*
  *(Note 2026-11-01, no decision changed: the repeat **E12b** ran on 2026-10-31, late evening, and was analysed by the `capture-analyst`
  agent on Opus 5.5 at the owner's request ([record](captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md), known-facts
  F160–F165). The writes it saw a second time (the step commands InsertStep, DeleteStep, MoveStep and id 42, and the Patch,
  Independents, StepContent, Sequence and Page writes) are now VERIFIED as SmartSoft's and the console's behaviour (F151, F152, F156,
  F157; F153 and F154 in part), which is no clearance: **nothing of E12 or E12b is cleared**, class F (Patch and Independents, "Never")
  and class D (Memory, NO-GO) stand, the step commands and the Sequence, SequenceMain, StepContent and Page writes are still in no class,
  and the key codes 0x32 and 0x3A that SmartSoft's stack view sent (F165) are outside class B1's whitelist. Two findings matter for
  any write of these objects later: a step command on a live stack changes what is live or next at once, without a GO (F164), and the
  console can be silent on USB while it writes its SD card (F163, H109; the condition added under "Conditions for E11" above).)*
- **E11's result** promotes nothing by itself; a second write session of ours (E11b) is needed before any
  write fact about our own host context becomes VERIFIED.

## SC-5 (2026-11-02): the automatic bank switch and held bumps, per class, after E9c

**Scope.** The owner's decision of 2026-11-02 ("Owner decisions on scope" in `reverse-engineering.md`): the UI drives memory faders and
channels 25-48 by pressing the console's FADERS key for that bank before the fader event (the automatic bank switch), and bumps may be
held from the UI. **Input:** the [E9c record](captures/2026-11-02-e9c-smartsoft-live-controls-every-bank.md) ("Input for the safety
check that follows", F171–F177), E9 / E9b (F131–F135, F141), E11 (F167–F169: our own host), the E12b codes (F165). **Checklist:** the
eleven items of `/hardware-safety-check`, answered per class below SC-2's own bar: a message class is cleared for sending only when its
bytes and the console's answer to them were seen in **two independent sessions** (VERIFIED as behaviour; "necessary, not sufficient"),
its side effects are understood, and the conditions hold. Everything else is NO-GO until E9d, the passive repeat named at the end.

### What E9c changed in the evidence

- **The fader index is the physical fader** (F171, H111 CONFIRMED in part): all 24 indices of bank 1-24 seen once (0x00, 0x04, 0x06 in two
  or more sessions); in bank 25-48 and in memory mode the same indices move that bank's content (two and four indices, once each).
- **The switch does not carry a level** (F172): after FADERS MEMS / 1-24 / 25-48 or a page change, a re-assigned fader is unmatched and
  the console **ignores PC events for it until one equals the new content's level**; the level left behind is kept. The failure mode of a
  wrong first event is therefore **nothing moves and the bump LED blinks**, not a jump of the output. This is the rule the automatic
  switch must implement: switch, wait for ConsoleStatus to report the bank, then start from the target's reported level.
- **Held bumps** behave as taps that last (F174): the output at the BUMPS level from the press to the release, ending within one poll of
  the release; seen in three sessions for bump 7 and memory bump 1, two for bump 10, one for channel bump 1.
- **The keys seen once before are now seen twice** (F175): FADERS 25-48 (0x37), IND 1 / IND 2 (0x47 / 0x48), STACK (0x3B), PAUSE (0x32),
  CLEAR (0x38); **NEXT is 0x3A** (F173) and silences the live levels into BlindLevels (18).

### Decision: GO (conditional) for classes A2 (three indices today), B4 (the bank keys), B5 (IND 1 / IND 2) and B3 (held bump 7, bump 10, memory bump 1); GO after E9d for the rest of A2, for A3 and A4; NO-GO for STACK, PAUSE, NEXT and CLEAR; the System pair (E / SC-4) not decided here

| # | Item | Status | Evidence |
|---|---|---|---|
| 1 | Evidence status | **Yes for the classes cleared today, by SC-2's bar** | A2 index 0x00 VERIFIED (E9, E9b, E9c, E11); 0x04 (E9b + E9c) and 0x06 (E9 + E9c) in two sessions; the other 21 indices once (E9c), so after E9d. A3 (bank 25-48): 0x37 in two sessions (E9b + E9c); the indices there once (0x00, 0x01) or never, so after E9d. A4 (memory faders): 0x39 / 0x36 in many sessions; the indices as memory faders once (0x00, 0x01, 0x02, 0x08) or never, so after E9d. B3 (held bumps): 0x06 and memory 0x00 in three sessions, 0x09 in two; channel bump 1 once (after E9d); other bumps never. B4 (0x37) and B5 (0x47, 0x48) in two sessions with the same answer (F175, F115). F131 / F132 / F133 reproduced in a fourth capture. |
| 2 | Side effects understood | **Yes for the cleared classes; the switch's rule is F172** | A fader event moves its physical fader's content at once once matched (F141, F171); after a switch nothing moves until matched (F172); a bank key changes what all 24 physical faders control, visible in ConsoleStatus body[0] / body[4] and the LEDs within one poll (F98, F175), and is what the owner asked for; IND 1 / 2 drive two outputs (DMX 97 / 98 by default, DMX 47 in this patch; F108, F115): live devices, the owner's rig decision; a held bump outputs at the BUMPS level until released (F174). Unobserved: a held bump longer than 2.85 s; a switch while a bump is held; a bump in bank 25-48. |
| 3 | Backup | **Condition** | An SD save before every live session (the E11 guide's gate), compared with the after-session save. |
| 4 | Rig | **Condition** | As SC-2; IND 1 / IND 2 added: the owner states what they drive on the rig before they are enabled in a session (a smoke machine or a cue light is the manual's example, F108). **2026-11-02: the owner does not know what they drive.** The captures do: IND 1 changed no DMX output in this show (unpatched; E9b, E9c), IND 2 drives DMX 47 (E8b, E9b, E9c), and the rig sits on DMX 5–16 (owner), so on this rig neither key reaches a fixture; the condition holds until the patch or the rig changes, and the guide asks again before a session. |
| 5 | Firmware path untouched | **Yes** | Bulk transfers on interface 1 only; nothing new. |
| 6 | Mode gating | **Condition (to build)** | The same write switch as SC-2 (`Device:Usb:LiveControlDate`); the automatic switch is a device-layer sequence (below), never a UI shortcut; the UI shows the console's bank (`faderBank`, since 2026-11-02) and an unmatched fader (the LED blink) from the console's own objects. |
| 7 | No blind retries | **Condition (to build)** | As SC-2: one write, a failure ends the session. For a held bump the release is sent exactly once; if the session dies before it, the device must not resend on reconnect (the latch of SC-2 condition 4 holds). |
| 8 | Logging on | **Yes, plus USBPcap in parallel** for the first session of each new class (E11b), as E11. | |
| 9 | Versions recorded | **Condition** | As SC-2. |
| 10 | Review done | **Yes: three passes, PASS on the third (2026-11-02, afternoon)** | `protocol-safety-reviewer` over the exact code (98cb499, 1eceabb, 0f2f1da), see "Review (SC-5 item 10)" below: the first pass found one blocker and three majors, the re-check resolved them and found one new major (the blackout level) and two minors, the third pass found everything resolved, one optional nit left as a follow-up and nothing open on the review side before E11b. Nothing is cleared for sending by the review itself. |
| 11 | Passive first | **Yes** | E9c shows SmartSoft producing every event this check names; our software has produced only SC-2's. |

### Classes

| Class | Message | Evidence after E9c | Decision |
|---|---|---|---|
| A2 | ConsoleEvent kind 0, fader events, **bank 1-24, indices 0x00–0x17** (channel faders 1–24) | F171: all 24 once; 0x00 VERIFIED, 0x04 and 0x06 in two sessions; the answer per index structural (F82–F84, F86) | **GO (conditional) now for 0x00, 0x04, 0x06; GO for the other 21 after E9d** (two sessions each). The encoder may carry all 24 with golden tests over E9c now; the whitelist enables an index only when its second session is recorded here. |
| A3 | fader events **in bank 25-48** (indices 0x00–0x17 = channels 25–48), after the FADERS 25-48 key | F171: 0x00 and 0x01 once; 0x02–0x17 never; the key 0x37 in two sessions | **GO after E9d** (every index there in two sessions); the key 0x37 is class B4 |
| A4 | fader events **as memory faders** (memory mode, the shown page) | F171: 0x00, 0x01, 0x02, 0x08 once; the rest never; F112 for the record's bytes 0 and 1 | **GO after E9d** (every index in memory mode in two sessions, on page 1; a page change from the PC is in no class: the UI follows the console's page, F113) |
| B3 | ConsoleEvent kind 1, **held bumps** (press, hold, release as the UI holds) | F174: bump 7 in three sessions, memory bump 1 in three, bump 10 in two, channel bump 1 in one; the other channel bumps 2–6, 8, 11–24 and memory bumps 2–24 never from a PC | **GO (conditional) for bump 7, bump 10 and memory bump 1 as holds bounded to 3 s** (the longest observed; the device releases at the bound and the UI shows it); **the other bumps after E9d** (each pressed in two sessions); a hold across a bank switch is refused |
| B4 | FADERS 25-48 (0x37) as a tap, with FADERS 1-24 (0x36) and FADERS MEMS (0x39) of class B1 | F175, F98: two sessions; ConsoleStatus body[4] | **GO (conditional)** as a tap, from the two states SmartSoft sent it in (from bank 1-24 or memory mode); it is the automatic switch's third key |
| B5 | IND 1 (0x47), IND 2 (0x48) as taps that toggle | F175, F115: two sessions; the console's own IND keys do the same (E8b) | **GO (conditional)** as toggles from a steady LED (blocks 72 / 73) that differs from the wish, as SOLO and BLACKOUT in SC-2; the rig condition (item 4) |
| B6 | STACK (0x3B) | F175: two sessions; it arms the stack for GO (F130) | **NO-GO**: no UI need; GO stays refused while STACK's LED is lit (SC-2) |
| B7 | PAUSE (0x32) | F175: two sessions; effect only on a running fade (E12b) | **NO-GO**: no use without the stack |
| B8 | NEXT (0x3A) | F173: a mode that silences all faders' live effect (BlindLevels) | **NO-GO**: shown in the UI from the console's state only; its use needs its own check |
| B9 | CLEAR (0x38) | F175 / E9: as a tap with nothing up and inside an edit; its double and triple effects never from a PC | **NO-GO** until a passive capture shows the double and triple press from the PC with levels up |
| E | the System pair at Connect (SC-4 candidate) | F58: 20 sessions in 10 captures (the row said "eleven sessions", an out-of-date count; corrected 2026-11-02, evening, F181); F177: the 158-record download | **Not decided here**; a separate check when the owner asks |

### Conditions for building the automatic switch and the holds

1. **The switch is a device-layer sequence, logged as one operation:** (a) refuse if a record function is armed, a bump is held or the
   console's bank is unknown; (b) send the bank key as a tap (0x36 / 0x37 / 0x39, class B1 / B4) from a state SmartSoft sent it in;
   (c) wait until ConsoleStatus reports the new bank (body[0], body[4]) and the LEDs arrived, within a bound (two seconds; else the
   session ends as a deviation); (d) send the first fader event with **the target's reported level** (ChannelLevels[c] for a channel,
   FaderInfo record k bytes 0 and 1 for a memory fader), then the drag; (e) show the bank and the LED blink from the console's objects.
   The switch back is the user's, or the next automatic switch. The physical faders now control the new bank: the UI says so.
   *(Annotated 2026-11-02 after the first review pass, MAJOR 1: "the target's reported level" is the bump's level while a bump of
   that physical fader is held, F174 VERIFIED: in 16 of 16 channel-bump holds FaderInfo record k read `ff 00 00 00` and
   ChannelLevels[c] 0xFF from the press to the release, in 51 of 51 memory-bump holds the record read `ff ff 00 00`; and in 9 of 16
   channel-bump holds the first reply after the release listed neither object. So the matching event of a fader is not sent from the
   press of our own bump on that fader until FaderInfo and ChannelLevels have been delivered after the release; a bump held on the
   PANEL while the UI moves the same fader cannot be told from a fader at full (F178, VERIFIED the same day: no object marks a press)
   and is a residual risk accepted by the coordinating assistant at the owner's delegation (the review table, MAJOR 1 (ii)) and
   acknowledged at the E11b gate, like F141. What the console does with a matching event at a bump's level is UNKNOWN: unobserved in
   every capture.)* *(Annotated 2026-11-02 after the re-check, new MAJOR: during a BLACKOUT the reported level is 0x00 for every fader
   while the MASTER byte stays 0xFF, F125 OBSERVED once, timed (E8b seq 26987 / 26991 / 26999, the content restored at 27754 / 27762;
   E11 attempt 3's BLACKOUT taps had nothing up, so no second observation; F114 VERIFIED for the LED), so a
   matching event would carry 0x00 whatever the content; LED block 53 shows the blackout, and the device refuses channel and memory
   fader events while it is not steady off. With SOLO on, a held bump replaces the output (F6, DOCUMENTED): whether FaderInfo of the
   other faders drops too is unobserved (INFERRED from F125 / F176), so a hold of ours under SOLO shadows every physical fader, and
   the panel case is part of Acceptance 6.)*
2. **A held bump is one operation with a bound:** press, the hold renewed by the UI, the release sent by the device when the UI
   releases or when the bound (3 s) expires, and before any Disconnect; a hold cannot span a bank switch (refused); the lost-release
   case (a process killed mid-hold leaves the bump down until the panel is pressed, INFERRED) **was accepted by the owner on 2026-11-02**
   ("ok" in chat, recorded in the E9c owner-statements file).
3. **One encoder, one whitelist** as SC-2: the indices and codes above as enums, nothing derived; golden tests over E9c (and E9d) for
   every new index and code, byte for byte, including the switch sequences SmartSoft used (0x39 then 0x00 …; 0x37 then 0x00 …).
4. **The previous byte** stays this host's own last value per index (F132), across a bank switch too (SmartSoft's counter does not
   reset at a switch: the index's last wire value is the previous byte whatever the bank).
5. **The UI starts every drag from the reported level** (SC-2 condition 6; with the switch it is what makes the first event match).
6. **Nothing else**: no page change from the PC, no NEXT, no STACK, no PAUSE, no CLEAR, no record key, no System write.

### E9d (the passive repeat that lifts the "after E9d" rows)

One capture, SmartSoft's Simulator, two SmartSoft sessions (a restart between them, as E9c's S8; counted as the second session for
what E9c saw once, as E3b's sessions were for E2): in each session every channel fader 1–24 once in bank 1-24, FADERS 25-48 then every
fader 1–24 once (channels 25–48) and one held bump there, FADERS MEMS then every fader 1–24 once as memory faders of page 1 (empty
memories move the record only) and every memory bump once, FADERS 1-24; a held bump of 5 s; the S4a case once more. The owner agreed
on 2026-11-02; the guided script `tools/usb-probe/e9d-guided-session.ps1` (written from the E9c one the same day, parse-checked, not run)
does exactly this in two SmartSoft sessions. **Run 2026-11-02, 09:47–10:14** ([E9d record](captures/2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.md)): both passes complete after a console power
cycle, 2 855 writes, 0 framing errors, `e9d/check.py` 228 steps as expected, `SHOW27.ASC` = `SHOW26.ASC`. The addendum below records the
decisions it lifts.

### SC-5 addendum (2026-11-02, after E9d): the "after E9d" rows decided

| Class | Evidence after E9d | Decision |
|---|---|---|
| A2, indices 0x01–0x03, 0x05, 0x07–0x17 (channel faders 2–4, 6, 8–24 of bank 1-24) | F171 VERIFIED: every index in E9c and in both E9d sessions, each moving its own record, channel, DMX address and LED block | **GO (conditional), as the three indices before:** the whitelist may enable all 24 indices of bank 1-24 under SC-2's conditions (the MASTER-at-full rule of condition 6 holds for every channel fader as for channel fader 1) |
| A3, bank 25-48 (indices 0x00–0x17 after the FADERS 25-48 key) | F171 VERIFIED: every index in both E9d sessions (0x00, 0x01 also E9c), moving record N, ChannelLevels[24 + N] and DMX 24 + N (channel 47 unpatched in this show: no DMX); the key 0x37 in three sessions | **GO (conditional)** for the fader events in bank 25-48, only through the switch sequence of condition 1 (the bank reported by ConsoleStatus body[4] before the first event) |
| A4, memory faders (indices 0x00–0x17 in memory mode, the shown page) | F171 VERIFIED: every index in both E9d sessions on page 1 (0x00 on page 2 once, E9c); an empty memory moves only its record | **GO (conditional)** for the fader events in memory mode on the page the console reports (F113), only through the switch sequence of condition 1; no page change from the PC |
| B3, held bumps: memory bumps 1–24 | F174 VERIFIED: codes 0x00–0x17 in memory mode held about 1 s in both sessions, memory bump 1 in five | **GO (conditional)** as holds bounded to 3 s, in memory mode on the reported page, with condition 2 |
| B3, held bumps: the bump of bank 25-48's first fader (code 0x00 in bank 25-48 = channel 25) | F174 VERIFIED (two sessions) | **GO (conditional)** as a hold bounded to 3 s, in bank 25-48 only |
| B3, held bumps: channel bumps 2–6, 8, 9, 11–24 of bank 1-24 and 26–48 of bank 25-48 | never pressed from a PC in channel mode (E9d's script omitted them); their codes 0x00–0x17 VERIFIED as memory bumps in two sessions (F174); the same codes as channel bumps 1, 7, 10 and 25 behaved by the physical-control rule, which E9c and E9d confirmed for every fader of three banks (F171) | **GO (conditional) on the physical-control rule** (addendum 2 below): the bytes are VERIFIED, the console's answer in channel mode is inferred from a rule that held in 100 of 100 fader and bump cases across three banks; residual risk, stated: a bump flashing a channel other than the one named, live and visible at once in the UI. **Control: E9e as a spot check** (four bumps per SmartSoft session, two sessions, about a minute each: faders 2, 12, 24 and the one labelled 48), run before E11b or declined by the owner in writing |
| B3, the hold bound | bump 7 held 4.67 s and 3.42 s (E9d), 2.19 s (E9c), 3.35 s (E9b): shown and ended cleanly every time | the bound stays **3 s** (every longer hold is a single observation of its length); it may be raised by a later addendum |

The conditions of SC-5 (the switch sequence, the bounded hold, one encoder and whitelist with golden tests over E9c and E9d, the
previous byte, the reported level, nothing else) are unchanged, and so is the order: the owner implements, `protocol-safety-reviewer`
passes, E11b runs with USBPcap. Nothing is sent before that.

**Addendum 2 (2026-11-02, the same day): the channel bumps, and why E9e is a spot check.** The first E9e script asked for every
channel bump of both banks twice (96 presses); the owner refused a second long repetitive session, with reason: E9d should have
covered them and the omission was the analyst's. The decision above therefore rests on the evidence that exists: the 24 key codes are
VERIFIED as bytes and as bumps in memory mode (F174), four of them as channel bumps in both banks (F174, F175), and the rule "a
ConsoleEvent names the physical control; the console's bank decides what it drives" held in every one of the 48 fader positions of
bank 1-24 and bank 25-48 and the 24 memory faders and 24 memory bumps of E9c and E9d (F171, F174: 100 of 100 cases). The remaining
unknown, whether a channel bump other than 1, 7, 10 and 25 lights a channel other than its own, would be a live, visible, self-correcting
surprise, below the risk already accepted for the MASTER and BLACKOUT events. E9e is kept as a four-bump spot check per session, about a
minute each, as the control of that inference; the owner may decline it in writing, in which case the inference stands alone and is
recorded as such. **E9e run 2026-11-02, 10:57–11:06 (record `captures/2026-11-02-e9e-smartsoft-channel-bumps-spot-check.md`): the four bumps in both sessions as predicted (codes 0x01,
0x0B, 0x17 in bank 1-24 → channels 2, 12, 24; 0x17 in bank 25-48 → channel 48), 0 framing errors, `SHOW29.ASC` = `SHOW28.ASC`: the control
passed, the inference stands, the decision above is unchanged.**

**Addendum 3 (2026-11-02, after the implementation): channel bump 1 of bank 1-24.** The row above lists the channel bumps 2–6, 8, 9,
11–24 and 26–48 and names 1, 7, 10 and 25 as the ones a PC pressed in channel mode, but no row decides channel bump 1 of bank 1-24
(code 0x00 in channel mode): the omission is the analyst's. Its evidence is stronger than the row's: the code is VERIFIED as memory
bump 1 (E9, E9b, E9c, E9d; F174) and as bank 25-48's first bump (E9d, two sessions), and SmartSoft's Simulator held it once in bank
1-24 (E9c S2's deviation step, 2.54 s: ChannelLevels[1], DMX 1, FaderInfo record 1 and LED block 1 at 0xFF from the press to the
release). **GO (conditional) as a hold bounded to 3 s in bank 1-24, on the same rule and under the same conditions as the row above;**
the implementation's reading 2 below is confirmed by this addendum, and the owner's acceptance on the day covers it with the rest of SC-5.

### Implementation (an Opus 5.5 agent at the owner's request, 2026-11-02)

The SC-5 classes were implemented on 2026-11-02 by an Opus 5.5 subagent at the owner's request, under the coordinating
assistant, which writes none of the sending code (CLAUDE.md); written for the `protocol-safety-reviewer`
pass of item 10 (committed as 98cb499 the same day, before the pass). **It clears nothing: nothing is sent before that pass and the owner's go for E11b** (with USBPcap, the backup and the
conditions above). The first pass (NEEDS CHANGES, "Review (SC-5 item 10)" below) was answered by the same agent in the working tree
the same day; "Changes after the first pass" at the end of this section lists each finding with what changed and its test,
"Changes after the re-check" does the same for the second pass, "Changes after E11b" for the bump shadow's end rule the owner
asked for after his bump test, and "Changes after the fourth pass" for that rule's review. What was built, where:

- **One encoder, one whitelist (condition 3).** `HspOutbound.cs`: `ClearedFader` = `Fader1` … `Fader24` (0x00–0x17), `Master`,
  `Bumps`; `ClearedKey` = `Bump1` … `Bump24` (0x00–0x17), `Go`, `Solo`, `Blackout`, `Faders1To24`, `Faders25To48` (0x37), `FadersMems`,
  `Ind1` (0x47), `Ind2` (0x48); each member's doc comment names its evidence (E9c S1–S6, E9d A–D, E9e; F171, F174, F175).
  `PhysicalControls.Fader(n)` / `Bump(n)` are explicit 24-row tables (F171, F174), with no arithmetic; a number outside 1–24 is
  refused. Unrepresentable: CLEAR 0x38, STACK 0x3B, PAUSE 0x32, NEXT 0x3A, REC MEM 0x41, EDIT MEM 0x42, COPY 0x3E and every other
  index or code; the boundary refuses a cast value as before.
- **The session (`HspReadSession.cs`).** `QueueKeyHold` (a bump code only; any other key is refused at the boundary, so a held GO,
  SOLO, bank key or IND is unrepresentable) and `ReleaseKey` (once: the release made due, or a hold whose press is still queued
  turned into a tap; false when there is nothing to release), `HoldInProgress`, `IsFaderQueued`. Nothing but the release goes while
  a key is held, as before; the placement, the counter and the previous byte (per index, whatever the bank: condition 4) are
  unchanged. The golden tests' press of any key (`QueueKeyPress`) stays internal.
- **The hold bound (`HspReadLoop.cs`, condition 2).** `MaxHold` = 3 s, `HoldReleaseLead` = 250 ms: on the loop's clock, 2.75 s after a
  press header the loop asks the session for the release, which goes at the next write slot (a tap's release followed its press after 46.6–76.7 ms in E11
  attempt 3, OBSERVED), whatever the UI does; a release later than 3 s after its press is noted in the transfer log as a deviation. A stop makes
  the release due at once and a fault sends it when no exchange is open (SC-2's paths, condition 4).
- **The device (`UsbSmartFadeDevice.cs`).**
  - *Controls and states.* Channel faders 1–48: channels 1–24 are physical faders 1–24 in bank 1-24, channels 25–48 the same faders
    in bank 25-48 (F171; an explicit 48-row table since the first pass); memory faders of the page the console shows (F113), only while the physical fader holds that page's memory
    (FaderInfo record k byte 3 = page − 1, H69 CONFIRMED); every channel and memory fader only with the MASTER at full (SC-2
    condition 6 extended: every PC event of E9c and E9d in memory mode or bank 25-48 came at full); a channel fader whose
    FaderInfo record byte 1 is not 0 (a memory left up across FADERS 1-24, F112's limit) refused. Bumps 1–48 and the memory bumps
    as holds, one at a time, only once FaderInfo has arrived (since the first pass); IND 1 / IND 2 as toggles from a steady LED (blocks 72 / 73) that the Independents object, once
    delivered, does not contradict, in channel mode, bank 1-24 (every PC press of E9b and E9c); FADERS 25-48 only from bank 1-24.
    NEXT mode on or unknown (ConsoleStatus body[6], F173) refuses every control, and so does memory mode entered from bank 25-48
    (the bank flag kept, F98): no PC control was seen in either but NEXT's fader events, once, which make the console list
    BlindLevels (18), an id that ends a session of ours.
  - *The automatic switch (condition 1).* A fader or bump of another bank than the console reports starts it, unless a record
    function is armed, a bump is held, the bank is unknown, the bank key was never sent by SmartSoft from the bank shown (FADERS
    1-24 from memory mode or bank 25-48; FADERS 25-48 and FADERS MEMS from bank 1-24) or the memory page differs (a): the key goes
    as a tap (b); every control asked meanwhile for the same bank waits device-side (the newest level wins), others are refused;
    once its press went, **the gate holds every write but a release** until ConsoleStatus reports the new bank, a LedStatus arrived
    after the key and nothing that reports the faders or the keys is listed and unfetched; past `BankSwitchReportBound` (2 s) the
    session ends with that reason, a fault like the other safe stops (c); then each waiting fader gets its matching event and a
    waiting bump its hold (a tap when the UI let go meanwhile) (d). The UI's own FADERS keys go through the same wait and bound.
  - *Matching (condition 1 d, F172).* Every physical fader is unmatched at Connect and after every reported change of bank, mode or
    page; its first event carries the target's reported level, ChannelLevels[c] for a channel (sent only when FaderInfo record k byte
    0 reads the same, else refused: another source drives the channel, a reading INFERRED for a memory up on another fader or the
    stack and contradicted for a bump held on that fader, F174, hence the bump's shadow added after the first pass; refused too until
    the console has delivered ChannelLevels in the session) and FaderInfo record k bytes 0 = 1 for a memory fader. The UI's level waits device-side and is queued only once the
    matching event was taken for sending, so it can never replace it in the queue; the matching event is asked again right before it
    goes (held while FaderInfo or ChannelLevels is listed and unfetched, dropped when the state or the level changed).
  - *Holds.* Press and release from the same code path (`PressBump`, `ReleaseBump`); released by the UI, by the loop's bound, by a
    stop, and at once when the console reports another bank or page (a hold does not span a bank switch, condition 2); a switch or a
    second bump while one is held is refused. The incomplete-write latch is unchanged. Since the first pass a bump this host pressed
    keeps its physical fader out of matching and fader events until the console has reported that fader again after the release
    (since E11b: until its reports read the content again, "Changes after E11b").
  - *Logged as one operation.* The transfer log gets a note when the bank key goes, when the console's report completes the switch
    (with its delay), for each control queued or not sent after it (with the reason), when a fader is matched, when the bound asks for
    a release and when a hold is released by a bank change. Since the first pass the notes of the bank key and of a matched fader,
    which their writes' `sent` callbacks leave, are written at the next event, after the payload, never between the gate and the header.
- **`ConsoleStateAssembler.cs`** exposes FaderInfo, whether ChannelLevels arrived, the Independents object, the IND LEDs and
  `ReportedFaderMode.NextMode` (body[6]); read only to refuse or to choose a matching level; since the first pass also `BlinkingFaders`, for the UI only.
- **Port, API, UI.** `DeviceCommands.PressFaders25To48`, `DeviceControls.Faders25To48`, `ISmartFadeDevice.PressFaders25To48Async`,
  `ConsoleService.PressFaders25To48Async`, the hub method `pressFaders25To48` (docs/api.md), `not_supported` on the simulator, the UI's
  FADERS 25-48 key as a tap when the device supports and names it; the faders, bumps and IND keys are enabled from `writableControls`
  as before.

**Golden tests.** Encoder (`HspLiveSessionTests`): every physical fader in each of the three banks (72 cases, the first event of each
index in E9d's first pass, the bank asserted from the capture's ConsoleStatus), every bump code (the 24 memory bumps' presses and
releases of E9d C; channel bumps 1 and 10 of E9c, 7 and bank 25-48's first of E9d, 2, 12, 24 and the one labelled 48 of E9e), FADERS
25-48 (E9c, E9d, E9e), IND 1 / IND 2 (E9c), the whitelist exactly SC-2's and SC-5's, the NO-GO keys refused. Session replays, SmartSoft's
event handed over at each of its writes and every transfer compared byte for byte (`ReplayHspPipe` gained `smartSoftSessionStart` and
`expectWritesFrom`): E9c S1 seq 868 … 97136 (1 005 writes; 61 skipped: the first step's 51 and the 10 STACK, PAUSE and NEXT writes), E9d S1
813 … 57701 (1 476 / 0) and S2 58796 … (1 358 / 17), E9e S1 994 … 22551 and S2 23515 … (10 / 2 each), with E9d's three coalesced previous
bytes declared (seq 21367, 78433, 81351; as E9's 16560); E9 and E9b recounted with the larger whitelist (328 / 11; 381 / 7, 0 / 3, 29 / 7).
Each range starts at the first zero-word poll after SmartSoft's show download (158 records, 802 bytes: longer than any list a session of
ours accepts, SC-1's bound unchanged), with SmartSoft's System pair counted before it; where SmartSoft's first event came before the
console's first LedStatus after the download (condition 8 holds a session of ours there), that first step is skipped (each ended with its
control at 0x00, so SmartSoft's previous bytes stay this host's); channel bump 2 is therefore covered by the encoder test only. A coverage
test reads the same ranges: every index in each bank, every bump code in memory mode, channel bumps 1, 7, 10, 12, 24 and bank 25-48's 1
and 24, SmartSoft's switch sequences (FADERS 25-48 then index 0x00; FADERS MEMS then index 0x00), IND 1 and IND 2. Interlock
(`LiveInterlockGoldenTests`): recounted over 782 SmartSoft writes (E9 gains a fourth range, 91094 … 95542, for bump 10 inside EDIT MEM): the
20 key events and 40 of the 61 fader 7 events SmartSoft sent inside a record function are dropped, the other 21 held, none of the 701
others dropped (counted first with a Python port of the rules). Device (`UsbSmartFadeDeviceTests`, the stand-in console with bodies from
E9b, E9c and E9d): the switch to bank 25-48 (bytes as E9d 18706, 18711, 21637) and to memory mode (E9d 29032, 29041, 30522), the 2 s
deviation, the barrier (a console answering three polls late; the test fails with the barrier removed), the page and byte-3 refusals (E9c
80286 / 80290), holds released by the UI, by the bound (2.75–3 s), before a Disconnect and on a bank change, a bank 25-48 bump through the
switch (E9e 19934 … 20835), FADERS 25-48 and NEXT mode, IND 1 (E9c 90315 / 90324) and a contradicting Independents object, an unknown
and an ambiguous matching level.

**Changes after the first pass (the same agent, 2026-11-02, in the working tree; nothing run against a console):**

- *Blocker, the statuses on the hardware path.* `UsbSmartFadeDevice`'s class `[Evidence]` reads F171, F174 and F175 VERIFIED
  (SmartSoft's events and the console's answers to them); F172 at each case's own status, (a) VERIFIED in part, (b) and (c) OBSERVED
  once, the 1-24 / 25-48 bank switch in none of its cases, a matching event from our own host unobserved until E11b; and the reading
  "FaderInfo record k agreeing with ChannelLevels[c] means no other source drives the channel" INFERRED for a memory up on another fader
  and for the stack, contradicted for a held bump (F174). The doc comments of `MatchingLevel` and `ConsoleStateAssembler.FaderInfo` say
  the same.
- *Major 1, a held bump's level is never matched.* `BumpShadow`: from the press of a bump this host sent until FaderInfo (and, for a
  channel bump, ChannelLevels) has been listed after its release and then delivered, a fader command for its physical fader, in any
  bank, is refused, and an event already queued for it is dropped at its slot (`Shadowed`, part of `FaderState`, asked at the command
  and again right before the write). Evidence: F174 (16 of 16 channel-bump holds read record k `ff 00 00 00` and ChannelLevels[c] 0xFF,
  51 of 51 memory-bump holds `ff ff 00 00`); after the release the first poll reply named neither object in 9 of 16 channel holds (E9c
  header seqs 29262, 86165, 87996; E9d 27254; E9e 17655, 18352, 18882, 28382, 28803) and FaderInfo at once after 51 of 51 memory
  holds, and a memory bump of an empty memory changes no channel, so a memory bump waits for FaderInfo only (the rule INFERRED from that
  OBSERVED evidence). Fail closed: a bump that changed neither report (its fader's content already at full) leaves the fader refused
  until the console lists both objects again for another reason (a panel move of that fader does it). *(Superseded the same day: the
  owner met this edge in his bump test after E11b and rejected it; the end rule is now the one under "Changes after E11b" below.)* A bump held on the panel cannot be
  told from a fader at that level (case a: not detected, the owner's written acceptance at the E11b gate). Tests: `A_fader_waits_after_its_own_bump_until_the_console_has_reported_it_again_after_the_release`
  (E9e seq 17675 / 17679 during the hold; after the release PlaybackInfo seq 17789 alone, as E9e's first reply after it, then
  FaderInfo seq 17796 alone, both refused; with ChannelLevels seq 17800 the matching event at 0x00 and the drag go) and
  `A_fader_event_queued_behind_a_press_of_its_own_bump_is_dropped_unsent`; each fails without the shadow, with a shadow that ends at
  the release, and with one that ends at FaderInfo alone.
- *Major 2, nothing between the gate and the header.* `SwitchKeySent` and `MatchingSent`, the `sent` callbacks that run between the
  gate and the header, change state only and leave their note under the lock (`LeaveNote`); `WriteLeftNotes` writes the notes left at
  the session thread's next event, after the payload (at the session's end before the log closes). Test:
  `No_note_is_written_between_the_gate_and_a_writes_header` (the pipe, the transfer log and the loop's waits recorded in one order: for
  every Data header no note between its slot wait and the header, and the two notes after their payloads; it fails when a callback
  writes its note itself). This also answers the nit on the "FADERS ... sent" note written before its header.
- *Major 3, the unmatched fader shown (condition 1 e, item 6).* `ConsoleStateAssembler.BlinkingFaders`: the physical faders 1-24 whose
  LedStatus block shows two different phases (a blink, F85, F116), empty while the LCD shows the page view or a record screen (blocks
  1-24 mean pages or selectable memories there, F113, F116); carried by `DeviceStateChange`, `ConsoleState` and the DTO
  (`blinkingFaders`, docs/api.md) to the UI, which marks the bump button of channel k or 24 + k (by `faderBank`) or of memory fader k
  on the console's page with a blinking outline and "blinking on the console". Display only: nothing is sent from it. The readings,
  all from E9c S4: unmatched after a mode switch, `46 00 00 / 0A 00 00` in memory mode (seq 51473, case a; 67160, case b) and
  `00 46 00 / 00 0A 00` in channel mode (seq 59535, case a), "blinking = unmatched" INFERRED from them; a fader still holding another
  page's memory, `C8 00 00 / 0A 00 00` (seq 80317, case c), though a PC value moves it at once; physical fader 2 while it was moved on
  the panel (seq 76674, 77680; not explained); the page view's first LED report, one exchange before its LCD title (seq 47749, 80267,
  84586). The 1-24 / 25-48 switch is in none of F172's cases, so the mirror claims nothing there beyond the console's own LED. A change
  of the fader bank or of the blinking faders now raises the full state, which carries both (the bank alone raised nothing before, a gap
  of the first implementation). Tests: `The_blinking_faders_are_the_LED_blocks_1_to_24_whose_phases_differ_outside_the_page_view_and_the_record_screens`
  (every change over E9c S1, seq 868 ... 97136, as listed above), `The_fader_bank_and_the_blinking_faders_reach_subscribers_with_the_full_state`,
  and the specs of the state service, the fader, the channel grid and the memory grid.
- *Minor 3.* `ChannelPosition` is an explicit 48-row table; `Every_channel_sits_on_the_bank_and_physical_fader_E9d_moved_it_by` checks
  every row against E9d's two passes (each channel named by the fader event whose value the next ChannelLevels showed on that channel
  alone; it fails with one row changed).
- *Minor 4.* A bump needs FaderInfo as a fader does (`FadersUnreported` in `PressBump`, fail closed): `A_bump_needs_the_consoles_FaderInfo_as_a_fader_does`
  (a channel bump and a memory bump refused, nothing sent).
- *Minor 5.* ChannelLevelsObject cites F67, F171 and F174 VERIFIED and F83, F96, F176 OBSERVED; FaderInfoObject F82 (layout, in part),
  F171 and F174 VERIFIED and F95, F96, F97, F99, F112, F176 OBSERVED, with their doc comments.
- *Nits.* `Bump10`'s evidence note separates the modes (E9's channel-mode press inside EDIT MEM, header seq 95535; its memory-mode press
  inside COPY, header seq 84654); the replay's summary says that the E9c range's last two fader events (headers 97131, 97135) were
  SmartSoft's in NEXT mode, reproduced by the session layer and refused by the device; docs/api.md and docs/architecture.md give the
  hold bound as released 2.75 s after the press on the device's clock, later only with a slow console, logged, and the MASTER rule for
  channel and memory faders only. In the device tests the stand-in console's pause got its own event per pause (a Resume followed at
  once by the next pause could unset the event before the paused poll saw it).

**Changes after the re-check (the same agent, 2026-11-02, in the working tree; nothing run against a console):**

- *New major, BLACKOUT.* `BlackoutNotOff` is part of `FaderState` and of the command's refusals: a channel or memory fader event (the
  matching event and the drag) goes only while LED block 53 reads steady off (F114, VERIFIED: steady blue while BLACKOUT is on), and not
  from the press of a BLACKOUT tap of ours until the console has reported block 53 again. BLACKOUT zeroes FaderInfo record k byte 0 and
  ChannelLevels[c] and leaves the MASTER byte at 0xFF (F125, OBSERVED once, timed: E8b seq 26987, 26991, 26999; the content back at
  27754 / 27762), so the agreement check and the MASTER rule pass while the reported level is not the content's; how the console answers
  a matching event at 0x00 during a blackout is UNKNOWN (never sent, never captured). Block 53 blinking is the MASTER below full (F85),
  refused before by the MASTER rule. E8b listed FaderInfo and ChannelLevels before LedStatus in one list at blackout on (seq 26983) and
  LedStatus before them at blackout off (seq 27746): the gate holds a write while LedStatus is listed and unfetched, the matching event's
  own condition while FaderInfo or ChannelLevels is, and a matching level that changed with them drops it. A console that split one
  change over two lists (never seen) would leave a window of about one poll in both directions: at blackout on from the panel (our
  own tap is covered by the press rule until the LED report), and at blackout off for the panel and our own tap alike, if LedStatus
  came in one list and FaderInfo / ChannelLevels in a later one (`_togglesSent` ends at the LED report and the reports would still
  read zero; E8b's off order was LedStatus 27750, then ChannelLevels 27754 and FaderInfo 27762 in one list, which the holds cover).
  The third pass's optional closing of that window, both objects required after block 53 goes off as the bump shadow requires them
  after a release, is a listed follow-up, not made before E11b. Tests: `Channel_and_memory_faders_wait_while_BLACKOUT_is_on_and_go_once_it_reads_off_and_the_faders_are_reported`
  (E8b's in-blackout bodies: channel fader 5 and a memory fader refused, nothing queued; after E8b's blackout-off bodies the matching
  event at the content's 0xFF, then the drag), `A_fader_event_queued_before_a_blackout_is_dropped_unsent` (the reported level 0x00 before
  and during the blackout, so only this rule drops it) and `After_a_BLACKOUT_tap_of_ours_the_faders_wait_for_the_consoles_report_of_its_LED`
  (SmartSoft's BLACKOUT bytes, E9 seq 33542 / 33547); each fails with its check removed.
- *Minor B, nothing written inside an exchange.* `WriteLeftNotes` runs only at an event that ends an exchange (`EndsExchange`: an idle
  poll reply, a list, an object), never between a reply header that announces a payload and that payload (SC-1 item 8, the loop's own
  rule). `No_note_is_written_between_the_gate_and_a_writes_header_nor_inside_an_exchange` (renamed) also checks that every announcing
  reply header in the session is followed by its payload and nothing else, and that the bank key's note follows the list read after the
  key; it fails when the notes are written at every event, as before.
- *Minor C, SOLO.* A bump of ours shadows every physical fader (`BumpShadow.AllFaders`, with the same release-then-reported rule as its
  own fader; since E11b the end rule there, plus two quiet poll replies for both objects) when SOLO's LED (block 52, F100) reads anything but steady off at its press or while the shadow lasts (a panel SOLO during
  our hold; the PC sends nothing between a press and its release): a bump with SOLO on replaces the output (F6, DOCUMENTED), so the other
  faders' reports may carry it, ChannelLevels and perhaps FaderInfo as BLACKOUT (F125) and the MASTER (F176) zero FaderInfo too; INFERRED,
  unobserved (E9c's only SOLO-mode hold, header 87996, had nothing else up). Tests:
  `A_bump_of_ours_held_with_SOLO_on_keeps_every_fader_waiting_until_the_faders_are_reported_after_its_release` (E9c's SOLO-on LedStatus
  seq 87881, the hold's FaderInfo 88006 and ChannelLevels 88010, after the release 88133 / 88141 in E9c's order: channel fader 3 refused
  during the hold and after the release until both are in, then matched at 0x00; SmartSoft's bump 7 bytes, E9c seq 87997 / 88124) and
  `SOLO_coming_on_during_a_bump_of_ours_widens_its_wait_to_every_fader`; each fails with its part removed. A panel bump under SOLO is
  the owner's Acceptance 6.
- *Nit D.* docs/api.md, the fader component, the UI's state model and `DeviceStateChange` say "after a mode switch (a 1-24 / 25-48
  switch INFERRED alike)".

**Changes after E11b (the same agent, 2026-11-02, at the owner's request after his bump test; in the working tree, nothing run against
a console):**

- *The bump shadow's end.* In the owner's later E11b session (`captures/2026-11-02-e11b-second-live-control-session.backend-6.jsonl`)
  bump 5 on channel fader 5 at 0x82 was listed at its press (record 5 `ff 00 00 00`, ChannelLevels[5] 0xFF, seq 4227 / 4231) and at its
  release (0x82 again, seq 4296 / 4300), but with the fader at 0xFF the press and the release listed nothing (seq 4571 / 4609, nothing
  in 1.5 s; OBSERVED once, log-only), so the first rule (both objects listed after the release and delivered) never ended and channel
  fader 5 stayed refused. The owner rejected that. `ShadowEnds` now ends a shadow once its release has been taken and a poll reply came
  after it, and the console's latest report of record k (and, for a channel bump, ChannelLevels[c]) is the content's again: (a) reported
  after the release (listed after it and delivered) or (b) unchanged for two poll replies after the release, and reading a level other
  than the bump's (BUMPS, FaderInfo body[193]: a held bump reads it, VERIFIED at BUMPS 0xFF, F97, F174, F178; 0x81 once in memory mode,
  F178; below full in channel mode never captured, INFERRED there) or the level reported when the press was taken (the release restored
  it). A report at the bump's level that differs from the pre-press level keeps the shadow: a tap's press
  reported after its release (F168). Case (b) is the bump that changed nothing (the owner's log; every observed release that changed a
  level was reported with at most one quiet poll reply before it, SmartSoft's E9c 7, E9d 52, E9e 8 releases, ours E11 attempt 3 4,
  E11b 3, so two quiet replies leave a margin of one). Nothing listed and not yet fetched counts. The rule is INFERRED. Two readings go beyond the
  owner's wording and are for the reviewer: a "quiet" poll reply is an idle reply or a list that names neither object (F57, VERIFIED: a
  list names what changed; with a stack running PlaybackInfo is listed at every poll, E8, and idle replies alone would keep the shadow),
  counted per object and from the release rather than from the press, the level test applying in both cases; and a second press taken
  while the first bump's shadow of the same target still waits keeps the first one's pre-press levels (its bump may still show then).
  With SOLO on (`AllFaders`) both objects must also have been quiet for two poll replies, so that a tap's late press report cannot end
  the wait while the other faders may still read muted (INFERRED). Fail closed where the reports cannot tell the bump from the content
  (F178, a held bump reads as its fader at that level): a bump held on the panel at the same time keeps the bump's level in the reports
  after our release (Acceptance 6), and a content that reaches the bump's level during our hold, or just before the press and not yet
  reported (narrowed by the fourth pass's minor H), reads as the bump after the release; in both the fader waits until it is moved. Tests:
  `A_bump_that_changed_nothing_ends_its_shadow_after_two_quiet_poll_replies_and_the_drag_then_goes` (the owner's full-fader case: seq
  4506 / 4510, nothing listed; refused with one quiet reply, the drag goes after the second, matched at 0xFF),
  `A_bump_on_a_fader_at_half_ends_its_shadow_when_the_level_before_the_press_is_reported_after_the_release` (seq 4146 / 4150, 4227 /
  4231, 4296 / 4300: matched at 0x82 right after the release's report),
  `A_taps_press_reported_after_its_release_keeps_the_shadow_until_the_level_before_the_press_is_reported` (the 0xFF report after the
  release keeps it, the 0x82 one ends it), `A_fader_moved_during_our_bump_ends_the_shadow_at_its_new_level` (0x54 after the release,
  seq 4446 / 4450, matched at 0x54) and `Two_bumps_in_a_row_keep_the_level_before_the_first_press`; the E9e test and the SOLO test
  updated (the SOLO one now also refused right after both reports, before two quiet replies). Each fails with its part removed (the
  end at the release, no level test, no pre-press clause, one quiet reply, a report alone, a listed and unfetched report counted, no
  SOLO quiet, no inheritance). SmartSoft's bytes for bump 5: E9d seq 38764 / 38879 (code 0x04, memory bump 5 there: the same physical
  bump in every bank, F171, F174).

**Changes after the fourth pass (the same agent, 2026-11-02, in the working tree; nothing run against a console):**

- *Minor H, the pre-press level is the current report.* A bump's press now waits while FaderInfo (and, for a channel bump,
  ChannelLevels) is listed and not fetched (its condition in `QueueBump`), so the level its shadow keeps as the content's is the
  console's current report: a write goes before pending fetches. Where the console lists LedStatus with them the gate already kept the
  press back: it waits while LedStatus is pending, and after any object reply the write's slot comes at least 62 ms plus the
  transfers after the list's poll reply (the fetch delay, the poll after the fetch, the write after that poll, at their floors), more
  than two poll periods (at most 62 ms), so the report is stale until the next poll reply. The case the new wait closes is a list
  naming FaderInfo or ChannelLevels without LedStatus (E9d: 38 of 513 such channel-mode lists and 348 of 430 memory-mode ones; LedStatus
  first in 71 of the 475 channel-mode lists naming it, none in memory mode). The optional "no pre-press level while another target's
  shadow waits on the same physical fader" is not taken: that shadow's bump was released before the bank, mode or page change between
  the two targets, the new press waits for the FaderInfo that change lists, and a null level would lock the new target's fader when its
  content is at the bump's level. Test: `A_bump_press_waits_for_its_faders_current_report` with two list orders from the owner's later
  E11b session, both ending with the drag matched at 0xFF after two quiet replies: FaderInfo and ChannelLevels without LedStatus (list
  seq 4019: seq 4023 / 4027; the transfer log notes the wait; it fails without the wait, the press then keeping 0x82 from seq 4146 /
  4150 and the shadow never ending) and LedStatus first (list seq 9297, a move to full on the panel: seq 9301, 9309, 9313; held by the
  gate as well, so it passes either way).
- *Nits I, J, K.* "A held bump reads the BUMPS level" is given per level: VERIFIED at BUMPS 0xFF (F97, F174, F178), 0x81 once in memory
  mode (F178), never captured below full in channel mode (INFERRED there). The quiet path cites the reply count instead of
  milliseconds: every observed release that changed a level was reported with at most one quiet poll reply before it (SmartSoft's E9c
  7, E9d 52, E9e 8 releases; ours E11 attempt 3 4, E11b 3; counted again here with the same result), so two quiet replies leave a margin
  of one. The fail-closed edges are worded in full in "Changes after E11b" and in `BumpShadow`'s comment.

**Readings the reviewer must check (where the text of SC-5 was read, narrowed or extended):**

1. Class B4 says FADERS 25-48 "from the two states SmartSoft sent it in (from bank 1-24 or memory mode)"; every PC press of E9b, E9c, E9d
   and E9e came from bank 1-24, so it is sent from bank 1-24 only (a channel of bank 25-48 from memory mode is refused until FADERS 1-24).
2. Channel bump 1 of bank 1-24 (code 0x00, once, E9c S2) was named in neither addendum row and is sent like the other channel bumps;
   SC-5 addendum 3 (written after this implementation) clears it, and the owner acknowledges it at the E11b gate.
3. Condition 1 (d)'s "ChannelLevels[c] for a channel" is kept, with FaderInfo record k byte 0 required to agree, and ChannelLevels
   required in the session: the console lists only what changed, so a channel fader is refused until the console has reported its
   channels (a channel fader or bump moved on the panel does it). E11 attempt 3's W0 panel actions gave 25 lists before the first
   write and none named ChannelLevels (`captures/2026-11-01-e11-first-live-control-session-attempt-3.backend.jsonl`, before seq
   8442), so the E11b guide needs such a step before its first channel fader.
4. Matching applies to every physical fader's first event in a session and after every reported change of bank, mode or page, not only
   after an automatic switch (a panel or UI bank key re-assigns the faders too, F172); on a matched fader the event equals its level.
5. The barrier (the gate holds every write after our bank key until the console's report) and the 2 s bound extend to the UI's FADERS
   keys; FADERS 1-24 from memory mode entered from bank 25-48 is now refused (SC-2's code allowed it; SmartSoft never sent it there).
6. The MASTER at full is required for memory faders too, and since the re-check BLACKOUT steady off for channel and memory faders;
   NEXT mode refuses every control; memory mode with the bank flag set refuses every bank-dependent control. All of them
   only refuse.
7. A bump of another bank starts the automatic switch, then the hold (SmartSoft's own sequence in E9d B and E9e: FADERS 25-48, then the
   bump); SC-5 describes the switch for fader events.
8. The hold bound is measured header to header and asked for at 2.75 s; a console answering polls 170–500 ms late (never observed)
   could carry a hold past 3 s, which is logged, not prevented. While a bump is held, the other faders' events wait for its release
   (nothing goes between a press and its release, as SmartSoft); its own fader's are refused (first pass, major 1).
9. The UI's memory fader thumb shows the user's request for up to 1.5 s before the report wins again (unchanged); the device's matching
   event keeps the first event at the reported level regardless.

Run line (E11b, on the owner's go only, after the reviewer's pass): unchanged, `dotnet run --project src/backend/SmartFade.Api --
--Device:Mode=Usb --Device:Usb:ConfirmDate=<today> --Device:Usb:LiveControlDate=<today>`.

### Review (SC-5 item 10)

**First pass (2026-11-02, `protocol-safety-reviewer`, over 98cb499 and the docs of 72e4d6d): NEEDS CHANGES, one blocker and three
majors; every byte from the whitelist, no NO-GO key, invented byte, retry or firmware path reachable, the read-only session and the
simulator unchanged, the golden replays reproduced count for count by the reviewer's own port** (build 0 warnings, 421 tests green).
The code fixes went to the Opus 5.5 subagent that wrote the implementation (CLAUDE.md), the decisions to the owner, the docs to the
coordinating assistant; the fixes were made the same afternoon (428 backend and 308 frontend tests green, the subagent broke each new
guard on purpose and saw its test fail), and nothing goes to E11b before the re-check. Findings and what was done:

| # | Finding | Status |
|---|---|---|
| BLOCKER | The matching-level comments (`UsbSmartFadeDevice.cs` MatchingLevel, the class `[Evidence]`, `ConsoleStateAssembler.cs`) state as fact, without a status word, that FaderInfo record k byte 0 is the physical fader's own level and that an agreeing ChannelLevels[c] means no other source drives the channel, and call the matching event VERIFIED; F174 contradicts the reading for a bump, and F172 is VERIFIED for its mode case only (the page case OBSERVED once, the bank case 1-24 / 25-48 in none of its cases). | **Fixed 2026-11-02 by the subagent, waits for the re-check:** tags and status words only: the class `[Evidence]` (`UsbSmartFadeDevice.cs:41-43`) now three tags (F171 / F174 / F175 VERIFIED as SmartSoft's events and the console's answers; F172 per case, (a) VERIFIED in part, (b) and (c) OBSERVED once, the 1-24 / 25-48 switch in none; "no other source" INFERRED for a memory or the stack, contradicted for a held bump), the `MatchingLevel` doc (`:992-1005`) and `ConsoleStateAssembler.FaderInfo` (`:107-116`, byte 0 OBSERVED, carries a held bump) likewise; our own host's matching event unobserved until E11b. |
| MAJOR 1 | The matching level can be a held bump's level: during a bump hold on physical fader k, and after our own channel-bump release when the first reply lists neither FaderInfo nor ChannelLevels (9 of 16 holds in E9c / E9d / E9e), the matching event and the UI's drag carry the bump's level; if the console takes the fader as matched, the content keeps the bump level when the bump ends (the jump SC-2 condition 6 and SC-5 condition 1 (d) exist to prevent). | **(i) Fixed 2026-11-02 by the subagent, waits for the re-check:** `BumpShadow` (`UsbSmartFadeDevice.cs:740-779`): a bump pressed by this host blocks its physical fader from the press until FaderInfo has been listed again after the release and delivered (a channel bump needs ChannelLevels too), checked at the command (`:1046`) and again before the write (`Recheck`, `RecheckMatching`, `CompleteSwitch` through `FaderState`, so a queued event is dropped); fail closed: a bump that changes neither report (content already at full) keeps its fader refused until both objects are listed again. Tests `A_fader_waits_after_its_own_bump_until_the_console_has_reported_it_again_after_the_release` (`UsbSmartFadeDeviceTests.cs:1906`, the stand-in console with E9e's in-hold bodies seq 17675 / 17679, PlaybackInfo 17789 alone after the release, FaderInfo 17796 alone, then ChannelLevels 17800) and `A_fader_event_queued_behind_a_press_of_its_own_bump_is_dropped_unsent` (`:1965`). (iii) condition 1 (d) and SC-2 condition 6 annotated with F174 (done). **(ii) The panel case:** not detectable on the device side as far as the review saw; the owner left the decision to the coordinating assistant on 2026-11-02 (chat: "I will leave that on you"), which first had the `capture-analyst` (Opus 5.5, read-only, the same afternoon) compare a held bump against a fader at full in every object of the existing captures: **not distinguishable in any observed object at BUMPS 0xFF** (F178: 19 of 19 channel holds and 57 of 57 memory holds, panel and PC, read as fader k at 0xFF with the same content in FaderInfo, ChannelLevels, OutputLevels, LedStatus and DisplayContent, in both directions, across E3, E3b, E8, E8b, E9, E9b, E9c, E9d, E9e and E11 attempt 3; ConsoleStatus is not listed at a press, no other LED block or LCD cell changes, no object marks the press). **Decision (the coordinating assistant, 2026-11-02, at the owner's delegation): the residual risk is accepted, not refused in code**, on these grounds: it is live and visible, changes no show data, ends with the next move of that fader (F141), is never exercised by E11b's steps, and the console's answer to a matching event at a bump's level is UNKNOWN; the owner acknowledges it at the E11b gate (Acceptance 6), and it is revisited if it ever shows. A one-off passive panel capture could still look for a marker under conditions never captured (`reverse-engineering.md` "Next capture (proposed by the SC-5 review, 2026-11-02)"). E11b's own steps never move a fader during or right after a bump of the same physical fader. |
| MAJOR 2 | A synchronous transfer-log write with flush and a logger call run between the gate's freshness check and the Data header for every bank key press and every matching event (`SwitchKeySent`, `MatchingSent` → `SessionNote` inside the session's `Sent` callback); SC-2 MAJOR 1 was closed on "no await, I/O or log note" there and the 62 ms analysis depends on it. | **Fixed 2026-11-02 by the subagent, waits for the re-check:** `SwitchKeySent` (`:1174`) and `MatchingSent` (`:1117`) change state and `LeaveNote` under the lock only; `WriteLeftNotes` (`:2140`, at the top of `OnEvent`) writes the notes after the payload, pending notes before the log closes (`:1706`). Test `No_note_is_written_between_the_gate_and_a_writes_header` (`UsbSmartFadeDeviceTests.cs:1993`: the pipe, the sink and the slot waits in one ordered list; for each of the four Data headers of the channel-30 switch no note between the slot wait and the header, both notes after their payloads). |
| MAJOR 3 | Condition 1 (e) and item 6's display half not built: only the bank reaches the contract; the UI's bump is lit from the output level, the console's unmatched blink (F172: block k's two triples differ, `46 00 00 / 0A 00 00`) is shown nowhere. | **Fixed 2026-11-02 by the subagent, waits for the re-check, as a narrower reading:** the captures show the blink as two different phases of a LedStatus block (F85, F116; E9c S4 seq 51473, 67160 `46 00 00 / 0A 00 00`, 59535 `00 46 00 / 00 0A 00` for unmatched faders), but also for a fader holding another page's memory (seq 80317), once for fader 2 moved on the panel (seq 76674, 77680, unexplained) and in the page view's first LED report (seq 47749, 80267, 84586), so the mirror is named `BlinkingFaders`, not "unmatched": the assembler reports LED blocks 1–24 whose phases differ (`ConsoleStateAssembler.cs:143`, `ReportBlinking` `:452`; empty while the LCD shows the page view or a record screen), through `DeviceStateChange`, `ConsoleState`, the DTO (`docs/api.md` `blinkingFaders`) to the UI (channel k or 24 + k by `faderBank`, memory fader k on the console's page; display only); golden test over all 19 changes of E9c seq 868–97136 (`ConsoleStateAssemblerTests.cs:292`); a pre-existing gap fixed on the way (`ConsoleService.cs:369`: a bank change alone reached no subscriber). |
| MINOR 1 | Channel bump 1 of bank 1-24 was sent by the code before any row cleared it; addendum 3 (written after the implementation) clears it, the E11b gate did not name it, reading 2 still said "to be confirmed". | **Done 2026-11-02:** a gate line in `e11b-guided-session.ps1` (the owner acknowledges addendum 3), reading 2 updated by the subagent. |
| MINOR 2 | SC-5 clears no hold longer than 3 s; the loop asks for the release at 2.75 s and can send it only after the next completed exchange, so a console replying near the 500 ms timeout could carry the release to about 3.8 s; logged, not prevented (reading 8); every reply in every capture came within 12 ms. | **Accepted by the owner 2026-11-02 (chat, after the first pass; "every reply came within 12 ms"): the logged overrun stands, no lead change; the guide's Acceptance 7 confirms it on the day. |
| MINOR 3 | `ChannelPosition` computes the physical fader as `channel − 24` while the docs say "no arithmetic" (each of the 48 pairs is observed, F171). | **Fixed 2026-11-02 by the subagent:** `ChannelPosition` (`UsbSmartFadeDevice.cs:841`) an explicit 48-row table; golden test `Every_channel_sits_on_the_bank_and_physical_fader_E9d_moved_it_by` (`UsbSmartFadeDeviceTests.cs:2057`) derives every row from E9d's two passes. |
| MINOR 4 | `NotIn`'s own refusals (record byte 1 for a channel, byte 3 for a memory) pass when FaderInfo has not arrived; faders are protected by `FadersUnreported`, `PressBump` is not. | **Fixed 2026-11-02 by the subagent:** `PressBump` (`:1291-1301`) checks `FadersUnreported()` first; test `A_bump_needs_the_consoles_FaderInfo_as_a_fader_does` (`:2036`). |
| MINOR 5 | The `[Evidence]` of `ChannelLevelsObject` and `FaderInfoObject` still says OBSERVED for a few channels and faders while the device picks a transmitted value from them. | **Fixed 2026-11-02 by the subagent:** `SimpleObjects.cs:141-142` (ChannelLevels: F67, F171, F174 VERIFIED; F83, F96, F176 OBSERVED) and `:176-177` (FaderInfo: F82 in part, F171, F174 VERIFIED; F95, F96, F97, F99, F112, F176 OBSERVED). |
| NIT | "at most 3 s" in docs/api.md and CLAUDE.md overstates the bound; "refuses every control with the MASTER below full" in architecture.md too broad; Bump10's evidence note mixes modes; the E9c replay range holds two NEXT-mode fader events the device would refuse; the "FADERS … sent" note is written before the header. | **Fixed 2026-11-02** (CLAUDE.md by the assistant; `docs/api.md:191`, `docs/architecture.md:252-256`, `HspOutbound.cs:137`, `HspLiveSessionTests.cs:371-374` by the subagent; the last with MAJOR 2). |

Confirmed correct by the reviewer (file-and-line in its report): the enums exactly SC-2 plus SC-5; `PhysicalControls` two explicit
24-row tables; `DataHeader` the only producer of a Data header; the whitelist test reading every value from capture byte 5; the NO-GO
payloads (E9 73006, E9b 47230, E9c 93437, 94597, 102688), the System write, ChannelText and Memory refused; 0x32, 0x38, 0x3A, 0x3B,
0x3E, 0x41, 0x42 unrepresentable; `QueueKeyHold` bump codes only; the per-member evidence by the reviewer's own count (bump 1 of bank
1-24 E9c 29262, bump 10 E9c 86727, the E9e codes in both sessions, 0x37 in four captures always from bank 1-24, 0x47 / 0x48 in E9b
and E9c from bank 1-24, every index in all three states in both E9d sessions); `BankKeyNotFrom` matching every observed origin; the
switch (refusals, tap, wait, the 2 s bound with a wide margin: ConsoleStatus and LedStatus listed 17–80 ms after every SmartSoft bank
key, delivered within 177 ms, FaderInfo in the same list whenever it changed; the barrier); the previous byte per index never reset;
the holds (released once, at the bound, before a Disconnect, on a bank or page change; a switch or a second bump refused while one is
held; the fault release and the latch unchanged); the 62 ms gate for every new write but MAJOR 2; no retries; the read-only session,
the simulator and the mode gating unchanged; every replay count and skip reproduced and each skip by a rule (condition 8's first
steps, the STACK / PAUSE / NEXT writes); the E11b guide's W0 (a panel move of a channel fader listed ChannelLevels with FaderInfo in
E8 18 of 18, E8b 18 of 18, E3 42 of 42) and its W-steps; the layering (no bytes in the UI).

**Second pass (re-check, 2026-11-02, afternoon, `protocol-safety-reviewer`, over 1eceabb and 88b9fcd): every first-pass finding
RESOLVED or accepted as recorded, with file-and-line evidence (the BLOCKER's three tags; MAJOR 1 (i)'s `BumpShadow` created in
`QueueBump`, checked at the command and before every write, ended only by a list after a poll reply after the release, E9e bodies in
its tests; MAJOR 1 (ii) accepted at the owner's delegation, the reviewer's own spot check agreeing with F178; MAJOR 2's window clean,
the `sent` callbacks state-only; MAJOR 3's `BlinkingFaders` display only, every reading with its status; the minors and nits as
listed; the stand-in console's pause fix removed no assertion); the invariants re-confirmed (the whitelist exactly SC-2 plus SC-5,
`DataHeader` the only producer, `HspReadSession.cs` and `HspReadLoop.cs` untouched, no NO-GO key reachable, holds bump codes only, no
retries, nothing between the gate and a header for any write class, the read-only session, the simulator, Communication and Domain
unchanged, the UI domain-level only). NEEDS CHANGES for one new major, two new minors and three nits (428 tests green); the fixes
were made the same afternoon (433 backend and 308 frontend tests, each new guard removed on purpose by the subagent and its test seen
to fail), and the third pass is the gate:**

| # | Finding | Status |
|---|---|---|
| MAJOR (new) | **The matching level reads 0x00 during a blackout:** BLACKOUT zeroes FaderInfo record k byte 0 and ChannelLevels[c] and leaves the MASTER byte at 0xFF (F125; E8b seq 26987, 26991, 26999, the content restored at 27754 / 27762), so `MasterNotAtFull`, the agreement check and the shadow pass and `MatchingLevel` returns 0x00 whatever the content; touching a fader in the UI during a blackout sends a matching event at 0x00 (the drag starts at the displayed 0); if the console takes the fader as matched, the content drops and at blackout off the channel stays dark or at the user's level. Reachable from our own BLACKOUT tap or the panel; detectable, LED block 53 is decoded as `Keys.Blackout`. E11b's steps never toggle BLACKOUT. | **RESOLVED (third pass, 2026-11-02):** `BlackoutNotOff` (`UsbSmartFadeDevice.cs:1578`) refuses channel and memory fader events while block 53 is not steady off, inside `FaderState` (`:822`: the matching event, the drag and the faders waiting on a switch) and at the command (`:1069`: no switch starts during a blackout); fader events are refused too from a BLACKOUT tap of ours until the console reports block 53 again (E8b listed FaderInfo and ChannelLevels before LedStatus in the same list, seq 26983); tag `:46` F125 OBSERVED once, F114 VERIFIED for the LED, the console's answer to a matching event at 0x00 during a blackout UNKNOWN; residual (stated): a panel blackout split over two lists, never seen, would leave about one poll. Tests `UsbSmartFadeDeviceTests.cs:2079` (E8b's in-blackout bodies refuse channel fader 5 and a memory fader; the blackout-off bodies 27750 / 27754 / 27762 let the matching event go at the content's 0xFF, then the drag), `:2118` (an event queued before a blackout dropped unsent), `:2147` (after our own BLACKOUT tap, E9 seq 33542 / 33547, the faders wait for the LED report). Condition 1 (d) and SC-2 condition 6 annotated (F125 at its status after the subagent's correction of the annotation). |
| MINOR B (new) | The deferred notes are written inside the next exchange: `WriteLeftNotes` runs at the top of `OnEvent` for every event, a payload-announcing reply header included, so after a bank key the "FADERS … sent" note is written between the next list's header and its payload (SC-1 item 8; the loop's own doc); the test at `:1993` does not check that spot. | **RESOLVED (third pass):** the left notes are written only at events that end an exchange (`EndsExchange` `:1980`, used at `:1879`: an idle poll reply, a list, an object); the test renamed `No_note_is_written_between_the_gate_and_a_writes_header_nor_inside_an_exchange` (`:2010`) also checks that every payload-announcing reply header is followed by its payload and nothing else (`:2053`) and that the bank key's note comes right after the list read after the key. |
| MINOR C (new) | SOLO with a held bump: F6 (DOCUMENTED) says a bump with SOLO on replaces the output, so the other channels read 0; whether FaderInfo of the other faders drops too is unobserved (E9c's only SOLO-mode hold had nothing else up); if it does, as for BLACKOUT (F125) and the MASTER (F176), the agreement check passes at 0 for every other fader, our own SOLO hold leaves them unshadowed, and Acceptance 6 names only the same fader. | **RESOLVED (third pass):** `BumpShadow.AllFaders` (`:775`): a bump of ours shadows every physical fader with the same release-then-reported rule when SOLO's LED (block 52) reads anything but steady off at the press (`:1351`) or comes on during the hold (`:2035`); tag `:47` INFERRED from F6 (DOCUMENTED), F125 and F176, unobserved; tests `:2184` (E9c's SOLO-on LedStatus 87881, the hold's 88006 / 88010, 88133 / 88141 after the release: channel fader 3 refused until both objects are in, then matched at 0x00) and `:2233` (SOLO coming on during a bump of ours). **Acceptance 6 widened** to the other faders under SOLO for the panel case (the guide and this section). |
| NIT D | "after a bank or mode switch" in `docs/api.md` and `channel-fader.component.ts`: F172 has no 1-24 / 25-48 case. | **RESOLVED (third pass):** `docs/api.md:142`, `channel-fader.component.ts:70`, `:121`, `console.models.ts:140`, `DeviceStateChange.cs:15-16`. |
| NIT E | F178 rests on the capture-analyst's uncommitted scripts; the seqs it lists make it reproducible and the reviewer's spot check agrees. | Noted; no change (the record of the scripts' location is in F178). |
| NIT F | Acceptance 3 and "Conditions for E11b" said "released by the device at 3 s at the latest", which Acceptance 7 and MINOR 2 qualify. | **Done 2026-11-02:** "asked for 2.75 s after the press (Acceptance 7 for the overrun)". |

**Third pass (2026-11-02, afternoon, `protocol-safety-reviewer`, over 0f2f1da and 9092a3f): PASS.** The second pass's major and
minors RESOLVED with file-and-line evidence and every cited seq confirmed against the captures (`BlackoutNotOff` at `:1578` inside
`FaderState` `:822` and at the command `:1069`, our own BLACKOUT tap kept unreported from `:1497` to the LED report `:2066`, cleared at
Connect `:310`; `EndsExchange` `:1980` at `:1879`; `BumpShadow.AllFaders` `:775` from `SoloNotOff()` `:1351`, widened at `:2035`; the
wording at `docs/api.md:142` and the four code sites; Acceptance 3 and 6 and the conditions in line; every file:line claim of this
table matching the code); the invariants re-confirmed (nothing in `SmartFade.Protocol` changed since 1eceabb, `HspReadSession.cs` and
`HspReadLoop.cs` untouched since 98cb499, the whitelist exactly SC-2 plus SC-5, `DataHeader` the only producer, no NO-GO key reachable,
no retries, the `sent` callbacks state-only, the read-only session unchanged, the UI domain-level); build 0 warnings, 433 backend tests.
One optional nit G (the blackout residual worded for one direction only: reworded above in the Implementation section for both; the
hardening, both objects required after block 53 goes off, is a follow-up). Two fail-closed behaviours the owner should know: if the
console never reports the LED change after our BLACKOUT tap, every fader stays refused until a reconnect (the next BLACKOUT toggle waits
the same way); with SOLO on, a channel bump of ours that changes neither report blocks every fader until a panel move lists both
objects. **Nothing on the review side is open before E11b; the review clears nothing for sending.** What remains is the owner's, as
"Conditions for E11b" lists it: the SD backup and its name, USBPcap before the Zadig bind, the versions, Acceptances 1 to 7 and the rig
answer at the gate, no SD save or load while connected, and the go on the day.

**Fourth pass (2026-11-02, evening, `protocol-safety-reviewer`, over 1144940: the bump shadow's end rule the owner asked for after
using the software): PASS**, no blocker, no major. Confirmed: `ShadowEnds` (`UsbSmartFadeDevice.cs:823`) cannot end a shadow on a
report at the bump's level unless the level before the press equalled it; the release must be taken and a poll reply seen after it,
lists before the release not counted (a tap's late press report, test `:2077`); BUMPS body[193] and record k come from the same
FaderInfo snapshot; in every captured bump release that changed a level (E9c 7, E9d 52, E9e 8, E11 attempt 3 4, E11b 3) at most one
quiet reply came before FaderInfo or ChannelLevels was listed, never two (in our own host up to 112 ms: bump 7's release in E11b, the
second reply); a second press on the same target inherits the waiting shadow's pre-press values (`:1417`); `PressSent` memory-only under
the gate (`:1409-1423`); the per-object quiet count rests on F57; the whitelist, `DataHeader`, `HspReadSession.cs`, `HspReadLoop.cs`, the
read-only session, the simulator and the other interlocks untouched; every test byte from a committed capture (backend-6 seqs 4146 /
4150, 4227 / 4231, 4296 / 4300, 4446 / 4450, 4506 / 4510; E9d 38764 / 38879, 11362; the owner's two cases confirmed: pseq 29 / 30 listed
after one idle reply, pseq 38 / 39 six idle replies and nothing listed). One minor and three nits, fixed the same evening by the
subagent (442 backend tests) and **confirmed by the reviewer the same evening (over 0909c84): RESOLVED, nothing open on the
review side for SC-5 item 10; the pass clears nothing for sending**:

| # | Finding | Status |
|---|---|---|
| MINOR H | The pre-press value can be stale: the bump press's condition did not hold while FaderInfo or ChannelLevels was listed and unfetched (`:1409`); reachable when such a list carries no LedStatus (the subagent's recount of E9d: 38 of 513 channel-mode lists and 348 of 430 memory-mode lists; with LedStatus first the gate already held the press). Safe direction: a fader moved to the bump's level within one poll before our bump keeps its shadow (the lockout, rarely); the unsafe direction would also need two quiet replies before the release's report, never observed. | **RESOLVED (confirmed over 0909c84):** the press holds at its slot while FaderInfo (and ChannelLevels for a channel bump) is listed and unfetched (`QueueBump` `:1421`, the log notes "…bump 5's press waits for the level its fader reports now"); test `A_bump_press_waits_for_its_faders_current_report` (`UsbSmartFadeDeviceTests.cs:2217`, both list orders from backend-6: seq 4019 / 4023 / 4027 without LedStatus, which fails without the wait, and 9297 / 9301 / 9309 / 9313 with LedStatus first). The optional null pre-press value was not taken: it would lock the new target's fader whenever its content is at the bump's level. |
| NIT I | "a held bump reads body[193]" tagged VERIFIED holds at BUMPS 0xFF only (F97; F178 has 0x81 once, memory mode; channel mode below full never captured). | **Fixed:** per BUMPS level in the tag (`:46`), the `BumpShadow` doc (`:747-750`), `ShadowEnds` and "Changes after E11b" (VERIFIED at 0xFF, 0x81 once, INFERRED below full in channel mode). |
| NIT J | The quiet path's docs cited "listed within 16-67 ms"; the bound that matters is the reply count (at most one quiet reply before the release's report in every observed release; up to 112 ms in our host). | **Fixed:** the reply count cited (`:824`, the tag, "Changes after E11b"), recounted by the subagent with the same result. |
| NIT K | "the fail-closed edge remains only while a bump is held on the panel" was incomplete: also when the content reaches the bump's level during our hold or just before the press; both fail closed, inherent to F178. | **Fixed:** both edges named (`:757`, "Changes after E11b"); the second narrowed by MINOR H's wait. |

### Conditions for E11b (the second write session, the first with SC-5's classes) and what to record

- Backup to the SD card first, the name in the log; USBPcap from before the Zadig bind; the guided script logs every owner action
  with the time; firmware, Windows build, cable, backend commit recorded. No SD save or load on the console while our software is
  connected (F163 / H109, as for E11).
- The `protocol-safety-reviewer` pass over the SC-5 implementation closed (item 10): every blocker and major fixed, the minors fixed or
  accepted in the SC-5 review table; the guide refuses to start the backend before.
- The owner gives the go on the day after reading SC-5, its addenda and the "Implementation" section, and accepts in writing (the guide
  asks each): the automatic switch presses the console's FADERS key, so the console's 24 physical faders control the new bank for anyone
  at the console until the next switch (visible on the FADERS LEDs, shown by the UI); the matching rule (F172: after a switch the console
  ignores a PC fader until an event equals the new content's level; the device sends the reported level first; a wrong first event moves
  nothing and the bump LED blinks); the bounded hold (the release asked for 2.75 s after the press on the device's clock, or before any
  Disconnect; MINOR 2 for the overrun) with the
  lost-release residual risk accepted on 2026-11-02; the last mover (F141) and MASTER's whole-output effect; the rig (memory 3 of page 1
  and channels 1, 2, 5 and 30 may light on whatever is patched to them; IND 1 is unpatched and IND 2 drives DMX 47 in this show, E8b);
  and, since the first review pass: addendum 3 (channel bump 1 of bank 1-24), the residual risk of a bump held on the panel while the
  UI moves the same fader (review MAJOR 1 (ii): not detectable, F178; the console's answer to a matching event at a bump's level
  UNKNOWN; accepted by the coordinating assistant at the owner's delegation on 2026-11-02, acknowledged at the gate; the guide's own
  steps never do it) and the hold overrun with a slow console (review MINOR 2: the release asked at 2.75 s goes at the next
  write slot, about 3.8 s at worst with replies near the 500 ms timeout, never seen, logged; accepted by the owner on 2026-11-02 in chat, confirmed at the gate).
- Script (`tools/usb-probe/e11b-guided-session.ps1`, written 2026-11-02 from the E11 one, parse-checked, not run), read-only first:
  connect, idle 60 s, W0 from the panel (FADERS MEMS, FADERS 1-24, SOLO on and off, the MASTER down and back, and channel fader 1 up a
  little and back to zero, so that the console lists ChannelLevels: the implementation refuses a channel fader until the console has
  reported its channels (reading 3), and E11 attempt 3's W0 never made it) until the UI shows the LEDs, the MASTER, channel mode and
  channel 1 at zero. Then, one at a time, 10 s between steps, the console's answer asked each time: W1 channel fader 1 to about 20 % and
  back (the SC-2 sanity check with attempt 2's three-case question: refused = not sent, W0 once more, a second refusal ends the session
  without a write; sent and not followed = the H66 stop); W2 channel fader 5 (a new A2 index) and back; W3 channel fader 30 (the
  automatic switch to bank 25-48, B4 then A3: the console's FADERS 25-48 LED, the LCD rows 25 / 37, then the first event at channel 30's
  reported level, then the drag) and back; W4 channel fader 2 (the switch back to 1-24) and back; W5 memory fader 3 of page 1 (the
  switch to memory mode, B4 then A4) and back; W6 memory bump 1 held about 2 s (B3, in memory mode); W7 channel bump 7 held about 2 s
  (the switch back to 1-24, then B3); W8 IND 1 on / off and IND 2 on / off (B5, optional); W9 the FADERS 25-48 and FADERS 1-24 keys as
  taps from the UI (B4); W10 MASTER to about half and back (optional, last); Disconnect at an idle point.
- Abort rules as E11, plus: a switch the console does not report within the bound ends the session (a deviation; no retry); a fader the
  console ignores after a switch (the bump LED blinking, no level change within two poll periods) ends it; a bump reported as still down
  after its release is pressed on the panel only after reading the log; a release that could not be sent leaves `INCOMPLETE-WRITE.txt`
  and nothing reconnects before the owner has read the log and removed the file.
- Record: every write with its `pseq` and the console's answer; for each switch the key pair, the ConsoleStatus (body[0], body[4]) and
  LedStatus reports with the time from the key's release to the report, the first fader event against the reported level and whether
  the console followed it (FaderInfo, ChannelLevels, OutputLevels, the LED block); for each hold the press-to-release time and the
  release's `pseq`; the bus against the backend's log byte for byte (as E8b); any deviation and the time it ended the session; the
  physical fader positions at the end. Afterwards an SD save, compared with the backup byte for byte (no show data may have changed).
- What E11b answers: the switch sequence and F172's matching rule from our own host, F174 (a held bump) from our host, the bank keys and
  IND 1 / IND 2 from our host (OBSERVED once if the console follows; VERIFIED needs a second session of ours). **E11b clears nothing by
  itself:** its outcome is recorded in its capture record and here, and SC-5's decisions stand or are annotated.

**Run 2026-11-02, 14:53–15:17 ([E11b record](captures/2026-11-02-e11b-second-live-control-session.md)):** the backend and the guide at 4aa02b2, every acceptance answered y,
the console power-cycled before, backup `SHOW30.ASC`, USBPcap before the Zadig bind; W0–W10 to the end, **117 ConsoleEvents** (93 fader
events, 12 press / release pairs) each 25–51 ms after a poll's reply (the 62 ms bound 117 of 117), and **the console followed every
control**: the automatic switch four times for a control and twice from the UI's keys (ConsoleStatus and the LEDs reported, the switch
done 129–176 ms after the key: condition 1 (c)'s 2 s bound with a wide margin; the matching event at the reported level, 0x00 in every
case, listing nothing; the drag applied to the new bank's content), the two holds (2.48 and 2.26 s, released by the UI; no bound release,
no overrun), IND 1 / IND 2, the bank keys, the MASTER; 0 framing errors, the bus = the backend's log byte for byte, `SHOW31.ASC` =
`SHOW30.ASC`; no refusal, no fault, no `INCOMPLETE-WRITE.txt`. One deviation by the owner (two panel presses before W5, the UI's
memory hint misread; no data changed; a UI follow-up). F179 and F180 OBSERVED once for our host, F167–F169 VERIFIED in part, F171 /
F174 / F175 / F115 annotated, H66 CONFIRMED for SC-5's classes. **E11b clears nothing by itself; SC-5's decisions and conditions stand**
(the unmatched case, the SOLO case, the blackout refusal and the hold bound were not exercised).

### What SC-5 does not clear

A fader event in a bank the console has not reported; a page change from the PC; NEXT, STACK, PAUSE, CLEAR; any bump not named above or in
the addenda as a hold; a hold longer than 3 s; the record keys (SC-3); ChannelText, Memory, the System pair (SC-4, not decided); anything in class F.
Nothing is cleared for sending before the owner implements the sequences, the reviewer passes them and the owner gives the go for E11b.

## SC-4 (2026-11-02, evening): the console's state at Connect through SmartSoft's start-up pair

**Question.** May our software send, once per session at Connect, the two System messages (id 39) SmartSoft sends at the start
of every Online session, `09 00 00` then `07 00 00`, so that the console lists its version and then its whole state and show (the
157–161-record download) as it does for SmartSoft, and the UI shows the console's mode, bank, page, key LEDs, levels, independents,
file name and show data without anyone touching the panel? The owner asked for exactly this on 2026-11-02 ("I do not want to press
something / move something just to get rid of 'Not reported by the console yet'"). Input: the SC-4 groundwork of the same evening
(F181–F186, H113–H116, `smartsoft-analysis.md` S11.17, `commands.md` "The System message (ID 39)"), SC-2 class E, SC-5 class E.

| # | Item | Status | Evidence |
|---|---|---|---|
| 1 | Evidence status | **VERIFIED for the bytes and the console's answers; the meaning INFERRED** | F181: the two payloads `00 03 00 27 09 00 00` and `01 03 00 27 07 00 00` byte-identical in 20 of 20 SmartSoft sessions across 10 captures, the only System writes of a session, its first two data writes; F182: after `09` the console lists ConsoleVersion at the next poll (16–22 ms; body `0C 33 2E 30 2E 32 2E 39 2E 30 2E 34 33` in 20 of 20), after `07` the download at the next poll (17–24 ms), 157–161 records in a fixed order, 792–812 bytes, fully fetched in 7.2–7.6 s, no other list and no write during it, the counter +1 per payload, no acknowledgement; F57 / F94 / F157: the download's records decoded and cross-checked with the console's own SHOW files; H30 and S11.17: SmartSoft sends `09` at reply header #40 and `07` at #79 from two static countdowns, `07` only if the ConsoleVersion equals "3.0.2.9.0.43" (INFERRED, high); the names of 0x09 and 0x07 UNKNOWN (ConsoleLink: "version query", "catalog request"). |
| 2 | Side effects understood | **Yes for what the captures can show; the rest UNKNOWN and bounded** | F184: in 9 sessions every live object delivered before the pair came back byte-identical in the download, a running sequence kept stepping across both writes, and every SD comparison is identical (10 pairs, among them SHOW04 = SHOW05 = SHOW07 across five sessions whose only data writes were the pair). The only effect ever observed is the listing. The erase and show-upload values of the same message (S11.17: 0, 1, 6, 10–16; F121) are one body byte away: **the encoder represents 0x09 and 0x07 and nothing else.** What 0x09 / 0x07 change inside the console beyond the listings, a cut-short download's remainder (H116) and the states never seen at a pair (F185) are UNKNOWN. |
| 3 | Backup | **Condition** | An SD save before E13 (the first session with the pair), its name in the log; an SD save after, byte-identical. |
| 4 | Rig | **Not affected** | The pair changed no level, LED or output in 20 of 20 sessions (F184); the rig rule of SC-1 / SC-2 stands for the controls that may follow. |
| 5 | Firmware path untouched | **Yes** | Bulk transfers on interface 1 only; System values 0 / 1 / 6 (the show-upload framing) and 10–16 unrepresentable; SFDownloader, DPInst, the self-test and the HID interface untouched. |
| 6 | Mode gating | **Condition (to build)** | A separate per-day switch `Device:Usb:StartupPairDate` (today's date, like `ConfirmDate` and `LiveControlDate`), so that SC-1's read-only session stays what SC-1 cleared (0 writes) unless the owner enables the pair deliberately; usable with and without live control; the UI shows "STATE AT CONNECT" beside HARDWARE while the switch is on and "downloading the console's state" during the download. |
| 7 | No blind retries | **Condition (to build)** | Exactly one pair per session (per Connect; F181, H113), never resent, not after a reconnect inside the same process without a new Connect; a header or payload that did not complete sets SC-2's incomplete-write latch (`INCOMPLETE-WRITE.txt`) as for any write. |
| 8 | Logging on | **Yes, plus USBPcap for E13** | The transfer log always; the bus for the first session. |
| 9 | Versions recorded | **Condition (to build): the version gate** | `07 00 00` goes only after ConsoleVersion was listed, fetched and read as "3.0.2.9.0.43" (F59; SmartSoft's own gate, S11.17); any other version, or no ConsoleVersion within two poll exchanges after `09`, ends the pair (no `07`), the session continues as a read session and the transfer log says why. The firmware, Windows build and backend commit recorded for E13 as for E11. |
| 10 | Review done | **Condition** | `protocol-safety-reviewer` over the encoder (two bodies), the placement, the version gate, the download handling (the bounds raised to what 20 downloads showed), the refusal of controls during the download, the switch and the golden tests, before E13; its findings fixed or recorded in a review table here. |
| 11 | Passive first | **Yes** | 20 passive observations of SmartSoft producing the pair and the console answering (F181, F182); E13 is the first time our host sends it. |

### Decision: GO (conditional) for class E as bounded below; NO-GO for every other System value, for ever (F121)

Cleared for building, reviewing and one first session E13 on the owner's go, nothing else:

1. **The pair, verbatim and once.** The session's first two data writes are `09 00 00` and `07 00 00` (id 39, pseq 0 and 1, the
   header built as every Data header of ours, F131 / F52), in that order, sent once per Connect; `09` after at least one completed
   poll exchange with nothing listed and unfetched (SC-2's gate), `07` only after the version gate of item 9 and one more completed
   poll exchange, with no other write between them (as SmartSoft: only the ConsoleVersion list and object came between, F182).
2. **The download is taken whole, and nothing is sent during it.** The read session's bounds are raised to what the 20 downloads showed
   (a list payload up to 812 bytes, F182; every record body length seen in those downloads and in E12 / E12b, with the golden replay of
   all 20 downloads through the session as the proof) and no further: an unseen length still ends the session with a fault, the safe
   stop of SC-1. From `07` until the download's last record is delivered (ChannelLevels, the fixed order of F182) every control is refused
   ("the console's state is being downloaded"); the blind window is 7.2–7.6 s (F182). A fault inside the download ends the session; the
   remainder of a cut download is UNKNOWN (H116): the recovery is a SmartSoft Online start, whose own pair and download consume whatever
   is pending (F181 / F182), or a session of ours that faults again until it does; never a retry of the pair by our code.
3. **The console's state before the pair is the operator's responsibility, as it is SmartSoft's.** SmartSoft sends the pair blind at
   header #40 (S11.17) and the captures show it in channel mode page 1 bank 1-24 (16), memory mode (4), with SOLO lit and the MASTER
   below full, with STACK lit and with a sequence running (F185), never in bank 25-48, NEXT, BLACKOUT, with a record function armed, a
   bump held, a GO fade running, a menu open or an SD operation under way. Our software cannot know the state before the pair (that is
   the point of it), so: the UI's Connect says what the console must not be doing (no menu, no SD save or load, no record function armed,
   no bump held), the device refuses the pair if anything already reported shows NEXT, BLACKOUT, a record function armed or bank 25-48
   (a pending list at Connect, F177 / H65), and the owner accepts in writing the residual: the pair sent in a state SmartSoft was never
   seen in, with the console's answer UNKNOWN (the only observed effect of the pair being a listing, and the erase values unrepresentable).
4. **What the pair does not give, and the MASTER probe.** The download never carries FaderInfo (the MASTER and BUMPS levels),
   DisplayContent or PlaybackInfo (F182, F186), and SC-2's condition that the MASTER be reported before a channel or memory fader
   moves stays. The owner refuses a panel action for it (2026-11-02, evening: "the smartsoft do it without me doing anything"), so the
   device learns the MASTER by **one probe after the download: a tap of the bump of an unpatched channel**, sent by the device itself.
   Evidence: a bump press makes the console list FaderInfo (the whole object, body[193] BUMPS and body[194] MASTER included),
   ChannelLevels and LedStatus (F174, F178; 16–67 ms after the press); an unpatched channel changes no DMX output (channel 49 in E8b,
   channel 47 in E9d: "no DMX"); the channel bumps 1–48 are cleared as holds by SC-5 (a tap being a 47–77 ms hold, F168) and the
   physical-control rule names the bump (F171 / F174). Conditions: the probe runs only when FaderInfo has not been reported since
   Connect, only in channel mode (bank 1-24 or 25-48 as the download's ConsoleStatus says; in memory mode the physical bump is a
   memory bump, so the device first switches to bank 1-24 by the automatic switch of SC-5 or, if that is refused, asks the user to
   move a fader), only for a channel whose Patch entry in the download is unpatched (0 in every one of its 512 DMX entries; the
   first such channel of the shown bank), never under SOLO, BLACKOUT or a record function, exactly once per session, with the
   bump shadow of SC-5 covering it as any bump of ours; if every channel of the shown bank is patched, no probe: the UI asks for one
   fader movement on the panel instead. The channel's bump LED blinks once and ChannelLevels[c] reads 0xFF for the tap; nothing on
   the stage changes (OBSERVED for unpatched channels, INFERRED for a channel whose only address is on a dimmer that is not in use).
   The owner accepts the probe in writing; the SC-5 matching rule and every other interlock are unchanged.
5. **One encoder, one whitelist, golden tests.** The two bodies as enum members with their evidence (F181), the placement and the version
   gate replayed over the 20 SmartSoft sessions (the pair's bytes, the ConsoleVersion gate, the download accepted record by record), the
   refusal of controls during the download, the switch's refusals (no `StartupPairDate` → no pair; a date not today → the backend refuses
   to start, as the other two dates), and SC-1's read-only session unchanged without the switch.
6. **Nothing else:** no other System value, no second pair, no pair on a reconnect without a new Connect, no pair while live control is
   sending (the pair is first or not at all), no change to what SC-1 / SC-2 / SC-5 cleared.

### Conditions for E13 (the first session with the pair) and what to record

- Backup to the SD card first, the name in the log; USBPcap from before the Zadig bind; a guided script (`tools/usb-probe/e13-guided-session.ps1`,
  to be written from the E11b one: the gate questions, the versions, the starting position, Connect with the pair, the read-only minute
  after the download, then one MASTER movement on the panel and the SC-2 sanity control, Disconnect at an idle point, the SD save).
- The owner gives the go after reading this section and accepts in writing: the residual of item 3 (a state never seen), the blind window
  of 7.2–7.6 s, the recovery path after a cut download (a SmartSoft start), and the MASTER probe of clause 4 (a bump tap of an unpatched
  channel sent by the device once after the download; one panel fader movement instead when every channel of the shown bank is patched).
  *(His answers of 2026-11-02, evening, in chat: to the blind window "And?", to the recovery "Hm what?" (explained: keep SmartSoft
  installed as the way out), to the MASTER "the smartsoft do it without me doing anything so the solution should be somewhere there"
  (answered by the probe); the written yes is still open.)*
- Record: the two writes with their placement (ms after the reply, the next poll), the ConsoleVersion bytes, the download (its record
  count, bytes, duration, the order against F182, every record decoded), what the UI showed at the end of the download (mode, bank, page,
  LEDs, levels, independents, file name, memories, patch, stack), that nothing changed (the live objects delivered later against the
  download's bodies; `SHOW` after = `SHOW` before), any refusal, any deviation and the time it ended the session.
- **E13 clears nothing by itself:** a second session of ours makes the pair's behaviour towards our host VERIFIED; the decision above
  stands or is annotated.

### What SC-4 does not clear

Every other System value (the erase commands 11–16, the page select 10, the show-upload framing 0 / 1 / 6: never, F121); the pair more than
once per session; the pair from a state the device already knows to be outside F185's; any fetch of an object the console did not list (SC-1
stands: the download is listed by the console); the Memory, Patch, Sequence, Step and Page writes SmartSoft makes when editing (E12 / E12b,
SC-2 class D and F: a check of their own if the owner wants the editor pages to write to the console).
