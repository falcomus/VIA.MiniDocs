import { afterEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ openPdf: vi.fn() }));
vi.mock('./pdfDocument', () => mocks);
import { requestPdfThumbnail } from './pdfThumbnail';

afterEach(() => {
  vi.restoreAllMocks();
  mocks.openPdf.mockReset();
});
it('rendert die erste Seite begrenzt, räumt auf und nutzt den Cache', async () => {
  const destroy = vi.fn().mockResolvedValue(undefined);
  const cleanup = vi.fn();
  const getPage = vi.fn().mockResolvedValue({
    getViewport: ({ scale }: { scale: number }) => ({ width: 600 * scale, height: 800 * scale }),
    render: vi.fn().mockReturnValue({ promise: Promise.resolve(), cancel: vi.fn() }),
    cleanup,
  });
  mocks.openPdf.mockResolvedValue({ promise: Promise.resolve({ getPage }), destroy });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    {} as CanvasRenderingContext2D,
  );
  vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,abc');
  const blob = new Blob(['1']);
  const done = vi.fn();
  requestPdfThumbnail(blob, done);
  await vi.waitFor(() => expect(done).toHaveBeenCalledWith('data:image/png;base64,abc'));
  await vi.waitFor(() => expect(destroy).toHaveBeenCalledOnce());
  expect(getPage).toHaveBeenCalledWith(1);
  expect(cleanup).toHaveBeenCalledOnce();
  requestPdfThumbnail(blob, done);
  expect(mocks.openPdf).toHaveBeenCalledOnce();
});
it('meldet Parserfehler und unterdrückt Rückmeldungen nach Abbruch', async () => {
  const destroy = vi.fn().mockResolvedValue(undefined);
  mocks.openPdf.mockResolvedValue({ promise: Promise.reject(new Error('kaputt')), destroy });
  const done = vi.fn();
  requestPdfThumbnail(new Blob(['broken']), done);
  await vi.waitFor(() => expect(done).toHaveBeenCalledWith(null));
  expect(destroy).toHaveBeenCalledOnce();
  let resolve!: (value: unknown) => void;
  mocks.openPdf.mockReturnValue(
    new Promise((done) => {
      resolve = done;
    }),
  );
  const cancelled = vi.fn();
  const stop = requestPdfThumbnail(new Blob(['late']), cancelled);
  await vi.waitFor(() => expect(resolve).toBeTypeOf('function'));
  stop();
  resolve({ promise: Promise.resolve({}), destroy });
  await vi.waitFor(() => expect(destroy).toHaveBeenCalledTimes(2));
  expect(cancelled).not.toHaveBeenCalled();
});

it('begrenzt mehrere sichtbare Vorschauen auf zwei parallele Renderaufgaben', async () => {
  const completed: Array<() => void> = [];
  let active = 0;
  let peak = 0;
  const cleanup = vi.fn();
  const destroy = vi.fn().mockImplementation(async () => {
    active--;
  });
  mocks.openPdf.mockImplementation(async () => {
    active++;
    peak = Math.max(peak, active);
    return {
      promise: Promise.resolve({
        getPage: async () => ({
          getViewport: ({ scale }: { scale: number }) => ({
            width: 600 * scale,
            height: 800 * scale,
          }),
          render: () => ({
            promise: new Promise<void>((resolve) => completed.push(resolve)),
            cancel: vi.fn(),
          }),
          cleanup,
        }),
      }),
      destroy,
    };
  });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    {} as CanvasRenderingContext2D,
  );
  vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue('data:image/png;base64,test');
  const done = vi.fn();
  const cancel = Array.from({ length: 5 }, (_, index) =>
    requestPdfThumbnail(new Blob([String(index)]), done),
  );
  await vi.waitFor(() => expect(completed).toHaveLength(2));
  expect(mocks.openPdf).toHaveBeenCalledTimes(2);
  cancel[4]();
  completed[0]();
  await vi.waitFor(() => expect(completed).toHaveLength(3));
  completed[1]();
  await vi.waitFor(() => expect(completed).toHaveLength(4));
  completed[2]();
  completed[3]();
  await vi.waitFor(() => expect(destroy).toHaveBeenCalledTimes(4));
  expect(mocks.openPdf).toHaveBeenCalledTimes(4);
  expect(done).toHaveBeenCalledTimes(4);
  expect(peak).toBe(2);
  expect(active).toBe(0);
});

it('bricht eine laufende Renderaufgabe ab und gibt die PDF-Aufgabe frei', async () => {
  let rejectRender!: (error: Error) => void;
  const destroy = vi.fn().mockResolvedValue(undefined);
  const cancelRender = vi.fn(() => rejectRender(new Error('abgebrochen')));
  mocks.openPdf.mockResolvedValue({
    promise: Promise.resolve({
      getPage: async () => ({
        getViewport: () => ({ width: 100, height: 100 }),
        render: () => ({
          promise: new Promise<void>((_resolve, reject) => {
            rejectRender = reject;
          }),
          cancel: cancelRender,
        }),
        cleanup: vi.fn(),
      }),
    }),
    destroy,
  });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    {} as CanvasRenderingContext2D,
  );
  const done = vi.fn();
  const stop = requestPdfThumbnail(new Blob(['cancel-active']), done);
  await vi.waitFor(() => expect(rejectRender).toBeTypeOf('function'));
  stop();
  await vi.waitFor(() => expect(destroy).toHaveBeenCalledOnce());
  expect(cancelRender).toHaveBeenCalledOnce();
  expect(done).not.toHaveBeenCalled();
});
