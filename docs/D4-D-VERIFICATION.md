# D4-D · API-Vertrag und React-Anmeldung

Der aktuelle Abnahmestand einschließlich Dateifreigabeliste steht im [D4-FINAL-Nachweis](D4-FINAL-VERIFICATION.md). D4-D-FIX3 und D4-D-INVITE-LINK wurden laut Projektverantwortlichem statisch nachgeprüft und freigegeben. Der abschließende Einladungsfall entfernt das Fragment auch bei bestehender Anmeldung und erklärt, dass der Einladungslink nach der Abmeldung erneut geöffnet werden muss. Die folgenden Abschnitte dokumentieren die jeweiligen früheren Prüfstände.

## D4-D-INVITE-LINK · Verifikation

Der HTTPS-Browsertest kopiert eine echte MiniDocs-Einladung aus der Administration. Er prüft die aktuelle Basisadresse, das Entfernen des Fragments, die Registrierung ohne manuelle Schlüsseleingabe, die anschließende Anmeldung sowie die Ablehnung bei Wiederverwendung. Ein ungültiges Fragment führt zur manuellen Eingabemöglichkeit. Für abgelaufene und verbrauchte Einladungen gilt dieselbe bestehende serverseitige 410-Antwort. Der Schlüssel wird nur im React-Zustand gehalten und über den bestehenden Registrierungsendpunkt eingelöst.

| Prüfung | Ergebnis | Exitcode |
| --- | --- | --- |
| Backendregression mit isoliertem PostgreSQL | 436 bestanden, keine Skips | 0 |
| Vollständiger Frontendcheck | 266 Vitest-Tests und Produktionsbuild bestanden | 0 |
| Reguläres Playwright-Gate | 7 bestanden, einschließlich der sechs Bestandstests und der unveränderten visuellen V2-Referenzen | 0 |

Der Projektverantwortliche hat den 45-Dateien-Diff statisch freigegeben und die erfolgreiche manuelle Anmeldung mit Benutzername und Passwort bestätigt. Die manuelle Tastatur-, Fokus- und Accessibility-Abnahme bleibt offen.

## D4-D-HANDOVER · laufende Verifikation

Der Projektverantwortliche hat die aktuelle Login-Gestaltung einschließlich Hintergrund, Logo, Card, Elevation und Abständen verbindlich freigegeben. D4-D-INVITE-LINK ist als separates Ticket vor D4-FINAL geplant und wurde in diesem Auftrag nicht implementiert.

Der Arbeitsbranch `fix/d4-d-fix2` steht auf der Ausgangsrevision `8a60b0b5fb88c55a1ac6898ee8a0c3419b262462`. Die vollständige Backendregression ergab in Debug und Release jeweils 436 bestandene Tests und keine Skips. Der Frontend-Gesamtcheck ergab 261 bestandene Tests. Locked Restore mit NuGet-Audit, Builds und Frontendcheck endeten jeweils mit Exitcode 0.

Der erste OpenAPI-Abgleich und Drift-Test schlugen fehl, weil der laufende lokale API-Prozess die Debug-Ausgabedateien sperrt. Mit separatem `BaseOutputPath=bin/handover/` bestanden OpenAPI-Abgleich und negativer Drift-Test mit Exitcode 0. Die TypeScript-Vertragsprüfung bestand mit Exitcode 0.

Die vollständige HTTPS-Browserabnahme mit VIA.Docs-Testdouble und die Ursachenklärung des früheren Registrierungsfehlers sind noch offen. Der frühere Browserlauf hat weder HTTP-Status noch fachlichen Fehlercode aufgezeichnet. Der erfolgreiche API-Test beweist die Ursache des damaligen Fehlers nicht. Die unabhängige Prüfung sämtlicher Änderungen und die Abschlussfreigabe stehen aus.

## D4-D-FIX3 · Korrekturen und Verifikation

Die Prüfung erfolgte auf `fix/d4-d-fix2` mit HEAD `8a60b0b5fb88c55a1ac6898ee8a0c3419b262462` und dem vorhandenen D4-D-Arbeitsstand. Das freigegebene Login-Design bleibt erhalten.

