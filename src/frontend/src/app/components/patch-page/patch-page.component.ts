import { afterNextRender, ChangeDetectionStrategy, Component, computed, ElementRef, HostListener, inject, Injector, signal } from '@angular/core';
import type { PatchEntryDto } from '../../core/show-file.models';
import { CHANNEL_COUNT, channelHue, DMX_COUNT, INDEPENDENT_CHANNELS, parsePatchTarget, patchTargetLabel } from '../../core/show-rules';
import { ShowStore } from '../../core/show.store';

/** The matrix: 32 DMX addresses per row, 16 rows. */
export const PATCH_COLUMNS = 32;
/** The channels a box may be patched to: 1..48, or the independents IND 1 / IND 2 (channels 49 and 50). */
export const TARGET_HINT = 'type a channel 1-48, i1 / i2 for IND 1 / IND 2, or 0 to unpatch';

/**
 * The PATCH page: the show's 512 DMX addresses as a matrix of boxes (the address on top, the channel below;
 * IND 1 / IND 2 for channels 49 and 50), coloured by channel. A click opens an inline input in the box (Enter
 * applies, Esc cancels, Delete or 0 unpatches); a drag over a run of boxes selects a range for the toolbar's
 * "Patch range" / "Unpatch range"; the arrow keys move between boxes, Shift extends. The proportional level and
 * the channel's label are in the detail line of the focused box. Everything is an edit of the `ShowStore`'s
 * working copy; nothing reaches a console.
 */
@Component({
  selector: 'sf-patch-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patch-page.component.html',
  styleUrl: './patch-page.component.scss',
})
export class PatchPageComponent {
  private readonly _store = inject(ShowStore);
  private readonly _host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly _injector = inject(Injector);

  protected readonly show = this._store.show;
  /** The 512 entries in rows of 32. */
  protected readonly rows = computed(() => {
    const patch = this.show()?.patch ?? [];
    const rows: PatchEntryDto[][] = [];
    for (let start = 0; start < patch.length; start += PATCH_COLUMNS) {
      rows.push(patch.slice(start, start + PATCH_COLUMNS));
    }
    return rows;
  });

  private readonly _anchor = signal<number | undefined>(undefined);
  private readonly _focus = signal<number | undefined>(undefined);
  private readonly _editing = signal<number | undefined>(undefined);
  private readonly _editText = signal('');
  private readonly _target = signal('');
  private readonly _notice = signal<string | undefined>(undefined);
  private _dragging = false;
  private _dragMoved = false;

  /** The box the keyboard is on (its DMX address). */
  protected readonly focus = this._focus.asReadonly();
  /** The box with the inline input open. */
  protected readonly editing = this._editing.asReadonly();
  protected readonly editText = this._editText.asReadonly();
  /** The toolbar's target channel, as typed. */
  protected readonly target = this._target.asReadonly();
  protected readonly notice = this._notice.asReadonly();
  /** The selected run of addresses, from the anchor to the focused box. */
  protected readonly range = computed(() => {
    const anchor = this._anchor();
    const focus = this._focus();
    return anchor === undefined || focus === undefined ? undefined : { low: Math.min(anchor, focus), high: Math.max(anchor, focus) };
  });
  protected readonly rangeSize = computed(() => {
    const range = this.range();
    return range ? range.high - range.low + 1 : 0;
  });
  protected readonly focusedEntry = computed(() => {
    const focus = this._focus();
    return focus === undefined ? undefined : this.show()?.patch.find((entry) => entry.dmx === focus);
  });
  /** The label of the focused box's channel (channels 1..48 only; the independents' labels are not edited here). */
  protected readonly focusedChannelLabel = computed(() => {
    const channel = this.focusedEntry()?.channel ?? 0;
    return channel >= 1 && channel <= CHANNEL_COUNT ? this.show()?.channels.find((c) => c.number === channel)?.label : undefined;
  });
  protected readonly summary = computed(() => {
    const patch = this.show()?.patch ?? [];
    const patched = patch.filter((entry) => entry.channel !== 0).length;
    const used = new Set(patch.map((entry) => entry.channel));
    const missing = Array.from({ length: CHANNEL_COUNT }, (_, i) => i + 1).filter((channel) => !used.has(channel));
    const independents = [INDEPENDENT_CHANNELS.ind1, INDEPENDENT_CHANNELS.ind2].map((channel) => ({
      name: patchTargetLabel(channel),
      addresses: patch.filter((entry) => entry.channel === channel).map((entry) => entry.dmx),
    }));
    return { patched, unpatched: patch.length - patched, missing, independents };
  });

