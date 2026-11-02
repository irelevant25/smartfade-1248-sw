/**
 * Domain types of the console API (docs/api.md, Phase 1 contract).
 *
 * The UI speaks domain concepts only: channels, levels, masters, memories, connection state.
 * Levels travel as the console's own unit, 0-255, and are rendered as percent (see level.ts).
 */

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

/**
 * 'hardware' is the real console over USB, read-only (safety check SC-1); the mode is always
 * shown in the UI, hardware mode loudly.
 */
export type DeviceMode = 'simulated' | 'replay' | 'hardware';

/*
 * Optional values (marked `?`) may arrive as JSON null instead of being left out: both mean
 * "not known" and the UI treats them the same.
 */

export interface ConnectionDto {
  status: ConnectionStatus;
  mode: DeviceMode;
  deviceDescription: string;
  error?: string | null;
}

/** Console limits as documented in docs/api.md. */
export interface CapabilitiesDto {
  channels: 48;
  physicalFaders: 24;
  memoryPages: 12;
  memoriesPerPage: 24;
  sequences: 48;
  stackSteps: 199;
  dmxAddresses: 512;
  independents: 2;
}

export interface ChannelStateDto {
  /** 1..48 */
  channel: number;
  /** 0..255, what the user asked for. */
  desired: number;
  /** 0..255, what the console (or simulator) reports the fader at. */
  actual: number;
  /** desired differs from actual and a command is in flight. */
  pending: boolean;
  /**
   * 0..255, what the console says the channel is doing after HTP with memories, bumps and the
   * MASTER. It lights the bump button, as on the console, and is never compared with `desired`.
   */
  output: number;
}

export interface MastersDto {
  /** 0..255, the MASTER fader. */
  master: number;
  /** 0..255, the BUMPS fader. */
  bumps: number;
  blackout: boolean;
  /** SOLO button: off = bumps pile on, on = a bump replaces the output. */
  solo: boolean;
  /** IND 1 / IND 2 toggles: two outputs separated from all other controls (F108). */
  independent1: boolean;
  independent2: boolean;
}

/** One memory, addressed as on the console: page 1..12, fader 1..24. */
export interface MemoryStateDto {
  page: number;
  fader: number;
  /** 24 × (page − 1) + fader, 1..288 */
  number: number;
  /** 0..255, the memory fader (playback level). */
  level: number;
  /** The memory has content; all-zero content counts as deleted. */
  recorded: boolean;
  /** False for a placeholder the console has not delivered (hardware mode: memories arrive one at a time, if at all); the simulator reports every memory. */
  reported: boolean;
  /** The console's label, up to three words; empty when none. */
  label: string;
}

/** A memory's content: one level per channel, index 0 = channel 1. */
export interface MemoryDto {
  page: number;
  fader: number;
  number: number;
  label: string;
  levels: number[];
}

export interface ConsoleStateDto {
  connection: ConnectionDto;
  capabilities: CapabilitiesDto;
  /**
   * Hub commands the connected device accepts ("setChannelLevel", "setMaster", "setBlackout",
   * "setBumps", "setSolo", "bumpChannel", "setMemoryLevel", "bumpMemory", "recordMemory",
   * "storeMemory"). The simulator accepts all; a real console may report none, in which case
   * the corresponding controls must be disabled.
   */
  supportedCommands?: string[];
  /**
   * The individual controls the device accepts commands for ("channelFader:N", "channelBump:N", "memoryFader:N",
   * "memoryBump:N", "master", "bumps", "solo", "blackout", "independent:N", "go", ...), or null when every control of
   * a supported command's kind is accepted (the simulator). A real console cleared for a few controls names exactly
   * those: the UI enables nothing else.
   */
  writableControls?: string[] | null;
  /** Exactly 48 entries. */
  channels: ChannelStateDto[];
  masters: MastersDto;
  /** 512 output levels, 0..255. */
  outputs: number[];
  /** Exactly 288 entries, memory 1 first. */
  memories: MemoryStateDto[];
  /**
   * False until the backend's device reported its state in the current connection; until then
   * the actual levels, masters, outputs and memories are placeholders, not console state.
   */
  actualStateReported?: boolean;
  /** False until the device reported the masters; until then they are placeholders, not console state. */
  mastersReported?: boolean;
  /** The same for the channel levels, the DMX outputs and the memories (a read-only console lists them only when they change). */
  channelsReported?: boolean;
  outputsReported?: boolean;
  memoriesReported?: boolean;
  /** False until the device reported IND 1 / IND 2 (hardware mode: the console's Independents object). */
  independentsReported?: boolean;
  /** The console's selected memory page, 1..12, when the device reports it; null for the simulator (the UI chooses). */
  memoryPage?: number | null;
  /**
   * What the console's 24 physical faders control (the FADERS keys 1-24 / 25-48 / MEMS), when the device reports it
   * (a real console: ConsoleStatus, F98); null for the simulator, which plays every bank at once.
   */
  faderBank?: FaderBank | null;
  /**
   * The physical faders 1..24 whose LED a real console blinks, outside its page view and record screens (LedStatus); empty
   * when none, null for the simulator. Display only: after a mode switch (a 1-24 / 25-48 switch INFERRED alike) the
   * console blinks a fader it treats as unmatched, whose PC moves take effect once they reach the content's level (F172);
   * a fader still holding another page's memory blinks too (H69).
   */
  blinkingFaders?: number[] | null;
  /** The key LEDs a real console reports (hardware mode); null until reported, always null for the simulator. */
  keys?: PanelKeysDto | null;
  /** The console's LCD, two lines (a real console only); kept for the contract, not shown by the UI (owner, 2026-10-31). */
  lcd?: string[] | null;
  /** The main playback a real console reports (PlaybackInfo record 5); null until reported, always null for the simulator. */
  crossfader?: CrossfaderDto | null;
  showFileName?: string | null;
  firmwareVersion?: string | null;
  /**
   * The real console's state at Connect (safety check SC-4: SmartSoft's start-up pair and the download it starts), present
   * while the backend requests it at every Connect (the UI shows STATE AT CONNECT then); null for every other device.
   */
  stateAtConnect?: StateAtConnectDto | null;
}

