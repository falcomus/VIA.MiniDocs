import { createServer } from 'vite';

const { MINIDOCS_E2E_CERT: cert, MINIDOCS_E2E_KEY: key } = process.env;
if (!cert || !key) throw new Error('Missing disposable browser regression certificate.');
const server = await createServer({
  mode: 'demo',
  server: { host: '127.0.0.1', port: 4179, strictPort: true, https: { cert, key } },
});
await server.listen();
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.once(signal, async () => {
    await server.close();
    process.exit(0);
  });
}
