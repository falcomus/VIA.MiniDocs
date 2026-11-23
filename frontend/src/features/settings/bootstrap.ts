import { applyTheme, readTheme, resolveTheme, systemIsDark } from './theme';
// Runs before the application module so no rendered UI appears in the wrong theme.
applyTheme(resolveTheme(readTheme(), systemIsDark()));
