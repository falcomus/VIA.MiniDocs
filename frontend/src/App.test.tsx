import { render, screen } from '@testing-library/react';
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
