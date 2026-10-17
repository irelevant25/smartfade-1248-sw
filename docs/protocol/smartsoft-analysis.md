# SmartSoft static analysis

Scope: `SmartSoft 3.0.2.9.0.43b.exe` and the files it contains
(extracted with `tools/source-audit/extract_smartsoft_installer.py`). S1–S10 are
static: headers, imports, exports, resources, strings. Nothing was executed or installed.
S11 (added 2026-10-16) adds **disassembly** of the USB code in `sfEngine.dll`; it is still
static and still INFERRED. String evidence is a **clue, not proof** of runtime behaviour.

Format per finding: **Observation / Interpretation / Confidence / How to verify.**

---

## S1. The supplied .exe is an installer, not the application

- **Observation:** Version resource `FileDescription = "ETC SmartSoft Installer"`, second
  version block `Win32 Cabinet Self-Extractor` / `WEXTRACT.EXE`. Imports `Cabinet.dll`.
  `RT_RCDATA` holds `CABINET` (31 225 388 bytes, `MSCF` magic), `RUNPROGRAM="setup.exe"`,
  `TITLE="ETC SmartSoft"`. The cabinet holds `setup.exe` and `smartsoft.msi`.
- **Interpretation:** Microsoft Wextract package wrapping a Visual Studio setup-project MSI.
- **Confidence:** High.
- **Verify:** Already verified by extraction. Hashes are in `docs/source-materials.md`.

## S2. Application technology

- **Observation:** `SmartSoft.exe` is PE32 i386, linker 8.0, imports `MSVCR80.dll`,
  `QtCore4/QtGui4/QtNetwork4.dll` (Qt 4.6.3.0 bundled), no CLR header. Resource paths
  `:/SmartSoft/Resources/*.png`, Qt signal/slot strings (`1consoleCache…Updated()`).
- **Interpretation:** Native C++ built with Visual Studio 2005 on Qt 4.6.3. Not .NET, Delphi or VB.
  A macOS build existed too (manual), which fits Qt.
- **Confidence:** High.
- **Verify:** Not needed.

## S3. Console logic lives in engine DLLs that are loaded per console type

- **Observation:** `sfEngine.dll` and `sfmlEngine.dll` each export exactly one symbol,
  `getConsole`. `SmartSoft.exe` contains strings `getConsole`, `sfEngine`, `sfmlEngine`,
  `Select console type:`, `on_smartFade1248PushButton_clicked`, and
  "SmartSoft failed to load the console selected". `sfEngine.dll` contains the **complete
  console LCD menu text** ("SmartFade Menu", "Patch by Dimmer", "Erase all data?",
  "Record Memory Pg", …), `smartfade.dat`, ASC writer format strings (`$SEQUENCE`,
  `$$MEMORY`, `SHOW%d.ASC`, `BACKUP%d.ASC`) and Qt `.ts` translations.
- **Interpretation:** SmartSoft loads a console engine (`getConsole` factory, `IConsoleHandler`
  in the exe) that runs the console's own firmware logic on the PC. That powers the
  simulator/offline editor. The same DLL owns the USB connection to the real console. The
  manual's phrase "it will run as part of the console application" fits this. The ASC
  writer and parser are in the engine, so the file format is the console's own format.
- **Confidence:** High for the DLL roles. Medium for "same code as firmware".
- **Verify:** Dynamic: run SmartSoft offline in a VM and observe which DLL loads per console choice.

## S4. Transport to the console is USB through the ETC "High-Speed-Pipe" kernel driver

- **Observation (driver):** `etchsp.inf` ("Installation file for ETC High-Speed-Pipe (etchsp)
  driver, by Peperoni-Light, Dr. Jan Menzel"), class `ETCHspClass`
  `{E029E7FA-4080-4057-BD79-9FD926EE064F}`, binds hardware IDs
  `USB\VID_14d5&PID_0103&MI_01`, `PID_0110`, `PID_0112`, `PID_0200`, `PID_0201`
  (+ `PID_FFFB` on ia64/amd64), device description **"USB High-Speed-Pipe"**,
  service `ETCHSP` kernel driver, demand start. `etchsp.sys` v2.00.000 imports
  `USBD_ParseConfigurationDescriptorEx`, `USBD_CreateConfigurationRequestEx`,
  `IoRegisterDeviceInterface`.
- **Observation (engine):** `sfEngine.dll` imports `SetupDiGetClassDevsW`,
  `SetupDiEnumDeviceInterfaces`, `SetupDiGetDeviceInterfaceDetailW`, `CreateFileW`,
  `ReadFile`, `WriteFile`, `DeviceIoControl`. Strings: `HighSpeedPipeline`,
  "Console: HIGHSPEED Path: %s", "Highspeed Device didn't open correctly",
  `SmartFadeUSBHandler`, `USBDevice`, "ERROR: reading BcdDevice failed", "ERROR: reading PID
  failed", "ERROR: reading product string failed".
- **Observation (manual):** "connect any SmartFade or SmartFadeML console to a computer
  running SmartSoft using a USB connection… Install PC drivers… without this the console will
  report as 'Unrecognized USB device'… right-click on 'USB High-Speed-Pipe' and select
  'Update Driver'".
- **Observation (negative):** Neither `SmartSoft.exe` nor `sfEngine.dll` imports `winmm.dll`
  (no `midiIn*`/`midiOut*`), DirectMusic or serial comm APIs (`SetCommState` etc.).
  Networking imports are limited to `SmartSoft.exe` (`setsockopt`, `htonl` from `WSOCK32`,
  34 QtNetwork symbols) and 2 QtNetwork symbols in `sfEngine.dll`.
- **Interpretation:** SmartSoft talks to the console over **USB only**, as a vendor-specific
  bulk (or similar) interface `MI_01` of a composite device with VID `0x14D5`. The
  vendor ID belongs to the INF author, Peperoni-Light (**HYPOTHESIS:** ownership not checked
  against the usb.ids registry). The engine opens the device via a SetupAPI device-interface
  path and uses `ReadFile`/`WriteFile`/`DeviceIoControl`. **SmartSoft does not use MIDI
  or a COM port to talk to the console.**
- **Confidence:** High (three independent source types agree). Not yet observed on our unit.
- **Verify:** (1) Plug the console in and record VID/PID/interfaces/driver (zero-risk).
  (2) USBPcap capture while SmartSoft connects.

## S5. The USB composite device probably also has a HID interface

- **Observation:** INF binds only `MI_01`. `sfEngine.dll` strings: "Console HID data: ",
  `DeviceStreamDataAvailable HIDConsole`, "Button: %d %d". `SmartSoft.exe` imports
  `HidD_GetHidGuid`, `HidD_GetAttributes`, `RegisterDeviceNotificationW`.
  `SFDownloader.exe` (firmware updater) imports HID APIs and prints
  "HID device with VID = 0x%x, PID = 0x%x", "Bootloader Version:".
- **Interpretation:** HYPOTHESIS: interface `MI_00` is a standard HID interface (no custom
  driver needed) used for console events (buttons) and/or device arrival. The bootloader
  presents as HID for firmware download. `SmartSoft.exe` uses the HID GUID for plug/unplug
  notification.
- **Confidence:** Medium.
- **Verify:** USB descriptors from USBTreeView / Device Manager / `pnputil /enum-devices /connected /ids`.

## S6. The USB protocol is an object-synchronisation protocol, not a "set channel" command set

- **Observation:** `sfEngine.dll` strings in one block (`0x1F118`–`0x1F830`):
  object names `SystemSettings, DirtyObjects, System, SystemSizes, ConsoleVersion, FileName,
  MoveStep, DeleteStep, InsertPointQStep, InsertStep, RepatchDevice, UnpatchDevice,
  PatchDevice, PersonalityUpdate, PersonalityLibraryReset, Personality, ChangedStatus,
  DeviceStatus, ConsoleStatus, LedStatus, DisplayContent, ConsoleEvent, InputLevels,
  BlindLevels, PlaybackInfo, OutputPalettes, ChannelLevels, FaderInfo, OutputLevels,
  MIDIPatch, Effects, ChannelText, Device, StepContent, SequenceMain, Palette`.
  Log strings: "Unhandled data object", "USB Message queued: %s (%d). Buffer count: %d",
  "Dirty object queued: %s %d %d %d", "Message out of sequence: %d %d",
  "USB Timeout: No answer", "USB Retrying...",
  "USB: ERROR: Wrong number of bytes read: %d %d", "USB: Invalid Poll reply",
  "USB OBJECT: Wrong object", "USB OBJECT: Empty object", "Object received: %s %d %d %d",
  "Re-Fetch object: %s %d %d %d", "waitForObject: %s %d %d %d",
  "Fetch object: %s (%d) %d %d %d", "USB FETCH Cmd failed", "Send Fetch: DirtyObjects: %d",
  "USB Cmd: Write failed…", "USB Data: Write failed…", "USB Write ok…".
  `SmartSoft.exe` has signals such as `consoleCacheChannelsUpdated()`,
  `consoleCacheFaderUpdated()`, `consoleCacheLedUpdated()`,
  `consoleCacheMemoryUpdated(Console::Memory)`, `consoleCacheSmartSoftCommand(int,int)`.
- **Interpretation (HYPOTHESIS):**
  - The PC **polls** the console ("Invalid Poll reply").
  - The console reports which objects changed (`DirtyObjects`), and the PC then **fetches**
    those objects ("Send Fetch: DirtyObjects", "Fetch object: name (id) a b c", with 3
    numeric qualifiers such as page/index/step).
  - Replies carry typed objects and are validated ("Wrong object", "Empty object",
    "Wrong number of bytes", "out of sequence"). There is a sequence number and a
    timeout + retry.
  - There are separate "Cmd" and "Data" writes.
  - SmartSoft keeps a **cache** of console state (`consoleCache…`). The console is the
    source of truth.
  - `ConsoleEvent` / `LedStatus` / `DisplayContent` / `FaderInfo` suggest the PC can inject
    front-panel events and mirror LEDs, LCD and fader positions. Writes to show data would
    then be edit objects (`InsertStep`, `ChannelText`, …).
  - Consequence: "Set channel 12 to 75 %" may **not exist** as a message. It may have to be
    expressed as a virtual fader/button event or a level object. Do not design the
    application API around a command set we have not seen.
- **Confidence:** Medium for the poll/dirty/fetch pattern. Low for every detail.
- **Verify:** USB capture of SmartSoft connecting, idle polling, one fader move on the console,
  one label edit in SmartSoft. Correlate with these strings.

## S7. Version handshake and connection preconditions

- **Observation:** `SmartSoft.exe` strings: "The SmartFade console must be running in Normal
  mode in order to connect to SmartSoft.", "The console software version does not match the
  SmartSoft software version.", "Please update one or both sides to a matching version.",
  `Online` / `Offline`, `Library version`, `DLL version`, `SmartSoft version`.
  `sfEngine.dll`: `ConsoleVersion`, "%0d.%0d.%0d%s%0d", "%0d.%0d.%0d.%0d.%0d.%0d".
- **Interpretation:** The connection starts with a version exchange (`ConsoleVersion` object).
  The version format is probably 6 parts (3.0.2.9.0.43). A mismatch blocks the session.
- **Confidence:** Medium-high.
- **Verify:** Capture the connection. Note the firmware version shown on the console.

## S8. Network usage is for the Capture visualiser, not the console

- **Observation:** `SmartSoft.exe`: `Capture2005DemoWindow`, `Capture2005MainWindow`,
  `refreshCaptureButton()`, `TitleBarCaptureButton.png`. Installer runs
  `AddFirewallRule.exe` for SmartSoft.exe. `Demoshow-SmartFade*.exe` import OpenGL + Winsock.
  An obsolete translation string in `sfEngine.dll` mentions "Ethernet output together with
  an E/Gate or visualization program… install IPX".
- **Interpretation:** QtNetwork/Winsock and the firewall rule serve the *Capture* lighting
  visualiser integration. The console has no Ethernet port.
- **Confidence:** Medium.
- **Verify:** Not a priority. Optional: `netstat` while SmartSoft runs.

## S9. Firmware update path is separate

- **Observation:** `SFDownloader.exe` (2007, MFC) uses HID. Console manual: updates via SD or
  USB with the console in self-test "Firmware Upgrade" mode. `SMRTFADE.SRC` is an ASCII
  hex-record image starting `01BT0B00SPF8…`.
- **Interpretation:** The bootloader has its own USB personality (HID). Not relevant to
  runtime control.
- **Safety:** **Never** exercise the bootloader or firmware download path. It can brick the console.

## S10. References to ASC / SFML / PERSLIB

- **Observation:** `sfEngine.dll`: `*.asc`, `%s.asc`, `SHOW%d.ASC`, `SHOW0%d.ASC`,
  `BACKUP%d.ASC`, and ASC keywords (see asc-format.md). `SmartSoft.exe`: "Show files (*.asc)",
  "This show file originates from another type of console and cannot be read!".
  PERSLIB/personality strings (`PersonalityUpdate`, `PersonalityLibraryReset`) appear in the
  SmartFade engine's object table, but the fixture library is ML-only (`perslib.bin` is
  bundled for the ML).
