import { Icon } from '../../components/Icon';
import type { DocumentSummary } from './document.types';
import styles from './DocumentSummary.module.css';
import { PdfThumbnail } from './PdfThumbnail';
import { DemoPdfThumbnail } from './DemoPdfThumbnail';
import { DemoInkPage } from './DemoInkPage';
export function TranscriptStatus({ document }: { readonly document: DocumentSummary }) {
  const status = !document.transcript
    ? 'missing'
    : document.transcript.edited
      ? 'edited'
      : 'available';
  const label =
    status === 'missing'
      ? 'Noch kein Transkript'
      : status === 'edited'
        ? 'Transkript vorhanden (bearbeitet)'
        : 'Transkript vorhanden';
  return (
    <span
      className={`${styles.status} ${status === 'missing' ? styles.missing : ''}`}
      aria-label={label}
      title={label}
    >
      <Icon name="text" />
      {status === 'missing' ? 'fehlt' : status === 'edited' ? 'bearb.' : ''}
    </span>
  );
}
export function DocumentActions({
  onTags,
  onOpenTab,
  document,
}: {
  readonly onTags?: () => void;
  readonly onOpenTab?: () => void;
  readonly document?: DocumentSummary;
}) {
  return (
    <div className={styles.actions}>
      <button disabled={!onTags} onClick={onTags} aria-label="Tags zuweisen">
        <Icon name="tag" />
      </button>
      <button disabled={!onOpenTab} onClick={onOpenTab} aria-label="In neuem Tab öffnen">
        <Icon name="external" />
      </button>
      {document?.demoUrl ? (
        <a
          href={document.demoUrl}
          download={document.fileName}
          aria-label="Herunterladen"
          onClick={(event) => event.stopPropagation()}
        >
          <Icon name="download" />
        </a>
      ) : (
        <button disabled aria-label="Herunterladen">
          <Icon name="download" />
        </button>
      )}
    </div>
  );
}
/** Reale PDF-Quellen werden bedarfsgerecht als Miniatur dargestellt. */
export function DemoPreview({ document }: { readonly document: DocumentSummary }) {
  if (document.demoUrl) return <DemoPdfThumbnail url={document.demoUrl} />;
  if (document.source?.kind === 'pdf') {
    return <PdfThumbnail blob={document.source.blob} />;
  }
  return <DemoInkPage document={document} page={1} label="Synthetische Demovorschau" />;
}
