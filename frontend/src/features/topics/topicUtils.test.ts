import { describe, expect, it } from 'vitest';
import { demoTopics } from './demoTopics';
import {
  canMoveTopic,
  deleteEmptyTopic,
  descendantIds,
  topicDeleteBlockReason,
  topicPath,
  topicNameError,
} from './topicUtils';

describe('Themenhierarchie', () => {
  it('ermittelt Pfad und tief verschachtelte Nachfahren', () => {
    expect(topicPath(demoTopics, 'alpha-steering').map((topic) => topic.name)).toEqual([
      'Kunden',
      'Kunde Alpha',
      'Lenkungskreis',
    ]);
    expect(descendantIds(demoTopics, 'customers').has('alpha-steering')).toBe(true);
  });
  it('verhindert Zyklen, Systemknoten und ungültige Ziele', () => {
    expect(canMoveTopic(demoTopics, 'customers', 'alpha-steering')).toBe(false);
    expect(canMoveTopic(demoTopics, 'alpha', 'alpha')).toBe(false);
    expect(canMoveTopic(demoTopics, 'unassigned', null)).toBe(false);
    expect(canMoveTopic(demoTopics, 'alpha', 'unassigned')).toBe(false);
    expect(canMoveTopic(demoTopics, 'alpha', 'missing')).toBe(false);
    expect(canMoveTopic(demoTopics, 'alpha', null)).toBe(true);
  });
});

it('erlaubt nur existierende, nicht geschützte Themen ohne direkte Dokumente und Unterthemen', () => {
  const documents = [{ topicId: 'alpha-steering' }, { topicId: 'alpha' }];
  expect(topicDeleteBlockReason(demoTopics, documents, 'missing')).toContain('existiert nicht');
  expect(topicDeleteBlockReason(demoTopics, documents, 'unassigned')).toContain('Systemthemen');
  expect(topicDeleteBlockReason(demoTopics, documents, 'alpha-steering')).toContain('Dokumente');
  expect(topicDeleteBlockReason(demoTopics, [], 'alpha')).toContain('Unterthemen');
  expect(topicDeleteBlockReason(demoTopics, documents, 'alpha')).toContain(
    'Dokumente und Unterthemen',
  );
  expect(topicDeleteBlockReason(demoTopics, [], 'alpha-steering')).toBeNull();
});
it('prüft bei Ausführung erneut und bewahrt Themen sowie Zuordnungen bei inzwischen belegtem Thema', () => {
  const docs: { topicId: string }[] = [];
  expect(topicDeleteBlockReason(demoTopics, docs, 'alpha-steering')).toBeNull();
  const later = [{ topicId: 'alpha-steering' }];
  expect(deleteEmptyTopic(demoTopics, later, 'alpha-steering')).toBe(demoTopics);
  expect(later).toEqual([{ topicId: 'alpha-steering' }]);
  const withChild = [...demoTopics, { id: 'new', parentId: 'alpha-steering', name: 'Neu' }];
  expect(deleteEmptyTopic(withChild, docs, 'alpha-steering')).toBe(withChild);
  const deleted = deleteEmptyTopic(demoTopics, docs, 'alpha-steering');
  expect(deleted).toHaveLength(demoTopics.length - 1);
  expect(deleted.some((topic) => topic.id === 'alpha-steering')).toBe(false);
});
it('prüft Themenbezeichnungen nur innerhalb desselben Parents und erlaubt reine Schreibweisenänderungen', () => {
  const topics = [{ id: 'root', parentId: null, name: 'Ärger' }];
  expect(topicNameError(topics, '\uFEFFÄRGER ', null)).toContain('existiert bereits');
  expect(topicNameError(topics, 'ÄRGER', null, 'root')).toBeUndefined();
  expect(topicNameError(topics, 'ärger', 'root')).toBeUndefined();
  expect(topicNameError(topics, 'Alle zugeordnet', 'root')).toBeUndefined();
  expect(topicNameError(topics, '\u00A0ALLE ZUGEORDNET', null)).toContain('reserviert');
  expect(topicNameError(topics, 'nicht ZUGEORDNET', null)).toContain('reserviert');
});
