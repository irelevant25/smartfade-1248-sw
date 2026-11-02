import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { LEVEL_MAX } from '../../core/console.models';
import { clampLevel, levelForKey, levelToPercent } from '../../core/level';

/** What a numbered fader controls; decides the bump button's colour and accessible name. */
export type FaderKind = 'channel' | 'memory';

/**
 * One vertical fader, laid out like the console: the level readout above, the fader, and the
 * numbered bump button below it (channels and memories), or a plain name (MASTER, BUMPS). The
 * thumb sits at the desired level (what the user asked for); the lit bar shows the actual level
 * (what the console reports); the amber stretch between them and the lamp show that the console
 * has not caught up yet.
 *
 * The bump button is momentary: it reports `bumpChange(true)` while held by pointer or by the
 * Space / Enter keys and `bumpChange(false)` on release. While `bumpArmed` (the console's REC MEM
 * or EDIT MEM is waiting for a memory) a press reports `select` instead. Its glow follows
 * `bumpLit`, as the console's bump LEDs mimic the output (manual: "Intensity mimic").
 *
 * Keys on the fader: Up / Down move by 1, PageUp / PageDown by 10, Home / End jump to 0 / 100 %.
 * Left / Right never change the level; they ask the parent to move focus to the neighbour.
 */
@Component({
  selector: 'sf-channel-fader',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './channel-fader.component.html',
  styleUrl: './channel-fader.component.scss',
})
export class ChannelFaderComponent {
  /** Channel or memory fader number, or a name such as MASTER. */
  readonly label = input.required<string | number>();
  readonly kind = input<FaderKind>('channel');
  /** Short text above the readout (a memory's label). */
  readonly caption = input('');
  /** Level the user asked for, 0-255. */
  readonly desired = input.required<number>();
  /** Level the console reports, 0-255. Omit when there is no separate reported level. */
  readonly actual = input<number>();
  /** The console has not confirmed the desired level yet. */
  readonly pending = input(false);
  readonly disabled = input(false);
  /** Shows the level without allowing changes. */
  readonly readOnly = input(false);
  /** Renders the label as the bump button under the fader. */
  readonly bumpable = input(false);
  /**
   * 0..1, how bright the bump button glows: the console lights a channel's button with its
   * output and a memory's button dimmed red when it holds content, at full when it contributes.
   */
  readonly bumpLit = input(0);
  /** REC MEM / EDIT MEM is waiting: the button blinks and a press selects instead of bumping. */
  readonly bumpArmed = input(false);
  /** The bump button alone is inactive (the device accepts no bump), the fader stays usable. */
  readonly bumpDisabled = input(false);
  /**
   * The console blinks this fader's LED (a real console's LedStatus, outside its page view and record screens): shown as a
   * blinking outline on the bump, display only. After a mode switch (a 1-24 / 25-48 switch INFERRED alike) the console
   * treats the fader as unmatched and takes a PC move only once it reaches the content's level (F172); a fader still
   * holding another page's memory blinks too.
   */
  readonly bumpBlinking = input(false);

  /** The user moved the fader to this level, 0-255. */
  readonly levelChange = output<number>();
  /** Left / Right was pressed: move focus to the previous / next fader. */
  readonly navigate = output<-1 | 1>();
  /** The bump button went down (true) or came back up (false). */
  readonly bumpChange = output<boolean>();
  /** The bump button was pressed while armed. */
  readonly select = output<void>();

  private readonly _injector = inject(Injector);
  private readonly _slider = viewChild.required<ElementRef<HTMLInputElement>>('slider');
  /** While the pointer holds the thumb the thumb follows it, not the echoes from the backend. */
  private readonly _dragLevel = signal<number | undefined>(undefined);
  /**
   * The level the last key press asked for, until the next render has shown it. The `desired`
   * input only changes at a render, so without this, keys arriving within the same frame would
   * all step from the same old level and increments would be lost.
   */
  private readonly _keyLevel = signal<number | undefined>(undefined);
  private _keyLevelScheduled = false;
  private readonly _held = signal(false);

