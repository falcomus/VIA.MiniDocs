> Historischer Sicherungsstand des Ticketkatalogs vor der Konsolidierung. Aktueller Status und offene Aufgaben: [TICKETS.md](TICKETS.md). Nachweise bleiben hier erhalten.

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
- **Status:** M6-A, M6-B und M6-C technisch umgesetzt; der vollständige KU-Vergleich ist offen. M6 insgesamt bleibt offen.
- **Ziel / Umfang:** Overlay/Tab, Trefferseite, Dokumentnavigation, Viewer-Metadaten und Tags, Demo-Zoom, seitenweiser Editor, Suchen/alle ersetzen, Synchronisierung und Splitter.
- **Ausgeschlossen:** dauerhafte Speicherung und echte VIA.Docs-Verbindung; Import-/Exportabläufe aus M7.
- **Abhängigkeiten:** M3–M5; M7 ergänzt importierte Dateien.
- **Akzeptanz:** Dokumente öffnen im gewählten Modus; Bearbeitung, Seitensprung und Navigation funktionieren mit Demo-/lokalen Daten; Grenzen der PDF-Darstellung sind sichtbar dokumentiert.
- **Technische DoD:** UI-DoD, Ressourcen-/Objekt-URL-Lebensdauer und Tastaturführung geprüft.
- **Tests:** Öffnungsmodus, Trefferseite, Navigation, Bearbeitung, Ersetzen, Escape; Browserprüfung für PDF, Scrollen und Tabs.
- **Risiken:** PDF-Dateien und Browser können abweichende Darstellungs- und Accessibility-Eigenschaften haben.

#### M6-A – Dokumentviewer und Navigation
- **Status:** technisch umgesetzt; vollständige Sicht-, Screenreader- und Zoomabnahme offen.
- **Umfang:** Doppelklick, Enter, Einzelaktion und konfigurierbarer Öffnungsmodus; Overlay mit Metadaten, Tags, Seiten- und Dokumentnavigation, Suchtrefferseite und unabhängigem Demozoom. Modifizierter Doppelklick, Mittelklick und Symbol öffnen einen neuen Tab.
- **Architektur:** Viewer speichert ID, Seitenzahl und bei Öffnung gültigen sortierten ID-/Trefferkontext; Dokumentdaten bleiben im bestehenden Zustand. Eigenständige Tab-URL: ?viewer=tab&document=<ID>&page=<Seite>, optional term. Zugriff anhand stabiler ID; Eingaben werden geprüft.
- **Grenzen:** Der separate Tab fordert aktuelle Dokumentdaten beim zugehörigen Übersichtstab an und überträgt Transkriptoperationen dorthin; Ergebnisreihenfolge und andere Änderungen werden nicht laufend synchronisiert. Synthetische Seiten sind keine PDF-Dateien. Popup-Blockaden sind wegen noopener nicht zuverlässig erkennbar.
- **Browsernachweis:** Integrierter Browser mit lokalem Vite-Server: Overlay per Doppelklick geöffnet, auf Seite 2 gewechselt und per Aktion echten neuen Tab mit dokumentierter URL und Seite 2 geöffnet. Keine vollständige visuelle oder Screenreader-Abnahme.
- **Tests / Nachweise:** npm run check Exit 0, 156 Tests einschließlich 13 neuer Viewer-Fälle; Fokus, Tastatur, Öffnungsmodi, Persistenz, sichere URLs, Trefferseiten, Navigation, Seiten, Zoom, Auswahl und Speicherfehler sowie Produktionsbuild geprüft. Viewer-Zoom nutzt einen Slider; Strg-Mausrad ist nicht implementiert.

