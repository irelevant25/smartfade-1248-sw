import { computed, Injectable, signal } from '@angular/core';
import {
  CHANNEL_COUNT,
  INDEPENDENT_COUNT,
  isMemoryFader,
  isMemoryPage,
  MEMORIES_PER_PAGE,
  MEMORY_COUNT,
  memoryNumber,
  OUTPUT_COUNT,
  type CapabilitiesDto,
  type ChannelStateDto,
  type ConnectionDto,
  type ConnectionStatus,
  type ConsoleStateDto,
  type DeviceMode,
  type ErrorDto,
  type MastersDto,
  type MemoryStateDto,
  type OutputsChangedDto,
  PanelKeysDto,
} from './console.models';
import { clampLevel } from './level';

/** Console limits as documented in docs/api.md; replaced by the backend's values on first state. */
const DEFAULT_CAPABILITIES: CapabilitiesDto = {
  channels: 48,
  physicalFaders: 24,
  memoryPages: 12,
  memoriesPerPage: 24,
  sequences: 48,
  stackSteps: 199,
  dmxAddresses: 512,
  independents: 2,
};

const IDLE_MASTERS: MastersDto = {
  master: 0,
  bumps: 0,
  blackout: false,
  solo: false,
  independent1: false,
  independent2: false,
};

function idleChannels(): ChannelStateDto[] {
  return Array.from({ length: CHANNEL_COUNT }, (_, index) => ({
    channel: index + 1,
    desired: 0,
    actual: 0,
    pending: false,
    output: 0,
  }));
}

function idleOutputs(): number[] {
  return new Array<number>(OUTPUT_COUNT).fill(0);
}

/** Every memory of the console, unrecorded, faders at zero, no label; index = number − 1. */
function idleMemories(): MemoryStateDto[] {
  return Array.from({ length: MEMORY_COUNT }, (_, index) => ({
    page: Math.floor(index / MEMORIES_PER_PAGE) + 1,
    fader: (index % MEMORIES_PER_PAGE) + 1,
    number: index + 1,
    level: 0,
    recorded: false,
    reported: false,
    label: '',
  }));
}

function isChannelNumber(value: unknown): value is number {
  return Number.isInteger(value) && (value as number) >= 1 && (value as number) <= CHANNEL_COUNT;
}

function isIndependentNumber(value: unknown): value is number {
  return (
    Number.isInteger(value) && (value as number) >= 1 && (value as number) <= INDEPENDENT_COUNT
  );
}

function isMemoryAddress(dto: MemoryStateDto | undefined): dto is MemoryStateDto {
  return dto !== undefined && isMemoryPage(dto.page) && isMemoryFader(dto.fader);
}

function sanitizeChannel(dto: ChannelStateDto): ChannelStateDto {
  return {
    channel: dto.channel,
    desired: clampLevel(dto.desired),
    actual: clampLevel(dto.actual),
    pending: Boolean(dto.pending),
    output: clampLevel(dto.output ?? 0),
  };
}

function sanitizeMasters(dto: MastersDto): MastersDto {
  return {
    master: clampLevel(dto.master),
    bumps: clampLevel(dto.bumps),
    blackout: Boolean(dto.blackout),
    solo: Boolean(dto.solo),
    independent1: Boolean(dto.independent1),
    independent2: Boolean(dto.independent2),
  };
}

const KEY_NAMES = ['stack', 'clear', 'copy', 'recSeq', 'magic', 'recMem', 'editMem', 'blackout', 'solo'] as const;
const KEY_LIGHTS = ['off', 'dim', 'full'];

/** The key LEDs, or undefined unless every key is present and well-formed (the simulator sends none). */
function sanitizeKeys(dto: PanelKeysDto | null | undefined): PanelKeysDto | undefined {
  if (!dto || typeof dto !== 'object') {
    return undefined;
  }
  const keys: Partial<PanelKeysDto> = {};
  for (const name of KEY_NAMES) {
    const led = dto[name];
    if (!led || typeof led !== 'object' || !KEY_LIGHTS.includes(led.light)) {
      return undefined;
    }
    keys[name] = { light: led.light, blinking: led.blinking === true };
  }
  return keys as PanelKeysDto;
}

/** The address comes from the DTO; the number is derived, so that it can never disagree. */
function sanitizeMemory(dto: MemoryStateDto): MemoryStateDto {
  return {
    page: dto.page,
    fader: dto.fader,
    number: memoryNumber(dto.page, dto.fader),
    level: clampLevel(dto.level),
    recorded: Boolean(dto.recorded),
    reported: dto.reported !== false,
    label: typeof dto.label === 'string' ? dto.label : '',
  };
}

