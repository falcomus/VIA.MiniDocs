# Ticketkatalog

Stand: 22.11.2026. Einstieg, Phasen und kritischer Pfad: [Masterplan](MASTERPLAN.md). Für jedes Ticket gelten zusätzlich die [allgemeinen und einschlägigen speziellen DoDs](DEFINITION-OF-DONE.md). Abhängigkeiten bezeichnen fachliche Voraussetzungen, keine Genehmigung für spätere produktive Zugriffe. **Technisch abgeschlossen** bedeutet bei M1/M2 ausdrücklich nicht, dass deren visuelle Abnahme bereits erfolgt ist. Die Meilensteine M1–M8 entsprechen dem [KU-Migrationsplan](architecture/KU-MIGRATION-PLAN.md); D-Tickets betreffen ausschließlich die später gesondert beauftragte Datenanbindung.

## Q1 – Accessibility und Themeninteraktion

- **Status:** technisch umgesetzt; Browser- und Screenreader-Abnahme offen. M1-/M2-Sichtabnahmen bleiben offen.
- **Ziel / Umfang:** Tastatur-Zielwahl für Themen mit vorhandener Verschieberegel; Dialogfokus nach Löschung, modaler Hintergrund und bedienbares Kontextmenü.
- **Ausgeschlossen:** M3, allgemeine CSS-Bereinigung, neue DnD-Library und Serveranbindung.
- **Abhängigkeiten:** M2, [Qualitätsaudit](architecture/REACT-QUALITY-REVIEW.md) R1–R3.
- **Akzeptanz:** gültige Ziele auswählbar, Zyklen/Systemknoten ausgeschlossen; Abbruch ohne Änderung; Fokus nach Löschen auf bestehendem Ziel; Menü mit Tastatur und Maus bedienbar.
- **Technische DoD / Tests:** TypeScript, Lint, Vitest, Build und Integrationsfälle einschließlich Verschieben, Abbruch, Schutzregeln und Fokus. Browser-/Screenreader-Abnahme gesondert offen.
- **Risiken:** Browser-DnD, `inert` und Assistive-Technology-Verhalten benötigen echte Browserprüfung.
- **Radix-Entscheidung:** Vorhandene Dialog-/Menüstruktur bleibt; eine kleine, zielgerichtete Korrektur und native Hintergrundisolierung reichen für den jetzigen Umfang. Radix Dialog/Context Menu (MIT, React-19-Unterstützung laut Audit) würde beide Komponenten und Portal-/Styling-/Testverhalten migrieren; aktuell kein nachgewiesener Vorteil, keine zweite Infrastruktur oder Installation. Bei neuem Dialogbedarf und nach Browser-/AT-Prüfung erneut bewerten.

## Q2 – CSS-Qualitätsprüfung und Design-Tokens

- **Status:** technisch abgeschlossen; Browser-/Accessibility-Abnahme offen.
- **Ziel / Umfang:** vorhandene CSS-Doppelregeln und ungenutzten Selektor bereinigen, Panel-Token nutzen, reduzierte Bewegung berücksichtigen, Stylelint in `npm run check` integrieren.
- **Ausgeschlossen:** M3, visuelle Neugestaltung, zusätzliche Styling-Systeme und andere Hilfsbibliotheken.
- **Abhängigkeiten:** Q1, CSS-Audit R11–R14 und bestehende CSS Modules.
- **Akzeptanz:** CSS-Lint ohne relevante Fehler; bestehende Tests bestehen; CSS-Modules-Syntax und eng begrenzte Ausnahmen geprüft; bestehende Desktopwerte beibehalten.
- **Technische DoD / Tests:** `npm run lint:css`, `npm run check`, `git diff --check`; CSS-Modules- und Gegenproben sowie statischer Import-/Klassennamenabgleich. Visuelle und Screenreader-Abnahmen sind nicht durch diese Prüfungen ersetzt.
- **Risiken:** schmale Fenster, tief verschachtelte Bäume, lange Titel, Überlauf, Kontrast, Fokus und Scrollverhalten benötigen reale Browserprüfung.

## Phase A – KU-Demo

### M1 – Grundlayout, Tokens und Icons
- **Status:** technisch abgeschlossen, Commit `b8a657b`; visuelle Browserabnahme offen, Nachverfolgung M8.
- **Ziel / Umfang:** AppShell, Kopf, Sidebar, Werkzeugleiste, Referenz-Tokens und SVG-Icons; vorhandene Komponenten einbinden.
- **Ausgeschlossen:** vollständige Funktionen M2–M8 und Backend.
- **Abhängigkeiten:** bestehende Vite-Basis und Originalreferenz.
- **Akzeptanz:** Grundstruktur und verwendete Referenzwerte vorhanden; vorhandene Auswahl erhalten; nicht verfügbare Controls deaktiviert. Die visuelle Abnahme ist gesondert offen.
- **Technische DoD / Tests:** bestehende Prüfung und Komponenten-/Interaktionstests; technischer Stand im [M1-Vergleich](architecture/M1-DESIGNVERGLEICH.md), UI-Abnahme in M8. Historisch keine nachträgliche Behauptung einer bestandenen Sichtprüfung.
- **Risiken:** Schriftmetriken, Fenstergröße, Scrollverhalten.