  protected readonly hint = TARGET_HINT;
  protected readonly boxLabel = patchTargetLabel;
  protected readonly hue = channelHue;

  protected isIndependent(channel: number): boolean {
    return channel === INDEPENDENT_CHANNELS.ind1 || channel === INDEPENDENT_CHANNELS.ind2;
  }

  protected inRange(dmx: number): boolean {
    const range = this.range();
    return range !== undefined && dmx >= range.low && dmx <= range.high;
  }

  /** The one box in the tab order: the focused one, else DMX 1. */
  protected tabIndex(dmx: number): number {
    return dmx === (this._focus() ?? 1) ? 0 : -1;
  }

  protected boxTitle(entry: PatchEntryDto): string {
    // the console's own show (SC-4) carries no proportional level (F70): none is shown for it
    const level = entry.level === null ? ' (no level in the download)' : ` at ${entry.level} %`;
    return `DMX ${entry.dmx}` + (entry.channel ? ` → ${patchTargetLabel(entry.channel)}${level}` : ': unpatched');
  }

  // --- pointer: a press selects, a drag extends, a release without a move opens the inline input ---

  protected onPointerDown(event: PointerEvent): void {
    const dmx = dmxOf(event.target);
    if (dmx === undefined || event.button !== 0) {
      return;
    }
    if (this._editing() !== undefined && this._editing() !== dmx) {
      this.commitEdit();
    }
    if (event.shiftKey && this._anchor() !== undefined) {
      this._focus.set(dmx);
      this._dragMoved = true;
    } else {
      this._anchor.set(dmx);
      this._focus.set(dmx);
      this._dragMoved = false;
    }
    this._dragging = true;
  }

  protected onPointerOver(event: PointerEvent): void {
    const dmx = dmxOf(event.target);
    if (!this._dragging || dmx === undefined || dmx === this._focus()) {
      return;
    }
    this._focus.set(dmx);
    this._dragMoved = true;
  }

  protected onPointerUp(event: PointerEvent): void {
    const dmx = dmxOf(event.target);
    const wasDragging = this._dragging;
    this._dragging = false;
    if (wasDragging && !this._dragMoved && dmx !== undefined && dmx === this._focus() && this._editing() !== dmx) {
      this.startEdit(dmx);
    }
  }

  @HostListener('document:pointerup')
  @HostListener('document:pointercancel')
  protected endDrag(): void {
    this._dragging = false;
  }

  // --- keyboard on the matrix ---

  protected onKeyDown(event: KeyboardEvent): void {
    if (this._editing() !== undefined || event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }
    const focus = this._focus() ?? 1;
    const move = (to: number) => {
      const dmx = Math.min(DMX_COUNT, Math.max(1, to));
      this._focus.set(dmx);
      if (!event.shiftKey || this._anchor() === undefined) {
        this._anchor.set(dmx);
      }
      this.focusBox(dmx);
    };
    switch (event.key) {
      case 'ArrowLeft':
        move(focus - 1);
        break;
      case 'ArrowRight':
        move(focus + 1);
        break;
      case 'ArrowUp':
        move(focus - PATCH_COLUMNS);
        break;
      case 'ArrowDown':
        move(focus + PATCH_COLUMNS);
        break;
      case 'Home':
        move(focus - ((focus - 1) % PATCH_COLUMNS));
        break;
      case 'End':
        move(focus - ((focus - 1) % PATCH_COLUMNS) + PATCH_COLUMNS - 1);
        break;
      case 'Enter':
      case 'F2':
        this.startEdit(focus);
        break;
      case 'Delete':
      case 'Backspace':
        this.unpatchRange();
        break;
      case 'Escape':
        this._anchor.set(focus);
        break;
      default:
        if (/^[0-9iI]$/.test(event.key)) {
          this.startEdit(focus, event.key);
          break;
        }
        return;
    }
    event.preventDefault();
  }