/** Where the console's state at Connect stands (docs/api.md `stateAtConnect`). */
export type StateAtConnectPhase = 'idle' | 'waiting' | 'requesting' | 'downloading' | 'done' | 'notSent';

/** What became of the MASTER probe after the download (SC-4 clause 4). */
export type MasterProbe = 'pending' | 'notNeeded' | 'sent' | 'notPossible';

export interface StateAtConnectDto {
  phase: StateAtConnectPhase;
  /** The download's record count once it was listed (157..161 observed). */
  records?: number | null;
  /** The download's records delivered so far. */
  delivered: number;
  /** Why the state was not requested or the request ended before the download. */
  reason?: string | null;
  masterProbe: MasterProbe;
  /** Why the MASTER probe could not run: one fader movement on the panel makes the console report the MASTER then. */
  masterProbeReason?: string | null;
}

export const STATE_AT_CONNECT_PHASES: readonly StateAtConnectPhase[] = ['idle', 'waiting', 'requesting', 'downloading', 'done', 'notSent'];
export const MASTER_PROBES: readonly MasterProbe[] = ['pending', 'notNeeded', 'sent', 'notPossible'];

/** The bank the console's physical faders control: channels 1-24, channels 25-48 or the memories of the selected page. */
export type FaderBank = 'channels1To24' | 'channels25To48' | 'memories';

export const FADER_BANKS: readonly FaderBank[] = ['channels1To24', 'channels25To48', 'memories'];

/** One key LED as the console reports it: its first blink phase, and whether the two phases differ. */
export interface KeyLedDto {
  light: 'off' | 'dim' | 'full';
  blinking: boolean;
}

/**
 * The key LEDs a real console reports: STACK and CLEAR of the crossfader section and the record
 * keys. Which LED is which key is inferred from one session (H72); SNAPSHOT and RATE have none.
 */
export interface PanelKeysDto {
  stack: KeyLedDto;
  clear: KeyLedDto;
  copy: KeyLedDto;
  recSeq: KeyLedDto;
  magic: KeyLedDto;
  recMem: KeyLedDto;
  editMem: KeyLedDto;
  /** The BLACKOUT and SOLO LEDs, for their blink (BLACKOUT blinks while the MASTER is below full). */
  blackout: KeyLedDto;
  solo: KeyLedDto;
}

/**
 * The console's main playback as reported (F130, observed): the stack step (0-based, read as the Next
 * step, hypothesis H76), Live and Next 0..255. Under the manual crossfaders A follows Live and B
 * follows Next (hypothesis H80); A gives 100 % Live when down.
 */
export interface CrossfaderDto {
  step: number;
  live: number;
  next: number;
}

export interface OutputsChangedDto {
  outputs: number[];
}

export interface ErrorDto {
  message: string;
  code?: string | null;
}

export const CHANNEL_COUNT = 48;
export const OUTPUT_COUNT = 512;
export const MEMORY_PAGES = 12;
export const MEMORIES_PER_PAGE = 24;
export const MEMORY_COUNT = MEMORY_PAGES * MEMORIES_PER_PAGE;
export const INDEPENDENT_COUNT = 2;
export const LEVEL_MIN = 0;
export const LEVEL_MAX = 255;

/** Memory number 1..288 of a page 1..12 and fader 1..24. */
export function memoryNumber(page: number, fader: number): number {
  return MEMORIES_PER_PAGE * (page - 1) + fader;
}

export function isMemoryPage(value: unknown): value is number {
  return Number.isInteger(value) && (value as number) >= 1 && (value as number) <= MEMORY_PAGES;
}

export function isMemoryFader(value: unknown): value is number {
  return (
    Number.isInteger(value) && (value as number) >= 1 && (value as number) <= MEMORIES_PER_PAGE
  );
}