#### M6-B – Seitenweiser Transkript-Editor
- **Status:** technisch umgesetzt; M6 insgesamt und M6-C bleiben offen.
- **Umfang und Datenmodell:** Bearbeitbarer mehrzeiliger Text je gültiger Dokumentseitennummer; fehlende Seiten werden bei Eingabe ergänzt. Das bestehende optionale Transkript mit Seiten und edited bleibt maßgeblich; leere Eingaben löschen keine Seite stillschweigend. Die synthetische Dokumentseite ist vom Transkript unabhängig.
- **Speicher und State:** Der Dokument-Reducer des Übersichtstabs ist für Transkripte maßgeblich. Ein separater Viewer überträgt Operationen dorthin und wartet vor der Rückkehr auf Bestätigung; Neuladen oder Schließen des maßgeblichen Tabs kann Änderungen verwerfen. Ein gezielter Transkriptvorgang erhält Tags und Verschiebe-Undo.
- **Suche und Ersetzen:** Dokumentweite, nicht überlappende Teilstringsuche ohne Beachtung von Groß-/Kleinschreibung oder Akzenten; Trefferpositionen beziehen sich auf den Originaltext. Treffer lassen sich seitenübergreifend ansteuern und einzeln ersetzen. Wie im Original ersetzt „Alle ersetzen“ dokumentweit wörtlich und ohne Beachtung der Groß-/Kleinschreibung, jedoch mit Beachtung von Akzenten; eine leere Ersetzung ist zulässig.
- **M5-Integration:** Die bestehende Volltextsuche berücksichtigt den aktuellen Transkripttext unmittelbar. Fällt das geöffnete Dokument aus einem aktiven Filter, bleibt der Viewer darauf geöffnet; Ergebnisliste, Auswahl und Anker werden aktualisiert.
- **Nachweise:** Fach- und Integrationstests prüfen unter anderem Seitennummern, Unicode, Suche/Ersetzung, Seiten- und Dokumentwechsel, aktive Filter, Undo, Tags und den eigenständigen Demo-Tab. Im integrierten Browser wurden Overlay, Bearbeitung, Seitenwechsel, Suche/Ersetzung, erneutes Öffnen und M5-Suche sowie Fokus an Seitengrenze und nach Ersetzen geprüft. Die Unicode-Suche wurde nach der letzten Suchlogikänderung erneut im Browser geprüft.
- **Offen / Risiken:** Der Projektverantwortliche meldet Transkriptbearbeitung über Dokumentwechsel und eine Screenreader-Prüfung als erfolgreich; konkrete Screenreader-Umgebung und vollständige visuelle Abnahme sind nicht dokumentiert. Gleichzeitige Bearbeitung derselben Seite in mehreren Viewern ist nicht konfliktfrei; spätere Texteingaben können frühere überschreiben. Keine dauerhafte Persistenz; M6-C bleibt offen.

#### M6-C – Dokumentdarstellung, Splitter und Synchronisierung
- **Status:** technisch umgesetzt und auf `main`/`origin/main` integriert; formaler Abschluss bleibt wegen des noch nicht nachgewiesenen KU-Vergleichs offen. M6 bleibt offen.
- **Architektur:** [PDF-Entscheidung](architecture/M6-C-PDF-ENTSCHEIDUNG.md). Der Quellenresolver unterscheidet Demo, lokalen PDF-Blob und fehlende Quelle. React-PDF rendert die aktive Seite; die echte Seitenzahl aktualisiert nach Identitätsprüfung die Metadaten. Ein Backend-Adapter ist nicht angeschlossen.
- **Splitter und Navigation:** Startwert 56 %, Grenzen 25–75 %, Pointer und Tastatur; auf schmalen Viewports untereinander. PDF, Navigation und Transkript teilen die aktive Seite. Die PDF-Seite wird eingepasst und bei Zoom innerhalb ihres Bereichs gescrollt; es gibt keine überlagernde Thumbnail-Leiste.
- **Scrollen:** Der KU-Migrationsplan nennt allgemein „synchrones Scrollen“. Für die Demo bleibt die optionale proportionale Kopplung vom Dokument zur aktiven Textarea erhalten. Der Projektverantwortliche hat entschieden: Eine zusätzliche automatische oder abschnittsgenaue Scrollsynchronisierung echter PDFs sowie eine fortlaufende PDF-Ansicht sind für M6-C und M7-A keine Abschlussvoraussetzung; gemeinsame Seitenwahl und separat scrollbar bedienbare Bereiche bleiben maßgeblich. Einzelheiten und Abweichungen stehen in der [PDF-Entscheidung](architecture/M6-C-PDF-ENTSCHEIDUNG.md).
- **Nachweise:** Echte lokale PDFs wurden exemplarisch im Overlay und separaten Tab geprüft. Der Projektverantwortliche bestätigt Seitenkopplung, Zoom und Splitter ohne Flackern, Transkriptbearbeitung über Dokumentwechsel, Suche/Ersetzen, 200 % Browserzoom, schmale Fenster, Tastatur einschließlich Splitter, Hell-/Dunkelmodus und Viewer-Neuladen mit F5. Automatisierte Tests ersetzen keine visuelle oder assistive Abnahme.
- **Offen / Risiken:** Der Projektverantwortliche meldet 200 % Browserzoom und Screenreader-Prüfung als erfolgreich; konkrete Screenreader-Umgebung und Umfang sind nicht dokumentiert. Der KU-Vergleich bei gleicher dokumentierter Fenstergröße bleibt offen. Der Projektverantwortliche hat die manuelle Touch-Prüfung ausdrücklich aus dem aktuellen Abnahmeumfang ausgenommen; sie wurde nicht durchgeführt. Weitere PDF-Fehler- und Layoutfälle bleiben Risiken. PDF.js unterstützt nicht alle PDF-Eigenschaften. Keine Persistenz oder VIA.Docs-Anbindung.

