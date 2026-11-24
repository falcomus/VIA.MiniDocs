import type { DocumentSummary } from '../documents/document.types';
import type { DocumentPageSourceResolver } from './DocumentPagePane';
export type Hit = { readonly page: number; readonly term: string };
export type ViewerSession = {
  readonly id: string;
  readonly ids: readonly string[];
  readonly hits: Readonly<Record<string, Hit | null>>;
};
export function startPage(doc: DocumentSummary, hit: Hit | null | undefined): number {
  return hit &&
    Number.isInteger(hit.page) &&
    hit.page >= 1 &&
    doc.pages !== null &&
    hit.page <= doc.pages &&
    doc.transcript?.pages.some((item) => item.page === hit.page)
    ? hit.page
    : 1;
}
export function viewerUrl(id: string, page: number, term?: string, owner?: string): string {
  const url = new URL(window.location.href);
  url.search = '';
  url.hash = '';
  url.searchParams.set('viewer', 'tab');
  url.searchParams.set('document', id);
  url.searchParams.set('page', String(page));
  if (term) url.searchParams.set('term', term);
  if (owner) url.searchParams.set('owner', owner);
  return url.toString();
}
export function openViewerTab(id: string, page: number, term?: string, owner?: string): boolean {
  try {
    window.open(viewerUrl(id, page, term, owner), '_blank', 'noopener,noreferrer');
    return true;
  } catch {
    return false;
  }
}
/** The current workspace and standalone tab contain synthetic demo documents only. */
export const demoDocumentSource: DocumentPageSourceResolver = () => ({ kind: 'demo' });
