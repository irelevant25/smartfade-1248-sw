import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { MemoryEditService } from '../../core/memory-edit.service';
import { createSampleState } from '../../core/testing/sample-state';
import { MemoryGridComponent, memoryBumpLit } from './memory-grid.component';

const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

describe('memoryBumpLit', () => {
  it('lights an empty memory faintly, a recorded one dimmed and a contributing one at full', () => {
    expect(memoryBumpLit({ recorded: false, level: 0 })).toBe(0.14);
    expect(memoryBumpLit({ recorded: false, level: 200 })).toBe(0.14);
    expect(memoryBumpLit({ recorded: true, level: 0 })).toBe(0.45);
    expect(memoryBumpLit({ recorded: true, level: 1 })).toBe(1);
  });
});

describe('MemoryGridComponent', () => {
  let fixture: ComponentFixture<MemoryGridComponent>;
  let state: ConsoleStateService;
  let edit: MemoryEditService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let setMemoryLevel: ReturnType<typeof vi.fn>;
  let bumpMemory: ReturnType<typeof vi.fn>;
  let recordMemory: ReturnType<typeof vi.fn>;
  let getMemory: ReturnType<typeof vi.fn>;
  let storeMemory: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = (selector: string) => element().querySelector(selector)?.textContent?.trim();
  const faders = () => element().querySelectorAll('sf-channel-fader');
  const sliders = () =>
    Array.from(element().querySelectorAll<HTMLInputElement>('input[type="range"]'));
  const bumpButtons = () => Array.from(element().querySelectorAll<HTMLButtonElement>('.fader__bump'));
  const lit = (index: number) => bumpButtons()[index].style.getPropertyValue('--lit');
  const armed = (index: number) => bumpButtons()[index].classList.contains('fader__bump--armed');
  const pageButton = (page: number) =>
    Array.from(element().querySelectorAll<HTMLButtonElement>('.memories__page'))[page - 1];

  const press = (slider: HTMLInputElement, key: string) => {
    slider.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    fixture.detectChanges();
  };

  const click = (button: HTMLButtonElement) => {
    button.click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    enabled = signal(true);
    setMemoryLevel = vi.fn();
    bumpMemory = vi.fn();
    recordMemory = vi.fn().mockResolvedValue(undefined);
    getMemory = vi.fn().mockResolvedValue({
      page: 1,
      fader: 1,
      number: 1,
      label: 'GROUP 1',
      levels: new Array<number>(48).fill(0),
    });
    storeMemory = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ChannelService,
          useValue: {
            enabled: enabled.asReadonly(),
            setMemoryLevel,
            bumpMemory,
            recordMemory,
            getMemory,
            storeMemory,
          },
        },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState());
    edit = TestBed.inject(MemoryEditService);

    fixture = TestBed.createComponent(MemoryGridComponent);
    fixture.detectChanges();
  });

  it('shows the 24 memory faders of page 1 with the three red levels of the bump buttons', () => {
    expect(faders()).toHaveLength(24);
    expect(bumpButtons()[0].textContent?.trim()).toBe('1');
    expect(bumpButtons()[23].textContent?.trim()).toBe('24');
    expect(bumpButtons()[0].getAttribute('aria-label')).toBe('Bump Memory 1');

    expect(lit(0)).toBe('0.45'); // memory 1: recorded, fader at zero
    expect(text('.fader__caption')).toBe('GROUP 1');
    expect(lit(1)).toBe('1'); // memory 2: recorded and at 128
    expect(sliders()[1].valueAsNumber).toBe(128);
    expect(lit(2)).toBe('0.14'); // memory 3: empty
  });

  it('shows the fader-mode keys only for a device that accepts them and sends the taps', () => {
    expect(element().querySelector('.memories__mode')).toBeNull();

    const pressFadersMems = vi.fn();
    const pressFaders1To24 = vi.fn();
    Object.assign(TestBed.inject(ChannelService), { pressFadersMems, pressFaders1To24 });
    state.applyConsoleStateChanged(
      createSampleState({
        supportedCommands: ['pressFadersMems', 'pressFaders1To24'],
        writableControls: ['fadersMems', 'faders1To24'],
      }),
    );
    fixture.detectChanges();

    const keys = Array.from(element().querySelectorAll<HTMLButtonElement>('.memories__mode-key'));
    expect(keys.map((key) => key.textContent?.trim())).toEqual(['1-24', 'Mems']);
    expect(keys.every((key) => !key.disabled)).toBe(true);
    click(keys[1]);
    click(keys[0]);
    expect(pressFadersMems).toHaveBeenCalledTimes(1);
    expect(pressFaders1To24).toHaveBeenCalledTimes(1);
  });

  it('switches pages with bigger page keys and marks the pages that hold a memory', () => {
    expect(pageButton(1).getAttribute('aria-pressed')).toBe('true');
    expect(pageButton(1).classList.contains('memories__page--content')).toBe(true);
    expect(pageButton(2).classList.contains('memories__page--content')).toBe(true);
    expect(pageButton(3).classList.contains('memories__page--content')).toBe(false);
    expect(element().querySelectorAll('.memories__page')).toHaveLength(12);

    click(pageButton(2));

    expect(pageButton(2).getAttribute('aria-pressed')).toBe('true');
    expect(pageButton(1).getAttribute('aria-pressed')).toBe('false');
    expect(faders()).toHaveLength(24);
    expect(lit(0)).toBe('0.45'); // memory 25
    expect(text('.fader__caption')).toBe('PAGE 2');
    expect(lit(1)).toBe('0.14');
  });

  it('passes a fader move on with page and fader', () => {
    press(sliders()[4], 'PageUp');
    expect(setMemoryLevel).toHaveBeenCalledWith(1, 5, 10);

    click(pageButton(3));
    press(sliders()[0], 'End');
    expect(setMemoryLevel).toHaveBeenLastCalledWith(3, 1, 255);
  });

  it('passes a bump press and release on with page and fader', () => {
    const button = bumpButtons()[1];

    button.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    button.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));

    expect(bumpMemory.mock.calls).toEqual([
      [1, 2, true],
      [1, 2, false],
    ]);
  });

  it('follows the backend', () => {
    state.applyMemoriesChanged([
      { page: 1, fader: 3, number: 3, level: 255, recorded: true, reported: true, label: 'NEW' },
    ]);
    fixture.detectChanges();

    expect(sliders()[2].valueAsNumber).toBe(255);
    expect(lit(2)).toBe('1');
  });

  it('says so while the console has not delivered any memory', () => {
    state.applyConsoleStateChanged(
      createSampleState({
        memoriesReported: false,
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
      }),
    );
    fixture.detectChanges();

    expect(text('.memories__note')).toBe('Not delivered by the console in this read-only session');
    expect(element().querySelector('.memories__faders--unreported')).not.toBeNull();

    state.applyMemoriesChanged([
      { page: 1, fader: 1, number: 1, level: 0, recorded: true, reported: true, label: 'OPONA' },
    ]);
    fixture.detectChanges();

    expect(element().querySelector('.memories__note')).toBeNull();
  });

  it('dims the memories the console has not delivered while others have arrived', () => {
    const sample = createSampleState({
      memoriesReported: true,
      connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
    });
    state.applyConsoleStateChanged({
      ...sample,
      memories: sample.memories.map((memory) => (memory.number === 1 ? memory : { ...memory, reported: false })),
    });
    fixture.detectChanges();

    expect(text('.memories__note')).toBe('Dimmed memories were not delivered by the console in this session');
    expect(element().querySelectorAll('.memories__fader--unreported')).toHaveLength(23);
    expect(element().querySelector('.memories__faders--unreported')).toBeNull();
    expect(lit(1)).toBe('0');
  });

  it('follows the page selected on the console when the device reports it', () => {
    state.applyConsoleStateChanged(
      createSampleState({
        memoryPage: 12,
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
      }),
    );
    fixture.detectChanges();

    expect(pageButton(12).getAttribute('aria-pressed')).toBe('true');
    expect(pageButton(12).classList.contains('memories__page--console')).toBe(true);
    expect(pageButton(1).getAttribute('aria-pressed')).toBe('false');

    // the user may look at another page; the console's page stays marked
    click(pageButton(3));
    expect(pageButton(3).getAttribute('aria-pressed')).toBe('true');
    expect(pageButton(12).classList.contains('memories__page--console')).toBe(true);
  });

  it('disables the faders and bump buttons while commands are not possible', () => {
    enabled.set(false);
    fixture.detectChanges();

    expect(sliders().every((slider) => slider.disabled)).toBe(true);
    expect(bumpButtons().every((button) => button.disabled)).toBe(true);
    expect(Array.from(element().querySelectorAll<HTMLButtonElement>('.memories__page')).every((key) => key.disabled)).toBe(true);
  });

  describe('with REC MEM armed', () => {
    beforeEach(() => {
      edit.toggleRecord();
      fixture.detectChanges();
    });

    it('arms every memory of the page and records into the one pressed', async () => {
      expect(element().querySelector('.memories--armed')).not.toBeNull();
      expect(text('.memories__hint')).toContain('REC MEM');
      expect(bumpButtons().every((_, index) => armed(index))).toBe(true);

      click(bumpButtons()[4]);
      await settle();
      fixture.detectChanges();

      expect(recordMemory).toHaveBeenCalledWith(1, 5);
      expect(bumpMemory).not.toHaveBeenCalled();
      expect(edit.mode()).toBe('idle');
      expect(bumpButtons().some((_, index) => armed(index))).toBe(false);
      expect(element().querySelector('.memories__hint')).toBeNull();
    });

    it('records on the page shown at the time', async () => {
      click(pageButton(4));

      click(bumpButtons()[0]);
      await settle();

      expect(recordMemory).toHaveBeenCalledWith(4, 1);
    });
  });

  describe('with EDIT MEM armed', () => {
    beforeEach(() => {
      edit.toggleEdit();
      fixture.detectChanges();
    });

    it('arms only recorded memories and opens the one pressed for editing', async () => {
      expect(armed(0)).toBe(true);
      expect(armed(1)).toBe(true);
      expect(armed(2)).toBe(false);

      click(bumpButtons()[2]);
      expect(getMemory).not.toHaveBeenCalled();

      click(bumpButtons()[0]);
      await settle();
      fixture.detectChanges();

      expect(getMemory).toHaveBeenCalledWith(1, 1);
      expect(edit.mode()).toBe('edit');
      expect(text('.memories__hint')).toContain('store');
      expect(bumpButtons().some((_, index) => armed(index))).toBe(false);
    });
  });

  describe('with COPY armed', () => {
    beforeEach(() => {
      edit.toggleCopy();
      fixture.detectChanges();
    });

    it('arms recorded memories as sources, then every other memory as the target', async () => {
      expect(text('.memories__hint')).toContain('COPY');
      expect(armed(0)).toBe(true);
      expect(armed(2)).toBe(false);

      click(bumpButtons()[0]);
      await settle();
      fixture.detectChanges();

      expect(edit.mode()).toBe('copy-target');
      expect(text('.memories__hint')).toContain('memory 1 into');
      expect(armed(0)).toBe(false);
      expect(armed(2)).toBe(true);
      expect(armed(23)).toBe(true);

      click(bumpButtons()[2]);
      await settle();
      fixture.detectChanges();

      expect(storeMemory).toHaveBeenCalledWith(1, 3, expect.any(Array));
      expect(edit.mode()).toBe('idle');
    });
  });
});
