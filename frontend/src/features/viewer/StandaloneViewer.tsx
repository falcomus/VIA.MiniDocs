import { useEffect, useReducer, useRef, useState } from 'react';
import { documentReducer } from '../documents/documentOperations';
import { demoDocuments } from '../documents/demoDocuments';
import { demoTags } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import type { DocumentSummary } from '../documents/document.types';
import type { TagDefinition } from '../documents/tags';
import type { Topic } from '../topics/topic.types';
import { useTheme } from '../settings/useTheme';
import { DocumentViewer } from './DocumentViewer';
import { channelName, matchesResponse } from './pdfTransfer';
import type { PdfResponse } from './pdfTransfer';
import type { TranscriptOperation } from '../documents/transcriptOperations';

/** A received Blob belongs to this tab; no continued connection or opener is needed. */
export function StandaloneViewer() {
  useTheme();
  const params = new URLSearchParams(window.location.search);
  const id = params.get('document') ?? '';
  const isViewer = params.get('viewer') === 'tab';
  const owner = params.get('owner') ?? '';
  const rawPage = params.get('page');
  const page = rawPage === null ? 1 : Number(rawPage);
  const demo = demoDocuments.find((item) => item.id === id);
  const [transfer, setTransfer] = useState<PdfResponse | 'missing' | null>(null);
  const [pending, setPending] = useState(0);
  const [message, setMessage] = useState('');
  const [topicState, setTopicState] = useState<{
    documentId: string;
    topicId: string;
    topics: readonly Topic[];
  } | null>(null);
  const channelRef = useRef<BroadcastChannel | null>(null);
  const pendingIds = useRef(new Set<string>());
  const returnRequested = useRef(false);
  const closeAfterAck = () => {
    window.close();
    setMessage('Bitte zum weiterhin geöffneten Übersichtstab zurückkehren.');
  };
  useEffect(() => {
    if (!pending) return;
    const timeout = window.setTimeout(() => {
      setMessage(
        'Keine Bestätigung von der Dokumentübersicht. Bitte diesen Tab geöffnet lassen und die Übersicht prüfen.',
      );
    }, 5000);
    return () => window.clearTimeout(timeout);
  }, [pending]);
  useEffect(() => {
    if ((demo && !owner) || !isViewer || !id) return;
    if (typeof BroadcastChannel === 'undefined') return;
    const requestId = crypto.randomUUID();
    const channel = new BroadcastChannel(channelName);
    channelRef.current = channel;
    const timeout = window.setTimeout(() => {
      channel.close();
      setTransfer('missing');
    }, 5000);
    channel.onmessage = (event: MessageEvent<unknown>) => {
      const data = event.data as Record<string, unknown> | null;
      if (
        owner &&
        data?.kind === 'transcript-ack' &&
        data.owner === owner &&
        typeof data.requestId === 'string' &&
        pendingIds.current.delete(data.requestId)
      ) {
        setPending(pendingIds.current.size);
        if (returnRequested.current && pendingIds.current.size === 0) closeAfterAck();
        return;
      }
      if (
        data?.kind === 'topic-state' &&
        data.owner === owner &&
        data.documentId === id &&
        typeof data.topicId === 'string' &&
        Array.isArray(data.topics)
      ) {
        setTopicState({ documentId: id, topicId: data.topicId, topics: data.topics as Topic[] });
        return;
      }
      if (!matchesResponse(event.data, requestId, id)) return;
      window.clearTimeout(timeout);
      if (!owner) channel.close();
      setTransfer(event.data);
    };
    channel.postMessage({
      kind: 'request',
      requestId,
      documentId: id,
      ...(owner ? { owner } : {}),
    });
    return () => {
      window.clearTimeout(timeout);
      channel.close();
      channelRef.current = null;
    };
  }, [demo, id, isViewer, owner]);
  if (
    !isViewer ||
    !id ||
    !Number.isInteger(page) ||
    page < 1 ||
    (demo && demo.pages !== null && page > demo.pages)
  )
    return <Unavailable />;
  if ((!demo || owner) && typeof BroadcastChannel === 'undefined') return <Unavailable />;
  if ((!demo || owner) && !transfer)
    return <main role="status">Dokument wird aus dem Hauptfenster angefordert …</main>;
  if ((!demo || owner) && transfer === 'missing') return <Unavailable />;
  const received = transfer && transfer !== 'missing' ? transfer : null;
  const doc = received?.document ?? demo;
  if (!doc) return <Unavailable />;
  return (
    <>
      {message && <p role="status">{message}</p>}
      <StandaloneDocument
        key={doc.id}
        doc={doc}
        page={page}
        term={params.get('term') ?? ''}
        topics={topicState?.topics ?? received?.topics ?? demoTopics}
        topicState={topicState}
        tags={received?.tags ?? demoTags}
        owner={owner}
        onMove={(documentId, topicId) => {
          if (!owner || !channelRef.current) return;
          const requestId = crypto.randomUUID();
          pendingIds.current.add(requestId);
          setPending(pendingIds.current.size);
          channelRef.current.postMessage({
            kind: 'topic-update',
            owner,
            requestId,
            documentId,
            topicId,
          });
        }}
        onEdit={(documentId, operation) => {
          if (!owner || !channelRef.current) return;
          const requestId = crypto.randomUUID();
          pendingIds.current.add(requestId);
          setPending(pendingIds.current.size);
          channelRef.current.postMessage({
            kind: 'transcript-update',
            owner,
            requestId,
            documentId,
            operation,
          });
        }}
        onReturn={
          owner
            ? () => {
                if (pendingIds.current.size) {
                  returnRequested.current = true;
                  setMessage('Änderungen werden noch an die Dokumentübersicht übertragen …');
                } else closeAfterAck();
              }
            : undefined
        }
      />
      {pending > 0 && <p role="status">{pending} Änderung(en) warten auf Bestätigung.</p>}
    </>
  );
}
function Unavailable() {
  return (
    <main>
      <h1>Dokument nicht verfügbar</h1>
      <p>
        Das Dokument konnte nicht aus dem Hauptfenster übernommen werden. Öffnen Sie es dort erneut;
        nach Neuladen gehen lokale PDFs verloren.
      </p>
      <a href={window.location.pathname}>Zur Dokumentübersicht</a>
    </main>
  );
}
function StandaloneDocument({
  doc,
  page,
  term,
  topics,
  tags,
  owner,
  onEdit,
  onMove,
  topicState,
  onReturn,
}: {
  readonly doc: DocumentSummary;
  readonly page: number;
  readonly term: string;
  readonly topics: readonly Topic[];
  readonly tags: readonly TagDefinition[];
  readonly owner: string;
  readonly onEdit: (id: string, operation: TranscriptOperation) => void;
  readonly onMove: (id: string, topicId: string) => void;
  readonly topicState: { documentId: string; topicId: string; topics: readonly Topic[] } | null;
  readonly onReturn?: () => void;
}) {
  const [state, dispatch] = useReducer(documentReducer, {
    documents: [doc],
    selected: [],
    anchor: null,
    undo: null,
    message: '',
  });
  useEffect(() => {
    if (topicState?.documentId === doc.id)
      dispatch({
        type: 'move',
        ids: [doc.id],
        target: topicState.topicId,
        topics: topicState.topics,
      });
  }, [topicState, doc.id]);
  return (
    <DocumentViewer
      owner={owner}
      onReturnOverview={onReturn}
      onPdfPages={(id, blob, pages) => dispatch({ type: 'pdf-pages', id, blob, pages })}
      getSource={(id) => {
        const current = state.documents.find((item) => item.id === id);
        return current ? (current.source ?? { kind: 'demo' }) : undefined;
      }}
      session={{ id: doc.id, ids: [doc.id], hits: { [doc.id]: { page, term } } }}
      initialPage={page}
      documents={state.documents}
      onMoveTopic={(id, topicId) => {
        onMove(id, topicId);
        dispatch({ type: 'move', ids: [id], target: topicId, topics });
      }}
      onEditTranscript={(id, operation) => {
        onEdit(id, operation);
        dispatch({
          type: 'transcript',
          id,
          operation,
          visibleIds: (documents) => documents.map((document) => document.id),
        });
      }}
      topics={topics}
      tags={tags}
    />
  );
}
