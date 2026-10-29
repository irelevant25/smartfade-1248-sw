# 2026-10-28 E1: USB identification of the owner's SmartFade 1248

- Date: 2026-10-28, 18:49–19:20 local time
- Experiment ID: **E1** (read-only USB identification) plus a passive HID listen ("E1-HID")
- Console: ETC SmartFade 1248. Serial: UNKNOWN (rear panel not read). Firmware:
  **3.0.2.9.0.43** (owner's reading of *Menu > Diagnostics > Software version* at about 19:55,
  F48, `2026-10-28-e1-owner-statements.txt`). Operating mode: not checked (owner to confirm
  Normal mode).
  USB identity: `VID_14D5&PID_0200&REV_0103`, product string "ETC SmartFade".
- Host: owner's main PC, Windows 11 Pro build 26300 (x64), **Memory Integrity (HVCI) on**.
  SmartSoft 3.0.43 installed (MSI, 2026-02-03) but **not running**. ETC driver package
  `etchsp.inf` 6.0.3.0 installed (`etchsp.sys` 2.00.000, amd64).
- Connection: USB A-B cable straight into the PC (xHCI root hub `USB\ROOT_HUB30\5&201A262C&0&0`,
  port 11; briefly port 12 at 19:03).
- Show backup taken: not applicable. Nothing was sent to the console beyond standard USB
  enumeration and GET_DESCRIPTOR reads issued through the hub driver. No HID output or feature
  reports, no HSP traffic (the HSP driver is not even loaded, see below).