### M2 – Themenbaum und Navigation
- **Status:** technisch abgeschlossen, Commit `bf29b663`; visuelle Browserabnahme offen, Nachverfolgung M8.
- **Ziel / Umfang:** Hierarchie, Auswahl, rekursive Zähler, Breadcrumb, Kontextmenü, Anlegen/Umbenennen/Löschen und Themen umhängen; nur Themen ohne direkt zugeordnete Dokumente und ohne Unterthemen dürfen gelöscht werden.
- **Ausgeschlossen:** Dokument-Drag-and-drop, ZIP-Export und Datenpersistenz; Spezialansicht wartender Transkripte gehört zu M7.
- **Abhängigkeiten:** M1, Demodaten.
- **Akzeptanz:** Baum und Dokumentliste synchron; Systemknoten geschützt, Zyklen verhindert; Dialoge und Menü mit Escape bedienbar. Visuelle Abnahme bleibt offen.
- **Technische DoD / Tests:** Baum-, Auswahl-, Dialog-, Schutz- und Löschtests; [M2-Vergleich](architecture/M2-DESIGNVERGLEICH.md); Browserabnahme in M8.
- **Risiken:** native Drop-Markierung und Scrollverhalten sowie Browser-/Screenreader-Verhalten der seit Q1 implementierten, automatisiert getesteten Tastaturalternative; in M8 gesondert prüfen und beheben bzw. Entscheidung dokumentieren.

### M3 – Listen- und Kachelansicht
- **Status:** technisch umgesetzt; visuelle Browser-/Screenreader-Abnahme offen.
- **Ziel / Umfang:** vorhandene Liste erweitern, Kacheln und Umschaltung, direkte Unterthemen als Ordner, Metadaten, Status, Vorschau und leere Zustände anhand des Originals; Demo-Vorschauen getrennt von echten Dateien.
- **Ausgeschlossen:** Auswahl, Sortierung, Zoom und Dokument-Drop aus M4; Suche, Viewer und echte VIA.Docs-Vorschauen.
- **Abhängigkeiten:** M1, M2; Komponenten für spätere Aktionen nur sichtbar, wenn klar deaktiviert.
- **Akzeptanz:** Liste und Kacheln zeigen die passenden Dokumente und Unterordner; leere Themen und Navigation entsprechen dem Referenzumfang; bestehende Themenauswahl funktioniert.
- **Technische DoD:** UI-DoD, datentypisierte Feature-Komponenten und CSS Modules; keine neue Datenanbindung.
- **Tests:** beide Ansichten, Wechsel, Unterordner, leere Themen und Metadaten; Browser- und Referenzvergleich.
- **Nachweise:** [M3-Designvergleich](architecture/M3-DESIGNVERGLEICH.md); 44 bestehende und 10 neue Testfälle. `npm run check` und `git diff --check`: Exit 0.
- **Risiken:** synthetische Vorschauen, Unterschiede zwischen Demodaten und Original, Performance; Browser-/Screenreader-/Zoom- und visuelle Abnahme offen.

### M4 – Auswahl, Sortierung, Zoom und Dokumentverschiebung
- **Status:** technisch umgesetzt; native Maus-DnD-, visuelle Browser- und Screenreader-Abnahme ausdrücklich offen.
- **Ziel / Umfang:** Klick, Checkbox, Strg/Meta-/Shift-Auswahl, Alle/Keine; Datum/Titel sortieren, Kachelzoom, Dokumente auf Thema/Ordner oder per Dialog verschieben, Undo.
- **Ausgeschlossen:** Volltext, Tagverwaltung, Viewer und echte Persistenz.
- **Abhängigkeiten:** M2, M3; Dialog-/Toast-Bausteine bei Bedarf.
- **Akzeptanz:** Mehrfachauswahl und Anker bleiben konsistent; Sortierung und Zoom wirken wie im Original; Verschieben und Rückgängig erhalten alle ursprünglichen Zuordnungen.
- **Technische DoD:** UI-DoD; abgeleitete Auswahl/Ansicht ohne redundante Datenkopien.
- **Tests:** Auswahlbereiche, fehlendes Datum, Sortierung, Zoom, Mehrfach-Drop und Undo; echte Browser-Drop-Prüfung.
- **Nachweise:** [M4-Designvergleich](architecture/M4-DESIGNVERGLEICH.md), gemeinsame Reducer-/Hook-Regeln und gezielte Interaktionstests. `npm run check`: Exit 0, 87 erfolgreiche Tests (55 bestehende + 32 neue), Produktionsbuild grün; Diff-Prüfungen Exit 0.
- **Risiken:** Browser-Drop-Events, Fokus, Kontrast, 200 % Zoom und Screenreader-Verhalten offen. Erneuter IAB-Zugriff auf lokale Vite-Demo scheiterte mit `ERR_CONNECTION_TIMED_OUT`; kein nativer Maus-DnD-Nachweis.

