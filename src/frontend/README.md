# SmartFade 1248 control UI

Angular front end of the SmartFade 1248 control software: one page, a real-time control surface
for the 48 channels, the masters and the 512 outputs. It talks to the backend only through the
domain contract in [docs/api.md](../../../docs/api.md) (SignalR hub `/hubs/console`): channels,
levels, masters, connection state, and nothing about how the console is reached. Levels travel
as 0-255 and are shown as percent.

Phase 1 scope: the backend runs a **simulated** console. The mode is always shown in the top bar
(SIMULATED in amber); a mode this page does not know is shown as a red alert instead.

## Requirements

Node 22 and npm 10. Everything else, including the Angular CLI 22, is installed by `npm install`.

## Install

```bash
cd src/frontend
npm install
```

## Run

```bash
npm start
```

Open <http://localhost:4200>. The dev server proxies `/api` and `/hubs` (with WebSockets) to
`http://localhost:5160` (see `proxy.conf.json`; edit it if the backend listens elsewhere).

**The backend must be running on port 5160 (mock / simulated mode) for the page to show data.**
Start the backend first, then use Connect in the top bar to connect the simulated console.
Without the backend the page still renders: status "Disconnected - Backend not reachable,
retrying", mode "Mode unknown", every fader disabled. It keeps retrying (after 0.5 s,
1 s, 2 s, 5 s, then every 10 s) and picks the backend up by itself when it appears, also after
the backend was restarted. The app writes nothing to the browser console while the backend is
down; note that the browser itself lists each failed connection attempt (`/hubs/console/negotiate`)
in its own network log, which page code cannot suppress.

## Test

```bash
npm test
```

Runs the unit tests once with Vitest on jsdom: headless, no browser to install, **no backend
needed** (the hub connection is replaced by a fake, `src/app/core/testing/fake-console-hub.ts`).
`npm run test:watch` keeps them running while you edit.

## Build

```bash
npm run build
```

Output goes to `dist/smartfade-ui`.

## Using the page

| Area | What it does |
|---|---|
| Top bar | Mode badge (SIMULATED / REPLAY), connection status, Connect / Disconnect, show file name and firmware version when known, last error (dismissible) |
| Channels | 48 faders in two banks of 24 (1-24, 25-48). The thumb is the desired level, the lit bar the level the console reports; an amber stretch and lamp show a level the console has not reached yet |
| Memories | The 24 memory faders of one page with the page keys 1-12; REC MEM / EDIT MEM / COPY from the Record column pick a memory here |
| Faders | The console's FADERS keys in its order and colours: MEMORIES (red), 25-48, 1-24 (green), NEXT (blue). A real console reports which bank its 24 physical faders control (`faderBank`) and that key is lit; MEMORIES and 1-24 are taps where the device accepts them, 25-48 and NEXT are shown inactive |
| Crossfader | The A / B faders (read-only playback from a real console), GO (a tap where accepted), STACK, PAUSE and RATE (inactive), CLEAR under them two keys wide (aborts an armed function, then clears the channel faders, then the memory faders) |
| Record | COPY, REC SEQ, MAGIC, REC MEM, EDIT MEM, SNAPSHOT in the console's colours; a real console's key LEDs are mirrored |
| Masters | BUMPS under SOLO, MASTER under BLACKOUT (blue; blinks while the MASTER is below full, lit while blackout is on) |
| Independents | IND 1 / IND 2 in their own section under the masters (DMX 97 / 98 by default) |
| Outputs | The 512 outputs as thin bars on one canvas; hover for the level of one output; collapsible |

Faders are disabled while the link to the backend is down or the console is not connected.

### Keyboard

| Key | Effect |
|---|---|
| Tab / Shift+Tab | Next / previous control (every fader is a tab stop) |
| Left / Right | Move focus to the previous / next fader (channel 24 continues at 25) |
| Up / Down | Level +1 / -1 |
| PageUp / PageDown | Level +10 / -10 |
| Home / End | Level 0 % / 100 % |
| Space / Enter | Press the focused button (Connect, Disconnect, Blackout) |

Levels move in steps of the console's own unit (0-255), so one step is about 0.4 %.

## How it works

```
components  ->  ChannelService  ->  ConsoleConnectionService  ->  hub /hubs/console
(render, capture input)  (intent)        (link, commands)
     ^                                          |
     |                                          v
     +----------  ConsoleStateService  <-- server events
                  (signals: channels, masters, outputs, connection, error)
```

- **ConsoleConnectionService** owns the SignalR connection. It connects on start-up, reconnects
  automatically, exposes the link status as a signal and dispatches every server event to the
  state service. Commands are sent once and never retried; a failed or rejected command is shown
  in the top bar and followed by a fresh `getState()`, because the backend is the source of truth.
- **ConsoleStateService** holds the page's copy of the console state as signals and applies each
  event (`consoleStateChanged`, `connectionStateChanged`, `channelStateChanged`, `mastersChanged`,
  `outputsChanged`, `errorOccurred`).
- **ChannelService** turns intent into commands: `setChannelLevel`, `setMaster`, `setBlackout`.
  It updates `desired` and `pending` at once (optimistically) and sends level changes at most
  once per animation frame per fader, with the latest value, so a fast drag cannot flood the
  backend. The backend confirms or corrects through its events.
- Components only render state, capture input and call these services. They are standalone,
  OnPush and signal based.

```
src/app/
  core/                 services, contract types, hub abstraction, level helpers
    testing/            fake hub and sample state for unit tests
  components/
    connection-bar/     mode badge, status, Connect / Disconnect, facts, error
    channel-fader/      one vertical fader (channels, MASTER, BUMPS)
    channel-grid/       the 48 faders in two banks
    memory-grid/        the 24 memory faders of a page, the page keys
    fader-mode-keys/    the FADERS column: MEMORIES, 25-48, 1-24, NEXT
    crossfader/         A / B, GO, STACK, PAUSE, RATE, CLEAR
    record-keys/        COPY, REC SEQ, MAGIC, REC MEM, EDIT MEM, SNAPSHOT
    masters-panel/      SOLO / BUMPS, BLACKOUT / MASTER
    independents-panel/ IND 1, IND 2
    show-file-page/     the .asc show-file viewer and editor
    output-meters/      512 output bars on a canvas
  app.component.*       the page layout
```

Conventions: selector prefix `sf-`, one folder per component with `.component.ts/.html/.scss`,
no routing and no state-management library, dependencies limited to Angular, RxJS and
`@microsoft/signalr`. Colours and fonts are CSS custom properties (`--sf-*`) in `src/styles.scss`.
