import { expect, it } from 'vitest';
import overview from '../../../../demo-data/ui-demo-30/Dokumentenuebersicht.json';

it('ordnet die vorhandenen Transkriptseiten den PDF-Metadaten zu', () => {
  const files = import.meta.glob<{ fileName: string; pages: { page: number; text: string }[] }>(
    '../../../../demo-data/ui-demo-30/*.json',
    { eager: true, import: 'default' },
  );
  const transcripts = Object.values(files).filter((file) => !Array.isArray(file) && file.fileName);
  expect(overview).toHaveLength(30);
  expect(overview.reduce((sum, doc) => sum + doc.seitenzahl, 0)).toBe(207);
  expect(transcripts).toHaveLength(27);
  expect(transcripts.reduce((sum, file) => sum + file.pages.length, 0)).toBe(192);
  for (const file of transcripts) {
    const document = overview.find((item) => item.dateiName === file.fileName);
    expect(document).toBeDefined();
    for (const page of file.pages) {
      expect(page.page).toBeGreaterThan(0);
      expect(page.page).toBeLessThanOrEqual(document!.seitenzahl);
      expect(page.text.trim()).not.toBe('');
    }
  }
});
