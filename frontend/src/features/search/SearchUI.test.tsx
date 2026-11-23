import { fireEvent, render, screen, within, cleanup } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { Highlight } from './Highlight';
import { localToday } from './useSearchState';
function input() {
  return screen.getByRole('searchbox', { name: 'Dokumente suchen' });
}
function grid() {
  return within(screen.getByRole('list', { name: 'Kachelansicht' }));
}
function ids() {
  return Array.from(document.querySelectorAll<HTMLElement>('[data-document-id]')).map(
    (item) => item.dataset.documentId,
  );
}
function transfer() {
  const values = new Map<string, string>();
  return {
    get types() {
      return [...values.keys()];
    },
    setData: (key: string, value: string) => values.set(key, value),
    getData: (key: string) => values.get(key) ?? '',
  };
}
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});
describe('Such- und Filteroberfläche', () => {
  it('sucht Titel, Volltext, Phrasen und UND-Begriffe ohne Submit', () => {
    render(<App />);
    fireEvent.change(input(), { target: { value: 'schritte' } });
    expect(ids()).toEqual(['demo-1', 'demo-3']);
    expect(screen.getByText('2 Treffer')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Suchergebnisse' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Nur Titel' }));
    expect(screen.getByText('Keine Treffer')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Volltext inkl. Transkripte' }));
    fireEvent.change(input(), { target: { value: '"nächste schritte" projekt' } });
    expect(ids()).toEqual(['demo-1']);
    expect(grid().getByText('Treffer S. 1')).toBeInTheDocument();
    fireEvent.change(input(), { target: { value: 'lenkung planungs' } });
    expect(ids()).toEqual(['demo-3']);
    expect(grid().getByText('Kunden / Kunde Alpha / Lenkungskreis')).toBeInTheDocument();
  });
  it('zeigt dieselben Treffer und sortiert mit aria-sort und Shift nach aktueller Reihenfolge', () => {
    render(<App />);
    fireEvent.change(input(), { target: { value: 'schritte' } });
    expect(ids()).toEqual(['demo-1', 'demo-3']);
    fireEvent.change(screen.getByRole('combobox', { name: 'Sortierung' }), {
      target: { value: 'date-asc' },
    });
    expect(ids()).toEqual(['demo-3', 'demo-1']);
    fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
    expect(ids()).toEqual(['demo-3', 'demo-1']);
    expect(screen.getByRole('columnheader', { name: 'Datum' })).toHaveAttribute(
      'aria-sort',
      'ascending',
    );
    const doc1 = document.querySelector<HTMLElement>('[data-document-id="demo-3"]')!,
      doc2 = document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!;
    fireEvent.click(doc1);
    fireEvent.click(doc2, { shiftKey: true });
    expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
    fireEvent.click(within(screen.getByRole('table')).getByRole('button', { name: 'Titel' }));
    expect(screen.getByRole('columnheader', { name: 'Titel' })).toHaveAttribute(
      'aria-sort',
      'ascending',
    );
    expect(screen.getByRole('columnheader', { name: 'Datum' })).not.toHaveAttribute('aria-sort');
    fireEvent.change(screen.getByRole('combobox', { name: 'Sortierung' }), {
      target: { value: 'relevance' },
    });
    expect(ids()).toEqual(['demo-1', 'demo-3']);
  });
  it('wählt nur sichtbare Treffer aus und leert Auswahl bei Filterwechsel', () => {
    render(<App />);
    fireEvent.change(input(), { target: { value: 'schritte' } });
    fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
    expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Aufgabe' }));
    expect(ids()).toEqual(['demo-1']);
    expect(screen.queryByText('2 Dokumente ausgewählt')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
    expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
    expect(
      screen.getByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
    ).toBeChecked();
  });
  it('kombiniert Zeitraum, Tag-UND und Themenunterbaum und setzt Filter zurück', () => {
    render(<App />);
    fireEvent.click(
      within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
        name: 'Kunden',
      }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
    fireEvent.change(screen.getByLabelText('Bereich'), { target: { value: 'topic' } });
    fireEvent.click(screen.getByRole('checkbox', { name: 'Projekt' }));
    fireEvent.change(screen.getByLabelText('Von'), { target: { value: '2026-10-29' } });
    fireEvent.change(screen.getByLabelText('Bis'), { target: { value: '2026-10-29' } });
    expect(ids()).toEqual(['demo-3']);
    fireEvent.click(screen.getByRole('checkbox', { name: 'Aufgabe' }));
    expect(screen.getByText('Keine Treffer')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Filter zurücksetzen' }));
    expect(screen.queryByText('Keine Treffer')).not.toBeInTheDocument();
  });
  it('setzt Datumspresets mit einem festen lokalen Datum und meldet ungültigen Bereich', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 10, 23, 12));
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
    fireEvent.click(screen.getByRole('button', { name: 'Dieses Jahr' }));
    expect(screen.getByLabelText('Von')).toHaveValue('2026-01-01');
    expect(screen.getByLabelText('Bis')).toHaveValue('2026-11-23');
    fireEvent.click(screen.getByRole('button', { name: '12 Monate' }));
    expect(screen.getByLabelText('Von')).toHaveValue('2025-11-23');
    fireEvent.change(screen.getByLabelText('Von'), { target: { value: '2026-12-01' } });
    expect(screen.getByRole('alert')).toHaveTextContent('Startdatum');
    expect(screen.getByText('Keine Treffer')).toBeInTheDocument();
  });
  it('verschiebt ausgewählte Treffer aus verschiedenen Themen per synthetischem Drop und macht ihn rückgängig', () => {
    render(<App />);
    fireEvent.change(input(), { target: { value: 'schritte' } });
    fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
    const dataTransfer = transfer();
    fireEvent.dragStart(document.querySelector('[data-document-id="demo-1"]')!, { dataTransfer });
    const target = within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Beta',
    }).parentElement!;
    fireEvent.dragOver(target, { dataTransfer });
    fireEvent.drop(target, { dataTransfer });
    expect(screen.getByRole('status', { name: 'Statusmeldung' })).toHaveTextContent(
      '2 Dokumente verschoben',
    );
    expect(screen.getByRole('button', { name: 'Rückgängig' })).toBeInTheDocument();
    expect(screen.getAllByText('Kunden / Kunde Beta')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
    expect(grid().getByText('Nicht zugeordnet')).toBeInTheDocument();
    expect(screen.getByText('Kunden / Kunde Alpha / Lenkungskreis')).toBeInTheDocument();
  });
  it('fokussiert mit /, beendet Suche per Escape und schließt Filter mit Fokus-Rückgabe', () => {
    render(<App />);
    fireEvent.keyDown(document, { key: '/' });
    expect(input()).toHaveFocus();
    fireEvent.change(input(), { target: { value: 'unauffindbar' } });
    expect(screen.getByText('Keine Treffer')).toBeInTheDocument();
    fireEvent.keyDown(input(), { key: 'Escape' });
    expect(input()).toHaveValue('');
    expect(screen.queryByText('Keine Treffer')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
    fireEvent.keyDown(screen.getByLabelText('Von'), { key: 'Escape' });
    expect(screen.getByRole('button', { name: 'Filter' })).toHaveFocus();
    expect(screen.getByRole('button', { name: 'Filter' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });
  it('hebt Unicode-Treffer ohne kaputte Positionen und ohne HTML-Injektion hervor', () => {
    const { container } = render(
      <Highlight text={'A😀 Fru\u0308h <b>École</b>'} terms={['fruh', 'ecole']} />,
    );
    expect(container.querySelectorAll('mark')).toHaveLength(2);
    expect(container.querySelectorAll('mark')[0]).toHaveTextContent('Fru\u0308h');
    expect(container.querySelectorAll('mark')[1]).toHaveTextContent('École');
    expect(container.querySelector('b')).toBeNull();
    expect(container).toHaveTextContent('A😀 Fru\u0308h <b>École</b>');
  });
  it('ermittelt lokale Kalendertage ohne UTC-Verschiebung', () => {
    expect(localToday(new Date(2026, 0, 1, 0, 1))).toBe('2026-01-01');
  });
});
it('ordnet echte Treffer anhand ihrer M5-A-Relevanz und kann Suchtext explizit löschen', () => {
  render(<App />);
  fireEvent.change(input(), { target: { value: 'projekt' } });
  expect(ids()[0]).toBe('demo-4');
  expect(screen.getByRole('combobox', { name: 'Sortierung' })).toHaveValue('relevance');
  fireEvent.click(screen.getByRole('button', { name: 'Suchtext löschen' }));
  expect(input()).toHaveValue('');
  expect(input()).toHaveFocus();
  expect(screen.queryByRole('heading', { name: 'Suchergebnisse' })).not.toBeInTheDocument();
});
