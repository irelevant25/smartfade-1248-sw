import type {
  ConsoleHub,
  ConsoleHubEventName,
  ConsoleHubEvents,
  ConsoleHubMethod,
} from '../console-hub';

export interface RecordedInvocation {
  method: ConsoleHubMethod;
  args: unknown[];
}

/**
 * In-memory stand-in for the SignalR hub, used by unit tests: it records the calls the UI makes
 * and lets a test emit server events and connection transitions. No network is involved.
 */
export class FakeConsoleHub implements ConsoleHub {
  readonly invocations: RecordedInvocation[] = [];
  /** Value that invoke() resolves with, per method. */
  readonly results = new Map<ConsoleHubMethod, unknown>();
  /** Methods whose invoke() rejects. */
  readonly failing = new Set<ConsoleHubMethod>();
  /** Outcomes of the next start() calls, consumed in order; start() succeeds when empty. */
  startOutcomes: Array<'succeed' | 'fail'> = [];

  startCalls = 0;
  stopCalls = 0;

  private _started = false;
  private readonly _handlers = new Map<string, (...args: unknown[]) => void>();
  private _reconnecting: ((error?: Error) => void) | undefined;
  private _reconnected: ((connectionId?: string) => void) | undefined;
  private _closed: ((error?: Error) => void) | undefined;

  start(): Promise<void> {
    this.startCalls++;
    if (this.startOutcomes.shift() === 'fail') {
      return Promise.reject(new Error('backend not reachable'));
    }
    this._started = true;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stopCalls++;
    if (this._started) {
      this._started = false;
      this._closed?.();
    }
    return Promise.resolve();
  }

  invoke<T = void>(method: ConsoleHubMethod, ...args: unknown[]): Promise<T> {
    this.invocations.push({ method, args });
    if (this.failing.has(method)) {
      return Promise.reject(new Error(`${method} failed`));
    }
    return Promise.resolve(this.results.get(method) as T);
  }

  on<K extends ConsoleHubEventName>(
    event: K,
    handler: (...args: ConsoleHubEvents[K]) => void,
  ): void {
    this._handlers.set(event, handler as (...args: unknown[]) => void);
  }

  onreconnecting(handler: (error?: Error) => void): void {
    this._reconnecting = handler;
  }

  onreconnected(handler: (connectionId?: string) => void): void {
    this._reconnected = handler;
  }

  onclose(handler: (error?: Error) => void): void {
    this._closed = handler;
  }

  /** Delivers a server event to the UI. */
  emit<K extends ConsoleHubEventName>(event: K, ...args: ConsoleHubEvents[K]): void {
    this._handlers.get(event)?.(...args);
  }

  simulateReconnecting(): void {
    this._reconnecting?.(new Error('connection lost'));
  }

  simulateReconnected(): void {
    this._reconnected?.('fake-connection');
  }

  /** The connection was closed by the server (as opposed to stop() called by the UI). */
  simulateClosed(): void {
    this._started = false;
    this._closed?.(new Error('closed by server'));
  }

  callsTo(method: ConsoleHubMethod): RecordedInvocation[] {
    return this.invocations.filter((call) => call.method === method);
  }
}
