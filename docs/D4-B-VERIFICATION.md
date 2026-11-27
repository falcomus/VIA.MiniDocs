# D4-B · Geschlossene Registrierung und Workspace-Isolation

## Ausgangsstand

`HEAD`, `main`, `origin/main` und der mit `git ls-remote --exit-code origin refs/heads/main` abgefragte Remote-Stand waren vor der Arbeit `131cb844630c6b53acb5729fe29ddd7181685597`. Die D4-A-Referenz umfasst 359 Backendtests und 225 Frontendtests.

## Implementierung und Sicherheitsgrenzen

- Die Einladung wird mit 32 Zufallsbytes erzeugt. PostgreSQL speichert nur den SHA-256-Hash der dekodierten Bytes. Der kanonische Base64url-Token wird einmal im bestätigten 201-Ergebnis ausgegeben. Die Detail- und Listenprojektionen wählen nur ID, Fristen und Zustandszeitpunkte. Bei verlorenem Erzeugungs-ACK kann der Administrator den Status nachsehen und die Einladung bei Bedarf widerrufen.
- Einladungsanlage und Widerruf verwenden den D4-A-Admin-Guard und die D2-Kommandos in derselben `LocalWriteTransaction`. Der Guard hält die Admin-Lifecycle-Sperre, die Actor-Identity-Sperre und den Actor-Session-Lock vor der Einladungszeile. Eine abgelaufene Einladung wird nach der Zeilensperre anhand der Datenbankzeit erkannt. Die Standardfrist beträgt 24 Stunden, die maximale Frist 30 Tage.
- Registrierung erzeugt Benutzer-, Workspace- und Systemtopic-ID vorab. Einladungssperre, Identity-Anlage über `UserManager`, Workspace und Systemthema `UNASSIGNED` sowie Konsum nutzen denselben Scoped-DbContext und dieselbe lokale Transaktion. Doppelte Kennungen ergeben 409 und lassen die Einladung unbenutzt. Vier nicht verfügbare Einladungszustände ergeben denselben 410-Fehler. Die Anmeldung erfolgt separat. Die Registrierungsantwort enthält kein Session-Cookie.
- Ein unbestätigter Commit ergibt 503. Der Dienst beobachtet in einem frischen Scope ausschließlich die vorab erzeugte und zusammenhängende ID-Gruppe. Es gibt keinen automatischen zweiten Schreibversuch. Das Ergebnis kann durch eine separate Anmeldung geklärt werden.
- Die Statusroute verwendet den Workspace aus der pro Anfrage validierten Session und dem eindeutigen `OwnerUserId` eines regulären Kontos. Die drei getrennten Policies steuern Session, Administrator und aktiven Workspace. Der aktive Fachkontext wird einmal pro nicht gepooltem DbContext aus dem serverseitigen Snapshot gebunden. Null ist der Standard und liefert keine fachlichen Zeilen. Pending, Suspended sowie deaktivierte oder AdminOnly-Konten erhalten keine aktive Bindung. Eine Administratorrolle erweitert die fachliche Sicht nicht.
- Zehn benannte EF-Filter erfassen Themen, Dokumente, Tags, Zuordnungen, veröffentlichte und ausstehende Transkriptseiten, ausstehende Transkripte, Dateioperationen, Receipts und Tombstones. Historische D2-Integrationstests verwenden einen ausdrücklich gekennzeichneten Testbeobachter, der nur den Workspace-Filter ausnimmt. Anwendungs- und HTTP-Tests prüfen die tatsächlichen Filter ohne diese Ausnahme. Workspace-Status, Identity und administrative Einladungen haben eigene begrenzte Lesewege. Befehle und Constraints bleiben eigenständige Schreibgrenzen.
- Acht HTTP-Endpunkte verwenden die D4-A-Middleware für HTTPS, CSRF, sichere Fehler und `no-store`. Cursors verwenden Data Protection. Sie sind an Admin-ID, Liste und Seitenlimit gebunden. Standard 50, maximal 100 Zeilen. Die Sortierung verwendet die eindeutige UUID. Account-PATCH vergleicht genau einen starken ETag und liefert den neuen Wert im Header und Body. Die Registrierung nutzt separate IP-, Namens- und Prozessbudgets plus acht gleichzeitig zugelassene Transaktionen ohne Wartequeue.
- Die Umsetzung benötigt keine Migration und keine neue Datenbankberechtigung. Das bestehende Sollinventar, Upgrade und Restore werden durch die Bestandssuite geprüft.

## Nachweismatrix

