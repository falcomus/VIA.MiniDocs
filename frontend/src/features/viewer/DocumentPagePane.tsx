import { useEffect, useState } from 'react';
import type { DocumentSummary } from '../documents/document.types';
import { ViewerPage } from './ViewerPage';
import styles from './DocumentPagePane.module.css';

/** PDF bytes are provided by a future adapter, not inferred from a filename. */
export type DocumentPageSource =
  { readonly kind: 'demo' } | { readonly kind: 'pdf'; readonly blob: Blob | null };

/** The caller resolves each document separately; absence is not treated as a demo. */
export type DocumentPageSourceResolver = (documentId: string) => DocumentPageSource | undefined;
export function DocumentPagePane({
  source,
  document,
  page,
  zoom,
}: {
  readonly source: DocumentPageSource;
  readonly document: DocumentSummary;
  readonly page: number;
  readonly zoom: number;
}) {
  const blob = source.kind === 'pdf' ? source.blob : null;
  const [result, setResult] = useState<{ blob: Blob; url: string | null } | null>(null);
  const [previousBlob, setPreviousBlob] = useState(blob);
  if (previousBlob !== blob) {
    setPreviousBlob(blob);
    setResult(null);
  }
  useEffect(() => {
    if (!blob) return;
    let cancelled = false;
    let url: string | null = null;
    async function prepare(sourceBlob: Blob) {
      try {
        const bytes = new Uint8Array(await sourceBlob.slice(0, 5).arrayBuffer());
        if (sourceBlob.type !== 'application/pdf' || String.fromCharCode(...bytes) !== '%PDF-') {
          if (!cancelled) {
            const next = { blob: sourceBlob, url: null };
            setResult(next);
          }
          return;
        }
        if (cancelled) return;
        url = URL.createObjectURL(sourceBlob);
        const next = { blob: sourceBlob, url };
        setResult(next);
      } catch {
        if (!cancelled) setResult({ blob: sourceBlob, url: null });
      }
    }
    void prepare(blob);
    return () => {
      cancelled = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [blob]);
  if (source.kind === 'demo') return <ViewerPage document={document} page={page} zoom={zoom} />;
  if (!blob) return <p role="alert">Keine PDF-Quelle vorhanden.</p>;
  if (!result || result.blob !== blob) return <p role="status">PDF-Quelle wird geprüft …</p>;
  if (!result.url)
    return <p role="alert">Die PDF-Quelle ist ungültig oder konnte nicht gelesen werden.</p>;
  return (
    <div className={styles.pdf}>
      <p>
        PDF: Zoom und Scrollen steuert der Browser. Interne Seitenwechsel werden nicht
        zurückgemeldet.
      </p>
      <iframe title={`PDF: ${document.title}`} src={`${result.url}#page=${page}`} />
    </div>
  );
}
