import type { Topic, TopicId } from './topic.types';
/** The system topic keeps its identity; only its label reflects direct document assignments. */
export function topicDisplayName(topic: Topic, documentTopicIds: readonly TopicId[]): string {
  return topic.systemKey === 'UNASSIGNED' && !documentTopicIds.includes(topic.id)
    ? 'Alle zugeordnet'
    : topic.name;
}
