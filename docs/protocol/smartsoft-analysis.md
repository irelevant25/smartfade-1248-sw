# SmartSoft static analysis

Scope: `SmartSoft 3.0.2.9.0.43b.exe` and the files it contains
(extracted with `tools/source-audit/extract_smartsoft_installer.py`). All analysis is
static: headers, imports, exports, resources, strings. Nothing was executed, installed or
disassembled. String evidence is a **clue, not proof** of runtime behaviour.

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

## Open static-analysis leads (only if captures prove insufficient)

1. Locate the code referencing the `Fetch object` / `Poll` strings in `sfEngine.dll` and
   recover message framing (header layout, object IDs, sequence numbers). This is
   disassembly and should only be done if captures leave ambiguity.
2. `DeviceIoControl` IOCTL codes used against `etchsp.sys`, e.g. for reading `BcdDevice`/PID.
3. GUID `{8D7140C7-BCAE-4BE2-A5C2-DFE48EBB759A}` in `SmartSoft.exe`: purpose UNKNOWN.
4. Byte pair `D5 14` (little-endian 0x14D5) at `sfEngine.dll` offset `0x99070`: may be the VID
   check. UNKNOWN.
