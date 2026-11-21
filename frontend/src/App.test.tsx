import { render, screen, within } from '@testing-library/react';
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