### M7 – Upload, Downloads, Dialoge und Einstellungen
- **Status:** M7-A und die zusätzlich beauftragte fortlaufende PDF-Darstellung M7-B-PDF sind technisch umgesetzt und auf `main` integriert; die formale UI-DoD bleibt offen. Der separate Transkriptdateiimport M7-B-TRANSKRIPT sowie M7-C/D bleiben geplant.
- **PDF-Abnahme aus M6-C:** Lokale PDF-Dateien mit React-PDF im Overlay und eigenständigen Tab prüfen; Laden/Fehler, Seiten- und Dokumentwechsel sowie Ressourcenfreigabe nachweisen. VIA.Docs-Dateiabruf, Berechtigung, Fehler und Ende-zu-Ende-Lebenszyklus folgen erst mit der Backend-Anbindung.
- **Ziel / Umfang:** lokale PDF-/ZIP-/MD-/TXT-Importe mit Bericht, Konfliktbehandlung und wartenden Transkripten; gleiche Dateinamen zulassen, unklare Transkriptzuordnungen zur Entscheidung stellen und erfolgreiche Teile von Importen übernehmen; Einzel-/ZIP-Download inkl. `Transkripte.md`; Einstellungen und Demo-Reset.
- **Ausgeschlossen:** Word-Einlesen, Backend, produktive Dateien und Datenbank. Word zeigt wie im Original einen Hinweis.
- **Abhängigkeiten:** M2–M6; Dialog-/Toast-Grundbausteine dürfen früher entstehen.
- **Akzeptanz:** Datei-Drop und Button-Import, Zuordnung nach Dateiname vor/nach PDF, Konfliktabfrage, Berichte und Downloads funktionieren lokal wie im Original; Einstellungen sind bedienbar.
- **Technische DoD:** UI-DoD, Import-/Exportlogik gekapselt, Objekt-URLs freigegeben.
- **Tests:** Parser, Duplikate, Zuordnung, Konflikte, ZIP, Teilerfolge und Reset; Dateien, Dateidialog und Downloads im Browser.
- **Risiken:** ZIP-Unterstützung, beschädigte Dateien, heuristische Seitenzählung und Ressourcenverbrauch.

#### M7-A – Lokaler PDF-Import und Viewer

