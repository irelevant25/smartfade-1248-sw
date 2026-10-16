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
  WinUSB and use a user-mode library. Decide after E1/E2 (H7).
- **Desktop shell:** WebView2 host vs Tauri. Deferred (brief §4).
- **Angular:** CLI 21.2 is installed. Standalone + signals + strict TS.
