import type { components, paths } from '../../../../backend/contracts/fixture.generated';

export type Session = components['schemas']['Session'];
export type AdminUser = components['schemas']['AdminUser'];
export type Invite = components['schemas']['Invite'];
export type Provisioning = components['schemas']['Provisioning'];
export type Problem = components['schemas']['ProblemDetails'];
export type Login =
  paths['/api/v1/session/login']['post']['requestBody']['content']['application/json'];
export type Registration =
  paths['/api/v1/registration']['post']['requestBody']['content']['application/json'];
export type InviteCreate =
  paths['/api/v1/admin/invitations']['post']['requestBody']['content']['application/json'];
export type Recovery =
  paths['/api/v1/admin/workspaces/{id}/recovery']['post']['requestBody']['content']['application/json'];
export type TechnicalInvitation =
  paths['/api/v1/admin/workspaces/{id}/technical-invitation']['post']['requestBody']['content']['application/json'];
export type UserPatch =
  paths['/api/v1/admin/users/{id}']['patch']['requestBody']['content']['application/json'];

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
  ) {
    super(code);
  }
}

export class ApiClient {
  private csrf: string | null = null;
  private tokenRequest: Promise<string> | null = null;
  private controller = new AbortController();

  reset() {
    this.controller.abort();
    this.controller = new AbortController();
    this.csrf = null;
    this.tokenRequest = null;
  }

  async request<T>(
    path: string,
    method = 'GET',
    body?: unknown,
    headers?: Record<string, string>,
    scope?: AbortSignal,
  ): Promise<{ data: T; response: Response }> {
    if (!path.startsWith('/api/v1/') || path.startsWith('//'))
      throw new Error('Ungültiger API-Pfad');
    const signal = scope
      ? AbortSignal.any([this.controller.signal, scope])
      : this.controller.signal;
    if (method !== 'GET' && !this.csrf) {
      const pending =
        this.tokenRequest ??
        this.request<components['schemas']['AntiForgery']>('/api/v1/antiforgery').then(
          (result) => result.data.requestToken,
        );
      this.tokenRequest = pending;
      let token: string;
      try {
        token = await pending;
      } finally {
        if (this.tokenRequest === pending) this.tokenRequest = null;
      }
      if (signal.aborted) throw new DOMException('Abgebrochen', 'AbortError');
      this.csrf = token;
    }
    const requestHeaders: Record<string, string> = { ...headers };
    if (method !== 'GET') requestHeaders['X-CSRF-TOKEN'] = this.csrf!;
    if (body !== undefined) requestHeaders['Content-Type'] = 'application/json';
    const response = await fetch(path, {
      method,
      credentials: 'same-origin',
      cache: 'no-store',
      redirect: 'error',
      signal,
      headers: requestHeaders,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    if (signal.aborted) throw new DOMException('Abgebrochen', 'AbortError');
    if (!response.ok) {
      let code = 'request_failed';
      try {
        code = ((await response.json()) as Problem).code || code;
      } catch {
        /* Non-JSON failure. */
      }
      if (signal.aborted) throw new DOMException('Abgebrochen', 'AbortError');
      if (
        response.status === 403 &&
        code === 'antiforgery_invalid' &&
        requestHeaders['X-CSRF-TOKEN'] === this.csrf
      )
        this.csrf = null;
      throw new ApiError(response.status, code);
    }
    const data = response.status === 204 ? (undefined as T) : ((await response.json()) as T);
    if (signal.aborted) throw new DOMException('Abgebrochen', 'AbortError');
    return { data, response };
  }
}

export function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError))
    return 'Die Verbindung ist fehlgeschlagen. Bitte prüfe den Status, bevor du erneut speicherst.';
  switch (error.status) {
    case 401:
      return 'Die Anmeldung ist ungültig oder die Sitzung abgelaufen.';
    case 403:
      return 'Die Berechtigung oder der Sicherheitstoken ist nicht mehr gültig.';
    case 409:
      return 'Der Zustand hat sich geändert oder der Vorgang muss geprüft werden.';
    case 410:
      return 'Die Einladung ist nicht mehr gültig.';
    case 412:
      return 'Der Eintrag wurde zwischenzeitlich geändert. Bitte lade die Liste erneut.';
    case 423:
      return 'Das Konto ist vorübergehend gesperrt.';
    case 428:
      return 'Die Versionsangabe fehlt. Bitte lade die Liste erneut.';
    case 429:
      return 'Zu viele Anfragen. Bitte versuche es später erneut.';
    case 503:
      return 'Der Dienst ist nicht verfügbar. Das Ergebnis kann unklar sein.';
    default:
      return 'Die Anfrage konnte nicht verarbeitet werden.';
  }
}
