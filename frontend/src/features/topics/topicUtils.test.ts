import { describe, expect, it } from 'vitest';
import { demoTopics } from './demoTopics';
import { canMoveTopic, descendantIds, topicPath } from './topicUtils';

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
