import { demoTags, tagNames } from './tags';
import { documentSelectionProps } from './documentInteractions';
import { Icon } from '../../components/Icon';
import type { DocumentItemsProps } from './DocumentList';
import { DemoPreview, DocumentActions, TranscriptStatus } from './DocumentSummary';
import { formatDocumentDate } from './documentDate';
import styles from './DocumentGrid.module.css';
export function DocumentGrid({
  documents,
  folders,
  folderCounts,
  onNavigate,
  workspace,
  documentDrag,
}: DocumentItemsProps) {
  return (
    <ul
      className={styles.grid}
      style={{ '--tile': `${workspace?.zoom ?? 190}px` } as React.CSSProperties}
      aria-label="Kachelansicht"
    >
      {folders.map((folder) => (
        <li
          key={folder.id}
          className={`${styles.tile} ${documentDrag?.target === folder.id ? styles.drop : ''}`}
          onDragOver={(event) => {
            if (documentDrag?.over(folder.id)) {
              event.preventDefault();
              event.dataTransfer.dropEffect = 'move';
            }
          }}
          onDragLeave={(event) => documentDrag?.leave(folder.id, event)}
          onDrop={(event) => {
            event.preventDefault();
            event.stopPropagation();
            documentDrag?.drop(folder.id, event.dataTransfer);
          }}
        >
          <button className={styles.folder} onClick={() => onNavigate(folder.id)}>
            <span className={`${styles.thumb} ${styles.folderThumb}`}>
              <Icon name="folder" />
            </span>
            <span className={styles.meta}>
              <span className={styles.title}>{folder.name}</span>
              <span className={styles.sub}>
                {folderCounts.get(folder.id) ?? 0}{' '}
                {(folderCounts.get(folder.id) ?? 0) === 1 ? 'Dokument' : 'Dokumente'}
              </span>
            </span>
          </button>
        </li>
      ))}
      {documents.map((document) => (
        <li
          key={document.id}
          className={`${styles.tile} ${workspace?.selected.includes(document.id) ? styles.selected : ''}`}
          {...(workspace && documentDrag
            ? documentSelectionProps(document.id, workspace, documentDrag)
            : {})}
        >
          <label className={styles.check}>
            <input
              type="checkbox"
              aria-label={`${document.title} auswählen`}
              checked={workspace?.selected.includes(document.id) ?? false}
              disabled={!workspace}
              onChange={() => workspace?.select(document.id, {}, true)}
            />
          </label>
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
            {tagNames(document.tagIds, demoTags).length ? (
              <div className={styles.sub}>{tagNames(document.tagIds, demoTags).join(' · ')}</div>
            ) : null}
          </div>
        </li>
      ))}
    </ul>
  );
}
