import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, errorMessage } from './api';
import type {
  ApiClient,
  Invite,
  InviteCreate,
  Provisioning,
  Recovery,
  TechnicalInvitation,
  UserPatch,
} from './api';
import type { components } from '../../../../backend/contracts/fixture.generated';
import { Icon } from '../../components/Icon';
import styles from './AuthApp.module.css';

const stateLabel: Record<Provisioning['state'], string> = {
  pending_invitation: 'Einladung ausstehend',
  ready: 'Bereit zur Verarbeitung',
  registering: 'Registrierung läuft',
  recovery_required: 'Überprüfung erforderlich',
  active: 'Technisch aktiv',
  cancelled: 'Abgebrochen',
};

type ProvisioningPage = components['schemas']['PageOfProvisioning'];

function isAuthorityFailure(failure: unknown) {
  return (
    failure instanceof ApiError &&
    (failure.status === 401 || (failure.status === 403 && failure.code === 'forbidden'))
  );
}
type ProvisioningView = {
  page: ProvisioningPage;
  revisions: ReadonlyMap<string, number>;
};

// Request order, not response arrival order, determines which observation is current.
function mergeProvisioningPage(
  previous: ProvisioningView | null,
  page: ProvisioningPage,
  revision: number,
): ProvisioningView {
  const revisions = new Map<string, number>();
  const items = page.items.map((item) => {
    const priorRevision = previous?.revisions.get(item.workspaceId) ?? 0;
    if (priorRevision > revision) {
      const prior = previous?.page.items.find((row) => row.workspaceId === item.workspaceId);
      if (prior) {
        revisions.set(item.workspaceId, priorRevision);
        return prior;
      }
    }
    revisions.set(item.workspaceId, revision);
    return item;
  });
  return { page: { ...page, items }, revisions };
}

/** The token and its copy status exist only for the current one-time display. */
function InvitationToken({ token, onClose }: { token: string; onClose: () => void }) {
  const [copyStatus, setCopyStatus] = useState('');
  async function copy() {
    try {
      await navigator.clipboard.writeText(token);
      setCopyStatus('Token kopiert.');
    } catch {
      setCopyStatus('Kopieren nicht möglich. Bitte den vollständigen Text markieren und kopieren.');
    }
  }
  async function copyLink() {
    const link = new URL('/', window.location.origin);
    link.hash = '/register?' + new URLSearchParams({ invite: token });
    try {
      await navigator.clipboard.writeText(link.href);
      setCopyStatus('Einladungslink kopiert.');
    } catch {
      setCopyStatus('Einladungslink konnte nicht kopiert werden. Versuche es erneut.');
    }
  }
  return (
    <div className={styles.tokenPanel}>
      <strong>Einmaliges Einladungstoken</strong>
      <p className={styles.hint}>Wird nur jetzt angezeigt. Kopiere es und bewahre es sicher auf.</p>
      <output className={styles.token}>{token}</output>
      <div className={styles.actions}>
        <button type="button" className={styles.primary} onClick={() => void copy()}>
          Token kopieren
        </button>
        <button type="button" onClick={() => void copyLink()}>
          Einladungslink kopieren
        </button>
        <button type="button" onClick={onClose}>
          Schließen
        </button>
      </div>
      <span role="status">{copyStatus}</span>
    </div>
  );
}

