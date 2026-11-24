import { useEffect, useRef, useState } from 'react';
import { requestPdfThumbnail } from '../pdf/pdfThumbnail';
import styles from './DocumentSummary.module.css';

export function PdfThumbnail({ blob }: { readonly blob: Blob }) {
  const element = useRef<HTMLDivElement>(null);
  const [result, setResult] = useState<{ blob: Blob; url: string | null } | null>(null);
  useEffect(() => {
    const target = element.current;
    if (!target) return;
    let cancel: (() => void) | undefined;
    const start = () => {
      if (!cancel) cancel = requestPdfThumbnail(blob, (url) => setResult({ blob, url }));
    };
    if (typeof IntersectionObserver === 'undefined') {
      start();
      return () => cancel?.();
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          observer.disconnect();
          start();
        }
      },
      { rootMargin: '100px' },
    );
    observer.observe(target);
    return () => {
      observer.disconnect();
      cancel?.();
    };
  }, [blob]);
  const current = result?.blob === blob ? result.url : undefined;
  return (
    <div ref={element} className={styles.pdfPreview}>
      {current ? (
        <img src={current} alt="PDF-Vorschau, erste Seite" />
      ) : (
        <span>{current === null ? 'Vorschau nicht verfügbar' : 'Vorschau wird geladen …'}</span>
      )}
    </div>
  );
}