/** Always exactly OUTPUT_COUNT whole levels in range, whatever the payload held. */
function normalizeOutputs(source: readonly number[]): number[] {
  const outputs = new Array<number>(OUTPUT_COUNT);
  for (let index = 0; index < OUTPUT_COUNT; index++) {
    outputs[index] = clampLevel(source[index] ?? 0);
  }
  return outputs;
}

/**
 * How long the user's latest request for a value outranks what an incoming event says the
 * desired value is. The backend echoes every command, so while the user keeps moving a fader
 * the echoes are always a little behind; without this they would pull the fader back to older
 * values. After this time, or as soon as an event agrees with the request, the backend wins.
 */
const REQUEST_WINDOW_MS = 1500;

/** What the user last asked for, until the backend has caught up with it. */
interface Request<T> {
  value: T;
  until: number;
}

function request<T>(value: T): Request<T> {
  return { value, until: Date.now() + REQUEST_WINDOW_MS };
}

/**
 * The value to show for something the user may have asked for: the user's request while the
 * report is older than it, otherwise the report. `settled` tells the caller to forget the request.
 */
function outrank<T>(
  requested: Request<T> | undefined,
  reported: T,
): { value: T; settled: boolean } {
  if (!requested || requested.value === reported || Date.now() >= requested.until) {
    return { value: reported, settled: true };
  }
  return { value: requested.value, settled: false };
}

/**
 * The page's copy of the console state, as a set of signals. It is written by two parties only:
 * the connection service applies what the backend reports, and the channel service applies the
 * user's intent optimistically (desired + pending, or a held request for the masters and the
 * memory faders) until the backend confirms it.
 */
@Injectable({ providedIn: 'root' })
export class ConsoleStateService {
  private readonly _connection = signal<ConnectionDto | undefined>(undefined);
  private readonly _capabilities = signal<CapabilitiesDto>(DEFAULT_CAPABILITIES);
  private readonly _channels = signal<readonly ChannelStateDto[]>(idleChannels());
  private readonly _masters = signal<MastersDto>(IDLE_MASTERS);
  private readonly _memories = signal<readonly MemoryStateDto[]>(idleMemories());
  private readonly _outputs = signal<readonly number[]>(idleOutputs());
  private readonly _showFileName = signal<string | undefined>(undefined);
  private readonly _firmwareVersion = signal<string | undefined>(undefined);
  private readonly _lastError = signal<ErrorDto | undefined>(undefined);
  private readonly _supportedCommands = signal<readonly string[]>([]);
  private readonly _mastersReported = signal(false);
  private readonly _channelsReported = signal(false);
  private readonly _outputsReported = signal(false);
  private readonly _memoriesReported = signal(false);
  private readonly _independentsReported = signal(false);
  private readonly _memoryPage = signal<number | undefined>(undefined);
  private readonly _keys = signal<PanelKeysDto | undefined>(undefined);

  private readonly _channelRequests = new Map<number, Request<number>>();
  private readonly _memoryRequests = new Map<number, Request<number>>();
  private readonly _independentRequests = new Map<number, Request<boolean>>();
  private _masterRequest: Request<number> | undefined;
  private _bumpsRequest: Request<number> | undefined;
  private _blackoutRequest: Request<boolean> | undefined;
  private _soloRequest: Request<boolean> | undefined;

  /** Undefined until the backend has reported at least once: the mode is then unknown. */
  readonly connection = this._connection.asReadonly();
  readonly capabilities = this._capabilities.asReadonly();
  /** Always 48 entries, index = channel - 1. */
  readonly channels = this._channels.asReadonly();
  readonly masters = this._masters.asReadonly();
  /** Always 288 entries, index = memory number - 1 (page 1 fader 1 first). */
  readonly memories = this._memories.asReadonly();
  /** Always 512 entries. */
  readonly outputs = this._outputs.asReadonly();
  readonly showFileName = this._showFileName.asReadonly();
  readonly firmwareVersion = this._firmwareVersion.asReadonly();
  readonly lastError = this._lastError.asReadonly();
  /** Hub commands the connected device accepts; empty for a read-only device (hardware mode). */
  readonly supportedCommands = this._supportedCommands.asReadonly();
  /** False until the device reported the masters: what the masters show before that is a placeholder. */
  readonly mastersReported = this._mastersReported.asReadonly();
  readonly channelsReported = this._channelsReported.asReadonly();
  readonly outputsReported = this._outputsReported.asReadonly();
  readonly memoriesReported = this._memoriesReported.asReadonly();
  /** False until the device reported IND 1 / IND 2; the masters' independents are placeholders before that. */
  readonly independentsReported = this._independentsReported.asReadonly();
  /** The console's selected memory page when the device reports one (hardware mode), else undefined. */
  readonly memoryPage = this._memoryPage.asReadonly();
  /** The key LEDs a real console reports (hardware mode), else undefined: the keys then follow the UI's own logic. */
  readonly keys = this._keys.asReadonly();

