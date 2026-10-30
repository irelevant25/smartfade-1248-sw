import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Mock } from 'vitest';
import { ChannelService } from './channel.service';
import type { MemoryDto } from './console.models';
import { MemoryEditService } from './memory-edit.service';

const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

function levelsWith(entries: Array<[channel: number, level: number]>): number[] {
  const levels = new Array<number>(48).fill(0);
  for (const [channel, level] of entries) {
    levels[channel - 1] = level;
  }
  return levels;
}

describe('MemoryEditService', () => {
  let service: MemoryEditService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let recordMemory: Mock<(page: number, fader: number) => Promise<void>>;
  let getMemory: Mock<(page: number, fader: number) => Promise<MemoryDto | undefined>>;
  let storeMemory: Mock<(page: number, fader: number, levels: readonly number[]) => Promise<void>>;

  const memory: MemoryDto = {
    page: 1,
    fader: 2,
    number: 2,
    label: 'WASH',
    levels: levelsWith([[3, 200]]),
  };

  beforeEach(() => {
    enabled = signal(true);
    recordMemory = vi.fn().mockResolvedValue(undefined);
    getMemory = vi.fn().mockResolvedValue(memory);
    storeMemory = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ChannelService,
          useValue: { enabled: enabled.asReadonly(), recordMemory, getMemory, storeMemory },
        },
      ],
    });
    service = TestBed.inject(MemoryEditService);
  });

  it('starts idle', () => {
    expect(service.mode()).toBe('idle');
    expect(service.draft()).toBeUndefined();
    expect(service.editing()).toBeUndefined();
    expect(service.loading()).toBe(false);
  });

  it('treats a pick while nothing is armed as an ordinary bump', async () => {
    expect(await service.pickMemory(1, 1, true)).toBe(false);

    expect(recordMemory).not.toHaveBeenCalled();
    expect(getMemory).not.toHaveBeenCalled();
  });

  describe('REC MEM', () => {
    it('records into the memory picked next, then disarms', async () => {
      service.toggleRecord();
      expect(service.mode()).toBe('record');

      expect(await service.pickMemory(2, 3, false)).toBe(true);

      expect(recordMemory).toHaveBeenCalledWith(2, 3);
      expect(service.mode()).toBe('idle');
    });

    it('is disarmed by a second press and by CLEAR', () => {
      service.toggleRecord();
      service.toggleRecord();
      expect(service.mode()).toBe('idle');

      service.toggleRecord();
      service.cancel();
      expect(service.mode()).toBe('idle');
    });

    it('ignores an invalid memory address', async () => {
      service.toggleRecord();

      expect(await service.pickMemory(13, 1, false)).toBe(false);
      expect(await service.pickMemory(1, 0, false)).toBe(false);

      expect(recordMemory).not.toHaveBeenCalled();
      expect(service.mode()).toBe('record');
    });
  });

  describe('EDIT MEM', () => {
    it('loads the picked memory into a draft', async () => {
      service.toggleEdit();
      expect(service.mode()).toBe('select');

      expect(await service.pickMemory(1, 2, true)).toBe(true);

      expect(getMemory).toHaveBeenCalledWith(1, 2);
      expect(service.mode()).toBe('edit');
      expect(service.draft()).toEqual({
        page: 1,
        fader: 2,
        number: 2,
        label: 'WASH',
        levels: levelsWith([[3, 200]]),
      });
      expect(service.editing()).toBe(service.draft());
      expect(service.loading()).toBe(false);
    });

    it('only opens recorded memories', async () => {
      service.toggleEdit();

      expect(await service.pickMemory(1, 2, false)).toBe(true);

      expect(getMemory).not.toHaveBeenCalled();
      expect(service.mode()).toBe('select');
    });

    it('goes idle when the memory cannot be read', async () => {
      getMemory.mockResolvedValue(undefined);
      service.toggleEdit();

      await service.pickMemory(1, 2, true);

      expect(service.mode()).toBe('idle');
      expect(service.draft()).toBeUndefined();
    });

    it('drops a read that completes after CLEAR', async () => {
      let resolve: (value: MemoryDto) => void = () => undefined;
      getMemory.mockReturnValue(new Promise<MemoryDto>((r) => (resolve = r)));
      service.toggleEdit();
      const pick = service.pickMemory(1, 2, true);
      expect(service.loading()).toBe(true);

      service.cancel();
      resolve(memory);
      await pick;

      expect(service.mode()).toBe('idle');
      expect(service.draft()).toBeUndefined();
    });

    it('edits the draft and stores it on the second EDIT MEM press', async () => {
      service.toggleEdit();
      await service.pickMemory(1, 2, true);

      service.setDraftLevel(5, 300);
      service.setDraftLevel(0, 10);
      service.setDraftLevel(49, 10);
      expect(service.draft()?.levels).toEqual(
        levelsWith([
          [3, 200],
          [5, 255],
        ]),
      );

      service.toggleEdit();
      await settle();

      expect(storeMemory).toHaveBeenCalledWith(
        1,
        2,
        levelsWith([
          [3, 200],
          [5, 255],
        ]),
      );
      expect(service.mode()).toBe('idle');
      expect(service.draft()).toBeUndefined();
    });

    it('abandons the draft on CLEAR', async () => {
      service.toggleEdit();
      await service.pickMemory(1, 2, true);
      service.setDraftLevel(5, 100);

      service.cancel();

      expect(storeMemory).not.toHaveBeenCalled();
      expect(service.mode()).toBe('idle');
      expect(service.draft()).toBeUndefined();
    });

    it('ignores draft changes while not editing', () => {
      service.setDraftLevel(5, 100);

      expect(service.draft()).toBeUndefined();
    });
  });

  describe('COPY', () => {
    it('reads the source picked first and stores it into the memory picked next', async () => {
      service.toggleCopy();
      expect(service.mode()).toBe('copy-source');
      expect(service.armed()).toBe(true);

      expect(await service.pickMemory(1, 2, true)).toBe(true);

      expect(getMemory).toHaveBeenCalledWith(1, 2);
      expect(service.mode()).toBe('copy-target');
      expect(service.copySource()).toEqual({
        page: 1,
        fader: 2,
        number: 2,
        levels: levelsWith([[3, 200]]),
      });

      expect(await service.pickMemory(4, 7, false)).toBe(true);

      expect(storeMemory).toHaveBeenCalledWith(4, 7, levelsWith([[3, 200]]));
      expect(service.mode()).toBe('idle');
      expect(service.copySource()).toBeUndefined();
    });

    it('takes only a recorded memory as the source and never copies onto itself', async () => {
      service.toggleCopy();

      await service.pickMemory(1, 2, false);
      expect(getMemory).not.toHaveBeenCalled();
      expect(service.mode()).toBe('copy-source');

      await service.pickMemory(1, 2, true);
      await service.pickMemory(1, 2, true);
      expect(storeMemory).not.toHaveBeenCalled();
      expect(service.mode()).toBe('copy-target');
    });

    it('is disarmed by a second press and by CLEAR', async () => {
      service.toggleCopy();
      service.toggleCopy();
      expect(service.mode()).toBe('idle');

      service.toggleCopy();
      await service.pickMemory(1, 2, true);
      service.cancel();
      expect(service.mode()).toBe('idle');
      expect(service.copySource()).toBeUndefined();
    });

    it('arming another function drops a copy in progress', async () => {
      service.toggleCopy();
      await service.pickMemory(1, 2, true);

      service.toggleRecord();

      expect(service.mode()).toBe('record');
      expect(service.copySource()).toBeUndefined();
    });
  });

  it('does not arm while the console is not connected', () => {
    enabled.set(false);

    service.toggleRecord();
    expect(service.mode()).toBe('idle');

    service.toggleEdit();
    expect(service.mode()).toBe('idle');
  });

  it('is cancelled when the console goes away', async () => {
    service.toggleEdit();
    await service.pickMemory(1, 2, true);
    expect(service.mode()).toBe('edit');

    enabled.set(false);
    TestBed.inject(ApplicationRef).tick();

    expect(service.mode()).toBe('idle');
    expect(service.draft()).toBeUndefined();
  });
});
