import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PdfRenderer } from './PdfRenderer';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});
it('passt Hochformat in Breite und Höhe ein und gibt den Größenbeobachter frei', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(500);
  const disconnect = vi.fn();
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {}
      disconnect = disconnect;
    },
  );
  const loaded = vi.fn();
  const blob = new Blob(['PDF'], { type: 'application/pdf' });
  const { unmount } = render(<PdfRenderer blob={blob} page={1} zoom={100} onLoaded={loaded} />);
  expect(await screen.findByRole('img', { name: 'PDF-Seite 1' })).toHaveAttribute(
    'data-width',
    '400',
  );
  expect(loaded).toHaveBeenCalledWith(blob, 7);
  unmount();
  expect(disconnect).toHaveBeenCalledOnce();
});
it('verwirft ein asynchrones Ladeergebnis nach dem Unmount', async () => {
  const loaded = vi.fn();
  const blob = new Blob(['PDF'], { type: 'application/pdf' });
  const { unmount } = render(<PdfRenderer blob={blob} page={1} zoom={100} onLoaded={loaded} />);
  unmount();
  await act(async () => {
    await Promise.resolve();
  });
  expect(loaded).not.toHaveBeenCalled();
});
it('hält die Einpassbreite bei Zoomwechseln unabhängig von der Inhaltshöhe stabil', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (
    this: HTMLElement,
  ) {
    return (this as HTMLElement).dataset.testid === 'scroll-container' ? 500 : 80;
  });
  const blob = new Blob(['PDF'], { type: 'application/pdf' });
  const loaded = vi.fn();
  const view = (zoom: number) => (
    <div data-testid="scroll-container">
      <div />
      <div>
        <PdfRenderer blob={blob} page={1} zoom={zoom} onLoaded={loaded} />
      </div>
    </div>
  );
  const { rerender } = render(view(50));
  expect(await screen.findByRole('img', { name: 'PDF-Seite 1' })).toHaveAttribute(
    'data-width',
    '336',
  );
  for (const zoom of [70, 100, 110, 200]) {
    rerender(view(zoom));
    expect(screen.getByRole('img', { name: 'PDF-Seite 1' })).toHaveAttribute('data-width', '336');
    expect(screen.getByRole('img', { name: 'PDF-Seite 1' })).toHaveAttribute(
      'data-scale',
      String(zoom / 100),
    );
  }
});
