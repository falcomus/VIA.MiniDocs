# Ticketkatalog

Aktuelle Aufgaben und offene Abnahmen; frühere Detailnachweise stehen im [Ticketarchiv](TICKETS-ARCHIV.md). Einstieg, Phasen und kritischer Pfad: [Masterplan](MASTERPLAN.md). Für jedes Ticket gelten zusätzlich die [allgemeinen und einschlägigen speziellen DoDs](DEFINITION-OF-DONE.md). Abhängigkeiten bezeichnen fachliche Voraussetzungen, keine Genehmigung für spätere produktive Zugriffe. **Technisch abgeschlossen** bedeutet bei M1/M2 ausdrücklich nicht, dass deren visuelle Abnahme bereits erfolgt ist. Die Meilensteine M1–M8 entsprechen dem [KU-Migrationsplan](architecture/KU-MIGRATION-PLAN.md); D-Tickets betreffen ausschließlich die später gesondert beauftragte Datenanbindung.

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
- **KU-Vergleich:** Laut Projektverantwortlichem wurde die fehlende Themenauswahl beim Vergleich von vier Screenshots der Übersicht und des Viewers erkannt; deren Korrektur wurde wie oben abgenommen. Gleiche dokumentierte Viewportgrößen und vollständige Hover-, Fokus-, Auswahl- und Scrollzustände sind damit nicht nachgewiesen. Der vollständige Vergleich von Bedienverhalten, Farben, Typografie, Icons und Abständen sowie die zuständige visuelle Gesamtfreigabe bleiben gemäß UI-DoD offen. Freigegebener UI-Feinschliff steht im [Ticketarchiv](TICKETS-ARCHIV.md), die PDF-Architekturentscheidung im verlinkten PDF-Dokument.
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

- **Status:** Dokumentation auf `main` integriert; offene Architektur- und Deploymentfragen bleiben bestehen. Keine Implementierungsfreigabe.
- **Umfang:** [Backend-Architektur](ARCHITECTURE.md), [ADR-Register](DECISIONS.md), Identität, Sicherheit, VIA.Docs, Datenmodell und Deployment. Quellcodeanalyse ist keine Deployment-Abnahme.
- **Planungsstand:** B0.1 MiniDocs-Bestandsaufnahme abgeschlossen; B0.2 VIA.Docs-Quellcodeanalyse abgeschlossen, Abweichung zwischen Commit und uncommittetem Einladungsentwurf dokumentiert; B0.3 Benutzer, Workspaces und Provisionierung freigegeben; B0.4 Sicherheitsarchitektur freigegeben, Hostingdetails offen; B0.5 Solution-, REST- und Engineering-Architektur nach ADR-014 bis ADR-016 freigegeben und dokumentiert; B0.6 PostgreSQL-Datenmodell fachlich freigegeben; B0.7 REST-Vertrag fachlich freigegeben; B0.8 Hosting-Zielarchitektur freigegeben, reale Deployment- und Betriebsfreigabe offen; B0.9 Architekturreview und Implementierungsreihenfolge fachlich freigegeben.
- **Abnahme:** D1 ist abgeschlossen. D2 einschließlich D2-06 ist unabhängig technisch abgenommen (Note 1−). Spätere Integrations- und Deployment-Nachweise bleiben gesonderte Gates.
- **Technischer Nachweis:** Frontend-Regressionsprüfung `npm run check` Exit 0, 219 Tests und Produktionsbuild erfolgreich; Build-Warnung für ein JavaScript-Chunk über 500 kB. Keine Backend-, Datenbank-, Deployment- oder produktiven Integrationstests im Dokumentationsauftrag.

### B0.5 – Backend-Projektarchitektur

