import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { App } from './App';

describe('App', () => {
  it('zeigt die nicht zugeordneten Demodokumente an', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));

    expect(screen.getByRole('heading', { level: 1, name: 'Nicht zugeordnet' })).toBeInTheDocument();
    expect(screen.getAllByRole('row')).toHaveLength(3); // Kopf + zwei Dokumente
  });

  it('zeigt Dokumente des gewählten Themas an', async () => {
    const user = userEvent.setup();
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));

    await user.click(screen.getByRole('button', { name: 'Lenkungskreis' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Lenkungskreis' })).toBeInTheDocument();
    expect(
      screen.getByText('Lenkungskreis – Planungsstand', { selector: 'strong' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Besprechungsnotizen – Eingang')).not.toBeInTheDocument();
  });
});

it('kennzeichnet vorbereitete Bedienelemente als deaktiviert', () => {
  render(<App />);
  expect(screen.getByRole('searchbox', { name: 'Dokumente suchen' })).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Hochladen' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Kachelansicht' })).toBeEnabled();
  expect(screen.getByRole('combobox', { name: 'Sortierung' })).toBeEnabled();
});

it('zeigt die Statistik aus den vorhandenen Demodaten', () => {
  render(<App />);
  expect(screen.getByText(/Dokumente ·/)).toHaveTextContent('6 Dokumente · 13 Seiten');
  expect(screen.getByText(/Daten nur im Browser/)).toBeInTheDocument();
});

it('legt ein Unterthema an und zeigt die leere Dokumentliste', async () => {
  const user = userEvent.setup();
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  await user.pointer({
    target: screen.getByRole('button', { name: 'Kunde Beta' }),
    keys: '[MouseRight]',
  });
  await user.click(screen.getByRole('menuitem', { name: 'Neues Unterthema' }));
  await user.type(screen.getByLabelText('Name'), 'Unterthema');
  await user.click(screen.getByRole('button', { name: 'OK' }));
  expect(screen.getByRole('heading', { level: 1, name: 'Unterthema' })).toBeInTheDocument();
  expect(screen.getByText('Keine Dokumente in diesem Thema')).toBeInTheDocument();
  await user.click(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Beta',
    }),
  );
  expect(
    screen.getByText('Notizen zur Zusammenarbeit', { selector: 'strong' }),
  ).toBeInTheDocument();
});

it('benennt ein Thema um und verhindert leere Namen', async () => {
  const user = userEvent.setup();
  render(<App />);
  await user.pointer({
    target: screen.getByRole('button', { name: 'Kunde Beta' }),
    keys: '[MouseRight]',
  });
  await user.click(screen.getByRole('menuitem', { name: 'Umbenennen' }));
  await user.clear(screen.getByLabelText('Name'));
  expect(screen.getByRole('button', { name: 'OK' })).toBeDisabled();
  await user.type(screen.getByLabelText('Name'), 'Kunde Gamma');
  await user.keyboard('{Enter}');
  expect(screen.getByRole('button', { name: 'Kunde Gamma' })).toBeInTheDocument();
});

it('verhindert das Löschen eines Teilbaums und erhält seine Dokumente', async () => {
  const user = userEvent.setup();
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  await user.click(screen.getByRole('button', { name: 'Lenkungskreis' }));
  await user.pointer({
    target: within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Alpha',
    }),
    keys: '[MouseRight]',
  });
  await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  expect(screen.getByRole('dialog')).toHaveTextContent('Unterthemen');
  expect(screen.getByRole('button', { name: 'Löschen' })).toBeDisabled();
  await user.click(screen.getByRole('button', { name: 'Abbrechen' }));
  expect(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Alpha',
    }),
  ).toBeInTheDocument();
  expect(
    screen.getByText('Lenkungskreis – Planungsstand', { selector: 'strong' }),
  ).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Projektplanung' }));
  expect(
    screen.getByText('Projektrahmen – Diskussion', { selector: 'strong' }),
  ).toBeInTheDocument();
});

it('schützt den Systemknoten und schließt Menü und Dialog mit Escape', async () => {
  const user = userEvent.setup();
  render(<App />);
  const system = screen.getByRole('button', { name: 'Nicht zugeordnet' });
  await user.pointer({ target: system, keys: '[MouseRight]' });
  expect(screen.getByRole('menuitem', { name: 'Alle Dokumente als ZIP' })).toBeDisabled();
  expect(screen.queryByRole('menuitem', { name: 'Löschen' })).not.toBeInTheDocument();
  await user.keyboard('{Escape}');
  expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  expect(system).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }));
  expect(screen.getByLabelText('Name')).toHaveFocus();
  await user.keyboard('{Escape}');
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' })).toHaveFocus();
});

