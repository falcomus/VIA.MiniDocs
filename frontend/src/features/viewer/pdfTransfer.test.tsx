import { act, cleanup, render, screen, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { StandaloneViewer } from './StandaloneViewer';
import { demoTopics } from '../topics/demoTopics';
import { demoTags } from '../documents/tags';
import { App } from '../../App';
import { fireEvent, waitFor } from '@testing-library/react';

vi.mock('../pdf/pdfDocument', () => ({ readPdfPageCount: vi.fn().mockResolvedValue(7) }));
vi.mock('../pdf/pdfThumbnail', () => ({ requestPdfThumbnail: () => () => {} }));

class TestChannel {
  static latest: TestChannel;
  onmessage: ((event: MessageEvent<unknown>) => void) | null = null;
  postMessage = vi.fn();
  close = vi.fn();
  constructor() {
    TestChannel.latest = this;
  }
}
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.useRealTimers();
  window.history.replaceState({}, '', '/');
});
it('akzeptiert nur die korrelierte Dokumentantwort und schließt den Listener', async () => {
  vi.stubGlobal('BroadcastChannel', TestChannel);
  vi.stubGlobal(
    'URL',
    Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:received'), revokeObjectURL: vi.fn() }),
  );
  window.history.replaceState({}, '', '/?viewer=tab&document=pdf-1&page=1');
  render(<StandaloneViewer />);
  const channel = TestChannel.latest;
  const request = channel.postMessage.mock.calls[0][0];
  const blob = new Blob([], { type: 'application/pdf' });
  vi.spyOn(blob, 'slice').mockReturnValue({
    arrayBuffer: async () => new TextEncoder().encode('%PDF-').buffer,
  } as Blob);
  const doc = {
    id: 'pdf-1',
    title: 'Test PDF',
    topicId: 'unassigned',
    date: null,
    pages: null,
    kind: 'Notiz',
    source: { kind: 'pdf', blob },
  };
  const response = {
    kind: 'response',
    requestId: request.requestId,
    documentId: 'pdf-1',
    document: doc,
    topics: demoTopics,
    tags: demoTags,
  };
  act(() => channel.onmessage?.({ data: { ...response, documentId: 'other' } } as MessageEvent));
  expect(screen.getByRole('status')).toHaveTextContent('angefordert');
  act(() => channel.onmessage?.({ data: { ...response, requestId: 'wrong' } } as MessageEvent));
  expect(screen.getByRole('status')).toHaveTextContent('angefordert');
  act(() => channel.onmessage?.({ data: response } as MessageEvent));
  expect(await screen.findByRole('region', { name: 'PDF-Darstellung' })).toBeInTheDocument();
  expect(channel.close).toHaveBeenCalled();
  expect(screen.getByText(/7 Seiten/)).toBeInTheDocument();
});
it('zeigt nach Zeitüberschreitung einen Fehler und räumt den Kanal auf', () => {
  vi.useFakeTimers();
  vi.stubGlobal('BroadcastChannel', TestChannel);
  window.history.replaceState({}, '', '/?viewer=tab&document=pdf-fehlt&page=1');
  render(<StandaloneViewer />);
  const channel = TestChannel.latest;
  act(() => vi.advanceTimersByTime(5000));
  expect(screen.getByRole('heading', { name: 'Dokument nicht verfügbar' })).toBeInTheDocument();
  expect(channel.close).toHaveBeenCalled();
});

it('beantwortet nur Anfragen zu im Hauptfenster importierten PDF-IDs', async () => {
  vi.stubGlobal('BroadcastChannel', TestChannel);
  const pdf = new File(['sample'], 'Test.pdf', { type: 'application/pdf' });
  vi.spyOn(pdf, 'slice').mockImplementation(
    (start) =>
      ({
        arrayBuffer: async () => new TextEncoder().encode(start === 0 ? '%PDF-' : '%%EOF').buffer,
      }) as Blob,
  );
  const { container, unmount } = render(<App />);
  const channel = TestChannel.latest;
  fireEvent.change(container.querySelector('input[type=file]')!, { target: { files: [pdf] } });
  await screen.findByText('Test');
  const row = container.querySelector<HTMLElement>('[data-document-id^="pdf-"]')!;
  const id = row.dataset.documentId!;
  act(() =>
    channel.onmessage?.({
      data: { kind: 'request', requestId: 'r1', documentId: 'unknown' },
    } as MessageEvent),
  );
  expect(channel.postMessage).not.toHaveBeenCalled();
  act(() =>
    channel.onmessage?.({
      data: { kind: 'request', requestId: 'r2', documentId: id },
    } as MessageEvent),
  );
  await waitFor(() =>
    expect(channel.postMessage).toHaveBeenCalledWith(
      expect.objectContaining({
        kind: 'response',
        requestId: 'r2',
        documentId: id,
        document: expect.objectContaining({ id, source: { kind: 'pdf', blob: pdf } }),
      }),
    ),
  );
  unmount();
  expect(channel.close).toHaveBeenCalled();
});

it('beantwortet eine Anfrage während ausstehender Änderung erst mit dem bestätigten aktuellen Stand', async () => {
  vi.stubGlobal('BroadcastChannel', TestChannel);
  const open = vi.spyOn(window, 'open').mockImplementation(() => null);
  const app = render(<App />);
  const channel = TestChannel.latest;
  const row = app.container.querySelector<HTMLElement>('[data-document-id="demo-1"]')!;
  fireEvent.click(within(row).getByRole('button', { name: 'In neuem Tab öffnen' }));
  const owner = new URL(String(open.mock.calls[0][0])).searchParams.get('owner');
  act(() => {
    channel.onmessage?.({
      data: {
        kind: 'transcript-update',
        owner,
        requestId: 'edit',
        documentId: 'demo-1',
        operation: { kind: 'set', page: 2, text: 'aktuell' },
      },
    } as MessageEvent);
    channel.onmessage?.({
      data: { kind: 'request', owner, requestId: 'read', documentId: 'demo-1' },
    } as MessageEvent);
  });
  await waitFor(() =>
    expect(channel.postMessage).toHaveBeenCalledWith(
      expect.objectContaining({
        kind: 'response',
        requestId: 'read',
        document: expect.objectContaining({
          transcript: expect.objectContaining({
            pages: expect.arrayContaining([{ page: 2, text: 'aktuell' }]),
          }),
        }),
      }),
    ),
  );
  expect(channel.postMessage).toHaveBeenCalledWith({
    kind: 'transcript-ack',
    owner,
    requestId: 'edit',
  });
});
