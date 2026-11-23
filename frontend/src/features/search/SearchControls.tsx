import { useEffect, useRef } from 'react';
import { Icon } from '../../components/Icon';
import type { TagDefinition } from '../documents/tags';
import { datePreset } from './search';
import { localToday } from './useSearchState';
import type { SearchControlsState, SearchState } from './useSearchState';
import styles from './SearchControls.module.css';
export function SearchControls({
  search,
  onChange,
  tags,
}: {
  readonly tags: readonly TagDefinition[];
  readonly search: SearchControlsState;
  readonly onChange: (next: SearchState) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const filterButton = useRef<HTMLButtonElement>(null);
  const { criteria } = search;
  const update = (patch: Partial<SearchState>) => onChange({ ...criteria, ...patch });
  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      const target = event.target;
      if (
        event.key === '/' &&
        !(
          target instanceof HTMLElement &&
          (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName))
        ) &&
        !document.querySelector('[aria-modal="true"]')
      ) {
        event.preventDefault();
        input.current?.focus();
      }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, []);
  return (
    <>
      <div className={styles.container}>
        <div className={styles.search}>
          <div className={styles.searchBox}>
            <Icon name="search" />
            <input
              ref={input}
              type="search"
              aria-label="Dokumente suchen"
              placeholder="Dokumente suchen …  (Taste /)"
              value={criteria.query}
              onChange={(event) => update({ query: event.target.value })}
              onKeyDown={(event) => {
                if (event.key === 'Escape') {
                  event.stopPropagation();
                  update({ query: '' });
                  event.currentTarget.blur();
                }
              }}
            />
            {criteria.query && (
              <button
                type="button"
                className={styles.clear}
                aria-label="Suchtext löschen"
                onClick={() => {
                  update({ query: '' });
                  input.current?.focus();
                }}
              >
                <Icon name="x" />
              </button>
            )}
            <div className={styles.segment} aria-label="Suchmodus">
              <button
                type="button"
                aria-pressed={criteria.mode === 'title'}
                className={criteria.mode === 'title' ? styles.active : undefined}
                onClick={() => update({ mode: 'title' })}
              >
                Nur Titel
              </button>
              <button
                type="button"
                aria-pressed={criteria.mode === 'full'}
                className={criteria.mode === 'full' ? styles.active : undefined}
                onClick={() => update({ mode: 'full' })}
              >
                Volltext inkl. Transkripte
              </button>
            </div>
          </div>
          <button
            ref={filterButton}
            className={styles.button}
            aria-expanded={search.filterOpen}
            aria-controls="search-filter"
            onClick={() => search.setFilterOpen(!search.filterOpen)}
          >
            <Icon name="filter" />
            Filter
          </button>
        </div>
      </div>
      {search.filterOpen && (
        <div
          id="search-filter"
          className={styles.filter}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.stopPropagation();
              search.setFilterOpen(false);
              filterButton.current?.focus();
            }
          }}
        >
          <div className={styles.group}>
            <span>Zeitraum</span>
            <label>
              Von{' '}
              <input
                type="date"
                value={criteria.from}
                onChange={(event) => update({ from: event.target.value })}
              />
            </label>
            <label>
              Bis{' '}
              <input
                type="date"
                value={criteria.to}
                onChange={(event) => update({ to: event.target.value })}
              />
            </label>
            <button onClick={() => update(datePreset('12m', localToday(new Date())))}>
              12 Monate
            </button>
            <button onClick={() => update(datePreset('year', localToday(new Date())))}>
              Dieses Jahr
            </button>
          </div>
          <fieldset className={styles.group}>
            <legend>Tags (alle müssen passen)</legend>
            {tags.map((tag) => (
              <label key={tag.id}>
                <input
                  type="checkbox"
                  checked={criteria.tagIds.includes(tag.id)}
                  onChange={(event) =>
                    update({
                      tagIds: event.target.checked
                        ? [...criteria.tagIds, tag.id]
                        : criteria.tagIds.filter((id) => id !== tag.id),
                    })
                  }
                />
                {tag.name}
              </label>
            ))}
          </fieldset>
          <label className={styles.group}>
            Bereich{' '}
            <select
              value={criteria.scope}
              onChange={(event) =>
                update({ scope: event.target.value === 'topic' ? 'topic' : 'all' })
              }
            >
              <option value="all">Alle Themen</option>
              <option value="topic">Aktuelles Thema inkl. Unterthemen</option>
            </select>
          </label>
          <button onClick={() => update({ from: '', to: '', tagIds: [], scope: 'all' })}>
            Filter zurücksetzen
          </button>
          {criteria.from && criteria.to && criteria.from > criteria.to && (
            <span role="alert">Das Startdatum liegt nach dem Enddatum.</span>
          )}
        </div>
      )}
    </>
  );
}
