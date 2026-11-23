import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { StandaloneViewer } from './features/viewer/StandaloneViewer';
import './styles/global.css';

const container = document.getElementById('root');

if (!container) {
  throw new Error('Der Anwendungs-Container #root wurde nicht gefunden.');
}

createRoot(container).render(
  <StrictMode>
    {new URLSearchParams(window.location.search).has('viewer') ||
    new URLSearchParams(window.location.search).has('document') ? (
      <StandaloneViewer />
    ) : (
      <App />
    )}
  </StrictMode>,
);
