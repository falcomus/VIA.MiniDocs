import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { StrictMode } from 'react';
import { App } from '../../App';
import { demoDocuments } from '../documents/demoDocuments';
import { demoTags } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import { DocumentPagePane } from './DocumentPagePane';
import { DocumentViewer } from './DocumentViewer';

beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
function openViewer() {
  render(<App />);
  fireEvent.doubleClick(document.querySelector('[data-document-id="demo-1"]')!);
  return within(screen.getByRole('dialog'));
}
it('begrenzt den Splitter per Tastatur und erhält Dokument und Transkript', () => {
  const v = openViewer();
  const editor = v.getByRole('textbox', { name: /Transkript Seite 1/ });
  fireEvent.change(editor, { target: { value: 'Unverändert' } });
  const splitter = v.getByRole('separator', { name: /Breite/ });
  expect(splitter).toHaveAttribute('aria-valuenow', '56');
  splitter.focus();
  fireEvent.keyDown(splitter, { key: 'End' });
  expect(splitter).toHaveAttribute('aria-valuenow', '75');
  fireEvent.keyDown(splitter, { key: 'ArrowRight' });
  expect(splitter).toHaveAttribute('aria-valuenow', '75');
  fireEvent.keyDown(splitter, { key: 'Home' });
  expect(splitter).toHaveAttribute('aria-valuenow', '25');
  fireEvent.keyDown(splitter, { key: 'ArrowRight', shiftKey: true });
  expect(splitter).toHaveAttribute('aria-valuenow', '35');
  expect(splitter).toHaveFocus();
  expect(editor).toHaveValue('Unverändert');
  expect(v.getByRole('heading', { level: 1 })).toHaveTextContent(demoDocuments[0].title);
});
it('koppelt Demo-Scrollen nur vorwärts und lässt sich abschalten', () => {
  const v = openViewer();
  const editor = v.getByRole('textbox', { name: /Transkript Seite 1/ });
  const pane = document.getElementById('viewer-document-pane')!;
  Object.defineProperties(pane, { scrollHeight: { value: 1000 }, clientHeight: { value: 500 } });
  Object.defineProperties(editor, { scrollHeight: { value: 600 }, clientHeight: { value: 200 } });
  pane.scrollTop = 250;
  fireEvent.scroll(pane);
  expect(editor.scrollTop).toBe(200);
  const sync = v.getByRole('checkbox', { name: /Synchron scrollen/ });
  expect(v.getByRole('region', { name: 'Transkript' })).toContainElement(sync);
  expect(v.getByRole('toolbar', { name: 'Dokumentansicht' })).not.toContainElement(sync);
  fireEvent.click(sync);
  pane.scrollTop = 500;
  fireEvent.scroll(pane);
  expect(editor.scrollTop).toBe(200);
  fireEvent.scroll(editor);
  expect(pane.scrollTop).toBe(500);
});
it('unterscheidet Demo und fehlende PDF-Quelle', () => {
  const props = { document: demoDocuments[0], page: 1, zoom: 100 };
  const { rerender } = render(<DocumentPagePane {...props} source={{ kind: 'demo' }} />);
  expect(screen.getByText('Synthetische Demoseite – kein PDF')).toBeInTheDocument();
  rerender(<DocumentPagePane {...props} source={{ kind: 'pdf', blob: null }} />);
  expect(screen.getByRole('alert')).toHaveTextContent('Keine PDF-Quelle');
  expect(screen.queryByRole('region', { name: 'PDF-Darstellung' })).not.toBeInTheDocument();
});
it('übergibt Blob-Quellen ohne Objekt-URL und zeigt Ladefehler', async () => {
  const create = vi.fn();
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create });
  const props = { document: demoDocuments[0], page: 1, zoom: 100 };
  const first = new Blob(['one'], { type: 'application/pdf' });
  const second = new Blob(['two'], { type: 'application/pdf' });
  const invalid = new Blob(['not pdf'], { type: 'text/plain' });
  const onPdfLoaded = vi.fn();
  const { rerender } = render(
    <DocumentPagePane {...props} source={{ kind: 'pdf', blob: first }} onPdfLoaded={onPdfLoaded} />,
  );
  expect(await screen.findByRole('img', { name: 'PDF-Seite 1' })).toBeInTheDocument();
  expect(onPdfLoaded).toHaveBeenCalledWith(first, 7);
  rerender(
    <DocumentPagePane
      {...props}
      page={2}
      source={{ kind: 'pdf', blob: second }}
      onPdfLoaded={onPdfLoaded}
    />,
  );
  expect(await screen.findByRole('img', { name: 'PDF-Seite 2' })).toBeInTheDocument();
  expect(onPdfLoaded).toHaveBeenCalledWith(second, 7);
  rerender(<DocumentPagePane {...props} source={{ kind: 'pdf', blob: invalid }} />);
  expect(screen.getByRole('alert')).toHaveTextContent('PDF konnte nicht geladen werden');
  expect(create).not.toHaveBeenCalled();
});
it('hält die PDF-Quelle im StrictMode bei Seitenwechsel und gibt keine Objekt-URL aus', async () => {
  const create = vi.fn();
  const revoke = vi.fn();
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
  const blob = new Blob(['test'], { type: 'application/pdf' });
  const loaded = vi.fn();
  const { rerender, unmount } = render(
    <StrictMode>
      <DocumentPagePane
        source={{ kind: 'pdf', blob }}
        document={demoDocuments[0]}
        page={1}
        zoom={100}
        onPdfLoaded={loaded}
      />
    </StrictMode>,
  );
  expect(await screen.findByRole('img', { name: 'PDF-Seite 1' })).toBeInTheDocument();
  rerender(
    <StrictMode>
      <DocumentPagePane
        source={{ kind: 'pdf', blob }}
        document={demoDocuments[0]}
        page={2}
        zoom={100}
        onPdfLoaded={loaded}
      />
    </StrictMode>,
  );
  expect(screen.getByRole('img', { name: 'PDF-Seite 2' })).toBeInTheDocument();
  expect(loaded).toHaveBeenCalledWith(blob, 7);
  unmount();
  expect(create).not.toHaveBeenCalled();
  expect(revoke).not.toHaveBeenCalled();
});

