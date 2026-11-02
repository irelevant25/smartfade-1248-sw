import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  CONSOLE_HUB_FACTORY,
  retryDelay,
  type ConsoleHub,
  type ConsoleHubMethod,
} from './console-hub';
import { ConsoleStateService } from './console-state.service';
import type { ConsoleStateDto, ErrorDto, MemoryDto } from './console.models';

/** State of the link between this page and the backend (not of the console itself). */
export type LinkStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

function toErrorDto(error: unknown): ErrorDto {
  const message = error instanceof Error && error.message ? error.message : 'The request failed.';
  return { message };
}

/**
 * Owns the connection to the backend hub: keeps it alive (initial connection and reconnects
 * retry indefinitely with a growing delay), dispatches every server event to the state service
 * and passes commands on. It never talks to a console itself and never retries a command.
 */
@Injectable({ providedIn: 'root' })
export class ConsoleConnectionService {
  private readonly _state = inject(ConsoleStateService);
  private readonly _createHub = inject(CONSOLE_HUB_FACTORY);

  private readonly _status = signal<LinkStatus>('disconnected');
  private readonly _busy = signal(false);

  private _hub: ConsoleHub | undefined;
  private _running = false;
  /** Identifies the current connect loop so that an older loop stops when a newer one starts. */
  private _generation = 0;
  private _wake: (() => void) | undefined;
  private _stateRequest: Promise<ConsoleStateDto | undefined> | undefined;

