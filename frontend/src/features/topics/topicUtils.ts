import type { Topic, TopicId } from './topic.types';

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

export function canMoveTopic(topics: readonly Topic[], id: TopicId, parentId: TopicId | null): boolean {
  const topic = topics.find((item) => item.id === id);
  const parent = parentId === null ? null : topics.find((item) => item.id === parentId);
  return !!topic && !topic.systemKey && (parentId === null || (!!parent && !parent.systemKey && !descendantIds(topics, id).has(parentId)));
}
