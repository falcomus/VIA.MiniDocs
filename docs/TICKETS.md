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
- **Ziel / Umfang:** Hierarchie, Auswahl, rekursive Zähler, Breadcrumb, Kontextmenü, Anlegen/Umbenennen/Löschen und Themen umhängen; Dokumente gelöschter Teilbäume nach „Nicht zugeordnet“.
- **Ausgeschlossen:** Dokument-Drag-and-drop, ZIP-Export und Datenpersistenz; Spezialansicht wartender Transkripte gehört zu M7.
- **Abhängigkeiten:** M1, Demodaten.
- **Akzeptanz:** Baum und Dokumentliste synchron; Systemknoten geschützt, Zyklen verhindert; Dialoge und Menü mit Escape bedienbar. Visuelle Abnahme bleibt offen.
- **Technische DoD / Tests:** Baum-, Auswahl-, Dialog-, Schutz- und Löschtests; [M2-Vergleich](architecture/M2-DESIGNVERGLEICH.md); Browserabnahme in M8.
- **Risiken:** Drop-Markierung, Scrollverhalten und fehlende Tastaturalternative zum Umhängen; in M8 gesondert prüfen und beheben bzw. Entscheidung dokumentieren.

### M3 – Listen- und Kachelansicht
- **Status:** geplant – nächstes Ticket.
- **Ziel / Umfang:** vorhandene Liste erweitern, Kacheln und Umschaltung, direkte Unterthemen als Ordner, Metadaten, Status, Vorschau und leere Zustände anhand des Originals; Demo-Vorschauen getrennt von echten Dateien.
- **Ausgeschlossen:** Auswahl, Sortierung, Zoom und Dokument-Drop aus M4; Suche, Viewer und echte VIA.Docs-Vorschauen.
- **Abhängigkeiten:** M1, M2; Komponenten für spätere Aktionen nur sichtbar, wenn klar deaktiviert.
- **Akzeptanz:** Liste und Kacheln zeigen die passenden Dokumente und Unterordner; leere Themen und Navigation entsprechen dem Referenzumfang; bestehende Themenauswahl funktioniert.
- **Technische DoD:** UI-DoD, datentypisierte Feature-Komponenten und CSS Modules; keine neue Datenanbindung.
- **Tests:** beide Ansichten, Wechsel, Unterordner, leere Themen und Metadaten; Browser- und Referenzvergleich.
- **Risiken:** synthetische Vorschauen, Unterschiede zwischen Demodaten und Original, Performance.

### M4 – Auswahl, Sortierung, Zoom und Dokumentverschiebung
- **Status:** geplant.
- **Ziel / Umfang:** Klick, Checkbox, Strg/Meta-/Shift-Auswahl, Alle/Keine; Datum/Titel sortieren, Kachelzoom, Dokumente auf Thema/Ordner oder per Dialog verschieben, Undo.
- **Ausgeschlossen:** Volltext, Tagverwaltung, Viewer und echte Persistenz.
- **Abhängigkeiten:** M2, M3; Dialog-/Toast-Bausteine bei Bedarf.
- **Akzeptanz:** Mehrfachauswahl und Anker bleiben konsistent; Sortierung und Zoom wirken wie im Original; Verschieben und Rückgängig erhalten alle ursprünglichen Zuordnungen.
- **Technische DoD:** UI-DoD; abgeleitete Auswahl/Ansicht ohne redundante Datenkopien.
- **Tests:** Auswahlbereiche, fehlendes Datum, Sortierung, Zoom, Mehrfach-Drop und Undo; echte Browser-Drop-Prüfung.
- **Risiken:** Browser-Drop-Events, nicht sichtbare Auswahl und versehentliche Verschiebung.

### M5 – Suche, Filter und Tags
- **Status:** geplant.
- **Ziel / Umfang:** Titel-/Volltextmodus mit Phrasen, Original-Teilstringsuche und Relevanz; Zeitraum, Voreinstellungen, Tag-UND und Themenbereich; Tags definieren und einzeln/mehrfach zuweisen.
- **Ausgeschlossen:** PostgreSQL-Suche, KI-Suche und produktive API.
- **Abhängigkeiten:** M2–M4; Trefferseite wird in M6 genutzt.
- **Akzeptanz:** kombinierte Filter, Treffer, Hervorhebung und Tagzuordnung entsprechen der Demo; Suchergebnisse in verfügbaren Ansichten.
- **Technische DoD:** UI-DoD; reine Such- und Filterlogik getrennt von Darstellung.
- **Tests:** Normalisierung, Phrasen, Filterkombinationen, Relevanz, keine Treffer, Tagpflege und Mehrfachzuweisung.
- **Risiken:** Verwechslung der Demo-Teilstringsuche mit späterer Datenbanksuche; Fokus und Filterzustand.

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
- **Ziel / Umfang:** lokale PDF-/ZIP-/MD-/TXT-Importe mit Bericht, Duplikat-/Konfliktbehandlung und wartenden Transkripten; Einzel-/ZIP-Download inkl. `Transkripte.md`; Einstellungen und Demo-Reset.
- **Ausgeschlossen:** Word-Einlesen, Backend, produktive Dateien und Datenbank. Word zeigt wie im Original einen Hinweis.
- **Abhängigkeiten:** M2–M6; Dialog-/Toast-Grundbausteine dürfen früher entstehen.
- **Akzeptanz:** Datei-Drop und Button-Import, Zuordnung nach Dateiname vor/nach PDF, Konfliktabfrage, Berichte und Downloads funktionieren lokal wie im Original; Einstellungen sind bedienbar.
- **Technische DoD:** UI-DoD, Import-/Exportlogik gekapselt, Objekt-URLs freigegeben.
- **Tests:** Parser, Duplikate, Zuordnung, Konflikte, ZIP, Teilerfolge und Reset; Dateien, Dateidialog und Downloads im Browser.
- **Risiken:** ZIP-Unterstützung, beschädigte Dateien, heuristische Seitenzählung und Ressourcenverbrauch.

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
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** integrierte Testumgebung, Sicherheits- und Fehlerfallprüfung, Betriebsdokumentation, Backup/Restore, Monitoring und Wiederanlauf für die beteiligten Systeme.
- **Ausgeschlossen:** Änderung von VIA.Docs, unautorisierte Produktivtests und automatische Produktivfreigabe.
- **Abhängigkeiten:** D1–D7 und M8; freigegebene Testumgebung, Testidentität und Zuständigkeiten.
- **Akzeptanz:** dokumentierter Ende-zu-Ende-Nachweis mit Testdaten, Rechte-/Fehlerfällen, Wiederherstellung und Wiederanlauf; offene Risiken und Betriebsverantwortung benannt.
- **Technische DoD:** allgemeine, API-, Datenbank- sowie Deployment-/Betriebs-DoD.
- **Tests:** Integrations- und Sicherheitstests, Ausfall-/Timeoutfälle, Backup/Restore und Wiederanlauf; keine produktiven Zugriffe ohne gesonderten Testauftrag.
- **Risiken:** Verfügbarkeit externer Testsysteme, übergreifende Datenkonsistenz und unklare Zuständigkeit für VIA.Docs-Backups.