- Rig (owner's statement, not used in E1): 4 RGB fixtures, 3 channels each, DMX start
  addresses 5, 8, 11, 14.
- Tools: `tools/usb-probe/e1-usb-descriptors.ps1` (GET_DESCRIPTOR through the parent hub,
  `IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION`, the USBView / USBTreeView mechanism),
  `tools/usb-probe/e1-hid-probe.ps1` (HID class driver queries, whose string queries make the
  class driver issue standard GET_DESCRIPTOR(STRING) requests on endpoint 0, plus `ReadFile`),
  `pnputil`, `Get-PnpDeviceProperty`, Windows event logs. Both scripts were revised after the
  safety review the same evening; the hashes below are of the revisions that produced the
  evidence (see `tools/usb-probe/README.md`).

## Action log

| Time | Action |
|---|---|
| 18:49:53 | Console plugged in. Kernel-PnP: composite device, `MI_00` (HID) and `MI_01` (HSP) configured; `MI_01` driver fails to load |
| 19:02–19:04 | Console re-plugged several times (root-hub ports 11 and 12). Same result every time |
| ~19:05 | `pnputil /enum-devices /connected /ids` and a PnP property dump read in-session (driver blocked); the archived `2026-10-28-e1-pnputil.txt` was regenerated at 19:20 in the same state |
| ~19:10 and 19:16 | Descriptor dump through the hub, run twice; the bytes were compared in-session and were identical, only the second run is archived → `2026-10-28-e1-descriptors.txt` |
| 19:17:14 | HID probe, then 10 s passive listen with nobody touching the console → `2026-10-28-e1-hid-probe.txt` |
| ~19:19 | Driver facts + event-log excerpt → `2026-10-28-e1-driver-events.txt` |
| 19:36:32 | Second passive HID listen, 60 s, after asking the owner in chat to move channel fader 1 and press a bump: **0 reports**. Whether anyone touched the console during that minute is unconfirmed, so this run is **inconclusive** for H28 → `2026-10-28-e1-hid-listen-60s-inconclusive.txt` |
| 19:45:49 | Owner switched Memory Integrity off and rebooted (`Win32_DeviceGuard.SecurityServicesRunning` now `{0}`) |
| 19:50:02 | Owner ran `e1-hid-probe.ps1 -ListenSeconds 30` with the instruction to move channel fader 1 and press a bump: **0 reports** → `2026-10-28-e1-hid-listen-owner-30s.txt` (F50, H28 rejected, H29 opened) |
| ~20:00 | Driver check: service `ETCHSP` running, `MI_01` node OK "USB High-Speed-Pipe", one `{8D7140C7-…}` device interface registered for it (F49). Owner reports firmware **3.0.2.9.0.43** from the Diagnostics menu (F48). USBPcap 1.5.4.0 found in `C:\Program Files\USBPcap`, its service stopped |
| ~20:15 | Protocol-safety review of this record and the scripts; wording tightened, missing evidence archived: `2026-10-28-e1-pnp-properties.txt`, `2026-10-28-e1-smartsoft-install.txt`, `2026-10-28-e1-driver-loaded.txt` (all regenerated in the driver-loaded state), `2026-10-28-e1-owner-statements.txt` |

## Evidence files

How each file was produced is stated in its header and here. Files are CRLF text; the
repository's `.gitattributes` keeps `docs/protocol/captures/**` and `tools/usb-probe/**` free of
line-ending conversion so the hashes reproduce after a checkout.

| File | SHA-256 | How produced | Content |
|---|---|---|---|
| `2026-10-28-e1-pnputil.txt` | `5c6f05a628119a62aa0e6af85eff3ec4716e9662741eeacac6f7b2f45e28a17b` | `pnputil /enum-devices /connected /ids`, output filtered to the blocks containing `VID_14D5`, two-line header added | Four device nodes in the driver-blocked state |
| `2026-10-28-e1-descriptors.txt` | `8829cfb1bff74c1f6d351e0463f30991a93ff6a2a844a961957c19d5a8929cb5` | Unedited output of `e1-usb-descriptors.ps1` (E1 revision), one-line header added | Hub-level connection info, device descriptor, configuration descriptor, failed string / HID-report-descriptor requests |
| `2026-10-28-e1-hid-probe.txt` | `c6a72d3c9af1d9cbc9dd03505b50dae1e84673e95992dfc4c7c850da4e55d61b` | Unedited output of `e1-hid-probe.ps1 -ListenSeconds 10` (E1 revision), one-line header added | `HIDD_ATTRIBUTES`, strings, `HIDP_CAPS`, link collections, value caps, passive listen result |
| `2026-10-28-e1-driver-events.txt` | `f55cebee3187384844ad69fc6e4d501784c1c1dc255c2b0c7920cfe00e1e20c1` | Composed by a PowerShell one-liner: system facts (lines 1–8, with explanatory remarks in parentheses) followed by unedited event-log lines | OS / HVCI state, `etchsp.sys` file facts, Kernel-PnP and CodeIntegrity events in the blocked state |
| `2026-10-28-e1-hid-listen-60s-inconclusive.txt` | `37fc9eb2f68f425c5b10c711188e9ec6d119970b4fd5674d01132188993c8a11` | Unedited output of `e1-hid-probe.ps1 -ListenSeconds 60` (E1 revision), header added | The inconclusive 19:36 run, 0 reports |
| `2026-10-28-e1-hid-listen-owner-30s.txt` | `80d5f81d13c918b0ae94a8136a97eb81e3be8038a8476498bb7f63ca8393119f` | Pasted by the owner from their shell (prompt line included), four-line header added | The owner-run 19:50 listen, 0 reports (F50) |
| `2026-10-28-e1-owner-statements.txt` | `45bffc4884c85ffb43070de61d2f280b3f4b674d46298fe17980f40cb528e6c6` | Transcribed from the chat by Claude | Rig, firmware reading, Memory Integrity and USBPcap statements (F48) |
| `2026-10-28-e1-pnp-properties.txt` | `aa703620f74182680269a2ea909c4977ca5fbfe68c4e9649fee4bdb5b342037b` | Unedited `Get-PnpDevice` / `Get-PnpDeviceProperty` output, regenerated at ~20:15 in the driver-loaded state | Every PnP property of the four nodes (bus-reported description, container, services, driver versions) |
| `2026-10-28-e1-smartsoft-install.txt` | `89aca0589b2124a64dc0935c66ce6e0d70b64e89055fc149dde9ea72528de22a` | Unedited registry / directory / `Get-FileHash` output, ~20:15 | SmartSoft installation facts (F47) |
| `2026-10-28-e1-driver-loaded.txt` | `aa0d98a0f0d9c14b38dcde6becd3bf84270b4ae2393bd162f7e59cf9ddf5de2f` | Unedited PowerShell output, ~20:15 | Memory Integrity off, service state, device nodes, `{8D7140C7-…}` interface registration, Kernel-PnP events since the reboot (F49) |
| `tools/usb-probe/e1-usb-descriptors.ps1` | E1 revision `8e12d83f4e6e44fef51049d32dbf98c3cce26e04a41846f17509e5e1cd781997`; current `fc8463909602ac4e1e2675086003942480fb7f04d83f2a12c700b6c2500d3ad2` | Script; current revision opens the hub with access 0 and was re-run once at ~20:20 with identical device bytes (not archived) | Produced the descriptor file |
| `tools/usb-probe/e1-hid-probe.ps1` | E1 revision `fc0aa9ea358ec9c1de7dfe560786f0655117d90c88aef6c1f9fbd7a1be4d1997`; current `56f0acad91f034980abaa130009fc6aba91a9c0738d97f9508118c29b121da49` | Script; current revision groups reports by the console's first byte and prints all reports | Produced the HID files |

## Observed result

### Device identity

- Instance `USB\VID_14D5&PID_0200\6&28cf390b&0&11`, "USB Composite Device" (`usbccgp`),
  hardware IDs `USB\VID_14D5&PID_0200&REV_0103`, `USB\VID_14D5&PID_0200`; bus-reported
  description **"ETC SmartFade"**; container `{FA470443-D252-11F1-90E9-AC30ECE91BB6}`.
- Device descriptor, identical from the hub cache and from a live GET_DESCRIPTOR:
  `12 01 10 01 00 00 00 40 D5 14 00 02 03 01 01 02 00 01`
  → bcdUSB 1.10, class/subclass/protocol 00 (per interface), EP0 max packet 64,
  idVendor 0x14D5, idProduct 0x0200, **bcdDevice 0x0103**, iManufacturer 1, iProduct 2,
  **iSerialNumber 0 (no serial number)**, 1 configuration.
- Speed: **full speed** (`USB_NODE_CONNECTION_INFORMATION_EX.Speed = 1`), device address 50,
  4 open pipes (the four endpoints of configuration 1).
- Strings through the HID class driver: manufacturer **"ETC"**, product **"ETC SmartFade"**,
  serial: none (error 87, consistent with iSerialNumber 0). String reads through the hub
  IOCTL failed with ERROR_GEN_FAILURE for every index, even for a 2-byte header read; the
  cause was not investigated (Windows itself obtained the product string at enumeration).

### Configuration descriptor (64 bytes)

```
09 02 40 00 02 01 00 C0 00            configuration 1: 2 interfaces, self-powered flag, 0 mA
09 04 00 00 02 03 00 00 00            interface 0: HID, 2 endpoints
09 21 11 01 00 01 22 1C 00            HID 1.11, country 0, report descriptor 28 bytes
07 05 81 03 40 00 0A                  EP 0x81 interrupt IN, 64 bytes, interval 10
07 05 02 03 40 00 0A                  EP 0x02 interrupt OUT, 64 bytes, interval 10
09 04 01 00 02 FF 00 00 00            interface 1: vendor specific, 2 endpoints
07 05 83 02 40 00 00                  EP 0x83 bulk IN, 64 bytes
07 05 04 02 40 00 00                  EP 0x04 bulk OUT, 64 bytes
```

| Interface | Class | Endpoints | Windows binding on this PC |
|---|---|---|---|
| 0 (`MI_00`) | HID 03/00/00, HID 1.11, 28-byte report descriptor | 0x81 interrupt IN 64 B / 10 ms, 0x02 interrupt OUT 64 B / 10 ms | `HidUsb` ("USB Input Device") → `HID\VID_14D5&PID_0200&MI_00`, "HID-compliant device", usage page 0000 / usage 0000. Started |
| 1 (`MI_01`) | vendor FF/00/00 | 0x83 bulk IN 64 B, 0x04 bulk OUT 64 B | `etchsp.inf` (published as `oem64.inf`) "USB High-Speed-Pipe", class ETCHspClass. **Problem code 39** |

No interface strings (iInterface 0), iConfiguration 0.

### HID interface (`MI_00`): structure as parsed by Windows

- `HIDD_ATTRIBUTES`: VendorID 0x14D5, ProductID 0x0200, VersionNumber 0x0103.
- `HIDP_CAPS`: Usage 0x0000, UsagePage 0x0000, InputReportByteLength 65,
  OutputReportByteLength 65, FeatureReportByteLength 0, 2 link collections
  (application collection containing one physical collection), 1 input value cap,
  1 output value cap, no button caps, no feature caps.
- Input value: usage page 0x0000, usage 0x0001, report ID 0, 8-bit × 64, logical -128..127,
  absolute. Output value: usage 0x0002, otherwise identical.
- Hence **no HID report IDs**: each direction is one raw 64-byte report. Windows user-mode
  buffers are 65 bytes; byte 0 is the report-ID byte, 0x00 when the descriptor defines no IDs
  (DOCUMENTED Windows HID behaviour; no report was received, so this byte was never seen).
- The raw 28-byte report descriptor could not be read through the hub IOCTL
  (ERROR_GEN_FAILURE). The structure above is reconstructed from the preparsed data.
- Passive listen for 10 s with the console idle: **0 input reports**.

### ETC driver on this PC

- `etchsp.sys`: 22 728 bytes, file version 2.00.000, product "High-Speed-Pipe", company ETC,
  x64 PE built 2010-04-30, SHA-256
  `ffb2f11f36c4805fa9e2c144050876d6d2796455f63b2b7f79a98dca50338496` (byte-identical to the
  amd64 copy in the SmartSoft 3.0.2.9.0.43b installer, see `docs/source-materials.md`).
  Authenticode valid, signer "Electronic Theatre Controls, Inc." (certificate valid to
  2018-02-25, timestamped).
- Windows refuses to load it while Memory Integrity is on:
  CodeIntegrity event 3111 *"attempted to load etchsp.sys that is not compatible with
  hypervisor enforcement. Failure bitmap 0x2. Status 0xC0000220"* → Kernel-PnP event 219
  *"\Driver\ETCHSP failed to load … Status 0xC000026C"* → device problem 39
  (CM_PROB_DRIVER_FAILED_LOAD), problem status 0xC000007B.
  `Win32_DeviceGuard.SecurityServicesRunning = {2}` (HVCI running).
- Consequence: on this PC SmartSoft cannot open the HSP interface, so it cannot go "Online".
  E2 needs Memory Integrity switched off for the session (owner's decision, reboot required)
  or a VM / spare PC where it is off.

### SmartSoft on this PC

- MSI product `{583AA4E9-DF76-4D6F-B212-5492A246773F}`, DisplayVersion 3.0.43, installed
  2026-02-03 from the self-extracting installer (`IXP000.TMP`). Files under
  `C:\Program Files (x86)\ETC\SmartSoft\` (Binaries, Documentation, Drivers, Examples,
  Firmware Updates, Resources, Showfiles) and `C:\Program Files (x86)\ETC\HighSpeed_Pipeline_Drivers\`.
- Installed binaries: `sfEngine.dll` 671 744 bytes, SHA-256
  `3228bd9bb59906f6b7efb0f99170608a511672ba6cc977c8de7c88c171487983`; `SmartSoft.exe`
  6 008 832 bytes, SHA-256 `4c4c9cf77bf57367b3f427c0006b5400ce42c36582ea5fa8c3e8174e280c8c01`.
  Both are byte-identical to the installer copies listed in `docs/source-materials.md`, so
  the static analysis in `smartsoft-analysis.md` describes exactly the binaries on this PC.
- `HKCU\Software\ETC\SmartSoft` exists but is empty: SmartSoft has probably never been run
  with a console on this PC.

## Conclusions and status changes

- **H1 → CONFIRMED** (facts F40, F42, F43): composite `VID_14D5`, interface 0 HID,
  interface 1 vendor class bound as "USB High-Speed-Pipe".
- **H21 → CONFIRMED** (F40): PID 0x0200.
- **H5 refined**; **H28 opened and rejected (provisionally)** the same evening; **H29 opened**:
  the HID interface is a raw 64-byte report pipe in both directions with no report IDs; it was
  silent at idle (F45) and in the owner-run listen with fader movement requested (F50).
- **H7 priority raised**: `etchsp.sys` cannot load while Memory Integrity is on, and the owner
  wants it on, so a user-mode driver (WinUSB on interface 1 + the standard HID class driver on
  interface 0) is the option that satisfies that (INFERRED that Memory Integrity is on by
  default on new Windows 11 installations; one build observed). Binding WinUSB would be a
  deliberate, reversible owner action that stops SmartSoft on that PC. H7 is untested.
- The "HID report IDs 1 / 2 / 3" of `smartsoft-analysis.md` S11.14.11 cannot be HID report
  IDs (there are none). INFERRED: the engine switches on the console's first byte, which is
  buffer offset 1 behind Windows' report-ID byte (holds only if the code reads offset 1).
- bcdDevice 0x0103 does not look like firmware "3.0.2.9.0.43" (F48). Its meaning stays
  UNKNOWN. HYPOTHESIS (H21 note): IOCTL 0x222004 returns this value.
- Not obtained: raw HID report descriptor bytes, string descriptors through the hub, a USB
  serial number (the device has none). The `{8D7140C7-…}` interface listing was obtained
  later in the evening once the driver loaded (F49).
- All hardware facts are **OBSERVED**, not VERIFIED: one session, one PC. Only an independent
  reproduction on our unit with its own evidence entry promotes them.

## Open follow-ups

1. ~~Owner: read the firmware version~~ **Done: 3.0.2.9.0.43 (F48).** Still open: rear-panel
   serial number; confirm the console was in Normal mode.
2. ~~Owner: choose the E2 host.~~ **Done: Memory Integrity off on this PC, driver loads (F49),
   USBPcap 1.5.4.0 installed.** The owner wants Memory Integrity back on afterwards.
3. ~~Zero-risk H28 test.~~ **Done by the owner: 0 reports (F50). H28 rejected, H29 opened.**
4. Reproduce E1 on another PC or after a power cycle to promote F40–F45 to VERIFIED.
5. Next: E2 (SmartSoft session capture with USBPcap), after the owner backs up the show to SD.
