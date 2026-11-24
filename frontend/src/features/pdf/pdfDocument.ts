import { pdfjs } from 'react-pdf';

// The viewer and import use the same locked PDF.js worker through Vite.
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

/** The caller must destroy the loading task even when parsing or rendering fails. */
export async function openPdf(blob: Blob) {
  const data = new Uint8Array(await blob.arrayBuffer());
  return pdfjs.getDocument({ data });
}

export async function readPdfPageCount(blob: Blob): Promise<number> {
  const task = await openPdf(blob);
  try {
    const document = await task.promise;
    if (!Number.isSafeInteger(document.numPages) || document.numPages < 1)
      throw new Error('PDF enthält keine lesbaren Seiten');
    return document.numPages;
  } finally {
    await task.destroy();
  }
}
