import type { DocumentSummary } from '../documents/document.types';
import styles from './DocumentViewer.module.css';
import { DemoInkPage } from '../documents/DemoInkPage';
export function ViewerPage({
  document,
  page,
  zoom,
}: {
  readonly document: DocumentSummary;
  readonly page: number;
  readonly zoom: number;
}) {
  return (
    <div className={styles.page} style={{ width: `${zoom * 6.8}px` }}>
      <DemoInkPage document={document} page={page} label="Synthetische Demoseite – kein PDF" />
    </div>
  );
}
