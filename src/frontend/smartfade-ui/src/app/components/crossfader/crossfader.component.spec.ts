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

  const element = () => fixture.nativeElement as HTMLElement;
  const key = (selector: string) => element().querySelector<HTMLButtonElement>(selector)!;

  beforeEach(() => {
    enabled = signal(true);
    presses = signal(0);
    press = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: ChannelService, useValue: { enabled: enabled.asReadonly() } },
        { provide: ClearKeyService, useValue: { presses: presses.asReadonly(), press } },
      ],
    });

    TestBed.inject(ConsoleStateService).applyConsoleStateChanged(createSampleState());

    fixture = TestBed.createComponent(CrossfaderComponent);
    fixture.detectChanges();
  });

  it('shows the A and B faders inactive, GO and STACK under A, PAUSE and CLEAR under B', () => {
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
    expect(columns[1].querySelector('.xfade__clear')).not.toBeNull();

    expect(key('.xfade__go').disabled).toBe(true);
    expect(key('.xfade__go').getAttribute('aria-label')).toBe('Go');
    expect(key('.xfade__go').querySelector('svg')).not.toBeNull();
    expect(key('.xfade__pause').disabled).toBe(true);
    expect(key('.xfade__pause').getAttribute('aria-label')).toBe('Pause');
    expect(key('.xfade__stack').disabled).toBe(true);
    expect(key('.xfade__stack').textContent?.trim()).toBe('Stack');
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
});
