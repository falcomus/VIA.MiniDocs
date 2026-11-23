export type ThemeMode = 'light' | 'dark' | 'system';
export type EffectiveTheme = 'light' | 'dark';
export const THEME_KEY = 'via.minidocs.theme';
export function readTheme(): ThemeMode {
  try {
    const value = window.localStorage.getItem(THEME_KEY);
    return value === 'dark' || value === 'system' || value === 'light' ? value : 'light';
  } catch {
    return 'light';
  }
}
export function systemIsDark(): boolean {
  return (
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  );
}
export function resolveTheme(mode: ThemeMode, dark: boolean): EffectiveTheme {
  return mode === 'system' ? (dark ? 'dark' : 'light') : mode;
}
export function applyTheme(theme: EffectiveTheme) {
  document.documentElement.dataset.theme = theme;
  document
    .querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    ?.setAttribute('content', theme === 'dark' ? '#101b30' : '#f6f7f9');
}
