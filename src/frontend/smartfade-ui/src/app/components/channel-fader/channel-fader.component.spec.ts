import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelFaderComponent } from './channel-fader.component';

describe('ChannelFaderComponent', () => {
  let fixture: ComponentFixture<ChannelFaderComponent>;
  let levels: number[];
  let moves: Array<-1 | 1>;

  const element = () => fixture.nativeElement as HTMLElement;
  const slider = () => element().querySelector<HTMLInputElement>('input[type="range"]')!;
  const text = (selector: string) => element().querySelector(selector)?.textContent?.trim();

  const set = (name: string, value: unknown) => {
    fixture.componentRef.setInput(name, value);
    fixture.detectChanges();
  };

  /** Presses a key on the slider and returns whether the component took the key over. */
  const press = (key: string, modifiers: KeyboardEventInit = {}): boolean => {
    const event = new KeyboardEvent('keydown', {
      key,
      bubbles: true,
      cancelable: true,
      ...modifiers,
    });
    slider().dispatchEvent(event);
    fixture.detectChanges();
    return event.defaultPrevented;
  };

  beforeEach(() => {
    fixture = TestBed.createComponent(ChannelFaderComponent);
    fixture.componentRef.setInput('label', 7);
    fixture.componentRef.setInput('desired', 100);
    fixture.detectChanges();

    levels = [];
    moves = [];
    fixture.componentInstance.levelChange.subscribe((level) => levels.push(level));
    fixture.componentInstance.navigate.subscribe((direction) => moves.push(direction));
  });

  describe('display', () => {
    it('is a vertical range input over 0..255', () => {
      expect(slider().min).toBe('0');
      expect(slider().max).toBe('255');
      expect(slider().step).toBe('1');
      expect(slider().getAttribute('aria-orientation')).toBe('vertical');
    });

    it('shows the label and the level as percent computed from 0..255', () => {
      expect(text('.fader__label')).toBe('7');
      expect(slider().valueAsNumber).toBe(100);
      expect(text('.fader__readout')).toBe('39%');

      set('desired', 255);
      expect(text('.fader__readout')).toBe('100%');

      set('desired', 0);
      expect(text('.fader__readout')).toBe('0%');

      set('desired', 128);
      expect(text('.fader__readout')).toBe('50%');
    });

    it('puts the readout above the fader and the label below it', () => {
      const order = Array.from(element().querySelector('.fader')!.children).map(
        (child) => child.className.split(' ')[0],
      );

      expect(order.indexOf('fader__readout')).toBeLessThan(order.indexOf('fader__rail'));
      expect(order.indexOf('fader__rail')).toBeLessThan(order.indexOf('fader__label'));
    });

    it('names the slider for assistive technology, in percent', () => {
      expect(slider().getAttribute('aria-label')).toBe('Channel 7');
      expect(slider().getAttribute('aria-valuetext')).toBe('39 percent');

      set('label', 'MASTER');
      expect(slider().getAttribute('aria-label')).toBe('MASTER');

      set('label', 3);
      set('kind', 'memory');
      expect(slider().getAttribute('aria-label')).toBe('Memory 3');
    });

    it('shows a caption above the readout when given one', () => {
      expect(element().querySelector('.fader__caption')).toBeNull();

      set('caption', 'GROUP 1');

      expect(text('.fader__caption')).toBe('GROUP 1');
    });

    it('shows the reported level only while it differs from the desired one', () => {
      const actual = () => element().querySelector('.fader__actual')!;

      set('actual', 100);
      expect(actual().classList.contains('fader__actual--shown')).toBe(false);

      set('actual', 64);
      expect(actual().classList.contains('fader__actual--shown')).toBe(true);
      expect(text('.fader__actual')).toBe('25%');
    });

    it('does not invent a reported level when there is none', () => {
      expect(element().querySelector('.fader__actual--shown')).toBeNull();
    });

    it('lights the lamp while the console has not confirmed the level', () => {
      const lamp = () => element().querySelector('.fader__lamp')!;
      expect(lamp().classList.contains('fader__lamp--on')).toBe(false);

      set('pending', true);

      expect(lamp().classList.contains('fader__lamp--on')).toBe(true);
      expect(slider().getAttribute('aria-valuetext')).toBe('39 percent, waiting for the console');
    });

    it('positions the bar and the gap from the desired and reported levels', () => {
      set('desired', 255);
      set('actual', 0);

      const rail = element().querySelector<HTMLElement>('.fader__rail')!;
      expect(rail.style.getPropertyValue('--level')).toBe('1');
      expect(rail.style.getPropertyValue('--actual')).toBe('0');
    });
  });

  describe('keyboard', () => {
    it.each([
      ['ArrowUp', 101],
      ['ArrowDown', 99],
      ['PageUp', 110],
      ['PageDown', 90],
      ['Home', 0],
      ['End', 255],
    ])('%s sets the level to %i', (key, expected) => {
      expect(press(key)).toBe(true);

      expect(levels).toEqual([expected]);
    });

    it('stops at the ends of the range', () => {
      set('desired', 255);
      press('ArrowUp');
      press('PageUp');
      press('End');
      expect(levels).toEqual([]);

      set('desired', 0);
      press('ArrowDown');
      press('PageDown');
      press('Home');
      expect(levels).toEqual([]);
    });

    it('moves focus instead of changing the level on Left and Right', () => {
      expect(press('ArrowLeft')).toBe(true);
      expect(press('ArrowRight')).toBe(true);

      expect(moves).toEqual([-1, 1]);
      expect(levels).toEqual([]);
    });

    it('leaves other keys and shortcuts alone', () => {
      expect(press('Tab')).toBe(false);
      expect(press('a')).toBe(false);
      expect(press('Home', { ctrlKey: true })).toBe(false);
      expect(press('ArrowUp', { altKey: true })).toBe(false);

      expect(levels).toEqual([]);
      expect(moves).toEqual([]);
    });

    it('continues from the level it is showing', () => {
      set('desired', 40);

      press('ArrowUp');

      expect(levels).toEqual([41]);
    });

    it('does not lose steps when keys arrive before the next render', () => {
      const keydown = (key: string) =>
        slider().dispatchEvent(
          new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }),
        );

      keydown('PageUp');
      keydown('PageUp');
      keydown('PageUp');
      keydown('ArrowDown');

      expect(levels).toEqual([110, 120, 130, 129]);
    });

    it('goes back to the desired level once rendered, if the parent did not take the change', () => {
      press('PageUp');
      fixture.detectChanges();

      expect(levels).toEqual([110]);
      expect(slider().valueAsNumber).toBe(100);
      expect(text('.fader__readout')).toBe('39%');
    });
  });

  describe('pointer', () => {
    const drag = (value: number) => {
      slider().value = String(value);
      slider().dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
    };

    it('reports the level the thumb is moved to', () => {
      drag(180);
      drag(190);

      expect(levels).toEqual([180, 190]);
    });

    it('follows the pointer while dragging, not the echoes of older values', () => {
      slider().dispatchEvent(new Event('pointerdown', { bubbles: true }));
      drag(200);

      set('desired', 150);

      expect(slider().valueAsNumber).toBe(200);
      expect(text('.fader__readout')).toBe('78%');
    });

    it('drops a drag that was under way when the fader gets disabled', () => {
      slider().dispatchEvent(new Event('pointerdown', { bubbles: true }));
      drag(200);
      expect(slider().valueAsNumber).toBe(200);

      set('disabled', true);
      expect(slider().valueAsNumber).toBe(100);

      set('disabled', false);
      expect(slider().valueAsNumber).toBe(100);
      expect(text('.fader__readout')).toBe('39%');
    });

    it('goes back to the desired level when the drag ends', () => {
      slider().dispatchEvent(new Event('pointerdown', { bubbles: true }));
      drag(200);
      set('desired', 150);

      slider().dispatchEvent(new Event('pointerup', { bubbles: true }));
      fixture.detectChanges();

      expect(slider().valueAsNumber).toBe(150);
      expect(text('.fader__readout')).toBe('59%');
    });
  });

  describe('bump button', () => {
    const button = () => element().querySelector<HTMLButtonElement>('.fader__bump');
    let bumps: boolean[];
    let selections: number;

    const pointer = (type: string) => {
      button()!.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
      fixture.detectChanges();
    };

    const key = (type: string, init: KeyboardEventInit): boolean => {
      const event = new KeyboardEvent(type, { bubbles: true, cancelable: true, ...init });
      button()!.dispatchEvent(event);
      fixture.detectChanges();
      return event.defaultPrevented;
    };

    beforeEach(() => {
      bumps = [];
      selections = 0;
      fixture.componentInstance.bumpChange.subscribe((held) => bumps.push(held));
      fixture.componentInstance.select.subscribe(() => selections++);
    });

    it('is a plain name unless the fader is bumpable', () => {
      expect(button()).toBeNull();
      expect(element().querySelector('span.fader__label')?.textContent?.trim()).toBe('7');
    });

    it('shows the number as a button under the fader', () => {
      set('bumpable', true);

      expect(element().querySelector('span.fader__label')).toBeNull();
      expect(button()!.textContent?.trim()).toBe('7');
      expect(button()!.getAttribute('aria-label')).toBe('Bump Channel 7');
      expect(button()!.getAttribute('aria-pressed')).toBe('false');
      const order = Array.from(element().querySelector('.fader')!.children);
      expect(order.indexOf(element().querySelector('.fader__rail')!)).toBeLessThan(
        order.indexOf(button()!),
      );

      set('kind', 'memory');
      expect(button()!.getAttribute('aria-label')).toBe('Bump Memory 7');
    });

    it('reports a press and a release by pointer', () => {
      set('bumpable', true);

      pointer('pointerdown');
      expect(bumps).toEqual([true]);
      expect(button()!.getAttribute('aria-pressed')).toBe('true');
      expect(button()!.classList.contains('fader__bump--held')).toBe(true);

      pointer('pointerup');
      expect(bumps).toEqual([true, false]);
      expect(button()!.getAttribute('aria-pressed')).toBe('false');
      expect(button()!.classList.contains('fader__bump--held')).toBe(false);
    });

    it('reports a press and a release by keyboard, ignoring key repeats', () => {
      set('bumpable', true);

      expect(key('keydown', { key: ' ' })).toBe(true);
      key('keydown', { key: ' ', repeat: true });
      expect(bumps).toEqual([true]);

      expect(key('keyup', { key: ' ' })).toBe(true);
      expect(bumps).toEqual([true, false]);

      key('keydown', { key: 'Enter' });
      key('keyup', { key: 'Enter' });
      expect(bumps).toEqual([true, false, true, false]);

      expect(key('keydown', { key: 'a' })).toBe(false);
      expect(bumps).toEqual([true, false, true, false]);
    });

    it('releases when it loses focus, and drops a held bump when disabled', () => {
      set('bumpable', true);

      pointer('pointerdown');
      button()!.dispatchEvent(new Event('blur'));
      fixture.detectChanges();
      expect(bumps).toEqual([true, false]);

      pointer('pointerdown');
      expect(bumps).toEqual([true, false, true]);

      set('disabled', true);
      expect(button()!.disabled).toBe(true);
      expect(button()!.classList.contains('fader__bump--held')).toBe(false);
      expect(bumps).toEqual([true, false, true]);
    });

    it('selects instead of bumping while armed', () => {
      set('bumpable', true);
      set('bumpArmed', true);

      expect(button()!.getAttribute('aria-label')).toBe('Select Channel 7');
      expect(button()!.classList.contains('fader__bump--armed')).toBe(true);
      expect(button()!.hasAttribute('aria-pressed')).toBe(false);

      pointer('pointerdown');
      button()!.click();
      fixture.detectChanges();

      expect(bumps).toEqual([]);
      expect(selections).toBe(1);
    });

    it('does not select or bump while disabled', () => {
      set('bumpable', true);
      set('bumpArmed', true);
      set('disabled', true);

      button()!.click();
      key('keydown', { key: ' ' });

      expect(selections).toBe(0);
      expect(bumps).toEqual([]);
    });

    it('glows as much as it is told to', () => {
      set('bumpable', true);
      set('bumpLit', 0.5);

      expect(button()!.style.getPropertyValue('--lit')).toBe('0.5');
    });

    it('keeps the plain name, not the button, in the accent colour while the fader is up', () => {
      expect(element().querySelector('.fader__name')).not.toBeNull();

      set('bumpable', true);

      expect(element().querySelector('.fader__name')).toBeNull();
      expect(button()!.classList.contains('fader__label')).toBe(true);
    });
  });

  describe('states', () => {
    it('cannot be operated when disabled', () => {
      set('disabled', true);

      expect(slider().disabled).toBe(true);
      expect(element().querySelector('.fader--idle')).not.toBeNull();
    });

    it('shows a level without allowing changes when read only', () => {
      set('readOnly', true);

      expect(slider().disabled).toBe(true);
      expect(element().querySelector('.fader--readonly')).not.toBeNull();
      expect(element().querySelector('.fader--idle')).toBeNull();

      set('disabled', true);
      expect(element().querySelector('.fader--idle')).not.toBeNull();
      expect(press('ArrowUp')).toBe(false);
      expect(levels).toEqual([]);
    });

    it('takes keyboard focus on request', () => {
      fixture.componentInstance.focus();

      expect(document.activeElement).toBe(slider());
    });
  });
});