### M5 – Suche, Filter und Tags
- **Status:** M5-A, M5-B und M5-C technisch umgesetzt; visueller und funktionaler Gesamtvergleich bleibt in M8 offen.
- **Ziel / Umfang:** Titel-/Volltextmodus mit Phrasen, Original-Teilstringsuche und Relevanz; Zeitraum, Voreinstellungen, Tag-UND und Themenbereich; Tags definieren und einzeln/mehrfach zuweisen.
- **Ausgeschlossen:** PostgreSQL-Suche, KI-Suche und produktive API.
- **Abhängigkeiten:** M2–M4; Trefferseite wird in M6 genutzt.
- **Akzeptanz:** kombinierte Filter, Treffer, Hervorhebung und Tagzuordnung entsprechen der Demo; Suchergebnisse in verfügbaren Ansichten.
- **Technische DoD:** UI-DoD; reine Such- und Filterlogik getrennt von Darstellung.
- **Tests:** Normalisierung, Phrasen, Filterkombinationen, Relevanz, keine Treffer, Tagpflege und Mehrfachzuweisung.
- **M5-A:** Reine Such-/Filterfunktionen und typisierter Ergebnisvertrag unter `frontend/src/features/search/`; stabile Tag-IDs mit zentralen Demodefinitionen, optionale Transkripte mit Seiten und Bearbeitungsstatus. Titel und Dateiname zählen pro Begriff einmal +10; Volltext addiert nicht überlappende Vorkommen. Trefferseite ist die erste Seite des ersten Suchbegriffs mit Texttreffer gemäß KU. Leere Suche liefert alle gefilterten Dokumente; Ergebnisreihenfolge bleibt unverändert, Relevanzsortierung ist separat und stabil. Ungültige Datumsfilter oder unbekannter Themenbereich liefern keine Treffer; Datums-Presets erhalten einen Stichtag, Schaltjahr-Rollover folgt `setFullYear` in UTC.
- **M5-A-Nachweise:** `npm run check` Exit 0, 110 Tests einschließlich 13 neuer Such-/Filterfälle, TypeScript, Lint, Stylelint, Prettier und Produktionsbuild erfolgreich; `git diff --check` Exit 0. M5-B muss dieselbe gefilterte Ergebnis-ID-Reihenfolge für Ansichten, Auswahl und Drag-and-drop nutzen; M5-C übernimmt veränderbare Tagdefinitionen und Zuweisung.
- **M5-B:** Suchfeld mit Titel-/Volltextmodus und `/`-Tastenkürzel; Datums-, Tag- und Themenfilter; Ergebnisdarstellung in Liste und Kacheln einschließlich Trefferseite und sicherer Hervorhebung. Eine abgeleitete sortierte Ergebnismenge bestimmt Anzeige, Zähler und Auswahl; bei Kriterienwechsel werden Auswahl und Anker zurückgesetzt. Dokumentverschiebung und Undo bleiben auch bei themenübergreifenden Treffern verfügbar. Direkte Unterthemen werden bei aktiver Suche oder Filterung wie im Original ausgeblendet.
- **M5-B-Nachweise:** `npm run check` Exit 0: 120 Tests, darunter 10 neue Integrationstests für Suche, Filter, Ansichten, Sortierung, Auswahl, Verschiebung und Undo sowie Tastaturbedienung; TypeScript, Lint, Stylelint, Formatprüfung und Produktionsbuild erfolgreich. Der lokale Vite-Server startete, der integrierte Browser meldete jedoch `ERR_CONNECTION_TIMED_OUT`. M5-C ergänzt veränderbare Tagdefinitionen und Zuweisung.
- **M5-C:** Zentrale Tagdefinitionen im Anwendungsspeicher; Namen, Beschreibung und Farbe verwalten; stabile IDs, eindeutige nichtleere Namen, gültige Farben und bestätigtes Löschen. Einzel- und Mehrfachzuweisung mit gemischtem Zustand. Beim Entfernen eines aktiven Filtertags werden Definition, Zuordnungen und Filter bereinigt. Tagänderungen erhalten das Verschiebe-Undo und entfernen ausgeblendete Dokumente aus Auswahl und Anker.
- **M5-C-Abweichungen:** Tag-Picker und -Verwaltung als modal bedienbare Dialoge statt KU-Popover beziehungsweise allgemeiner Einstellungen; Löschbestätigung und Validierung ergänzen den Originalablauf. Chips nutzen farbige Ränder und Punkte mit dunkler Schrift.
- **M5-C-Nachweise:** 8 neue Tests für Tagpflege, Validierung, Einzel- und gemischte Mehrfachzuweisung, Filter, Anzeige, Fokus und Undo. npm run check: 128 Tests einschließlich Produktionsbuild. Der lokal gestartete Vite-Server war im integrierten Browser nicht erreichbar (ERR_CONNECTION_TIMED_OUT).
- **Risiken:** Änderungen bestehen nur im Demo-Arbeitsspeicher; Browser-/Screenreader-, native Drag-and-drop- und visuelle Abnahmen bleiben offen. Demo-Teilstringsuche ist keine Datenbanksuche.

