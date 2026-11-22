import { Icon } from '../../components/Icon';
import styles from './SelectionBar.module.css';
export function SelectionBar({
  count,
  hasDocuments,
  onAll,
  onClear,
  onMove,
}: {
  readonly count: number;
  readonly hasDocuments: boolean;
  readonly onAll: () => void;
  readonly onClear: () => void;
  readonly onMove: () => void;
}) {
  return (
    <div
      className={`${styles.bar} ${count ? styles.active : styles.hint}`}
      aria-label="Dokumentauswahl"
    >
      {count ? (
        <>
          <b aria-live="polite">
            {count} {count === 1 ? 'Dokument' : 'Dokumente'} ausgewählt
          </b>
          <span className={styles.spacer} />
          {count === 1 && (
            <button disabled>
              <Icon name="open" /> Öffnen
            </button>
          )}
          <button disabled>
            <Icon name="tag" /> Tags zuweisen
          </button>
          <button onClick={onMove}>
            <Icon name="move" /> Verschieben nach …
          </button>
          <button disabled>
            <Icon name="download" /> {count === 1 ? 'Herunterladen' : 'Als ZIP herunterladen'}
          </button>
          <button onClick={onClear}>
            <Icon name="x" />
            Auswahl aufheben
          </button>
        </>
      ) : (
        <>
          Klick wählt aus · Strg/⇧-Klick für Mehrfachauswahl · Auswahl auf ein Thema links ziehen
          ordnet zu
          <span className={styles.spacer} />
          {hasDocuments && <button onClick={onAll}>Alle auswählen</button>}
        </>
      )}
    </div>
  );
}