  /** Status of the console itself (not of the link to the backend). */
  readonly deviceStatus = computed<ConnectionStatus>(
    () => this._connection()?.status ?? 'disconnected',
  );
  readonly mode = computed<DeviceMode | undefined>(() => this._connection()?.mode);

  // ---- Events from the backend ------------------------------------------------------------

  /** consoleStateChanged: a complete snapshot replaces everything, including pending requests. */
  applyConsoleStateChanged(state: ConsoleStateDto): void {
    const channels = idleChannels();
    for (const dto of state.channels ?? []) {
      if (isChannelNumber(dto?.channel)) {
        channels[dto.channel - 1] = sanitizeChannel(dto);
      }
    }
    const memories = idleMemories();
    for (const dto of state.memories ?? []) {
      if (isMemoryAddress(dto)) {
        const memory = sanitizeMemory(dto);
        memories[memory.number - 1] = memory;
      }
    }
    this._channelRequests.clear();
    this._memoryRequests.clear();
    this._independentRequests.clear();
    this._masterRequest = undefined;
    this._bumpsRequest = undefined;
    this._blackoutRequest = undefined;
    this._soloRequest = undefined;
    this._connection.set(state.connection);
    this._capabilities.set(state.capabilities ?? DEFAULT_CAPABILITIES);
    this._channels.set(channels);
    this._masters.set(sanitizeMasters(state.masters ?? IDLE_MASTERS));
    this._memories.set(memories);
    this._outputs.set(normalizeOutputs(state.outputs ?? []));
    this._showFileName.set(state.showFileName ?? undefined);
    this._firmwareVersion.set(state.firmwareVersion ?? undefined);
    this._supportedCommands.set(
      Array.isArray(state.supportedCommands)
        ? state.supportedCommands.filter((name): name is string => typeof name === 'string')
        : [],
    );
    this._mastersReported.set(Boolean(state.mastersReported));
    this._channelsReported.set(Boolean(state.channelsReported));
    this._outputsReported.set(Boolean(state.outputsReported));
    this._memoriesReported.set(Boolean(state.memoriesReported));
    this._independentsReported.set(Boolean(state.independentsReported));
    const page = state.memoryPage;
    this._memoryPage.set(
      typeof page === 'number' && Number.isInteger(page) && page >= 1 && page <= this._capabilities().memoryPages
        ? page
        : undefined,
    );
    this._keys.set(sanitizeKeys(state.keys));
  }

  /** Whether the connected device accepts a hub command (docs/api.md `supportedCommands`). */
  supports(command: string): boolean {
    return this._supportedCommands().includes(command);
  }

  /** connectionStateChanged */
  applyConnectionStateChanged(connection: ConnectionDto): void {
    this._connection.set(connection);
  }

  /** channelStateChanged: one or more channels; the others keep their identity. */
  applyChannelStateChanged(updates: readonly ChannelStateDto[]): void {
    if (!Array.isArray(updates)) {
      return;
    }
    const valid = updates
      .filter((dto) => isChannelNumber(dto?.channel))
      .map((dto) => this._outrankChannel(sanitizeChannel(dto)));
    if (valid.length === 0) {
      return;
    }
    this._channelsReported.set(true);
    this._channels.update((current) => {
      const next = current.slice();
      for (const dto of valid) {
        next[dto.channel - 1] = dto;
      }
      return next;
    });
  }

  /** mastersChanged */
  applyMastersChanged(masters: MastersDto): void {
    this._mastersReported.set(true);
    const reported = sanitizeMasters(masters);
    const master = outrank(this._masterRequest, reported.master);
    const bumps = outrank(this._bumpsRequest, reported.bumps);
    const blackout = outrank(this._blackoutRequest, reported.blackout);
    const solo = outrank(this._soloRequest, reported.solo);
    const independent1 = outrank(this._independentRequests.get(1), reported.independent1);
    const independent2 = outrank(this._independentRequests.get(2), reported.independent2);
    if (master.settled) {
      this._masterRequest = undefined;
    }
    if (bumps.settled) {
      this._bumpsRequest = undefined;
    }
    if (blackout.settled) {
      this._blackoutRequest = undefined;
    }
    if (solo.settled) {
      this._soloRequest = undefined;
    }
    if (independent1.settled) {
      this._independentRequests.delete(1);
    }
    if (independent2.settled) {
      this._independentRequests.delete(2);
    }
    this._masters.set({
      master: master.value,
      bumps: bumps.value,
      blackout: blackout.value,
      solo: solo.value,
      independent1: independent1.value,
      independent2: independent2.value,
    });
  }

