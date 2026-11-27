import type { components, paths } from './fixture.generated.js';
const valid: components['schemas']['ContractFixture'] = { note: null, state: 'Ready' };
// @ts-expect-error unknown enum values are forbidden
const invalid: components['schemas']['ContractFixture'] = { note: null, state: 'Other' };
// @ts-expect-error note must be present
const missing: components['schemas']['ContractFixture'] = { state: 'Ready' };
const header: paths['/api/v1/_contract-fixtures/binary']['get']['parameters']['header'] = { 'If-Range': '"etag"', Range: 'bytes=0-1' };
type ErrorResponses = paths['/api/v1/_contract-fixtures/error']['get']['responses'];
const problem: ErrorResponses[500]['content']['application/problem+json'] = { code: 'internal_error', traceId: 'trace' };
// @ts-expect-error both problem identifiers are mandatory strings
const missingProblem: ErrorResponses[500]['content']['application/problem+json'] = {};
const invitation: paths['/api/v1/admin/invitations']['post']['requestBody']['content']['application/json'] = {};
// @ts-expect-error the error fixture never returns HTTP 200
type UnexpectedSuccess = ErrorResponses[200];
type RecoveryLimited = paths['/api/v1/admin/workspaces/{id}/recovery']['post']['responses'][429];
const recoveryLimit: RecoveryLimited['headers'] = { 'Cache-Control': 'no-store', 'Retry-After': 60 };
// @ts-expect-error a rate-limited response must declare Retry-After
const missingRetry: RecoveryLimited['headers'] = { 'Cache-Control': 'no-store' };
type SessionFailure = paths['/api/v1/session']['get']['responses'][500];
const sessionFailure: SessionFailure['content']['application/problem+json'] = problem;
// @ts-expect-error internal errors on session routes require Cache-Control
const missingCacheControl: SessionFailure['headers'] = {};
void [valid, invalid, missing, header, problem, missingProblem, invitation, recoveryLimit, missingRetry, sessionFailure, missingCacheControl];
