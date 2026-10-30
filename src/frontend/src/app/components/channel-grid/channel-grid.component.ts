import { ChangeDetectionStrategy, Component, computed, inject, viewChildren } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { MemoryEditService } from '../../core/memory-edit.service';
import { ChannelFaderComponent } from '../channel-fader/channel-fader.component';

/** What one channel fader shows: the live channel, or the draft of the memory being edited. */
interface FaderView {
  channel: number;
  desired: number;
  actual: number | undefined;
  pending: boolean;
  /** 0..255, what the console says the channel is doing; lights the bump button. */
  output: number;
}

interface Bank {
  first: number;
  last: number;
  faders: readonly FaderView[];
}

/**
 * The 48 channel faders in banks of 24 (the console's two banks), laid out as two rows, each
 * with its bump button. While a memory is being edited (EDIT MEM) the faders show and change
 * that memory's draft instead of the live channels. Left / Right on a fader moves focus to its
 * neighbour in channel order.
 */
@Component({
  selector: 'sf-channel-grid',
  imports: [ChannelFaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './channel-grid.component.html',
  styleUrl: './channel-grid.component.scss',
})
export class ChannelGridComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _channels = inject(ChannelService);
  private readonly _edit = inject(MemoryEditService);
  /** In template order, which is channel order. */
  private readonly _faders = viewChildren(ChannelFaderComponent);

  protected readonly enabled = this._channels.enabled;
  protected readonly editing = this._edit.editing;
  /** The device does not take channel levels (a read-only console): the faders only show. */
  protected readonly readOnly = computed(() => !this._state.supports('setChannelLevel'));
  protected readonly bumpsDisabled = computed(() => !this._state.supports('bumpChannel'));
  /** Until the console reported the channel levels, the faders show placeholders (zero), not console state. */
  protected readonly reported = this._state.channelsReported;

  protected readonly banks = computed<Bank[]>(() => {
    const channels = this._state.channels();
    const draft = this.editing();
    const readOnly = this.readOnly();
    const bankSize = this._state.capabilities().physicalFaders;
    const banks: Bank[] = [];
    for (let start = 0; start < channels.length; start += bankSize) {
      const faders = channels.slice(start, start + bankSize).map<FaderView>((channel) =>
        draft
          ? {
              channel: channel.channel,
              desired: draft.levels[channel.channel - 1] ?? 0,
              actual: undefined,
              pending: false,
              output: channel.output,
            }
          : {
              channel: channel.channel,
              // a read-only console reports levels only: the thumb follows the report
              desired: readOnly ? channel.actual : channel.desired,
              actual: channel.actual,
              pending: channel.pending,
              output: channel.output,
            },
      );
      banks.push({ first: start + 1, last: start + faders.length, faders });
    }
    return banks;
  });

  protected readonly editTitle = computed(() => {
    const draft = this.editing();
    if (!draft) {
      return '';
    }
    const name = draft.label ? ` "${draft.label}"` : '';
    return `Editing memory ${draft.number}${name} (page ${draft.page}, fader ${draft.fader})`;
  });

  protected setLevel(channel: number, level: number): void {
    if (this.editing()) {
      this._edit.setDraftLevel(channel, level);
    } else {
      this._channels.setChannelLevel(channel, level);
    }
  }

  protected bump(channel: number, pressed: boolean): void {
    this._channels.bumpChannel(channel, pressed);
  }

  protected store(): void {
    void this._edit.store();
  }

  protected cancel(): void {
    this._edit.cancel();
  }

  protected moveFocus(channel: number, direction: -1 | 1): void {
    this._faders()[channel - 1 + direction]?.focus();
  }
}
