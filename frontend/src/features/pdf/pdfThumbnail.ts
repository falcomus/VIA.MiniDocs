import { openPdf } from './pdfDocument';

const cache = new WeakMap<Blob, string>();
const queue: Array<() => void> = [];
let running = 0;
const maximumConcurrent = 2;

function schedule(job: () => Promise<void>) {
  queue.push(() => {
    running++;
    void job().finally(() => {
      running--;
      queue.shift()?.();
    });
  });
  if (running < maximumConcurrent) queue.shift()?.();
}

/** Generates a bounded first-page preview; cleanup cancels queued or active work. */
export function requestPdfThumbnail(
  blob: Blob,
  complete: (result: string | null) => void,
): () => void {
  const cached = cache.get(blob);
  if (cached) {
    complete(cached);
    return () => {};
  }
  let cancelled = false;
  let cancelRender: (() => void) | undefined;
  schedule(async () => {
    if (cancelled) return;
    let task: Awaited<ReturnType<typeof openPdf>> | undefined;
    try {
      task = await openPdf(blob);
      if (cancelled) return;
      const document = await task.promise;
      if (cancelled) return;
      const page = await document.getPage(1);
      if (cancelled) return;
      const original = page.getViewport({ scale: 1 });
      const scale = Math.min(320 / original.width, 452 / original.height);
      const viewport = page.getViewport({ scale });
      const canvas = window.document.createElement('canvas');
      canvas.width = Math.ceil(viewport.width);
      canvas.height = Math.ceil(viewport.height);
      const context = canvas.getContext('2d');
      if (!context) throw new Error('Keine Zeichenfläche verfügbar');
      const render = page.render({ canvasContext: context, canvas, viewport });
      cancelRender = () => render.cancel();
      await render.promise;
      page.cleanup();
      if (cancelled) return;
      const result = canvas.toDataURL('image/png');
      cache.set(blob, result);
      complete(result);
    } catch {
      if (!cancelled) complete(null);
    } finally {
      if (task) await task.destroy().catch(() => {});
    }
  });
  return () => {
    cancelled = true;
    cancelRender?.();
  };
}
