import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { AdminPanel } from './AdminPanel';
import { ApiClient } from './api';

afterEach(() => vi.unstubAllGlobals());

it('sendet ETag und zeigt Versionskonflikte ohne Wiederholung', async () => {
  const user = {
    id: 'user-id',
    userName: 'Clara',
    isDisabled: false,
    accountKind: 'regular',
    roles: [],
    etag: '"v1"',
  };
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
    if (init?.method === 'PATCH') return result(412, { code: 'version_mismatch', traceId: 't' });
    return result(200, { items: path.endsWith('/users') ? [user] : [], nextCursor: null });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Deaktivieren' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('zwischenzeitlich geändert');
  const writes = fetcher.mock.calls.filter(([, init]) => init?.method === 'PATCH');
  expect(writes).toHaveLength(1);
  expect((writes[0][1]?.headers as Record<string, string>)['If-Match']).toBe('"v1"');
  fireEvent.click(screen.getByRole('button', { name: 'Benutzerliste aktualisieren' }));
  await waitFor(() =>
    expect(fetcher.mock.calls.filter(([path]) => String(path).endsWith('/users'))).toHaveLength(2),
  );
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === 'PATCH')).toHaveLength(1);
});

it('zeigt einmalige Einladung und löscht sie beim Schließen', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
      if (init?.method === 'POST') return result(201, { id: 'i', token: 'einmalig' });
      return result(200, { items: [], nextCursor: null });
    }),
  );
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Einladung erstellen' }));
  expect(await screen.findByText('einmalig')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Schließen' }));
  await waitFor(() => expect(screen.queryByText('einmalig')).not.toBeInTheDocument());
});

it('kopiert das vollständige einmalige Token und entfernt die Anzeige beim Schließen', async () => {
  const token = 'A'.repeat(43);
  const writeText = vi.fn(async () => undefined);
  const prior = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
  try {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        if (String(input).endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
        if (init?.method === 'POST') return result(201, { id: 'i', token });
        return result(200, { items: [], nextCursor: null });
      }),
    );
    render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Einladung erstellen' }));
    expect(await screen.findByText(token)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Token kopieren' }));
    expect(await screen.findByText('Token kopiert.')).toBeInTheDocument();
    expect(writeText).toHaveBeenCalledTimes(1);
    expect(writeText).toHaveBeenCalledWith(token);
    fireEvent.click(screen.getByRole('button', { name: 'Schließen' }));
    expect(screen.queryByText(token)).not.toBeInTheDocument();
    expect(screen.queryByText('Token kopiert.')).not.toBeInTheDocument();
  } finally {
    if (prior) Object.defineProperty(navigator, 'clipboard', prior);
    else Reflect.deleteProperty(navigator, 'clipboard');
  }
});

it('bestätigt Recovery einmalig und zeigt den ausstehenden Abschluss', async () => {
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
    if (path.endsWith('/recovery') && init?.method === 'POST') {
      const accepted = result(202, { workspaceId: 'w', state: 'recovery_required' });
      accepted.headers.set('Location', '/api/v1/admin/provisioning?workspaceId=w');
      accepted.headers.set('Retry-After', '5');
      return accepted;
    }
    return result(200, {
      items: path.endsWith('/provisioning')
        ? [{ workspaceId: 'w', state: 'recovery_required' }]
        : [],
      nextCursor: null,
    });
  });
  vi.stubGlobal('fetch', fetcher);
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Überprüfung bestätigen' }));
  expect(await screen.findByText(/Abschluss steht noch aus/)).toBeInTheDocument();
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1);
});

it('meldet Rollenentzug, verwirft laufende Antworten und unterscheidet CSRF-Fehler', async () => {
  let release: (value: Response) => void = () => undefined;
  const delayed = new Promise<Response>((resolve) => {
    release = resolve;
  });
  const revoked = vi.fn();
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith('/users')) return delayed;
      if (path.endsWith('/invitations')) return result(403, { code: 'forbidden', traceId: 't' });
      return result(200, { items: [], nextCursor: null });
    }),
  );
  const view = render(
    <AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} onForbidden={revoked} />,
  );
  await waitFor(() => expect(revoked).toHaveBeenCalledTimes(1));
  view.unmount();
  await act(async () =>
    release(result(200, { items: [{ id: 'x', userName: 'spät' }], nextCursor: null })),
  );
  expect(screen.queryByText('spät')).not.toBeInTheDocument();
});

