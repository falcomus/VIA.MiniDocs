import type { Topic, TopicId } from '../topics/topic.types';
import type { DocumentSummary } from './document.types';
import { Icon } from '../../components/Icon';
import { DemoPreview, DocumentActions, TranscriptStatus } from './DocumentSummary';
import { formatDocumentDate } from './documentDate';
import styles from './DocumentList.module.css';
export interface DocumentItemsProps {
  readonly documents: readonly DocumentSummary[];
  readonly folders: readonly Topic[];
  readonly folderCounts: ReadonlyMap<TopicId, number>;
  readonly onNavigate: (id: TopicId) => void;
}
export function DocumentList({ documents, folders, folderCounts, onNavigate }: DocumentItemsProps) {
  return (
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">
              <input type="checkbox" aria-label="Alle auswählen" disabled />
            </th>
            <th scope="col" aria-label="Vorschau" />
            <th scope="col">Titel</th>
            <th scope="col">Datum</th>
            <th scope="col">Seiten</th>
            <th scope="col">Tags</th>
            <th scope="col">Transkript</th>
            <th scope="col">Aktionen</th>
          </tr>
        </thead>
        <tbody>
          {folders.map((folder) => (
            <tr key={folder.id}>
              <td />
              <td>
                <Icon name="folder" />
              </td>
              <td>
                <button className={styles.folder} onClick={() => onNavigate(folder.id)}>
                  {folder.name}
                </button>
              </td>
              <td />
              <td>{folderCounts.get(folder.id) ?? 0} Dok.</td>
              <td colSpan={3} />
            </tr>
          ))}
          {documents.map((document) => (
            <tr key={document.id}>
              <td>
                <input type="checkbox" disabled aria-label={`${document.title} auswählen`} />
              </td>
              <td>
                <div className={styles.mini}>
                  <DemoPreview document={document} />
                </div>
              </td>
              <td>
                <strong className={styles.title}>{document.title}</strong>
                <span className={styles.kind}>
                  {document.fileName ?? '—'} · {document.kind}
                </span>
              </td>
              <td className={styles.secondary}>{formatDocumentDate(document.date)}</td>
              <td className={styles.secondary}>{document.pages}</td>
              <td>{document.tags?.join(' · ') || '—'}</td>
              <td>
                <TranscriptStatus document={document} />
              </td>
              <td>
                <DocumentActions />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