| Reviewbefund | Korrektur und Regressionsnachweis |
| --- | --- |
| 1. Session und Anmeldung | Eine laufende Sessionprüfung verhindert paralleles Absenden des Formulars. Tab- und Sichtbarkeitsereignisse während einer Anmeldung werden für die aktuelle Generation zu einer nachgelagerten Prüfung zusammengefasst. Ein Identitätswechsel startet die Administration mit einem neuen Scope und verwirft alte Anfragen, Listen und vertrauliche Anzeigen. Zwei Tests halten Session-, Login- und Admin-Antworten gezielt an und prüfen die Reihenfolge und den Wechsel zwischen zwei Administratorkonten. |
| 2. Provisionierungs-POST | Die 202-Antwort übernimmt die beim Absendestart vergebene Revision. Ein anschließender Status-GET erhält beim Start seine eigene Revision. Ein Test liefert nach einer neueren Listenbeobachtung einen älteren 202-Status und prüft, dass der bereits sichtbare aktive Zustand bestehen bleibt. |
| 3. Admin-Listen | Benutzer- und Einladungslisten prüfen jeweils ihre eigene letzte Anfragerevision. Überholte Daten und nicht sicherheitsrelevante Fehler werden verworfen. Autoritätsfehler der laufenden Admin-Sitzung behalten ihre bisherige Wirkung. Zwei Tests liefern gleichzeitig offene Cursor-Antworten in umgekehrter Reihenfolge. |
| 4. OpenAPI-Fehler 500 | Die drei D4-Routengruppen deklarieren HTTP 500 als `application/problem+json`. Der Host-Schematest prüft die exakten Fehlerstatus aller 15 Operationen, den gemeinsamen ProblemDetails-Verweis und den erforderlichen Cache-Control-Header. Der generierte TypeScript-Vertrag und seine Typprüfung enthalten die neue Antwort. |

| Prüfung | Exitcode | Ergebnis |
| --- | --- | --- |
| Locked Restore mit `NuGetAudit=true` und `NuGetAuditMode=all` | 0 | Keine Auditwarnung. |
| Backend-Build Debug und Release | je 0 | Jeweils null Fehler und Warnungen. |
| Backendregression Debug und Release gegen isoliertes PostgreSQL 18.7 | je 0 | Jeweils 436 bestanden, keine Skips. Beide TRX-Listen sind mit dem D4-D-HANDOVER-Stand identisch. |
| `npm --prefix backend run generate`, `check`, `typecheck`, `test:drift` | je 0 | Generierung reproduzierbar, Vertrag typgeprüft. Der negative Drift-Test weist einen veralteten Vertrag zurück. |
| `npm --prefix frontend run check` | 0 | Formatierung, TypeScript, ESLint, Stylelint, 266 Vitest-Tests und Produktionsbuild bestanden. Fünf gezielte Tests kamen zu den 261 Bestandstests hinzu. Keine Skips. |
| `git diff --check`, Index- und HEAD-Prüfung, ergänzende Whitespace- und Secret-Mustersuche | je 0 | Keine Whitespace-Fehler oder Musterfunde. Index leer und HEAD unverändert. Die ergänzende Prüfung erfasst auch die unversionierten Paketdateien. |

Vertragsgenerierung und Builds verwendeten separate Ausgabeverzeichnisse, da der laufende lokale API-Host die Standardausgabe belegt. Der erste Debug-Lauf meldete Exitcode 1, weil die zunächst verwendete OpenAPI-Transformation das Schema inline ausgab. Die Deklaration über die Routengruppen verwendet den gemeinsamen ProblemDetails-Verweis. Der finale Debug- und Release-Lauf bestätigt die Korrektur.

Die manuelle HTTPS-Browserabnahme, der unabhängige Nachreview und der formelle Git-Abschluss bleiben offen. Die Ursache des historischen Registrierungsfehlers bleibt unbekannt. D4-D-INVITE-LINK ist ein separates Folgeticket mit der ausdrücklich freigegebenen Fragment-Ausnahme zur URL-Regel.

## D4-D-FIX · Befundmatrix

| Befund | Korrektur und lokaler Nachweis |
| --- | --- |
| P1-01 | Eine lokale Logout-Sperre verhindert Session-Neuaufbau durch Tab- oder Sichtbarkeitsereignisse bis zur bewussten erneuten Anmeldung. Der Test hält die Logout-Antwort an und löst beide Ereignisse vor und nach unbestätigtem Logout aus. |
| P1-02 | `403 forbidden` beendet den Admin-Scope, bricht Anfragen ab und erzwingt eine erneute Sessionprüfung. `antiforgery_invalid` bleibt ein CSRF-Fehler. Tests prüfen Rollenentzug bei laufender Abfrage und laufender Aktion mit verspäteter Antwort. |
| P2-01 | Ungültige CSRF-Tokens werden verworfen. Eine neue bewusste Aktion fordert einen neuen Token an, ohne automatische Schreibwiederholung. Tests prüfen Token-Konkurrenz, Identitätswechsel und Abbruch beim asynchronen Fehler-Body. |
| P2-02 | Benutzerlisten lassen sich nach 412 gezielt aktualisieren. Statusabfragen nutzen separate Timer pro Workspace, validieren Location und Retry-After und führen Ergebnisse ohne Überschreiben anderer Workspaces zusammen. Der Admin-Scope bricht beim Verlassen ab. Die technische Einladung verlangt eine explizite Bestätigung und kann verworfen werden. |
| P2-03 | Der Vertragstest prüft alle 15 Operationen mit Fehlerstatus, ProblemDetails-Content-Type, Cache-Control und CSRF-Anforderung sowie Location, Retry-After, ETag, If-Match und die optionale Einladungsfrist. Das Schema dokumentiert die beobachteten Sicherheitsheader. |

