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
