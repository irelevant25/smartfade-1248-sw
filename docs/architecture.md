# Architecture

Status: proposal of 2026-09-17, **Phase 1 implemented on 2026-10-30** with the owner's approval
(see "Phase 1 as built" at the end). The layering below is unchanged.

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
| Names `SmartFace.*` | `SmartFade.*` | Official product name (all ETC documents). **Confirmed by the owner 2026-10-30 ("for now yes").** |
| `MockTransport` at the byte layer as the main mock | **Simulated device at the `ISmartFadeDevice` layer.** Byte-level transports only for replay/tests | A byte-level mock needs byte formats, which would mean inventing a protocol (forbidden). A domain-level simulator satisfies "Angular → .NET → Mock" without fake bytes. |
| API centred on commands (`setChannelLevel`, `recordCue`) | Commands **plus** an object/state-sync device contract | Static analysis suggests the USB protocol is poll + dirty-object + fetch, with no obvious "set level" command (H3, H6). The device interface must accommodate state reports independent of commands. |
| `SmartFace.Protocol` + `SmartFace.Communication` | Kept, but inside a device module, created only when the first message is verified | Avoid empty speculative projects |
| `docs/protocol/asc-format.md` | `docs/formats/asc-format.md` | Brief §88: separate file-format docs from protocol docs |
| ASC converter | Library `SmartFade.ShowFile.Asc` + thin CLI in `tools/asc-converter/` | Reusable by the main app (brief §92) |

## Open technology decisions

- **.NET version:** this machine has SDK 8.0.403. .NET 8 support ends **10 Nov 2026**
  (≈ 2 months). Recommend installing the .NET 10 SDK (current LTS) before scaffolding.
  *Done 2026-10-30: SDK 10.0.403 installed, pinned in `global.json`, every project on net10.0.*
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
  small either way, and nothing in it that transmits can be written before a message is cleared for sending (VERIFIED is necessary, not sufficient; none is cleared *(2026-10-28 wording; SC-1 cleared the poll and the listed fetch on 2026-10-30, and the WinUSB pipe was written then)*),
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
- **Angular:** CLI 21.2 was installed when this was written; the app was generated with CLI
  22.2 on 2026-10-30 (standalone, signals, zoneless, strict TS, Vitest).

## Phase 1 as built (2026-10-30)

Owner approval: 2026-10-30 ("you have my approval"). The first vertical slice of the brief,
Angular → SignalR → .NET → simulated console, with the protocol decoders tested against the
committed captures. **No code in the solution can transmit to a console** *(2026-10-30 morning
wording, kept; superseded the same afternoon by safety check SC-1, see the "Hardware path"
bullet below: the poll and the listed fetch may now be sent by the read-only USB device)*: the only
transports are a loopback and a capture replay, the device registered by default is the
simulator, and `Device:Mode` accepts nothing else (CLAUDE.md: VERIFIED is necessary, not
sufficient; no message is cleared for sending *(until SC-1, the same day)*).

```
SmartFade.sln
Directory.Build.props                nullable, implicit usings, warnings as errors
src/backend/
  SmartFade.Domain/                  capabilities, levels (0..255, F72/F83), masters, snapshot,
                                     memory / patch / step / sequence / independent / label
  SmartFade.Application/             ISmartFadeDevice port, DeviceStateChange, ConsoleStateStore
                                     (desired vs actual, never merged), ConsoleService (use cases, events)
  SmartFade.Devices.Simulated/       SimulatedSmartFadeDevice: domain-level model, no bytes
                                     (1:1 patch DOCUMENTED default; output = level × MASTER / 255
                                     rounded, a simulation assumption; Blackout → 0, DOCUMENTED F5)
  SmartFade.Protocol/                decoders (plus, since SC-1, HspOutbound and HspReadSession for the
                                     cleared poll and listed fetch): HspHeader (F52/F53), inbound/outbound payloads
                                     (F54), DirtyObjectsList (F57), object decoders annotated with
                                     [Evidence(status, facts)], HspSessionDecoder (transfer stream → events)
  SmartFade.Communication/           IDeviceTransport (brief §10), LoopbackTransport, CaptureFile
                                     (normalized JSONL reader), ReplayTransport (plays the console side
                                     of a capture; Send only records)
  SmartFade.Api/                     ASP.NET Core host: /hubs/console (SignalR), /api/status,
                                     /api/console/state|connect|disconnect, ConsoleEventsBroadcaster
                                     (outputs coalesced to ~30/s), CORS for the Angular dev server
src/frontend/                        Angular (standalone, signals), contract in docs/api.md
tests/                               xUnit: Domain, Application (service + simulator),
                                     Protocol (framing bytes from E2; golden decodes of the E2, E3,
                                     E3b captures), Communication (capture reader, transports)
```

Decisions taken while building:

- **Target framework net10.0** (current LTS). The scaffold was built on net8.0 first because
  winget could not install the .NET 10 SDK from this session (error 0x8A15005E twice); the
  owner installed SDK 10.0.403 manually the same morning and every project was retargeted
  with one `TargetFramework` line, build and tests unchanged.
