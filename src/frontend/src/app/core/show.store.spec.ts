import { ShowStore } from './show.store';
import { sampleShow } from './testing/sample-show';

describe('ShowStore', () => {
  let store: ShowStore;

  beforeEach(() => {
    store = new ShowStore();
  });

  const load = () => store.setShow(sampleShow(), 'SHOW07.ASC', 'file');

  it('holds nothing until a show is set, then a copy of its own, named and sourced', () => {
    expect(store.loaded()).toBe(false);
    expect(store.show()).toBeUndefined();
    expect(store.dirty()).toBe(false);
    expect(store.setPatchChannel(1, 12)).toBe(false); // nothing to edit

    const dto = sampleShow();
    store.setShow(dto, 'SHOW07.ASC', 'file');
    dto.patch[0].channel = 99; // the caller's object is not the store's
    expect(store.loaded()).toBe(true);
    expect(store.name()).toBe('SHOW07.ASC');
    expect(store.source()).toBe('file');
    expect(store.show()!.patch[0].channel).toBe(1);
    expect(store.dirty()).toBe(false);

    store.setShow(sampleShow(), 'console show', 'console'); // the seam for the console's own show (SC-4)
    expect(store.source()).toBe('console');
  });

  it('marks edits as dirty, reverts them, and clears everything', () => {
    load();
    store.setPatchChannel(1, 12);
    expect(store.dirty()).toBe(true);
    expect(store.show()!.patch[0].channel).toBe(12);
    expect(store.original()!.patch[0].channel).toBe(1);

    store.revert();
    expect(store.dirty()).toBe(false);
    expect(store.show()!.patch[0].channel).toBe(1);

    store.clear();
    expect(store.loaded()).toBe(false);
    expect(store.name()).toBeUndefined();
    expect(store.source()).toBeUndefined();
  });

  it('patches one address, a run of addresses in either order, keeps the levels, and refuses channels outside 0..96', () => {
    load();
    expect(store.setPatchChannel(1, 12)).toBe(true);
    expect(store.show()!.patch[0]).toEqual({ dmx: 1, channel: 12, level: 100 });
    expect(store.setPatchChannel(1, 97)).toBe(false);
    expect(store.setPatchChannel(1, -1)).toBe(false);
    expect(store.setPatchChannel(1, 1.5)).toBe(false);
    expect(store.setPatchChannel(513, 1)).toBe(false);
    expect(store.show()!.patch[0].channel).toBe(12);

    store.setPatchLevel(102, 50);
    expect(store.setPatchRange(103, 100, 50)).toBe(true);
    expect(store.show()!.patch.slice(99, 103).map((e) => [e.channel, e.level])).toEqual([[50, 100], [50, 100], [50, 50], [50, 100]]);
    expect(store.show()!.patch[103].channel).toBe(0);
    expect(store.setPatchRange(100, 103, 0)).toBe(true);
    expect(store.show()!.patch.slice(99, 103).every((e) => e.channel === 0)).toBe(true);
    expect(store.setPatchRange(600, 601, 1)).toBe(false);
  });

  it('sets the proportional level of an address within 0..100 only', () => {
    load();
    expect(store.setPatchLevel(47, 50)).toBe(true);
    expect(store.show()!.patch[46]).toEqual({ dmx: 47, channel: 50, level: 50 });
    expect(store.setPatchLevel(47, 101)).toBe(false);
    expect(store.setPatchLevel(47, -1)).toBe(false);
    expect(store.setPatchLevel(47, NaN)).toBe(false);
    expect(store.setPatchLevel(0, 100)).toBe(false);
    expect(store.show()!.patch[46].level).toBe(50);
  });

  it("edits a step's times and memory, keeps the number, and refuses what the console would not write", () => {
    load();
    expect(store.setStep('stack', 0, { followOn: '1:30' })).toBe(true);
    expect(store.setStep('stack', 0, { memoryNumber: 267 })).toBe(true);
    expect(store.setStep(0, 0, { upTime: '2.5' })).toBe(true);
    expect(store.dirty()).toBe(true);

    expect(store.setStep('stack', 0, { upTime: '3,5' })).toBe(false); // not a console time
    expect(store.setStep('stack', 0, { memoryNumber: 300 })).toBe(false); // outside 1..288
    expect(store.setStep('stack', 0, { memoryNumber: null })).toBe(false); // a memory step always has one
    expect(store.setStep('stack', 9, { upTime: '1.0' })).toBe(false); // no such step
    expect(store.setStep(5, 0, { upTime: '1.0' })).toBe(false); // no such sequence

    const stack = store.show()!.mainStack!;
    expect(stack.steps[0]).toMatchObject({ number: '1.0', followOn: '1:30', memoryPage: 11, memoryFader: 2, memoryNumber: 267, upTime: '5.0', channels: [] });
    expect(store.show()!.sequences[0].steps[0].upTime).toBe('2.5');
    expect(store.stepsOf('stack')).toBe(stack.steps);
    expect(store.stepsOf(7)).toBeUndefined();
  });

  it("inserts and removes steps, numbers them as the console's files do, and keeps a step's channels equal to its memory's", () => {
    load();
    // a cue after 1.0 takes 1.1 (2.0 follows), copies memory 1 with its channels; after the last cue the next whole number
    expect(store.insertStep('stack', 0)).toBe(true);
    expect(store.insertStep('stack', 4)).toBe(true);
    let stack = store.show()!.mainStack!.steps;
    expect(stack.map((s) => s.number)).toEqual(['1.0', '1.1', '2.0', '13.0', '13.1', '14.0']);
    expect(stack[1]).toMatchObject({ memoryNumber: 1, cueParams: 1, upTime: '5.0', channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }] });
    expect(stack[5]).toMatchObject({ memoryNumber: 1, cueParams: 1, label: null }); // after a State step: a memory step with the first memory

    // changing the memory replaces the channels with the new memory's (265 has channel 37) and a sequence step's number
    expect(store.setStep('stack', 1, { memoryNumber: 265 })).toBe(true);
    expect(store.show()!.mainStack!.steps[1]).toMatchObject({ number: '1.1', memoryNumber: 265, channels: [{ channel: 37, percent: 100 }] });
    expect(store.insertStep(0, 0)).toBe(true);
    expect(store.setStep(0, 0, { memoryNumber: 1 })).toBe(true);
    const sequence = store.show()!.sequences[0].steps;
    expect(sequence.map((s) => s.number)).toEqual(['1.0', '265.0']);
    expect(sequence[0].channels).toEqual([{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }]);

    // no number fits between 1.0 and 1.1; nothing outside the list
    expect(store.insertStep('stack', 0)).toBe(false);
    expect(store.insertStep('stack', 6)).toBe(false);
    expect(store.insertStep('stack', -2)).toBe(false);
    expect(store.show()!.mainStack!.steps).toHaveLength(6);

    // an empty sequence gets a first step numbered by its memory, with the sequences' usual times
    expect(store.insertStep(1, -1)).toBe(true);
    expect(store.show()!.sequences[1].steps[0]).toMatchObject({ number: '1.0', memoryNumber: 1, upTime: '1.0', downTime: '1.0', followOn: '0' });

    // removing keeps the other numbers
    expect(store.removeStep('stack', 1)).toBe(true);
    stack = store.show()!.mainStack!.steps;
    expect(stack.map((s) => s.number)).toEqual(['1.0', '2.0', '13.0', '13.1', '14.0']);
    expect(store.removeStep('stack', 5)).toBe(false);
    expect(store.dirty()).toBe(true);
  });

  it("edits a memory's channels and labels, keeps the steps that use the memory in step, and refuses what the files never write", () => {
    load();
    // channels in the files' form, sorted into rising order; cue 1.0 uses memory 1 and follows it
    expect(store.setMemoryChannels(1, [{ channel: 9, percent: 100 }, { channel: 7, percent: 50 }])).toBe(true);
    expect(store.show()!.memories[0].channels).toEqual([{ channel: 7, percent: 50 }, { channel: 9, percent: 100 }]);
    expect(store.show()!.mainStack!.steps[0].channels).toEqual([{ channel: 7, percent: 50 }, { channel: 9, percent: 100 }]);

    // channel 49, 101 %, 0 %, a channel twice, an unknown memory: refused
    expect(store.setMemoryChannels(1, [{ channel: 49, percent: 100 }])).toBe(false);
    expect(store.setMemoryChannels(1, [{ channel: 7, percent: 101 }])).toBe(false);
    expect(store.setMemoryChannels(1, [{ channel: 7, percent: 0 }])).toBe(false);
    expect(store.setMemoryChannels(1, [{ channel: 7, percent: 1 }, { channel: 7, percent: 2 }])).toBe(false);
    expect(store.setMemoryChannels(2, [])).toBe(false);
    expect(store.show()!.memories[0].channels).toEqual([{ channel: 7, percent: 50 }, { channel: 9, percent: 100 }]);
    expect(store.setMemoryChannels(1, [])).toBe(true);
    expect(store.show()!.memories[0].channels).toEqual([]);
    expect(store.show()!.mainStack!.steps[0].channels).toEqual([]);

    // labels: the Text line follows from the lines as the console derives it; 7 characters and '&' are refused
    expect(store.setLabelLine({ kind: 'memory', number: 1 }, 1, 'OPONA2')).toBe(true);
    expect(store.setLabelLine({ kind: 'memory', number: 1 }, 2, 'X')).toBe(true);
    expect(store.show()!.memories[0].label).toEqual({ text: 'OPONA2X', line1: 'OPONA2', line2: 'X', line3: '' });
    expect(store.setLabelLine({ kind: 'memory', number: 1 }, 1, 'SEDEM7A')).toBe(false);
    expect(store.setLabelLine({ kind: 'memory', number: 1 }, 3, 'A&B')).toBe(false);
    expect(store.setLabelLine({ kind: 'memory', number: 2 }, 1, 'X')).toBe(false);
    expect(store.labelOf({ kind: 'memory', number: 1 })).toEqual({ text: 'OPONA2X', line1: 'OPONA2', line2: 'X', line3: '' });

    expect(store.setLabelLine({ kind: 'channel', number: 7 }, 1, 'CAJ')).toBe(true);
    expect(store.show()!.channels[0].label).toEqual({ text: 'CAJ', line1: 'CAJ', line2: '', line3: '' });
    expect(store.setLabelLine({ kind: 'channel', number: 8 }, 1, 'X')).toBe(false);
    expect(store.setLabelLine({ kind: 'sequence', index: 0 }, 1, 'KOTOL')).toBe(true);
    expect(store.show()!.sequences[0].label).toEqual({ text: 'KOTOL', line1: 'KOTOL', line2: '', line3: '' });
    expect(store.setLabelLine({ kind: 'sequence', index: 1 }, 2, 'NEW')).toBe(true); // a sequence without a label gets one
    expect(store.show()!.sequences[1].label).toEqual({ text: '', line1: '', line2: 'NEW', line3: '' });
    expect(store.setLabelLine({ kind: 'sequence', index: 9 }, 1, 'X')).toBe(false);
    expect(store.dirty()).toBe(true);
  });
});
