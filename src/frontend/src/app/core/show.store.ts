import { computed, Injectable, signal } from '@angular/core';
import type { ChannelPercentDto, LabelDto, SequenceDto, ShowFileDto, StepDto } from './show-file.models';
import {
  EMPTY_LABEL,
  isLabelLine,
  labelText,
  MAX_MEMORY_CHANNEL,
  MAX_PATCH_CHANNEL,
  MEMORY_COUNT,
  memoryFields,
  nextCueNumber,
  TIME_PATTERN,
  type LabelTarget,
  type StepHome,
} from './show-rules';

/** Where the current show came from: a `.asc` file from the card, or (later, after its safety check) the console. */
export type ShowSource = 'file' | 'console';

/**
 * The one place the show lives in the UI: the show as loaded, the working copy the PATCH and STACK / SEQ pages
 * edit, whether it was edited, and every edit operation with the rules of docs/formats/asc-format.md ("Step rules
 * learned from the files", "Label and memory rules learned from the files"). Today the show comes from a file parsed
 * by the backend (the Show menu); `setShow(dto, name, 'console')` is the seam for the console's own show once its
 * download is cleared (SC-4). Edits change only the copy here: they leave through the Show menu's "Save to card"
 * as a new file, never over USB.
 */
@Injectable({ providedIn: 'root' })
export class ShowStore {
  private readonly _original = signal<ShowFileDto | undefined>(undefined);
  private readonly _edited = signal<ShowFileDto | undefined>(undefined);
  private readonly _name = signal<string | undefined>(undefined);
  private readonly _source = signal<ShowSource | undefined>(undefined);

  /** The show as loaded, or undefined. */
  readonly original = this._original.asReadonly();
  /** The working copy the pages show and edit; equals the loaded show until an edit is made. */
  readonly show = this._edited.asReadonly();
  /** The name the show was loaded under (the file's name). */
  readonly name = this._name.asReadonly();
  readonly source = this._source.asReadonly();
  readonly loaded = computed(() => this._edited() !== undefined);
  readonly dirty = computed(() => {
    const original = this._original();
    const edited = this._edited();
    return original !== undefined && edited !== undefined && JSON.stringify(original) !== JSON.stringify(edited);
  });

  /** Replaces the current show (edits included) with `dto`, as loaded from `source` under `name`. */
  setShow(dto: ShowFileDto, name: string, source: ShowSource): void {
    this._original.set(structuredClone(dto));
    this._edited.set(structuredClone(dto));
    this._name.set(name);
    this._source.set(source);
  }

  clear(): void {
    this._original.set(undefined);
    this._edited.set(undefined);
    this._name.set(undefined);
    this._source.set(undefined);
  }

  /** Drops every edit: the working copy becomes the show as loaded again. */
  revert(): void {
    const original = this._original();
    this._edited.set(original ? structuredClone(original) : undefined);
  }

  /** The steps of the stack or of a sequence, or undefined when the show has no such home. */
  stepsOf(home: StepHome): StepDto[] | undefined {
    return this.homeOf(this._edited(), home)?.steps;
  }

  /** Patch edit: DMX address `dmx` drives `channel` (0 = unpatched, 1..96). Returns false when refused. */
  setPatchChannel(dmx: number, channel: number): boolean {
    return this.setPatchRange(dmx, dmx, channel);
  }

  /**
   * Patch edit over a run of addresses: every DMX address from `fromDmx` to `toDmx` (either order) drives `channel`
   * (0 = unpatched, 1..96); the proportional levels are kept. Returns false, and changes nothing, when refused.
   */
  setPatchRange(fromDmx: number, toDmx: number, channel: number): boolean {
    const edited = this._edited();
    if (!edited || !Number.isInteger(channel) || channel < 0 || channel > MAX_PATCH_CHANNEL) {
      return false;
    }
    const low = Math.min(fromDmx, toDmx);
    const high = Math.max(fromDmx, toDmx);
    let changed = false;
    const patch = edited.patch.map((entry) => {
      if (entry.dmx < low || entry.dmx > high || entry.channel === channel) {
        return entry;
      }
      changed = true;
      return { ...entry, channel };
    });
    if (!patch.some((entry) => entry.dmx >= low && entry.dmx <= high)) {
      return false;
    }
    if (changed) {
      this._edited.set({ ...edited, patch });
    }
    return true;
  }

