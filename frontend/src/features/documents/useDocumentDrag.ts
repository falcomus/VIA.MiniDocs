import { useEffect, useRef, useState } from 'react';
import type { DragEvent } from 'react';
import type { Topic } from '../topics/topic.types';
import type { DocumentWorkspace } from './useDocumentWorkspace';
import { canMoveDocuments } from './documentOperations';
const mime = 'application/x-minidoc-docs';
/** Internal session only; payload must exactly match the locally recorded document IDs. */
export function useDocumentDrag(
  workspace: DocumentWorkspace,
  topics: readonly Topic[],
  cancelTopicDrag: () => void,
  onMoved: (id: string) => void,
  navigationRevision?: { readonly current: number },
) {
  const session = useRef<readonly string[] | null>(null);
  const versions = useRef<ReadonlyMap<string, string>>(new Map());
  const [target, setTarget] = useState<string | null>(null);
  useEffect(() => {
    const clear = () => {
      session.current = null;
      setTarget(null);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') clear();
    };
    const start = () => clear();
    document.addEventListener('dragstart', start, true);
    document.addEventListener('drop', clear);
    document.addEventListener('dragend', clear);
    document.addEventListener('keydown', escape);
    window.addEventListener('blur', clear);
    return () => {
      session.current = null;
      document.removeEventListener('dragstart', start, true);
      document.removeEventListener('drop', clear);
      document.removeEventListener('dragend', clear);
      document.removeEventListener('keydown', escape);
      window.removeEventListener('blur', clear);
    };
  }, []);
  return {
    target,
    start(id: string, transfer: DataTransfer) {
      cancelTopicDrag();
      const ids = workspace.selected.includes(id) ? workspace.selected : [id];
      if (!workspace.visible.some((doc) => doc.id === id)) return;
      if (!workspace.selected.includes(id)) workspace.select(id, {});
      session.current = ids;
      versions.current = new Map(
        workspace.documents
          .filter((doc) => ids.includes(doc.id))
          .map((doc) => [doc.id, doc.etag ?? '']),
      );
      setTarget(null);
      transfer.setData(mime, JSON.stringify(ids));
      transfer.effectAllowed = 'move';
    },
    handles(transfer: DataTransfer) {
      return session.current !== null || Array.from(transfer.types ?? []).includes(mime);
    },
    over(id: string) {
      const valid =
        !!session.current && canMoveDocuments(workspace.documents, topics, session.current, id);
      setTarget(valid ? id : null);
      return valid;
    },
    leave(id: string, event: DragEvent<HTMLElement>) {
      if (
        !(event.relatedTarget instanceof Node) ||
        !event.currentTarget.contains(event.relatedTarget)
      )
        setTarget((current) => (current === id ? null : current));
    },
    drop(id: string, transfer: DataTransfer) {
      const ids = session.current;
      session.current = null;
      setTarget(null);
      if (
        !ids ||
        transfer.getData(mime) !== JSON.stringify(ids) ||
        transfer.getData('application/x-minidoc-topic') ||
        !canMoveDocuments(workspace.documents, topics, ids, id)
      ) {
        workspace.setMessage('Dokumente können nicht dorthin verschoben werden.');
        return false;
      }
      const changed = workspace.documents.some(
        (document) => ids.includes(document.id) && document.topicId !== id,
      );
      const revision = navigationRevision?.current;
      const result = workspace.move(ids, id, versions.current);
      if (result)
        void result.then((moved) => {
          if (moved && changed && revision === navigationRevision?.current) onMoved(id);
        });
      else if (changed) onMoved(id);
      return changed;
    },
  };
}
export type DocumentDrag = ReturnType<typeof useDocumentDrag>;
