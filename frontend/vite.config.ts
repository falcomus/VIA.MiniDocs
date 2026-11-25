import { readFileSync, createReadStream, existsSync, statSync, lstatSync } from 'node:fs';
import { resolve, basename } from 'node:path';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vitest/config';

type DemoRecord = { dateiName: string; [key: string]: unknown };

function demoData(): Plugin {
  const root = resolve(process.cwd(), '../demo-data/ui-demo-30');
  const virtualId = '\0virtual:demo-data';
  let enabled = false;
  let allowedFiles = new Set<string>();
  return {
    name: 'local-demo-data',
    configResolved(config) {
      enabled = config.command === 'serve' && config.mode !== 'test';
    },
    resolveId(id) {
      if (id === 'virtual:demo-data') return virtualId;
    },
    load(id) {
      if (id !== virtualId) return;
      if (!enabled) return 'export default null';
      const records = JSON.parse(
        readFileSync(resolve(root, 'Dokumentenuebersicht.json'), 'utf8'),
      ) as DemoRecord[];
      allowedFiles = new Set(records.map((record) => record.dateiName));
      const transcripts = Object.fromEntries(
        records.flatMap(({ dateiName }) => {
          const file = resolve(root, dateiName.replace(/\.pdf$/i, '.json'));
          return existsSync(file) ? [[dateiName, JSON.parse(readFileSync(file, 'utf8'))]] : [];
        }),
      );
      const enriched = records.map((record) => ({
        ...record,
        sizeBytes: statSync(resolve(root, record.dateiName)).size,
      }));
      return `export default ${JSON.stringify({ records: enriched, transcripts })}`;
    },
    configureServer(server) {
      const records = JSON.parse(
        readFileSync(resolve(root, 'Dokumentenuebersicht.json'), 'utf8'),
      ) as DemoRecord[];
      allowedFiles = new Set(records.map((record) => record.dateiName));
      server.middlewares.use('/__demo_pdf', (req, res, next) => {
        let name: string;
        try {
          name = decodeURIComponent((req.url ?? '').split('?')[0].slice(1));
        } catch {
          res.statusCode = 400;
          res.end();
          return;
        }
        if (!name || name !== basename(name) || /[\\/]/.test(name) || !allowedFiles.has(name)) {
          res.statusCode = 404;
          res.end();
          return;
        }
        const file = resolve(root, name);
        if (!existsSync(file)) {
          next();
          return;
        }
        if (!lstatSync(file).isFile()) {
          res.statusCode = 404;
          res.end();
          return;
        }
        res.setHeader('Content-Type', 'application/pdf');
        createReadStream(file)
          .on('error', () => {
            res.statusCode = 500;
            res.end();
          })
          .pipe(res);
      });
    },
  };
}
export default defineConfig({
  plugins: [react(), demoData()],
  test: { environment: 'jsdom', setupFiles: ['./src/test/setup.ts'], css: true },
});
