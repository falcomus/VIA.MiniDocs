# D4-A · Identity, Sessions und Sicherheitsfundament

## Umfang und Ausgangsstand

D4-01 und D4-02 bauen auf `d20e6f7ae30950cb2bb7e79616778137d862746e` auf. Vor der D4-A-Implementierung zeigten `HEAD`, `main`, `origin/main` und `git ls-remote --exit-code origin refs/heads/main` denselben Commit. Der unabhängige statische Review bewertete den Ausgangsstand mit 2+ (1,7). D4-A-FIX hat alle vier Befunde behoben.

Die unabhängige statische Nachprüfung der 33 Paketdateien hat alle vier FIX-Befunde geschlossen und D4-A mit **Note 1− (1,3)** technisch freigegeben.

Die API stellt `GET /api/v1/antiforgery`, `POST /api/v1/session/login`, `GET /api/v1/session` und `DELETE /api/v1/session` bereit. Identity verwendet die bestehenden EF-Standardstores. Login nutzt `UserManager` und `SignInManager`, neutrale Ablehnungen, Dummy-Verifikation, eine Passwortanlage-Policy mit kurzer Sperrliste und zeitliches Identity-Lockout. Normalisierte Namen und Client-IP laufen in einem global und lokal begrenzten Fixed Window je Prozess. Geschützte Anfragen laden Session, Stempel, Kontostatus und Rollen aktuell aus PostgreSQL. API-Antworten unterscheiden bestätigten Logout, invalides Cookie, Antiforgery-Ablehnung und unbekannten DB-Ausgang.

Erhaltene Schichten sind API, Application, Infrastructure und Domain. Die API bindet Wire-DTOs, Cookies, Antiforgery, Fehler und lokale Betriebsmodi. Infrastructure kapselt die Identity-, Session- und Administrationspersistenz. Die beiden neuen operativen Relationen werden über die SQL-Migration verwaltet. Der bestehende EF-Identity-/Fachmodell-Snapshot verändert sich dadurch nicht.

## Migration und Rechte

| Baustein | Abgrenzung |
| --- | --- |
| `20261127090000_SessionSecurity` | Additive Tabellen `identity_sessions` und `administrator_bootstrap`, Hash-PK, Identity-FKs, Zeit- und Singleton-Checks, Ablaufindex, sieben begrenzte SQL-Funktionen. Der Session-Datensatz bindet den beim Login bestätigten Stamp. |
| `minidocs_runtime` | Lesezugriff auf Sessiondaten und sechs einzelne EXECUTE-Signaturen. Keine Session-DML, keine Bootstrap-Ausführung, kein direktes `is_disabled`-Update und keine Rollenmitgliedschafts-DML. |
| `minidocs_command_owner` | NOLOGIN. Eng zugewiesene spaltenbezogene Identity-Rechte und die über die SQL-Kommandos gebrauchten Session- und Bootstrap-Rechte. Nach der Migration besitzt er kein CREATE auf `minidocs`. |
| `minidocs_bootstrap` | Lokaler separater LOGIN NOINHERIT mit CONNECT, Schema-USAGE und genau einer EXECUTE-Freigabe. Der Aufruf prüft zusätzlich `session_user`. Der Principal hat kein Runtime- oder Tabellenrecht. |
| Kataloggate | Liest Rollen, ACLs, Memberships, Funktionen samt Body-Hashes und `search_path`, D4-Relationen, Constraints und Ablaufindex. Prüft D2.06 vor und D4 nach der additiven Migration. Archiv-Restore ergänzt ausschließlich die fehlenden datenbankweiten CONNECT-ACLs. |

Die Datenbank muss vor dem Upgrade mit dem isolierten Bootstrap-Principal nach `PrepareDatabaseRoles.sql` vorbereitet werden. DDL bleibt dem Migrationsbetrieb vorbehalten. Die Bootstrap-Operation validiert und hasht das Passwort mit dem Identity-Stack. Das eng begrenzte SQL-Kommando schreibt Konto, normalisierte Rolle, Mitgliedschaft und Einmalmarker atomar. Ein bereits vorhandener gleichnamiger Benutzer, ein AdminOnly-Bestand oder eine vorhandene Administratorrolle löst eine kontrollierte Ablehnung aus. Ein gewöhnlicher D2-Bestand bleibt erhalten.

