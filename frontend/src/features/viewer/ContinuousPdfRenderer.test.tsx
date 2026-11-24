import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { ContinuousPdfRenderer } from './ContinuousPdfRenderer';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

it('hält Seitenpositionen vor, erkennt die sichtbare Seite und begrenzt gerenderte Seiten', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (
    this: HTMLElement,
  ) {
    return this.dataset.testid === 'pdf-toolbar' ? 0 : 500;
  });
  const rectangle = (top: number, bottom: number): DOMRect => ({
    top,
    bottom,
    left: 0,
    right: 640,
    width: 640,
    height: bottom - top,
    x: 0,
    y: top,
    toJSON: () => ({}),
  });
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement,
  ) {
    const container = document.querySelector<HTMLElement>('[data-testid="pdf-scroll"]');
    const number = Number(this.dataset.pdfPage);
    return number
      ? rectangle(
          (number - 1) * 500 - (container?.scrollTop ?? 0),
          number * 500 - (container?.scrollTop ?? 0),
        )
      : rectangle(0, 500);
  });
  const loaded = vi.fn();
  const blob = new Blob(['pdf'], { type: 'application/pdf' });
  function TestViewer() {
    const [page, setPage] = useState(1);
    const [navigation, setNavigation] = useState(0);
    const [zoom, setZoom] = useState(100);
    return (
      <>
        <output aria-label="Aktive Seite">{page}</output>
        <button
          onClick={() => {
            setPage(5);
            setNavigation((value) => value + 1);
          }}
        >
          Seite fünf
        </button>
        <button onClick={() => setZoom(70)}>Zoom</button>
        <div data-testid="pdf-scroll">
          <div data-testid="pdf-toolbar" />
          <div>
            <ContinuousPdfRenderer
              blob={blob}
              pages={7}
              page={page}
              zoom={zoom}
              navigation={navigation}
              onLoaded={loaded}
              onVisiblePage={setPage}
            />
          </div>
        </div>
      </>
    );
  }
  render(<TestViewer />);
  const scroll = screen.getByTestId('pdf-scroll');
  await waitFor(() => expect(scroll.querySelectorAll('[data-pdf-page]')).toHaveLength(7));
  expect(within(scroll).getAllByRole('img').length).toBeLessThanOrEqual(5);
  scroll.scrollTop = 650;
  fireEvent.scroll(scroll);
  await waitFor(() => expect(screen.getByLabelText('Aktive Seite')).toHaveTextContent('2'));
  expect(within(scroll).getByRole('img', { name: 'PDF-Seite 2' })).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Seite fünf' }));
  expect(screen.getByLabelText('Aktive Seite')).toHaveTextContent('5');
  expect(scroll.scrollTop).toBe(2000);
  expect(within(scroll).getAllByRole('img').length).toBeLessThanOrEqual(5);
  fireEvent.click(screen.getByRole('button', { name: 'Zoom' }));
  expect(screen.getByLabelText('Aktive Seite')).toHaveTextContent('5');
  expect(loaded).toHaveBeenCalledTimes(1);
});
