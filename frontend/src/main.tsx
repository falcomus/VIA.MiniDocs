import { lazy, StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthApp } from './features/auth/AuthApp';
import './styles/global.css';

const DemoApp = lazy(() => import('./App').then(({ App }) => ({ default: App })));
const DemoViewer = lazy(() =>
  import('./features/viewer/StandaloneViewer').then(({ StandaloneViewer }) => ({
    default: StandaloneViewer,
  })),
);
const container = document.getElementById('root');

if (!container) {
  throw new Error('Der Anwendungs-Container #root wurde nicht gefunden.');
}

createRoot(container).render(
  <StrictMode>
    {import.meta.env.MODE !== 'demo' ? (
      <AuthApp />
    ) : (
      <Suspense fallback={<p role="status">Demo wird geladen …</p>}>
        {new URLSearchParams(window.location.search).has('viewer') ||
        new URLSearchParams(window.location.search).has('document') ? (
          <DemoViewer />
        ) : (
          <DemoApp />
        )}
      </Suspense>
    )}
  </StrictMode>,
);
