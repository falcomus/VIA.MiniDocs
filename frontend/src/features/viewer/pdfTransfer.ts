import type { DocumentSummary } from '../documents/document.types';
import type { TagDefinition } from '../documents/tags';
import type { Topic } from '../topics/topic.types';
import type { TranscriptOperation } from '../documents/transcriptOperations';

export const channelName = 'via.minidocs.pdf-transfer.v1';
export type PdfRequest = { kind: 'request'; requestId: string; documentId: string; owner?: string };
export type TranscriptUpdate = {
  kind: 'transcript-update';
  requestId: string;
  documentId: string;
  owner: string;
  operation: TranscriptOperation;
};
export function isTranscriptUpdate(value: unknown): value is TranscriptUpdate {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  if (
    v.kind !== 'transcript-update' ||
    typeof v.requestId !== 'string' ||
    typeof v.documentId !== 'string' ||
    typeof v.owner !== 'string' ||
    !v.operation ||
    typeof v.operation !== 'object'
  )
    return false;
  const op = v.operation as Record<string, unknown>;
  if (op.kind === 'set') return Number.isInteger(op.page) && typeof op.text === 'string';
  if (op.kind === 'replace-all')
    return typeof op.query === 'string' && typeof op.replacement === 'string';
  if (
    op.kind !== 'replace-one' ||
    typeof op.query !== 'string' ||
    typeof op.replacement !== 'string' ||
    !op.match ||
    typeof op.match !== 'object'
  )
    return false;
  const match = op.match as Record<string, unknown>;
  return (
    Number.isInteger(match.page) && Number.isInteger(match.start) && Number.isInteger(match.end)
  );
}
export type PdfResponse = {
  kind: 'response';
  requestId: string;
  documentId: string;
  document: DocumentSummary;
  topics: readonly Topic[];
  tags: readonly TagDefinition[];
};
export function isRequest(value: unknown): value is PdfRequest {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return (
    v.kind === 'request' &&
    typeof v.requestId === 'string' &&
    typeof v.documentId === 'string' &&
    (v.owner === undefined || typeof v.owner === 'string')
  );
}
export function matchesResponse(
  value: unknown,
  requestId: string,
  documentId: string,
): value is PdfResponse {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  if (
    v.kind !== 'response' ||
    v.requestId !== requestId ||
    v.documentId !== documentId ||
    !v.document ||
    typeof v.document !== 'object'
  )
    return false;
  const doc = v.document as DocumentSummary;
  return (
    doc.id === documentId &&
    typeof doc.title === 'string' &&
    typeof doc.topicId === 'string' &&
    (doc.pages === null || (Number.isInteger(doc.pages) && doc.pages > 0)) &&
    (!doc.source || (doc.source.kind === 'pdf' && doc.source.blob instanceof Blob)) &&
    Array.isArray(v.topics) &&
    Array.isArray(v.tags)
  );
}
