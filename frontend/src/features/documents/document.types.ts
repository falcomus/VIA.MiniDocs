import type { TagId } from './tags';
import type { TopicId } from '../topics/topic.types';
export type DocumentView = 'list' | 'grid';
export interface DocumentSummary {
  readonly id: string;
  readonly etag?: string;
  readonly title: string;
  readonly topicId: TopicId;
  readonly date: string | null;
  readonly pages: number | null;
  readonly metadataOnly?: boolean;
  readonly demoUrl?: string;
  readonly sizeBytes?: number;
  readonly source?: { readonly kind: 'pdf'; readonly blob: Blob };
  readonly kind: 'Besprechung' | 'Notiz';
  readonly fileName?: string;
  readonly tagIds?: readonly TagId[];
  readonly transcript?: {
    readonly pages: readonly { readonly page: number; readonly text: string }[];
    readonly edited: boolean;
  } | null;
  readonly demoPreview?: { readonly lines: readonly string[] };
  /** Immutable demo ink, separate from the editable transcript, matching KU's source model. */
  readonly demoInk?: {
    readonly head: string;
    readonly pages: readonly (readonly string[])[];
  };
}
