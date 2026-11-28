import { expect, it } from 'vitest';
import { validateTag } from './tagOperations';

const candidate = (name: string) => ({ name, description: '', color: '#123456' });

it('wendet dieselbe Trim- und de-Lowercase-Regel für neue und vorhandene Tagnamen an', () => {
  const vectors = [
    ['\uFEFF\u00A0ÄRGER\u2003\t', 'ärger'],
    ['\u0085ÄRGER\u0085', '\u0085ärger\u0085'],
    ['İSTANBUL', 'i\u0307stanbul'],
    ['ΟΣ', 'ος'],
    ['ẞ', 'ß'],
    ['A\u0308', 'a\u0308'],
    ['Ä', 'ä'],
    ['\u1680\u202FGrüN\u3000', 'grün'],
  ] as const;
  for (const [input, key] of vectors) {
    expect(input.trim().toLocaleLowerCase('de')).toBe(key);
    expect(validateTag(candidate(input), [{ id: 'existing', ...candidate(key) }])).toContain(
      'bereits vergeben',
    );
    expect(validateTag(candidate(key), [{ id: 'existing', ...candidate(input) }])).toContain(
      'bereits vergeben',
    );
    expect(validateTag(candidate(input), [{ id: 'existing', ...candidate(key) }], 'existing')).toBe(
      '',
    );
  }
});

it('trimmt ECMAScript-Leerzeichen, erhält NEL und unterscheidet Akzente', () => {
  const whitespace =
    '\u0009\u000A\u000B\u000C\u000D\u0020\u00A0\u1680\u2000\u2001\u2002\u2003' +
    '\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF';
  expect(validateTag(candidate(whitespace), [])).toContain('Namen');
  expect(validateTag(candidate('\u0085'), [])).toBe('');
  expect(validateTag(candidate('A'), [{ id: 'existing', ...candidate('Ä') }])).toBe('');
  expect(validateTag(candidate('A\u0308'), [{ id: 'existing', ...candidate('Ä') }])).toBe('');
});
