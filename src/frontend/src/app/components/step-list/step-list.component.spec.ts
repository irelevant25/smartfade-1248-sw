import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShowStore } from '../../core/show.store';
import { sampleShow } from '../../core/testing/sample-show';
import { StepListComponent } from './step-list.component';

describe('StepListComponent', () => {
  let fixture: ComponentFixture<StepListComponent>;
  let store: ShowStore;
  let selected: number[];

  const element = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(element().querySelectorAll<HTMLElement>('.steps__row:not(.steps__row--head)'));
  const field = (label: string) => element().querySelector<HTMLInputElement>(`input[aria-label="${label}"]`)!;
  const change = (input: HTMLInputElement, value: string) => {
    input.value = value;
    input.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
  };
  const key = (target: Element, keyName: string) => {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: keyName, bubbles: true, cancelable: true }));
    fixture.detectChanges();
  };

  beforeEach(() => {
    store = TestBed.inject(ShowStore);
    store.setShow(sampleShow(), 'SHOW07.ASC', 'file');
    selected = [];
    fixture = TestBed.createComponent(StepListComponent);
    fixture.componentRef.setInput('home', 'stack');
    fixture.componentRef.setInput('steps', store.show()!.mainStack!.steps);
    fixture.componentInstance.select.subscribe((index) => selected.push(index));
    fixture.detectChanges();
  });

  it('renders one row per step with its number, its content by kind, and the five times in the row', () => {
    expect(rows()).toHaveLength(4);
    const cells = (index: number) => ({
      number: rows()[index].querySelector('.steps__num')?.textContent,
      kind: rows()[index].querySelector('.steps__kind')?.textContent,
      data: rows()[index].querySelector('.steps__kind')?.getAttribute('data-kind'),
      text: rows()[index].querySelector('.steps__text')?.textContent,
    });
    expect(cells(0)).toEqual({ number: '1.0', kind: 'MEM 1', data: 'memory', text: 'OPONA' });
    expect(cells(1)).toEqual({ number: '2.0', kind: 'MEM 2', data: 'memory', text: 'not recorded in this show' });
    expect(cells(2)).toEqual({ number: '13.0', kind: 'CHANNEL', data: 'channel', text: '47/100' });
    expect(cells(3)).toEqual({ number: '13.1', kind: 'STATE', data: 'state', text: 'STATE' });
    expect(rows()[1].querySelectorAll('input')).toHaveLength(5);
    expect(field('Up of step 2.0').value).toBe('5.0');
    expect(field('Up delay of step 2.0').value).toBe('2');
    expect(field('Follow of step 2.0').value).toBe('1:30');
    expect(rows().map((row) => row.getAttribute('aria-selected'))).toEqual(['false', 'false', 'false', 'false']);
    expect(rows().map((row) => row.getAttribute('tabindex'))).toEqual(['0', '-1', '-1', '-1']);
  });

  it('reports a click and the arrow keys as a selection and marks the selected row', () => {
    rows()[1].click();
    fixture.detectChanges();
    expect(selected).toEqual([1]);

    fixture.componentRef.setInput('selected', 1);
    fixture.detectChanges();
    expect(rows()[1].getAttribute('aria-selected')).toBe('true');
    expect(rows()[1].classList.contains('steps__row--selected')).toBe(true);
    expect(rows()[1].getAttribute('tabindex')).toBe('0');
    rows()[1].click(); // the selected row again: nothing new
    expect(selected).toEqual([1]);

    key(rows()[1], 'ArrowDown');
    key(rows()[1], 'ArrowUp');
    key(rows()[1], 'End');
    key(rows()[1], 'Home');
    expect(selected).toEqual([1, 2, 0, 3, 0]);
    key(field('Up of step 2.0'), 'ArrowDown'); // inside an input the arrows are the input's
    expect(selected).toEqual([1, 2, 0, 3, 0]);
  });

  it('edits a time in the row, restores a value the console would not write, and restores on Escape', () => {
    change(field('Up of step 1.0'), '2.5');
    expect(store.show()!.mainStack!.steps[0].upTime).toBe('2.5');
    fixture.componentRef.setInput('steps', store.show()!.mainStack!.steps);
    fixture.detectChanges();

    change(field('Up of step 1.0'), '3,5');
    expect(store.show()!.mainStack!.steps[0].upTime).toBe('2.5');
    expect(field('Up of step 1.0').value).toBe('2.5');

    const input = field('Follow of step 1.0');
    input.value = '9';
    key(input, 'Escape');
    expect(input.value).toBe('0');
    expect(store.show()!.mainStack!.steps[0].followOn).toBe('0');
  });

  it('shows an empty list as such', () => {
    fixture.componentRef.setInput('home', 1);
    fixture.componentRef.setInput('steps', []);
    fixture.detectChanges();

    expect(rows()).toHaveLength(0);
    expect(element().querySelector('.steps__none')?.textContent).toContain('No steps');
  });
});
