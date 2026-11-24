import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { StandaloneViewer } from './StandaloneViewer';
import { TranscriptPane } from './TranscriptPane';
import { demoDocuments } from '../documents/demoDocuments';
const row = (id: string) => document.querySelector<HTMLElement>(`[data-document-id="${id}"]`)!;
const dialog = () => within(screen.getByRole('dialog'));
const editor = () => {
  const heading = dialog().getByRole('heading', { name: /Transkript · Seite/ });
  const page = heading.textContent?.match(/\d+/)?.[0];
  return dialog().getByRole('textbox', { name: `Transkript Seite ${page}` });
};
beforeEach(() => {
  localStorage.removeItem('via.minidocs.opening-mode');
  window.history.replaceState({}, '', '/');
});
afterEach(() => {
  cleanup();
  window.history.replaceState({}, '', '/');
});
it('führt die aktive Transkriptkarte nur bei eingeschalteter Nachführung nach', () => {
  const scroll = vi.fn();
  const original = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'scrollIntoView');
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    value: scroll,
  });
  try {
    const props = { document: demoDocuments[0], onPageChange: () => {}, onEdit: () => {} };
    const view = render(<TranscriptPane {...props} page={1} followPage={false} />);
    view.rerender(<TranscriptPane {...props} page={2} followPage={false} />);
    expect(scroll).not.toHaveBeenCalled();
    view.rerender(<TranscriptPane {...props} page={1} followPage />);
    expect(scroll).toHaveBeenCalledOnce();
  } finally {
    if (original) Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', original);
    else Reflect.deleteProperty(HTMLElement.prototype, 'scrollIntoView');
  }
});
it('kennzeichnet Suchgruppen und Icon-Aktionen zugänglich und aktiviert sie bei Treffern', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  expect(dialog().getByRole('group', { name: 'Transkript durchsuchen' })).toBeInTheDocument();
  expect(dialog().getByRole('group', { name: 'Transkript ersetzen' })).toBeInTheDocument();
  expect(dialog().queryByText('Suchbegriff eingeben')).not.toBeInTheDocument();
  expect(dialog().getByRole('textbox', { name: 'Suchen' })).toHaveAttribute(
    'placeholder',
    'Suchen',
  );
  expect(dialog().getByRole('textbox', { name: 'Ersetzen durch' })).toHaveAttribute(
    'placeholder',
    'Ersetzen durch',
  );
  const previous = dialog().getByRole('button', { name: 'Vorheriger Treffer' });
  const next = dialog().getByRole('button', { name: 'Nächster Treffer' });
  expect(previous).toBeDisabled();
  expect(next).toBeDisabled();
  expect(previous).toHaveAttribute('title', 'Vorheriger Treffer');
  fireEvent.change(dialog().getByRole('textbox', { name: 'Suchen' }), {
    target: { value: 'Projektstart' },
  });
  expect(next).toBeEnabled();
  fireEvent.click(next);
  expect(dialog().getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveFocus();
});
it('zeigt alle Seiten und aktiviert die gewählte Karte ohne fremde Seiten zu ändern', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const first = dialog().getByRole('textbox', { name: 'Transkript Seite 1' });
  const second = dialog().getByRole('textbox', { name: 'Transkript Seite 2' });
  const original = (first as HTMLTextAreaElement).value;
  fireEvent.focus(second);
  fireEvent.change(second, { target: { value: 'Zweite Seite' } });
  expect(first).toHaveValue(original);
  expect(dialog().getByRole('button', { name: 'Seite 2' })).toHaveAttribute('aria-current', 'true');
  fireEvent.click(dialog().getByRole('button', { name: 'Seite 1' }));
  expect(dialog().getByRole('button', { name: 'Seite 1' })).toHaveAttribute('aria-current', 'true');
});
it('speichert Änderungen sofort, erhält sie beim Seitenwechsel und erneutem Öffnen', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const text = editor();
  fireEvent.change(text, { target: { value: 'Neue Zeile\nProjektabschluss' } });
  expect(dialog().getByText(/Bearbeitet/)).toBeInTheDocument();
  const ink = dialog().getByRole('img', { name: 'Synthetische Demoseite – kein PDF' });
  expect(ink).toHaveTextContent('Projektstart: Nächste Schritte');
  expect(ink).not.toHaveTextContent('Projektabschluss');
  fireEvent.click(dialog().getByRole('button', { name: 'Nächste Seite' }));
  expect((editor() as HTMLTextAreaElement).value).toContain('Frühjahr');
  fireEvent.click(dialog().getByRole('button', { name: 'Vorherige Seite' }));
  expect(editor()).toHaveValue('Neue Zeile\nProjektabschluss');
  fireEvent.click(dialog().getByRole('button', { name: 'Schließen' }));
  fireEvent.doubleClick(row('demo-1'));
  expect(editor()).toHaveValue('Neue Zeile\nProjektabschluss');
  fireEvent.click(dialog().getByRole('button', { name: 'Schließen' }));
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'projektabschluss' } });
  expect(row('demo-1')).toBeInTheDocument();
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'projektstart' } });
  expect(screen.getByText('Keine Treffer')).toBeInTheDocument();
});
it('legt ein fehlendes Transkript an und erhält den Inhalt beim Dokumentwechsel', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-2'));
  expect(dialog().getByText('Noch kein Transkript')).toBeInTheDocument();
  fireEvent.change(editor(), { target: { value: 'Mein neuer Text' } });
  expect(dialog().getByText(/Bearbeitet/)).toBeInTheDocument();
  fireEvent.click(dialog().getByRole('button', { name: 'Vorheriges Dokument' }));
  fireEvent.click(dialog().getByRole('button', { name: 'Nächstes Dokument' }));
  expect(editor()).toHaveValue('Mein neuer Text');
});
it('findet auf mehreren Seiten, navigiert und ersetzt einzeln und anschließend alle', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  fireEvent.change(editor(), { target: { value: 'Alpha alpha' } });
  fireEvent.click(dialog().getByRole('button', { name: 'Nächste Seite' }));
  fireEvent.change(editor(), { target: { value: 'ALPHA\nalpha' } });
  fireEvent.change(dialog().getByRole('textbox', { name: 'Suchen' }), {
    target: { value: 'alpha' },
  });
  expect(dialog().getByText('4 Treffer')).toBeInTheDocument();
  fireEvent.click(dialog().getByRole('button', { name: 'Nächster Treffer' }));
  expect(editor()).toHaveValue('Alpha alpha');
  expect(editor()).toHaveFocus();
  expect(editor()).toHaveProperty('selectionStart', 0);
  fireEvent.click(dialog().getByRole('button', { name: 'Vorheriger Treffer' }));
  expect(editor()).toHaveValue('ALPHA\nalpha');
  fireEvent.change(dialog().getByRole('textbox', { name: 'Ersetzen durch' }), {
    target: { value: 'Beta' },
  });
  fireEvent.click(dialog().getByRole('button', { name: 'Ersetzen' }));
  expect(editor()).toHaveValue('ALPHA\nBeta');
  fireEvent.click(dialog().getByRole('button', { name: 'Alle ersetzen' }));
  expect(dialog().getByRole('status')).toHaveTextContent('3 Stellen ersetzt');
  expect(editor()).toHaveValue('Beta\nBeta');
  fireEvent.click(dialog().getByRole('button', { name: 'Vorherige Seite' }));
  expect(editor()).toHaveValue('Beta Beta');
  fireEvent.change(dialog().getByRole('textbox', { name: 'Suchen' }), {
    target: { value: 'nichts' },
  });
  expect(dialog().getByText('0 Treffer')).toBeInTheDocument();
  fireEvent.change(dialog().getByRole('textbox', { name: 'Suchen' }), { target: { value: '' } });
  expect(dialog().getByRole('button', { name: 'Alle ersetzen' })).toBeDisabled();
});
it('hält den Viewer bei wegfallendem Suchtreffer offen und bereinigt die Hintergrundauswahl', () => {
  render(<App />);
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'schritte' } });
  fireEvent.click(row('demo-3'));
  fireEvent.doubleClick(row('demo-3'));
  expect((editor() as HTMLTextAreaElement).value).toContain('Schritte');
  fireEvent.change(editor(), { target: { value: 'Geänderter Text' } });
  expect(screen.getByRole('dialog')).toBeInTheDocument();
  fireEvent.keyDown(editor(), { key: 'ArrowLeft' });
  expect(
    dialog().getByRole('heading', { name: 'Lenkungskreis – Planungsstand', level: 1 }),
  ).toBeInTheDocument();
  fireEvent.keyDown(editor(), { key: 'a', ctrlKey: true });
  expect(screen.getByRole('dialog')).toBeInTheDocument();
  fireEvent.click(dialog().getByRole('button', { name: 'Schließen' }));
  expect(document.querySelector('[data-document-id="demo-3"]')).toBeNull();
  expect(screen.queryByText('1 Dokument ausgewählt')).not.toBeInTheDocument();
});
it('bearbeitet den eigenständigen Tab mit sichtbarer In-Memory-Grenze', () => {
  window.history.replaceState({}, '', '/?viewer=tab&document=demo-1&page=1');
  render(<StandaloneViewer />);
  expect(screen.getByText(/Änderungen aus anderen Tabs/)).toBeInTheDocument();
  fireEvent.change(screen.getByRole('textbox', { name: 'Transkript Seite 1' }), {
    target: { value: 'Nur hier' },
  });
  expect(screen.getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveValue('Nur hier');
  expect(screen.getByText(/gehen beim Neuladen verloren/)).toBeInTheDocument();
});
it('hält den Fokus bei Seiten- und Treffergrenzen auf einem bedienbaren Element', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const next = dialog().getByRole('button', { name: 'Nächste Seite' });
  next.focus();
  fireEvent.click(next);
  expect(dialog().getByRole('button', { name: 'Vorherige Seite' })).toHaveFocus();
  fireEvent.change(editor(), { target: { value: 'Einmal' } });
  fireEvent.change(dialog().getByRole('textbox', { name: 'Suchen' }), {
    target: { value: 'Einmal' },
  });
  const replace = dialog().getByRole('button', { name: 'Alle ersetzen' });
  replace.focus();
  fireEvent.click(replace);
  expect(editor()).toHaveFocus();
  expect(replace).toBeDisabled();
});
it('erhält die Änderung auf Seite 2 über Dokumentwechsel und Rückkehr', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  fireEvent.click(dialog().getByRole('button', { name: 'Nächste Seite' }));
  fireEvent.change(editor(), { target: { value: 'Nur Dokument A, Seite zwei' } });
  fireEvent.click(dialog().getByRole('button', { name: 'Nächstes Dokument' }));
  expect(editor()).toHaveValue('');
  fireEvent.click(dialog().getByRole('button', { name: 'Vorheriges Dokument' }));
  fireEvent.click(dialog().getByRole('button', { name: 'Nächste Seite' }));
  expect(editor()).toHaveValue('Nur Dokument A, Seite zwei');
});
it('erhält dokumentbezogene Änderungen nach Schließen und Öffnen eines anderen Dokuments', () => {
  render(<App />);
  fireEvent.doubleClick(row('demo-1'));
  const first = dialog().getByRole('textbox', { name: 'Transkript Seite 1' });
  const original = (first as HTMLTextAreaElement).value;
  fireEvent.change(dialog().getByRole('textbox', { name: 'Transkript Seite 2' }), {
    target: { value: 'Änderung auf Seite zwei' },
  });
  fireEvent.click(dialog().getByRole('button', { name: 'Schließen' }));
  fireEvent.doubleClick(row('demo-2'));
  expect(dialog().getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveValue('');
  fireEvent.click(dialog().getByRole('button', { name: 'Schließen' }));
  fireEvent.doubleClick(row('demo-1'));
  expect(dialog().getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveValue(original);
  expect(dialog().getByRole('textbox', { name: 'Transkript Seite 2' })).toHaveValue(
    'Änderung auf Seite zwei',
  );
});
