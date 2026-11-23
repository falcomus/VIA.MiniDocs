import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { demoDocuments } from '../documents/demoDocuments';
import { demoTags } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import { DocumentPagePane } from './DocumentPagePane';
import { DocumentViewer } from './DocumentViewer';

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
  fireEvent.click(v.getByRole('checkbox', { name: /Synchron scrollen/ }));
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
  expect(screen.queryByTitle(/PDF:/)).not.toBeInTheDocument();
});
it('prüft PDF-Signatur und gibt Objekt-URLs bei Quellenwechsel und Unmount frei', async () => {
  const create = vi.fn().mockReturnValueOnce('blob:first').mockReturnValueOnce('blob:second');
  const revoke = vi.fn();
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
  function pdf(header: string) {
    const blob = new Blob([], { type: 'application/pdf' });
    vi.spyOn(blob, 'slice').mockReturnValue({
      arrayBuffer: async () => new TextEncoder().encode(header).buffer,
    } as Blob);
    return blob;
  }
  const props = { document: demoDocuments[0], page: 1, zoom: 100 };
  const first = pdf('%PDF-');
  const second = pdf('%PDF-');
  const invalid = pdf('wrong');
  const { rerender, unmount } = render(
    <DocumentPagePane {...props} source={{ kind: 'pdf', blob: first }} />,
  );
  expect(await screen.findByTitle(/PDF:/)).toHaveAttribute('src', 'blob:first#page=1');
  rerender(<DocumentPagePane {...props} page={2} source={{ kind: 'pdf', blob: second }} />);
  expect(await screen.findByTitle(/PDF:/)).toHaveAttribute('src', 'blob:second#page=2');
  expect(revoke).toHaveBeenCalledWith('blob:first');
  rerender(<DocumentPagePane {...props} source={{ kind: 'pdf', blob: invalid }} />);
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('ungültig'));
  expect(revoke).toHaveBeenCalledWith('blob:second');
  create.mockReturnValueOnce('blob:third');
  rerender(<DocumentPagePane {...props} source={{ kind: 'pdf', blob: first }} />);
  expect(await screen.findByTitle(/PDF:/)).toHaveAttribute('src', 'blob:third#page=1');
  unmount();
  expect(revoke).toHaveBeenCalledWith('blob:third');
  expect(create).toHaveBeenCalledTimes(3);
});

it('ordnet Quellen beim Dokumentwechsel der aktuellen ID zu und gibt die alte URL frei', async () => {
  const create = vi.fn().mockReturnValue('blob:document-one');
  const revoke = vi.fn();
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
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
      onClose={() => {}}
    />,
  );
  expect(await screen.findByTitle(/PDF:/)).toHaveAttribute('src', 'blob:document-one#page=1');
  fireEvent.click(screen.getByRole('button', { name: 'Nächstes Dokument' }));
  expect(screen.getByText('Synthetische Demoseite – kein PDF')).toBeInTheDocument();
  expect(revoke).toHaveBeenCalledWith('blob:document-one');
  fireEvent.click(screen.getByRole('button', { name: 'Nächstes Dokument' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Keine PDF-Quelle vorhanden');
  expect(screen.queryByTitle(/PDF:/)).not.toBeInTheDocument();
  expect(screen.queryByText('Synthetische Demoseite – kein PDF')).not.toBeInTheDocument();
  expect(getSource).toHaveBeenCalledWith('demo-3');
  fireEvent.click(screen.getByRole('button', { name: 'Vorheriges Dokument' }));
  fireEvent.click(screen.getByRole('button', { name: 'Vorheriges Dokument' }));
  expect(await screen.findByTitle(/PDF:/)).toHaveAttribute('src', 'blob:document-one#page=1');
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
