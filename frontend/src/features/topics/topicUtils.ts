import type { Topic, TopicId } from './topic.types';

/** Returns the source ID and all descendants, visiting each ID once even in cyclic input. */
export function descendantIds(topics: readonly Topic[], id: TopicId): Set<TopicId> {
  const result = new Set<TopicId>();
  const visit = (current: TopicId) => {
    if (result.has(current)) return;
    result.add(current);
    topics.filter((topic) => topic.parentId === current).forEach((topic) => visit(topic.id));
  };
  visit(id);
  return result;
}

/** Returns the known ancestor path; stops at missing parents or a repeated ID. */
export function topicPath(topics: readonly Topic[], id: TopicId): Topic[] {
  const path: Topic[] = [];
  const visited = new Set<TopicId>();
  let current = topics.find((topic) => topic.id === id);
  while (current && !visited.has(current.id)) {
    visited.add(current.id);
    path.unshift(current);
    current = topics.find((topic) => topic.id === current?.parentId);
  }
  return path;
}

/** Allows root or existing non-system parents; rejects missing sources, system nodes and cycles. */
export function canMoveTopic(
  topics: readonly Topic[],
  id: TopicId,
  parentId: TopicId | null,
): boolean {
  const topic = topics.find((item) => item.id === id);
  const parent = parentId === null ? null : topics.find((item) => item.id === parentId);
  return (
    !!topic &&
    !topic.systemKey &&
    (parentId === null ||
      (!!parent && !parent.systemKey && !descendantIds(topics, id).has(parentId)))
  );
}

/** Returns a user-facing reason unless the existing, non-system topic has no direct content. */
export function topicDeleteBlockReason(
  topics: readonly Topic[],
  documents: readonly { readonly topicId: TopicId }[],
  id: TopicId,
): string | null {
  const topic = topics.find((item) => item.id === id);
  if (!topic) return 'Dieses Thema existiert nicht mehr.';
  if (topic.systemKey) return 'Systemthemen können nicht gelöscht werden.';
  const hasDocuments = documents.some((document) => document.topicId === id);
  const hasChildren = topics.some((item) => item.parentId === id);
  if (hasDocuments && hasChildren)
    return 'Dieses Thema enthält Dokumente und Unterthemen. Bitte zuerst Dokumente verschieben und Unterthemen entfernen.';
  if (hasDocuments)
    return 'Dieses Thema enthält Dokumente. Bitte zuerst die Dokumente verschieben.';
  if (hasChildren)
    return 'Dieses Thema enthält Unterthemen. Bitte zuerst die Unterthemen entfernen.';
  return null;
}

/** Rechecks current contents at mutation time and removes only the requested node. */
export function deleteEmptyTopic(
  topics: readonly Topic[],
  documents: readonly { readonly topicId: TopicId }[],
  id: TopicId,
): readonly Topic[] {
  return topicDeleteBlockReason(topics, documents, id)
    ? topics
    : topics.filter((topic) => topic.id !== id);
}
