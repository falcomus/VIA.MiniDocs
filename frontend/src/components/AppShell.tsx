import type { OpeningMode } from '../features/viewer/openingMode';
import { SettingsMenu } from '../features/settings/SettingsMenu';
import type { TagDefinition } from '../features/documents/tags';
import { SearchControls } from '../features/search/SearchControls';
import type { SearchControlsState, SearchState } from '../features/search/useSearchState';
import type { DocumentSort } from '../features/documents/documentOperations';
import type { DocumentView } from '../features/documents/document.types';
import type { ReactNode } from 'react';
import type { Topic, TopicId } from '../features/topics/topic.types';
import type { TopicDrag } from '../features/topics/useTopicDrag';
import { Icon } from './Icon';
import styles from './AppShell.module.css';

export function AppShell({
  sidebar,
  children,
  modal,
  search,
  onSearchChange,
  tags,
  onManageTags,
  openingMode,
  onOpeningModeChange,
}: {
  readonly tags: readonly TagDefinition[];
  readonly onManageTags: () => void;
  readonly openingMode: OpeningMode;
  readonly onOpeningModeChange: (mode: OpeningMode) => void;
  readonly search: SearchControlsState;
  readonly onSearchChange: (state: SearchState) => void;
  readonly sidebar: ReactNode;
  readonly children: ReactNode;
  readonly modal?: ReactNode;
}) {
  return (
    <div className={styles.app}>
      <div className={styles.shell} inert={!!modal}>
        <Header
          search={search}
          onSearchChange={onSearchChange}
          tags={tags}
          onManageTags={onManageTags}
          openingMode={openingMode}
          onOpeningModeChange={onOpeningModeChange}
        />
        <div className={styles.layout}>
          <aside className={styles.sidebar}>{sidebar}</aside>
          <main className={styles.main}>{children}</main>
        </div>
      </div>
      {modal}
    </div>
  );
}

function Header({
  search,
  onSearchChange,
  tags,
  onManageTags,
  openingMode,
  onOpeningModeChange,
}: {
  readonly openingMode: OpeningMode;
  readonly onOpeningModeChange: (mode: OpeningMode) => void;
  readonly tags: readonly TagDefinition[];
  readonly onManageTags: () => void;
  readonly search: SearchControlsState;
  readonly onSearchChange: (state: SearchState) => void;
}) {
  return (
    <header className={styles.topbar}>
      <div className={styles.brand}>
        <span className={styles.logo}>MD</span>
        <div>
          <b>MiniDoc</b>
          <small>Prototyp · Notizen-Archiv</small>
        </div>
      </div>
      <SearchControls search={search} onChange={onSearchChange} tags={tags} />
      <div className={styles.actions}>
        <button className={styles.button} disabled>
          <Icon name="text" />
          Transkripte importieren
        </button>
        <button className={`${styles.button} ${styles.primary}`} disabled>
          <Icon name="upload" />
          Hochladen
        </button>
        <SettingsMenu
          onManageTags={onManageTags}
          openingMode={openingMode}
          onOpeningModeChange={onOpeningModeChange}
        />
      </div>
    </header>
  );
}

export function Sidebar({
  children,
  documentCount,
  pageCount,
  onAdd,
  drag,
}: {
  readonly children: ReactNode;
  readonly documentCount: number;
  readonly pageCount: number;
  readonly onAdd: () => void;
  readonly drag: TopicDrag;
}) {
  return (
    <>
      <div
        className={styles.sidebarHeading}
        onDragOver={(event) => {
          if (Array.from(event.dataTransfer.types ?? []).includes('application/x-minidoc-docs'))
            return;
          if (drag.over(null)) {
            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';
          }
        }}
        onDrop={(event) => {
          if (Array.from(event.dataTransfer.types ?? []).includes('application/x-minidoc-docs'))
            return;
          event.preventDefault();
          event.stopPropagation();
          drag.drop(null, event.dataTransfer);
        }}
      >
        <span>Themen</span>
        <button
          type="button"
          className={styles.add}
          aria-label="Neues Thema auf oberster Ebene"
          onClick={onAdd}
        >
          <Icon name="plus" />
        </button>
      </div>
      <div className={styles.tree}>{children}</div>
      <div className={styles.sidebarFoot}>
        <b>{documentCount}</b> Dokumente · <b>{pageCount}</b> Seiten
        <br />
        <span>Prototyp – Daten nur im Browser, Reload setzt zurück</span>
      </div>
    </>
  );
}

export function Toolbar({
  searchActive,
  queryActive,
  sort,
  onSortChange,
  zoom,
  onZoomChange,
  view,
  onViewChange,
  title,
  ancestors,
  onNavigate,
}: {
  readonly searchActive: boolean;
  readonly queryActive: boolean;
  readonly sort: DocumentSort | 'relevance';
  readonly onSortChange: (sort: DocumentSort | 'relevance') => void;
  readonly zoom: number;
  readonly onZoomChange: (zoom: number) => void;
  readonly view: DocumentView;
  readonly onViewChange: (view: DocumentView) => void;
  readonly title: string;
  readonly ancestors: readonly Topic[];
  readonly onNavigate: (id: TopicId) => void;
}) {
  return (
    <div className={styles.toolbar}>
      <div className={styles.crumbs}>
        {!searchActive &&
          ancestors.map((topic) => (
            <span className={styles.crumb} key={topic.id}>
              <button type="button" onClick={() => onNavigate(topic.id)}>
                {topic.name}
              </button>
              <Icon name="chevron" />
            </span>
          ))}
        <h1>{searchActive ? 'Suchergebnisse' : title}</h1>
      </div>
      <div className={styles.tools}>
        <select
          className={styles.select}
          aria-label="Sortierung"
          value={sort}
          onChange={(event) => onSortChange(event.target.value as DocumentSort | 'relevance')}
        >
          {searchActive && queryActive && <option value="relevance">Relevanz</option>}
          <option value="date-desc">Datum – neueste zuerst</option>
          <option value="date-asc">Datum – älteste zuerst</option>
          <option value="title">Titel A–Z</option>
        </select>
        <div className={`${styles.segment} ${styles.square}`} aria-label="Ansicht">
          <button
            className={view === 'list' ? styles.active : undefined}
            aria-label="Listenansicht"
            aria-pressed={view === 'list'}
            onClick={() => onViewChange('list')}
          >
            <Icon name="list" />
          </button>
          <button
            className={view === 'grid' ? styles.active : undefined}
            aria-label="Kachelansicht"
            aria-pressed={view === 'grid'}
            onClick={() => onViewChange('grid')}
          >
            <Icon name="grid" />
          </button>
        </div>
        <label className={styles.zoom}>
          Zoom{' '}
          <input
            type="range"
            min="110"
            max="380"
            step="10"
            value={zoom}
            aria-label="Zoom"
            disabled={view === 'list'}
            onChange={(event) => onZoomChange(Number(event.target.value))}
          />
        </label>
      </div>
    </div>
  );
}

export function Content({ children }: { readonly children: ReactNode }) {
  return (
    <>
      <div className={styles.content}>{children}</div>
    </>
  );
}
