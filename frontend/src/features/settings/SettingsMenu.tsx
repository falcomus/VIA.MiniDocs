import type { OpeningMode } from '../viewer/openingMode';
import { useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { useTheme } from './useTheme';
import type { ThemeMode } from './theme';
import styles from './SettingsMenu.module.css';
export function SettingsMenu({
  onManageTags,
  openingMode,
  onOpeningModeChange,
}: {
  readonly onManageTags: () => void;
  readonly openingMode: OpeningMode;
  readonly onOpeningModeChange: (mode: OpeningMode) => void;
}) {
  const [open, setOpen] = useState(false);
  const { mode, setMode } = useTheme();
  const root = useRef<HTMLDivElement>(null);
  const gear = useRef<HTMLButtonElement>(null);
  const first = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (!open) return;
    first.current?.focus();
    const outside = (event: PointerEvent) => {
      if (event.target instanceof Node && !root.current?.contains(event.target)) setOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        setOpen(false);
        gear.current?.focus();
      }
    };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('pointerdown', outside);
      document.removeEventListener('keydown', escape);
    };
  }, [open]);
  const choices: readonly [ThemeMode, string][] = [
    ['light', 'Hell'],
    ['dark', 'Dunkel'],
    ['system', 'System'],
  ];
  return (
    <div className={styles.root} ref={root}>
      <button
        ref={gear}
        type="button"
        className={styles.gear}
        aria-label="Einstellungen"
        aria-expanded={open}
        aria-controls="settings-panel"
        onClick={() => setOpen(!open)}
      >
        <Icon name="gear" />
      </button>
      {open && (
        <section id="settings-panel" className={styles.panel} aria-label="Einstellungen">
          <h2>Einstellungen</h2>
          <fieldset>
            <legend>Darstellung</legend>
            <div className={styles.choices}>
              {choices.map(([value, label]) => (
                <label key={value}>
                  <input
                    ref={mode === value ? first : undefined}
                    type="radio"
                    name="theme-mode"
                    value={value}
                    checked={mode === value}
                    onChange={() => setMode(value)}
                  />
                  <span>{label}</span>
                </label>
              ))}
            </div>
          </fieldset>
          <fieldset>
            <legend>Dokumente öffnen</legend>
            <div className={styles.choices}>
              {(
                [
                  ['overlay', 'Im Overlay'],
                  ['tab', 'In neuem Tab'],
                ] as const
              ).map(([value, label]) => (
                <label key={value}>
                  <input
                    type="radio"
                    name="opening-mode"
                    checked={openingMode === value}
                    onChange={() => onOpeningModeChange(value)}
                  />
                  {label}
                </label>
              ))}
            </div>
          </fieldset>
          <button
            className={styles.manage}
            onClick={() => {
              setOpen(false);
              gear.current?.focus();
              onManageTags();
            }}
          >
            Tags verwalten
          </button>
        </section>
      )}
    </div>
  );
}
