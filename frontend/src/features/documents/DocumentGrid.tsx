import { Icon } from '../../components/Icon';
import type { DocumentItemsProps } from './DocumentList';
import { DemoPreview, DocumentActions, TranscriptStatus } from './DocumentSummary';
import { formatDocumentDate } from './documentDate';
import styles from './DocumentGrid.module.css';
export function DocumentGrid({ documents, folders, folderCounts, onNavigate }: DocumentItemsProps) {
  return (
    <ul className={styles.grid} aria-label="Kachelansicht">
      {folders.map((folder) => (
        <li key={folder.id} className={styles.tile}>
          <button className={styles.folder} onClick={() => onNavigate(folder.id)}>
            <span className={`${styles.thumb} ${styles.folderThumb}`}>
              <Icon name="folder" />
            </span>
            <span className={styles.meta}>
              <span className={styles.title}>{folder.name}</span>
              <span className={styles.sub}>{folderCounts.get(folder.id) ?? 0} Dokumente</span>
            </span>
          </button>
        </li>
      ))}
      {documents.map((document) => (
        <li key={document.id} className={styles.tile}>
          <div className={styles.actions}>
            <DocumentActions />
          </div>
          <div className={styles.thumb}>
            <DemoPreview document={document} />
          </div>
          <div className={styles.meta}>
            <div className={styles.title} title={document.title}>
              {document.title}
            </div>
            <div className={styles.sub}>
              {formatDocumentDate(document.date)} · {document.pages} S.{' '}
              <TranscriptStatus document={document} />
            </div>
            <div className={styles.sub}>{document.kind}</div>
            {document.tags?.length ? (
              <div className={styles.sub}>{document.tags.join(' · ')}</div>
            ) : null}
          </div>
        </li>
      ))}
    </ul>
  );
}
