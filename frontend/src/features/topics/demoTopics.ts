import type { Topic } from './topic.types';

/** Reine Demodaten; keine API und keine dauerhafte Speicherung. */
export const demoTopics: readonly Topic[] = [
  { id: 'unassigned', parentId: null, name: 'Nicht zugeordnet', systemKey: 'UNASSIGNED' },
  { id: 'customers', parentId: null, name: 'Kunden' },
  { id: 'alpha', parentId: 'customers', name: 'Kunde Alpha' },
  { id: 'alpha-steering', parentId: 'alpha', name: 'Lenkungskreis' },
  { id: 'alpha-project', parentId: 'alpha', name: 'Projektplanung' },
  { id: 'beta', parentId: 'customers', name: 'Kunde Beta' },
  { id: 'own-notes', parentId: null, name: 'Eigene Notizen' },
  { id: 'ideas', parentId: 'own-notes', name: 'Ideen und Konzepte' },
];
