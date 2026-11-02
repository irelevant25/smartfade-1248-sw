import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { StepDto } from '../../core/show-file.models';
import { channelPairsText, labelDisplay, parseChannelPairs, stepKind, type StepHome } from '../../core/show-rules';
import { ShowStore } from '../../core/show.store';
import { StepListComponent, TIME_FIELDS, type TimeField } from '../step-list/step-list.component';

/** One tab: the stack, or a sequence by its position in the show's list. */
export interface StepTab {
  home: StepHome;
  label: string;
  count: number;
}

export const NO_CUE_NUMBER_NOTICE = 'No step was added: no free cue number fits there (one decimal, below 200).';

/**
 * The STACK / SEQ page: a tab for the stack and one per sequence (its label, or its number), the step list of the
 * chosen one, and, beside it, the detail panel of the selected step: the referenced memory with its label and
 * channels (editable, every step that uses the memory follows), a channel step's channels, a State step's label,
 * the five times in the forms the files use, and the insert-after / remove actions with the numbering rules of
 * docs/formats/asc-format.md. A sequence's label is edited above its list. Everything is an edit of the
 * `ShowStore`'s working copy; nothing reaches a console.
 */
@Component({
  selector: 'sf-stack-page',
  imports: [StepListComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stack-page.component.html',
  styleUrl: './stack-page.component.scss',
})
export class StackPageComponent {
  private readonly _store = inject(ShowStore);

  protected readonly show = this._store.show;
  protected readonly tabs = computed<StepTab[]>(() => {
    const show = this.show();
    if (!show) {
      return [];
    }
    const tabs: StepTab[] = [];
    if (show.mainStack) {
      tabs.push({ home: 'stack', label: 'Stack', count: show.mainStack.steps.length });
    }
    show.sequences.forEach((sequence, index) => {
      tabs.push({ home: index, label: labelDisplay(sequence.label) || `Sequence ${sequence.index}`, count: sequence.steps.length });
    });
    return tabs;
  });

  private readonly _home = signal<StepHome>('stack');
  private readonly _selected = signal<number | undefined>(undefined);
  private readonly _notice = signal<string | undefined>(undefined);

  /** The chosen tab; the first one when the chosen home is not in this show. */
  protected readonly home = computed<StepHome | undefined>(() => {
    const tabs = this.tabs();
    const wanted = this._home();
    return tabs.some((tab) => tab.home === wanted) ? wanted : tabs[0]?.home;
  });
  protected readonly steps = computed<StepDto[]>(() => {
    const home = this.home();
    return home === undefined ? [] : (this._store.stepsOf(home) ?? []);
  });
  /** The selected step's index, when it still exists. */
  protected readonly selected = computed(() => {
    const index = this._selected();
    return index !== undefined && index < this.steps().length ? index : undefined;
  });
  protected readonly step = computed(() => {
    const index = this.selected();
    return index === undefined ? undefined : this.steps()[index];
  });
  /** The sequence of the chosen tab (undefined for the stack). */
  protected readonly sequence = computed(() => {
    const home = this.home();
    return typeof home === 'number' ? this.show()?.sequences[home] : undefined;
  });
  /** The memory the selected step references, when it is recorded in the show. */
  protected readonly memory = computed(() => {
    const number = this.step()?.memoryNumber;
    return number === null || number === undefined ? undefined : this.show()?.memories.find((m) => m.number === number);
  });
  protected readonly notice = this._notice.asReadonly();

  protected readonly timeFields = TIME_FIELDS;
  protected readonly kind = stepKind;
  protected readonly channelsText = channelPairsText;
  protected readonly labelDisplay = labelDisplay;

  protected kindLabel(step: StepDto): string {
    switch (stepKind(step)) {
      case 'memory':
        return 'memory step';
      case 'channel':
        return 'channel step';
      case 'state':
        return 'state step';
      default:
        return 'step';
    }
  }

  protected showTab(home: StepHome): void {
    this._home.set(home);
    this._selected.set(undefined);
    this._notice.set(undefined);
  }

  protected selectStep(index: number): void {
    this._selected.set(index);
    this._notice.set(undefined);
  }

  /** One line of the chosen sequence's label; restored when refused. */
  protected setSequenceLabel(line: 1 | 2 | 3, event: Event): void {
    const input = event.target as HTMLInputElement;
    const home = this.home();
    if (typeof home !== 'number' || !this._store.setLabelLine({ kind: 'sequence', index: home }, line, input.value)) {
      input.value = this.sequence()?.label?.[`line${line}`] ?? '';
    }
  }

  /** The memory of the selected step, 1..288 (a memory step always has one); restored when refused. */
  protected setMemory(event: Event): void {
    const input = event.target as HTMLInputElement;
    const home = this.home();
    const index = this.selected();
    const text = input.value.trim();
    if (home === undefined || index === undefined || !this._store.setStep(home, index, { memoryNumber: text === '' ? null : Number(text) })) {
      input.value = String(this.step()?.memoryNumber ?? '');
    }
  }

  /** A time of the selected step in the detail panel; restored when refused. */
  protected setTime(field: TimeField, event: Event): void {
    const input = event.target as HTMLInputElement;
    const home = this.home();
    const index = this.selected();
    if (home === undefined || index === undefined || !this._store.setStep(home, index, { [field]: input.value.trim() })) {
      input.value = this.step()?.[field] ?? '';
    }
  }

  /** One line of the referenced memory's label; restored when refused. */
  protected setMemoryLabel(line: 1 | 2 | 3, event: Event): void {
    const input = event.target as HTMLInputElement;
    const memory = this.memory();
    if (!memory || !this._store.setLabelLine({ kind: 'memory', number: memory.number }, line, input.value)) {
      input.value = memory?.label[`line${line}`] ?? '';
    }
  }

  /** The referenced memory's channels in the files' form (`7/100 15/8`); the input shows the stored form afterwards. */
  protected setMemoryChannels(event: Event): void {
    const input = event.target as HTMLInputElement;
    const memory = this.memory();
    const pairs = parseChannelPairs(input.value);
    if (memory && pairs) {
      this._store.setMemoryChannels(memory.number, pairs);
    }
    input.value = channelPairsText(this.memory()?.channels ?? []);
  }

  /** Inserts a step after the selected one (a copy of its memory and times) and selects it. */
  protected insertAfter(): void {
    const home = this.home();
    const index = this.selected();
    if (home === undefined || index === undefined) {
      return;
    }
    if (this._store.insertStep(home, index)) {
      this._selected.set(index + 1);
      this._notice.set(undefined);
    } else {
      this._notice.set(NO_CUE_NUMBER_NOTICE);
    }
  }

  protected addFirst(): void {
    const home = this.home();
    if (home !== undefined && this._store.insertStep(home, -1)) {
      this._selected.set(0);
      this._notice.set(undefined);
    } else {
      this._notice.set(NO_CUE_NUMBER_NOTICE);
    }
  }

  /** Removes the selected step (the others keep their numbers) and selects the one now at its place. */
  protected remove(): void {
    const home = this.home();
    const index = this.selected();
    if (home === undefined || index === undefined || !this._store.removeStep(home, index)) {
      return;
    }
    const remaining = this.steps().length;
    this._selected.set(remaining === 0 ? undefined : Math.min(index, remaining - 1));
    this._notice.set(undefined);
  }
}