it('ordnet Quellen beim Dokumentwechsel der aktuellen ID zu und entfernt die alte Darstellung', async () => {
  const blob = new Blob([], { type: 'application/pdf' });
  vi.spyOn(blob, 'slice').mockReturnValue({
    arrayBuffer: async () => new TextEncoder().encode('%PDF-').buffer,
  } as Blob);
  const getSource = vi.fn((id: string) =>
    id === 'demo-1'
      ? { kind: 'pdf' as const, blob }
      : id === 'demo-2'
        ? { kind: 'demo' as const }
        : undefined,
  );
  render(
    <DocumentViewer
      session={{ id: 'demo-1', ids: ['demo-1', 'demo-2', 'demo-3'], hits: {} }}
      documents={demoDocuments}
      topics={demoTopics}
      tags={demoTags}
      getSource={getSource}
      onEditTranscript={() => {}}
      onMoveTopic={() => {}}
      onClose={() => {}}
    />,
  );
  expect(await screen.findByRole('region', { name: 'PDF-Darstellung' })).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Nächstes Dokument' }));
  expect(screen.getByText('Synthetische Demoseite – kein PDF')).toBeInTheDocument();
  expect(screen.queryByRole('region', { name: 'PDF-Darstellung' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Nächstes Dokument' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Keine PDF-Quelle vorhanden');
  expect(screen.queryByTitle(/PDF:/)).not.toBeInTheDocument();
  expect(screen.queryByText('Synthetische Demoseite – kein PDF')).not.toBeInTheDocument();
  expect(getSource).toHaveBeenCalledWith('demo-3');
  fireEvent.click(screen.getByRole('button', { name: 'Vorheriges Dokument' }));
  fireEvent.click(screen.getByRole('button', { name: 'Vorheriges Dokument' }));
  expect(await screen.findByRole('region', { name: 'PDF-Darstellung' })).toBeInTheDocument();
});

it('erhält die PDF-Darstellung beim Wechsel der Themenzuordnung', async () => {
  const blob = new Blob(['test'], { type: 'application/pdf' });
  const source = { kind: 'pdf' as const, blob };
  const props = {
    session: { id: 'demo-1', ids: ['demo-1'], hits: {} },
    topics: demoTopics,
    tags: demoTags,
    getSource: () => source,
    onEditTranscript: () => {},
    onMoveTopic: () => {},
  };
  const { rerender } = render(<DocumentViewer {...props} documents={demoDocuments} />);
  const pdf = await screen.findByRole('region', { name: 'PDF-Darstellung' });
  fireEvent.click(screen.getByRole('button', { name: 'Vergrößern' }));
  rerender(
    <DocumentViewer
      {...props}
      documents={demoDocuments.map((doc) =>
        doc.id === 'demo-1' ? { ...doc, topicId: 'alpha' } : doc,
      )}
    />,
  );
  expect(screen.getByRole('region', { name: 'PDF-Darstellung' })).toBe(pdf);
  expect(screen.getByRole('slider', { name: 'Viewer-Zoom' })).toHaveValue('110');
  expect(screen.getByRole('combobox', { name: 'Thema' })).toHaveValue('alpha');
});

it('beendet Pointer-Abbruch und Capture-Verlust und erhält die Splitterauswahl', () => {
  const v = openViewer();
  const splitter = v.getByRole('separator', { name: /Breite/ });
  const parent = splitter.parentElement!;
  vi.spyOn(parent, 'getBoundingClientRect').mockReturnValue({ left: 0, width: 1000 } as DOMRect);
  let captured = false;
  splitter.setPointerCapture = vi.fn(() => {
    captured = true;
  });
  splitter.hasPointerCapture = vi.fn(() => captured);
  splitter.releasePointerCapture = vi.fn(() => {
    captured = false;
  });
  const pointer = (type: string, x = 0) => {
    const event = new MouseEvent(type, { bubbles: true, button: 0, clientX: x });
    Object.defineProperty(event, 'pointerId', { value: 1 });
    fireEvent(splitter, event);
  };
  pointer('pointerdown');
  expect(splitter).toHaveFocus();
  pointer('pointermove', 700);
  expect(splitter).toHaveAttribute('aria-valuenow', '70');
  pointer('pointercancel');
  pointer('pointermove', 400);
  expect(splitter).toHaveAttribute('aria-valuenow', '70');
  pointer('pointerdown');
  pointer('lostpointercapture');
  pointer('pointermove', 400);
  expect(splitter).toHaveAttribute('aria-valuenow', '70');
  fireEvent.keyDown(splitter, { key: 'ArrowRight' });
  expect(v.getByRole('heading', { level: 1 })).toHaveTextContent(demoDocuments[0].title);
});

it('verschiebt den Fokus beim Ausblenden des Splitters auf die Seitenauswahl', () => {
  const v = openViewer();
  const splitter = v.getByRole('separator', { name: /Breite/ });
  splitter.focus();
  splitter.style.display = 'none';
  fireEvent(window, new Event('resize'));
  expect(v.getByRole('combobox', { name: /Seite/ })).toHaveFocus();
});
