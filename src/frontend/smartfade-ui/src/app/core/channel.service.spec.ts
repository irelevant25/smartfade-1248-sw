import { signal, type WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Mock } from 'vitest';
import { ChannelService } from './channel.service';
import { ConsoleConnectionService, type LinkStatus } from './console-connection.service';
import { ConsoleStateService } from './console-state.service';
import type { MemoryDto } from './console.models';
import { FRAME_SCHEDULER, type FrameScheduler } from './frame-scheduler';
import { createSampleState } from './testing/sample-state';

/** Animation frames that only run when the test says so. */
class ManualFrames implements FrameScheduler {
  private _nextHandle = 1;
  private readonly _callbacks = new Map<number, () => void>();

  /** Callbacks waiting for the next frame. */
  get pending(): number {
    return this._callbacks.size;
  }

  request(callback: () => void): number {
    const handle = this._nextHandle++;
    this._callbacks.set(handle, callback);
    return handle;
  }

  cancel(handle: number): void {
    this._callbacks.delete(handle);
  }

  /** Runs one animation frame. */
  frame(): void {
    const callbacks = [...this._callbacks.values()];
    this._callbacks.clear();
    callbacks.forEach((callback) => callback());
  }
}

describe('ChannelService', () => {
  let frames: ManualFrames;
  let link: WritableSignal<LinkStatus>;
  let sendChannelLevel: Mock<(channel: number, level: number) => Promise<void>>;
  let sendMaster: Mock<(level: number) => Promise<void>>;
  let sendBumps: Mock<(level: number) => Promise<void>>;
  let sendBlackout: Mock<(on: boolean) => Promise<void>>;
  let sendSolo: Mock<(on: boolean) => Promise<void>>;
  let sendBumpChannel: Mock<(channel: number, pressed: boolean) => Promise<void>>;
  let sendMemoryLevel: Mock<(page: number, fader: number, level: number) => Promise<void>>;
  let sendBumpMemory: Mock<(page: number, fader: number, pressed: boolean) => Promise<void>>;
  let sendRecordMemory: Mock<(page: number, fader: number) => Promise<void>>;
  let sendStoreMemory: Mock<(page: number, fader: number, levels: number[]) => Promise<void>>;
  let getMemory: Mock<(page: number, fader: number) => Promise<MemoryDto | undefined>>;
  let sendIndependent: Mock<(number: number, on: boolean) => Promise<void>>;
  let sendClearChannels: Mock<() => Promise<void>>;
  let sendClearMemories: Mock<() => Promise<void>>;
  let state: ConsoleStateService;
  let service: ChannelService;

  beforeEach(() => {
    frames = new ManualFrames();
    link = signal<LinkStatus>('connected');
    sendChannelLevel = vi.fn().mockResolvedValue(undefined);
    sendMaster = vi.fn().mockResolvedValue(undefined);
    sendBumps = vi.fn().mockResolvedValue(undefined);
    sendBlackout = vi.fn().mockResolvedValue(undefined);
    sendSolo = vi.fn().mockResolvedValue(undefined);
    sendBumpChannel = vi.fn().mockResolvedValue(undefined);
    sendMemoryLevel = vi.fn().mockResolvedValue(undefined);
    sendBumpMemory = vi.fn().mockResolvedValue(undefined);
    sendRecordMemory = vi.fn().mockResolvedValue(undefined);
    sendStoreMemory = vi.fn().mockResolvedValue(undefined);
    getMemory = vi.fn().mockResolvedValue(undefined);
    sendIndependent = vi.fn().mockResolvedValue(undefined);
    sendClearChannels = vi.fn().mockResolvedValue(undefined);
    sendClearMemories = vi.fn().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        { provide: FRAME_SCHEDULER, useValue: frames },
        {
          // The hub connection is mocked: the backend is never needed.
          provide: ConsoleConnectionService,
          useValue: {
            status: link.asReadonly(),
            sendChannelLevel,
            sendMaster,
            sendBumps,
            sendBlackout,
            sendSolo,
            sendBumpChannel,
            sendMemoryLevel,
            sendBumpMemory,
            sendRecordMemory,
            sendStoreMemory,
            getMemory,
            sendIndependent,
            sendClearChannels,
            sendClearMemories,
          },
        },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState());
    service = TestBed.inject(ChannelService);
  });

  describe('setChannelLevel', () => {
    it('sends two rapid updates for the same channel once, with the last value', () => {
      service.setChannelLevel(5, 10);
      service.setChannelLevel(5, 20);

      expect(sendChannelLevel).not.toHaveBeenCalled();

      frames.frame();

      expect(sendChannelLevel).toHaveBeenCalledTimes(1);
      expect(sendChannelLevel).toHaveBeenCalledWith(5, 20);
    });

    it('coalesces a whole drag into one update per frame', () => {
      for (let level = 1; level <= 100; level++) {
        service.setChannelLevel(9, level);
      }
      expect(frames.pending).toBe(1);

      frames.frame();

      expect(sendChannelLevel).toHaveBeenCalledTimes(1);
      expect(sendChannelLevel).toHaveBeenCalledWith(9, 100);
    });

    it('sends each channel once per frame when several move together', () => {
      service.setChannelLevel(1, 10);
      service.setChannelLevel(2, 20);
      service.setChannelLevel(1, 30);

      frames.frame();

      expect(sendChannelLevel).toHaveBeenCalledTimes(2);
      expect(sendChannelLevel).toHaveBeenCalledWith(1, 30);
      expect(sendChannelLevel).toHaveBeenCalledWith(2, 20);
    });

    it('starts a new coalescing window after each frame', () => {
      service.setChannelLevel(5, 10);
      frames.frame();
      service.setChannelLevel(5, 20);
      frames.frame();

      expect(sendChannelLevel.mock.calls).toEqual([
        [5, 10],
        [5, 20],
      ]);
    });

    it('sends nothing when a frame runs and no level was requested', () => {
      frames.frame();

      expect(sendChannelLevel).not.toHaveBeenCalled();
      expect(sendMaster).not.toHaveBeenCalled();
      expect(sendBumps).not.toHaveBeenCalled();
      expect(sendMemoryLevel).not.toHaveBeenCalled();
    });

    it('updates desired and pending at once, before anything is sent', () => {
      service.setChannelLevel(5, 120);

      expect(state.channels()[4]).toEqual({
        channel: 5,
        desired: 120,
        actual: 0,
        pending: true,
        output: 0,
      });
      expect(sendChannelLevel).not.toHaveBeenCalled();
    });

    it('leaves the reported level alone', () => {
      service.setChannelLevel(1, 10);

      expect(state.channels()[0].actual).toBe(255);
      expect(state.channels()[0].output).toBe(255);
    });

    it('rounds and clamps levels to 0..255', () => {
      service.setChannelLevel(3, 999.4);
      service.setChannelLevel(4, -20);
      service.setChannelLevel(6, 12.6);

      frames.frame();

      expect(sendChannelLevel).toHaveBeenCalledWith(3, 255);
      expect(sendChannelLevel).toHaveBeenCalledWith(4, 0);
      expect(sendChannelLevel).toHaveBeenCalledWith(6, 13);
    });

    it('ignores channels outside 1..48', () => {
      service.setChannelLevel(0, 10);
      service.setChannelLevel(49, 10);
      service.setChannelLevel(1.5, 10);

      expect(frames.pending).toBe(0);
      expect(sendChannelLevel).not.toHaveBeenCalled();
    });

    it('does nothing while the link to the backend is down', () => {
      link.set('reconnecting');

      service.setChannelLevel(5, 10);
      frames.frame();

      expect(service.enabled()).toBe(false);
      expect(sendChannelLevel).not.toHaveBeenCalled();
      expect(state.channels()[4].desired).toBe(0);
    });

    it('does nothing while the console is not connected', () => {
      state.applyConnectionStateChanged({
        status: 'disconnected',
        mode: 'simulated',
        deviceDescription: 'Simulated SmartFade 1248',
      });

      service.setChannelLevel(5, 10);
      frames.frame();

      expect(service.enabled()).toBe(false);
      expect(sendChannelLevel).not.toHaveBeenCalled();
    });
  });

  describe('setMaster', () => {
    it('coalesces rapid updates into one with the last value', () => {
      service.setMaster(10);
      service.setMaster(50);
      service.setMaster(90);

      expect(state.masters().master).toBe(90);
      expect(sendMaster).not.toHaveBeenCalled();

      frames.frame();

      expect(sendMaster).toHaveBeenCalledTimes(1);
      expect(sendMaster).toHaveBeenCalledWith(90);
    });

    it('shares the frame with channel updates', () => {
      service.setMaster(90);
      service.setChannelLevel(2, 7);

      expect(frames.pending).toBe(1);
      frames.frame();

      expect(sendMaster).toHaveBeenCalledWith(90);
      expect(sendChannelLevel).toHaveBeenCalledWith(2, 7);
    });
  });

  describe('setBumps', () => {
    it('applies the level at once and sends the last value per frame', () => {
      service.setBumps(10);
      service.setBumps(300);

      expect(state.masters().bumps).toBe(255);
      expect(sendBumps).not.toHaveBeenCalled();

      frames.frame();

      expect(sendBumps).toHaveBeenCalledTimes(1);
      expect(sendBumps).toHaveBeenCalledWith(255);
      expect(sendMaster).not.toHaveBeenCalled();
    });

    it('does nothing while commands are not possible', () => {
      link.set('disconnected');

      service.setBumps(10);
      frames.frame();

      expect(sendBumps).not.toHaveBeenCalled();
      expect(state.masters().bumps).toBe(255);
    });
  });

  describe('setBlackout', () => {
    it('applies the toggle at once and sends it right away', () => {
      service.setBlackout(true);

      expect(state.masters().blackout).toBe(true);
      expect(sendBlackout).toHaveBeenCalledTimes(1);
      expect(sendBlackout).toHaveBeenCalledWith(true);
      expect(frames.pending).toBe(0);
    });

    it('does nothing while commands are not possible', () => {
      link.set('disconnected');

      service.setBlackout(true);

      expect(state.masters().blackout).toBe(false);
      expect(sendBlackout).not.toHaveBeenCalled();
    });
  });

  describe('setSolo', () => {
    it('applies the toggle at once and sends it right away', () => {
      service.setSolo(true);

      expect(state.masters().solo).toBe(true);
      expect(sendSolo).toHaveBeenCalledTimes(1);
      expect(sendSolo).toHaveBeenCalledWith(true);
      expect(frames.pending).toBe(0);
    });

    it('does nothing while commands are not possible', () => {
      link.set('disconnected');

      service.setSolo(true);

      expect(state.masters().solo).toBe(false);
      expect(sendSolo).not.toHaveBeenCalled();
    });
  });

  describe('setIndependent', () => {
    it('applies the toggle at once and sends it right away', () => {
      service.setIndependent(2, true);

      expect(state.masters().independent2).toBe(true);
      expect(state.masters().independent1).toBe(false);
      expect(sendIndependent).toHaveBeenCalledWith(2, true);
      expect(frames.pending).toBe(0);
    });

    it('ignores independents other than 1 and 2, and a dead link', () => {
      service.setIndependent(0, true);
      service.setIndependent(3, true);
      link.set('disconnected');
      service.setIndependent(1, true);

      expect(sendIndependent).not.toHaveBeenCalled();
      expect(state.masters().independent1).toBe(false);
    });
  });

  describe('clearChannels and clearMemories', () => {
    it('zero every channel fader at once, drop queued moves and send the clear', () => {
      service.setChannelLevel(5, 120);

      service.clearChannels();
      frames.frame();

      expect(state.channels().every((channel) => channel.desired === 0)).toBe(true);
      expect(state.channels()[0]).toEqual({
        channel: 1,
        desired: 0,
        actual: 255,
        pending: true,
        output: 255,
      });
      expect(sendClearChannels).toHaveBeenCalledTimes(1);
      expect(sendChannelLevel).not.toHaveBeenCalled();
    });

    it('zero every memory fader at once and send the clear', () => {
      service.setMemoryLevel(1, 3, 90);

      service.clearMemories();
      frames.frame();

      expect(state.memories().every((memory) => memory.level === 0)).toBe(true);
      expect(sendClearMemories).toHaveBeenCalledTimes(1);
      expect(sendMemoryLevel).not.toHaveBeenCalled();
    });

    it('do nothing while commands are not possible', () => {
      link.set('disconnected');

      service.clearChannels();
      service.clearMemories();

      expect(sendClearChannels).not.toHaveBeenCalled();
      expect(sendClearMemories).not.toHaveBeenCalled();
      expect(state.channels()[0].desired).toBe(255);
    });
  });

  describe('bumpChannel', () => {
    it('sends the press and the release right away, in order', () => {
      service.bumpChannel(7, true);
      service.bumpChannel(7, false);

      expect(sendBumpChannel.mock.calls).toEqual([
        [7, true],
        [7, false],
      ]);
      expect(frames.pending).toBe(0);
    });

    it('ignores channels outside 1..48 and a dead link', () => {
      service.bumpChannel(0, true);
      service.bumpChannel(49, true);
      link.set('disconnected');
      service.bumpChannel(1, true);

      expect(sendBumpChannel).not.toHaveBeenCalled();
    });
  });

  describe('setMemoryLevel', () => {
    it('applies the level at once and sends each memory once per frame with the last value', () => {
      service.setMemoryLevel(1, 3, 10);
      service.setMemoryLevel(1, 3, 20);
      service.setMemoryLevel(2, 3, 30);

      expect(state.memories()[2].level).toBe(20);
      expect(state.memories()[24 + 2].level).toBe(30);
      expect(sendMemoryLevel).not.toHaveBeenCalled();
      expect(frames.pending).toBe(1);

      frames.frame();

      expect(sendMemoryLevel.mock.calls).toEqual([
        [1, 3, 20],
        [2, 3, 30],
      ]);
    });

    it('shares the frame with the other faders and clamps the level', () => {
      service.setMemoryLevel(12, 24, 999);
      service.setChannelLevel(2, 7);
      service.setBumps(1);

      expect(frames.pending).toBe(1);
      frames.frame();

      expect(sendMemoryLevel).toHaveBeenCalledWith(12, 24, 255);
      expect(sendChannelLevel).toHaveBeenCalledWith(2, 7);
      expect(sendBumps).toHaveBeenCalledWith(1);
    });

    it('ignores memories outside page 1..12 and fader 1..24', () => {
      service.setMemoryLevel(0, 1, 10);
      service.setMemoryLevel(13, 1, 10);
      service.setMemoryLevel(1, 0, 10);
      service.setMemoryLevel(1, 25, 10);

      expect(frames.pending).toBe(0);
      expect(sendMemoryLevel).not.toHaveBeenCalled();
    });
  });

  describe('bumpMemory', () => {
    it('sends the press and the release right away', () => {
      service.bumpMemory(2, 5, true);
      service.bumpMemory(2, 5, false);

      expect(sendBumpMemory.mock.calls).toEqual([
        [2, 5, true],
        [2, 5, false],
      ]);
    });

    it('ignores an invalid memory', () => {
      service.bumpMemory(0, 5, true);

      expect(sendBumpMemory).not.toHaveBeenCalled();
    });
  });

  describe('memories', () => {
    it('records into a memory right away', async () => {
      await service.recordMemory(3, 4);

      expect(sendRecordMemory).toHaveBeenCalledWith(3, 4);
    });

    it('stores 48 clamped levels into a memory', async () => {
      const levels = new Array<number>(48).fill(0);
      levels[0] = 300;
      levels[1] = 12.4;

      await service.storeMemory(1, 1, levels);

      const sent = sendStoreMemory.mock.calls[0][2];
      expect(sent).toHaveLength(48);
      expect(sent[0]).toBe(255);
      expect(sent[1]).toBe(12);
    });

    it('refuses to store a wrong number of levels', async () => {
      await service.storeMemory(1, 1, [1, 2, 3]);

      expect(sendStoreMemory).not.toHaveBeenCalled();
    });

    it('reads a memory', async () => {
      const memory: MemoryDto = {
        page: 1,
        fader: 1,
        number: 1,
        label: '',
        levels: new Array<number>(48).fill(0),
      };
      getMemory.mockResolvedValue(memory);

      expect(await service.getMemory(1, 1)).toBe(memory);
      expect(getMemory).toHaveBeenCalledWith(1, 1);
    });

    it('does nothing while commands are not possible', async () => {
      link.set('disconnected');

      await service.recordMemory(1, 1);
      await service.storeMemory(1, 1, new Array<number>(48).fill(0));
      expect(await service.getMemory(1, 1)).toBeUndefined();

      expect(sendRecordMemory).not.toHaveBeenCalled();
      expect(sendStoreMemory).not.toHaveBeenCalled();
      expect(getMemory).not.toHaveBeenCalled();
    });
  });
});
