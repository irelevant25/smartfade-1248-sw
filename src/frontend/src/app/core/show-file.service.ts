import { computed, Injectable, signal } from '@angular/core';
import type { ShowFileDto } from './show-file.models';

/**
 * Opens a `.asc` show file chosen in the browser and holds an editable copy of it. The bytes go to the
 * backend, which parses them with the show-file library and returns the DTO (docs/api.md,
 * `POST /api/showfile/parse`); edits change only the copy here; "download" sends the copy back to
 * `POST /api/showfile/render` and saves the rendered bytes as a NEW file through the browser's
 * download. Nothing is stored on the backend, the original file is never touched, and nothing
 * reaches a console.
 */
@Injectable({ providedIn: 'root' })
export class ShowFileService {
  private readonly _file = signal<ShowFileDto | undefined>(undefined);
  private readonly _edited = signal<ShowFileDto | undefined>(undefined);
  private readonly _busy = signal(false);
  private readonly _error = signal<string | undefined>(undefined);

  /** The file as parsed, or undefined. */
  readonly file = this._file.asReadonly();
  /** The working copy the page edits; equals the parsed file until an edit is made. */
  readonly edited = this._edited.asReadonly();
  readonly busy = this._busy.asReadonly();
  readonly error = this._error.asReadonly();
  readonly dirty = computed(() => {
    const original = this._file();
    const edited = this._edited();
    return original !== undefined && edited !== undefined && JSON.stringify(original) !== JSON.stringify(edited);
  });

  async open(file: File): Promise<void> {
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const response = await fetch(`/api/showfile/parse?name=${encodeURIComponent(file.name)}`, {
        method: 'POST',
        body: file,
      });
      if (!response.ok) {
        throw new Error(await describe(response));
      }
      const dto = (await response.json()) as ShowFileDto;
      this._file.set(dto);
      this._edited.set(structuredClone(dto));
    } catch (error) {
      this._file.set(undefined);
      this._edited.set(undefined);
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }

  close(): void {
    this._file.set(undefined);
    this._edited.set(undefined);
    this._error.set(undefined);
  }

  /** Patch edit: DMX address `dmx` drives `channel` (0 = unpatched, 1..96). Out-of-range values are ignored. */
  setPatchChannel(dmx: number, channel: number): void {
    const edited = this._edited();
    if (!edited || !Number.isInteger(channel) || channel < 0 || channel > 96) {
      return;
    }
    const index = edited.patch.findIndex((entry) => entry.dmx === dmx);
    if (index < 0 || edited.patch[index].channel === channel) {
      return;
    }
    const patch = edited.patch.slice();
    patch[index] = { ...patch[index], channel };
    this._edited.set({ ...edited, patch });
  }

  revert(): void {
    const original = this._file();
    this._edited.set(original ? structuredClone(original) : undefined);
  }

  /** Renders the working copy on the backend and hands the bytes to the browser as a new file. */
  async download(): Promise<void> {
    const edited = this._edited();
    if (!edited) {
      return;
    }
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const response = await fetch('/api/showfile/render', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(edited),
      });
      if (!response.ok) {
        throw new Error(await describe(response));
      }
      const name = fileNameOf(response.headers.get('Content-Disposition')) ?? editedName(edited.name);
      saveBlob(await response.blob(), name);
    } catch (error) {
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }
}

async function describe(response: Response): Promise<string> {
  const problem = (await response.json().catch(() => undefined)) as { message?: string } | undefined;
  return problem?.message ?? `The backend answered ${response.status}.`;
}

function fileNameOf(contentDisposition: string | null): string | undefined {
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  return match ? decodeURIComponent(match[1]) : undefined;
}

export function editedName(original: string): string {
  const dot = original.lastIndexOf('.');
  return dot > 0 ? `${original.slice(0, dot)}-edited${original.slice(dot)}` : `${original}-edited.asc`;
}

function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
