import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it } from 'vitest';
import { App } from '../../App';
import { DocumentList } from './DocumentList';
import { demoDocuments } from './demoDocuments';
import { formatDocumentDate } from './documentDate';
import { TagChips } from './TagChips';
it.each([null, '', 'bad', '2026-02-30', '2026-13-01'])(
  'zeigt für ungültiges Datum %s einen Platzhalter',
  (date) => expect(formatDocumentDate(date)).toBe('ohne Datum'),
);
it('zeigt gültige Kalenderdaten deutsch an', () =>
  expect(formatDocumentDate('2024-02-29')).toBe('29.02.2024'));
it('trennt Kachelsteuerung und Vorschau und erhält Checkboxauswahl und Öffnen', async () => {
  const user = userEvent.setup();
  render(<App />);
  const checkbox = screen.getByRole('checkbox', {
    name: 'Besprechungsnotizen – Eingang auswählen',
  });
  const card = checkbox.closest('li')!;
  const actionRow = checkbox.closest('label')!.parentElement!;
  expect(actionRow).toContainElement(within(card).getByRole('button', { name: 'Tags zuweisen' }));
  expect(actionRow).toContainElement(
    within(card).getByRole('button', { name: 'In neuem Tab öffnen' }),
  );
  expect(actionRow).not.toContainElement(
    within(card).getByRole('img', { name: 'Synthetische Demovorschau' }),
  );
  await user.click(checkbox.closest('label')!);
  expect(checkbox).toBeChecked();
  await user.dblClick(within(card).getByTitle('Besprechungsnotizen – Eingang'));
  expect(screen.getByRole('dialog')).toBeInTheDocument();
});
it('zeigt Tagfarbe nur am Punkt und behält lange Bezeichnungen', () => {
  const tags = [
    { id: 'long', name: 'Sehr lange Tagbezeichnung', color: '#ff0000', description: '' },
  ];
  render(<TagChips ids={['long']} tags={tags} />);
  const chip = screen.getByText('Sehr lange Tagbezeichnung');
  expect(chip.style.borderColor).toBe('');
  expect(chip.firstElementChild).toHaveStyle({ backgroundColor: '#ff0000' });
});
it('zeigt Dateiname, Typ, Tags und bearbeiteten Transkriptstatus in Originalspalten', () => {
  render(
    <DocumentList
      documents={[
        {
          ...demoDocuments[0],
          date: null,
          fileName: 'demo.pdf',
          tagIds: ['projekt'],
          transcript: { edited: true, pages: [{ page: 1, text: 'Test' }] },
        },
      ]}
      folders={[]}
      folderCounts={new Map()}
      onNavigate={() => {}}
    />,
  );
  expect(screen.getByRole('columnheader', { name: 'Transkript' })).toBeInTheDocument();
  expect(screen.getByText(/demo.pdf/)).toBeInTheDocument();
  expect(screen.getByText('Projekt')).toBeInTheDocument();
  expect(screen.getByLabelText('Transkript vorhanden (bearbeitet)')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Herunterladen' })).toBeDisabled();
});
it('behält Kachelansicht bei Themenwechsel und navigiert direkte Ordner mit Tastatur samt Breadcrumb', async () => {
  const user = userEvent.setup();
  render(<App />);
  await user.click(screen.getByRole('button', { name: 'Kachelansicht' }));
  expect(screen.getByRole('button', { name: 'Kachelansicht' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  expect(screen.getAllByRole('img', { name: 'Synthetische Demovorschau' })).toHaveLength(2);
  await user.click(screen.getByRole('button', { name: 'Kunden' }));
  const grid = within(screen.getByRole('list', { name: 'Kachelansicht' }));
  expect(grid.getAllByRole('button')).toHaveLength(2);
  expect(grid.queryByRole('button', { name: /Lenkungskreis/ })).not.toBeInTheDocument();
  grid.getByRole('button', { name: /Kunde Alpha/ }).focus();
  await user.keyboard('{Enter}');
  expect(screen.getByRole('heading', { name: 'Kunde Alpha' })).toBeInTheDocument();
  expect(
    within(screen.getByRole('list', { name: 'Kachelansicht' })).getByRole('button', {
      name: /Lenkungskreis/,
    }),
  ).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Listenansicht' }));
  expect(screen.getByRole('table')).toHaveTextContent('Lenkungskreis');
  const table = within(screen.getByRole('table'));
  await user.click(table.getByRole('button', { name: 'Lenkungskreis' }));
  expect(screen.getByRole('heading', { name: 'Lenkungskreis' })).toBeInTheDocument();
  expect(screen.getByRole('table')).toHaveTextContent('Lenkungskreis – Planungsstand');
});
it.each(['Listenansicht', 'Kachelansicht'])(
  'zeigt leere Themen in %s ohne falschen Upload',
  (name) => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name }));
    fireEvent.click(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }));
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Leer' } });
    fireEvent.click(screen.getByRole('button', { name: 'OK' }));
    expect(screen.getByText('Keine Dokumente in diesem Thema')).toBeInTheDocument();
  },
);
