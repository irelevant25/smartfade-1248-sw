import { Injectable, signal } from '@angular/core';

/** The pages of the app: the console (the default) and the show-file viewer. One is shown at a time. */
export type AppView = 'console' | 'show-file';

@Injectable({ providedIn: 'root' })
export class ViewService {
  private readonly _view = signal<AppView>('console');

  readonly view = this._view.asReadonly();

  show(view: AppView): void {
    this._view.set(view);
  }
}
