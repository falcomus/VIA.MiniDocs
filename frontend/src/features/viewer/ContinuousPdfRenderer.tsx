import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Document, Page } from 'react-pdf';
import '../pdf/pdfDocument';
import 'react-pdf/dist/Page/TextLayer.css';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import styles from './PdfRenderer.module.css';

type Size = { width: number; zoom: number };
const sizeKey = (size: Size) => `${size.width}:${size.zoom}`;

/** Each nearby page keeps its last finished canvas until its replacement is ready. */
function BufferedPage({
  number,
  width,
  zoom,
  onRatio,
}: {
  readonly number: number;
  readonly width: number;
  readonly zoom: number;
  readonly onRatio: (number: number, ratio: number) => void;
}) {
  const target = { width, zoom };
  const key = sizeKey(target);
  const targetKey = useRef(key);
  const [ready, setReady] = useState<Size | null>(null);
  const [failed, setFailed] = useState(false);
  useLayoutEffect(() => {
    targetKey.current = key;
  }, [key]);
  const previous = ready && sizeKey(ready) !== key ? ready : null;
  return (
    <div className={styles.layers}>
      {[...(previous ? [previous] : []), target].map((item) => {
        const itemKey = sizeKey(item);
        const pending = !!previous && itemKey === key;
        return (
          <div
            key={itemKey}
            className={pending ? styles.pending : undefined}
            aria-hidden={pending || undefined}
          >
            <Page
              pageNumber={number}
              width={item.width}
              scale={item.zoom / 100}
              onLoadSuccess={({ originalWidth, originalHeight }) => {
                if (originalWidth > 0 && originalHeight > 0)
                  onRatio(number, originalWidth / originalHeight);
              }}
              onRenderSuccess={() => {
                if (targetKey.current === itemKey) {
                  setReady((value) => (value && sizeKey(value) === itemKey ? value : item));
                  setFailed(false);
                }
              }}
              onRenderError={() => {
                if (targetKey.current === itemKey) setFailed(true);
              }}
              onLoadError={() => {
                if (targetKey.current === itemKey) setFailed(true);
              }}
              loading={<p>PDF-Seite wird geladen …</p>}
              error={<p role="alert">PDF-Seite konnte nicht dargestellt werden.</p>}
            />
          </div>
        );
      })}
      {previous && failed && <p role="alert">PDF-Seite konnte nicht dargestellt werden.</p>}
    </div>
  );
}

