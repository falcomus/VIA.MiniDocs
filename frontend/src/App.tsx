import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { AppShell, Content, Sidebar, Toolbar } from './components/AppShell';
import { topicDisplayName } from './features/topics/topicDisplayName';
import { TopicTree } from './features/topics/TopicTree';
import { useTopicDrag } from './features/topics/useTopicDrag';
import { TopicDialog } from './features/topics/TopicDialog';
import type { TopicDialogState } from './features/topics/TopicDialog';
import {
  canMoveTopic,
  deleteEmptyTopic,
  topicDeleteBlockReason,
  topicPath,
} from './features/topics/topicUtils';
import type { Topic, TopicId } from './features/topics/topic.types';
import { initialDocuments, initialTopics, initialTags } from './features/documents/demoDataset';
import { DocumentOverview } from './features/documents/DocumentOverview';
import type { DocumentView } from './features/documents/document.types';
import styles from './App.module.css';
import { useDocumentWorkspace } from './features/documents/useDocumentWorkspace';
import { useDocumentDrag } from './features/documents/useDocumentDrag';
import { useSearchState, emptySearch } from './features/search/useSearchState';
import type { SearchState } from './features/search/useSearchState';
import { useOpeningMode } from './features/viewer/openingMode';
import { DocumentViewer } from './features/viewer/DocumentViewer';
import { openViewerTab, startPage } from './features/viewer/viewerModel';
import { importPdfs } from './features/documents/pdfImport';
import {
  channelName,
  isRequest,
  isTranscriptUpdate,
  isTopicUpdate,
} from './features/viewer/pdfTransfer';
import type { PdfRequest } from './features/viewer/pdfTransfer';
import type { ViewerSession } from './features/viewer/viewerModel';
import { SelectionBar } from './features/documents/SelectionBar';
import { TagDialog } from './features/documents/TagDialog';
import type { TagDialogState } from './features/documents/TagDialog';

