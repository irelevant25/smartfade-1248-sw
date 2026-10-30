import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ChannelService } from './channel.service';
import { MemoryEditService } from './memory-edit.service';

/** Presses closer together than this count as one series (the manual says "in rapid succession"; the figure is ours). */
export const CLEAR_SERIES_MS = 1000;

/**
 * The console's CLEAR key (manual v3.0 printed p.56, F109): it never erases content. One press
 * aborts an armed REC MEM / EDIT MEM / COPY; two presses in a row set every channel fader to
 * zero; three presses set every memory fader to zero. (The fourth press, sequence faders, has
 * nothing to act on in the simulator yet.)
 */
@Injectable({ providedIn: 'root' })
export class ClearKeyService {
  private readonly _channels = inject(ChannelService);
  private readonly _edit = inject(MemoryEditService);

  private readonly _presses = signal(0);
  private _timer: ReturnType<typeof setTimeout> | undefined;

  /** Presses in the current series, for the key's own feedback; 0 when the series is over. */
  readonly presses = this._presses.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this._timer));
  }

  press(): void {
    if (!this._channels.enabled()) {
      return;
    }
    const count = this._presses() + 1;
    this._presses.set(count);
    clearTimeout(this._timer);
    this._timer = setTimeout(() => this._presses.set(0), CLEAR_SERIES_MS);

    switch (count) {
      case 1:
        this._edit.cancel();
        break;
      case 2:
        this._channels.clearChannels();
        break;
      case 3:
        this._channels.clearMemories();
        break;
      default:
        break;
    }
  }
}
