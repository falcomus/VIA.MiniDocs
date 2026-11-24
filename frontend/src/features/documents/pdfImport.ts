import type { DocumentSummary } from './document.types';
import { readPdfPageCount } from '../pdf/pdfDocument';

/** Header and MIME checks are plausibility checks, not a full PDF parser. */
export async function importPdfs(
  files: readonly File[],
): Promise<{ documents: DocumentSummary[]; errors: string[] }> {
  const documents: DocumentSummary[] = [];
  const errors: string[] = [];
  for (const file of files) {
    try {
      if (!/\.pdf$/i.test(file.name) || (file.type && file.type !== 'application/pdf'))
        throw new Error('Dateityp ist kein PDF');
      const header = new Uint8Array(await file.slice(0, 5).arrayBuffer());
      if (String.fromCharCode(...header) !== '%PDF-') throw new Error('Ungültige PDF-Signatur');
      const tail = new TextDecoder().decode(await file.slice(-2048).arrayBuffer());
      if (!tail.includes('%%EOF')) throw new Error('PDF-Dateiende fehlt oder ist nicht lesbar');
      const blob =
        file.type === 'application/pdf' ? file : new Blob([file], { type: 'application/pdf' });
      let pages: number;
      try {
        pages = await readPdfPageCount(blob);
      } catch {
        throw new Error('PDF ist beschädigt, verschlüsselt oder nicht lesbar');
      }
      documents.push({
        id: `pdf-${crypto.randomUUID()}`,
        title: file.name.replace(/\.pdf$/i, ''),
        fileName: file.name,
        topicId: 'unassigned',
        date: null,
        pages,
        kind: 'Notiz',
        tagIds: [],
        transcript: null,
        source: { kind: 'pdf', blob },
      });
    } catch (error) {
      errors.push(
        `${file.name}: ${error instanceof Error ? error.message : 'Datei konnte nicht gelesen werden'}`,
      );
    }
  }
  return { documents, errors };
}
