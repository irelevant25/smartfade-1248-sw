import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { LEVEL_MAX } from '../../core/console.models';
import { ChannelFaderComponent } from '../channel-fader/channel-fader.component';

/**
 * The console's MASTERS section: BUMPS fader with the SOLO key above it on the left, MASTER fader
 * with the BLACKOUT key above it on the right (the owner's panel, 2026-10-29 and 2026-10-30); the
 * INDEPENDENTS are their own section under it since 2026-11-02 (`sf-independents-panel`). The
 * BLACKOUT key blinks while the MASTER is below full and lights up while blackout is on (manual, F5).
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
  /** A device cleared for some of the masters only (docs/api.md `writableControls`) enables just those. */
  protected readonly writable = (control: string) => this._state.writable(control);
  /** Until the device reported the masters, what they show is a placeholder, not console state. */
  protected readonly mastersReported = this._state.mastersReported;
  /**
   * The real console's download at Connect (SC-4) carries no FaderInfo, so the MASTER stays unreported until a fader moves; the
   * device then taps the bump of an unpatched channel once (the MASTER probe, clause 4). Only when that could not run does the
   * panel ask for one movement of the MASTER, with the reason as its title.
   */
  protected readonly moveMasterHint = computed(() => {
    const stateAtConnect = this._state.stateAtConnect();
    return !this.mastersReported() && stateAtConnect?.masterProbe === 'notPossible'
      ? stateAtConnect.masterProbeReason ?? 'The console has not reported the MASTER'
      : undefined;
  });
  /**
   * The real console is only observed: SOLO and BLACKOUT are LED readings (F100, F114) that arrive
   * with the masters, and the independents come from their own object (F115), so each key says
   * what it rests on, and says "not reported" until that part arrived.
   */
  protected readonly observedOnly = computed(() => this._state.mode() === 'hardware');
  /**
   * SOLO and BLACKOUT are LED readings (blocks 52 / 53, F100 / F114) that a real console delivers with LedStatus, before
   * its first FaderInfo: once the key LEDs are known the two keys mirror them like the record keys do (lit, full opacity,
   * read-only), and only before that are they unknown (dashed). The simulator reports them with its masters.
   */
  protected readonly ledMode = computed(() => this._state.keys() !== undefined);
  protected readonly soloUnknown = computed(() => this.observedOnly() && !this.mastersReported() && !this.ledMode());
  protected readonly blackoutUnknown = computed(() => this.observedOnly() && !this.mastersReported() && !this.ledMode());
  protected readonly soloOn = computed(() => {
    const keys = this._state.keys();
    return keys ? keys.solo.light === 'full' : this.masters().solo;
  });
  /** BLACKOUT's LED is steady blue while blackout is on (F114); its blink means the MASTER is below full (F85), not blackout. */
  protected readonly blackoutOn = computed(() => {
    const keys = this._state.keys();
    return keys ? keys.blackout.light === 'full' && !keys.blackout.blinking : this.masters().blackout;
  });
  /**
   * "As soon as the Master is faded under 100% the Blackout button will blink" (F5). The simulator
   * follows the manual; the real console reports its BLACKOUT LED, whose blink (F85) is shown as
   * reported, and nothing is assumed while no LED report exists.
   */
  protected readonly blackoutBlinks = computed(() => {
    const keys = this._state.keys();
    if (keys) {
      return keys.blackout.blinking;   // the console's own LED (F85): the reading, not the rule
    }
    return (
      this.enabled() && !this.observedOnly() && !this.masters().blackout && this.masters().master < LEVEL_MAX
    );
  });

  protected setMaster(level: number): void {
    this._channels.setMaster(level);
  }

  protected setBumps(level: number): void {
    this._channels.setBumps(level);
  }

  protected toggleBlackout(): void {
    this._channels.setBlackout(!this.blackoutOn());
  }

  protected toggleSolo(): void {
    this._channels.setSolo(!this.soloOn());
  }
}
