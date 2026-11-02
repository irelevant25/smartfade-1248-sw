import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';

/**
 * The console's FADERS keys (manual F7; the owner's order and colours, 2026-11-02): MEMORIES (red),
 * 25-48 (green), 1-24 (green) and NEXT (blue). The 24 physical faders control one bank at a time, and
 * a real console reports which one (`faderBank`, ConsoleStatus, F98): the key of that bank is lit, as
 * its LED is on the console. MEMORIES, 25-48 and 1-24 are taps on a device that models the mode and
 * names the controls writable (the real console with live control: SC-2 class B1 for MEMORIES and
 * 1-24, SC-5 class B4 for 25-48; the key codes SmartSoft sends, E9 to E9e; the console refuses a key
 * from a bank SmartSoft never sent it from and says why); NEXT (the blind NEXT mode of the manual,
 * "Live or Next mode") is NO-GO under SC-5 and stays inactive. The simulator plays every bank at once,
 * so the keys mean nothing there and say so.
 */
@Component({
  selector: 'sf-fader-mode-keys',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fader-mode-keys.component.html',
  styleUrl: './fader-mode-keys.component.scss',
})
export class FaderModeKeysComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _channels = inject(ChannelService);

  protected readonly enabled = this._channels.enabled;
  /** What the console's physical faders control, when it reports it; undefined on the simulator and until reported. */
  protected readonly bank = this._state.faderBank;
  protected readonly reported = computed(() => this.bank() !== undefined);
  protected readonly canMems = computed(
    () => this._state.supports('pressFadersMems') && this._state.writable('fadersMems'),
  );
  protected readonly can1To24 = computed(
    () => this._state.supports('pressFaders1To24') && this._state.writable('faders1To24'),
  );
  protected readonly can25To48 = computed(
    () => this._state.supports('pressFaders25To48') && this._state.writable('faders25To48'),
  );
  protected readonly note = computed(() => {
    const mode = this._state.mode();
    if (mode === undefined) {
      return undefined;
    }
    if (mode === 'simulated') {
      return 'The simulator plays every bank at once';
    }
    return this.reported() ? undefined : 'Not reported by the console yet';
  });
  private readonly _ledTitle = 'Reading from the console (ConsoleStatus: memory mode and bank, F98)';
  protected readonly memsTitle = computed(() =>
    this.canMems()
      ? 'FADERS MEMS on the console: the physical faders to the memories of the shown page (a key tap)'
      : this.reported()
        ? this._ledTitle
        : 'FADERS MEMS: not available on this device',
  );
  protected readonly lowerTitle = computed(() =>
    this.can1To24()
      ? 'FADERS 1-24 on the console: the physical faders to channels 1-24 (a key tap)'
      : this.reported()
        ? this._ledTitle
        : 'FADERS 1-24: not available on this device',
  );
  protected readonly upperTitle = computed(() =>
    this.can25To48()
      ? 'FADERS 25-48 on the console: the physical faders to channels 25-48 (a key tap)'
      : this.reported()
        ? this._ledTitle
        : 'FADERS 25-48: not available on this device',
  );
  protected readonly nextTitle =
    'NEXT: levels set blind into the Next fader of the crossfader (manual, "Live or Next mode"); not sent from the PC (SC-5: NO-GO)';

  protected pressMems(): void {
    this._channels.pressFadersMems();
  }

  protected press1To24(): void {
    this._channels.pressFaders1To24();
  }

  protected press25To48(): void {
    this._channels.pressFaders25To48();
  }
}
