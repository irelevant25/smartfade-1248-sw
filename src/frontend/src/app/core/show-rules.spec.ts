import {
  channelHue,
  channelPairsText,
  labelDisplay,
  labelText,
  nextCueNumber,
  parseChannelPairs,
  parsePatchTarget,
  patchTargetLabel,
  stepKind,
} from './show-rules';
import { label, sampleShow } from './testing/sample-show';

describe('show rules', () => {
  it('numbers a new cue from its neighbours as the files do', () => {
    expect(nextCueNumber(undefined, undefined)).toBe('1.0');
    expect(nextCueNumber('14.0', undefined)).toBe('15.0');
    expect(nextCueNumber('10.0', '11.0')).toBe('10.1');
    expect(nextCueNumber('10.1', '10.2')).toBeUndefined();
    expect(nextCueNumber('199.9', undefined)).toBeUndefined();
    expect(nextCueNumber(undefined, '2.0')).toBe('1.0');
  });

  it('derives the Text line and parses channel pairs as the files write them', () => {
    expect(labelText('NOC 3', 'MORE', '')).toBe('NOC 3');
    expect(labelText('LAFIAO', 'GULA', '')).toBe('LAFIAOGULA');
    expect(labelText('BLUE 2', 'HVIRZD', 'X')).toBe('BLUE 2HVIRZDX');
    expect(labelText('&#268;AJ123', '456789', '01234')).toBe('?AJ12345678901234'); // SHOW15.ASC: an entity is one character and renders as '?'
    expect(labelText('PREDNA', '', '&#268;ERVEN')).toBe('PREDNA');
    expect(parseChannelPairs('7/100 15/8')).toEqual([{ channel: 7, percent: 100 }, { channel: 15, percent: 8 }]);
    expect(parseChannelPairs('  ')).toEqual([]);
    expect(parseChannelPairs('7-100')).toBeUndefined();
    expect(channelPairsText([{ channel: 7, percent: 100 }, { channel: 15, percent: 8 }])).toBe('7/100 15/8');
  });

  it('reads a patch target as typed: a channel 1-48, i1 / i2 / IND 1 / IND 2, 0 or nothing to unpatch', () => {
    expect(parsePatchTarget('12')).toBe(12);
    expect(parsePatchTarget(' 48 ')).toBe(48);
    expect(parsePatchTarget('0')).toBe(0);
    expect(parsePatchTarget('')).toBe(0);
    expect(parsePatchTarget('i1')).toBe(49);
    expect(parsePatchTarget('I2')).toBe(50);
    expect(parsePatchTarget('IND 1')).toBe(49);
    expect(parsePatchTarget('ind2')).toBe(50);
    expect(parsePatchTarget('49')).toBeUndefined(); // the independents are typed as i1 / i2, never as a number
    expect(parsePatchTarget('96')).toBeUndefined();
    expect(parsePatchTarget('i3')).toBeUndefined();
    expect(parsePatchTarget('a')).toBeUndefined();
    expect(parsePatchTarget('1.5')).toBeUndefined();
  });

  it('shows IND 1 / IND 2 for channels 49 and 50, nothing for an unpatched address, the number otherwise', () => {
    expect(patchTargetLabel(0)).toBe('');
    expect(patchTargetLabel(49)).toBe('IND 1');
    expect(patchTargetLabel(50)).toBe('IND 2');
    expect(patchTargetLabel(7)).toBe('7');
    expect(patchTargetLabel(63)).toBe('63');
  });

  it('tells the step kinds apart as the model does ($$CUEPARAMS 1 with a memory, 0 a channel step, 2 a State step)', () => {
    const [memory1, memory2, channel, state] = sampleShow().mainStack!.steps;
    expect([memory1, memory2, channel, state].map(stepKind)).toEqual(['memory', 'memory', 'channel', 'state']);
    expect(stepKind({ ...channel, cueParams: null })).toBe('other');
  });

  it('gives every channel and both independents a hue of its own', () => {
    const hues = Array.from({ length: 50 }, (_, i) => channelHue(i + 1));
    expect(new Set(hues).size).toBe(50);
    expect(hues.every((hue) => hue >= 0 && hue < 360)).toBe(true);
  });

  it('joins the lines of a label for display and falls back to its Text line', () => {
    expect(labelDisplay(null)).toBe('');
    expect(labelDisplay(label('OPONA', 'OPONA', '2'))).toBe('OPONA / 2');
    expect(labelDisplay({ text: 'FROM-TEXT', line1: '', line2: '', line3: '' })).toBe('FROM-TEXT');
  });
});
