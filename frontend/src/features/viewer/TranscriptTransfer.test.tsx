import { act, cleanup, fireEvent, render, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { StandaloneViewer } from './StandaloneViewer';

class Channel {
  static channels = new Set<Channel>();
  onmessage: ((event: MessageEvent<unknown>) => void) | null = null;
  constructor() {
    Channel.channels.add(this);
  }
  postMessage(data: unknown) {
    for (const peer of Channel.channels) {
      if (peer !== this) queueMicrotask(() => peer.onmessage?.({ data } as MessageEvent));
    }
  }
  close() {
    Channel.channels.delete(this);
    this.onmessage = null;
  }
}
afterEach(() => {
  cleanup();
  Channel.channels.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  localStorage.removeItem('via.minidocs.opening-mode');
  window.history.replaceState({}, '', '/');
});
it('überträgt Seitenänderungen vor Rückkehr und öffnet danach die aktuellen Daten', async () => {
  vi.stubGlobal('BroadcastChannel', Channel);
  const open = vi.spyOn(window, 'open').mockImplementation(() => null);
  const close = vi.spyOn(window, 'close').mockImplementation(() => {});
  const app = render(<App />);
  const row = app.container.querySelector<HTMLElement>('[data-document-id="demo-1"]')!;
  fireEvent.click(within(row).getByRole('button', { name: 'In neuem Tab öffnen' }));
  const url = new URL(String(open.mock.calls[0][0]));
  window.history.replaceState({}, '', url.pathname + url.search);
  const viewer = render(<StandaloneViewer />);
  const ui = within(viewer.container);
  const second = await ui.findByRole('textbox', { name: 'Transkript Seite 2' });
  fireEvent.change(second, { target: { value: 'Seite zwei neu' } });
  fireEvent.change(ui.getByRole('textbox', { name: 'Transkript Seite 1' }), {
    target: { value: 'Seite eins neu' },
  });
  fireEvent.click(ui.getByRole('button', { name: 'Zur Dokumentübersicht' }));
  expect(close).not.toHaveBeenCalled();
  await waitFor(() => expect(close).toHaveBeenCalledOnce());
  viewer.unmount();
  window.history.replaceState({}, '', '/');
  fireEvent.click(within(row).getByRole('button', { name: 'In neuem Tab öffnen' }));
  const reopened = new URL(String(open.mock.calls[1][0]));
  window.history.replaceState({}, '', reopened.pathname + reopened.search);
  const next = render(<StandaloneViewer />);
  expect(
    await within(next.container).findByRole('textbox', { name: 'Transkript Seite 2' }),
  ).toHaveValue('Seite zwei neu');
  expect(within(next.container).getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveValue(
    'Seite eins neu',
  );
  fireEvent.change(within(next.container).getByRole('textbox', { name: 'Suchen' }), {
    target: { value: 'neu' },
  });
  fireEvent.change(within(next.container).getByRole('textbox', { name: 'Ersetzen durch' }), {
    target: { value: 'aktuell' },
  });
  fireEvent.click(within(next.container).getByRole('button', { name: 'Nächster Treffer' }));
  fireEvent.click(within(next.container).getByRole('button', { name: 'Ersetzen' }));
  fireEvent.click(within(next.container).getByRole('button', { name: 'Alle ersetzen' }));
  fireEvent.click(within(next.container).getByRole('button', { name: 'Zur Dokumentübersicht' }));
  await waitFor(() => expect(close).toHaveBeenCalledTimes(2));
  next.unmount();
  window.history.replaceState({}, '', '/');
  const secondRow = app.container.querySelector<HTMLElement>('[data-document-id="demo-2"]')!;
  fireEvent.click(within(secondRow).getByRole('button', { name: 'In neuem Tab öffnen' }));
  const otherUrl = new URL(String(open.mock.calls[2][0]));
  window.history.replaceState({}, '', otherUrl.pathname + otherUrl.search);
  const other = render(<StandaloneViewer />);
  fireEvent.change(
    await within(other.container).findByRole('textbox', { name: 'Transkript Seite 1' }),
    { target: { value: 'Nur Dokument B' } },
  );
  fireEvent.click(within(other.container).getByRole('button', { name: 'Zur Dokumentübersicht' }));
  await waitFor(() => expect(close).toHaveBeenCalledTimes(3));
  other.unmount();
  window.history.replaceState({}, '', '/');
  fireEvent.click(within(row).getByRole('button', { name: 'In neuem Tab öffnen' }));
  const finalUrl = new URL(String(open.mock.calls[3][0]));
  window.history.replaceState({}, '', finalUrl.pathname + finalUrl.search);
  const final = render(<StandaloneViewer />);
  expect(
    await within(final.container).findByRole('textbox', { name: 'Transkript Seite 2' }),
  ).toHaveValue('Seite zwei aktuell');
  expect(within(final.container).getByRole('textbox', { name: 'Transkript Seite 1' })).toHaveValue(
    'Seite eins aktuell',
  );
  final.unmount();
  app.unmount();
  await act(async () => {});
  expect(Channel.channels.size).toBe(0);
});
