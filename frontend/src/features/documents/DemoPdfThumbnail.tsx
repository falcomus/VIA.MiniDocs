import { useEffect, useRef, useState } from 'react';
import { PdfThumbnail } from './PdfThumbnail';
import styles from './DocumentSummary.module.css';

const cache = new Map<string, Blob>();
const maxCached = 8;

/** Lädt lokale PDF-Quellen erst nahe am sichtbaren Bereich. */
export function DemoPdfThumbnail({ url }: { readonly url: string }) {
  const element = useRef<HTMLDivElement>(null);
  const [result, setResult] = useState<{ url: string; blob: Blob | null } | null>(null);
  useEffect(() => {
    const target = element.current;
    if (!target) return;
    const controller = new AbortController();
    let started = false;
    const start = () => {
      if (started) return;
      started = true;
      const cached = cache.get(url);
      if (cached) {
        cache.delete(url);
        cache.set(url, cached);
        setResult({ url, blob: cached });
        return;
      }
      fetch(url, { signal: controller.signal })
        .then((response) => {
          if (!response.ok) throw new Error('PDF nicht verfügbar');
          return response.blob();
        })
        .then((blob) => {
          if (controller.signal.aborted) return;
          cache.set(url, blob);
          if (cache.size > maxCached) cache.delete(cache.keys().next().value!);
          setResult({ url, blob });
        })
        .catch(() => {
          if (!controller.signal.aborted) setResult({ url, blob: null });
        });
    };
    if (typeof IntersectionObserver === 'undefined') start();
    else {
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
        controller.abort();
      };
    }
    return () => controller.abort();
  }, [url]);
  const current = result?.url === url ? result : null;
  return (
    <div ref={element} className={styles.pdfPreview}>
      {current?.blob ? (
        <PdfThumbnail blob={current.blob} />
      ) : (
        <span>{current ? 'Vorschau nicht verfügbar' : 'Vorschau wird geladen …'}</span>
      )}
    </div>
  );
}
