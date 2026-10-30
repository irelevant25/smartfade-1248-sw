import type { ConnectionDto } from '../../core/console.models';
import { canConnect, canDisconnect, describeMode, describeStatus } from './connection-view';

const device = (overrides: Partial<ConnectionDto> = {}): ConnectionDto => ({
  status: 'connected',
  mode: 'simulated',
  deviceDescription: 'Simulated SmartFade 1248',
  ...overrides,
});

describe('connection view', () => {
  describe('describeMode', () => {
    it('shows SIMULATED as a warning', () => {
      expect(describeMode('simulated')).toMatchObject({ label: 'Simulated', tone: 'warn' });
    });

    it('shows REPLAY as information', () => {
      expect(describeMode('replay')).toMatchObject({ label: 'Replay', tone: 'info' });
    });

    it('says the mode is unknown until the backend has reported it', () => {
      expect(describeMode(undefined)).toMatchObject({ label: 'Mode unknown', tone: 'unknown' });
    });

    it('shows a mode it does not know as an alert instead of passing it off as a known one', () => {
      const unexpected = 'hardware' as unknown as Parameters<typeof describeMode>[0];

      expect(describeMode(unexpected)).toMatchObject({ label: 'HARDWARE', tone: 'alert' });
    });
  });

  describe('describeStatus', () => {
    it('reports the backend link before anything else', () => {
      expect(describeStatus('disconnected', device())).toMatchObject({
        label: 'Disconnected',
        tone: 'idle',
        detail: 'Backend not reachable, retrying',
      });
      expect(describeStatus('connecting', undefined)).toMatchObject({
        label: 'Connecting',
        tone: 'busy',
      });
      expect(describeStatus('reconnecting', device())).toMatchObject({
        label: 'Reconnecting',
        tone: 'busy',
      });
    });

    it('reports the console once the link is up', () => {
      expect(describeStatus('connected', device())).toEqual({
        label: 'Connected',
        tone: 'ok',
        detail: 'Simulated SmartFade 1248',
      });
      expect(describeStatus('connected', device({ status: 'connecting' }))).toMatchObject({
        label: 'Connecting',
        tone: 'busy',
      });
      expect(describeStatus('connected', device({ status: 'error' }))).toMatchObject({
        label: 'Error',
        tone: 'error',
      });
      expect(describeStatus('connected', device({ status: 'disconnected' }))).toMatchObject({
        label: 'Disconnected',
        tone: 'idle',
        detail: 'Console not connected',
      });
    });

    it('treats a missing report as a console that is not connected', () => {
      expect(describeStatus('connected', undefined)).toMatchObject({
        label: 'Disconnected',
        tone: 'idle',
      });
    });
  });

  describe('canConnect / canDisconnect', () => {
    it('allow Connect only when the link is up and the console is not', () => {
      expect(canConnect('connected', device({ status: 'disconnected' }))).toBe(true);
      expect(canConnect('connected', device({ status: 'error' }))).toBe(true);
      expect(canConnect('connected', undefined)).toBe(true);
      expect(canConnect('connected', device({ status: 'connected' }))).toBe(false);
      expect(canConnect('connected', device({ status: 'connecting' }))).toBe(false);
      expect(canConnect('disconnected', device({ status: 'disconnected' }))).toBe(false);
      expect(canConnect('reconnecting', device({ status: 'disconnected' }))).toBe(false);
    });

    it('allow Disconnect only when the link is up and the console is connected or connecting', () => {
      expect(canDisconnect('connected', device({ status: 'connected' }))).toBe(true);
      expect(canDisconnect('connected', device({ status: 'connecting' }))).toBe(true);
      expect(canDisconnect('connected', device({ status: 'disconnected' }))).toBe(false);
      expect(canDisconnect('connected', device({ status: 'error' }))).toBe(false);
      expect(canDisconnect('connected', undefined)).toBe(false);
      expect(canDisconnect('disconnected', device({ status: 'connected' }))).toBe(false);
    });
  });
});