- **Interpretation:** ASC reading/writing is shared engine code (console SD card format). The
  object table is probably shared between SF and SFML protocol variants.
- **Confidence:** Medium.

---

## S11. Message framing (static, INFERRED)

Date: 2026-10-16. Target: `analysis/extracted/SmartSoft_3.0.2.9.0.43b/files/sfEngine.dll`
(PE32 i386, SHA-256 `3228bd9b…7983`, ImageBase `0x10000000`). Everything in this section is
**INFERRED** from disassembly. Nothing here is VERIFIED and nothing here may be sent to a
console. Its purpose is to decode the E2 capture faster, not to replace it.

### S11.0 Method and limitations

- **Tooling (first pass, 2026-10-16 morning):** the REA MCP server opened the DLL, but every
  REA analysis backend was unavailable on the analysis machine at that time (Ghidra not
  installed, Hopper macOS-only, IDA not configured, REA's objdump/rizin runners fail with
  `ERR_REA_WINDOWS_NATIVE`). No decompiler was available. Fallback:
  `tools/source-audit/x86_probe.py` (pefile + capstone, linear-sweep disassembly, string
  cross-references, heuristic function boundaries). All evidence in S11.1–S11.13 is raw x86
  assembly read by hand. **Function boundaries are heuristic** (direct call targets +
  `push ebp; mov ebp, esp` after padding) and names `sub_XXXXXXXX` are ours.
- **Tooling (second pass, same day, later):** Ghidra 12.1.4 became usable through REA
  (`provider_id = "ghidra"`). S11.14 re-verifies S11.2–S11.10 against decompiler output and
  marks each claim CONFIRMED / CORRECTED / UNRESOLVED. Where S11.14 corrects an earlier
  subsection, the earlier text is kept and an inline **CORRECTED** note points to S11.14.
- **Address convention:** virtual addresses (VA). In this DLL every section's raw offset
  equals its RVA, so **file offset = VA − 0x10000000** (`.text` VA 0x10001000 = raw
  0x1000, `.rdata` 0x1001D000 = 0x1D000, `.data` 0x1009C000 = 0x9C000).
- **Not covered:** what `etchsp.sys` does with the ReadFile/WriteFile buffers (bulk vs
  interrupt, URB packetisation, driver-side timeouts). The 12-byte "command" is what the
  *engine* hands to the driver, not necessarily what appears on the wire 1:1.
- **Stack-offset reading:** all local-variable offsets were computed by hand from push
  counts. One miscount changes a field position. Treat every field offset as "probable".

### S11.1 Where the strings are used

| String (VA) | Reference | Function | Role (INFERRED) |
|---|---|---|---|
| "Send Fetch: DirtyObjects: %d" (0x1001F7AC) | 0x1001ACB1 | `sub_1001AB00` | HSP thread main loop (QThread::run of `HighspeedPipelineThread`, allocated 0xAF0 bytes in `sub_100193F0`) |
| "Fetch object: %s (%d) %d %d %d" (0x1001F774) | 0x1001ADC8 | `sub_1001AB00` | logged right after the Fetch command WriteFile |
| "USB: Invalid Poll reply" (0x1001F6C8) | 0x1001AFE0 | `sub_1001AB00` | reply header word0 ∉ {3, 4} |
| "USB: ERROR: Wrong number of bytes read: %d %d" (0x1001F670) | 0x1001B18A | `sub_1001AB00` | payload ReadFile returned ≠ announced length |
| "USB Cmd: Write failed…" (0x1001F7CC) / "USB Data: Write failed…" (0x1001F7F4) / "USB Write ok…" (0x1001F820) | 0x1001AC76 / 0x1001AC63 / 0x1001AC55 | `sub_1001AB00` | two consecutive WriteFile calls per outgoing data object |
| "USB FETCH Cmd failed" (0x1001F794) | 0x1001AD9A, 0x1001AE7D | `sub_1001AB00` | Fetch / Re-Fetch WriteFile returned ≠ 12 |
| "waitForObject…", "Re-Fetch object…", "Object received…", "USB OBJECT: Wrong object", "USB OBJECT: Empty object", "USB Timeout: No answer", "USB Retrying..." | 0x1001AE0E, 0x1001AEAA, 0x1001AF75, 0x1001AFCE, 0x1001AF88, 0x1001B1C2, 0x1001B1BB | `sub_1001AB00` | see S11.2 |
| "Message out of sequence: %d %d" (0x1001F4F8) | 0x10019AF4 | `sub_10019810` | `QObject::event` override of the console object; consumes the received payloads on the GUI/engine thread |
| "HighSpeed data: %4d: " (0x1001F524), "HighspeedPipelineThreadDataAvailable HighSpeedPipeline" (0x1001F53C) | 0x10019A0C, 0x1001999A | `sub_10019810` | hex-dump logging of every received payload; event name match |
| "Dirty object queued: %s %d %d %d" (0x1001F3CC) | 0x10018090 | `sub_100174F0` | per-object parser (receive side), DirtyObjects case |
| "Unhandled data object" (0x1001F350) | 0x100180E3, 0x1001677C | `sub_100174F0`, `sub_10015E50` | default case of the receive parser and of the transmit serializer |
| "USB Message queued: %s (%d). Buffer count: %d" (0x1001F39C) | 0x10017496 | `sub_100173B0` | enqueue of an outgoing object |
| "Console: HIGHSPEED Path: %s" (0x1001F48C), "Highspeed Device didn't open correctly" (0x1001F450) | 0x10019441, 0x100194D6 | `sub_100193F0` | connect: find device, open, start thread |
| "ERROR: reading product string / PID / BcdDevice failed: %d" (0x1001F5E8 / 0x1001F5C8 / 0x1001F5A0) | 0x1001A6B2 / 0x1001A6DD / 0x1001A708 | `sub_1001A630` | device identification via DeviceIoControl |

