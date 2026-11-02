import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { ShowFileDto } from '../../core/show-file.models';
import { ShowStore } from '../../core/show.store';
import { sampleShow } from '../../core/testing/sample-show';
import { ShowMenuComponent } from './show-menu.component';

describe('ShowMenuComponent', () => {
  let fixture: ComponentFixture<ShowMenuComponent>;
  let store: ShowStore;

  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));
  const element = () => fixture.nativeElement as HTMLElement;
  const trigger = () => element().querySelector<HTMLButtonElement>('.menu__trigger')!;
  const panel = () => element().querySelector<HTMLElement>('.menu__panel');
  const save = () => element().querySelector<HTMLButtonElement>('.menu__save')!;
  const openMenu = () => {
    if (!panel()) {
      trigger().click();
      fixture.detectChanges();
    }
  };
  const mockParse = (show: ShowFileDto | { status: number; message: string }) =>
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () =>
      'status' in show && !('name' in show)
        ? new Response(JSON.stringify({ message: show.message, code: 'invalid_argument' }), { status: show.status })
        : new Response(JSON.stringify(show), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    );
  const pickFile = async () => {
    openMenu();
    const input = element().querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File([new Uint8Array([0x4d])], 'SHOW07.ASC')], configurable: true });
    input.dispatchEvent(new Event('change'));
    await settle();
    await settle();
    fixture.detectChanges();
  };

  beforeEach(() => {
    store = TestBed.inject(ShowStore);
    fixture = TestBed.createComponent(ShowMenuComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts closed and opens a panel that says no show is loaded, with Save and Revert disabled', () => {
    expect(trigger().textContent).toContain('Show');
    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    expect(panel()).toBeNull();

    openMenu();
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    expect(panel()?.textContent).toContain('No show loaded');
    expect(panel()?.querySelector('.menu__load')?.textContent).toContain('Open file…');
    expect(save().disabled).toBe(true);
    expect(element().querySelector<HTMLButtonElement>('.menu__revert')?.disabled).toBe(true);
  });

  it('opens a file through the backend into the store, names it, and closes', async () => {
    mockParse(sampleShow());
    await pickFile();

    expect(fetch).toHaveBeenCalledWith('/api/showfile/parse?name=SHOW07.ASC', expect.objectContaining({ method: 'POST' }));
    expect(store.loaded()).toBe(true);
    expect(store.name()).toBe('SHOW07.ASC');
    expect(store.source()).toBe('file');
    expect(panel()).toBeNull();
    expect(trigger().querySelector('.menu__name')?.textContent).toBe('SHOW07.ASC');
    expect(trigger().querySelector('.menu__dot')).toBeNull();

    openMenu();
    expect(panel()?.textContent).toContain('SHOW07.ASC');
    expect(panel()?.textContent).toContain('unchanged');
    expect(panel()?.querySelector('.menu__load')?.textContent).toContain('Open another file…');
  });

  it('reports a file the backend refuses, keeps the panel open and loads nothing', async () => {
    mockParse({ status: 400, message: 'The request body must be the .asc file.' });
    await pickFile();

    expect(panel()?.querySelector('.menu__error')?.textContent).toContain('request body');
    expect(store.loaded()).toBe(false);
    expect(trigger().querySelector('.menu__name')).toBeNull();
  });

  it('enables Save as file only for a loaded and edited show, saves the rendered file as a new file, and keeps the edits marked', async () => {
    mockParse(sampleShow());
    await pickFile();
    openMenu();
    expect(save().disabled).toBe(true);

    store.setPatchChannel(1, 12);
    fixture.detectChanges();
    expect(save().disabled).toBe(false);
    expect(trigger().querySelector('.menu__dot')).not.toBeNull();
    expect(panel()?.querySelector('.menu__edited')?.textContent).toContain('edited');

    const calls: { url: string; body: string }[] = [];
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      calls.push({ url: String(input), body: String(init?.body) });
      return new Response('MANUFACTURER AVAB', {   // a string body: jsdom's Blob lacks stream(), which the fetch Response needs
        status: 200,
        headers: { 'Content-Disposition': 'attachment; filename="SHOW07-edited.ASC"' },
      });
    });
    Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:edited'), revokeObjectURL: vi.fn() });
    const clicks: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicks.push(this.download);
    });

    save().click();
    for (let turn = 0; turn < 6; turn++) {
      await settle();
    }
    fixture.detectChanges();

    expect(calls[0].url).toBe('/api/showfile/render');
    const sent = JSON.parse(calls[0].body) as ShowFileDto;
    expect(sent.patch[0]).toEqual({ dmx: 1, channel: 12, level: 100 });
    expect(clicks).toEqual(['SHOW07-edited.ASC']);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:edited');
    expect(panel()).toBeNull();
    expect(store.dirty()).toBe(true); // the browser's save cannot be confirmed, so the edits stay marked and can be saved again
  });

  it('reverts the edits', async () => {
    mockParse(sampleShow());
    await pickFile();
    store.setPatchChannel(1, 12);
    openMenu();

    element().querySelector<HTMLButtonElement>('.menu__revert')!.click();
    fixture.detectChanges();

    expect(store.dirty()).toBe(false);
    expect(store.show()!.patch[0].channel).toBe(1);
    expect(save().disabled).toBe(true);
  });

  it('closes on Escape and on a click outside, not on a click inside', () => {
    openMenu();
    panel()!.click();
    fixture.detectChanges();
    expect(panel()).not.toBeNull();

    document.body.click();
    fixture.detectChanges();
    expect(panel()).toBeNull();

    openMenu();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(panel()).toBeNull();
  });
});
