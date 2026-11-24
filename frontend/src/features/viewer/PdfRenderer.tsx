import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page } from 'react-pdf';
import '../pdf/pdfDocument';
import 'react-pdf/dist/Page/TextLayer.css';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import styles from './PdfRenderer.module.css';

type RenderSize = { page: number; width: number; zoom: number };
const renderKey = (value: RenderSize) => `${value.page}:${value.width}:${value.zoom}`;

export function PdfRenderer({
  blob,
  page,
  zoom,
  onLoaded,
}: {
  readonly blob: Blob;
  readonly page: number;
  readonly zoom: number;
  readonly onLoaded: (blob: Blob, pages: number) => void;
}) {
  const pane = useRef<HTMLDivElement>(null);
  const active = useRef<Blob | null>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [aspect, setAspect] = useState<{ page: number; ratio: number } | null>(null);
  const [ready, setReady] = useState<RenderSize | null>(null);
  const [failedKey, setFailedKey] = useState<string | null>(null);
  useEffect(() => {
    active.current = blob;
    return () => {
      active.current = null;
    };
  }, [blob]);
  useEffect(() => {
    const element = pane.current?.parentElement;
    if (!element) return;
    // The viewport grows with the PDF itself; measure its constrained scroll container instead.
    const container = element.parentElement ?? element;
    const toolbar = container !== element ? element.previousElementSibling : null;
    const measure = () => {
      const css = getComputedStyle(element);
      const horizontal = (parseFloat(css.paddingLeft) || 0) + (parseFloat(css.paddingRight) || 0);
      const vertical = (parseFloat(css.paddingTop) || 0) + (parseFloat(css.paddingBottom) || 0);
      const width = Math.max(0, Math.floor(container.clientWidth - horizontal));
      const height = Math.max(
        0,
        Math.floor(container.clientHeight - (toolbar?.clientHeight ?? 0) - vertical),
      );
      setSize((previous) =>
        previous.width === width && previous.height === height ? previous : { width, height },
      );
    };
    measure();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure);
      return () => window.removeEventListener('resize', measure);
    }
    const observer = new ResizeObserver(measure);
    observer.observe(container);
    if (toolbar) observer.observe(toolbar);
    return () => observer.disconnect();
  }, []);
  const width =
    size.height > 0 && aspect?.page === page
      ? Math.min(size.width, Math.floor(size.height * aspect.ratio))
      : size.width;
  const target = { page, width, zoom };
  const targetKey = renderKey(target);
  const currentTarget = useRef(targetKey);
  useLayoutEffect(() => {
    currentTarget.current = targetKey;
  }, [targetKey]);
  const previous = ready && renderKey(ready) !== targetKey ? ready : null;
  return (
    <div ref={pane} className={styles.pane} aria-label="PDF-Dokument">
      <Document
        file={blob}
        loading={<p role="status">PDF wird geladen …</p>}
        error={<p role="alert">PDF konnte nicht geladen werden.</p>}
        onLoadSuccess={({ numPages }) => {
          if (active.current === blob) onLoaded(blob, numPages);
        }}
      >
        {width > 0 && (
          <div className={styles.layers}>
            {[...(previous ? [previous] : []), target].map((item) => {
              const key = renderKey(item);
              const pending = !!previous && key === targetKey;
              return (
                <div
                  key={key}
                  className={pending ? styles.pending : undefined}
                  aria-hidden={pending || undefined}
                >
                  <Page
                    pageNumber={item.page}
                    width={item.width}
                    scale={item.zoom / 100}
                    onLoadSuccess={({ originalWidth, originalHeight }) => {
                      if (originalWidth > 0 && originalHeight > 0 && item.page === page)
                        setAspect((value) =>
                          value?.page === page && value.ratio === originalWidth / originalHeight
                            ? value
                            : { page, ratio: originalWidth / originalHeight },
                        );
                    }}
                    onRenderSuccess={() => {
                      if (key === currentTarget.current) {
                        setReady((value) => (value && renderKey(value) === key ? value : item));
                        setFailedKey(null);
                      }
                    }}
                    onRenderError={() => {
                      if (key === currentTarget.current) setFailedKey(key);
                    }}
                    onLoadError={() => {
                      if (key === currentTarget.current) setFailedKey(key);
                    }}
                    loading={<p role="status">PDF-Seite wird geladen …</p>}
                    error={<p role="alert">PDF-Seite konnte nicht dargestellt werden.</p>}
                  />
                </div>
              );
            })}
          </div>
        )}
        {previous && failedKey === targetKey && (
          <p role="alert">PDF-Seite konnte nicht dargestellt werden.</p>
        )}
      </Document>
    </div>
  );
}
