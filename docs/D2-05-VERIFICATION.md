# D2-05 – Befundmatrix und Verifikation

Die Befunde des unabhängigen Nachreviews und die nachfolgenden Korrekturen stehen in der [Erweiterung D2-06](D2-06-VERIFICATION.md). Die folgenden Ergebnisse dokumentieren den damaligen D2-05-Stand.

**Stand 26.11.2026.** Branch `codex/d2-postgresql-persistence`, Ausgangscommit `48af1e323b5f6345c68737d22380fcc7d25c83c2`. Die D2-Persistenz und die D2-05-Korrekturen liegen im lokalen Arbeitsbaum. Der unabhängige statische Review „Unabhängiger Backend-Review – VIA.MiniDocs D1/D2“ ist die Quelle der Original-Befund-IDs. Dieser Nachweis zeichnet die Implementierung und die Prüfergebnisse nach. Die unabhängige Neubewertung und technische Gesamtabnahme stehen aus.

Das Backend behält .NET 10, EF Core, Npgsql und seine bestehende Solution-Struktur. [ADR-019](DECISIONS.md#adr-019--verbindliche-persistenzbefehle-freigegeben-für-d2-05) legt die Schreibgrenze fest. Die Migration `EnforceWriteBoundaries` installiert gezielte Befehle und Rechte zusammen. Erstellungszeitstempel stammen aus der DB. Fachbefehle prüfen erwartete Versionen, erhalten unveränderte Versionen bei No-ops und erhöhen bei Mutationen die betroffenen Versionen und ggf. die Transkriptrevision atomar. ASP.NET Core Identity behält den EF-Store mit spaltenbeschränkten Routineoperationen. Authentifizierte Workspace-Auswahl und externer I/O bleiben im späteren Application-/API-Ablauf.

## Originalbefunde

Dateinamen in der Tabelle beziehen sich auf [Infrastructure/Persistence](../backend/src/VIA.MiniDocs.Infrastructure/Persistence), dessen [Migrations-SQL](../backend/src/VIA.MiniDocs.Infrastructure/Persistence/Migrations/WriteBoundary), [Domain](../backend/src/VIA.MiniDocs.Domain) oder [Backendtests](../backend/tests/VIA.MiniDocs.Tests). „Bestanden“ bezieht sich auf den D2-Nachweis in der isolierten Suite.

| Original-ID | Ursache | Implementierte Korrektur | Betroffene Dateien | Zugehörige Tests | Verifikationsstatus | Spätere Zuordnung |
| --- | --- | --- | --- | --- | --- | --- |
| P1-01 | Seiten-DML umgeht Revision und Phantomschutz | Geschützte Tabellen. Replace-All und Seitenedit prüfen Revision und pflegen Aggregat und Seiten im selben Befehl. | `Documents.sql`, `Privileges.sql`, `TranscriptPersistence.cs` | T01, T02, vorhandene Transkripttests | Bestanden | D7 nutzt diese Grenze für API/Import. |
| P1-02 | Zulässiger Einzelstatus erlaubt verbotenen Übergang | Zustandsgebundene Befehle, CHECKs und Datei-Übergangstrigger. Unknown bleibt für weitere Remote-Writes gesperrt. Bestätigte Zuordnung kann in den Verifikationspfad wechseln. | `Files.sql`, `OperationLeasePersistence.cs`, `Privileges.sql` | T04–T06, T11, T14 | Bestanden | D5 verifiziert Belege und kontrollierte externe Wiederanläufe. |
| P1-03 | Verifiziertes Konto konnte ohne lokalen Abschluss aktiviert werden | Verzögerte Prüfung fordert passenden aktiven Provisionierungsvorgang. Aktivierung schließt Vorgang, bindet Konto und entfernt Einladung zusammen. CHECKs sichern Credential-Mindestform. | `Provisioning.sql`, `ProvisioningPersistence.cs` | T07, Rollen-/Restore-Test, vorhandene Provisionierungs-Leasetests | Bestanden | D4 weist externen Login und Data Protection nach. |
| P1-04 | Dokument-/Operations-DML und Receipt-DML umgehen Purge und Frist | Purge-Befehl verbindet Bereinigung, Beleg und Sperrmarken. Terminaler DELETE erfordert Receipt bei Entfernung. Receipt-Löschung nur im fälligen begrenzten Job. | `Files.sql`, `Privileges.sql`, `TrashPersistence.cs`, `OperationRetentionPersistence.cs` | T08–T11, T16, vorhandene Trash-/Retention-Tests | Bestanden | D5 weist den tatsächlichen Remote-Löschbeleg nach. |
| P1-05 | Direkter Kindzeilenpfad kann inverse Sperrreihenfolge nutzen | Runtime verliert Kind-DML. Befehle sperren zuerst den Workspace. Tatsächliche Workspace-Zeilenänderung erhält Snapshot-Schutz. Transaktionshelfer bildet `40001` und `40P01` auf lokalen Konflikt ab. | `Foundation.sql`, `Privileges.sql`, `D2Commands.cs`, `LocalWriteTransaction.cs` | T02, T12, T13, T15, bestehende Baumkonkurrenztests | Bestanden auf Runtime-Grenze; privilegierte Korrekturpfade bleiben betrieblich kontrolliert | D4/D5 testen Commit-vor-HTTP und keine externen Konfliktretries. |
| P1-06 | EF-Concurrency-Token inkrementiert die Fachversion nicht selbst | Direktes Fach-`SaveChanges` ist der Runtime gesperrt. Gezielte Befehle verlangen und erhöhen Fachversionen. Private Setter und DB-Checks schützen Versionswerte. Tagzuordnung/-löschung invalidieren betroffene Dokumente. | `Foundation.sql`, `Documents.sql`, `Entities.cs`, `DocumentPersistence.cs` | T03, Tagversionstest, ursprünglicher EF-Concurrency-Test | Bestanden | D6 sichert Versionen der späteren Mehrfachendpunkte. |
| P2-01 | Kombinationen aus Status, Lease, ID, Hash und Versuch schwach | Lebenszyklus-CHECKs für Datei- und Provisionierungsoperationen. Positiver gemessener Digest, gültige Lease und externe Identität passend zum Zustand. | `Files.sql`, `Provisioning.sql` | T06, T07, vorhandene Lease-/Digesttests | Bestanden | D4/D5 liefern hochwertige externe Belege. |
| P2-02 | Öffentliche Setter auf Fachstatus, IDs, Leases und Versionen | Kritische Entity-Eigenschaften gekapselt. AspNet-Identity-Schutzfelder sind nicht Teil des Routineupdates. Historischer EF-Token-Test setzt Version über EF Entry als expliziten Mappingnachweis. | `Entities.cs`, `Workspace.cs`, `MiniDocsUser.cs`, `IdentityAndWorkspaceConfigurations.cs` | T03, Identity-Store-Test, Domain-Mappingtests | Bestanden | – |
| P2-03 | Kein globaler Query-Filter | Workspace-FKs bleiben erhalten und Befehle binden Workspace/Ressource. Lokale Dokumentbefehle verlangen aktiven Workspace mit freigegebenem Owner. Der vertrauenswürdige Request-Workspace fehlt im D2-Host. | `Foundation.sql`, `Documents.sql`, `Files.sql`, `TICKETS.md` | Workspace-Isolationstests; HTTP/Query-Filter offen | Lokal geprüft, Query-Filter offen | D4/D6-Gate mit fail-closed Scope und Negativtests vor Fachdatenendpunkten. |
| P2-04 | Leere technische Credentials und Leerwerte möglich | Datenbank prüft technische Credentials, positive Versionen, kritische Datei-, Request-, Lease- und Kandidaten-IDs. Befehle validieren Argumente am fachlichen Einstieg. | `Foundation.sql`, `Provisioning.sql`, `Files.sql`, `Documents.sql` | T06, T07, Modell- und Migrationssuite | Bestanden im D2-Umfang | D4 validiert externe Credential-Semantik. |
| P2-05 | Clientwerte überschreiben DB-Zeitdefaults | Clientseitige Erstellungszeit entfernt. DB-Defaults für Erzeugung, DB-Uhr beim Schreiben und nach relevanter Sperre bei Ablaufvergleichen. | `Entities.cs`, `Workspace.cs`, Schreib-SQL | T03, Einladungsablauf-/Retention-Tests | Bestanden | – |
| P2-06 | `name_key` kann leer oder falsch normalisiert sein | Nichtleere Normalform als CHECK. Runtime kann Tags nicht direkt anlegen oder umbenennen. Zuordnung und Löschung sind versioniert. | `Documents.sql`, `Privileges.sql`, `TICKETS.md` | Tagversionstest; vollständige Unicode-Parität offen | Lokale DB-Grenze geprüft | D6 muss Normalform bei Create/Rename und Unicode-/Kollationsparität nachweisen. |
| P2-07 | Freies Textfeld für Dateivorgangsfehler | Enum, explizite stabile EF-Konvertierung und geschlossener SQL-Wertebereich. | `DocumentStates.cs`, `Entities.cs`, `DomainTextConverters.cs`, `FileOperationConfigurations.cs`, `Files.sql` | `DomainTextMappingTests`, EF-Drift | Bestanden | D3/D5 ordnen externe Fehler typisiert und redigiert zu. |
| P2-08 | Rollback, Tracking und Commit-Vertrag beim Aufrufer | Atomare Funktionsstatements plus `LocalWriteTransaction` für zusammengesetzte lokale Abläufe. Commit vor Ergebnis, Rollback bei Konflikt/Abweisung, kein eingebauter Retry. | `LocalWriteTransaction.cs`, `D2Commands.cs` | T03, T13, T16, Identity-Store-Test | Bestanden | D4/D5 verwenden den Wrapper für ihren Workflow und testen Commit-vor-HTTP. |
| P2-09 | Fach-DML, Spalten-ACLs und Sammelgrant zu breit | Rechte nur für vorgesehene Tabellen/Felder, Signatur-Liste und isolierter NOLOGIN-Funktionsbesitzer. Interne Helper sind privat. Kein standardmäßiges `PUBLIC EXECUTE`. | `PrepareDatabaseRoles.sql`, `Privileges.sql`, `GrantRuntimePrivileges.sql` | T11, Upgrade-ACL-Test, Rollen-/Restore-Test | Bestanden | D8 prüft effektive Rollen im Ziel. |
| P2-10 | Seitenzahl, Seiten und Transkriptflags können widersprechen | Geschützte Seitenzahländerung und Pending-Zuweisung mit beiden Revisionen. Verzögerter Konsistenzcheck für bestehende Seiten. | `Documents.sql`, `DocumentPersistence.cs`, `TranscriptPersistence.cs` | T01, T02, `PendingAssignmentChecksBothRevisionsAndPageCount` | Bestanden im D2-Umfang | D7 prüft API, Import, Mehrdeutigkeit und fachliche Anwendung der Quellmetadaten. |

## Weitere Reviewhinweise

Der Review vergibt hier keine eigenen P3-Nummern. Die Abschnittsnummern sind die Originalreferenzen.

| Originalreferenz | Ursache | Korrektur und Dateien | Test / Nachweis | Spätere Zuordnung |
| --- | --- | --- | --- | --- |
| §6.1 und §11 | Workspace-Zeilenupdate kostet WAL und serialisiert fachliche Writes | Bestehendes Sperrprinzip beibehalten und in `Foundation.sql` zentralisiert. | T12, T13, T15 bestanden | D8 misst Last und Sperrkosten. |
| §7.1–7.4 | Mappingtests, DB-Zustandstests und Remote-Nachweise haben unterschiedliche Reichweite | Runtime-Tests ergänzen direkte Alternativpfade. Die Originalkonkurrenztests bleiben erhalten. | T01–T16; D2-Tests erfassen keine realen externen Anfragen. | D3–D5 prüfen Netzwerk und externe Seiteneffekte. |
| §7.5 und §10.4 | Duplizierte Sperrwartehelfer und kurze Wartezeit | `PostgresWait.cs` beobachtet `pg_blocking_pids()` für alte und neue Tests mit gemeinsamem begrenztem Timeout. | Zwei vollständige Läufe bestanden | D8 wiederholt unter CI-Last; keine Aussage zur Langzeit-Flakiness. |
| §8 | Kurzlebiges Testpasswort im Docker-CLI-Argument | `PostgresTestDatabase.cs` reicht es als Prozess-Umgebungsvariable weiter. Eigene Testcontainer bleiben an Loopback gebunden und werden über eindeutiges Eigentum bereinigt. | Beide vollständigen Testläufe bestanden | Docker-Berechtigte können Containerumgebung weiterhin sehen. |
| §9.1 | EF-Drift umfasst manuelles SQL und ACLs nicht | Separate Katalog-, Ablauf- und Rechteprüfungen. | EF-Drift, T11, Restore bestanden | – |
| §9.2 | Initiales Down ist destruktiv. Historische Migration muss reproduzierbar bleiben. | `D2IntegritySql` und Initialmigration unverändert. Additives Upgrade. Folge-Migration verweist für Rückfall auf Backup-Wiederherstellung bei gesperrtem Schreibbetrieb. | Originaler Down/Up-Test bleibt für die initiale Migration; zusätzlich Upgrade und Restore bestanden. | D8 systemübergreifender Rückfall. |
| §9.3 | Restore desselben Testservers mit existierenden Rollen und Platzhalterbytes | Bestehender Dump-/Restore-Test prüft neue Besitz- und Runtime-Grenze und positive Schreibbefehle nach Restore. | `RestrictedRolesMigrateOperateAndRestoreWithoutGrantingDdlToRuntime` bestanden | D8 separater Server, Rollen, Schlüsselring und VIA.Docs-Abgleich. |
| §10.3–10.4 | Wachsende gekoppelte Klassen, doppelte Leasevalidierung, uneinheitlicher Stil | Provisionierung in `ProvisioningPersistence.cs`, Dateioperationen und Purge bleiben getrennt. Leasevalidierung in `D2Commands.cs`. SQL wählt feste Fachbefehle, C# verwendet Enums. Einheitliche Einrückung im geänderten Persistenzcode. | Build und Mappingtests bestanden. | Breiter Formatierungsumbau ist für D2 nicht erforderlich. |

## Alle 16 geforderten Nachprüfungen

Die neuen Methoden stehen in `PostgresWriteBoundaryTests.cs`, soweit keine andere Klasse angegeben ist.

| Test-ID | Originalanforderung in Reihenfolge des Reviews | Tatsächlicher Nachweis |
| --- | --- | --- |
| T01 | Direkte Seitenänderung gegen Replace-All mit alter Revision | `AlternativePageWritesAreDeniedWhileReplaceAllHoldsTheWorkspace`, UPDATE-Fall. Direkte Änderung abgewiesen, echter vorheriger Commit macht alten Replace-All ungültig. Bestanden. |
| T02 | Paralleler Seiten-INSERT/-UPDATE/-DELETE über alternative Pfade | Dieselbe Theorie mit allen drei DML-Varianten während aktivem Replace-All. Alle abgewiesen, gespeicherter Text und Revision geprüft. Bestanden. |
| T03 | Gewöhnliches EF-Update ohne manuelles Inkrement | `TrackedUpdateCannotBypassVersionAndCommandRejectsStaleVersion`. EF-Bypass abgewiesen, Fachbefehl erhöht Version, alter Befehl scheitert. Bestanden. |
| T04 | Unknown darf nicht Prepared oder Failed werden | `InvalidUploadTransitionsAreRejectedEvenByTheLegacyTablePath` sowie Runtime-Rechteprüfung. Bestanden. |
| T05 | Completed darf nicht reaktiviert werden | Derselbe Transitionstest, Fall Completed → Prepared. Bestanden. |
| T06 | Unzulässige Lease-/Remote-ID-/Digest-Kombinationen | `InconsistentLifecycleRowsFailAtTheDatabase` und `UploadFencesOldWorkersAndCompletesOnlyWithMatchingDigest`. Bestanden. |
| T07 | Aktiver Workspace ohne lokalen Abschluss | `ActiveWorkspaceRequiresAndRetainsVerifiedTechnicalIdentity` prüft Konto ohne Abschluss und positives Paar. Rollen-/Restore-Test nutzt echten lokalen Provisionierungsabschluss. Bestanden. |
| T08 | Direkte Dokumentlöschung ohne Purge | `RuntimeHasOnlyExplicitCommandsAndNoProtectedDml`. Bestanden. |
| T09 | Entfernen abgeschlossenem DELETE ohne Receipt | `DeletingCompletedDeleteWithoutReceiptViolatesConstraint`; zusätzlich generelles Runtime-DELETE-Verbot. Bestanden. |
| T10 | Vorzeitige Receipt-Löschung | Direktes DELETE abgewiesen. `RetentionIsBoundedWorkspaceScopedAndNeverRemovesUnknownOrRecentOperations` prüft Frist und Batch. Bestanden. |
| T11 | Negative Runtime-Nachweise zu allen P1 | `RuntimeHasOnlyExplicitCommandsAndNoProtectedDml`, `UpgradeClosesLegacyTableAndColumnGrants` und T01–T03. Bestandene Objekt-, Spalten-, Funktions- und Rollenchecks. |
| T12 | Workspace-first gegen direkten Kindzeilen-Schreibpfad | T02 prüft direkte DML-Verbote während wartungsrelevanter Transaktion. Bestehende Topic-/Document-Tests beobachten Sperre und FK-Schutz. Bestanden. |
| T13 | Kontrollierter Deadlock-/SerializationFailure-Rollback | `DeadlockReturnsConflictAndRollsBackEarlierWork` und `SerializationFailureReturnsConflictAndRollsBackEarlierWork`. Echte PostgreSQL-Konflikte, Endzustand überprüft. Bestanden. |
| T14 | Retirement und Reservierung in beiden Reihenfolgen | `ReservationCannotRaceRetirementInEitherOrder`. Genau eine Sperrmarke und keine neue Operation bleiben zurück. Bestanden. |
| T15 | Unabhängige Workspaces parallel | `IndependentWorkspaceCanWriteWhileAnotherIsLocked`. Zweiter Workspace committet unter gehaltenem ersten Workspace. Bestanden. |
| T16 | Erzwungener DB-Fehler mitten in Purge und Uploadabschluss | `RealDatabaseFailureMidCommandRollsBackAllWrites`, zwei Fälle mit kontrolliertem DB-Triggerfehler. Operation, Dokument, Receipts und Sperrmarken nach Rollback geprüft. Bestanden. |

## Ergänzende Befunde aus D2-05A

| Original-ID | Ursache | Korrektur und Dateien | Test / Verifikationsstatus | Spätere Zuordnung |
| --- | --- | --- | --- | --- |
| E-01 | Breite direkte DML | P1-01/02/04/06, `Privileges.sql` | T11 bestanden | – |
| E-02 | Kindzeile vor Workspace | P1-05, `Foundation.sql`, `LocalWriteTransaction.cs` | T12, T13, T15 bestanden | Kontrollierte DBA-Pfade bleiben vorgeschrieben. |
| E-03 | Fehlende Übergangsgrenze | P1-02, P2-01, `Files.sql`, `Provisioning.sql` | T04–T07 bestanden | Remote-Belege D4/D5. |
| E-04 | Umgehbare Purge- und Receipt-Atomarität | P1-04, `Files.sql` | T08–T10, T16 bestanden | D5 externe Bestätigung. |
| E-05 | Entkoppelte Transkript-/Dokumentversionen | P1-01/06 und P2-10, `Documents.sql` | T01–T03, Tag-/Pending-Tests bestanden | Vollständige API D6/D7. |
| E-06 | Einladung und Aktivierung umgehbar | P1-03, `Provisioning.sql`, `InvitationPersistence.cs` | T07, echter Identity-Store, Einladungsrennen bestanden | D4 Sitzung und externe Verifikation. |
| E-07 | Funktions-Sammelgrant, mögliche spätere Rechteerhöhung | P2-09, `Privileges.sql`, fester `search_path` | T11 und Runtime-Restore bestanden. Katalog prüft Definer, NOLOGIN-Besitz und festen Suchpfad. Ein gleichnamiges Objekt im Sitzungssuchpfad ändert den Schreibpfad nicht. | D8 Ziel-ACLs. |
| E-08 | Uploadreservierung und Abschlussvorbedingungen | `Files.sql`, versionierte Dokument-/Operation-Kopplung | T14, T16, Digests, falsche Dokumentversion und Fence geprüft | D5 Bytes und verifizierte Metadaten. |
| E-09 | Wartungsgrenze und Sperrmarken | `Files.sql`, Frist und strikt begrenzte Befehle | T10, T14 bestanden | – |
| E-10 | Kein authentifizierter Workspace-Filter oder Session-Schutz | P2-03, `Foundation.sql`, explizite D4/D6-Gates | Lokale Workspace-Grenze geprüft. Authentifizierter End-to-End-Nachweis offen. | D4/D6. |
| E-11 | Primär privilegierte Tests | `PostgresWriteBoundaryTests.cs`, Rollen-/Restore-Fixture, gemeinsame Beobachtungshilfe | T01–T16 und beide vollständigen Läufe bestanden | – |
| E-12 | Restore erhält Bytes, Statusdokumentation war älter | Restore mit neuer Runtime und Grants. Architektur, Entscheidungen, Tickets und Masterplan synchronisiert. | DB-Restore und EF-Historie bestanden | D8 Entschlüsselung, externer Bestand, produktionsnaher Restore. |

## Ausgeführte Prüfungen

| Prüfung | Ergebnis |
| --- | --- |
| Docker und Image | Docker-Server 29.9.2 erreichbar. Lokal vorhandenes `postgres:18.7` mit gepinntem Digest `sha256:c699303802144847d238196173655dbbecf7a96aeca1565f2b02eb671a1f0b6b`. Ausschließlich Fixture-eigene PostgreSQL-Container. |
| Unveränderte Baseline | 98 bestanden, 0 fehlgeschlagen, 0 übersprungen. |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode` | Exitcode 0. |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore --configuration Debug` | Exitcode 0. |
| Vollständiger Debug-Testlauf | 127 bestanden, 0 fehlgeschlagen, 0 übersprungen. Exitcode 0. |
| Locked Restore und vollständiger Debug-Testlauf mit separatem `--artifacts-path` | 127 bestanden, 0 fehlgeschlagen, 0 übersprungen. Exitcode 0. |
| `dotnet ef migrations has-pending-model-changes` | Keine Modellabweichung. Exitcode 0. |
| PostgreSQL-Migration, Upgrade und Restore | Leere Datenbank, Upgrade vom Initialschema einschließlich alter Spalten-/Funktionsgrants, Ablehnung eines nicht belegten Altabschlusses, initiales Down/Up und `pg_dump`/`pg_restore` bestanden. |
| OpenAPI, TypeScript und Contract Drift | `npm run check`, `npm run typecheck` und `npm run test:drift` im Backend bestanden. Der negative Test weist eine veraltete Vertragsdatei nach. Gemeinsame Verträge und Frontend erhielten keine Änderungen. |
| Git-Diff-Prüfung | `git diff --check` sowie Einzelprüfung der 46 unversionierten Backend-/Dokumentationsdateien ohne Whitespace-Befund. Kein Diff in gemeinsamen Vertragsdateien oder Frontend. |
| Befehlsinventar | 39 SQL-Befehle mit zugeordnetem C#-Aufruf. 38 sind der Runtime mit exakter Signatur freigegeben. Die isolierte Einlösung ohne Workspace-Anlage bleibt intern. |

## Arbeitspakete aus D2-05A

| Paket | Implementierung und Nachweis |
| --- | --- |
| P1 · Rollen und Transaktionsgrenze | Additive Migration, fester Definer-Kontext, Workspace-Lock und explizite ACLs. P1-05, P2-08/09 und T11–T15. |
| P2 · Einladungen, Identity und Provisionierung | Einladungslock, atomare Registrierung, gefenceter Abschluss und aktive Workspace-Invariante. P1-03, P2-04 und T07 samt echtem EF-Identity-Store. |
| P3 · Fachaggregate und Versionen | Themen-, Tag-, Dokument-, Transkript- und Pending-Befehle sowie Versions- und Seitenzahlregeln. P1-01/06, P2-02/05/06/10 und T01–T03. |
| P4 · Dateien, Papierkorb und Retention | Gebundene Reservierung, Lebenszyklus, Lease-Fencing und Abschluss einschließlich Receipt und Tombstone. P1-02/04, P2-01/07 und T04–T06, T08–T10, T14/T16. |
| P5 · Verifikation und Dokumentation | Beide vollständigen Läufe, gezielte Katalog-/Runtime-Tests, Upgrade, Restore, EF-/Contract-Drift und konkrete Folgegates. |

## Freigabegrenze

Die API hat weiterhin nur technische Vertragsfixtures. Für spätere fachliche Schreibpfade müssen die freigegebenen Befehle mit serverseitig ermitteltem Workspace verwendet werden. Die gemeinsame Runtime-Anmeldung prüft lokale Invarianten und kann die Identität eines Browserbenutzers oder den Erfolg eines Remote-Aufrufs nicht selbst beweisen. D4/D6 ergänzen authentifizierten Workspace-Kontext und Query-Filter. D3/D5 prüfen die VIA.Docs-Semantik und den tatsächlichen Commit-/HTTP-Ablauf. D8 prüft Restore mit Rollen, Schlüsseln und externer Konsistenz sowie Betriebs- und Lastverhalten. Alle Übergaben sind als Freigabegates im [Ticketkatalog](TICKETS.md) verankert.
