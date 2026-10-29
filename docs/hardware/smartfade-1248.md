# ETC SmartFade 1248: hardware

Status vocabulary: see [../protocol/README.md](../protocol/README.md).
"DOCUMENTED" means stated in official ETC material. It does **not** mean observed on the
user's unit.

## Identity

| Item | Value | Status | Source |
|---|---|---|---|
| Manufacturer | Electronic Theatre Controls (ETC). Show files carry `MANUFACTURER AVAB` | DOCUMENTED | Spec sheet; ASC header |
| Model | SmartFade 1248, order code `SF1248` | DOCUMENTED | Spec sheet |
| Family | SF1248, SF1296 (96 ch), SF2496 (96 ch), SFML (moving light). SmartSoft groups 1248 with 1296 ("SmartFade 1248/1296") | DOCUMENTED | Spec sheet; SmartSoft manual p.10 |
| Firmware on the user's unit | **3.0.2.9.0.43**, read by the owner from *Menu > Diagnostics > Software version* on 2026-10-28. Same version as the SmartSoft 3.0.2b package, so SmartSoft's version check should pass | **VERIFIED 2026-10-28** (owner's reading, reproduced by the ConsoleVersion string on USB in E2, f226; F48, F59) | Owner, 2026-10-28; console manual v3.0 p.25 |
| USB identity of the user's unit | `VID_14D5` / `PID_0200` / bcdDevice `0x0103`; strings "ETC" / "ETC SmartFade"; **no USB serial number** | OBSERVED (E1, 2026-10-28) | [../protocol/captures/2026-10-28-e1-usb-identification.md](../protocol/captures/2026-10-28-e1-usb-identification.md) |
| Meaning of bcdDevice 0x0103 | UNKNOWN: USB-stack revision or a firmware number. Does not match "3.0.2" | UNKNOWN | E1 |
| Firmware shipped with SmartSoft 3.0.2b | SmartFade 3.0.2.9.0.43 (`SMRTFADE.SRC`) | DOCUMENTED | `Setup Information.rtf` |
| Serial number / hardware revision | UNKNOWN (back panel) | UNKNOWN | |

## Physical interfaces

| Interface | Detail | Status | Source |
|---|---|---|---|
| USB | "USB input (Series B device connector)"; spec sheet: "USB interface to PC for software upgrades"; SmartSoft manual: SmartSoft connects over USB. **E1:** full-speed USB 1.1 composite device: interface 0 HID (interrupt IN 0x81 / OUT 0x02, 64 B, 10 ms), interface 1 vendor-specific (bulk IN 0x83 / OUT 0x04, 64 B); self-powered flag set, 0 mA declared | DOCUMENTED + OBSERVED (E1, 2026-10-28) | RTF spec D.1.f, E.1; spec sheet; SmartSoft manual p.4; E1 record |
| MIDI In | 5-pin DIN presumed (connector type not stated) | DOCUMENTED (connector type UNKNOWN) | Spec sheet |
| MIDI Out | as above | DOCUMENTED | Spec sheet |
| DMX512 Out | 1 universe, 512 outputs. Output speed adjustable (*DMX Out Speed*) | DOCUMENTED | Spec sheet; manual p.18 |
| DMX512 In | 1 universe. Merge, map to fader, or DMX backup | DOCUMENTED | Spec sheet; RTF C.2.o |
| SD card | Standard SD only (no SDHC), FAT16, 8.3 file names, show files in USITT ASCII | DOCUMENTED | Manual p.6–7 |
| Power | 12 V AC/DC from external PSU (90–265 VAC) | DOCUMENTED | RTF F.3 |
| RS-232 / Ethernet | **Not listed** in any interface list | DOCUMENTED absence (spec sheet, RTF) | |

## Capabilities (DOCUMENTED unless a note says otherwise; not yet verified on the unit)

**Operating modes** (selected at power-up by holding the power button): *Full Control*
(Normal), *TwoScene*, *DMXBackup*. SmartSoft requires Normal mode: its binary contains
"The SmartFade console must be running in Normal mode in order to connect to SmartSoft."

| Capability | Normal mode | Notes |
|---|---|---|
| Intensity channels | 48 (2 × 24 channel faders, Live/Next) | HTP. 1248 manual text says "48 (96)" to cover 1296/2496. *Note 2026-10-29:* the console manual says the 1248 has two rows of 12 faders, i.e. **24 physical faders**, which the FADERS buttons switch between the channel banks 1-24 and 25-48 (known-facts F7; printed pp.10-12; the bank reading is INFERRED, F7); "2 × 24" above means the two banks |
| Channel levels resolution | UI levels 0–100 % (ASC stores integer percent). DMX output 8-bit | ASC: `CHAN n/level` |
| Patch | 512 DMX addresses → channels, 1:1 default, per-dimmer proportional level (`PATCH 1 dmx/ch/level`) | |
| Independents | 2 (IND1, IND2), toggle/bump modes, patched like channels | ASC `$INDEPENDENT` |
| Memories | 288 = 12 pages × 24 memory faders, each with bump | |
| Sequences | 48 = 12 pages × 4 sequence faders, max 24 steps each; chase / tap BPM / Magic | |
| Cue stack | 1 stack, max 199 steps, point cues (e.g. 10.1–10.6), up/down/wait/follow-on times, rate override | |
| Snapshots | 10 temporary looks | |
| Masters | Grand Master fader, Blackout button, Bumps master fader, Solo/Pile-on | Panel labels on the owner's unit: **MASTER** (Grand Master, rightmost) and **BUMPS** (bump master), INDEPENDENTS below them (owner's statement 2026-10-29, OBSERVED, not covered by this section's DOCUMENTED heading; the manual's names are in known-facts F5); the Blackout button blinks while the Master is below 100 % (manual p.56) |
| Crossfader pair | Manual, timed, rate override; GO, PAUSE, RATE, CLEAR | |
| DMX backup | 24 backup looks × 512 channels | ASC `$BACKUPLOOK` (keyword seen in engine, not in sample) |
| Undo | last record command | |
| Magic | random levels / random chases | |
| Languages | EN, FR, DE, ES (+ IT in engine translations) | |
| MIDI | Basic MIDI (CC/PC/Note), MSC (Go/Stop/Resume…), master/slave sync | See [midi-spec-analysis.md](../protocol/midi-spec-analysis.md) |
| **Not on 1248** | Moving lights / fixtures / palettes / effects (SmartFade ML only) | ASC 1248 dialect has none of these |

## Limitations relevant to this project

- USB is a **device-side** port. The PC is the host, and all PC↔console traffic goes through
  one USB cable (see [smartsoft-analysis.md](../protocol/smartsoft-analysis.md)).
- SmartSoft refuses to connect when console and SmartSoft versions differ (binary string:
  "The console software version does not match the SmartSoft software version.").
- MIDI is 31 250 baud with 7-bit data values, so levels over MIDI are 0–127 (≈0.8 % steps).
- No documented way to read the full console state over MIDI. A MIDI *master* transmits
  fader and button actions, which gives an event stream, not a state snapshot.

## Owner's test rig (stated 2026-10-28)

Four RGB fixtures, 3 DMX channels each, DMX start addresses **5, 8, 11 and 14** (DMX 5–16
in total). With the documented 1:1 default patch these are console channels 5–16; the actual
patch on the unit and the fixtures' channel order (R, G, B assumed) are UNKNOWN. Any active experiment (E3, E5, E7) should
target these channels and expect visible colour changes.

## Information needed from the owner

See the "What I need from you" list in the audit report and
[../protocol/reverse-engineering.md](../protocol/reverse-engineering.md#hardware-evidence-requested).
