import { describe, expect, it } from 'vitest';
import { demoDocuments } from '../documents/demoDocuments';
import type { DocumentSummary } from '../documents/document.types';
import { demoTags, tagNames } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import {
  datePreset,
  normalizeSearchText,
  parseSearchQuery,
  searchDocuments,
  sortByRelevance,
} from './search';
import type { SearchCriteria } from './search';
const all: SearchCriteria = { query: '', mode: 'full', scope: { kind: 'all' } };
const search = (query: string, extra: Partial<SearchCriteria> = {}) =>
  searchDocuments(demoDocuments, demoTopics, { ...all, query, ...extra });
describe('KU-Suchsemantik', () => {
  it('normalisiert Groß-/Kleinschreibung und Akzente ohne ae-Umschreibung', () => {
    expect(normalizeSearchText('FRÜH École')).toBe('fruh ecole');
    expect(search('FRUHJAHR')[0]?.documentId).toBe('demo-1');
    expect(search('fruehjahr')).toEqual([]);
  });
  it('zerlegt Phrasen und Einzelbegriffe wie die Referenz', () => {
    expect(parseSearchQuery('  "Nächste Schritte" PROJEKT ')).toEqual([
      'nachste schritte',
      'projekt',
    ]);
    expect(search('"nächste schritte" projekt').map((r) => r.documentId)).toEqual(['demo-1']);
    expect(search('"schritte nächste"')).toEqual([]);
  });
  it('verknüpft Begriffe per UND auch über Titel und verschiedene Seiten', () => {
    expect(search('eingang aufgaben').map((r) => r.documentId)).toEqual(['demo-1']);
    expect(search('eingang niemals')).toEqual([]);
  });
  it('findet Teilstrings in Titel und Dateiname im Titelmodus', () => {
    expect(search('sprechungs', { mode: 'title' }).map((r) => r.documentId)).toEqual(['demo-1']);
    expect(search('notizen-3.pdf', { mode: 'title' }).map((r) => r.documentId)).toEqual(['demo-3']);
    expect(search('frühjahr', { mode: 'title' })).toEqual([]);
  });
  it('zählt Titelbonus einmal und nicht überlappende Vorkommen auf Transkriptseiten', () => {
    const docs: DocumentSummary[] = [
      {
        ...demoDocuments[0],
        title: 'Alpha alpha',
        fileName: 'alpha.pdf',
        transcript: {
          edited: false,
          pages: [
            { page: 1, text: 'alpha alpha' },
            { page: 2, text: 'alpha' },
          ],
        },
      },
    ];
    expect(searchDocuments(docs, demoTopics, { ...all, query: 'alpha' })[0]).toEqual({
      documentId: 'demo-1',
      score: 13,
      terms: ['alpha'],
      hit: { page: 1, term: 'alpha' },
    });
  });
  it('liefert die erste Trefferseite des ersten passenden Suchbegriffs und gewichtete Relevanz', () => {
    const results = search('schritte');
    expect(results.map((r) => r.hit?.page)).toEqual([1, 2]);
    expect(
      sortByRelevance([
        { ...results[1], score: 11 },
        { ...results[0], score: 1 },
      ]).map((r) => r.documentId),
    ).toEqual(['demo-3', 'demo-1']);
    expect(search('frühjahr')[0].hit).toEqual({ page: 2, term: 'fruhjahr' });
  });
  it('behandelt fehlende und leere Transkripte', () => {
    const docs: DocumentSummary[] = [
      { ...demoDocuments[0], transcript: null },
      { ...demoDocuments[1], transcript: { edited: false, pages: [] } },
    ];
    expect(searchDocuments(docs, demoTopics, { ...all, query: 'frühjahr' })).toEqual([]);
    expect(searchDocuments(docs, demoTopics, { ...all, query: '' })).toHaveLength(2);
  });
});
describe('Filter und Presets', () => {
  it('filtert Datum inklusive Grenzen und schließt ungültige Daten nur bei aktivem Zeitraum aus', () => {
    const docs: DocumentSummary[] = [
      { ...demoDocuments[0], date: '2026-11-18' },
      { ...demoDocuments[1], date: '2026-02-30' },
    ];
    expect(
      searchDocuments(docs, demoTopics, { ...all, from: '2026-11-18', to: '2026-11-18' }).map(
        (r) => r.documentId,
      ),
    ).toEqual(['demo-1']);
    expect(searchDocuments(docs, demoTopics, all)).toHaveLength(2);
    expect(searchDocuments(docs, demoTopics, { ...all, from: 'invalid' })).toEqual([]);
    expect(
      searchDocuments(docs, demoTopics, { ...all, from: '2026-12-01', to: '2026-01-01' }),
    ).toEqual([]);
  });
  it('berechnet Presets deterministisch einschließlich Schaltjahr', () => {
    expect(datePreset('year', '2026-11-23')).toEqual({ from: '2026-01-01', to: '2026-11-23' });
    expect(datePreset('12m', '2024-02-29')).toEqual({ from: '2023-03-01', to: '2024-02-29' });
    expect(() => datePreset('year', '2026-02-30')).toThrow(RangeError);
  });
  it('schließt Themenunterbäume und nur vorhandene Themen ein', () => {
    expect(
      search('', { scope: { kind: 'topic', topicId: 'alpha' } }).map((r) => r.documentId),
    ).toEqual(['demo-3', 'demo-4']);
    expect(search('', { scope: { kind: 'topic', topicId: 'missing' } })).toEqual([]);
  });
  it('kombiniert Tag-UND, Suchtext, Zeitraum und Themenunterbaum', () => {
    expect(
      search('schritte', {
        tagIds: ['projekt', 'aufgabe'],
        from: '2026-11-18',
        to: '2026-11-18',
        scope: { kind: 'topic', topicId: 'unassigned' },
      }).map((r) => r.documentId),
    ).toEqual(['demo-1']);
    expect(search('schritte', { tagIds: ['projekt', 'idee'] })).toEqual([]);
  });
  it('gibt bei leeren Filtern alle Dokumente ohne Relevanz aus', () => {
    const results = search('');
    expect(results).toHaveLength(demoDocuments.length);
    expect(results.every((r) => r.score === 0 && r.hit === null && r.terms.length === 0)).toBe(
      true,
    );
  });
  it('löst IDs über zentrale Tagdefinitionen auf', () => {
    expect(tagNames(['projekt', 'missing'], demoTags)).toEqual(['Projekt']);
    expect(new Set(demoTags.map((t) => t.id)).size).toBe(demoTags.length);
  });
});
