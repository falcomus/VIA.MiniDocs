import type { ReactNode } from 'react';
import styles from './AppHeader.module.css';

/** Shared product header; it has no dependency on document or session data. */
export function AppHeader({
  subtitle,
  children,
}: {
  readonly subtitle: string;
  readonly children: ReactNode;
}) {
  return (
    <header className={styles.topbar}>
      <div className={styles.brand}>
        <span className={styles.logo}>MD</span>
        <div>
          <b>MiniDoc</b>
          <small>{subtitle}</small>
        </div>
      </div>
      {children}
    </header>
  );
}
