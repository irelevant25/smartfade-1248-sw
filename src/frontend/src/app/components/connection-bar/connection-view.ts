import type { LinkStatus } from '../../core/console-connection.service';
import type { ConnectionDto, ConnectionStatus, DeviceMode, StateAtConnectDto } from '../../core/console.models';

export type ModeTone = 'warn' | 'info' | 'alert' | 'unknown';

export interface ModeView {
  label: string;
  tone: ModeTone;
  hint: string;
}

export type StatusTone = 'ok' | 'busy' | 'idle' | 'error';

export interface StatusView {
  label: string;
  tone: StatusTone;
  detail: string;
}

/**
 * What the mode badge shows. The mode comes from the backend only, so before the backend has
 * answered it is unknown, and a mode this UI does not recognise is shown loudly rather than
 * quietly passed off as a known one.
 */
export function describeMode(mode: DeviceMode | undefined): ModeView {
  switch (mode) {
    case undefined:
      return {
        label: 'Mode unknown',
        tone: 'unknown',
        hint: 'The mode is reported by the backend, which has not answered yet.',
      };
    case 'simulated':
      return {
        label: 'Simulated',
        tone: 'warn',
        hint: 'Simulated console: no real hardware is involved.',
      };
    case 'replay':
      return { label: 'Replay', tone: 'info', hint: 'Replaying recorded console data.' };
    case 'hardware':
      return {
        label: 'Hardware',
        tone: 'alert',
        hint: 'The real console, read-only (safety check SC-1) unless a LIVE CONTROL badge is shown beside this one.',
      };
    default:
      return {
        label: String(mode).toUpperCase(),
        tone: 'alert',
        hint: 'The backend reported a mode this page does not know.',
      };
  }
}

/**
 * One status for the whole chain: the link to the backend comes first, and only when it is up
 * does the status of the console behind it count.
 */
export function describeStatus(
  link: LinkStatus,
  connection: ConnectionDto | undefined,
): StatusView {
  if (link === 'connecting') {
    return { label: 'Connecting', tone: 'busy', detail: 'Reaching the backend' };
  }
  if (link === 'reconnecting') {
    return { label: 'Reconnecting', tone: 'busy', detail: 'Connection to the backend lost' };
  }
  if (link === 'disconnected') {
    return { label: 'Disconnected', tone: 'idle', detail: 'Backend not reachable, retrying' };
  }

  switch (connection?.status) {
    case 'connected':
      return { label: 'Connected', tone: 'ok', detail: connection.deviceDescription };
    case 'connecting':
      return { label: 'Connecting', tone: 'busy', detail: connection.deviceDescription };
    case 'error':
      return { label: 'Error', tone: 'error', detail: connection.deviceDescription };
    default:
      return {
        label: 'Disconnected',
        tone: 'idle',
        detail:
          connection?.mode === 'hardware'
            ? 'Console not connected: press Connect with the console on, or start the backend in simulated mode to work without it'
            : 'Console not connected',
      };
  }
}

export type StateAtConnectTone = 'busy' | 'warn' | 'idle';

export interface StateAtConnectView {
  text: string;
  tone: StateAtConnectTone;
}

/**
 * What the bar says about the real console's state at Connect (SC-4) besides its STATE AT CONNECT badge: before Connect,
 * what the console must be showing and must not be doing when the pair is sent blind, as SmartSoft sends it (clause 3: the
 * states every pair was seen in, F185, and the states the device refuses once reported, the page among them since the first
 * review's MAJOR 1); while the pair and the download run, that the state is being requested or downloaded (every control is
 * refused then, 7.2–7.6 s observed); why the state was not requested; and after the download, a MASTER probe that stopped
 * away from the bank or mode the download reported (the first review's MINOR 1). Nothing else once the download is complete,
 * nor without the switch.
 */
export function describeStateAtConnect(state: StateAtConnectDto | undefined, status: ConnectionStatus | undefined): StateAtConnectView | undefined {
  if (!state) {
    return undefined;
  }
  switch (state.phase) {
    case 'idle':
      return status === 'connected' || status === 'connecting'
        ? undefined
        : {
            text: 'Before Connect: page 1, bank 1-24 or memory mode, NEXT and BLACKOUT off; no menu open on the console, no SD save or load, no record function armed, no bump held',
            tone: 'idle',
          };
    case 'done':
      return state.masterProbe === 'stopped'
        ? { text: upperFirst(state.masterProbeReason ?? 'the MASTER probe stopped without switching back'), tone: 'warn' }
        : undefined;
    case 'waiting':
    case 'requesting':
      return { text: "Requesting the console's state…", tone: 'busy' };
    case 'downloading':
      return {
        text: typeof state.records === 'number' ? `Downloading the console's state… ${state.delivered} / ${state.records}` : "Downloading the console's state…",
        tone: 'busy',
      };
    case 'notSent':
      return { text: `The console's state was not requested: ${state.reason ?? 'no reason given'}`, tone: 'warn' };
    default:
      return undefined;
  }
}

/** The backend's reasons start in lower case, as clauses of its log notes; the bar shows one as a sentence. */
function upperFirst(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** The Connect button makes sense only when the link is up and the console is not. */
export function canConnect(link: LinkStatus, connection: ConnectionDto | undefined): boolean {
  const status = connection?.status ?? 'disconnected';
  return link === 'connected' && (status === 'disconnected' || status === 'error');
}

/** The Disconnect button makes sense only when the link is up and the console is (or is trying to be). */
export function canDisconnect(link: LinkStatus, connection: ConnectionDto | undefined): boolean {
  const status = connection?.status;
  return link === 'connected' && (status === 'connected' || status === 'connecting');
}
