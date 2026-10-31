import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { ChannelPercentDto, LabelDto, StepDto } from '../../core/show-file.models';
import { ShowFileService } from '../../core/show-file.service';

/**
 * The show-file page (E10, step 1): open a `.asc` show file from disk and browse what it holds, read-only:
 * the stack and the sequences with their steps, the memories, the patch, the labels and the settings.
 * The backend parses the file (docs/api.md, `POST /api/showfile/parse`); nothing is written anywhere
 * and nothing reaches a console. Field meanings carry the statuses of docs/formats/asc-format.md.
 */
@Component({
  selector: 'sf-show-file-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './show-file-page.component.html',
  styleUrl: './show-file-page.component.scss',
})
export class ShowFilePageComponent {
  private readonly _files = inject(ShowFileService);

  protected readonly file = this._files.file;
  protected readonly busy = this._files.busy;
  protected readonly error = this._files.error;

  protected readonly patchedCount = computed(() => this.file()?.patch.filter((p) => p.channel !== 0).length ?? 0);
  protected readonly namedChannels = computed(() => this.file()?.channels.filter((c) => !isEmpty(c.label)) ?? []);
  protected readonly namedPages = computed(() => this.file()?.pages.filter((p) => !isEmpty(p.label)) ?? []);
  /** The 512 patch entries in rows of 32 DMX addresses, for the grid. */
  protected readonly patchRows = computed(() => {
    const patch = this.file()?.patch ?? [];
    const rows: (typeof patch)[] = [];
    for (let start = 0; start < patch.length; start += 32) {
      rows.push(patch.slice(start, start + 32));
    }
    return rows;
  });

  protected async pick(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const chosen = input.files?.[0];
    if (chosen) {
      await this._files.open(chosen);
    }
    input.value = '';
  }

  protected close(): void {
    this._files.close();
  }

  protected labelText(label: LabelDto | null): string {
    if (!label) {
      return '';
    }
    const lines = [label.line1, label.line2, label.line3].filter((line) => line.trim().length > 0);
    return lines.length > 0 ? lines.join(' / ') : label.text;
  }

  protected memoryRef(step: StepDto): string {
    return step.memoryNumber === null ? '' : `${step.memoryNumber} (page ${step.memoryPage! + 1}, fader ${step.memoryFader! + 1})`;
  }

  protected channelsText(channels: ChannelPercentDto[]): string {
    return channels.map((c) => `${c.channel}/${c.percent}`).join(' ');
  }
}

function isEmpty(label: LabelDto): boolean {
  return label.text.trim().length === 0 && label.line1.trim().length === 0 && label.line2.trim().length === 0 && label.line3.trim().length === 0;
}
