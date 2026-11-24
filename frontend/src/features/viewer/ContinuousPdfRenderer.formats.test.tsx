import { render, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
vi.mock('react-pdf', async () => {
  const React = await import('react');
  return {
    pdfjs: { GlobalWorkerOptions: {} },
    Document: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
    Page: ({
      pageNumber,
      onLoadSuccess,
    }: {
      pageNumber: number;
      onLoadSuccess: (page: { originalWidth: number; originalHeight: number }) => void;
    }) => {
      React.useEffect(() => {
        onLoadSuccess(
          pageNumber === 1
            ? { originalWidth: 800, originalHeight: 1000 }
            : { originalWidth: 1200, originalHeight: 600 },
        );
      }, [pageNumber, onLoadSuccess]);
      return <div />;
    },
  };
});
import { ContinuousPdfRenderer } from './ContinuousPdfRenderer';
it('berücksichtigt unterschiedliche Seitenverhältnisse ohne die Dokumentquelle zu wechseln', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(500);
  const blob = new Blob(['pdf']);
  const { container } = render(
    <div>
      <div />
      <div>
        <ContinuousPdfRenderer
          blob={blob}
          page={1}
          pages={2}
          zoom={100}
          navigation={0}
          onLoaded={() => {}}
          onVisiblePage={() => {}}
        />
      </div>
    </div>,
  );
  const first = container.querySelector<HTMLElement>('[data-pdf-page="1"]')!;
  const second = container.querySelector<HTMLElement>('[data-pdf-page="2"]')!;
  await waitFor(() => expect(first.style.minHeight).not.toBe(second.style.minHeight));
  expect(first.style.width).toBe(second.style.width);
  vi.restoreAllMocks();
});
