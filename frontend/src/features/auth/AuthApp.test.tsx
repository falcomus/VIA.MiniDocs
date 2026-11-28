import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthApp } from './AuthApp';

const session = {
  userId: 'u',
  displayName: 'Clara',
  roles: [],
  workspaceId: 'w',
  workspaceStatus: 'pending',
};
function response(status: number, body: unknown = {}) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    headers: new Headers(),
  } as Response;
}
afterEach(() => vi.unstubAllGlobals());

it('behält bei Sichtbarkeitswechsel die Themenauswahl einer bestätigten Workspace-Sitzung', async () => {
  const active = { ...session, workspaceStatus: 'active' };
  let reads = 0;
  let current = active;
  let revoked = false;
  let topicName = 'Gewähltes Thema';
  let removed = false;
  let parentId: string | null = null;
  const visibility = vi.spyOn(document, 'visibilityState', 'get');
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string) => {
      if (path === '/api/v1/session') {
        reads++;
        return revoked
          ? response(401, { code: 'authentication_required' })
          : response(200, current);
      }
      if (path === '/api/v1/topics')
        return response(200, [
          {
            topic: { id: 'inbox', name: 'Nicht zugeordnet', parentId: null, isSystem: true },
            etag: '"1"',
          },
          {
            topic: { id: 'parent', name: 'Zielordner', parentId: null, isSystem: false },
            etag: '"1"',
          },
          ...(!removed && current.workspaceId === active.workspaceId
            ? [
                {
                  topic: { id: 'selected', name: topicName, parentId, isSystem: false },
                  etag: '"1"',
                },
              ]
            : []),
        ]);
      return response(200, { items: [], nextCursor: null });
    }),
  );
  try {
    render(<AuthApp />);
    fireEvent.click(await screen.findByRole('button', { name: /^Gewähltes Thema$/ }));
    expect(screen.getByRole('heading', { name: 'Gewähltes Thema' })).toBeVisible();
    function returnToTab() {
      visibility.mockReturnValue('hidden');
      fireEvent(document, new Event('visibilitychange'));
      visibility.mockReturnValue('visible');
      fireEvent(document, new Event('visibilitychange'));
    }
    returnToTab();
    await waitFor(() => expect(reads).toBe(2));
    await screen.findByRole('navigation', { name: 'Themen' });
    expect(screen.getByRole('heading', { name: 'Gewähltes Thema' })).toBeVisible();
    topicName = 'Umbenanntes Thema';
    parentId = 'parent';
    returnToTab();
    expect(await screen.findByRole('heading', { name: 'Umbenanntes Thema' })).toBeVisible();
    removed = true;
    returnToTab();
    expect(await screen.findByRole('heading', { name: 'Alle zugeordnet' })).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: /^Zielordner$/ }));
    expect(screen.getByRole('heading', { name: 'Zielordner' })).toBeVisible();
    current = { ...active, workspaceId: 'workspace-2' };
    returnToTab();
    await waitFor(() => expect(reads).toBe(5));
    await screen.findByRole('navigation', { name: 'Themen' });
    expect(screen.getByRole('heading', { name: 'Alle zugeordnet' })).toBeVisible();
    expect(screen.queryByRole('heading', { name: topicName })).not.toBeInTheDocument();
    revoked = true;
    returnToTab();
    expect(await screen.findByRole('heading', { name: 'Anmelden' })).toBeVisible();
    expect(screen.queryByRole('navigation', { name: 'Themen' })).not.toBeInTheDocument();
  } finally {
    visibility.mockRestore();
  }
});

it('meldet mit CSRF an und meldet mit neuem Token ab', async () => {
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (path.endsWith('/login')) return response(200, session);
    if (init?.method === 'DELETE') return response(204);
    return response(401, { code: 'authentication_required', traceId: 't' });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  fireEvent.change(await screen.findByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'password' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Anmelden' }).closest('form')!);
  expect(await screen.findByText(/Workspace wird eingerichtet/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Abmelden' }));
  expect(await screen.findByRole('button', { name: 'Anmelden' })).toBeInTheDocument();
  const writes = fetcher.mock.calls.filter(([, options]) => options?.method !== 'GET');
  expect(writes).toHaveLength(2);
  expect(
    writes.every(
      ([, options]) => (options?.headers as Record<string, string>)['X-CSRF-TOKEN'] === 'csrf',
    ),
  ).toBe(true);
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/antiforgery'))).toHaveLength(
    2,
  );
});

it('verwirft private Anzeige und meldet unbestätigtes Logout', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
      if (init?.method === 'DELETE')
        return response(503, { code: 'dependency_unavailable', traceId: 't' });
      return response(200, session);
    }),
  );
  render(<AuthApp />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abmelden' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Ergebnis kann unklar sein');
  expect(screen.queryByText(/Willkommen, Clara/)).not.toBeInTheDocument();
  expect(screen.getByText(/Abmeldung wurde noch nicht bestätigt/)).toBeInTheDocument();
});

