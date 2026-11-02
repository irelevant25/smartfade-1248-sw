import { ChangeDetectionStrategy, Component, computed, ElementRef, HostListener, inject, signal } from '@angular/core';
import { ShowFileService } from '../../core/show-file.service';
import { ShowStore } from '../../core/show.store';

/**
 * The Show menu in the top bar: a button that opens a compact panel with the loaded show's name and its "edited"
 * marker, "Open file…" (a `.asc` file chosen in the browser, parsed by the backend, then handed to the
 * `ShowStore`), "Save as file…" (the edited show rendered by the backend and saved as a new file through the
 * browser, for the owner to copy to the SD card, which the PC cannot write; only when a show is loaded and edited) and "Revert edits". It is the only place in
 * the UI that deals with files; the PATCH and STACK / SEQ pages only read the store. Nothing reaches a console.
 */
@Component({
  selector: 'sf-show-menu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './show-menu.component.html',
  styleUrl: './show-menu.component.scss',
})
export class ShowMenuComponent {
  private readonly _store = inject(ShowStore);
  private readonly _files = inject(ShowFileService);
  private readonly _host = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly _open = signal(false);
  private readonly _busy = signal(false);
  private readonly _error = signal<string | undefined>(undefined);

  protected readonly open = this._open.asReadonly();
  protected readonly busy = this._busy.asReadonly();
  protected readonly error = this._error.asReadonly();
  protected readonly name = this._store.name;
  protected readonly loaded = this._store.loaded;
  protected readonly dirty = this._store.dirty;
  /** Saving makes sense only for a loaded show with edits (the opened file is on the PC or the card already). */
  protected readonly canSave = computed(() => this.loaded() && this.dirty() && !this.busy());

  protected toggle(): void {
    this._open.update((open) => !open);
  }

  protected close(): void {
    this._open.set(false);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: Event): void {
    if (this._open() && event.target instanceof Node && !this._host.nativeElement.contains(event.target)) {
      this._open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this._open.set(false);
  }

  /** The file picker's change: the chosen file goes to the backend, the DTO to the store (replacing any edits). */
  protected async pick(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const chosen = input.files?.[0];
    input.value = '';
    if (!chosen) {
      return;
    }
    this._busy.set(true);
    this._error.set(undefined);
    try {
      const dto = await this._files.parse(chosen);
      this._store.setShow(dto, chosen.name, 'file');
      this._open.set(false);
    } catch (error) {
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }

  /** Renders the edited show on the backend and saves it as a new file through the browser; the edits stay marked until a file is loaded again. */
  protected async save(): Promise<void> {
    const show = this._store.show();
    if (!show || !this.canSave()) {
      return;
    }
    this._busy.set(true);
    this._error.set(undefined);
    try {
      await this._files.download(show);
      this._open.set(false);
    } catch (error) {
      this._error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this._busy.set(false);
    }
  }

  protected revert(): void {
    this._store.revert();
    this._error.set(undefined);
  }
}
