import { useLayoutEffect, useRef, useState } from 'react';
import type { DocumentSummary } from '../documents/document.types';
import { findTranscriptMatches, countReplaceMatches } from '../documents/transcriptOperations';
import type { TranscriptMatch, TranscriptOperation } from '../documents/transcriptOperations';
import styles from './TranscriptPane.module.css';
export function TranscriptPane({
  document,
  page,
  onPageChange,
  onEdit,
  editorRef,
}: {
  readonly document: DocumentSummary;
  readonly page: number;
  readonly onPageChange: (page: number) => void;
  readonly onEdit: (operation: TranscriptOperation) => void;
  readonly editorRef?: React.RefObject<HTMLTextAreaElement | null>;
}) {
  const [query, setQuery] = useState('');
  const [replacement, setReplacement] = useState('');
  const [cursor, setCursor] = useState(-1);
  const [selection, setSelection] = useState<TranscriptMatch | null>(null);
  const [notice, setNotice] = useState('');
  const fallbackInput = useRef<HTMLTextAreaElement>(null);
  const input = editorRef ?? fallbackInput;
  const matches = findTranscriptMatches(document, query);
  const current = cursor >= 0 && cursor < matches.length ? cursor : -1;
  const value = document.transcript?.pages.find((part) => part.page === page)?.text ?? '';
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
      <header>
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
        <label>
          Suchen
          <input
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setCursor(-1);
              setSelection(null);
              setNotice('');
            }}
          />
        </label>
        <label>
          Ersetzen durch
          <input value={replacement} onChange={(event) => setReplacement(event.target.value)} />
        </label>
        <span>{query ? `${matches.length} Treffer` : 'Suchbegriff eingeben'}</span>
        <button disabled={!matches.length} onClick={() => go(-1)}>
          Vorheriger Treffer
        </button>
        <button disabled={!matches.length} onClick={() => go(1)}>
          Nächster Treffer
        </button>
        <button disabled={!matches.length} onClick={replaceOne}>
          Ersetzen
        </button>
        <button disabled={!matches.length} onClick={replaceAll}>
          Alle ersetzen
        </button>
      </div>
      <p>Die Suche ignoriert Akzente; „Alle ersetzen“ beachtet sie.</p>
      <span role="status" className={styles.notice}>
        {notice}
      </span>
      <label className={styles.editor}>
        Transkript Seite {page}
        <textarea
          ref={input}
          value={value}
          onChange={(event) => {
            onEdit({ kind: 'set', page, text: event.target.value });
            setSelection(null);
            setCursor(-1);
          }}
          spellCheck
        />
      </label>
      <p>Änderungen gelten sofort im Arbeitsspeicher und gehen beim Neuladen verloren.</p>
    </section>
  );
}