  /** Patch edit: the proportional level of one address, 0..100 % (every file seen has 100). Returns false when refused. */
  setPatchLevel(dmx: number, level: number): boolean {
    const edited = this._edited();
    if (!edited || !Number.isInteger(level) || level < 0 || level > 100) {
      return false;
    }
    const index = edited.patch.findIndex((entry) => entry.dmx === dmx);
    if (index < 0) {
      return false;
    }
    if (edited.patch[index].level !== level) {
      const patch = edited.patch.slice();
      patch[index] = { ...patch[index], level };
      this._edited.set({ ...edited, patch });
    }
    return true;
  }

  /**
   * Step edit: the fade times, the delays, the follow-on and the referenced memory of an existing step. Times
   * must look like the console's (TIME_PATTERN); the memory is 1..288 and cannot be removed (every step the
   * console's files write with $$CUEPARAMS 1 has one: 908 of 908 in 34 files). A memory step repeats its memory's
   * channel list (894 of 894 when the memory is in the file, none when it is not), so a memory change replaces
   * the channels, and a sequence step takes the memory's number as its own (383 of 383). Stack cue numbers are
   * never changed here. Returns false, and changes nothing, when a value is refused.
   */
  setStep(
    home: StepHome,
    index: number,
    change: Partial<Pick<StepDto, 'upTime' | 'upDelay' | 'downTime' | 'downDelay' | 'followOn'>> & { memoryNumber?: number | null },
  ): boolean {
    const edited = this._edited();
    if (!edited) {
      return false;
    }
    const sequence = this.homeOf(edited, home);
    const step = sequence?.steps[index];
    if (!sequence || !step) {
      return false;
    }
    const { memoryNumber, ...times } = change;
    for (const value of Object.values(times)) {
      if (typeof value !== 'string' || !TIME_PATTERN.test(value)) {
        return false;
      }
    }
    let memory: Partial<StepDto> = {};
    if ('memoryNumber' in change) {
      if (typeof memoryNumber !== 'number' || !Number.isInteger(memoryNumber) || memoryNumber < 1 || memoryNumber > MEMORY_COUNT) {
        return false;
      }
      memory = memoryFields(edited, memoryNumber, home === 'stack' ? step.number : undefined);
    }
    this.replaceSteps(edited, home, sequence, withStep(sequence.steps, index, { ...step, ...times, ...memory }));
    return true;
  }

  /**
   * Inserts a step after `afterIndex` (-1 = at the front) of the stack or a sequence, as a copy of its neighbour's
   * memory and times, so that the user then changes the memory. A stack cue gets the next free number (one decimal,
   * below 200) between its neighbours; a sequence step gets its memory's number. Returns false when there is nothing
   * to copy, no free cue number, or the stack is full.
   */
  insertStep(home: StepHome, afterIndex: number): boolean {
    const edited = this._edited();
    const sequence = this.homeOf(edited, home);
    if (!edited || !sequence || afterIndex < -1 || afterIndex >= sequence.steps.length) {
      return false;
    }
    const previous = sequence.steps[afterIndex];
    const next = sequence.steps[afterIndex + 1];
    const template = previous ?? next;
    const memoryNumber = template?.memoryNumber ?? edited.memories[0]?.number ?? 1;
    const times = template
      ? { upTime: template.upTime, upDelay: template.upDelay, downTime: template.downTime, downDelay: template.downDelay, followOn: template.followOn }
      : home === 'stack'
        ? { upTime: '5.0', upDelay: '0', downTime: '5.0', downDelay: '0', followOn: '0' }
        : { upTime: '1.0', upDelay: '0', downTime: '1.0', downDelay: '0', followOn: '0' };
    let number: string | undefined;
    if (home === 'stack') {
      number = nextCueNumber(previous?.number, next?.number);
      if (number === undefined) {
        return false;
      }
    }
    const step: StepDto = {
      number: number ?? `${memoryNumber}.0`,
      memoryPage: null,
      memoryFader: null,
      memoryNumber: null,
      cueParams: 1,
      ...times,
      channels: [],
      label: null,
      ...memoryFields(edited, memoryNumber, number),
    };
    const steps = sequence.steps.slice();
    steps.splice(afterIndex + 1, 0, step);
    this.replaceSteps(edited, home, sequence, steps);
    return true;
  }

  /** Removes a step; the other steps keep their numbers (the console's files never renumber). */
  removeStep(home: StepHome, index: number): boolean {
    const edited = this._edited();
    const sequence = this.homeOf(edited, home);
    if (!edited || !sequence || index < 0 || index >= sequence.steps.length) {
      return false;
    }
    const steps = sequence.steps.slice();
    steps.splice(index, 1);
    this.replaceSteps(edited, home, sequence, steps);
    return true;
  }

