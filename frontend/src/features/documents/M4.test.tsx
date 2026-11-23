import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import topicStyles from '../topics/TopicTree.module.css';
import { App } from '../../App';
function checkbox(title = 'Besprechungsnotizen – Eingang') {
  return screen.getByRole('checkbox', { name: `${title} auswählen` });
}
function row(title = 'Besprechungsnotizen – Eingang') {
  return document.querySelector<HTMLElement>(
    `[data-document-id="${title.startsWith('Besprechungs') ? 'demo-1' : 'demo-2'}"]`,
  )!;
}
function transfer() {
  const data = new Map<string, string>();
  return {
    get types() {
      return [...data.keys()];
    },
    setData: (key: string, value: string) => data.set(key, value),
    getData: (key: string) => data.get(key) ?? '',
  };
}
function treeTarget() {
  return within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
    name: 'Kunde Beta',
  }).parentElement!;
}
it.each(['Kachelansicht', 'Listenansicht'])(
  'wählt per Klick, Ctrl, Meta, Shift und Checkbox in %s aus',
  (view) => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: view }));
    fireEvent.click(row());
    expect(checkbox()).toBeChecked();
    expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
    fireEvent.click(row('Offene Punkte – Entwurf'), { ctrlKey: true });
    expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
    fireEvent.click(row(), { metaKey: true });
    expect(checkbox()).not.toBeChecked();
    fireEvent.click(row('Offene Punkte – Entwurf'));
    fireEvent.click(row(), { shiftKey: true });
    expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
    fireEvent.click(checkbox());
    expect(checkbox()).not.toBeChecked();
    fireEvent.click(screen.getByRole('button', { name: 'Auswahl aufheben' }));
    expect(screen.queryByText(/Dokumente? ausgewählt/)).not.toBeInTheDocument();
  },
);
it('behält Alle-Auswahl beim Ansichtwechsel und bereinigt sie beim Themenwechsel', () => {
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  expect(checkbox()).toBeChecked();
  expect(screen.getByRole('checkbox', { name: 'Alle auswählen' })).toBeChecked();
  fireEvent.click(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Beta',
    }),
  );
  expect(screen.queryByText(/Dokumente? ausgewählt/)).not.toBeInTheDocument();
  expect(checkbox('Notizen zur Zusammenarbeit')).not.toBeChecked();
});
it('sortiert die sichtbare Reihenfolge und nutzt sie für Shift-Auswahl', () => {
  render(<App />);
  fireEvent.click(row());
  fireEvent.change(screen.getByRole('combobox', { name: 'Sortierung' }), {
    target: { value: 'date-asc' },
  });
  const items = screen
    .getAllByRole('listitem')
    .filter((item) => item.hasAttribute('data-document-id'));
  expect(items.map((item) => item.dataset.documentId)).toEqual(['demo-2', 'demo-1']);
  fireEvent.click(row('Offene Punkte – Entwurf'), { shiftKey: true });
  expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
  fireEvent.change(screen.getByRole('combobox', { name: 'Sortierung' }), {
    target: { value: 'title' },
  });
  expect(
    screen
      .getAllByRole('listitem')
      .filter((item) => item.hasAttribute('data-document-id'))
      .map((item) => item.dataset.documentId),
  ).toEqual(['demo-1', 'demo-2']);
});
it('begrenzt Kachelzoom und deaktiviert ihn für die Liste', () => {
  render(<App />);
  const zoom = screen.getByRole('slider', { name: 'Zoom' });
  expect(zoom).toHaveAttribute('min', '110');
  expect(zoom).toHaveAttribute('max', '380');
  expect(zoom).toHaveAttribute('step', '10');
  fireEvent.change(zoom, { target: { value: '380' } });
  expect(screen.getByRole('list', { name: 'Kachelansicht' })).toHaveStyle('--tile: 380px');
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  expect(zoom).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Kachelansicht' }));
  expect(zoom).toHaveValue('380');
});
it('verschiebt per Tastaturdialog, gibt Fokus zurück und macht die Mehrfachverschiebung rückgängig', async () => {
  const user = userEvent.setup();
  render(<App />);
  await user.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
  expect(screen.getByRole('combobox', { name: 'Zielthema' })).toHaveFocus();
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'beta');
  await user.click(screen.getByRole('button', { name: 'Verschieben' }));
  expect(screen.getByRole('button', { name: 'Alle zugeordnet' })).toHaveFocus();
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Rückgängig' }));
  expect(checkbox()).toBeInTheDocument();
  expect(checkbox('Offene Punkte – Entwurf')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
});
it('bricht den Dokumentdialog mit Escape ohne Datenänderung ab', async () => {
  const user = userEvent.setup();
  render(<App />);
  await user.click(checkbox());
  const trigger = screen.getByRole('button', { name: 'Verschieben nach …' });
  await user.click(trigger);
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'beta');
  await user.keyboard('{Escape}');
  expect(trigger).toHaveFocus();
  expect(checkbox()).toBeChecked();
  expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
});
it('verschiebt die ausgewählte Gruppe per synthetischem Drop auf den Baum und stellt sie per Undo wieder her', () => {
  render(<App />);
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  fireEvent.dragOver(treeTarget(), { dataTransfer });
  fireEvent.drop(treeTarget(), { dataTransfer });
  expect(screen.getByRole('status', { name: 'Statusmeldung' })).toHaveTextContent(
    '2 Dokumente verschoben',
  );
  expect(screen.getByRole('heading', { name: 'Kunde Beta' })).toBeInTheDocument();
  expect(checkbox()).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
  expect(
    screen.queryByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
  ).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Nicht zugeordnet' }));
  expect(checkbox()).toBeInTheDocument();
});
it.each(['mismatch', 'no-start', 'escape', 'mixed'])(
  'weist widersprüchlichen Dokument-Drop %s ohne Zustandsänderung ab',
  (reason) => {
    render(<App />);
    const dataTransfer = transfer();
    if (reason !== 'no-start') fireEvent.dragStart(row(), { dataTransfer });
    else dataTransfer.setData('application/x-minidoc-docs', '["demo-1"]');
    if (reason === 'mismatch') dataTransfer.setData('application/x-minidoc-docs', '["demo-2"]');
    if (reason === 'mixed') dataTransfer.setData('application/x-minidoc-topic', 'alpha');
    if (reason === 'escape') fireEvent.keyDown(document, { key: 'Escape' });
    fireEvent.drop(treeTarget(), { dataTransfer });
    expect(checkbox()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
  },
);
it.each(['Listenansicht', 'Kachelansicht'])(
  'verschiebt per synthetischem Drop auf einen direkten Ordner in %s',
  async (view) => {
    const user = userEvent.setup();
    render(<App />);
    await user.click(checkbox());
    await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'customers');
    await user.click(screen.getByRole('button', { name: 'Verschieben' }));
    await user.click(
      within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
        name: 'Kunden',
      }),
    );
    await user.click(screen.getByRole('button', { name: view }));
    const container =
      view === 'Listenansicht'
        ? screen.getByRole('table')
        : screen.getByRole('list', { name: 'Kachelansicht' });
    const folder = within(container)
      .getByRole('button', { name: /Kunde Beta/ })
      .closest(view === 'Listenansicht' ? 'tr' : 'li')!;
    const dataTransfer = transfer();
    fireEvent.dragStart(row(), { dataTransfer });
    fireEvent.dragOver(folder, { dataTransfer });
    fireEvent.drop(folder, { dataTransfer });
    expect(screen.getByRole('heading', { name: 'Kunde Beta' })).toBeInTheDocument();
    expect(checkbox()).toBeInTheDocument();
    await user.click(
      within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
        name: 'Kunde Beta',
      }),
    );
    expect(checkbox()).toBeInTheDocument();
    expect(checkbox('Notizen zur Zusammenarbeit')).toBeInTheDocument();
  },
);
it('ordnet ein einzelnes Dokument dem Systemknoten zu und belässt andere Dokumente im Ursprung', () => {
  render(<App />);
  fireEvent.click(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Beta',
    }),
  );
  const source = document.querySelector<HTMLElement>('[data-document-id="demo-5"]')!;
  const dataTransfer = transfer();
  fireEvent.dragStart(source, { dataTransfer });
  const target = screen.getByRole('button', { name: 'Nicht zugeordnet' }).parentElement!;
  fireEvent.drop(target, { dataTransfer });
  fireEvent.click(screen.getByRole('button', { name: 'Nicht zugeordnet' }));
  expect(checkbox('Notizen zur Zusammenarbeit')).toBeInTheDocument();
  expect(checkbox()).toBeInTheDocument();
});
it('ersetzt beim Dragstart eines nicht ausgewählten Dokuments die bisherige Auswahl', () => {
  render(<App />);
  fireEvent.click(checkbox());
  const dataTransfer = transfer();
  fireEvent.dragStart(row('Offene Punkte – Entwurf'), { dataTransfer });
  expect(checkbox()).not.toBeChecked();
  expect(checkbox('Offene Punkte – Entwurf')).toBeChecked();
  fireEvent.drop(treeTarget(), { dataTransfer });
  expect(screen.getByRole('heading', { name: 'Kunde Beta' })).toBeInTheDocument();
  expect(checkbox('Offene Punkte – Entwurf')).toBeInTheDocument();
  expect(
    screen.queryByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
  ).not.toBeInTheDocument();
});
it('verhindert Dokumentverschiebung nach zwischenzeitlichem Themen-Dragstart', () => {
  render(<App />);
  const docs = transfer();
  fireEvent.dragStart(row(), { dataTransfer: docs });
  const topic = transfer();
  fireEvent.dragStart(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Kunde Alpha',
    }).parentElement!,
    { dataTransfer: topic },
  );
  fireEvent.drop(treeTarget(), { dataTransfer: docs });
  expect(checkbox()).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
});
it.each(['dragend', 'blur', 'outside-drop'])(
  'verhindert Dokumentverschiebung nach %s',
  (reason) => {
    render(<App />);
    const dataTransfer = transfer();
    fireEvent.dragStart(row(), { dataTransfer });
    fireEvent.dragOver(treeTarget(), { dataTransfer });
    expect(treeTarget()).toHaveClass(topicStyles.drop);
    if (reason === 'dragend') fireEvent.dragEnd(row());
    if (reason === 'blur') fireEvent.blur(window);
    if (reason === 'outside-drop') fireEvent.drop(document.body);
    expect(treeTarget()).not.toHaveClass(topicStyles.drop);
    fireEvent.drop(treeTarget(), { dataTransfer });
    expect(checkbox()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
  },
);
it('wählt Dokumente per Leertaste aus und setzt Escape und Ctrl+A im Inhaltsbereich um', () => {
  render(<App />);
  row().focus();
  fireEvent.keyDown(row(), { key: ' ' });
  expect(checkbox()).toBeChecked();
  fireEvent.keyDown(row(), { key: 'a', ctrlKey: true });
  expect(checkbox('Offene Punkte – Entwurf')).toBeChecked();
  fireEvent.keyDown(row(), { key: 'Escape' });
  expect(checkbox()).not.toBeChecked();
  expect(checkbox('Offene Punkte – Entwurf')).not.toBeChecked();
});
it('liest Dokument-Nutzdaten erst beim Drop und entfernt globale Listener beim Unmount', () => {
  const add = vi.spyOn(document, 'addEventListener'),
    remove = vi.spyOn(document, 'removeEventListener');
  try {
    const { unmount } = render(<App />);
    const listeners = add.mock.calls.filter(([type]) =>
      ['dragstart', 'drop', 'dragend', 'keydown'].includes(type),
    );
    const dataTransfer = transfer();
    const get = vi.spyOn(dataTransfer, 'getData');
    fireEvent.dragStart(row(), { dataTransfer });
    fireEvent.dragOver(treeTarget(), { dataTransfer });
    expect(get).not.toHaveBeenCalled();
    unmount();
    expect(listeners.some(([type, , capture]) => type === 'dragstart' && capture === true)).toBe(
      true,
    );
    for (const [type, listener, capture] of listeners) {
      if (capture === undefined) expect(remove).toHaveBeenCalledWith(type, listener);
      else expect(remove).toHaveBeenCalledWith(type, listener, capture);
    }
  } finally {
    vi.restoreAllMocks();
  }
});
it('sortiert per Tabellenkopf und erhält Zoom am unteren Grenzwert beim Ansichtwechsel', () => {
  render(<App />);
  const zoom = screen.getByRole('slider', { name: 'Zoom' });
  fireEvent.change(zoom, { target: { value: '110' } });
  expect(screen.getByRole('list', { name: 'Kachelansicht' })).toHaveStyle('--tile: 110px');
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  const table = within(screen.getByRole('table'));
  fireEvent.click(table.getByRole('button', { name: 'Datum' }));
  expect(screen.getByRole('combobox', { name: 'Sortierung' })).toHaveValue('date-asc');
  fireEvent.click(table.getByRole('button', { name: 'Titel' }));
  expect(screen.getByRole('combobox', { name: 'Sortierung' })).toHaveValue('title');
  fireEvent.click(screen.getByRole('button', { name: 'Kachelansicht' }));
  expect(zoom).toHaveValue('110');
});
it('verschiebt mehrere Dokumente gezielt in ein verschachteltes Baumthema und stellt Zähler und Herkunft per Undo wieder her', () => {
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  const count = (name: string) =>
    tree.getByRole('button', { name }).getAttribute('aria-description');
  expect(count('Nicht zugeordnet')).toBe('2 Dokumente einschließlich Unterthemen');
  expect(count('Kunden')).toBe('3 Dokumente einschließlich Unterthemen');
  expect(count('Kunde Alpha')).toBe('2 Dokumente einschließlich Unterthemen');
  expect(count('Lenkungskreis')).toBe('1 Dokument einschließlich Unterthemen');
  expect(tree.getByRole('button', { name: 'Lenkungskreis' })).not.toHaveAttribute('title');
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  const target = tree.getByRole('button', { name: 'Lenkungskreis' }).parentElement!;
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  expect(count('Alle zugeordnet')).toBe('0 Dokumente einschließlich Unterthemen');
  expect(count('Kunden')).toBe('5 Dokumente einschließlich Unterthemen');
  expect(count('Kunde Alpha')).toBe('4 Dokumente einschließlich Unterthemen');
  expect(count('Lenkungskreis')).toBe('3 Dokumente einschließlich Unterthemen');
  expect(screen.getByRole('status', { name: 'Statusmeldung' })).toHaveTextContent(
    '2 Dokumente verschoben.',
  );
  fireEvent.click(tree.getByRole('button', { name: 'Kunde Alpha' }));
  expect(screen.getByRole('status', { name: 'Statusmeldung' })).toBeEmptyDOMElement();
  expect(screen.queryByText('2 Dokumente verschoben.')).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Rückgängig' })).toBeInTheDocument();
  expect(
    screen.queryByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
  ).not.toBeInTheDocument();
  fireEvent.click(tree.getByRole('button', { name: 'Lenkungskreis' }));
  expect(checkbox()).toBeInTheDocument();
  expect(checkbox('Offene Punkte – Entwurf')).toBeInTheDocument();
  expect(checkbox('Lenkungskreis – Planungsstand')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
  expect(count('Kunden')).toBe('3 Dokumente einschließlich Unterthemen');
  expect(count('Kunde Alpha')).toBe('2 Dokumente einschließlich Unterthemen');
  expect(count('Lenkungskreis')).toBe('1 Dokument einschließlich Unterthemen');
  expect(count('Nicht zugeordnet')).toBe('2 Dokumente einschließlich Unterthemen');
  expect(
    screen.queryByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
  ).not.toBeInTheDocument();
  expect(checkbox('Lenkungskreis – Planungsstand')).toBeInTheDocument();
  fireEvent.click(tree.getByRole('button', { name: 'Nicht zugeordnet' }));
  expect(checkbox()).toBeInTheDocument();
  expect(checkbox('Offene Punkte – Entwurf')).toBeInTheDocument();
  expect(screen.getByText(/Dokumente ·/)).toHaveTextContent('6 Dokumente · 13 Seiten');
});
it.each(['Listenansicht', 'Kachelansicht'])(
  'ordnet beim Drop auf einen Unterordner in %s das genaue Kind zu und stellt es wieder her',
  async (view) => {
    const user = userEvent.setup();
    render(<App />);
    const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
    await user.click(checkbox());
    await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'alpha');
    await user.click(screen.getByRole('button', { name: 'Verschieben' }));
    await user.click(tree.getByRole('button', { name: 'Kunde Alpha' }));
    await user.click(screen.getByRole('button', { name: view }));
    const area = within(
      view === 'Listenansicht'
        ? screen.getByRole('table')
        : screen.getByRole('list', { name: 'Kachelansicht' }),
    );
    const folder = area
      .getByRole('button', { name: /Lenkungskreis/ })
      .closest(view === 'Listenansicht' ? 'tr' : 'li')!;
    if (view === 'Kachelansicht') expect(folder).toHaveTextContent('1 Dokument');
    const dataTransfer = transfer();
    fireEvent.dragStart(row(), { dataTransfer });
    fireEvent.dragOver(folder, { dataTransfer });
    fireEvent.drop(folder, { dataTransfer });
    expect(tree.getByRole('button', { name: 'Kunde Alpha' })).toHaveAttribute(
      'aria-description',
      '3 Dokumente einschließlich Unterthemen',
    );
    expect(tree.getByRole('button', { name: 'Lenkungskreis' })).toHaveAttribute(
      'aria-description',
      '2 Dokumente einschließlich Unterthemen',
    );
    expect(screen.getByRole('heading', { name: 'Lenkungskreis' })).toBeInTheDocument();
    expect(tree.getByRole('button', { name: 'Lenkungskreis' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(
      within(screen.getByRole('main')).getByRole('button', { name: 'Kunde Alpha' }),
    ).toBeInTheDocument();
    expect(checkbox()).toBeInTheDocument();
    expect(checkbox('Lenkungskreis – Planungsstand')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Rückgängig' }));
    expect(tree.getByRole('button', { name: 'Lenkungskreis' })).toHaveAttribute(
      'aria-description',
      '1 Dokument einschließlich Unterthemen',
    );
    expect(
      screen.queryByRole('checkbox', { name: 'Besprechungsnotizen – Eingang auswählen' }),
    ).not.toBeInTheDocument();
    await user.click(tree.getByRole('button', { name: 'Kunde Alpha' }));
    expect(checkbox()).toBeInTheDocument();
    expect(screen.getByText(/Dokumente ·/)).toHaveTextContent('6 Dokumente · 13 Seiten');
  },
);
it('klappt die Vorfahren eines Ordnerziels nach dem Drop auf und synchronisiert Breadcrumb und Baum', async () => {
  const user = userEvent.setup();
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  await user.click(checkbox());
  await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'alpha');
  await user.click(screen.getByRole('button', { name: 'Verschieben' }));
  await user.click(tree.getByRole('button', { name: 'Kunde Alpha' }));
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' }));
  const folder = within(screen.getByRole('list', { name: 'Kachelansicht' }))
    .getByRole('button', { name: /Lenkungskreis/ })
    .closest('li')!;
  const dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  fireEvent.drop(folder, { dataTransfer });
  expect(screen.getByRole('heading', { name: 'Lenkungskreis' })).toBeInTheDocument();
  expect(tree.getByRole('button', { name: 'Lenkungskreis' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' })).toHaveAttribute(
    'aria-expanded',
    'true',
  );
  expect(checkbox()).toBeInTheDocument();
  expect(screen.getByRole('main')).toHaveTextContent('Kunden');
  expect(screen.getByRole('main')).toHaveTextContent('Kunde Alpha');
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' }));
  expect(tree.queryByRole('button', { name: 'Lenkungskreis' })).not.toBeInTheDocument();
});
it('navigiert bei einem unveränderten oder ungültigen Drop nicht und erhält die Themenwahl', () => {
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  let dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  fireEvent.drop(tree.getByRole('button', { name: 'Nicht zugeordnet' }).parentElement!, {
    dataTransfer,
  });
  expect(screen.getByRole('heading', { name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(screen.queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument();
  dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  dataTransfer.setData('application/x-minidoc-docs', '["falsch"]');
  fireEvent.drop(treeTarget(), { dataTransfer });
  expect(screen.getByRole('heading', { name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(checkbox()).toBeInTheDocument();
});
it('zeigt nach Verschieben des letzten Dokuments den positiven Systemstatus und stellt ihn per Undo zurück', async () => {
  const user = userEvent.setup();
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-description',
    '2 Dokumente einschließlich Unterthemen',
  );
  await user.click(checkbox());
  await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'beta');
  await user.click(screen.getByRole('button', { name: 'Verschieben' }));
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-description',
    '1 Dokument einschließlich Unterthemen',
  );
  await user.click(checkbox('Offene Punkte – Entwurf'));
  await user.click(screen.getByRole('button', { name: 'Verschieben nach …' }));
  await user.selectOptions(screen.getByRole('combobox', { name: 'Zielthema' }), 'beta');
  await user.click(screen.getByRole('button', { name: 'Verschieben' }));
  const complete = tree.getByRole('button', { name: 'Alle zugeordnet' });
  expect(complete).toHaveAttribute('data-topic-id', 'unassigned');
  expect(complete).toHaveAttribute('aria-current', 'page');
  expect(complete).toHaveTextContent(/^Alle zugeordnet$/);
  expect(within(complete).getByText('Alle zugeordnet')).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Alle zugeordnet' })).toBeInTheDocument();
  complete.focus();
  await user.keyboard('{Shift>}{F10}{/Shift}');
  expect(screen.queryByRole('menuitem', { name: 'Löschen' })).not.toBeInTheDocument();
  await user.keyboard('{Escape}');
  expect(complete).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Rückgängig' }));
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(screen.getByRole('heading', { name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(checkbox('Offene Punkte – Entwurf')).toBeInTheDocument();
});
it('nimmt Dokument-Drop auf Alle zugeordnet an und kehrt beim Undo zum positiven Zustand zurück', () => {
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const first = transfer();
  fireEvent.dragStart(row(), { dataTransfer: first });
  fireEvent.drop(treeTarget(), { dataTransfer: first });
  const complete = tree.getByRole('button', { name: 'Alle zugeordnet' });
  expect(complete).toHaveAttribute('data-topic-id', 'unassigned');
  const second = transfer();
  fireEvent.dragStart(row(), { dataTransfer: second });
  fireEvent.dragOver(complete.parentElement!, { dataTransfer: second });
  fireEvent.drop(complete.parentElement!, { dataTransfer: second });
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(screen.getByRole('heading', { name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(checkbox()).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
  expect(tree.getByRole('button', { name: 'Alle zugeordnet' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(screen.getByRole('heading', { name: 'Alle zugeordnet' })).toBeInTheDocument();
});
it('kehrt nach Themenlöschung mit Dokumenten automatisch zu Nicht zugeordnet zurück', async () => {
  const user = userEvent.setup();
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  await user.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const dataTransfer = transfer();
  fireEvent.dragStart(row(), { dataTransfer });
  fireEvent.drop(treeTarget(), { dataTransfer });
  expect(tree.getByRole('button', { name: 'Alle zugeordnet' })).toBeInTheDocument();
  await user.pointer({
    target: tree.getByRole('button', { name: 'Kunde Beta' }),
    keys: '[MouseRight]',
  });
  await user.click(screen.getByRole('menuitem', { name: 'Löschen' }));
  await user.click(screen.getByRole('button', { name: 'Löschen' }));
  expect(tree.getByRole('button', { name: 'Nicht zugeordnet' })).toHaveAttribute(
    'aria-description',
    '3 Dokumente einschließlich Unterthemen',
  );
  await user.click(tree.getByRole('button', { name: 'Nicht zugeordnet' }));
  expect(screen.getByRole('heading', { name: 'Nicht zugeordnet' })).toBeInTheDocument();
  expect(checkbox()).toBeInTheDocument();
});
it('öffnet nach Drop-Navigation und manuellem Einklappen ein neu angelegtes Unterthema dauerhaft sichtbar', async () => {
  const user = userEvent.setup();
  render(<App />);
  const tree = within(screen.getByRole('navigation', { name: 'Themen' }));
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' }));
  const first = transfer();
  fireEvent.dragStart(row(), { dataTransfer: first });
  const alpha = within(screen.getByRole('main'));
  fireEvent.drop(tree.getByRole('button', { name: 'Kunden' }).parentElement!, {
    dataTransfer: first,
  });
  const folder = alpha.getByRole('button', { name: /Kunde Alpha/ }).closest('li')!;
  const second = transfer();
  fireEvent.dragStart(row(), { dataTransfer: second });
  fireEvent.drop(folder, { dataTransfer: second });
  expect(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' })).toHaveAttribute(
    'aria-expanded',
    'true',
  );
  expect(tree.getByRole('button', { name: 'Kunde Alpha' })).toHaveAttribute('aria-current', 'page');
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' }));
  expect(tree.queryByRole('button', { name: 'Kunde Alpha' })).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Listenansicht' }));
  expect(tree.queryByRole('button', { name: 'Kunde Alpha' })).not.toBeInTheDocument();
  await user.pointer({
    target: tree.getByRole('button', { name: 'Kunden' }),
    keys: '[MouseRight]',
  });
  await user.click(screen.getByRole('menuitem', { name: 'Neues Unterthema' }));
  await user.type(screen.getByLabelText('Name'), 'Neues Q7-Thema');
  await user.click(screen.getByRole('button', { name: 'OK' }));
  expect(tree.getByRole('button', { name: 'Neues Q7-Thema' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' })).toHaveAttribute(
    'aria-expanded',
    'true',
  );
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen einklappen' }));
  expect(tree.queryByRole('button', { name: 'Neues Q7-Thema' })).not.toBeInTheDocument();
  await user.click(tree.getByRole('button', { name: 'Kunden: Unterthemen aufklappen' }));
  expect(tree.getByRole('button', { name: 'Neues Q7-Thema' })).toBeInTheDocument();
});
