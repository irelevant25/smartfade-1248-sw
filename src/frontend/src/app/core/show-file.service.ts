import { Injectable, signal } from '@angular/core';
import type { ShowFileDto } from './show-file.models';

/**
 * Opens a `.asc` show file chosen in the browser: the bytes go to the backend, which parses them with
 * the show-file library and returns the DTO (docs/api.md, `POST /api/showfile/parse`). Read-only:
 * nothing is stored anywhere and nothing reaches a console.
 */
@Injectable({ providedIn: 'root' })
export class ShowFileService {
  private readonly _file = signal<ShowFileDto | undefined>(undefined);
  private readonly _busy = signal(false);
  private readonly _error = signal<string | undefined>(undefined);

  /** The last file parsed, or undefined. */
  readonly file = this._file.asReadonly();
  readonly busy = this._busy.asReadonly();
  readonly error = this._error.asReadonly();

  async open(file: File): Promise<void> {
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const response = await fetch(`/api/showfile/parse?name=${encodeURIComponent(file.name)}`, {
        method: 'POST',
        body: file,
      });
      if (!response.ok) {
        const problem = (await response.json().catch(() => undefined)) as { message?: string } | undefined;
        throw new Error(problem?.message ?? `The backend answered ${response.status}.`);
      }
      this._file.set((await response.json()) as ShowFileDto);
    } catch (error) {
      this._file.set(undefined);
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }

  close(): void {
    this._file.set(undefined);
    this._error.set(undefined);
  }
}