it('verschiebt ein Thema vollständig per Tastatur und filtert ungültige Ziele', async () => {
  const user = userEvent.setup();
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  const alpha = tree.getByRole('button', { name: 'Kunde Alpha' });
  alpha.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.getByRole('menuitem', { name: 'Neues Unterthema' })).toHaveFocus();
  await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');
  const target = screen.getByRole('combobox', { name: 'Zielthema' });
  expect(target).toHaveFocus();
  expect(
    within(target).queryByRole('option', { name: /Nicht zugeordnet/ }),
  ).not.toBeInTheDocument();
  expect(within(target).queryByRole('option', { name: /Kunde Alpha/ })).not.toBeInTheDocument();
  expect(within(target).getByRole('option', { name: 'Oberste Ebene' })).toBeInTheDocument();
  await user.selectOptions(target, 'own-notes');
  await user.tab();
  await user.tab();
  await user.keyboard('{Enter}');
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.getByRole('status')).toHaveTextContent('Thema verschoben');
  const moved = tree.getByRole('button', { name: 'Kunde Alpha' });
  expect(moved.closest('ul')?.closest('li')).toHaveTextContent('Eigene Notizen');
  await user.click(moved);
  expect(screen.getByRole('heading', { level: 1, name: 'Kunde Alpha' })).toBeInTheDocument();
  expect(tree.getByRole('button', { name: 'Eigene Notizen' })).toBeInTheDocument();
});

it('bricht das Verschieben ohne Änderung ab und gibt den Fokus zurück', async () => {
  const user = userEvent.setup();
  render(<App />);
  const beta = screen.getByRole('button', { name: 'Kunde Beta' });
  beta.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  await user.click(screen.getByRole('menuitem', { name: 'Verschieben…' }));
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'own-notes');
  await user.keyboard('{Escape}');
  expect(beta).toHaveFocus();
  expect(beta.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
});

it('stellt nach dem Löschen eines leeren Unterthemas den Fokus auf sein Elternthema', async () => {
  const user = userEvent.setup();
  render(<App />);
  const parent = screen.getByRole('button', { name: 'Kunde Alpha' });
  parent.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  await user.click(screen.getByRole('menuitem', { name: 'Neues Unterthema' }));
  await user.type(screen.getByLabelText('Name'), 'Leeres Kind');
  await user.keyboard('{Enter}');
  const child = screen.getByRole('button', { name: 'Leeres Kind' });
  child.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  expect(screen.getByRole('button', { name: 'Abbrechen' })).toHaveFocus();
  await user.tab({ shift: true });
  expect(screen.getByRole('button', { name: 'Löschen' })).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Löschen' }));
  expect(parent).toHaveFocus();
  expect(child.isConnected).toBe(false);
  await user.click(screen.getByRole('button', { name: 'Lenkungskreis' }));
  expect(
    screen.getByRole('checkbox', { name: 'Lenkungskreis – Planungsstand auswählen' }),
  ).toBeInTheDocument();
});

it('bietet im Systemknoten ein fokussierbares Schließen und keine Änderungsaktionen', async () => {
  const user = userEvent.setup();
  render(<App />);
  const system = screen.getByRole('button', { name: 'Nicht zugeordnet' });
  system.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.getByRole('menuitem', { name: 'Menü schließen' })).toHaveFocus();
  expect(screen.queryByRole('menuitem', { name: 'Verschieben…' })).not.toBeInTheDocument();
  await user.keyboard('{Enter}');
  expect(system).toHaveFocus();
});

