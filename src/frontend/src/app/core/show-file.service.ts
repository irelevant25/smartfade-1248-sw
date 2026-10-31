import { computed, Injectable, signal } from '@angular/core';
import type { ChannelPercentDto, LabelDto, SequenceDto, ShowFileDto, StepDto } from './show-file.models';

/** Where a step lives: the main stack, or a sequence by its position in the file's list. */
export type StepHome = 'stack' | number;

/** A time as the console's files write it: seconds with at most one decimal, or m:ss. */
export const TIME_PATTERN = /^([0-9]+([.][0-9])?|[0-9]+:[0-9]{2})$/;
const MEMORIES_PER_PAGE = 24;

/**
 * Opens a `.asc` show file chosen in the browser and holds an editable copy of it. The bytes go to the
 * backend, which parses them with the show-file library and returns the DTO (docs/api.md,
 * `POST /api/showfile/parse`); edits change only the copy here; "download" sends the copy back to
 * `POST /api/showfile/render` and saves the rendered bytes as a NEW file through the browser's
 * download. Nothing is stored on the backend, the original file is never touched, and nothing
 * reaches a console.
 */
@Injectable({ providedIn: 'root' })
export class ShowFileService {
  private readonly _file = signal<ShowFileDto | undefined>(undefined);
  private readonly _edited = signal<ShowFileDto | undefined>(undefined);
  private readonly _busy = signal(false);
  private readonly _error = signal<string | undefined>(undefined);

  /** The file as parsed, or undefined. */
  readonly file = this._file.asReadonly();
  /** The working copy the page edits; equals the parsed file until an edit is made. */
  readonly edited = this._edited.asReadonly();
  readonly busy = this._busy.asReadonly();
  readonly error = this._error.asReadonly();
  readonly dirty = computed(() => {
    const original = this._file();
    const edited = this._edited();
    return original !== undefined && edited !== undefined && JSON.stringify(original) !== JSON.stringify(edited);
  });

  async open(file: File): Promise<void> {
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const response = await fetch(`/api/showfile/parse?name=${encodeURIComponent(file.name)}`, {
        method: 'POST',
        body: file,
      });
      if (!response.ok) {
        throw new Error(await describe(response));
      }
      const dto = (await response.json()) as ShowFileDto;
      this._file.set(dto);
      this._edited.set(structuredClone(dto));
    } catch (error) {
      this._file.set(undefined);
      this._edited.set(undefined);
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }

  close(): void {
    this._file.set(undefined);
    this._edited.set(undefined);
    this._error.set(undefined);
  }

  /** Patch edit: DMX address `dmx` drives `channel` (0 = unpatched, 1..96). Out-of-range values are ignored. */
  setPatchChannel(dmx: number, channel: number): void {
    const edited = this._edited();
    if (!edited || !Number.isInteger(channel) || channel < 0 || channel > 96) {
      return;
    }
    const index = edited.patch.findIndex((entry) => entry.dmx === dmx);
    if (index < 0 || edited.patch[index].channel === channel) {
      return;
    }
    const patch = edited.patch.slice();
    patch[index] = { ...patch[index], channel };
    this._edited.set({ ...edited, patch });
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
    const sequence = home === 'stack' ? edited.mainStack : edited.sequences[home];
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
      if (typeof memoryNumber !== 'number' || !Number.isInteger(memoryNumber) || memoryNumber < 1 || memoryNumber > 12 * MEMORIES_PER_PAGE) {
        return false;
      }
      memory = memoryFields(edited, memoryNumber, home === 'stack' ? step.number : undefined);
    }
    this.replaceSteps(edited, home, sequence, withStep(sequence.steps, index, { ...step, ...times, ...memory }));
    return true;
  }

  /**
   * Inserts a step after `afterIndex` (-1 = at the front) of the stack or a sequence, as a copy of its neighbour's
   * memory and times, so that the user then changes the memory in the row. A stack cue gets the next free number
   * (one decimal, below 200) between its neighbours; a sequence step gets its memory's number. Returns false when
   * there is nothing to copy, no free cue number, or the stack is full.
   */
  insertStep(home: StepHome, afterIndex: number): boolean {
    const edited = this._edited();
    const sequence = home === 'stack' ? edited?.mainStack : edited?.sequences[home];
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
    const sequence = home === 'stack' ? edited?.mainStack : edited?.sequences[home];
    if (!edited || !sequence || index < 0 || index >= sequence.steps.length) {
      return false;
    }
    const steps = sequence.steps.slice();
    steps.splice(index, 1);
    this.replaceSteps(edited, home, sequence, steps);
    return true;
  }

  private replaceSteps(edited: ShowFileDto, home: StepHome, sequence: SequenceDto, steps: StepDto[]): void {
    const next: SequenceDto = { ...sequence, steps };
    this._edited.set(
      home === 'stack'
        ? { ...edited, mainStack: next }
        : { ...edited, sequences: edited.sequences.map((s, i) => (i === home ? next : s)) },
    );
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
    if (!edited || value.length > LABEL_LINE_LENGTH || !LABEL_LINE_PATTERN.test(value)) {
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
        const next = { ...sequence, label: update(sequence.label ?? { text: '', line1: '', line2: '', line3: '' }) };
        this._edited.set({ ...edited, sequences: edited.sequences.map((s, i) => (i === target.index ? next : s)) });
        return true;
      }
    }
  }

  revert(): void {
    const original = this._file();
    this._edited.set(original ? structuredClone(original) : undefined);
  }

  /** Renders the working copy on the backend and hands the bytes to the browser as a new file. */
  async download(): Promise<void> {
    const edited = this._edited();
    if (!edited) {
      return;
    }
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const response = await fetch('/api/showfile/render', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(edited),
      });
      if (!response.ok) {
        throw new Error(await describe(response));
      }
      const name = fileNameOf(response.headers.get('Content-Disposition')) ?? editedName(edited.name);
      saveBlob(await response.blob(), name);
    } catch (error) {
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }
}

