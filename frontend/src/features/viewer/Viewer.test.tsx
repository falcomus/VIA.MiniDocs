import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { demoDocuments } from '../documents/demoDocuments';
import { startPage, viewerUrl } from './viewerModel';
import { StandaloneViewer } from './StandaloneViewer';

const row = (id: string) => document.querySelector<HTMLElement>(`[data-document-id="${id}"]`)!;
const viewer = () =>
  within(screen.getByRole('dialog', { name: /Besprechungsnotizen|Offene Punkte|Lenkungskreis/ }));
/** Checks actual navigation and matching transcript/SVG page, not removed HTML helper text. */
function expectDemoPage(page: number, count: number) {
  const pageSelect = screen.getByRole('combobox', { name: /^Seite/ });
  expect(pageSelect).toHaveValue(String(page));
  expect(within(pageSelect).getAllByRole('option')).toHaveLength(count);
  expect(screen.getByRole('heading', { name: `Transkript · Seite ${page}` })).toBeInTheDocument();
  expect(screen.getByRole('img', { name: 'Synthetische Demoseite – kein PDF' })).toHaveTextContent(
    `S. ${page}`,
  );
}
beforeEach(() => {
  localStorage.removeItem('via.minidocs.opening-mode');
  window.history.replaceState({}, '', '/');
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  localStorage.removeItem('via.minidocs.opening-mode');
  window.history.replaceState({}, '', '/');
});
it.each(['Kachelansicht', 'Listenansicht'])(
  'öffnet aus %s per Doppelklick mit Metadaten und Seitengrenzen',
  (view) => {
    render(<App />);
    if (view === 'Listenansicht') fireEvent.click(screen.getByRole('button', { name: view }));
    row('demo-1').focus();
    fireEvent.doubleClick(row('demo-1'));
    expect(viewer().getByText(/Demo-Notizen-1.pdf/)).toBeInTheDocument();
    expect(
      viewer().getByRole('img', { name: 'Synthetische Demoseite – kein PDF' }),
    ).toBeInTheDocument();
    expect(viewer().getByRole('combobox', { name: /Seite/ })).toHaveValue('1');
    expect(viewer().getByRole('button', { name: 'Vorherige Seite' })).toBeDisabled();
    fireEvent.click(viewer().getByRole('button', { name: 'Nächste Seite' }));
    expectDemoPage(2, 2);
    expect(viewer().getByRole('button', { name: 'Nächste Seite' })).toBeDisabled();
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    expect(row('demo-1')).toHaveFocus();
  },
);
it('erhält Einzel-/Mehrfachauswahl und öffnet die Auswahl über die Aktionsleiste', () => {
  render(<App />);
  fireEvent.click(row('demo-1'));
  expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
  fireEvent.click(row('demo-2'), { ctrlKey: true });
  expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
  fireEvent.click(row('demo-1'));
  fireEvent.click(screen.getByRole('button', { name: 'Öffnen' }));
  expectDemoPage(1, 2);
  fireEvent.click(viewer().getByRole('button', { name: 'Schließen' }));
  expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
});
it('öffnet per Enter, Leertaste bleibt Auswahl, ohne Transkript sind Seiten bedienbar', () => {
  render(<App />);
  row('demo-2').focus();
  fireEvent.keyDown(row('demo-2'), { key: ' ' });
  expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
  fireEvent.keyDown(row('demo-2'), { key: 'Enter' });
  expect(
    viewer().getByRole('img', { name: 'Synthetische Demoseite – kein PDF' }),
  ).toBeInTheDocument();
  expect(viewer().getByRole('button', { name: 'Nächste Seite' })).toBeDisabled();
});
it('verwendet Trefferseite und die sortierte themenübergreifende Suchreihenfolge', () => {
  render(<App />);
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'schritte' } });
  fireEvent.change(screen.getByRole('combobox', { name: 'Sortierung' }), {
    target: { value: 'date-asc' },
  });
  fireEvent.doubleClick(row('demo-3'));
  expectDemoPage(2, 4);
  expect(viewer().getByText(/Suchtreffer: schritte/)).toBeInTheDocument();
  expect(viewer().getByRole('button', { name: 'Vorheriges Dokument' })).toBeDisabled();
  fireEvent.click(viewer().getByRole('button', { name: 'Nächstes Dokument' }));
  expectDemoPage(1, 2);
  expect(viewer().getByRole('button', { name: 'Nächstes Dokument' })).toBeDisabled();
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'ArrowLeft' });
  expectDemoPage(2, 4);
});
it('zeigt die KU-orientierte Werkzeugleiste im Dokumentbereich und erhält die Navigation', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const pane = screen.getByRole('toolbar', { name: 'Dokumentansicht' });
  expect(document.querySelector('#viewer-document-pane')).toContainElement(pane);
  expect(within(pane).getByRole('slider', { name: 'Viewer-Zoom' })).toHaveValue('100');
  fireEvent.click(within(pane).getByRole('button', { name: 'Vergrößern' }));
  expect(within(pane).getByRole('slider', { name: 'Viewer-Zoom' })).toHaveValue('110');
  fireEvent.click(within(pane).getByRole('button', { name: 'Nächste Seite' }));
  expectDemoPage(2, 2);
  expect(within(pane).getByRole('button', { name: 'Nächste Seite' })).toBeDisabled();
  expect(viewer().getByRole('button', { name: 'Schließen' })).toBeInTheDocument();
});
it('prüft Trefferseiten und erzeugt stabile sichere Tab-URLs', () => {
  const doc = demoDocuments[0];
  expect(startPage(doc, { page: 2, term: 'lösung' })).toBe(2);
  expect(startPage(doc, { page: 0, term: 'x' })).toBe(1);
  expect(startPage(doc, { page: 3, term: 'x' })).toBe(1);
  expect(startPage(demoDocuments[1], { page: 1, term: 'x' })).toBe(1);
  const url = new URL(viewerUrl('a b', 2, 'lösung'));
  expect(url.searchParams.get('document')).toBe('a b');
  expect(url.searchParams.get('page')).toBe('2');
  expect(url.searchParams.get('term')).toBe('lösung');
});
it('öffnet Symbol und modifizierten Doppelklick als neue Tabs mit noopener', () => {
  const open = vi.spyOn(window, 'open').mockImplementation(() => null);
  render(<App />);
  fireEvent.click(within(row('demo-1')).getByRole('button', { name: 'In neuem Tab öffnen' }));
  expect(open).toHaveBeenCalledWith(
    expect.stringContaining('document=demo-1'),
    '_blank',
    'noopener,noreferrer',
  );
  fireEvent.doubleClick(row('demo-2'), { ctrlKey: true });
  expect(open).toHaveBeenCalledTimes(2);
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  fireEvent(
    row('demo-1'),
    new MouseEvent('auxclick', { button: 1, bubbles: true, cancelable: true }),
  );
  expect(open).toHaveBeenCalledTimes(3);
});
it('persistiert Öffnungsmodus und fällt bei ungültigem Wert auf Overlay zurück', () => {
  localStorage.setItem('via.minidocs.opening-mode', 'invalid');
  const open = vi.spyOn(window, 'open').mockImplementation(() => null);
  const first = render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Einstellungen' }));
  expect(screen.getByRole('radio', { name: 'Im Overlay' })).toBeChecked();
  fireEvent.click(screen.getByRole('radio', { name: 'In neuem Tab' }));
  expect(localStorage.getItem('via.minidocs.opening-mode')).toBe('tab');
  first.unmount();
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  expect(open).toHaveBeenCalledOnce();
});
it('validiert eigenständige Tab-URLs und öffnet eine angegebene Demoseite', () => {
  window.history.replaceState({}, '', '/?viewer=tab&document=demo-1&page=2');
  const { unmount } = render(<StandaloneViewer />);
  expectDemoPage(2, 2);
  unmount();
  window.history.replaceState({}, '', '/?viewer=tab&document=demo-1&page=99');
  render(<StandaloneViewer />);
  expect(screen.getByRole('heading', { name: 'Dokument nicht verfügbar' })).toBeInTheDocument();
});
it('begrenzt Viewer-Zoom unabhängig vom Kachelzoom und schützt den Hintergrund', () => {
  render(<App />);
  fireEvent.click(row('demo-1'));
  fireEvent.doubleClick(row('demo-1'));
  expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
  const slider = viewer().getByRole('slider', { name: /Viewer-Zoom/ });
  fireEvent.change(slider, { target: { value: '200' } });
  expect(slider).toHaveValue('200');
  fireEvent.keyDown(slider, { key: 'ArrowRight' });
  expectDemoPage(1, 2);
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
  expect(screen.getByRole('slider', { name: 'Zoom' })).toHaveValue('190');
  expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
});
it('meldet die Demo-Tab-Grenze und zeigt ohne Suchbegriff keinen Trefferhinweis', () => {
  window.history.replaceState({}, '', '/?viewer=tab&document=demo-1&page=2');
  render(<StandaloneViewer />);
  expect(
    screen.getByText(/Änderungen aus anderen Tabs werden nicht übernommen/),
  ).toBeInTheDocument();
  expect(screen.queryByText(/Suchtreffer:/)).not.toBeInTheDocument();
});
it('öffnet trotz nicht verfügbarem Speicher im Overlay und hält den Fokus im Dialog', () => {
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const dialog = screen.getByRole('dialog');
  const close = viewer().getByRole('button', { name: 'Schließen' });
  expect(dialog.contains(document.activeElement)).toBe(true);
  close.focus();
  fireEvent.keyDown(close, { key: 'Tab' });
  expect(dialog.contains(document.activeElement)).toBe(true);
});
it('meldet einen Fehler beim Öffnen eines Browser-Tabs', () => {
  vi.spyOn(window, 'open').mockImplementation(() => {
    throw new Error('blocked');
  });
  render(<App />);
  fireEvent.click(within(row('demo-1')).getByRole('button', { name: 'In neuem Tab öffnen' }));
  expect(screen.getByRole('status', { name: 'Statusmeldung' })).toHaveTextContent(
    'konnte nicht geöffnet',
  );
});
