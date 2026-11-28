# D6 – Themen-, Tag- und Metadatenpersistenz

## D6-FINAL-CLOSURE – Abnahme

D6 ist technisch und funktional abgenommen. Das angenommene kritische Abschlussreview bewertet den Stand mit Note 1,3 und weist 0 Critical, 0 High, 0 Medium sowie einen redaktionellen Low-Befund zum damals veralteten Abnahmestand aus. Der Review wurde von einem an den Abnahmekorrekturen beteiligten Bearbeiter durchgeführt. Der formale Git-Abschluss steht aus.

Der Projektverantwortliche bestätigt die manuelle Nachabnahme:

| Prüfung | Ergebnis |
| --- | --- |
| D6-UX-03 – Themenauswahl beim Browser-Tabwechsel | Bestanden |
| D6-UX-02 – Direkte Tag-Anlage einschließlich Dialogkorrektur | 8/8 bestanden |
| D6-UX-01 – Undo-Sichtbarkeit | 8/8 bestanden |
| D6-FUNC-01 – Themennamen nach Datenbankupgrade | 10/10 bestanden |
| Ergänzende Tag-Konfliktprüfung | 6/6 bestanden |
| Persistentes Testdatenbankupgrade einschließlich Kataloggate und Datenerhalt | Bestanden |

Der letzte vollständige automatisierte Nachweis umfasst 475/475 Backendtests in Debug und Release, 278/278 Vitest und 12/12 Playwright. Nach der späteren Dialogkorrektur bestanden 18 Komponententests sowie Formatierung, Typecheck und CSS-Lint. PERF-01, D3-INTEGRATION und die übrigen offenen Betriebs- und Abnahmegates bleiben bestehen.

## D6-FINAL-ACCEPTANCE-FIXES

Ausgangsstand auf `fix/d4-d-fix2` bei `88950d7e3722dc418884bc5a662b489bf7f17051` mit beabsichtigten lokalen D6-Änderungen. Letzter Nachweis 469 Backendtests je Konfiguration, 275 Vitest-Tests und 11 Playwright-Tests.

- **D6-FUNC-01.** Die freigegebene Fachänderung ersetzt die frühere B0.6-Regel. Migration `20261128110000_TopicSiblingNames` sperrt die Thementabelle während der Vorprüfung und Installation. Bei bestehenden Kollisionen oder belegten reservierten Hauptnamen bricht sie transaktional ab. Der eindeutige PostgreSQL-Index erfasst NULL-Parents, der CHECK schützt reservierte Namen und das Restore-Gate überprüft beide Definitionen. PATCH schreibt die endgültige Kombination mit einer Versionsanhebung und erhält Autorisierung, Workspace-Sperre, ETag und Zyklusprüfung. HTTP 409 liefert `topic_name_conflict` oder `topic_name_reserved`.
- **D6-UX-02.** Ein Formular für beide Dialogpfade. Die Zuweisung nutzt die ursprüngliche Dokumentauswahl und die zugehörigen ETags und läuft über den bestehenden atomaren Batch. Kann der zweite Aufruf nicht bestätigt werden, zeigt der Dialog die bestätigte Anlage und die unbestätigte Zuweisung, lädt den Workspace neu und führt keine automatische Wiederholung aus.
- **D6-UX-01.** Der bestehende Undo-Vorgang erhält einen sichtbaren Status mit Anzahl, eine fokussierbare Schaltfläche und einen Screenreader-Status. Die Zielbezeichnung wird weggelassen, weil der Undo-Stand die ursprünglichen Themen und keinen stabilen Zielnamen speichert.

### Prüfungen

| Prüfung | Ergebnis | Exitcode |
| --- | --- | --- |
| `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter FullyQualifiedName~TopicSiblingNames` | 4/4 mit isoliertem PostgreSQL. Erstversuch im Standard-Buildpfad durch laufende API gesperrt. Ausweichlauf zunächst mit fehlendem CancellationToken-Argument im Test. Beides korrigiert. | 0 nach zwei Läufen mit 1 |
| `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --logger "trx;LogFileName=D6-Acceptance-Debug.trx"` | 475/475, 0 übersprungen, einschließlich Rolle, Konkurrenz, Migration und Restore. | 0 |
| `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 -c Release --logger "trx;LogFileName=D6-Acceptance-Release.trx"` | 475/475, 0 übersprungen. | 0 |
| `npm.cmd --prefix backend run check` und `npm.cmd --prefix backend run typecheck` | OpenAPI und generierter TypeScript-Vertrag stimmen überein. Backend-Vertragsbuild mit 0 Warnungen und 0 Fehlern. TypeScript-Vertragsassertions bestanden. | je 0 |
| `npm.cmd --prefix frontend run check` | Prettier, TypeScript, ESLint, Stylelint, 278/278 Vitest in 36 Dateien und Vite-Build. | 0 |
| `npm.cmd --prefix frontend run test:e2e` | 12/12 Playwright einschließlich Tabwechsel, Undo, persistenter Tag-Zuweisung nach Reload und dreier visueller Referenzen. Browser-Testhost 1/1. | 0 |
| `git diff --check`, Kontrolle neuer Textdateien, `git diff --cached --quiet` und `git diff --quiet HEAD -- frontend/e2e/reference` | Keine Whitespace-Befunde, leerer Index und drei bytegleiche Bildreferenzen. | je 0 |

