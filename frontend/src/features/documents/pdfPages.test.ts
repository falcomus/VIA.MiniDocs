import { expect, it } from 'vitest';
import { documentReducer } from './documentOperations';
import type { DocumentState } from './documentOperations';
import type { DocumentSummary } from './document.types';

it('übernimmt Seitenzahlen nur für passende PDF-Quelle und erhält Bearbeitungen und Undo', () => {
  const blob = new Blob(['PDF'], { type: 'application/pdf' });
  const document: DocumentSummary = {
    id: 'p',
    title: 'PDF',
    topicId: 'unassigned',
    date: null,
    pages: null,
    kind: 'Notiz',
    source: { kind: 'pdf', blob },
    tagIds: ['a'],
    transcript: { edited: true, pages: [{ page: 1, text: 'Text' }] },
  };
  const undo = new Map([['p', 'topic']]);
  const state: DocumentState = {
    documents: [document],
    selected: ['p'],
    anchor: 'p',
    undo,
    message: '',
  };
  expect(
    documentReducer(state, { type: 'pdf-pages', id: 'p', blob: new Blob(['other']), pages: 7 }),
  ).toBe(state);
  expect(documentReducer(state, { type: 'pdf-pages', id: 'other', blob, pages: 7 })).toBe(state);
  expect(documentReducer(state, { type: 'pdf-pages', id: 'p', blob, pages: 0 })).toBe(state);
  const next = documentReducer(state, { type: 'pdf-pages', id: 'p', blob, pages: 7 });
  expect(next.documents[0]).toMatchObject({
    pages: 7,
    tagIds: ['a'],
    transcript: document.transcript,
  });
  expect(next.undo).toBe(undo);
  expect(next.selected).toEqual(['p']);
  expect(documentReducer(next, { type: 'pdf-pages', id: 'p', blob, pages: 7 })).toBe(next);
});
