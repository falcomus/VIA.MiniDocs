import { StrictMode } from 'react';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { applyTheme, readTheme, resolveTheme, THEME_KEY } from './theme';

let dark = false;
let listeners: Set<(event: MediaQueryListEvent) => void>;
let remove: ReturnType<typeof vi.fn>;
function setSystem(value: boolean) {
  dark = value;
  for (const fn of listeners) fn({ matches: value } as MediaQueryListEvent);
}
function open() {
  fireEvent.click(screen.getByRole('button', { name: 'Einstellungen' }));
}
function choose(name: string) {
  fireEvent.click(screen.getByRole('radio', { name }));
}
function theme() {
  return document.documentElement.dataset.theme;
}
beforeEach(() => {
  window.localStorage.clear();
  dark = false;
  listeners = new Set();
  remove = vi.fn((type: string, fn: (event: MediaQueryListEvent) => void) => {
    if (type === 'change') listeners.delete(fn);
  });
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: vi.fn(() => ({
      get matches() {
        return dark;
      },
      media: '(prefers-color-scheme: dark)',
      addEventListener: (type: string, fn: (event: MediaQueryListEvent) => void) => {
        if (type === 'change') listeners.add(fn);
      },
      removeEventListener: remove,
    })),
  });
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  window.localStorage.clear();
  delete document.documentElement.dataset.theme;
});
it('startet ohne Präferenz hell und öffnet Einstellungen statt direkt die Tagverwaltung', () => {
  render(<App />);
  expect(theme()).toBe('light');
  open();
  expect(screen.getByRole('radio', { name: 'Hell' })).toBeChecked();
  expect(screen.getByRole('radio', { name: 'Dunkel' })).not.toBeChecked();
  expect(screen.getByRole('radio', { name: 'System' })).not.toBeChecked();
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});
it('lädt gespeichertes Dunkel, speichert Auswahl und stellt sie beim Neustart wieder her', () => {
  localStorage.setItem(THEME_KEY, 'dark');
  const { unmount } = render(<App />);
  expect(theme()).toBe('dark');
  open();
  expect(screen.getByRole('radio', { name: 'Dunkel' })).toBeChecked();
  choose('Hell');
  expect(theme()).toBe('light');
  expect(localStorage.getItem(THEME_KEY)).toBe('light');
  unmount();
  render(<App />);
  expect(theme()).toBe('light');
  open();
  expect(screen.getByRole('radio', { name: 'Hell' })).toBeChecked();
});
it('berücksichtigt Systemmodus und Livewechsel, aber ignoriert OS-Wechsel bei expliziter Wahl', () => {
  dark = true;
  render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
  expect(theme()).toBe('light');
  open();
  choose('System');
  expect(theme()).toBe('dark');
  setSystem(false);
  expect(theme()).toBe('light');
  setSystem(true);
  expect(theme()).toBe('dark');
  choose('Hell');
  setSystem(true);
  expect(theme()).toBe('light');
  choose('Dunkel');
  setSystem(false);
  expect(theme()).toBe('dark');
  expect(listeners.size).toBe(0);
});
it('fällt bei ungültiger Präferenz oder nicht verfügbarem Speicher sicher auf Hell zurück', () => {
  localStorage.setItem(THEME_KEY, 'invalid');
  expect(readTheme()).toBe('light');
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  expect(readTheme()).toBe('light');
  render(<App />);
  expect(theme()).toBe('light');
  open();
  choose('Dunkel');
  expect(theme()).toBe('dark');
});
it('wendet dieselben Regeln beim frühen Bootstrap an', () => {
  expect(resolveTheme(readTheme(), true)).toBe('light');
  localStorage.setItem(THEME_KEY, 'system');
  applyTheme(resolveTheme(readTheme(), true));
  expect(theme()).toBe('dark');
});
it('schließt per Escape mit Fokus auf dem Zahnrad und per Außenklick ohne Fokuszwang', async () => {
  const user = userEvent.setup();
  render(<App />);
  const gear = screen.getByRole('button', { name: 'Einstellungen' });
  await user.click(gear);
  expect(screen.getByRole('radio', { name: 'Hell' })).toHaveFocus();
  await user.keyboard('{ArrowRight}');
  expect(screen.getByRole('radio', { name: 'Dunkel' })).toBeChecked();
  await user.keyboard('{Escape}');
  expect(gear).toHaveFocus();
  expect(gear).toHaveAttribute('aria-expanded', 'false');
  await user.click(gear);
  fireEvent.pointerDown(screen.getByRole('heading', { name: 'Nicht zugeordnet' }));
  expect(gear).toHaveAttribute('aria-expanded', 'false');
  expect(gear).not.toHaveFocus();
});
it('öffnet die bestehende Tagverwaltung und stellt Fokus auf das Zahnrad zurück', async () => {
  const user = userEvent.setup();
  render(<App />);
  const gear = screen.getByRole('button', { name: 'Einstellungen' });
  await user.click(gear);
  await user.click(screen.getByRole('button', { name: 'Tags verwalten' }));
  expect(screen.getByRole('dialog', { name: 'Tags verwalten' })).toBeInTheDocument();
  expect(gear).toHaveAttribute('aria-expanded', 'false');
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Fertig' }));
  expect(gear).toHaveFocus();
});
it('erhält Suche, Filter, Auswahl und Ansicht beim Theme-Wechsel', () => {
  render(<App />);
  fireEvent.change(screen.getByRole('searchbox', { name: 'Dokumente suchen' }), {
    target: { value: 'schritte' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Filter' }));
  fireEvent.click(screen.getByRole('checkbox', { name: 'Projekt' }));
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  open();
  choose('Dunkel');
  expect(theme()).toBe('dark');
  expect(screen.getByRole('searchbox')).toHaveValue('schritte');
  expect(screen.getByRole('checkbox', { name: 'Projekt' })).toBeChecked();
  expect(screen.getByText('2 Dokumente ausgewählt')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Listenansicht' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});
it('räumt System- und Außenklick-Listener bei Wechsel und Unmount auf', () => {
  const add = vi.spyOn(document, 'addEventListener');
  const off = vi.spyOn(document, 'removeEventListener');
  const { unmount } = render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
  open();
  choose('System');
  expect(listeners.size).toBe(1);
  unmount();
  expect(listeners.size).toBe(0);
  expect(remove).toHaveBeenCalled();
  const pointerHandler = add.mock.calls.find((call) => call[0] === 'pointerdown')?.[1];
  expect(
    off.mock.calls.some((call) => call[0] === 'pointerdown' && call[1] === pointerHandler),
  ).toBe(true);
});
it('erhält Tagzuordnung, Themenwechsel und Verschiebe-Undo beim Theme-Wechsel', () => {
  render(<App />);
  const row = document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!;
  fireEvent.click(within(row).getByRole('button', { name: 'Tags zuweisen' }));
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('checkbox', { name: 'Idee' }));
  fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Fertig' }));
  fireEvent.click(screen.getByRole('button', { name: 'Alle auswählen' }));
  const values = new Map<string, string>();
  const dataTransfer = {
    get types() {
      return [...values.keys()];
    },
    setData: (key: string, value: string) => values.set(key, value),
    getData: (key: string) => values.get(key) ?? '',
  };
  fireEvent.dragStart(row, { dataTransfer });
  const target = within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
    name: 'Kunde Beta',
  }).parentElement!;
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
  expect(screen.getByRole('heading', { name: 'Kunde Beta' })).toBeInTheDocument();
  open();
  choose('Dunkel');
  expect(screen.getByRole('heading', { name: 'Kunde Beta' })).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Rückgängig' }));
  fireEvent.click(
    within(screen.getByRole('navigation', { name: 'Themen' })).getByRole('button', {
      name: 'Nicht zugeordnet',
    }),
  );
  expect(
    within(document.querySelector<HTMLElement>('[data-document-id="demo-1"]')!).getByText('Idee'),
  ).toBeInTheDocument();
});
