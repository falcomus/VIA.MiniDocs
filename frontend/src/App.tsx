import { useLayoutEffect, useRef, useState } from 'react';
import { AppShell, Content, Sidebar, Toolbar } from './components/AppShell';
import { demoTopics } from './features/topics/demoTopics';
import { TopicTree } from './features/topics/TopicTree';
import { useTopicDrag } from './features/topics/useTopicDrag';
import { TopicDialog } from './features/topics/TopicDialog';
import type { TopicDialogState } from './features/topics/TopicDialog';
import { canMoveTopic, descendantIds, topicPath } from './features/topics/topicUtils';
import type { Topic, TopicId } from './features/topics/topic.types';
import { demoDocuments } from './features/documents/demoDocuments';
import { DocumentOverview } from './features/documents/DocumentOverview';
import type { DocumentView } from './features/documents/document.types';
import styles from './App.module.css';
import { useDocumentWorkspace } from './features/documents/useDocumentWorkspace';
import { useDocumentDrag } from './features/documents/useDocumentDrag';
import { SelectionBar } from './features/documents/SelectionBar';

export function App() {
  const [view, setView] = useState<DocumentView>('grid');
  const [topics, setTopics] = useState<readonly Topic[]>(demoTopics);
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>('unassigned');
  const workspace = useDocumentWorkspace(demoDocuments, topics, selectedTopicId);
  const { documents, update: setDocuments } = workspace;
  const [dialog, setDialog] = useState<TopicDialogState | null>(null);
  const [dialogError, setDialogError] = useState('');
  const drag = useTopicDrag(topics, moveTopic, rejectMove);
  const documentDrag = useDocumentDrag(workspace, topics, drag.cancel);
  function navigate(id: TopicId) {
    workspace.clear();
    setSelectedTopicId(id);
  }
  const returnFocus = useRef<HTMLElement | null>(null);
  const fallbackFocus = useRef<TopicId>('unassigned');
  const restoreFocus = useRef(false);
  const path = topicPath(topics, selectedTopicId);
  const dialogTopic =
    dialog && 'id' in dialog ? topics.find((topic) => topic.id === dialog.id) : undefined;
  const deletedIds =
    dialog?.kind === 'delete' && dialogTopic
      ? descendantIds(topics, dialogTopic.id)
      : new Set<TopicId>();
  const affectedDocuments = documents.filter((document) => deletedIds.has(document.topicId)).length;

  function openDialog(next: TopicDialogState) {
    setDialogError('');
    returnFocus.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    fallbackFocus.current = selectedTopicId;
    setDialog(next);
  }
  function closeDialog() {
    restoreFocus.current = true;
    setDialog(null);
  }

  // Restore only after React has committed deletion/movement and removed the inert background.
  useLayoutEffect(() => {
    if (dialog || !restoreFocus.current) return;
    restoreFocus.current = false;
    const previous = returnFocus.current;
    if (previous?.isConnected && !previous.closest('[inert]')) {
      previous.focus();
      return;
    }
    const target = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-topic-id]')).find(
      (item) => item.dataset.topicId === fallbackFocus.current,
    );
    (
      target ??
      document.querySelector<HTMLButtonElement>('[aria-label="Neues Thema auf oberster Ebene"]')
    )?.focus();
  }, [dialog, documents, workspace.selected.length]);

  function rejectMove() {
    workspace.setMessage('Ein Thema kann nicht dorthin verschoben werden.');
  }
  function moveTopic(id: TopicId, parentId: TopicId | null): boolean {
    if (!canMoveTopic(topics, id, parentId)) {
      rejectMove();
      return false;
    }
    setTopics((previous) =>
      previous.map((topic) => (topic.id === id ? { ...topic, parentId } : topic)),
    );
    workspace.setMessage('Thema verschoben.');
    return true;
  }
  function submit(value: string) {
    if (!dialog) return;
    if (dialog.kind === 'documents') {
      if (!topics.some((topic) => topic.id === value)) {
        setDialogError('Das gewählte Ziel ist nicht zulässig.');
        return;
      }
      workspace.move(dialog.ids, value);
    } else if (dialog.kind === 'new') {
      const parent = dialog.parentId && topics.find((topic) => topic.id === dialog.parentId);
      if (!value.trim() || (dialog.parentId !== null && (!parent || parent.systemKey))) return;
      const id = `topic-${crypto.randomUUID()}`;
      setTopics((previous) => [...previous, { id, parentId: dialog.parentId, name: value.trim() }]);
      navigate(id);
    } else if (dialog.kind === 'rename') {
      if (!dialogTopic || dialogTopic.systemKey || !value.trim()) return;
      setTopics((previous) =>
        previous.map((topic) =>
          topic.id === dialog.id ? { ...topic, name: value.trim() } : topic,
        ),
      );
    } else if (dialog.kind === 'move') {
      if (!moveTopic(dialog.id, value || null)) {
        setDialogError('Das gewählte Ziel ist nicht zulässig.');
        return;
      }
      fallbackFocus.current = value || 'unassigned';
    } else {
      if (!dialogTopic || dialogTopic.systemKey) return;
      fallbackFocus.current = dialogTopic.parentId ?? 'unassigned';
      setTopics((previous) => previous.filter((topic) => !deletedIds.has(topic.id)));
      setDocuments((previous) =>
        previous.map((document) =>
          deletedIds.has(document.topicId) ? { ...document, topicId: 'unassigned' } : document,
        ),
      );
      if (deletedIds.has(selectedTopicId)) navigate(dialogTopic.parentId ?? 'unassigned');
    }
    closeDialog();
  }
  const modal = dialog && (
    <TopicDialog
      dialog={dialog}
      topics={topics}
      affectedDocuments={affectedDocuments}
      descendantCount={deletedIds.size}
      error={dialogError}
      onCancel={closeDialog}
      onSubmit={submit}
    />
  );
  return (
    <AppShell
      modal={modal}
      sidebar={
        <Sidebar
          documentCount={documents.length}
          pageCount={documents.reduce((sum, document) => sum + document.pages, 0)}
          onAdd={() => openDialog({ kind: 'new', parentId: null })}
          drag={drag}
        >
          <TopicTree
            topics={topics}
            selectedTopicId={selectedTopicId}
            onSelect={navigate}
            documentTopicIds={documents.map((document) => document.topicId)}
            onNew={(id) => openDialog({ kind: 'new', parentId: id })}
            onRename={(id) => openDialog({ kind: 'rename', id })}
            onDelete={(id) => openDialog({ kind: 'delete', id })}
            onMoveDialog={(id) => openDialog({ kind: 'move', id })}
            documentDrag={documentDrag}
            drag={drag}
          />
        </Sidebar>
      }
    >
      <Toolbar
        sort={workspace.sort}
        onSortChange={workspace.setSort}
        zoom={workspace.zoom}
        onZoomChange={workspace.setZoom}
        view={view}
        onViewChange={setView}
        title={path.at(-1)?.name ?? 'Unbekanntes Thema'}
        ancestors={path.slice(0, -1)}
        onNavigate={navigate}
      />
      <SelectionBar
        count={workspace.selected.length}
        hasDocuments={workspace.visible.length > 0}
        onAll={() => workspace.all(true)}
        onClear={() => {
          returnFocus.current = null;
          fallbackFocus.current = selectedTopicId;
          restoreFocus.current = true;
          workspace.clear();
        }}
        onMove={() => openDialog({ kind: 'documents', ids: workspace.selected })}
      />
      <Content>
        <section
          aria-label="Dokumentübersicht"
          onKeyDown={(event) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a') {
              event.preventDefault();
              workspace.all(true);
            } else if (event.key === 'Escape') workspace.clear();
          }}
        >
          <DocumentOverview
            topics={topics}
            workspace={workspace}
            documentDrag={documentDrag}
            selectedTopicId={selectedTopicId}
            view={view}
            onNavigate={navigate}
          />
        </section>
        <span role="status" aria-label="Statusmeldung" className={styles.notice}>
          {workspace.message}
        </span>
        {workspace.undo && (
          <div className={styles.toast}>
            <span>
              {workspace.undo.size}{' '}
              {workspace.undo.size === 1 ? 'Dokument verschoben.' : 'Dokumente verschoben.'}
            </span>
            <button
              onClick={() => {
                returnFocus.current = null;
                fallbackFocus.current = selectedTopicId;
                restoreFocus.current = true;
                workspace.undoMove();
              }}
            >
              Rückgängig
            </button>
          </div>
        )}
      </Content>
    </AppShell>
  );
}
