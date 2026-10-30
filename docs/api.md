# API contract: Angular UI ↔ .NET backend (Phase 1)

Status: **Phase 1 contract (2026-10-30, extended the same day with memories, bumps and SOLO at
the owner's request)**, owner-approved scope "Angular → SignalR → .NET → simulated console". It
carries domain concepts only (channels, levels, masters, memories, connection state); no bytes,
no transports, no object IDs. Levels travel as the console's own unit, **0–255** (the bodies
that carry them are VERIFIED; the "0xFF = 100 %" reading is OBSERVED against one show file, F72;
live levels F83); the UI renders percent. Memories are addressed as on the console: **page 1–12,
fader 1–24** (memory number = 24 × (page − 1) + fader).
The contract will be refined when real-console capabilities are verified (brief §21: "do not
finalize the API until the underlying hardware capabilities are known").

## Transport

- SignalR hub: `/hubs/console` (JSON protocol, camelCase).
- REST: `/api/...` for non-realtime operations.
- Development: backend on `http://localhost:5160` (port 5000 is taken by another application on
  the owner's PC), Angular dev server on `http://localhost:4200` with a proxy for `/api` and
  `/hubs` (CORS is also allowed for the dev origin).

## DTOs

```ts
type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';
type DeviceMode = 'simulated' | 'replay';          // 'hardware' does not exist in Phase 1; 'replay' is reserved
                                                    // (a capture-driven device) and not selectable yet

interface ConnectionDto {
  status: ConnectionStatus;
  mode: DeviceMode;                                 // always shown in the UI (brief: mode must be visible)
  deviceDescription: string;                        // e.g. "Simulated SmartFade 1248"
  error?: string;
}

interface CapabilitiesDto {                         // DOCUMENTED console limits (known-facts F3, F7)
  channels: 48; physicalFaders: 24; memoryPages: 12; memoriesPerPage: 24;
  sequences: 48; stackSteps: 199; dmxAddresses: 512; independents: 2;
}

interface ChannelStateDto {
  channel: number;                                  // 1..48
  desired: number;                                  // 0..255, what the user asked for
  actual: number;                                   // 0..255, the fader level the console (or simulator) reports
  pending: boolean;                                 // desired != actual and a command is in flight
  output: number;                                   // 0..255, what the console says the channel is doing after HTP with
                                                    // memories, bumps and the MASTER (the console's ChannelLevels object
                                                    // carries post-MASTER values, F96, F97); lights the bump button,
                                                    // never compared with desired
}

interface MastersDto {
  master: number;                                   // 0..255 (the MASTER / Grand Master fader)
  bumps: number;                                    // 0..255 (the BUMPS fader: the level bumps go to, F6)
  blackout: boolean;
  solo: boolean;                                    // bump mode: false = pile-on, true = solo (F6)
  independent1: boolean;                            // IND 1 / IND 2 toggles: outputs separated from all other
  independent2: boolean;                            // controls, DMX 97 / 98 by default (F108)
}

interface MemoryStateDto {                          // one memory as the console addresses it
  page: number;                                     // 1..12
  fader: number;                                    // 1..24 (the memory fader and the bump button under it)
  number: number;                                   // 24 × (page − 1) + fader, 1..288
  level: number;                                    // 0..255, the memory fader (playback level)
  recorded: boolean;                                // has content; all-zero content counts as deleted (F107)
  label: string;                                    // the console's label (up to three words), '' when none
}

interface MemoryDto {                               // a memory's content, read on demand
  page: number; fader: number; number: number; label: string;
  levels: number[];                                 // exactly 48 entries, index 0 = channel 1, 0..255
}

interface ConsoleStateDto {
  connection: ConnectionDto;
  capabilities: CapabilitiesDto;
  supportedCommands: string[];                      // hub commands the connected device accepts; the simulator accepts
                                                    // all thirteen ("setChannelLevel", "setMaster", "setBlackout",
                                                    // "setBumps", "setSolo", "bumpChannel", "setMemoryLevel",
                                                    // "bumpMemory", "recordMemory", "storeMemory", "setIndependent",
                                                    // "clearChannels", "clearMemories"); a real console may report
                                                    // none (H6 open): the UI disables the rest
  channels: ChannelStateDto[];                      // exactly 48 entries
  masters: MastersDto;
  outputs: number[];                                // 512 DMX output levels 0..255 (simulator model; real: OBSERVED object)
  memories: MemoryStateDto[];                       // exactly 288 entries, memory 1 (page 1, fader 1) first
  actualStateReported: boolean;                     // false until the device reported its state in this connection;
                                                    // until then actual levels / masters / outputs / memories are placeholders
  showFileName?: string;                            // e.g. "SHOW04.ASC" when known
  firmwareVersion?: string;                         // e.g. "3.0.2.9.0.43" when known
}

interface ErrorDto {
  message: string;
  code?: string;      // stable: not_connected | invalid_channel | invalid_level | invalid_memory | invalid_argument |
                      //         not_supported | device_error
}
```

Optional members (`error`, `showFileName`, `firmwareVersion`, `code`) are sent by the backend as
JSON `null` when absent; clients treat `null` and a missing member alike. After a reconnect and
after every `errorOccurred` the UI calls `getState()` again, because its optimistic values may be
stale; the backend must expect those extra reads.

## SignalR hub `/hubs/console`

Client → server. The `set…`, `bump…`, `recordMemory` and `storeMemory` commands return as soon as
the application layer accepted them (validated and queued for the device, in order); the device's
confirmation arrives as `channelStateChanged` / `mastersChanged` / `memoriesChanged` /
`outputsChanged`, and a device execution failure as a broadcast `errorOccurred` with code
`device_error`. `connect()` and `disconnect()` return when the attempt has run; `getMemory()`
returns the content.

| Method | Arguments | Effect |
|---|---|---|
| `getState()` | — | returns `ConsoleStateDto` |
| `connect()` | — | connects the configured device (simulated in Phase 1); the connect is owned by the server, a client that disconnects mid-way does not abort it |
| `disconnect()` | — | disconnects |
| `setChannelLevel(channel, level)` | `number 1..48`, `number 0..255` | sets the desired level; rejected to the caller with `errorOccurred` (`invalid_channel`, `invalid_level`, `not_connected`, `not_supported`) |
| `setMaster(level)` | `number 0..255` | MASTER level command (simulator; the application keeps no separate desired value for masters yet) |
| `setBlackout(on)` | `boolean` | blackout on/off (simulator) |
| `setBumps(level)` | `number 0..255` | BUMPS level command: the level bumps go to (simulator) |
| `setSolo(on)` | `boolean` | bump mode: off = pile-on, on = solo (simulator) |
| `bumpChannel(channel, pressed)` | `number 1..48`, `boolean` | the bump button under a channel fader went down (`true`) or up (`false`); a client must send the release it owes |
| `setMemoryLevel(page, fader, level)` | `number 1..12`, `number 1..24`, `number 0..255` | moves a memory fader (`invalid_memory` when the address is out of range) |
| `bumpMemory(page, fader, pressed)` | `number 1..12`, `number 1..24`, `boolean` | the bump button under a memory fader went down or up |
| `recordMemory(page, fader)` | `number 1..12`, `number 1..24` | REC MEM: stores the current look into the memory (F107) |
| `storeMemory(page, fader, levels)` | `number 1..12`, `number 1..24`, `number[48]` of `0..255` | EDIT MEM: replaces the memory's channel levels; all zero deletes it (`invalid_argument` for a wrong count, `invalid_level` for a value out of range) |
| `getMemory(page, fader)` | `number 1..12`, `number 1..24` | returns `MemoryDto`, or `null` after an `errorOccurred` to the caller (`invalid_memory`, `not_connected`) |
| `setIndependent(number, on)` | `number 1..2`, `boolean` | IND 1 / IND 2 on or off (`invalid_argument` for another number) |
| `clearChannels()` | — | the console's CLEAR pressed twice: every channel fader to zero (desired levels drop at once, the device confirms) |
| `clearMemories()` | — | CLEAR pressed three times: every memory fader to zero; nothing is erased (F109) |

Server → client:

| Event | Payload | When |
|---|---|---|
| `consoleStateChanged` | `ConsoleStateDto` | on hub connection, after every connection status change (connecting, connected, disconnected, error) and after a full snapshot from the device |
| `connectionStateChanged` | `ConnectionDto` | every connection status change |
| `channelStateChanged` | `ChannelStateDto[]` (one or more channels) | desired level, actual level or output of any channel changed |
| `mastersChanged` | `MastersDto` | master, bumps, blackout or solo changed |
| `memoriesChanged` | `MemoryStateDto[]` (one or more memories) | a memory fader moved, or a memory was recorded, stored or deleted |
| `outputsChanged` | `{ outputs: number[] }` | DMX output levels changed (coalesced, at most ~30 per second) |
| `errorOccurred` | `ErrorDto` | to the caller when a command was rejected; to all clients when the device reported an error or a queued command failed |

Browser requests must come from one of the configured local origins (`Security:AllowedOrigins`,
default the Angular dev server and the backend itself); any other `Origin` is refused with 403
before CORS or the hub run. With the simulator this is harmless; it is a precondition for any
future hardware mode (no web page may ever drive a console without the owner's action).

## REST

| Route | Returns |
|---|---|
| `GET /api/status` | `{ mode, connection: ConnectionDto, backendVersion, protocolKnowledge: { messagesClearedForSending: 0 } }` (the zero is a rule, not a counter; fact counts live in the knowledge base, not in the API) |
| `GET /api/console/state` | `ConsoleStateDto` |
| `POST /api/console/connect` | `ConnectionDto` |
| `POST /api/console/disconnect` | `ConnectionDto` |

## Rules carried by the contract

- Every command above is what the **simulator** accepts. Whether a real 1248 accepts any PC-side
  level, master, bump or memory command is UNKNOWN (H6 open; H24 and the PC → console part of H5
  untested); MASTER, BUMPS, SOLO and Blackout are physical, show-critical controls on the console,
  and REC MEM / EDIT MEM change show data.
- The simulator's model behind these commands (HTP across channel and memory faders, bumps at the
  BUMPS level, SOLO replacing the output, REC MEM recording the look, all-zero = deleted, the
  independents on DMX 97 / 98 untouched by the masters, CLEAR never erasing) follows the console
  manual where it states the behaviour (F5, F6, F107, F108, F109) and is marked INFERRED where it
  does not (proportional memory playback, recording before the MASTER, the independents' level
  and modes); it is documented in `SmartFade.Devices.Simulated`. COPY is a UI workflow over
  `getMemory` + `storeMemory`; the CLEAR press count (one press aborts an armed function) lives
  in the UI too.
- Levels are the console's unit, 0..255; "0xFF = 100 %" is OBSERVED against one show file (F72),
  not a specification.
- The UI never sees bytes, object IDs, transports or endpoints.
- `mode` is always present so the UI can show a prominent badge (SIMULATED / REPLAY); a
  hardware mode will be added only when a message is cleared for sending.
- Commands are expressed as desired state; the backend keeps desired and actual apart and never
  assumes a write succeeded (architecture principle 3). Memory fader levels, masters and bumps
  are kept as actual values only; the UI holds its own request until the backend confirms it.
