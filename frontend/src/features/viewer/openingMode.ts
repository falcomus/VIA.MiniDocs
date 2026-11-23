import { useState } from 'react';
export type OpeningMode = 'overlay' | 'tab';
const key = 'via.minidocs.opening-mode';
export function readOpeningMode(): OpeningMode {
  try {
    return localStorage.getItem(key) === 'tab' ? 'tab' : 'overlay';
  } catch {
    return 'overlay';
  }
}
export function useOpeningMode() {
  const [mode, setMode] = useState<OpeningMode>(readOpeningMode);
  return {
    mode,
    setMode: (next: OpeningMode) => {
      setMode(next);
      try {
        localStorage.setItem(key, next);
      } catch {
        /* Preference remains usable for this session. */
      }
    },
  };
}
