import { InjectionToken } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type {
  ChannelStateDto,
  ConnectionDto,
  ConsoleStateDto,
  CrossfaderDto,
  ErrorDto,
  MastersDto,
  MemoryStateDto,
  OutputsChangedDto,
} from './console.models';

/** The hub of the backend (docs/api.md). The dev server proxies it to the backend. */
export const CONSOLE_HUB_URL = '/hubs/console';

/** Server to client events and their payloads (docs/api.md). */
export interface ConsoleHubEvents {
  consoleStateChanged: [state: ConsoleStateDto];
  connectionStateChanged: [connection: ConnectionDto];
  channelStateChanged: [channels: ChannelStateDto[]];
  mastersChanged: [masters: MastersDto];
  memoriesChanged: [memories: MemoryStateDto[]];
  outputsChanged: [update: OutputsChangedDto];
  lcdChanged: [lines: string[]];
  crossfaderChanged: [crossfader: CrossfaderDto];
  errorOccurred: [error: ErrorDto];
}

export type ConsoleHubEventName = keyof ConsoleHubEvents;

/** Client to server methods (docs/api.md). */
export type ConsoleHubMethod =
  | 'getState'
  | 'connect'
  | 'disconnect'
  | 'setChannelLevel'
  | 'setMaster'
  | 'setBlackout'
  | 'setBumps'
  | 'setSolo'
  | 'bumpChannel'
  | 'pressGo'
  | 'pressFadersMems'
  | 'pressFaders1To24'
  | 'setMemoryLevel'
  | 'bumpMemory'
  | 'recordMemory'
  | 'storeMemory'
  | 'getMemory'
  | 'setIndependent'
  | 'clearChannels'
  | 'clearMemories';

/**
 * The part of a SignalR `HubConnection` that the UI uses. A real `HubConnection` satisfies it;
 * tests substitute a fake, so nothing in the unit tests needs a backend.
 */
export interface ConsoleHub {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<T = void>(method: ConsoleHubMethod, ...args: unknown[]): Promise<T>;
  on<K extends ConsoleHubEventName>(
    event: K,
    handler: (...args: ConsoleHubEvents[K]) => void,
  ): void;
  onreconnecting(handler: (error?: Error) => void): void;
  onreconnected(handler: (connectionId?: string) => void): void;
  onclose(handler: (error?: Error) => void): void;
}

export type ConsoleHubFactory = () => ConsoleHub;

/** Waits between attempts to reach the backend, in ms. The last value repeats indefinitely. */
const RETRY_DELAYS_MS = [500, 1000, 2000, 5000, 10000] as const;

/** Delay before retry number `attempt` (0 = first retry). */
export function retryDelay(attempt: number): number {
  return RETRY_DELAYS_MS[Math.min(Math.max(attempt, 0), RETRY_DELAYS_MS.length - 1)];
}

/**
 * Creates the real hub connection. SignalR's own logging is switched off on purpose: while the
 * backend is not running the page must stay quiet, and every problem is surfaced in the UI
 * (connection bar) instead of the browser console.
 */
export function createConsoleHub(): ConsoleHub {
  return new HubConnectionBuilder()
    .withUrl(CONSOLE_HUB_URL)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount),
    })
    .configureLogging(LogLevel.None)
    .build();
}

export const CONSOLE_HUB_FACTORY = new InjectionToken<ConsoleHubFactory>('CONSOLE_HUB_FACTORY', {
  providedIn: 'root',
  factory: () => createConsoleHub,
});
