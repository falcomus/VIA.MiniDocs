import { normalizeSearchText } from './search';
/** Map normalized units to original UTF-16 offsets so diacritics and surrogate pairs remain intact. */
export function Highlight({
  text,
  terms,
}: {
  readonly text: string;
  readonly terms: readonly string[];
}) {
  let normalized = '';
  const starts: number[] = [];
  const ends: number[] = [];
  for (let index = 0; index < text.length;) {
    const cp = text.codePointAt(index);
    if (cp === undefined) break;
    const char = String.fromCodePoint(cp);
    const end = index + char.length;
    const part = normalizeSearchText(char);
    if (!part && ends.length) ends[ends.length - 1] = end;
    for (let i = 0; i < part.length; i++) {
      starts.push(index);
      ends.push(end);
    }
    normalized += part;
    index = end;
  }
  const ranges: { start: number; end: number }[] = [];
  for (const term of terms) {
    if (!term) continue;
    let offset = normalized.indexOf(term);
    while (offset >= 0) {
      ranges.push({ start: starts[offset], end: ends[offset + term.length - 1] });
      offset = normalized.indexOf(term, offset + term.length);
    }
  }
  ranges.sort((a, b) => a.start - b.start || b.end - a.end);
  const merged: { start: number; end: number }[] = [];
  for (const r of ranges) {
    const last = merged.at(-1);
    if (last && r.start <= last.end) last.end = Math.max(last.end, r.end);
    else merged.push({ ...r });
  }
  const output: React.ReactNode[] = [];
  let position = 0;
  for (const r of merged) {
    output.push(text.slice(position, r.start));
    output.push(<mark key={`${r.start}-${r.end}`}>{text.slice(r.start, r.end)}</mark>);
    position = r.end;
  }
  output.push(text.slice(position));
  return <>{output}</>;
}
