# Reverse-engineering plan

## Safety rules (non-negotiable)

1. **Passive before active.** Observe SmartSoft ↔ console traffic before our software sends
   anything to the console.
2. **Never send captured or hand-built bytes to the console** until the message is understood
   and its effect is known to be harmless (e.g. a read/poll). Replaying edit messages can
   overwrite show data.
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
| **E1** | **USB identification.** Power the console in Normal mode, connect USB to a Windows PC *without* SmartSoft installed. Record Device Manager entry, `pnputil /enum-devices /connected /ids` output, and a descriptor dump with USBTreeView (Uwe Sieber). Repeat on the PC where SmartSoft is installed. | None (read-only) | USB A-B cable, PC | H1, H2. VID/PID, number of interfaces, endpoint types and sizes, HID report descriptor, serial string, bcdDevice (firmware?) |
| **E2** | **SmartSoft session capture.** Install Wireshark + USBPcap. Start capture, launch SmartSoft with the console connected, wait until "Online", idle 30 s, close SmartSoft. Save as pcapng. | None to the rig. SmartSoft may sync the show, so back up first | E1 done, SmartSoft installed, matching firmware | H3, H4. Framing, poll cadence, version exchange, bulk vs HID usage |
| **E3** | **Single-action captures.** One capture per action, each with a spoken/typed timestamp log: (a) move channel fader 1 from 0 → full → 0 slowly on the console; (b) press one memory bump; (c) move GM; (d) press GO on an empty/safe stack; (e) in SmartSoft rename one channel label. | Low (backup first) | E2 | H5, H6. Which objects change for which action, level encoding |
| **E4** | **MIDI observation.** Console MIDI OUT → USB-MIDI interface → PC. Record with a MIDI monitor (e.g. MIDI-OX or our tool). Move GM, bumps master, crossfaders, a memory fader, press a bump, GO, PAUSE, blackout, change page. | None | USB-MIDI interface, MIDI cable | H9. Resolves MIDI doc contradictions for TX |
| E5 | MIDI control test. PC → console MIDI IN, one message at a time: GM CC127 0 → 127, then PC 125/124, observe console + DMX out | Low-medium (blackout/GO) | E4 done | Verified MIDI write path |
| E6 | ASC round-trip probes. On console: record a memory on page 2 fader 5, a channel step, a stack state step, set MIDI channel 3, patch IND1. Save to SD. Diff with previous file | None | SD card | H10–H14 |
| E7 | DMX output observation (optional). USB-DMX input or DMX analyser on console DMX OUT | None | DMX interface | Ground truth for "actual state" of outputs |

**Recommended first: E1 then E2.** E1 costs ten minutes and settles whether our unit uses the
documented USB device. E2 is the single capture that turns the static hypotheses
(poll / dirty / fetch objects) into observed facts.

## Hardware evidence requested

Things that cannot be determined from the supplied materials:

1. Console firmware version (*Menu > Diagnostics > Software version*) and serial number / rear-panel photo.
2. Whether SmartSoft has ever connected to this console "Online", on which PC/OS, with which SmartSoft version.
3. E1 output: Device Manager name + hardware IDs, `pnputil /enum-devices /connected /ids` (or USBTreeView report) with the console plugged in.
4. Whether a USB-MIDI interface and MIDI cables are available (for E4).
5. Whether a spare PC or VM is available for SmartSoft + USBPcap.
6. Whether a USB-DMX interface is available (optional, E7).
7. More `.asc` files, ideally made deliberately for E6.

## Tooling plan

| Tool | Location | Status |
|---|---|---|
| Source audit scripts (installer extraction, PE report, strings) | `tools/source-audit/` | Done |
| Capture importer: pcapng (USBPcap) → normalized capture JSONL | `tools/protocol-analyzer/` | Planned after E2 |
| Hex/record viewer, diff of two captures, object-name annotator | `tools/protocol-analyzer/` | Planned |
| MIDI recorder (Windows MIDI → JSONL) | `tools/protocol-analyzer/` | Planned for E4 |
| Replay (to mock transport only) | backend `ReplayTransport` | Planned |

## Capture format (proposal)

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
{"type":"frame","seq":17,"t":1.234567,"ts":"2026-09-20T10:30:00.123456Z","dir":"out","iface":1,"ep":"0x02","xfer":"bulk","len":8,"data":"01020A7F00000000"}
{"type":"note","t":5.000000,"text":"moved channel fader 1 to full"}
```

- `t` = seconds since capture start (µs precision), `dir` = `out` (host→console) / `in`.
- MIDI captures use the same format with `"transport":"midi"` and no `iface/ep/xfer`.
- Notes are inserted from the operator's action log so the decoder can correlate.
