import raw from 'virtual:demo-data';
import type { DocumentSummary } from './document.types';
import type { Topic } from '../topics/topic.types';
import type { TagDefinition } from './tags';
import { demoDocuments } from './demoDocuments';
import { demoTopics } from '../topics/demoTopics';
import { demoTags } from './tags';

const topics: Topic[] = [
  { id: 'unassigned', parentId: null, name: 'Nicht zugeordnet', systemKey: 'UNASSIGNED' },
];
const tags: TagDefinition[] = [];
const tagIds = new Map<string, string>();
const documents: DocumentSummary[] = (raw?.records ?? []).map((record) => {
  let parent: string | null = null;
  for (const name of record.empfohlenesThema
    .split('/')
    .map((part) => part.trim())
    .filter(Boolean)) {
    const id: string = `topic:${parent ?? ''}/${name}`;
    if (!topics.some((topic) => topic.id === id)) topics.push({ id, parentId: parent, name });
    parent = id;
  }
  const ids = record.tags.map((name) => {
    let id = tagIds.get(name);
    if (!id) {
      id = `tag:${name}`;
      tagIds.set(name, id);
      tags.push({ id, name, description: '', color: '#225b79' });
    }
    return id;
  });
  const transcript = raw?.transcripts[record.dateiName];
  return {
    id: `dataset:${record.dateiName}`,
    title: record.titel,
    fileName: record.dateiName,
    date: record.datum,
    topicId: parent ?? 'unassigned',
    pages: record.seitenzahl,
    sizeBytes: record.sizeBytes,
    kind: /besprechung|protokoll/i.test(record.titel) ? 'Besprechung' : 'Notiz',
    tagIds: ids,
    transcript: transcript ? { edited: false, pages: transcript.pages } : null,
    demoUrl: `/__demo_pdf/${encodeURIComponent(record.dateiName)}`,
  };
});
export const initialDocuments = raw ? documents : demoDocuments;
export const initialTopics = raw ? topics : demoTopics;
export const initialTags = raw ? tags : demoTags;
