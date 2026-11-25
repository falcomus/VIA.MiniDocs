# KU-Migrationsplan

> Historischer Migrationsplan und Referenz für den ursprünglichen Umfang. Für aktuellen Stand, gültige Aufgaben und freigegebene Abweichungen gelten [Masterplan](../MASTERPLAN.md) und [Tickets](../TICKETS.md). Insbesondere wurde die ursprüngliche Löschregel durch Q8 ersetzt; der KU-Prototyp bleibt als Vergleichsgegenstand erhalten.

Vorbereitung abgeschlossen bedeutet nicht, dass die UI migriert ist. Pakete aufeinander aufbauen, bestehende Funktionen erhalten und jeweils gegen die lokale HTML-Referenz und das Inventar prüfen. Dateinamen sind Vorschläge; fachliche Logik bleibt bei den jeweiligen Features. Demo-Zustand bleibt flüchtig. Tests bezeichnen bei Bedarf zusätzlich zur bestehenden `npm run check`-Prüfung erforderliche Interaktions- bzw. Browserprüfungen.

## M1 – Grundlayout, Design-Tokens und Icons
- Dateien: `App.tsx`, `App.module.css`, `styles/global.css`, ggf. `styles/tokens.css`, `components/AppShell`, `components/Icon`.
- Abhängigkeiten: keine außer bestehendem Vite/CSS-Modules-Gerüst; SVG-Pfade und CSS-Variablen der Referenz untersuchen.
- Abnahme: Kopf, Seitenleiste, Arbeitsbereich, Farben, Schrift, Abstände und Icons entsprechen der Referenz; bestehende Auswahl funktioniert.
- Tests: bestehende Tests und Layout-/Screenshotvergleich in repräsentativer Fenstergröße.
- Risiken: Schriftmetriken, Icons und enge Fenster; keine eigene visuelle Neuinterpretation.

