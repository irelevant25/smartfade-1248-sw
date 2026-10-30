import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConsoleConnectionService, type LinkStatus } from '../../core/console-connection.service';
import { ConsoleStateService } from '../../core/console-state.service';
import type { ConnectionDto } from '../../core/console.models';
import { createSampleState } from '../../core/testing/sample-state';
import { ConnectionBarComponent } from './connection-bar.component';

describe('ConnectionBarComponent', () => {
  let fixture: ComponentFixture<ConnectionBarComponent>;
  let state: ConsoleStateService;
  let link: ReturnType<typeof signal<LinkStatus>>;
  let connect: ReturnType<typeof vi.fn>;
  let disconnect: ReturnType<typeof vi.fn>;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = (selector: string) => element().querySelector(selector)?.textContent?.trim();
  const badge = () => element().querySelector<HTMLElement>('.bar__mode')!;
  const buttons = () => {
    const [connectKey, disconnectKey] = Array.from(element().querySelectorAll('button.sf-key'));
    return {
      connect: connectKey as HTMLButtonElement,
      disconnect: disconnectKey as HTMLButtonElement,
    };
  };
  const deviceIs = (overrides: Partial<ConnectionDto>) => {
    state.applyConnectionStateChanged({ ...createSampleState().connection, ...overrides });
    fixture.detectChanges();
  };

  beforeEach(() => {
    link = signal<LinkStatus>('disconnected');
    connect = vi.fn();
    disconnect = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ConsoleConnectionService,
          useValue: {
            status: link.asReadonly(),
            busy: signal(false).asReadonly(),
            connect,
            disconnect,
          },
        },
      ],
    });
    state = TestBed.inject(ConsoleStateService);
    fixture = TestBed.createComponent(ConnectionBarComponent);
    fixture.detectChanges();
  });

  describe('without a backend', () => {
    it('renders, shows the console as disconnected and the mode as unknown', () => {
      expect(text('h1')).toBe('SmartFade1248');
      expect(text('.bar__mode')).toBe('Mode unknown');
      expect(badge().dataset['tone']).toBe('unknown');
      expect(text('.bar__status-label')).toBe('Disconnected');
      expect(text('.bar__status-detail')).toBe('Backend not reachable, retrying');
    });

    it('offers neither Connect nor Disconnect', () => {
      expect(buttons().connect.disabled).toBe(true);
      expect(buttons().disconnect.disabled).toBe(true);
    });

    it('shows no show file, firmware or error', () => {
      expect(element().querySelector('.bar__fact')).toBeNull();
      expect(element().querySelector('.bar__error')).toBeNull();
    });
  });

  describe('with the backend and a connected simulated console', () => {
    beforeEach(() => {
      link.set('connected');
      state.applyConsoleStateChanged(createSampleState());
      fixture.detectChanges();
    });

    it('shows SIMULATED prominently as a warning', () => {
      expect(text('.bar__mode')).toBe('Simulated');
      expect(badge().dataset['tone']).toBe('warn');
    });

    it('shows the status and the device', () => {
      expect(text('.bar__status-label')).toBe('Connected');
      expect(text('.bar__status-detail')).toBe('Simulated SmartFade 1248');
      expect(element().querySelector('.bar__status')?.getAttribute('data-tone')).toBe('ok');
    });

    it('shows the show file name and the firmware version', () => {
      const facts = Array.from(element().querySelectorAll('.bar__fact')).map((fact) => [
        fact.querySelector('dt')?.textContent?.trim(),
        fact.querySelector('dd')?.textContent?.trim(),
      ]);

      expect(facts).toEqual([
        ['Show', 'SHOW04.ASC'],
        ['Firmware', '3.0.2.9.0.43'],
      ]);
    });

    it('omits the facts the backend does not know, whether left out or sent as null', () => {
      state.applyConsoleStateChanged(
        createSampleState({ showFileName: undefined, firmwareVersion: undefined }),
      );
      fixture.detectChanges();
      expect(element().querySelector('.bar__fact')).toBeNull();

      state.applyConsoleStateChanged(
        createSampleState({ showFileName: null, firmwareVersion: null }),
      );
      fixture.detectChanges();
      expect(element().querySelector('.bar__fact')).toBeNull();
    });

    it('shows no error strip when the connection error is null', () => {
      deviceIs({ error: null });

      expect(element().querySelector('.bar__error')).toBeNull();
    });

    it('allows Disconnect but not Connect', () => {
      expect(buttons().connect.disabled).toBe(true);
      expect(buttons().disconnect.disabled).toBe(false);
    });

    it('asks the connection service to disconnect', () => {
      buttons().disconnect.click();

      expect(disconnect).toHaveBeenCalledTimes(1);
      expect(connect).not.toHaveBeenCalled();
    });

    it('shows REPLAY in its own colour', () => {
      deviceIs({ mode: 'replay' });

      expect(text('.bar__mode')).toBe('Replay');
      expect(badge().dataset['tone']).toBe('info');
    });

    it('shows a mode it does not know as an alert', () => {
      deviceIs({ mode: 'hardware' as unknown as ConnectionDto['mode'] });

      expect(text('.bar__mode')).toBe('HARDWARE');
      expect(badge().dataset['tone']).toBe('alert');
    });
  });

  describe('with the console not connected', () => {
    beforeEach(() => {
      link.set('connected');
      state.applyConsoleStateChanged(
        createSampleState({
          connection: {
            status: 'disconnected',
            mode: 'simulated',
            deviceDescription: 'Simulated SmartFade 1248',
          },
        }),
      );
      fixture.detectChanges();
    });

    it('allows Connect but not Disconnect', () => {
      expect(text('.bar__status-label')).toBe('Disconnected');
      expect(buttons().connect.disabled).toBe(false);
      expect(buttons().disconnect.disabled).toBe(true);
    });

    it('asks the connection service to connect', () => {
      buttons().connect.click();

      expect(connect).toHaveBeenCalledTimes(1);
      expect(disconnect).not.toHaveBeenCalled();
    });
  });

  describe('link states', () => {
    it.each([
      ['connecting', 'Connecting'],
      ['reconnecting', 'Reconnecting'],
    ] as const)('shows %s as busy', (status, label) => {
      link.set(status);
      fixture.detectChanges();

      expect(text('.bar__status-label')).toBe(label);
      expect(element().querySelector('.bar__status')?.getAttribute('data-tone')).toBe('busy');
    });
  });

  describe('errors', () => {
    it('shows the last error reported to the page and lets the user dismiss it', () => {
      state.applyErrorOccurred({ message: 'Channel out of range', code: 'range' });
      fixture.detectChanges();

      expect(text('.bar__error-text')).toBe('Channel out of range');
      expect(element().querySelector('.bar__error')?.getAttribute('role')).toBe('alert');

      element().querySelector<HTMLButtonElement>('.bar__error-dismiss')!.click();
      fixture.detectChanges();

      expect(element().querySelector('.bar__error')).toBeNull();
    });

    it('shows an error of the console connection, which cannot be dismissed', () => {
      link.set('connected');
      deviceIs({ status: 'error', error: 'The simulated console stopped.' });

      expect(text('.bar__error-text')).toBe('The simulated console stopped.');
      expect(element().querySelector('.bar__error-dismiss')).toBeNull();
      expect(text('.bar__status-label')).toBe('Error');
    });
  });
});
