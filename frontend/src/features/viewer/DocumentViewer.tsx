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
  onPdfPages,
  getSource,
  owner,
  onReturnOverview,
}: {
  readonly session: ViewerSession;
  readonly documents: readonly DocumentSummary[];
  readonly topics: readonly Topic[];
  readonly tags: readonly TagDefinition[];
  readonly onClose?: () => void;
  readonly initialPage?: number;
  readonly getSource: DocumentPageSourceResolver;
  readonly owner?: string;
  readonly onReturnOverview?: () => void;
  readonly onEditTranscript: (id: string, operation: TranscriptOperation) => void;
  readonly onPdfPages?: (id: string, blob: Blob, pages: number) => void;
}) {
  const [id, setId] = useState(session.id);
  const current = documents.find((doc) => doc.id === id);
  const source = getSource(id) ?? { kind: 'pdf', blob: null };
  const [page, setPage] = useState(() =>
    current ? (initialPage ?? startPage(current, session.hits[id])) : 1,
  );
  const [zoom, setZoom] = useState(100);
  const [navigation, setNavigation] = useState(0);
  const navigateToPage = (next: number) => {
    setPage(next);
    setNavigation((value) => value + 1);
  };
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
      navigateToPage(startPage(next, session.hits[next.id]));
    }
  };
  const pages =
    current?.pages && Number.isInteger(current.pages) && current.pages > 0 ? current.pages : 1;
  const validPage = Math.max(1, Math.min(page, pages));
  const fileDetails = current
    ? `${current.fileName ?? '—'} · ${current.kind} · ${
        current.pages === null ? 'Seitenzahl unbekannt' : `${current.pages} Seiten`
      }`
    : '';
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
          <nav className={styles.documentNavigation} aria-label="Dokumentnavigation">
            <button
              ref={previousDocument}
              className={styles.iconButton}
              type="button"
              aria-label="Vorheriges Dokument"
              title="Vorheriges Dokument"
              disabled={index <= 0}
              onClick={() => step(-1)}
            >
              <span aria-hidden="true">‹</span>
            </button>
            <button
              ref={nextDocument}
              className={styles.iconButton}
              type="button"
              aria-label="Nächstes Dokument"
              title="Nächstes Dokument"
              disabled={index < 0 || index >= ids.length - 1}
              onClick={() => step(1)}
            >
              <span aria-hidden="true">›</span>
            </button>
          </nav>
          <h1 id="viewer-title" title={current?.title}>
            {current?.title ?? 'Dokument nicht gefunden'}
          </h1>
          <div className={styles.headerActions}>
            {index >= 0 && ids.length > 1 && (
              <span
                className={styles.documentCounter}
                aria-label={`Dokument ${index + 1} von ${ids.length}`}
              >
                {index + 1} / {ids.length}
              </span>
            )}
            {current && onClose && (
              <button
                className={styles.actionButton}
                type="button"
                aria-label="In neuem Tab öffnen"
                onClick={() => {
                  if (!openViewerTab(current.id, validPage, undefined, owner))
                    setTabError('Der Dokument-Tab konnte nicht geöffnet werden.');
                }}
              >
                <span aria-hidden="true">↗</span> Neuer Tab
              </button>
            )}
            {onClose ? (
              <button
                className={styles.iconButton}
                type="button"
                aria-label="Schließen"
                title="Schließen (Esc)"
                onClick={onClose}
              >
                <span aria-hidden="true">×</span>
              </button>
            ) : onReturnOverview ? (
              <button className={styles.actionButton} type="button" onClick={onReturnOverview}>
                Zur Dokumentübersicht
              </button>
            ) : (
              <a className={styles.actionButton} href={window.location.pathname}>
                Zur Dokumentübersicht
              </a>
            )}
          </div>
        </header>
        {tabError && <p role="alert">{tabError}</p>}
        {current ? (
          <>
            <div className={styles.meta}>
              <span className={styles.metaLabel}>Datum</span>
              <span className={styles.metaValue}>{formatDocumentDate(current.date)}</span>
              <span className={styles.metaLabel}>Thema</span>
              <span className={styles.topicName}>
                {topicPath(topics, current.topicId)
                  .map((topic) => topic.name)
                  .join(' / ') || 'Unbekanntes Thema'}
              </span>
              <span className={styles.metaLabel}>Tags</span>
              <TagChips ids={current.tagIds} tags={tags} />
              <TranscriptStatus document={current} />
              {session.hits[id]?.term && startPage(current, session.hits[id]) === validPage && (
                <span className={styles.searchHit}>Suchtreffer: {session.hits[id]?.term}</span>
              )}
              <span className={styles.fileInfo} title={fileDetails}>
                {fileDetails}
              </span>
              {!onClose && (
                <span className={styles.tabNotice}>
                  {owner
                    ? 'Änderungen werden an die offene Dokumentübersicht übertragen; sie sind nicht dauerhaft gespeichert.'
                    : 'Änderungen aus anderen Tabs werden nicht übernommen und sind nicht dauerhaft gespeichert.'}
                </span>
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
                tabIndex={0}
                aria-label="Dokumentbereich"
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
                <div className={styles.pageControls} role="toolbar" aria-label="Dokumentansicht">
                  {(source.kind === 'demo' || source.blob) && (
                    <div className={styles.zoomControls}>
                      <button
                        className={styles.toolbarButton}
                        type="button"
                        aria-label="Verkleinern"
                        title="Verkleinern"
                        disabled={zoom <= 50}
                        onClick={() => setZoom((value) => Math.max(50, value - 10))}
                      >
                        −
                      </button>
                      <input
                        className={styles.zoomSlider}
                        type="range"
                        aria-label="Viewer-Zoom"
                        min="50"
                        max="200"
                        step="10"
                        value={zoom}
                        onChange={(event) => setZoom(Number(event.target.value))}
                      />
                      <button
                        className={styles.toolbarButton}
                        type="button"
                        aria-label="Vergrößern"
                        title="Vergrößern"
                        disabled={zoom >= 200}
                        onClick={() => setZoom((value) => Math.min(200, value + 10))}
                      >
                        +
                      </button>
                      <span className={styles.zoomValue}>{zoom} %</span>
                    </div>
                  )}
                  {current.pages === null ? (
                    <span className={styles.pageLoading}>
                      PDF wird geladen; die Seitenzahl ist noch unbekannt.
                    </span>
                  ) : (
                    <div className={styles.pageNavigation}>
                      <button
                        ref={previousPage}
                        className={styles.toolbarButton}
                        type="button"
                        aria-label="Vorherige Seite"
                        title="Vorherige Seite"
                        disabled={validPage <= 1}
                        onClick={() => {
                          if (validPage === 2) pendingFocus.current = nextPage;
                          navigateToPage(validPage - 1);
                        }}
                      >
                        ‹
                      </button>
                      <label className={styles.pagePicker}>
                        Seite
                        <select
                          ref={pageSelect}
                          aria-label="Seite"
                          value={validPage}
                          onChange={(event) => navigateToPage(Number(event.target.value))}
                        >
                          {Array.from({ length: pages }, (_, i) => (
                            <option key={i + 1} value={i + 1}>
                              {i + 1}
                            </option>
                          ))}
                        </select>
                        / {pages}
                      </label>
                      <button
                        ref={nextPage}
                        className={styles.toolbarButton}
                        type="button"
                        aria-label="Nächste Seite"
                        title="Nächste Seite"
                        disabled={validPage >= pages}
                        onClick={() => {
                          if (validPage + 1 === pages) pendingFocus.current = previousPage;
                          navigateToPage(validPage + 1);
                        }}
                      >
                        ›
                      </button>
                    </div>
                  )}
                  {(source.kind === 'demo' || source.blob) && (
                    <label className={styles.syncScroll}>
                      <input
                        type="checkbox"
                        checked={syncScroll}
                        onChange={(event) => setSyncScroll(event.target.checked)}
                      />
                      Synchron scrollen {source.kind === 'demo' ? '(ungefähr)' : '(seitenweise)'}
                    </label>
                  )}
                </div>
                <div className={styles.pageViewport}>
                  <DocumentPagePane
                    source={source}
                    document={current}
                    page={validPage}
                    zoom={zoom}
                    navigation={navigation}
                    onVisiblePage={setPage}
                    onPdfLoaded={(blob, count) => {
                      onPdfPages?.(current.id, blob, count);
                      setPage((previous) => Math.max(1, Math.min(previous, count)));
                    }}
                  />
                </div>
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
                onPageChange={navigateToPage}
                followPage={source.kind === 'demo' || syncScroll}
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