Der Deaktivierungsdienst validiert den serverberechneten starken ETag über die kanonische Zielrepräsentation. Das SQL-Kommando serialisiert über Admin-Lifecycle → Ziel-Workspace → Identity-Zeilen in UUID-Reihenfolge → Actor-Session. Es prüft nach einem Lock-Wait erneut Actor, Rolle, Stamp und Sitzung. Das Ziel wird von Anfang an im benötigten stärkeren Modus gesperrt. Der atomare Statuswechsel ändert Security- und ConcurrencyStamp. Wiederfreigabe stellt alte Autorität nicht wieder her. Der Workspace-Status bleibt vom Kontostatus unabhängig. Ein zuerst wirksam autorisierter lokaler Write darf committen. Bereits versandte Fremdoperationen liegen außerhalb dieser Garantie.

## Betriebskonfiguration

- `ConnectionStrings__MiniDocs` verbindet den regulären Host ausschließlich mit dem Runtime-Principal. Lokale Wartungsaufrufe starten denselben Build ohne HTTP-Listener.
- `--bootstrap-administrator` erhält für diesen Aufruf die Verbindung des Bootstrap-Principals über den geschützten lokalen Credential-Kanal. Ein geschützter stdin-Kanal liefert zuerst Username und dann Passwort. Das Ergebnis ist 0 bei bestätigtem Commit, 1 bei kontrollierter Ablehnung und 2 bei unklarem Ergebnis. Bei unklarem Ergebnis den Einmalmarker im freigegebenen Betriebsverfahren klären.
- `--cleanup-sessions` verarbeitet mit der Runtime-Verbindung bis zu 500 abgelaufene Zeilen. Ein geplanter lokaler Betriebsvorgang darf ihn regelmäßig ausführen. Parallelität verwendet `SKIP LOCKED`.
- `Security__KeyRing__Directory` ist ein bereitgestellter absoluter Verzeichnispfad auf einem vertrauenswürdigen Dateisystem außerhalb des Releases. `ApplicationName` bleibt je Umgebung stabil. Die Prüfung beginnt an der Pfadwurzel und lehnt Symlinks, Windows-Reparse-Points und für andere Identitäten austauschbare Pfadkomponenten ab.
- Unter Linux prüft der Host mit `statx` Eigentümer-UID, Dateityp, Linkzahl und Modus. Ringverzeichnis und XML-Dateien gehören der effektiven Dienst-UID und gewähren weder Gruppe noch anderen Benutzern Zugriff. Vorfahren gehören der Dienst-UID oder Root und sind gegen Austausch geschützt. Ein Root-eigenes Sticky-Verzeichnis wie `/tmp` darf Vorfahr sein, wenn auch jede folgende Komponente die Prüfung besteht. Das temporäre Verzeichnis der ASP.NET-Dateiablage fällt ebenfalls unter diese Prüfung. [Linux statx](https://www.man7.org/linux/man-pages/man2/statx.2.html) · [ASP.NET-Dateiablage](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/DataProtection/DataProtection/src/Repositories/FileSystemXmlRepository.cs)
- Unter Windows prüft der Host einen lokalen Pfad auf einem festen Laufwerk, den Eigentümer-SID und die vollständige DACL. Dienst-SID, SYSTEM, lokale Administratoren und TrustedInstaller gelten als vertrauenswürdig. Beim Ring und seinen Dateien weist er Freigaben an weitere Identitäten ab. Bei Vorfahren weist er unter anderem Rechte zum Löschen von Kindern, Ändern der DACL oder Austauschen der Komponente ab. [Windows-Dateizugriffsrechte](https://learn.microsoft.com/en-us/windows/win32/fileio/file-access-rights-constants)
- `Security__KeyRing__CertificateFile` und `CertificatePassword` kommen aus geschützter Konfiguration. Die private PKCS#12-Datei mit RSA-Schlüssel und ihre Pfadkette werden ebenfalls geprüft. Frühere Zertifikate stehen mit `PreviousCertificates__<Index>__File` und `Password` zur Entschlüsselung und zum Restore bereit.
- `AllowInitialCreation` wird ausschließlich zur geplanten Erstinitialisierung im bereitgestellten leeren Ring gesetzt und anschließend entfernt. Der Host prüft Verzeichnis, vorhandene geschützte XML-Schlüssel, Zertifikate und kryptografischen Roundtrip vor Webbetrieb. Einen leeren oder beschädigten Bestandsring aus dem geschützten Backup wiederherstellen. Frühere private Entschlüsselungszertifikate bleiben konfiguriert, solange nicht widerrufene Schlüssel im Ring darauf angewiesen sind. Der betriebliche Stilllegungsprozess berücksichtigt die maximale Ticketlaufzeit und bewahrt die für Backups notwendigen Zertifikate auf. Der systemübergreifende Restore bleibt ein D8-Gate.
- `Security__Session__IdleMinutes` und `AbsoluteMinutes` können gegenüber 30 bzw. 480 Minuten abgesenkt werden. `Security__KnownProxies__<Index>` enthält ausschließlich explizite Proxy-IP-Adressen. Ohne Eintrag ist die Verarbeitung von Forwarded Headers ausgeschaltet. Development verwendet einen eigenen persistenten geschützten Ring. Tests erzeugen isolierte Ringe und Zertifikate.

## Nachweismatrix

| Prüffeld | Implementierung und Testfall |
| --- | --- |
| R-03 · Workspace-first und isolierte Identity-Zeile | `WorkspaceFirstSerializesD2WriteAndAdministrativeDeactivation` prüft beide Gewinnerreihenfolgen mit echtem D2-Prepare und echter D4-Mutation. `IdentityRoutineWriteBlocksActualD2OrDeactivation` prüft Routine-Update vor D2-`FOR SHARE OF u` und vor D4-Zieländerung, jeweils READ COMMITTED und REPEATABLE READ. Dritter Backend-Observer prüft den konkreten PID in `pg_blocking_pids`. REPEATABLE READ muss SQLSTATE 40001 liefern. |
| SEC-09 · Konto-, Rollen- und Sessiongrenze | `LoginCannotPublishValidSessionAcrossAdministrativeDeactivation` pausiert vor Session-Anlage nach Passwortprüfung und nach Commit vor Cookie-Ausgabe. `IdentityRehashOrLockoutCannotOverwriteAConcurrentDeactivation` beobachtet die tatsächlich blockierten Identity-Store-Updates für Rehash und Lockout. Weitere Fälle belegen unveränderten Workspace, keine Wiederbelebung nach Wiederfreigabe, aktuelle Rollen, parallelen Logout und konkurrierenden Letztadmin-Schutz. |
| Session-Lebensdauer und Commit | `RenewalCannotResurrectRevocationAndCleanupIsBounded`, `RenewalIsCappedByAbsoluteDeadline` und `UnconfirmedCommitDoesNotPublishACookie`. Konkurrenznachweis über PostgreSQL-PIDs und gezielte Verlustsimulation am EF-Commit-Übergang. |
| Cookie, Fixation, Antiforgery und HTTP | `HttpsLoginCsrfAndLogoutAreFailClosedAndCannotBeReplayed` verwendet Kestrel auf Loopback mit TLS und isoliertem Zertifikat. Geprüft werden Cookie-Attribute, Re-Login, alte Tokens, Logout, Replay und 401/403. `LogoutDifferentiatesUnconfirmedFailureFromParallelConfirmedRevocation` ergänzt 503 und bestätigten 204-No-op. Fremde Forwarded Headers autorisieren keinen unsicheren Aufruf. |
| Identity-Lockout und Limits | `LockoutDoesNotRevealAccountForWrongPasswordsAndLimiterCountsNormalizedName` prüft das fünfte Fehlereignis, 423 ausschließlich mit passendem Passwort und 429 über die normalisierte Kennung. `CapsIpNameAndGlobalWindowsAndClearsPartitions` prüft IP-, Namens- und Gesamtbudget samt Fensterwechsel mit kontrollierter Uhr. |
| SEC-11 · Redaktion | HTTP-Tests prüfen keine Passwortwiedergabe im Login, keine DB-Namen im 503-Body und kein Cookie bei unklarem Commit. Aufgezeichnete Log-Ausgaben und Activity-Tags der ASP.NET-Core- und Npgsql-Quellen werden auf Passwort, CSRF-Token und Cookie geprüft. Sessionfehler loggen Status und Trace-ID. EF-Command-, Query- und Update-Ausgaben sind am API-Sicherheitsrand gefiltert. |
| SEC-12/13 · Neustart und Rotation | `RingSurvivesRestartAndNewWriterButForeignRingRejectsOldCookies` prüft Cookie und Antiforgery nach Neustart mit zusätzlichem Schreibzertifikat sowie fremden Ring. Startup-Tests prüfen fehlende Konfiguration, fehlendes Zertifikat und einen ungeplant leeren Ring. |
| SEC-14 · PostgreSQL-Restore, Rollen und Bootstrap | `UpgradeChecksRuntimePrivilegesAndRestoresBootstrapMarker` prüft D2→D4, parallelen Bootstrap, Runtime-Ablehnung, Restore und erneute Ablehnung am erhaltenen Marker. `RestrictedRolesMigrateOperateAndRestoreWithoutGrantingDdlToRuntime` prüft saubere Vollmigration und die fortbestehende D2-Berechtigungsgrenze. `BootstrapUsesIdentityPasswordPolicyAndCreatesAnAdminOnlyLogin` geht vom Anwendungsdienst bis zum tatsächlichen Login. |

## Prüfungen und Restgates

Die lokale Datenbankprüfung verwendet ausschließlich die Fixture-eigenen Loopback-Container der verfügbaren Docker-Engine 29.9.2 mit PostgreSQL 18.7 und festem Digest. Für die Kestrel-Prüfung sind ausschließlich lokale Testzertifikate vorgesehen.

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false -p:NuGetAudit=true` | 0 | Lockdateien unverändert, Audit aktiviert |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Debug -m:1 -nr:false` | 0 | Keine Warnungen oder Fehler |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Release -m:1 -nr:false` | 0 | Keine Warnungen oder Fehler |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -c Debug -m:1 -nr:false --filter "Package=D4-A\|FullyQualifiedName~HostTests\|FullyQualifiedName~PostgresFinalHardeningTests.GateRejectsCatalogDrift"` | 0 | 85 bestanden, 0 übersprungen |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -c Debug -m:1 -nr:false` | 0 | 359 bestanden, 0 übersprungen |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -c Release -m:1 -nr:false` | 0 | 359 bestanden, 0 übersprungen |
| `docker run --rm --network none … dotnet vstest …`, gefiltert auf `KeyRingPathTests`, `HostTests.ProductionStartup` und `HostTests.ProductionDoesNotExposeFixtures` | je 0 | Linux-SDK-Image `mcr.microsoft.com/dotnet/sdk@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d`, UID 0 und UID 1654, je 11 bestanden und 0 übersprungen |
| `npm --prefix backend run check`, `typecheck`, `test:drift` | je 0 | Vertragsdatei reproduzierbar, absichtlich veralteter Vertrag erkannt |
| `npm --prefix frontend run check` | 0 | Format, TypeScript, Lint, CSS-Lint, 225 Vitest-Tests und Vite-Build |
| `git diff --check`, `git diff --cached --exit-code` | je 0 | Kein Whitespace-Fehler im Diff, Index leer |
| Whitespace- und gezielter Secret-Musterscan mit `rg -l` über die Paketdateien | je 1 | Keine Treffer. Keine Zertifikatsdateien, Logs, Archive oder Build-/Testartefakte im Änderungsumfang. |

Die Codex-Verifikation weist je 359 bestandene Backendtests in Debug und Release sowie 225 bestandene Frontendtests aus. Der Reviewer hat keine unabhängigen Testläufe ausgeführt.

Die produktiven Betriebsabnahmen für den Proxy, den Zertifikatswechsel und den organisationsweiten Restore stehen aus. Der Key-Ring setzt vertrauenswürdige Betriebssystemadministration und ein entsprechend provisioniertes Dateisystem voraus. Rate-Limits gelten je Hostprozess. Das Frontend meldet beim Vite-Build weiterhin große Chunks. D4-B und D4-C benötigen ihre gesonderten Freigaben, D3-INTEGRATION bleibt offen.

## D4-A-FIX · Befundmatrix

| Befund | Korrektur und Nachweis |
| --- | --- |
| P1-01 · Rate Limiting | `LoginRateLimits.TryAcquire` prüft unter derselben Sperre alle drei Budgets, bevor es einen Zähler oder eine Partition ändert. `ExhaustedPartitionCannotConsumeOtherBudgets` weist jeweils 1.025 abgelehnte Anfragen nach ausgeschöpftem IP- oder Namenslimit ab und nutzt danach das vollständige verbleibende Gesamtbudget. `ConcurrentAdmissionsStayWithinTheApplicableBudget` prüft konkurrierend 30 je IP, fünf je Normalname und 240 insgesamt. |
| P1-02 · Key-Ring-Sicherheit | `KeyRingPath` prüft Eigentümer, lokale Rechte, Dateitypen und die gesamte Pfadkette. Das Repository prüft XML-Dateien beim Laden und nach der Ablage. Die Tests prüfen Linux-UID, Windows-SID, fremde Zugriffsrechte, austauschbare Vorfahren und verlinkte Vorfahren. Unter Linux ändert ein isolierter Root-Test den echten Eigentümer bei unverändertem Modus 0700. Windows verwendet einen veränderten Sicherheitsdeskriptor für den Eigentümertest und echte ACLs sowie eine Junction für die Dateisystemtests. |
| P2-01 · Rotation und Konkurrenz | Der Rotationstest liest die tatsächlichen Data-Protection-Key-IDs aus beiden Session-Tickets. Ein separater Ring mit ausschließlich neuer Schlüsseldatei und neuem Zertifikat akzeptiert das neue Ticket. Mit dem alten Zertifikat allein scheitert der Start. Nach erneutem Neustart akzeptiert der vollständige Ring beide Session- und Antiforgery-Tickets. Fehlende Entschlüsselungszertifikate, beschädigtes XML, fehlerhafte PKCS#12-Dateien und abgelaufene Schreibzertifikate lassen den Host kontrolliert scheitern. Die Login-Tests prüfen Deaktivierung mit und ohne Wiederfreigabe an beiden definierten Haltepunkten. Nach Wiederfreigabe erhält erst ein neuer Login gültige Autorität. [Data-Protection-Ticketformat](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/authenticated-encryption-details?view=aspnetcore-10.0) |
| P2-02 · Kataloggate | Das Inventar vergleicht Spaltentypen und Nullbarkeit, gebundene Constraint-Spalten, kanonische Check-Ausdrücke, FK-Ziele und -Aktionen sowie den zugehörigen Primary-Key-Index. Sieben PostgreSQL-Negativtests decken gleichnamige Änderungen an Hashlänge, Zeitgrenzen, Singleton, PK-Spalte, FK-Ziel, FK-Aktion und Nullbarkeit ab. Der unveränderte Katalog besteht im Read-only-Modus. |

## Änderungsumfang

| Bereich | Dateien |
| --- | --- |
| API-Host | `Program.cs`, `VIA.MiniDocs.Api.csproj`, `Security/KeyRingPath.cs`, `Security/LocalSecurityOperations.cs`, `Security/LoginRateLimits.cs`, `Security/ProtectedKeyRing.cs`, `Security/SecurityRegistration.cs`, `Security/SessionHttp.cs`, `Security/SessionMiddleware.cs` |
| Infrastructure | `VIA.MiniDocs.Infrastructure.csproj`, `Identity/AdministratorAccounts.cs`, `Identity/IdentityInput.cs`, `Identity/SessionStore.cs` |
| PostgreSQL | `Persistence/GrantRuntimePrivileges.sql`, `Persistence/PrepareDatabaseRoles.sql`, `Persistence/RestoreDatabaseConnect.sql`, `Persistence/Migrations/20261127090000_SessionSecurity.cs`, `Persistence/Migrations/D4/SessionSecurity.sql` |
| Backendtests | `HostTests.cs`, `KeyRingPathTests.cs`, `LoginRateLimitTests.cs`, `PostgresFinalHardeningTests.cs`, `PostgresModelTests.cs`, `PostgresRoleAndRestoreTests.cs`, `PostgresSessionTests.cs`, `PostgresTestDatabase.cs`, `SessionHostTests.cs`, `SessionKeyRingFixture.cs` |
| Vertrag und Dokumentation | `backend/contracts/fixture.generated.ts`, `README.md`, `docs/D4-A-VERIFICATION.md`, `docs/MASTERPLAN.md`, `docs/TICKETS.md` |

Der Arbeitsbaum umfasst 15 bereits versionierte geänderte Dateien und 18 neue Paketdateien. D4-A-FIX änderte elf Dateien, darunter die neu hinzugekommenen `KeyRingPath.cs` und `KeyRingPathTests.cs`. `#INTERNAL DOCS/`, `.local-review/` und `MiniDocs-Start.cmd` waren schon in der Baseline lokal vorhanden. HEAD und Git-Index blieben auf dem Ausgangsstand.
