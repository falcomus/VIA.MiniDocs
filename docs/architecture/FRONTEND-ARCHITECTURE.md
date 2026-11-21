# Frontend-Architektur für die KU-Demo

## Ausgangspunkt und Ziel

Die bestehende Vite-Anwendung mit React 19, TypeScript strict, CSS Modules, Vitest und React Testing Library bleibt erhalten. `App` verbindet derzeit die Demo-Arrays mit `TopicTree` und `DocumentList`; Auswahl und aufgeklappte Themen liegen in React-State. Die Testbasis ist bewusst kleiner und visuell nicht die KU-Referenz. Keine Backend- oder Datenbankimplementierung in dieser Vorbereitungsphase.

## Strukturvorschlag

Unter `frontend/src` bleiben `features/topics` und `features/documents` erhalten; nach Bedarf ergänzen: `features/search`, `features/tags`, `features/import`, `features/viewer`, `features/settings`, `components` für Shell/Dialog/Popover/Toast, `services` für Schnittstellen und Demo-Adapter, `styles` für Tokens und globale Grundlagen. `App` bzw. ein kleiner Container verbindet `AppShell`, `Header`, `TopicTree`, `ViewToolbar`, `DocumentList`/`DocumentGrid`, `SelectionBar` und `DocumentViewer`. Fachliche Komponenten und ihre CSS Modules bleiben möglichst beieinander; vorhandene Komponenten schrittweise erweitern.

## Daten und Zustand

Nach Bedarf bestehende Typen erweitern: `Topic { id, parentId, name, systemKey? }`; `Tag { id, name, description, color }`; `Document { id, fileName, title, date: string | null, topicId, pageCount, tagIds, kind, transcript?, source }`; `Transcript { pages: { page, text }[], edited }`; `PendingTranscript { fileName, pages, source, importedAt, metadata }`. IDs als nachvollziehbare Typen; Unterscheidung zwischen simulierten Seiten und lokal importierter PDF-Datei explizit modellieren. Keine angenommene VIA.Docs-ID als gesicherter API-Vertrag. Datumswerte als ISO-Kalenderdaten behandeln. Modelle sind Entwürfe, keine Datenbankschemata.

`useState`/`useReducer` für gemeinsamen Demo-Zustand und explizite Aktionen; abgeleitete sichtbare Dokumente, Sortierung und Treffer aus Daten und Filtern berechnen statt doppelt speichern. Auswahlanker, geöffnetes Thema, View/Zoom, Filter, Viewer, Dialoge und temporäre Undo-Aktion getrennt halten. Context nur bei tatsächlichem Prop-Drilling. Keine zusätzliche State-Management-Bibliothek ohne nachgewiesene Notwendigkeit.

## Logik und Grenzen

Reine Funktionen für Baumoperationen, Sortierung, Suchnormalisierung, Auswahlbereiche, Importparser und Konfliktregeln; Hooks wie `useSelection`, `useSearch`, `useDragDrop` und `useKeyboardShortcuts` nach Bedarf. Browser-Dateiobjekte und Objekt-URLs hinter Import-/Preview-/Download-Services kapseln und beim Ersatz bzw. Unmount freigeben. React verwaltet UI; imperative Browser-APIs nur über Refs/Effekte, nicht als Ersatz fürs Rendering. Demo-Repository mit klarer In-Memory-Lebensdauer; kein vorgetäuschtes dauerhaftes Speichern. Seed und synthetische Vorschau nachvollziehbar aus Originalverhalten ableiten.

Spätere Grenze: `React/TypeScript → ASP.NET Core MiniDocs API (.NET 10) → PostgreSQL` für Themen, Tags, Transkripte, Dokumentmetadaten und Suche; die API bindet zusätzlich `VIA.Docs V1.2 REST API` für Originaldateien, Abruf und Vorschaubilder an. VIA.Docs verwendet Application-Authentifizierung, JWT und vorhandene Registrierung mittels Einladung. Zugangsdaten bleiben serverseitig; keine direkten VIA.Docs-Secrets im Frontend. Details zu Tokens, Verträgen, Hosting und Fehlerfällen erst gegen echte Schnittstellen und gesonderten Testauftrag klären. Der Entwurf im Begleitdokument ist keine bestätigte API-Spezifikation.

## Fehler und Tests

Importfehler, nicht unterstützte Formate, Duplikate, fehlende PDFs, Konflikte und blockierte Tabs sichtbar melden; Teilerfolge im Importbericht erhalten. Fehler bei künftigem Netzwerkzugriff an der Servicegrenze typisieren und ohne Geheimnisse anzeigen. Undo und bestätigungspflichtige Aktionen explizit modellieren.

Tests nah an Features: reine Fachlogik für Hierarchie, Filter, Import und Sortierung; React Testing Library für Auswahl, Suche, Dialoge, Viewer und Tastatur; Browser-Integration für echte Datei-Drops, PDF, Downloads und visuelle Referenzvergleiche, soweit die Testumgebung sie unterstützt. `npm run check` bleibt Mindestprüfung; Browserverhalten und Bildvergleich benötigen gesonderte Prüfung. Keine neue Testinfrastruktur ohne konkreten Bedarf.
