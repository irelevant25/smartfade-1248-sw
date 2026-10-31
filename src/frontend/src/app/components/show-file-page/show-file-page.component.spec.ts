import { TestBed } from '@angular/core/testing';
import { ShowFilePageComponent } from './show-file-page.component';
import type { ShowFileDto } from '../../core/show-file.models';
import { nextCueNumber, ShowFileService } from '../../core/show-file.service';

const label = (line1 = '', text = line1) => ({ text, line1, line2: '', line3: '' });

/** A small show in the shape of docs/api.md's ShowFileDto. */
function sampleShow(): ShowFileDto {
  const patch = Array.from({ length: 512 }, (_, i) => ({ dmx: i + 1, channel: i < 48 ? i + 1 : 0, level: 100 }));
  return {
    name: 'SHOW07.ASC',
    bytes: 15944,
    lines: 1153,
    lineEnding: 'lf',
    manufacturer: 'AVAB',
    console: 'SMARTFADE',
    softwareVersion: '3.0.2 43',
    ident: '3.0',
    clearAll: true,
    mainStack: {
      index: 48, b: 2, c: 0, d: 256, e: 256, isMainStack: true, label: null,
      steps: [
        { number: '1.0', memoryPage: 0, memoryFader: 0, memoryNumber: 1, cueParams: 1, upTime: '5.0', upDelay: '0', downTime: '5.0', downDelay: '0', followOn: '0', channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }], label: null },
        { number: '2.0', memoryPage: 0, memoryFader: 1, memoryNumber: 2, cueParams: 1, upTime: '5.0', upDelay: '2', downTime: '5.0', downDelay: '0', followOn: '1:30', channels: [{ channel: 1, percent: 17 }], label: null },
      ],
    },
    sequences: [
      { index: 0, b: 0, c: 0, d: 638, e: 256, isMainStack: false, label: label('KOTLIK'), steps: [
        { number: '265.0', memoryPage: 11, memoryFader: 0, memoryNumber: 265, cueParams: 1, upTime: '1.0', upDelay: '0', downTime: '1.0', downDelay: '0', followOn: '0', channels: [], label: null },
      ] },
    ],
    memories: [
      { number: 1, page: 1, fader: 1, channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }], label: label('OPONA') },
      { number: 265, page: 12, fader: 1, channels: [{ channel: 37, percent: 100 }], label: label('') },
    ],
    patch,
    independents: [{ number: 1, a: 0, b: 255, c: 0, d: 1, label: label('') }],
    channels: [{ number: 7, label: label('Caj') }],
    pages: [],
    settings: [{ key: 'MIDIActive', value: '1' }],
    unknownLines: 0,
    warnings: [],
  };
}

