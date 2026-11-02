import { Injectable } from '@angular/core';
import type { ShowFileDto } from './show-file.models';

/**
 * The HTTP layer for `.asc` show files (docs/api.md): the bytes of a file chosen in the browser go to
 * `POST /api/showfile/parse`, which returns the DTO the `ShowStore` then holds; an edited DTO goes to
 * `POST /api/showfile/render`, and the rendered bytes are saved as a NEW file through the browser's
 * download. Nothing is stored on the backend, the original file is never touched, and nothing reaches
 * a console.
 */
@Injectable({ providedIn: 'root' })
export class ShowFileService {
  /** Parses `file` on the backend. Throws an Error with the backend's message when it refuses the file. */
  async parse(file: File): Promise<ShowFileDto> {
    const response = await fetch(`/api/showfile/parse?name=${encodeURIComponent(file.name)}`, {
      method: 'POST',
      body: file,
    });
    if (!response.ok) {
      throw new Error(await describe(response));
    }
    return (await response.json()) as ShowFileDto;
  }

  /** Renders `show` on the backend and hands the bytes to the browser as a new file (`<name>-edited.asc`). */
  async download(show: ShowFileDto): Promise<void> {
    const response = await fetch('/api/showfile/render', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(show),
    });
    if (!response.ok) {
      throw new Error(await describe(response));
    }
    const name = fileNameOf(response.headers.get('Content-Disposition')) ?? editedName(show.name);
    saveBlob(await response.blob(), name);
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
