import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { ChannelService } from './channel.service';
import { CHANNEL_COUNT, isMemoryFader, isMemoryPage } from './console.models';
import { clampLevel } from './level';

/**
 * What the REC MEM, EDIT MEM and COPY keys are doing, mirroring the console (manual v3.0,
 * printed pp. 32 and 44, F107):
 * - `record`: REC MEM is armed; the next memory pressed receives the current look.
 * - `select`: EDIT MEM is armed; the next recorded memory pressed is opened for editing.
 * - `edit`: a memory's levels are on the channel faders as a draft; EDIT MEM stores them,
 *   CLEAR aborts.
 * - `copy-source`: COPY is armed; the next recorded memory pressed is the source.
 * - `copy-target`: the source is read; the next memory pressed receives a copy of it.
 */
export type MemoryEditMode = 'idle' | 'record' | 'select' | 'edit' | 'copy-source' | 'copy-target';

/** The memory being edited, with its levels as the user is changing them (not yet stored). */
export interface MemoryDraft {
  page: number;
  fader: number;
  number: number;
  label: string;
  levels: readonly number[];
}

/** The memory chosen as the source of a COPY, with the levels read from the backend. */
export interface CopySource {
  page: number;
  fader: number;
  number: number;
  levels: readonly number[];
}

/**
 * The REC MEM / EDIT MEM / COPY workflow of the console, kept here because several panels take
 * part: the record keys arm a function, the memory panel picks the memory, the channel panel
 * shows and edits the draft, the CLEAR key aborts. Nothing is sent until a memory is picked
 * (record, copy) or the draft is stored (edit); the draft is a blind edit, the stage does not
 * change until it is stored.
 */
@Injectable({ providedIn: 'root' })
export class MemoryEditService {
  private readonly _channels = inject(ChannelService);

  private readonly _mode = signal<MemoryEditMode>('idle');
  private readonly _draft = signal<MemoryDraft | undefined>(undefined);
  private readonly _copySource = signal<CopySource | undefined>(undefined);
  private readonly _loading = signal(false);

  readonly mode = this._mode.asReadonly();
  readonly draft = this._draft.asReadonly();
  readonly copySource = this._copySource.asReadonly();
  /** True while a memory's content is being fetched. */
  readonly loading = this._loading.asReadonly();
  /** The draft while in edit mode, else undefined. */
  readonly editing = computed(() => (this._mode() === 'edit' ? this._draft() : undefined));
  /** True while any of the three functions is armed or in progress (the console blinks the key). */
  readonly armed = computed(() => this._mode() !== 'idle');

  constructor() {
    // Losing the console ends whatever was armed or being edited: the draft would be stale.
    effect(() => {
      if (!this._channels.enabled()) {
        this.cancel();
      }
    });
  }

  /** The REC MEM key: arms recording, or disarms it when pressed again. */
  toggleRecord(): void {
    if (!this._channels.enabled()) {
      return;
    }
    const wasArmed = this._mode() === 'record';
    this._reset();
    this._mode.set(wasArmed ? 'idle' : 'record');
  }

  /** The EDIT MEM key: arms editing; while editing it stores the draft; pressed again while armed it disarms. */
  toggleEdit(): void {
    if (!this._channels.enabled()) {
      return;
    }
    if (this._mode() === 'edit') {
      void this.store();
      return;
    }
    const wasArmed = this._mode() === 'select';
    this._reset();
    this._mode.set(wasArmed ? 'idle' : 'select');
  }

  /** The COPY key: arms copying, or disarms it when pressed again. */
  toggleCopy(): void {
    if (!this._channels.enabled()) {
      return;
    }
    const wasArmed = this._mode() === 'copy-source' || this._mode() === 'copy-target';
    this._reset();
    this._mode.set(wasArmed ? 'idle' : 'copy-source');
  }

  /** The CLEAR key: leaves whatever is armed; an edit in progress is abandoned. */
  cancel(): void {
    this._reset();
  }

  /**
   * A memory's bump button pressed while a function is armed. Returns false when nothing is
   * armed, in which case the press is an ordinary bump.
   */
  async pickMemory(page: number, fader: number, recorded: boolean): Promise<boolean> {
    if (!isMemoryPage(page) || !isMemoryFader(fader)) {
      return false;
    }
    switch (this._mode()) {
      case 'record':
        this._mode.set('idle');
        await this._channels.recordMemory(page, fader);
        return true;
      case 'select':
        await this._openForEditing(page, fader, recorded);
        return true;
      case 'copy-source':
        await this._takeCopySource(page, fader, recorded);
        return true;
      case 'copy-target':
        await this._copyInto(page, fader);
        return true;
      default:
        return false;
    }
  }

  /** Changes one channel of the draft (1..48). */
  setDraftLevel(channel: number, level: number): void {
    const draft = this._draft();
    if (
      !draft ||
      this._mode() !== 'edit' ||
      !Number.isInteger(channel) ||
      channel < 1 ||
      channel > CHANNEL_COUNT
    ) {
      return;
    }
    const value = clampLevel(level);
    if (draft.levels[channel - 1] === value) {
      return;
    }
    const levels = draft.levels.slice();
    levels[channel - 1] = value;
    this._draft.set({ ...draft, levels });
  }

  /** Stores the draft into its memory and leaves edit mode (the console's second EDIT MEM press). */
  async store(): Promise<void> {
    const draft = this._draft();
    if (!draft || this._mode() !== 'edit') {
      return;
    }
    this._reset();
    await this._channels.storeMemory(draft.page, draft.fader, draft.levels);
  }

  private async _openForEditing(page: number, fader: number, recorded: boolean): Promise<void> {
    if (!recorded) {
      return;   // only memories with content can be edited (console manual printed p.44)
    }
    const memory = await this._read(page, fader);
    if (this._mode() !== 'select') {
      return;   // cancelled or disconnected meanwhile
    }
    if (!memory) {
      this._reset();
      return;
    }
    this._draft.set({ page, fader, number: memory.number, label: memory.label, levels: memory.levels });
    this._mode.set('edit');
  }

  private async _takeCopySource(page: number, fader: number, recorded: boolean): Promise<void> {
    if (!recorded) {
      return;   // "All buttons with content will blink" (console manual printed p.44)
    }
    const memory = await this._read(page, fader);
    if (this._mode() !== 'copy-source') {
      return;
    }
    if (!memory) {
      this._reset();
      return;
    }
    this._copySource.set({ page, fader, number: memory.number, levels: memory.levels });
    this._mode.set('copy-target');
  }

  private async _copyInto(page: number, fader: number): Promise<void> {
    const source = this._copySource();
    if (!source) {
      this._reset();
      return;
    }
    if (source.page === page && source.fader === fader) {
      return;   // copying a memory onto itself changes nothing
    }
    this._reset();
    await this._channels.storeMemory(page, fader, source.levels);
  }

  /** Reads a memory's content; undefined when it cannot be read or is malformed. */
  private async _read(
    page: number,
    fader: number,
  ): Promise<{ number: number; label: string; levels: number[] } | undefined> {
    this._loading.set(true);
    const memory = await this._channels.getMemory(page, fader);
    this._loading.set(false);
    if (!memory || memory.levels.length !== CHANNEL_COUNT) {
      return undefined;
    }
    return { number: memory.number, label: memory.label ?? '', levels: memory.levels.map(clampLevel) };
  }

  private _reset(): void {
    this._mode.set('idle');
    this._draft.set(undefined);
    this._copySource.set(undefined);
    this._loading.set(false);
  }
}
