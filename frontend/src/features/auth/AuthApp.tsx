import { useCallback, useEffect, useRef, useState } from 'react';
import { useTheme } from '../settings/useTheme';
import { AdminPanel } from './AdminPanel';
import { ApiClient, ApiError, errorMessage } from './api';
import type { Login, Registration, Session } from './api';
import { AppHeader } from '../../components/AppHeader';
import styles from './AuthApp.module.css';

const signedInInviteNotice =
  'Du bist bereits angemeldet. Melde dich ab und öffne den Einladungslink erneut, um ein neues Konto zu erstellen.';

export function AuthApp() {
  const [api] = useState(() => new ApiClient());
  const [sessionView, setSession] = useState<{ session: Session; generation: number } | null>(null);
  const session = sessionView?.session;
  const [loading, setLoading] = useState(true);
  const [register, setRegister] = useState(false);
  const [linkToken, setLinkToken] = useState('');
  const inviteDuringSessionCheck = useRef(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [logoutUnknown, setLogoutUnknown] = useState(false);
  const epoch = useRef(0);
  const sessionRead = useRef<number | null>(null);
  const submission = useRef(false);
  const deferredRefresh = useRef<number | null>(null);
  const logoutLocked = useRef(false);
  const channel = useRef<BroadcastChannel | null>(null);
  const initialInput = useRef<HTMLInputElement>(null);
  const { mode, setMode } = useTheme();

  useEffect(() => {
    if (!loading && !session && !logoutUnknown) initialInput.current?.focus();
  }, [loading, session, register, logoutUnknown, linkToken]);

  useEffect(() => {
    function consumeInvite() {
      const prefix = '#/register?';
      if (!window.location.hash.startsWith(prefix)) return;
      const token = new URLSearchParams(window.location.hash.slice(prefix.length)).get('invite');
      window.history.replaceState(
        window.history.state,
        '',
        window.location.pathname + window.location.search,
      );
      if (session) {
        setMessage(signedInInviteNotice);
        return;
      }
      if (submission.current || logoutLocked.current) return;
      inviteDuringSessionCheck.current = true;
      const valid = !!token && /^[A-Za-z0-9_-]{43}$/.test(token);
      setRegister(true);
      setLinkToken(valid ? token : '');
      setMessage('');
      setError(
        valid
          ? ''
          : 'Der Einladungslink ist ungültig. Du kannst einen Einladungsschlüssel manuell eingeben.',
      );
    }
    consumeInvite();
    window.addEventListener('hashchange', consumeInvite);
    return () => window.removeEventListener('hashchange', consumeInvite);
  }, [session]);

  const invalidate = useCallback(() => {
    epoch.current++;
    api.reset();
  }, [api]);

  const clear = useCallback(() => {
    invalidate();
    setSession(null);
    setError('');
    setMessage('');
    setBusy(false);
    setLogoutUnknown(false);
  }, [invalidate]);

  const refresh = useCallback(async () => {
    if (logoutLocked.current || submission.current) return;
    const generation = epoch.current;
    if (sessionRead.current === generation) return;
    sessionRead.current = generation;
    setLoading(true);
    try {
      const result = await api.request<Session>('/api/v1/session');
      if (generation === epoch.current && !logoutLocked.current) {
        if (inviteDuringSessionCheck.current) setMessage(signedInInviteNotice);
        setLinkToken('');
        setRegister(false);
        setSession({ session: result.data, generation });
      }
    } catch (failure) {
      if (generation !== epoch.current) return;
      if (!(failure instanceof ApiError && failure.status === 401)) setError(errorMessage(failure));
      setSession(null);
    } finally {
      if (generation === epoch.current) inviteDuringSessionCheck.current = false;
      if (sessionRead.current === generation) sessionRead.current = null;
      if (generation === epoch.current) setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    let disposed = false;
    void Promise.resolve().then(() => {
      if (!disposed) return refresh();
    });
    if (typeof BroadcastChannel !== 'undefined') {
      channel.current = new BroadcastChannel('minidocs-session-v1');
      channel.current.onmessage = () => {
        if (logoutLocked.current) return;
        if (submission.current) {
          deferredRefresh.current = epoch.current;
          return;
        }
        clear();
        void refresh();
      };
    }
    const visibility = () => {
      if (logoutLocked.current) return;
      if (submission.current) {
        if (document.visibilityState === 'visible') deferredRefresh.current = epoch.current;
        return;
      }
      clear();
      if (document.visibilityState === 'visible') void refresh();
    };
    document.addEventListener('visibilitychange', visibility);
    return () => {
      disposed = true;
      invalidate();
      document.removeEventListener('visibilitychange', visibility);
      channel.current?.close();
      channel.current = null;
    };
  }, [clear, invalidate, refresh]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submission.current || sessionRead.current === epoch.current) return;
    submission.current = true;
    const form = event.currentTarget;
    const data = new FormData(form);
    const generation = epoch.current;
    setBusy(true);
    setError('');
    try {
      const login: Login = {
        userName: String(data.get('userName')),
        password: String(data.get('password')),
      };
      if (register) {
        const input: Registration = {
          ...login,
          token: linkToken || String(data.get('token')).trim(),
        };
        await api.request('/api/v1/registration', 'POST', input);
        if (generation !== epoch.current) return;
        api.reset();
        setLinkToken('');
        setRegister(false);
        setMessage('Registrierung abgeschlossen. Bitte melde dich an.');
      } else {
        const result = await api.request<Session>('/api/v1/session/login', 'POST', login);
        if (generation !== epoch.current) return;
        api.reset();
        // Only a confirmed, current login may release an uncertain logout.
        logoutLocked.current = false;
        setSession({ session: result.data, generation });
        setMessage('');
        channel.current?.postMessage('changed');
      }
    } catch (failure) {
      if (generation === epoch.current) {
        if (register && failure instanceof ApiError && failure.status === 410) setLinkToken('');
        setError(
          register &&
            failure instanceof ApiError &&
            failure.status === 400 &&
            failure.code === 'validation_failed'
            ? 'Die Registrierung wurde abgelehnt. Prüfe das vollständige Einladungstoken, den Benutzernamen und die Passwortregeln.'
            : !(failure instanceof ApiError)
              ? 'Die Verbindung zum MiniDocs-Backend ist fehlgeschlagen. Prüfe, ob MiniDocs gestartet ist, bevor du es erneut versuchst.'
              : errorMessage(failure),
        );
      }
    } finally {
      submission.current = false;
      form.reset();
      const revalidate = deferredRefresh.current === generation;
      deferredRefresh.current = null;
      if (generation === epoch.current) {
        setBusy(false);
        if (revalidate && !logoutLocked.current) {
          invalidate();
          setSession(null);
          void refresh();
        }
      }
    }
  }

  async function logout() {
    const generation = epoch.current;
    logoutLocked.current = true;
    api.reset();
    setBusy(true);
    setError('');
    setSession(null);
    setLogoutUnknown(true);
    try {
      await api.request('/api/v1/session', 'DELETE');
      if (generation !== epoch.current) return;
      logoutLocked.current = false;
      clear();
      channel.current?.postMessage('changed');
    } catch (failure) {
      if (generation !== epoch.current) return;
      if (failure instanceof ApiError && failure.status === 401) {
        logoutLocked.current = false;
        clear();
        channel.current?.postMessage('changed');
      } else {
        clear();
        setLogoutUnknown(true);
        setError(errorMessage(failure));
      }
    } finally {
      if (generation === epoch.current) setBusy(false);
    }
  }

  const invalidated = useCallback(() => {
    clear();
    channel.current?.postMessage('changed');
  }, [clear]);

  const forbidden = useCallback(() => {
    clear();
    void refresh();
  }, [clear, refresh]);

  return (
    <main className={styles.page}>
      <AppHeader subtitle="VIA.MiniDocs">
        <label className={styles.theme}>
          Darstellung{' '}
          <select value={mode} onChange={(e) => setMode(e.target.value as typeof mode)}>
            <option value="system">System</option>
            <option value="light">Hell</option>
            <option value="dark">Dunkel</option>
          </select>
        </label>
      </AppHeader>
      <div className={`${styles.content}${session ? '' : ` ${styles.authContent}`}`}>
        {!session && <AuthArtwork />}
        {loading ? (
          <p role="status">Sitzung wird geprüft …</p>
        ) : logoutUnknown ? (
          <section className={`${styles.card} ${styles.loginCard}`}>
            <AuthBrand />
            <h1>Abmeldung wird geprüft</h1>
            <p>Die serverseitige Abmeldung wurde noch nicht bestätigt.</p>
            {error && (
              <p role="alert" className={styles.error}>
                {error}
              </p>
            )}
            <button
              disabled={busy}
              onClick={() => {
                clear();
                setRegister(false);
              }}
            >
              Erneut anmelden
            </button>
          </section>
        ) : session ? (
          <>
            <section className={styles.card}>
              <div className={styles.row}>
                <h1>Willkommen, {session.displayName}</h1>
                <button disabled={busy} onClick={() => void logout()}>
                  Abmelden
                </button>
              </div>
              {session.workspaceStatus === 'pending' && (
                <p>
                  Dein Workspace wird eingerichtet. Die technische Bereitstellung steht noch aus.
                </p>
              )}
              {session.workspaceStatus === 'suspended' && (
                <p>Dein Workspace ist gesperrt. Bitte wende dich an die Administration.</p>
              )}
              {session.workspaceStatus === 'active' && (
                <p>Dein Workspace ist aktiv. Die Dokumentfunktionen werden noch angebunden.</p>
              )}
              {session.workspaceId === null && (
                <p>Dieses Administrationskonto hat keinen privaten Workspace.</p>
              )}
            </section>
            {error && (
              <p role="alert" className={styles.error}>
                {error}
              </p>
            )}
            {message && <p role="status">{message}</p>}
            {sessionView && session.roles.includes('ADMINISTRATOR') && (
              <AdminPanel
                key={`${session.userId}:${sessionView.generation}`}
                api={api}
                onUnauthorized={invalidated}
                onForbidden={forbidden}
              />
            )}
          </>
        ) : (
          <section className={`${styles.card} ${styles.loginCard}`}>
            <AuthBrand />
            <div className={styles.authIntro}>
              <h1>{register ? 'Mit Einladung registrieren' : 'Anmelden'}</h1>
              <p>
                {register
                  ? 'Erstelle deinen persönlichen Zugang zu MiniDocs.'
                  : 'Dein Zugang zum digitalen Dokumentenarchiv.'}
              </p>
            </div>
            {error && (
              <p role="alert" className={styles.error}>
                {error}
              </p>
            )}
            {message && <p role="status">{message}</p>}
            <form
              key={register ? 'register' : 'login'}
              onSubmit={(e) => void submit(e)}
              className={styles.form}
            >
              {register && !linkToken && (
                <label>
                  Einladungstoken
                  <input
                    ref={initialInput}
                    name="token"
                    autoComplete="off"
                    autoCapitalize="none"
                    spellCheck={false}
                    aria-describedby="registration-token-hint"
                    required
                  />
                </label>
              )}
              {register && !linkToken && (
                <p id="registration-token-hint" className={styles.hint}>
                  Das Einladungstoken besteht aus 43 Zeichen. Bitte vollständig einfügen.
                </p>
              )}
              <label>
                Benutzername
                <input
                  ref={register && !linkToken ? undefined : initialInput}
                  name="userName"
                  autoComplete="username"
                  maxLength={256}
                  required
                />
              </label>
              <label>
                Passwort
                <input
                  name="password"
                  type="password"
                  autoComplete={register ? 'new-password' : 'current-password'}
                  minLength={register ? 12 : undefined}
                  maxLength={register ? 128 : 1024}
                  aria-describedby={register ? 'registration-password-hint' : undefined}
                  required
                />
              </label>
              {register && (
                <p id="registration-password-hint" className={styles.hint}>
                  12 bis 128 Zeichen. Kein häufig verwendetes Passwort und nicht der Benutzername.
                </p>
              )}
              <button className={styles.primary} disabled={busy}>
                {register ? 'Registrieren' : 'Anmelden'}
              </button>
            </form>
            <button
              className={styles.secondary}
              onClick={() => {
                epoch.current++;
                setRegister(!register);
                setLinkToken('');
                setBusy(false);
                setError('');
                setMessage('');
                api.reset();
              }}
            >
              {register ? 'Zur Anmeldung' : 'Mit Einladung registrieren'}
            </button>
          </section>
        )}
      </div>
    </main>
  );
}

/** The familiar MiniDoc mark is repeated at a readable scale on the authentication card. */
function AuthBrand() {
  return (
    <div className={styles.authBrand}>
      <span className={styles.authLogo} aria-hidden="true">
        MD
      </span>
      <span>VIA.MiniDocs</span>
    </div>
  );
}

/** Decorative, theme-aware artwork; no external image requests or interactive elements. */
function AuthArtwork() {
  return (
    <svg
      className={styles.authArtwork}
      viewBox="0 0 1440 900"
      preserveAspectRatio="xMidYMid slice"
      aria-hidden="true"
      focusable="false"
    >
      <g fill="none" stroke="currentColor" strokeWidth="1.5">
        <g transform="translate(100 190) rotate(-13 160 210)">
          <rect x="0" y="22" width="270" height="354" rx="18" />
          <rect x="22" y="0" width="270" height="354" rx="18" fill="var(--panel)" />
          <path d="M220 0v72h72M55 108h160M55 132h202M55 156h136M55 205h202M55 229h174M55 253h202M55 277h94" />
          <rect x="55" y="54" width="110" height="22" rx="5" fill="currentColor" />
        </g>
        <g transform="translate(1055 410) rotate(12 140 180)">
          <rect x="22" y="22" width="265" height="350" rx="18" />
          <rect width="265" height="350" rx="18" fill="var(--panel)" />
          <path d="M195 0v70h70M34 102h194M34 126h144M34 150h194M34 208h194M34 232h168M34 256h194M34 280h110" />
          <rect x="34" y="52" width="100" height="22" rx="5" fill="currentColor" />
        </g>
        <path
          d="M-30 715C260 605 370 780 575 850M930 95C1120 180 1320 90 1470 160"
          strokeDasharray="5 12"
        />
        <circle cx="390" cy="125" r="7" />
        <circle cx="1090" cy="170" r="11" />
        <circle cx="1010" cy="740" r="5" />
        <circle cx="240" cy="748" r="9" />
      </g>
    </svg>
  );
}
