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
  first seen in E9c need one more capture (E9d). No derivation replaces a capture.
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
