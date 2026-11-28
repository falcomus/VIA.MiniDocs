import type { ReactNode } from 'react';
import { App } from '../../App';
import type { ApiClient } from '../auth/api';
import { usePersistentWorkspace } from './usePersistentWorkspace';

export function PersistentApp({
  api,
  workspaceId,
  revalidation,
  onSessionChanged,
  actions,
}: {
  readonly api: ApiClient;
  readonly workspaceId: string;
  readonly revalidation: number;
  readonly onSessionChanged: () => void;
  readonly actions: ReactNode;
}) {
  const workspace = usePersistentWorkspace(api, workspaceId, onSessionChanged, revalidation);
  return workspace.ready ? (
    <App persistent={workspace} accountActions={actions} />
  ) : (
    <section>
      <p role="status">Dein Workspace ist aktiv. Die Dokumentübersicht wird geladen.</p>
      {workspace.error && (
        <p role="alert">
          {workspace.error} <button onClick={() => void workspace.reload()}>Neu laden</button>
        </p>
      )}
      {actions}
    </section>
  );
}
