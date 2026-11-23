import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { DocumentSummary } from '../documents/document.types';
import type { Topic } from '../topics/topic.types';
import { topicPath } from '../topics/topicUtils';
import type { TagDefinition } from '../documents/tags';
import { TagChips } from '../documents/TagChips';
import { TranscriptStatus } from '../documents/DocumentSummary';
import { formatDocumentDate } from '../documents/documentDate';
import { openViewerTab, startPage } from './viewerModel';
import type { ViewerSession } from './viewerModel';
import { TranscriptPane } from './TranscriptPane';
import type { TranscriptOperation } from '../documents/transcriptOperations';
import { DocumentPagePane } from './DocumentPagePane';
import type { DocumentPageSourceResolver } from './DocumentPagePane';
import styles from './DocumentViewer.module.css';
export function DocumentViewer({
  session,
  documents,
  topics,
  tags,
  onClose,
  initialPage,
  onEditTranscript,
  getSource,
}: {
  readonly session: ViewerSession;
  readonly documents: readonly DocumentSummary[];
  readonly topics: readonly Topic[];
  readonly tags: readonly TagDefinition[];
  readonly onClose?: () => void;
  readonly initialPage?: number;
  readonly getSource: DocumentPageSourceResolver;
  readonly onEditTranscript: (id: string, operation: TranscriptOperation) => void;
}) {
  const [id, setId] = useState(session.id);
  const current = documents.find((doc) => doc.id === id);
  const source = getSource(id) ?? { kind: 'pdf', blob: null };
  const [page, setPage] = useState(() =>
    current ? (initialPage ?? startPage(current, session.hits[id])) : 1,
  );
  const [zoom, setZoom] = useState(100);
  const [tabError, setTabError] = useState('');
  const [split, setSplit] = useState(56);
  const [syncScroll, setSyncScroll] = useState(true);
  const body = useRef<HTMLDivElement>(null);
  const splitter = useRef<HTMLDivElement>(null);
  const pageSelect = useRef<HTMLSelectElement>(null);
  const dragPointer = useRef<number | null>(null);
  const editor = useRef<HTMLTextAreaElement>(null);
  const ref = useRef<HTMLDivElement>(null);
  const previousPage = useRef<HTMLButtonElement>(null);
  const nextPage = useRef<HTMLButtonElement>(null);
  const previousDocument = useRef<HTMLButtonElement>(null);
  const nextDocument = useRef<HTMLButtonElement>(null);
  const pendingFocus = useRef<React.RefObject<HTMLButtonElement | null> | null>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
  }, []);
  useEffect(() => {
    function handleResize() {
      const element = splitter.current;
      if (!element || getComputedStyle(element).display !== 'none') return;
      if (dragPointer.current !== null && element.hasPointerCapture(dragPointer.current))
        element.releasePointerCapture(dragPointer.current);
      dragPointer.current = null;
      if (document.activeElement === element) pageSelect.current?.focus();
    }
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);
  useLayoutEffect(() => {
    pendingFocus.current?.current?.focus();
    pendingFocus.current = null;
  }, [page, id]);
  const ids = session.ids.filter((item) => documents.some((doc) => doc.id === item));
  const index = ids.indexOf(id);
  const step = (offset: number) => {
    const next = documents.find((doc) => doc.id === ids[index + offset]);
    if (next) {
      if (
        offset > 0 &&
        index + offset === ids.length - 1 &&
        document.activeElement === nextDocument.current
      )
        pendingFocus.current = previousDocument;
      if (offset < 0 && index + offset === 0 && document.activeElement === previousDocument.current)
        pendingFocus.current = nextDocument;
      setId(next.id);
      setPage(startPage(next, session.hits[next.id]));
    }
  };
  const pages = current && Number.isInteger(current.pages) && current.pages > 0 ? current.pages : 1;
  const validPage = Math.max(1, Math.min(page, pages));
  return (
    <div className={onClose ? styles.backdrop : styles.standalone}>
      <div
        ref={ref}
        className={styles.viewer}
        role={onClose ? 'dialog' : undefined}
        aria-modal={onClose ? 'true' : undefined}
        aria-labelledby="viewer-title"
        onKeyDown={(event) => {
          if (event.key === 'Escape' && onClose) {
            event.stopPropagation();
            onClose();
            return;
          }
          if (event.key === 'Tab' && onClose) {
            const controls = Array.from(
              event.currentTarget.querySelectorAll<HTMLElement>(
                'button:not(:disabled), select:not(:disabled), input:not(:disabled), textarea:not(:disabled), a[href], [role=separator][tabindex="0"]',
              ),
            );
            if (event.shiftKey && document.activeElement === controls[0]) {
              event.preventDefault();
              controls.at(-1)?.focus();
            } else if (!event.shiftKey && document.activeElement === controls.at(-1)) {
              event.preventDefault();
              controls[0]?.focus();
            }
          }
          if (
            (event.target as HTMLElement).closest(
              'input,select,textarea,[role=separator],[contenteditable="true"]',
            )
          )
            return;
          if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            event.preventDefault();
            event.stopPropagation();
            step(event.key === 'ArrowLeft' ? -1 : 1);
          }
        }}
      >
        <header className={styles.header}>
          <h1 id="viewer-title">{current?.title ?? 'Dokument nicht gefunden'}</h1>
          <div className={styles.controls}>
            <button ref={previousDocument} disabled={index <= 0} onClick={() => step(-1)}>
              Vorheriges Dokument
            </button>
            <button
              ref={nextDocument}
              disabled={index < 0 || index >= ids.length - 1}
              onClick={() => step(1)}
            >
              Nächstes Dokument
            </button>
            {current && onClose && (
              <button
                onClick={() => {
                  if (!openViewerTab(current.id, validPage))
                    setTabError('Der Dokument-Tab konnte nicht geöffnet werden.');
                }}
              >
                In neuem Tab öffnen
              </button>
            )}
            {onClose ? (
              <button onClick={onClose}>Schließen</button>
            ) : (
              <a href={window.location.pathname}>Zur Dokumentübersicht</a>
            )}
          </div>
        </header>
        {tabError && <p role="alert">{tabError}</p>}
        {current ? (
          <>
            <div className={styles.meta}>
              {!onClose && (
                <span>Demodaten: Änderungen aus anderen Tabs werden nicht übernommen.</span>
              )}
              <span>
                {current.fileName ?? '—'} · {formatDocumentDate(current.date)} · {current.kind} ·{' '}
                {current.pages} Seiten
              </span>
              <span>
                {topicPath(topics, current.topicId)
                  .map((topic) => topic.name)
                  .join(' / ') || 'Unbekanntes Thema'}
              </span>
              <TranscriptStatus document={current} />
              <TagChips ids={current.tagIds} tags={tags} />
              {session.hits[id]?.term && startPage(current, session.hits[id]) === validPage && (
                <span>Suchtreffer: {session.hits[id]?.term}</span>
              )}
            </div>
            <div className={styles.controls}>
              <button
                ref={previousPage}
                disabled={validPage <= 1}
                onClick={() => {
                  if (validPage === 2) pendingFocus.current = nextPage;
                  setPage(validPage - 1);
                }}
              >
                Vorherige Seite
              </button>
              <label>
                Seite{' '}
                <select
                  ref={pageSelect}
                  value={validPage}
                  onChange={(event) => setPage(Number(event.target.value))}
                >
                  {Array.from({ length: pages }, (_, i) => (
                    <option key={i + 1} value={i + 1}>
                      {i + 1}
                    </option>
                  ))}
                </select>{' '}
                von {pages}
              </label>
              <button
                ref={nextPage}
                disabled={validPage >= pages}
                onClick={() => {
                  if (validPage + 1 === pages) pendingFocus.current = previousPage;
                  setPage(validPage + 1);
                }}
              >
                Nächste Seite
              </button>
              {source.kind === 'demo' && (
                <label>
                  Viewer-Zoom{' '}
                  <input
                    type="range"
                    min="50"
                    max="200"
                    step="10"
                    value={zoom}
                    onChange={(event) => setZoom(Number(event.target.value))}
                  />{' '}
                  {zoom} %
                </label>
              )}
              {source.kind === 'demo' && (
                <label>
                  <input
                    type="checkbox"
                    checked={syncScroll}
                    onChange={(event) => setSyncScroll(event.target.checked)}
                  />{' '}
                  Synchron scrollen (ungefähr)
                </label>
              )}
            </div>
            <div
              ref={body}
              className={styles.body}
              style={{ '--split': split + '%' } as React.CSSProperties}
            >
              <div
                id="viewer-document-pane"
                className={styles.pages}
                onScroll={(event) => {
                  if (!syncScroll || source.kind !== 'demo' || !editor.current) return;
                  const origin = event.currentTarget;
                  const max = origin.scrollHeight - origin.clientHeight;
                  const target = editor.current;
                  if (max > 0)
                    target.scrollTop =
                      (origin.scrollTop / max) *
                      Math.max(0, target.scrollHeight - target.clientHeight);
                }}
              >
                <DocumentPagePane source={source} document={current} page={validPage} zoom={zoom} />
              </div>
              <div
                ref={splitter}
                className={styles.splitter}
                role="separator"
                aria-label="Breite des Dokumentbereichs"
                aria-controls="viewer-document-pane"
                aria-orientation="vertical"
                aria-valuemin={25}
                aria-valuemax={75}
                aria-valuenow={split}
                tabIndex={0}
                onKeyDown={(event) => {
                  const value =
                    event.key === 'Home'
                      ? 25
                      : event.key === 'End'
                        ? 75
                        : event.key === 'ArrowLeft'
                          ? split - (event.shiftKey ? 10 : 1)
                          : event.key === 'ArrowRight'
                            ? split + (event.shiftKey ? 10 : 1)
                            : null;
                  if (value !== null) {
                    event.preventDefault();
                    event.stopPropagation();
                    setSplit(Math.max(25, Math.min(75, value)));
                  }
                }}
                onPointerDown={(event) => {
                  if (event.button !== 0 || dragPointer.current !== null) return;
                  event.preventDefault();
                  event.currentTarget.focus();
                  event.currentTarget.setPointerCapture(event.pointerId);
                  dragPointer.current = event.pointerId;
                }}
                onPointerMove={(event) => {
                  if (
                    dragPointer.current !== event.pointerId ||
                    !event.currentTarget.hasPointerCapture(event.pointerId) ||
                    !body.current
                  )
                    return;
                  const rect = body.current.getBoundingClientRect();
                  if (rect.width > 0)
                    setSplit(
                      Math.max(
                        25,
                        Math.min(75, Math.round(((event.clientX - rect.left) / rect.width) * 100)),
                      ),
                    );
                }}
                onPointerUp={(event) => {
                  if (dragPointer.current !== event.pointerId) return;
                  if (event.currentTarget.hasPointerCapture(event.pointerId))
                    event.currentTarget.releasePointerCapture(event.pointerId);
                  dragPointer.current = null;
                }}
                onPointerCancel={(event) => {
                  if (dragPointer.current !== event.pointerId) return;
                  if (event.currentTarget.hasPointerCapture(event.pointerId))
                    event.currentTarget.releasePointerCapture(event.pointerId);
                  dragPointer.current = null;
                }}
                onLostPointerCapture={(event) => {
                  if (dragPointer.current === event.pointerId) dragPointer.current = null;
                }}
              />
              <TranscriptPane
                key={current.id}
                document={current}
                page={validPage}
                editorRef={editor}
                onPageChange={setPage}
                onEdit={(operation) => onEditTranscript(current.id, operation)}
              />
            </div>
          </>
        ) : (
          <p role="alert">Das angeforderte Dokument ist nicht verfügbar.</p>
        )}
      </div>
    </div>
  );
}
