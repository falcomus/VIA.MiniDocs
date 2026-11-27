import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const frontend = fileURLToPath(new URL('../', import.meta.url));
const root = fileURLToPath(new URL('../../', import.meta.url));
const modes = process.argv.slice(2);
if (modes.length > 1 || (modes.length && !['--functional', '--candidates'].includes(modes[0])))
  throw new Error('Use test:e2e, test:e2e -- --functional or test:e2e -- --candidates');
const env = {
  ...process.env,
  MINIDOCS_E2E_FRONTEND: frontend,
  MINIDOCS_E2E_FUNCTIONAL: modes.includes('--functional') ? '1' : '0',
  MINIDOCS_E2E_CANDIDATES: modes.includes('--candidates') ? '1' : '0',
};
for (const [command, args, cwd] of [
  [
    process.execPath,
    ['node_modules/typescript/bin/tsc', '--noEmit', '-p', 'e2e/tsconfig.json'],
    frontend,
  ],
  [process.execPath, ['node_modules/vite/bin/vite.js', 'build'], frontend],
  [
    'dotnet',
    [
      'test',
      'backend/tests/VIA.MiniDocs.Tests',
      '--no-restore',
      '-p:BrowserRegression=true',
      '-p:BaseOutputPath=bin/browser-regression/',
      '--filter',
      'FullyQualifiedName=VIA.MiniDocs.Tests.SessionHostTests.BrowserRegression',
      '--logger',
      'console;verbosity=normal',
    ],
    root,
  ],
]) {
  const result = spawnSync(command, args, { cwd, env, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
