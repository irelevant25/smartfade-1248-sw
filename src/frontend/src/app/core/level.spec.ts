import { clampLevel, levelForKey, levelToPercent } from './level';

describe('level helpers', () => {
  describe('clampLevel', () => {
    it('keeps whole levels inside 0..255', () => {
      expect(clampLevel(0)).toBe(0);
      expect(clampLevel(255)).toBe(255);
      expect(clampLevel(256)).toBe(255);
      expect(clampLevel(-1)).toBe(0);
    });

    it('rounds to a whole level', () => {
      expect(clampLevel(10.4)).toBe(10);
      expect(clampLevel(10.5)).toBe(11);
    });

    it('treats anything that is not a number as zero and infinity as the ends of the range', () => {
      expect(clampLevel(Number.NaN)).toBe(0);
      expect(clampLevel(Infinity)).toBe(255);
      expect(clampLevel(-Infinity)).toBe(0);
    });
  });

  describe('levelToPercent', () => {
    it('maps 0..255 to 0..100', () => {
      expect(levelToPercent(0)).toBe(0);
      expect(levelToPercent(255)).toBe(100);
      expect(levelToPercent(128)).toBe(50);
      expect(levelToPercent(64)).toBe(25);
    });

    it('never leaves 0..100', () => {
      expect(levelToPercent(-10)).toBe(0);
      expect(levelToPercent(1000)).toBe(100);
    });
  });

  describe('levelForKey', () => {
    it('moves by 1 with the up and down arrows', () => {
      expect(levelForKey('ArrowUp', 100)).toBe(101);
      expect(levelForKey('ArrowDown', 100)).toBe(99);
    });

    it('moves by 10 with PageUp and PageDown', () => {
      expect(levelForKey('PageUp', 100)).toBe(110);
      expect(levelForKey('PageDown', 100)).toBe(90);
    });

    it('jumps to the ends with Home and End', () => {
      expect(levelForKey('Home', 100)).toBe(0);
      expect(levelForKey('End', 100)).toBe(255);
    });

    it('stops at the ends of the range', () => {
      expect(levelForKey('ArrowUp', 255)).toBe(255);
      expect(levelForKey('ArrowDown', 0)).toBe(0);
      expect(levelForKey('PageUp', 250)).toBe(255);
      expect(levelForKey('PageDown', 4)).toBe(0);
    });

    it('leaves other keys alone', () => {
      expect(levelForKey('a', 100)).toBeUndefined();
      expect(levelForKey('Tab', 100)).toBeUndefined();
      expect(levelForKey('ArrowLeft', 100)).toBeUndefined();
      expect(levelForKey('ArrowRight', 100)).toBeUndefined();
    });
  });
});
