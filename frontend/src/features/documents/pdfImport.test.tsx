import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from '../../App';
import { importPdfs } from './pdfImport';
import { readPdfPageCount } from '../pdf/pdfDocument';

vi.mock('../pdf/pdfDocument', () => ({ readPdfPageCount: vi.fn().mockResolvedValue(7) }));
vi.mock('../pdf/pdfThumbnail', () => ({ requestPdfThumbnail: () => () => {} }));

function file(name: string, header = '%PDF-', tail = '%%EOF', type = 'application/pdf') {
  const value = new File(['content'], name, { type });
  vi.spyOn(value, 'slice').mockImplementation(
    (start) =>
      ({
        arrayBuffer: async () => new TextEncoder().encode(start === 0 ? header : tail).buffer,
      }) as Blob,
  );
  return value;
}
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
it('importiert mehrere gleichnamige Dateien mit eindeutigen IDs und meldet Teilfehler', async () => {
  const result = await importPdfs([
    file('Gleich.pdf'),
    file('Gleich.pdf'),
    file('kaputt.pdf', 'wrong'),
    file('bild.jpg'),
  ]);
  expect(result.documents).toHaveLength(2);
  expect(result.documents[0].id).not.toBe(result.documents[1].id);
  expect(result.documents[0]).toMatchObject({
    title: 'Gleich',
    topicId: 'unassigned',
    pages: 7,
    source: { kind: 'pdf' },
  });
  expect(result.errors).toHaveLength(2);
  expect(result.errors.join(' ')).toContain('kaputt.pdf');
});
it('lehnt fehlendes PDF-Ende und Lesefehler ab', async () => {
  const broken = file('fehler.pdf');
  vi.mocked(broken.slice).mockImplementation(() => {
    throw new Error('Lesefehler');
  });
  const result = await importPdfs([file('ende.pdf', '%PDF-', 'unvollständig'), broken]);
  expect(result.documents).toHaveLength(0);
  expect(result.errors.join(' ')).toMatch(/Dateiende.*Lesefehler/);
});
it('behält Teilerfolge bei Parserfehlern und meldet unlesbare PDFs', async () => {
  vi.mocked(readPdfPageCount).mockRejectedValueOnce(new Error('PasswordException'));
  const result = await importPdfs([file('geschuetzt.pdf'), file('lesbar.pdf')]);
  expect(result.documents).toHaveLength(1);
  expect(result.documents[0].pages).toBe(7);
  expect(result.errors[0]).toMatch(/geschuetzt\.pdf.*verschlüsselt/);
});
it('integriert importierte Dokumente in Übersicht, Suche und Auswahl', async () => {
  const { container } = render(<App />);
  const input = container.querySelector<HTMLInputElement>('input[type=file]')!;
  fireEvent.change(input, {
    target: { files: [file('Spezialprobe.pdf'), file('fehler.pdf', 'wrong')] },
  });
  expect(await screen.findByText('Spezialprobe')).toBeInTheDocument();
  expect(screen.getByText(/1 PDF importiert.*fehler.pdf/)).toBeInTheDocument();
  expect(screen.getByRole('status', { name: 'Statusmeldung' })).toHaveTextContent(
    '1 PDF importiert',
  );
  expect(screen.getByRole('region', { name: 'Dokumentübersicht' })).not.toContainElement(
    screen.getByRole('status', { name: 'Statusmeldung' }),
  );
  expect(screen.getByText(/7 S\./)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Listenansicht' }));
  const importedRow = screen.getByText('Spezialprobe').closest('tr');
  expect(importedRow).not.toBeNull();
  expect(within(importedRow!).getByText('7')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Kachelansicht' }));
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'Spezialprobe' } });
  await waitFor(() => expect(screen.getByText('1 Treffer')).toBeInTheDocument());
  fireEvent.click(screen.getByRole('checkbox', { name: 'Spezialprobe auswählen' }));
  expect(screen.getByText('1 Dokument ausgewählt')).toBeInTheDocument();
});

it('zeigt nach PDF-Laden dieselbe Seite in Darstellung und Transkript und bewahrt sie beim Zoom', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  const { container } = render(<App />);
  fireEvent.change(container.querySelector<HTMLInputElement>('input[type=file]')!, {
    target: { files: [file('Sieben.pdf')] },
  });
  await screen.findByText('Sieben');
  fireEvent.doubleClick(screen.getByText('Sieben'));
  expect(await screen.findByRole('img', { name: 'PDF-Seite 1' })).toBeInTheDocument();
  expect(await screen.findByText(/7 Seiten/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Nächste Seite' }));
  expect(screen.getByRole('img', { name: 'PDF-Seite 2' })).toBeInTheDocument();
  expect(screen.getByRole('textbox', { name: 'Transkript Seite 2' })).toBeInTheDocument();
  fireEvent.change(screen.getByRole('slider', { name: /Viewer-Zoom/ }), {
    target: { value: '150' },
  });
  expect(screen.getByRole('img', { name: 'PDF-Seite 2' })).toHaveAttribute('data-scale', '1.5');
  expect(screen.getByRole('textbox', { name: 'Transkript Seite 2' })).toBeInTheDocument();
});

it('aktiviert beim PDF-Scrollen die zugehörige Transkriptkarte und navigiert über deren Seitenaktion', async () => {
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(640);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (
    this: HTMLElement,
  ) {
    return this.getAttribute('role') === 'toolbar' ? 0 : 500;
  });
  const rect = (top: number, bottom: number): DOMRect => ({
    top,
    bottom,
    left: 0,
    right: 640,
    width: 640,
    height: bottom - top,
    x: 0,
    y: top,
    toJSON: () => ({}),
  });
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement,
  ) {
    const container = document.getElementById('viewer-document-pane');
    const number = Number(this.dataset.pdfPage);
    return number
      ? rect(
          (number - 1) * 500 - (container?.scrollTop ?? 0),
          number * 500 - (container?.scrollTop ?? 0),
        )
      : rect(0, 500);
  });
  const { container } = render(<App />);
  fireEvent.change(container.querySelector<HTMLInputElement>('input[type=file]')!, {
    target: { files: [file('Scroll.pdf')] },
  });
  await screen.findByText('Scroll');
  fireEvent.doubleClick(screen.getByText('Scroll'));
  await screen.findByRole('img', { name: 'PDF-Seite 1' });
  const pane = document.getElementById('viewer-document-pane')!;
  pane.scrollTop = 650;
  fireEvent.scroll(pane);
  await waitFor(() =>
    expect(screen.getByRole('button', { name: 'Seite 2' })).toHaveAttribute('aria-current', 'true'),
  );
  expect(screen.getByRole('combobox', { name: 'Seite' })).toHaveValue('2');
  fireEvent.click(screen.getByRole('button', { name: 'Seite 4' }));
  expect(screen.getByRole('combobox', { name: 'Seite' })).toHaveValue('4');
  expect(pane.scrollTop).not.toBe(650);
});