describe('ShowFilePageComponent', () => {
  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function openFile(show: ShowFileDto | { status: number; message: string }) {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () =>
      'status' in show && !('name' in show)
        ? new Response(JSON.stringify({ message: show.message, code: 'invalid_argument' }), { status: show.status })
        : new Response(JSON.stringify(show), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    );
    const fixture = TestBed.createComponent(ShowFilePageComponent);
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File([new Uint8Array([0x4d])], 'SHOW07.ASC');
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change'));
    await settle();
    await settle();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('starts empty and explains itself', () => {
    const fixture = TestBed.createComponent(ShowFilePageComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('.show__empty')?.textContent).toContain('Choose one of your show files');
    expect(root.querySelector('.show__hint')?.textContent).toContain('nothing reaches the console');
    expect(root.querySelector('.show__table')).toBeNull();
  });

  it('shows the stack, the sequences, the memories, the patch, the labels and the settings of an opened file', async () => {
    const root = await openFile(sampleShow());

    expect(fetch).toHaveBeenCalledWith('/api/showfile/parse?name=SHOW07.ASC', expect.objectContaining({ method: 'POST' }));
    expect(root.querySelector('.show__facts')?.textContent).toContain('SHOW07.ASC');
    expect(root.querySelector('.show__facts')?.textContent).toContain('48 of 512');
    const tables = Array.from(root.querySelectorAll('.show__table'));
    expect(tables).toHaveLength(3); // stack, one sequence, memories
    expect(tables[0].querySelectorAll('tbody tr')).toHaveLength(2);
    const cell = (label: string) => tables[0].querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)?.value;
    expect(cell('Memory of cue 1.0')).toBe('1');
    expect(tables[0].textContent).toContain('p1 f1');
    expect(cell('Follow-on of cue 2.0')).toBe('1:30');
    expect(root.textContent).toContain('Sequence 0 "KOTLIK"');
    expect(tables[2].textContent).toContain('OPONA');
    expect(tables[2].textContent).toContain('7/100 8/100');
    expect(root.querySelectorAll('.show__patch-cell')).toHaveLength(512);
    expect(root.querySelectorAll('.show__patch-cell--empty')).toHaveLength(512 - 48);
    expect(root.textContent).toContain('7 Caj');
    expect(root.textContent).toContain('MIDIActive');
  });

  it('reports a rejected file and keeps nothing', async () => {
    const root = await openFile({ status: 400, message: 'The request body must be the .asc file.' });

    expect(root.querySelector('.show__error')?.textContent).toContain('request body');
    expect(root.querySelector('.show__table')).toBeNull();
  });

  it('edits the patch in the working copy and downloads the rendered result as a new file', async () => {
    const root = await openFile(sampleShow());
    const calls: { url: string; body: string }[] = [];
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      calls.push({ url: String(input), body: String(init?.body) });
      return new Response('MANUFACTURER AVAB', {   // a string body: jsdom's Blob lacks stream(), which the fetch Response needs
        status: 200,
        headers: { 'Content-Disposition': 'attachment; filename="SHOW07-edited.ASC"' },
      });
    });
    const createObjectUrl = vi.fn(() => 'blob:edited');
    const revokeObjectUrl = vi.fn();
    Object.assign(URL, { createObjectURL: createObjectUrl, revokeObjectURL: revokeObjectUrl });
    const clicks: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicks.push(this.download);
    });
    const fixture = TestBed.createComponent(ShowFilePageComponent);
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;

    const cell = page.querySelector<HTMLInputElement>('input[aria-label="Channel for DMX 1"]')!;
    cell.value = '12';
    cell.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(page.querySelector('.show__dirty')?.textContent).toContain('edited');
    expect(page.querySelector<HTMLButtonElement>('.show__revert')?.disabled).toBe(false);

    page.querySelector<HTMLButtonElement>('.show__download')!.click();
    for (let turn = 0; turn < 6; turn++) {
      await settle();
    }
    fixture.detectChanges();

    expect(page.querySelector('.show__error')?.textContent ?? '').toBe('');
    expect(calls[0].url).toBe('/api/showfile/render');
    const sent = JSON.parse(calls[0].body) as { patch: { dmx: number; channel: number }[] };
    expect(sent.patch[0]).toEqual({ dmx: 1, channel: 12, level: 100 });
    expect(clicks).toEqual(['SHOW07-edited.ASC']);
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:edited');

    page.querySelector<HTMLButtonElement>('.show__revert')!.click();
    fixture.detectChanges();
    expect(page.querySelector('.show__dirty')).toBeNull();
    expect(page.querySelector<HTMLInputElement>('input[aria-label="Channel for DMX 1"]')?.value).toBe('1');
    expect(root).toBeDefined();
  });

  it("edits a step's times and memory, keeps the number, and restores an input the console would not write", async () => {
    await openFile(sampleShow());
    const fixture = TestBed.createComponent(ShowFilePageComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const field = (label: string) => root.querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
    const typeInto = (input: HTMLInputElement, value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };

    typeInto(field('Follow-on of cue 1.0'), '1:30');
    typeInto(field('Memory of cue 1.0'), '267');
    typeInto(field('Up time of sequence 0 step 265.0'), '2.5');
    expect(root.querySelector('.show__dirty')).not.toBeNull();
    expect(field('Memory of cue 1.0').nextElementSibling?.textContent).toContain('p12 f3');

    typeInto(field('Up time of cue 1.0'), '3,5'); // not a console time: restored
    expect(field('Up time of cue 1.0').value).toBe('5.0');
    typeInto(field('Memory of cue 1.0'), '300'); // outside 1..288: restored
    expect(field('Memory of cue 1.0').value).toBe('267');

    const service = TestBed.inject(ShowFileService);
    const stack = service.edited()!.mainStack!;
    expect(stack.steps[0]).toMatchObject({ number: '1.0', followOn: '1:30', memoryPage: 11, memoryFader: 2, memoryNumber: 267, upTime: '5.0' });
    expect(service.edited()!.sequences[0].steps[0].upTime).toBe('2.5');
  });

  it("inserts and removes steps, numbers them as the console's files do, and keeps a step's channels equal to its memory's", async () => {
    await openFile(sampleShow());
    const fixture = TestBed.createComponent(ShowFilePageComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const click = (label: string) => {
      root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!.click();
      fixture.detectChanges();
    };
    const field = (label: string) => root.querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
    const service = TestBed.inject(ShowFileService);

    // a cue after 1.0 takes 1.1 (2.0 follows), copies memory 1 with its channels; after the last cue the next whole number
    click('Insert a step after cue 1.0');
    click('Insert a step after cue 2.0');
    let stack = service.edited()!.mainStack!.steps;
    expect(stack.map((s) => s.number)).toEqual(['1.0', '1.1', '2.0', '3.0']);
    expect(stack[1]).toMatchObject({ memoryNumber: 1, cueParams: 1, upTime: '5.0', channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }] });

    // changing the memory replaces the channels with the new memory's (265 has channel 37) and a sequence step's number
    field('Memory of cue 1.1').value = '265';
    field('Memory of cue 1.1').dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(service.edited()!.mainStack!.steps[1]).toMatchObject({ number: '1.1', memoryNumber: 265, channels: [{ channel: 37, percent: 100 }] });
    click('Insert a step after sequence 0 step 265.0');
    field('Memory of sequence 0 step 265.0').value = '1';
    field('Memory of sequence 0 step 265.0').dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const sequence = service.edited()!.sequences[0].steps;
    expect(sequence.map((s) => s.number)).toEqual(['1.0', '265.0']);
    expect(sequence[0].channels).toEqual([{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }]);

    // the memory cannot be removed from a step; no number fits between 1.0 and 1.1
    field('Memory of cue 1.0').value = '';
    field('Memory of cue 1.0').dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(field('Memory of cue 1.0').value).toBe('1');
    click('Insert a step after cue 1.0');
    expect(root.querySelector('.show__notice')?.textContent).toContain('no free cue number');
    expect(service.edited()!.mainStack!.steps).toHaveLength(4);

    // removing keeps the other numbers
    click('Remove cue 1.1');
    stack = service.edited()!.mainStack!.steps;
    expect(stack.map((s) => s.number)).toEqual(['1.0', '2.0', '3.0']);
    expect(root.querySelector('.show__dirty')).not.toBeNull();
  });

  it('numbers a new cue from its neighbours as the files do', () => {
    expect(nextCueNumber(undefined, undefined)).toBe('1.0');
    expect(nextCueNumber('14.0', undefined)).toBe('15.0');
    expect(nextCueNumber('10.0', '11.0')).toBe('10.1');
    expect(nextCueNumber('10.1', '10.2')).toBeUndefined();
    expect(nextCueNumber('199.9', undefined)).toBeUndefined();
    expect(nextCueNumber(undefined, '2.0')).toBe('1.0');
  });
});
