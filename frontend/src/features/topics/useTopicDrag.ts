import { useEffect, useRef, useState } from 'react';
import type { Topic, TopicId } from './topic.types';
import { canMoveTopic } from './topicUtils';

const topicMimeType = 'application/x-minidoc-topic';

/** Shared internal drag session for topic rows and the sidebar root; payload alone never authorizes a move. */
export function useTopicDrag(
  topics: readonly Topic[],
  onMove: (id: TopicId, parentId: TopicId | null) => void,
  onReject: () => void,
) {
  const source = useRef<TopicId | null>(null);
  const [dropTarget, setDropTarget] = useState<TopicId | null>(null);

  useEffect(() => {
    const clear = () => {
      source.current = null;
      setDropTarget(null);
    };
    const cancelOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') clear();
    };
    document.addEventListener('drop', clear);
    document.addEventListener('dragend', clear);
    document.addEventListener('keydown', cancelOnEscape);
    window.addEventListener('blur', clear);
    return () => {
      source.current = null;
      document.removeEventListener('drop', clear);
      document.removeEventListener('dragend', clear);
      document.removeEventListener('keydown', cancelOnEscape);
      window.removeEventListener('blur', clear);
    };
  }, []);

  return {
    dropTarget,
    cancel() {
      source.current = null;
      setDropTarget(null);
    },
    start(id: TopicId, transfer: DataTransfer) {
      source.current = null;
      setDropTarget(null);
      if (!canMoveTopic(topics, id, null)) return false;
      transfer.setData(topicMimeType, id);
      transfer.effectAllowed = 'move';
      source.current = id;
      return true;
    },
    over(parentId: TopicId | null) {
      const allowed = source.current !== null && canMoveTopic(topics, source.current, parentId);
      setDropTarget(allowed ? parentId : null);
      return allowed;
    },
    leave(id: TopicId) {
      setDropTarget((current) => (current === id ? null : current));
    },
    /** Consume once, including rejected drops. Missing or mismatched payloads fail closed. */
    drop(parentId: TopicId | null, transfer: DataTransfer) {
      const id = source.current;
      source.current = null;
      setDropTarget(null);
      if (
        id === null ||
        transfer.getData(topicMimeType) !== id ||
        !canMoveTopic(topics, id, parentId)
      ) {
        onReject();
        return false;
      }
      onMove(id, parentId);
      return true;
    },
  };
}

export type TopicDrag = ReturnType<typeof useTopicDrag>;