Der Namensvergleich der TRX-Dateien mit den vorherigen D6-Final-Läufen bestätigt alle 469 Bestandstests in beiden Konfigurationen. Die Backend- und Vertragsläufe nutzten `BaseOutputPath=bin/d6-acceptance/`. Die Testläufe verwendeten `DOTNET_PROCESSOR_COUNT=4` und `DOTNET_gcServer=0`. Der laufende lokale API-Prozess blieb in Betrieb. Seine Datenbank blieb unangetastet. Für diese drei Korrekturen ist kein absichtlich fehlschlagender Vorher-Lauf aufgezeichnet.

### Dateiumfang

| Geänderte Dateien | Zweck |
| --- | --- |
| `WorkspaceMetadataHttp.cs` | Spezifische HTTP-Konflikte für PostgreSQL-Verletzungen erst nach Verlassen der lokalen Schreibtransaktion zurückgeben. |
| `20261128110000_TopicSiblingNames.cs`, `D6/TopicSiblingNames.sql` | Neue Migration registrieren, Vorprüfung und Integritätsregeln installieren sowie die atomare Themenänderung hinter der vorhandenen Schreibgrenze implementieren. |
| `GrantRuntimePrivileges.sql` | Definition des neuen Indexes, des CHECKs und des aktualisierten Schreibkommandos migrationsabhängig prüfen. |
| `WorkspaceMetadataNormalizationTests.cs`, `PostgresRoleAndRestoreTests.cs`, `PostgresFinalHardeningTests.cs` | Normalisierung, Workspace-Abgrenzung, atomaren PATCH, blockierende Konkurrenz, Upgrade-Abbruch, Restore und Katalogdrift mit PostgreSQL nachweisen. |
| `App.tsx`, `App.module.css`, `topicUtils.ts` | Demo-Namensregel in allen Bedienpfaden anwenden, den bestehenden Dialog anbinden und die Undo-Anzeige gestalten. |
| `TagDialog.tsx`, `TagDialog.module.css`, `usePersistentWorkspace.ts` | Vorhandene Anlage im Zuweisungsdialog wiederverwenden, Auswahl und ETags mitführen, Zuweisung ausführen und Teilfehler anzeigen. |
| `TagUI.test.tsx`, `PersistentMetadata.test.tsx`, `topicUtils.test.ts`, `metadata.pw.ts` | Abbruch und Mehrfachauswahl, Teilfehler mit ursprünglichen ETags, Demo-Namensregel sowie Undo und persistente Zuweisung nach Reload prüfen. |
| `ARCHITECTURE.md`, `DECISIONS.md`, `MASTERPLAN.md`, `TICKETS.md`, `D6-VERIFICATION.md` | Freigegebene Fachänderung, Projektstand, Abnahmegrenzen und Nachweise festhalten. |

Die oben bestätigte manuelle Nachabnahme umfasst die vier Abnahmekorrekturen und die ergänzende Tag-Konfliktprüfung. Für weitere Datenbankupgrades bleiben die Vorprüfung auf Namenskonflikte, die exklusive Tabellensperre und eine geprüfte Sicherung zu berücksichtigen. Die Migration ändert keine bestehenden Themenzeilen. PERF-01, D3-INTEGRATION und alle bisherigen Betriebsabnahmen gelten fort.

## D6-UX-03 – Themenauswahl beim Tabwechsel

Die Sichtbarkeitsbehandlung leerte die Sitzung und entfernte die persistente Übersicht aus dem React-Baum. Der anschließende Neuaufbau initialisierte die lokale Themen-ID am Systemknoten. Der neue React-Test reproduzierte den Verlust nach erfolgreicher erneuter Sitzungsabfrage.

Eine Sichtbarkeitsprüfung bei aktiver Workspace-Sitzung lässt die Übersicht eingehängt und blendet sie bis zur Serverantwort aus. Bestätigt der Server denselben Benutzer und denselben aktiven Workspace, bleibt die Komponentenidentität erhalten und der vorhandene Snapshot-Loader aktualisiert die Daten. Umbenennungen und Verschiebungen behalten die Themen-ID. Ist die ID im bestätigten Snapshot entfallen, greift die bestehende Auswahl des Systemknotens. Eine abweichende oder fehlgeschlagene Sitzung verwirft den privaten Zustand. Benachrichtigungen über Sitzungswechsel und die Metadaten-Synchronisierung behalten ihre getrennten Kanäle.

