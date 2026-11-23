import { expect, it } from 'vitest';
import { demoDocuments } from './demoDocuments';
import { demoTopics } from '../topics/demoTopics';
import { documentReducer } from './documentOperations';
import { searchDocuments } from '../search/search';
import {
  applyTranscriptOperation,
  countReplaceMatches,
  findTranscriptMatches,
} from './transcriptOperations';
const doc = demoDocuments[0];
it('bearbeitet und ergänzt gültige Seiten ohne Metadaten oder andere Seiten zu verändern', () => {
  const edited = applyTranscriptOperation(doc, { kind: 'set', page: 2, text: 'Neu\nZeile' });
  expect(edited.transcript?.edited).toBe(true);
  expect(edited.transcript?.pages[0]).toBe(doc.transcript?.pages[0]);
  expect(edited.transcript?.pages[1].text).toBe('Neu\nZeile');
  expect(edited.tagIds).toBe(doc.tagIds);
  expect(edited.topicId).toBe(doc.topicId);
  expect(applyTranscriptOperation(doc, { kind: 'set', page: 0, text: 'x' })).toBe(doc);
  expect(applyTranscriptOperation(doc, { kind: 'set', page: 3, text: 'x' })).toBe(doc);
  expect(
    applyTranscriptOperation(doc, { kind: 'set', page: 1, text: doc.transcript!.pages[0].text }),
  ).toBe(doc);
});
it('legt fehlende Transkripte und Seiten nur bei tatsächlicher Eingabe sortiert an', () => {
  const empty = demoDocuments[1];
  expect(applyTranscriptOperation(empty, { kind: 'set', page: 1, text: '' })).toBe(empty);
  const added = applyTranscriptOperation(empty, { kind: 'set', page: 1, text: 'Hallo' });
  expect(added.transcript).toEqual({ edited: true, pages: [{ page: 1, text: 'Hallo' }] });
  const sparse = { ...doc, transcript: { edited: false, pages: [{ page: 2, text: 'zwei' }] } };
  const complete = applyTranscriptOperation(sparse, { kind: 'set', page: 1, text: 'eins' });
  expect(complete.transcript?.pages.map((part) => part.page)).toEqual([1, 2]);
});
it('findet akzent- und großschreibungsunabhängig mit korrekten Unicode-Positionen', () => {
  const source = {
    ...doc,
    transcript: { edited: false, pages: [{ page: 1, text: '😀 Fru\u0308h FRÜH\nEnde' }] },
  };
  const hits = findTranscriptMatches(source, 'früh');
  expect(hits).toHaveLength(2);
  expect(source.transcript.pages[0].text.slice(hits[0].start, hits[0].end)).toBe('Fru\u0308h');
  expect(findTranscriptMatches(source, '')).toEqual([]);
  const replaced = applyTranscriptOperation(source, {
    kind: 'replace-one',
    match: hits[0],
    query: 'früh',
    replacement: 'Spät',
  });
  expect(replaced.transcript?.pages[0].text).toBe('😀 Spät FRÜH\nEnde');
});
it('ersetzt wörtlich ohne Regex-Injektion auf allen Seiten und erlaubt leere Ersetzung', () => {
  const source = {
    ...doc,
    transcript: {
      edited: false,
      pages: [
        { page: 1, text: 'A.a a.A ä' },
        { page: 2, text: 'a.a\nA.A' },
      ],
    },
  };
  expect(countReplaceMatches(source, 'a.a')).toBe(4);
  const changed = applyTranscriptOperation(source, {
    kind: 'replace-all',
    query: 'a.a',
    replacement: '',
  });
  expect(changed.transcript?.pages.map((part) => part.text)).toEqual(['  ä', '\n']);
  expect(
    applyTranscriptOperation(source, { kind: 'replace-all', query: '', replacement: 'x' }),
  ).toBe(source);
  expect(findTranscriptMatches(source, 'a')).toHaveLength(9);
  expect(countReplaceMatches(source, 'a')).toBe(8);
});
it('erhält Transkript und Tags nach Verschieben, Bearbeiten und Undo', () => {
  const initial = { documents: demoDocuments, selected: [], anchor: null, undo: null, message: '' };
  const moved = documentReducer(initial, {
    type: 'move',
    ids: ['demo-1'],
    target: 'beta',
    topics: demoTopics,
  });
  const edited = documentReducer(moved, {
    type: 'transcript',
    id: 'demo-1',
    operation: { kind: 'set', page: 1, text: 'Projektabschluss' },
    visibleIds: (docs) => docs.map((item) => item.id),
  });
  expect(edited.documents[1]).toBe(moved.documents[1]);
  expect(edited.undo).toBe(moved.undo);
  const undone = documentReducer(edited, { type: 'undo', topics: demoTopics });
  expect(undone.documents[0].topicId).toBe('unassigned');
  expect(undone.documents[0].tagIds).toEqual(doc.tagIds);
  expect(undone.documents[0].transcript?.pages[0].text).toBe('Projektabschluss');
  const criteria = {
    query: 'projektabschluss',
    mode: 'full' as const,
    scope: { kind: 'all' as const },
  };
  expect(
    searchDocuments(undone.documents, demoTopics, criteria).map((hit) => hit.documentId),
  ).toContain('demo-1');
  expect(
    searchDocuments(undone.documents, demoTopics, { ...criteria, query: 'projektstart' }),
  ).toHaveLength(0);
});
it('erhält Tag- und Transkriptänderungen bei aufeinanderfolgenden Reducer-Aktionen', () => {
  const initial = { documents: demoDocuments, selected: [], anchor: null, undo: null, message: '' };
  const tagged = documentReducer(initial, {
    type: 'tags',
    ids: ['demo-1'],
    tagId: 'idee',
    on: true,
    visibleAfter: ['demo-1'],
  });
  const edited = documentReducer(tagged, {
    type: 'transcript',
    id: 'demo-1',
    operation: { kind: 'set', page: 2, text: 'Neu' },
    visibleIds: (docs) => docs.map((item) => item.id),
  });
  expect(edited.documents[0].tagIds).toContain('idee');
  expect(edited.documents[0].transcript?.pages[1].text).toBe('Neu');
  const again = documentReducer(edited, {
    type: 'tags',
    ids: ['demo-1'],
    tagId: 'projekt',
    on: false,
    visibleAfter: ['demo-1'],
  });
  expect(again.documents[0].tagIds).not.toContain('projekt');
  expect(again.documents[0].transcript?.pages[1].text).toBe('Neu');
});
it('berücksichtigt kontextabhängige Unicode-Kleinschreibung ohne Positionsverlust', () => {
  const source = { ...doc, transcript: { edited: false, pages: [{ page: 1, text: '😀 ΟΣ' }] } };
  const hits = findTranscriptMatches(source, 'ος');
  expect(hits).toHaveLength(1);
  expect(source.transcript.pages[0].text.slice(hits[0].start, hits[0].end)).toBe('ΟΣ');
});