it('unterbricht eine laufende Admin-Aktion bei Rollenentzug', async () => {
  let finishWrite: (value: Response) => void = () => undefined;
  let deny: (value: Response) => void = () => undefined;
  const write = new Promise<Response>((resolve) => {
    finishWrite = resolve;
  });
  const pendingList = new Promise<Response>((resolve) => {
    deny = resolve;
  });
  const revoked = vi.fn();
  const fetcher = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
    if (init?.method === 'POST') return write;
    if (path.endsWith('/users')) return pendingList;
    return result(200, { items: [], nextCursor: null });
  });
  vi.stubGlobal('fetch', fetcher);
  const view = render(
    <AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} onForbidden={revoked} />,
  );
  fireEvent.click(screen.getByRole('button', { name: 'Einladung erstellen' }));
  await waitFor(() =>
    expect(fetcher.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true),
  );
  await act(async () => deny(result(403, { code: 'forbidden', traceId: 't' })));
  expect(revoked).toHaveBeenCalledTimes(1);
  view.unmount();
  await act(async () => finishWrite(result(201, { id: 'i', token: 'spätes-token' })));
  expect(screen.queryByText('spätes-token')).not.toBeInTheDocument();
});

it('verwirft einen technischen Token beim Abbruch und verlangt die Bestätigung', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => result(200, { items: [], nextCursor: null })),
  );
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Workspace-ID'), { target: { value: 'w' } });
  fireEvent.change(screen.getByLabelText('Technische Einladung'), { target: { value: 'secret' } });
  expect(screen.getByRole('button', { name: 'Einladung hinterlegen' })).toBeDisabled();
  fireEvent.click(screen.getByLabelText(/Ich bestätige die Übergabe/));
  expect(screen.getByRole('button', { name: 'Einladung hinterlegen' })).toBeEnabled();
  fireEvent.click(screen.getByRole('button', { name: 'Eingabe verwerfen' }));
  expect(screen.getByLabelText('Technische Einladung')).toHaveValue('');
  expect(screen.getByRole('button', { name: 'Einladung hinterlegen' })).toBeDisabled();
});

it('fragt mehrere angenommene Workspaces unabhängig nach ihrer Wartezeit ab', async () => {
  const getPaths: string[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
      if (init?.method === 'POST') {
        const workspace = path.includes('/w1/') ? 'w1' : 'w2';
        const accepted = result(202, { workspaceId: workspace, state: 'recovery_required' });
        accepted.headers.set('Location', `/api/v1/admin/provisioning?workspaceId=${workspace}`);
        accepted.headers.set('Retry-After', '1');
        return accepted;
      }
      if (path.includes('workspaceId=')) getPaths.push(path);
      return result(200, {
        items: path.endsWith('/provisioning')
          ? ['w1', 'w2'].map((workspaceId) => ({ workspaceId, state: 'recovery_required' }))
          : [],
        nextCursor: null,
      });
    }),
  );
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  const buttons = await screen.findAllByRole('button', { name: 'Überprüfung bestätigen' });
  vi.useFakeTimers();
  try {
    await act(async () => fireEvent.click(buttons[0]));
    await act(async () => fireEvent.click(buttons[1]));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(getPaths).toEqual(
      expect.arrayContaining([
        '/api/v1/admin/provisioning?workspaceId=w1',
        '/api/v1/admin/provisioning?workspaceId=w2',
      ]),
    );
  } finally {
    vi.useRealTimers();
  }
});