it('hält den Logout trotz Tab-Ereignissen und verspäteten Antworten gesperrt', async () => {
  let notify: (() => void) | undefined;
  class Channel {
    set onmessage(value: (() => void) | null) {
      notify = value ?? undefined;
    }
    postMessage() {}
    close() {
      notify = undefined;
    }
  }
  let finish: (value: Response) => void = () => undefined;
  let loginSucceeds = false;
  let finishLogin: (value: Response) => void = () => undefined;
  let entered: () => void = () => undefined;
  const pending = new Promise<Response>((resolve) => {
    finish = resolve;
  });
  const dispatched = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input).endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (init?.method === 'DELETE') {
      entered();
      return pending;
    }
    if (String(input).endsWith('/login'))
      return loginSucceeds
        ? new Promise<Response>((resolve) => {
            finishLogin = resolve;
          })
        : response(401, { code: 'authentication_required', traceId: 't' });
    return response(200, loginSucceeds ? { ...session, displayName: 'Neue Sitzung' } : session);
  });
  vi.stubGlobal('BroadcastChannel', Channel);
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  fireEvent.click(await screen.findByRole('button', { name: 'Abmelden' }));
  await dispatched;
  act(() => {
    notify?.();
    document.dispatchEvent(new Event('visibilitychange'));
  });
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/session'))).toHaveLength(2);
  await act(async () => finish(response(503, { code: 'dependency_unavailable', traceId: 't' })));
  expect(await screen.findByRole('alert')).toBeInTheDocument();
  act(() => {
    notify?.();
    document.dispatchEvent(new Event('visibilitychange'));
  });
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Erneut anmelden' }));
  expect(screen.getByRole('button', { name: 'Anmelden' })).toBeInTheDocument();
  const notifyTabs = () => {
    notify?.();
    document.dispatchEvent(new Event('visibilitychange'));
  };
  const login = async () => {
    fireEvent.change(screen.getByLabelText('Benutzername'), { target: { value: 'Clara' } });
    fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'password' } });
    await act(async () =>
      fireEvent.submit(screen.getByRole('button', { name: 'Anmelden' }).closest('form')!),
    );
  };
  await act(async () => notifyTabs());
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  await login();
  expect(screen.getByRole('alert')).toHaveTextContent('Anmeldung ist ungültig');
  await act(async () => notifyTabs());
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/session'))).toHaveLength(2);
  loginSucceeds = true;
  await login();
  await act(async () => notifyTabs());
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/session'))).toHaveLength(2);
  await act(async () => finishLogin(response(200, { ...session, displayName: 'Neue Sitzung' })));
  expect(screen.getByText('Willkommen, Neue Sitzung')).toBeInTheDocument();
  await act(async () => notify?.());
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/session'))).toHaveLength(3);
  expect(screen.getByText('Willkommen, Neue Sitzung')).toBeInTheDocument();
});

it('verwirft eine verspätete Anmeldeantwort nach Moduswechsel', async () => {
  let resolveLogin: (value: Response) => void = () => undefined;
  const delayed = new Promise<Response>((resolve) => {
    resolveLogin = resolve;
  });
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
      if (String(input).endsWith('/login')) return delayed;
      return response(401, { code: 'authentication_required', traceId: 't' });
    }),
  );
  render(<AuthApp />);
  fireEvent.change(await screen.findByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'password' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Anmelden' }).closest('form')!);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Anmelden' })).toBeDisabled());
  fireEvent.click(screen.getByRole('button', { name: 'Mit Einladung registrieren' }));
  resolveLogin(response(200, session));
  await waitFor(() => expect(screen.getByLabelText('Einladungstoken')).toBeInTheDocument());
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
});

it('serialisiert automatische Sessionabfragen und eine laufende Anmeldung', async () => {
  let notify: (() => void) | undefined;
  class Channel {
    set onmessage(callback: () => void) {
      notify = callback;
    }
    postMessage() {}
    close() {
      notify = undefined;
    }
  }
  let finishRead!: (value: Response) => void;
  let finishLogin!: (value: Response) => void;
  let reads = 0;
  const fetcher = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (path.endsWith('/login'))
      return new Promise<Response>((resolve) => {
        finishLogin = resolve;
      });
    if (++reads === 1) return response(401, { code: 'authentication_required' });
    return new Promise<Response>((resolve) => {
      finishRead = resolve;
    });
  });
  vi.stubGlobal('BroadcastChannel', Channel);
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  const form = (await screen.findByRole('button', { name: 'Anmelden' })).closest('form')!;
  act(() => {
    notify?.();
    fireEvent.submit(form);
  });
  expect(reads).toBe(2);
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/login'))).toHaveLength(0);
  await act(async () => finishRead(response(401, { code: 'authentication_required' })));

  fireEvent.change(screen.getByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'password' } });
  const currentForm = screen.getByRole('button', { name: 'Anmelden' }).closest('form')!;
  await act(async () => {
    fireEvent.submit(currentForm);
    fireEvent.submit(currentForm);
  });
  act(() => {
    notify?.();
    document.dispatchEvent(new Event('visibilitychange'));
  });
  expect(reads).toBe(2);
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/login'))).toHaveLength(1);
  await act(async () => finishLogin(response(200, session)));
  expect(reads).toBe(3);
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  await act(async () =>
    finishRead(response(200, { ...session, userId: 'new', displayName: 'Neue Sitzung' })),
  );
  expect(screen.getByText('Willkommen, Neue Sitzung')).toBeInTheDocument();
});