  /** memoriesChanged: one or more memories; the others keep their identity. */
  applyMemoriesChanged(updates: readonly MemoryStateDto[]): void {
    if (!Array.isArray(updates)) {
      return;
    }
    const valid = updates
      .filter(isMemoryAddress)
      .map((dto) => this._outrankMemory(sanitizeMemory(dto)));
    if (valid.length === 0) {
      return;
    }
    this._memoriesReported.set(true);
    this._memories.update((current) => {
      const next = current.slice();
      for (const dto of valid) {
        next[dto.number - 1] = dto;
      }
      return next;
    });
  }

  /** outputsChanged */
  applyOutputsChanged(update: OutputsChangedDto): void {
    if (!Array.isArray(update?.outputs)) {
      return;
    }
    this._outputsReported.set(true);
    this._outputs.set(normalizeOutputs(update.outputs));
  }

  /** errorOccurred (also used for failures that happen on this side of the link). */
  applyErrorOccurred(error: ErrorDto): void {
    this._lastError.set({
      message: error?.message || 'Unknown error',
      ...(error?.code ? { code: error.code } : {}),
    });
  }

  clearError(): void {
    this._lastError.set(undefined);
  }

  // ---- The user's intent, applied before the backend confirms it --------------------------

  /** Sets what the user asked for; pending until the reported level catches up. */
  setDesiredChannelLevel(channel: number, level: number): void {
    if (!isChannelNumber(channel)) {
      return;
    }
    this._channelRequests.set(channel, request(level));
    this._channels.update((current) => {
      const existing = current[channel - 1];
      if (existing.desired === level && existing.pending === (level !== existing.actual)) {
        return current;
      }
      const next = current.slice();
      next[channel - 1] = { ...existing, desired: level, pending: level !== existing.actual };
      return next;
    });
  }

  /** CLEAR: every channel fader's desired level becomes zero at once. */
  clearDesiredChannels(): void {
    this._channels.update((current) =>
      current.map((channel) => {
        this._channelRequests.set(channel.channel, request(0));
        return channel.desired === 0 && channel.pending === (channel.actual !== 0)
          ? channel
          : { ...channel, desired: 0, pending: channel.actual !== 0 };
      }),
    );
  }

  setDesiredMaster(level: number): void {
    this._masterRequest = request(level);
    this._masters.update((masters) =>
      masters.master === level ? masters : { ...masters, master: level },
    );
  }

  setDesiredBumps(level: number): void {
    this._bumpsRequest = request(level);
    this._masters.update((masters) =>
      masters.bumps === level ? masters : { ...masters, bumps: level },
    );
  }

  setDesiredBlackout(on: boolean): void {
    this._blackoutRequest = request(on);
    this._masters.update((masters) =>
      masters.blackout === on ? masters : { ...masters, blackout: on },
    );
  }

  setDesiredSolo(on: boolean): void {
    this._soloRequest = request(on);
    this._masters.update((masters) => (masters.solo === on ? masters : { ...masters, solo: on }));
  }

  /** IND 1 (1) or IND 2 (2) on or off. */
  setDesiredIndependent(number: number, on: boolean): void {
    if (!isIndependentNumber(number)) {
      return;
    }
    this._independentRequests.set(number, request(on));
    const key = number === 1 ? 'independent1' : 'independent2';
    this._masters.update((masters) => (masters[key] === on ? masters : { ...masters, [key]: on }));
  }

  /** The user moved a memory fader; shown at once, held until the backend agrees. */
  setDesiredMemoryLevel(page: number, fader: number, level: number): void {
    if (!isMemoryPage(page) || !isMemoryFader(fader)) {
      return;
    }
    const number = memoryNumber(page, fader);
    this._memoryRequests.set(number, request(level));
    this._memories.update((current) => {
      const existing = current[number - 1];
      if (existing.level === level) {
        return current;
      }
      const next = current.slice();
      next[number - 1] = { ...existing, level };
      return next;
    });
  }

  /** CLEAR: every memory fader to zero at once. */
  clearDesiredMemoryLevels(): void {
    this._memories.update((current) =>
      current.map((memory) => {
        this._memoryRequests.set(memory.number, request(0));
        return memory.level === 0 ? memory : { ...memory, level: 0 };
      }),
    );
  }

  /** A report of a channel that is older than the user's request must not undo the request. */
  private _outrankChannel(reported: ChannelStateDto): ChannelStateDto {
    const desired = outrank(this._channelRequests.get(reported.channel), reported.desired);
    if (desired.settled) {
      this._channelRequests.delete(reported.channel);
      return reported;
    }
    return {
      ...reported,
      desired: desired.value,
      pending: reported.pending || desired.value !== reported.actual,
    };
  }

  private _outrankMemory(reported: MemoryStateDto): MemoryStateDto {
    const level = outrank(this._memoryRequests.get(reported.number), reported.level);
    if (level.settled) {
      this._memoryRequests.delete(reported.number);
      return reported;
    }
    return { ...reported, level: level.value };
  }
}