  // --- the inline input ---

  protected startEdit(dmx: number, initial = ''): void {
    this._anchor.set(dmx);
    this._focus.set(dmx);
    this._editText.set(initial || patchTargetLabel(this.show()?.patch.find((entry) => entry.dmx === dmx)?.channel ?? 0));
    this._editing.set(dmx);
    afterNextRender(
      () => {
        const input = this._host.nativeElement.querySelector<HTMLInputElement>('.patch__editor');
        input?.focus();
        if (!initial) {
          input?.select();
        }
      },
      { injector: this._injector },
    );
  }

  protected onEditorInput(event: Event): void {
    this._editText.set((event.target as HTMLInputElement).value);
  }

  protected onEditorKey(event: KeyboardEvent): void {
    event.stopPropagation();
    if (event.key === 'Enter' || event.key === 'Tab') {
      event.preventDefault();
      this.commitEdit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelEdit();
    }
  }

  protected onEditorBlur(): void {
    if (this._editing() !== undefined) {
      this.commitEdit();
    }
  }

  /** Applies the typed target to the box (1-48, i1 / i2, 0 or empty = unpatched); a text that is none of these changes nothing. */
  protected commitEdit(): void {
    const dmx = this._editing();
    if (dmx === undefined) {
      return;
    }
    this._editing.set(undefined);
    const text = this._editText();
    const channel = parsePatchTarget(text);
    if (channel === undefined) {
      this._notice.set(`"${text.trim()}" is not a target: ${TARGET_HINT}.`);
    } else {
      this._notice.set(undefined);
      this._store.setPatchChannel(dmx, channel);
    }
    this.focusBox(dmx);
  }

  protected cancelEdit(): void {
    const dmx = this._editing();
    this._editing.set(undefined);
    if (dmx !== undefined) {
      this.focusBox(dmx);
    }
  }

  // --- the toolbar: a range to one channel, or unpatched ---

  protected onTargetInput(event: Event): void {
    this._target.set((event.target as HTMLInputElement).value);
  }

  protected patchRange(): void {
    const range = this.range();
    const channel = parsePatchTarget(this._target());
    if (!range) {
      this._notice.set('Select a run of boxes first (click or drag over them).');
      return;
    }
    if (channel === undefined || channel === 0) {
      this._notice.set(`"${this._target().trim()}" is not a channel: ${TARGET_HINT}.`);
      return;
    }
    this._notice.set(undefined);
    this._store.setPatchRange(range.low, range.high, channel);
  }

  protected unpatchRange(): void {
    const range = this.range();
    if (!range) {
      this._notice.set('Select a run of boxes first (click or drag over them).');
      return;
    }
    this._notice.set(undefined);
    this._store.setPatchRange(range.low, range.high, 0);
  }

  // --- the detail line of the focused box ---

  /** The proportional level of the focused address, 0..100 % (every file seen has 100); restored when refused. */
  protected setLevel(event: Event): void {
    const input = event.target as HTMLInputElement;
    const entry = this.focusedEntry();
    const level = input.value.trim() === '' ? NaN : Number(input.value);
    if (!entry || !this._store.setPatchLevel(entry.dmx, level)) {
      input.value = String(entry?.level ?? '');
    }
  }

  /** One line of the focused channel's label; restored when refused (over 6 characters, or a character the files never write). */
  protected setChannelLabel(line: 1 | 2 | 3, event: Event): void {
    const input = event.target as HTMLInputElement;
    const channel = this.focusedEntry()?.channel ?? 0;
    if (!this._store.setLabelLine({ kind: 'channel', number: channel }, line, input.value)) {
      input.value = this.focusedChannelLabel()?.[`line${line}`] ?? '';
    }
  }

  private focusBox(dmx: number): void {
    afterNextRender(
      () => this._host.nativeElement.querySelector<HTMLElement>(`[data-dmx="${dmx}"]`)?.focus(),
      { injector: this._injector },
    );
  }
}

function dmxOf(target: EventTarget | null): number | undefined {
  const box = target instanceof Element ? target.closest<HTMLElement>('[data-dmx]') : null;
  const dmx = box ? Number(box.dataset['dmx']) : NaN;
  return Number.isInteger(dmx) ? dmx : undefined;
}
