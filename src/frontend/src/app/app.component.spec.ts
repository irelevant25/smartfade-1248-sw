import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { ConsoleConnectionService } from './core/console-connection.service';
import { CONSOLE_HUB_FACTORY } from './core/console-hub';
import { ConsoleStateService } from './core/console-state.service';
import { FakeConsoleHub } from './core/testing/fake-console-hub';
import { createSampleState } from './core/testing/sample-state';

const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

/** 48 channel faders, 24 memory faders of the shown page, the crossfader pair, BUMPS and MASTER. */
const FADER_COUNT = 48 + 24 + 2 + 2;
/** The crossfader pair is inactive; everything else is live when the console is connected. */
const INACTIVE_FADERS = 2;

describe('AppComponent', () => {
  let hub: FakeConsoleHub;

  const element = (fixture: { nativeElement: unknown }) => fixture.nativeElement as HTMLElement;
  const text = (root: HTMLElement, selector: string) =>
    root.querySelector(selector)?.textContent?.trim();

  beforeEach(() => {
    hub = new FakeConsoleHub();
    hub.results.set('getState', createSampleState());
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: CONSOLE_HUB_FACTORY, useValue: () => hub }],
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('lays the page out like the console: bar, channels over memories, record keys, crossfader, masters, outputs', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const root = element(fixture);

    const order = Array.from(root.children).map((child) => child.tagName.toLowerCase());
    expect(order).toEqual(['sf-connection-bar', 'main', 'sf-output-meters']);
    expect(
      Array.from(root.querySelector('main')!.children).map((child) => child.tagName.toLowerCase()),
    ).toEqual([
      'sf-channel-grid',
      'sf-memory-grid',
      'sf-record-keys',
      'sf-crossfader',
      'sf-masters-panel',
    ]);
    expect(root.querySelectorAll('sf-channel-fader')).toHaveLength(FADER_COUNT);
    expect(root.querySelectorAll('.fader__bump')).toHaveLength(48 + 24);
    expect(root.querySelectorAll('.masters__ind')).toHaveLength(2);
  });

  describe('when the backend is not running', () => {
    it('renders with the status disconnected and writes nothing to the console', async () => {
      const errors = vi.spyOn(console, 'error').mockImplementation(() => undefined);
      const warnings = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
      hub.startOutcomes = ['fail', 'fail', 'fail'];

      const fixture = TestBed.createComponent(AppComponent);
      TestBed.inject(ConsoleConnectionService).start();
      await settle();
      fixture.detectChanges();
      const root = element(fixture);

      expect(text(root, '.bar__status-label')).toBe('Disconnected');
      expect(text(root, '.bar__status-detail')).toBe('Backend not reachable, retrying');
      expect(text(root, '.bar__mode')).toBe('Mode unknown');
      expect(root.querySelector('.bar__error')).toBeNull();
      expect(root.querySelectorAll<HTMLInputElement>('input[type="range"]')).toHaveLength(
        FADER_COUNT,
      );
      expect(
        Array.from(root.querySelectorAll<HTMLInputElement>('input[type="range"]')).every(
          (slider) => slider.disabled,
        ),
      ).toBe(true);
      expect(
        Array.from(root.querySelectorAll<HTMLButtonElement>('.fader__bump')).every(
          (button) => button.disabled,
        ),
      ).toBe(true);
      expect(errors).not.toHaveBeenCalled();
      expect(warnings).not.toHaveBeenCalled();
    });
  });

  describe('when the backend is running', () => {
    it('shows the state it reports, with the mode in the badge', async () => {
      const fixture = TestBed.createComponent(AppComponent);
      TestBed.inject(ConsoleConnectionService).start();
      await settle();
      fixture.detectChanges();
      const root = element(fixture);

      expect(text(root, '.bar__mode')).toBe('Simulated');
      expect(text(root, '.bar__status-label')).toBe('Connected');
      expect(text(root, '.masters__blackout')).toBe('Blackout');
      expect(text(root, '.masters__solo')).toBe('Solo');
      expect(
        Array.from(root.querySelectorAll<HTMLInputElement>('input[type="range"]')).filter(
          (slider) => slider.disabled,
        ),
      ).toHaveLength(INACTIVE_FADERS);
    });

    it('applies what the backend pushes to the faders', async () => {
      const fixture = TestBed.createComponent(AppComponent);
      TestBed.inject(ConsoleConnectionService).start();
      await settle();

      hub.emit('channelStateChanged', [
        { channel: 4, desired: 255, actual: 255, pending: false, output: 255 },
      ]);
      hub.emit('memoriesChanged', [
        { page: 1, fader: 6, number: 6, level: 200, recorded: true, label: 'SIX' },
      ]);
      fixture.detectChanges();

      const sliders = element(fixture).querySelectorAll<HTMLInputElement>('input[type="range"]');
      expect(sliders[3].valueAsNumber).toBe(255);
      expect(sliders[48 + 5].valueAsNumber).toBe(200);
      expect(TestBed.inject(ConsoleStateService).channels()[3].desired).toBe(255);
      expect(TestBed.inject(ConsoleStateService).memories()[5].label).toBe('SIX');
    });
  });
});
