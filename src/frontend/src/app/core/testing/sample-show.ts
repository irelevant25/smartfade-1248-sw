import type { LabelDto, ShowFileDto, StepDto } from '../show-file.models';

export const label = (line1 = '', text = line1, line2 = '', line3 = ''): LabelDto => ({ text, line1, line2, line3 });

const memoryStep = (number: string, memoryNumber: number, channels: StepDto['channels'], times: Partial<StepDto> = {}): StepDto => ({
  number,
  memoryPage: Math.floor((memoryNumber - 1) / 24),
  memoryFader: (memoryNumber - 1) % 24,
  memoryNumber,
  cueParams: 1,
  upTime: '5.0',
  upDelay: '0',
  downTime: '5.0',
  downDelay: '0',
  followOn: '0',
  channels,
  label: null,
  ...times,
});

/**
 * A small show in the shape of docs/api.md's ShowFileDto, for unit tests: a stack with two memory steps, a channel
 * step and a State step (the three kinds SHOW15.ASC holds), one sequence, two memories, channel 7 labelled, and a
 * patch of DMX 1..48 to channels 1..48 except DMX 47 to IND 2 (channel 50, as the owner's console has it) and DMX 97
 * to IND 1 (channel 49).
 */
export function sampleShow(): ShowFileDto {
  const patch = Array.from({ length: 512 }, (_, i) => ({ dmx: i + 1, channel: i < 48 ? i + 1 : 0, level: 100 }));
  patch[46] = { dmx: 47, channel: 50, level: 100 };
  patch[96] = { dmx: 97, channel: 49, level: 100 };
  return {
    name: 'SHOW07.ASC',
    bytes: 15944,
    lines: 1153,
    lineEnding: 'lf',
    manufacturer: 'AVAB',
    console: 'SMARTFADE',
    softwareVersion: '3.0.2 43',
    ident: '3.0',
    clearAll: true,
    mainStack: {
      index: 48, b: 2, c: 0, d: 256, e: 256, isMainStack: true, label: null,
      steps: [
        memoryStep('1.0', 1, [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }]),
        memoryStep('2.0', 2, [{ channel: 1, percent: 17 }], { upDelay: '2', followOn: '1:30' }),
        { number: '13.0', memoryPage: null, memoryFader: null, memoryNumber: null, cueParams: 0, upTime: '5.0', upDelay: '0', downTime: '5.0', downDelay: '0', followOn: '0', channels: [{ channel: 47, percent: 100 }], label: null },
        { number: '13.1', memoryPage: null, memoryFader: null, memoryNumber: null, cueParams: 2, upTime: '5.0', upDelay: '0', downTime: '5.0', downDelay: '0', followOn: '0', channels: [], label: label('STATE') },
      ],
    },
    sequences: [
      { index: 0, b: 0, c: 0, d: 638, e: 256, isMainStack: false, label: label('KOTLIK'), steps: [
        memoryStep('265.0', 265, [], { upTime: '1.0', downTime: '1.0' }),
      ] },
      { index: 1, b: 0, c: 0, d: 638, e: 256, isMainStack: false, label: null, steps: [] },
    ],
    memories: [
      { number: 1, page: 1, fader: 1, channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }], label: label('OPONA') },
      { number: 265, page: 12, fader: 1, channels: [{ channel: 37, percent: 100 }], label: label('') },
    ],
    patch,
    independents: [{ number: 1, a: 0, b: 255, c: 0, d: 1, label: label('') }],
    channels: [{ number: 7, label: label('Caj') }],
    pages: [],
    settings: [{ key: 'MIDIActive', value: '1' }],
    unknownLines: 0,
    warnings: [],
  };
}