export function AdminPanel({
  api,
  onUnauthorized,
  onForbidden,
}: {
  api: ApiClient;
  onUnauthorized: () => void;
  onForbidden?: () => void;
}) {
  const [users, setUsers] = useState<components['schemas']['PageOfAdminUser'] | null>(null);
  const [invitations, setInvitations] = useState<components['schemas']['PageOfInvite'] | null>(
    null,
  );
  const [provisioningView, setProvisioning] = useState<ProvisioningView | null>(null);
  const provisioning = provisioningView?.page;
  const [token, setToken] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [workspace, setWorkspace] = useState('');
  const [technicalToken, setTechnicalToken] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const mounted = useRef(true);
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const scope = useRef(new AbortController());
  const sequence = useRef(0);
  const listRevisions = useRef({ users: 0, invitations: 0, provisioning: 0 });
  const statusRevisions = useRef(new Map<string, number>());

  const request = useCallback(
    <T,>(path: string, method = 'GET', body?: unknown, headers?: Record<string, string>) =>
      api.request<T>(path, method, body, headers, scope.current.signal),
    [api],
  );

  const report = useCallback(
    (failure: unknown) => {
      if (!mounted.current || (failure instanceof DOMException && failure.name === 'AbortError'))
        return;
      if (failure instanceof ApiError && failure.status === 401) onUnauthorized();
      else if (
        failure instanceof ApiError &&
        failure.status === 403 &&
        failure.code === 'forbidden'
      ) {
        mounted.current = false;
        scope.current.abort();
        onForbidden?.();
      } else setError(errorMessage(failure));
    },
    [onUnauthorized, onForbidden],
  );

  const load = useCallback(
    async (kind: 'users' | 'invitations' | 'provisioning', cursor?: string) => {
      const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
      const revision = ++sequence.current;
      listRevisions.current[kind] = revision;
      const isCurrent = () => mounted.current && listRevisions.current[kind] === revision;
      try {
        if (kind === 'users') {
          const result = await request<components['schemas']['PageOfAdminUser']>(
            `/api/v1/admin/users${query}`,
          );
          if (isCurrent()) setUsers(result.data);
        } else if (kind === 'invitations') {
          const result = await request<components['schemas']['PageOfInvite']>(
            `/api/v1/admin/invitations${query}`,
          );
          if (isCurrent()) setInvitations(result.data);
        } else {
          const result = await request<ProvisioningPage>(`/api/v1/admin/provisioning${query}`);
          if (isCurrent())
            setProvisioning((previous) => mergeProvisioningPage(previous, result.data, revision));
        }
      } catch (failure) {
        if (isCurrent() || isAuthorityFailure(failure)) report(failure);
      }
    },
    [request, report],
  );

  useEffect(() => {
    let disposed = false;
    const pendingTimers = timers.current;
    const pendingStatuses = statusRevisions.current;
    scope.current = new AbortController();
    mounted.current = true;
    void Promise.resolve().then(() => {
      if (disposed) return;
      void load('users');
      void load('invitations');
      void load('provisioning');
    });
    return () => {
      disposed = true;
      scope.current.abort();
      mounted.current = false;
      for (const timer of pendingTimers.values()) clearTimeout(timer);
      pendingTimers.clear();
      pendingStatuses.clear();
    };
  }, [load]);

  async function action(work: () => Promise<void>) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await work();
    } catch (failure) {
      report(failure);
    } finally {
      if (mounted.current) setBusy(false);
    }
  }

  function updateStatus(item: Provisioning, revision: number) {
    setProvisioning((previous) => {
      if (
        !previous ||
        (previous.revisions.get(item.workspaceId) ?? 0) > revision ||
        !previous.page.items.some((row) => row.workspaceId === item.workspaceId)
      )
        return previous;
      const revisions = new Map(previous.revisions);
      revisions.set(item.workspaceId, revision);
      return {
        page: {
          ...previous.page,
          items: previous.page.items.map((row) =>
            row.workspaceId === item.workspaceId ? item : row,
          ),
        },
        revisions,
      };
    });
  }

  function supersedeStatus(workspaceId: string) {
    const timer = timers.current.get(workspaceId);
    if (timer !== undefined) clearTimeout(timer);
    timers.current.delete(workspaceId);
    const revision = ++sequence.current;
    statusRevisions.current.set(workspaceId, revision);
    return revision;
  }

  function scheduleStatus(response: Response, workspaceId: string, revision: number) {
    const location = response.headers.get('Location');
    const retry = Number(response.headers.get('Retry-After'));
    if (
      !location ||
      !location.startsWith('/api/v1/admin/provisioning?') ||
      !Number.isFinite(retry) ||
      retry < 0 ||
      retry > 2_147_483 ||
      !/^\d+$/.test(response.headers.get('Retry-After') ?? '')
    )
      return false;
    const url = new URL(location, window.location.origin);
    if (
      url.origin !== window.location.origin ||
      url.pathname !== '/api/v1/admin/provisioning' ||
      [...url.searchParams.keys()].length !== 1 ||
      url.searchParams.get('workspaceId') !== workspaceId
    )
      return false;
    timers.current.set(
      workspaceId,
      setTimeout(
        () => {
          timers.current.delete(workspaceId);
          if (statusRevisions.current.get(workspaceId) !== revision) return;
          const readRevision = ++sequence.current;
          statusRevisions.current.set(workspaceId, readRevision);
          void request<ProvisioningPage>(location)
            .then((result) => {
              if (!mounted.current || statusRevisions.current.get(workspaceId) !== readRevision)
                return;
              const item = result.data.items.find((fresh) => fresh.workspaceId === workspaceId);
              if (item) updateStatus(item, readRevision);
            })
            .catch((failure: unknown) => {
              if (
                statusRevisions.current.get(workspaceId) === readRevision ||
                isAuthorityFailure(failure)
              )
                report(failure);
            })
            .finally(() => {
              if (statusRevisions.current.get(workspaceId) === readRevision)
                statusRevisions.current.delete(workspaceId);
            });
        },
        Math.max(1, retry) * 1000,
      ),
    );
    return true;
  }

  async function provision(id: string, recovery?: Recovery['action']) {
    const target = `/api/v1/admin/workspaces/${encodeURIComponent(id)}/${recovery ? 'recovery' : 'technical-invitation'}`;
    const body: Recovery | TechnicalInvitation = recovery
      ? { action: recovery }
      : { token: technicalToken };
    setTechnicalToken('');
    setConfirmed(false);
    const revision = supersedeStatus(id);
    let scheduled = false;
    try {
      const result = await request<Provisioning>(target, 'POST', body);
      if (!mounted.current || statusRevisions.current.get(id) !== revision) return;
      // The reply describes the POST, even when a later observation arrived first.
      if (result.data.workspaceId === id) updateStatus(result.data, revision);
      setNotice('Die Verarbeitung wurde angenommen. Der Abschluss steht noch aus.');
      scheduled = scheduleStatus(result.response, id, revision);
    } finally {
      if (!scheduled && statusRevisions.current.get(id) === revision)
        statusRevisions.current.delete(id);
    }
  }

  return (
    <section className={styles.adminLayout} aria-label="Administration">
      <nav className={styles.adminNav} aria-label="Administrationsbereiche">
        <h2>Administration</h2>
        <a href="#admin-invitations">
          <Icon name="tag" /> Einladungen
        </a>
        <a href="#admin-users">
          <Icon name="list" /> Benutzer
        </a>
        <a href="#admin-provisioning">
          <Icon name="gear" /> Bereitstellung
        </a>
      </nav>
      <div className={styles.adminContent}>
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
        {notice && <p role="status">{notice}</p>}
        <section
          id="admin-invitations"
          className={styles.card}
          aria-labelledby="admin-invitations-heading"
        >
          <div className={styles.row}>
            <h3 id="admin-invitations-heading">Einladungen</h3>
            <button
              className={styles.primary}
              disabled={busy}
              onClick={() =>
                void action(async () => {
                  setToken('');
                  const input: InviteCreate = {};
                  const result = await request<Invite>('/api/v1/admin/invitations', 'POST', input);
                  setToken(result.data.token ?? '');
                  await load('invitations');
                })
              }
            >
              <Icon name="plus" /> Einladung erstellen
            </button>
          </div>
          {token && <InvitationToken key={token} token={token} onClose={() => setToken('')} />}
          <ul className={styles.records}>
            {invitations?.items.map((item) => (
              <li key={item.id}>
                <div className={styles.recordText}>
                  <span>{item.id}</span>
                  <small>
                    {item.revokedAt
                      ? 'Widerrufen'
                      : item.redeemedAt
                        ? 'Eingelöst'
                        : `Gültig bis ${new Date(item.expiresAt).toLocaleString('de-DE')}`}
                  </small>
                </div>
                {!item.revokedAt && !item.redeemedAt && (
                  <button
                    disabled={busy}
                    onClick={() =>
                      void action(async () => {
                        await request(`/api/v1/admin/invitations/${item.id}`, 'DELETE');
                        await load('invitations');
                      })
                    }
                  >
                    Widerrufen
                  </button>
                )}
              </li>
            ))}
          </ul>
          {invitations && invitations.items.length === 0 && <p>Keine Einladungen vorhanden.</p>}
          {invitations?.nextCursor && (
            <button onClick={() => void load('invitations', invitations.nextCursor!)}>
              Weitere Einladungen
            </button>
          )}
        </section>
        <section id="admin-users" className={styles.card} aria-labelledby="admin-users-heading">
          <h3 id="admin-users-heading">Benutzer</h3>
          <p className={styles.hint}>
            Das letzte aktive Administratorkonto kann nicht deaktiviert werden.
          </p>
          <ul className={styles.records}>
            {users?.items.map((user) => (
              <li key={user.id}>
                <div className={styles.recordText}>
                  <span>{user.userName ?? user.id}</span>
                  <small>
                    {user.isDisabled ? 'Deaktiviert' : 'Aktiv'} ·{' '}
                    {user.accountKind === 'admin_only' ? 'Administrationskonto' : 'Benutzerkonto'}
                  </small>
                </div>
                <button
                  disabled={busy}
                  onClick={() =>
                    void action(async () => {
                      const patch: UserPatch = { isDisabled: !user.isDisabled };
                      await request(`/api/v1/admin/users/${user.id}`, 'PATCH', patch, {
                        'If-Match': user.etag,
                      });
                      await load('users');
                    })
                  }
                >
                  {user.isDisabled ? 'Freigeben' : 'Deaktivieren'}
                </button>
              </li>
            ))}
          </ul>
          {users && users.items.length === 0 && <p>Keine Benutzer vorhanden.</p>}
          <button onClick={() => void load('users')}>Benutzerliste aktualisieren</button>
          {users?.nextCursor && (
            <button onClick={() => void load('users', users.nextCursor!)}>Weitere Benutzer</button>
          )}
        </section>
        <section
          id="admin-provisioning"
          className={styles.card}
          aria-labelledby="admin-provisioning-heading"
        >
          <h3 id="admin-provisioning-heading">Technische Bereitstellung</h3>
          <p className={styles.hint}>
            Technische Einladung aus VIA.Docs für einen vorhandenen Workspace hinterlegen.
          </p>
          <form
            className={styles.form}
            onSubmit={(event) => {
              event.preventDefault();
              void action(() => provision(workspace));
            }}
          >
            <label>
              Workspace-ID
              <input
                value={workspace}
                onChange={(event) => {
                  setWorkspace(event.target.value);
                  setTechnicalToken('');
                  setConfirmed(false);
                }}
                required
              />
            </label>
            <label>
              Technische Einladung
              <input
                type="password"
                autoComplete="off"
                value={technicalToken}
                onChange={(event) => setTechnicalToken(event.target.value)}
                required
              />
            </label>
            <label className={styles.checkbox}>
              <input
                type="checkbox"
                checked={confirmed}
                onChange={(event) => setConfirmed(event.target.checked)}
                required
              />
              Ich bestätige die Übergabe oder den Austausch der technischen Einladung.
            </label>
            <div className={styles.actions}>
              <button className={styles.primary} disabled={busy || !confirmed}>
                Einladung hinterlegen
              </button>
              <button
                type="button"
                onClick={() => {
                  setTechnicalToken('');
                  setConfirmed(false);
                }}
              >
                Eingabe verwerfen
              </button>
            </div>
          </form>
          <ul className={styles.records}>
            {provisioning?.items.map((item) => (
              <li key={item.workspaceId}>
                <span className={styles.recordText}>
                  {item.workspaceId} · {stateLabel[item.state]}
                </span>
                {item.state === 'ready' && (
                  <button
                    disabled={busy}
                    onClick={() => void action(() => provision(item.workspaceId, 'retry_if_safe'))}
                  >
                    Verarbeitung bestätigen
                  </button>
                )}
                {item.state === 'recovery_required' && (
                  <button
                    disabled={busy}
                    onClick={() => void action(() => provision(item.workspaceId, 'verify'))}
                  >
                    Überprüfung bestätigen
                  </button>
                )}
              </li>
            ))}
          </ul>
          {provisioning && provisioning.items.length === 0 && (
            <p>Keine Bereitstellungen vorhanden.</p>
          )}
          {provisioning?.nextCursor && (
            <button onClick={() => void load('provisioning', provisioning.nextCursor!)}>
              Weitere Bereitstellungen
            </button>
          )}
          <button onClick={() => void load('provisioning')}>Status aktualisieren</button>
        </section>
      </div>
    </section>
  );
}