Die gesonderte reale HTTPS-Browserabnahme und die unabhängige Nachprüfung bleiben offen.

## D4-D-FIX2 · Restbefunde

- Die lokale Sperre nach unbestätigtem Logout bleibt auch nach Öffnen des Login-Formulars und fehlgeschlagener Anmeldung bestehen; erst eine erfolgreiche aktuelle Anmeldung hebt sie auf.
- Provisionierungsbeobachtungen werden nach Anfragereihenfolge und Workspace zusammengeführt. Überholte Statusantworten dürfen weder neuere Statuswerte noch eine neuere Liste überschreiben; andere Workspaces behalten ihre eigenen Timer.
- Recovery erhält ein eigenes begrenztes Fixed-Window-Budget nach dem bestehenden Admin-Limiter (5 je Actor pro Minute, 240 global pro Host, begrenzte Partitionen). Nach Authentifizierung, CSRF und Eingabeprüfung erfolgt die Admission vor dem fachlichen Kommando. 429 enthält `rate_limited`, `traceId`, `Retry-After` und `no-store`. Das Budget ist pro Prozess, nicht clusterweit. Einladungslimits bleiben getrennt.
- Der Vertragstest prüft zusätzlich sämtliche deklarierten 413/415- und sonstigen D4-Fehlerantworten; der Recovery-Hosttest prüft 429, unveränderte Version und erneute Admission nach einem deterministischen Fensterwechsel.

FIX2-Quelländerungen und Regressionstests sind vorbereitet. Lokale Builds, PostgreSQL-Tests, Frontendcheck und tatsächliche Neugenerierung des TypeScript-Vertrags sind noch nachzuweisen. Die früheren Testergebnisse gelten nicht als FIX2-Nachweis.

### FIX-Prüfprotokoll

| Prüfung | Exitcode | Ergebnis |
| --- | --- | --- |
| Locked Restore mit `NuGetAudit=true` und `NuGetAuditMode=all` | 0 | Keine Auditwarnung. |
| Debug-/Release-Build und vollständige PostgreSQL-Regression | je 0 | Je 436 bestanden, keine Skips. TRX-Vergleich bestätigt alle 436 Backend-Bestandstests. |
| `npm --prefix backend run check`, `typecheck`, `test:drift` | je 0 | Vertrag reproduzierbar, negativer Drift-Test bestanden. |
| `npm --prefix frontend run check` | 0 | 257 Tests bestanden, keine Skips. Bestehende Logout-503-Assertion wurde an die verlangte sofortige Entfernung privater Daten angepasst. Die übrigen Bestandstests bleiben erhalten. |
| `git diff --check`, Indexprüfung und Backend-TRX-Abgleich | je 0 | Whitespace-Prüfung erfolgreich, Index leer, Bestandstests erhalten. |
| Secret- und Whitespace-Mustersuche | je 1 | Keine Treffer. |

## Stand

Ausgangsrevision `8a60b0b5fb88c55a1ac6898ee8a0c3419b262462` auf `main`. Die lokale Umsetzung und automatisierte Verifikation sind durchgeführt. Die HTTPS-Browserprüfung, die unabhängige Prüfung und der formelle Git-Abschluss stehen aus. D4-FINAL und BACKEND-QUALITY-GATE-01 benötigen eigene Aufträge.

## Umsetzung

- Das OpenAPI-Schema führt `code` und `traceId` als erforderliche Strings. `expiresAt` ist bei der Einladungserstellung optional. Die 15 D4-Operationen und ihre Erfolgsantworten werden im Schematest geprüft. Der HTTP-Test hält die acht Registrierungsslots und weist die tatsächliche 429-Antwort nach.
- Der reguläre Einstieg rendert ein Session-Gate. Der Demo-Einstieg benötigt den expliziten Vite-Modus `demo`; die Demo- und Viewer-Module werden erst in diesem Modus geladen. Die Vite-Middleware für Demo-PDFs wird nur im Demo-Modus aktiviert.
- Der Same-Origin-Client hält den Antiforgery-Token ausschließlich im Speicher, erneuert ihn nach Identitätswechsel und sendet mutierende Aufrufe einmal. Bei Tabwechsel, Sitzungswechsel oder Abmeldung werden offene Requests abgebrochen. Ein nach Abbruch verspätetes Ergebnis wird verworfen. 503 beim Logout bleibt als unbestätigtes Ergebnis sichtbar.
- Anmeldung, einladungsgebundene Registrierung, Workspace-Status und Administration verwenden den generierten Vertrag. Administratoren verwalten Einladungen und Konten mit ETag sowie technische Einladungen und Recovery. Die 202-Antwort wird als angenommene Verarbeitung angezeigt; `Location` und `Retry-After` steuern eine Statusabfrage.
- `README.md` beschreibt den lokalen HTTPS-Proxy und die getrennte Demo-Aktivierung. Zertifikat und Schlüssel werden lokal bereitgestellt. Der Proxy validiert das Backend-Zertifikat.

