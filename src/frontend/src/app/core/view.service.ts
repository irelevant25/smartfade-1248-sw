import { Injectable, signal } from '@angular/core';

/**
 * The views of the app, switched in the top bar: LIVE (the console, the default), PATCH (the show's patch
 * matrix) and STACK / SEQ (the show's stack and sequences). One is shown at a time.
 */
export type AppView = 'live' | 'patch' | 'stack';

@Injectable({ providedIn: 'root' })
export class ViewService {
  private readonly _view = signal<AppView>('live');

  readonly view = this._view.asReadonly();

  show(view: AppView): void {
    this._view.set(view);
  }
}
