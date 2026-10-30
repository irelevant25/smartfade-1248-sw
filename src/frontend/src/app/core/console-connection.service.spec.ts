import { TestBed } from '@angular/core/testing';
import { CONSOLE_HUB_FACTORY, retryDelay } from './console-hub';
import { ConsoleConnectionService } from './console-connection.service';
import { ConsoleStateService } from './console-state.service';
import { FakeConsoleHub } from './testing/fake-console-hub';
import { createSampleState } from './testing/sample-state';

/** Lets every already-resolved promise continuation run (real timers only). */
const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

describe('ConsoleConnectionService', () => {
  let hub: FakeConsoleHub;
  let state: ConsoleStateService;
  let service: ConsoleConnectionService;

  beforeEach(() => {
    hub = new FakeConsoleHub();
    hub.results.set('getState', createSampleState());
    TestBed.configureTestingModule({
      providers: [{ provide: CONSOLE_HUB_FACTORY, useValue: () => hub }],
    });
    state = TestBed.inject(ConsoleStateService);
    service = TestBed.inject(ConsoleConnectionService);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('opening the link', () => {
    it('is disconnected until started and does not touch the hub', () => {
      expect(service.status()).toBe('disconnected');
      expect(hub.startCalls).toBe(0);
    });

    it('connects, then reads the full state with getState()', async () => {
      service.start();
      expect(service.status()).toBe('connecting');

      await settle();

      expect(service.status()).toBe('connected');
      expect(hub.callsTo('getState')).toHaveLength(1);
      expect(state.connection()?.mode).toBe('simulated');
      expect(state.channels()[0].desired).toBe(255);
    });

    it('starts only once however often start() is called', async () => {
      service.start();
      service.start();
      await settle();

      expect(hub.startCalls).toBe(1);
    });

    it('keeps retrying while the backend is unreachable, with a growing delay', async () => {
      vi.useFakeTimers();
      hub.startOutcomes = ['fail', 'fail'];

      service.start();
      await vi.advanceTimersByTimeAsync(0);
      expect(hub.startCalls).toBe(1);
      expect(service.status()).toBe('disconnected');

      await vi.advanceTimersByTimeAsync(retryDelay(0) - 1);
      expect(hub.startCalls).toBe(1);
      await vi.advanceTimersByTimeAsync(1);
      expect(hub.startCalls).toBe(2);
      expect(service.status()).toBe('disconnected');

      expect(retryDelay(1)).toBeGreaterThan(retryDelay(0));
      await vi.advanceTimersByTimeAsync(retryDelay(1));
      expect(hub.startCalls).toBe(3);
      expect(service.status()).toBe('connected');
    });

    it('stops retrying once stop() is called', async () => {
      vi.useFakeTimers();
      hub.startOutcomes = ['fail', 'fail', 'fail'];

      service.start();
      await vi.advanceTimersByTimeAsync(0);
      await service.stop();
      await vi.advanceTimersByTimeAsync(60_000);

      expect(hub.startCalls).toBe(1);
      expect(service.status()).toBe('disconnected');
    });
  });

  describe('server events', () => {
    beforeEach(async () => {
      service.start();
      await settle();
    });

    it('applies consoleStateChanged', () => {
      hub.emit(
        'consoleStateChanged',
        createSampleState({ masters: { master: 12, bumps: 3, blackout: true, solo: true, independent1: false, independent2: false } }),
      );

      expect(state.masters()).toEqual({ master: 12, bumps: 3, blackout: true, solo: true, independent1: false, independent2: false });
    });

    it('applies connectionStateChanged', () => {
      hub.emit('connectionStateChanged', {
        status: 'disconnected',
        mode: 'simulated',
        deviceDescription: 'Simulated SmartFade 1248',
      });

      expect(state.deviceStatus()).toBe('disconnected');
    });

    it('applies channelStateChanged', () => {
      hub.emit('channelStateChanged', [
        { channel: 10, desired: 77, actual: 70, pending: true, output: 70 },
      ]);

      expect(state.channels()[9]).toEqual({
        channel: 10,
        desired: 77,
        actual: 70,
        pending: true,
        output: 70,
      });
    });

    it('applies mastersChanged', () => {
      hub.emit('mastersChanged', { master: 1, bumps: 2, blackout: true, solo: false, independent1: false, independent2: false });

      expect(state.masters()).toEqual({ master: 1, bumps: 2, blackout: true, solo: false, independent1: false, independent2: false });
    });

    it('applies outputsChanged', () => {
      hub.emit('outputsChanged', { outputs: [5, 6, 7] });

      expect(state.outputs().slice(0, 3)).toEqual([5, 6, 7]);
    });

    it('applies errorOccurred and refreshes the state, because the rejected command may have left optimistic values behind', async () => {
      const refreshesBefore = hub.callsTo('getState').length;

      hub.emit('errorOccurred', { message: 'Channel out of range', code: 'range' });
      await settle();

      expect(state.lastError()).toEqual({ message: 'Channel out of range', code: 'range' });
      expect(hub.callsTo('getState')).toHaveLength(refreshesBefore + 1);
    });
  });

  describe('losing and regaining the link', () => {
    beforeEach(async () => {
      service.start();
      await settle();
    });

    it('shows reconnecting, then reads the state again when the link is back', async () => {
      hub.simulateReconnecting();
      expect(service.status()).toBe('reconnecting');

      hub.simulateReconnected();
      expect(service.status()).toBe('connected');
      await settle();

      expect(hub.callsTo('getState')).toHaveLength(2);
    });

    it('connects again when the server closes the connection, and says so', async () => {
      hub.simulateClosed();
      expect(service.status()).toBe('reconnecting');

      await settle();

      expect(hub.startCalls).toBe(2);
      expect(service.status()).toBe('connected');
    });

    it('does not start over after stop()', async () => {
      await service.stop();
      await settle();

      expect(hub.stopCalls).toBe(1);
      expect(hub.startCalls).toBe(1);
      expect(service.status()).toBe('disconnected');
    });
  });

  describe('connect() and disconnect()', () => {
    it('do nothing while the link is down', async () => {
      await service.connect();
      await service.disconnect();

      expect(hub.invocations).toHaveLength(0);
    });

    it('call the hub methods of the same name', async () => {
      service.start();
      await settle();

      await service.connect();
      await service.disconnect();

      expect(hub.callsTo('connect')).toHaveLength(1);
      expect(hub.callsTo('disconnect')).toHaveLength(1);
    });

    it('send one request at a time, so a double click cannot repeat a side effect', async () => {
      service.start();
      await settle();

      const first = service.connect();
      const second = service.connect();
      expect(service.busy()).toBe(true);
      await Promise.all([first, second]);

      expect(hub.callsTo('connect')).toHaveLength(1);
      expect(service.busy()).toBe(false);
    });

    it('report a failure without retrying', async () => {
      service.start();
      await settle();
      hub.failing.add('connect');

      await service.connect();

      expect(hub.callsTo('connect')).toHaveLength(1);
      expect(state.lastError()?.message).toContain('connect');
      expect(service.busy()).toBe(false);
    });

    it('clear the previous error before a new request', async () => {
      service.start();
      await settle();
      state.applyErrorOccurred({ message: 'old' });

      await service.connect();

      expect(state.lastError()).toBeUndefined();
    });
  });

  describe('getState()', () => {
    it('shares one request between concurrent calls', async () => {
      service.start();
      await settle();
      const before = hub.callsTo('getState').length;

      await Promise.all([service.getState(), service.getState()]);

      expect(hub.callsTo('getState')).toHaveLength(before + 1);
    });

    it('reports a failure', async () => {
      service.start();
      await settle();
      hub.failing.add('getState');

      const result = await service.getState();

      expect(result).toBeUndefined();
      expect(state.lastError()?.message).toContain('getState');
    });
  });

  describe('commands', () => {
    beforeEach(async () => {
      service.start();
      await settle();
    });

    it('call the hub methods with the arguments of the contract', async () => {
      await service.sendChannelLevel(3, 40);
      await service.sendMaster(90);
      await service.sendBlackout(true);

      expect(hub.callsTo('setChannelLevel')).toEqual([
        { method: 'setChannelLevel', args: [3, 40] },
      ]);
      expect(hub.callsTo('setMaster')).toEqual([{ method: 'setMaster', args: [90] }]);
      expect(hub.callsTo('setBlackout')).toEqual([{ method: 'setBlackout', args: [true] }]);
    });

    it('are sent once: a failure is reported and followed by a state refresh, not a retry', async () => {
      hub.failing.add('setMaster');
      const refreshesBefore = hub.callsTo('getState').length;

      await service.sendMaster(90);
      await settle();

      expect(hub.callsTo('setMaster')).toHaveLength(1);
      expect(state.lastError()?.message).toContain('setMaster');
      expect(hub.callsTo('getState')).toHaveLength(refreshesBefore + 1);
    });

    it('are dropped while the link is down', async () => {
      hub.simulateReconnecting();

      await service.sendChannelLevel(3, 40);

      expect(hub.callsTo('setChannelLevel')).toHaveLength(0);
    });
  });
});