## Nachweise und offene Abnahmen

| Bereich | Nachweis |
| --- | --- |
| API | Generierte TypeScript-Typen, OpenAPI-Schemaprüfung und echter HTTP-429-Test mit isoliertem PostgreSQL. |
| Browserzustand | Vitest prüft Anmeldung, Registrierung, Logout mit 503, CSRF, verspätete Antworten, Tab-Benachrichtigung und Workspace-Zustände. |
| Administration | Vitest prüft ETag-Konflikt, einmalige Tokenanzeige und Recovery-Bestätigung. |
| Reale Browserprüfung | Die beauftragte lokale HTTPS-Browserprüfung mit isoliertem PostgreSQL und VIA.Docs-Testdouble ist noch nicht durchgeführt. Tastatur-, Fokus- und visuelle Abnahme im Browser stehen aus. |

## Prüfprotokoll

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false -p:NuGetAudit=true -p:NuGetAuditMode=all` | 0 | Locked Restore mit aktivem NuGet-Audit. |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Debug --disable-build-servers -m:1` und Release | je 0 | Keine Warnungen und Fehler. |
| `dotnet test backend/VIA.MiniDocs.sln --no-build -c Debug` und Release | je 0 | Je 436 Tests bestanden, keine Skips. TRX-Abgleich bestätigt alle 435 Bestandstests. PostgreSQL 18.7 läuft isoliert. |
| `npm --prefix backend run generate`, `check`, `typecheck`, `test:drift` | je 0 | Reproduzierbarer OpenAPI-Vertrag und negativer Drift-Test. Das TypeScript-Contract-Fixture wurde für die nun erforderlichen ProblemDetails-Felder ergänzt. |
| `npm --prefix frontend run check` | 0 | Format, TypeScript, ESLint, Stylelint, 247 Tests und Produktionsbuild. Der bestehende Tag-Test lief bei hoher Parallelität wiederholt in das Zeitlimit. Die Test-Workerzahl wurde auf vier begrenzt und die Suite erneut ausgeführt. |
| `git diff --check`, Indexprüfung und TRX-Namensvergleich | je 0 | Keine Whitespace-Fehler, leerer Index, alle 435 Backend-Bestandstests vorhanden. |
| Secret-Mustersuche | 1 | Keine Treffer. |

D3-INTEGRATION, D8 und produktive Betriebsabnahmen bleiben offen. Ein produktiver Zugriff auf VIA.Docs wurde nicht durchgeführt.

## D4-D-BROWSER-FIX · Sichtbefunde und lokale Diagnose

Beim manuellen Browserlauf über lokales HTTPS waren die Administratoranmeldung und Einladungserstellung erfolgreich. Ein Registrierungsversuch zeigte einen unspezifischen Fehler; HTTP-Status und fachlicher Code wurden dabei nicht aufgezeichnet. Die ursprüngliche Ursache ist noch offen.

Die Anmeldung und Administration werden an die bestehende Demo-Designsprache angepasst: gemeinsame, von Fach- und Sessiondaten unabhängige Kopfleiste, kompakte Navigation und Listen, responsive Umbrüche, sichtbare primäre Aktionen sowie korrekt ausgerichtete Checkbox. Das einmalige Token ist vollständig als auswählbare Ausgabe sichtbar und kann bewusst kopiert werden; beim Schließen und Scope-Wechsel verschwindet es aus der Oberfläche. Das Betriebssystem verwaltet die vom Benutzer bewusst befüllte Zwischenablage. Registrierungsfelder zeigen die geltenden Längenvorgaben und einen verständlichen Hinweis bei HTTP 400; der Serververtrag und die Passwortregeln werden nicht verändert. Ein bewusst eingefügtes Token wird von äußeren Leerzeichen bereinigt.

Offen bleiben die Auswertung einer automatisierten HTTPS-API-Diagnose der Registrierung, die vollständige Frontendregression für diese Änderungen sowie die Sicht-, Tastatur- und Browserabnahme. Die Diagnose darf nur in der isolierten persistenten lokalen Testumgebung erfolgen. Keine produktiven VIA.Docs-Zugriffe und keine Git-Freigabe.
