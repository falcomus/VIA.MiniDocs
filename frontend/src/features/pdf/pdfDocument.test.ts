import { expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ getDocument: vi.fn(), destroy: vi.fn() }));
vi.mock('react-pdf', () => ({
  pdfjs: { GlobalWorkerOptions: {}, getDocument: mocks.getDocument },
}));
import { readPdfPageCount } from './pdfDocument';

it('übernimmt die echte Parser-Seitenzahl und gibt die Ladeaufgabe frei', async () => {
  mocks.destroy.mockResolvedValue(undefined);
  mocks.getDocument.mockReturnValue({
    promise: Promise.resolve({ numPages: 7 }),
    destroy: mocks.destroy,
  });
  const blob = { arrayBuffer: async () => new Uint8Array([1]).buffer } as Blob;
  await expect(readPdfPageCount(blob)).resolves.toBe(7);
  expect(mocks.destroy).toHaveBeenCalledOnce();
});
it('gibt die Ladeaufgabe auch bei beschädigtem oder verschlüsseltem PDF frei', async () => {
  mocks.destroy.mockClear();
  mocks.getDocument.mockReturnValue({
    promise: Promise.reject(new Error('PasswordException')),
    destroy: mocks.destroy,
  });
  const blob = { arrayBuffer: async () => new Uint8Array([1]).buffer } as Blob;
  await expect(readPdfPageCount(blob)).rejects.toThrow('PasswordException');
  expect(mocks.destroy).toHaveBeenCalledOnce();
});
