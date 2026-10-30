import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { MemoryEditService } from '../../core/memory-edit.service';
import { createSampleState } from '../../core/testing/sample-state';
import { ChannelGridComponent } from './channel-grid.component';

const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

function levelsWith(entries: Array<[channel: number, level: number]>): number[] {
  const levels = new Array<number>(48).fill(0);
  for (const [channel, level] of entries) {
    levels[channel - 1] = level;
  }
  return levels;
}

describe('ChannelGridComponent', () => {
  let fixture: ComponentFixture<ChannelGridComponent>;
  let state: ConsoleStateService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let setChannelLevel: ReturnType<typeof vi.fn>;
  let bumpChannel: ReturnType<typeof vi.fn>;
  let getMemory: ReturnType<typeof vi.fn>;
  let storeMemory: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = (selector: string) => element().querySelector(selector)?.textContent?.trim();
  const sliders = () =>
    Array.from(element().querySelectorAll<HTMLInputElement>('input[type="range"]'));
  const bumpButtons = () => Array.from(element().querySelectorAll<HTMLButtonElement>('.fader__bump'));

  const press = (slider: HTMLInputElement, key: string) => {
    slider.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    fixture.detectChanges();
  };

  beforeEach(() => {
    enabled = signal(true);
    setChannelLevel = vi.fn();
    bumpChannel = vi.fn();
    getMemory = vi.fn();
    storeMemory = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        // The channel service has its own tests; here only the calls matter.
        {
          provide: ChannelService,
          useValue: {
            enabled: enabled.asReadonly(),
            setChannelLevel,
            bumpChannel,
            getMemory,
            storeMemory,
            recordMemory: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState());

    fixture = TestBed.createComponent(ChannelGridComponent);
    fixture.detectChanges();
  });

  it('shows 48 faders in two banks of 24', () => {
    const banks = element().querySelectorAll('.bank');

    expect(banks).toHaveLength(2);
    expect(banks[0].querySelectorAll('sf-channel-fader')).toHaveLength(24);
    expect(banks[1].querySelectorAll('sf-channel-fader')).toHaveLength(24);
    expect(sliders()).toHaveLength(48);
    expect(banks[0].querySelector('.bank__label')?.textContent?.trim()).toBe('1–24');
    expect(banks[1].querySelector('.bank__label')?.textContent?.trim()).toBe('25–48');
  });

  it('numbers the faders by channel, on the bump buttons', () => {
    const labels = bumpButtons().map((button) => button.textContent?.trim());

    expect(labels).toHaveLength(48);
    expect(labels[0]).toBe('1');
    expect(labels[23]).toBe('24');
    expect(labels[24]).toBe('25');
    expect(labels[47]).toBe('48');
    expect(bumpButtons()[6].getAttribute('aria-label')).toBe('Bump Channel 7');
  });

  it('shows the desired and reported level of each channel', () => {
    expect(sliders()[0].valueAsNumber).toBe(255);
    expect(sliders()[1].valueAsNumber).toBe(128);
    expect(sliders()[2].valueAsNumber).toBe(0);

    const second = element().querySelectorAll('sf-channel-fader')[1];
    expect(second.querySelector('.fader__actual--shown')?.textContent?.trim()).toBe('25%');
    expect(second.querySelector('.fader__lamp--on')).not.toBeNull();
  });

  it('lights each bump button with the channel output the console reports', () => {
    expect(bumpButtons()[0].style.getPropertyValue('--lit')).toBe('1');
    expect(bumpButtons()[3].style.getPropertyValue('--lit')).toBe('0');

    state.applyChannelStateChanged([
      { channel: 4, desired: 0, actual: 0, pending: false, output: 255 },
    ]);
    fixture.detectChanges();

    expect(bumpButtons()[3].style.getPropertyValue('--lit')).toBe('1');
    expect(sliders()[3].valueAsNumber).toBe(0);
  });

  it('follows state changes pushed by the backend', () => {
    state.applyChannelStateChanged([
      { channel: 3, desired: 51, actual: 51, pending: false, output: 51 },
    ]);
    fixture.detectChanges();

    expect(sliders()[2].valueAsNumber).toBe(51);
  });

  it('passes a level change on to the channel service with the channel number', () => {
    press(sliders()[4], 'PageUp');

    expect(setChannelLevel).toHaveBeenCalledTimes(1);
    expect(setChannelLevel).toHaveBeenCalledWith(5, 10);
  });

  it('passes a bump press and release on with the channel number', () => {
    const button = bumpButtons()[6];

    button.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    button.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));

    expect(bumpChannel.mock.calls).toEqual([
      [7, true],
      [7, false],
    ]);
    expect(setChannelLevel).not.toHaveBeenCalled();
  });

  it('disables every fader and bump button while commands are not possible', () => {
    enabled.set(false);
    fixture.detectChanges();

    expect(sliders().every((slider) => slider.disabled)).toBe(true);
    expect(bumpButtons().every((button) => button.disabled)).toBe(true);

    enabled.set(true);
    fixture.detectChanges();

    expect(sliders().every((slider) => !slider.disabled)).toBe(true);
    expect(bumpButtons().every((button) => !button.disabled)).toBe(true);
  });

  describe('moving focus with the arrow keys', () => {
    it('Right moves to the next channel and Left back', () => {
      sliders()[2].focus();

      press(sliders()[2], 'ArrowRight');
      expect(document.activeElement).toBe(sliders()[3]);

      press(sliders()[3], 'ArrowLeft');
      expect(document.activeElement).toBe(sliders()[2]);
    });

    it('continues from channel 24 into the second bank', () => {
      sliders()[23].focus();

      press(sliders()[23], 'ArrowRight');

      expect(document.activeElement).toBe(sliders()[24]);
    });

    it('stays put at the first and the last channel', () => {
      sliders()[0].focus();
      press(sliders()[0], 'ArrowLeft');
      expect(document.activeElement).toBe(sliders()[0]);

      sliders()[47].focus();
      press(sliders()[47], 'ArrowRight');
      expect(document.activeElement).toBe(sliders()[47]);
    });

    it('does not change any level', () => {
      sliders()[2].focus();

      press(sliders()[2], 'ArrowRight');

      expect(setChannelLevel).not.toHaveBeenCalled();
    });
  });

  describe('while a memory is being edited (EDIT MEM)', () => {
    let edit: MemoryEditService;

    beforeEach(async () => {
      edit = TestBed.inject(MemoryEditService);
      getMemory.mockResolvedValue({
        page: 1,
        fader: 1,
        number: 1,
        label: 'GROUP 1',
        levels: levelsWith([
          [7, 200],
          [8, 100],
        ]),
      });
      edit.toggleEdit();
      await edit.pickMemory(1, 1, true);
      fixture.detectChanges();
    });

    it('shows the memory draft on the faders, with a banner', () => {
      expect(text('.grid__edit-title')).toBe('Editing memory 1 "GROUP 1" (page 1, fader 1)');
      expect(sliders()[6].valueAsNumber).toBe(200);
      expect(sliders()[7].valueAsNumber).toBe(100);
      // channel 1 is live at full, but the draft holds zero for it
      expect(sliders()[0].valueAsNumber).toBe(0);
      expect(element().querySelector('.fader__actual--shown')).toBeNull();
      expect(element().querySelector('.fader__lamp--on')).toBeNull();
    });

    it('changes the draft instead of the live channel', () => {
      press(sliders()[6], 'PageUp');

      expect(setChannelLevel).not.toHaveBeenCalled();
      expect(edit.draft()?.levels[6]).toBe(210);
      expect(sliders()[6].valueAsNumber).toBe(210);
      expect(state.channels()[6].desired).toBe(0);
    });

    it('still bumps the live channel', () => {
      const button = bumpButtons()[6];

      button.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));

      expect(bumpChannel).toHaveBeenCalledWith(7, true);
    });

    it('stores the draft and goes back to the live channels', async () => {
      press(sliders()[6], 'PageUp');

      element().querySelector<HTMLButtonElement>('.grid__edit-store')!.click();
      await settle();
      fixture.detectChanges();

      expect(storeMemory).toHaveBeenCalledTimes(1);
      const [page, fader, levels] = storeMemory.mock.calls[0] as [number, number, number[]];
      expect(page).toBe(1);
      expect(fader).toBe(1);
      expect(levels[6]).toBe(210);
      expect(levels[7]).toBe(100);
      expect(element().querySelector('.grid__edit')).toBeNull();
      expect(sliders()[0].valueAsNumber).toBe(255);
    });

    it('abandons the draft on Cancel', () => {
      press(sliders()[6], 'PageUp');

      element().querySelector<HTMLButtonElement>('.grid__edit-cancel')!.click();
      fixture.detectChanges();

      expect(storeMemory).not.toHaveBeenCalled();
      expect(element().querySelector('.grid__edit')).toBeNull();
      expect(sliders()[6].valueAsNumber).toBe(0);
      expect(sliders()[0].valueAsNumber).toBe(255);
    });
  });
});
