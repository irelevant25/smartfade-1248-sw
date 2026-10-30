import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ChannelService } from '../../core/channel.service';
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

  protected readonly enabled = this._channels.enabled;
  protected readonly copyArmed = computed(() => {
    const mode = this._edit.mode();
    return mode === 'copy-source' || mode === 'copy-target';
  });
  protected readonly recordArmed = computed(() => this._edit.mode() === 'record');
  protected readonly editArmed = computed(() => {
    const mode = this._edit.mode();
    return mode === 'select' || mode === 'edit';
  });

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
