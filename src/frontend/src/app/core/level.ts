import { LEVEL_MAX, LEVEL_MIN } from './console.models';

/** Coarse key step (PageUp / PageDown), in 0-255 units. */
export const LEVEL_COARSE_STEP = 10;

/** Forces any number into the console's integer range 0-255 (NaN becomes 0). */
export function clampLevel(value: number): number {
  if (!Number.isFinite(value)) {
    return value === Infinity ? LEVEL_MAX : LEVEL_MIN;
  }
  return Math.min(LEVEL_MAX, Math.max(LEVEL_MIN, Math.round(value)));
}

/** 0-255 to whole percent, 0-100. */
export function levelToPercent(level: number): number {
  return Math.round((clampLevel(level) * 100) / LEVEL_MAX);
}

/**
 * The level a fader should take after a key press, or undefined when the key does not move a
 * fader. Up/Down move by 1, PageUp/PageDown by 10, Home/End jump to 0 / 255.
 */
export function levelForKey(key: string, current: number): number | undefined {
  switch (key) {
    case 'ArrowUp':
      return clampLevel(current + 1);
    case 'ArrowDown':
      return clampLevel(current - 1);
    case 'PageUp':
      return clampLevel(current + LEVEL_COARSE_STEP);
    case 'PageDown':
      return clampLevel(current - LEVEL_COARSE_STEP);
    case 'Home':
      return LEVEL_MIN;
    case 'End':
      return LEVEL_MAX;
    default:
      return undefined;
  }
}