export function App() {
  const [view, setView] = useState<DocumentView>('grid');
  const [topics, setTopics] = useState<readonly Topic[]>(initialTopics);
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>(
    initialDocuments[0]?.topicId ?? 'unassigned',
  );
  const search = useSearchState();
  const opening = useOpeningMode();
  const [viewer, setViewer] = useState<ViewerSession | null>(null);
  const [tags, setTags] = useState(initialTags);
  const [tagDialog, setTagDialog] = useState<TagDialogState | null>(null);
  const workspace = useDocumentWorkspace(initialDocuments, topics, selectedTopicId, search);
  const [owner] = useState(() => crypto.randomUUID());
  const pendingAcks = useRef<string[]>([]);
  const pendingRequests = useRef<PdfRequest[]>([]);
  const transferChannel = useRef<BroadcastChannel | null>(null);
  const [ackSequence, setAckSequence] = useState(0);
  const editTranscript = useRef(workspace.editTranscript);
  const moveDocument = useRef(workspace.move);
  const previousTopics = useRef(new Map<string, string>());
  const previousTopicList = useRef(topics);
  useEffect(() => {
    editTranscript.current = workspace.editTranscript;
    moveDocument.current = workspace.move;
  }, [workspace.editTranscript, workspace.move]);
  function changeSearch(next: SearchState) {
    workspace.clear();
    search.setCriteria(next);
  }
  const { documents } = workspace;
  const transferData = useRef({ documents, topics, tags });
  useEffect(() => {
    transferData.current = { documents, topics, tags };
    for (const document of documents) {
      const previous = previousTopics.current.get(document.id);
      if (
        previous !== undefined &&
        (previous !== document.topicId || previousTopicList.current !== topics)
      )
        transferChannel.current?.postMessage({
          kind: 'topic-state',
          owner,
          documentId: document.id,
          topicId: document.topicId,
          topics,
        });
    }
    previousTopics.current = new Map(documents.map((document) => [document.id, document.topicId]));
    previousTopicList.current = topics;
    for (const requestId of pendingAcks.current.splice(0))
      transferChannel.current?.postMessage({ kind: 'transcript-ack', owner, requestId });
    for (const request of pendingRequests.current.splice(0)) {
      const document = documents.find((item) => item.id === request.documentId);
      if (document && (request.owner || document.source))
        transferChannel.current?.postMessage({
          kind: 'response',
          requestId: request.requestId,
          documentId: document.id,
          document,
          topics,
          tags,
        });
    }
  }, [documents, topics, tags, ackSequence, owner]);
  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return;
    const channel = new BroadcastChannel(channelName);
    transferChannel.current = channel;
    channel.onmessage = (event: MessageEvent<unknown>) => {
      if (isTopicUpdate(event.data)) {
        const update = event.data;
        if (
          update.owner !== owner ||
          !transferData.current.documents.some((doc) => doc.id === update.documentId) ||
          !transferData.current.topics.some((topic) => topic.id === update.topicId)
        )
          return;
        moveDocument.current([update.documentId], update.topicId);
        pendingAcks.current.push(update.requestId);
        setAckSequence((value) => value + 1);
        return;
      }
      if (isTranscriptUpdate(event.data)) {
        const update = event.data;
        if (
          update.owner !== owner ||
          !transferData.current.documents.some((doc) => doc.id === update.documentId)
        )
          return;
        editTranscript.current(update.documentId, update.operation);
        pendingAcks.current.push(update.requestId);
        setAckSequence((value) => value + 1);
        return;
      }
      if (!isRequest(event.data)) return;
      const request = event.data;
      if (request.owner && request.owner !== owner) return;
      if (pendingAcks.current.length) {
        pendingRequests.current.push(request);
        return;
      }
      const document = transferData.current.documents.find(
        (item) => item.id === request.documentId,
      );
      if (!document || (!request.owner && !document.source)) return;
      channel.postMessage({
        kind: 'response',
        requestId: request.requestId,
        documentId: document.id,
        document,
        topics: transferData.current.topics,
        tags: transferData.current.tags,
      });
    };
    return () => {
      transferChannel.current = null;
      channel.close();
    };
  }, [owner]);
  const importRef = useRef<(files: File[]) => void>(() => {});
  useEffect(() => {
    importRef.current = (files) => {
      void importPdfs(files).then(({ documents: imported, errors }) => {
        if (imported.length) {
          workspace.importDocuments(imported);
          setSelectedTopicId('unassigned');
          search.setCriteria({ ...emptySearch, mode: search.criteria.mode });
        }
        workspace.setMessage([`${imported.length} PDF importiert.`, ...errors].join(' '));
      });
    };
  });
  useEffect(() => {
    function isFiles(event: DragEvent) {
      return Array.from(event.dataTransfer?.types ?? []).includes('Files');
    }
    function over(event: DragEvent) {
      if (isFiles(event)) event.preventDefault();
    }
    function drop(event: DragEvent) {
      if (!isFiles(event)) return;
      event.preventDefault();
      event.stopPropagation();
      importRef.current(Array.from(event.dataTransfer?.files ?? []));
    }
    window.addEventListener('dragover', over, true);
    window.addEventListener('drop', drop, true);
    return () => {
      window.removeEventListener('dragover', over, true);
      window.removeEventListener('drop', drop, true);
    };
  }, []);
  const [dialog, setDialog] = useState<TopicDialogState | null>(null);
  const [dialogError, setDialogError] = useState('');
  const drag = useTopicDrag(topics, moveTopic, rejectMove);
  const [dropNavigation, setDropNavigation] = useState<{ id: TopicId } | null>(null);
  const documentDrag = useDocumentDrag(workspace, topics, drag.cancel, (id) => {
    setSelectedTopicId(id);
    setDropNavigation({ id });
  });
  function navigate(id: TopicId) {
    workspace.clear();
    search.setCriteria({ ...emptySearch, mode: search.criteria.mode });
    workspace.setMessage('');
    setSelectedTopicId(id);
  }
  const returnFocus = useRef<HTMLElement | null>(null);
  const fallbackFocus = useRef<TopicId>('unassigned');
  const restoreFocus = useRef(false);
  const documentTopicIds = documents.map((document) => document.topicId);
  const path = topicPath(topics, selectedTopicId);
  const dialogTopic =
    dialog && 'id' in dialog ? topics.find((topic) => topic.id === dialog.id) : undefined;
  const deleteReason =
    dialog?.kind === 'delete' ? topicDeleteBlockReason(topics, documents, dialog.id) : null;

  function openDocument(id: string, forceTab = false) {
    const doc = workspace.visible.find((item) => item.id === id);
    if (!doc) return;
    const hits = Object.fromEntries(
      workspace.visible.map((item) => [item.id, workspace.results.get(item.id)?.hit ?? null]),
    );
    const hit = hits[id];
    if (forceTab || opening.mode === 'tab') {
      if (!openViewerTab(id, startPage(doc, hit), hit?.term, owner))
        workspace.setMessage('Der Dokument-Tab konnte nicht geöffnet werden.');
      return;
    }
    const active = document.activeElement;
    returnFocus.current =
      active instanceof HTMLElement && active !== document.body
        ? active
        : (Array.from(document.querySelectorAll<HTMLElement>('[data-document-id]')).find(
            (item) => item.dataset.documentId === id,
          ) ?? null);
    fallbackFocus.current = selectedTopicId;
    setViewer({ id, ids: workspace.visible.map((item) => item.id), hits });
  }
  function openTags(next: TagDialogState) {
    returnFocus.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    fallbackFocus.current = selectedTopicId;
    setTagDialog(next);
  }
  function closeTags() {
    restoreFocus.current = true;
    setTagDialog(null);
  }
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
    if (dialog || tagDialog || viewer || !restoreFocus.current) return;
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
  }, [dialog, tagDialog, viewer, documents, workspace.selected.length]);

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
      const reason = topicDeleteBlockReason(topics, documents, dialog.id);
      if (reason || !dialogTopic) {
        setDialogError(reason ?? 'Dieses Thema existiert nicht mehr.');
        return;
      }
      fallbackFocus.current = dialogTopic.parentId ?? 'unassigned';
      setTopics((previous) => deleteEmptyTopic(previous, documents, dialog.id));
      if (selectedTopicId === dialog.id) navigate(dialogTopic.parentId ?? 'unassigned');
    }
    closeDialog();
  }
  const topicModal = dialog && (
    <TopicDialog
      dialog={dialog}
      topics={topics}
      deleteReason={deleteReason}
      error={dialogError}
      onCancel={closeDialog}
      onSubmit={submit}
    />
  );
  const tagModal = tagDialog && (
    <TagDialog
      key={tagDialog.kind}
      dialog={tagDialog}
      tags={tags}
      documents={documents}
      visibleIds={workspace.visible.map((doc) => doc.id)}
      onAssign={(ids, id, on) => workspace.setTag(ids, id, on)}
      onSave={(tag) => {
        setTags((previous) =>
          previous.some((item) => item.id === tag.id)
            ? previous.map((item) => (item.id === tag.id ? tag : item))
            : [...previous, tag],
        );
        workspace.setMessage('Tag gespeichert.');
      }}
      onDelete={(id) => {
        setTags((previous) => previous.filter((item) => item.id !== id));
        workspace.removeTag(id);
        search.setCriteria((previous) => ({
          ...previous,
          tagIds: previous.tagIds.filter((item) => item !== id),
        }));
      }}
      onManage={() => setTagDialog({ kind: 'manage' })}
      onClose={closeTags}
    />
  );
  return (
    <AppShell
      onImport={(files) => importRef.current(Array.from(files))}
      tags={tags}
      onManageTags={() => openTags({ kind: 'manage' })}
      modal={
        topicModal ||
        tagModal ||
        (viewer && (
          <DocumentViewer
            owner={owner}
            onPdfPages={workspace.setPdfPages}
            getSource={(id) => {
              const doc = documents.find((item) => item.id === id);
              return doc
                ? (doc.source ?? (doc.demoUrl ? { kind: 'pdf', blob: null } : { kind: 'demo' }))
                : undefined;
            }}
            onEditTranscript={workspace.editTranscript}
            onMoveTopic={(id, topicId) => workspace.move([id], topicId)}
            session={viewer}
            documents={documents}
            topics={topics}
            tags={tags}
            onClose={() => {
              restoreFocus.current = true;
              setViewer(null);
            }}
          />
        ))
      }
      openingMode={opening.mode}
      onOpeningModeChange={opening.setMode}
      search={search}
      onSearchChange={changeSearch}
      sidebar={
        <Sidebar
          documentCount={documents.length}
          pageCount={documents.reduce((sum, document) => sum + (document.pages ?? 0), 0)}
          unknownPageCount={documents.filter((document) => document.pages === null).length}
          onAdd={() => openDialog({ kind: 'new', parentId: null })}
          drag={drag}
        >
          <TopicTree
            topics={topics}
            selectedTopicId={selectedTopicId}
            onSelect={navigate}
            documentTopicIds={documentTopicIds}
            onNew={(id) => openDialog({ kind: 'new', parentId: id })}
            onRename={(id) => openDialog({ kind: 'rename', id })}
            onDelete={(id) => openDialog({ kind: 'delete', id })}
            onMoveDialog={(id) => openDialog({ kind: 'move', id })}
            documentDrag={documentDrag}
            dropNavigation={dropNavigation}
            drag={drag}
          />
        </Sidebar>
      }
    >
      <Toolbar
        searchActive={search.active}
        queryActive={!!search.criteria.query.trim()}
        sort={workspace.sort}
        onSortChange={workspace.setSort}
        zoom={workspace.zoom}
        onZoomChange={workspace.setZoom}
        view={view}
        onViewChange={setView}
        title={path.at(-1) ? topicDisplayName(path.at(-1)!, documentTopicIds) : 'Unbekanntes Thema'}
        ancestors={path
          .slice(0, -1)
          .map((topic) => ({ ...topic, name: topicDisplayName(topic, documentTopicIds) }))}
        onNavigate={navigate}
      />
      {search.active && (
        <div className={styles.searchResults}>
          <strong>{workspace.visible.length} Treffer</strong>
          <button onClick={() => changeSearch({ ...emptySearch, mode: search.criteria.mode })}>
            Suche beenden
          </button>
        </div>
      )}
      <SelectionBar
        count={workspace.selected.length}
        download={(() => {
          const doc = documents.find((item) => item.id === workspace.selected[0]);
          return workspace.selected.length === 1 && doc?.demoUrl && doc.fileName
            ? { url: doc.demoUrl, fileName: doc.fileName }
            : undefined;
        })()}
        hasDocuments={workspace.visible.length > 0}
        onAll={() => workspace.all(true)}
        onClear={() => {
          returnFocus.current = null;
          fallbackFocus.current = selectedTopicId;
          restoreFocus.current = true;
          workspace.clear();
        }}
        onOpen={() => {
          if (workspace.selected.length === 1) openDocument(workspace.selected[0]);
        }}
        onTags={() => openTags({ kind: 'picker', ids: workspace.selected })}
        onMove={() => openDialog({ kind: 'documents', ids: workspace.selected })}
      />
      <div className={styles.statusBar}>
        <span role="status" aria-label="Statusmeldung" className={styles.notice}>
          {workspace.message}
        </span>
        {workspace.undo && (
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
        )}
      </div>
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
            onOpen={openDocument}
            tags={tags}
            onTags={(id) =>
              openTags({
                kind: 'picker',
                ids: workspace.selected.includes(id) ? workspace.selected : [id],
              })
            }
            workspace={workspace}
            documentDrag={documentDrag}
            selectedTopicId={selectedTopicId}
            view={view}
            onNavigate={navigate}
          />
        </section>
      </Content>
    </AppShell>
  );
}
