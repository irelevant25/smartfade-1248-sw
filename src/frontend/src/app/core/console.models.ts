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
  showFileName?: string | null;
  firmwareVersion?: string | null;
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
