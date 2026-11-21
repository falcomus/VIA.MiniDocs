import type { TopicId } from '../topics/topic.types';

export interface DocumentSummary {
  readonly id: string;
  readonly title: string;
  readonly topicId: TopicId;
  readonly date: string;
  readonly pages: number;
  readonly kind: 'Besprechung' | 'Notiz';
}
