import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShowStore } from '../../core/show.store';
import { sampleShow } from '../../core/testing/sample-show';
import { StackPageComponent } from './stack-page.component';

describe('StackPageComponent', () => {
  let fixture: ComponentFixture<StackPageComponent>;
  let store: ShowStore;

  const element = () => fixture.nativeElement as HTMLElement;
  const tabs = () => Array.from(element().querySelectorAll<HTMLButtonElement>('.stack__tab'));
  const rows = () => Array.from(element().querySelectorAll<HTMLElement>('.steps__row:not(.steps__row--head)'));
  const detail = () => element().querySelector<HTMLElement>('.stack__detail')!;
  const detailText = () => detail().textContent?.replace(/\s+/g, ' ') ?? '';
  const field = (label: string) => element().querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
  const button = (selector: string) => element().querySelector<HTMLButtonElement>(selector)!;
  const click = (target: HTMLElement) => {
    target.click();
    fixture.detectChanges();
  };
  const change = (input: HTMLInputElement, value: string) => {
    input.value = value;
    input.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
  };
  const stackNumbers = () => store.show()!.mainStack!.steps.map((s) => s.number);

  beforeEach(() => {
    store = TestBed.inject(ShowStore);
    fixture = TestBed.createComponent(StackPageComponent);
  });

  it('shows one message when no show is loaded', () => {
    fixture.detectChanges();

    expect(element().querySelector('.stack__empty')?.textContent?.trim()).toBe(
      "No show loaded. Open a file (Show menu); the console's own show will appear here at Connect once that is cleared.",
    );
    expect(element().querySelector('.stack__tabs')).toBeNull();
  });

  describe('with a show', () => {
    beforeEach(() => {
      store.setShow(sampleShow(), 'SHOW07.ASC', 'file');
      fixture.detectChanges();
    });

    it('offers a tab for the stack and one per sequence (its label or its number) and lists the chosen one', () => {
      expect(tabs().map((tab) => tab.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['Stack 4', 'KOTLIK 1', 'Sequence 1 0']);
      expect(tabs()[0].getAttribute('aria-selected')).toBe('true');
      expect(rows()).toHaveLength(4);
      expect(element().querySelector('.stack__seq-label')).toBeNull();
      expect(detailText()).toContain('Select a step');

      click(tabs()[1]);
      expect(tabs()[1].getAttribute('aria-selected')).toBe('true');
      expect(rows()).toHaveLength(1);
      expect(rows()[0].querySelector('.steps__num')?.textContent).toBe('265.0');
      expect(field('Label line 1 of sequence 0').value).toBe('KOTLIK');

      change(field('Label line 1 of sequence 0'), 'KOTOL');
      expect(store.show()!.sequences[0].label).toEqual({ text: 'KOTOL', line1: 'KOTOL', line2: '', line3: '' });
      expect(tabs()[1].textContent).toContain('KOTOL');
      change(field('Label line 1 of sequence 0'), 'SEDEM7A');
      expect(field('Label line 1 of sequence 0').value).toBe('KOTOL');
    });

    it('selects a row and shows its detail beside the list: the memory with its label and channels, a channel step, a State step', () => {
      click(rows()[0]);
      expect(rows()[0].getAttribute('aria-selected')).toBe('true');
      expect(detailText()).toContain('Step 1.0 memory step');
      expect(field('Memory of the selected step').value).toBe('1');
      expect(detailText()).toContain('page 1, fader 1');
      expect(field('Label line 1 of memory 1').value).toBe('OPONA');
      expect(field('Channels of memory 1').value).toBe('7/100 8/100');
      expect(field('Up of the selected step').value).toBe('5.0');
      expect(button('.stack__insert')).not.toBeNull();
      expect(button('.stack__remove')).not.toBeNull();

      click(rows()[1]);
      expect(detailText()).toContain('Step 2.0 memory step');
      expect(detailText()).toContain('Memory 2 is not recorded in this show');
      expect(element().querySelector('input[aria-label="Channels of memory 2"]')).toBeNull();

      const mono = () => detail().querySelector('.stack__mono')?.textContent;
      click(rows()[2]);
      expect(detailText()).toContain('Step 13.0 channel step');
      expect(detail().querySelector('.stack__field-name')?.textContent).toBe('Channels');
      expect(mono()).toBe('47/100');
      expect(element().querySelector('input[aria-label="Memory of the selected step"]')).toBeNull();

      click(rows()[3]);
      expect(detailText()).toContain('Step 13.1 state step');
      expect(detail().querySelector('.stack__field-name')?.textContent).toBe('Label');
      expect(mono()).toBe('STATE');
      expect(rows().map((row) => row.getAttribute('aria-selected'))).toEqual(['false', 'false', 'false', 'true']);
    });

    it("edits the step's memory and times and the memory's label and channels in the panel, within the files' rules", () => {
      click(rows()[0]);

      change(field('Memory of the selected step'), '267');
      expect(store.show()!.mainStack!.steps[0]).toMatchObject({ number: '1.0', memoryNumber: 267, memoryPage: 11, memoryFader: 2, channels: [] });
      expect(detailText()).toContain('page 12, fader 3');
      expect(detailText()).toContain('Memory 267 is not recorded');
      change(field('Memory of the selected step'), '300');
      expect(field('Memory of the selected step').value).toBe('267');
      change(field('Memory of the selected step'), '1');

      change(field('Follow of the selected step'), '1:30');
      expect(store.show()!.mainStack!.steps[0].followOn).toBe('1:30');
      change(field('Up of the selected step'), '3,5');
      expect(field('Up of the selected step').value).toBe('5.0');
      expect(store.show()!.mainStack!.steps[0].upTime).toBe('5.0');

      change(field('Label line 1 of memory 1'), 'OPONA2');
      change(field('Label line 2 of memory 1'), 'X');
      expect(store.show()!.memories[0].label).toEqual({ text: 'OPONA2X', line1: 'OPONA2', line2: 'X', line3: '' });
      change(field('Label line 1 of memory 1'), 'SEDEM7A');
      expect(field('Label line 1 of memory 1').value).toBe('OPONA2');
      change(field('Label line 3 of memory 1'), 'A&B');
      expect(field('Label line 3 of memory 1').value).toBe('');

      change(field('Channels of memory 1'), '9/100 7/50');
      expect(store.show()!.memories[0].channels).toEqual([{ channel: 7, percent: 50 }, { channel: 9, percent: 100 }]);
      expect(store.show()!.mainStack!.steps[0].channels).toEqual([{ channel: 7, percent: 50 }, { channel: 9, percent: 100 }]);
      expect(field('Channels of memory 1').value).toBe('7/50 9/100');
      change(field('Channels of memory 1'), '49/100');
      expect(field('Channels of memory 1').value).toBe('7/50 9/100');
      change(field('Channels of memory 1'), '7-50');
      expect(field('Channels of memory 1').value).toBe('7/50 9/100');
      change(field('Channels of memory 1'), '');
      expect(store.show()!.memories[0].channels).toEqual([]);
      expect(store.dirty()).toBe(true);
    });

    it("inserts after the selected step, numbers it as the console's files do, selects it, removes, and says when no number fits", () => {
      click(rows()[0]);
      click(button('.stack__insert'));
      expect(stackNumbers()).toEqual(['1.0', '1.1', '2.0', '13.0', '13.1']);
      expect(detailText()).toContain('Step 1.1 memory step');
      expect(rows()[1].getAttribute('aria-selected')).toBe('true');
      expect(store.show()!.mainStack!.steps[1]).toMatchObject({ memoryNumber: 1, channels: [{ channel: 7, percent: 100 }, { channel: 8, percent: 100 }] });

      click(rows()[0]);
      click(button('.stack__insert')); // nothing fits between 1.0 and 1.1
      expect(element().querySelector('.stack__notice')?.textContent).toContain('no free cue number');
      expect(stackNumbers()).toHaveLength(5);

      click(rows()[4]);
      click(button('.stack__insert')); // after the last cue: the next whole number
      expect(stackNumbers()).toEqual(['1.0', '1.1', '2.0', '13.0', '13.1', '14.0']);
      expect(element().querySelector('.stack__notice')).toBeNull();

      click(rows()[1]);
      click(button('.stack__remove'));
      expect(stackNumbers()).toEqual(['1.0', '2.0', '13.0', '13.1', '14.0']);
      expect(detailText()).toContain('Step 2.0'); // the step now at that place
      expect(rows()[1].getAttribute('aria-selected')).toBe('true');
    });

    it('adds the first step to an empty sequence, numbered by its memory, and removes down to none', () => {
      click(tabs()[2]);
      expect(rows()).toHaveLength(0);
      expect(detailText()).toContain('No steps here yet');

      click(button('.stack__add-first'));
      expect(store.show()!.sequences[1].steps.map((s) => s.number)).toEqual(['1.0']);
      expect(detailText()).toContain('Step 1.0 memory step');
      expect(rows()).toHaveLength(1);

      click(button('.stack__remove'));
      expect(store.show()!.sequences[1].steps).toEqual([]);
      expect(detailText()).toContain('No steps here yet');
      expect(button('.stack__add-first')).not.toBeNull();
    });

    it('drops the selection when the tab changes and forgets a tab the next show lacks', () => {
      click(rows()[2]);
      click(tabs()[1]);
      expect(detailText()).toContain('Select a step');
      click(tabs()[0]);
      expect(rows().every((row) => row.getAttribute('aria-selected') === 'false')).toBe(true);

      click(tabs()[2]);
      store.setShow({ ...sampleShow(), sequences: [] }, 'SHOW08.ASC', 'file');
      fixture.detectChanges();
      expect(tabs().map((tab) => tab.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['Stack 4']);
      expect(rows()).toHaveLength(4);
    });
  });
});
