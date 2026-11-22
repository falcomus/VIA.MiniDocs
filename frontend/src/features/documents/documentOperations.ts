import type { DocumentSummary } from './document.types';
import type { Topic } from '../topics/topic.types';
export type DocumentSort = 'date-desc' | 'date-asc' | 'title';
/** Calendar validation is shared by display and sorting, including leap-day normalization. */
export function calendarValue(value: string | null): number | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const time = Date.parse(`${value}T00:00:00Z`);
  return Number.isFinite(time) && new Date(time).toISOString().slice(0, 10) === value ? time : null;
}
/** Stable ties retain input order; undated items sort first ascending, last descending, as in KU. */
export function sortDocuments(documents: readonly DocumentSummary[], sort: DocumentSort) {
  return [...documents].sort((a, b) => {
    if (sort === 'title') return a.title.localeCompare(b.title, 'de');
    const first = calendarValue(a.date),
      second = calendarValue(b.date);
    if (first === second) return 0;
    const order = first === null ? -1 : second === null ? 1 : first < second ? -1 : 1;
    return sort === 'date-desc' ? -order : order;
  });
}
/** Documents may target any existing topic, including the inbox, but never the topic-tree root. */
export function canMoveDocuments(
  documents: readonly DocumentSummary[],
  topics: readonly Topic[],
  ids: readonly string[],
  target: string,
) {
  return (
    ids.length > 0 &&
    new Set(ids).size === ids.length &&
    ids.every((id) => documents.some((doc) => doc.id === id)) &&
    topics.some((topic) => topic.id === target)
  );
}
export interface DocumentState {
  readonly documents: readonly DocumentSummary[];
  readonly selected: readonly string[];
  readonly anchor: string | null;
  readonly undo: ReadonlyMap<string, string> | null;
  readonly message: string;
}
export type DocumentAction =
  | { type: 'select'; id: string; visible: readonly string[]; toggle: boolean; range: boolean }
  | { type: 'all'; ids: readonly string[] }
  | { type: 'clear' }
  | { type: 'message'; value: string }
  | {
      type: 'update';
      update: (documents: readonly DocumentSummary[]) => readonly DocumentSummary[];
    }
  | { type: 'move'; ids: readonly string[]; target: string; topics: readonly Topic[] }
  | { type: 'undo'; topics: readonly Topic[] };
/** Move and its per-document undo snapshot are committed atomically against current reducer data. */
export function documentReducer(state: DocumentState, action: DocumentAction): DocumentState {
  switch (action.type) {
    case 'message':
      return { ...state, message: action.value };
    case 'clear':
      return { ...state, selected: [], anchor: null };
    case 'all':
      return {
        ...state,
        selected: action.ids.filter((id) => state.documents.some((doc) => doc.id === id)),
        anchor: null,
      };
    case 'select': {
      if (!action.visible.includes(action.id)) return state;
      const selected = new Set(
        action.toggle ? state.selected.filter((id) => action.visible.includes(id)) : [],
      );
      const anchor = state.anchor && action.visible.includes(state.anchor) ? state.anchor : null;
      if (action.range && anchor) {
        const a = action.visible.indexOf(anchor),
          b = action.visible.indexOf(action.id);
        action.visible.slice(Math.min(a, b), Math.max(a, b) + 1).forEach((id) => selected.add(id));
      } else if (action.toggle && selected.has(action.id)) selected.delete(action.id);
      else selected.add(action.id);
      return {
        ...state,
        selected: [...selected],
        anchor: action.range && anchor ? anchor : action.id,
      };
    }
    case 'update':
      return {
        ...state,
        documents: action.update(state.documents),
        selected: [],
        anchor: null,
        undo: null,
        message: '',
      };
    case 'move': {
      if (!canMoveDocuments(state.documents, action.topics, action.ids, action.target))
        return { ...state, message: 'Dokumente können nicht dorthin verschoben werden.' };
      const ids = new Set(action.ids);
      const moved = state.documents.filter(
        (doc) => ids.has(doc.id) && doc.topicId !== action.target,
      );
      if (!moved.length)
        return {
          ...state,
          selected: [],
          anchor: null,
          message: 'Dokumente sind bereits diesem Thema zugeordnet.',
        };
      const undo = new Map(moved.map((doc) => [doc.id, doc.topicId]));
      return {
        ...state,
        documents: state.documents.map((doc) =>
          undo.has(doc.id) ? { ...doc, topicId: action.target } : doc,
        ),
        selected: [],
        anchor: null,
        undo,
        message: `${moved.length} ${moved.length === 1 ? 'Dokument' : 'Dokumente'} verschoben.`,
      };
    }
    case 'undo': {
      if (!state.undo) return state;
      // If a former topic was removed, preserve the documents in the system inbox.
      return {
        ...state,
        documents: state.documents.map((doc) => {
          const previous = state.undo?.get(doc.id);
          return previous
            ? {
                ...doc,
                topicId: action.topics.some((topic) => topic.id === previous)
                  ? previous
                  : 'unassigned',
              }
            : doc;
        }),
        undo: null,
        selected: [],
        anchor: null,
        message: 'Verschiebung rückgängig gemacht.',
      };
    }
  }
}