### M6 – Viewer und Transkript-Editor
- **Status:** geplant.
- **Ziel / Umfang:** Overlay/Tab, Trefferseite, Dokumentnavigation, Viewer-Metadaten und Tags, Demo-Zoom, seitenweiser Editor, Suchen/alle ersetzen, Synchronisierung und Splitter.
- **Ausgeschlossen:** dauerhafte Speicherung und echte VIA.Docs-Verbindung; Import-/Exportabläufe aus M7.
- **Abhängigkeiten:** M3–M5; M7 ergänzt importierte Dateien.
- **Akzeptanz:** Dokumente öffnen im gewählten Modus; Bearbeitung, Seitensprung und Navigation funktionieren mit Demo-/lokalen Daten; Grenzen des Browser-PDF-Viewers sind sichtbar dokumentiert.
- **Technische DoD:** UI-DoD, Ressourcen-/Objekt-URL-Lebensdauer und Tastaturführung geprüft.
- **Tests:** Öffnungsmodus, Trefferseite, Navigation, Bearbeitung, Ersetzen, Escape; Browserprüfung für PDF, Scrollen und Tabs.
- **Risiken:** native PDF-Viewer erlauben nicht in jedem Browser vollständige Synchronisation oder Zoomsteuerung.

### M7 – Upload, Downloads, Dialoge und Einstellungen
- **Status:** geplant.
- **Ziel / Umfang:** lokale PDF-/ZIP-/MD-/TXT-Importe mit Bericht, Konfliktbehandlung und wartenden Transkripten; gleiche Dateinamen zulassen, unklare Transkriptzuordnungen zur Entscheidung stellen und erfolgreiche Teile von Importen übernehmen; Einzel-/ZIP-Download inkl. `Transkripte.md`; Einstellungen und Demo-Reset.
- **Ausgeschlossen:** Word-Einlesen, Backend, produktive Dateien und Datenbank. Word zeigt wie im Original einen Hinweis.
- **Abhängigkeiten:** M2–M6; Dialog-/Toast-Grundbausteine dürfen früher entstehen.
- **Akzeptanz:** Datei-Drop und Button-Import, Zuordnung nach Dateiname vor/nach PDF, Konfliktabfrage, Berichte und Downloads funktionieren lokal wie im Original; Einstellungen sind bedienbar.
- **Technische DoD:** UI-DoD, Import-/Exportlogik gekapselt, Objekt-URLs freigegeben.
- **Tests:** Parser, Duplikate, Zuordnung, Konflikte, ZIP, Teilerfolge und Reset; Dateien, Dateidialog und Downloads im Browser.
- **Risiken:** ZIP-Unterstützung, beschädigte Dateien, heuristische Seitenzählung und Ressourcenverbrauch.

### Q8 – Themenlöschung an KU-Regel anpassen
- **Status:** technisch umgesetzt; Browser-, Screenreader- und Sichtabnahmen offen.
- **Ziel / Umfang:** Nur existierende, nicht geschützte Themen ohne direkt zugeordnete Dokumente und ohne Unterthemen löschen. Auch leere Unterthemen verhindern das Löschen ihres Elternknotens; keine Teilbaumlöschung oder Umzuordnung von Dokumenten.
- **Ausgeschlossen:** Papierkorb, Backend und weitere Funktionen.
- **Abhängigkeiten:** M2 und bestehende Dokumentzuordnungen.
- **Akzeptanz:** Gemeinsame Fachprüfung für Dialog und Löschung, erneute Prüfung bei der Zustandsänderung; gesperrte Löschung mit verständlicher Begründung, Bestätigung und Fokusführung für leere Themen. Systemthemen bleiben geschützt; Abbruch und Löschung ändern keine Dokumentzuordnungen.
- **Tests:** Fachtests für fehlende und geschützte Knoten, Dokumente, Unterthemen, beide Hindernisse, leere Knoten und nachträgliche Belegung; Dialog- und Integrationstests für Sperre, Escape, Abbruch und Fokus. Bestehende Tests zur früheren Teilbaumlöschung an die neue Regel angepasst.
- **Nachweise:** npm run check Exit 0; 131 Tests, davon 3 neue Fach-/Dialogtests, sowie Format-, TypeScript-, ESLint-, Stylelint- und Produktionsbuild-Prüfung erfolgreich. Bestehende Tests für den früheren Löschablauf wurden an die verbindliche Regel angepasst.
- **Risiken:** Browser-/Screenreader-Verhalten und visuelle Abnahme sind nicht durch jsdom-Tests nachgewiesen.

