import { expect, it } from 'vitest';
import { demoTopics } from '../topics/demoTopics';
import { demoDocuments } from './demoDocuments';
import { documentReducer, sortDocuments } from './documentOperations';
import type { DocumentState } from './documentOperations';
const initial: DocumentState = {
  documents: demoDocuments,
  selected: [],
  anchor: null,
  undo: null,
  message: '',
};
it('sortiert stabil ohne Eingabemutation und behandelt fehlende/ungültige Daten deterministisch', () => {
  const docs = [
    { ...demoDocuments[0], id: 'a', date: null },
    { ...demoDocuments[0], id: 'b', date: '2026-02-30' },
    { ...demoDocuments[0], id: 'c', date: '2024-02-29' },
    { ...demoDocuments[0], id: 'd', date: '2024-02-29' },
  ];
  expect(sortDocuments(docs, 'date-desc').map((d) => d.id)).toEqual(['c', 'd', 'a', 'b']);
  expect(sortDocuments(docs, 'date-asc').map((d) => d.id)).toEqual(['a', 'b', 'c', 'd']);
  expect(sortDocuments(docs, 'title').map((d) => d.id)).toEqual(['a', 'b', 'c', 'd']);
  expect(docs.map((d) => d.id)).toEqual(['a', 'b', 'c', 'd']);
});
it('verwendet beim Shift-Bereich den ID-Anker in der neuen Sortierung', () => {
  let state = documentReducer(initial, {
    type: 'select',
    id: 'demo-1',
    visible: ['demo-1', 'demo-2', 'demo-3'],
    toggle: false,
    range: false,
  });
  state = documentReducer(state, {
    type: 'select',
    id: 'demo-3',
    visible: ['demo-2', 'demo-1', 'demo-3'],
    toggle: false,
    range: true,
  });
  expect(state.selected).toEqual(['demo-1', 'demo-3']);
  expect(state.anchor).toBe('demo-1');
});
it('stellt beim Undo unterschiedliche Ursprünge wieder her und verliert keine Dokumente', () => {
  const moved = documentReducer(initial, {
    type: 'move',
    ids: ['demo-1', 'demo-3'],
    target: 'beta',
    topics: demoTopics,
  });
  expect(
    moved.documents.filter((d) => ['demo-1', 'demo-3'].includes(d.id)).map((d) => d.topicId),
  ).toEqual(['beta', 'beta']);
  const undone = documentReducer(moved, { type: 'undo', topics: demoTopics });
  expect(undone.documents).toEqual(demoDocuments);
  expect(undone.undo).toBeNull();
  expect(initial.documents).toEqual(demoDocuments);
});
it('macht bei aufeinanderfolgenden Verschiebungen nur die letzte erfolgreiche Aktion rückgängig', () => {
  const a = documentReducer(initial, {
    type: 'move',
    ids: ['demo-1'],
    target: 'beta',
    topics: demoTopics,
  });
  const b = documentReducer(a, {
    type: 'move',
    ids: ['demo-1', 'demo-3'],
    target: 'ideas',
    topics: demoTopics,
  });
  const undo = documentReducer(b, { type: 'undo', topics: demoTopics });
  expect(undo.documents.find((d) => d.id === 'demo-1')?.topicId).toBe('beta');
  expect(undo.documents.find((d) => d.id === 'demo-3')?.topicId).toBe('alpha-steering');
});
it.each([
  { ids: ['missing'], target: 'beta' },
  { ids: ['demo-1', 'demo-1'], target: 'beta' },
  { ids: ['demo-1'], target: 'missing' },
  { ids: [], target: 'beta' },
])('weist ungültige Verschiebung %j atomar ab', ({ ids, target }) => {
  const result = documentReducer(initial, { type: 'move', ids, target, topics: demoTopics });
  expect(result.documents).toBe(initial.documents);
  expect(result.undo).toBeNull();
  expect(result.message).toContain('nicht dorthin');
});
it('nutzt bei nachfolgenden Aktionen den aktuellen Dokumentzustand', () => {
  const a = documentReducer(initial, {
    type: 'move',
    ids: ['demo-1'],
    target: 'beta',
    topics: demoTopics,
  });
  const b = documentReducer(a, {
    type: 'move',
    ids: ['demo-1'],
    target: 'ideas',
    topics: demoTopics,
  });
  expect(b.undo?.get('demo-1')).toBe('beta');
});