Der Projektverantwortliche hat den realen Tabwechsel erfolgreich manuell nachgetestet. PERF-01 und die separaten Integrationsgates gelten fort.

| Prüfung | Ergebnis | Exitcode |
| --- | --- | --- |
| Neuer AuthApp-Regressionsfall vor dem Fix | Erwarteter Fehler. Nach der Sitzungsabfrage fehlt die Überschrift des gewählten Themas. | 1 |
| Gezielte AuthApp-Tests nach dem Fix | 16 bestanden. Der neue Fall prüft auch geänderte Metadaten, gelöschtes Thema, Workspace-Wechsel und 401. | 0 |
| `npm --prefix frontend run check` und `npm --prefix frontend test` | Vollständiger Frontendcheck mit Typen, Lint und Produktionsbuild bestanden. Vitest bestätigt 275 Tests in 36 Dateien. Die erste Check-Ausführung meldete TS2769 für eine Testing-Library-Option. Die Selektoren wurden korrigiert. | je 0 nach Korrektur |
| `npm --prefix frontend run test:e2e` | 11 bestanden. Einschließlich zweier Tabwechsel, bestehender Zweitab-Konkurrenzprüfung und drei unveränderter visueller Referenzen. | 0 |
| `git diff --check` und ergänzende Prüfung der unversionierten D6-Dateien | Keine Whitespace-Fehler, Index leer. | 0 |

Der Playwright-Fall wechselt zwischen zwei Tabs und wartet auf die echte Session-HTTP-Antwort. Falls Headless Chromium beim Vordergrundwechsel beide Tabs sichtbar meldet, löst der Test das Sichtbarkeitsereignis gezielt aus. Diese Variante deckt den React- und HTTP-Pfad ab. Die manuelle Rückmeldung des Projektverantwortlichen bestätigt den Tabwechsel im Zielbrowser.

Ausgangscommit `88950d7e3722dc418884bc5a662b489bf7f17051` auf `fix/d4-d-fix2`. Der vorbestehende D3-Integrationsnachweis bleibt erhalten. D6 verwendet lokale Daten und Testdoubles.

## Früherer Freigabestand vor den Abnahmekorrekturen – 28.11.2026

