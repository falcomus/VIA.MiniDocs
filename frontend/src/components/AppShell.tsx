import type { ReactNode } from 'react';
import { Icon } from './Icon';
import styles from './AppShell.module.css';

export function AppShell({ sidebar, children }: { readonly sidebar: ReactNode; readonly children: ReactNode }) {
  return <div className={styles.app}><Header /><div className={styles.layout}><aside className={styles.sidebar}>{sidebar}</aside><main className={styles.main}>{children}</main></div></div>;
}

function Header() {
  return <header className={styles.topbar}>
    <div className={styles.brand}><span className={styles.logo}>MD</span><div><b>MiniDoc</b><small>Prototyp · Notizen-Archiv</small></div></div>
    <div className={styles.search}>
      <div className={styles.searchBox}><Icon name="search" /><input type="search" aria-label="Dokumente suchen" placeholder="Dokumente suchen …  (Taste /)" disabled /><div className={styles.segment} aria-label="Suchmodus"><button disabled>Nur Titel</button><button className={styles.active} disabled>Volltext inkl. Transkripte</button></div></div>
      <button className={styles.button} disabled><Icon name="filter" />Filter</button>
    </div>
    <div className={styles.actions}><button className={styles.button} disabled><Icon name="text" />Transkripte importieren</button><button className={`${styles.button} ${styles.primary}`} disabled><Icon name="upload" />Hochladen</button><button className={`${styles.button} ${styles.iconButton}`} aria-label="Einstellungen" disabled><Icon name="gear" /></button></div>
  </header>;
}

export function Sidebar({ children, documentCount, pageCount }: { readonly children: ReactNode; readonly documentCount: number; readonly pageCount: number }) {
  return <><div className={styles.sidebarHeading}><span>Themen</span><button className={styles.add} aria-label="Neues Thema auf oberster Ebene" disabled><Icon name="plus" /></button></div><div className={styles.tree}>{children}</div><div className={styles.sidebarFoot}><b>{documentCount}</b> Dokumente · <b>{pageCount}</b> Seiten<br /><span>Prototyp – Daten nur im Browser, Reload setzt zurück</span></div></>;
}

export function Toolbar({ title }: { readonly title: string }) {
  return <div className={styles.toolbar}><div className={styles.crumbs}><h1>{title}</h1></div><div className={styles.tools}>
    <select className={styles.select} aria-label="Sortierung" disabled><option>Sortierung folgt</option></select>
    <div className={`${styles.segment} ${styles.square}`} aria-label="Ansicht"><button className={styles.active} aria-label="Listenansicht" disabled><Icon name="list" /></button><button aria-label="Kachelansicht" disabled><Icon name="grid" /></button></div>
    <label className={styles.zoom}>Zoom <input type="range" min="110" max="380" value="190" aria-label="Zoom" disabled readOnly /></label>
  </div></div>;
}

export function Content({ children }: { readonly children: ReactNode }) {
  return <><div className={styles.bulkHint}>Auswahl, Mehrfachauswahl und Öffnen folgen in weiteren Paketen.</div><div className={styles.content}>{children}</div></>;
}