it('verwirft beim Wechsel zwischen zwei Administratoren Token und alte Listenanfragen', async () => {
  let notify: (() => void) | undefined;
  class Channel {
    set onmessage(callback: () => void) {
      notify = callback;
    }
    postMessage() {}
    close() {
      notify = undefined;
    }
  }
  let switched = false;
  let finishUsers!: (value: Response) => void;
  let oldSignal: AbortSignal | null | undefined;
  vi.stubGlobal('BroadcastChannel', Channel);
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith('/session'))
        return response(200, {
          ...session,
          roles: ['ADMINISTRATOR'],
          userId: switched ? 'b' : 'a',
          displayName: switched ? 'Berta' : 'Anna',
        });
      if (path.endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
      if (init?.method === 'POST') return response(201, { id: 'invite', token: 'einmalig' });
      if (path.endsWith('/users') && !switched) {
        oldSignal = init?.signal;
        return new Promise<Response>((resolve) => {
          finishUsers = resolve;
        });
      }
      return response(200, { items: [], nextCursor: null });
    }),
  );
  render(<AuthApp />);
  fireEvent.click(await screen.findByRole('button', { name: 'Einladung erstellen' }));
  expect(await screen.findByText('einmalig')).toBeInTheDocument();
  switched = true;
  await act(async () => notify?.());
  expect(screen.getByText('Willkommen, Berta')).toBeInTheDocument();
  expect(oldSignal?.aborted).toBe(true);
  await act(async () =>
    finishUsers(
      response(200, { items: [{ id: 'old', userName: 'Alter Benutzer' }], nextCursor: null }),
    ),
  );
  expect(screen.queryByText('einmalig')).not.toBeInTheDocument();
  expect(screen.queryByText(/Alter Benutzer/)).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Administration' })).toBeInTheDocument();
});

it('entfernt das Passwort nach einer fehlgeschlagenen Anmeldung', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) =>
      String(input).endsWith('/antiforgery')
        ? response(200, { requestToken: 'csrf' })
        : response(401, { code: 'authentication_required', traceId: 't' }),
    ),
  );
  render(<AuthApp />);
  fireEvent.change(await screen.findByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'sensitive' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Anmelden' }).closest('form')!);
  expect(await screen.findByRole('alert')).toBeInTheDocument();
  expect(screen.getByLabelText('Passwort')).toHaveValue('');
});

