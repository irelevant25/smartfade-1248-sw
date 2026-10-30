import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
import { ConsoleStateService } from '../../core/console-state.service';
import { MemoryEditService } from '../../core/memory-edit.service';

/**
 * The console's "Record & Edit functions" column (manual v3.0 printed p.13): COPY, REC SEQ,
 * MAGIC, REC MEM, EDIT MEM, SNAPSHOT. COPY, REC MEM and EDIT MEM work against the simulator's
 * memories; REC SEQ, MAGIC and SNAPSHOT need sequences, the stack and snapshots, which the
 * simulator does not model yet, so they are shown but inactive.
 */
@Component({
  selector: 'sf-record-keys',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './record-keys.component.html',
  styleUrl: './record-keys.component.scss',
})
export class RecordKeysComponent {
  private readonly _edit = inject(MemoryEditService);
  private readonly _channels = inject(ChannelService);
  private readonly _state = inject(ConsoleStateService);

  protected readonly enabled = this._channels.enabled;
  /** COPY and EDIT MEM store a memory, REC MEM records one: inactive on a device that accepts neither. */
  protected readonly canStore = computed(() => this._state.supports('storeMemory'));
  protected readonly canRecord = computed(() => this._state.supports('recordMemory'));
  protected readonly copyArmed = computed(() => {
    const mode = this._edit.mode();
    return mode === 'copy-source' || mode === 'copy-target';
  });
  protected readonly recordArmed = computed(() => this._edit.mode() === 'record');
  protected readonly editArmed = computed(() => {
    const mode = this._edit.mode();
    return mode === 'select' || mode === 'edit';
  });
  /**
   * A real console reports its key LEDs (F116; which LED is which key is inferred, H72): the keys
   * then mirror them, lit, dimmed or blinking, instead of the simulator's armed functions.
   */
  protected readonly keys = this._state.keys;
  protected readonly copyLed = computed(() => this._state.keys()?.copy);
  protected readonly recSeqLed = computed(() => this._state.keys()?.recSeq);
  protected readonly magicLed = computed(() => this._state.keys()?.magic);
  protected readonly recMemLed = computed(() => this._state.keys()?.recMem);
  protected readonly editMemLed = computed(() => this._state.keys()?.editMem);
  protected readonly ledTitle = 'LED reading from the console (which key is which is inferred)';

  protected toggleCopy(): void {
    this._edit.toggleCopy();
  }

  protected toggleRecord(): void {
    this._edit.toggleRecord();
  }

  protected toggleEdit(): void {
    this._edit.toggleEdit();
  }
}
