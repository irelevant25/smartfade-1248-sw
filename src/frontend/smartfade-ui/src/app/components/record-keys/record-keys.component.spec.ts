import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChannelService } from '../../core/channel.service';
import { MemoryEditService } from '../../core/memory-edit.service';
import { RecordKeysComponent } from './record-keys.component';

describe('RecordKeysComponent', () => {
  let fixture: ComponentFixture<RecordKeysComponent>;
  let edit: MemoryEditService;
  let enabled: ReturnType<typeof signal<boolean>>;

  const element = () => fixture.nativeElement as HTMLElement;
  const keys = () => Array.from(element().querySelectorAll<HTMLButtonElement>('.record__key'));
  const key = (selector: string) => element().querySelector<HTMLButtonElement>(selector)!;

  const click = (button: HTMLButtonElement) => {
    button.click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    enabled = signal(true);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ChannelService,
          useValue: {
            enabled: enabled.asReadonly(),
            recordMemory: vi.fn().mockResolvedValue(undefined),
            getMemory: vi.fn().mockResolvedValue(undefined),
            storeMemory: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    });
    edit = TestBed.inject(MemoryEditService);

    fixture = TestBed.createComponent(RecordKeysComponent);
    fixture.detectChanges();
  });

  it("shows the console's record keys in order, the unmodelled ones inactive", () => {
    expect(keys().map((button) => button.textContent?.trim())).toEqual([
      'Copy',
      'Rec Seq',
      'Magic',
      'Rec Mem',
      'Edit Mem',
      'Snapshot',
    ]);
    expect(keys().map((button) => button.disabled)).toEqual([
      false,
      true,
      true,
      false,
      false,
      true,
    ]);
    expect(keys().map((button) => button.className.includes('record__key--white'))).toEqual([
      true,
      false,
      true,
      false,
      false,
      false,
    ]);
    expect(keys()[1].classList.contains('record__key--yellow')).toBe(true);
    expect(keys().map((button) => button.className.includes('record__key--red'))).toEqual([
      false,
      false,
      false,
      true,
      true,
      true,
    ]);
    expect(key('.record__copy').disabled).toBe(false);
    expect(key('.record__rec').disabled).toBe(false);
    expect(key('.record__edit').disabled).toBe(false);
    expect(keys()[1].disabled).toBe(true);
    expect(keys()[2].disabled).toBe(true);
    expect(keys()[5].disabled).toBe(true);
  });

  it('arms REC MEM, EDIT MEM and COPY and lights the armed key', () => {
    click(key('.record__rec'));
    expect(edit.mode()).toBe('record');
    expect(key('.record__rec').getAttribute('aria-pressed')).toBe('true');
    expect(key('.record__rec').classList.contains('record__key--lit')).toBe(true);

    click(key('.record__edit'));
    expect(edit.mode()).toBe('select');
    expect(key('.record__rec').getAttribute('aria-pressed')).toBe('false');
    expect(key('.record__edit').getAttribute('aria-pressed')).toBe('true');

    click(key('.record__copy'));
    expect(edit.mode()).toBe('copy-source');
    expect(key('.record__copy').getAttribute('aria-pressed')).toBe('true');
    expect(key('.record__edit').getAttribute('aria-pressed')).toBe('false');

    click(key('.record__copy'));
    expect(edit.mode()).toBe('idle');
    expect(key('.record__copy').getAttribute('aria-pressed')).toBe('false');
  });

  it('disables the working keys while commands are not possible', () => {
    enabled.set(false);
    fixture.detectChanges();

    expect(key('.record__copy').disabled).toBe(true);
    expect(key('.record__rec').disabled).toBe(true);
    expect(key('.record__edit').disabled).toBe(true);
  });
});
