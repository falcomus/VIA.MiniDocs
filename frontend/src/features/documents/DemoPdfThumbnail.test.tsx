import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { DemoPdfThumbnail } from './DemoPdfThumbnail';

vi.mock('./PdfThumbnail', () => ({ PdfThumbnail: () => <img alt="PDF-Vorschau, erste Seite" /> }));
afterEach(() => vi.unstubAllGlobals());

it('lädt eine PDF-Quelle und zeigt ihre Miniatur', async () => {
  const fetcher = vi.fn().mockResolvedValue({ ok: true, blob: async () => new Blob(['pdf']) });
  vi.stubGlobal('fetch', fetcher);
  render(<DemoPdfThumbnail url="/demo/erste.pdf" />);
  expect(screen.getByText('Vorschau wird geladen …')).toBeInTheDocument();
  expect(await screen.findByAltText('PDF-Vorschau, erste Seite')).toBeInTheDocument();
  expect(fetcher).toHaveBeenCalledTimes(1);
});

it('zeigt bei einem Ladefehler einen neutralen Fallback', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));
  render(<DemoPdfThumbnail url="/demo/fehler.pdf" />);
  await waitFor(() => expect(screen.getByText('Vorschau nicht verfügbar')).toBeInTheDocument());
});

it('lädt erst bei Sichtbarkeit und verwendet die Quelle nach erneutem Einhängen aus dem Cache', async () => {
  let notify!: IntersectionObserverCallback;
  const disconnect = vi.fn();
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      constructor(callback: IntersectionObserverCallback) {
        notify = callback;
      }
      observe() {}
      disconnect() {
        disconnect();
      }
    },
  );
  const fetcher = vi.fn().mockResolvedValue({ ok: true, blob: async () => new Blob(['pdf']) });
  vi.stubGlobal('fetch', fetcher);
  const view = render(<DemoPdfThumbnail url="/demo/cache-regression.pdf" />);
  expect(fetcher).not.toHaveBeenCalled();
  notify([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
  expect(await screen.findByAltText('PDF-Vorschau, erste Seite')).toBeInTheDocument();
  view.unmount();
  render(<DemoPdfThumbnail url="/demo/cache-regression.pdf" />);
  notify([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
  expect(await screen.findByAltText('PDF-Vorschau, erste Seite')).toBeInTheDocument();
  expect(fetcher).toHaveBeenCalledTimes(1);
  expect(disconnect).toHaveBeenCalled();
});

it('bricht einen nicht mehr benötigten Abruf beim Entfernen ab', () => {
  const fetcher = vi.fn().mockImplementation((_url: string, options: RequestInit) => {
    return new Promise((_resolve, reject) =>
      options.signal!.addEventListener('abort', () => reject(new Error('abort'))),
    );
  });
  vi.stubGlobal('fetch', fetcher);
  const view = render(<DemoPdfThumbnail url="/demo/abbruch-regression.pdf" />);
  const signal = fetcher.mock.calls[0][1].signal as AbortSignal;
  view.unmount();
  expect(signal.aborted).toBe(true);
});
