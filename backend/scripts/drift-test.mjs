import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const temp = await mkdtemp(join(tmpdir(), 'minidocs-drift-'));
try {
  const baseline = join(temp, 'outdated.ts');
  await writeFile(baseline, '// deliberately outdated contract\n');
  const result = spawnSync(process.execPath, [fileURLToPath(new URL('./contracts.mjs', import.meta.url)), 'check'], {env: {...process.env, CONTRACT_BASELINE: baseline}, encoding: 'utf8'});
  if (result.status !== 1 || !result.stderr.includes('Generated contract drift detected')) {
    console.error(result.stdout, result.stderr);
    throw new Error('Drift detection did not reject the outdated contract');
  }
  console.log('Negative drift test passed: stale contract rejected');
} finally { await rm(temp, {recursive: true, force: true}); }