- **Levels are bytes end to end** (0..255, the console's unit); the UI converts to percent.
- **Golden tests read the committed JSONL captures directly** from `docs/protocol/captures/`
  (located through `SmartFade.sln`), so every expected number traces to an experiment record.
- **`SmartFade.Infrastructure` is not created** until something needs persistence.
- **Replay as a device** (a `ReplayDevice` that drives the application from a capture) is the
  next step after the slice; the transport and the decoders for it exist.
- **Protocol and Communication are not part of the running process.** They are standalone
  projects (no dependency on Domain or Application) referenced only by their tests; the
  earlier text above, which places them inside `SmartFade.Devices.Usb`, describes where they
  will be consumed once a device uses them. A test pins that `SmartFade.Api` has no reference
  path to either.
- **Naming:** `SmartFade.*` and `ISmartFadeDevice` (the brief's `SmartFace.*` /
  `ISmartFaceClient`) are now built in; the owner approved Phase 1 with these names on
  2026-10-30 and confirmed them explicitly the same day ("for now yes"; deviations table above).
- **Command pump (review item, 2026-10-30):** every device call goes through one
  single-consumer queue in `ConsoleService`, so commands and the device's reports stay in
  order and the device sees a token owned by the service, never a UI client's; `set…`
  commands return on acceptance, `connect`/`disconnect` when the attempt has run. The
  simulator's connection state machine uses a generation counter (a disconnect or link loss
  during a connect wins; a second connect is ignored) and has test hooks for console-side
  changes, link loss and failing commands, so the failure half of desired-versus-actual is
  tested end to end.
- **Local web exposure:** an Origin guard refuses browser requests from non-local origins and
  `AllowedHosts` is restricted to localhost. Harmless with the simulator; a precondition for
  any hardware mode, together with command capability flags (`supportedCommands` in the
  contract) so that an observation-only real device can refuse control commands.
- **Tests:** Domain, Application (service, simulator, silent and failing devices, ordering,
  connection races), Protocol (framing bytes and golden decodes of E2, E3 and E3b),
  Communication (capture reader, transports) and Api (SignalR round trip, mode refusal,
  origin guard, pinned wire names).
- **Memories, bumps and SOLO (2026-10-30, owner request, same day):** the simulator models the
  console's memory faders (12 pages × 24, REC MEM, EDIT MEM, all-zero = deleted), the bump
  buttons under every channel and memory fader (pile-on at the BUMPS level, or solo with SOLO on)
  and reports a per-channel **output** next to the fader level, as the console's ChannelLevels
  object carries post-MASTER values (F96, F97). The model follows the manual where it states the
  behaviour (HTP F107, bump modes F6, Master and Blackout F5) and marks its own readings INFERRED
  (proportional memory playback, recording before the MASTER). Ten hub commands, a
  `memoriesChanged` event and `getMemory` were added to the contract (docs/api.md); memory
  content is fetched on demand, the state carries only each memory's fader level, recorded flag
  and label. The UI lays the panel out like the console: readout above each fader, the numbered
  bump button below it, memories as a third row with page keys and the REC MEM / EDIT MEM / CLEAR
  keys, BUMPS under SOLO and MASTER under BLACKOUT on the right. EDIT MEM is a blind edit held in
  the UI (`MemoryEditService`) and stored with one command, so desired and actual stay apart.
  **Second pass the same day (owner feedback):** BUMPS starts at zero like the owner's console
  (F82, F95); the BLACKOUT key is blue, blinks while the MASTER is below full (F5) and lights
  while on; memory bump buttons glow red at three levels (empty, recorded, contributing; F8) and
  the key text contrasts with the glow; the console's record column (COPY, REC SEQ, MAGIC, REC
  MEM, EDIT MEM, SNAPSHOT, in the owner's key colours) and crossfader column (A / B faders, GO,
  STACK, PAUSE, CLEAR) sit between the faders and the masters, with the keys the simulator does
  not model yet shown inactive; the INDEPENDENTS IND 1 / IND 2 toggle DMX 97 / 98 by default
  (F108) under the masters; COPY copies a memory through `getMemory` + `storeMemory`; CLEAR aborts
  an armed function, then clears the channel faders, then the memory faders (F109, press count in
  the UI, `clearChannels` / `clearMemories` on the device). A patch editor (DMX 1–512 → channels
  and independents, the console's Patch, F70) and the stack / sequence pages are the owner's
  next wishes; both are UI over data the console already reports, but writing them to a real
  console is a protocol question that stays closed until a message is cleared.
- **Hardware path (2026-10-30, safety check SC-1, `docs/protocol/safety-checks.md`):** the
  first clearance, for a read-only session: the poll and the fetch of a record the console
  listed. `SmartFade.Protocol` gained its only encoder (`HspOutbound`) and the session state
  machine `HspReadSession`, which decides what to send next and faults on any reply no capture
  has shown; `SmartFade.Communication` gained `IHspPipe` with `WinUsbHspPipe` (WinUSB on
  interface 1, P/Invoke, no third-party library, Memory Integrity can stay on) and
  `ReplayHspPipe` (a committed capture that checks every write against SmartSoft's);
  `SmartFade.Devices.Usb` is the read-only `ISmartFadeDevice` (`SupportedCommands = None`,
  cadence from F62 with floors, every transfer logged in the capture format, no retry). The
  golden proof is `HspReadSessionTests`: replayed over E2, E3 and E3b, the engine's 38 517
  polls and 1 651 fetches are byte-identical to SmartSoft's. `Device:Mode=Usb` selects it for
  one run, the API now references the device project (the earlier "no reference path" test
  became "simulated mode registers no console pipe"), and the UI shows HARDWARE in the alert
  colour with every control read-only. The first live session is experiment E8 (runbook in
  `docs/protocol/reverse-engineering.md`), still waiting for the owner's go *(E8 ran on 2026-10-30:
  861 s, no fault; record in `docs/protocol/captures/`; the readings it gave are shown by the UI)*.
