import { useEffect, useRef, useState } from 'react';
import type { Topic, TopicId } from './topic.types';
import { canMoveTopic, topicPath } from './topicUtils';
import styles from './TopicDialog.module.css';

export type TopicDialogState =
  | { kind: 'documents'; ids: readonly string[] }
  | { kind: 'new'; parentId: TopicId | null }
  | { kind: 'rename' | 'delete' | 'move'; id: TopicId };

interface TopicDialogProps {
  readonly dialog: TopicDialogState;
  readonly topics: readonly Topic[];
  readonly deleteReason: string | null;
  readonly error: string;
  readonly busy?: boolean;
  readonly onCancel: () => void;
  readonly onSubmit: (value: string) => void;
}

export function TopicDialog({
  dialog,
  topics,
  deleteReason,
  error,
  busy = false,
  onCancel,
  onSubmit,
}: TopicDialogProps) {
  const topic = 'id' in dialog ? topics.find((item) => item.id === dialog.id) : undefined;
  const [value, setValue] = useState(
    dialog.kind === 'documents'
      ? (topics[0]?.id ?? '')
      : dialog.kind === 'rename'
        ? (topic?.name ?? '')
        : '',
  );
  const dialogRef = useRef<HTMLDivElement>(null);
  const validTargets =
    dialog.kind === 'documents'
      ? topics
      : dialog.kind === 'move'
        ? topics.filter((item) => canMoveTopic(topics, dialog.id, item.id))
        : [];

  useEffect(() => {
    dialogRef.current?.querySelector<HTMLElement>('input, select, button:not(:disabled)')?.focus();
  }, []);

  useEffect(() => {
    if (busy) dialogRef.current?.focus();
    else if (document.activeElement === dialogRef.current)
      dialogRef.current
        ?.querySelector<HTMLElement>('input, select, button:not(:disabled)')
        ?.focus();
  }, [busy]);

  const title =
    dialog.kind === 'documents'
      ? `${dialog.ids.length} ${dialog.ids.length === 1 ? 'Dokument' : 'Dokumente'} verschieben nach …`
      : dialog.kind === 'new'
        ? dialog.parentId
          ? `Neues Unterthema in „${topics.find((item) => item.id === dialog.parentId)?.name}“`
          : 'Neues Thema'
        : dialog.kind === 'rename'
          ? 'Thema umbenennen'
          : dialog.kind === 'delete'
            ? 'Thema löschen'
            : `„${topic?.name}“ verschieben`;
  return (
    <div
      className={styles.backdrop}
      onMouseDown={(event) => {
        if (!busy && event.target === event.currentTarget) onCancel();
      }}
    >
      <div
        className={styles.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="topic-dialog-title"
        ref={dialogRef}
        tabIndex={-1}
        aria-busy={busy}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.stopPropagation();
            if (!busy) onCancel();
          }
          if (event.key === 'Tab') {
            if (busy) {
              event.preventDefault();
              return;
            }
            const controls = Array.from(
              event.currentTarget.querySelectorAll<HTMLElement>(
                'input:not(:disabled), select:not(:disabled), button:not(:disabled)',
              ),
            );
            if (event.shiftKey && document.activeElement === controls[0]) {
              event.preventDefault();
              controls.at(-1)?.focus();
            } else if (!event.shiftKey && document.activeElement === controls.at(-1)) {
              event.preventDefault();
              controls[0]?.focus();
            }
          }
        }}
      >
        <h2 id="topic-dialog-title">{title}</h2>
        {dialog.kind === 'delete' ? (
          <p>{deleteReason ?? `„${topic?.name}“ löschen?`}</p>
        ) : (
          <form
            id="topic-form"
            onSubmit={(event) => {
              event.preventDefault();
              if (!busy) onSubmit(value);
            }}
          >
            {dialog.kind === 'move' || dialog.kind === 'documents' ? (
              <>
                <label htmlFor="topic-target">Zielthema</label>
                <select
                  id="topic-target"
                  disabled={busy}
                  value={value}
                  onChange={(event) => setValue(event.target.value)}
                >
                  {dialog.kind === 'move' && <option value="">Oberste Ebene</option>}
                  {validTargets.map((item) => (
                    <option key={item.id} value={item.id}>
                      {topicPath(topics, item.id)
                        .map((part) => part.name)
                        .join(' / ')}
                    </option>
                  ))}
                </select>
              </>
            ) : (
              <>
                <label htmlFor="topic-name">Name</label>
                <input
                  id="topic-name"
                  disabled={busy}
                  value={value}
                  onChange={(event) => setValue(event.target.value)}
                />
              </>
            )}
          </form>
        )}
        {error && <p role="alert">{error}</p>}
        <div className={styles.actions}>
          <button type="button" onClick={onCancel} disabled={busy}>
            Abbrechen
          </button>
          <button
            type={dialog.kind === 'delete' ? 'button' : 'submit'}
            form={dialog.kind === 'delete' ? undefined : 'topic-form'}
            onClick={dialog.kind === 'delete' ? () => onSubmit(value) : undefined}
            disabled={
              busy ||
              (dialog.kind === 'delete' && !!deleteReason) ||
              ((dialog.kind === 'new' || dialog.kind === 'rename') && !value.trim())
            }
          >
            {dialog.kind === 'delete'
              ? 'Löschen'
              : dialog.kind === 'move' || dialog.kind === 'documents'
                ? 'Verschieben'
                : 'OK'}
          </button>
        </div>
      </div>
    </div>
  );
}
