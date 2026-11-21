import type { DocumentSummary } from './document.types';

/** Fiktive Dokumente zur UI-Erprobung; keine echten Kundenunterlagen. */
export const demoDocuments: readonly DocumentSummary[] = [
  { id: 'demo-1', title: 'Besprechungsnotizen – Eingang', topicId: 'unassigned', date: '2026-11-18', pages: 2, kind: 'Besprechung' },
  { id: 'demo-2', title: 'Offene Punkte – Entwurf', topicId: 'unassigned', date: '2026-11-15', pages: 1, kind: 'Notiz' },
  { id: 'demo-3', title: 'Lenkungskreis – Planungsstand', topicId: 'alpha-steering', date: '2026-10-29', pages: 4, kind: 'Besprechung' },
  { id: 'demo-4', title: 'Projektrahmen – Diskussion', topicId: 'alpha-project', date: '2026-09-11', pages: 3, kind: 'Besprechung' },
  { id: 'demo-5', title: 'Notizen zur Zusammenarbeit', topicId: 'beta', date: '2026-10-02', pages: 1, kind: 'Notiz' },
  { id: 'demo-6', title: 'Ideen für die nächste Version', topicId: 'ideas', date: '2026-11-12', pages: 2, kind: 'Notiz' },
];