it('registriert mit Einladung ohne eine Sitzung zu erstellen', async () => {
  const fetcher = vi.fn(async (input: RequestInfo | URL) => {
    if (String(input).endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (String(input).endsWith('/registration'))
      return response(201, { userId: 'u', workspaceStatus: 'pending' });
    return response(401, { code: 'authentication_required', traceId: 't' });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  fireEvent.click(await screen.findByRole('button', { name: 'Mit Einladung registrieren' }));
  expect(screen.getByLabelText('Einladungstoken')).toHaveFocus();
  fireEvent.change(screen.getByLabelText('Einladungstoken'), { target: { value: 'invitation' } });
  fireEvent.change(screen.getByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'strongpassword' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Registrieren' }).closest('form')!);
  expect(await screen.findByText(/Registrierung abgeschlossen/)).toBeInTheDocument();
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
  expect(fetcher.mock.calls.some(([path]) => String(path).endsWith('/session/login'))).toBe(false);
});

it('meldet eine abgelehnte Registrierung verständlich und entfernt das Passwort', async () => {
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (path.endsWith('/registration')) {
      expect(JSON.parse(String(init?.body)).token).toBe('A'.repeat(43));
      return response(400, { code: 'validation_failed', traceId: 't' });
    }
    return response(401, { code: 'authentication_required', traceId: 't' });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  fireEvent.click(await screen.findByRole('button', { name: 'Mit Einladung registrieren' }));
  expect(screen.getByLabelText('Passwort')).toHaveAttribute('minlength', '12');
  fireEvent.change(screen.getByLabelText('Einladungstoken'), {
    target: { value: ` ${'A'.repeat(43)} ` },
  });
  fireEvent.change(screen.getByLabelText('Benutzername'), { target: { value: 'Claus_Demo' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'strongpassword' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Registrieren' }).closest('form')!);
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Prüfe das vollständige Einladungstoken',
  );
  expect(screen.getByLabelText('Passwort')).toHaveValue('');
  expect(
    fetcher.mock.calls.filter(([path]) => String(path).endsWith('/registration')),
  ).toHaveLength(1);
});

it.each([
  ['active', 'Dein Workspace ist aktiv'],
  ['suspended', 'Dein Workspace ist gesperrt'],
])('zeigt den Workspace-Zustand %s', async (status, text) => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => response(200, { ...session, workspaceStatus: status })),
  );
  render(<AuthApp />);
  expect(await screen.findByText(new RegExp(text))).toBeInTheDocument();
});

it('verwirft den privaten Zustand bei einer Änderung in einem anderen Tab', async () => {
  let notify: (() => void) | undefined;
  class FakeChannel {
    set onmessage(value: (() => void) | null) {
      notify = value ?? undefined;
    }
    postMessage() {
      /* This test sends a message from the other tab. */
    }
    close() {
      notify = undefined;
    }
  }
  let valid = true;
  vi.stubGlobal('BroadcastChannel', FakeChannel);
  vi.stubGlobal(
    'fetch',
    vi.fn(async () =>
      valid
        ? response(200, session)
        : response(401, { code: 'authentication_required', traceId: 't' }),
    ),
  );
  render(<AuthApp />);
  expect(await screen.findByText(/Willkommen, Clara/)).toBeInTheDocument();
  valid = false;
  notify?.();
  expect(await screen.findByRole('button', { name: 'Anmelden' })).toBeInTheDocument();
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
});

it('zeigt einem AdminOnly-Konto die Verwaltung ohne privaten Workspace', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).includes('/admin/')) return response(200, { items: [], nextCursor: null });
      return response(200, {
        ...session,
        roles: ['ADMINISTRATOR'],
        workspaceId: null,
        workspaceStatus: null,
      });
    }),
  );
  render(<AuthApp />);
  expect(await screen.findByText(/keinen privaten Workspace/)).toBeInTheDocument();
  expect(await screen.findByRole('heading', { name: 'Administration' })).toBeInTheDocument();
});

it('entfernt administrative Daten bei Rollenentzug und liest die Session neu', async () => {
  let rejectAdmin: (value: Response) => void = () => undefined;
  let releaseUsers: (value: Response) => void = () => undefined;
  const forbidden = new Promise<Response>((resolve) => {
    rejectAdmin = resolve;
  });
  const users = new Promise<Response>((resolve) => {
    releaseUsers = resolve;
  });
  let sessionReads = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith('/session')) {
        sessionReads++;
        return response(200, { ...session, roles: sessionReads === 1 ? ['ADMINISTRATOR'] : [] });
      }
      if (path.endsWith('/admin/users')) return users;
      if (path.endsWith('/admin/invitations')) return forbidden;
      return response(200, { items: [], nextCursor: null });
    }),
  );
  render(<AuthApp />);
  expect(await screen.findByRole('heading', { name: 'Administration' })).toBeInTheDocument();
  await act(async () => rejectAdmin(response(403, { code: 'forbidden', traceId: 't' })));
  await waitFor(() => expect(sessionReads).toBe(2));
  await act(async () =>
    releaseUsers(response(200, { items: [{ id: 'x', userName: 'Geheim' }], nextCursor: null })),
  );
  expect(screen.queryByRole('heading', { name: 'Administration' })).not.toBeInTheDocument();
  expect(screen.queryByText('Geheim')).not.toBeInTheDocument();
  expect(screen.getByText(/Willkommen, Clara/)).toBeInTheDocument();
});

it('zeigt einen Backend-Verbindungsfehler ohne Schreibwiederholung und entfernt das Passwort', async () => {
  const fetcher = vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return response(200, { requestToken: 'csrf' });
    if (path.endsWith('/login')) throw new TypeError('Network error');
    return response(401, { code: 'authentication_required', traceId: 't' });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AuthApp />);
  fireEvent.change(await screen.findByLabelText('Benutzername'), { target: { value: 'Clara' } });
  fireEvent.change(screen.getByLabelText('Passwort'), { target: { value: 'sensitive' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Anmelden' }).closest('form')!);
  expect(await screen.findByRole('alert')).toHaveTextContent('Verbindung zum MiniDocs-Backend');
  expect(screen.getByLabelText('Passwort')).toHaveValue('');
  expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/login'))).toHaveLength(1);
  expect(screen.queryByText(/Willkommen/)).not.toBeInTheDocument();
});
