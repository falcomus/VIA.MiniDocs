import { afterEach, expect, it, vi } from 'vitest';
import { ApiClient, ApiError, errorMessage } from './api';

afterEach(() => vi.unstubAllGlobals());

it.each([401, 403, 409, 410, 412, 423, 428, 429, 503])(
  'meldet HTTP %i ohne automatische Schreibwiederholung',
  async (status) => {
    const fetcher = vi.fn(async (path: RequestInfo | URL) => {
      if (String(path).endsWith('antiforgery'))
        return { ok: true, status: 200, json: async () => ({ requestToken: 'csrf' }) } as Response;
      return {
        ok: false,
        status,
        json: async () => ({ code: 'failure', traceId: 't' }),
      } as Response;
    });
    vi.stubGlobal('fetch', fetcher);
    const api = new ApiClient();
    let caught: unknown;
    try {
      await api.request('/api/v1/registration', 'POST', {});
    } catch (error) {
      caught = error;
    }
    expect(caught).toBeInstanceOf(ApiError);
    expect(errorMessage(caught)).not.toBe('Die Anfrage konnte nicht verarbeitet werden.');
    expect(fetcher).toHaveBeenCalledTimes(2);
  },
);

it('verwirft eine Antwort nach Abbruch auch bei nicht kooperativem Transport', async () => {
  let complete: (result: Response) => void = () => undefined;
  const pending = new Promise<Response>((resolve) => {
    complete = resolve;
  });
  vi.stubGlobal(
    'fetch',
    vi.fn(() => pending),
  );
  const api = new ApiClient();
  const result = api.request('/api/v1/session');
  api.reset();
  complete({ ok: true, status: 200, json: async () => ({}) } as Response);
  await expect(result).rejects.toMatchObject({ name: 'AbortError' });
});

it('erneuert nach CSRF-Fehler erst bei der nächsten bewussten Aktion', async () => {
  let tokens = 0;
  let writes = 0;
  const headers: string[] = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: RequestInfo | URL, init?: RequestInit) => {
      if (String(path).endsWith('/antiforgery')) {
        tokens++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ requestToken: `token${tokens}` }),
        } as Response;
      }
      writes++;
      headers.push((init?.headers as Record<string, string>)['X-CSRF-TOKEN']);
      return writes === 1
        ? ({
            ok: false,
            status: 403,
            json: async () => ({ code: 'antiforgery_invalid', traceId: 't' }),
          } as Response)
        : ({ ok: true, status: 204 } as Response);
    }),
  );
  const api = new ApiClient();
  await expect(api.request('/api/v1/session', 'DELETE')).rejects.toMatchObject({
    code: 'antiforgery_invalid',
  });
  expect(writes).toBe(1);
  await api.request('/api/v1/session', 'DELETE');
  expect(headers).toEqual(['token1', 'token2']);
});

it('verwirft einen Fehler, dessen Body nach dem Identitätswechsel eintrifft', async () => {
  let complete: (value: unknown) => void = () => undefined;
  let entered: () => void = () => undefined;
  const body = new Promise((resolve) => {
    complete = resolve;
  });
  const started = new Promise<void>((resolve) => {
    entered = resolve;
  });
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        ({
          ok: false,
          status: 403,
          json: () => {
            entered();
            return body;
          },
        }) as Response,
    ),
  );
  const api = new ApiClient();
  const request = api.request('/api/v1/session');
  await started;
  api.reset();
  complete({ code: 'forbidden', traceId: 't' });
  await expect(request).rejects.toMatchObject({ name: 'AbortError' });
});

it('verwirft eine alte Tokenantwort nach dem Wechsel des Identitätskontexts', async () => {
  let finish: (value: Response) => void = () => undefined;
  const pending = new Promise<Response>((resolve) => {
    finish = resolve;
  });
  let calls = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      calls++;
      if (calls === 1) return pending;
      if (calls === 2)
        return { ok: true, status: 200, json: async () => ({ requestToken: 'new' }) } as Response;
      return { ok: true, status: 204 } as Response;
    }),
  );
  const api = new ApiClient();
  const old = api.request('/api/v1/session', 'DELETE');
  api.reset();
  await api.request('/api/v1/session', 'DELETE');
  finish({ ok: true, status: 200, json: async () => ({ requestToken: 'old' }) } as Response);
  await expect(old).rejects.toMatchObject({ name: 'AbortError' });
  expect(calls).toBe(3);
});

it('teilt eine gleichzeitige Tokenanforderung zwischen bewussten Aktionen', async () => {
  let finish: (value: Response) => void = () => undefined;
  const token = new Promise<Response>((resolve) => {
    finish = resolve;
  });
  const fetcher = vi.fn(async (path: RequestInfo | URL) =>
    String(path).endsWith('/antiforgery') ? token : ({ ok: true, status: 204 } as Response),
  );
  vi.stubGlobal('fetch', fetcher);
  const api = new ApiClient();
  const first = api.request('/api/v1/session', 'DELETE');
  const second = api.request('/api/v1/session', 'DELETE');
  expect(fetcher).toHaveBeenCalledTimes(1);
  finish({ ok: true, status: 200, json: async () => ({ requestToken: 'shared' }) } as Response);
  await Promise.all([first, second]);
  expect(fetcher).toHaveBeenCalledTimes(3);
});
