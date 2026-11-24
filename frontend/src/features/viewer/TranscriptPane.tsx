import { useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { DocumentSummary } from '../documents/document.types';
import { findTranscriptMatches, countReplaceMatches } from '../documents/transcriptOperations';
import type { TranscriptMatch, TranscriptOperation } from '../documents/transcriptOperations';
import { Icon } from '../../components/Icon';
import styles from './TranscriptPane.module.css';
export function TranscriptPane({
  document,
  page,
  onPageChange,
  onEdit,
  editorRef,
  followPage = true,
}: {
  readonly document: DocumentSummary;
  readonly page: number;
  readonly onPageChange: (page: number) => void;
  readonly onEdit: (operation: TranscriptOperation) => void;
  readonly editorRef?: React.RefObject<HTMLTextAreaElement | null>;
  readonly followPage?: boolean;
}) {
  const [query, setQuery] = useState('');
  const [replacement, setReplacement] = useState('');
  const [cursor, setCursor] = useState(-1);
  const [selection, setSelection] = useState<TranscriptMatch | null>(null);
  const [notice, setNotice] = useState('');
  const fallbackInput = useRef<HTMLTextAreaElement>(null);
  const input = editorRef ?? fallbackInput;
  const matches = useMemo(() => findTranscriptMatches(document, query), [document, query]);
  const current = cursor >= 0 && cursor < matches.length ? cursor : -1;
  const texts = useMemo(
    () => new Map(document.transcript?.pages.map((part) => [part.page, part.text])),
    [document.transcript],
  );
  const activeCard = useRef<HTMLElement>(null);
  useLayoutEffect(() => {
    if (followPage && !activeCard.current?.contains(window.document.activeElement))
      activeCard.current?.scrollIntoView?.({ block: 'nearest' });
  }, [page, followPage]);
  useLayoutEffect(() => {
    if (selection && selection.page === page) {
      input.current?.focus();
      input.current?.setSelectionRange(selection.start, selection.end);
    }
  }, [input, page, selection]);
  function go(offset: number) {
    if (!matches.length) return;
    const index =
      current < 0
        ? offset > 0
          ? 0
          : matches.length - 1
        : (current + offset + matches.length) % matches.length;
    setCursor(index);
    setSelection(matches[index]);
    onPageChange(matches[index].page);
  }
  function replaceOne() {
    const match = matches[current < 0 ? 0 : current];
    if (!match) return;
    input.current?.focus();
    onEdit({ kind: 'replace-one', match, query, replacement });
    setSelection(null);
    setCursor(-1);
    setNotice('Treffer ersetzt, sofern sich der Text geändert hat.');
  }
  function replaceAll() {
    const count = countReplaceMatches(document, query);
    if (!count) {
      setNotice('Keine wörtlichen Treffer zum Ersetzen.');
      return;
    }
    input.current?.focus();
    onEdit({ kind: 'replace-all', query, replacement });
    setSelection(null);
    setCursor(-1);
    setNotice(`${count} ${count === 1 ? 'Stelle' : 'Stellen'} ersetzt.`);
  }
  return (
    <section className={styles.pane} aria-label="Transkript">
      <header className={styles.srOnly}>
        <h2>Transkript · Seite {page}</h2>
        <span>
          {document.transcript
            ? document.transcript.edited
              ? 'Bearbeitet · nur in dieser Sitzung'
              : 'Vorhanden'
            : 'Noch kein Transkript'}
        </span>
      </header>
      <div className={styles.search}>
        <strong>Transkript</strong>
        <div className={styles.group} role="group" aria-label="Transkript durchsuchen">
          <input
            value={query}
            aria-label="Suchen"
            placeholder="Suchen"
            aria-describedby="transcript-search-help"
            onChange={(event) => {
              setQuery(event.target.value);
              setCursor(-1);
              setSelection(null);
              setNotice('');
            }}
          />
          <button
            className={styles.iconButton}
            aria-label="Vorheriger Treffer"
            title="Vorheriger Treffer"
            disabled={!matches.length}
            onClick={() => go(-1)}
          >
            <Icon name="chevron" className={styles.previousIcon} />
          </button>
          <button
            className={styles.iconButton}
            aria-label="Nächster Treffer"
            title="Nächster Treffer"
            disabled={!matches.length}
            onClick={() => go(1)}
          >
            <Icon name="chevron" />
          </button>
          {query && <span className={styles.count}>{matches.length} Treffer</span>}
        </div>
        <div
          className={`${styles.group} ${styles.replaceGroup}`}
          role="group"
          aria-label="Transkript ersetzen"
        >
          <input
            aria-label="Ersetzen durch"
            placeholder="Ersetzen durch"
            value={replacement}
            onChange={(event) => setReplacement(event.target.value)}
          />
          <button disabled={!matches.length} onClick={replaceOne}>
            Ersetzen
          </button>
          <button disabled={!matches.length} onClick={replaceAll}>
            Alle ersetzen
          </button>
        </div>
      </div>
      <span className={styles.srOnly} id="transcript-search-help">
        Die Suche ignoriert Akzente; „Alle ersetzen“ beachtet sie.
      </span>
      <span role="status" className={styles.notice}>
        {notice}
      </span>
      <div className={styles.cards}>
        {Array.from({ length: document.pages ?? 1 }, (_, index) => {
          const number = index + 1;
          const text = texts.get(number) ?? '';
          return (
            <section
              key={number}
              ref={number === page ? activeCard : undefined}
              className={`${styles.card} ${number === page ? styles.active : ''}`}
              aria-label={`Transkriptkarte Seite ${number}`}
            >
              <div className={styles.cardHeader}>
                <button
                  type="button"
                  onClick={() => onPageChange(number)}
                  aria-current={number === page ? 'true' : undefined}
                >
                  Seite {number}
                </button>
                <span>{text.length} Zeichen</span>
              </div>
              <textarea
                ref={number === page ? input : undefined}
                aria-label={`Transkript Seite ${number}`}
                value={text}
                rows={Math.min(12, Math.max(3, text.split('\n').length + 1))}
                placeholder="Noch kein Transkript"
                onFocus={() => {
                  if (number !== page) onPageChange(number);
                }}
                onChange={(event) => {
                  onEdit({ kind: 'set', page: number, text: event.target.value });
                  setSelection(null);
                  setCursor(-1);
                }}
                spellCheck
              />
            </section>
          );
        })}
      </div>
      <p>Änderungen gelten sofort im Arbeitsspeicher und gehen beim Neuladen verloren.</p>
    </section>
  );
}
