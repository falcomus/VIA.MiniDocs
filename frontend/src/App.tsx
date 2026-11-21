import { useEffect, useRef, useState } from 'react';
import { AppShell, Content, Sidebar, Toolbar } from './components/AppShell';
import { demoTopics } from './features/topics/demoTopics';
import { TopicTree } from './features/topics/TopicTree';
import { canMoveTopic, descendantIds, topicPath } from './features/topics/topicUtils';
import type { Topic, TopicId } from './features/topics/topic.types';
import { demoDocuments } from './features/documents/demoDocuments';
import { DocumentList } from './features/documents/DocumentList';
import styles from './features/topics/TopicDialog.module.css';

type DialogState = { kind: 'new'; parentId: TopicId | null } | { kind: 'rename' | 'delete'; id: TopicId };

export function App() {
  const [topics, setTopics] = useState<readonly Topic[]>(demoTopics);
  const [documents, setDocuments] = useState(demoDocuments);
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>('unassigned');
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [name, setName] = useState('');
  const [notice, setNotice] = useState('');
  const dialogRef = useRef<HTMLDivElement>(null);
  const returnFocus = useRef<HTMLElement | null>(null);
  const path = topicPath(topics, selectedTopicId);
  const visibleDocuments = documents.filter((document) => document.topicId === selectedTopicId);
  const dialogTopic = dialog && dialog.kind !== 'new' ? topics.find((topic) => topic.id === dialog.id) : undefined;
  const deletedIds = dialogTopic ? descendantIds(topics, dialogTopic.id) : new Set<TopicId>();
  const affectedDocuments = documents.filter((document) => deletedIds.has(document.topicId)).length;

  function openDialog(next: DialogState) {
    returnFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setName(next.kind === 'rename' ? topics.find((topic) => topic.id === next.id)?.name ?? '' : '');
    setDialog(next);
  }
  function closeDialog() { setDialog(null); returnFocus.current?.focus(); }
  useEffect(() => {
    if (!dialog) return;
    dialogRef.current?.querySelector<HTMLElement>('input, button')?.focus();
  }, [dialog]);

  function submit() {
    if (!dialog) return;
    if (dialog.kind === 'new') {
      const parent = dialog.parentId && topics.find((topic) => topic.id === dialog.parentId);
      if (!name.trim() || (dialog.parentId !== null && (!parent || parent.systemKey))) return;
      const id = `topic-${crypto.randomUUID()}`;
      setTopics((previous) => [...previous, { id, parentId: dialog.parentId, name: name.trim() }]);
      setSelectedTopicId(id);
    } else if (dialog.kind === 'rename') {
      if (!dialogTopic || dialogTopic.systemKey || !name.trim()) return;
      setTopics((previous) => previous.map((topic) => topic.id === dialog.id ? { ...topic, name: name.trim() } : topic));
    } else {
      if (!dialogTopic || dialogTopic.systemKey) return;
      setTopics((previous) => previous.filter((topic) => !deletedIds.has(topic.id)));
      setDocuments((previous) => previous.map((document) => deletedIds.has(document.topicId) ? { ...document, topicId: 'unassigned' } : document));
      if (deletedIds.has(selectedTopicId)) setSelectedTopicId(dialogTopic.parentId ?? 'unassigned');
    }
    closeDialog();
  }
  function moveTopic(id: TopicId, parentId: TopicId | null) {
    if (!canMoveTopic(topics, id, parentId)) { setNotice('Ein Thema kann nicht dorthin verschoben werden.'); return; }
    setTopics((previous) => previous.map((topic) => topic.id === id ? { ...topic, parentId } : topic));
    setNotice('Thema verschoben.');
  }
  return <AppShell sidebar={<Sidebar documentCount={documents.length} pageCount={documents.reduce((sum, document) => sum + document.pages, 0)} onAdd={() => openDialog({ kind: 'new', parentId: null })} onMoveRoot={(id) => moveTopic(id, null)}><TopicTree topics={topics} selectedTopicId={selectedTopicId} onSelect={setSelectedTopicId} documentTopicIds={documents.map((document) => document.topicId)} onNew={(id) => openDialog({ kind: 'new', parentId: id })} onRename={(id) => openDialog({ kind: 'rename', id })} onDelete={(id) => openDialog({ kind: 'delete', id })} onMove={moveTopic} /></Sidebar>}>
    <Toolbar title={path.at(-1)?.name ?? 'Unbekanntes Thema'} ancestors={path.slice(0, -1)} onNavigate={setSelectedTopicId} />
    <Content><section aria-label="Dokumentübersicht"><DocumentList documents={visibleDocuments} /></section><span role="status" className={styles.notice}>{notice}</span></Content>
    {dialog && <div className={styles.backdrop} onMouseDown={(event) => { if (event.target === event.currentTarget) closeDialog(); }}>
      <div className={styles.dialog} role="dialog" aria-modal="true" aria-labelledby="topic-dialog-title" ref={dialogRef} onKeyDown={(event) => {
        if (event.key === 'Escape') { event.stopPropagation(); closeDialog(); }
        if (event.key === 'Tab') {
          const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>('input, button:not(:disabled)'));
          if (event.shiftKey && document.activeElement === controls[0]) { event.preventDefault(); controls.at(-1)?.focus(); }
          else if (!event.shiftKey && document.activeElement === controls.at(-1)) { event.preventDefault(); controls[0]?.focus(); }
        }
      }}>
        <h2 id="topic-dialog-title">{dialog.kind === 'new' ? dialog.parentId ? `Neues Unterthema in „${topics.find((topic) => topic.id === dialog.parentId)?.name}“` : 'Neues Thema' : dialog.kind === 'rename' ? 'Thema umbenennen' : 'Thema löschen'}</h2>
        {dialog.kind === 'delete' ? <p>„{dialogTopic?.name}“{deletedIds.size > 1 ? ` und ${deletedIds.size - 1} Unterthema/-themen` : ''} löschen? {affectedDocuments > 0 && `${affectedDocuments} ${affectedDocuments === 1 ? 'Dokument wird' : 'Dokumente werden'} nach „Nicht zugeordnet“ verschoben.`}</p> : <form id="topic-form" onSubmit={(event) => { event.preventDefault(); submit(); }}><label htmlFor="topic-name">Name</label><input id="topic-name" value={name} onChange={(event) => setName(event.target.value)} /></form>}
        <div className={styles.actions}><button type="button" onClick={closeDialog}>Abbrechen</button><button type={dialog.kind === 'delete' ? 'button' : 'submit'} form={dialog.kind === 'delete' ? undefined : 'topic-form'} onClick={dialog.kind === 'delete' ? submit : undefined} disabled={dialog.kind !== 'delete' && !name.trim()}>{dialog.kind === 'delete' ? 'Löschen' : 'OK'}</button></div>
      </div>
    </div>}
  </AppShell>;
}
