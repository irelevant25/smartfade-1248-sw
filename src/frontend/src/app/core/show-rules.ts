import type { ChannelPercentDto, LabelDto, ShowFileDto, StepDto } from './show-file.models';

/**
 * The shapes a show must keep to stay inside what the console's own files were seen to write
 * (docs/formats/asc-format.md, "Step rules learned from the files" and "Label and memory rules learned
 * from the files", OBSERVED on 34 console-written files). Pure functions; the store applies them, the
 * backend's `ShowFileRules` repeats them before a file is rendered. Nothing here reaches a console.
 */

/** Where a step lives: the main stack, or a sequence by its position in the show's list. */
export type StepHome = 'stack' | number;

/** Where a label lives: a memory or a channel by its number, or a sequence by its position in the show's list. */
export type LabelTarget = { kind: 'memory'; number: number } | { kind: 'channel'; number: number } | { kind: 'sequence'; index: number };

/** A time as the console's files write it: seconds with at most one decimal, or m:ss. */
export const TIME_PATTERN = /^([0-9]+([.][0-9])?|[0-9]+:[0-9]{2})$/;
export const MEMORIES_PER_PAGE = 24;
export const MEMORY_COUNT = 12 * MEMORIES_PER_PAGE;
/** The highest channel a memory of the console's files names (4 860 of 4 860 pairs in 34 files). */
export const MAX_MEMORY_CHANNEL = 48;
/** A label line as the console writes it: at most 6 characters. */
export const LABEL_LINE_LENGTH = 6;
/** Printable ASCII without '&' (the console writes other characters as numeric entities, never produced here). */
export const LABEL_LINE_PATTERN = /^[ -%'-~]*$/;

/** The console's 48 channels; a patch entry may also name the independents (below) or, in a file, up to 96. */
export const CHANNEL_COUNT = 48;
/** IND 1 and IND 2 are channels 49 and 50 in the console's patch (E8b, VERIFIED: IND 2 = channel 50 at DMX 47). */
export const INDEPENDENT_CHANNELS = { ind1: 49, ind2: 50 } as const;
/** The highest channel number a patch entry carries (the backend accepts 0..96). */
export const MAX_PATCH_CHANNEL = 96;
export const DMX_COUNT = 512;

/** What a step is, as the model distinguishes it ($$CUEPARAMS: 1 = memory step, 0 = channel step, 2 = State step; H12). */
export type StepKind = 'memory' | 'channel' | 'state' | 'other';

export function stepKind(step: StepDto): StepKind {
  if (step.memoryNumber !== null) {
    return 'memory';
  }
  switch (step.cueParams) {
    case 2:
      return 'state';
    case 0:
      return 'channel';
    default:
      return 'other';
  }
}

/**
 * The fields a memory reference sets on a step: page and fader (0-based, 24 per page), the number, $$CUEPARAMS 1,
 * the memory's channel list copied (or none when the memory is not in the show), and, for a sequence step
 * (no `stackNumber`), the step number = the memory number as the console's files write it (`265.0`).
 */
export function memoryFields(show: ShowFileDto, memoryNumber: number, stackNumber: string | undefined): Partial<StepDto> {
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

/** A label line is accepted when it is at most 6 printable ASCII characters without '&'. */
export function isLabelLine(value: string): boolean {
  return value.length <= LABEL_LINE_LENGTH && LABEL_LINE_PATTERN.test(value);
}

/** The lines of a label joined for display (`OPONA / 2`), or its `Text` line when the lines are empty. */
export function labelDisplay(label: LabelDto | null | undefined): string {
  if (!label) {
    return '';
  }
  const lines = [label.line1, label.line2, label.line3].filter((line) => line.trim().length > 0);
  return lines.length > 0 ? lines.join(' / ') : label.text;
}

export const EMPTY_LABEL: LabelDto = { text: '', line1: '', line2: '', line3: '' };

/**
 * Channel pairs as the files write them, `7/100 15/8`: channel and percent, separated by spaces; an empty text is an
 * empty list. Undefined when the text is not of that form (the ranges are checked by the store).
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

/** Channel pairs in the files' own form, `7/100 15/8`. */
export function channelPairsText(channels: ChannelPercentDto[]): string {
  return channels.map((c) => `${c.channel}/${c.percent}`).join(' ');
}

/**
 * A patch target as typed in the matrix: a channel 1..48, "i1" / "i2" / "IND 1" / "IND 2" for the independents
 * (channels 49 and 50), an empty text or 0 to unpatch. Undefined for anything else.
 */
export function parsePatchTarget(text: string): number | undefined {
  const value = text.trim().toLowerCase().replace(/\s+/g, '');
  if (value === '' || value === '0') {
    return 0;
  }
  const independent = /^(?:i|ind)([12])$/.exec(value);
  if (independent) {
    return independent[1] === '1' ? INDEPENDENT_CHANNELS.ind1 : INDEPENDENT_CHANNELS.ind2;
  }
  if (!/^[0-9]{1,2}$/.test(value)) {
    return undefined;
  }
  const channel = Number(value);
  return channel >= 1 && channel <= CHANNEL_COUNT ? channel : undefined;
}

/** What a patch box shows for its channel: nothing when unpatched, "IND 1" / "IND 2" for the independents. */
export function patchTargetLabel(channel: number): string {
  switch (channel) {
    case 0:
      return '';
    case INDEPENDENT_CHANNELS.ind1:
      return 'IND 1';
    case INDEPENDENT_CHANNELS.ind2:
      return 'IND 2';
    default:
      return String(channel);
  }
}

/** One hue per channel (the golden angle keeps neighbours apart); the independents sit far from the channels' hues. */
export function channelHue(channel: number): number {
  return Math.round((channel * 137.508) % 360);
}