### Q9 – Papierkorb und Wiederherstellung planen
- **Status:** geplant.
- **Ziel / Umfang:** Papierkorb und Wiederherstellung gemäß KU-Entscheidung vorsehen; Umfang, Aufbewahrung und Zusammenspiel mit der Themenlöschung fachlich klären.
- **Ausgeschlossen:** Implementierung in M5-B.
- **Abhängigkeiten:** abgestimmte Löschregeln und spätere Persistenzentscheidung.
- **Akzeptanz / Tests:** erst nach Festlegung von Lebenszyklus und Zuständigkeiten konkretisieren.
- **Risiko:** Wiederherstellbarkeit ist derzeit nicht implementiert.

### M8 – Visueller und funktionaler Gesamtvergleich
- **Status:** geplant; enthält ausdrücklich die offenen visuellen Abnahmen von M1 und M2.
- **Ziel / Umfang:** Inventar zeilenweise prüfen, Referenzzustände bei gleicher Fenstergröße vergleichen, M1-/M2-Abnahmen nachholen, Abweichungen und Tastaturalternative zur Themenverschiebung prüfen.
- **Ausgeschlossen:** neue Produktfeatures, produktive Anbindung und eigenmächtige Neugestaltung.
- **Abhängigkeiten:** M1–M7.
- **Akzeptanz:** Sichtprüfung durch zuständige Person mit dokumentierter Umgebung und Ergebnissen; wichtige Originalabläufe geprüft; jede Abweichung behoben oder ausdrücklich entschieden und offen ausgewiesen.
- **Technische DoD:** UI-DoD einschließlich dokumentierter visueller Abnahme; kein Ersatz durch bloßen Quellenvergleich.
- **Tests:** `npm run check`, Browser-Szenarien für Ansichten, Themen, Drag-and-drop, Suche, Viewer, Import/Export und Tastatur; Screenshotvergleich soweit verfügbar.
- **Risiken:** Browser- und PDF-Viewer-Unterschiede, native Dialoge, fehlende zulässige Vergleichsumgebung.

## Phase B – Spätere Datenanbindung und Betrieb

### D1 – MiniDocs-API und Verträge
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** ASP.NET Core (.NET 10) API-Grundlage, dokumentierte Verträge und Fehlerantworten für die MiniDocs-Schicht; Dienste und Datenzugriff trennen.
- **Ausgeschlossen:** Änderung von VIA.Docs, produktiver Zugriff und unbestätigte Endpunkte als Tatsachen festschreiben.
- **Abhängigkeiten:** Architektur- und Sicherheitsentscheidungen; Abstimmung mit D2–D4.
- **Akzeptanz:** Verträge für die benötigten Funktionen prüfbar; Eingaben, Ausgaben, Fehler und Authentifizierungsgrenzen dokumentiert.
- **Technische DoD:** allgemeine und API-DoD, geeignete .NET-Build-/Testbefehle festlegen.
- **Tests:** Vertrags-, Validierungs-, Fehler- und Integrationstests mit kontrollierten Abhängigkeiten.
- **Risiken:** tatsächliche API-Verträge und Hosting noch offen.

### D2 – PostgreSQL-Modell und Migrationen
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** Themen, Dokumentmetadaten, Tags und seitenweise Transkripte samt Relationen und versionierten Migrationen nach abgestimmtem Modell.
- **Ausgeschlossen:** Änderungen an bestehenden Datenbanken; unbestätigte Tabellenentwürfe ungeprüft übernehmen.
- **Abhängigkeiten:** D1-Vertragsabstimmung; isolierte freigegebene Testdatenbank.
- **Akzeptanz:** Schema, Systemknoten, Zuordnung und Integritätsregeln nachvollziehbar; Migration und Rückweg bzw. Wiederherstellung nachgewiesen.
- **Technische DoD:** Datenbank-DoD inklusive Backup/Restore und Datenintegrität.
- **Tests:** Migration/Rollback bzw. Restore, Constraints, Beziehungen und Konfliktfälle.
- **Risiken:** Datenverlust bei Migration, Dateinamenkollisionen und noch offene Suchstrategie.

