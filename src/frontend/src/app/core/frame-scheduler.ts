import { InjectionToken } from '@angular/core';

/**
 * Schedules work for the next animation frame. Injected (instead of calling the browser API
 * directly) so that tests can drive frames by hand.
 */
export interface FrameScheduler {
  request(callback: () => void): number;
  cancel(handle: number): void;
}

export const FRAME_SCHEDULER = new InjectionToken<FrameScheduler>('FRAME_SCHEDULER', {
  providedIn: 'root',
  factory: () => ({
    request: (callback) => requestAnimationFrame(() => callback()),
    cancel: (handle) => cancelAnimationFrame(handle),
  }),
});
