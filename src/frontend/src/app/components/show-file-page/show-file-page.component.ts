import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { ChannelPercentDto, LabelDto, SequenceDto, StepDto } from '../../core/show-file.models';
import { ShowFileService, TIME_PATTERN, type StepHome } from '../../core/show-file.service';

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
  /** The working copy: what the tables and the patch grid show, and what a download renders. */
  protected readonly edited = this._files.edited;
  protected readonly dirty = this._files.dirty;
  protected readonly busy = this._files.busy;
  protected readonly error = this._files.error;

  protected readonly patchedCount = computed(() => this.edited()?.patch.filter((p) => p.channel !== 0).length ?? 0);
  protected readonly namedChannels = computed(() => this.file()?.channels.filter((c) => !isEmpty(c.label)) ?? []);
  protected readonly namedPages = computed(() => this.file()?.pages.filter((p) => !isEmpty(p.label)) ?? []);
  /** The 512 patch entries in rows of 32 DMX addresses, for the grid. */
  protected readonly patchRows = computed(() => {
    const patch = this.edited()?.patch ?? [];
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

  /** A patch cell's input: DMX address `dmx` now drives the typed channel (0 clears it). */
  protected setChannel(dmx: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const channel = input.value.trim() === '' ? 0 : Number(input.value);
    if (!Number.isInteger(channel) || channel < 0 || channel > 96) {
      input.value = String(this.edited()?.patch.find((entry) => entry.dmx === dmx)?.channel ?? 0);
      return;
    }
    this._files.setPatchChannel(dmx, channel);
  }

  /** A time cell of a step: accepted when it looks like the console's times, else the input is restored. */
  protected setTime(home: StepHome, index: number, field: 'upTime' | 'upDelay' | 'downTime' | 'downDelay' | 'followOn', event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value.trim();
    if (!TIME_PATTERN.test(value) || !this._files.setStep(home, index, { [field]: value })) {
      input.value = this.stepOf(home, index)?.[field] ?? '';
    }
  }

  /** The memory cell of a step: 1..288, or empty for none; else the input is restored. */
  protected setMemory(home: StepHome, index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const text = input.value.trim();
    const number = text === '' ? null : Number(text);
    if (!this._files.setStep(home, index, { memoryNumber: number })) {
      input.value = String(this.stepOf(home, index)?.memoryNumber ?? '');
    }
  }

  /** The position of a sequence in the file's list: the service addresses steps by it. */
  protected seqIndex(sequence: SequenceDto): number {
    return this.edited()?.sequences.indexOf(sequence) ?? -1;
  }

  private stepOf(home: StepHome, index: number): StepDto | undefined {
    const show = this.edited();
    return (home === 'stack' ? show?.mainStack : show?.sequences[home])?.steps[index];
  }

  protected revert(): void {
    this._files.revert();
  }

  protected download(): void {
    void this._files.download();
  }

  protected labelText(label: LabelDto | null): string {
    if (!label) {
      return '';
    }
    const lines = [label.line1, label.line2, label.line3].filter((line) => line.trim().length > 0);
    return lines.length > 0 ? lines.join(' / ') : label.text;
  }

  protected channelsText(channels: ChannelPercentDto[]): string {
    return channels.map((c) => `${c.channel}/${c.percent}`).join(' ');
  }
}

function isEmpty(label: LabelDto): boolean {
  return label.text.trim().length === 0 && label.line1.trim().length === 0 && label.line2.trim().length === 0 && label.line3.trim().length === 0;
}
