import { Icon } from '../../components/Icon';
import type { DocumentSummary } from './document.types';
import styles from './DocumentSummary.module.css';
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
export function DocumentActions({ onTags }: { readonly onTags?: () => void }) {
  return (
    <div className={styles.actions}>
      <button disabled={!onTags} onClick={onTags} aria-label="Tags zuweisen">
        <Icon name="tag" />
      </button>
      <button disabled aria-label="In neuem Tab öffnen">
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
  return (
    <svg
      className={styles.preview}
      viewBox="0 0 210 297"
      role="img"
      aria-label="Synthetische Demovorschau"
    >
      <rect width="210" height="297" fill="#faf9f5" />
      <text x="18" y="25" fontSize="9" fill="#667085">
        DEMO · {document.kind}
      </text>
      {(
        document.demoPreview?.lines ?? [
          document.title,
          'Demo-Notizen',
          'Besprechung und nächste Schritte',
        ]
      ).map((line, index) => (
        <text key={index} x="18" y={55 + index * 22} fontSize="8" fill="#344054">
          {line.slice(0, 36)}
        </text>
      ))}
    </svg>
  );
}
