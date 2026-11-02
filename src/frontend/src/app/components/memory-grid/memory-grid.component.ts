import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  viewChildren,
} from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { MemoryEditService } from '../../core/memory-edit.service';
import type { MemoryStateDto } from '../../core/console.models';
import { ChannelFaderComponent } from '../channel-fader/channel-fader.component';

/**
 * The glow of a memory's bump button: red at three levels, as on the console (manual: empty
 * slots dim, "A recorded memory is indicated in red. The bump button lights up at full if the
 * fader is above zero, contributing to the light output"; the owner's E3b observation, F8).
 */
export function memoryBumpLit(memory: Pick<MemoryStateDto, 'recorded' | 'level'>): number {
  if (!memory.recorded) {
    return 0.14;
  }
  return memory.level > 0 ? 1 : 0.45;
}

/**
 * The 24 memory faders of one page with the page keys 1-12. Each fader's bump button is
 * numbered as on the console and glows red: faintly when the memory is empty, dimmed when it is
 * recorded, at full while the fader contributes to the output. While REC MEM, EDIT MEM or COPY is
 * armed (record keys panel) the eligible buttons blink and a press picks the memory instead of
 * bumping it. The console's FADERS keys (MEMS / 1-24) live in the FADERS column
 * (`sf-fader-mode-keys`) since 2026-11-02.
 */
@Component({
  selector: 'sf-memory-grid',
  imports: [ChannelFaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './memory-grid.component.html',
  styleUrl: './memory-grid.component.scss',
})
export class MemoryGridComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _channels = inject(ChannelService);
  private readonly _edit = inject(MemoryEditService);
  /** In template order, which is fader order on the current page. */
  private readonly _faders = viewChildren(ChannelFaderComponent);

  protected readonly enabled = this._channels.enabled;
  protected readonly mode = this._edit.mode;
  protected readonly loading = this._edit.loading;
  protected readonly copySource = this._edit.copySource;
  protected readonly readOnly = computed(() => !this._state.supports('setMemoryLevel'));
  protected readonly bumpsDisabled = computed(() => !this._state.supports('bumpMemory'));

  /** A device cleared for a few memory faders or bumps only (docs/api.md `writableControls`) enables just those. */
  protected faderReadOnly(memory: MemoryStateDto): boolean {
    return this.readOnly() || !this._state.writable(`memoryFader:${memory.number}`);
  }

  protected bumpDisabledFor(memory: MemoryStateDto): boolean {
    return (this.bumpsDisabled() || !this._state.writable(`memoryBump:${memory.number}`)) && !this.armedFor(memory);
  }

  /**
   * The console blinks this memory fader's LED: in memory mode, on the page the console shows, physical fader k is memory
   * fader k (F171, F113); display only (F172: an unmatched fader after a mode switch, or one still holding another page's memory).
   */
  protected bumpBlinkingFor(memory: MemoryStateDto): boolean {
    return (
      this._state.faderBank() === 'memories' &&
      this.consolePage() === memory.page &&
      this._state.blinkingFaders().has(memory.fader)
    );
  }
  /** Until the console delivered memories, every memory shown is a placeholder (unrecorded, no label). */
  protected readonly reported = this._state.memoriesReported;
  /** Some memories delivered, others still placeholders (a read-only console sends them one at a time). */
  protected readonly hasUnreported = computed(() => this._state.memories().some((memory) => !memory.reported));
  protected readonly unreportedNote = computed(() =>
    this._state.mode() === 'hardware'
      ? 'Not delivered by the console in this read-only session'
      : 'Not reported by the console yet',
  );
  /** The page shown, 1..12 (a UI choice; the simulator plays every page's faders). */
  protected readonly page = signal(1);
  /** The console's own selected page when the device reports it (hardware mode, F113), else undefined. */
  protected readonly consolePage = this._state.memoryPage;

  constructor() {
    // The real console tells which page its memory faders play: the grid follows it, and a page
    // key still lets the user look at another page until the console's page changes again.
    effect(() => {
      const page = this.consolePage();
      if (page !== undefined) {
        this.page.set(page);
      }
    });
  }

  protected readonly pages = computed(() =>
    Array.from({ length: this._state.capabilities().memoryPages }, (_, index) => index + 1),
  );

  /** Whether each page (index = page − 1) holds a recorded memory, for the page keys. */
  protected readonly pageHasContent = computed(() => {
    const flags = new Array<boolean>(this._state.capabilities().memoryPages).fill(false);
    for (const memory of this._state.memories()) {
      if (memory.recorded && memory.page >= 1 && memory.page <= flags.length) {
        flags[memory.page - 1] = true;
      }
    }
    return flags;
  });

  /** The memories of the current page, in fader order. */
  protected readonly memories = computed(() => {
    const page = this.page();
    return this._state.memories().filter((memory) => memory.page === page);
  });

  protected readonly hint = computed(() => {
    switch (this.mode()) {
      case 'record':
        return 'REC MEM: press a memory to record the current look into it.';
      case 'select':
        return this.loading()
          ? 'EDIT MEM: reading the memory…'
          : 'EDIT MEM: press a recorded memory to edit it on the channel faders.';
      case 'edit':
        return 'EDIT MEM: change the channel faders, then press EDIT MEM to store or CLEAR to abort.';
      case 'copy-source':
        return this.loading()
          ? 'COPY: reading the memory…'
          : 'COPY: press the memory to copy.';
      case 'copy-target': {
        const source = this.copySource();
        return `COPY: press the memory to copy memory ${source?.number ?? '?'} into.`;
      }
      default:
        return '';
    }
  });

  protected bumpLit(memory: MemoryStateDto): number {
    return memoryBumpLit(memory);
  }

  protected selectPage(page: number): void {
    if (this.pages().includes(page)) {
      this.page.set(page);
    }
  }

  protected armedFor(memory: MemoryStateDto): boolean {
    switch (this.mode()) {
      case 'record':
        return true;
      case 'select':
      case 'copy-source':
        return memory.recorded;
      case 'copy-target': {
        const source = this.copySource();
        return !(source && source.page === memory.page && source.fader === memory.fader);
      }
      default:
        return false;
    }
  }

  protected setLevel(memory: MemoryStateDto, level: number): void {
    this._channels.setMemoryLevel(memory.page, memory.fader, level);
  }

  protected bump(memory: MemoryStateDto, pressed: boolean): void {
    this._channels.bumpMemory(memory.page, memory.fader, pressed);
  }

  protected pick(memory: MemoryStateDto): void {
    void this._edit.pickMemory(memory.page, memory.fader, memory.recorded);
  }

  protected moveFocus(fader: number, direction: -1 | 1): void {
    this._faders()[fader - 1 + direction]?.focus();
  }
}
