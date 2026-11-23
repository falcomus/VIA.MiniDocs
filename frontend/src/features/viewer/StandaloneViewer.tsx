import { demoDocuments } from '../documents/demoDocuments';
import { demoTags } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import { useTheme } from '../settings/useTheme';
import { DocumentViewer } from './DocumentViewer';
export function StandaloneViewer() {
  useTheme();
  const params = new URLSearchParams(window.location.search);
  const id = params.get('document') ?? '';
  const doc = demoDocuments.find((item) => item.id === id);
  const rawPage = params.get('page');
  const page = rawPage === null ? 1 : Number(rawPage);
  if (
    params.get('viewer') !== 'tab' ||
    !doc ||
    !Number.isInteger(page) ||
    page < 1 ||
    page > doc.pages
  )
    return (
      <main>
        <h1>Dokument nicht verfügbar</h1>
        <p>Die Dokumentadresse oder Seitennummer ist ungültig.</p>
        <a href={window.location.pathname}>Zur Dokumentübersicht</a>
      </main>
    );
  const term = params.get('term') ?? '';
  return (
    <DocumentViewer
      session={{ id, ids: [id], hits: { [id]: { page, term } } }}
      initialPage={page}
      documents={demoDocuments}
      topics={demoTopics}
      tags={demoTags}
    />
  );
}
