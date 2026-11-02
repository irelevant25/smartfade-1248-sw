import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ClearKeyService } from '../../core/clear-key.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { createSampleState } from '../../core/testing/sample-state';
import { CrossfaderComponent } from './crossfader.component';

describe('CrossfaderComponent', () => {
  let fixture: ComponentFixture<CrossfaderComponent>;
  let enabled: ReturnType<typeof signal<boolean>>;
  let presses: ReturnType<typeof signal<number>>;
  let press: ReturnType<typeof vi.fn>;
  let pressGo: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const key = (selector: string) => element().querySelector<HTMLButtonElement>(selector)!;

  beforeEach(() => {
    enabled = signal(true);
    presses = signal(0);
    press = vi.fn();
    pressGo = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: ChannelService, useValue: { enabled: enabled.asReadonly(), pressGo } },
        { provide: ClearKeyService, useValue: { presses: presses.asReadonly(), press } },
      ],
    });

    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(createSampleState());

    fixture = TestBed.createComponent(CrossfaderComponent);
    fixture.detectChanges();
  });

  it('shows the A and B faders inactive, GO and STACK under A, PAUSE and RATE under B, CLEAR under both', () => {
    const columns = element().querySelectorAll('.xfade__column');
    expect(columns).toHaveLength(2);

    const labels = Array.from(element().querySelectorAll('.fader__label')).map((label) =>
      label.textContent?.trim(),
    );
    expect(labels).toEqual(['A', 'B']);
    expect(
      Array.from(element().querySelectorAll<HTMLInputElement>('input[type="range"]')).every(
        (slider) => slider.disabled,
      ),
    ).toBe(true);

    for (const column of Array.from(columns)) {
      expect(Array.from(column.children).map((child) => child.tagName.toLowerCase())).toEqual([
        'sf-channel-fader',
        'button',
        'button',
      ]);
    }
    expect(columns[0].querySelector('.xfade__go')).not.toBeNull();
    expect(columns[0].querySelector('.xfade__stack')).not.toBeNull();
    expect(columns[1].querySelector('.xfade__pause')).not.toBeNull();
    expect(columns[1].querySelector('.xfade__rate')).not.toBeNull();
    // CLEAR is two keys wide: outside the columns, right after them
    expect(element().querySelector('.xfade__column .xfade__clear')).toBeNull();
    expect(element().querySelector('.xfade__columns')?.nextElementSibling?.classList.contains('xfade__clear')).toBe(true);

    expect(key('.xfade__go').disabled).toBe(true);
    expect(key('.xfade__go').getAttribute('aria-label')).toBe('Go');
    expect(key('.xfade__go').querySelector('svg')).not.toBeNull();
    expect(key('.xfade__pause').disabled).toBe(true);
    expect(key('.xfade__pause').getAttribute('aria-label')).toBe('Pause');
    expect(key('.xfade__rate').disabled).toBe(true);
    expect(key('.xfade__rate').textContent?.trim()).toBe('Rate');
    expect(key('.xfade__stack').disabled).toBe(true);
    expect(key('.xfade__stack').textContent?.trim()).toBe('Stack');
  });

  it('enables GO as a tap only for a device that models the stack and names the control writable', () => {
    expect(key('.xfade__go').disabled).toBe(true);

    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(
      createSampleState({ supportedCommands: ['pressGo'], writableControls: ['go'] }),
    );
    fixture.detectChanges();

    expect(key('.xfade__go').disabled).toBe(false);
    key('.xfade__go').click();
    expect(pressGo).toHaveBeenCalledTimes(1);

    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(
      createSampleState({ supportedCommands: ['pressGo'], writableControls: ['master'] }),
    );
    fixture.detectChanges();
    expect(key('.xfade__go').disabled).toBe(true);
  });

  it('passes CLEAR presses on and shows a series in progress', () => {
    expect(key('.xfade__clear').disabled).toBe(false);

    key('.xfade__clear').click();
    key('.xfade__clear').click();

    expect(press).toHaveBeenCalledTimes(2);

    presses.set(2);
    fixture.detectChanges();
    expect(key('.xfade__clear').classList.contains('xfade__clear--series')).toBe(true);

    presses.set(0);
    fixture.detectChanges();
    expect(key('.xfade__clear').classList.contains('xfade__clear--series')).toBe(false);
  });

  it('disables CLEAR while commands are not possible', () => {
    enabled.set(false);
    fixture.detectChanges();

    expect(key('.xfade__clear').disabled).toBe(true);
  });

  it('disables CLEAR on a device that clears nothing (hardware mode)', () => {
    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(
      createSampleState({ supportedCommands: [] }),
    );
    fixture.detectChanges();

    expect(key('.xfade__clear').disabled).toBe(true);
  });

  it('mirrors the STACK and CLEAR LEDs when a read-only device reports them', () => {
    const led = (light: 'off' | 'dim' | 'full', blinking = false) => ({ light, blinking });
    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(
      createSampleState({
        supportedCommands: [],
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
        keys: { stack: led('full', true), clear: led('full', true), copy: led('off'), recSeq: led('off'), magic: led('full', true), recMem: led('full'), editMem: led('dim'), blackout: led('off'), solo: led('off') },
      }),
    );
    fixture.detectChanges();
    const stack = element().querySelector<HTMLButtonElement>('.xfade__stack')!;
    const clear = element().querySelector<HTMLButtonElement>('.xfade__clear')!;

    expect(stack.classList.contains('xfade__key--lit')).toBe(true);
    expect(stack.classList.contains('sf-key--blink')).toBe(true);
    expect(stack.classList.contains('xfade__key--led')).toBe(true);
    expect(stack.getAttribute('title')).toContain('LED reading');
    expect(clear.classList.contains('xfade__key--lit')).toBe(true);
    expect(clear.disabled).toBe(true);
    expect(element().querySelector('.xfade__note')?.textContent).toContain('Observed from the console');
  });

  it('shows the main playback a read-only device reports on the A and B faders', () => {
    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(
      createSampleState({
        supportedCommands: [],
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
        crossfader: { step: 0, live: 250, next: 5 },
      }),
    );
    fixture.detectChanges();
    const sliders = Array.from(element().querySelectorAll<HTMLInputElement>('input[type="range"]'));

    expect(sliders[0].valueAsNumber).toBe(5); // A: 255 - Live
    expect(sliders[1].valueAsNumber).toBe(5); // B: Next
    expect(element().querySelectorAll('.fader--readonly')).toHaveLength(2); // shown, never movable
    expect(element().querySelector('.xfade__note')?.textContent).toContain('next step 1');

    TestBed.inject(ConsoleStateService).applyCrossfaderChanged({ step: 1, live: 255, next: 0 });
    fixture.detectChanges();
    expect(sliders[0].valueAsNumber).toBe(0);
    expect(element().querySelector('.xfade__note')?.textContent).toContain('next step 2');
  });
});