- **Status:** M7-A und der nachträgliche M7-A-FIX sind technisch umgesetzt. Der formale Abschluss von M7-A und M6-C bleibt wegen des noch nicht nachgewiesenen KU-Vergleichs offen.
- **M7-A-FIX – Themenauswahl:** Beim KU-Vergleich fehlte im React-Viewer die hierarchische Auswahl neben „Thema“. Das native Auswahlfeld bietet sortierte, eingerückte Themen und „Nicht zugeordnet“. Die bestehende Dokumentverschiebung aktualisiert die Übersicht; verbundene Viewer-Tabs übertragen Änderungen über den bestehenden BroadcastChannel mit Bestätigung. Die Demo speichert Änderungen weiterhin nicht dauerhaft.
- **M7-A-FIX – Browserabnahme vom 25.11.2026:** Der Projektverantwortliche bestätigt 5/5 Prüfungen: Hierarchie, Vorauswahl und Einrückung; Verschieben und Rückkehr zu „Nicht zugeordnet“; Erhalt von PDF-Seite, Zoom, Scrollposition und Transkriptbearbeitung; Themenwechsel zwischen Übersicht und separatem Tab in beiden Richtungen; Tastaturbedienung, Fokusdarstellung und Screenreader-Beschriftung. Die Prüfung wurde vom Projektverantwortlichen durchgeführt, nicht von Codex. Angaben zu Browser und Screenreader-Umgebung liegen nicht vor.
- **M7-A-FIX – Grenzen:** Änderungen bleiben sitzungsgebunden; gleichzeitige Änderungen sind nicht konfliktfrei. Wird ein Zielthema zwischen Auswahl und Verarbeitung entfernt, bleibt die Bestätigung aus und der Viewer meldet dies nach Ablauf der Wartezeit. Eine systematische Prüfung konkurrierender Themenwechsel ist nicht dokumentiert.
- **KU-Vergleich:** Laut Projektverantwortlichem wurde die fehlende Themenauswahl beim Vergleich von vier Screenshots der Übersicht und des Viewers erkannt; deren Korrektur wurde wie oben abgenommen. Gleiche dokumentierte Viewportgrößen und vollständige Hover-, Fokus-, Auswahl- und Scrollzustände sind damit nicht nachgewiesen. Der vollständige Vergleich von Bedienverhalten, Farben, Typografie, Icons und Abständen sowie die zuständige visuelle Gesamtfreigabe bleiben gemäß UI-DoD offen. Freigegebener UI-Feinschliff und die PDF-Architekturentscheidung sind unten beziehungsweise in der PDF-Entscheidung dokumentiert.
- **Umfang:** Lokaler Einzel-/Mehrfachimport per Dateiauswahl oder Datei-Drop in den bestehenden Dokumentzustand; eindeutige IDs und neutrale PDF-Vorschau auch bei identischen Dateinamen. Typ, PDF-Kopf und Dateiende werden auf Plaausibilität geprüft; dies ist keine vollständige Struktur- oder Sicherheitsprüfung.
- **Datenfluss:** PDF-Blobs verbleiben im Arbeitsspeicher. PDF.js ermittelt beim Import die Seitenzahl. React-PDF 10.5.0 rendert die gewählte Seite mit lokalem PDF.js-Worker. Die erste Seite wird bei Sichtbarkeit mit begrenzter Auflösung und maximal zwei parallelen Aufgaben als Miniatur erzeugt; der Blob-identische Speicher-Cache ist sitzungsgebunden. Der separate Tab fordert einen aktuellen Snapshot beim zugehörigen Übersichtstab an und sendet Transkriptoperationen mit Bestätigung zurück. Ohne Quelle nach Neuladen wird ein Fehler gezeigt. Keine dauerhafte Persistenz.
- **Nachweise:** Der Projektverantwortliche bestätigt PDF-Import, Seitenzahl und Miniatur, separaten Viewer-Tab, Seitenkopplung, Zoom und Splitter ohne Flackern, Transkriptbearbeitung über Dokumentwechsel, Suche/Ersetzen, 200 % Browserzoom, schmale Fenster, Tastatur einschließlich Splitter, Hell-/Dunkelmodus und Viewer-Neuladen mit F5. Echte lokale PDFs wurden auch exemplarisch im Overlay angezeigt; eine ungültige Datei wurde beim Import abgewiesen. Dies ist keine vollständige PDF-Abnahme.
- **Offen / Risiken:** Der Projektverantwortliche meldet 200 % Browserzoom, 50–200 % PDF-Zoom ohne Flackern und eine Screenreader-Prüfung als erfolgreich; Screenreader-Umgebung und Umfang sind nicht dokumentiert. Der KU-Vergleich bei gleicher dokumentierter Fenstergröße bleibt offen. Der Projektverantwortliche hat die manuelle Touch-Prüfung ausdrücklich aus dem aktuellen Abnahmeumfang ausgenommen; sie wurde nicht durchgeführt. Weitere reale beschädigte, verschlüsselte und komplexe PDFs bleiben Risiken. PDF.js kann nicht jede PDF-Eigenschaft darstellen; die Anwendung speichert importierte Daten nicht dauerhaft.
- **Restprüfung:** Bei 640 × 360 CSS-Pixeln blieben für den Inhalt im Overlay zunächst nur 33 px; bei schmalem und niedrigem Viewport scrollt nun der gesamte Viewer. Im Browser waren PDF und Transkript danach durch Scrollen erreichbar. Der Projektverantwortliche meldet inzwischen eine erfolgreiche Prüfung bei 200 % Browserzoom. Sechs lokale PDF-Importe und ein Wechsel zwischen Listen- und Kachelansicht wurden exemplarisch geprüft; automatisierte Tests begrenzen aktive Miniaturrenders auf zwei und prüfen Abbruch und Freigabe. Prozessspeicher und belastbare Wartezeiten wurden nicht gemessen.
- **Freigegebener UI-Feinschliff:** Dokumentkacheln verwenden eine reservierte Aktionszeile über der Vorschau statt der überlagernden KU-Aktionen. Tag-Chips haben neutrale Rahmen und behalten ihre Farbpunkte; Status und Undo stehen gemeinsam oberhalb der Übersicht. Der visuelle Gesamtvergleich bleibt offen; 200 % Browserzoom wurden laut Projektverantwortlichem geprüft.
- **Transkript-Seitenkarten:** Alle bekannten Dokumentseiten sind als einzeln bearbeitbare Karten dargestellt; Karten- und Viewer-Seitenwahl teilen den Seitenzustand. Suche und Ersetzen bleiben dokumentweit. Der Projektverantwortliche meldet echte PDFs, Zoom und Screenreader-Prüfung als erfolgreich; die vollständige Browser-Gesamtabnahme bleibt offen.
- **Grenzen:** Die separaten Arbeiten M7-B-PDF, M7-B-TRANSKRIPT und M7-C/D, Backend, IndexedDB und weitere PDF-Viewer-Libraries sind nicht Bestandteil von M7-A.

