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
type DeviceMode = 'simulated' | 'replay' | 'hardware';
                                                    // 'hardware' = the real console over USB, read-only (safety check SC-1,
                                                    // docs/protocol/safety-checks.md): supportedCommands is empty;
                                                    // 'replay' is reserved (a capture-driven device) and not selectable yet

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
  reported: boolean;                                // false for a placeholder the console has not delivered yet (hardware mode:
                                                    // memories arrive one at a time, if at all); the simulator reports every memory
  label: string;                                    // the console's label (up to three words), '' when none
}

interface KeyLedDto { light: 'off' | 'dim' | 'full'; blinking: boolean; }   // one key LED: its first blink phase, and whether the phases differ

interface CrossfaderDto {                           // PlaybackInfo record 5 (F130, OBSERVED): the stack step (0-based; read as the Next step, H76),
  step: number; live: number; next: number;         // Live and Next 0..255 (A follows Live, B follows Next under the manual crossfaders, H80)
}

interface PanelKeysDto {                            // STACK and CLEAR (crossfader section), the record keys; SNAPSHOT and RATE have no identified LED
  stack: KeyLedDto; clear: KeyLedDto; copy: KeyLedDto; recSeq: KeyLedDto; magic: KeyLedDto; recMem: KeyLedDto; editMem: KeyLedDto;
  blackout: KeyLedDto; solo: KeyLedDto;             // the LED blocks 53 / 52, for the blink (block 53 blinks while the MASTER is below full, F85)
}

interface MemoryDto {                               // a memory's content, read on demand
  page: number; fader: number; number: number; label: string;
  levels: number[];                                 // exactly 48 entries, index 0 = channel 1, 0..255
}

