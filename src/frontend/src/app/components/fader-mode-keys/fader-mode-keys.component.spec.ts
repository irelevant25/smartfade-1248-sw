import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { createSampleState } from '../../core/testing/sample-state';
import { FaderModeKeysComponent } from './fader-mode-keys.component';

describe('FaderModeKeysComponent', () => {
  let fixture: ComponentFixture<FaderModeKeysComponent>;
  let state: ConsoleStateService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let pressFadersMems: ReturnType<typeof vi.fn>;
  let pressFaders1To24: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const keys = () => Array.from(element().querySelectorAll<HTMLButtonElement>('.faders__key'));
  const key = (selector: string) => element().querySelector<HTMLButtonElement>(selector)!;
  const note = () => element().querySelector('.faders__note')?.textContent?.trim();
  const hardware = { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248' } as const;

  beforeEach(() => {
    enabled = signal(true);
    pressFadersMems = vi.fn();
    pressFaders1To24 = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: ChannelService, useValue: { enabled: enabled.asReadonly(), pressFadersMems, pressFaders1To24 } },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState());

    fixture = TestBed.createComponent(FaderModeKeysComponent);
    fixture.detectChanges();
  });

  it('shows the four FADERS keys in the console order, all inactive on the simulator', () => {
    expect(element().querySelector('#faders-title')?.textContent?.trim()).toBe('Faders');
    expect(keys().map((k) => k.textContent?.trim())).toEqual(['Memories', '25-48', '1-24', 'Next']);
    expect(keys().every((k) => k.disabled)).toBe(true);
    expect(keys().every((k) => k.getAttribute('aria-pressed') === null)).toBe(true);
    expect(key('.faders__mems').classList.contains('faders__key--red')).toBe(true);
    expect(key('.faders__upper').classList.contains('faders__key--green')).toBe(true);
    expect(key('.faders__lower').classList.contains('faders__key--green')).toBe(true);
    expect(key('.faders__next').classList.contains('faders__key--blue')).toBe(true);
    expect(note()).toBe('The simulator plays every bank at once');
  });

  it('lights the key of the bank a real console reports, read-only', () => {
    state.applyConsoleStateChanged(
      createSampleState({ connection: hardware, supportedCommands: [], faderBank: 'memories' }),
    );
    fixture.detectChanges();

    expect(note()).toBeUndefined();
    expect(key('.faders__mems').classList.contains('faders__key--lit')).toBe(true);
    expect(key('.faders__mems').getAttribute('aria-pressed')).toBe('true');
    expect(key('.faders__lower').classList.contains('faders__key--lit')).toBe(false);
    expect(key('.faders__lower').getAttribute('aria-pressed')).toBe('false');
    expect(keys().every((k) => k.disabled)).toBe(true);
    expect(key('.faders__mems').classList.contains('faders__key--led')).toBe(true);
    expect(key('.faders__mems').classList.contains('sf-key--inactive')).toBe(false);

    state.applyConsoleStateChanged(
      createSampleState({ connection: hardware, supportedCommands: [], faderBank: 'channels25To48' }),
    );
    fixture.detectChanges();
    expect(key('.faders__upper').classList.contains('faders__key--lit')).toBe(true);
    expect(key('.faders__mems').classList.contains('faders__key--lit')).toBe(false);
  });

  it('says so while a real console has not reported its bank, and ignores a bank it does not know', () => {
    state.applyConsoleStateChanged(createSampleState({ connection: hardware, supportedCommands: [] }));
    fixture.detectChanges();
    expect(note()).toBe('Not reported by the console yet');

    state.applyConsoleStateChanged(
      createSampleState({ connection: hardware, supportedCommands: [], faderBank: 'bank-7' as never }),
    );
    fixture.detectChanges();
    expect(note()).toBe('Not reported by the console yet');
    expect(keys().some((k) => k.classList.contains('faders__key--lit'))).toBe(false);
  });

  it('sends MEMORIES and 1-24 as taps only for a device that accepts them; 25-48 and NEXT stay inactive', () => {
    state.applyConsoleStateChanged(
      createSampleState({
        connection: hardware,
        supportedCommands: ['pressFadersMems', 'pressFaders1To24'],
        writableControls: ['fadersMems', 'faders1To24'],
        faderBank: 'channels1To24',
      }),
    );
    fixture.detectChanges();

    expect(key('.faders__mems').disabled).toBe(false);
    expect(key('.faders__lower').disabled).toBe(false);
    expect(key('.faders__upper').disabled).toBe(true);
    expect(key('.faders__next').disabled).toBe(true);
    expect(key('.faders__lower').classList.contains('faders__key--lit')).toBe(true);

    key('.faders__mems').click();
    key('.faders__lower').click();
    expect(pressFadersMems).toHaveBeenCalledTimes(1);
    expect(pressFaders1To24).toHaveBeenCalledTimes(1);

    enabled.set(false);
    fixture.detectChanges();
    expect(key('.faders__mems').disabled).toBe(true);
    expect(key('.faders__lower').disabled).toBe(true);
  });
});
