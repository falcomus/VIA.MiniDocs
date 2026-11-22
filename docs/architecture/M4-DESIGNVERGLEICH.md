# M4 – Quellenvergleich und offene Abnahme

Referenz: lokale KU-HTML-Datei. Geprüft wurden Auswahlleiste (CSS 95–100, `renderBulk`), Dokumentmarkierungen/Checkboxen (107–155), Sortierung/Toolbar (728–754), `moveDocs`/Zielwahl/Undo (864–895) und Ereignisverträge (1335–1421). Keine Originalreferenz wird veröffentlicht.

## Übernommene Werte und Abläufe

- Auswahlleiste: `var(--accent)` (#225b79), Weiß, 42 px Mindesthöhe, 8 px Abstand, Außenabstand 0/20/8 px, Innenabstand 6/8/6/12 px, vorhandener Radius. Buttons 28 px hoch, 12,5 px Schrift, Hintergrund Weiß mit 12 %, Rahmen mit 25 %, Hover mit 22 % Deckkraft; Originalpfade für Öffnen, Tags, Verschieben, Download und Schließen.
- Ohne Auswahl gedämpfter Hinweis mit gestricheltem Rahmen und „Alle auswählen“. Bei Auswahl korrekte Singular-/Pluralzahl. Verschieben und Aufheben aktiv; Öffnen (nur einzeln), Tags und Download bleiben deaktiviert. Kein Viewer-/Tag-/Downloadversprechen.
- Einzelklick ersetzt, Ctrl/Meta schaltet um, Shift bildet den Bereich über aktuelle Sortierung und stabilen ID-Anker; Ctrl/Meta+Shift ergänzt. Checkbox schaltet um und setzt Anker. Ordner gehören nicht zur Dokumentauswahl. Ansichtwechsel erhält IDs; Navigation und erfolgreiche Verschiebung bereinigen Auswahl/Anker.
- Datum neu/alt, Titel A–Z; auch Tabellenkopf bedienbar. Ungültige oder fehlende Kalenderdaten gelten als undatiert (aufsteigend zuerst, absteigend zuletzt); Gleichstände behalten die Eingabereihenfolge. Kein mutierendes Sortieren, keine Suchrelevanz.
- Kachelzoom 110–380 px in 10er-Schritten, Start 190 px. Derselbe lokale CSS-Wert steuert das responsive Grid; Listenansicht deaktiviert den Slider und wird durch Zoom nicht verändert.
- Listenmarkierung `var(--sel)`, Kachelrahmen und 2-px-Auswahlring `var(--accent-2)`, native Checkboxen mit Akzentfarbe. Dropziele im vorhandenen Baumstil; Ordner mit Akzenthintergrund und gestrichelter Kontur (Grid) bzw. Innenrahmen (Liste).
- Dragstart eines ausgewählten Dokuments nimmt die sichtbare Auswahl mit; andernfalls wird nur das gezogene Dokument ausgewählt. Dokumente können auf Baumthemen inklusive „Nicht zugeordnet“, auf direkte Ordner beider Ansichten oder per Tastaturdialog verschoben werden. Der Sidebar-Kopf bleibt entsprechend Original ausschließlich Themen-Wurzelablage.

## Architektur und Sicherheitsvertrag

`useDocumentWorkspace` hält Dokumente, ausgewählte IDs, Anker und die letzte Undo-Zuordnung im Reducer; Sortierung und Zoom separat als kleiner UI-State. Sichtbare Dokumente und Ordnerzähler werden abgeleitet. `documentOperations` enthält Kalender-, Sortier-, Ziel- und Zustandsregeln; Darstellung bleibt in den bestehenden Ansichten. Keine neue Library, globale Variable, Datenkopie oder API.

`useDocumentDrag` verwendet `application/x-minidoc-docs`, eine lokale Sitzung und exakt übereinstimmende ID-Nutzdaten. Übertragene Daten allein autorisieren keine Verschiebung; fehlende, widersprüchliche, gemischte oder abgebrochene Drags werden abgewiesen. `dragover` liest keine geschützten Nutzdaten. Drop verbraucht die Sitzung; Escape, Drag-End, Außen-Drop, Blur, neuer Dragstart und Unmount bereinigen sie. Dokumentstart beendet eine etwaige Themen-Sitzung; der bestehende `useTopicDrag`-Vertrag bleibt erhalten. Reducer validiert gegen aktuelle Dokumentdaten und ein existierendes Ziel, einschließlich Systemthema, vor jeder atomaren Verschiebung. Keine Dokumentduplikate oder unvalidierte IDs.

Undo speichert jede tatsächlich geänderte Ursprungszuordnung einzeln. Nur die letzte erfolgreiche Verschiebung ist rückgängig machbar; neue erfolgreiche Verschiebung ersetzt den Snapshot. Undo ist einmalig. Weitere Dokument-Neuzuordnung beim Löschen von Themen invalidiert den Snapshot. Ohne tatsächliche Änderung wird kein neuer Snapshot angelegt. Ungültige Operationen verändern Dokumente nicht. Fokus kehrt nach Dialogabbruch zum Auslöser zurück, bei entferntem Auslöser sowie nach Undo/Aufheben auf das aktuelle Baumthema. Tastaturauswahl über Checkboxen, Leertaste/Enter auf Dokumenten, Ctrl/Meta+A und Escape im Inhaltsbereich.

## Bewusste Abweichungen und offene Risiken

- Dokumentzielwahl nutzt den bestehenden modalen Themen-Zielwahldialog mit nativem Select und Bestätigung statt des klickbaren Original-Zielbaums mit sofortiger Ausführung. Fokusfalle, Escape, inert-Hintergrund und Rückgabe werden wiederverwendet; keine zweite Dialog-/CSS-Infrastruktur.
- Undo bleibt bis zur nächsten erfolgreichen Verschiebung bzw. Invalidierung sichtbar, statt nach 4,2 Sekunden zu verschwinden. Dadurch bleibt es ohne Zeitdruck tastaturbedienbar. Keine mehrstufige Undo-Historie oder mehrere gleichzeitige Toasts.
- Checkboxen und spätere deaktivierte Aktionen bleiben sichtbar statt ausschließlich bei Hover; die bekannte vereinfachte M3-Demovorschau wird nicht neu gestaltet. Kein eigener imperativ erzeugter Drag-Ghost.
- Auswahlleiste darf bei knapper Breite umbrechen; tatsächliche Proportionen, Kontrast, Fokus, Zoom und Scrollen sind nicht visuell abgenommen. Wiederholte gleichlautende Live-Region-Meldungen benötigen weiterhin AT-Prüfung.

## Nachweise und offene Abnahme

Alle bisherigen 55 Tests bleiben erhalten. Komponenten-/Integrations- und Reducertests prüfen Auswahlmodifikatoren, Checkboxen, Alle/Keine, Ansicht-/Themenwechsel, Sortieranker, Datumssonderfälle, Zoomgrenzen/Schrittweite, Dialog/Fokus/Abbruch, Baum-/Ordner-/Systemdrops, Sitzungskonflikte und Cleanup sowie Undo unterschiedlicher Ursprünge und aufeinanderfolgender Aktionen. Synthetische jsdom-Drops sind kein nativer Browsernachweis.

Browserzugang in M4 erneut geprüft: Die lokale Vite-Demo lief auf `http://127.0.0.1:5174/`; das erlaubte In-App-Browserwerkzeug meldete `net::ERR_CONNECTION_TIMED_OUT`. Keine Umgehung oder native Browsersteuerung versucht. Der temporäre Server wurde beendet. Keine Screenshots, Sichtprüfung oder Screenreaderprüfung durchgeführt.

**Offene ausdrückliche M4-DoD:** native Maus-DnD-Prüfung, Referenzvergleich bei identischer Fenstergröße, schmale Fenster/200 % Zoom, Hover/Fokus/Scroll-/Kontrastprüfung und Screenreader-Abnahme. Technische Umsetzung und grüne Checks sind davon getrennt. Offene Q1/M1–M3-Abnahmen bleiben offen; M5 und Q5 wurden nicht begonnen.
## Technischer Abschlussnachweis

- `npm run check`: Exit 0; Prettier, TypeScript strict, ESLint, Stylelint, **87 Tests in 6 Dateien** und Vite-Produktionsbuild erfolgreich. 55 bestehende Fälle erhalten, 32 neue fachliche/Interaktionsfälle.
- `git diff --check`: Exit 0; `git diff --cached --check`: vor Commit verpflichtend geprüft (Exit 0).
- Selbstreview: eine maßgebliche Dokumentliste, abgeleitete sichtbare Reihenfolge, ID-basierte Auswahl, atomare Verschiebung/Undo, getrennte Drag-Sitzungen, lokale CSS-Zustände und vorhandene Tokens. Kein `!important`, keine neuen globalen Styles, Libraries oder Backend-Zugriffe. Kommentare erläutern Kalenderprüfung, stabile Sortiergleichstände, Zustandsatomarität, Drag-Autorisierung und Undo-Fallback.
- Die oben genannten nativen Browser-/AT-/Sichtnachweise bleiben trotz erfolgreicher technischer Prüfungen offen.
