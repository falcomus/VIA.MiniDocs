import { useState } from 'react';
import { demoTopics } from './features/topics/demoTopics';
import { TopicTree } from './features/topics/TopicTree';
import type { TopicId } from './features/topics/topic.types';
import { demoDocuments } from './features/documents/demoDocuments';
import { DocumentList } from './features/documents/DocumentList';
import styles from './App.module.css';

export function App() {
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>('unassigned');
  const selectedTopic = demoTopics.find((topic) => topic.id === selectedTopicId);
  const visibleDocuments = demoDocuments.filter((document) => document.topicId === selectedTopicId);

  return (
    <div className={styles.app}>
      <header className={styles.topbar}>
        <div className={styles.brand}>
          <span className={styles.brandMark} aria-hidden="true">M</span>
          <div className={styles.brandName}>MiniDoc <span>Testbasis</span></div>
        </div>
        <span className={styles.demoBadge}>Nur Beispieldaten · Keine Speicherung</span>
      </header>
      <div className={styles.layout}>
        <aside className={styles.sidebar}>
          <TopicTree
            topics={demoTopics}
            selectedTopicId={selectedTopicId}
            onSelect={setSelectedTopicId}
          />
          <div className={styles.sidebarFoot}>
            <strong>React-Prototyp</strong>
            <span>Ohne Backend-Anbindung</span>
          </div>
        </aside>
        <main className={styles.main}>
          <div className={styles.breadcrumb}>MiniDoc <span aria-hidden="true">/</span> Themen</div>
          <div className={styles.headingRow}>
            <div>
              <h1>{selectedTopic?.name ?? 'Unbekanntes Thema'}</h1>
              <p>Die Übersicht zeigt fiktive Beispieldokumente aus dem gewählten Thema.</p>
            </div>
            <span className={styles.count} aria-label={`${visibleDocuments.length} Dokumente`}>
              {visibleDocuments.length} Dokumente
            </span>
          </div>
          <section className={styles.documents} aria-label="Dokumentübersicht">
            <div className={styles.sectionHeading}>
              <h2>Dokumente</h2>
              <span>Ansicht: Liste</span>
            </div>
            <DocumentList documents={visibleDocuments} />
          </section>
          <p className={styles.disclaimer}>
            Testumfang: Themen auf- und zuklappen, Thema auswählen, Liste anzeigen.
            Keine PDF-Dateien, keine Suche und kein Speichern.
          </p>
        </main>
      </div>
    </div>
  );
}