it('verwirft überholte Statusantworten desselben Workspaces und überholte Listen', async () => {
  const pending: ((value: Response) => void)[] = [];
  const listPending: ((value: Response) => void)[] = [];
  const page = (state: string) =>
    result(200, { items: [{ workspaceId: 'w', state }], nextCursor: null });
  let lists = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
      if (init?.method === 'POST') {
        const accepted = result(202, { workspaceId: 'w', state: 'recovery_required' });
        accepted.headers.set('Location', '/api/v1/admin/provisioning?workspaceId=w');
        accepted.headers.set('Retry-After', '1');
        return accepted;
      }
      if (path.includes('workspaceId='))
        return new Promise<Response>((resolve) => {
          pending.push(resolve);
        });
      if (path.endsWith('/provisioning')) {
        if (++lists === 1) return page('recovery_required');
        return new Promise<Response>((resolve) => {
          listPending.push(resolve);
        });
      }
      return result(200, { items: [], nextCursor: null });
    }),
  );
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  await screen.findByRole('button', { name: 'Überprüfung bestätigen' });
  vi.useFakeTimers();
  try {
    const confirm = async () => {
      await act(async () =>
        fireEvent.click(screen.getByRole('button', { name: 'Überprüfung bestätigen' })),
      );
      await act(async () => vi.advanceTimersByTimeAsync(1000));
    };
    await confirm();
    await confirm();
    expect(pending).toHaveLength(2);
    await act(async () => pending[1](page('recovery_required')));
    await act(async () => pending[0](page('registering')));
    expect(screen.getByText('w · Überprüfung erforderlich')).toBeInTheDocument();
    expect(screen.queryByText('w · Registrierung läuft')).not.toBeInTheDocument();

    await confirm();
    await act(async () =>
      fireEvent.click(screen.getByRole('button', { name: 'Status aktualisieren' })),
    );
    await act(async () => listPending[0](page('recovery_required')));
    await act(async () => pending[2](page('registering')));
    expect(screen.getByText('w · Überprüfung erforderlich')).toBeInTheDocument();

    await act(async () =>
      fireEvent.click(screen.getByRole('button', { name: 'Status aktualisieren' })),
    );
    await confirm();
    await act(async () => pending[3](page('active')));
    await act(async () => listPending[1](page('registering')));
    expect(screen.getByText('w · Technisch aktiv')).toBeInTheDocument();
  } finally {
    vi.useRealTimers();
  }
});

it('verwirft einen verspäteten POST-Status nach einer neueren Listenbeobachtung', async () => {
  let finishWrite!: (value: Response) => void;
  let reads = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith('/antiforgery')) return result(200, { requestToken: 'csrf' });
      if (init?.method === 'POST')
        return new Promise<Response>((resolve) => {
          finishWrite = resolve;
        });
      const items = path.endsWith('/provisioning')
        ? [{ workspaceId: 'w', state: ++reads === 1 ? 'recovery_required' : 'active' }]
        : [];
      return result(200, { items, nextCursor: null });
    }),
  );
  render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
  const verify = await screen.findByRole('button', { name: 'Überprüfung bestätigen' });
  await act(async () => fireEvent.click(verify));
  fireEvent.click(screen.getByRole('button', { name: 'Status aktualisieren' }));
  expect(await screen.findByText('w · Technisch aktiv')).toBeInTheDocument();
  await act(async () => finishWrite(result(202, { workspaceId: 'w', state: 'recovery_required' })));
  expect(screen.getByText('w · Technisch aktiv')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Überprüfung bestätigen' })).not.toBeInTheDocument();
});

it.each([
  ['users', 'Weitere Benutzer', { userName: 'Benutzer', isDisabled: false }],
  ['invitations', 'Weitere Einladungen', { expiresAt: '2027-01-01T00:00:00Z' }],
] as const)(
  'behält bei überholten Seitenantworten für %s die jüngste Liste',
  async (path, label, fields) => {
    const pending: ((value: Response) => void)[] = [];
    const page = (id: string, nextCursor: string | null) =>
      result(200, {
        items: [{ ...fields, id, ...('userName' in fields ? { userName: id } : {}) }],
        nextCursor,
      });
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        if (url.endsWith(`/${path}?cursor=next`))
          return new Promise<Response>((resolve) => {
            pending.push(resolve);
          });
        if (url.endsWith(`/${path}`)) return page('Ausgangseintrag', 'next');
        return result(200, { items: [], nextCursor: null });
      }),
    );
    render(<AdminPanel api={new ApiClient()} onUnauthorized={vi.fn()} />);
    const next = await screen.findByRole('button', { name: label });
    fireEvent.click(next);
    fireEvent.click(next);
    expect(pending).toHaveLength(2);
    await act(async () => pending[1](page('Neuester Eintrag', null)));
    await act(async () => pending[0](page('Überholter Eintrag', 'next')));
    expect(screen.getByText('Neuester Eintrag')).toBeInTheDocument();
    expect(screen.queryByText('Überholter Eintrag')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
  },
);

function result(status: number, body: unknown) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    headers: new Headers(),
  } as Response;
}