### D3 – VIA.Docs-V1.2-Client
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** serverseitige, typisierte VIA.Docs-Clientgrenze für später benötigte Datei-/Vorschauoperationen nach verifiziertem Vertrag.
- **Ausgeschlossen:** Änderungen am VIA.Docs-Server; produktiver Zugriff ohne gesonderten Testauftrag.
- **Abhängigkeiten:** D1; tatsächliche VIA.Docs-Schnittstellen und Testumgebung klären; D4 für authentifizierte Integration.
- **Akzeptanz:** bestätigte Verträge, Fehler, Timeouts und IDs kapseln; keine Credentials im Browser.
- **Technische DoD:** API-DoD und dokumentierte Fehlerbehandlung; Secrets serverseitig.
- **Tests:** Vertrags-/Negativtests mit Stubs, später freigegebene Integrationstests.
- **Risiken:** unbekannte API-Details, Previewgrößen und Ablauf bei Nichtverfügbarkeit.

### D4 – Anwendungskonto, Einladung und JWT
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** bestehendes VIA.Docs-Einladungs-/Application-Verfahren für die MiniDocs-Serverintegration verifizieren; JWT-Bezug, sichere Konfiguration und Erneuerung nach tatsächlichem Vertrag.
- **Ausgeschlossen:** neue VIA.Docs-Registrierung erfinden, Zugangsdaten ins Frontend legen oder produktive Konten ohne Auftrag verwenden.
- **Abhängigkeiten:** D1, D3-Vertragsklärung; freigegebene Testidentität und Geheimnisverwaltung.
- **Akzeptanz:** nachvollziehbarer Einladungs- und Tokenablauf mit minimalen Rechten; keine Secrets in Logs, Git oder Frontend.
- **Technische DoD:** API-/Sicherheits-DoD; Betriebsverantwortung und Rotation dokumentiert.
- **Tests:** erfolgreiche und ungültige/abgelaufene Tokenfälle, Berechtigungsfehler und sichere Konfiguration in Testumgebung.
- **Risiken:** reale Rollen, Tokenlebensdauer und Registrierung noch ungeklärt.

### D5 – PDF-Upload, Download und Vorschau
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** MiniDocs-API verbindet PDF-Upload, Dateiabruf und Vorschauen mit VIA.Docs und speichert zugehörige Referenzen/Metadaten.
- **Ausgeschlossen:** Änderung von VIA.Docs, produktive Dateien oder neue Viewer-Funktionen außerhalb des vereinbarten Umfangs.
- **Abhängigkeiten:** D1–D4; M7 für den vollständigen UI-Importfluss.
- **Akzeptanz:** Test-PDF kann hochgeladen, abgerufen und mit bestätigter Vorschau angezeigt werden; Teilfehler hinterlassen nachvollziehbaren Zustand.
- **Technische DoD:** API- und Datenbank-DoD; Fehler- und Wiederanlaufregel für zwei Systeme vereinbart.
- **Tests:** End-to-End mit Testdateien, fehlgeschlagener Upload, nicht verfügbare Vorschau, Zugriffs- und Wiederholungsfälle.
- **Risiken:** inkonsistente Metadaten und Dateien bei Teilausfällen, Größen- und Vorschaugrenzen.

### D6 – Themen-, Tag- und Metadatenpersistenz
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** dauerhafte Themenhierarchie, Tagzuordnung und Dokumentmetadaten über MiniDocs-API und PostgreSQL; Frontend an die vereinbarten Grenzen anschließen.
- **Ausgeschlossen:** VIA.Docs-Serveränderungen, Suchindex und Transkriptbearbeitung aus D7.
- **Abhängigkeiten:** D1, D2; M2 und M5 für zugehörige UI-Abläufe.
- **Akzeptanz:** Ändern, Neuladen und erneutes Lesen erhalten Daten; Systemknoten, Lösch- und Zuordnungsregeln bleiben gewahrt.
- **Technische DoD:** API- und Datenbank-DoD, relevante Verträge und Fehlerzustände dokumentiert.
- **Tests:** CRUD und Integrität, Reload, Systemschutz, Konflikte und API-/DB-Integration.
- **Risiken:** konkurrierende Änderungen und Atomarität; tatsächlicher Einzelnutzerumfang klären.

### D7 – Transkripte und Volltextsuche
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** seitenweise Transkriptpersistenz, Zuordnung per Dateiname einschließlich wartender Transkripte, Suche und Filter über die MiniDocs-API; Suchverhalten gegen M5 abgleichen.
- **Ausgeschlossen:** KI-Transkription, semantische Suche, automatische Scan-Zerlegung und ungeprüfte Performanceversprechen.
- **Abhängigkeiten:** D1, D2, D6; M5–M7 für vollständige UI-Flüsse; D5 für Verknüpfung mit echten PDFs.
- **Akzeptanz:** bearbeitete Transkripte bleiben nach Reload erhalten; wartende Zuordnung und Konflikte sind definiert; Suchtreffer mit Seite und vereinbarten Filtern reproduzierbar.
- **Technische DoD:** API- und Datenbank-DoD; Suchsemantik und Indexstrategie ausdrücklich entscheiden.
- **Tests:** Import-/Zuordnungskonflikte, Transkriptbearbeitung, Suche/Filter, Sonderzeichen und API-/DB-Integration.
- **Risiken:** Abweichung zwischen Original-Teilstringsuche und Datenbanksuche; Dateinamenkollisionen.

