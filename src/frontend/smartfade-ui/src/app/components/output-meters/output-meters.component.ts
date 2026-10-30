import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ConsoleStateService } from '../../core/console-state.service';
import { LEVEL_MAX, OUTPUT_COUNT } from '../../core/console.models';
import { levelToPercent } from '../../core/level';

/** Output addresses labelled under the meters. */
const SCALE_ADDRESSES = [1, 65, 129, 193, 257, 321, 385, 449];

/**
 * Draws one thin bar per output on a single canvas: 512 bars cost one 2D draw call loop, not 512
 * components. A redraw happens after Angular has rendered a change of the outputs (so at most
 * once per frame) or a resize, and never while the panel is collapsed.
 */
function drawOutputs(canvas: HTMLCanvasElement, outputs: readonly number[]): void {
  const ratio = window.devicePixelRatio || 1;
  const width = Math.round(canvas.clientWidth * ratio);
  const height = Math.round(canvas.clientHeight * ratio);
  if (width === 0 || height === 0) {
    return;
  }
  const context = canvas.getContext('2d');
  if (!context) {
    return;
  }
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }

  const style = getComputedStyle(canvas);
  const live = style.getPropertyValue('--sf-meter').trim() || '#3fd0e0';
  const idle = style.getPropertyValue('--sf-meter-idle').trim() || '#222a33';

  const step = width / OUTPUT_COUNT;
  const gap = step >= 3 ? Math.max(1, Math.round(ratio)) : 0;
  const baseline = Math.max(1, Math.round(ratio));

  context.clearRect(0, 0, width, height);
  for (let index = 0; index < OUTPUT_COUNT; index++) {
    const x = Math.floor(index * step);
    const barWidth = Math.max(1, Math.floor((index + 1) * step) - x - gap);
    const level = outputs[index] ?? 0;
    if (level > 0) {
      const barHeight = Math.max(baseline + 1, Math.round((level / LEVEL_MAX) * height));
      context.fillStyle = live;
      context.fillRect(x, height - barHeight, barWidth, barHeight);
    } else {
      context.fillStyle = idle;
      context.fillRect(x, height - baseline, barWidth, baseline);
    }
  }
}

/** The 512 DMX outputs as a collapsible strip of bars. */
@Component({
  selector: 'sf-output-meters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './output-meters.component.html',
  styleUrl: './output-meters.component.scss',
})
export class OutputMetersComponent {
  private readonly _state = inject(ConsoleStateService);
  private readonly _canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  /** Counts size changes of the canvas so that a resize triggers a redraw. */
  private readonly _resizes = signal(0);
  /** Index (0-based) of the output under the pointer. */
  private readonly _hovered = signal<number | undefined>(undefined);

  protected readonly collapsed = signal(false);
  protected readonly scale = SCALE_ADDRESSES.map((address) => ({
    address,
    offset: ((address - 1) / OUTPUT_COUNT) * 100,
  }));
  protected readonly activeCount = computed(
    () => this._state.outputs().filter((level) => level > 0).length,
  );
  protected readonly readout = computed(() => {
    const index = this._hovered();
    return index === undefined
      ? ''
      : `DMX ${index + 1}: ${levelToPercent(this._state.outputs()[index])}%`;
  });

  constructor() {
    // Watch the canvas for size changes while it exists.
    afterRenderEffect((onCleanup) => {
      const canvas = this._canvas()?.nativeElement;
      if (!canvas || typeof ResizeObserver === 'undefined') {
        return;
      }
      const observer = new ResizeObserver(() => this._resizes.update((count) => count + 1));
      observer.observe(canvas);
      onCleanup(() => observer.disconnect());
    });

    // Redraw when the outputs or the size change. Returns before reading the outputs when the
    // canvas is not there, so a collapsed panel does no work for output updates.
    afterRenderEffect(() => {
      const canvas = this._canvas()?.nativeElement;
      if (!canvas) {
        return;
      }
      this._resizes();
      drawOutputs(canvas, this._state.outputs());
    });
  }

  protected toggle(): void {
    this.collapsed.update((collapsed) => !collapsed);
    this._hovered.set(undefined);
  }

  protected onPointerMove(event: PointerEvent): void {
    const canvas = event.currentTarget as HTMLCanvasElement;
    const width = canvas.clientWidth;
    if (width === 0) {
      return;
    }
    const index = Math.floor((event.offsetX / width) * OUTPUT_COUNT);
    this._hovered.set(Math.min(OUTPUT_COUNT - 1, Math.max(0, index)));
  }

  protected onPointerLeave(): void {
    this._hovered.set(undefined);
  }
}