it('weist einen synthetischen ungültigen Drop ab und erhält die Hierarchie', () => {
  render(<App />);
  const alpha = screen.getByRole('button', { name: 'Kunde Alpha' });
  const target = screen.getByRole('button', { name: 'Lenkungskreis' });
  const dataTransfer = { setData: () => {}, getData: () => 'alpha' };
  fireEvent.dragStart(alpha.parentElement!, { dataTransfer });
  fireEvent.drop(target.parentElement!, { dataTransfer });
  expect(screen.getByRole('status')).toHaveTextContent('nicht dorthin verschoben');
  expect(alpha.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
  expect(screen.getByRole('button', { name: 'Lenkungskreis' })).toBeInTheDocument();
});

it('stellt nach dem Löschen eines Wurzelthemas den Fokus auf Nicht zugeordnet', async () => {
  const user = userEvent.setup();
  render(<App />);

  await user.click(screen.getByRole('button', { name: 'Neues Thema auf oberster Ebene' }));
  await user.type(screen.getByLabelText('Name'), 'Leere Wurzel');
  await user.keyboard('{Enter}');
  const root = screen.getByRole('button', { name: 'Leere Wurzel' });
  await user.click(root);
  await user.keyboard('{Shift>}{F10}{/Shift}');
  await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  await user.click(screen.getByRole('button', { name: 'Löschen' }));

  expect(root.isConnected).toBe(false);
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { level: 1, name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveFocus();
});

it('übernimmt eine gültige Themenverschiebung nach synthetischem Dragstart und Drop', () => {
  render(<App />);
  const source = screen.getByRole('button', { name: 'Kunde Alpha' }).parentElement!;
  const target = screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!;
  const dataTransfer = { setData: () => {}, getData: () => 'alpha' };
  fireEvent.dragStart(source, { dataTransfer });
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  const moved = screen.getByRole('button', { name: 'Kunde Alpha' });
  expect(moved.closest('ul')?.closest('li')).toHaveTextContent('Kunde Beta');
  expect(screen.getByRole('status')).toHaveTextContent('Thema verschoben.');
});

it('verschiebt über den Sidebar-Kopf auf die oberste Ebene und beendet die Sitzung', () => {
  render(<App />);
  const source = screen.getByRole('button', { name: 'Kunde Alpha' }).parentElement!;
  const root = screen.getByRole('button', {
    name: 'Neues Thema auf oberster Ebene',
  }).parentElement!;
  const dataTransfer = { setData: () => {}, getData: () => 'alpha' };
  fireEvent.dragStart(source, { dataTransfer });
  expect(fireEvent.dragOver(root, { dataTransfer })).toBe(false);
  fireEvent.drop(root, { dataTransfer });
  const moved = screen.getByRole('button', { name: 'Kunde Alpha' });
  expect(moved.closest('ul')?.parentElement).toBe(
    screen.getByRole('navigation', { name: 'Themen' }),
  );
  expect(screen.getByRole('status')).toHaveTextContent('Thema verschoben.');
  // Reusing the old payload must not move the topic back under another row.
  fireEvent.drop(screen.getByRole('button', { name: 'Kunde Beta' }).parentElement!, {
    dataTransfer,
  });
  expect(moved.closest('ul')?.parentElement).toBe(
    screen.getByRole('navigation', { name: 'Themen' }),
  );
  expect(screen.getByRole('status')).toHaveTextContent('nicht dorthin verschoben');
});

it.each(['mismatch', 'missing', 'no-start', 'escape'])(
  'weist am Sidebar-Kopf einen Drop bei %s ohne Hierarchieänderung zurück',
  (reason) => {
    render(<App />);
    const alpha = screen.getByRole('button', { name: 'Kunde Alpha' });
    const beta = screen.getByRole('button', { name: 'Kunde Beta' });
    const root = screen.getByRole('button', {
      name: 'Neues Thema auf oberster Ebene',
    }).parentElement!;
    const dataTransfer = {
      setData: () => {},
      getData: () => (reason === 'mismatch' ? 'beta' : reason === 'missing' ? '' : 'alpha'),
    };
    if (reason !== 'no-start') fireEvent.dragStart(alpha.parentElement!, { dataTransfer });
    if (reason === 'escape') fireEvent.keyDown(document, { key: 'Escape' });
    expect(fireEvent.dragOver(root, { dataTransfer })).toBe(
      reason === 'no-start' || reason === 'escape',
    );
    fireEvent.drop(root, { dataTransfer });
    expect(alpha.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
    expect(beta.closest('ul')?.closest('li')).toHaveTextContent('Kunden');
    expect(screen.getByRole('status')).toHaveTextContent('nicht dorthin verschoben');
    expect(fireEvent.dragOver(root, { dataTransfer })).toBe(true);
  },
);

it('startet mit aktiver Kachelansicht und den nicht zugeordneten Dokumenten', () => {
  render(<App />);
  expect(screen.getByRole('button', { name: 'Kachelansicht' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  expect(screen.getByRole('button', { name: 'Listenansicht' })).toHaveAttribute(
    'aria-pressed',
    'false',
  );
  const grid = within(screen.getByRole('list', { name: 'Kachelansicht' }));
  expect(grid.getAllByRole('listitem')).toHaveLength(2);
  expect(grid.getByTitle('Besprechungsnotizen – Eingang')).toBeInTheDocument();
  expect(grid.getByTitle('Offene Punkte – Entwurf')).toBeInTheDocument();
  expect(screen.queryByRole('table')).not.toBeInTheDocument();
});
