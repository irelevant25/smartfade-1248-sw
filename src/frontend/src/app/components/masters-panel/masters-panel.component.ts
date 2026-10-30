import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { LEVEL_MAX } from '../../core/console.models';
import { ChannelFaderComponent } from '../channel-fader/channel-fader.component';

/**
 * The right-hand section of the console: BUMPS fader with the SOLO key above it on the left,
 * MASTER fader with the BLACKOUT key above it on the right, and the INDEPENDENTS keys IND 1 and
 * IND 2 below them (the owner's panel, 2026-10-29 and 2026-10-30). The BLACKOUT key blinks while
 * the MASTER is below full and lights up while blackout is on (manual, F5).
 */
@Component({
  selector: 'sf-masters-panel',
  imports: [ChannelFaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './masters-panel.component.html',
  styleUrl: './masters-panel.component.scss',
})
export class MastersPanelComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _channels = inject(ChannelService);

  protected readonly masters = this._state.masters;
  protected readonly enabled = this._channels.enabled;
  protected readonly supports = (command: string) => this._state.supports(command);
  /** Until the device reported the masters, what they show is a placeholder, not console state. */
  protected readonly mastersReported = this._state.mastersReported;
  /**
   * The real console is only observed: SOLO and BLACKOUT are LED readings (F100, F114) that arrive
   * with the masters, and the independents come from their own object (F115), so each key says
   * what it rests on, and says "not reported" until that part arrived.
   */
  protected readonly observedOnly = computed(() => this._state.mode() === 'hardware');
  protected readonly blackoutUnknown = computed(() => this.observedOnly() && !this.mastersReported());
  protected readonly independentsUnknown = computed(() => this.observedOnly() && !this._state.independentsReported());
  /**
   * "As soon as the Master is faded under 100% the Blackout button will blink" (F5). The simulator
   * follows the manual; the real console is only observed, and its key shows the LED's steady
   * state only (the blink of the LED itself, F85, is not rendered), so the key does not blink in
   * hardware mode.
   */
  protected readonly blackoutBlinks = computed(
    () =>
      this.enabled() &&
      !this.observedOnly() &&
      !this.masters().blackout &&
      this.masters().master < LEVEL_MAX,
  );

  protected setMaster(level: number): void {
    this._channels.setMaster(level);
  }

  protected setBumps(level: number): void {
    this._channels.setBumps(level);
  }

  protected toggleBlackout(): void {
    this._channels.setBlackout(!this.masters().blackout);
  }

  protected toggleSolo(): void {
    this._channels.setSolo(!this.masters().solo);
  }

  protected toggleIndependent(number: 1 | 2): void {
    const on = number === 1 ? this.masters().independent1 : this.masters().independent2;
    this._channels.setIndependent(number, !on);
  }
}