  /** Status of the link to the backend. */
  readonly status = this._status.asReadonly();
  /** True while a connect / disconnect request is being processed. */
  readonly busy = this._busy.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => void this.stop());
  }

  /** Opens the link and keeps it open. Safe to call more than once. */
  start(): void {
    if (this._running) {
      return;
    }
    this._running = true;
    this._hub ??= this._createAndWireHub();
    void this._connectWithRetry(this._hub, ++this._generation);
  }

  /** Closes the link and stops retrying. */
  async stop(): Promise<void> {
    this._running = false;
    this._generation++;
    this._wake?.();
    this._status.set('disconnected');
    try {
      await this._hub?.stop();
    } catch {
      // Already stopped; nothing left to release.
    }
  }

  /** Hub method connect(): connects the configured device. The result arrives as events. */
  connect(): Promise<void> {
    return this._runExclusive('connect');
  }

  /** Hub method disconnect(). */
  disconnect(): Promise<void> {
    return this._runExclusive('disconnect');
  }

  /** Hub method getState(): reads the full state and applies it. Concurrent calls share one request. */
  getState(): Promise<ConsoleStateDto | undefined> {
    const hub = this._hub;
    if (!hub || this._status() !== 'connected') {
      return Promise.resolve(undefined);
    }
    this._stateRequest ??= this._fetchState(hub).finally(() => {
      this._stateRequest = undefined;
    });
    return this._stateRequest;
  }

  sendChannelLevel(channel: number, level: number): Promise<void> {
    return this._send('setChannelLevel', channel, level);
  }

  sendMaster(level: number): Promise<void> {
    return this._send('setMaster', level);
  }

  sendBlackout(on: boolean): Promise<void> {
    return this._send('setBlackout', on);
  }

  sendBumps(level: number): Promise<void> {
    return this._send('setBumps', level);
  }

  sendSolo(on: boolean): Promise<void> {
    return this._send('setSolo', on);
  }

  sendBumpChannel(channel: number, pressed: boolean): Promise<void> {
    return this._send('bumpChannel', channel, pressed);
  }

  sendPressGo(): Promise<void> {
    return this._send('pressGo');
  }

  sendPressFadersMems(): Promise<void> {
    return this._send('pressFadersMems');
  }

  sendPressFaders1To24(): Promise<void> {
    return this._send('pressFaders1To24');
  }

  sendPressFaders25To48(): Promise<void> {
    return this._send('pressFaders25To48');
  }

  sendMemoryLevel(page: number, fader: number, level: number): Promise<void> {
    return this._send('setMemoryLevel', page, fader, level);
  }

  sendBumpMemory(page: number, fader: number, pressed: boolean): Promise<void> {
    return this._send('bumpMemory', page, fader, pressed);
  }

  sendRecordMemory(page: number, fader: number): Promise<void> {
    return this._send('recordMemory', page, fader);
  }

  sendStoreMemory(page: number, fader: number, levels: readonly number[]): Promise<void> {
    return this._send('storeMemory', page, fader, Array.from(levels));
  }

  sendIndependent(number: number, on: boolean): Promise<void> {
    return this._send('setIndependent', number, on);
  }

  sendClearChannels(): Promise<void> {
    return this._send('clearChannels');
  }

  sendClearMemories(): Promise<void> {
    return this._send('clearMemories');
  }

  /**
   * Hub method getMemory(): one memory's content, or undefined when the link is down or the
   * backend rejected the request (the rejection arrives as an `errorOccurred` event).
   */
  async getMemory(page: number, fader: number): Promise<MemoryDto | undefined> {
    const hub = this._hub;
    if (!hub || this._status() !== 'connected') {
      return undefined;
    }
    try {
      const memory = await hub.invoke<MemoryDto | null>('getMemory', page, fader);
      return memory ?? undefined;
    } catch (error) {
      this._state.applyErrorOccurred(toErrorDto(error));
      return undefined;
    }
  }

  private _createAndWireHub(): ConsoleHub {
    const hub = this._createHub();

    hub.on('consoleStateChanged', (state) => this._state.applyConsoleStateChanged(state));
    hub.on('connectionStateChanged', (connection) =>
      this._state.applyConnectionStateChanged(connection),
    );
    hub.on('channelStateChanged', (channels) => this._state.applyChannelStateChanged(channels));
    hub.on('mastersChanged', (masters) => this._state.applyMastersChanged(masters));
    hub.on('memoriesChanged', (memories) => this._state.applyMemoriesChanged(memories));
    hub.on('outputsChanged', (update) => this._state.applyOutputsChanged(update));
    hub.on('lcdChanged', (lines) => this._state.applyLcdChanged(lines));
    hub.on('crossfaderChanged', (crossfader) => this._state.applyCrossfaderChanged(crossfader));
    hub.on('errorOccurred', (error) => {
      this._state.applyErrorOccurred(error);
      // A rejected command leaves optimistic values behind; the backend's state is the truth.
      void this.getState();
    });

    hub.onreconnecting(() => this._status.set('reconnecting'));
    hub.onreconnected(() => {
      this._status.set('connected');
      void this.getState().then((state) => this._connectSimulatorIfIdle(state));
    });
    hub.onclose(() => {
      if (this._running) {
        // Closed by the server although we want the link: connect again.
        void this._connectWithRetry(hub, ++this._generation, 'reconnecting');
      } else {
        this._status.set('disconnected');
      }
    });

    return hub;
  }

  private async _connectWithRetry(
    hub: ConsoleHub,
    generation: number,
    firstStatus: 'connecting' | 'reconnecting' = 'connecting',
  ): Promise<void> {
    const isCurrent = () => this._running && generation === this._generation;

    for (let attempt = 0; isCurrent(); attempt++) {
      if (attempt === 0) {
        this._status.set(firstStatus);
      }
      try {
        await hub.start();
      } catch {
        if (!isCurrent()) {
          return;
        }
        this._status.set('disconnected');
        await this._sleep(retryDelay(attempt));
        continue;
      }
      if (!isCurrent()) {
        await hub.stop().catch(() => undefined);
        return;
      }
      this._status.set('connected');
      this._connectSimulatorIfIdle(await this.getState());
      return;
    }
  }

  /**
   * The simulated console connects by itself when the link comes up: there is no hardware behind it and nothing to
   * protect, and the owner wants the software usable without the console (2026-11-02). The real console is never
   * connected without a press of Connect: that is an explicit, visible act (SC-1, SC-2). A simulator the user has
   * disconnected on purpose stays so until the link is made again.
   */
  private _connectSimulatorIfIdle(state: ConsoleStateDto | undefined): void {
    if (state?.connection.mode === 'simulated' && state.connection.status === 'disconnected') {
      void this.connect();
    }
  }

  private _sleep(milliseconds: number): Promise<void> {
    return new Promise<void>((resolve) => {
      const done = () => {
        clearTimeout(timer);
        resolve();
      };
      const timer = setTimeout(done, milliseconds);
      this._wake = done;
    });
  }

  private async _fetchState(hub: ConsoleHub): Promise<ConsoleStateDto | undefined> {
    try {
      const state = await hub.invoke<ConsoleStateDto>('getState');
      this._state.applyConsoleStateChanged(state);
      return state;
    } catch (error) {
      this._state.applyErrorOccurred(toErrorDto(error));
      return undefined;
    }
  }

  /** connect / disconnect have side effects on the console: one at a time, never retried. */
  private async _runExclusive(method: 'connect' | 'disconnect'): Promise<void> {
    const hub = this._hub;
    if (!hub || this._busy() || this._status() !== 'connected') {
      return;
    }
    this._busy.set(true);
    this._state.clearError();
    try {
      await hub.invoke(method);
    } catch (error) {
      this._state.applyErrorOccurred(toErrorDto(error));
    } finally {
      this._busy.set(false);
    }
  }

  /** Sends a command once. A failure is reported and followed by a state refresh, not a retry. */
  private async _send(method: ConsoleHubMethod, ...args: unknown[]): Promise<void> {
    const hub = this._hub;
    if (!hub || this._status() !== 'connected') {
      return;
    }
    try {
      await hub.invoke(method, ...args);
    } catch (error) {
      this._state.applyErrorOccurred(toErrorDto(error));
      void this.getState();
    }
  }
}