async function describe(response: Response): Promise<string> {
  const problem = (await response.json().catch(() => undefined)) as { message?: string } | undefined;
  return problem?.message ?? `The backend answered ${response.status}.`;
}

function fileNameOf(contentDisposition: string | null): string | undefined {
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match ? decodeURIComponent(match[1]) : undefined;
}

export function editedName(original: string): string {
  const dot = original.lastIndexOf('.');
  return dot > 0 ? `${original.slice(0, dot)}-edited${original.slice(dot)}` : `${original}-edited.asc`;
}

function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

/**
 * The fields a memory reference sets on a step: page and fader (0-based, 24 per page), the number, $$CUEPARAMS 1,
 * the memory's channel list copied (or none when the memory is not in the file), and, for a sequence step
 * (no `stackNumber`), the step number = the memory number as the console's files write it (`265.0`).
 */
function memoryFields(show: ShowFileDto, memoryNumber: number, stackNumber: string | undefined): Partial<StepDto> {
  const memory = show.memories.find((m) => m.number === memoryNumber);
  return {
    memoryPage: Math.floor((memoryNumber - 1) / MEMORIES_PER_PAGE),
    memoryFader: (memoryNumber - 1) % MEMORIES_PER_PAGE,
    memoryNumber,
    cueParams: 1,
    channels: memory ? memory.channels.map((c) => ({ ...c })) : [],
    number: stackNumber ?? `${memoryNumber}.0`,
  };
}

function withStep(steps: StepDto[], index: number, step: StepDto): StepDto[] {
  const copy = steps.slice();
  copy[index] = step;
  return copy;
}

/**
 * The next free stack cue number between two neighbours, in the console's form (one decimal, `1.0`..`199.9`,
 * rising through the stack in every file): after the last cue the next whole number, between two cues the
 * previous plus a tenth when that is still below the next; undefined when no number fits.
 */
export function nextCueNumber(previous: string | undefined, next: string | undefined): string | undefined {
  const tenths = (text: string | undefined) => (text === undefined ? undefined : Math.round(Number(text) * 10));
  const prev = tenths(previous);
  const after = tenths(next);
  if (prev !== undefined && Number.isNaN(prev)) {
    return undefined;
  }
  let candidate: number;
  if (prev === undefined) {
    candidate = after === undefined ? 10 : Math.min(10, after - 1);
  } else if (after === undefined) {
    candidate = (Math.floor(prev / 10) + 1) * 10;
  } else {
    candidate = prev + 1;
  }
  if (candidate < 1 || candidate > 1999 || (after !== undefined && candidate >= after)) {
    return undefined;
  }
  return `${Math.floor(candidate / 10)}.${candidate % 10}`;
}

/** Where a label lives: a memory or a channel by its number, or a sequence by its position in the file's list. */
export type LabelTarget = { kind: 'memory'; number: number } | { kind: 'channel'; number: number } | { kind: 'sequence'; index: number };

/** The highest channel a memory of the console's files names (4 860 of 4 860 pairs in 34 files). */
export const MAX_MEMORY_CHANNEL = 48;
/** A label line as the console writes it: at most 6 characters. */
export const LABEL_LINE_LENGTH = 6;
/** Printable ASCII without '&' (the console writes other characters as numeric entities, never produced here). */
export const LABEL_LINE_PATTERN = /^[ -%'-~]*$/;

/**
 * The `Text` line of a label block as the console derives it from the three lines (4 282 of 4 282 blocks in 34 files):
 * line 1, then line 2 only when line 1 is full (6 characters), then line 3 only when line 2 is full too.
 */
export function labelText(line1: string, line2: string, line3: string): string {
  const text =
    labelLength(line1) < LABEL_LINE_LENGTH
      ? line1
      : labelLength(line2) < LABEL_LINE_LENGTH
        ? line1 + line2
        : line1 + line2 + line3;
  // an HTML numeric entity (the console's form of a character outside ASCII, `&#268;` = Č) is one character and
  // becomes '?' in the derived line (SHOW15.ASC, E12b, observed once)
  return text.replace(ENTITY_PATTERN, '?');
}

const ENTITY_PATTERN = /&#[0-9]+;/g;

/** Characters as the console counts them: an entity is one. */
export function labelLength(line: string): number {
  return line.replace(ENTITY_PATTERN, 'x').length;
}

/**
 * Channel pairs as the files write them, `7/100 15/8`: channel and percent, separated by spaces; an empty text is an
 * empty list. Undefined when the text is not of that form (the ranges are checked by the service).
 */
export function parseChannelPairs(text: string): ChannelPercentDto[] | undefined {
  const parts = text.trim().split(/\s+/).filter((p) => p.length > 0);
  const pairs: ChannelPercentDto[] = [];
  for (const part of parts) {
    const match = /^([0-9]{1,2})\/([0-9]{1,3})$/.exec(part);
    if (!match) {
      return undefined;
    }
    pairs.push({ channel: Number(match[1]), percent: Number(match[2]) });
  }
  return pairs;
}
