import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { App } from './App';

describe('App', () => {
  it('zeigt die nicht zugeordneten Demodokumente an', () => {
    render(<App />);

    expect(screen.getByRole('heading', { level: 1, name: 'Nicht zugeordnet' })).toBeInTheDocument();
    expect(screen.getAllByRole('row')).toHaveLength(3); // Kopf + zwei Dokumente
  });

  it('zeigt Dokumente des gewählten Themas an', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('button', { name: 'Lenkungskreis' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Lenkungskreis' })).toBeInTheDocument();
    expect(screen.getByText('Lenkungskreis – Planungsstand')).toBeInTheDocument();
    expect(screen.queryByText('Besprechungsnotizen – Eingang')).not.toBeInTheDocument();
  });
});

it('kennzeichnet vorbereitete Bedienelemente als deaktiviert', () => {
  render(<App />);
  expect(screen.getByRole('searchbox', { name: 'Dokumente suchen' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Hochladen' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Kachelansicht' })).toBeDisabled();
  expect(screen.getByRole('combobox', { name: 'Sortierung' })).toBeDisabled();
});

it('zeigt die Statistik aus den vorhandenen Demodaten', () => {
  render(<App />);
  expect(screen.getByText(/Dokumente ·/)).toBeInTheDocument();
  expect(screen.getByText(/Daten nur im Browser/)).toBeInTheDocument();
});

it('legt ein Unterthema an und zeigt die leere Dokumentliste', async () => {
  const user = userEvent.setup(); render(<App />);
  await user.pointer({ target: screen.getByRole('button', { name: 'Kunde Beta' }), keys: '[MouseRight]' });
  await user.click(screen.getByRole('menuitem', { name: 'Neues Unterthema' }));
  await user.type(screen.getByLabelText('Name'), 'Unterthema');
  await user.click(screen.getByRole('button', { name: 'OK' }));
  expect(screen.getByRole('heading', { level: 1, name: 'Unterthema' })).toBeInTheDocument();
  expect(screen.getByText('Keine Dokumente in diesem Thema')).toBeInTheDocument();
  await user.click(within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', { name: 'Kunde Beta' }));
  expect(screen.getByText('Notizen zur Zusammenarbeit')).toBeInTheDocument();
});

it('benennt ein Thema um und verhindert leere Namen', async () => {
  const user = userEvent.setup(); render(<App />);
  await user.pointer({ target: screen.getByRole('button', { name: 'Kunde Beta' }), keys: '[MouseRight]' });
  await user.click(screen.getByRole('menuitem', { name: 'Umbenennen' }));
  await user.clear(screen.getByLabelText('Name'));
  expect(screen.getByRole('button', { name: 'OK' })).toBeDisabled();
  await user.type(screen.getByLabelText('Name'), 'Kunde Gamma'); await user.keyboard('{Enter}');
  expect(screen.getByRole('button', { name: 'Kunde Gamma' })).toBeInTheDocument();
});

it('löscht Teilbäume und ordnet ihre Dokumente dem Systemknoten zu', async () => {
  const user = userEvent.setup(); render(<App />);
  await user.click(screen.getByRole('button', { name: 'Lenkungskreis' }));
  await user.pointer({ target: within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', { name: 'Kunde Alpha' }), keys: '[MouseRight]' });
  await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  expect(screen.getByRole('dialog')).toHaveTextContent('2 Dokumente werden nach „Nicht zugeordnet“ verschoben');
  await user.click(screen.getByRole('button', { name: 'Löschen' }));
  expect(screen.queryByRole('button', { name: 'Kunde Alpha' })).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { level: 1, name: 'Kunden' })).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Nicht zugeordnet' }));
  expect(screen.getByText('Lenkungskreis – Planungsstand')).toBeInTheDocument();
  expect(screen.getByText('Projektrahmen – Diskussion')).toBeInTheDocument();
});

it('schützt den Systemknoten und schließt Menü und Dialog mit Escape', async () => {
  const user = userEvent.setup(); render(<App />);
  const system = screen.getByRole('button', { name: 'Nicht zugeordnet' });
  await user.pointer({ target: system, keys: '[MouseRight]' });
  expect(screen.getByRole('menuitem', { name: 'Alle Dokumente als ZIP' })).toBeDisabled();
  expect(screen.queryByRole('menuitem', { name: 'Löschen' })).not.toBeInTheDocument();
  await user.keyboard('{Escape}'); expect(screen.queryByRole('menu')).not.toBeInTheDocument(); expect(system).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }));
  expect(screen.getByLabelText('Name')).toHaveFocus(); await user.keyboard('{Escape}');
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' })).toHaveFocus();
});

