import { useEffect, useRef, useState } from 'react';
import type { DocumentSummary } from './document.types';
import type { TagDefinition } from './tags';
import { validateTag } from './tagOperations';
import { TagChips } from './TagChips';
import styles from './TagDialog.module.css';

type DialogState = { kind: 'picker'; ids: readonly string[] } | { kind: 'manage' };
function MixedCheckbox({
  checked,
  mixed,
  onChange,
  label,
  disabled,
}: {
  readonly checked: boolean;
  readonly mixed: boolean;
  readonly onChange: () => void;
  readonly label: string;
  readonly disabled?: boolean;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = mixed;
  }, [mixed]);
  return (
    <input
      ref={ref}
      type="checkbox"
      checked={checked}
      aria-label={label}
      onChange={onChange}
      disabled={disabled}
    />
  );
}
export function TagDialog({
  dialog,
  tags,
  documents,
  visibleIds,
  onAssign,
  onSave,
  onDelete,
  onManage,
  onClose,
  mutationError,
  onReload,
}: {
  readonly dialog: DialogState;
  readonly tags: readonly TagDefinition[];
  readonly documents: readonly DocumentSummary[];
  readonly visibleIds: readonly string[];
  readonly onAssign: (
    ids: readonly string[],
    id: string,
    on: boolean,
  ) => void | Promise<boolean | null>;
  readonly onSave: (
    tag: TagDefinition,
    mode: 'create' | 'update',
    selected?: readonly DocumentSummary[],
  ) => void | Promise<boolean | 'assignment_unconfirmed' | null>;
  readonly onDelete: (id: string, expectedEtag?: string) => void | Promise<boolean | null>;
  readonly onManage: () => void;
  readonly onClose: () => void;
  readonly mutationError?: string;
  readonly onReload?: () => Promise<void>;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [editing, setEditing] = useState<string | null>(null);
  const [assignment, setAssignment] = useState<readonly DocumentSummary[]>();
  const [editVersion, setEditVersion] = useState<string>();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState('#225b79');
  const [error, setError] = useState('');
  const [confirm, setConfirm] = useState<TagDefinition | null>(null);
  const [notice, setNotice] = useState('');
  const pendingRef = useRef(false);
  const [pending, setPending] = useState(false);
  function after(
    action: () => void | Promise<boolean | 'assignment_unconfirmed' | null>,
    success: (result: true | 'assignment_unconfirmed') => void,
  ) {
    if (pendingRef.current) return;
    setNotice('');
    setError('');
    const result = action();
    if (!result) {
      success(true);
      return;
    }
    pendingRef.current = true;
    setPending(true);
    void result
      .then((confirmed) => {
        if (confirmed) success(confirmed);
      })
      .catch(() => setError('Die Aktion konnte nicht bestätigt werden.'))
      .finally(() => {
        pendingRef.current = false;
        setPending(false);
      });
  }
  const done = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('button, input')?.focus();
  }, []);
  useEffect(() => {
    if (pending) ref.current?.focus();
    else if (document.activeElement === ref.current || document.activeElement === document.body)
      (editing ? ref.current?.querySelector<HTMLElement>('form input') : done.current)?.focus();
  }, [pending, editing]);
  const chosen =
    dialog.kind === 'picker'
      ? documents.filter((doc) => dialog.ids.includes(doc.id) && visibleIds.includes(doc.id))
      : [];
  const edit = (tag?: TagDefinition) => {
    setEditing(tag?.id ?? 'new');
    setAssignment(dialog.kind === 'picker' ? chosen : undefined);
    setEditVersion(tag?.etag);
    setConfirm(null);
    setName(tag?.name ?? '');
    setDescription(tag?.description ?? '');
    setColor(tag?.color ?? '#225b79');
    setError('');
  };
  const save = () => {
    if (!editing || pendingRef.current) return;
    const value = { name: name.trim(), description: description.trim(), color };
    const problem = validateTag(value, tags, editing ?? undefined);
    if (problem) {
      setError(problem);
      return;
    }
    const mode = editing === 'new' ? 'create' : 'update';
    const tag = {
      ...value,
      id: mode === 'create' ? `tag-${crypto.randomUUID()}` : editing,
      etag: editVersion,
    };
    after(
      () => (assignment ? onSave(tag, mode, assignment) : onSave(tag, mode)),
      (result) => {
        setEditing(null);
        setError(
          result === 'assignment_unconfirmed'
            ? 'Tag angelegt. Die Zuweisung konnte nicht bestätigt werden.'
            : '',
        );
        if (result === true)
          setNotice(assignment ? 'Tag angelegt und zugewiesen.' : 'Tag gespeichert.');
        done.current?.focus();
      },
    );
  };
  function cancelEdit() {
    setEditing(null);
    setError('');
    ref.current?.focus();
  }
  const form = editing && (
    <form
      className={styles.form}
      onSubmit={(event) => {
        event.preventDefault();
        save();
      }}
    >
      <label>
        Name
        <input
          disabled={pending}
          autoFocus
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      </label>
      <label>
        Beschreibung
        <input
          disabled={pending}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />
      </label>
      <label>
        Farbe
        <input
          type="color"
          disabled={pending}
          value={color}
          onChange={(event) => setColor(event.target.value)}
        />
      </label>
      {error && <p role="alert">{error}</p>}
      <div className={styles.actions}>
        <button disabled={pending} type="button" onClick={cancelEdit}>
          Abbrechen
        </button>{' '}
        <button disabled={pending} type="submit">
          Speichern
        </button>
      </div>
    </form>
  );
  return (
    <div
      className={styles.backdrop}
      onMouseDown={(event) => {
        if (!pendingRef.current && event.target === event.currentTarget) onClose();
      }}
    >
      <div
        ref={ref}
        className={styles.dialog}
        role="dialog"
        aria-modal="true"
        aria-busy={pending}
        tabIndex={-1}
        aria-labelledby="tag-dialog-title"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.stopPropagation();
            if (pendingRef.current) return;
            if (confirm) setConfirm(null);
            else if (editing) cancelEdit();
            else onClose();
          }
          if (event.key === 'Tab') {
            if (pendingRef.current) {
              event.preventDefault();
              return;
            }
            const controls = Array.from(
              event.currentTarget.querySelectorAll<HTMLElement>(
                'input:not(:disabled), button:not(:disabled)',
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
        <h2 id="tag-dialog-title">
          {editing
            ? editing === 'new'
              ? 'Neuer Tag'
              : 'Tag bearbeiten'
            : dialog.kind === 'manage'
              ? 'Tags verwalten'
              : `Tags für ${chosen.length} ${chosen.length === 1 ? 'Dokument' : 'Dokumente'}`}
        </h2>
        <span role="status" aria-live="polite">
          {notice}
        </span>
        {mutationError && (
          <p role="alert">
            {mutationError}{' '}
            {onReload && (
              <button
                disabled={pending}
                onClick={() =>
                  after(
                    async () => {
                      await onReload();
                      return false;
                    },
                    () => {},
                  )
                }
              >
                Neu laden
              </button>
            )}
          </p>
        )}
        {error && !editing && <p role="alert">{error}</p>}
        {dialog.kind === 'picker' && !editing ? (
          <>
            <p>Klick schaltet die Zuordnung für alle sichtbaren ausgewählten Dokumente um.</p>
            <div className={styles.picker}>
              {tags.map((tag) => {
                const count = chosen.filter((doc) => doc.tagIds?.includes(tag.id)).length;
                return (
                  <label key={tag.id}>
                    <MixedCheckbox
                      label={tag.name}
                      disabled={pending || !chosen.length}
                      checked={chosen.length > 0 && count === chosen.length}
                      mixed={count > 0 && count < chosen.length}
                      onChange={() => {
                        after(
                          () =>
                            onAssign(
                              chosen.map((doc) => doc.id),
                              tag.id,
                              count !== chosen.length,
                            ),
                          () => setNotice('Tagzuordnung aktualisiert.'),
                        );
                      }}
                    />
                    <TagChips ids={[tag.id]} tags={tags} />
                  </label>
                );
              })}
            </div>
            {!chosen.length && (
              <p role="status">Keine der ausgewählten Dokumente sind noch sichtbar.</p>
            )}
            <div className={styles.actions}>
              <button
                className={styles.addFirst}
                disabled={pending || !chosen.length}
                onClick={() => edit()}
              >
                + Neuer Tag
              </button>
              <button disabled={pending} onClick={onManage}>
                Tags verwalten …
              </button>
              <button ref={done} disabled={pending} onClick={onClose}>
                Fertig
              </button>
            </div>
          </>
        ) : dialog.kind === 'manage' ? (
          <>
            <p>Tags erscheinen an Dokumenten. Änderungen gelten sofort.</p>
            <button disabled={pending || !!editing} className={styles.add} onClick={() => edit()}>
              Neuer Tag
            </button>
            <div className={styles.rows}>
              {tags.map((tag) => (
                <div className={styles.row} key={tag.id}>
                  <TagChips ids={[tag.id]} tags={tags} />
                  <span>{tag.description}</span>
                  <span>{documents.filter((doc) => doc.tagIds?.includes(tag.id)).length} Dok.</span>
                  <button
                    disabled={pending}
                    onClick={() => edit(tag)}
                    aria-label={`${tag.name} bearbeiten`}
                  >
                    Bearbeiten
                  </button>
                  <button
                    disabled={pending}
                    onClick={() => {
                      setEditing(null);
                      setConfirm(tag);
                    }}
                    aria-label={`${tag.name} löschen`}
                  >
                    Löschen
                  </button>
                </div>
              ))}
            </div>
            {confirm && (
              <div className={styles.form} role="alert">
                <p>
                  „{confirm.name}“ löschen? Der Tag wird aus{' '}
                  {documents.filter((doc) => doc.tagIds?.includes(confirm.id)).length} Dokumenten
                  und aktiven Filtern entfernt.
                </p>
                <div>
                  <button disabled={pending} onClick={() => setConfirm(null)}>
                    Abbrechen
                  </button>{' '}
                  <button
                    disabled={pending}
                    onClick={() => {
                      after(
                        () => onDelete(confirm.id, confirm.etag),
                        () => {
                          setConfirm(null);
                          setNotice('Tag gelöscht.');
                          done.current?.focus();
                        },
                      );
                    }}
                  >
                    Endgültig löschen
                  </button>
                </div>
              </div>
            )}
            {form}
            <div className={styles.actions}>
              <button ref={done} disabled={pending} onClick={onClose}>
                Fertig
              </button>
            </div>
          </>
        ) : null}
        {dialog.kind === 'picker' && form}
      </div>
    </div>
  );
}
export type TagDialogState = DialogState;