### D8 – Integrations-, Sicherheits- und Betriebstests
- **KU-Vorgabe:** sicheren Zugriff von unterwegs vorsehen; Authentifizierung, Betriebsmodell und Absicherung vor Umsetzung konkretisieren.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** integrierte Testumgebung, Sicherheits- und Fehlerfallprüfung, Betriebsdokumentation, Backup/Restore, Monitoring und Wiederanlauf für die beteiligten Systeme.
- **Ausgeschlossen:** Änderung von VIA.Docs, unautorisierte Produktivtests und automatische Produktivfreigabe.
- **Abhängigkeiten:** D1–D7 und M8; freigegebene Testumgebung, Testidentität und Zuständigkeiten.
- **Akzeptanz:** dokumentierter Ende-zu-Ende-Nachweis mit Testdaten, Rechte-/Fehlerfällen, Wiederherstellung und Wiederanlauf; offene Risiken und Betriebsverantwortung benannt.
- **Technische DoD:** allgemeine, API-, Datenbank- sowie Deployment-/Betriebs-DoD.
- **Tests:** Integrations- und Sicherheitstests, Ausfall-/Timeoutfälle, Backup/Restore und Wiederanlauf; keine produktiven Zugriffe ohne gesonderten Testauftrag.
- **Risiken:** Verfügbarkeit externer Testsysteme, übergreifende Datenkonsistenz und unklare Zuständigkeit für VIA.Docs-Backups.

## Q3 – Unabhängige Auditbefunde und Qualitätsregeln

- **Status:** technisch umgesetzt; Browser-/Screenreader-Abnahmen offen.
- **Ziel/Umfang:** Statusstyles in App-eigenes CSS Module verlegen, Callback-Test korrekt benennen, Wurzel-Löschfokus testen und Reviewregeln präzisieren.
- **Ausgeschlossen:** M3, neue Libraries, globale Formatierung und pauschale Refactorings.
- **Abhängigkeiten:** Q1/Q2 und konkrete Audit-Arbeitsaufträge.
- **Akzeptanz/DoD:** keine fachfremde Status-CSS-Abhängigkeit; Testaussage entspricht Assertions; Wurzel-Fallback auf „Nicht zugeordnet“ belegt; allgemeine DoD und Standards erfüllt.
- **Tests/Risiken:** bestehende Tests plus gezielter Wurzel-Löschtest; jsdom ersetzt keine Browser-/Screenreader-Abnahme.

## Q4 – Einheitliche Frontend-Formatierung

- **Status:** technisch abgeschlossen; Einführung durch aktuellen Auftrag freigegeben.
- **Ziel/Umfang:** Prettier 3.10.1, definierter Frontendumfang, einmalige Formatierung und verpflichtendes `format:check` in `npm run check`.
- **Ausgeschlossen:** fachliche Änderungen, Refactorings, typisierte CSS Modules, M3 und Formatierung lokaler Originalreferenzen/generierter Dateien.
- **Abhängigkeiten:** Engineering-Standards, `.editorconfig` und vorhandene ESLint-/Stylelint-Prüfungen.
- **Akzeptanz/DoD:** Konfiguration und Ignorierregeln dokumentiert; Formatcheck, CSS-Lint, bestehende Tests und Build erfolgreich; Quellcode-Diff ausschließlich durch Prettier erzeugt.
- **Tests/Risiken:** 21 bestehende Tests; Abgleich der 18 geänderten Quellcode-/Bestandskonfigurationsdateien mit Prettier-Ausgabe des vorherigen Git-Stands. Formatter ersetzt keine fachliche, visuelle oder Accessibility-Abnahme.

## Q5 – Typisierte CSS Modules für npm run check evaluieren

- **Status:** geplant, Freigabe vor Installation erforderlich.
- **Ziel/Umfang:** konkrete Deklarationen generieren und ihre Aktualität vor TypeScript im Check prüfen; z. B. den in Q2 bewerteten Generator im begrenzten Probeumfang testen.
- **Ausgeschlossen:** bloßes Editorplugin als CI-Nachweis, neue Styling-Systeme und automatische Architekturfreigabe.
- **Abhängigkeiten:** bestehende Vite-/TypeScript-Konfiguration, Q2-Werkzeugbewertung.
- **Akzeptanz/DoD:** fehlende Klasse und veraltete/fehlende Deklaration lassen den Check fehlschlagen; gültige CSS-Modules-Imports und Vite-Namenskonvention funktionieren; Generator-/Commitstrategie und Kompatibilität dokumentiert.
- **Tests/Risiken:** positive und negative CLI-Proben plus bestehende Tests; veraltete generierte Typen oder abweichende Namenskonventionen dürfen keine falsche Sicherheit erzeugen.

