import { Highlight } from '../search/Highlight';
import { topicPath } from '../topics/topicUtils';
import { demoTags } from './tags';
import type { TagDefinition } from './tags';
import { TagChips } from './TagChips';
import { documentSelectionProps } from './documentInteractions';
import type { DocumentWorkspace } from './useDocumentWorkspace';
import type { DocumentDrag } from './useDocumentDrag';
import type { Topic, TopicId } from '../topics/topic.types';
import type { DocumentSummary } from './document.types';
import { Icon } from '../../components/Icon';
import { DemoPreview, DocumentActions, TranscriptStatus } from './DocumentSummary';
import { hasRedundantPdfFilename } from './documentDisplay';
import { formatDocumentDate } from './documentDate';
import styles from './DocumentList.module.css';
export interface DocumentItemsProps {
  readonly tags?: readonly TagDefinition[];
  readonly onTags?: (id: string) => void;
  readonly onOpen?: (id: string, forceTab: boolean) => void;
  readonly workspace?: DocumentWorkspace;
  readonly documentDrag?: DocumentDrag;
  readonly documents: readonly DocumentSummary[];
  readonly topics?: readonly Topic[];
  readonly folders: readonly Topic[];
  readonly folderCounts: ReadonlyMap<TopicId, number>;
  readonly onNavigate: (id: TopicId) => void;
}
export function DocumentList({
  documents,
  tags = demoTags,
  onTags,
  onOpen,
  topics = [],
  folders,
  folderCounts,
  onNavigate,
  workspace,
  documentDrag,
}: DocumentItemsProps) {
  return (
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">
              <input
                type="checkbox"
                aria-label="Alle auswählen"
                checked={!!documents.length && workspace?.selected.length === documents.length}
                disabled={!workspace || !documents.length}
                onChange={(event) => workspace?.all(event.target.checked)}
              />
            </th>
            <th scope="col" aria-label="Vorschau" />
            <th scope="col" aria-sort={workspace?.sort === 'title' ? 'ascending' : undefined}>
              <button
                className={styles.sort}
                disabled={!workspace}
                onClick={() => workspace?.setSort('title')}
              >
                Titel
              </button>
            </th>
            <th
              scope="col"
              aria-sort={
                workspace?.sort === 'date-asc'
                  ? 'ascending'
                  : workspace?.sort === 'date-desc'
                    ? 'descending'
                    : undefined
              }
            >
              <button
                className={styles.sort}
                disabled={!workspace}
                onClick={() =>
                  workspace?.setSort(workspace.sort === 'date-desc' ? 'date-asc' : 'date-desc')
                }
              >
                Datum
              </button>
            </th>
            <th scope="col">Seiten</th>
            <th scope="col">Tags</th>
            <th scope="col">Transkript</th>
            {workspace?.searchActive && <th scope="col">Thema</th>}
            <th scope="col">Aktionen</th>
          </tr>
        </thead>
        <tbody>
          {folders.map((folder) => (
            <tr
              key={folder.id}
              className={documentDrag?.target === folder.id ? styles.drop : undefined}
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
            <tr
              key={document.id}
              className={workspace?.selected.includes(document.id) ? styles.selected : undefined}
              {...(workspace && documentDrag
                ? documentSelectionProps(document.id, workspace, documentDrag, onOpen)
                : {})}
            >
              <td>
                <input
                  type="checkbox"
                  disabled={!workspace}
                  checked={workspace?.selected.includes(document.id) ?? false}
                  onChange={() => workspace?.select(document.id, {}, true)}
                  aria-label={`${document.title} auswählen`}
                />
              </td>
              <td>
                <div className={styles.mini}>
                  <DemoPreview document={document} />
                </div>
              </td>
              <td>
                <strong className={styles.title}>
                  <Highlight
                    text={document.title}
                    terms={workspace?.results.get(document.id)?.terms ?? []}
                  />
                </strong>
                <span className={styles.kind} title={document.fileName ?? undefined}>
                  {(!hasRedundantPdfFilename(document) || workspace?.searchActive) && (
                    <>
                      <Highlight
                        text={document.fileName ?? '—'}
                        terms={workspace?.results.get(document.id)?.terms ?? []}
                      />{' '}
                      ·{' '}
                    </>
                  )}
                  {document.kind}
                </span>
              </td>
              <td className={styles.secondary}>{formatDocumentDate(document.date)}</td>
              <td className={styles.secondary}>{document.pages ?? 'Unbekannt'}</td>
              <td>
                <TagChips ids={document.tagIds} tags={tags} />
              </td>
              <td>
                <TranscriptStatus document={document} />
                {workspace?.results.get(document.id)?.hit && (
                  <span> Treffer S. {workspace.results.get(document.id)?.hit?.page}</span>
                )}
              </td>
              {workspace?.searchActive && (
                <td>
                  {topicPath(topics, document.topicId)
                    .map((topic) => topic.name)
                    .join(' / ')}
                </td>
              )}
              <td>
                <DocumentActions
                  onTags={onTags ? () => onTags(document.id) : undefined}
                  onOpenTab={onOpen ? () => onOpen(document.id, true) : undefined}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