FIX03 ist nach unabhängiger statischer Abschlussprüfung des Exports `VIA.MiniDocs-BACKEND-AUDIT-20261128-121419-f12a059e.zip` technisch freigegeben (Note 1− / 1,3; 0 Critical, 0 High, 0 Medium). Die neun ursprünglichen Befunde sind geschlossen. Die beiden Low-Restbefunde N-01/N-02 sind mit D6-FINAL-LOW korrigiert; die vollständige lokale Regression ist bestanden. Die Note bezieht sich auf den unabhängig geprüften FIX03-Stand; eine erneute unabhängige Prüfung des FINAL-LOW-Patches wird nicht behauptet. Der Reviewer hat die Tests nicht selbst ausgeführt. Performance bleibt als [PERF-01](TICKETS.md#perf-01--performance-und-skalierungsabnahme) verbindliches separates Produktivfreigabegate.

### D6-FINAL-LOW – Abschlusskorrekturen

N-01: HTTP-Längengrenzen zählen Unicode-Codepoints gemäß PostgreSQL `char_length()` und OpenAPI; Grenzen und JSON-Byte-Limit unverändert. Ein HTTP-Test prüft Create/PATCH-Grenzen, supplementary Unicode, Ablehnung der Überschreitung und unveränderte Daten/ETags. N-02: Das lesende Restore-Gate prüft `tags.name_key` auf Typ `text` und NOT NULL; ein negativer Katalogtest verwendet die vorhandene Manipulations-/Rollback-Infrastruktur. Keine Änderung der Migration, SQL-Schreibfunktionen, Rechte, OpenAPI-Schemas oder UI. EF-Core-Empfehlungen sind in die Engineering-Standards übernommen.

### D6-FINAL-LOW – vollständiges Regression-Gate

Quelle: vorgelegtes `D6-FINAL-REGRESSION-Konsolenprotokoll.txt` vom 28.11.2026, Arbeitsbranch `fix/d4-d-fix2`. Der PowerShell-Block prüft nach jedem der sieben Hauptkommandos `$LASTEXITCODE -ne 0` und bricht bei Fehler mit `throw` ab. Die abschließende Ausgabe `D6 REGRESSION GATE: ALL PASSED` wurde erreicht. Exitcode 0 ist damit für diese sieben Kommandos aus dem erfolgreichen Kontrollfluss ableitbar; numerische Exitcodes sind nicht separat protokolliert.

| Tatsächlich ausgeführtes Prüfkommando | Ergebnis | Exitcode |
| --- | --- | --- |
| `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --logger "trx;LogFileName=D6-Final-Debug.trx"` | 469/469, 0 fehlgeschlagen, 0 übersprungen; 69,0 s Tests / 70,2 s gesamt | 0 |
| `dotnet test backend/VIA.MiniDocs.sln -c Release --no-restore -m:1 --logger "trx;LogFileName=D6-Final-Release.trx"` | 469/469, 0 fehlgeschlagen, 0 übersprungen; 73,2 s Tests / 76,7 s gesamt | 0 |
| `npm.cmd --prefix backend run check` | OpenAPI TypeScript contract matches; Build erfolgreich | 0 |
| `npm.cmd --prefix backend run typecheck` | tsc erfolgreich | 0 |
| `npm.cmd --prefix frontend run check` | Prettier, TypeScript, ESLint, Stylelint, 274/274 Vitest in 36 Dateien und Vite-Produktionsbuild erfolgreich | 0 |
| `npm.cmd --prefix frontend run test:e2e` | 10/10 Playwright (19,3 s), einschließlich drei visueller Referenztests; Browser-Testhost 1/1, 0 übersprungen, 24,1 s Testdauer / 27,1 s gesamt | 0 |
| `git diff --check` | Keine Diff-Fehler; vier Zeilenendenhinweise (LF/CRLF) | 0 |
| `git status --short --branch` | Arbeitsbranch `fix/d4-d-fix2`; geänderte und unversionierte Dateien vorhanden | Nicht separat geprüft |

Die Backend-Gesamtsuite wächst von 467 auf 469 Tests je Konfiguration; alle 469 bestehen. Der separate gefilterte FINAL-LOW-Lauf ist im vorgelegten Protokoll nicht enthalten und wird nicht als ausgeführt behauptet. Die vollständige Suite enthält nach dokumentiertem Änderungsumfang die beiden neuen Fälle; einzelne Testnamen und TRX-Rohdateien wurden nicht separat ausgewertet. Das Protokoll enthält Fehler- und Warnlogs aus den Testhosts; die Testzusammenfassungen melden keine fehlgeschlagenen Tests. Die TRX-Ausgaben wurden beim finalen Lauf überschrieben. Die doppelte Playwright-Konsolenausgabe wird als ein Lauf mit zehn Tests gewertet, nicht als zwanzig Tests.

**Damals offene Freigabepunkte:** Die oben dokumentierte D6-Nachabnahme ist inzwischen bestätigt. Die gesonderte D4-Tastatur-/Fokusprüfung und der formale Git-Abschluss bleiben offen. Reale D3-INTEGRATION und PERF-01 bleiben eigenständige Gates. Für weitere Datenbankupgrades gelten die aktuelle Migrationsvorprüfung und der dokumentierte Wiederherstellungsweg.

## Frühere Verifikationsstände (Nachweisverlauf)

Die folgenden Abschnitte halten die damaligen Paketstände fest; dort als ausstehend bezeichnete Prüfungen sind durch die oben dokumentierten Ergebnisse überholt, soweit dort ausdrücklich bestätigt.

### FIX03 – unabhängiger Abschlussreview und Regression vor D6-FINAL-LOW

FIX03 ist nach unabhängiger statischer Abschlussprüfung des Exports `VIA.MiniDocs-BACKEND-AUDIT-20261128-121419-f12a059e.zip` technisch freigegeben (Note 1− / 1,3; 0 Critical, 0 High, 0 Medium). Neun ursprüngliche Befunde geschlossen, Performanceoptimierung messwertabhängig zurückgestellt, Dokumentationsabschluss zum damaligen Prüfzeitpunkt offen. Der Abschlussreview nennt zwei Low-Restbefunde N-01 (Unicode-Längen) und N-02 (`tags.name_key` NOT NULL im Restore-Gate). Die folgenden lokalen Konsolennachweise betreffen den geprüften FIX03-Stand, **vor D6-FINAL-LOW**. Der Reviewer hat die Tests nicht selbst ausgeführt.

| Prüfkommando | Ergebnis |
| --- | --- |
| `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --logger "trx;LogFileName=D6-Final-Debug.trx"` | 467/467, 0 übersprungen, 58,3 s Tests / 59,5 s gesamt |
| `dotnet test backend/VIA.MiniDocs.sln -c Release --no-restore -m:1 --logger "trx;LogFileName=D6-Final-Release.trx"` | 467/467, 0 übersprungen, 60,2 s Tests / 63,2 s gesamt |
| `npm.cmd --prefix backend run check` | OpenAPI TypeScript contract matches |
| `npm.cmd --prefix backend run typecheck` | tsc erfolgreich |
| `npm.cmd --prefix frontend run check` | Prettier, TypeScript, ESLint, Stylelint, 274/274 Vitest, Produktionsbuild erfolgreich |
| `npm.cmd --prefix frontend run test:e2e` | 10/10 Playwright (15,0 s), alle drei visuellen Referenztests; Testhost 1/1, 21,7 s gesamt |
| `git diff --check` und `git status --short --branch` | Keine Diff-Fehler, Zeilenendenwarnungen, Arbeitsbranch `fix/d4-d-fix2` |

Die vollständige Konsolenausgabe wurde vorgelegt. Numerische Prozess-Exitcodes wurden nicht gesondert ausgegeben; die erfolgreiche Beendigung ist aus den Ergebnismeldungen ersichtlich. TRX-Rohdateien wurden bei dieser Prüfung nicht separat ausgewertet.

## Änderungsumfang

| Datei | Fachlicher Zweck |
| --- | --- |
| `WorkspaceMetadataHttp.cs` | Workspace-gebundene B0.7-Endpunkte, starke ETags, atomare Batch- und Undo-Verträge. |
| `Program.cs` | D6-Routen einbinden und ihre Header sowie Parameter im OpenAPI-Vertrag ausweisen. |
| `fixture.generated.ts` | Die D6-DTOs und Routen aus dem OpenAPI-Host bereitstellen. |
| `WorkspaceMetadataPersistence.cs` | Typisierte Aufrufe über die bestehende D2-Kommandogrenze. |
| `20261128100000_WorkspaceMetadata.cs` und `D6/WorkspaceMetadata.sql` | Additive, versionierte Schreibkommandos. Änderungsrechte nur für den Command Owner. |
| `VIA.MiniDocs.Infrastructure.csproj` | Neue SQL-Migration als Ressource einbetten. |
| `GrantRuntimePrivileges.sql` | Neue Funktionen, ihre Hashes und eng begrenzte Spaltenrechte im Restore-Gate prüfen. |
| `WorkspaceMetadataHostTests.cs` | Reale Laufzeitrolle, HTTPS-Sitzungen, Fremd-IDs, ETags, Batch-Rollback, Undo, Tag-Normalisierung, indirekte ETags und gegenläufige Themenbewegungen prüfen. |
| `BrowserRegressionTests.cs` und `SessionHostTests.cs` | Eigenen aktiven Benutzer mit Dokument ausschließlich für lokale Tests bereitstellen. |
| `metadata.pw.ts` | Die echte persistente Oberfläche mit Drag & Drop, Undo und Reload prüfen. |
| `AuthApp.tsx` und `PersistentApp.tsx` | Aktive Sitzung an die bestehende Dokumentübersicht anschließen, Administration separat erreichbar halten. |
| `usePersistentWorkspace.ts` | Gleiche Herkunft, vorhandener CSRF-Client, API-Zustand und Konfliktbehandlung, versionsgebundenes Undo und Tab-Abgleich. |
| `App.tsx` und `AppShell.tsx` | Dieselbe Übersicht für Demo und persistente Daten verwenden und D5-Dateiaktionen im persistenten Modus sperren. |
| `useDocumentWorkspace.ts` und `useDocumentDrag.ts` | Bestehende Auswahl, Ansicht und Drag & Drop mit bestätigten persistenten Schreibresultaten verbinden. |
| `document.types.ts` und `DocumentSummary.tsx` | Persistente Metadaten als solche anzeigen und die synthetische Demovorschau dafür unterdrücken. |
| `TagDialog.tsx` | Persistente Aktionen erst nach der Serverbestätigung als erfolgreich anzeigen. |
| `MASTERPLAN.md`, `TICKETS.md`, `D6-VERIFICATION.md` | Stand, Nachweise und Freigabegrenzen dokumentieren. |

## Abnahmegrenzen

D5 bleibt für produktive Dateioperationen zuständig. Die D6-Tests verwenden lokale Dokumentmetadaten. Die D7-Such- und Transkriptpfade sowie die ausstehenden realen D3-INTEGRATION-Nachweise bleiben gesondert.

## Prüfstand

Vor D6-FIX02 waren folgende lokale Prüfergebnisse nachgewiesen:

- Backend Debug: 439/439 nach D6-FIX01; TRX `D6-FIX01.trx`.
- Backend Release: 439/439, 0 übersprungen; TRX `D6-Release.trx`, Gesamtdauer 61,5 s.
- Frontend: 266 Vitest-Tests, vollständiger `npm run check` erfolgreich.
- API: `npm --prefix backend run check` meldete `OpenAPI TypeScript contract matches`.
- Playwright: 9/9 einschließlich D6 Drag & Drop, Undo, Reload und aller drei visuellen Referenzen; 19,9 s einschließlich Build.
- Build-Artefaktkorrektur: `backend/Directory.Build.props` schließt `bin/**` und `obj/**` aus. Standardbuild 6,2 s, alternativer Ausgabepfad 3,7 s; kein verschachteltes `bin`.

## D6-FIX02 – Versionskonflikte und UI-Zustand

Der statische Review des Exports `VIA.MiniDocs-BACKEND-AUDIT-20261128-105619-32bac1da.zip` ergab Lücken in Versionsbindung und asynchroner Dialogsteuerung. D6-FIX02 hält beim Bearbeiten/Bestätigen und Drag-Beginn gelesene Versionen fest. Tag-Neuanlage und -Bearbeitung sind explizit getrennt. Gelöschte Tags werden aus effektiven Filtern entfernt. Bei fehlgeschlagener Aktualisierung bleiben weitere Schreibaktionen bis zum bestätigten Reload gesperrt; bestätigte Änderungen und unklare Ergebnisse werden unterschieden. Dialoge verhindern konkurrierende Aktionen während eines Writes; verspätete Drop-Bestätigungen überschreiben keine neuere Navigation.

Neu: sechs gezielte Vitest-Regressionstests und ein Browser-Test mit zwei Tabs. Die lokale Ausführung am 28.11.2026 nach FIX02 bestätigte `npm.cmd --prefix frontend run check`: Prettier, TypeScript, ESLint, Stylelint, 272/272 Vitest (35 Dateien, 25,70 s) und Produktionsbuild (280 ms) erfolgreich. `npm.cmd --prefix frontend run test:e2e`: 10/10 einschließlich Zwei-Tab-Test und drei visueller Referenzen, 20,7 s einschließlich Build. Die übermittelten Konsolenprotokolle zeigen erfolgreichen Abschluss; numerische Prozess-Exitcodes wurden nicht gesondert ausgegeben. Die abschließende Regressionsfreigabe nach FIX03 steht aus. Backend, Vertragsdateien, CSS und visuelle Referenzen sind durch FIX02 unverändert. Die bisherige Freigabegrenze (manuelle Abnahme, vollständige D3-INTEGRATION, unabhängiger Review und Git-Abschluss) bleibt offen.

## D6-FIX03-A – Sicherheits- und Konkurrenznachweise

Drei unabhängige statische Reviews des Exports `VIA.MiniDocs-BACKEND-AUDIT-20261128-111508-c4c1204a.zip` meldeten keine Critical-Befunde, aber insbesondere fehlende D6-Pflichtnachweise. `WorkspaceMetadataQualityTests.cs` ergänzt vier HTTP-Testmethoden und drei deterministische PostgreSQL-Race-Fälle: beidseitige Workspace-Isolation, gemischte Batches in beiden Reihenfolgen, Admin-only und deaktivierte Sitzungen, fehlendes/ungültiges CSRF bei allen D6-Mutationen, kombinierter Dokument-PATCH mit injiziertem Fehler nach der Themenverschiebung, Konkurrenz mit gleichem ETag, veraltetes Undo mit Rollback und indirekte ETags mehrerer betroffener Dokumente. Die Race-Fälle verwenden die echte Runtime-Rolle und beobachten blockierte PostgreSQL-Verbindungen. Produktionscode und bisherige Tests bleiben unverändert.

Prüfung offen: `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "FullyQualifiedName~MetadataHttpIsolationIsBidirectional|FullyQualifiedName~MetadataHttpRequiresWorkspaceAuthority|FullyQualifiedName~MetadataHttpCombinedDocumentPatch|FullyQualifiedName~MetadataHttpTagRenameAndDelete|FullyQualifiedName~MetadataPostgres" --logger "trx;LogFileName=D6-FIX03-A.trx"`. Erwartet: sieben neue Testfälle; noch nicht lokal kompiliert oder ausgeführt. Die bisherigen 439 Backendtests bleiben unverändert. Vollständiges Regression-Gate nach FIX03, Vertrags-/Validierungsfehler, Restore-Gate, Upgrade/Restore und Schichtentrennung weiterhin offen.

## D6-FIX03-A – lokaler Prüfnachweis

Am 28.11.2026 bestätigte das übermittelte Konsolenprotokoll `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-A" --logger "trx;LogFileName=D6-FIX03-A.trx"`: alle fünf Projekte kompiliert, 7/7 neue Testfälle bestanden, 0 übersprungen, Gesamtdauer 16,4 s (Testdauer 10,3 s). Kein vollständiger Regressionstest nach FIX03-A; 446 Backendtests werden vor weiteren Ergänzungen erwartet. Der TRX-Rohinhalt wurde nicht erneut geprüft.

## D6-FIX03-B1 – Dokumentcursor

Dokumentcursor verwenden einen eigenen Data-Protection-Zweck und speichern den ursprünglichen primären Sortierschlüssel (Titel oder nullable Datum) und UUID, gebunden an Workspace, Filter, Sortierung und Limit. Die Fortsetzung benötigt keinen erneuten Anker-Lookup. Ein bestandsweiter Snapshot wird weiterhin nicht zugesichert. Alte Dokumentcursor werden bewusst ungültig; Admin- und Tagcursor bleiben unverändert. UTF-8-Kodierung der Titel schützt das vereinbarte Cursor-Limit auch bei langen Unicode-Titeln. Fünf neue PostgreSQL-HTTP-Testfälle prüfen Sortierung, Gleichstände, NULL, Ankeränderung/-verschiebung/-Löschung, Cursorbindung und maximale Titellänge. Lokale Kompilierung und Ausführung stehen aus: `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-B1" --logger "trx;LogFileName=D6-FIX03-B1.trx"`. FIX03-B2 (Eingabevalidierung, Normalisierung, OpenAPI), FIX03-C/D und vollständige Regression bleiben offen.

## D6-FIX03-B1 – lokaler Prüfnachweis

Das übermittelte Konsolenprotokoll vom 28.11.2026 bestätigt `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-B1" --logger "trx;LogFileName=D6-FIX03-B1.trx"`: alle fünf Projekte kompiliert, 5/5 Testfälle erfolgreich, 0 übersprungen, Gesamtdauer 11,9 s (Testdauer 8,6 s). Erwarteter Gesamtbestand nach B1: 451; vollständige Regression ausstehend.

## D6-FIX03-B2A – Eingabevalidierung und OpenAPI

Explizites JSON-null wird für nicht-nullable D6-PATCH-Felder zurückgewiesen, optionale Felder bleiben weglassbar. Datum und Parent dürfen explizit gelöscht werden. Tag-Create und Tag-Patch haben getrennte DTOs. PostgreSQL-inkompatible NUL-Zeichen werden an der HTTP-Grenze abgewiesen. D6-OpenAPI-Schemas beschreiben die Optionalität, nullable Felder, Enum-Werte und numerische Seitenzahl; TypeScript-Vertragsassertions werden ergänzt. Drei neue HTTP-Testfälle prüfen Negativfälle, unveränderte Ressourcen/ETags und erlaubte PATCH-Semantik. Generierter Vertrag muss lokal mit `npm.cmd --prefix backend run generate` aktualisiert und anschließend mit `npm.cmd --prefix backend run check` und `npm.cmd --prefix backend run typecheck` geprüft werden. `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-B2A" --logger "trx;LogFileName=D6-FIX03-B2A.trx"` ist auszuführen. Noch keine lokale Kompilierung oder Testausführung für B2A; Unicode-Normalisierung, Restore-Gate, Upgrade/Restore, Schichtentrennung und Gesamtregression bleiben offen.

## D6-FIX03-B2A – lokaler Prüfnachweis

Am 28.11.2026 bestätigte das übermittelte Konsolenprotokoll `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-B2A" --logger "trx;LogFileName=D6-FIX03-B2A.trx"`: 3/3 neue HTTP-Tests bestanden, 0 übersprungen; Testdauer 6,0 s, Gesamtdauer 8,2 s. `npm.cmd --prefix backend run generate`, `npm.cmd --prefix backend run check` (Vertrag stimmt überein) und `npm.cmd --prefix backend run typecheck` wurden erfolgreich abgeschlossen. Numerische Exitcodes nicht separat ausgegeben. Erwarteter Gesamtbestand: 454 Backendtests; Gesamtregression noch offen.

## D6-FIX03-B2B – Unicode-Normalisierung v1

Frontend-Trim-Semantik bleibt erhalten: ECMAScript-Whitespace einschließlich BOM (U+FEFF), ohne NEL (U+0085). HTTP und die drei D6-SQL-Namensprüfungen verwenden dieselbe explizite Zeichenmenge. PostgreSQL berechnet den Tag-Schlüssel weiterhin atomar per deutscher ICU-Kleinschreibung; keine Akzent- oder Unicode-Kompositionsfaltung. Die SQL-Body-Hashes im Restore-Gate werden angepasst, Funktionsinventar und Rechte bleiben unverändert. Zwei neue Backendtests prüfen HTTP-Create/Rename/Conflict einschließlich tatsächlicher Schlüssel und die SQL-Grenze ohne HTTP; zwei Vitest-Tests spiegeln die Unicode-Gegenproben. Die D6-Migration ist noch nicht freigegeben; diese Korrektur setzt für Tests eine frisch migrierte isolierte Datenbank voraus. Ein bereits mit dem früheren D6-Stand migrierter Bestand benötigt einen kontrollierten Upgrade-/Restore-Weg; keine automatische Änderung eines bestehenden Datenbestands. ICU-/Unicode-Versionswechsel sind nicht pauschal als vollständig kompatibel nachgewiesen. Lokale Kompilierung und Testausführung stehen aus. Der lokal in B2A neu generierte OpenAPI-Vertrag wird nicht überschrieben; die Vertragsprüfung bleibt erforderlich. FIX03-C/D und vollständige Regression weiterhin offen.


## D6-FIX03-B2B – lokaler Prüfnachweis

Das übermittelte Konsolenprotokoll vom 28.11.2026 bestätigt `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-B2B" --logger "trx;LogFileName=D6-FIX03-B2B.trx"`: 2/2 Backendtests erfolgreich, 0 übersprungen; Testdauer 6,5 s, Gesamtdauer 9,2 s. `npm.cmd --prefix frontend run check`: Prettier, TypeScript, ESLint, Stylelint, 274/274 Vitest (36 Dateien) und Produktionsbuild erfolgreich. Numerische Exitcodes nicht separat ausgegeben. Erwarteter Backend-Gesamtbestand vor C: 456; vollständige Regression bleibt offen.

## D6-FIX03-C – relationales Restore-Gate und D6-Upgrade/Restore

Das lesende Restore-Gate erhält ein aus der versionierten D2-Migration abgeleitetes Inventar der fachlichen Primär-/Alternate Keys, Workspace-Fremdschlüssel einschließlich Referenzziel, Spaltenreihenfolge, NULL-Zustand und Löschregeln, sowie der für D6 wesentlichen Eindeutigkeitsindizes. Keine neuen DDL- oder Runtime-Rechte, keine Datenmigration und kein neues Backup-Verfahren. Neun negative Katalogvarianten prüfen die Ablehnung fehlender oder veränderter Schlüssel, Fremdschlüssel und Unique-Indizes; nach Rollback muss das unveränderte Gate im read-only-Modus bestehen. Ein zusätzlicher Test migriert eine mit Themenhierarchie, Tags, Dokumenten, Zuordnung und Versionen befüllte D4-Datenbank, stellt die prä-D6-Sicherung in einer separaten Testdatenbank wieder her, migriert den Ausgangsbestand auf D6, schreibt unter der Runtime-Rolle und verifiziert nach D6-Backup/Restore die vollständigen Metadaten und Versionswerte sowie erneute Schreibfähigkeit und Zurückweisung einer alten Version. Nur isolierte PostgreSQL-Testcontainer; kein produktiver Restore. Lokal noch nicht kompiliert oder ausgeführt: `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-C" --logger "trx;LogFileName=D6-FIX03-C.trx"`. FIX03-D, Gesamtregression, manuelle Abnahme, D3-INTEGRATION und Git-Abschluss bleiben offen.


## D6-FIX03-C – lokaler Prüfnachweis

Das übermittelte Konsolenprotokoll vom 28.11.2026 bestätigt `dotnet test backend/VIA.MiniDocs.sln --no-restore -m:1 --filter "Package=D6-FIX03-C" --logger "trx;LogFileName=D6-FIX03-C.trx"`: alle fünf Projekte kompiliert, 10/10 Testfälle bestanden, 0 übersprungen; Testdauer 5,6 s, Gesamtdauer 7,3 s. Erwarteter Gesamtbestand nach C: 466 Backendtests. Numerischer Exitcode nicht separat ausgegeben; Gesamtregression noch offen.

## D6-FIX03-D – Schichtentrennung und Abschluss

Die zusammengesetzte Dokumentänderung, Batch-Move, Batch-Tags und Einzel-Tagzuordnung werden von `WorkspaceDocumentWrites` in Application koordiniert. Der request-scoped Infrastructure-Adapter verwendet den bereits autorisierten DbContext, EF-Workspace-Filter, die vorhandene D6-Sperrgrenze und `LocalWriteTransaction`. API behält HTTP-Validierung, ETag-Dekodierung, Fehlerabbildung, DTOs und Routendefinitionen; die Application-Schicht besitzt keine HTTP- oder EF-Abhängigkeit. Die SQL-Kommandos, Lock-Reihenfolge und Migrationen bleiben in D unverändert. Einfache Topic-/Tag-CRUD-Pfade behalten ihre bestehende Struktur; keine neue generische Repository- oder Mediator-Architektur. Der Build-Ausschluss umfasst zusätzlich beliebig verschachtelte bin-/obj-Verzeichnisse. Eine Änderung der OpenAPI-Schemas oder des visuellen Frontends ist nicht beabsichtigt.

D6 wird aufgrund der noch ausstehenden VIA.Docs-Deployment-Bestätigung lokal mit Dateitestdoubles weiterentwickelt. Das ist keine Abnahme von D3-INTEGRATION oder Freigabe produktiver Zugriffe. Die formale Freigabe und Reihenfolge für die weitere Fachimplementierung einschließlich D5 sowie die manuelle Abnahme, unabhängige Abschlussprüfung und der Git-Abschluss bleiben ausdrücklich offen.

Die Kompilierung und erneute Ausführung der FIX03-Tests, der vollständigen Debug-/Release-Regressionssuite, des Vertragschecks, des Frontend-Checks und der Browserregression stehen nach D noch aus. Ein neuer HTTP-Test sichert die Reihenfolge der Versionsfehler und Rollback bei ungültigem späterem Batch-Element. Erwarteter Gesamtbestand: 467 Backendtests. Die bislang erfolgreichen 27 gezielten Backendtests ersetzen diese abschließenden Prüfungen nicht. Die finale Dokumentation hält die tatsächlichen Befehle, Exitcodes und Testergebnisse nach ihrer Ausführung fest.