## Q6 – M2-Dropmarkierung und Testpräzision

- **Status:** technisch umgesetzt; Browser-/Screenreader- und visuelle Abnahmen offen.
- **Ziel/Umfang:** gültige Themenzeilen beim Maus-DnD im KU-Stil markieren; ungültige Ziele mit vorhandener `canMoveTopic`-Regel ausschließen; Feedback bei Verlassen, Escape, Drag-End, Drop und Fensterfokusverlust entfernen. Zeilen- und Wurzelablage teilen eine interne Sitzung in `useTopicDrag`; lokale Quell-ID maßgeblich, fehlende/abweichende Nutzdaten und Drops ohne aktive Sitzung abweisen. Tastaturzielwahl bleibt erhalten.
- **Ausgeschlossen:** Q5, M3, neue Libraries, allgemeine Refactorings und Backend.
- **Abhängigkeiten:** M2, Q1 und Original-CSS `.trow.drop`.
- **Akzeptanz/DoD:** `dragover` nutzt lokale Quell-ID statt geschütztem `getData`; keine gültige Markierung für Systemknoten, Selbstziel oder Nachfahren. Drop verbraucht die Sitzung auch bei Ablehnung, prüft Nutzdaten und `canMoveTopic` vor dem Move-Callback; gesondertes `onReject` erhält die Statusmeldung ohne ungültigen Move. Root-Drop verwendet denselben Vertrag. Status-/Statistiktests behaupten nur tatsächlich belegtes Verhalten; Hook- und Baumverträge dokumentiert.
- **Tests/Nacharbeit:** alle bisherigen 31 Fälle erhalten und auf den expliziten Vertrag angepasst; 13 zusätzliche Fälle ergeben 44 erfolgreiche Tests. Nachweise für widersprüchliche/fehlende Nutzdaten, fehlenden Dragstart, alle Abbruchpfade, ungültige Ziele ohne Move-Callback, Einmalverbrauch, Root-Zustandsänderung/Abweisung und Listener-Entfernung beim Unmount. Statistik prüft 6 Dokumente und 13 Seiten. Synthetischer Drop ist ausdrücklich kein Browser-E2E-Test.
- **Formatierung/Validierung:** vor Änderungen `npm run format:check`: Exit 0; beide JS-Konfigurationen im Arbeitsverzeichnis und Git mit LF (`git ls-files --eol`: `i/lf w/lf`), CRLF-Verdacht im Repository nicht bestätigt. `.gitattributes` ergänzt `*.js text eol=lf` für reproduzierbare Checkouts; keine Normalisierung der bereits korrekten Dateien erforderlich. Abschließend `npm run format:check`, `npm run check` (TypeScript, ESLint, Stylelint, 44 Tests und Produktionsbuild) und `git diff --check`: jeweils Exit 0.
- **Risiken/Abnahmen:** kein neuer Browser-Smoketest durchgeführt, da der bisherige Browserzugriff durch Timeout bzw. fehlende URL-Richtlinienunterstützung blockiert war. Die Blockierung wurde nicht umgangen und in Q6 nicht erneut geprüft. Maus-DnD, Tastaturalternative, Dialogfokus, schmale Fenster, 200 % Zoom und geringe Dialoghöhe bleiben real im Browser zu prüfen; keine M8-Abnahme.
## Q7-FIX – Baum-Expansion nach Dokument-Drop

- **Status:** technisch umgesetzt; native Browser-, Screenreader- und visuelle Abnahmen bleiben offen.
- **Befund:** Nach Drop-Navigation konnte ein manuell eingeklappter Vorfahr den Öffnungsversuch beim Anlegen eines Unterthemas überlagern; ein gezielter Integrationstest reproduzierte den unsichtbaren neuen Knoten vor der Korrektur (Vitest Exit 1).
- **Umfang:** Ein Aufklappzustand verarbeitet jeden Drop-Navigationsimpuls einmalig; manuelles Ein-/Ausklappen und gezieltes Öffnen für Unterthemen und Themen-Drops greifen auf denselben Zustand zu. Keine Änderung von M5, Q5, Styling oder Dependencies.
- **Akzeptanz/Tests:** Integrationstest deckt verschachtelte Dokument-Drop-Navigation, manuelles Einklappen, gewöhnlichen Rerender, Kontextmenü-Neuanlage und anschließendes manuelles Auf-/Zuklappen ab. Bestehende Tests zu Baum, Fokus und DnD bleiben erhalten. Synthetische Drop-Ereignisse sind kein nativer Browsernachweis.
- **Risiken:** browserabhängige Drag-/Fokus- und Screenreader-Abnahmen sind weiterhin offen.
- **Technischer Nachweis:** `npm run check` Exit 0; 97 Tests einschließlich des neuen Regressionstests sowie Produktionsbuild erfolgreich. `git diff --check` und `git diff --cached --check` jeweils Exit 0.