export function ContinuousPdfRenderer({
  blob,
  page,
  pages,
  zoom,
  navigation,
  onLoaded,
  onVisiblePage,
}: {
  readonly blob: Blob;
  readonly page: number;
  readonly pages: number;
  readonly zoom: number;
  readonly navigation: number;
  readonly onLoaded: (blob: Blob, pages: number) => void;
  readonly onVisiblePage: (page: number) => void;
}) {
  const pane = useRef<HTMLDivElement>(null);
  const scroll = useRef<HTMLElement | null>(null);
  const activePage = useRef(page);
  const onVisible = useRef(onVisiblePage);
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [count, setCount] = useState(pages);
  const [documentReady, setDocumentReady] = useState(false);
  const [ratios, setRatios] = useState<Record<number, number>>({});
  useLayoutEffect(() => {
    activePage.current = page;
    onVisible.current = onVisiblePage;
  }, [page, onVisiblePage]);
  useEffect(() => {
    const viewport = pane.current?.parentElement;
    const container = viewport?.parentElement;
    if (!viewport || !container) return;
    scroll.current = container;
    const toolbar = viewport.previousElementSibling;
    const measure = () => {
      const css = getComputedStyle(viewport);
      const horizontal = (parseFloat(css.paddingLeft) || 0) + (parseFloat(css.paddingRight) || 0);
      const vertical = (parseFloat(css.paddingTop) || 0) + (parseFloat(css.paddingBottom) || 0);
      const width = Math.max(0, Math.floor(container.clientWidth - horizontal));
      const height = Math.max(
        0,
        Math.floor(container.clientHeight - (toolbar?.clientHeight ?? 0) - vertical),
      );
      setSize((old) => (old.width === width && old.height === height ? old : { width, height }));
    };
    measure();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure);
      return () => {
        scroll.current = null;
        window.removeEventListener('resize', measure);
      };
    }
    const observer = new ResizeObserver(measure);
    observer.observe(container);
    if (toolbar) observer.observe(toolbar);
    return () => {
      scroll.current = null;
      observer.disconnect();
    };
  }, []);
  useEffect(() => {
    const container = scroll.current;
    if (!container) return;
    let frame = 0;
    const update = () => {
      frame = 0;
      const bounds = container.getBoundingClientRect();
      const toolbar = pane.current?.parentElement?.previousElementSibling as HTMLElement | null;
      const visibleTop = Math.min(bounds.bottom, bounds.top + (toolbar?.clientHeight ?? 0));
      let best = 0,
        selected = activePage.current;
      const elements = pane.current?.querySelectorAll<HTMLElement>('[data-pdf-page]');
      if (!elements?.length) return;
      // Ordered placeholders let long documents locate the visible range without measuring every page.
      let low = 0,
        high = elements.length;
      while (low < high) {
        const middle = Math.floor((low + high) / 2);
        if (elements[middle].getBoundingClientRect().bottom <= visibleTop) low = middle + 1;
        else high = middle;
      }
      for (let index = low; index < elements.length; index++) {
        const element = elements[index];
        const rect = element.getBoundingClientRect();
        if (rect.top >= bounds.bottom) break;
        const visible = Math.max(
          0,
          Math.min(rect.bottom, bounds.bottom) - Math.max(rect.top, visibleTop),
        );
        if (
          visible > best + 1 ||
          (visible > 0 &&
            Math.abs(visible - best) <= 1 &&
            Number(element.dataset.pdfPage) === activePage.current)
        ) {
          best = visible;
          selected = Number(element.dataset.pdfPage);
        }
      }
      if (best > 0 && selected !== activePage.current) {
        activePage.current = selected;
        onVisible.current(selected);
      }
    };
    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };
    container.addEventListener('scroll', onScroll, { passive: true });
    return () => {
      container.removeEventListener('scroll', onScroll);
      if (frame) cancelAnimationFrame(frame);
    };
  }, []);
  useLayoutEffect(() => {
    const container = scroll.current;
    const target = pane.current?.querySelector<HTMLElement>(
      `[data-pdf-page="${activePage.current}"]`,
    );
    if (!container || !target) return;
    const offset = target.getBoundingClientRect().top - container.getBoundingClientRect().top;
    const toolbar = pane.current?.parentElement?.previousElementSibling as HTMLElement | null;
    container.scrollTop += offset - (toolbar?.clientHeight ?? 0);
  }, [navigation, zoom, size.width, count, documentReady]);
  const width = size.height > 0 ? Math.min(size.width, Math.floor(size.height * 0.8)) : size.width;
  const fallbackRatio = ratios[1] ?? 0.8;
  return (
    <div ref={pane} className={styles.pane} aria-label="PDF-Dokument">
      <Document
        file={blob}
        loading={<p role="status">PDF wird geladen …</p>}
        error={<p role="alert">PDF konnte nicht geladen werden.</p>}
        onLoadSuccess={({ numPages }) => {
          setCount(numPages);
          setDocumentReady(true);
          onLoaded(blob, numPages);
        }}
      >
        {width > 0 &&
          Array.from({ length: count }, (_, index) => {
            const number = index + 1;
            const ratio = ratios[number] ?? fallbackRatio;
            return (
              <div
                key={number}
                data-pdf-page={number}
                className={styles.continuousPage}
                style={{ width: (width * zoom) / 100, minHeight: (width * zoom) / 100 / ratio }}
              >
                {Math.abs(number - page) <= 2 ? (
                  <BufferedPage
                    number={number}
                    width={width}
                    zoom={zoom}
                    onRatio={(id, value) =>
                      setRatios((old) => (old[id] === value ? old : { ...old, [id]: value }))
                    }
                  />
                ) : (
                  <span className={styles.placeholder}>
                    Seite {number} wird bei Bedarf geladen.
                  </span>
                )}
              </div>
            );
          })}
      </Document>
    </div>
  );
}
