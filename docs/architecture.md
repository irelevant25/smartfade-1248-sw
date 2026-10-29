# Architecture (proposal, not yet implemented)

Status: **proposal for review** (2026-09-17). Nothing below is built yet.

## Principles

1. The UI speaks domain concepts only. No bytes, no transport names.
2. Hardware knowledge flows one way: `docs/protocol` (evidence) → `SmartFade.Protocol`
   (only VERIFIED messages) → device implementation → application → UI.
3. The console is the source of truth. The backend keeps **desired** and **actual** state
   separately and never assumes a write succeeded.
4. One local .NET process, one Angular app. No brokers, no microservices.

## Layers and data flow

```
Angular UI (standalone components, signals/RxJS)
  services: ConnectionService, ChannelService, PlaybackService, StateService
        │  SignalR hub /hubs/console  (realtime: commands in, state deltas out)
        │  REST /api/...              (settings, show files, diagnostics)
        ▼
SmartFade.Api            ASP.NET Core host: hub, controllers, DI, logging, config
        ▼
SmartFade.Application    use cases: Connect/Disconnect, SetChannelLevel, Go, …
                         ConsoleStateStore (desired vs actual), change notifications
                         ports: ISmartFadeDevice, IShowRepository, IClock
        ▼
SmartFade.Domain         pure model: ChannelLevel, Memory, Step, Sequence, Stack, Patch,
                         Independent, Masters, ConsoleCapabilities, ConnectionState
        ▲
        │ implements ISmartFadeDevice
        ├── SmartFade.Devices.Simulated   in-process simulator (no bytes), default mode
        └── SmartFade.Devices.Usb         real console. Built only after E2/E3 (future)
               ├── SmartFade.Protocol       message encode/decode, framing, validation
               └── SmartFade.Communication  IDeviceTransport: UsbHspTransport (future),
                                            ReplayTransport (captures), LoopbackTransport (tests)
        (optional, separate path)
        └── SmartFade.Devices.Midi        MIDI subset (GO/GM/bumps…) after E4/E5 (future)

SmartFade.ShowFile.Asc   ASC parser library (no hardware deps) ← tools/asc-converter CLI
SmartFade.Infrastructure SQLite settings/show storage (later)
```

A fader move: UI (pointer event, rAF-coalesced) → hub `setChannelLevel` → application
updates **desired** level → device → (simulator applies immediately / real device sends
message, awaits object update) → **actual** level updated from the console's report → hub
pushes `channelStateChanged {desired, actual, pending}`. Throttling only if a verified
hardware limit requires it.

## Deviations from the brief (and why)

| Brief | Proposal | Reason |
|---|---|---|
| Names `SmartFace.*` | `SmartFade.*` | Official product name (all ETC documents). **Owner to confirm.** |
| `MockTransport` at the byte layer as the main mock | **Simulated device at the `ISmartFadeDevice` layer.** Byte-level transports only for replay/tests | A byte-level mock needs byte formats, which would mean inventing a protocol (forbidden). A domain-level simulator satisfies "Angular → .NET → Mock" without fake bytes. |
| API centred on commands (`setChannelLevel`, `recordCue`) | Commands **plus** an object/state-sync device contract | Static analysis suggests the USB protocol is poll + dirty-object + fetch, with no obvious "set level" command (H3, H6). The device interface must accommodate state reports independent of commands. |
| `SmartFace.Protocol` + `SmartFace.Communication` | Kept, but inside a device module, created only when the first message is verified | Avoid empty speculative projects |
| `docs/protocol/asc-format.md` | `docs/formats/asc-format.md` | Brief §88: separate file-format docs from protocol docs |
| ASC converter | Library `SmartFade.ShowFile.Asc` + thin CLI in `tools/asc-converter/` | Reusable by the main app (brief §92) |

## Open technology decisions

- **.NET version:** this machine has SDK 8.0.403. .NET 8 support ends **10 Nov 2026**
  (≈ 2 months). Recommend installing the .NET 10 SDK (current LTS) before scaffolding.