## M2 – Themenbaum und Navigation
- Dateien: `features/topics/TopicTree*`, `topic.types.ts`, Baumfunktionen, `Breadcrumbs`, Dialog/Popover.
- Abhängigkeiten: M1, Demodaten und gemeinsame Zustandsaktionen.
- Ursprüngliche Referenz: Hierarchie, rekursive Zähler, Pfad, Kontextmenü, Erstellen/Umbenennen/Löschen und Systemknoten; im KU-Prototyp verschiebt Löschen Dokumente nach „Nicht zugeordnet“. Der aktuelle Stand gemäß [Q8](../TICKETS.md#q8--themenlöschung-an-ku-regel-anpassen) erlaubt nur das Löschen leerer Themen ohne Unterthemen; Themen umhängen einschließlich Zyklenschutz.
- Tests: Baumoperationen, Auswahl, Auf-/Zuklappen, Kontextmenü, Systemschutz und Umhängen.
- Risiken: zirkuläre Hierarchie, Auswahl nach Löschung, Tastaturzugang zum Kontextmenü.

## M3 – Listen- und Kachelansicht
- Dateien: `features/documents/DocumentList*`, `DocumentRow`, `DocumentGrid`, `DocumentTile`, Demo-Vorschau-Service.
- Abhängigkeiten: M1–M2, Dokument-/Themenmodell und synthetische Vorschau.
- Abnahme: Ordner und Dokumente mit Originalspalten, Metadaten, Transkriptstatus, Aktionen und Zuständen; Ansicht umschaltbar, Doppelklick-Ziele vorbereitet.
- Tests: beide Ansichten, leere Themen, Unterordner und Datenanzeige; visueller Vergleich.
- Risiken: Vorschaugenerierung und Performance bei vielen Seiten.

## M4 – Auswahl, Sortierung, Zoom, Drag-and-Drop
- Dateien: `ViewToolbar`, `SelectionBar`, `useSelection`, Sortierfunktionen, `useDragDrop`, Themenbaum und Dokumentansichten.
- Abhängigkeiten: M2–M3; Dialog/Toast für Verschieben und Rückgängig.
- Abnahme: Klick/Checkbox/Strg/Shift, Alle/Keine, korrekte Sortierung und Kachelzoom; Auswahl auf Thema/Ordner ziehen oder Ziel wählen; Undo stellt ursprüngliche Themen wieder her.
- Tests: Auswahlbereiche und Anker, Sortierung mit fehlendem Datum, Zoom, Mehrfachverschiebung und Undo; echter Browser für Drag-and-drop.
- Risiken: Browser-Drag-Events, versteckte Auswahl und versehentliches Verschieben.

## M5 – Suche, Filter und Tags
- Dateien: `features/search/SearchBar`, `FilterBar`, Suchfunktionen, `features/tags/TagChip`, `TagPicker`, `TagSettings`.
- Abhängigkeiten: M2–M4, Tagmodell und Suchtreffer-Darstellung.
- Abnahme: Titel-/Volltextmodus, Phrasen und alle Suchbegriffe, Zeitraum/Preset, Tag-UND, Themenbereich inkl. Unterthemen, Relevanz und Trefferseite; Tagpflege und Zuweisung einzeln/mehrfach.
- Tests: Normalisierung, Phrasen, Filterkombinationen, Relevanz, leere Treffer, Tagpflege und Mehrfachzuweisung.
- Risiken: Original-Teilstrings nicht mit späterer Datenbanksuche verwechseln; Fokus und Filterzustand bei Navigation.

## M6 – Viewer und Transkript-Editor
- Dateien: `features/viewer/DocumentViewer`, `PdfPane`, `TranscriptPane`, `ViewerSplitter`, Preview-/Tab-Service.
- Abhängigkeiten: M3–M5 für Auswahl, Treffer und Dokumentmodell; M7 ergänzt den Importpfad.
- Abnahme: Overlay/Tab, Trefferseite, vorheriges/nächstes Dokument, Zoom, Metadaten/Tags, seitenweise Editor, leeres Transkript, Suchen/alle ersetzen, synchrones Scrollen und Splitter; PDF im Browser-Viewer.
- Tests: Öffnungsmodi, Navigation, Metadaten, Bearbeitung und Ersetzen, Tastatur/Escape; Browserprüfung für PDF, Scrollen und Tab.
- Risiken: Browser-PDF-Viewer begrenzt Scrollsynchronisation und Zoomsteuerung; Objekt-URL-Lebensdauer, Fokus und ungespeicherte Demoänderungen.

## M7 – Upload, Downloads, Dialoge, Einstellungen
- Dateien: `features/import`, `features/settings`, `services/importService`, `downloadService`, gemeinsame `Dialog`, `Popover`, `Toast`.
- Abhängigkeiten: M2–M6; Demo-Dokument- und Transkriptmodell. Grundbausteine der Dialoge dürfen bereits früher entstehen.
- Abnahme: PDF/ZIP/Markdown/TXT per Button und Fenster-Drop, Importbericht, Dateinamen-Abgleich, wartende Transkripte, Konfliktabfrage; Einzel-/ZIP-Download inkl. Transkripte.md; Tags, Öffnungsmodus und Demo-Reset. Word zeigt wie im Original nur Hinweis.
- Tests: Parser, Duplikate, Zuordnung vor/nach PDF, Konflikte, Teilerfolg, ZIP und Reset; echte Dateien, Download und Datei-Drop im Browser.
- Risiken: ZIP-Methoden/Browser-Support, beschädigte Dateien, PDF-Seitenzählung, Ressourcenfreigabe; keine Produktionsdaten verwenden.

## M8 – visueller und funktionaler Gesamtvergleich
- Dateien: alle betroffenen Features, Testdateien und ggf. Vergleichsdokumentation; keine parallele App.
- Abhängigkeiten: M1–M7.
- Abnahme: Inventar zeilenweise durchgehen; Szenarien und Bildschirmzustände beider Ansichten mit Original vergleichen, Abweichungen und bewusst offene Punkte dokumentieren; keine Originalfunktion stillschweigend entfernen.
- Tests: `npm run check`; Browser-Szenarien für Suche, Drag-and-drop, Import, Viewer, Downloads, Dialoge und Tastatur; Vergleich von Bildschirmfotos in dokumentierter Auflösung.
- Risiken: Browserunterschiede und nicht automatisierbare native PDF-/Dateidialoge; bekannte Abweichungen transparent festhalten.
