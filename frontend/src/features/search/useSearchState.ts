import { useState } from 'react';
import type { SearchMode } from './search';
import type { TagId } from '../documents/tags';
export interface SearchState {
  readonly query: string;
  readonly mode: SearchMode;
  readonly from: string;
  readonly to: string;
  readonly tagIds: readonly TagId[];
  readonly scope: 'all' | 'topic';
}
export const emptySearch: SearchState = {
  query: '',
  mode: 'full',
  from: '',
  to: '',
  tagIds: [],
  scope: 'all',
};
export function useSearchState(validTagIds?: readonly TagId[]) {
  const [stored, setCriteria] = useState<SearchState>(emptySearch);
  const criteria = validTagIds
    ? { ...stored, tagIds: stored.tagIds.filter((id) => validTagIds.includes(id)) }
    : stored;
  const [filterOpen, setFilterOpen] = useState(false);
  const active = !!(
    criteria.query.trim() ||
    criteria.from ||
    criteria.to ||
    criteria.tagIds.length ||
    criteria.scope === 'topic'
  );
  return { criteria, setCriteria, filterOpen, setFilterOpen, active };
}
export type SearchControlsState = ReturnType<typeof useSearchState>;
/** Local calendar date, computed at action time, is passed to deterministic presets. */
export function localToday(now: Date): string {
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
