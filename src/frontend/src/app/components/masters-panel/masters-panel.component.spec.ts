import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { createSampleState } from '../../core/testing/sample-state';
import { MastersPanelComponent } from './masters-panel.component';

describe('MastersPanelComponent', () => {
  let fixture: ComponentFixture<MastersPanelComponent>;
  let state: ConsoleStateService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let setMaster: ReturnType<typeof vi.fn>;
  let setBumps: ReturnType<typeof vi.fn>;
  let setBlackout: ReturnType<typeof vi.fn>;
  let setSolo: ReturnType<typeof vi.fn>;
  let setIndependent: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const faders = () => element().querySelectorAll('sf-channel-fader');
  const label = (index: number) => faders()[index].querySelector('.fader__label')?.textContent?.trim();
  const readout = (index: number) =>
    faders()[index].querySelector('.fader__readout')?.textContent?.trim();
  const slider = (index: number) =>
    faders()[index].querySelector<HTMLInputElement>('input[type="range"]')!;
  const blackout = () => element().querySelector<HTMLButtonElement>('.masters__blackout')!;
  const solo = () => element().querySelector<HTMLButtonElement>('.masters__solo')!;

  const press = (input: HTMLInputElement, key: string) => {
    input.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    fixture.detectChanges();
  };

  beforeEach(() => {
    enabled = signal(true);
    setMaster = vi.fn();
    setBumps = vi.fn();
    setBlackout = vi.fn();
    setSolo = vi.fn();
    setIndependent = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ChannelService,
          useValue: {
            enabled: enabled.asReadonly(),
            setMaster,
            setBumps,
            setBlackout,
            setSolo,
            setIndependent,
          },
        },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(
      createSampleState({ masters: { master: 200, bumps: 51, blackout: false, solo: false, independent1: false, independent2: false } }),
    );

    fixture = TestBed.createComponent(MastersPanelComponent);
    fixture.detectChanges();
  });

  it('lays out BUMPS under SOLO on the left and MASTER under BLACKOUT on the right', () => {
    expect(label(0)).toBe('BUMPS');
    expect(label(1)).toBe('MASTER');

    const columns = element().querySelectorAll('.masters__column');
    expect(columns).toHaveLength(2);
    expect(columns[0].querySelector('.masters__solo')).not.toBeNull();
    expect(columns[0].querySelector('.fader__label')?.textContent?.trim()).toBe('BUMPS');
    expect(columns[1].querySelector('.masters__blackout')).not.toBeNull();
    expect(columns[1].querySelector('.fader__label')?.textContent?.trim()).toBe('MASTER');
    for (const column of Array.from(columns)) {
      expect(Array.from(column.children).map((child) => child.tagName.toLowerCase())).toEqual([
        'button',
        'sf-channel-fader',
      ]);
    }
  });

  describe('MASTER', () => {
    it('shows the master level', () => {
      expect(slider(1).valueAsNumber).toBe(200);
      expect(readout(1)).toBe('78%');
    });

    it('passes a level change on to the channel service', () => {
      press(slider(1), 'PageDown');

      expect(setMaster).toHaveBeenCalledWith(190);
      expect(setBumps).not.toHaveBeenCalled();
    });

    it('cannot be moved while commands are not possible', () => {
      enabled.set(false);
      fixture.detectChanges();

      expect(slider(1).disabled).toBe(true);
    });
  });

  describe('BUMPS', () => {
    it('shows the bumps level', () => {
      expect(slider(0).valueAsNumber).toBe(51);
      expect(readout(0)).toBe('20%');
      expect(slider(0).disabled).toBe(false);
    });

    it('passes a level change on to the channel service', () => {
      press(slider(0), 'PageUp');

      expect(setBumps).toHaveBeenCalledWith(61);
      expect(setMaster).not.toHaveBeenCalled();
    });

    it('follows the backend', () => {
      state.applyMastersChanged({ master: 200, bumps: 255, blackout: false, solo: false, independent1: false, independent2: false });
      fixture.detectChanges();

      expect(slider(0).valueAsNumber).toBe(255);
    });

    it('is dimmed with everything else while nothing is live', () => {
      expect(faders()[0].querySelector('.fader--idle')).toBeNull();

      enabled.set(false);
      fixture.detectChanges();

      expect(faders()[0].querySelector('.fader--idle')).not.toBeNull();
      expect(slider(0).disabled).toBe(true);
    });
  });

  describe('BLACKOUT', () => {
    it('is off at first and says so to assistive technology', () => {
      expect(blackout().textContent?.trim()).toBe('Blackout');
      expect(blackout().getAttribute('aria-pressed')).toBe('false');
      expect(blackout().classList.contains('masters__blackout--on')).toBe(false);
    });

    it('lights up when the backend reports it on', () => {
      state.applyMastersChanged({ master: 200, bumps: 51, blackout: true, solo: false, independent1: false, independent2: false });
      fixture.detectChanges();

      expect(blackout().getAttribute('aria-pressed')).toBe('true');
      expect(blackout().classList.contains('masters__blackout--on')).toBe(true);
    });

    it('asks for blackout on when it is off, and for off when it is on', () => {
      blackout().click();
      expect(setBlackout).toHaveBeenLastCalledWith(true);

      state.applyMastersChanged({ master: 200, bumps: 51, blackout: true, solo: false, independent1: false, independent2: false });
      fixture.detectChanges();
      blackout().click();
      expect(setBlackout).toHaveBeenLastCalledWith(false);
    });

    it('cannot be pressed while commands are not possible', () => {
      enabled.set(false);
      fixture.detectChanges();

      expect(blackout().disabled).toBe(true);
    });
  });

  describe('BLACKOUT while the MASTER is below full', () => {
    const masters = (master: number, blackout = false) => ({
      master,
      bumps: 51,
      blackout,
      solo: false,
      independent1: false,
      independent2: false,
    });

    it('blinks, and stops when the MASTER is back at full or blackout is on', () => {
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(true);

      state.applyMastersChanged(masters(255));
      fixture.detectChanges();
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(false);

      state.applyMastersChanged(masters(100, true));
      fixture.detectChanges();
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(false);
      expect(blackout().classList.contains('masters__blackout--on')).toBe(true);
    });

    it('does not blink while nothing is live', () => {
      enabled.set(false);
      fixture.detectChanges();

      expect(blackout().classList.contains('masters__blackout--blink')).toBe(false);
    });
  });

  it('holds the masters only: the independents are their own section (sf-independents-panel)', () => {
    const children = Array.from(element().querySelector('.masters')!.children);
    expect(children.map((child) => child.className.split(' ')[0])).toEqual(['sf-panel-title', 'masters__columns']);
    expect(element().querySelector('#independents-title')).toBeNull();
  });

  describe('with a read-only device (hardware mode)', () => {
    it('shows the masters without letting anything be changed, and says which keys the console has not reported', () => {
      state.applyConsoleStateChanged(
        createSampleState({
          supportedCommands: [],
          connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
          masters: { master: 128, bumps: 51, blackout: false, solo: true, independent1: false, independent2: false },
        }),
      );
      fixture.detectChanges();

      expect(slider(0).disabled).toBe(true);
      expect(slider(1).disabled).toBe(true);
      expect(faders()[1].querySelector('.fader--readonly')).not.toBeNull();
      expect(slider(1).valueAsNumber).toBe(128);
      expect(solo().disabled).toBe(true);
      expect(solo().getAttribute('aria-pressed')).toBe('true');
      expect(solo().getAttribute('title')).toContain('inferred');
      expect(blackout().disabled).toBe(true);
      // BLACKOUT arrives with the masters as an LED reading (F114); the independents have their own object (F115)
      expect(blackout().classList.contains('masters__toggle--unknown')).toBe(false);
      expect(blackout().getAttribute('aria-pressed')).toBe('false');
      expect(blackout().getAttribute('title')).toContain('LED reading');
      // the MASTER is below full, but without an LED report the manual's rule is not applied to a console
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(false);
    });

    it('lights BLACKOUT from the reported masters and blinks it as the console reports its LED', () => {
      state.applyConsoleStateChanged(
        createSampleState({
          supportedCommands: [],
          independentsReported: true,
          connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
          masters: { master: 255, bumps: 0, blackout: true, solo: false, independent1: true, independent2: false },
        }),
      );
      fixture.detectChanges();

      expect(blackout().classList.contains('masters__blackout--on')).toBe(true);

      // the BLACKOUT LED reported as blinking (the MASTER below full, F85) blinks here too
      const led = { light: 'off' as const, blinking: false };
      state.applyConsoleStateChanged(
        createSampleState({
          supportedCommands: [],
          connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
          masters: { master: 136, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false },
          keys: { stack: led, clear: led, copy: led, recSeq: led, magic: led, recMem: led, editMem: led, solo: led, blackout: { light: 'full', blinking: true } },
        }),
      );
      fixture.detectChanges();
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(true);
    });

    it('mirrors the SOLO and BLACKOUT LEDs as soon as the console reported its keys, even before its masters', () => {
      const led = (light: 'off' | 'dim' | 'full', blinking = false) => ({ light, blinking });
      const hardware = { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' } as const;
      state.applyConsoleStateChanged(createSampleState({ supportedCommands: [], connection: hardware, mastersReported: false }));
      fixture.detectChanges();
      // nothing known yet: dashed, no pressed state
      expect(solo().classList.contains('masters__toggle--unknown')).toBe(true);
      expect(blackout().classList.contains('masters__toggle--unknown')).toBe(true);
      expect(solo().getAttribute('aria-pressed')).toBeNull();

      state.applyConsoleStateChanged(
        createSampleState({
          supportedCommands: [],
          connection: hardware,
          mastersReported: false,
          keys: { stack: led('off'), clear: led('off'), copy: led('off'), recSeq: led('off'), magic: led('off'), recMem: led('off'), editMem: led('off'), blackout: led('full'), solo: led('full') },
        }),
      );
      fixture.detectChanges();
      // the LEDs are known: the keys mirror them, lit, read-only, not dashed
      expect(solo().classList.contains('masters__toggle--unknown')).toBe(false);
      expect(solo().classList.contains('masters__toggle--led')).toBe(true);
      expect(solo().classList.contains('masters__solo--on')).toBe(true);
      expect(solo().getAttribute('aria-pressed')).toBe('true');
      expect(blackout().classList.contains('masters__toggle--led')).toBe(true);
      expect(blackout().classList.contains('masters__blackout--on')).toBe(true);
      expect(solo().disabled).toBe(true);

      // the MASTER-below-full blink of block 53 is not blackout
      state.applyConsoleStateChanged(
        createSampleState({
          supportedCommands: [],
          connection: hardware,
          mastersReported: false,
          keys: { stack: led('off'), clear: led('off'), copy: led('off'), recSeq: led('off'), magic: led('off'), recMem: led('off'), editMem: led('off'), blackout: led('full', true), solo: led('off') },
        }),
      );
      fixture.detectChanges();
      expect(blackout().classList.contains('masters__blackout--on')).toBe(false);
      expect(blackout().classList.contains('masters__blackout--blink')).toBe(true);
      expect(solo().classList.contains('masters__solo--on')).toBe(false);
    });

    it('marks the masters as placeholders until the console reported them', () => {
      state.applyConsoleStateChanged(createSampleState({ mastersReported: false }));
      fixture.detectChanges();

      expect(element().querySelector('.masters--unreported')).not.toBeNull();
      expect(element().querySelector('.masters__note')?.textContent?.trim()).toBe(
        'Not reported by the console yet',
      );

      state.applyMastersChanged({ master: 10, bumps: 0, blackout: false, solo: false, independent1: false, independent2: false });
      fixture.detectChanges();

      expect(element().querySelector('.masters--unreported')).toBeNull();
      expect(element().querySelector('.masters__note')).toBeNull();
    });
  });

  describe('SOLO', () => {
    it('is off at first and says so to assistive technology', () => {
      expect(solo().textContent?.trim()).toBe('Solo');
      expect(solo().getAttribute('aria-pressed')).toBe('false');
      expect(solo().classList.contains('masters__solo--on')).toBe(false);
    });

    it('lights up when the backend reports it on', () => {
      state.applyMastersChanged({ master: 200, bumps: 51, blackout: false, solo: true, independent1: false, independent2: false });
      fixture.detectChanges();

      expect(solo().getAttribute('aria-pressed')).toBe('true');
      expect(solo().classList.contains('masters__solo--on')).toBe(true);
    });

    it('asks for solo on when it is off, and for off when it is on', () => {
      solo().click();
      expect(setSolo).toHaveBeenLastCalledWith(true);

      state.applyMastersChanged({ master: 200, bumps: 51, blackout: false, solo: true, independent1: false, independent2: false });
      fixture.detectChanges();
      solo().click();
      expect(setSolo).toHaveBeenLastCalledWith(false);
      expect(setBlackout).not.toHaveBeenCalled();
    });

    it('cannot be pressed while commands are not possible', () => {
      enabled.set(false);
      fixture.detectChanges();

      expect(solo().disabled).toBe(true);
    });
  });
});
