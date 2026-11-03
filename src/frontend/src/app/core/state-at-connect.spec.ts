import { TestBed } from '@angular/core/testing';
import { describeStateAtConnect } from '../components/connection-bar/connection-view';
import { CONSOLE_HUB_FACTORY } from './console-hub';
import { ConsoleConnectionService } from './console-connection.service';
import { ConsoleStateService } from './console-state.service';
import type { StateAtConnectDto } from './console.models';
import { ShowStore } from './show.store';
import { FakeConsoleHub } from './testing/fake-console-hub';
import { sampleShow } from './testing/sample-show';
import { createSampleState } from './testing/sample-state';

/** Lets every already-resolved promise continuation run (real timers only). */
const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

const downloading: StateAtConnectDto = { phase: 'downloading', records: 157, delivered: 40, reason: null, masterProbe: 'pending', masterProbeReason: null };

/** The real console's state at Connect (safety check SC-4): what the UI shows of the start-up pair and its download. */
describe('state at Connect (SC-4)', () => {
  describe('describeStateAtConnect', () => {
    it('says nothing without the switch, nor once the download is complete', () => {
      expect(describeStateAtConnect(undefined, 'connected')).toBeUndefined();
      expect(describeStateAtConnect({ ...downloading, phase: 'done', delivered: 157 }, 'connected')).toBeUndefined();
    });

    it('before Connect, says what the console must be showing and must not be doing when the pair goes blind', () => {
      const idle: StateAtConnectDto = { phase: 'idle', delivered: 0, masterProbe: 'pending' };
      expect(describeStateAtConnect(idle, 'disconnected')).toEqual({
        text: 'Before Connect: page 1, bank 1-24 or memory mode, NEXT and BLACKOUT off; no menu open on the console, no SD save or load, no record function armed, no bump held',
        tone: 'idle',
      });
      expect(describeStateAtConnect(idle, 'connected')).toBeUndefined();
    });

    it('after the download, says where the console was left when the MASTER probe stopped, also with the MASTER known', () => {
      const reason =
        'the MASTER probe stopped (REC MEM is lit or blinking (SC-2 condition 5)) and did not switch back: the console shows channel mode, bank 25-48; the download reported channel mode, bank 1-24';
      const stopped: StateAtConnectDto = { phase: 'done', records: 157, delivered: 157, reason: null, masterProbe: 'stopped', masterProbeReason: reason };
      expect(describeStateAtConnect(stopped, 'connected')).toEqual({
        text: 'The MASTER probe stopped (REC MEM is lit or blinking (SC-2 condition 5)) and did not switch back: the console shows channel mode, bank 25-48; the download reported channel mode, bank 1-24',
        tone: 'warn',
      });
      expect(describeStateAtConnect({ ...stopped, masterProbe: 'sent', masterProbeReason: null }, 'connected')).toBeUndefined();
      expect(describeStateAtConnect({ ...stopped, masterProbe: 'notPossible', masterProbeReason: 'every channel of both banks is patched' }, 'connected')).toBeUndefined();
    });

    it('while the pair and the download run, says that the state is requested or downloaded, with the progress', () => {
      expect(describeStateAtConnect({ ...downloading, phase: 'waiting', records: null, delivered: 0 }, 'connected')).toEqual({ text: "Requesting the console's state…", tone: 'busy' });
      expect(describeStateAtConnect({ ...downloading, phase: 'requesting', records: null, delivered: 0 }, 'connected')?.tone).toBe('busy');
      expect(describeStateAtConnect(downloading, 'connected')).toEqual({ text: "Downloading the console's state… 40 / 157", tone: 'busy' });
      expect(describeStateAtConnect({ ...downloading, records: null }, 'connected')?.text).toBe("Downloading the console's state…");
    });

    it('says why the state was not requested', () => {
      const refused: StateAtConnectDto = { phase: 'notSent', delivered: 0, reason: '09 00 00 refused: NEXT mode is on', masterProbe: 'notPossible' };
      expect(describeStateAtConnect(refused, 'connected')).toEqual({ text: "The console's state was not requested: 09 00 00 refused: NEXT mode is on", tone: 'warn' });
    });
  });

  describe('ConsoleStateService', () => {
    it('takes the state at Connect from the full state and from its own event, and drops what it does not know', () => {
      const state = TestBed.inject(ConsoleStateService);
      expect(state.stateAtConnect()).toBeUndefined();

      state.applyConsoleStateChanged(createSampleState({ stateAtConnect: { phase: 'idle', delivered: 0, masterProbe: 'pending' } }));
      expect(state.stateAtConnect()?.phase).toBe('idle');

      state.applyStateAtConnectChanged(downloading);
      expect(state.stateAtConnect()).toEqual(downloading);

      state.applyStateAtConnectChanged({ ...downloading, phase: 'unknown' as StateAtConnectDto['phase'] });
      expect(state.stateAtConnect()).toBeUndefined();

      state.applyConsoleStateChanged(createSampleState());   // a device without the switch sends none
      expect(state.stateAtConnect()).toBeUndefined();
    });
  });

  describe('ShowStore.offerConsoleShow', () => {
    let store: ShowStore;

    beforeEach(() => {
      store = new ShowStore();
    });

    it('fills the store with the console show, sourced as the console, replacing an unedited show', () => {
      store.setShow(sampleShow(), 'SHOW07.ASC', 'file');
      store.offerConsoleShow({ ...sampleShow(), name: 'SHOW14.ASC (console)' });

      expect(store.source()).toBe('console');
      expect(store.name()).toBe('SHOW14.ASC (console)');
      expect(store.dirty()).toBe(false);
      expect(store.pendingConsoleShow()).toBeUndefined();
    });

    it('never throws a file with unsaved edits away: the console show waits until the user takes it', () => {
      store.setShow(sampleShow(), 'SHOW07.ASC', 'file');
      store.setPatchChannel(1, 12);
      store.offerConsoleShow({ ...sampleShow(), name: 'SHOW14.ASC (console)' });

      expect(store.source()).toBe('file');
      expect(store.show()!.patch[0].channel).toBe(12);
      expect(store.pendingConsoleShow()?.name).toBe('SHOW14.ASC (console)');

      expect(store.takePendingConsoleShow()).toBe(true);
      expect(store.source()).toBe('console');
      expect(store.show()!.patch[0].channel).toBe(1);
      expect(store.pendingConsoleShow()).toBeUndefined();
      expect(store.takePendingConsoleShow()).toBe(false);
    });

    it('replaces an edited console show with a newer one from the console', () => {
      store.offerConsoleShow({ ...sampleShow(), name: 'SHOW14.ASC (console)' });
      store.setPatchChannel(1, 12);
      store.offerConsoleShow({ ...sampleShow(), name: 'SHOW14.ASC (console)' });

      expect(store.show()!.patch[0].channel).toBe(1);
      expect(store.pendingConsoleShow()).toBeUndefined();
    });
  });

  describe('ConsoleConnectionService', () => {
    let hub: FakeConsoleHub;

    beforeEach(async () => {
      hub = new FakeConsoleHub();
      hub.results.set('getState', createSampleState());
      TestBed.configureTestingModule({ providers: [{ provide: CONSOLE_HUB_FACTORY, useValue: () => hub }] });
      TestBed.inject(ConsoleConnectionService).start();
      await settle();
    });

    it('applies stateAtConnectChanged', () => {
      hub.emit('stateAtConnectChanged', downloading);

      expect(TestBed.inject(ConsoleStateService).stateAtConnect()).toEqual(downloading);
    });

    it("hands the console's show of consoleShowChanged to the store, which fills the PATCH and STACK / SEQ pages", () => {
      const show = { ...sampleShow(), name: 'SHOW14.ASC (console)' };
      hub.emit('consoleShowChanged', show);

      const store = TestBed.inject(ShowStore);
      expect(store.source()).toBe('console');
      expect(store.name()).toBe('SHOW14.ASC (console)');
      expect(store.show()!.mainStack!.steps).toHaveLength(show.mainStack!.steps.length);
    });
  });
});
