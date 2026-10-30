import {
  CHANNEL_COUNT,
  MEMORIES_PER_PAGE,
  MEMORY_COUNT,
  OUTPUT_COUNT,
  type ConsoleStateDto,
  type MemoryStateDto,
} from '../console.models';

/** All 288 memories, unrecorded and at zero, in memory-number order. */
export function createSampleMemories(): MemoryStateDto[] {
  return Array.from({ length: MEMORY_COUNT }, (_, index) => ({
    page: Math.floor(index / MEMORIES_PER_PAGE) + 1,
    fader: (index % MEMORIES_PER_PAGE) + 1,
    number: index + 1,
    level: 0,
    recorded: false,
    label: '',
  }));
}

/**
 * A complete, connected console state for unit tests (domain level only, in the shape of
 * docs/api.md). Channel 1 is at full, channel 2 is at 128 and waiting for the console, the rest
 * are at zero. Memory 1 (page 1 fader 1) is recorded and labelled, memory 2 is recorded and its
 * fader is at 128, memory 25 (page 2 fader 1) is recorded; the others are empty.
 */
export function createSampleState(overrides: Partial<ConsoleStateDto> = {}): ConsoleStateDto {
  const channels = Array.from({ length: CHANNEL_COUNT }, (_, index) => ({
    channel: index + 1,
    desired: 0,
    actual: 0,
    pending: false,
    output: 0,
  }));
  channels[0] = { channel: 1, desired: 255, actual: 255, pending: false, output: 255 };
  channels[1] = { channel: 2, desired: 128, actual: 64, pending: true, output: 64 };

  const outputs = new Array<number>(OUTPUT_COUNT).fill(0);
  outputs[0] = 255;
  outputs[1] = 128;

  const memories = createSampleMemories();
  memories[0] = { ...memories[0], recorded: true, label: 'GROUP 1' };
  memories[1] = { ...memories[1], recorded: true, level: 128 };
  memories[24] = { ...memories[24], recorded: true, label: 'PAGE 2' };

  return {
    connection: {
      status: 'connected',
      mode: 'simulated',
      deviceDescription: 'Simulated SmartFade 1248',
    },
    capabilities: {
      channels: 48,
      physicalFaders: 24,
      memoryPages: 12,
      memoriesPerPage: 24,
      sequences: 48,
      stackSteps: 199,
      dmxAddresses: 512,
      independents: 2,
    },
    supportedCommands: [
      'setChannelLevel',
      'setMaster',
      'setBlackout',
      'setBumps',
      'setSolo',
      'bumpChannel',
      'setMemoryLevel',
      'bumpMemory',
      'recordMemory',
      'storeMemory',
      'setIndependent',
      'clearChannels',
      'clearMemories',
    ],
    channels,
    masters: {
      master: 200,
      bumps: 255,
      blackout: false,
      solo: false,
      independent1: false,
      independent2: false,
    },
    outputs,
    memories,
    actualStateReported: true,
    mastersReported: true,
    channelsReported: true,
    outputsReported: true,
    memoriesReported: true,
    showFileName: 'SHOW04.ASC',
    firmwareVersion: '3.0.2.9.0.43',
    ...overrides,
  };
}