The object-name pointer table lives at **`.data` 0x1009C6A0** (file 0x9C6A0) and is
indexed by object ID in four places (`mov edx, [eax*4 + 0x1009c6a0]` at 0x1001ADBE,
0x1001AE05, 0x1001AEA1, 0x1001AF6C, 0x10018083, 0x1001748D). See S11.6.

### S11.2 The HSP thread loop (`sub_1001AB00`, file 0x1AB00–0x1B213)

- **Observation (structure, from the control flow):**
  ```text
  loop while this->stop (byte this+0x19) == 0:
    A. if outgoing queue (global QList 0x1009CDC4) not empty:
         copy first element (0xA8E bytes) to stack; delete it
         WriteFile(h, Cmd[12] = {1, len, (stale w2..w5)}, 12)      ; "USB Cmd: Write failed" if ≠ 12
         WriteFile(h, element+2, len)  where len = u16 at element+0 ; "USB Data: Write failed" / "USB Write ok"
         msleep(10)
    B. if no fetch pending (expected id == 0x2B) and dirty queue (global QList 0x1009CDC8) not empty:
         log "Send Fetch: DirtyObjects: %d" (count)
         pop first record {id, a, b, c}; remember it as "expected"
         WriteFile(h, Cmd[12] = {2, 0, id, a, b, c}, 12)            ; "USB FETCH Cmd failed" if ≠ 12
         log "Fetch object: name (id) a b c"; msleep(10)
         if id == 0x2B: do not wait for it
       else if fetch pending:
         log "waitForObject: name a b c"; wait_count++
         if wait_count > 10: re-send the same Fetch Cmd, log "Re-Fetch object", msleep(10), wait_count = 0
    C. up to 5 attempts:
         WriteFile(h, Cmd[12] = {0, 0, (stale w2..w5)}, 12)         ; poll
         ReadFile(h, Hdr[12], 12)
         if bytes ≠ 12: log "USB Retrying..." (attempts left > 1) or "USB Timeout: No answer"; msleep(20); retry
         if Hdr.w1 > 0:
            if Hdr.w0 == 3: log "Object received: name w3 w4 w5"
                            if no fetch pending: "USB OBJECT: Empty object"
                            else if (w2,w3,w4,w5) == expected (id,a,b,c): matched = 1
                            else "USB OBJECT: Wrong object"
            else if Hdr.w0 != 4: "USB: Invalid Poll reply"
            ReadFile(h, this+0x1A, Hdr.w1)                            ; payload, max 0xA8E bytes
            if bytes ≠ Hdr.w1: "USB: ERROR: Wrong number of bytes read"; if fetch pending: prepend expected record back onto the dirty queue; expected = 0x2B
            else: lock this+0xAA8; append QByteArray(payload, len) to list this+0xAAC; unlock;
                  post event "HighspeedPipelineThreadDataAvailable " to the console QObject
                  if matched: expected = 0x2B, wait_count = 0
         break
    msleep(20)
  ```
- **Evidence (selected):** Cmd buffer = frame+0x50, 12 bytes: `push 0xc; lea edx,[esp+0x5c]; push edx; push eax; call WriteFile` at 0x1001AC0A–0x1001AC24 and `cmp dword ptr [esp+0x18], 0xc` at 0x1001AC26. Data write at 0x1001AC30–0x1001AC41 uses `movsx esi, si` (si = first word of the queued element) as byte count and `lea edx,[esp+0x6a]` (= element+2) as buffer. Reply header read into the same 12-byte buffer at 0x1001AF09–0x1001AF17, payload read into `[ebx+0x1a]` at 0x1001B004–0x1001B009. Expected-record sentinel `0x2b` set at 0x1001AB54, tested at 0x1001AC91, 0x1001ADE4, 0x1001AF83. Retry counter `mov edi, 5` at 0x1001AEC4; sleeps at 0x1001AC84 (10), 0x1001ADD3 (10), 0x1001AEB5 (10), 0x1001B1D3 (20).
- **Interpretation:** one thread does everything; there is no separate reader thread and
  no asynchronous I/O (`lpOverlapped` is always 0). Every console→PC transfer is solicited
  by a poll. The console cannot push.
- **Confidence:** High for the loop shape, medium for the exact field positions.
  **2026-10-16 (S11.14):** loop shape and field positions CONFIRMED by Ghidra. Two
  CORRECTED details: (1) the "up to 5 attempts" counter only covers a header `ReadFile`
  that returns ≠ 12 bytes; a poll `WriteFile` that returns ≠ 12 bytes is retried
  **without delay and without limit** (the `msleep(20)` sits inside the `WriteFile == 12`
  branch); (2) when the reply header has `w1 ≤ 0` (signed) the code checks neither w0 nor
  anything else and simply ends the attempt, so "type 4 with w1 = 0" is one of several
  silent outcomes, not the only one.
- **How to verify:** E2: OUT transfers must alternate 12-byte headers with optional data,
  and every IN transfer must be preceded by a 12-byte OUT whose word0 = 0.

### S11.3 The 12-byte command header (PC → console)

- **Observation:** the engine writes six **16-bit words, native little-endian** (word
  stores `mov word ptr [esp+…]`), at frame offsets 0x50–0x5B:

  | word | offset | Poll (type 0) | Data (type 1) | Fetch (type 2) | Evidence |
  |---|---|---|---|---|---|
  | w0 | 0 | 0 | 1 | 2 | 0x1001AEDB (`si`=0), 0x1001AC1D (`1`), 0x1001AD70 / 0x1001AE5F (`2`) |
  | w1 | 2 | 0 | payload length (first word of the queued element) | 0 | 0x1001AEE0, 0x1001AC13, 0x1001AD77 |
  | w2 | 4 | **stale** | **stale** | object ID | 0x1001AD7E (`di`) |
  | w3 | 6 | stale | stale | qualifier a | 0x1001AD83 (`bx`) |
  | w4 | 8 | stale | stale | qualifier b | 0x1001AD5C (`dx`) |
  | w5 | 10 | stale | stale | qualifier c | 0x1001AD88 (`si`) |

  "Stale" = the buffer is not cleared between commands (the prologue initialises only
  0x14, 0x1C, 0x20–0x2C and 0x3C); words 2–5 keep whatever the previous Fetch or reply
  header left there, and are uninitialised stack bytes on the very first poll.
- **Interpretation:** only w0 and w1 are meaningful in Poll and Data commands. A decoder
  must **ignore w2–w5 for types 0 and 1**; they will look like echoes of earlier traffic.
- **Confidence:** High for w0/w1 of all three types, high for w2 = object ID of Fetch,
  medium for the order a/b/c. **2026-10-16 (S11.14): field order and the stale-words
  claim CONFIRMED by Ghidra (locals `local_abc, local_aba, local_ab8, local_ab6,
  local_ab4, local_ab2`); order a/b/c is now high.**
- **How to verify:** E2: (1) all 12-byte OUT transfers have w0 ∈ {0,1,2}; (2) a type-1
  header is immediately followed by an OUT transfer of exactly w1 bytes; (3) in type-0/1
  headers words 2–5 repeat the words of the last type-2 header or last reply header.

### S11.4 Outgoing data payload (PC → console)