it('verschiebt ein Thema vollständig per Tastatur und filtert ungültige Ziele', async () => {
  const user = userEvent.setup(); render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  const alpha = tree.getByRole('button', { name: 'Kunde Alpha' }); alpha.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.getByRole('menuitem', { name: 'Neues Unterthema' })).toHaveFocus();
  await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');
  const target = screen.getByRole('combobox', { name: 'Zielthema' });
  expect(target).toHaveFocus();
  expect(within(target).queryByRole('option', { name: /Nicht zugeordnet/ })).not.toBeInTheDocument();
  expect(within(target).queryByRole('option', { name: /Kunde Alpha/ })).not.toBeInTheDocument();
  expect(within(target).getByRole('option', { name: 'Oberste Ebene' })).toBeInTheDocument();
  await user.selectOptions(target, 'own-notes');
  await user.tab(); await user.tab(); await user.keyboard('{Enter}');
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.getByRole('status')).toHaveTextContent('Thema verschoben');
  const moved = tree.getByRole('button', { name: 'Kunde Alpha' });
  expect(moved.closest('ul')?.closest('li')).toHaveTextContent('Eigene Notizen');
  await user.click(moved);
  expect(screen.getByRole('heading', { level: 1, name: 'Kunde Alpha' })).toBeInTheDocument();
  expect(tree.getByRole('button', { name: 'Eigene Notizen' })).toBeInTheDocument();
});

it('bricht das Verschieben ohne Änderung ab und gibt den Fokus zurück', async () => {
  const user = userEvent.setup(); render(<App />);
  const beta = screen.getByRole('button', { name: 'Kunde Beta' }); beta.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  await user.click(screen.getByRole('menuitem', { name: 'Verschieben…' }));
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'own-notes');
  await user.keyboard('{Escape}');
  expect(beta).toHaveFocus();
  expect(beta.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
});

it('stellt nach dem Löschen den Fokus auf ein bestehendes Elternthema', async () => {
  const user = userEvent.setup(); render(<App />);
  const alpha = screen.getByRole('button', { name: 'Kunde Alpha' }); alpha.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}'); await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  expect(screen.getByRole('button', { name: 'Abbrechen' })).toHaveFocus();
  await user.tab({ shift: true }); expect(screen.getByRole('button', { name: 'Löschen' })).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Löschen' }));
  expect(screen.getByRole('button', { name: 'Kunden' })).toHaveFocus();
  expect(alpha.isConnected).toBe(false);
});

it('bietet im Systemknoten ein fokussierbares Schließen und keine Änderungsaktionen', async () => {
  const user = userEvent.setup(); render(<App />);
  const system = screen.getByRole('button', { name: 'Nicht zugeordnet' }); system.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.getByRole('menuitem', { name: 'Menü schließen' })).toHaveFocus();
  expect(screen.queryByRole('menuitem', { name: 'Verschieben…' })).not.toBeInTheDocument();
  await user.keyboard('{Enter}'); expect(system).toHaveFocus();
});

it('weist einen ungültigen Maus-Drop im vollständigen Ablauf zurück', () => {
  render(<App />);
  const alpha = screen.getByRole('button', { name: 'Kunde Alpha' });
  const target = screen.getByRole('button', { name: 'Lenkungskreis' });
  fireEvent.drop(target.parentElement!, { dataTransfer: { types: ['application/x-minidoc-topic'], getData: () => 'alpha' } });
  expect(screen.getByRole('status')).toHaveTextContent('nicht dorthin verschoben');
  expect(alpha.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
  expect(screen.getByRole('button', { name: 'Lenkungskreis' })).toBeInTheDocument();
});
