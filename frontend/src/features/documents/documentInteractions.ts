import type { MouseEvent, KeyboardEvent } from 'react';
import type { DocumentWorkspace } from './useDocumentWorkspace';
import type { DocumentDrag } from './useDocumentDrag';
/** Nested action/checkbox events must not also trigger row selection. */
export function documentSelectionProps(
  id: string,
  workspace: DocumentWorkspace,
  drag: DocumentDrag,
  onOpen?: (id: string, forceTab: boolean) => void,
) {
  const select = (event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>) => {
    if ((event.target as HTMLElement).closest('button,input,label')) return;
    workspace.select(id, event);
  };
  return {
    tabIndex: 0,
    draggable: true,
    'data-document-id': id,
    onClick: select,
    onDoubleClick: (event: MouseEvent<HTMLElement>) => {
      if (!(event.target as HTMLElement).closest('button,input,label,a'))
        onOpen?.(id, event.ctrlKey || event.metaKey);
    },
    onAuxClick: (event: MouseEvent<HTMLElement>) => {
      if (event.button === 1 && !(event.target as HTMLElement).closest('button,input,label,a')) {
        event.preventDefault();
        onOpen?.(id, true);
      }
    },
    onKeyDown: (event: KeyboardEvent<HTMLElement>) => {
      if (event.key === ' ' || event.key === 'Enter') {
        if ((event.target as HTMLElement).closest('button,input,label')) return;
        event.preventDefault();
        if (event.key === 'Enter' && onOpen) onOpen(id, false);
        else select(event);
      }
    },
    onDragStart: (event: React.DragEvent<HTMLElement>) => {
      event.stopPropagation();
      drag.start(id, event.dataTransfer);
    },
  };
}
