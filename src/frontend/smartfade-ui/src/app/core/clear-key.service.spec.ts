import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ChannelService } from './channel.service';
import { CLEAR_SERIES_MS, ClearKeyService } from './clear-key.service';
import { MemoryEditService } from './memory-edit.service';

describe('ClearKeyService', () => {
  let service: ClearKeyService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let clearChannels: ReturnType<typeof vi.fn>;
  let clearMemories: ReturnType<typeof vi.fn>;
  let cancel: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    enabled = signal(true);
    clearChannels = vi.fn();
    clearMemories = vi.fn();
    cancel = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: ChannelService, useValue: { enabled: enabled.asReadonly(), clearChannels, clearMemories } },
        { provide: MemoryEditService, useValue: { cancel } },
      ],
    });
    service = TestBed.inject(ClearKeyService);
  });

  afterEach(() => vi.useRealTimers());

  it('aborts an armed function on the first press, clears the channel faders on the second and the memory faders on the third', () => {
    service.press();
    expect(cancel).toHaveBeenCalledTimes(1);
    expect(clearChannels).not.toHaveBeenCalled();
    expect(service.presses()).toBe(1);

    service.press();
    expect(clearChannels).toHaveBeenCalledTimes(1);
    expect(clearMemories).not.toHaveBeenCalled();
    expect(service.presses()).toBe(2);

    service.press();
    expect(clearMemories).toHaveBeenCalledTimes(1);
    expect(service.presses()).toBe(3);

    service.press();
    expect(clearChannels).toHaveBeenCalledTimes(1);
    expect(clearMemories).toHaveBeenCalledTimes(1);
  });

  it('starts a new series after a pause', () => {
    service.press();
    vi.advanceTimersByTime(CLEAR_SERIES_MS + 1);
    expect(service.presses()).toBe(0);

    service.press();

    expect(cancel).toHaveBeenCalledTimes(2);
    expect(clearChannels).not.toHaveBeenCalled();
  });

  it('keeps a series going while the presses come quickly', () => {
    service.press();
    vi.advanceTimersByTime(CLEAR_SERIES_MS - 1);
    service.press();

    expect(clearChannels).toHaveBeenCalledTimes(1);
  });

  it('does nothing while commands are not possible', () => {
    enabled.set(false);

    service.press();
    service.press();

    expect(cancel).not.toHaveBeenCalled();
    expect(clearChannels).not.toHaveBeenCalled();
    expect(service.presses()).toBe(0);
  });
});