- **USB access (future):** reuse ETC's `etchsp.sys` through its device interface, *or* bind
  WinUSB and use a user-mode library. Decide after E1/E2 (H7). **E1 (2026-10-28):** `etchsp.sys`
  is refused by Windows 11 while Memory Integrity (HVCI) is on (known-facts F46), so reusing it
  is not viable on a stock Windows 11 PC. Plan for WinUSB on interface 1 (bulk 0x04 OUT /
  0x83 IN) plus the standard HID class driver on interface 0; final decision after E2. The
  owner switched Memory Integrity off only for the E2 capture and wants it back on afterwards
  (2026-10-28), which rules out the ETC driver for our backend: WinUSB (H7, still untested) is
  the plan. Binding WinUSB to interface 1 is a driver installation that displaces `etchsp.sys`
  and stops SmartSoft on that PC; it is a deliberate, reversible action taken by the owner,
  never performed by our code or installer. **2026-10-29, third-party precedent:** ConsoleLink
  (MIT, SmartFade **ML**, see [protocol/consolelink-analysis.md](protocol/consolelink-analysis.md))
  runs the same protocol family from user mode with libusb on Windows, macOS, Linux and
  Raspberry Pi; on Windows its README requires replacing the console's driver with WinUSB via
  Zadig, after which SmartSoft cannot see the console until the ETC driver is restored. So
  the user-mode path works on the sibling console, and its cost is the same for every
  backend language: SmartSoft and our software cannot share the console on one PC without
  switching drivers.
- **Backend stack (owner's question, 2026-10-29):** the brief prescribes Angular + .NET
  (ASP.NET Core, SignalR, "the .NET backend should own all hardware communication"). A
  Node.js backend (the `usb` npm package is a libusb binding, state pushed over WebSockets)
  would be the same single-process-plus-browser design that ConsoleLink implements in Python,
  and it would need the identical WinUSB driver switch, so it is not a capability gain. It
  would be a gain only in tooling uniformity (TypeScript end to end next to the Angular UI).
  Recommendation: keep the brief's .NET backend unless the owner changes the brief; in .NET
  the bulk endpoints are reachable through the WinUSB API directly (no libusb needed on
  Windows) or through a libusb binding if cross-platform matters later. The device layer is
  small either way, and nothing in it that transmits can be written before a message is cleared for sending (VERIFIED is necessary, not sufficient; none is cleared),
  so this decision is not on the critical path yet.
- **State of protocol knowledge (2026-10-28, after E2):** the real device layer now has
  **observed framing to build on** (poll / dirty-list / fetch over bulk 0x04 / 0x83,
  [packet-format.md](protocol/packet-format.md), [responses.md](protocol/responses.md),
  [state-model.md](protocol/state-model.md); known-facts F51–F69), which fits the
  object/state-sync device contract above (the console's state arrives as objects named by
  DirtyObjects lists, F57, F66). It is **OBSERVED once, no protocol element is VERIFIED**: a read-only
  decoder against `ReplayTransport` and captures is the first thing that can be built on it;
  anything that writes to a console (even SmartSoft's own two System messages, F58) is not
  allowed until promoted, understood and known to be harmless (safety rule 2), and reviewed. E2 also showed that no observed message sets a level,
  fires GO or edits show data (H6 open), so `SetChannelLevel`-style commands still have no
  protocol element behind them. Interface 0 (HID) was not used by SmartSoft (F63): the real
  device needs only the bulk interface, which suits a WinUSB binding of interface 1 (H7).
  **Update 2026-10-29, after E3 (a second session after a console power cycle):** the framing and the
  read-side facts F51–F55, F57–F65, F67, F68, F70–F77 are now VERIFIED as observed behaviour
  ("OBSERVED once, no protocol element is VERIFIED" above is the 2026-10-28 wording, kept). The
  decoder-first conclusion stands and **writing is still not allowed**: VERIFIED never clears a
  message for sending, and F58 is VERIFIED only as SmartSoft's behaviour. Three E3 findings matter
  for the desired-vs-actual store: the live objects FaderInfo, DisplayContent and PlaybackInfo are
  not in the 157-record list, so the actual state of faders, MASTER, BUMPS and LCD is known only
  after the console reports a change (F89, H58); the connect-time list differs between sessions (F80);
  and every console-side action (faders, MASTER, a mode switch, a bump) arrives as state objects only
  (F82–F88), so the "actual" side must be built from objects, not from events (H5).
- **Desktop shell:** WebView2 host vs Tauri. Deferred (brief §4).
- **Angular:** CLI 21.2 is installed. Standalone + signals + strict TS.
