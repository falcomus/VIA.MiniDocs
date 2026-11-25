import { spawnSync } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import openapiTS, { astToString } from 'openapi-typescript';

const root = fileURLToPath(new URL('../', import.meta.url));
const mode = process.argv[2];
if (!['generate', 'check'].includes(mode)) throw new Error('Use generate or check');
const build = spawnSync('dotnet', ['build', 'src/VIA.MiniDocs.Api/VIA.MiniDocs.Api.csproj', '--no-restore', '-t:Rebuild'], {cwd: root, env: {...process.env, ASPNETCORE_ENVIRONMENT: 'ContractTests'}, stdio: 'inherit'});
if (build.status !== 0) process.exit(build.status ?? 1);
const source = new URL('../src/VIA.MiniDocs.Api/obj/VIA.MiniDocs.Api.json', import.meta.url);
const target = new URL('../contracts/fixture.generated.ts', import.meta.url);
const generated = astToString(await openapiTS(source));
if (mode === 'generate') await writeFile(target, generated);
else if (generated !== await readFile(process.env.CONTRACT_BASELINE ?? target, 'utf8')) {
  console.error('Generated contract drift detected. Run npm run generate.');
  process.exit(1);
}
console.log('OpenAPI TypeScript contract ' + (mode === 'check' ? 'matches' : 'generated'));