  /**
   * Memory edit: the channel list of a memory (channels 1..48 at 1..100 %, in rising order, each once, as the 496
   * memories of the console's files; a channel at 0 is left out). Every step that references the memory repeats the
   * list, so those steps are updated too (894 of 894 in the files). Returns false, and changes nothing, when refused.
   */
  setMemoryChannels(number: number, channels: ChannelPercentDto[]): boolean {
    const edited = this._edited();
    const index = edited?.memories.findIndex((m) => m.number === number) ?? -1;
    if (!edited || index < 0) {
      return false;
    }
    const sorted = channels.map((c) => ({ ...c })).sort((a, b) => a.channel - b.channel);
    for (let i = 0; i < sorted.length; i++) {
      const { channel, percent } = sorted[i];
      if (!Number.isInteger(channel) || channel < 1 || channel > MAX_MEMORY_CHANNEL || !Number.isInteger(percent) || percent < 1 || percent > 100) {
        return false;
      }
      if (i > 0 && sorted[i - 1].channel === channel) {
        return false;
      }
    }
    const memories = edited.memories.slice();
    memories[index] = { ...memories[index], channels: sorted };
    const repeat = (sequence: SequenceDto): SequenceDto => ({
      ...sequence,
      steps: sequence.steps.map((step) => (step.memoryNumber === number ? { ...step, channels: sorted.map((c) => ({ ...c })) } : step)),
    });
    this._edited.set({
      ...edited,
      memories,
      mainStack: edited.mainStack ? repeat(edited.mainStack) : null,
      sequences: edited.sequences.map(repeat),
    });
    return true;
  }

  /**
   * Label edit: one of the three lines of a memory, channel or sequence label, at most 6 printable ASCII characters
   * (no '&': the console writes other characters as numeric entities, which this editor never produces). The `text`
   * line follows from the three lines as the console derives it (4 282 of 4 282 blocks). Returns false when refused.
   */
  setLabelLine(target: LabelTarget, line: 1 | 2 | 3, value: string): boolean {
    const edited = this._edited();
    if (!edited || !isLabelLine(value)) {
      return false;
    }
    const update = (label: LabelDto): LabelDto => {
      const lines = { line1: label.line1, line2: label.line2, line3: label.line3, [`line${line}`]: value };
      return { ...lines, text: labelText(lines.line1, lines.line2, lines.line3) };
    };
    switch (target.kind) {
      case 'memory': {
        const index = edited.memories.findIndex((m) => m.number === target.number);
        if (index < 0) {
          return false;
        }
        const memories = edited.memories.slice();
        memories[index] = { ...memories[index], label: update(memories[index].label) };
        this._edited.set({ ...edited, memories });
        return true;
      }
      case 'channel': {
        const index = edited.channels.findIndex((c) => c.number === target.number);
        if (index < 0) {
          return false;
        }
        const channels = edited.channels.slice();
        channels[index] = { ...channels[index], label: update(channels[index].label) };
        this._edited.set({ ...edited, channels });
        return true;
      }
      case 'sequence': {
        const sequence = edited.sequences[target.index];
        if (!sequence) {
          return false;
        }
        const next = { ...sequence, label: update(sequence.label ?? EMPTY_LABEL) };
        this._edited.set({ ...edited, sequences: edited.sequences.map((s, i) => (i === target.index ? next : s)) });
        return true;
      }
    }
  }

  /** The label of a memory, channel or sequence as the working copy holds it. */
  labelOf(target: LabelTarget): LabelDto | null | undefined {
    const show = this._edited();
    switch (target.kind) {
      case 'memory':
        return show?.memories.find((m) => m.number === target.number)?.label;
      case 'channel':
        return show?.channels.find((c) => c.number === target.number)?.label;
      case 'sequence':
        return show?.sequences[target.index]?.label;
    }
  }

  private homeOf(show: ShowFileDto | undefined, home: StepHome): SequenceDto | null | undefined {
    return home === 'stack' ? show?.mainStack : show?.sequences[home];
  }

  private replaceSteps(edited: ShowFileDto, home: StepHome, sequence: SequenceDto, steps: StepDto[]): void {
    const next: SequenceDto = { ...sequence, steps };
    this._edited.set(
      home === 'stack'
        ? { ...edited, mainStack: next }
        : { ...edited, sequences: edited.sequences.map((s, i) => (i === home ? next : s)) },
    );
  }
}

function withStep(steps: StepDto[], index: number, step: StepDto): StepDto[] {
  const copy = steps.slice();
  copy[index] = step;
  return copy;
}
