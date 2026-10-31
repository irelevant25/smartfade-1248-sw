import { computed, DestroyRef, inject, Injectable } from '@angular/core';
import { ConsoleConnectionService } from './console-connection.service';
import { ConsoleStateService } from './console-state.service';
import {
  CHANNEL_COUNT,
  INDEPENDENT_COUNT,
  isMemoryFader,
  isMemoryPage,
  memoryNumber,
  type MemoryDto,
} from './console.models';
import { FRAME_SCHEDULER } from './frame-scheduler';
import { clampLevel } from './level';

interface QueuedMemoryLevel {
  page: number;
  fader: number;
  level: number;
}

/**
 * Turns user intent into commands. Fader moves (channels, memories, MASTER, BUMPS) are applied
 * optimistically and sent at most once per animation frame per fader, carrying the latest value,
 * so a fast drag does not flood the backend. Buttons (bumps, SOLO, BLACKOUT, REC MEM, EDIT MEM)
 * are sent right away, in order. The backend remains the source of truth: it confirms or
 * corrects the optimistic values through its events.
 */
@Injectable({ providedIn: 'root' })
export class ChannelService {
  private readonly _state = inject(ConsoleStateService);
  private readonly _connection = inject(ConsoleConnectionService);
  private readonly _frames = inject(FRAME_SCHEDULER);

  /** Latest level per channel waiting for the next frame; a newer value replaces an older one. */
  private readonly _queuedChannels = new Map<number, number>();
  /** Latest level per memory (by number) waiting for the next frame. */
  private readonly _queuedMemories = new Map<number, QueuedMemoryLevel>();
  private _queuedMaster: number | undefined;
  private _queuedBumps: number | undefined;
  private _frame: number | undefined;

