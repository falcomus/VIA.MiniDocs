import { useState } from 'react';
import { AppShell, Content, Sidebar, Toolbar } from './components/AppShell';
import { demoTopics } from './features/topics/demoTopics';
import { TopicTree } from './features/topics/TopicTree';
import type { TopicId } from './features/topics/topic.types';
import { demoDocuments } from './features/documents/demoDocuments';
import { DocumentList } from './features/documents/DocumentList';

export function App() {
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>('unassigned');
  const selectedTopic = demoTopics.find((topic) => topic.id === selectedTopicId);
  const visibleDocuments = demoDocuments.filter((document) => document.topicId === selectedTopicId);
  return <AppShell sidebar={<Sidebar documentCount={demoDocuments.length} pageCount={demoDocuments.reduce((sum, document) => sum + document.pages, 0)}><TopicTree topics={demoTopics} selectedTopicId={selectedTopicId} onSelect={setSelectedTopicId} /></Sidebar>}>
    <Toolbar title={selectedTopic?.name ?? 'Unbekanntes Thema'} />
    <Content><section aria-label="Dokumentübersicht"><DocumentList documents={visibleDocuments} /></section></Content>
  </AppShell>;
}
