# Known facts

Only DOCUMENTED, INFERRED-with-high-confidence or VERIFIED items. Each has a source.
Nothing here has yet been VERIFIED on the user's console. The verified count is **0**.

Last updated: 2026-10-16 (Ghidra re-verification of S11; duplicate IDs F14/F15 in the SmartSoft rows renumbered to F16/F17).

## Hardware

| # | Fact | Status | Source |
|---|---|---|---|
| F1 | The product is the ETC **SmartFade** 1248 (SF1248), not "SmartFace" | DOCUMENTED | Spec sheet, all manuals |
| F2 | Interfaces: DMX512 out (1 universe), DMX512 in, MIDI In, MIDI Out, USB (Series B device), SD card slot. No RS-232 and no Ethernet listed | DOCUMENTED | Spec sheet "Interfaces"; RTF spec D.1 |
| F3 | Normal mode: 48 channels, 288 memories (12 pages × 24), 48 sequences (12 × 4) of ≤ 24 steps, 199-step stack, 10 snapshots, 2 independents, GM, blackout, bumps master | DOCUMENTED | Spec sheet; RTF; console manual v3.0 |
| F4 | Operating modes Full Control / TwoScene / DMXBackup, selected at power-up | DOCUMENTED | Console manual v3.0 p.5 |

## SmartSoft ↔ console link

| # | Fact | Status | Source |
|---|---|---|---|
| F10 | SmartSoft connects to SmartFade consoles over **USB** and needs an ETC driver. Without it the console shows as "Unrecognized USB device". With it the device is "USB High-Speed-Pipe" | DOCUMENTED | SmartSoft manual 3.0.1 p.4 |
| F11 | The ETC driver (`etchsp.sys`, "High-Speed-Pipe", by Peperoni-Light) binds USB `VID_14D5` with PIDs `0103, 0110, 0112, 0200, 0201` (+`FFFB` on 64-bit lists), **interface `MI_01`** | INFERRED (driver INF content, high) | `etchsp.inf` in installer |
| F12 | The SmartSoft application and its SmartFade engine DLL import no Windows MIDI (winmm), DirectMusic or serial-port APIs | INFERRED (import tables, high) | `pe_report.py` on SmartSoft.exe, sfEngine.dll |
| F13 | `sfEngine.dll` opens a device by SetupAPI interface path and uses ReadFile/WriteFile/DeviceIoControl. Its strings reference HighSpeedPipeline, Poll, Fetch object, DirtyObjects, timeouts and retries | INFERRED (imports + strings, high) | smartsoft-analysis.md S4, S6 |
| F14 | `sfEngine.dll` selects the console by enumerating device-interface GUID `{8D7140C7-BCAE-4BE2-A5C2-DFE48EBB759A}` (interface index 0 and 1 only) and keeping the interface whose `DeviceIoControl(0x222000)` returns PID **0x0200**; IOCTLs 0x222004 (BcdDevice) and 0x222008 (product string) are also issued. There is no VID comparison in the engine | INFERRED (hand-read disassembly + Ghidra pseudocode agree, high; not observed on hardware) | smartsoft-analysis.md S11.10 and S11.14.10, 2026-10-16 |
| F15 | `sfEngine.dll` identifies protocol objects by an 8-bit ID that indexes a 42-entry name table (0 Memory … 20 ConsoleEvent … 36 FileName, 37 ConsoleVersion, 38 SystemSizes, 40 DirtyObjects, 41 SystemSettings; full list S11.6). Receive handlers for IDs 40, 36, 37 and 38 behave as their names suggest. ID 42 is used by code but has no table entry | INFERRED (hand-read disassembly + Ghidra pseudocode and table bytes agree, high) | smartsoft-analysis.md S11.6 and S11.14.6 |
| F16 | SmartSoft requires console Normal mode and a console firmware version matching SmartSoft | INFERRED (UI strings, medium-high) | SmartSoft.exe strings; manual p.5 |
| F17 | SmartSoft 3.0.2b package contains SmartSoft, SmartFade and SmartFade ML software, all version 3.0.2.9.0.43 | DOCUMENTED | `Setup Information.rtf` |

## MIDI

| # | Fact | Status | Source |
|---|---|---|---|
| F20 | The console implements Control Change, Program Change and Note On/Off on one user-set channel, plus MIDI Show Control (fw 3.0) | DOCUMENTED | [M15], [M30] |
| F21 | The MIDI spec v1.5 and the console manual v3.0 **disagree** on PC 124–127 and on the CC 124/125 names | DOCUMENTED contradiction | midi-spec-analysis.md |
| F22 | MIDI provides no acknowledgements and no state readback | DOCUMENTED | [M15] |

## Files

| # | Fact | Status | Source |
|---|---|---|---|
| F30 | Console and SmartSoft show files use "ASCII Text Representation for Lighting Console Data" (USITT) with `.asc` extension, 8.3 names on SD | DOCUMENTED | Console manual v3.0 p.6 |
| F31 | SFML = **SmartFade ML** personality library. PERSLIB.BIN is loaded into an ML console from SD | DOCUMENTED | SFML README.txt; FullReference.html title |
| F32 | The SmartFade 1248 has no fixture/personality feature | DOCUMENTED (absence) | Spec sheet, console manual 3.0 |

## Rejected / superseded

*(none yet)*
