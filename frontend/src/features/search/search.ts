import { normalizeSearchText } from './normalize';
export { normalizeSearchText } from './normalize';
import type { DocumentSummary } from '../documents/document.types';
import type { TagId } from '../documents/tags';
import { calendarValue } from '../documents/documentOperations';
import type { Topic, TopicId } from '../topics/topic.types';
import { descendantIds } from '../topics/topicUtils';

export type SearchMode = 'title' | 'full';
export interface SearchCriteria {
  readonly query: string;
  readonly mode: SearchMode;
  readonly from?: string;
  readonly to?: string;
  readonly tagIds?: readonly TagId[];
  readonly scope: { readonly kind: 'all' } | { readonly kind: 'topic'; readonly topicId: TopicId };
}
export interface SearchResult {
  readonly documentId: string;
  readonly score: number;
  readonly terms: readonly string[];
  readonly hit: { readonly page: number; readonly term: string } | null;
}
export interface DateRange {
  readonly from: string;
  readonly to: string;
}
/** Mirrors KU parsing: double-quoted phrases or non-whitespace terms. */
export function parseSearchQuery(query: string): string[] {
  return Array.from(query.matchAll(/"([^"]+)"|(\S+)/g), (match) =>
    normalizeSearchText(match[1] || match[2]),
  ).filter(Boolean);
}
/** Uses an ISO calendar date supplied by the caller. Leap-day rollover follows the original Date.setFullYear behavior. */
export function datePreset(preset: '12m' | 'year', today: string): DateRange {
  const time = calendarValue(today);
  if (time === null) throw new RangeError('Ungültiger Stichtag');
  if (preset === 'year') return { from: `${today.slice(0, 4)}-01-01`, to: today };
  const date = new Date(time);
  date.setUTCFullYear(date.getUTCFullYear() - 1);
  return { from: date.toISOString().slice(0, 10), to: today };
}
/** KU uses AND across terms, +10 once per title/filename term, plus non-overlapping page occurrences. */
export function searchDocuments(
  documents: readonly DocumentSummary[],
  topics: readonly Topic[],
  criteria: SearchCriteria,
): SearchResult[] {
  const terms = parseSearchQuery(criteria.query);
  const from = criteria.from ? calendarValue(criteria.from) : null;
  const to = criteria.to ? calendarValue(criteria.to) : null;
  if (
    (criteria.from && from === null) ||
    (criteria.to && to === null) ||
    (from !== null && to !== null && from > to)
  )
    return [];
  const selectedScope = criteria.scope;
  if (selectedScope.kind === 'topic' && !topics.some((topic) => topic.id === selectedScope.topicId))
    return [];
  const scope =
    selectedScope.kind === 'topic' ? descendantIds(topics, selectedScope.topicId) : null;
  const results: SearchResult[] = [];
  for (const document of documents) {
    if (scope && !scope.has(document.topicId)) continue;
    const date = calendarValue(document.date);
    if (
      (from !== null || to !== null) &&
      (date === null || (from !== null && date < from) || (to !== null && date > to))
    )
      continue;
    if (criteria.tagIds?.some((id) => !document.tagIds?.includes(id))) continue;
    const title = normalizeSearchText(`${document.title} ${document.fileName ?? ''}`);
    const pages =
      criteria.mode === 'full'
        ? (document.transcript?.pages ?? []).map((page) => ({
            page: page.page,
            text: normalizeSearchText(page.text),
          }))
        : [];
    let score = 0;
    let hit: SearchResult['hit'] = null;
    let matched = true;
    for (const term of terms) {
      const inTitle = title.includes(term);
      let occurrences = 0;
      let firstPage: number | null = null;
      for (const page of pages) {
        let pos = page.text.indexOf(term);
        while (pos >= 0) {
          occurrences++;
          firstPage ??= page.page;
          pos = page.text.indexOf(term, pos + term.length);
        }
      }
      if (!inTitle && !occurrences) {
        matched = false;
        break;
      }
      score += (inTitle ? 10 : 0) + occurrences;
      if (hit === null && firstPage !== null) hit = { page: firstPage, term };
    }
    if (matched) results.push({ documentId: document.id, score, terms, hit });
  }
  return results;
}
/** Original relevance view is descending; stable ties retain input order. */
export function sortByRelevance(results: readonly SearchResult[]): SearchResult[] {
  return [...results].sort((a, b) => b.score - a.score);
}
