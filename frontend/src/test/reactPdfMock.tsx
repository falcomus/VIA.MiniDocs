import { createElement, useEffect, useRef } from 'react';
import type { ReactNode } from 'react';

/** jsdom has no PDF canvas; real rendering is covered separately in a browser. */
// The mock mirrors react-pdf's mixed component and worker exports; it is never hot-reloaded.
// eslint-disable-next-line react-refresh/only-export-components
export const pdfjs = { GlobalWorkerOptions: { workerSrc: '' } };
export function Document({
  file,
  onLoadSuccess,
  children,
  error,
}: {
  file: Blob;
  onLoadSuccess: (pdf: { numPages: number }) => void;
  children: ReactNode;
  error: ReactNode;
}) {
  const callback = useRef(onLoadSuccess);
  useEffect(() => {
    callback.current = onLoadSuccess;
  }, [onLoadSuccess]);
  useEffect(() => {
    let active = true;
    void Promise.resolve().then(() => {
      if (active && file.type === 'application/pdf') callback.current({ numPages: 7 });
    });
    return () => {
      active = false;
    };
  }, [file]);
  return createElement(
    'div',
    { role: 'region', 'aria-label': 'PDF-Darstellung' },
    file.type === 'application/pdf' ? children : error,
  );
}
export function Page({
  pageNumber,
  width,
  scale,
  onLoadSuccess,
  onRenderSuccess,
}: {
  pageNumber: number;
  width: number;
  scale: number;
  onLoadSuccess: (page: { originalWidth: number; originalHeight: number }) => void;
  onRenderSuccess?: () => void;
}) {
  const callback = useRef(onLoadSuccess);
  useEffect(() => {
    callback.current = onLoadSuccess;
  }, [onLoadSuccess]);
  useEffect(() => {
    callback.current({ originalWidth: 800, originalHeight: 1000 });
  }, [pageNumber]);
  useEffect(() => {
    onRenderSuccess?.();
  }, [pageNumber, width, scale, onRenderSuccess]);
  return createElement('div', {
    role: 'img',
    'aria-label': `PDF-Seite ${pageNumber}`,
    'data-width': width,
    'data-scale': scale,
  });
}
