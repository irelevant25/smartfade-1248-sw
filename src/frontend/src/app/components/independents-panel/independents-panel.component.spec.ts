import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { createSampleState } from '../../core/testing/sample-state';
import { IndependentsPanelComponent } from './independents-panel.component';

describe('IndependentsPanelComponent', () => {
  let fixture: ComponentFixture<IndependentsPanelComponent>;
  let state: ConsoleStateService;
  let enabled: ReturnType<typeof signal<boolean>>;
  let setIndependent: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const ind = (number: 1 | 2) =>
    element().querySelectorAll<HTMLButtonElement>('.independents__key')[number - 1];
  const masters = (independent1: boolean, independent2: boolean) => ({
    master: 200,
    bumps: 51,
    blackout: false,
    solo: false,
    independent1,
    independent2,
  });

  beforeEach(() => {
    enabled = signal(true);
    setIndependent = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: ChannelService, useValue: { enabled: enabled.asReadonly(), setIndependent } }],
    });
    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState({ masters: masters(false, false) }));

    fixture = TestBed.createComponent(IndependentsPanelComponent);
    fixture.detectChanges();
  });

  it('is its own section with IND 1 and IND 2, both off', () => {
    expect(element().querySelector('#independents-title')?.textContent?.trim()).toBe('Independents');
    expect(ind(1).textContent?.trim()).toBe('Ind 1');
    expect(ind(2).textContent?.trim()).toBe('Ind 2');
    expect(ind(1).getAttribute('aria-pressed')).toBe('false');
    expect(ind(2).getAttribute('aria-pressed')).toBe('false');
    expect(element().querySelector('.independents__note')).toBeNull();
  });

  it('lights up when the backend reports one on', () => {
    state.applyMastersChanged(masters(false, true));
    fixture.detectChanges();

    expect(ind(2).getAttribute('aria-pressed')).toBe('true');
    expect(ind(2).classList.contains('independents__key--on')).toBe(true);
    expect(ind(1).classList.contains('independents__key--on')).toBe(false);
  });

  it('asks for the opposite of the reported state', () => {
    ind(1).click();
    expect(setIndependent).toHaveBeenLastCalledWith(1, true);

    state.applyMastersChanged(masters(true, false));
    fixture.detectChanges();
    ind(1).click();
    expect(setIndependent).toHaveBeenLastCalledWith(1, false);

    ind(2).click();
    expect(setIndependent).toHaveBeenLastCalledWith(2, true);
  });

  it('cannot be pressed while commands are not possible, or on a device that does not accept them', () => {
    enabled.set(false);
    fixture.detectChanges();
    expect(ind(1).disabled).toBe(true);
    expect(ind(2).disabled).toBe(true);

    enabled.set(true);
    state.applyConsoleStateChanged(createSampleState({ supportedCommands: [] }));
    fixture.detectChanges();
    expect(ind(1).disabled).toBe(true);
  });

  it('says "not reported" on a real console until its Independents object arrived', () => {
    state.applyConsoleStateChanged(
      createSampleState({
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
        supportedCommands: [],
        independentsReported: false,
      }),
    );
    fixture.detectChanges();

    expect(element().querySelector('.independents__note')?.textContent?.trim()).toBe('Not reported by the console yet');
    expect(ind(1).classList.contains('independents__key--unknown')).toBe(true);
    expect(ind(1).getAttribute('aria-pressed')).toBeNull();

    state.applyConsoleStateChanged(
      createSampleState({
        connection: { status: 'connected', mode: 'hardware', deviceDescription: 'SmartFade 1248, read-only' },
        supportedCommands: [],
        independentsReported: true,
        masters: masters(true, false),
      }),
    );
    fixture.detectChanges();
    expect(element().querySelector('.independents__note')).toBeNull();
    expect(ind(1).classList.contains('independents__key--on')).toBe(true);
    expect(ind(1).getAttribute('title')).toContain('IND 1 identified by a timed press');
  });
});
