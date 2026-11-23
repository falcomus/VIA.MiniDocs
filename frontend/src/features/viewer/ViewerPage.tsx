import type { DocumentSummary } from '../documents/document.types';
import styles from './DocumentViewer.module.css';
export function ViewerPage({
  document,
  page,
  zoom,
}: {
  readonly document: DocumentSummary;
  readonly page: number;
  readonly zoom: number;
}) {
  const lines = page === 1 ? document.demoPreview?.lines : undefined;
  return (
    <div
      className={styles.page}
      style={{
        width: `${zoom * 6.8}px`,
        minHeight: `${zoom * 6}px`,
        padding: `${zoom * 0.3}px`,
        fontSize: `${zoom / 100}em`,
      }}
    >
      <strong>Synthetische Demoseite – kein PDF</strong>
      <h3>{document.title}</h3>
      <p>
        Seite {page} von {document.pages}
      </p>
      {lines ? (
        <pre>{lines.join('\n')}</pre>
      ) : (
        <p>Für diese Seite sind keine Demo-Textdaten vorhanden.</p>
      )}
      <p>Die synthetische Seite ist unabhängig vom bearbeitbaren Transkript.</p>
    </div>
  );
}
