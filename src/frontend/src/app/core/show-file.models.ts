/**
 * A parsed `.asc` show file as the backend's `POST /api/showfile/parse` returns it (docs/api.md). Every
 * field carries the status docs/formats/asc-format.md gives it: numbers and times are the file's own
 * text, the meaning of most fields is inferred, and nothing here reaches a console.
 */
export interface LabelDto {
  text: string;
  line1: string;
  line2: string;
  line3: string;
}

export interface ChannelPercentDto {
  channel: number;
  percent: number;
}

export interface StepDto {
  number: string;
  memoryPage: number | null;
  memoryFader: number | null;
  memoryNumber: number | null;
  cueParams: number | null;
  upTime: string;
  upDelay: string;
  downTime: string;
  downDelay: string;
  followOn: string;
  channels: ChannelPercentDto[];
  label: LabelDto | null;
}

export interface SequenceDto {
  index: number;
  b: number;
  c: number;
  d: number;
  e: number;
  isMainStack: boolean;
  label: LabelDto | null;
  steps: StepDto[];
}

export interface MemoryFileDto {
  number: number;
  page: number;
  fader: number;
  channels: ChannelPercentDto[];
  label: LabelDto;
}

export interface PatchEntryDto {
  dmx: number;
  channel: number;
  /** The proportional level in percent as the file writes it; null for the console's own show (SC-4), whose Patch carries no level (F70). */
  level: number | null;
}

export interface IndependentDto {
  number: number;
  a: number;
  b: number;
  c: number;
  d: number;
  label: LabelDto;
}

export interface LabelledDto {
  number: number;
  label: LabelDto;
}

export interface SettingDto {
  key: string;
  value: string;
}

export interface FileWarningDto {
  line: number;
  message: string;
}

export interface ShowFileDto {
  name: string;
  bytes: number;
  lines: number;
  lineEnding: 'lf' | 'crlf';
  manufacturer: string;
  console: string;
  softwareVersion: string;
  ident: string;
  clearAll: boolean;
  mainStack: SequenceDto | null;
  sequences: SequenceDto[];
  memories: MemoryFileDto[];
  patch: PatchEntryDto[];
  independents: IndependentDto[];
  channels: LabelledDto[];
  pages: LabelledDto[];
  settings: SettingDto[];
  unknownLines: number;
  warnings: FileWarningDto[];
}
