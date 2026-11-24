import { Icon } from '../../components/Icon';
import type { DocumentSummary } from './document.types';
import styles from './DocumentSummary.module.css';
import { PdfThumbnail } from './PdfThumbnail';
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
}: {
  readonly onTags?: () => void;
  readonly onOpenTab?: () => void;
}) {
  return (
    <div className={styles.actions}>
      <button disabled={!onTags} onClick={onTags} aria-label="Tags zuweisen">
        <Icon name="tag" />
      </button>
      <button disabled={!onOpenTab} onClick={onOpenTab} aria-label="In neuem Tab öffnen">
        <Icon name="external" />
      </button>
      <button disabled aria-label="Herunterladen">
        <Icon name="download" />
      </button>
    </div>
  );
}
/** Synthetic demo ink only: no URL, file access or implied real PDF preview. */
export function DemoPreview({ document }: { readonly document: DocumentSummary }) {
  if (document.source?.kind === 'pdf') {
    return <PdfThumbnail blob={document.source.blob} />;
  }
  return <DemoInkPage document={document} page={1} label="Synthetische Demovorschau" />;
}
