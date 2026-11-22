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
) {
  const [state, dispatch] = useReducer(documentReducer, {
    documents: initial,
    selected: [],
    anchor: null,
    undo: null,
    message: '',
  });
  const [sort, setSort] = useState<DocumentSort>('date-desc');
  const [zoom, setZoom] = useState(190);
  const visible = sortDocuments(
    state.documents.filter((doc) => doc.topicId === topicId),
    sort,
  );
  const visibleIds = visible.map((doc) => doc.id);
  const selected = state.selected.filter((id) => visibleIds.includes(id));
  return {
    ...state,
    selected,
    visible,
    sort,
    setSort,
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
    update: (update: (documents: readonly DocumentSummary[]) => readonly DocumentSummary[]) =>
      dispatch({ type: 'update', update }),
    move: (ids: readonly string[], target: string) =>
      dispatch({ type: 'move', ids, target, topics }),
    undoMove: () => dispatch({ type: 'undo', topics }),
  };
}
export type DocumentWorkspace = ReturnType<typeof useDocumentWorkspace>;