#### M7-B-PDF – Fortlaufende PDF-Anzeige und seitenweise Synchronisierung
- **Status:** technisch umgesetzt; die beauftragte Browserabnahme wurde vom Projektverantwortlichen am 24.11.2026 mit 5 von 5 Prüfungen erfolgreich bestätigt. Der technische Stand ist auf `main`/`origin/main` integriert; der formale Abschluss bleibt wegen des noch nicht nachgewiesenen KU-Vergleichs offen. Diese Erweiterung ändert nicht die frühere Abgrenzung der Abschlusskriterien von M6-C und M7-A.
- **Umfang:** PDF-Seiten untereinander mit begrenztem Rendering der aktiven Seite und benachbarter Seiten; sichtbare Seite bestimmt Seitenanzeige und aktive Transkriptkarte. Der Schalter steuert das Nachführen der Karte; Seitensteuerung und Transkriptaktionen navigieren zur PDF-Seite. Zoom, Splitter und Transkriptbearbeitung bleiben erhalten.
- **Grenzen:** Keine abschnitts- oder wortgenaue Zuordnung, keine fortlaufenden Demoseiten, keine Persistenz oder Backend-Anbindung. Der ursprünglich ebenfalls als M7-B bezeichnete Transkriptdateiimport bleibt als separate spätere Arbeit unter M7-B-TRANSKRIPT geplant.
- **Browserabnahme vom 24.11.2026:** Der Projektverantwortliche bestätigt mit echtem PDF 5/5 Prüfungen: fortlaufendes Scrollen mit aktiver und nachgeführter Transkriptkarte; abgeschaltete Nachführung; Navigation von Transkriptkarte 4 zur PDF-Seite 4; Zoom bei 70 %, 100 % und 200 % sowie Splitter ohne beanstandetes Flackern oder unerwartete Seitensprünge; separater Viewer-Tab mit Scrollen und Transkriptbearbeitung. Die Prüfung wurde nicht von Codex durchgeführt.
- **Risiken:** Speicherverbrauch und Performance großer PDFs sind nicht systematisch untersucht. Bei unterschiedlichen Seitenformaten können nachträglich korrigierte Platzhalterhöhen die Scrollposition beeinflussen. Der Produktionsbuild meldet ein JavaScript-Chunk über 500 kB; Auswirkungen auf Ladezeiten sind nicht systematisch gemessen. Die manuelle Touch-Prüfung ist durch Entscheidung des Projektverantwortlichen aus dem aktuellen Abnahmeumfang ausgenommen und wurde nicht durchgeführt.
- **Git und Restnachweis:** Commit `160cb340dd81548a4fcb3afc3baf96c532f37501` wurde auf `main` integriert und nach `origin/main` gepusht; Remote-Stand geprüft. `npm run check` Exit 0 mit 216 Tests und Produktionsbuild. Die UI-DoD verlangt weiterhin den Vergleich der Referenzzustände und des Bedienverhaltens bei gleicher dokumentierter Fenstergröße einschließlich Farben, Typografie, Icons, Abständen und Zuständen sowie die visuelle Abnahme. Der Zugriff auf die lokale Referenz über den integrierten Browser wurde durch dessen URL-Sicherheitsrichtlinie blockiert; ein vollständiger Vergleich ist nicht nachgewiesen.

### Q8 – Themenlöschung an KU-Regel anpassen
- **Status:** technisch umgesetzt; Browser-, Screenreader- und Sichtabnahmen offen.
- **Ziel / Umfang:** Nur existierende, nicht geschützte Themen ohne direkt zugeordnete Dokumente und ohne Unterthemen löschen. Auch leere Unterthemen verhindern das Löschen ihres Elternknotens; keine Teilbaumlöschung oder Umzuordnung von Dokumenten.
- **Ausgeschlossen:** Papierkorb, Backend und weitere Funktionen.
- **Abhängigkeiten:** M2 und bestehende Dokumentzuordnungen.
- **Akzeptanz:** Gemeinsame Fachprüfung für Dialog und Löschung, erneute Prüfung bei der Zustandsänderung; gesperrte Löschung mit verständlicher Begründung, Bestätigung und Fokusführung für leere Themen. Systemthemen bleiben geschützt; Abbruch und Löschung ändern keine Dokumentzuordnungen.
- **Tests:** Fachtests für fehlende und geschützte Knoten, Dokumente, Unterthemen, beide Hindernisse, leere Knoten und nachträgliche Belegung; Dialog- und Integrationstests für Sperre, Escape, Abbruch und Fokus. Bestehende Tests zur früheren Teilbaumlöschung an die neue Regel angepasst.
- **Nachweise:** npm run check Exit 0; 131 Tests, davon 3 neue Fach-/Dialogtests, sowie Format-, TypeScript-, ESLint-, Stylelint- und Produktionsbuild-Prüfung erfolgreich. Bestehende Tests für den früheren Löschablauf wurden an die verbindliche Regel angepasst.
- **Risiken:** Browser-/Screenreader-Verhalten und visuelle Abnahme sind nicht durch jsdom-Tests nachgewiesen.