  protected readonly held = this._held.asReadonly();
  protected readonly level = computed(
    () => this._dragLevel() ?? this._keyLevel() ?? this.desired(),
  );
  protected readonly reported = computed(() => this.actual() ?? this.level());
  protected readonly levelRatio = computed(() => this.level() / LEVEL_MAX);
  protected readonly reportedRatio = computed(() => this.reported() / LEVEL_MAX);
  protected readonly percent = computed(() => levelToPercent(this.level()));
  protected readonly reportedPercent = computed(() => levelToPercent(this.reported()));
  protected readonly differs = computed(() => this.reported() !== this.level());
  protected readonly accessibleName = computed(() => {
    const label = this.label();
    if (typeof label !== 'number') {
      return label;
    }
    return this.kind() === 'memory' ? `Memory ${label}` : `Channel ${label}`;
  });
  protected readonly bumpName = computed(
    () =>
      (this.bumpArmed() ? `Select ${this.accessibleName()}` : `Bump ${this.accessibleName()}`) +
      (this.bumpBlinking() ? ', blinking on the console' : ''),
  );
  protected readonly blinkingTitle =
    'Blinking on the console: unmatched after a mode switch (a 1-24 / 25-48 switch presumably alike), so a move from here takes effect only once it reaches the level of what the fader now controls; or the fader still holds a memory of another page';
  protected readonly valueText = computed(
    () => `${this.percent()} percent${this.pending() ? ', waiting for the console' : ''}`,
  );

  constructor() {
    // A disabled input fires neither blur nor pointerup, so a drag or a bump that was under way
    // when the fader got disabled (say, the console dropped) must be ended here.
    effect(() => {
      if (this.disabled()) {
        this._dragLevel.set(undefined);
        this._held.set(false);
      }
    });
  }

  /** Moves keyboard focus to this fader. */
  focus(): void {
    this._slider().nativeElement.focus();
  }

  protected onInput(event: Event): void {
    const level = clampLevel((event.target as HTMLInputElement).valueAsNumber);
    this._dragLevel.set(level);
    this.levelChange.emit(level);
  }

  protected beginDrag(): void {
    this._dragLevel.set(this.desired());
  }

  protected endDrag(): void {
    this._dragLevel.set(undefined);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.altKey || event.metaKey) {
      return;
    }
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      this.navigate.emit(event.key === 'ArrowLeft' ? -1 : 1);
      return;
    }
    if (this.readOnly()) {
      return;
    }
    const next = levelForKey(event.key, this.level());
    if (next === undefined) {
      return;
    }
    event.preventDefault();
    if (next !== this.level()) {
      this._requestFromKey(next);
    }
  }

  // ---- Bump button ---------------------------------------------------------------------------

  protected pressBump(event: PointerEvent): void {
    if (this.bumpArmed() || this.disabled() || this.bumpDisabled() || event.button !== 0) {
      return;
    }
    const target = event.currentTarget as HTMLElement | null;
    try {
      target?.setPointerCapture?.(event.pointerId);
    } catch {
      // Not every environment supports pointer capture; the button still releases on blur.
    }
    this._setHeld(true);
  }

  protected releaseBump(): void {
    this._setHeld(false);
  }

  protected onBumpKeydown(event: KeyboardEvent): void {
    if (event.key !== ' ' && event.key !== 'Enter') {
      return;
    }
    if (this.bumpArmed()) {
      return;   // the key's click selects
    }
    event.preventDefault();
    if (!event.repeat && !this.disabled() && !this.bumpDisabled()) {
      this._setHeld(true);
    }
  }

  protected onBumpKeyup(event: KeyboardEvent): void {
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      this._setHeld(false);
    }
  }

  protected onBumpClick(): void {
    if (this.bumpArmed() && !this.disabled() && !this.bumpDisabled()) {
      this.select.emit();
    }
  }

  private _setHeld(held: boolean): void {
    if (this._held() === held) {
      return;
    }
    this._held.set(held);
    this.bumpChange.emit(held);
  }

  private _requestFromKey(level: number): void {
    this._keyLevel.set(level);
    this.levelChange.emit(level);
    if (!this._keyLevelScheduled) {
      this._keyLevelScheduled = true;
      afterNextRender(
        () => {
          this._keyLevelScheduled = false;
          this._keyLevel.set(undefined);
        },
        { injector: this._injector },
      );
    }
  }
}
