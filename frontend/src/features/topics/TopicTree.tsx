import { useState } from 'react';
import type { Topic, TopicId } from './topic.types';
import { Icon } from '../../components/Icon';
import styles from './TopicTree.module.css';

interface TopicTreeProps {
  readonly topics: readonly Topic[];
  readonly selectedTopicId: TopicId;
  readonly onSelect: (id: TopicId) => void;
}

interface TopicBranchProps extends TopicTreeProps {
  readonly topic: Topic;
  readonly expandedIds: ReadonlySet<TopicId>;
  readonly onToggle: (id: TopicId) => void;
}

function TopicBranch({
  topic,
  topics,
  selectedTopicId,
  onSelect,
  expandedIds,
  onToggle,
}: TopicBranchProps) {
  const children = topics.filter((candidate) => candidate.parentId === topic.id);
  const hasChildren = children.length > 0;
  const expanded = expandedIds.has(topic.id);
  const selected = topic.id === selectedTopicId;

  return (
    <li className={styles.item}>
      <div className={`${styles.itemRow} ${selected ? styles.selected : ''}`}>
        {hasChildren ? (
          <button
            type="button"
            className={styles.disclosure}
            onClick={() => onToggle(topic.id)}
            aria-expanded={expanded}
            aria-label={`${topic.name}: Unterthemen ${expanded ? 'einklappen' : 'aufklappen'}`}
          >
            <Icon name="chevron" className={expanded ? styles.expanded : ''} />
          </button>
        ) : (
          <span className={styles.disclosureSpacer} aria-hidden="true" />
        )}
        <button
          type="button"
          className={styles.topicButton}
          onClick={() => onSelect(topic.id)}
          aria-current={selected ? 'page' : undefined}
        >
          <Icon name={topic.systemKey ? 'inbox' : 'folder'} className={topic.systemKey ? styles.system : styles.folder} />
          <span className={styles.topicLabel}>{topic.name}</span>
        </button>
      </div>
      {hasChildren && expanded && (
        <ul className={styles.children}>
          {children.map((child) => (
            <TopicBranch
              key={child.id}
              topic={child}
              topics={topics}
              selectedTopicId={selectedTopicId}
              onSelect={onSelect}
              expandedIds={expandedIds}
              onToggle={onToggle}
            />
          ))}
        </ul>
      )}
    </li>
  );
}

export function TopicTree({ topics, selectedTopicId, onSelect }: TopicTreeProps) {
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<TopicId>>(
    () => new Set(['customers', 'alpha', 'own-notes']),
  );

  function toggle(id: TopicId) {
    setExpandedIds((previous) => {
      const next = new Set(previous);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  const roots = topics.filter((topic) => topic.parentId === null);

  return (
    <nav aria-label="Themen">
      {roots.length === 0 ? (
        <p className={styles.empty}>Noch keine Themen vorhanden.</p>
      ) : (
        <ul className={styles.roots}>
          {roots.map((root) => (
            <TopicBranch
              key={root.id}
              topic={root}
              topics={topics}
              selectedTopicId={selectedTopicId}
              onSelect={onSelect}
              expandedIds={expandedIds}
              onToggle={toggle}
            />
          ))}
        </ul>
      )}
    </nav>
  );
}