### Q10 – Premium Dark Mode und Einstellungsmenü
- **Status:** technisch umgesetzt; visuelle, Browser- und Screenreader-Abnahmen offen.
- **Umfang:** Zahnrad öffnet Einstellungen mit nativen Optionen Hell, Dunkel und System sowie Zugang zur bestehenden Tagverwaltung. Escape, Außenklick und Fokusrückgabe sind implementiert.
- **Theme:** Standard Hell; ausschließlich die Präferenz wird unter via.minidocs.theme gespeichert. Systemmodus folgt Änderungen von prefers-color-scheme; Speicherfehler und ungültige Werte fallen sicher zurück. Ein frühes Bootstrap-Modul setzt data-theme vor dem React-Mount, ohne Inline-Skript; eine vollständige Flashfreiheit ist ohne Browserprüfung nicht nachgewiesen.
- **CSS / Gestaltung:** Gemeinsame Komponenten und zentrale semantische Tokens; bestehende helle Werte bleiben erhalten, Dunkel verwendet Deep Navy mit Blau und Türkis. Papierartige Vorschauen und benutzerdefinierte Tagfarben bleiben erhalten. Der Dark Mode ist eine freigegebene Erweiterung zum KU-Original.
- **Nachweise:** npm run check Exit 0, 141 Tests inklusive 10 neuer Theme-/Menüfälle, TypeScript, ESLint, Stylelint, Formatprüfung und Produktionsbuild erfolgreich. Rechnerische Dark-Kontraste: Haupttext/Panel 13,67:1, Sekundärtext/Panel 8,47:1, Weiß/Primärfläche 7,22:1 und Fokusakzent/Panel 7,68:1.
- **Risiken / Abnahmen:** Vite startete lokal, der integrierte Browser meldete ERR_CONNECTION_TIMED_OUT. Reale Sicht-, Kontrast-, Zoom-, native DnD- und Screenreader-Prüfungen sowie Flashprüfung bleiben offen; rechnerische Farbpaare und jsdom ersetzen sie nicht.
- **Q10-FIX:** Die synthetische SVG-Vorschau nutzte eine starre nahezu weiße Papierfläche; alle Dokumentaktionsbuttons waren pauschal gedimmt und mit deaktiviertem Cursor versehen. Theme-Tokens dämpfen nun ausschließlich das Demopapier und schaffen im Dark Mode einen Passepartout-Abstand; die Aktionsgruppe erhält eine dunkle Hinterlegung. Aktive Buttons erhalten Pointer und Hover, nur deaktivierte Buttons bleiben gedimmt. Im Light Mode bleiben Papierfarbe und Kartenabmessungen erhalten; die korrigierten Aktionszustände gelten ebenfalls dort.
- **Q10-FIX-Nachweise:** npm run check Exit 0, 143 Tests einschließlich zwei neuer Fälle für aktive Tagzuweisung und deaktivierte Aktionen in Liste und Kacheln; rechnerische Kontraste: Demolabel/Papier 5,75:1, Demotext/Papier 7,83:1, aktives Icon/Aktionsfläche 13,67:1. Der Browser meldete beim lokalen Zugriff ERR_CONNECTION_TIMED_OUT; visuelle Wirkung, Zoom, Überlagerungen und Screenreader-Abnahme bleiben offen.
- **Abgrenzung:** Keine M6-/M7-Funktionen oder weitere Theme-Bibliothek; Q9 bleibt geplant.

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

### B0-DOC – Backend-Architektur dokumentieren

- **Status:** Dokumentation zur Prüfung durch den Projektverantwortlichen erstellt; keine Implementierungsfreigabe.
- **Umfang:** [Backend-Architektur](architecture/BACKEND-ARCHITECTURE.md), [ADR-Register](architecture/ARCHITECTURE-DECISIONS.md), Identität, Sicherheit, VIA.Docs, Datenmodell und Deployment. Quellcodeanalyse ist keine Deployment-Abnahme.
- **Planungsstand:** B0.1 MiniDocs-Bestandsaufnahme abgeschlossen; B0.2 VIA.Docs-Quellcodeanalyse abgeschlossen, Abweichung zwischen Commit und uncommittetem Einladungsentwurf dokumentiert; B0.3 Benutzer, Workspaces und Provisionierung freigegeben; B0.4 Sicherheitsarchitektur freigegeben, Hostingdetails offen; B0.5 Solution- und Projektarchitektur vorgeschlagen, Freigabe offen; B0.6 PostgreSQL-Datenmodell offen; B0.7 REST-Verträge offen; B0.8 Hosting und Deployment-Verifikation offen; B0.9 abschließende Dokumentation und Implementierungstickets offen.
- **Abnahme:** fachliche Prüfung und Freigabe des Dokumentationsstands durch den Projektverantwortlichen offen. D1–D8 bleiben geplante Implementierungstickets und benötigen gesonderte Freigabe.
- **Technischer Nachweis:** Frontend-Regressionsprüfung `npm run check` Exit 0, 219 Tests und Produktionsbuild erfolgreich; Build-Warnung für ein JavaScript-Chunk über 500 kB. Keine Backend-, Datenbank-, Deployment- oder produktiven Integrationstests im Dokumentationsauftrag. Der Git-Abschluss bleibt gemäß allgemeiner DoD bis zu gesonderter Freigabe offen.