| Prüffeld | Lokaler Nachweis |
| --- | --- |
| Einladungs- und HTTP-Vertrag | `AdminInvitationRegistersExactlyOnePendingWorkspaceWithoutIssuingSession` prüft CSRF, Hashspeicherung, Standardfrist, 409 ohne Konsum, erfolgreiche Registrierung, Einmalnutzung, AdminOnly, eigenen Workspace und Secret-Redaktion in Antwort, Logs und Activity-Tags. |
| Listen und Versionskontrolle | `AdminListsBindCursorsAndPatchUsesStrongIfMatch` prüft UUID-Seitenwechsel, falsches Limit, manipulierten und listenfremden Cursor, fehlendes oder unzulässiges If-Match, ETag-Rückgabe, 412 und die begrenzte Benutzerprojektion. |
| Registrierung und PostgreSQL-Zeilenkonkurrenz | `ARegistrationHoldsItsInvitationAndExposesNoPartialCommit` prüft gleichzeitige Einlösung und Registrierung vor Widerruf. `RevocationWinningTheRowLockPreventsRegistration` prüft die umgekehrte Reihenfolge. `ExpiryWhileWaitingIsEvaluatedOnDatabaseClock` beweist den Ablauf auf der Datenbankuhr nach beobachtetem Blocker. |
| Rollback und unbestätigter Commit | `FailureBeforeWorkspaceRollsBackAndLostCommitIsObservedWithoutRetry` prüft den Fehler nach Identity-Anlage sowie einen am Commit-Übergang simulierten verlorenen ACK. Der unabhängige Beobachter sieht entweder keine Registrierung oder exakt eine vollständige Registrierung. |
| Admin-Autorität | `InvitationWriteSerializesWithLossOfAdministratorAuthority` kombiniert Einladungserstellung und Widerruf mit Kontodeaktivierung, Rollenentzug, Stamp-Änderung und Logout in beiden Gewinnerreihenfolgen. Der dritte Beobachter prüft die konkrete Sperrkante mit `pg_blocking_pids`. Der Rollenentzug wird als späterer Lifecycle-Schreibpfad im Test nachgebildet. |
| Workspace-Isolation | `ParallelRuntimeContextsSeeOnlyTheirOwnActiveWorkspace` prüft alle zehn Entitätstypen in zwei realen aktiven Workspaces, einen regulären Administrator, fehlenden Kontext, AdminOnly, Pending, Suspended, Deaktivierung und parallel ausgeführte Abfragen aus separat gebundenen Kontexten. |
| Ressourcenbegrenzung | Der Unit-Test `RegistrationCapsSimultaneousTransactionsWithoutQueuing` prüft die acht aktiven Plätze und die sofortige Ablehnung bei ausgeschöpfter Kapazität. Der HTTP-Integrationstest für die tatsächliche 429-Antwort bleibt D4-D vorbehalten. |

## Freigabestand

Die unabhängige statische Prüfung der 32 Paketdateien ist mit **Note 1− (1,3)** abgeschlossen. Die technische Freigabe ist erteilt. Eine separate FIX-Runde ist nicht erforderlich.

Die Codex-Verifikation ergab je 385 bestandene Backendtests in Debug und Release sowie 225 bestandene Frontendtests. Alle 359 D4-A-Bestandstests sind erhalten. Der Reviewer hat keine unabhängigen Testläufe ausgeführt.

### Verbindliche Restpunkte

- **D4-D** korrigiert die Optionalität von `expiresAt` im generierten TypeScript-Vertrag und weist die tatsächliche HTTP-429-Antwort bei ausgeschöpfter Registrierungskapazität mit einem Integrationstest nach.
- **D6** weist die fachliche Mandantenisolation vollständig mit HTTP-Ende-zu-Ende-Tests nach.

D4-C benötigt weiterhin seine separate Detailfreigabe. D3-INTEGRATION, D8 und die produktiven Betriebsabnahmen bleiben offen. Die Limitwirkung mehrerer Hostprozesse ist Teil der Betriebsabnahme.

## Prüfprotokoll

Die Datenbanktests verwenden PostgreSQL 18.7 in isolierten, durch die bestehende Fixture verwalteten Loopback-Containern. Für HTTPS und externe Kommunikationsgrenzen gelten die lokalen Testvorrichtungen der Bestandssuite.

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false -p:NuGetAudit=true` | 0 | Locked Restore mit aktiviertem Audit |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Debug -m:1 -nr:false` und Release | je 0 | Jeweils 0 Warnungen und Fehler |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -c Debug -m:1 -nr:false --filter Package=D4-B` | 0 | 26 bestanden, 0 übersprungen |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -c Debug -m:1 -nr:false` und Release | je 0 | Je 385 bestanden, 0 übersprungen. Namensvergleich der TRX-Ergebnisse bestätigt die 359 D4-A-Bestandstests. |
| `npm --prefix backend run generate`, `check`, `typecheck`, `test:drift` | je 0 | OpenAPI-Generierung, Vertragsabgleich und negativer Drift-Test |
| `npm --prefix frontend run check` | 0 | 225 Tests, Format-, Typ-, Lint- und CSS-Prüfungen sowie Build. Vite meldet weiterhin große Chunks. |
| `git diff --check`, `git diff --cached --exit-code` und Umfangsprüfung | je 0 | Kein Whitespace-Fehler, leerer Index |
| Gezielte Secret- und Whitespace-Mustersuche mit `rg` | je 1 | Keine Treffer. Keine Schlüsseldateien oder Build-/Testartefakte im Änderungsumfang. |

## Änderungsumfang

- API und Vertrag: `Program.cs`, `SecurityRegistration.cs`, `SessionHttp.cs`, `AdministrationHttp.cs`, `RegistrationHttp.cs`, `BoundedJson.cs`, `WorkspaceAuthorization.cs` und `backend/contracts/fixture.generated.ts`.
- Persistenz: `MiniDocsDbContext.cs`, `SessionStore.cs`, `AdministratorAccounts.cs`, `AdministratorInvitations.cs`, `Registration.cs` und `LocalWriteTransaction.cs`.
- Tests: neue `RegistrationHostTests.cs`, `WorkspaceAuthorizationTests.cs` und `PostgresObservations.cs`. Die Teilklasse ergänzt `SessionHostTests.cs`. `LoginRateLimitTests.cs` prüft die Kapazitätsgrenze. Zehn D2-Testsuiten verwenden den benannten Testbeobachter für vorhandene Datenbankassertions.
- Dokumentation: `D4-B-VERIFICATION.md`, `MASTERPLAN.md` und `TICKETS.md`.
