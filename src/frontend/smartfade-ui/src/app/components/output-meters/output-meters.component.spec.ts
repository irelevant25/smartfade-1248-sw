import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConsoleStateService } from '../../core/console-state.service';
import { createSampleState } from '../../core/testing/sample-state';
import { OutputMetersComponent } from './output-meters.component';

/** A recording stand-in for the canvas 2D context (jsdom has none). */
function createFakeContext() {
  return {
    clearRect: vi.fn(),
    fillRect: vi.fn(),
    fillStyle: '' as string,
  };
}

describe('OutputMetersComponent', () => {
  let fixture: ComponentFixture<OutputMetersComponent>;
  let state: ConsoleStateService;
  let context: ReturnType<typeof createFakeContext>;

  const element = () => fixture.nativeElement as HTMLElement;
  const canvas = () => element().querySelector<HTMLCanvasElement>('canvas');
  const toggle = () => element().querySelector<HTMLButtonElement>('.outputs__toggle')!;
  const text = (selector: string) => element().querySelector(selector)?.textContent?.trim();

  beforeEach(async () => {
    context = createFakeContext();
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      context as unknown as CanvasRenderingContext2D,
    );
    // jsdom does no layout: give the canvas a size.
    vi.spyOn(HTMLCanvasElement.prototype, 'clientWidth', 'get').mockReturnValue(1024);
    vi.spyOn(HTMLCanvasElement.prototype, 'clientHeight', 'get').mockReturnValue(72);

    state = TestBed.inject(ConsoleStateService);
    state.applyConsoleStateChanged(createSampleState());

    fixture = TestBed.createComponent(OutputMetersComponent);
    await fixture.whenStable();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('is expanded at first and shows how many outputs are active', () => {
    expect(canvas()).not.toBeNull();
    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(text('.outputs__summary')).toBe('2 active');
  });

  it('draws one bar per output on a single canvas', () => {
    expect(element().querySelectorAll('canvas')).toHaveLength(1);
    expect(context.clearRect).toHaveBeenCalledTimes(1);
    expect(context.fillRect).toHaveBeenCalledTimes(512);
  });

  it('draws a bar as tall as the level, from the bottom', () => {
    // 1024 px for 512 outputs is 2 px each. Output 1 is at full: its bar spans the whole height.
    expect(context.fillRect.mock.calls[0]).toEqual([0, 0, 2, 72]);
    // Output 2 is at 128 of 255: a bit more than half.
    const second = context.fillRect.mock.calls[1];
    expect(second[1]).toBe(72 - Math.round((128 / 255) * 72));
    // Output 3 is off: only the baseline.
    expect(context.fillRect.mock.calls[2][3]).toBe(1);
  });

  it('redraws when the outputs change', async () => {
    state.applyOutputsChanged({ outputs: [0, 0, 255] });
    await fixture.whenStable();

    expect(context.clearRect).toHaveBeenCalledTimes(2);
    expect(context.fillRect).toHaveBeenCalledTimes(1024);
    expect(text('.outputs__summary')).toBe('1 active');
  });

  it('labels the scale under the bars', () => {
    const labels = Array.from(element().querySelectorAll('.outputs__tick')).map((tick) =>
      tick.textContent?.trim(),
    );

    expect(labels).toEqual(['1', '65', '129', '193', '257', '321', '385', '449']);
  });

  describe('collapsing', () => {
    it('removes the canvas, keeps the summary and does no drawing for output updates', async () => {
      toggle().click();
      await fixture.whenStable();

      expect(canvas()).toBeNull();
      expect(toggle().getAttribute('aria-expanded')).toBe('false');
      expect(text('.outputs__summary')).toBe('2 active');

      const drawn = context.fillRect.mock.calls.length;
      state.applyOutputsChanged({ outputs: [255, 255, 255] });
      await fixture.whenStable();

      expect(context.fillRect.mock.calls.length).toBe(drawn);
      expect(text('.outputs__summary')).toBe('3 active');
    });

    it('draws again when expanded', async () => {
      toggle().click();
      await fixture.whenStable();
      state.applyOutputsChanged({ outputs: [255, 255, 255] });
      const drawn = context.fillRect.mock.calls.length;

      toggle().click();
      await fixture.whenStable();

      expect(canvas()).not.toBeNull();
      expect(context.fillRect.mock.calls.length).toBe(drawn + 512);
    });
  });

  describe('hovering', () => {
    const moveTo = async (offsetX: number) => {
      const event = new MouseEvent('pointermove', { bubbles: true });
      Object.defineProperty(event, 'offsetX', { value: offsetX });
      canvas()!.dispatchEvent(event);
      await fixture.whenStable();
    };

    it('reads out the address and level under the pointer', async () => {
      // 1024 px wide for 512 outputs: 2 px each, so x = 2 is output 2.
      await moveTo(2);

      expect(text('.outputs__readout')).toBe('DMX 2: 50%');
    });

    it('keeps the read-out inside the range at the edges', async () => {
      await moveTo(-5);
      expect(text('.outputs__readout')).toBe('DMX 1: 100%');

      await moveTo(5000);
      expect(text('.outputs__readout')).toBe('DMX 512: 0%');
    });

    it('clears the read-out when the pointer leaves', async () => {
      await moveTo(2);

      canvas()!.dispatchEvent(new MouseEvent('pointerleave'));
      await fixture.whenStable();

      expect(text('.outputs__readout')).toBe('');
    });
  });
});