interface ConsoleStateDto {
  connection: ConnectionDto;
  capabilities: CapabilitiesDto;
  supportedCommands: string[];                      // hub commands the connected device accepts; the simulator accepts
                                                    // the thirteen it models ("setChannelLevel", "setMaster", "setBlackout",
                                                    // "setBumps", "setSolo", "bumpChannel", "setMemoryLevel",
                                                    // "bumpMemory", "recordMemory", "storeMemory", "setIndependent",
                                                    // "clearChannels", "clearMemories"); the key taps "pressGo",
                                                    // "pressFadersMems", "pressFaders1To24" and "pressFaders25To48" come
                                                    // only from a device that models the stack and the fader mode (the
                                                    // real console with live control, SC-2 class B1, SC-5 class B4); a
                                                    // read-only real console reports none: the UI disables the rest
  writableControls: string[] | null;                // the individual controls the device accepts commands for, or null
                                                    // (a real console with live control, SC-2 / SC-5, names its whitelist here)
                                                    // when every control of a supported command's kind is accepted (the
                                                    // simulator). Names: "channelFader:N" / "channelBump:N" (N = 1..48),
                                                    // "memoryFader:N" / "memoryBump:N" (N = memory number 1..288),
                                                    // "master", "bumps", "solo", "blackout", "independent:1|2", "go",
                                                    // "fadersMems", "faders1To24", "faders25To48", "recMem", "editMem",
                                                    // "copy", "clear". A device cleared for some controls only (the real
                                                    // console under SC-2 / SC-5: every channel fader and bump 1..48, every
                                                    // memory fader and bump, MASTER, BUMPS, SOLO, BLACKOUT, IND 1 / 2, GO
                                                    // and the three FADERS keys; never CLEAR or a record key) names
                                                    // exactly those; the UI enables nothing else and the device still
                                                    // refuses, with its reason, what the console's reported state does
                                                    // not allow (another page, NEXT mode, a record function armed …)
  channels: ChannelStateDto[];                      // exactly 48 entries
  masters: MastersDto;
  outputs: number[];                                // 512 DMX output levels 0..255 (simulator model; real: OBSERVED object)
  memories: MemoryStateDto[];                       // exactly 288 entries, memory 1 (page 1, fader 1) first
  actualStateReported: boolean;                     // false until the device reported its state in this connection;
                                                    // until then actual levels / masters / outputs / memories are placeholders
  mastersReported: boolean;                         // false until the device reported the masters (hardware mode: until the
                                                    // console delivered FaderInfo); the UI dims them and says so until then
  channelsReported: boolean;                        // the same for the channel levels, the DMX outputs and the memories:
  outputsReported: boolean;                         // a read-only console reports them only when they change (a memory
  memoriesReported: boolean;                        // only when it is listed), so each panel says "not reported" until then
  independentsReported: boolean;                    // false until the device reported IND 1 / IND 2 (hardware mode: the console's
                                                    // Independents object, F115); the simulator reports them with its snapshot
  memoryPage: number | null;                        // the console's selected memory page, 1..12, when the device reports it
                                                    // (hardware mode: ConsoleStatus, F113); null for the simulator (the UI chooses)
  faderBank: string | null;                         // what the console's 24 physical faders control, when the device reports it
                                                    // (hardware mode: ConsoleStatus body[0] / body[4], F98): "channels1To24",
                                                    // "channels25To48" or "memories"; null for the simulator, which plays every
                                                    // bank at once. The UI lights the FADERS key of that bank (added 2026-11-02)
  keys: PanelKeysDto | null;                        // the key LEDs a real console reports (hardware mode, LedStatus; F116, F117,
                                                    // key identities INFERRED, H72); null until reported, always null for the simulator
  lcd: string[] | null;                             // the console's LCD, two lines of 20 characters (bar glyphs as block characters;
                                                    // F86); null until a real console reported it, always null for the simulator
  crossfader: CrossfaderDto | null;                 // the main playback as reported (F130): null until reported, always null for the simulator
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
| `pressGo()` | — | GO as a tap (press and release as one operation): the next step of the stack; `not_supported` on the simulator; the real console takes it only in channel mode, bank 1-24, with STACK dim and no record function armed (SC-2) |
| `pressFadersMems()` | — | FADERS MEMS as a tap: the physical faders to memory mode; `not_supported` on the simulator |
| `pressFaders1To24()` | — | FADERS 1-24 as a tap: the physical faders to channel mode, bank 1-24; `not_supported` on the simulator; the real console takes it only from memory mode or bank 25-48 |
| `pressFaders25To48()` | — | FADERS 25-48 as a tap: the physical faders to channel mode, bank 25-48; `not_supported` on the simulator; the real console takes it only from channel mode, bank 1-24 (SC-5 class B4) |
| `bumpChannel(channel, pressed)` | `number 1..48`, `boolean` | the bump button under a channel fader went down (`true`) or up (`false`); a client must send the release it owes. The real console with live control holds the bump until the release, at most 3 s (the device releases it then, and before a disconnect), one bump at a time (SC-5 class B3) |
| `setMemoryLevel(page, fader, level)` | `number 1..12`, `number 1..24`, `number 0..255` | moves a memory fader (`invalid_memory` when the address is out of range); the real console takes it only for the page it shows (no page change is sent) |
| `bumpMemory(page, fader, pressed)` | `number 1..12`, `number 1..24`, `boolean` | the bump button under a memory fader went down or up; held as `bumpChannel` on the real console |
| `recordMemory(page, fader)` | `number 1..12`, `number 1..24` | REC MEM: stores the current look into the memory (F107) |
| `storeMemory(page, fader, levels)` | `number 1..12`, `number 1..24`, `number[48]` of `0..255` | EDIT MEM: replaces the memory's channel levels; all zero deletes it (`invalid_argument` for a wrong count, `invalid_level` for a value out of range) |
| `getMemory(page, fader)` | `number 1..12`, `number 1..24` | returns `MemoryDto`, or `null` after an `errorOccurred` to the caller (`invalid_memory`, `not_connected`) |
| `setIndependent(number, on)` | `number 1..2`, `boolean` | IND 1 / IND 2 on or off (`invalid_argument` for another number); the real console toggles it with one tap from a steady LED, in channel mode, bank 1-24 (SC-5 class B5) |
| `clearChannels()` | — | the console's CLEAR pressed twice: every channel fader to zero (desired levels drop at once, the device confirms) |
| `clearMemories()` | — | CLEAR pressed three times: every memory fader to zero; nothing is erased (F109) |

On the real console with live control a fader or bump of another bank than the console shows (a
channel of 25-48 in bank 1-24, a memory fader in channel mode …) starts the device's automatic bank
switch (safety check SC-5 condition 1): the device presses the console's FADERS key for that bank,
sends nothing else until the console has reported the new bank (else, after 2 s, the connection ends
in `error`), then moves the fader first to the level the console reports for it and only then to the
level asked for. The console's faders then control the new bank, as after a press on the panel;
`faderBank` in the state says which. Nothing in the contract changes for this: the commands and their
arguments are the ones above.

Server → client:

| Event | Payload | When |
|---|---|---|
| `consoleStateChanged` | `ConsoleStateDto` | on hub connection, after every connection status change (connecting, connected, disconnected, error) and after a full snapshot from the device |
| `connectionStateChanged` | `ConnectionDto` | every connection status change |
| `channelStateChanged` | `ChannelStateDto[]` (one or more channels) | desired level, actual level or output of any channel changed |
| `mastersChanged` | `MastersDto` | master, bumps, blackout or solo changed |
| `memoriesChanged` | `MemoryStateDto[]` (one or more memories) | a memory fader moved, or a memory was recorded, stored or deleted |
| `outputsChanged` | `{ outputs: number[] }` | DMX output levels changed (coalesced, at most ~30 per second) |
| `lcdChanged` | `string[]` (two lines) | a real console's LCD text changed (hardware mode only) |
| `crossfaderChanged` | `CrossfaderDto` | a real console's main playback record changed: a GO fade, a manual crossfade, a stack step (hardware mode only) |
| `errorOccurred` | `ErrorDto` | to the caller when a command was rejected; to all clients when the device reported an error or a queued command failed |

Browser requests must come from one of the configured local origins (`Security:AllowedOrigins`,
default the Angular dev server and the backend itself); any other `Origin` is refused with 403
before CORS or the hub run. With the simulator this is harmless; it is a precondition for any
future hardware mode (no web page may ever drive a console without the owner's action).

## REST

| Route | Returns |
|---|---|
| `GET /api/status` | `{ mode, connection: ConnectionDto, backendVersion, protocolKnowledge: { messagesClearedForSending: 2 } }` (a rule, not a counter: SC-1 cleared the poll and the listed fetch for a read-only session; fact counts live in the knowledge base, not in the API) |
| `GET /api/console/state` | `ConsoleStateDto` |
| `POST /api/console/connect` | `ConnectionDto` |
| `POST /api/console/disconnect` | `ConnectionDto` |
| `POST /api/showfile/render` | the `.asc` bytes (`application/octet-stream`, `Content-Disposition` with `<name>-edited.asc`, never the original name) rendered from a `ShowFileDto` the UI edited: the DTO is mapped back to the model and written with the engine's layout (the committed sample survives parse, map and render byte for byte). `400` with `invalid_argument` when the patch is not 512 addresses in order with channels 0..96 and levels 0..100, when a step's time, delay or follow-on is not in a form the console's files use (seconds with at most one decimal, or m:ss; OBSERVED on 33 files, docs/formats/asc-format.md), when a step's memory is not page 0..11 with fader 0..23 (or only one of the two is given) or its CueParams is not 0..2, or when the file had unknown lines (they would be lost). Since step 4 the render also checks the shapes every console-written file has (docs/formats/asc-format.md "Step rules learned from the files"): stack cue numbers with one decimal, at most 199.9, rising through the stack; a memory step with `$$CUEPARAMS 1` and its memory's channel list repeated (none when the memory is not in the file); a sequence step numbered by its memory (`265.0`). Existing numbers are rendered as the DTO carries them; the UI numbers only the steps it inserts. Since step 5 it also checks every memory (channels in rising order, each once, 1..48, at 1..100 %) and every label block (three lines of at most 6 characters, an HTML numeric entity counting as one; printable ASCII only, '&' only in such an entity; the `Text` line derived from the lines as the console derives it: line 1, then line 2 only when line 1 is full, then line 3 only when line 2 is full too; "Label and memory rules learned from the files" in the format document). The backend stores nothing and overwrites nothing; saving is the browser's download |
| `POST /api/showfile/parse?name=<file name>` | `ShowFileDto`: the `.asc` file sent as the request body, parsed by `SmartFade.ShowFile.Asc` (read-only; nothing is stored, nothing reaches a console). `400` with `invalid_argument` for an empty body or a body above 4 MB. The DTO carries the header fields, the main stack and the sequences with their steps (numbers and times as the file wrote them, the referenced memory as page / fader / number), the memories with page and fader derived from the number (24 per page), the 512 patch entries, the independents, the channel and page labels, the settings, the count of unknown lines kept verbatim and the reader's warnings; every field's meaning has the status docs/formats/asc-format.md gives it |

## Rules carried by the contract

- Every command above is what the **simulator** accepts. Whether a real 1248 accepts any PC-side
  level, master, bump or memory command is UNKNOWN (H6 open; H24 and the PC → console part of H5
  untested); MASTER, BUMPS, SOLO and Blackout are physical, show-critical controls on the console,
  and REC MEM / EDIT MEM change show data.
- The simulator's model behind these commands (HTP across channel and memory faders, bumps at the
  BUMPS level, SOLO replacing the output, REC MEM recording the look, all-zero = deleted, the
  independents on DMX 97 / 98 untouched by the masters, CLEAR never erasing) follows the console
  manual where it states the behaviour (F5, F6, F107, F108, F109) and is marked INFERRED where it
  does not (proportional memory playback, the independents' level and modes); REC MEM records the
  look after the MASTER (OBSERVED once, E9b F145: fader 0x7C at MASTER 0x86 stored 0x41; the earlier
  pre-MASTER reading was replaced on 2026-10-31); it is documented in `SmartFade.Devices.Simulated`. COPY is a UI workflow over
  `getMemory` + `storeMemory`; the CLEAR press count (one press aborts an armed function) lives
  in the UI too.
- Levels are the console's unit, 0..255; "0xFF = 100 %" is OBSERVED against one show file (F72),
  not a specification.
- The UI never sees bytes, object IDs, transports or endpoints.
- `mode` is always present so the UI can show a prominent badge (SIMULATED / REPLAY / HARDWARE).
  In `hardware` mode (read-only, SC-1) `supportedCommands` is empty: the UI shows every fader at
  the reported level, read-only, and the backend answers every command with `not_supported`.
  Channel `actual` and `output` are then the same number, the console's post-MASTER level
  (OBSERVED, F96). `blackout`, `independent1` and `independent2` are never reported by the
  console in this mode (no observed object carries them) and the UI marks their keys as unknown;
  `solo` comes from an LED reading (INFERRED, F100). Inbound decoding for display may rest on
  OBSERVED or INFERRED readings, marked as such in the UI; no transmitted byte depends on them
  (owner decision recorded in docs/protocol/safety-checks.md, SC-1).
- Commands are expressed as desired state; the backend keeps desired and actual apart and never
  assumes a write succeeded (architecture principle 3). Memory fader levels, masters and bumps
  are kept as actual values only; the UI holds its own request until the backend confirms it.
