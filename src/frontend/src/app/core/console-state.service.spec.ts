import { TestBed } from '@angular/core/testing';
import { ConsoleStateService } from './console-state.service';
import {
  CHANNEL_COUNT,
  MEMORY_COUNT,
  OUTPUT_COUNT,
  type OutputsChangedDto,
} from './console.models';
import { createSampleState } from './testing/sample-state';

describe('ConsoleStateService', () => {
  let service: ConsoleStateService;

  beforeEach(() => {
    service = TestBed.inject(ConsoleStateService);
  });

  describe('before the backend has reported anything', () => {
    it('is idle: 48 channels at zero, 288 empty memories, 512 outputs at zero, no connection report', () => {
      expect(service.channels()).toHaveLength(CHANNEL_COUNT);
      expect(service.channels()[0]).toEqual({
        channel: 1,
        desired: 0,
        actual: 0,
        pending: false,
        output: 0,
      });
      expect(service.channels()[47]).toEqual({
        channel: 48,
        desired: 0,
        actual: 0,
        pending: false,
        output: 0,
      });
      expect(service.memories()).toHaveLength(MEMORY_COUNT);
      expect(service.memories()[0]).toEqual({
        page: 1,
        fader: 1,
        number: 1,
        level: 0,
        recorded: false,
        reported: false,
        label: '',
      });
      expect(service.memories()[287]).toEqual({
        page: 12,
        fader: 24,
        number: 288,
        level: 0,
        recorded: false,
        reported: false,
        label: '',
      });
      expect(service.outputs()).toHaveLength(OUTPUT_COUNT);
      expect(service.outputs().every((level) => level === 0)).toBe(true);
      expect(service.masters()).toEqual({ master: 0, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false });
      expect(service.connection()).toBeUndefined();
      expect(service.mode()).toBeUndefined();
      expect(service.deviceStatus()).toBe('disconnected');
      expect(service.lastError()).toBeUndefined();
    });
  });

  describe('consoleStateChanged', () => {
    it('replaces every signal with the snapshot', () => {
      const snapshot = createSampleState();

      service.applyConsoleStateChanged(snapshot);

      expect(service.connection()).toEqual(snapshot.connection);
      expect(service.capabilities()).toEqual(snapshot.capabilities);
      expect(service.channels()[0]).toEqual({
        channel: 1,
        desired: 255,
        actual: 255,
        pending: false,
        output: 255,
      });
      expect(service.channels()[1]).toEqual({
        channel: 2,
        desired: 128,
        actual: 64,
        pending: true,
        output: 64,
      });
      expect(service.masters()).toEqual({ master: 200, bumps: 255, blackout: false, solo: false, independent1: false, independent2: false });
      expect(service.memories()[0]).toEqual({
        page: 1,
        fader: 1,
        number: 1,
        level: 0,
        recorded: true,
        reported: true,
        label: 'GROUP 1',
      });
      expect(service.memories()[1].level).toBe(128);
      expect(service.memories()[24]).toEqual({
        page: 2,
        fader: 1,
        number: 25,
        level: 0,
        recorded: true,
        reported: true,
        label: 'PAGE 2',
      });
      expect(service.memories()[2].recorded).toBe(false);
      expect(service.outputs()[0]).toBe(255);
      expect(service.outputs()[1]).toBe(128);
      expect(service.showFileName()).toBe('SHOW04.ASC');
      expect(service.firmwareVersion()).toBe('3.0.2.9.0.43');
      expect(service.deviceStatus()).toBe('connected');
      expect(service.mode()).toBe('simulated');
    });

    it('forgets optional details the new snapshot no longer carries', () => {
      service.applyConsoleStateChanged(createSampleState());

      service.applyConsoleStateChanged(
        createSampleState({ showFileName: undefined, firmwareVersion: undefined }),
      );

      expect(service.showFileName()).toBeUndefined();
      expect(service.firmwareVersion()).toBeUndefined();
    });

    it('treats null optional values as absent, as the backend sends them', () => {
      service.applyConsoleStateChanged(createSampleState());

      service.applyConsoleStateChanged(
        createSampleState({
          showFileName: null,
          firmwareVersion: null,
          connection: {
            status: 'connected',
            mode: 'simulated',
            deviceDescription: 'Simulated SmartFade 1248',
            error: null,
          },
        }),
      );

      expect(service.showFileName()).toBeUndefined();
      expect(service.firmwareVersion()).toBeUndefined();
    });

    it('keeps exactly 48 channels when the snapshot lists fewer', () => {
      service.applyConsoleStateChanged(
        createSampleState({
          channels: [{ channel: 5, desired: 10, actual: 10, pending: false, output: 10 }],
        }),
      );

      expect(service.channels()).toHaveLength(CHANNEL_COUNT);
      expect(service.channels()[4]).toEqual({
        channel: 5,
        desired: 10,
        actual: 10,
        pending: false,
        output: 10,
      });
      expect(service.channels()[0]).toEqual({
        channel: 1,
        desired: 0,
        actual: 0,
        pending: false,
        output: 0,
      });
    });

    it('keeps exactly 288 memories, placed by page and fader, when the snapshot lists fewer', () => {
      service.applyConsoleStateChanged(
        createSampleState({
          memories: [
            { page: 3, fader: 2, number: 999, level: 40, recorded: true, reported: true, label: 'X' },
            { page: 13, fader: 1, number: 1, level: 40, recorded: true, reported: true, label: 'bad' },
          ],
        }),
      );

      expect(service.memories()).toHaveLength(MEMORY_COUNT);
      expect(service.memories()[49]).toEqual({
        page: 3,
        fader: 2,
        number: 50,
        level: 40,
        recorded: true,
        reported: true,
        label: 'X',
      });
      expect(service.memories()[0].recorded).toBe(false);
      expect(service.memories().filter((memory) => memory.recorded)).toHaveLength(1);
    });
  });

  describe('supported commands', () => {
    it('knows nothing until the backend reports, then answers per command', () => {
      expect(service.supports('setChannelLevel')).toBe(false);

      service.applyConsoleStateChanged(createSampleState());

      expect(service.supports('setChannelLevel')).toBe(true);
      expect(service.supports('clearMemories')).toBe(true);
      expect(service.supports('fly')).toBe(false);
      expect(service.supportedCommands()).toHaveLength(13);
    });

    it('treats a read-only device as supporting nothing', () => {
      service.applyConsoleStateChanged(createSampleState({ supportedCommands: [] }));
      expect(service.supports('setChannelLevel')).toBe(false);

      service.applyConsoleStateChanged(createSampleState({ supportedCommands: undefined }));
      expect(service.supportedCommands()).toEqual([]);
    });
  });

  describe('connectionStateChanged', () => {
    it('updates the connection and nothing else', () => {
      service.applyConsoleStateChanged(createSampleState());
      const channelsBefore = service.channels();
      const mastersBefore = service.masters();
      const memoriesBefore = service.memories();

      service.applyConnectionStateChanged({
        status: 'error',
        mode: 'replay',
        deviceDescription: 'Replay of a recording',
        error: 'The recording ended.',
      });

      expect(service.connection()).toEqual({
        status: 'error',
        mode: 'replay',
        deviceDescription: 'Replay of a recording',
        error: 'The recording ended.',
      });
      expect(service.deviceStatus()).toBe('error');
      expect(service.mode()).toBe('replay');
      expect(service.channels()).toBe(channelsBefore);
      expect(service.masters()).toBe(mastersBefore);
      expect(service.memories()).toBe(memoriesBefore);
    });
  });

  describe('channelStateChanged', () => {
    beforeEach(() => service.applyConsoleStateChanged(createSampleState()));

    it('updates the listed channels and leaves the others untouched', () => {
      const before = service.channels();

      service.applyChannelStateChanged([
        { channel: 7, desired: 100, actual: 90, pending: true, output: 90 },
        { channel: 8, desired: 40, actual: 40, pending: false, output: 20 },
      ]);

      const after = service.channels();
      expect(after[6]).toEqual({ channel: 7, desired: 100, actual: 90, pending: true, output: 90 });
      expect(after[7]).toEqual({ channel: 8, desired: 40, actual: 40, pending: false, output: 20 });
      expect(after).toHaveLength(CHANNEL_COUNT);
      expect(after[0]).toBe(before[0]);
      expect(after[2]).toBe(before[2]);
      expect(after[47]).toBe(before[47]);
    });

    it('ignores channels outside 1..48', () => {
      const before = service.channels();

      service.applyChannelStateChanged([
        { channel: 0, desired: 10, actual: 10, pending: false, output: 10 },
        { channel: 49, desired: 10, actual: 10, pending: false, output: 10 },
        { channel: 1.5, desired: 10, actual: 10, pending: false, output: 10 },
      ]);

      expect(service.channels()).toBe(before);
    });

    it('keeps levels inside 0..255 and treats a missing output as zero', () => {
      service.applyChannelStateChanged([
        { channel: 3, desired: 300, actual: -5, pending: false, output: 999 },
        { channel: 4, desired: 1, actual: 1, pending: false } as never,
      ]);

      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 255,
        actual: 0,
        pending: false,
        output: 255,
      });
      expect(service.channels()[3].output).toBe(0);
    });
  });

  describe('mastersChanged', () => {
    it('updates the masters', () => {
      service.applyMastersChanged({ master: 10, bumps: 20, blackout: true, solo: true, independent1: false, independent2: false });

      expect(service.masters()).toEqual({ master: 10, bumps: 20, blackout: true, solo: true, independent1: false, independent2: false });
    });

    it('keeps levels inside 0..255 and reads a missing solo as off', () => {
      service.applyMastersChanged({ master: 999, bumps: -1, blackout: false } as never);

      expect(service.masters()).toEqual({ master: 255, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false });
    });
  });

  describe('memoriesChanged', () => {
    beforeEach(() => service.applyConsoleStateChanged(createSampleState()));

    it('updates the listed memories and leaves the others untouched', () => {
      const before = service.memories();

      service.applyMemoriesChanged([
        { page: 1, fader: 3, number: 3, level: 255, recorded: true, reported: true, label: 'NEW' },
        { page: 12, fader: 24, number: 288, level: 7, recorded: false, reported: true, label: '' },
      ]);

      const after = service.memories();
      expect(after[2]).toEqual({
        page: 1,
        fader: 3,
        number: 3,
        level: 255,
        recorded: true,
        reported: true,
        label: 'NEW',
      });
      expect(after[287].level).toBe(7);
      expect(after).toHaveLength(MEMORY_COUNT);
      expect(after[0]).toBe(before[0]);
      expect(after[1]).toBe(before[1]);
    });

    it('places a memory by page and fader, not by the number it carries', () => {
      service.applyMemoriesChanged([
        { page: 2, fader: 1, number: 1, level: 9, recorded: true, reported: true, label: '' },
      ]);

      expect(service.memories()[24].level).toBe(9);
      expect(service.memories()[24].number).toBe(25);
      expect(service.memories()[0].level).toBe(0);
    });

    it('ignores invalid addresses and keeps levels inside 0..255', () => {
      const before = service.memories();

      service.applyMemoriesChanged([
        { page: 0, fader: 1, number: 1, level: 9, recorded: true, reported: true, label: '' },
        { page: 1, fader: 25, number: 25, level: 9, recorded: true, reported: true, label: '' },
      ]);
      expect(service.memories()).toBe(before);

      service.applyMemoriesChanged([
        { page: 1, fader: 4, number: 4, level: 300, recorded: true, reported: true, label: 'HOT' },
      ]);
      expect(service.memories()[3].level).toBe(255);
    });
  });

  describe('outputsChanged', () => {
    it('replaces the output levels and always keeps 512 entries', () => {
      service.applyOutputsChanged({ outputs: [1, 2, 3] });

      expect(service.outputs()).toHaveLength(OUTPUT_COUNT);
      expect(service.outputs().slice(0, 4)).toEqual([1, 2, 3, 0]);
    });

    it('keeps levels inside 0..255', () => {
      service.applyOutputsChanged({ outputs: [300, -1] });

      expect(service.outputs().slice(0, 2)).toEqual([255, 0]);
    });

    it('ignores a payload without an outputs list', () => {
      service.applyOutputsChanged({ outputs: [9] });
      const before = service.outputs();

      service.applyOutputsChanged({} as OutputsChangedDto);

      expect(service.outputs()).toBe(before);
    });
  });

  describe('errorOccurred', () => {
    it('keeps the last error until it is cleared', () => {
      service.applyErrorOccurred({ message: 'Channel out of range', code: 'range' });

      expect(service.lastError()).toEqual({ message: 'Channel out of range', code: 'range' });

      service.clearError();

      expect(service.lastError()).toBeUndefined();
    });

    it('leaves out a code the backend sent as null', () => {
      service.applyErrorOccurred({ message: 'Console not connected', code: null });

      expect(service.lastError()).toEqual({ message: 'Console not connected' });
    });

    it('replaces an earlier error', () => {
      service.applyErrorOccurred({ message: 'first' });
      service.applyErrorOccurred({ message: 'second' });

      expect(service.lastError()?.message).toBe('second');
    });
  });

  describe('the user’s latest request versus older reports', () => {
    beforeEach(() => service.applyConsoleStateChanged(createSampleState()));
    afterEach(() => vi.useRealTimers());

    it('is not undone by a report that is older than the request', () => {
      service.setDesiredChannelLevel(3, 100);

      service.applyChannelStateChanged([
        { channel: 3, desired: 40, actual: 40, pending: false, output: 40 },
      ]);

      // The reported actual level is taken; the desired level stays what the user asked for.
      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 100,
        actual: 40,
        pending: true,
        output: 40,
      });
    });

    it('survives a whole drag whose echoes arrive late', () => {
      for (const level of [10, 20, 30]) {
        service.setDesiredChannelLevel(3, level);
      }

      service.applyChannelStateChanged([
        { channel: 3, desired: 10, actual: 10, pending: false, output: 10 },
      ]);
      expect(service.channels()[2].desired).toBe(30);
      service.applyChannelStateChanged([
        { channel: 3, desired: 20, actual: 20, pending: false, output: 20 },
      ]);
      expect(service.channels()[2].desired).toBe(30);
      service.applyChannelStateChanged([
        { channel: 3, desired: 30, actual: 30, pending: false, output: 30 },
      ]);

      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 30,
        actual: 30,
        pending: false,
        output: 30,
      });
    });

    it('ends when a report agrees with it, after which the backend decides again', () => {
      service.setDesiredChannelLevel(3, 100);
      service.applyChannelStateChanged([
        { channel: 3, desired: 100, actual: 60, pending: true, output: 60 },
      ]);

      service.applyChannelStateChanged([
        { channel: 3, desired: 30, actual: 60, pending: true, output: 60 },
      ]);

      expect(service.channels()[2].desired).toBe(30);
    });

    it('does not hold back reports for other channels', () => {
      service.setDesiredChannelLevel(3, 100);

      service.applyChannelStateChanged([
        { channel: 4, desired: 50, actual: 50, pending: false, output: 50 },
      ]);

      expect(service.channels()[3]).toEqual({
        channel: 4,
        desired: 50,
        actual: 50,
        pending: false,
        output: 50,
      });
    });

    it('is given up after a while, in case the backend changed or refused the value', () => {
      vi.useFakeTimers();
      service.setDesiredChannelLevel(3, 100);

      vi.advanceTimersByTime(1600);
      service.applyChannelStateChanged([
        { channel: 3, desired: 40, actual: 40, pending: false, output: 40 },
      ]);

      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 40,
        actual: 40,
        pending: false,
        output: 40,
      });
    });

    it('is ended by a full snapshot, which is authoritative', () => {
      service.setDesiredChannelLevel(3, 100);
      service.setDesiredMaster(10);
      service.setDesiredBlackout(true);
      service.setDesiredMemoryLevel(1, 1, 99);

      service.applyConsoleStateChanged(createSampleState());
      service.applyChannelStateChanged([
        { channel: 3, desired: 40, actual: 40, pending: false, output: 40 },
      ]);
      service.applyMastersChanged({ master: 77, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false });
      service.applyMemoriesChanged([
        { page: 1, fader: 1, number: 1, level: 5, recorded: true, reported: true, label: '' },
      ]);

      expect(service.channels()[2].desired).toBe(40);
      expect(service.masters()).toEqual({ master: 77, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false });
      expect(service.memories()[0].level).toBe(5);
    });

    it('holds MASTER, BUMPS, BLACKOUT and SOLO the same way', () => {
      service.setDesiredMaster(50);
      service.setDesiredBumps(60);
      service.setDesiredBlackout(true);
      service.setDesiredSolo(true);

      service.applyMastersChanged({ master: 200, bumps: 33, blackout: false, solo: false, independent1: false, independent2: false });
      expect(service.masters()).toEqual({ master: 50, bumps: 60, blackout: true, solo: true, independent1: false, independent2: false });

      service.applyMastersChanged({ master: 50, bumps: 60, blackout: true, solo: true, independent1: false, independent2: false });
      service.applyMastersChanged({ master: 12, bumps: 35, blackout: false, solo: false, independent1: false, independent2: false });
      expect(service.masters()).toEqual({ master: 12, bumps: 35, blackout: false, solo: false, independent1: false, independent2: false });
    });

    it('holds a memory fader the same way', () => {
      service.setDesiredMemoryLevel(1, 2, 200);

      service.applyMemoriesChanged([
        { page: 1, fader: 2, number: 2, level: 128, recorded: true, reported: true, label: '' },
      ]);
      expect(service.memories()[1].level).toBe(200);

      service.applyMemoriesChanged([
        { page: 1, fader: 2, number: 2, level: 200, recorded: true, reported: true, label: '' },
      ]);
      service.applyMemoriesChanged([
        { page: 1, fader: 2, number: 2, level: 10, recorded: true, reported: true, label: '' },
      ]);
      expect(service.memories()[1].level).toBe(10);
    });
  });

  describe('optimistic updates', () => {
    beforeEach(() => service.applyConsoleStateChanged(createSampleState()));

    it('marks a channel pending when the desired level differs from the reported one', () => {
      service.setDesiredChannelLevel(3, 100);

      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 100,
        actual: 0,
        pending: true,
        output: 0,
      });
    });

    it('clears pending when the desired level equals the reported one', () => {
      service.setDesiredChannelLevel(2, 64);

      expect(service.channels()[1]).toEqual({
        channel: 2,
        desired: 64,
        actual: 64,
        pending: false,
        output: 64,
      });
    });

    it('does not notify when nothing changes', () => {
      const before = service.channels();
      const memoriesBefore = service.memories();

      service.setDesiredChannelLevel(1, 255);
      service.setDesiredMemoryLevel(1, 2, 128);

      expect(service.channels()).toBe(before);
      expect(service.memories()).toBe(memoriesBefore);
    });

    it('ignores channels outside 1..48 and invalid memories', () => {
      const before = service.channels();
      const memoriesBefore = service.memories();

      service.setDesiredChannelLevel(0, 10);
      service.setDesiredChannelLevel(49, 10);
      service.setDesiredMemoryLevel(13, 1, 10);
      service.setDesiredMemoryLevel(1, 0, 10);

      expect(service.channels()).toBe(before);
      expect(service.memories()).toBe(memoriesBefore);
    });

    it('sets the desired masters', () => {
      service.setDesiredMaster(33);
      service.setDesiredBumps(44);
      service.setDesiredBlackout(true);
      service.setDesiredSolo(true);

      expect(service.masters()).toEqual({ master: 33, bumps: 44, blackout: true, solo: true, independent1: false, independent2: false });
    });

    it('sets the desired memory fader level', () => {
      service.setDesiredMemoryLevel(2, 3, 77);

      expect(service.memories()[26]).toEqual({
        page: 2,
        fader: 3,
        number: 27,
        level: 77,
        recorded: false,
        reported: true,
        label: '',
      });
    });

    it('sets the desired independents and holds them against older reports', () => {
      service.setDesiredIndependent(1, true);
      service.setDesiredIndependent(3, true);

      expect(service.masters().independent1).toBe(true);
      expect(service.masters().independent2).toBe(false);

      service.applyMastersChanged({
        master: 200,
        bumps: 0,
        blackout: false,
        solo: false,
        independent1: false,
        independent2: true,
      });

      expect(service.masters().independent1).toBe(true);
      expect(service.masters().independent2).toBe(true);
    });

    it('CLEAR zeroes every channel fader and every memory fader at once', () => {
      service.setDesiredMemoryLevel(1, 3, 90);

      service.clearDesiredChannels();
      service.clearDesiredMemoryLevels();

      expect(service.channels()[0]).toEqual({
        channel: 1,
        desired: 0,
        actual: 255,
        pending: true,
        output: 255,
      });
      expect(service.channels()[2]).toEqual({
        channel: 3,
        desired: 0,
        actual: 0,
        pending: false,
        output: 0,
      });
      expect(service.memories().every((memory) => memory.level === 0)).toBe(true);

      // the clear is a request too: an older echo must not bring a level back
      service.applyChannelStateChanged([
        { channel: 1, desired: 255, actual: 255, pending: false, output: 255 },
      ]);
      service.applyMemoriesChanged([
        { page: 1, fader: 3, number: 3, level: 90, recorded: false, reported: true, label: '' },
      ]);
      expect(service.channels()[0].desired).toBe(0);
      expect(service.memories()[2].level).toBe(0);
    });
  });
});
