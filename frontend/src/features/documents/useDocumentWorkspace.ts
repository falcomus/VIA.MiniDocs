import type { TranscriptOperation } from './transcriptOperations';
import { setDocumentTags } from './tagOperations';
import { searchDocuments, sortByRelevance } from '../search/search';
import type { SearchControlsState } from '../search/useSearchState';
import { useReducer, useState } from 'react';
import type { Topic } from '../topics/topic.types';
import type { DocumentSummary } from './document.types';
import { documentReducer, sortDocuments } from './documentOperations';
import type { DocumentSort } from './documentOperations';
/** Keeps selection by stable ID; range selection uses the same sorted sequence as rendering. */
export function useDocumentWorkspace(
  initial: readonly DocumentSummary[],
  topics: readonly Topic[],
  topicId: string,
  search: SearchControlsState,
) {
  const [state, dispatch] = useReducer(documentReducer, {
    documents: initial,
    selected: [],
    anchor: null,
    undo: null,
    message: '',
  });
  const [sort, setSort] = useState<DocumentSort>('date-desc');
  const [searchSort, setSearchSort] = useState<DocumentSort | 'relevance'>('relevance');
  const [zoom, setZoom] = useState(190);
  const results = search.active
    ? searchDocuments(state.documents, topics, {
        ...search.criteria,
        scope: search.criteria.scope === 'topic' ? { kind: 'topic', topicId } : { kind: 'all' },
      })
    : [];
  const resultMap = new Map(results.map((result) => [result.documentId, result]));
  const base = search.active
    ? state.documents.filter((doc) => resultMap.has(doc.id))
    : state.documents.filter((doc) => doc.topicId === topicId);
  const effectiveSort =
    search.active && search.criteria.query.trim()
      ? searchSort
      : search.active && searchSort !== 'relevance'
        ? searchSort
        : sort;
  const byId = new Map(base.map((document) => [document.id, document]));
  const visible =
    effectiveSort === 'relevance'
      ? sortByRelevance(results).flatMap((result) => {
          const document = byId.get(result.documentId);
          return document ? [document] : [];
        })
      : sortDocuments(base, effectiveSort);
  const visibleIds = visible.map((doc) => doc.id);
  const selected = state.selected.filter((id) => visibleIds.includes(id));
  return {
    ...state,
    selected,
    visible,
    searchActive: search.active,
    results: resultMap,
    sort: effectiveSort,
    setSort: (next: DocumentSort | 'relevance') => {
      if (search.active) setSearchSort(next);
      else if (next !== 'relevance') setSort(next);
    },
    zoom,
    setZoom: (value: number) => setZoom(Math.max(110, Math.min(380, Math.round(value / 10) * 10))),
    setMessage: (value: string) => dispatch({ type: 'message', value }),
    clear: () => dispatch({ type: 'clear' }),
    all: (checked: boolean) => dispatch({ type: 'all', ids: checked ? visibleIds : [] }),
    select: (
      id: string,
      event: { ctrlKey?: boolean; metaKey?: boolean; shiftKey?: boolean },
      checkbox = false,
    ) =>
      dispatch({
        type: 'select',
        id,
        visible: visibleIds,
        toggle: checkbox || !!event.ctrlKey || !!event.metaKey,
        range: !checkbox && !!event.shiftKey,
      }),
    editTranscript: (id: string, operation: TranscriptOperation) =>
      dispatch({
        type: 'transcript',
        id,
        operation,
        visibleIds: (documents) =>
          search.active
            ? searchDocuments(documents, topics, {
                ...search.criteria,
                scope:
                  search.criteria.scope === 'topic' ? { kind: 'topic', topicId } : { kind: 'all' },
              }).map((result) => result.documentId)
            : documents
                .filter((document) => document.topicId === topicId)
                .map((document) => document.id),
      }),
    setTag: (ids: readonly string[], tagId: string, on: boolean) => {
      const allowed = ids.filter((id) => visibleIds.includes(id));
      const changed = setDocumentTags(state.documents, allowed, tagId, on);
      const visibleAfter = search.active
        ? searchDocuments(changed, topics, {
            ...search.criteria,
            scope: search.criteria.scope === 'topic' ? { kind: 'topic', topicId } : { kind: 'all' },
          }).map((result) => result.documentId)
        : changed.filter((doc) => doc.topicId === topicId).map((doc) => doc.id);
      dispatch({ type: 'tags', ids: allowed, tagId, on, visibleAfter });
    },
    removeTag: (tagId: string) =>
      dispatch({
        type: 'tags',
        ids: state.documents.map((doc) => doc.id),
        tagId,
        on: false,
        visibleAfter: state.documents.map((doc) => doc.id),
      }),
    update: (update: (documents: readonly DocumentSummary[]) => readonly DocumentSummary[]) =>
      dispatch({ type: 'update', update }),
    move: (ids: readonly string[], target: string) =>
      dispatch({ type: 'move', ids, target, topics }),
    undoMove: () => dispatch({ type: 'undo', topics }),
  };
}
export type DocumentWorkspace = ReturnType<typeof useDocumentWorkspace>;