### D1 – MiniDocs-API und Verträge
- **Planungsergänzung:** Backend-Grundstruktur und API-Verträge nach B0.5/B0.7; ADR-014 bis ADR-016 sind noch nicht freigegeben. Akzeptanz vorläufig, Implementierung gesondert beauftragen.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** ASP.NET Core (.NET 10) API-Grundlage, dokumentierte Verträge und Fehlerantworten für die MiniDocs-Schicht; Dienste und Datenzugriff trennen.
- **Ausgeschlossen:** Änderung von VIA.Docs, produktiver Zugriff und unbestätigte Endpunkte als Tatsachen festschreiben.
- **Abhängigkeiten:** Architektur- und Sicherheitsentscheidungen; Abstimmung mit D2–D4.
- **Akzeptanz:** Verträge für die benötigten Funktionen prüfbar; Eingaben, Ausgaben, Fehler und Authentifizierungsgrenzen dokumentiert.
- **Technische DoD:** allgemeine und API-DoD, geeignete .NET-Build-/Testbefehle festlegen.
- **Tests:** Vertrags-, Validierungs-, Fehler- und Integrationstests mit kontrollierten Abhängigkeiten.
- **Risiken:** tatsächliche API-Verträge und Hosting noch offen.

### D2 – PostgreSQL-Modell und Migrationen
- **Planungsergänzung:** B0.6 klärt das physische Modell; verbindliche Workspace-Isolation und zusammengesetzte Fremdschlüssel nach ADR-011, Tests gegen echtes PostgreSQL. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** Themen, Dokumentmetadaten, Tags und seitenweise Transkripte samt Relationen und versionierten Migrationen nach abgestimmtem Modell.
- **Ausgeschlossen:** Änderungen an bestehenden Datenbanken; unbestätigte Tabellenentwürfe ungeprüft übernehmen.
- **Abhängigkeiten:** D1-Vertragsabstimmung; isolierte freigegebene Testdatenbank.
- **Akzeptanz:** Schema, Systemknoten, Zuordnung und Integritätsregeln nachvollziehbar; Migration und Rückweg bzw. Wiederherstellung nachgewiesen.
- **Technische DoD:** Datenbank-DoD inklusive Backup/Restore und Datenintegrität.
- **Tests:** Migration/Rollback bzw. Restore, Constraints, Beziehungen und Konfliktfälle.
- **Risiken:** Datenverlust bei Migration, Dateinamenkollisionen und noch offene Suchstrategie.

### D3 – VIA.Docs-V1.2-Client
- **Planungsergänzung:** Typisierter serverseitiger Client; Commit und uncommittierter Einladungsentwurf sowie Deployment-Verifikation gemäß [Integration](architecture/VIA-DOCS-INTEGRATION.md) getrennt behandeln. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** serverseitige, typisierte VIA.Docs-Clientgrenze für später benötigte Datei-/Vorschauoperationen nach verifiziertem Vertrag.
- **Ausgeschlossen:** Änderungen am VIA.Docs-Server; produktiver Zugriff ohne gesonderten Testauftrag.
- **Abhängigkeiten:** D1; tatsächliche VIA.Docs-Schnittstellen und Testumgebung klären; D4 für authentifizierte Integration.
- **Akzeptanz:** bestätigte Verträge, Fehler, Timeouts und IDs kapseln; keine Credentials im Browser.
- **Technische DoD:** API-DoD und dokumentierte Fehlerbehandlung; Secrets serverseitig.
- **Tests:** Vertrags-/Negativtests mit Stubs, später freigegebene Integrationstests.
- **Risiken:** unbekannte API-Details, Previewgrößen und Ablauf bei Nichtverfügbarkeit.

### D4 – Anwendungskonto, Einladung und JWT
- **Planungsergänzung:** MiniDocs Identity, Benutzereinladungen, getrennte technische Einladung, wiederanlauffähige Provisionierung, Secret-Schutz und getrennte JWTs nach ADR-001 bis ADR-012; Hosting/Schlüsselbestand und Sitzungsvalidierung konkretisieren. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** bestehendes VIA.Docs-Einladungs-/Application-Verfahren für die MiniDocs-Serverintegration verifizieren; JWT-Bezug, sichere Konfiguration und Erneuerung nach tatsächlichem Vertrag.
- **Ausgeschlossen:** neue VIA.Docs-Registrierung erfinden, Zugangsdaten ins Frontend legen oder produktive Konten ohne Auftrag verwenden.
- **Abhängigkeiten:** D1, D3-Vertragsklärung; freigegebene Testidentität und Geheimnisverwaltung.
- **Akzeptanz:** nachvollziehbarer Einladungs- und Tokenablauf mit minimalen Rechten; keine Secrets in Logs, Git oder Frontend.
- **Technische DoD:** API-/Sicherheits-DoD; Betriebsverantwortung und Rotation dokumentiert.
- **Tests:** erfolgreiche und ungültige/abgelaufene Tokenfälle, Berechtigungsfehler und sichere Konfiguration in Testumgebung.
- **Risiken:** reale Rollen, Tokenlebensdauer und Registrierung noch ungeklärt.

