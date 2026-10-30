import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ClearKeyService } from '../../core/clear-key.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { ChannelFaderComponent } from '../channel-fader/channel-fader.component';

/**
 * The console's crossfader section (manual v3.0 printed p.60): the Live (A) and Next (B)
 * faders with the GO and STACK keys under A and the PAUSE and CLEAR keys under B. The stack and
 * crossfades are not in the simulator yet, so the faders, GO, PAUSE and STACK are shown
 * inactive; CLEAR works as on the console (one press aborts an armed function, two presses
 * clear the channel faders, three the memory faders).
 */
@Component({
  selector: 'sf-crossfader',
  imports: [ChannelFaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './crossfader.component.html',
  styleUrl: './crossfader.component.scss',
})
export class CrossfaderComponent {
  private readonly _channels = inject(ChannelService);
  private readonly _clear = inject(ClearKeyService);
  private readonly _state = inject(ConsoleStateService);

  protected readonly enabled = this._channels.enabled;
  protected readonly canClear = computed(() => this._state.supports('clearChannels'));
  protected readonly clearPresses = this._clear.presses;
  /** A real console reports the STACK and CLEAR LEDs (F117, F116; identities inferred, H72). */
  protected readonly keys = this._state.keys;
  protected readonly stackLed = computed(() => this._state.keys()?.stack);
  protected readonly clearLed = computed(() => this._state.keys()?.clear);
  protected readonly ledTitle = 'LED reading from the console (STACK identified by a timed press, E8b; CLEAR inferred)';

  protected clear(): void {
    this._clear.press();
  }
}