- **Status:** abgeschlossen hinsichtlich Freigabe und Dokumentationsabgleich; ADR-014, ADR-015 und ADR-016 sind freigegeben, ADR-013 ist als Zielarchitektur freigegeben; die Betriebsfreigabe bleibt offen. Keine Solution angelegt und keine Implementierung freigegeben.
- **Umgebung:** Lesend festgestellt: .NET SDK 10.0.404 und Visual Studio Community 2026 18.11.4; die Versionen erfüllen die [Microsoft-Vorgabe für `net10.0`](https://learn.microsoft.com/de-de/dotnet/core/porting/versioning-sdk-msbuild-vs). Eine konkrete Projekt-/Workload-Buildprüfung wurde nicht durchgeführt; `dotnet --info` meldete beim Zugriff auf die Windows-Dienstverwaltung einen Berechtigungsfehler. VS Code ist vorhanden und die .NET-CLI eine mögliche Alternative.

### B0.6 – PostgreSQL-Datenmodell vorbereiten

- **Status / Umfang:** Gesamtmodell und Verträge gemäß [B0.6/B0.7](ARCHITECTURE.md#b06--postgresql-datenmodell-und-persistenzarchitektur) und ADR-017/018 sind fachlich freigegeben. D2 implementiert Modell, EF-Mapping, Migrationen, Constraints und Schreibbefehle nach ADR-019. Die [D2-06-Befundmatrix](D2-06-VERIFICATION.md) dokumentiert den lokalen Nachweis. Betriebsnachweise folgen D8.
- **Identity / Workspace:** ASP.NET Core Identity, geschlossene Benutzereinladungen, privater 1:1-Workspace, getrennte technische VIA.Docs-Anwendung und wiederanlauffähiger Provisionierungsvorgang müssen gemeinsam mit Status- und Secret-Lebenszyklen modelliert werden; fachliche Regeln stehen in ADR-001 bis ADR-012. Benutzeridentität, Workspace-Zugehörigkeit und die technische ApplicationId bleiben getrennt. Gesperrte Konten und nicht aktive Workspaces sind zu berücksichtigen.
- **React-Bestand:** `DocumentSummary` führt eigene ID, Titel, optionale Datei-/Datumsangaben, Art, Themen-ID, optionale Tag-IDs, nullable Seitenzahl und optionale seitenweise Transkripte mit Bearbeitungsstatus; lokale PDF-Blobs sind keine Persistenz. PDF-Import erlaubt gleiche Dateinamen, erzeugt unabhängige IDs und liest die Seitenzahl. Themen besitzen Eltern-ID und geschützten Systemknoten; nur Themen ohne direkte Dokumente und Unterthemen sind löschbar. Tags besitzen ID, Name, Beschreibung und Farbe. Dokument-Tag-Zuordnungen und Transkriptseiten sind fachlich eigenständig abzubilden.
- **Suche / Bearbeitung:** Die Demo normalisiert Groß-/Kleinschreibung und Akzente, unterstützt Phrasen, Teilstrings und UND über Begriffe/Tags, Datumsbereich sowie Themenbereich mit Nachfahren; Trefferpunkte und erste Trefferseite sind im bestehenden Suchcode definiert. Der separate Viewer synchronisiert Änderungen sitzungsgebunden; persistente Nebenläufigkeits- und Konfliktregeln sind noch nicht implementiert. Wartende Transkriptimporte sind geplant, nicht vorhandener React-Fachzustand.
- **Freigabe / Nachweise:** Gesamtkatalog, Constraint- und EF-Mapping, Sicherheits- und Migrationstestanforderungen sind vorgeschlagen; Papierkorb, Wiederherstellung, Pending-Import und UUID-Such-Tie-Breaker sind fachlich freigegeben; technische Klärung ungeklärter externer Vorgänge bleibt offen. ADR-013 und VIA.Docs-Deployment bleiben ungeprüft. SEC-01 bis SEC-15 und weitere im Entwurf genannte Integrationstests sind nicht ausgeführt.

### B0.7 – REST-Verträge

- **Status:** Paket A und der [REST-Vertrag](ARCHITECTURE.md#b07--rest-vertragsentwurf-zur-architekturfreigabe) einschließlich Upload-Vergleich und -Aufbewahrung, Cursor-Bindung, DTOs und Limits sind fachlich freigegeben. D1-Backend-Fundament implementiert; keine fachlichen REST-Endpunkte implementiert, tatsächliches VIA.Docs-Deployment nicht verifiziert.

**Umsetzungsnachweise:** D1 prüft OpenAPI-Generierung, Nullable/Enum/ProblemDetails, Header und Drift; D2 beweist Constraints, atomare Reservierung und Versionierung auf echtem PostgreSQL; D4 weist Antiforgery, Session-Sperre und Isolation nach; D5 prüft Stream-/Digestvergleich, unbekannte Remote-Ergebnisse, parallele Request-IDs und Sperrmarken; D6 prüft Batch-Atomarität und ETags; D7 prüft KU-Transkriptformate, Cursor-/Suchparität und Unicode; D8 weist Proxy-/Range-Verhalten und externe Integration in gesondert freigegebener Testumgebung nach. Die D1-Fixture-Nachweise und die lokalen D2-Nachweise liegen vor. Die unabhängige D2-Endprüfung ist bestanden; D3 ist lokal technisch verifiziert und unabhängig mit Note 1− abgenommen. Reale Integration und die Nachweise aus D4–D8 stehen aus. B0.8 und B0.9 sind fachlich freigegeben; ihre technischen Nachweise bleiben offen.

### B0.8 – Hosting, Deployment und Betrieb

- **Status:** [Betriebskonzept](ARCHITECTURE.md#hosting-deployment-und-betrieb) und ADR-013 als Zielarchitektur fachlich freigegeben; keine Implementierungs-, Installations-, Integrations- oder Betriebsfreigabe.
- **Vorschlag:** Linux, systemd, Kestrel auf Loopback, Nginx/TLS, ein Origin und eigene PostgreSQL-Datenbank; Key Ring dauerhaft und X.509-verschlüsselt, Rollen und Umgebungen getrennt, kontrollierte Migration und systemübergreifender Restore. Verfügbarkeit und Betreiberzuständigkeiten sind nicht nachgewiesen.
- **Nachweise:** D2 prüft Datenbankrechte, Migration und Restore; D4 Data-Protection-Initialisierung, Rotation, Session und Restore; D5 Streaming/Range und Ausfallverhalten; D8 Zielhost, TLS/Proxy, Limits, Secrets, Backup/Restore und freigegebene End-to-End-Tests. Produktive externe Aufrufe benötigen gesonderte Freigabe. Kein Betriebsnachweis wurde in B0.8 ausgeführt.
- **Gates:** Plattformneutraler lokaler D1-Start benötigt einen Implementierungsauftrag und freigegebene betroffene Sicherheitsentscheidungen, keinen vorhandenen Produktionsserver. Reale VIA.Docs-Integration benötigt isolierte Testumgebung/Vertrag/Identität und Testfreigabe. Demo-/Produktivbetrieb erfordert ADR-013, Betreiberentscheidungen und reale Zielumgebungsnachweise.

### B0-FINAL-C – VIA.Docs-Integrationsvertrag

- **Status:** [Statischer Quellabgleich und Abnahmematrix](VIA-DOCS-API.md#quellabgleich-b0-final-c) fachlich freigegeben; kein laufendes Deployment, keine API-Aufrufe oder neue Testergebnisse nachgewiesen.
- **Abweichung:** VIA.Docs-Quellstand erzwingt 25 MiB je Dokument und puffert Multipart über `ReadFormAsync`. MiniDocs schlägt ebenfalls 25 MiB und eingangsseitiges Streaming vor; die Grenze ist fachlich freigegeben und benötigt reale Prüfung vor Integration.
- **Gates:** D3/D4/D5 prüfen Vertrag, Isolation, Einladungsdeployment und kontrollierte Wiederanlaufpfade; D8 ergänzt zugelassene echte Proxy-/Range-, Preview-, Speicher- und Restore-Nachweise. Lokal sind Testdoubles ohne reale VIA.Docs-Berechtigungen nutzbar. ADR-013 ist als Zielarchitektur freigegeben; die Betriebsfreigabe bleibt offen.

### Abhängigkeiten und gemeinsame Freigabegates D1–D8

**Status:** D1 ist abgeschlossen. D2-06 ist implementiert und unabhängig technisch abgenommen (Note 1−). Der formelle Ticketabschluss richtet sich nach der Git-DoD. D3-01 bis D3-05 sind lokal technisch und dokumentarisch abgeschlossen und unabhängig mit Note 1− abgenommen. D3-INTEGRATION bleibt ein verbindlicher gesonderter Abnahmepunkt. D4-PLAN einschließlich FIX ist unabhängig mit Note 1− (1,3) abgenommen. D4-A ist unabhängig mit Note 1− (1,3) technisch freigegeben. Die produktiven Betriebsabnahmen stehen aus. D4-B ist nach unabhängiger statischer Prüfung mit Note 1− (1,3) technisch freigegeben. D4-C-FIX (D4-05 bis D4-07) ist nach unabhängiger statischer Prüfung der 25 Paketdateien mit Note 1− (1,3) technisch freigegeben. Alle drei FIX-Befunde sind geschlossen. Die Codex-Verifikation ergab je 435 bestandene Backendtests in Debug und Release bei vollständigem Erhalt der 410 Bestandstests sowie 225 bestandene Frontendtests. Der Reviewer hat keine unabhängigen Testläufe ausgeführt. D4-FINAL hat das automatisierte Regression-Gate bestanden. Die Architektur- und Vertragsentscheidungen sind fachlich freigegeben. Reihenfolge: **D1 → D2 → D3 → D4 → D6 → D5 → D7 → D8**. D3 benötigt nur D1; D4 benötigt D1/D2 und die Client-Schnittstelle D3; D6 benötigt D1/D2/D4; D5 benötigt D1–D4 und D6; D7 benötigt D1/D2/D4/D6 und D5 für echte PDF-Flüsse; D8 benötigt D1–D7. D6 entwickelt Themen-/Metadatenfunktionen mit Dateitestdoubles; D5 integriert die fertigen Funktionen ohne provisorische Fachimplementierung. D7 darf reine Such-/Transkriptteile nach D6 mit Dateitestdoubles vorbereiten; vollständiger Abschluss mit PDF-Fluss benötigt D5. Kein Ticket setzt sein eigenes Folgeticket voraus. M8 ist ein separates fachliches UI-Gate, kein Blocker für Backendtests.

**Freigegebene Grenzwerte:** MiniDocs zunächst zentral konfigurierbar auf höchstens **25 MiB pro Dokument** begrenzen, passend zum statisch geprüften VIA.Docs-Quellstand; eine Erhöhung erst nach bestätigtem kompatiblem Remote-Vertrag und geeigneten Tests. Der bisherige 100-MiB-Vorschlag wird ersetzt; die 25 MiB sind fachlich freigegeben. VIA.Docs `ReadFormAsync` kann puffern; MiniDocs-Streaming belegt keine ungepufferte Gesamtkette. SVG-Preview-Fallback ist kein fertig gerendertes PDF; DELETE 204 ist keine physische Löschgarantie. Remote-Idempotenz und sichere Dateiwiederauffindung nach verlorenem ACK sind nicht nachgewiesen. Einladungsrouten sind seit VIA.Docs-Commit 8489ba8 lokal versioniert. Das Deployment ist ungeprüft. [VIA.Docs-Vertrag und 13 Prüffälle](VIA-DOCS-API.md#integrations--und-abnahmematrix).

**Gemeinsame DoD und Status:** Vor Beginn: expliziter Ticketauftrag und Freigabe jeweils betroffener Entscheidungen; echte PostgreSQL-Tests nur mit autorisierter isolierter DB. Implementierungsabschluss umfasst Code, präzise Tests, Review und Dokumentation nach [Standards](ENGINEERING-STANDARDS.md) und [DoD](DEFINITION-OF-DONE.md). Technische Abnahme verlangt die im jeweiligen Ticket genannten erfolgreichen automatisierten Nachweise; PostgreSQL-InMemory ersetzt PostgreSQL nicht. Fachliche Abnahme prüft die benannten Benutzerfälle und offene UI-Nachweise separat. Reale Integrationsabnahme setzt bestätigten VIA.Docs-Deploy-/Migrationsstand, isolierte Identitäten und gesonderte Testfreigabe voraus; fehlende reale Systeme blockieren keine zulässige Arbeit mit Testdoubles. Deployment-/Betriebsfreigabe setzt ADR-013, Betreiberentscheidungen und Zielumgebungsnachweise voraus. Keine dieser Freigaben impliziert eine andere. Jeder Abschlussbericht nennt Änderungen, konkrete Testbefehle/Exitcodes, Scope der Testdoubles, Abnahmen, Risiken und offene Gates. Kein Produktionszugriff ohne gesonderten Auftrag.

### D1 – MiniDocs-API und Verträge

- **Status:** am 25.11.2026 durch den Projektverantwortlichen technisch abgenommen und abgeschlossen; lokale Start- und Prüfbefehle sowie Paketversionen stehen in der [README](../README.md#backend-lokal-d1). Elf Backendtests, Contract-Drift-Negativprobe und 225 Frontendtests erfolgreich. Keine Datenbank, Identity, VIA.Docs-Anbindung oder fachlichen Endpunkte implementiert.

- **Ziel:** erstes lokal baubares Backend-Fundament nach ADR-014–016; empfohlener Reasoning-Level **hoch**.
- **Voraussetzungen:** gesonderter D1-Auftrag; Vertrags-/Toolentscheidungen sind fachlich freigegeben; lokal .NET-10-SDK, kompatibles Node/npm und isolierter Arbeitsbereich. Kein PostgreSQL-Server, VIA.Docs, Deployment oder ADR-013-Hostingfreigabe erforderlich. Paketinstallation erst im ausdrücklich beauftragten Implementierungslauf.
- **Umfang/Projekte:** `backend/VIA.MiniDocs.sln`, `Directory.Build.props`, `Directory.Packages.props`; Domain ohne Projektabhängigkeiten, Application → Domain, Infrastructure → Application/Domain, Api → Application/Infrastructure, `tests/VIA.MiniDocs.Tests` mit xUnit. .NET 10, nullable/strict Warnungen, zentrale gepinnte Paketverwaltung, reproduzierbarer Build, Standard-DI und asynchrones I/O. Api: Minimal-API-Host, sichere Fehlerbehandlung/ProblemDetails und OpenAPI aus tatsächlichen Endpoint-/DTO-Metadaten; Application/Domain: nur benötigte abstrakte Grenzen; Infrastructure: lokale Testdoubles, keine reale Integration.
- **Verträge/Tests:** OpenAPI auf reproduzierbarem Buildweg erzeugen; gepinnte Generierung von TypeScript-Typen aus OpenAPI mit Strict-Prüfung und Drift-Erkennung gemäß ADR-015. Ein begrenzter nichtfachlicher Testendpoint/Testfixture prüft Nullable, Enum, ProblemDetails und Header; Multipart und Binärschema als Fixture, ohne fiktive Fach-API als implementiert auszugeben. Unit- und Hostintegrationstests für DI, ProblemDetails ohne Secrets, Routen-/OpenAPI-Metadaten und Abhängigkeitsrichtung; negative Driftprobe. PostgreSQL und reale VIA.Docs-Tests sind für D1 nicht erforderlich.
- **Nicht-Ziele:** Identity, Datenbank, Migrationen, Geschäftsoperationen D2–D7, Frontend-Redesign, Serverinstallation und produktiver Netzwerkzugriff.
- **Akzeptanz/Gates:** dokumentierte `dotnet restore/build/test` und lokale Startanleitung funktionieren, Paketversionen sind zentral festgelegt, keine Zyklen, Generator produziert reproduzierbare typprüfbare Ergebnisse; Build-/Testprotokoll und Architekturprüfung vorlegen. Fachliche Abnahme bestätigt Scope und Vertrag, keine Integrations-/Produktionsfreigabe. Allgemeine Gates oben gelten.

### D2 – PostgreSQL-Modell und Migrationen

- **Status D2-06 (26.11.2026):** Lokale Schreibgrenzen, Berechtigungs- und Restore-Gate, Statusprüfung nach Sperrwartezeit und Pending-Bereinigung sind umgesetzt. Der unabhängige Endreview bestätigt Note 1− (Architektur 1,0) und keine verbleibenden D2-Abnahmeblocker. Zwei Hauptläufe mit je 169 erfolgreichen Tests, davon 105 PostgreSQL-Integrationsfälle, und fünf Wiederholungen von je zehn Konkurrenzfällen sind dokumentiert. Keine weitere verpflichtende D2-Korrekturrunde. Formeller Abschluss gemäß Git-DoD; keine Integrations- oder Produktionsfreigabe. [Befundmatrix, Verifikation und Endreview-Ergebnis](D2-06-VERIFICATION.md).

- **Ziel:** Datenmodell/Constraints nach ADR-011/016/017/018 und [B0.6](ARCHITECTURE.md#b06--postgresql-datenmodell-und-persistenzarchitektur); Reasoning **extra hoch**.
- **Voraussetzungen/Schichten:** D1, Freigabe des konkreten Modells und einer isolierten echten PostgreSQL-Testdatenbank. Tests auf Constraint-Ebene verwenden kontrollierte Daten; API-/Identity-Flüsse folgen in D4–D8. Infrastructure verantwortet EF-Mapping, Migrationen und Transaktionsgrenzen; Domain enthält Invarianten; Application definiert Grenzen, API keine direkten DB-Umgehungen. Tabellen/Relationen für Identity, Workspaces, Themen, Dokumente, Tags, Seiten, Pending-Import, Provisionierung, Uploadvorgänge, Sperrmarken und Löschreceipts entsprechend freigegebenem Vertrag modellieren; kein PDF dauerhaft ablegen.
- **Nicht-Ziele:** VIA.Docs-Schema, Produktionsmigration, vollständige CRUD-Handler aus D4–D7, neue Repository-Frameworks.
- **Akzeptanz/Sicherheit:** Zusammengesetzte Workspace-FKs, Kardinalität, Systemknotenschutz, Zustandschecks, Workspace-first, DB-Versionierung und atomare Receipt-/Sperrmarkenübergänge bleiben verbindlich. Fach-DML durch die Runtime ist gesperrt. Direkte und alternative Schreibpfade sowie Migration und Datenbank-Restore werden mit der Runtime-Rolle geprüft.
- **Tests/Nachweise:** Unit-Tests für reine Invarianten; echte PostgreSQL-Integration für Fremdzuordnung, direkte Constraints, parallele Baum-/Zuordnungsänderungen, Phantomschutz, konkurrierende Einladungen, Upload-Reservierung, Restore/Purge, Migration/Restore und Rollback. Kein InMemory-Ersatz. Echte VIA.Docs-Aufrufe nicht erforderlich. Technische, fachliche und Deployment-Gates gemäß gemeinsamer DoD; Backup-/Zielsystemnachweis zusätzlich D8.

### D3 – VIA.Docs-V1.2-Client

- **Status:** D3-01 bis D3-05 sind implementiert, lokal verifiziert und unabhängig mit Gesamtnote 1− abgenommen. A-01 bis A-06 und B-01 bis B-04 sind geschlossen. Der formelle Git-Abschluss wurde auf origin/main verifiziert. D3-INTEGRATION steht aus. [Gesamtnachweis](D3-C-VERIFICATION.md)
- **Ziel und Grenzen:** Serverinterne V1.2-REST-Integration nach ADR-004–007, 010–011 und 014–019. Application definiert die typisierte Grenze, Infrastructure setzt sie mit Standard-.NET-Mechanismen um. D4/D5 autorisieren und orchestrieren. Keine zusätzlichen Projekte, VIA.Docs-Projektreferenz, Verwaltungsregistrierung, React-Direktzugriffe, Remote-DB-Zugriffe oder Änderungen an D2. D3-Clienttests laufen mit HTTP-Testdoubles ohne PostgreSQL. Die vollständige Backendregression verwendet weiterhin isoliertes PostgreSQL.

#### D3-01 – Clientarchitektur und HTTP-Grundlagen

- **Umfang:** Application-Vertrag und Ergebnistypen; private Infrastructure-Wire-DTOs und explizites Mapping; fail-closed Options/DI; HTTPS-Origin, sichere feste Routen und Querycodierung. Einmalige Request-Erzeugung mit pro Request gesetztem Bearer und ohne Cookies, Redirects, Secret-Logs oder Übernahme fremder URLs. Fristen, Cancellation, Ownership, JSON-/Error-Limits und Retry gemäß [Clientvertrag](VIA-DOCS-API.md#d3-plan--freigegebener-clientvertrag-für-d3-a-und-planung-für-d3-b). Infrastruktur-Seams nur für Transport und Uhr; keine neue generische Repository-/Policy-Schicht.
- **Akzeptanz:** Falsche Basisadresse, Off-Origin-Location/Redirect und Eingabe-Pfade erreichen keinen unerwarteten Host. Fehlertypen unterscheiden HTTP-Status, Vertrag, Cancellation vor Versand, Timeout und unklaren bereits versandten Write. Rohantwort und Header mit Secrets erscheinen in keiner Diagnose. Timeout umfasst Deserialisierung und Ressourcenfreigabe.
- **Tests/Nachweis:** Aufzeichnender Handler prüft Methode, URI und erlaubte Header; Cases für Prefix, Escaping, unzulässige URI, 3xx, Proxy-HTML, unbekannte JSON-Felder, fehlerhafte Pflichtfelder, 503/internal_error, gültigen/ungültigen/fehlenden Retry-Hinweis und Abbruch. Verträge/DI mit lokalen Testkonfigurationen nachweisen. Retry-Obergrenze auch bei kombiniertem 401 und Transportfehler prüfen.

#### D3-02 – Authentifizierung und technische Identitäten

- **Umfang:** Login, verifizierte gebundene technische Identität, einmalige Einlösung und Wiederauffindung als D4-Schnittstelle. Cache nur pro verifizierter Identität, aktueller Credential-Referenz und Generation, getrennt von Registrierungskandidaten. Ablauf, koordinierte Erneuerung, zielgerichtete 401-Invalidierung, Wechsel und konkurrierende Erneuerung nach [Clientvertrag](VIA-DOCS-API.md#d3-plan--freigegebener-clientvertrag-für-d3-a-und-planung-für-d3-b). D4 speichert vorbereitete Credentials nach ADR-010 und übernimmt Commit/Fence und externen Nachweis.
- **Akzeptanz:** Korrekte normalisierte Namen und IDs nötig. Fehlendes, abgelaufenes, inkonsistentes oder widerrufenes Token scheitert kontrolliert. Unbekanntes Einlösungsergebnis liefert keinen zweiten POST und wird durch erneuten Login als eigener Schritt geklärt. Fehlender oder abweichender Endpoint unterbindet die Fähigkeit; kein administrativer Schlüssel im Client.
- **Tests/Nachweis:** Fiktive A/B-Identitäten mit parallelen Aufrufen und strikt korrekten Authorization-Headern; Single Flight bei Ablauf, wartender Cancel, verspätetes altes Token, Netzwerk-/JSON-Fehler, 401/404/409/429/503, verlorene Einlösungsantwort und spätere Login-Klärung. Handler zählt Schreibversuche. Lokalen HTTP-Transport mit Verlust der Antwort zusätzlich auf tatsächlich empfangene Requests prüfen.

#### D3-03 – Lesende Dateioperationen

- **Umfang:** Owner-gebundene kanonische Liste und Metadaten, GET/HEAD, single-range, Conditional Request und Preview. Remote-Cursor bleibt opak und ist unabhängig vom späteren Browsercursor. Original-Stream und Response teilen eine explizite Lebensdauer. SVG-Fallback und gerenderte Vorschau getrennt abbilden, Seitenzahl optional.
- **Akzeptanz:** Status und Header genau wie im [Quellabgleich](VIA-DOCS-API.md#d3-plan--evidenz-und-kritische-vertragsbefunde) behandeln. Zugriff und Validator bleiben ans Workspace-/Identitätsziel gebunden. Weder Read noch Retry hält bei Cancellation Ressourcen offen. Keine Range-Garantie bei Full Range oder If-Range. Read gilt nur vor Body-Freigabe als wiederholbar.
- **Tests/Nachweis:** Zweite Listenseite, Cursor mit Sonderzeichen und Leerseite mit weiterem Cursor; fremde/missing ID; Einzelmetadaten mit null/fehlender Seitenzahl und ungültigem Hash; 200 nach Range, 206 mit korrekten und inkonsistenten Boundaries, Body-loses HEAD/304, 416 inklusive `bytes */N`; Ready mit PNG und Ready mit SVG, Fehler und vom Server bestimmter MIME-Typ. Nicht seekbarer großer Teststream weist begrenztes Lesen, vorzeitiges EOF, Timeout, Cancellation, Freigabe ohne Lesen und Freigabe nach Abbruch nach.

#### D3-04 – Schreibende Dateioperationen

- **Umfang:** Einmal lesbares PDF-Multipart bis 25 MiB, Uploadkandidat und getrennte Metadatenprüfung, DELETE mit qualifizierter 404- und 204-Auswertung. Abbildung auf D2-Verträge für `prepared → in_flight → remote_known → completed`, `unknown`, `delete_in_flight/delete_unknown` und Purge-Receipt. D5 verarbeitet bewusst gespeicherte Größen/Digests und reserviert/committet vor Dispatch. D3 entscheidet über Transportergebnis und liefert typisierte Informationen; der Client ändert keine lokale Fachoperation.
- **Akzeptanz:** Vor Versand nachweislich lokale Ablehnung von nach Dispatch unklarem Ausgang unterscheiden. Pro autorisiertem Versuch höchstens ein externer Write; Timeout, Cancellation, 401, Redirect, abgebrochene Verbindung oder ungültiges Erfolgs-DTO lösen keinen automatischen zweiten Versuch aus. 201-ID früh bindbar; `rawWidth` nie als Seitenzahl. Bytezahl und SHA-256 separat nachweisen; 404 allein genügt weder für fachlichen Purge noch physische Löschung.
- **Tests/Nachweis:** Nicht seekbarer einmal lesbarer Eingabestream mit Sentinel für Doppelread und Ganzpufferung; Limits darunter/genau/darüber bei bekannter und unbekannter Länge; korrekte Multipart-Form. 201 mit falscher ID/Handhabung von Location, fehlendem Hash, widersprüchlicher Größe oder Early Response; kein falscher Abschluss. DELETE 204, 404, 401, unerwartetes 2xx, Timeout und verlorenes ACK. Handler und kontrollierter Loopback prüfen Zahl logischer Versuche und tatsächlich empfangener Requests einschließlich Abbruch nach Annahme; Transport-Replays unter Testbedingungen ausschließen, bevor der Write-Pfad akzeptiert wird.

#### D3-05 – Gesamtverifikation und Abnahme

- **Technische Akzeptanz:** Testdoubles vollständig grün einschließlich Nebenläufigkeit, Ressourcen, Fehler und Redaktion. SEC-01/02/04 anhand gebundener Read-/DELETE-Requests, SEC-10 mit verlorener Einlösungsantwort und ohne doppelten POST lokal vorbereiten; SEC-11/15 mit Redaktions- und Cacheisolationstests prüfen. Vollständige SEC-01–15-Nachweise verbleiben an den jeweiligen D4–D8-Grenzen. D2 bleibt binär/vertraglich kompatibel; keine DDL-/DML-Erweiterungen. Review prüft besonders Commit-vor-HTTP, getrennte Read-/Write-Ergebnisse und die Lastgrenzen.
- **Test-/Evidenzpflicht:** `dotnet restore backend/VIA.MiniDocs.sln --locked-mode`, Debug-Build `--no-restore`, gezielte Client-Suite ohne externe Dienste und vollständiges `dotnet test` einschließlich echter isolierter PostgreSQL-Tests. Backend `npm run check`, `npm run typecheck`, `npm run test:drift`; Frontend-Regression bei gemeinsamem Vertragsdiff. Git-Diff einschließlich neuer Dateien und Secret-Prüfung. Protokolle mit Befehlen, Exitcodes und Testumfang, Matrix VD-01–08/SEC und unabhängiges Abschlussreview vorlegen. Technische Teilergebnisse getrennt von der formellen Git-DoD ausweisen.
- **Reale Abnahme, separater Auftrag:** Betreiber bestätigt Build/Commit/Migration, Basisadresse, TLS/Proxy, Isolierung und Löschfreigabe für Testdaten, mindestens zwei getrennte technische Identitäten und zulässige Testzugänge. Testmatrix des [Integrationsvertrags](VIA-DOCS-API.md#integrations--und-abnahmematrix) einschließlich Admin-Key-freier Einlösung, Datei-Ownership, Range/HEAD, Preview-MIME, Limits, Fehler-/Retry-Header und qualifizierter Löschsemantik ausführen. Nötige Netz-/Ressourcenmessungen und Abnahmen werden D4/D5/D8 zugeordnet. Der technische Abschluss des Clients kann vor dem realen Nachweis erfolgen und ist keine Betriebsfreigabe.

#### Implementierungspakete

| Auftrag | Umfang und Abhängigkeit | Überprüfbare Übergabe |
| --- | --- | --- |
| D3-A | D3-01 und D3-02. D1 und abgenommene D2-Baseline als Regression, separater Auftrag und Freigabe der Cliententscheidungen. | Build, typisierte Grenzen, Transport-/Auth-/Cache-/Einladungstests, nachgewiesene einzelne POST-Versuche und dokumentierter Interface-Stand als Grundlage für D3-B und später D4. Keine reale Integrations- oder formelle D3-Gesamtabnahme. |
| D3-B | D3-03 und D3-04 nach technischer Teilprüfung von A. | Lese-/Write-Vertragstests, Ressourcen- und Limitnachweis, Persistenzübergabematrix und Befunde VD-01–08 mit konkreten Regressionstests. |
| D3-C | D3-05 nach A und B. | Vollständige reproduzierbare Regression und unabhängiges Review mit tatsächlichen Ergebnissen; reale Integration und Betriebsfreigabe gesondert ausweisen. Formeller Abschluss nur gemäß DoD. |

#### D3-INTEGRATION – Reale VIA.Server-Abnahme

- **Status:** Verbindlich offen. Der lokale D3-Abschluss erlaubt D4-PLAN und die weitere lokale D4-Entwicklung mit Testdoubles nach den jeweiligen Aufträgen.
- **Voraussetzungen:** Korrigierten veröffentlichten Build und seine Commit-Zuordnung, angewandte EF-Migrationen, gültige TLS-Kette und Zertifikatsübereinstimmung mit dem angesprochenen Host, Proxy-/Uploadlimits und Eignung des Betriebs ohne S3 nachweisen. Zwei technisch und fachlich getrennte Testidentitäten autorisieren. Credentials geschützt bereitstellen und Testupload, Löschung sowie den Einladungstest gesondert freigeben.
- **Abnahme:** Nur eigene isolierte Testdaten verwenden. Reale Datei-, Authentifizierungs-, Preview-, Range-, Lösch- und Redaktionsnachweise protokollieren. Zweifelhafte oder fehlende VIA.Docs-Tests hinsichtlich ihrer Relevanz für den veröffentlichten Betrieb ohne S3 bewerten. Production-S3 bleibt außerhalb der Voraussetzungen. [Testplan und Gates](D3-C-VERIFICATION.md#viaserver--testplan-für-einen-gesonderten-auftrag)

### D4 – Anwendungskonto, Einladung und JWT

- **Status** D4-PLAN einschließlich FIX ist unabhängig mit Note 1− (1,3) abgenommen. Alle sechs Planungsbefunde sind geschlossen. Die unabhängige statische Nachprüfung der 33 Paketdateien hat alle vier FIX-Befunde geschlossen und D4-A (D4-01 und D4-02) mit Note 1− (1,3) technisch freigegeben. Die Codex-Verifikation weist je 359 bestandene Backendtests in Debug und Release sowie 225 bestandene Frontendtests aus. Der Reviewer hat keine unabhängigen Testläufe ausgeführt. D4-B (D4-03 und D4-04) ist nach unabhängiger statischer Prüfung der 32 Paketdateien mit Note 1− (1,3) technisch freigegeben. Eine separate FIX-Runde ist nicht erforderlich. Die Codex-Verifikation ergab je 385 bestandene Backendtests in Debug und Release sowie 225 bestandene Frontendtests. Alle 359 Bestandstests sind erhalten. Für D4-B hat der Reviewer keine unabhängigen Testläufe ausgeführt. D4-C-FIX (D4-05 bis D4-07) ist nach unabhängiger statischer Prüfung der 25 Paketdateien mit Note 1− (1,3) technisch freigegeben. Alle drei FIX-Befunde sind geschlossen. Die Codex-Verifikation ergab je 435 bestandene Backendtests in Debug und Release bei vollständigem Erhalt der 410 Bestandstests sowie 225 bestandene Frontendtests. Der Reviewer hat keine unabhängigen Testläufe ausgeführt. [Umsetzungspakete, Verträge und Tests](D4-PLAN.md#7-umsetzungszuschnitt-und-abnahme-der-zwischenstände) · [D4-A-Nachweis](D4-A-VERIFICATION.md) · [D4-B-Nachweis](D4-B-VERIFICATION.md) · [D4-C-Nachweis](D4-C-VERIFICATION.md)

- **Freigabegate aus D2-05:** Registrieren mit `UserManager` im gemeinsamen Kontext und lokaler Transaktion über Einladungslock, Anlage und Einlösung. Application autorisiert Administratoroperationen und ordnet die normalisierte Rolle `ADMINISTRATOR` zu. Pending-/gesperrte Konten und bestehende Sitzungen fail closed prüfen. Authentifizierten Workspace-Kontext und Query-Filter vor fachlichen Endpunkten aktivieren. Kontoänderungen sperren zuerst den Workspace, danach Identity. Zusätzlich die isolierte Konkurrenz um die Identity-Zeile und die Wirkung von FOR SHARE OF u im tatsächlichen Deaktivierungsdienst prüfen (Endreview R-03). Bei verlorener Commit-Bestätigung reservierte Vorgangs-ID prüfen, bevor externe I/O zulässig ist. Nachgewiesenen Wiederanlauf nach Abschluss einer unklaren Registrierung getrennt freigeben und prüfen. Provisionierung erst nach reserviertem Commit extern beginnen und erst nach verifiziertem Login lokal abschließen. Prüfbarkeit von Secret-Redaktion, Zweckbindung und Data Protection nach ADR-010. Browserauthentifizierung erfolgt mit Cookie, VIA.Docs-JWT bleibt serverseitig.

- **Folgeaufgabe aus D1:** Die ProblemDetails-Felder `code` und `traceId` im generierten OpenAPI-Fehlerschema ausdrücklich als Strings typisieren und die Übereinstimmung mit den ausgelieferten Fehlerantworten durch Vertrags- und Generatortests nachweisen.

- **BACKEND-QUALITY-GATE-01.** Die konsolidierten Befunde zu Dispatch, Bound-Identity, Fehlerklassifikation und D2-Restore-Gate sind in BACKEND-QUALITY-GATE-01-FIX bearbeitet. Nachweise und Abnahmegrenzen stehen im [Auditnachweis](BACKEND-QUALITY-GATE-01-VERIFICATION.md).

- **D4-D-Stand.** D4-D-FIX3 und D4-D-INVITE-LINK sind statisch nachgeprüft und freigegeben. Der automatisierte HTTPS-Browsernachweis mit Testdouble ist bestanden. Die 45 Dateien sind statisch freigegeben. Die manuelle Anmeldung mit Benutzername und Passwort war erfolgreich. Tastatur-, Fokus- und Accessibility-Abnahme sowie Merge und Push stehen aus. [D4-FINAL-Nachweis](D4-FINAL-VERIFICATION.md)

- **D4-D-INVITE-LINK · statisch freigegeben.** Administratoren können nach der Einladungserstellung einen vollständigen Link im Format `https://<host>/#/register?invite=TOKEN` kopieren. Die ausdrücklich freigegebene Ausnahme zur URL-Regel in [D4-PLAN §3](D4-PLAN.md#3-einladung-registrierung-und-workspace) gilt ausschließlich für den einmaligen MiniDocs-Einladungsschlüssel im browserseitigen URL-Fragment. Die Registrierungsmaske übernimmt den Schlüssel in ihren Arbeitsspeicher und entfernt das Fragment per `history.replaceState`. Bei bestehender Sitzung erscheint ein Hinweis zur Abmeldung und zum erneuten Öffnen des Links. Der Server erhält den Schlüssel weiterhin im POST-Body des bestehenden Registrierungsendpunkts. Manuelle Eingabe bleibt verfügbar. Abgelaufene und verbrauchte Einladungen verwenden die bestehende HTTP-410-Behandlung.

- **Verbindliche Folgereihenfolge.** Auf den formellen D4-Abschluss folgen BACKEND-QUALITY-GATE-01, D3-INTEGRATION gegen den echten VIA.Docs-V1.2-Server, D6 und D5 zur Anbindung der bestehenden Dokumentenoberfläche und anschließend D7, erforderlicher UI-Feinschliff und D8. Die reale VIA.Docs-Integration ist vor der weiteren Fachimplementierung nachzuweisen. Die bestehende Dokumentenoberfläche bleibt das produktive Frontend. Aktive reguläre Benutzer erhalten später ihre eigenen Daten und die vorhandenen Bedienfunktionen. Die Administration bleibt ein rollenabhängiger Zusatzbereich. Im authentifizierten Betrieb werden keine Fachdaten simuliert.

- **Restpunkte aus D4-B.** Die Optionalität von `expiresAt` im generierten TypeScript-Vertrag und die tatsächliche HTTP-429-Antwort bei ausgeschöpfter Registrierungskapazität sind nachgewiesen. Fachliche HTTP-Isolation bleibt Teil von D6. D3-INTEGRATION, D8 und produktive Betriebsabnahmen bleiben offen.

- **Ziel:** MiniDocs Identity, geschlossene Registrierung und wiederanlauffähige technische Provisionierung nach ADR-001–012/017; Reasoning **extra hoch**.
- **Voraussetzungen/Schichten:** D1/D2 und D3-Schnittstelle; D4-A ist technisch abgenommen. D4-B ist technisch abgenommen. Die Detailfreigabe für D4-C ist erteilt. API besitzt Einladungs-, Login-, Logout-, Session-, Antiforgery-, Admin- und Provisionierungsrouten; Application steuert Autorisierung/Zustände; Infrastructure Identity, PostgreSQL, Data Protection und via D3 getrennte technische JWTs; Domain schützt Konto-/Workspace-Invarianten.
- **Akzeptanz:** reguläre Nutzer genau ein Workspace, Admin-only optional ohne Workspace und ohne privaten Zugriff; Einladungen einmalig/fristgebunden nach DB-Zeit nach Sperre; Cookie/Antiforgery vor/nach Login, Sperre bestehender Sitzungen und keine Secrets im Browser; geschütztes vorbereitendes Secret vor Remote-Registrierung, höchstens ein Vorgang, Unknown ohne Blind-Retry. Key-Ring dauerhaft/verschlüsselt und fail closed nach freigegebener Betriebskonfiguration.
- **Tests:** Unit für Zustände, Token-/Secretredaktion und Policy; echte PostgreSQL-Tests für parallele Einlösung/Expiry/Sperren und Provisionierungs-Leases, HTTP-Tests für CSRF/Login/Logout/Session-Invalidierung und admin-only. Testdouble für externe Registrierung, verlorene Antwort, verspäteten Worker sowie separate JWT-Caches. Realer Einladungs-/Deploymentnachweis erst nach gesonderter Freigabe; SEC-08–15 siehe Matrix. Keine produktiven Credentials oder Änderung von VIA.Docs. Technische/fachliche/Integrations- und Betriebsgates gemäß gemeinsamer DoD; Key-Ring-Restore in D8 zusätzlich nachweisen.

- **Nicht-Ziele:** Keine Änderung von VIA.Docs, keine produktiven Zugangsdaten und keine eigene Kryptografie.

### D5 – PDF-Upload, Download und Vorschau

- **Freigabegate aus D2-05:** Nachweisen, dass lokale Commitfehler, Timeout oder abgelaufener Fence keine erneute externe Schreibaktion auslösen. Echte Bytezahl und SHA-256 aus abgeschlossenen Streams prüfen. Dokumentversion und gebundene Remote-ID beim Abschluss weiterreichen. Alternative kontrollierte Wege wie nachweislich nicht gesendeter DELETE oder sicherer Wiederanlauf benötigen den dokumentierten externen Beleg und einen eigenen Zustandstest vor Aktivierung. Keine freihändige Zustandsrücksetzung. Aufruf- und Worker-Budgets verwenden höchstens 15 Minuten Lease. Bei unklarem lokalen Commit anhand der zuvor festgelegten Request-/Operations-ID klären, ob und welche Berechtigung existiert.

- **Ziel:** autorisierte Dateioperationen und Papierkorb nach ADR-004/011/017/018 und B0.7; Reasoning **extra hoch**.
- **Voraussetzungen/Schichten:** D1–D4 und D6 mit produktiven Themen-/Metadatenfunktionen, freigegebene 25-MiB-Grenze und Upload-ID-/Sperrmarken-/Receipt-Verträge sowie eine echte isolierte PostgreSQL-Testdatenbank. API streamt innerhalb Request-Lebensdauer, Application orchestriert lokale Zustandsübergänge, Infrastructure persistiert und nutzt D3, Domain prüft Invarianten; kein Netzwerk innerhalb DB-Transaktion.
- **Umfang:** Upload-Request-ID mit serverseitiger Länge/SHA-256 nur bei vollständigem Stream, Mehrfachanfragen, Status/Unknown/Lease-Fencing und 30-Tage-/Sperrmarkenvorschlag; Original GET/HEAD/Range, Preview inkl. SVG-Fallback, Seitennachführung, Papierkorb/Restore und finale Löschung samt 30-Tage-Löschreceipt. Eingehender MiniDocs-Stream wird nicht dauerhaft als PDF gepuffert; VIA.Docs selbst nutzt `ReadFormAsync`. Weiterverwendung von Viewer und bestehenden Bedienabläufen ohne Neugestaltung.
- **Akzeptanz/Tests:** Unit für Zustands- und Größenvalidierung; echte PostgreSQL-Integration mit HTTP-Testdoubles für gleichzeitige IDs, fremde Statusabfrage, verlorene Antwort, Abbruch vor/nach Versand, unbekannte Ergebnisse, verspäteten Worker, Themenlöschung gegen Uploadaktivierung/Restore, Restore gegen DELETE, Receipt-Ablauf und Sperrmarken. Keine ungesicherte externe Wiederholung, kein 204-als-physische-Löschung, keine neue Remote-Korrelation erfinden. Technischer Abschluss mit Testdoubles; gesondert realer Test für 25 MiB, Speicher-/Proxy-Pufferung, 200/206/304/416, Vorschau und Löschung; D8 Betriebsgate. Keine Änderung an VIA.Docs oder Produktionsdaten.

- **Nicht-Ziele:** Keine VIA.Docs-Änderungen, keine produktiven Dateien und keine vollständige dauerhafte PDF-Kopie in MiniDocs.

### D6 – Themen-, Tag- und Metadatenpersistenz

- **Verbindlicher Restpunkt aus D4-B.** Die fachliche Mandantenisolation vollständig mit HTTP-Ende-zu-Ende-Tests nachweisen.

- **Freigabegate aus D2-05:** Autorisierte Lese- und Schreibpfade mit aufgelöstem Workspace, aktivem Konto und EF-Query-Filtern prüfen. Für Bulk, Jobs und `IgnoreQueryFilters` explizite Prädikate und Negativtests belegen. Trimmen und deutsche Kleinschreibung für `name_key` einheitlich bei Create/Rename anwenden und gegen Frontend, Unicode und DB-Kollation testen. Atomare Mehrfachoperationen und ETag-Änderungen für alle betroffenen Dokumente nachweisen.

- **Ziel:** Thema-/Tag-/Dokumentmetadaten und bestehende Übersicht nach ADR-011/017 und B0.7; Reasoning **hoch**.
- **Voraussetzungen/Schichten:** D1/D2/D4 und freigegebene Metadatenverträge. Domain prüft Systemknoten/Zyklen; Application autorisiert, serialisiert und koordiniert; Infrastructure verwaltet PostgreSQL; API bietet Topic-/Tag-/Document-Routen und atomare Batch-Moves/Tagänderungen. React integriert die API schrittweise ohne visuelle Neugestaltung; vor D5 nur Dateitestdoubles.
- **Akzeptanz:** eigene Daten bleiben nach Reload erhalten; Inbox geschützt, Themenlöschung nur leer einschließlich Papierkorbdokumente; Parent-/Document-Änderungen atomar, Batch und Undo bleiben bei Konflikt konsistent. Metadaten-/Tagänderungen einschließlich indirekter Änderungen invalidieren korrekte ETags.
- **Tests:** Unit für Baum und Validierung; echte PostgreSQL-Races für gegenläufige Umzüge und Löschung gegen vorhandene Zuordnung, Fremd-FKs und ETag 412/428; Löschung gegen reale Uploadaktivierung/Restore ist nach D6 in D5 und abschließend D8 nachzuweisen; API-/React-Integration mit Testdoubles und Tab-Synchronisierung. Keine Transkript-/Suchimplementierung aus D7, keine Remote-Änderung. Technische/fachliche/Gesamtintegrationsgates gemäß oben; sichtbarer KU-Vergleich bleibt M8.

- **Nicht-Ziele:** Keine Transkript- oder Suchimplementierung aus D7 und keine Neugestaltung der Oberfläche.

### D7 – Transkripte und Volltextsuche

- **Freigabegate aus D2-05:** Seiten-API, Replace-All, Seitenzahlanpassung und Pending-Übernahme verwenden die versionierte Schreibgrenze. Beide ETags sowie konkurrierende Seitenanlage/-löschung prüfen. Importgrenzen, Mehrdeutigkeitsentscheidung die atomare fachliche Anwendung der Pending-Quellmetadaten vor ihrer Bereinigung und die atomare Seitenbereinigung beim Verwerfen nach B0.7 nachweisen. Der D2-Zuordnungsbefehl bereinigt die Originalseiten und reduziert den Kopf bereits.

- **Ziel:** persistente Transkripte, wartender Import und React-paritätische Suche nach ADR-011/017 und B0.7; Reasoning **extra hoch**.
- **Voraussetzungen/Schichten:** D1/D2/D4/D6 und freigegebene Import-/Suchverträge. Domain trägt Seiten-/Revisionsregeln, Application Zuordnung und Suche, Infrastructure persistiert/indiziert, API stellt Transkript-/Pending-/Suchrouten bereit; React übernimmt bestehenden Viewer und geplanten M7-B-TRANSKRIPT-Import. Echte PDFs erst mit D5.
- **Umfang/Akzeptanz:** Seiten-/Aggregatrevision atomar für Insert/Update/Delete und Replace-all; gleiche Dateinamen nie mehrdeutig automatisch zuordnen. KU-Referenz `.md`, `.markdown` und strukturgleiches `.txt` mit `# Dateiname.pdf`, `## Seite n` und belegten Metadaten; keine unbelegten Formate. Normalisierung, Phrasen/Teilstrings, UND, Datums-/Themen-/Tagfilter, Score/erste Trefferseite wie React; UUID-Tie-Breaker und cursorgebundene Filter, kein Snapshot versprochen.
- **Tests:** Unit für Parser, Unicode, Such- und Ersetzparität; echte PostgreSQL-Tests für parallele Seiteneinfügung und Replace-all, Pending-Doppelzuordnung, 412/428, Cursor-Manipulation und Cross-Tenant-Zugriff; API-/React-Test mit Testdoubles, repräsentative Performance-/Indexmessung ohne harte unbelegte Zusage. Keine KI-/semantische Suche oder D5-Implementierung. Technische/fachliche und spätere reale Integrations-/Deploymentgates gemäß gemeinsamer DoD.

- **Nicht-Ziele:** Keine KI-Transkription, semantische Suche, Scan-Zerlegung oder unbelegte Importformate.

### D8 – Integrations-, Sicherheits- und Betriebstests

- **Freigabegate aus D2-05:** Restore in getrenntem PostgreSQL-Ziel mit nachgewiesenen Rollen, ACLs und Schlüsselmaterial. Dasselbe lesende D2-06-Gate ausführen. Nur Datenbank-Verbindungsrechte außerhalb des Archives ergänzen. Objekt-ACLs, Besitzer und Default-ACLs vor der Prüfung unverändert lassen. Entschlüsselung mit wiederhergestelltem Data-Protection-Ring und Zertifikaten prüfen. Externe Schreibpfade bis zur Klärung offener VIA.Docs-Vorgänge gesperrt lassen. Workspace-Sperrkosten und wiederholte Parallelitätsläufe unter CI-Last messen. SEC-01 bis SEC-15 einschließlich Sitzungs- und Systemgrenzen vollständig zuordnen.

- **Ziel:** nach D1–D7 Sicherheits-, Integrations- und Betriebsgates prüfen; Reasoning **extra hoch**.
- **Voraussetzungen/Schichten:** D1–D7 technisch abgeschlossen, isolierte PostgreSQL- und freigegebene VIA.Docs-Testumgebung für reale Nachweise; ADR-013-Freigabe und benannter Betreiber vor Deployment. Schichten über öffentliche Grenzen prüfen; kein neuer Fachcode oder VIA.Docs-Änderungen ohne Auftrag. D8 kann Testdouble-Prüfungen vorbereiten, reale Freigabe nicht vorwegnehmen.
- **Akzeptanz:** SEC-01–15 und 13 Matrixfälle mit dokumentiertem Ergebnis, echte Tenantisolation beider Demo-Identitäten, freigegebener Einladungsweg, PDF-Upload/-Range/Preview und kontrollierter DELETE, große PDFs und tatsächliche Multipart-/Proxy-Pufferung. Backup/Restore von DB, Key Ring, Zertifikaten und Remote-Referenzen, Worker-/Lease-Abbrüche, Unknown und Recovery praktisch nachweisen; keine automatische Übernahme mehrdeutiger Dateien.
- **Tests:** Unit für Hilfsprüfungen, echte PostgreSQL-/HTTP-Integration, realer Dienst und vorgesehener Proxy mit freigegebenen Testdaten; Sicherheits-, Last-, Cancellation-, Neustart-, Rotations-, Restore- und Smoke-Tests. Testdouble-Erfolg ausdrücklich getrennt von realer Integration; UI-Gesamtabnahme M8 und Touch-Ausnahme bleiben separat. Kein produktiver Zugriff, keine Installation oder Produktionsfreigabe ohne gesonderten Auftrag. Bericht nennt Tests, Limits, Verantwortliche und gescheiterte/offene Gates.

- **Nicht-Ziele:** Keine VIA.Docs-Änderungen, produktiven Zugriffe oder Installation ohne gesonderten Auftrag.

### Sicherheits- und Integrationszuordnung

| Sicherheitsnachweis | Hauptverantwortung; ergänzender Gesamtnachweis D8 |
| --- | --- |
| SEC-01–04 eigene/fremde Dokumente lesen, ändern, löschen | D4 Autorisierung, D5 Dateifluss, D6 Metadaten |
| SEC-05–06 fremde Themen-/Tagzuordnung | D2 Datenbank-FKs, D6 API-/Batch-Tests |
| SEC-07 fremde Transkripte | D2 DB-Isolation, D7 API-/Seitentests |
| SEC-08 Administrator ohne Privatdaten | D4 Rechte-/Workspace-Tests |
| SEC-09 gesperrte bestehende Sitzungen | D4 Session-Invalidierung |
| SEC-10 parallele Provisionierung | D2 Constraint/Lease, D4 Provisionierungsablauf |
| SEC-11 Secrets in Antworten/Logs | D1 ProblemDetails/Logging, D3 Client, D4 Secret-Lifecycle |
| SEC-12–14 Neustart, Rotation und Restore | D4 Data Protection, D8 isolierter Systemrestore |
| SEC-15 JWT-Mandantenisolation | D3 Client-Caches, D4 Provisionierung/Auth |

Die **13 nummerierten Matrixzeilen** in [VIA.Docs-API](VIA-DOCS-API.md#integrations--und-abnahmematrix) sind wie folgt zugeordnet: 1 D3/D4/D8; 2 D3/D4/D8; 3 D4/D8; 4 D3/D4; 5 D3/D5/D8; 6 D2/D5/D8; 7 D5/D8; 8 D3/D5/D8; 9 D3/D5/D8; 10 D2/D5/D8; 11 D2/D4/D5; 12 D2/D4/D8; 13 D3/D8. D8 bündelt die gesonderte reale Integrations-/Betriebsabnahme, soweit die jeweilige Zeile reale Nachweise verlangt. Alle Zuordnungen sind Anforderungen, keine durchgeführten Tests.
## Q3 – Unabhängige Auditbefunde und Qualitätsregeln

- **Status:** technisch umgesetzt; Browser-/Screenreader-Abnahmen offen.
- **Ziel/Umfang:** Statusstyles in App-eigenes CSS Module verlegen, Callback-Test korrekt benennen, Wurzel-Löschfokus testen und Reviewregeln präzisieren.
- **Ausgeschlossen:** M3, neue Libraries, globale Formatierung und pauschale Refactorings.
- **Abhängigkeiten:** Q1/Q2 und konkrete Audit-Arbeitsaufträge.
- **Akzeptanz/DoD:** keine fachfremde Status-CSS-Abhängigkeit; Testaussage entspricht Assertions; Wurzel-Fallback auf „Nicht zugeordnet“ belegt; allgemeine DoD und Standards erfüllt.
- **Tests/Risiken:** bestehende Tests plus gezielter Wurzel-Löschtest; jsdom ersetzt keine Browser-/Screenreader-Abnahme.

## Q4 – Einheitliche Frontend-Formatierung

- **Status:** technisch abgeschlossen; Einzelheiten und Nachweise im [Ticketarchiv](TICKETS-ARCHIV.md#q4--einheitliche-frontend-formatierung). Die Formatprüfung bleibt Bestandteil von `npm run check`.

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

## UI-POLISH – VIA Professional Design System

- **Status:** verbindlich geplant nach der funktionalen Entwicklung; keine Implementierungsfreigabe. Repräsentative Light-/Dark-Designstudien vorab erstellen und durch den Projektverantwortlichen freigeben lassen.
- **Ziel:** eigenständige, konsistente VIA-Farbwelt für professionelle Desktop-Webanwendungen; perspektivisch für weitere VIA-Anwendungen nutzbar, ohne separate Design-System-Library in V1.
- **Umfang:** semantische Tokens und abgestimmte Light-/Dark-Oberflächen, professionelle Typografie, Abstände, Größen, dezente Schatten, Rahmen und Rundungen; einheitliche Hover-, Active-, Selected-, Focus- und Disabled-Zustände, Dialoge, Menüs, Tooltips und Icons; dezente Animationen mit `prefers-reduced-motion`, WCAG-AA-Kontraste, klare Hierarchie und angemessene Informationsdichte.
- **Referenzen:** [MUI Color](https://mui.com/material-ui/customization/color/), [MUI Default Theme](https://mui.com/material-ui/customization/default-theme/), [Material Theme Builder](https://material-foundation.github.io/material-theme-builder/), [Material Symbols](https://fonts.google.com/icons) und Material Design 3 dienen als Inspiration. Vor konkreter Ressourcenübernahme Lizenz prüfen; selektive Werkzeuge, Icons oder Komponenten nur mit nachgewiesenem Vorteil gesondert vorschlagen.
- **Grenzen:** bestehendes KU-Layout, Fachlogik, CSS Modules und zentrale Design-Tokens erhalten; keine MUI-, Emotion- oder Tailwind-Installation, kein paralleles Styling-System, keine unkontrollierten Overrides, keine ungefragte Bibliothek und keine Verschlechterung von Accessibility, Performance oder Testbarkeit.
- **Abnahme:** freigegebene Designstudien, dokumentierte Kontrastmessungen und Browser-/Tastatur-/Screenreaderprüfung beider Themes; bestehende funktionale und visuelle Abnahmen bleiben gültige Anforderungen.

## UI-DEMO-30 – Fiktiver PDF-Datenbestand

- **Status:** UI-DEMO-30 einschließlich Demo-Integration, PDFs, Transkripten und PDF-Miniaturen fachlich freigegeben. Der Projektverantwortliche bestätigt Kachelansicht, PDF-Miniaturen, Download und Dokumentverschiebung persönlich; ein vollständiger KU-Vergleich und weitere nicht nachgewiesene Browser-/Screenreader-Abnahmen bleiben offen.
- **Daten:** 30 lokale PDF-Dokumente, 207 Seiten, 192 Transkriptseiten; Quellen und Umfang siehe [README](../README.md#lokaler-demo-datenbestand). Der Produktionsbuild enthält den Datenbestand nicht.
- **Risiken:** gemischte Seitenformate, lange PDFs und Ressourcenverhalten benötigen weitere Browser-/Lastprüfungen. Der Produktionsbuild meldet weiterhin ein JavaScript-Chunk über 500 kB; die bestehende Warnung bleibt als technische Schuld offen. UI-POLISH bleibt gesondert geplant.
