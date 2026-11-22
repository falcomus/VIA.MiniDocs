import { useEffect, useRef, useState } from 'react';
import type { Topic, TopicId } from './topic.types';
import { descendantIds } from './topicUtils';
import type { TopicDrag } from './useTopicDrag';
import { Icon } from '../../components/Icon';
import styles from './TopicTree.module.css';

interface TopicTreeProps {
  readonly topics: readonly Topic[];
  readonly selectedTopicId: TopicId;
  readonly onSelect: (id: TopicId) => void;
  readonly documentTopicIds?: readonly TopicId[];
  readonly onNew?: (id: TopicId) => void;
  readonly onRename?: (id: TopicId) => void;
  readonly onDelete?: (id: TopicId) => void;
  readonly onMoveDialog?: (id: TopicId) => void;
  readonly drag?: TopicDrag;
}

export function TopicTree({
  topics,
  selectedTopicId,
  onSelect,
  documentTopicIds = [],
  onNew,
  onRename,
  onDelete,
  onMoveDialog,
  drag,
}: TopicTreeProps) {
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<TopicId>>(
    () => new Set(['customers', 'alpha', 'own-notes']),
  );
  const [menu, setMenu] = useState<{ id: TopicId; x: number; y: number } | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (!menu) return;
    menuRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
    const closeOnOutside = (event: MouseEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenu(null);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Tab') {
        setMenu(null);
        return;
      }
      if (event.key === 'Escape') {
        event.preventDefault();
        setMenu(null);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener('mousedown', closeOnOutside);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('mousedown', closeOnOutside);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [menu]);

  function toggle(id: TopicId) {
    setExpandedIds((previous) => {
      const next = new Set(previous);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }
  function openMenu(event: React.MouseEvent<HTMLButtonElement>, id: TopicId) {
    event.preventDefault();
    triggerRef.current = event.currentTarget;
    const rect = event.currentTarget.getBoundingClientRect();
    setMenu({
      id,
      x: Math.max(0, Math.min(event.clientX || rect.left, window.innerWidth - 220)),
      y: Math.max(0, Math.min(event.clientY || rect.bottom, window.innerHeight - 190)),
    });
  }
  function children(parentId: TopicId | null): Topic[] {
    return topics
      .filter((topic) => topic.parentId === parentId)
      .slice()
      .sort(
        (a, b) =>
          Number(!!b.systemKey) - Number(!!a.systemKey) || a.name.localeCompare(b.name, 'de'),
      );
  }
  function branch(topic: Topic): React.ReactNode {
    const kids = children(topic.id);
    const expanded = expandedIds.has(topic.id);
    const countIds = descendantIds(topics, topic.id);
    const count = documentTopicIds.filter((id) => countIds.has(id)).length;
    return (
      <li className={styles.item} key={topic.id}>
        <div
          className={`${styles.itemRow} ${selectedTopicId === topic.id ? styles.selected : ''} ${drag?.dropTarget === topic.id ? styles.drop : ''}`}
          draggable={!!drag && !topic.systemKey}
          onDragStart={(event) => {
            event.stopPropagation();
            if (!drag?.start(topic.id, event.dataTransfer)) event.preventDefault();
          }}
          onDragOver={(event) => {
            if (drag?.over(topic.id)) {
              event.preventDefault();
              event.dataTransfer.dropEffect = 'move';
            }
          }}
          onDragLeave={(event) => {
            if (
              !(event.relatedTarget instanceof Node) ||
              !event.currentTarget.contains(event.relatedTarget)
            ) {
              drag?.leave(topic.id);
            }
          }}
          onDrop={(event) => {
            if (!drag) return;
            event.preventDefault();
            event.stopPropagation();
            if (drag.drop(topic.id, event.dataTransfer))
              setExpandedIds((previous) => new Set([...previous, topic.id]));
          }}
        >
          {kids.length > 0 ? (
            <button
              type="button"
              className={styles.disclosure}
              onClick={() => toggle(topic.id)}
              aria-expanded={expanded}
              aria-label={`${topic.name}: Unterthemen ${expanded ? 'einklappen' : 'aufklappen'}`}
            >
              <Icon name="chevron" className={expanded ? styles.expanded : ''} />
            </button>
          ) : (
            <span className={styles.disclosureSpacer} />
          )}
          <button
            type="button"
            className={styles.topicButton}
            data-topic-id={topic.id}
            aria-description={`${count} Dokumente einschließlich Unterthemen`}
            title={`${topic.name} · ${count} Dokumente`}
            aria-current={selectedTopicId === topic.id ? 'page' : undefined}
            onClick={() => onSelect(topic.id)}
            onDoubleClick={() => {
              if (kids.length) toggle(topic.id);
            }}
            onContextMenu={(event) => openMenu(event, topic.id)}
            onKeyDown={(event) => {
              if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
                event.preventDefault();
                const rect = event.currentTarget.getBoundingClientRect();
                triggerRef.current = event.currentTarget;
                setMenu({ id: topic.id, x: rect.left, y: rect.bottom });
              }
            }}
          >
            <Icon
              name={topic.systemKey ? 'inbox' : 'folder'}
              className={topic.systemKey ? styles.system : styles.folder}
            />
            <span className={styles.topicLabel}>{topic.name}</span>
            {count > 0 && (
              <span
                className={`${styles.count} ${topic.systemKey ? styles.hot : ''}`}
                aria-hidden="true"
              >
                {count}
              </span>
            )}
          </button>
        </div>
        {topic.systemKey && <div className={styles.separator} />}
        {kids.length > 0 && expanded && <ul className={styles.children}>{kids.map(branch)}</ul>}
      </li>
    );
  }
  const menuTopic = topics.find((topic) => topic.id === menu?.id);
  function action(callback?: (id: TopicId) => void) {
    if (menu && callback) {
      const id = menu.id;
      setMenu(null);
      triggerRef.current?.focus();
      callback(id);
    }
  }
  return (
    <nav aria-label="Themen">
      {children(null).length ? (
        <ul className={styles.roots}>{children(null).map(branch)}</ul>
      ) : (
        <p className={styles.empty}>Noch keine Themen vorhanden.</p>
      )}
      {menu && menuTopic && (
        <div
          ref={menuRef}
          className={styles.menu}
          role="menu"
          onKeyDown={(event) => {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
            event.preventDefault();
            const items = Array.from(
              event.currentTarget.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'),
            );
            const index = items.indexOf(document.activeElement as HTMLButtonElement);
            const next =
              event.key === 'ArrowDown'
                ? (index + 1) % items.length
                : (index - 1 + items.length) % items.length;
            items[next]?.focus();
          }}
          aria-label={`Kontextmenü ${menuTopic.name}`}
          style={{ left: menu.x, top: menu.y }}
        >
          {!menuTopic.systemKey && (
            <>
              {onNew && (
                <button
                  role="menuitem"
                  onClick={() => {
                    setExpandedIds((previous) => new Set([...previous, menuTopic.id]));
                    action(onNew);
                  }}
                >
                  <Icon name="folderPlus" /> Neues Unterthema
                </button>
              )}
              {onRename && (
                <button role="menuitem" onClick={() => action(onRename)}>
                  <Icon name="edit" /> Umbenennen
                </button>
              )}
            </>
          )}
          <button role="menuitem" disabled>
            <Icon name="download" /> Alle Dokumente als ZIP
          </button>
          {((!onNew && !onRename && !onDelete && !onMoveDialog) || menuTopic.systemKey) && (
            <button
              role="menuitem"
              onClick={() => {
                setMenu(null);
                triggerRef.current?.focus();
              }}
            >
              Menü schließen
            </button>
          )}
          {!menuTopic.systemKey && (
            <>
              {onMoveDialog && (
                <button role="menuitem" onClick={() => action(onMoveDialog)}>
                  <Icon name="folder" /> Verschieben…
                </button>
              )}
              {onDelete && (
                <>
                  <div className={styles.menuSeparator} />
                  <button
                    role="menuitem"
                    className={styles.danger}
                    onClick={() => action(onDelete)}
                  >
                    <Icon name="trash" /> Löschen
                  </button>
                </>
              )}
            </>
          )}
        </div>
      )}
    </nav>
  );
}
