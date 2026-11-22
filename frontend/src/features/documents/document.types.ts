import type { TopicId } from '../topics/topic.types';
export type DocumentView = 'list' | 'grid';
export interface DocumentSummary {
  readonly id: string;
  readonly title: string;
  readonly topicId: TopicId;
  readonly date: string | null;
  readonly pages: number;
  readonly kind: 'Besprechung' | 'Notiz';
  readonly fileName?: string;
  readonly tags?: readonly string[];
  readonly transcriptStatus?: 'missing' | 'available' | 'edited';
  readonly demoPreview?: { readonly lines: readonly string[] };
}
