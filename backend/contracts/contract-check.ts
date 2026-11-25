import type { components, paths } from './fixture.generated.js';
const valid: components['schemas']['ContractFixture'] = { note: null, state: 'Ready' };
// @ts-expect-error unknown enum values are forbidden
const invalid: components['schemas']['ContractFixture'] = { note: null, state: 'Other' };
// @ts-expect-error note must be present
const missing: components['schemas']['ContractFixture'] = { state: 'Ready' };
const header: paths['/api/v1/_contract-fixtures/binary']['get']['parameters']['header'] = { 'If-Range': '"etag"', Range: 'bytes=0-1' };
type ErrorResponses = paths['/api/v1/_contract-fixtures/error']['get']['responses'];
const problem: ErrorResponses[500]['content']['application/problem+json'] = {};
// @ts-expect-error the error fixture never returns HTTP 200
type UnexpectedSuccess = ErrorResponses[200];
void [valid, invalid, missing, header, problem];
