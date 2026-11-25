import { render, screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { DocumentActions } from './DocumentSummary';
import { SelectionBar } from './SelectionBar';
import type { DocumentSummary } from './document.types';

it('bietet für ein Demo-PDF den Originalnamen in Karten- und Auswahlaktion an', () => {
  const document: DocumentSummary = {
    id: 'd',
    title: 'Test',
    topicId: 'unassigned',
    date: null,
    pages: 1,
    kind: 'Notiz',
    fileName: 'Beispiel.pdf',
    demoUrl: '/__demo_pdf/Beispiel.pdf',
  };
  render(
    <>
      <DocumentActions document={document} />
      <SelectionBar
        count={1}
        hasDocuments
        onAll={vi.fn()}
        onClear={vi.fn()}
        onMove={vi.fn()}
        onTags={vi.fn()}
        onOpen={vi.fn()}
        download={{ url: document.demoUrl!, fileName: document.fileName! }}
      />
    </>,
  );
  for (const link of screen.getAllByRole('link', { name: /Herunterladen/ })) {
    expect(link).toHaveAttribute('href', '/__demo_pdf/Beispiel.pdf');
    expect(link).toHaveAttribute('download', 'Beispiel.pdf');
  }
  expect(screen.getAllByRole('link', { name: /Herunterladen/ })).toHaveLength(2);
});
