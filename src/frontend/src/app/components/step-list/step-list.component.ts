import { afterNextRender, ChangeDetectionStrategy, Component, ElementRef, inject, Injector, input, output } from '@angular/core';
import type { StepDto } from '../../core/show-file.models';
import { channelPairsText, labelDisplay, stepKind, type StepHome, type StepKind } from '../../core/show-rules';
import { ShowStore } from '../../core/show.store';

/** A step's time field as the row and the detail panel edit it. */
export type TimeField = 'upTime' | 'upDelay' | 'downTime' | 'downDelay' | 'followOn';

export const TIME_FIELDS: { key: TimeField; label: string }[] = [
  { key: 'upTime', label: 'Up' },
  { key: 'upDelay', label: 'Up delay' },
  { key: 'downTime', label: 'Down' },
  { key: 'downDelay', label: 'Down delay' },
  { key: 'followOn', label: 'Follow' },
];

/**
 * The step list of the STACK / SEQ page: one row per step with its number, its content (the memory's number and
 * label, a channel step's channels, a State step's label) and the five times in narrow columns, the times editable
 * in the row (Enter or Tab applies, Esc restores). Selecting a row (click, or the arrow keys) is reported to the
 * page, which shows the step's detail panel. Edits go to the `ShowStore`; nothing reaches a console.
 */
@Component({
  selector: 'sf-step-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './step-list.component.html',
  styleUrl: './step-list.component.scss',
})
export class StepListComponent {
  private readonly _store = inject(ShowStore);
  private readonly _host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly _injector = inject(Injector);

  readonly home = input.required<StepHome>();
  readonly steps = input.required<StepDto[]>();
  readonly selected = input<number | undefined>(undefined);
  readonly select = output<number>();

  protected readonly timeFields = TIME_FIELDS;

  /** The badge of the content column: the memory's number, or the step's kind. */
  protected kindLabel(step: StepDto): string {
    switch (stepKind(step)) {
      case 'memory':
        return `MEM ${step.memoryNumber}`;
      case 'channel':
        return 'CHANNEL';
      case 'state':
        return 'STATE';
      default:
        return 'STEP';
    }
  }

  /** The content column's text: the memory's label, a channel step's channels in the files' form, a State step's label. */
  protected content(step: StepDto): string {
    switch (stepKind(step)) {
      case 'memory': {
        const memory = this._store.show()?.memories.find((m) => m.number === step.memoryNumber);
        return memory ? labelDisplay(memory.label) : 'not recorded in this show';
      }
      case 'state':
        return labelDisplay(step.label);
      default:
        return channelPairsText(step.channels);
    }
  }

  protected kindOf(step: StepDto): StepKind {
    return stepKind(step);
  }

  /** The one row in the tab order: the selected one, else the first. */
  protected tabIndex(index: number): number {
    return index === (this.selected() ?? 0) ? 0 : -1;
  }

  protected onRowClick(index: number): void {
    if (this.selected() !== index) {
      this.select.emit(index);
    }
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.target instanceof HTMLInputElement || this.steps().length === 0) {
      return;
    }
    const current = this.selected() ?? 0;
    const last = this.steps().length - 1;
    let next: number;
    switch (event.key) {
      case 'ArrowDown':
        next = Math.min(last, current + 1);
        break;
      case 'ArrowUp':
        next = Math.max(0, current - 1);
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = last;
        break;
      case 'Enter':
      case ' ':
        next = current;
        break;
      default:
        return;
    }
    event.preventDefault();
    if (next !== this.selected()) {
      this.select.emit(next);
    }
    this.focusRow(next);
  }

  /** A time cell: accepted when it looks like the console's times, else the input is restored. */
  protected setTime(index: number, field: TimeField, event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!this._store.setStep(this.home(), index, { [field]: input.value.trim() })) {
      input.value = this.steps()[index]?.[field] ?? '';
    }
  }

  protected onTimeKey(index: number, field: TimeField, event: KeyboardEvent): void {
    const input = event.target as HTMLInputElement;
    if (event.key === 'Enter') {
      event.preventDefault();
      input.blur();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      input.value = this.steps()[index]?.[field] ?? '';
      input.blur();
    }
  }

  private focusRow(index: number): void {
    afterNextRender(
      () => this._host.nativeElement.querySelector<HTMLElement>(`[data-index="${index}"]`)?.focus(),
      { injector: this._injector },
    );
  }
}
