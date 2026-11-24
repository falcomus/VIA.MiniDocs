import type { DocumentSummary } from './document.types';

/** Fiktive Dokumente zur UI-Erprobung; keine echten Kundenunterlagen. */
export const demoDocuments: readonly DocumentSummary[] = [
  {
    id: 'demo-1',
    demoInk: {
      head: 'Besprechung',
      pages: [
        ['Besprechungsnotizen – Eingang', 'Projektstart: Nächste Schritte festlegen.'],
        ['Frühjahr: Lösung prüfen und Aufgaben sammeln.'],
      ],
    },
    fileName: 'Demo-Notizen-1.pdf',
    title: 'Besprechungsnotizen – Eingang',
    topicId: 'unassigned',
    date: '2026-11-18',
    pages: 2,
    kind: 'Besprechung',
    tagIds: ['projekt', 'aufgabe'],
    transcript: {
      edited: false,
      pages: [
        { page: 1, text: 'Projektstart: Nächste Schritte festlegen.' },
        { page: 2, text: 'Frühjahr: Lösung prüfen und Aufgaben sammeln.' },
      ],
    },
  },
  {
    id: 'demo-2',
    demoInk: {
      head: 'Offene Punkte',
      pages: [['Offene Punkte – Entwurf', 'Demo-Notizen', 'Besprechung und nächste Schritte']],
    },
    fileName: 'Demo-Notizen-2.pdf',
    title: 'Offene Punkte – Entwurf',
    topicId: 'unassigned',
    date: '2026-11-15',
    pages: 1,
    kind: 'Notiz',
    tagIds: ['idee'],
    transcript: null,
  },
  {
    tagIds: ['projekt'],
    transcript: {
      edited: false,
      pages: [
        { page: 1, text: 'Planung für den Lenkungskreis.' },
        { page: 2, text: 'Aufgaben und nächste Schritte.' },
      ],
    },
    id: 'demo-3',
    demoInk: {
      head: 'Lenkungskreis',
      pages: [
        ['Lenkungskreis – Planungsstand', 'Planung für den Lenkungskreis.'],
        ['Aufgaben und nächste Schritte.'],
        [],
        [],
      ],
    },
    fileName: 'Demo-Notizen-3.pdf',
    title: 'Lenkungskreis – Planungsstand',
    topicId: 'alpha-steering',
    date: '2026-10-29',
    pages: 4,
    kind: 'Besprechung',
  },
  {
    id: 'demo-4',
    demoInk: {
      head: 'Projektrahmen',
      pages: [
        ['Projektrahmen – Diskussion', 'Demo-Notizen', 'Besprechung und nächste Schritte'],
        [],
        [],
      ],
    },
    fileName: 'Demo-Notizen-4.pdf',
    title: 'Projektrahmen – Diskussion',
    topicId: 'alpha-project',
    date: '2026-09-11',
    pages: 3,
    kind: 'Besprechung',
  },
  {
    tagIds: ['aufgabe'],
    transcript: { edited: false, pages: [{ page: 1, text: 'Zusammenarbeit und Rückmeldung.' }] },
    id: 'demo-5',
    demoInk: {
      head: 'Notizen',
      pages: [['Notizen zur Zusammenarbeit', 'Zusammenarbeit und Rückmeldung.']],
    },
    fileName: 'Demo-Notizen-5.pdf',
    title: 'Notizen zur Zusammenarbeit',
    topicId: 'beta',
    date: '2026-10-02',
    pages: 1,
    kind: 'Notiz',
  },
  {
    id: 'demo-6',
    demoInk: {
      head: 'Ideen',
      pages: [
        ['Ideen für die nächste Version', 'Demo-Notizen', 'Besprechung und nächste Schritte'],
        [],
      ],
    },
    fileName: 'Demo-Notizen-6.pdf',
    title: 'Ideen für die nächste Version',
    topicId: 'ideas',
    date: '2026-11-12',
    pages: 2,
    kind: 'Notiz',
  },
];
