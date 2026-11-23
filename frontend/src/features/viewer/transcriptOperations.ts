import { normalizeSearchText } from '../search/normalize';
import type { DocumentSummary } from '../documents/document.types';
export interface TranscriptMatch {
  readonly page: number;
  readonly start: number;
  readonly end: number;
}
export type TranscriptOperation =
  | { readonly kind: 'set'; readonly page: number; readonly text: string }
  | {
      readonly kind: 'replace-one';
      readonly match: TranscriptMatch;
      readonly query: string;
      readonly replacement: string;
    }
  | { readonly kind: 'replace-all'; readonly query: string; readonly replacement: string };
/** Normalized substring positions are mapped back to UTF-16 offsets in the original text. */
export function findTranscriptMatches(doc: DocumentSummary, query: string): TranscriptMatch[] {
  const needle = normalizeSearchText(query);
  if (!needle) return [];
  const matches: TranscriptMatch[] = [];
  for (const part of [...(doc.transcript?.pages ?? [])]
    .filter((item) => Number.isInteger(item.page) && item.page >= 1 && item.page <= doc.pages)
    .sort((a, b) => a.page - b.page)) {
    const normalized = normalizeSearchText(part.text);
    const starts: number[] = [],
      ends: number[] = [];
    for (let i = 0; i < part.text.length;) {
      const code = part.text.codePointAt(i);
      if (code === undefined) break;
      const char = String.fromCodePoint(code),
        end = i + char.length,
        piece = normalizeSearchText(char);
      if (!piece && ends.length) ends[ends.length - 1] = end;
      for (let j = 0; j < piece.length; j++) {
        starts.push(i);
        ends.push(end);
      }
      i = end;
    }
    let at = normalized.indexOf(needle);
    while (at >= 0) {
      matches.push({ page: part.page, start: starts[at], end: ends[at + needle.length - 1] });
      at = normalized.indexOf(needle, at + needle.length);
    }
  }
  return matches;
}
/** Applies one operation without changing metadata, unrelated pages or unchanged documents. */
export function applyTranscriptOperation(
  doc: DocumentSummary,
  operation: TranscriptOperation,
): DocumentSummary {
  const pages = doc.transcript?.pages ?? [];
  const setPage = (page: number, text: string): DocumentSummary => {
    if (!Number.isInteger(page) || page < 1 || page > doc.pages) return doc;
    const existing = pages.find((part) => part.page === page);
    if ((existing?.text ?? '') === text) return doc;
    return {
      ...doc,
      transcript: {
        edited: true,
        pages: [...pages.filter((part) => part.page !== page), { page, text }].sort(
          (a, b) => a.page - b.page,
        ),
      },
    };
  };
  if (operation.kind === 'set') return setPage(operation.page, operation.text);
  if (operation.kind === 'replace-one') {
    const { match, query, replacement } = operation;
    const text = pages.find((part) => part.page === match.page)?.text;
    if (
      !query ||
      text === undefined ||
      !findTranscriptMatches(doc, query).some(
        (item) => item.page === match.page && item.start === match.start && item.end === match.end,
      )
    )
      return doc;
    return setPage(match.page, text.slice(0, match.start) + replacement + text.slice(match.end));
  }
  if (!operation.query) return doc;
  // KU counts normalized matches but replaces literal, case-insensitive occurrences.
  const literal = literalPattern(operation.query);
  let changed = false;
  const updated = pages.map((part) => {
    const text = part.text.replace(literal, () => operation.replacement);
    if (text === part.text) return part;
    changed = true;
    return { ...part, text };
  });
  return changed ? { ...doc, transcript: { edited: true, pages: updated } } : doc;
}
/** KU replacement uses literal, case-insensitive matches across the whole document. */
export function countReplaceMatches(doc: DocumentSummary, query: string): number {
  if (!query) return 0;
  return (doc.transcript?.pages ?? []).reduce(
    (count, page) => count + [...page.text.matchAll(literalPattern(query))].length,
    0,
  );
}

function literalPattern(query: string): RegExp {
  return new RegExp(query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
}
