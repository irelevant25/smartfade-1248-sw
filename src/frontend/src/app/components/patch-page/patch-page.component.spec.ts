import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShowStore } from '../../core/show.store';
import { sampleShow } from '../../core/testing/sample-show';
import { PatchPageComponent } from './patch-page.component';

describe('PatchPageComponent', () => {
  let fixture: ComponentFixture<PatchPageComponent>;
  let store: ShowStore;

  const element = () => fixture.nativeElement as HTMLElement;
  const box = (dmx: number) => element().querySelector<HTMLElement>(`[data-dmx="${dmx}"]`)!;
  const boxes = () => Array.from(element().querySelectorAll<HTMLElement>('.patch__box'));
  const grid = () => element().querySelector<HTMLElement>('.patch__matrix')!;
  const editor = () => element().querySelector<HTMLInputElement>('.patch__editor');
  const pointer = (target: Element, type: string, init: MouseEventInit = {}) => {
    target.dispatchEvent(new MouseEvent(type, { bubbles: true, button: 0, ...init }));
    fixture.detectChanges();
  };
  const clickBox = (dmx: number) => {
    pointer(box(dmx), 'pointerdown');
    pointer(box(dmx), 'pointerup');
  };
  const key = (target: Element, keyName: string, init: KeyboardEventInit = {}) => {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: keyName, bubbles: true, cancelable: true, ...init }));
    fixture.detectChanges();
  };
  const type = (input: HTMLInputElement, value: string) => {
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  };
  const change = (input: HTMLInputElement, value: string) => {
    input.value = value;
    input.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
  };
  const channelOf = (dmx: number) => store.show()!.patch[dmx - 1].channel;

  beforeEach(() => {
    store = TestBed.inject(ShowStore);
    fixture = TestBed.createComponent(PatchPageComponent);
  });

  it('shows one message when no show is loaded', () => {
    fixture.detectChanges();

    expect(element().querySelector('.patch__empty')?.textContent?.trim()).toBe(
      "No show loaded. Open a file (Show menu); the console's own show will appear here at Connect once that is cleared.",
    );
    expect(element().querySelector('.patch__matrix')).toBeNull();
  });

  describe('with a show', () => {
    beforeEach(() => {
      store.setShow(sampleShow(), 'SHOW07.ASC', 'file');
      fixture.detectChanges();
    });

    it('renders the 512 addresses in 16 rows of 32, IND 1 / IND 2 for channels 49 and 50, a colour per channel, and the summary', () => {
      expect(boxes()).toHaveLength(512);
      expect(element().querySelectorAll('.patch__row')).toHaveLength(16);
      expect(element().querySelectorAll('.patch__row')[0].querySelectorAll('.patch__box')).toHaveLength(32);
      expect(box(1).querySelector('.patch__dmx')?.textContent).toBe('1');
      expect(box(1).querySelector('.patch__channel')?.textContent).toBe('1');
      expect(box(47).querySelector('.patch__channel')?.textContent).toBe('IND 2');
      expect(box(47).classList.contains('patch__box--ind')).toBe(true);
      expect(box(97).querySelector('.patch__channel')?.textContent).toBe('IND 1');
      expect(box(100).querySelector('.patch__channel')?.textContent).toBe('');
      expect(box(100).classList.contains('patch__box--empty')).toBe(true);
      expect(box(1).getAttribute('aria-label')).toBe('DMX 1, 1');
      expect(box(47).getAttribute('aria-label')).toBe('DMX 47, IND 2');
      expect(box(100).getAttribute('aria-label')).toBe('DMX 100, unpatched');
      expect(box(1).style.getPropertyValue('--hue')).not.toBe('');
      expect(box(1).style.getPropertyValue('--hue')).not.toBe(box(2).style.getPropertyValue('--hue'));
      expect(box(100).style.getPropertyValue('--hue')).toBe('');
      expect(boxes().filter((b) => b.getAttribute('tabindex') === '0')).toHaveLength(1); // one tab stop: DMX 1 until a box is chosen
      expect(box(1).getAttribute('tabindex')).toBe('0');

      const summary = element().querySelector('.patch__summary')?.textContent?.replace(/\s+/g, ' ');
      expect(summary).toContain('49 patched, 463 unpatched');
      expect(summary).toContain('without an address: channel 47');
      expect(summary).toContain('IND 1: DMX 97');
      expect(summary).toContain('IND 2: DMX 47');
    });

    it('opens an inline input on a click; Enter applies a channel or i1 / i2, Esc cancels, a wrong text changes nothing, Delete unpatches', () => {
      clickBox(1);
      expect(editor()?.value).toBe('1');
      expect(editor()?.getAttribute('aria-label')).toBe('Channel for DMX 1');
      type(editor()!, '12');
      key(editor()!, 'Enter');
      expect(editor()).toBeNull();
      expect(channelOf(1)).toBe(12);
      expect(box(1).querySelector('.patch__channel')?.textContent).toBe('12');

      clickBox(2);
      type(editor()!, 'i1');
      key(editor()!, 'Enter');
      expect(channelOf(2)).toBe(49);
      expect(box(2).querySelector('.patch__channel')?.textContent).toBe('IND 1');

      clickBox(3);
      type(editor()!, 'x');
      key(editor()!, 'Enter');
      expect(channelOf(3)).toBe(3);
      expect(element().querySelector('.patch__notice')?.textContent).toContain('"x" is not a target');

      clickBox(4);
      type(editor()!, '20');
      key(editor()!, 'Escape');
      expect(editor()).toBeNull();
      expect(channelOf(4)).toBe(4);

      clickBox(5);
      type(editor()!, '0');
      key(editor()!, 'Enter');
      expect(channelOf(5)).toBe(0);

      clickBox(6);
      key(editor()!, 'Escape');
      key(grid(), 'Delete'); // the focused box (DMX 6) is the selection
      expect(channelOf(6)).toBe(0);
      expect(store.dirty()).toBe(true);
    });

    it('applies a typed-in channel when the input loses focus', () => {
      clickBox(8);
      type(editor()!, '30');
      editor()!.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(editor()).toBeNull();
      expect(channelOf(8)).toBe(30);
    });

    it('selects a run of boxes with a drag and patches or unpatches it from the toolbar', () => {
      pointer(box(100), 'pointerdown');
      pointer(box(101), 'pointerover');
      pointer(box(102), 'pointerover');
      pointer(box(103), 'pointerover');
      pointer(box(103), 'pointerup');

      expect(editor()).toBeNull(); // a drag opens no input
      expect(element().querySelector('.patch__selection')?.textContent?.trim()).toBe('DMX 100-103 (4 addresses)');
      expect(boxes().filter((b) => b.getAttribute('aria-selected') === 'true').map((b) => b.dataset['dmx'])).toEqual(['100', '101', '102', '103']);

      type(element().querySelector<HTMLInputElement>('.patch__target')!, 'i2');
      element().querySelector<HTMLButtonElement>('.patch__patch-range')!.click();
      fixture.detectChanges();
      expect([100, 101, 102, 103].map(channelOf)).toEqual([50, 50, 50, 50]);
      expect(box(103).querySelector('.patch__channel')?.textContent).toBe('IND 2');

      element().querySelector<HTMLButtonElement>('.patch__unpatch-range')!.click();
      fixture.detectChanges();
      expect([100, 101, 102, 103].map(channelOf)).toEqual([0, 0, 0, 0]);

      type(element().querySelector<HTMLInputElement>('.patch__target')!, '77');
      element().querySelector<HTMLButtonElement>('.patch__patch-range')!.click();
      fixture.detectChanges();
      expect(element().querySelector('.patch__notice')?.textContent).toContain('"77" is not a channel');
      expect(channelOf(100)).toBe(0);
    });

    it('moves between boxes with the arrow keys, Shift extends the selection, Home and End go to the row ends', () => {
      key(grid(), 'ArrowRight'); // from the default DMX 1
      expect(box(2).getAttribute('tabindex')).toBe('0');
      expect(box(1).getAttribute('tabindex')).toBe('-1');
      key(grid(), 'ArrowDown');
      expect(box(34).getAttribute('tabindex')).toBe('0');
      expect(element().querySelector('.patch__selection')?.textContent?.trim()).toBe('DMX 34');

      key(grid(), 'ArrowRight', { shiftKey: true });
      key(grid(), 'ArrowRight', { shiftKey: true });
      expect(element().querySelector('.patch__selection')?.textContent?.trim()).toBe('DMX 34-36 (3 addresses)');
      expect(boxes().filter((b) => b.getAttribute('aria-selected') === 'true')).toHaveLength(3);
      expect(element().querySelector('.patch__detail-title')?.textContent).toBe('DMX 36');

      key(grid(), 'End');
      expect(box(64).getAttribute('tabindex')).toBe('0');
      key(grid(), 'Home');
      expect(box(33).getAttribute('tabindex')).toBe('0');
      key(grid(), 'ArrowUp');
      key(grid(), 'ArrowUp');
      expect(box(1).getAttribute('tabindex')).toBe('0'); // never above DMX 1

      key(grid(), '7'); // a digit starts the inline input with that character
      expect(editor()?.value).toBe('7');
      key(editor()!, 'Enter');
      expect(channelOf(1)).toBe(7);
    });

    it('edits the proportional level and the channel label of the focused box in the detail line', () => {
      clickBox(7);
      key(editor()!, 'Escape');
      const detail = () => element().querySelector('.patch__detail')?.textContent?.replace(/\s+/g, ' ');
      expect(detail()).toContain('DMX 7');
      expect(detail()).toContain('→ 7');

      const level = () => element().querySelector<HTMLInputElement>('input[aria-label="Proportional level of DMX 7"]')!;
      expect(level().value).toBe('100');
      change(level(), '50');
      expect(store.show()!.patch[6].level).toBe(50);
      change(level(), '101');
      expect(level().value).toBe('50');

      const line1 = () => element().querySelector<HTMLInputElement>('input[aria-label="Label line 1 of channel 7"]')!;
      expect(line1().value).toBe('Caj');
      change(line1(), 'CAJ');
      expect(store.show()!.channels[0].label).toEqual({ text: 'CAJ', line1: 'CAJ', line2: '', line3: '' });
      change(line1(), 'SEDEM7A');
      expect(line1().value).toBe('CAJ');

      clickBox(47); // IND 2: a level, no channel label
      key(editor()!, 'Escape');
      expect(detail()).toContain('→ IND 2');
      expect(element().querySelector('input[aria-label="Proportional level of DMX 47"]')).not.toBeNull();
      expect(element().querySelector('.patch__label')).toBeNull();

      clickBox(100); // unpatched: neither
      key(editor()!, 'Escape');
      expect(detail()).toContain('unpatched');
      expect(element().querySelector('.patch__level')).toBeNull();
    });
  });
});
