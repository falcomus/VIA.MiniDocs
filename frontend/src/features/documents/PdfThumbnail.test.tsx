import { render, screen, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock('../pdf/pdfThumbnail', () => ({ requestPdfThumbnail: mocks.request }));
import { PdfThumbnail } from './PdfThumbnail';
it('behält Vorschauen bei Rerenders und räumt bei Quellenwechsel auf', async () => {
  const cancelFirst = vi.fn();
  const cancelSecond = vi.fn();
  mocks.request.mockReset().mockReturnValueOnce(cancelFirst).mockReturnValueOnce(cancelSecond);
  const first = new Blob(['first']);
  const second = new Blob(['second']);
  const { rerender, unmount } = render(<PdfThumbnail blob={first} />);
  await waitFor(() => expect(mocks.request).toHaveBeenCalledTimes(1));
  rerender(<PdfThumbnail blob={first} />);
  expect(mocks.request).toHaveBeenCalledTimes(1);
  rerender(<PdfThumbnail blob={second} />);
  expect(cancelFirst).toHaveBeenCalledOnce();
  expect(mocks.request).toHaveBeenCalledTimes(2);
  unmount();
  expect(cancelSecond).toHaveBeenCalledOnce();
});
it('zeigt den Fehlerzustand bei fehlgeschlagener Vorschau', async () => {
  mocks.request.mockReset().mockImplementation((_blob, complete: (value: null) => void) => {
    queueMicrotask(() => complete(null));
    return () => {};
  });
  render(<PdfThumbnail blob={new Blob(['bad'])} />);
  expect(await screen.findByText('Vorschau nicht verfügbar')).toBeInTheDocument();
});
