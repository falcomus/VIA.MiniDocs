import { useLayoutEffect, useState } from 'react';
import { applyTheme, readTheme, resolveTheme, systemIsDark, THEME_KEY } from './theme';
import type { ThemeMode } from './theme';
/** Owns the preference; the effective palette is synchronized with browser APIs. */
export function useTheme() {
  const [mode, setMode] = useState<ThemeMode>(readTheme);
  useLayoutEffect(() => {
    const media =
      typeof window.matchMedia === 'function'
        ? window.matchMedia('(prefers-color-scheme: dark)')
        : null;
    const update = () => applyTheme(resolveTheme(mode, media?.matches ?? systemIsDark()));
    update();
    try {
      window.localStorage.setItem(THEME_KEY, mode);
    } catch {
      /* Storage is optional. */
    }
    if (mode === 'system' && media) {
      media.addEventListener('change', update);
      return () => media.removeEventListener('change', update);
    }
  }, [mode]);
  return { mode, setMode };
}
