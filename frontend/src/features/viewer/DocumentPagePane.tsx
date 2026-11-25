import { useEffect, useState } from 'react';
import type { DocumentSummary } from '../documents/document.types';
import { PdfRenderer } from './PdfRenderer';
import { ContinuousPdfRenderer } from './ContinuousPdfRenderer';
import { ViewerPage } from './ViewerPage';

/** Sources remain owned by the document workspace; absence is not a demo. */
export type DocumentPageSource =
  { readonly kind: 'demo' } | { readonly kind: 'pdf'; readonly blob: Blob | null };
export type DocumentPageSourceResolver = (documentId: string) => DocumentPageSource | undefined;
export function DocumentPagePane({
  source,
  document,
  page,
  zoom,
  onPdfLoaded,
  navigation,
  onVisiblePage,
}: {
  readonly source: DocumentPageSource;
  readonly document: DocumentSummary;
  readonly page: number;
  readonly zoom: number;
  readonly onPdfLoaded?: (blob: Blob, pages: number) => void;
  readonly navigation?: number;
  readonly onVisiblePage?: (page: number) => void;
}) {
  if (source.kind === 'demo') return <ViewerPage document={document} page={page} zoom={zoom} />;
  if (!source.blob && document.demoUrl)
    return (
      <LazyPdf
        key={document.id}
        url={document.demoUrl}
        document={document}
        page={page}
        zoom={zoom}
        onPdfLoaded={onPdfLoaded}
        navigation={navigation}
        onVisiblePage={onVisiblePage}
      />
    );
  if (!source.blob) return <p role="alert">Keine PDF-Quelle vorhanden.</p>;
  if (onVisiblePage)
    return (
      <ContinuousPdfRenderer
        key={document.id}
        blob={source.blob}
        page={page}
        pages={document.pages ?? 1}
        zoom={zoom}
        navigation={navigation ?? 0}
        onLoaded={onPdfLoaded ?? (() => {})}
        onVisiblePage={onVisiblePage}
      />
    );
  return (
    <PdfRenderer
      key={document.id}
      blob={source.blob}
      page={page}
      zoom={zoom}
      onLoaded={onPdfLoaded ?? (() => {})}
    />
  );
}

function LazyPdf({
  url,
  ...props
}: Omit<Parameters<typeof DocumentPagePane>[0], 'source'> & { readonly url: string }) {
  const [state, setState] = useState<Blob | 'error' | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetch(url, { signal: controller.signal })
      .then((r) => {
        if (!r.ok) throw new Error('PDF');
        return r.blob();
      })
      .then(setState)
      .catch(() => {
        if (!controller.signal.aborted) setState('error');
      });
    return () => controller.abort();
  }, [url]);
  if (state === 'error') return <p role="alert">PDF konnte nicht geladen werden.</p>;
  if (!state) return <p role="status">PDF wird geladen …</p>;
  return <DocumentPagePane {...props} source={{ kind: 'pdf', blob: state }} />;
}
