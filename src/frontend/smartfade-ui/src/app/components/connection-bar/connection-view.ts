import type { LinkStatus } from '../../core/console-connection.service';
import type { ConnectionDto, DeviceMode } from '../../core/console.models';

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
      return { label: 'Disconnected', tone: 'idle', detail: 'Console not connected' };
  }
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
