import { useReducer } from 'react';
import { documentReducer } from '../documents/documentOperations';
import { demoDocuments } from '../documents/demoDocuments';
import { demoTags } from '../documents/tags';
import { demoTopics } from '../topics/demoTopics';
import { useTheme } from '../settings/useTheme';
import { DocumentViewer } from './DocumentViewer';
import { demoDocumentSource } from './viewerModel';
export function StandaloneViewer() {
  useTheme();
  const [state, dispatch] = useReducer(documentReducer, {
    documents: demoDocuments,
    selected: [],
    anchor: null,
    undo: null,
    message: '',
  });
  const params = new URLSearchParams(window.location.search);
  const id = params.get('document') ?? '';
  const doc = state.documents.find((item) => item.id === id);
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
      getSource={demoDocumentSource}
      session={{ id, ids: [id], hits: { [id]: { page, term } } }}
      initialPage={page}
      documents={state.documents}
      onEditTranscript={(id, operation) =>
        dispatch({
          type: 'transcript',
          id,
          operation,
          visibleIds: (documents) => documents.map((document) => document.id),
        })
      }
      topics={demoTopics}
      tags={demoTags}
    />
  );
}