### D5 – PDF-Upload, Download und Vorschau
- **Planungsergänzung:** Autorisiertes Streaming ohne vollständige `byte[]`-Pufferung; unterstützte Range-Requests, Status 200/206/304/416, Header, Cancellation und Speicherverbrauch gemäß [Integration](architecture/VIA-DOCS-INTEGRATION.md) nachweisen. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** MiniDocs-API verbindet PDF-Upload, Dateiabruf und Vorschauen mit VIA.Docs und speichert zugehörige Referenzen/Metadaten.
- **Ausgeschlossen:** Änderung von VIA.Docs, produktive Dateien oder neue Viewer-Funktionen außerhalb des vereinbarten Umfangs.
- **Abhängigkeiten:** D1–D4; M7 für den vollständigen UI-Importfluss.
- **Akzeptanz:** Test-PDF kann hochgeladen, abgerufen und mit bestätigter Vorschau angezeigt werden; Teilfehler hinterlassen nachvollziehbaren Zustand.
- **Technische DoD:** API- und Datenbank-DoD; Fehler- und Wiederanlaufregel für zwei Systeme vereinbart.
- **Tests:** End-to-End mit Testdateien, fehlgeschlagener Upload, nicht verfügbare Vorschau, Zugriffs- und Wiederholungsfälle.
- **Risiken:** inkonsistente Metadaten und Dateien bei Teilausfällen, Größen- und Vorschaugrenzen.

### D6 – Themen-, Tag- und Metadatenpersistenz
- **Planungsergänzung:** workspace-bezogene Themen-, Tag- und Dokumentmetadaten mit Integritäts- und Isolationsregeln; B0.6/B0.7 offen. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** dauerhafte Themenhierarchie, Tagzuordnung und Dokumentmetadaten über MiniDocs-API und PostgreSQL; Frontend an die vereinbarten Grenzen anschließen.
- **Ausgeschlossen:** VIA.Docs-Serveränderungen, Suchindex und Transkriptbearbeitung aus D7.
- **Abhängigkeiten:** D1, D2; M2 und M5 für zugehörige UI-Abläufe.
- **Akzeptanz:** Ändern, Neuladen und erneutes Lesen erhalten Daten; Systemknoten, Lösch- und Zuordnungsregeln bleiben gewahrt.
- **Technische DoD:** API- und Datenbank-DoD, relevante Verträge und Fehlerzustände dokumentiert.
- **Tests:** CRUD und Integrität, Reload, Systemschutz, Konflikte und API-/DB-Integration.
- **Risiken:** konkurrierende Änderungen und Atomarität; tatsächlicher Einzelnutzerumfang klären.

### D7 – Transkripte und Volltextsuche
- **Planungsergänzung:** Transkriptseiten, Konflikt- und Nebenläufigkeitskontrolle sowie Suchsemantik; wartende Transkripte sind geplanter Importumfang, kein bereits implementiertes Frontend-Modell. Akzeptanz vorläufig.
- **Status:** geplant, gesonderter Auftrag erforderlich.
- **Ziel / Umfang:** seitenweise Transkriptpersistenz, Zuordnung per Dateiname einschließlich wartender Transkripte, Suche und Filter über die MiniDocs-API; Suchverhalten gegen M5 abgleichen.
- **Ausgeschlossen:** KI-Transkription, semantische Suche, automatische Scan-Zerlegung und ungeprüfte Performanceversprechen.
- **Abhängigkeiten:** D1, D2, D6; M5–M7 für vollständige UI-Flüsse; D5 für Verknüpfung mit echten PDFs.
- **Akzeptanz:** bearbeitete Transkripte bleiben nach Reload erhalten; wartende Zuordnung und Konflikte sind definiert; Suchtreffer mit Seite und vereinbarten Filtern reproduzierbar.
- **Technische DoD:** API- und Datenbank-DoD; Suchsemantik und Indexstrategie ausdrücklich entscheiden.
- **Tests:** Import-/Zuordnungskonflikte, Transkriptbearbeitung, Suche/Filter, Sonderzeichen und API-/DB-Integration.
- **Risiken:** Abweichung zwischen Original-Teilstringsuche und Datenbanksuche; Dateinamenkollisionen.

### D8 – Integrations-, Sicherheits- und Betriebstests
- **Planungsergänzung:** SEC-01 bis SEC-15, Deployment-Verifikation, Wiederanlauf, Schlüsselrotation und Backup/Restore nach [Sicherheit](architecture/SECURITY-ARCHITECTURE.md) und [Deployment](architecture/DEPLOYMENT.md). Akzeptanz vorläufig; keine Test- oder Produktivfreigabe.
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
