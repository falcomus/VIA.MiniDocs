import { fireEvent, render, screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import { App } from '../../App';
import { validateTag } from './tagOperations';
import { demoTags } from './tags';
import { demoDocuments } from './demoDocuments';
import { demoTopics } from '../topics/demoTopics';
import { documentReducer } from './documentOperations';

function openManage() {
  fireEvent.click(screen.getByRole('button', { name: 'Einstellungen' }));
  fireEvent.click(screen.getByRole('button', { name: 'Tags verwalten' }));
}
function modal() {
  return within(screen.getByRole('dialog'));
}
function search(value: string) {
  fireEvent.change(screen.getByRole('searchbox', { name: 'Dokumente suchen' }), {
    target: { value },
  });
}
function openPicker(id: string) {
  const row = document.querySelector<HTMLElement>(`[data-document-id="${id}"]`)!;
  const button = within(row).getByRole('button', { name: 'Tags zuweisen' });
  button.focus();
  fireEvent.click(button);
}

it('legt einen Tag an, validiert Namen und bearbeitet Name, Beschreibung und Farbe', () => {
  render(<App />);
  openManage();
  fireEvent.click(modal().getByRole('button', { name: 'Neuer Tag' }));
  fireEvent.click(modal().getByRole('button', { name: 'Speichern' }));
  expect(modal().getByRole('alert')).toHaveTextContent('Namen');
  fireEvent.change(modal().getByLabelText('Name'), { target: { value: 'projekt' } });
  fireEvent.click(modal().getByRole('button', { name: 'Speichern' }));
  expect(modal().getByRole('alert')).toHaveTextContent('bereits vergeben');
  fireEvent.change(modal().getByLabelText('Name'), { target: { value: 'Wichtig' } });
  fireEvent.change(modal().getByLabelText('Beschreibung'), { target: { value: 'Priorität' } });
  fireEvent.change(modal().getByLabelText('Farbe'), { target: { value: '#123456' } });
  fireEvent.click(modal().getByRole('button', { name: 'Speichern' }));
  expect(modal().getByText('Priorität')).toBeInTheDocument();
  fireEvent.click(modal().getByRole('button', { name: 'Wichtig bearbeiten' }));
  fireEvent.change(modal().getByLabelText('Name'), { target: { value: 'Dringend' } });
  fireEvent.click(modal().getByRole('button', { name: 'Speichern' }));
  expect(modal().getByRole('button', { name: 'Dringend bearbeiten' })).toBeInTheDocument();
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
  expect(screen.getByRole('checkbox', { name: 'Dringend' })).toBeInTheDocument();
});
it('bewahrt die Tagidentität beim Umbenennen und zeigt die Zuordnung in beiden Ansichten', () => {
  render(<App />);
  openManage();
  fireEvent.click(modal().getByRole('button', { name: 'Projekt bearbeiten' }));
  fireEvent.change(modal().getByLabelText('Name'), { target: { value: 'Projekt Neu' } });
  fireEvent.click(modal().getByRole('button', { name: 'Speichern' }));
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!).getByText(
      'Projekt Neu',
    ),
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!).getByText(
      'Projekt Neu',
    ),
  ).toBeInTheDocument();
});
it('ordnet einem Dokument Tags zu und entfernt sie wieder, mit Fokus-Rückgabe', () => {
  render(<App />);
  openPicker('demo-1');
  expect(modal().getByRole('checkbox', { name: 'Projekt' })).toBeChecked();
  fireEvent.click(modal().getByRole('checkbox', { name: 'Projekt' }));
  fireEvent.click(modal().getByRole('checkbox', { name: 'Idee' }));
  fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
  const row = within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!);
  expect(row.queryByText('Projekt')).not.toBeInTheDocument();
  expect(row.getByText('Idee')).toBeInTheDocument();
  expect(row.getByRole('button', { name: 'Tags zuweisen' })).toHaveFocus();
});
it('weist bei gemischter Auswahl über Themen hinweg zu und entfernt bei vollständiger Auswahl', () => {
  render(<App />);
  search('schritte');
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  fireEvent.click(
    within(screen.getByLabelText('Dokumentauswahl')).getByRole('button', { name: 'Tags zuweisen' }),
  );
  const checkbox = modal().getByRole('checkbox', { name: 'Aufgabe' });
  expect(checkbox).toHaveProperty('indeterminate', true);
  fireEvent.click(checkbox);
  expect(checkbox).toBeChecked();
  fireEvent.click(checkbox);
  expect(checkbox).not.toBeChecked();
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!).queryByText(
      'Aufgabe',
    ),
  ).not.toBeInTheDocument();
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-3"]')!).queryByText(
      'Aufgabe',
    ),
  ).not.toBeInTheDocument();
});
it('entfernt ausgefilterte Dokumente aus der Auswahl und löscht Tags nur nach Bestätigung', () => {
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
  fireEvent.click(screen.getByRole('checkbox', { name: 'Projekt' }));
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  fireEvent.click(
    within(screen.getByLabelText('Dokumentauswahl')).getByRole('button', { name: 'Tags zuweisen' }),
  );
  fireEvent.click(modal().getByRole('checkbox', { name: 'Projekt' }));
  expect(modal().getByText(/Keine der ausgewählten Dokumente/)).toBeInTheDocument();
  fireEvent.click(modal().getByRole('button', { name: 'Tags verwalten …' }));
  fireEvent.click(modal().getByRole('button', { name: 'Projekt löschen' }));
  expect(modal().getByRole('alert')).toHaveTextContent('aktiven Filtern');
  fireEvent.click(modal().getByRole('button', { name: 'Endgültig löschen' }));
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  expect(screen.queryByRole('checkbox', { name: 'Projekt' })).not.toBeInTheDocument();
  expect(screen.queryByText('2 Dokumente ausgewählt')).not.toBeInTheDocument();
});
it('validiert Farbwerte und erhält Verschiebe-Undo bei gezielter Tagänderung', () => {
  expect(validateTag({ name: 'Test', description: '', color: 'red' }, demoTags)).toContain('Farbe');
  const initial = { documents: demoDocuments, selected: [], anchor: null, undo: null, message: '' };
  const moved = documentReducer(initial, {
    type: 'move',
    ids: ['demo-1'],
    target: 'beta',
    topics: demoTopics,
  });
  const tagged = documentReducer(moved, {
    type: 'tags',
    ids: ['demo-1'],
    tagId: 'idee',
    on: true,
    visibleAfter: ['demo-1'],
  });
  const undone = documentReducer(tagged, { type: 'undo', topics: demoTopics });
  expect(undone.documents.find((doc) => doc.id === 'demo-1')?.topicId).toBe('unassigned');
  expect(undone.documents.find((doc) => doc.id === 'demo-1')?.tagIds).toContain('idee');
});
it('löscht einen verwendeten Tag nach Bestätigung aus Dokumenten und aktivem Filter', () => {
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
  fireEvent.click(screen.getByRole('checkbox', { name: 'Projekt' }));
  openManage();
  fireEvent.click(modal().getByRole('button', { name: 'Projekt löschen' }));
  expect(modal().getByRole('alert')).toHaveTextContent('2 Dokumenten');
  fireEvent.click(modal().getByRole('button', { name: 'Endgültig löschen' }));
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  expect(screen.queryByRole('checkbox', { name: 'Projekt' })).not.toBeInTheDocument();
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!).queryByText(
      'Projekt',
    ),
  ).not.toBeInTheDocument();
  expect(screen.queryByText('Keine Treffer')).not.toBeInTheDocument();
});
it('erhält Undo nach Tagänderung bei aktiver themenübergreifender Suche', () => {
  render(<App />);
  search('schritte');
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const values = new Map<string, string>();
  const dataTransfer = {
    get types() {
      return [...values.keys()];
    },
    setData: (key: string, value: string) => values.set(key, value),
    getData: (key: string) => values.get(key) ?? '',
  };
  fireEvent.dragStart(document.querySelector('[data-document-id="demo-1"]')!, { dataTransfer });
  const target = within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
    name: 'Kunde Beta',
  }).parentElement!;
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  openPicker('demo-1');
  fireEvent.click(modal().getByRole('checkbox', { name: 'Idee' }));
  fireEvent.click(modal().getByRole('button', { name: 'Fertig' }));
  fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
  const row = within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!);
  expect(row.getByText('Nicht zugeordnet')).toBeInTheDocument();
  expect(row.getByText('Idee')).toBeInTheDocument();
});
