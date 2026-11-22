import type { DocumentWorkspace } from './useDocumentWorkspace';
import type { DocumentDrag } from './useDocumentDrag';
import type { Topic, TopicId } from '../topics/topic.types';
import { descendantIds } from '../topics/topicUtils';
import type { DocumentView } from './document.types';
import { DocumentList } from './DocumentList';
import { DocumentGrid } from './DocumentGrid';
import styles from './DocumentOverview.module.css';
/** Both views derive their direct children from the same source; recursive counts include descendants. */
export function DocumentOverview({
  workspace,
  documentDrag,
  topics,
  selectedTopicId,
  view,
  onNavigate,
}: {
  readonly workspace: DocumentWorkspace;
  readonly documentDrag: DocumentDrag;
  readonly topics: readonly Topic[];
  readonly selectedTopicId: TopicId;
  readonly view: DocumentView;
  readonly onNavigate: (id: TopicId) => void;
}) {
  const folders = topics.filter((topic) => topic.parentId === selectedTopicId);
  const visible = workspace.visible;
  const counts = new Map(
    folders.map((folder) => {
      const ids = descendantIds(topics, folder.id);
      return [
        folder.id,
        workspace.documents.filter((document) => ids.has(document.topicId)).length,
      ] as const;
    }),
  );
  if (!folders.length && !visible.length)
    return (
      <div className={styles.empty} role="status">
        <strong>Keine Dokumente in diesem Thema</strong>
        <p>
          Dieses Thema ist leer. Upload folgt später; Dokumente können über den Verschiebedialog
          zugeordnet werden.
        </p>
      </div>
    );
  const View = view === 'list' ? DocumentList : DocumentGrid;
  return (
    <View
      workspace={workspace}
      documentDrag={documentDrag}
      documents={visible}
      folders={folders}
      folderCounts={counts}
      onNavigate={onNavigate}
    />
  );
}
