import type { DocumentSummary } from './document.types';

/** Preserve the filename in the model while avoiding duplicate visible PDF labels. */
export function hasRedundantPdfFilename(document: DocumentSummary): boolean {
  return (
    document.source?.kind === 'pdf' &&
    !!document.fileName &&
    document.fileName.replace(/\.pdf$/i, '') === document.title
  );
}
