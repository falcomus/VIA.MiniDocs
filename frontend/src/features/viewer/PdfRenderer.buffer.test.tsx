import { act, render, screen, within } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({
  callbacks: new Map<string, () => void>(),
  mounted: vi.fn(),
  unmounted: vi.fn(),
}));
vi.mock('react-pdf', async () => {
  const React = await import('react');
  return {
    pdfjs: { GlobalWorkerOptions: {} },
    Document: ({ children }: { children: React.ReactNode }) => {
      React.useEffect(() => {
        mocks.mounted();
        return () => {
          mocks.unmounted();
        };
      }, []);
      return <div data-testid="document">{children}</div>;
    },
    Page: ({
      pageNumber,
      scale,
      onRenderSuccess,
    }: {
      pageNumber: number;
      scale: number;
      onRenderSuccess: () => void;
    }) => {
      const key = `${pageNumber}:${scale}`;
      mocks.callbacks.set(key, onRenderSuccess);
      return <div data-testid={`page-${key}`}>{key}</div>;
    },
  };
});
import { PdfRenderer } from './PdfRenderer';
it('behält die letzte fertige PDF-Seite bei Zoom- und Seitenwechsel sichtbar bis zum neuen Renderabschluss', () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(600);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(400);
  const blob = new Blob(['pdf']);
  const loaded = vi.fn();
  const { rerender } = render(<PdfRenderer blob={blob} page={1} zoom={100} onLoaded={loaded} />);
  act(() => mocks.callbacks.get('1:1')?.());
  expect(screen.getByTestId('page-1:1')).toBeVisible();
  rerender(<PdfRenderer blob={blob} page={1} zoom={70} onLoaded={loaded} />);
  expect(screen.getByTestId('page-1:1')).toBeVisible();
  expect(screen.getByTestId('page-1:0.7').parentElement).toHaveAttribute('aria-hidden', 'true');
  act(() => mocks.callbacks.get('1:0.7')?.());
  expect(screen.queryByTestId('page-1:1')).not.toBeInTheDocument();
  expect(within(screen.getByTestId('document')).getByTestId('page-1:0.7')).toBeVisible();
  rerender(<PdfRenderer blob={blob} page={2} zoom={200} onLoaded={loaded} />);
  expect(screen.getByTestId('page-1:0.7')).toBeVisible();
  act(() => mocks.callbacks.get('1:1')?.());
  expect(screen.getByTestId('page-1:0.7')).toBeVisible();
  act(() => mocks.callbacks.get('2:2')?.());
  expect(screen.getByTestId('page-2:2')).toBeVisible();
  expect(mocks.mounted).toHaveBeenCalledOnce();
  expect(mocks.unmounted).not.toHaveBeenCalled();
});

it('hält die fertige Seite bei wiederholtem Resize sichtbar und verwendet die jüngste Größe', () => {
  let width = 600;
  let notify!: ResizeObserverCallback;
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockImplementation(() => width);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(400);
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(callback: ResizeObserverCallback) {
        notify = callback;
      }
      observe() {}
      disconnect() {}
    },
  );
  const blob = new Blob(['resize']);
  const { unmount } = render(<PdfRenderer blob={blob} page={1} zoom={70} onLoaded={() => {}} />);
  act(() => mocks.callbacks.get('1:0.7')?.());
  for (const nextWidth of [550, 480, 510]) {
    width = nextWidth;
    act(() => notify([], {} as ResizeObserver));
    const pages = screen.getAllByTestId('page-1:0.7');
    expect(pages).toHaveLength(2);
    expect(pages[0]).toBeVisible();
    expect(pages[1].parentElement).toHaveAttribute('aria-hidden', 'true');
  }
  act(() => mocks.callbacks.get('1:0.7')?.());
  expect(screen.getAllByTestId('page-1:0.7')).toHaveLength(1);
  unmount();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});