- **Observation:** `sub_10015E50(id, a, b, c, out)` serialises an object into
  `out = [u16 bodyLen LE][u8 id][body…]` (`mov word ptr [eax], di` at 0x10015F03,
  `mov byte ptr [eax+2], bl` at 0x10015F09; body written from `out+3`, 0x10015E5D).
  `sub_100173B0(out)` then builds the queue element `[u16 total = bodyLen+4][u8 seq][copy
  of out, bodyLen+3 bytes]` (0x1001743C–0x1001745C: `memcpy(frame+0x13, out, len+3)`,
  seq byte at frame+0x12, total at frame+0x10) and logs "USB Message queued". The thread
  writes bytes from element+2, so the **payload on the wire is**:

  | offset | size | content |
  |---|---|---|
  | 0 | 1 | sequence number (global byte 0x10343110, post-incremented per queued message, only touched at 0x1001743F/0x10017457) |
  | 1 | 2 | bodyLen, **little-endian** (copied verbatim from the serializer's native word) |
  | 3 | 1 | object ID |
  | 4 | bodyLen | body; 16-bit fields are emitted **big-endian** (hi byte via `sub_100088F0`, lo byte via `sub_10008900`, e.g. 0x10015F6D–0x10015F8D) |

  Maximum element size 0xA8E bytes (copy loop `mov ecx, 0x2a3; rep movsd; movsw` at
  0x1001ABB6–0x1001ABC1 copies 0x2A3·4+2 = 2702 bytes), so bodyLen ≤ 2698.
- **Observation (coalescing):** `sub_100173B0` first scans the queue for an element whose
  object ID is 0x14 (20 = `ConsoleEvent`) with body byte0 == 0 and body byte2 equal to the
  new one, and overwrites its body byte3 instead of appending (0x100173D0–0x10017428,
  0x100174C0–0x100174D4).
  **CORRECTED 2026-10-16 (S11.14.4):** the byte positions above were off by one. With
  body byte 0 = kind, the coalescing key is **body byte 1** (fader index) and the byte
  overwritten is **body byte 2** (new level); body byte 3 (previous level) keeps the value
  of the first queued event. The old text is kept for the record.
- **Interpretation:** the mixed endianness (LE length, BE body) is a probable artefact of
  the serializer writing the length natively. It also means the length word cannot be
  used to tell direction. `ConsoleEvent` sub-type 0 is a repeated, overwritable event
  (HYPOTHESIS: fader movement, keyed by byte2 = fader, byte3 = level).
  **CORRECTED 2026-10-16:** keyed by body byte 1 = fader, byte 2 = new level, byte 3 =
  previous level; see S11.14.4 and the ConsoleEvent builder `sub_100169C0`.
- **Confidence:** Medium-high for the layout; low for the ConsoleEvent reading.
- **How to verify:** E2/E3: OUT payloads after a type-1 header: byte3 ∈ 0..41, bytes 1–2
  == header w1 − 4, byte0 increments by one per data payload.

### S11.5 Reply header and received payload (console → PC)

- **Observation (header):** the 12-byte reply is read into the same buffer and decoded as
  LE words: `w0` = 3 ("Object received", 0x1001AF46) or 4 (poll reply, 0x1001AFDA; anything
  else logs "Invalid Poll reply"); `w1` = payload length, compared as a signed word
  (`cmp word ptr [esp+0x52], 0; jle` at 0x1001AF35) and used as the ReadFile byte count
  (0x1001AFF4–0x1001B009); `w2..w5` are compared against the expected `(id, a, b, c)` at
  0x1001AF94–0x1001AFC5. For type 4 with w1 > 0 the payload is read too (same path).
- **Observation (payload):** the consumer (`sub_10019810`, 0x100199FD–0x10019B08) takes
  the QByteArray, dumps it ("HighSpeed data: %4d: " + "%3d " per byte), checks
  `data[0] == (lastSeq + 1)` as a signed byte (`movzx eax, byte ptr [0x1009c878]`,
  `lea edx,[eax+1]`, sign fix-up 0x10019AD8–0x10019AEE), logs "Message out of sequence:
  %d %d" (received, last) otherwise, stores `lastSeq = data[0]` and calls
  `sub_10018290(data + 2)`. That function calls the two parsers `sub_100151A0(p)` (console
  mirror state) and `sub_100174F0(p)` (application callbacks, "Dirty object queued"). Both
  read the **object ID at `p[2]`** (`movzx eax, byte ptr [esi+2]; cmp eax, 0x2a` at
  0x100151AA / `cmp eax, 0x29` at 0x100174FA) and the body from `p[3]`.

  | offset | size | content |
  |---|---|---|
  | 0 | 1 | sequence number, checked against last+1 (mod 256) |
  | 1 | 1 | **UNKNOWN** (never read by the code we traced) |
  | 2 | 2 | probably bodyLen (same position as in the serializer's `[u16 len][id][body]` layout, since the parsers are handed `data+2`); **not checked by code** |
  | 4 | 1 | object ID (0..41) |
  | 5 | … | body, 16-bit fields big-endian (`shl cx, 8; add cx, dx` pattern throughout both parsers) |

- **Interpretation:** inbound payloads carry one byte more of header than outbound ones.
  Whether `data[1]` is a flag, an echo of our sequence, or padding is unknown. The
  sequence check is informational only: an out-of-sequence payload is still parsed.
- **Confidence:** High for byte0 and byte4, medium for bytes 2–3, none for byte1.
  **2026-10-16 (S11.14.5):** `data+2` CONFIRMED (asm `ADD ESI,2; PUSH ESI; CALL
  sub_10018290` at 0x10019AFE); byte0 and byte4 CONFIRMED; bytes 1–3 are read by **no**
  code path, so "probably bodyLen" for bytes 2–3 stays a HYPOTHESIS (confidence lowered to
  low-medium).
- **How to verify:** E2: in every IN payload byte4 ∈ 0..41 and the u16 at 2–3 (try LE
  first, then BE) equals header w1 − 5; byte0 increments by one across *all* IN payloads
  regardless of header type; observe what byte1 does.

### S11.6 Object IDs (confirmed by code, not by table order)

- **Observation:** `.data` 0x1009C6A0 is a 42-entry `const char*` table. The ID is the
  index used by the code (six `[reg*4 + 0x1009c6a0]` loads, S11.1). The receive parsers
  dispatch on the same byte through jump tables (`sub_100174F0`: index table 0x10018154,
  jump table 0x100180F0; `sub_100151A0`: 0x1001592C / 0x100158E8), which lets us
  cross-check several IDs against handler behaviour:

  | ID | Name | Handler evidence (receive parser `sub_100174F0`) |
  |---|---|---|
  | 0 | Memory | 0x10017516: record index = body[0]·48 + body[1], stride 0xA98 |
  | 1 | Palette | unhandled (default) |
  | 2 | Sequence | 0x10017590: index = body[0]·12 + body[1], stride 0x1B8 |
  | 3 | SequenceMain | 0x100176D4 |
  | 4 | StepContent | 0x10017758 |
  | 5 | Patch | 0x1001787C: u16 BE count at body[0..1], then bulk copy |
  | 6 | Device | unhandled |
  | 7 | Group | unhandled |
  | 8 | Page | 0x10017842 |
  | 9 | ChannelText | 0x1001793D |
  | 10 | Effects | unhandled |
  | 11 | MIDIPatch | unhandled |
  | 12 | Independents | 0x100178B0 |
  | 13 | OutputLevels | 0x10017A51: u16 BE count, then bytes |
  | 14 | FaderInfo | 0x10017B5A: body[0] = count of 4-byte records |
  | 15 | ChannelLevels | 0x10017AC7: body[0] = n, n−2 level bytes, then 2 trailing bytes |
  | 16 | OutputPalettes | unhandled |
  | 17 | PlaybackInfo | 0x10017BCF: body[0] = count of 16-byte records |
  | 18 | BlindLevels | 0x10017B2F |
  | 19 | InputLevels | 0x10017A85 |
  | 20 | ConsoleEvent | 0x10017D40 |
  | 21 | DisplayContent | 0x10017DD7: body[0] = n bytes |
  | 22 | LedStatus | 0x10017D51: body[0] = count of 6-byte records |
  | 23 | ConsoleStatus | 0x10017CDF |
  | 24–35 | DeviceStatus … MoveStep | unhandled on receive (ML / edit commands) |
  | 36 | FileName | 0x10017977: body[0] = n, then n u16 characters appended to a QString, read **little-endian** here (`ax = body[i+1] << 8 \| body[i]`, 0x100179A0–0x100179AD) but **big-endian** in the mirror parser `sub_100151A0` case 0x10015602 (`ax = body[i] << 8 \| body[i+1]`, 0x10015640–0x1001564D). The two parsers of the same object disagree; E2 decides. **S11.14.6:** contradiction CONFIRMED by Ghidra; the outbound serializer writes FileName **big-endian** (2 : 1 for BE, still not settled) |
  | 37 | ConsoleVersion | 0x100179EE: body[0] = n, then n bytes appended as QChars to a QString |
  | 38 | SystemSizes | 0x10017E3A: body[0], u16 BE body[1..2], body[3..7]; compares against 0x30 (48) / 0x60 (96) |
  | 39 | System | 0x10017E0F |
  | 40 | **DirtyObjects** | 0x10017FF8: parses the dirty list and logs "Dirty object queued" (S11.7) |
  | 41 | SystemSettings | 0x10017F34 |

  ID 0x2B (43) is the "no object pending" sentinel in the thread; ID 0x2A (42) is accepted
  by the range check but has no handler. The next dword after the table (0x1009C748)
  points to "bad allocation" and is unrelated.
- **Interpretation:** the IDs are **confirmed by code** (index use + handler semantics such
  as DirtyObjects = 40, ConsoleVersion = 37 as a string, SystemSizes = 38 with 48/96), not
  assumed from string order. Note the table order is the *reverse* of the string block
  order in `.rdata` (0x1F118 `SystemSettings` is ID 41).
- **Confidence:** High.
- **How to verify:** E2: the first Fetch headers of a session should carry w2 = 37/38/41
  (version, sizes, settings) and the first payloads byte4 = 40 (dirty list).

### S11.7 Dirty-object list and the three qualifiers

- **Observation:** DirtyObjects handler (0x10017FF8–0x100180CA): `count` = u16 BE at
  body[0..1]; then `count` records of **5 bytes**: `[u8 id][u16 BE a][u8 b][u8 c]`
  (loads at 0x10018020–0x10018047). Each record is stored as a 12-byte
  `{u32 id; u16 a; u16 b; u16 c; u16 pad}` and appended to the global dirty queue
  0x1009CDC8 (`sub_100171C0`, 12-byte element 0x10017206–0x1001722D). The thread copies
  `a` into Fetch w3, `b` into w4 and `c` into w5 (S11.3) and compares the reply's w3–w5
  against them. The log "Dirty object queued: %s %d %d %d" prints name, a, b, c.
- **Observation (what a, b mean for some objects, from the serializer/parsers):** for
  Memory, a·48+b indexes the memory array (page, fader); for Sequence a·12+b. For other
  objects the qualifiers are passed through unchanged. Nothing tells us what `c` is.
- **Interpretation:** the "3 numeric qualifiers" are (a: u16, b: u8, c: u8) addressing
  an instance of the object (page/fader/step-like). The console decides what is dirty;
  SmartSoft only echoes the triple back in the Fetch.
- **Confidence:** High for the record format, low for the semantics of a/b/c.
  **2026-10-16 (S11.14.7):** record format `[id][u16 BE a][b][c]` and the 12-byte queue
  element CONFIRMED (asm 0x10018020–0x10018073). Semantics now INFERRED for Memory and
  Sequence (a = page, b = slot); `c` is still never interpreted by any code we read.
- **How to verify:** E2: decode the first type-4 payload with byte4 = 40; the length must
  be 5 + 2 + 5·count. Correlate the triples with the following Fetch headers (w2..w5).

### S11.8 Sequence numbers

- **Observation:** two independent 8-bit counters. PC→console: global 0x10343110,
  incremented per queued data message, placed at payload byte0 (S11.4). Console→PC:
  payload byte0, expected to be previous+1 (global 0x1009C878, S11.5). Neither counter is
  present in the 12-byte headers. Neither is reset in the code we traced (0x1009C878 is
  only written in `sub_10019810`).
- **Interpretation:** "Message out of sequence" means a lost or duplicated console
  payload, not a header problem. Poll replies with payload also consume a sequence number.
- **Confidence:** High.
- **How to verify:** E2 as in S11.5. E2 should also reveal the initial value on both sides.

### S11.9 Timing

- **Observation:** `msleep(10)` after a Data write and after a Fetch; `msleep(20)` at the
  end of every loop iteration and between poll retries; up to 5 poll attempts per
  iteration; a pending Fetch is re-sent after >10 iterations without the matching object.
  The `ReadFile` calls are synchronous; any timeout comes from the driver (UNKNOWN).
- **Interpretation:** idle cadence ≈ one poll every 20 ms plus transfer time (≈ 40–50 polls/s).
- **Confidence:** Medium (depends on how `etchsp.sys` services a 12-byte read with nothing pending).
- **How to verify:** E2 idle section: inter-poll interval histogram.

### S11.10 Device discovery and IOCTLs (open leads #2 and #3)

- **Observation (`sub_100193F0`, connect):** calls `sub_1001A630(0x200, &path)`
  (`push ebp; push 0x200; call` at 0x1001941C–0x10019428); on success logs "Console:
  HIGHSPEED Path: %s", allocates the thread object (0xAF0 bytes), names it
  "HighSpeedPipeline", opens the path with `sub_1001A760` and starts the thread
  (`QThread::start(5)` = HighestPriority).
- **Observation (`sub_1001A630`, identify):** for member index 0 and 1: `sub_1001A490`
  enumerates `SetupDiGetClassDevsW(GUID @0x1001F574, NULL, NULL, 0x12)` (DIGCF_PRESENT |
  DIGCF_DEVICEINTERFACE), `SetupDiEnumDeviceInterfaces(…, index)`,
  `SetupDiGetDeviceInterfaceDetailW`, then `CreateFileW(path, GENERIC_READ|GENERIC_WRITE,
  share 0, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL)`. Then three `DeviceIoControl` calls with
  **no input buffer**:

  | IOCTL | CTL_CODE decode | out size | used as |
  |---|---|---|---|
  | 0x222008 | FILE_DEVICE_UNKNOWN, function 0x802, METHOD_BUFFERED | 0x800 bytes | "product string" (only logged on failure) |
  | 0x222000 | function 0x800 | 2 bytes | **PID**, compared as LE u16 with the argument **0x0200** (0x1001A718–0x1001A725) |
  | 0x222004 | function 0x801 | 2 bytes | "BcdDevice" (read, not compared) |

  The handle is closed and the matching path is returned; `sub_1001A760` re-opens it with
  `CreateFileA` for the thread. Close path: `sub_1001A7B0` sets the stop flag, waits 5 s,
  terminates the thread if needed, `CloseHandle`.
- **Observation (GUID):** bytes at 0x1001F574 `c7 40 71 8d ae bc e2 4b a5 c2 df e4 8e bb
  75 9a` = **`{8D7140C7-BCAE-4BE2-A5C2-DFE48EBB759A}`**, the GUID of open lead #3.
- **Interpretation:** that GUID is the **device-interface GUID** registered by `etchsp.sys`
  (`IoRegisterDeviceInterface` import, S4), distinct from the INF *class* GUID
  `{E029E7FA-…}`. `sfEngine.dll` does **not** check the VID at all; it relies on the driver
  binding (VID 14D5) and selects the interface whose PID IOCTL returns **0x0200**. So the
  SmartFade 1248/2496 engine expects **PID 0x0200**; the other PIDs in the INF belong to
  other products (ML engine not analysed). The three IOCTLs are driver-private; their
  semantics beyond "returns PID / BcdDevice / string" are not interpreted here.
- **Confidence:** High for GUID, IOCTL codes and the 0x0200 comparison. **2026-10-16
  (S11.14.10): all CONFIRMED by Ghidra.**
- **How to verify:** E1: the enumerated device must show `VID_14D5&PID_0200` (H21). A
  SetupAPI listing of interface GUID `{8D7140C7-…}` on a PC with the driver installed and
  the console attached must return the same path SmartSoft logs.

### S11.11 Lead #4 (`D5 14` at file 0x99070) — REJECTED as a VID check

- **Observation:** VA 0x10099070 lies in `.rdata`. No instruction references 0x10099068,
  0x1009906C, 0x10099070 or 0x10099072, and no immediate 0x14D5 (or 0x14D5xxxx) exists in
  `.text`. The bytes sit in a repeating record pattern: `… 00 01 00 01 d5 14 | … 00 01 00 03
  9a 52 | … 00 01 00 04 71 5b | … 00 01 00 05 fc 3c …`, with the other records holding
  unrelated 16-bit values, adjacent to UTF-16 text (`00 74 00 73` = "ts") near the embedded
  Qt translation data.
- **Interpretation:** a coincidental 16-bit value inside a serialized data blob, not a VID.
  The engine has no VID comparison (S11.10).
- **Confidence:** High.

### S11.12 What this means for decoding E2 (checklist)

1. Split OUT traffic on interface MI_01 into 12-byte headers (w0 ∈ {0,1,2}) and data
   payloads (follow a w0 = 1 header, length w1).
2. Pair each poll (w0 = 0) with the next IN transfer of 12 bytes; w0 ∈ {3,4}; if w1 > 0 the
   next IN transfer is the payload.
3. In every payload: byte0 = sequence; IN payloads: byte4 = object ID, body from byte5; OUT
   payloads: byte3 = object ID, body from byte4. Name IDs with the table in S11.6.
4. Ignore w2–w5 of type 0/1 headers (stale).
5. Expect at session start: Fetches for 37/38/41 and a type-4 payload with ID 40 whose
   records drive the subsequent Fetches.
6. Expect 16-bit body fields big-endian, the LE length word being the exception.

### S11.13 Remaining unknowns from this pass

*(Status after the Ghidra pass: see S11.14.12.)*

- Meaning of received payload byte1 (S11.5) and whether bytes 2–3 are really a length.
- Semantics of the Fetch qualifiers a/b/c per object (S11.7).
- Whether the driver splits/aggregates the 12-byte and payload buffers into USB packets,
  and which endpoints/transfer type carry them (only a capture can tell).
- What the console sends when nothing is dirty (type 4 with w1 = 0 is the code's "nothing"
  path, but that is our reading, not an observation).
- Initial sequence values and whether a reconnect resets them.
- Body layouts for every object except the ones sketched in S11.6.

### S11.14 Decompiler re-verification (Ghidra 12.1.4 via REA, 2026-10-16)

Everything in this subsection is still **INFERRED** (static analysis). It re-reads the
functions of S11.2–S11.10 with real decompiler output instead of hand-read capstone
assembly. Status per claim: **CONFIRMED** (pseudocode agrees), **CORRECTED** (pseudocode
disagrees; old text kept above with an inline note), **UNRESOLVED** (pseudocode cannot
decide).

#### S11.14.0 Tooling and limitations

- REA `open_binary(provider_id="ghidra")` on `sfEngine.dll` (SHA-256 `3228bd9b…7983`).
  REA session diagnostics: Ghidra **12.1.4** (`analyzeHeadless.bat` and the
  `win_x86_64/decompile.exe` both found), JAVA_HOME
  `C:\Program Files\Microsoft\jdk-21.0.12.101-hotspot`, OpenJDK 21.0.12.1 64-bit.
  `binary_overview`: 647 procedures, 587 defined strings, image base 0x10000000, sections
  as in S11.0.
- Pseudocode came from `procedure_pseudo_code`; where Ghidra dropped call arguments the
  Listing from `procedure_assembly` was used. Limitations REA reported, verbatim:
  - "Pseudocode is Ghidra decompiler output, not original source and not text-equivalent
    to Hopper output."
  - "Ghidra Listing reports a terminal call at 0x100157f5 to 0x10004010 … fallthrough is
    excluded by Ghidra's flow model. The reason for the terminal flow is not established by
    this observation." (same note for 0x10015801, 0x1001580d, 0x10015819, 0x10015825; all
    inside the `System` case of `sub_100151A0`, so that case may be cut short.)
  - `sub_1001AB00`: "WARNING: Type propagation algorithm not settling".
  - `sub_10019810`: "Removing unreachable block (ram,0x10019ae6)" (the signed fix-up of the
    sequence compare; harmless).
  - `sub_100174F0`: "Could not recover jumptable at 0x100180e7. Too many branches" (the
    default case; all 32 handled cases decompiled).
  - `sub_10015E50`: five "Removing unreachable block" warnings (0x100166c6, 0x10016505,
    0x10016754, 0x1001668e, 0x100166fa) inside the serializer.
  - "Only resolved Ghidra call references are returned; unresolved computed or indirect
    calls remain unknown" (for `procedure_callers`).
- **Annotations could not be stored.** `set_addresses_names`, `set_address_name`,
  `set_comment` and `annotate_native_function` all returned `capability_unavailable`:
  "Windows Ghidra P0 does not admit database mutation" / "operation is not declared by
  this provider". REA also states the imported Program is ephemeral and deleted on close.
  The intended names are therefore recorded here instead:

  | Our name (alias) | Ghidra name | Role (INFERRED) |
  |---|---|---|
  | `sub_1001AB00` | `FUN_1001ab00` | HSP thread loop (`HspThread_run`) |
  | `sub_10019810` | `FUN_10019810` | console `QObject::event` override; consumes HSP payloads and HID reports |
  | `sub_10018290` | `FUN_10018290` | payload dispatcher: calls both parsers with `data+2` |
  | `sub_100174F0` | `FUN_100174f0` | receive parser, application side ("Dirty object queued") |
  | `sub_100151A0` | `FUN_100151a0` | receive parser, engine mirror (also used for local apply) |
  | `sub_10015E50` | `FUN_10015e50` | serializer `(id, a, b, c, out)` |
  | `sub_10018180` | `FUN_10018180` | `sendObject(id, a, b, c)`: serialize → queue → mirror |
  | `sub_100173B0` | `FUN_100173b0` | outgoing queue producer (seq byte, coalescing) |
  | `sub_100169C0` / `sub_10018790` | `FUN_100169c0` / `FUN_10018790` | ConsoleEvent builder / sender |
  | `sub_10005FF0` | `FUN_10005ff0` | ConsoleEvent ring push `(kind, arg, arg2)` (100 entries) |
  | `sub_100182B0` | `FUN_100182b0` | full upload PC → console (every object) |
  | `sub_10018840` | `FUN_10018840` | ChangedStatus (ID 25) sender |
  | `sub_10018220` | `FUN_10018220` | start-up handshake tick (System sub-codes 9 and 7) |
  | `sub_100171C0` / `sub_1001AA60` | `FUN_100171c0` / `FUN_1001aa60` | dirty queue append / prepend (12-byte records) |
  | `sub_100193F0` / `sub_1001A630` / `sub_1001A490` / `sub_1001A760` | `FUN_100193f0` / … | connect / find device by PID / enumerate interface / open + start thread |
  | `sub_10015E00` | `FUN_10015e00` | application callback `(id, a, b, c)` through `DAT_1009ce80` |
  | table 0x1009C6A0 | `PTR_s_Memory_1009c6a0` | object-name table (S11.6) |

#### S11.14.1 Thread loop (S11.2): CONFIRMED with two corrections

- **Observation:** the decompiled loop matches S11.2 step for step. Quotes:
  `local_aba = local_ab0; local_abc = 1; WriteFile(h, &local_abc, 0xc, …); … WriteFile(h,
  local_aae, (int)sVar5, …)` (data: header type 1, length = first word of the element,
  data from element+2); `local_abc = 2; local_aba = 0; local_ab8 = (short)iVar9; local_ab6
  = (short)uVar4; local_ab4 = local_ac8._2_2_; local_ab2 = (short)uVar6;` (fetch);
  `local_abc = 0; local_aba = 0; WriteFile(…0xc…); ReadFile(h, &local_abc, 0xc, …)` (poll
  and reply into the **same** buffer); `if (0 < local_aba) { if (local_abc == 3) {…}
  else if (local_abc != 4) qDebug("USB: Invalid Poll reply"); ReadFile(h, this+0x1a,
  (int)local_aba, …)`; match test `local_ab8 == local_aec && local_ab6 == (local_ae8 &
  0xffff) && local_ab4 == local_ae8 >> 0x10 && local_ab2 == (local_ae4 & 0xffff)`;
  re-queue on short read `FUN_1001aa60(&local_aec)` with `ECX = 0x1009cdc8` (asm
  0x1001B1A4), i.e. the dirty queue; sentinel `0x2b`; `iVar9 = 5` retry counter;
  `msleep(10)` ×3, `msleep(0x14)`.
- **CORRECTED (minor):** the 5-attempt counter decrements only when the header `ReadFile`
  returns ≠ 12 bytes. If the poll `WriteFile` itself returns ≠ 12 bytes the loop jumps
  straight to `while (0 < iVar9)` with the counter untouched and **no sleep**: a dead
  handle makes the thread spin until the stop flag is checked (which only happens after
  the inner loop exits). Not a protocol fact, but relevant for our own driver design.
- **CORRECTED (minor):** `if (0 < local_aba)` is a signed 16-bit test on w1. A header with
  w1 ≤ 0 ends the attempt without looking at w0, so a console reply with w0 ∉ {3, 4} and
  w1 = 0 would go unnoticed.
- **New:** `if (DAT_1009c5dd != 0) FUN_10018220()` runs after every successfully read
  reply header (start-up handshake, S11.14.11). The largest payload seen is tracked in
  `local_ad0` (never used).
- **Confidence:** High. **How to verify:** unchanged (E2).

#### S11.14.2 12-byte header (S11.3): CONFIRMED

- **Observation:** six consecutive 16-bit locals `local_abc, local_aba, local_ab8,
  local_ab6, local_ab4, local_ab2` = w0..w5. Poll writes only `local_abc = 0; local_aba =
  0`; data writes only `local_abc = 1; local_aba = len`; fetch writes all six
  (`local_ab8 = id, local_ab6 = a, local_ab4 = b, local_ab2 = c`). Because the reply header
  is `ReadFile`d into `&local_abc`, w2–w5 of a later poll/data header are whatever the last
  **reply** or last **fetch** left there. The "stale words" claim is CONFIRMED and sharpened:
  the stale content is usually the last reply header's w2–w5, not only the last fetch's.
- **Observation (a/b/c order):** the dirty record is `{u32 id; u32 a | b << 16; u32 c}`
  (`uVar4 = piVar3[1]; local_ac4 = piVar3[2]`), and the fetch writes `local_ab6 =
  (short)uVar4` (a), `local_ab4 = local_ac8._2_2_` (b), `local_ab2 = (short)uVar6` (c).
  The log `"Fetch object: %s (%d) %d %d %d"` prints `local_ab8, local_ab6, local_ab4,
  local_ab2` in that order, matching the `DirtyObjects` parser's `(id, a, b, c)`
  (S11.14.7). Order CONFIRMED.
- **Confidence:** High. **How to verify:** E2 as in S11.3.

#### S11.14.3 Outbound payload (S11.4): CONFIRMED

- **Observation (serializer `sub_10015E50(id, a, b, c, out)`):** `local_4 = out + 3`
  (body pointer); every case ends with `*param_5 = <bodyLen>; *(byte*)(param_5 + 1) = id`
  (`param_5` is `short*`, so `param_5 + 1` is byte offset 2). Layout `[u16 len LE][u8
  id][body]` CONFIRMED. Sixteen-bit body fields are written `FUN_100088f0(x)` (high byte)
  then `FUN_10008900(x)` (low byte): **big-endian** CONFIRMED.
- **Observation (queue producer `sub_100173B0(out)`):** `local_a9e = DAT_10343110;
  DAT_10343110 += 1; memcpy(&local_a9d, out, len + 3); local_aa0 = len + 4;` then
  `FUN_100170c0(&local_aa0)` appends and `Sleep(1)`. Element `[u16 total][seq][out…]`
  CONFIRMED; wire payload `[seq][u16 LE len][id][body]` CONFIRMED.
- **Observation (max size):** copy loop `0x2a3` dwords + 1 word = 2702 bytes CONFIRMED;
  the serializer's callers reserve 2004-byte stack buffers (`local_7d8[2004]`), so real
  bodies are ≤ 2000 bytes.
- **Confidence:** High. **How to verify:** E2 as in S11.4.

#### S11.14.4 ConsoleEvent coalescing (S11.4): CORRECTED

- **Observation:** in `sub_100173B0`, `out` is the serializer buffer, so `(char)param_1[1]`
  is `out[2]` = id, `*(char*)((int)param_1 + 3)` is `out[3]` = **body[0]**,
  `(char)param_1[2]` is `out[4]` = **body[1]**, `*(byte*)((int)param_1 + 5)` is `out[5]` =
  **body[2]**. The queued element is compared as `local_a9b == 0x14 && local_a9a == 0 &&
  local_a99 == body[1]` (element offsets 5, 6, 7 = id, body[0], body[1]) and
  `local_a98 = body[2]` (offset 8) is overwritten; the element's body[3] is untouched.
- **Observation (what the body means, builder `sub_100169C0`):** kind 0: `out[3] = 0;
  out[4] = FUN_10016970(index); out[5] = value; out[6] = DAT_1009ce84[index];
  DAT_1009ce84[index] = value; len = 4; id = 0x14`. Kind 1: `[1][FUN_10016800(index)]
  [pressed]`, len 3. Kind 2: `[2][index][value]`, len 3. The ring that feeds the builder
  (`sub_10005FF0`) is also filled by the HID branch of `sub_10019810`: HID report ID 2 →
  kind 0 with `(value << 8 | index)`, report ID 1 → kind 1 `(button, pressed)` logged as
  "Button: %d %d", report ID 3 → kind 2.
- **Interpretation:** `ConsoleEvent` kind 0 = fader move `[0][fader][new level][previous
  level]`; SmartSoft coalesces successive moves of the same fader by updating the new
  level and keeping the first "previous level". Kind 1 = button `[1][button][pressed]`,
  kind 2 = a third control class (encoder / wheel?), UNKNOWN. The index translation
  helpers (`sub_10016970`, `sub_10016800`) were not decompiled; the wire index may differ
  from the HID index.
- **Confidence:** Medium-high for the byte positions, medium for the semantics.
- **How to verify:** E3 (virtual fader move in SmartSoft while capturing): OUT data
  payloads with id 20, body[0] = 0, constant body[1], rising/falling body[2], body[3] equal
  to the level before the move.

#### S11.14.5 Inbound payload (S11.5): CONFIRMED for bytes 0 and 4, UNRESOLVED for 1–3

- **Observation:** `sub_10019810`: `bVar10 = *(byte *)local_104[3]` (QByteArray data[0]);
  `if (bVar10 != (byte)(DAT_1009c878 + 1)) qDebug("Message out of sequence: %d %d");
  DAT_1009c878 = bVar10; FUN_10018290();`. Ghidra dropped the argument; the Listing shows
  `0x10019afe ADD ESI,0x2; PUSH ESI; CALL 0x10018290` with ESI = data pointer, so the
  parsers receive **data+2**. `sub_10018290(p)` is just `FUN_100151a0(p); FUN_100174f0(p);
  DAT_1009c5dc = 0;`. Both parsers switch on `p[2]` (`switch(param_1[2])`,
  `switch(*(undefined1 *)(param_1 + 2))`) and read the body from `p[3]`.
- **Observation (bytes 1–3):** no instruction in `sub_10019810`, `sub_10018290` or either
  parser reads `data[1]`, `p[0]` or `p[1]`. The hex dump prints them, nothing interprets
  them.
- **Interpretation:** inbound = `[seq][?][?][?][id][body]`; the "u16 len at 2–3" reading
  of S11.5 is a plausible mirror of the outbound layout but has **no code evidence**.
- **Confidence:** High for byte 0 (sequence, 8-bit, +1) and byte 4 (id); none for 1–3.
- **How to verify:** E2 as in S11.5 (test LE and BE length at 2–3, record byte 1).

#### S11.14.6 Object IDs and FileName endianness (S11.6): CONFIRMED

- **Observation:** `read_bytes(0x1009c6a0, 176)` gives 44 pointers; strings 0..41 are
  exactly the S11.6 table (24 DeviceStatus, 25 ChangedStatus, 26 Personality,
  27 PersonalityLibraryReset, 28 PersonalityUpdate, 29 PatchDevice, 30 UnpatchDevice,
  31 RepatchDevice, 32 InsertStep, 33 InsertPointQStep, 34 DeleteStep, 35 MoveStep).
  Entry 42 points to "bad allocation" and entry 43 to non-string data, so the table has
  **42 entries**. The serializer nevertheless has `case 0x2a` (ID 42, 4-byte body) and
  the mirror parser handles `case 0x2a`; a log line for ID 42 would print "bad
  allocation" as the name. ID 42 is a real, unnamed object (UNKNOWN purpose).
- **Observation (FileName, ID 36):** application parser `case 0x24`: `param_1 = p + 4;
  … (ushort)param_1[1] * 0x100 + (ushort)*param_1` → `body[2] << 8 | body[1]`
  (**little-endian**, stops at 0, at most `body[0]` chars). Mirror parser `case 0x24`:
  `pbVar12 = p + 5; uVar5 = pbVar12[-1] * 0x100 + *pbVar12` → `body[1] << 8 | body[2]`
  (**big-endian**), stored to `DAT_10374f00[]` and additionally filtered into a ≤ 12-char
  `[A-Za-z0-9.]` ASCII name at `DAT_10363ce0` (an 8.3 file name). Serializer `case 0x24`:
  `body[0] = 0x3c; for (i = 0; i < 0x3c; i += 2) { *local_4 = byte[i + 1]; local_4[1] =
  byte[i]; }` → 30 u16 characters, **high byte first**, from the same `DAT_10374f00`.
- **Interpretation:** the contradiction is real: two of three code paths (mirror parser,
  serializer) use BE, one (the parser that feeds the SmartSoft UI string) uses LE. Also
  note the serializer puts **60** (bytes) in body[0] while both parsers treat body[0] as a
  **character** count; only the 0-terminator keeps that consistent.
- **Confidence:** High that the code disagrees; the wire format stays UNRESOLVED.
- **How to verify:** E2: body of the FileName payload after a show load; see H22.

#### S11.14.7 DirtyObjects record (S11.7): CONFIRMED

- **Observation:** `case 0x28: if (CONCAT11(param_1[3], param_1[4]) != 0)` → count is
  **u16 BE** at body[0..1]. Listing 0x10018020–0x10018073 per record (EDI = record):
  `MOVZX EBP,[EDI]` (id), `MOVZX SI,[EDI+1]`, `MOVZX DX,[EBX+1]` (= record+2), `SHL SI,8;
  ADD SI,DX` (a = BE u16 of bytes 1–2), `MOVZX AX,[EDI+2]` after `ADD EDI,1` (= record+3,
  b), `MOVZX CX,[EDI+1]` after `ADD EDI,2` (= record+4, c); stored as `dword [ESP+0x10] =
  id; word [+0x14] = a; word [+0x16] = b; word [+0x18] = c` and passed to
  `sub_100171C0` with `ECX = 0x1009cdc8` (12-byte `QListData::append`). Record
  `[id][u16 BE a][b][c]`, 5 bytes, CONFIRMED; element `{u32 id; u16 a; u16 b; u16 c; pad}`
  CONFIRMED. After each record the application callback is invoked as
  `callback(0x27 /* System */, 8, remaining_count, 0)` (progress notification).
- **Confidence:** High. **How to verify:** E2 as in S11.7.

#### S11.14.8 Sequence numbers (S11.8): CONFIRMED

- `DAT_10343110` (outbound, post-incremented in `sub_100173B0`) and `DAT_1009c878`
  (inbound, `(byte)(DAT_1009c878 + 1)` compare, then overwritten) are the only two
  counters; neither is reset in the decompiled code. Confidence high.

#### S11.14.9 Timing (S11.9): CONFIRMED, one addition

- `QThread::msleep(10)` after the data write, after a fetch and after a re-fetch;
  `QThread::msleep(0x14)` after every poll attempt that got as far as the `ReadFile`;
  `Sleep(1)` in the queue producer (GUI thread). The "spin on failed poll write" of
  S11.14.1 is the only new timing fact.

#### S11.14.10 Device discovery and IOCTLs (S11.10): CONFIRMED

- `sub_1001A490`: `local_3c.Data1 = 0x8d7140c7; Data2 = 0xbcae; Data3 = 0x4be2; Data4 =
  a5 c2 df e4 8e bb 75 9a` (= `{8D7140C7-BCAE-4BE2-A5C2-DFE48EBB759A}`);
  `SetupDiGetClassDevsW(&guid, 0, 0, 0x12)`; `SetupDiEnumDeviceInterfaces(…, index, …)`;
  `CreateFileW(path, 0xc0000000, 0, 0, 3, 0x80, 0)`.
- `sub_1001A630(short pid, QString *path)`: `for index 0..1: DeviceIoControl(h, 0x222008,
  0, 0, buf, 0x800, …)` ("product string"), `DeviceIoControl(h, 0x222000, 0, 0,
  local_814, 2, …)` ("PID"), `DeviceIoControl(h, 0x222004, 0, 0, local_808, 2, …)`
  ("BcdDevice"), `CloseHandle`, `if (local_814[0] == param_1) break;`.
- `sub_100193F0`: `FUN_1001a630(0x200, this + 0x10, …)`, then "Console: HIGHSPEED Path",
  `operator_new(0xaf0)`, thread named "HighSpeedPipeline", `sub_1001A760` →
  `CreateFileA(path, 0xc0000000, 0, 0, 3, 0x80, 0); QThread::start(this, 5)`
  (Qt4 `HighestPriority`). Ghidra types `sub_1001A630` as `void` although the caller
  tests its return value; this is a decompiler signature limitation, not a contradiction.
- All of S11.10 CONFIRMED. Confidence high.

#### S11.14.11 New observations (not in S11.2–S11.10)

- **Start-up handshake (`sub_10018220`, relevant to H4).** While `DAT_1009c5dd != 0`
  (set at connect), every reply header triggers: `DAT_1009c7d0--`; when it reaches 0,
  `sendObject(0x27 /* System */, 9, 0, 0)`; then a second countdown `DAT_1009c7cc--`;
  when that reaches 0, `if (QString(DAT_1034208c /* received ConsoleVersion */) ==
  FUN_1000a400() /* own version string */) sendObject(0x27, 7, 0, 0); DAT_1009c5dd = 0`.
  **Interpretation:** the version comparison is done by the PC after it has received
  `ConsoleVersion` (ID 37, body = `[n][n ASCII bytes]`); on a match the PC sends `System`
  sub-code 7. What the PC does on mismatch is not in this function (the UI string lives in
  SmartSoft.exe). **Confidence:** medium. **How to verify:** E2: early in the session
  expect an OUT data payload with id 39 and body `[9][0][0]`, later `[7][0][0]`.
- **`System` (ID 39) is a command object.** Serializer: body `[a][b][c]` (3 bytes). Sent
  by the PC with a ∈ {0, 1, 6, 7, 9} (`sub_100182B0`, `sub_10018220`) and with a = 8 only
  through the *application callback* (progress, never on the wire). The mirror parser
  understands a ∈ {10 (with b < 12), 11, 12, 13, 16}. Semantics UNKNOWN; "6" precedes a
  full upload and "1" ends it.
- **Full upload order (`sub_100182B0`).** `System(a=0)`, `System(a=6)`, `Patch`, `Device`
  per used slot, `Palette` per flagged slot, `Memory(a = page, b = 0..47)` for non-empty
  memories, per page `Page(a)` then `Sequence(a = page, b = 0..11)` for non-empty
  sequences, `SequenceMain(a = 0, b = 12)` + `StepContent(a = step)` per step,
  `Group`, `ChannelText(a = 0..95)`, `Effects`, `DeviceStatus`, `FileName`,
  `Independents`, `System(a=1)`. This fixes the qualifier semantics for Memory
  (**a = page, b = slot**; the engine array is 12 × 48 memories, `DAT_1009ce78` = 24 or
  48 memories per page) and Sequence (**a = page, b = sequence in page**; mirror array
  stride 4 per page). `c` is always 0 on the PC side.
- **`SystemSizes` (ID 38) fields.** `body[0]` → channel count (compared with 0x60 = 96 for
  the 2496 layout), `body[1..2]` u16 BE → patch byte count (used as the `memcpy` length of
  `Patch`, so 512 is expected), `body[3]` → compared with 0x30 = 48: if equal the engine
  switches to 48 memories per page (2496), otherwise 24 (1248); `body[4..6]` stored,
  `body[7]` → `DAT_1034203e` (echoed in outbound `SystemSettings`). Semantics of 4–6
  UNKNOWN.
- **`ChannelLevels` (ID 15).** `n = body[0]; memcpy(levels, body + 1, n - 2);` then
  `body[n - 1]` and `body[n]` are compared with the two `Independents` records
  (`DAT_1033d9c2`, `DAT_1033d9f2`) and trigger an Independents callback when changed.
  **Interpretation:** `[n][n − 2 channel levels][IND1][IND2]`, i.e. n = 50 on a 1248
  (consistent with H11's "channels 49/50 are the independents"). Confidence medium.
- **`SystemSettings` (ID 41) asymmetry.** The parser consumes 18 body bytes
  (`body[1..11]` bytes, three u16 BE at 12–17); the serializer emits 19 (`body[0..11]`,
  then `body[12] = DAT_1034203e`, then the three u16 BE at 13–18). One extra byte on the
  way out; UNKNOWN whether the console tolerates or expects it.
- **`ChangedStatus` (ID 25, `sub_10018840`).** Sent from a 100-entry ring: body
  `[0][u16 BE hi-word][byte][byte]` (5 bytes) per entry whose flag is 0. Meaning UNKNOWN.
- **HID path.** `sub_10019810` also handles "DeviceStreamDataAvailable HIDConsole": HID
  report ID 1 = button bitmap (bit changes logged "Button: %d %d"), ID 2 = fader values
  (changed bytes → kind-0 events, cached at `DAT_10343118[]`), ID 3 = a third report. This
  is the first code evidence that the engine reads the **HID interface** of a console
  (H1/H5) in addition to the HSP pipe.
- **Offline flag.** `DAT_1009c5dc` ("no console"): `sendObject` skips the USB queue and
  applies the object locally through the mirror parser when it is set; cleared on connect
  and on the first received payload.

#### S11.14.12 Status of the S11.13 unknowns after this pass

| Unknown | Status after S11.14 |
|---|---|
| Inbound byte 1 | UNKNOWN (read by no code) |
| Inbound bytes 2–3 = length? | UNRESOLVED (read by no code; HYPOTHESIS only) |
| Qualifiers a/b/c: Memory | a = page, b = slot (INFERRED, medium-high); c unused |
| Qualifiers a/b/c: Sequence | a = page, b = sequence in page (INFERRED, medium); c unused |
| Qualifiers a/b/c: ChannelLevels | PC sends (0, 0, 0); console side UNKNOWN |
| Qualifiers a/b/c: ConsoleEvent | not fetched by qualifier; body carries kind/index/value (S11.14.4); PC sends it with (0,0,0) via the builder |
| What the console sends when idle | still unobserved; code treats any header with w1 ≤ 0 as "nothing" |
| Initial sequence values | UNKNOWN (no reset code found) |
| Body layouts | sketched for IDs 0, 2, 3, 4, 5, 12, 15, 20, 25, 36, 37, 38, 39, 40, 41 (serializer and parsers agree except FileName endianness and the SystemSettings extra byte) |

---

## Open static-analysis leads (only if captures prove insufficient)

1. ~~Locate the code referencing the `Fetch object` / `Poll` strings…~~ **Done 2026-10-16:
   S11, re-verified with Ghidra in S11.14.** Request builders `sub_100182B0` /
   `sub_10018840` read (S11.14.11). Remaining sub-leads: the index translation helpers
   `sub_10016970` / `sub_10016800` (ConsoleEvent fader/button numbering), the text helpers
   `sub_10013970` / `sub_100139F0` (label encoding), and the `System` sub-code handlers;
   only if E2/E3 leave ambiguity.
2. ~~`DeviceIoControl` IOCTL codes…~~ **Done: S11.10** (0x222000 PID, 0x222004 BcdDevice,
   0x222008 product string; driver-private, not interpreted).
3. ~~GUID `{8D7140C7-BCAE-4BE2-A5C2-DFE48EBB759A}`: purpose UNKNOWN.~~ **Resolved
   (INFERRED): HSP device-interface GUID used with SetupDiGetClassDevsW, S11.10.** Its use
   in `SmartSoft.exe` was not re-checked.
4. ~~Byte pair `D5 14` at `sfEngine.dll` offset `0x99070`: may be the VID check.~~
   **REJECTED, S11.11.** No VID check exists in `sfEngine.dll`; selection is by interface
   GUID + PID 0x0200.
5. New: `sfmlEngine.dll` — which PID does the ML engine ask for? (Would confirm the
   PID-per-product reading of S11.10. Low priority; not a 1248 question.)
