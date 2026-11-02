import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';

/**
 * The console's INDEPENDENTS section (manual F108): the keys IND 1 and IND 2, two outputs separated
 * from all other controls (DMX 97 / 98 by default), in their own section under the masters (the
 * owner's layout, 2026-11-02). A real console reports them from its own object (F115), so the keys
 * say "not reported" until that arrived and name the LED identification (E8b) when read-only.
 */
@Component({
  selector: 'sf-independents-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './independents-panel.component.html',
  styleUrl: './independents-panel.component.scss',
})
export class IndependentsPanelComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _channels = inject(ChannelService);

  protected readonly masters = this._state.masters;
  protected readonly enabled = this._channels.enabled;
  protected readonly supports = (command: string) => this._state.supports(command);
  /** A device cleared for one independent only (docs/api.md `writableControls`) enables just that one. */
  protected readonly writable = (control: string) => this._state.writable(control);
  protected readonly observedOnly = computed(() => this._state.mode() === 'hardware');
  protected readonly unknown = computed(() => this.observedOnly() && !this._state.independentsReported());
  /** On a real console the keys mirror the reported IND state (F115): read-only, but lit like the record keys. */
  protected readonly ledMode = computed(() => this.observedOnly() && this._state.independentsReported());

  protected toggle(number: 1 | 2): void {
    const on = number === 1 ? this.masters().independent1 : this.masters().independent2;
    this._channels.setIndependent(number, !on);
  }
}
