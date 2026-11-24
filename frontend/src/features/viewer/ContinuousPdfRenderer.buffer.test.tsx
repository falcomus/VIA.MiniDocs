import { act, render, screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
const state = vi.hoisted(() => ({ callbacks: new Map<number, () => void>(), mounted: vi.fn() }));
vi.mock('react-pdf', async () => {
  const React = await import('react');
  return {
    pdfjs: { GlobalWorkerOptions: {} },
    Document: ({ children }: { children: React.ReactNode }) => {
      React.useEffect(() => {
        state.mounted();
      }, []);
      return <div>{children}</div>;
    },
    Page: ({ scale, onRenderSuccess }: { scale: number; onRenderSuccess: () => void }) => {
      state.callbacks.set(scale, onRenderSuccess);
      return <div data-testid={`scale-${scale}`} />;
    },
  };
});
import { ContinuousPdfRenderer } from './ContinuousPdfRenderer';
it('behält den fertigen Canvas beim Zoom bis zum neuen Renderabschluss sichtbar', () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(600);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(400);
  const blob = new Blob(['pdf']);
  const view = (zoom: number) => (
    <div>
      <div />
      <div>
        <ContinuousPdfRenderer
          blob={blob}
          page={1}
          pages={1}
          zoom={zoom}
          navigation={0}
          onLoaded={() => {}}
          onVisiblePage={() => {}}
        />
      </div>
    </div>
  );
  const { rerender } = render(view(100));
  act(() => state.callbacks.get(1)?.());
  rerender(view(70));
  expect(screen.getByTestId('scale-1')).toBeVisible();
  expect(screen.getByTestId('scale-0.7').parentElement).toHaveAttribute('aria-hidden', 'true');
  act(() => state.callbacks.get(0.7)?.());
  expect(screen.queryByTestId('scale-1')).not.toBeInTheDocument();
  expect(screen.getByTestId('scale-0.7')).toBeVisible();
  expect(state.mounted).toHaveBeenCalledOnce();
  vi.restoreAllMocks();
});