  /** True when commands can be sent: the backend link is up and the console is connected. */
  readonly enabled = computed(
    () => this._connection.status() === 'connected' && this._state.deviceStatus() === 'connected',
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this._frame !== undefined) {
        this._frames.cancel(this._frame);
      }
    });
  }

  /** Sets the desired level of one channel (1..48) to a level in 0..255. */
  setChannelLevel(channel: number, level: number): void {
    if (!this.enabled() || !Number.isInteger(channel) || channel < 1 || channel > CHANNEL_COUNT) {
      return;
    }
    const value = clampLevel(level);
    this._state.setDesiredChannelLevel(channel, value);
    this._queuedChannels.set(channel, value);
    this._scheduleFlush();
  }

  /** Sets the desired MASTER level (0..255). */
  setMaster(level: number): void {
    if (!this.enabled()) {
      return;
    }
    const value = clampLevel(level);
    this._state.setDesiredMaster(value);
    this._queuedMaster = value;
    this._scheduleFlush();
  }

  /** Sets the desired BUMPS level (0..255): the level bumps go to. */
  setBumps(level: number): void {
    if (!this.enabled()) {
      return;
    }
    const value = clampLevel(level);
    this._state.setDesiredBumps(value);
    this._queuedBumps = value;
    this._scheduleFlush();
  }

  /** Switches blackout on or off. A deliberate toggle, so it is sent right away. */
  setBlackout(on: boolean): void {
    if (!this.enabled()) {
      return;
    }
    this._state.setDesiredBlackout(on);
    void this._connection.sendBlackout(on);
  }

  /** Switches SOLO on or off (bump mode: pile-on or solo). Sent right away. */
  setSolo(on: boolean): void {
    if (!this.enabled()) {
      return;
    }
    this._state.setDesiredSolo(on);
    void this._connection.sendSolo(on);
  }

  /** GO as a tap: the next step of the stack. Sent right away; only a device that models the stack accepts it (the UI enables the key from `supportedCommands`). */
  pressGo(): void {
    if (!this.enabled()) {
      return;
    }
    void this._connection.sendPressGo();
  }

  /** FADERS MEMS as a tap: the physical faders to memory mode. Sent right away. */
  pressFadersMems(): void {
    if (!this.enabled()) {
      return;
    }
    void this._connection.sendPressFadersMems();
  }

  /** FADERS 1-24 as a tap: the physical faders to channel mode, bank 1-24. Sent right away. */
  pressFaders1To24(): void {
    if (!this.enabled()) {
      return;
    }
    void this._connection.sendPressFaders1To24();
  }

  /** IND 1 (1) or IND 2 (2) on or off. Sent right away. */
  setIndependent(number: number, on: boolean): void {
    if (
      !this.enabled() ||
      !Number.isInteger(number) ||
      number < 1 ||
      number > INDEPENDENT_COUNT
    ) {
      return;
    }
    this._state.setDesiredIndependent(number, on);
    void this._connection.sendIndependent(number, on);
  }

  /** CLEAR pressed twice: every channel fader to zero, at once here and then on the console. */
  clearChannels(): void {
    if (!this.enabled()) {
      return;
    }
    this._queuedChannels.clear();
    this._state.clearDesiredChannels();
    void this._connection.sendClearChannels();
  }

  /** CLEAR pressed three times: every memory fader to zero. */
  clearMemories(): void {
    if (!this.enabled()) {
      return;
    }
    this._queuedMemories.clear();
    this._state.clearDesiredMemoryLevels();
    void this._connection.sendClearMemories();
  }

  /** Presses (true) or releases (false) the bump button under a channel fader. Sent right away. */
  bumpChannel(channel: number, pressed: boolean): void {
    if (!this.enabled() || !Number.isInteger(channel) || channel < 1 || channel > CHANNEL_COUNT) {
      return;
    }
    void this._connection.sendBumpChannel(channel, pressed);
  }

  /** Moves a memory fader (page 1..12, fader 1..24) to a level in 0..255. */
  setMemoryLevel(page: number, fader: number, level: number): void {
    if (!this.enabled() || !isMemoryPage(page) || !isMemoryFader(fader)) {
      return;
    }
    const value = clampLevel(level);
    this._state.setDesiredMemoryLevel(page, fader, value);
    this._queuedMemories.set(memoryNumber(page, fader), { page, fader, level: value });
    this._scheduleFlush();
  }

  /** Presses (true) or releases (false) the bump button under a memory fader. Sent right away. */
  bumpMemory(page: number, fader: number, pressed: boolean): void {
    if (!this.enabled() || !isMemoryPage(page) || !isMemoryFader(fader)) {
      return;
    }
    void this._connection.sendBumpMemory(page, fader, pressed);
  }

  /** REC MEM: asks the backend to store the current look into the memory. */
  recordMemory(page: number, fader: number): Promise<void> {
    if (!this.enabled() || !isMemoryPage(page) || !isMemoryFader(fader)) {
      return Promise.resolve();
    }
    return this._connection.sendRecordMemory(page, fader);
  }

  /** EDIT MEM: replaces the memory's 48 channel levels. */
  storeMemory(page: number, fader: number, levels: readonly number[]): Promise<void> {
    if (
      !this.enabled() ||
      !isMemoryPage(page) ||
      !isMemoryFader(fader) ||
      levels.length !== CHANNEL_COUNT
    ) {
      return Promise.resolve();
    }
    return this._connection.sendStoreMemory(page, fader, levels.map(clampLevel));
  }

  /** Reads one memory's content, or undefined when it cannot be read right now. */
  getMemory(page: number, fader: number): Promise<MemoryDto | undefined> {
    if (!this.enabled() || !isMemoryPage(page) || !isMemoryFader(fader)) {
      return Promise.resolve(undefined);
    }
    return this._connection.getMemory(page, fader);
  }

  private _scheduleFlush(): void {
    if (this._frame === undefined) {
      this._frame = this._frames.request(() => this._flush());
    }
  }

  private _flush(): void {
    this._frame = undefined;

    const channels = Array.from(this._queuedChannels);
    this._queuedChannels.clear();
    const memories = Array.from(this._queuedMemories.values());
    this._queuedMemories.clear();
    const master = this._queuedMaster;
    this._queuedMaster = undefined;
    const bumps = this._queuedBumps;
    this._queuedBumps = undefined;

    for (const [channel, level] of channels) {
      void this._connection.sendChannelLevel(channel, level);
    }
    for (const { page, fader, level } of memories) {
      void this._connection.sendMemoryLevel(page, fader, level);
    }
    if (master !== undefined) {
      void this._connection.sendMaster(master);
    }
    if (bumps !== undefined) {
      void this._connection.sendBumps(bumps);
    }
  }
}
