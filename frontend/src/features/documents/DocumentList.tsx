import type { DocumentSummary } from './document.types';
import styles from './DocumentList.module.css';

interface DocumentListProps {
  readonly documents: readonly DocumentSummary[];
}

const dateFormatter = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'UTC',
});

export function DocumentList({ documents }: DocumentListProps) {
  if (documents.length === 0) {
    return (
      <div className={styles.empty} role="status">
        <strong>Keine Dokumente in diesem Thema</strong>
        <p>Dies ist nur eine Demo. Upload und echte Dokumentverwaltung folgen später.</p>
      </div>
    );
  }

  return (
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">Dokument</th>
            <th scope="col">Datum</th>
            <th scope="col">Seiten</th>
          </tr>
        </thead>
        <tbody>
          {documents.map((document) => (
            <tr key={document.id}>
              <td>
                <div className={styles.titleRow}>
                  <span className={styles.documentIcon} aria-hidden="true">
                    ▤
                  </span>
                  <span>
                    <strong className={styles.title}>{document.title}</strong>
                    <span className={styles.kind}>{document.kind}</span>
                  </span>
                </div>
              </td>
              <td className={styles.secondary}>
                {dateFormatter.format(new Date(`${document.date}T00:00:00Z`))}
              </td>
              <td className={styles.secondary}>{document.pages}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
