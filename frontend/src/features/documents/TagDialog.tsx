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
}: {
  readonly checked: boolean;
  readonly mixed: boolean;
  readonly onChange: () => void;
  readonly label: string;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = mixed;
  }, [mixed]);
  return (
    <input ref={ref} type="checkbox" checked={checked} aria-label={label} onChange={onChange} />
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
}: {
  readonly dialog: DialogState;
  readonly tags: readonly TagDefinition[];
  readonly documents: readonly DocumentSummary[];
  readonly visibleIds: readonly string[];
  readonly onAssign: (ids: readonly string[], id: string, on: boolean) => void;
  readonly onSave: (tag: TagDefinition) => void;
  readonly onDelete: (id: string) => void;
  readonly onManage: () => void;
  readonly onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [editing, setEditing] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState('#225b79');
  const [error, setError] = useState('');
  const [confirm, setConfirm] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const done = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('button, input')?.focus();
  }, []);
  const chosen =
    dialog.kind === 'picker'
      ? documents.filter((doc) => dialog.ids.includes(doc.id) && visibleIds.includes(doc.id))
      : [];
  const edit = (tag?: TagDefinition) => {
    setEditing(tag?.id ?? 'new');
    setName(tag?.name ?? '');
    setDescription(tag?.description ?? '');
    setColor(tag?.color ?? '#225b79');
    setError('');
  };
  const save = () => {
    const value = { name: name.trim(), description: description.trim(), color };
    const problem = validateTag(value, tags, editing ?? undefined);
    if (problem) {
      setError(problem);
      return;
    }
    onSave({ ...value, id: editing === 'new' ? `tag-${crypto.randomUUID()}` : editing! });
    setEditing(null);
    setError('');
    setNotice('Tag gespeichert.');
    done.current?.focus();
  };
  return (
    <div
      className={styles.backdrop}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        ref={ref}
        className={styles.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="tag-dialog-title"
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.stopPropagation();
            if (confirm) setConfirm(null);
            else if (editing) setEditing(null);
            else onClose();
          }
          if (event.key === 'Tab') {
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
          {dialog.kind === 'manage'
            ? 'Tags verwalten'
            : `Tags für ${chosen.length} ${chosen.length === 1 ? 'Dokument' : 'Dokumente'}`}
        </h2>
        <span role="status" aria-live="polite">
          {notice}
        </span>
        {dialog.kind === 'picker' ? (
          <>
            <p>Klick schaltet die Zuordnung für alle sichtbaren ausgewählten Dokumente um.</p>
            <div className={styles.picker}>
              {tags.map((tag) => {
                const count = chosen.filter((doc) => doc.tagIds?.includes(tag.id)).length;
                return (
                  <label key={tag.id}>
                    <MixedCheckbox
                      label={tag.name}
                      checked={chosen.length > 0 && count === chosen.length}
                      mixed={count > 0 && count < chosen.length}
                      onChange={() => {
                        setNotice('Tagzuordnung aktualisiert.');
                        onAssign(
                          chosen.map((doc) => doc.id),
                          tag.id,
                          count !== chosen.length,
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
              <button onClick={onManage}>Tags verwalten …</button>
              <button ref={done} onClick={onClose}>
                Fertig
              </button>
            </div>
          </>
        ) : (
          <>
            <p>Tags erscheinen an Dokumenten. Änderungen gelten sofort.</p>
            <div className={styles.rows}>
              {tags.map((tag) => (
                <div className={styles.row} key={tag.id}>
                  <TagChips ids={[tag.id]} tags={tags} />
                  <span>{tag.description}</span>
                  <span>{documents.filter((doc) => doc.tagIds?.includes(tag.id)).length} Dok.</span>
                  <button onClick={() => edit(tag)} aria-label={`${tag.name} bearbeiten`}>
                    Bearbeiten
                  </button>
                  <button onClick={() => setConfirm(tag.id)} aria-label={`${tag.name} löschen`}>
                    Löschen
                  </button>
                </div>
              ))}
            </div>
            {confirm && (
              <div className={styles.form} role="alert">
                <p>
                  „{tags.find((tag) => tag.id === confirm)?.name}“ löschen? Der Tag wird aus{' '}
                  {documents.filter((doc) => doc.tagIds?.includes(confirm)).length} Dokumenten und
                  aktiven Filtern entfernt.
                </p>
                <div>
                  <button onClick={() => setConfirm(null)}>Abbrechen</button>{' '}
                  <button
                    onClick={() => {
                      onDelete(confirm);
                      setConfirm(null);
                      setNotice('Tag gelöscht.');
                      done.current?.focus();
                    }}
                  >
                    Endgültig löschen
                  </button>
                </div>
              </div>
            )}
            {editing ? (
              <form
                className={styles.form}
                onSubmit={(event) => {
                  event.preventDefault();
                  save();
                }}
              >
                <label>
                  Name
                  <input autoFocus value={name} onChange={(event) => setName(event.target.value)} />
                </label>
                <label>
                  Beschreibung
                  <input
                    value={description}
                    onChange={(event) => setDescription(event.target.value)}
                  />
                </label>
                <label>
                  Farbe
                  <input
                    type="color"
                    value={color}
                    onChange={(event) => setColor(event.target.value)}
                  />
                </label>
                {error && <p role="alert">{error}</p>}
                <div>
                  <button type="button" onClick={() => setEditing(null)}>
                    Abbrechen
                  </button>{' '}
                  <button type="submit">Speichern</button>
                </div>
              </form>
            ) : (
              <button className={styles.add} onClick={() => edit()}>
                Neuer Tag
              </button>
            )}
            <div className={styles.actions}>
              <button onClick={onClose}>Fertig</button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
export type TagDialogState = DialogState;
