# D4-C · Technische VIA.Docs-Provisionierung und Recovery

## D4-C-FIX · Reviewbefunde

Der Worker bindet jeden Provisionierungsaufruf an Workspace, Vorgang, Konto, Credential-Generation, Lease-Owner, Version und Aufrufart. Der D3-Client prüft diese Autorität nach der Credential-Auflösung unmittelbar vor dem Transport anhand eines frischen PostgreSQL-Lesevorgangs. Dieser erfolgreiche Lesevorgang ist der maßgebliche lokale Autorisierungspunkt. Eine danach eintretende Deaktivierung kann einen bereits autorisierten oder möglicherweise versandten Netzwerkaufruf nicht rückwirkend verhindern. Der SQL-Fence schützt weiterhin den lokalen Abschluss. Zwischen Prüfung und HTTP besteht keine offene Datenbanktransaktion.

Der Worker prüft vor Kandidatenspeicherung und Completion erneut den eigenen Fence, die Lease-Restdauer und das Gesamtbudget einschließlich Reserve und lokalem Transaktionsbudget. Vor dem D3-Transport prüft die versuchsgebundene Autorisierung zusätzlich das verbleibende Gesamtbudget und die Lease. Shutdown unterbindet weitere lokale Schritte. Möglicherweise ausgeführte externe Aktionen bleiben konservativ im persistenten Zustand.

| Befund | Nachweis |
| --- | --- |
| P1-01 | `OldWorkerCannotDispatchAfterReenableBeforeCredentialResolution` hält den echten D3-Pfad vor der Credential-Auflösung an. Nach Deaktivierung und Wiederfreigabe unterbleiben der alte Registrierungs-POST beziehungsweise Recovery-Login. Der alte Completion-Fence wird zusätzlich abgewiesen. |
| P2-01 | `AdministratorAuthorityLossSerializesWithProvisioning` kombiniert Prepare, Austausch und Recovery mit Deaktivierung, Rollenentzug und SecurityStamp-Änderung in beiden Gewinnerreihenfolgen. `pg_blocking_pids` bestätigt die Sperrkante. Ein zuvor entzogener Actor erhält keine Annahmebestätigung und bewirkt keine Zustandsänderung. |
| P2-02 | `ExhaustedBudgetOrLeasePreventsCandidateAndCompletion` prüft Gesamtbudget und Lease jeweils vor Kandidatenspeicherung und Completion. `ShutdownAfterPossibleDispatchDoesNotPersistCandidateOrComplete` prüft den Abbruch nach möglichem Versand. |

Die unabhängige statische Nachprüfung der 25 Paketdateien hat alle drei FIX-Befunde geschlossen. D4-C ist mit **Note 1− (1,3)** technisch freigegeben. Der Reviewer hat keine unabhängigen Testläufe ausgeführt.

Die FIX-Verifikation umfasst je 435 bestandene Backendtests in Debug und Release ohne Skips. Der TRX-Namensvergleich bestätigt alle 410 D4-C-Bestandstests in beiden Konfigurationen. Der vollständige Frontendcheck ergab 225 bestandene Tests. Vite meldet die bestehende Chunkgrößenwarnung.

| FIX-Prüfung | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false -p:NuGetAudit=true -p:NuGetAuditMode=all` | 0 | Locked Restore mit NuGet-Audit. |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Debug --disable-build-servers -m:1` und Release | je 0 | Keine Warnungen oder Fehler. |
| `dotnet test backend/VIA.MiniDocs.sln --no-build -c Debug` und Release | je 0 | Je 435 bestanden, keine Skips. |
| `npm --prefix backend run check`, `typecheck` und `test:drift` | je 0 | Vertrag aktuell, TypeScript gültig, negativer Drift-Test bestanden. |
| `npm --prefix frontend run check` | 0 | 225 Tests und Build bestanden. |
| TRX-Vergleich, `git diff --check`, `git diff --cached --exit-code` | je 0 | Alle 410 Bestandstests vorhanden, keine Whitespace-Fehler, leerer Index. |
| Secret-Mustersuche mit `rg` | 1 | Keine Treffer. |

Der FIX ändert gegenüber dem D4-C-Stand `ViaDocsAuthentication.cs`, `ProvisioningCredentialProvider.cs`, `ProvisioningWorker.cs`, `TechnicalProvisioningTests.cs` und diesen Nachweis. Das gesamte Paket umfasst damit 25 Dateien. Die separate Prüfung des tatsächlichen Remote-Branchs wurde für den FIX nicht wiederholt. Lokales `HEAD`, `main` und `origin/main` stehen auf `d3996f8c18555ead8d205471a0c4865fd4f4dab7`.

## Ausgangsstand

Der verifizierte Branch ist `main`. `HEAD`, `main`, der lokale `origin/main` und der separat mit `git ls-remote --exit-code origin refs/heads/main` abgefragte Remote waren zum Beginn `d3996f8c18555ead8d205471a0c4865fd4f4dab7`. Der Index war leer. D4-B liefert die Referenz von je 385 Backendtests in Debug und Release sowie 225 Frontendtests.

## Implementierung und Sicherheitsgrenzen

- Die neue Migration `20261127100000_TechnicalProvisioning` ergänzt `credential_generation` als positiven `bigint` mit Standardwert 1. `ViaDocsAccount.Id` bleibt die Referenz. Die Generation bezeichnet das Credential, die Aggregatversion schützt die aktuelle Schreibautorität. Der Runtime-Principal erhält genau ein zusätzliches `EXECUTE`. Es gibt keine zusätzlichen allgemeinen Schreibrechte. Das lesende Kataloggate prüft Spaltentyp, Default, Constraint, ACLs und die Hashes aller vier betroffenen Befehle. Historische Migrationen bleiben unverändert.
- Der neue Austauschbefehl setzt unter der Workspace-Sperre eine exakte Version, Pending, einen aktivierten Eigentümer, einen unbegonnenen Ready-Vorgang und ein unverifiziertes Konto voraus. Er ändert Einladungsciphertext, Version und Aktualisierungszeit. Claim und Complete behalten ihre Signaturen. Sie prüfen die gültige Eigentümerzeile mit `FOR SHARE OF u` und den zugehörigen Vorgang unter der Workspace-Sperre. Complete vergleicht Kandidat, ApplicationId und Fence und entfernt die Einladung. Recovery kann die technische Bindung eines suspendierten Workspace abschließen und erhält die Suspendierung.
- Kontodeaktivierung erhöht in derselben Transaktion die Version laufender Registering- und RecoveryRequired-Vorgänge. Kandidat, Lease und Status bleiben erhalten. Eine Wiederfreigabe stellt die alte Version nicht wieder her. Bereits autorisierte oder möglicherweise versandte HTTP-Aufrufe werden konservativ über die gespeicherte Evidenz und Recovery behandelt.
- Neue Secrets stammen aus 32 CSPRNG-Bytes. Data Protection verwendet den persistenten X.509-geschützten D4-A-Ring. Separate Formate `MDC1` und `MDI1`, feste Payloadgrenzen und getrennte Zwecke binden Credentials an Workspace, Konto und Generation sowie Einladungen an Workspace und Vorgang. Der Provider überprüft die aktuelle PostgreSQL-Bindung in jedem Scope. Die Worker-Lease ist eine separate Autorisierungsgrenze. Bestandsinventar und erneute Kompatibilitätsprüfung sperren inkompatible Werte, bevor der Worker schreibt. Bei Ready werden bis zum eigenen bestätigten Begin lediglich Hülle und Länge des Einladungsciphertexts geprüft.
- Der standardmäßig deaktivierte Worker nutzt zyklisch nach Workspace-ID sortierte, begrenzte Batches. Standardwerte sind 5 Sekunden Polling, Batchgröße 8, zwei parallele Versuche je Host, 120 Sekunden Lease, 75 Sekunden Versuch, mindestens 15 Sekunden Reserve und bis zu 5 Sekunden pro lokaler Operation. Die D2-Grenze von 900 Sekunden gilt weiter. Die Startvalidierung prüft die Budgets gegen den D3-Timeout. PostgreSQL speichert die gesamte Arbeitsautorität.
- Der Worker verlangt bestätigten Begin-Commit und anschließende Prüfung seines eigenen Fence vor dem einzigen Registrierungs-POST. Der Registrierungsbefund ist Kandidatenevidenz. Nur eine unabhängige Verifikation mit `FindAsync` erlaubt den Abschluss. Unklarer Begin berechtigt auch bei gefundener Operations-ID zu keinem POST. Unklarer Kandidatencommit oder unbekannter Remoteausgang führen zur Recovery nach Lease-Ablauf. Bei unklarem Abschlusscommit wird das konsistente lokale Tupel aus Workspace, Vorgang, Konto und ApplicationId gelesen. Fehlgeschlagene Verifikation erhält die Prüflease bis zum Ablauf als Backoff.
- Die drei Admin-Routen verwenden die D4-A/B-Autorisierung, `no-store`, CSRF für POST und die bestehende Cursor-Bindung. Die Projektion enthält Workspace-ID, Status und Zeitstempel. Die POSTs bestätigen mit 202 lediglich eine lokal bestätigte, verfügbare Verarbeitung. `Location` ist die gefilterte Statusabfrage. `Retry-After` steuert diese Abfrage. Unbestätigte Ergebnisse erhalten 503. OpenAPI und der generierte TypeScript-Vertrag enthalten die neuen Routen.

## Nachweismatrix

| Bereich | Lokaler Nachweis |
| --- | --- |
| Prepare, Austausch, Admin-Guard | Zwei Admins bereiten denselben Workspace vor. Austausch und Begin konkurrieren in beiden Gewinnerreihenfolgen. Konto, Secret, Operations-ID und Generation bleiben beim Austausch identisch. Sitzungsentzug konkurriert mit Prepare, Austausch und Recovery jeweils in beiden Reihenfolgen. Die sperrende Verbindung wird über `pg_blocking_pids` bestätigt. |
| Commit, Kandidat und Find | Zum Zeitpunkt des POST sieht eine unabhängige Verbindung Registering und den Lease-Owner. Vor Find ist der Kandidat gespeichert. Simulierte verlorene ACKs für Begin, Kandidat und Complete werden getrennt geprüft. Abweichende Applications-IDs und suspendiertes Registering sind abgedeckt. |
| Fence und Backoff | Zwei Worker können denselben persistenten Vorgang beobachten. Genau einer sendet den POST. Deaktivierung blockiert den Claim. Deaktivierung und Wiederfreigabe zwischen Begin-Commit und Dispatch sowie während eines angehaltenen Find entziehen den alten Fence dauerhaft. Ein neu aufgebauter DI-Host öffnet denselben X.509-geschützten Ring und respektiert den persistenten Backoff. Nach Lease-Ablauf erfolgt Find mit neuer Lease ohne zweites Redeem. Recovery-Completion belässt Suspended. |
| Provider und D3-Client | Prozessinterne Testtransporte prüfen das D3-Protokoll mit zwei echten PostgreSQL-Workspaces und getrennten Application-IDs und Secrets. Frische Scopes sehen den aktuellen Status, die Lease sowie fremde Referenzen und veränderte Generationen. Bound-Logins beider Workspaces behalten getrennte Cacheeinträge. |
| Fachliche Schutzwerte | Deterministische Zweck- und Kontextprüfungen, falsche Generation, manipuliertes Envelope, falscher Payload und falscher Ring. File-Ring-Neustart, neuer Schreibschlüssel, lesbare alte und neue Credentials, isolierte Dateikopie und falsches Zertifikat. |
| Rechte, Upgrade und Restore | Clean-Install als Migrator und Regression mit Runtime-Rollen. Upgrade aus `SessionSecurity`, Dump/Restore und Bytegleichheit eines inkompatiblen Altbestands ohne Dispatch. Das lesende Gate verwirft ein abgeschwächtes Generation-Constraint, fremde Spaltenrechte und `PUBLIC EXECUTE`. |
| HTTP und Redigierung | Echter Host prüft 401, CSRF, 400, 409, 503 bei fehlender Bereitschaft und verlorenem ACK, 202 mit `Location` und `Retry-After`, idempotente Bestätigung und eine Projektion ohne vertrauliche Felder. Einladung, Secret und Secret-Ciphertext werden in aufgezeichneten Logs und Activities gesucht. |

## Prüfprotokoll

Die Tests nutzen PostgreSQL 18.7 in isolierten Loopback-Containern und für VIA.Docs ausschließlich lokale Testtransporte und Testdoubles. Der TRX-Namensvergleich gegen beide D4-B-Berichte weist alle 385 Bestandsfälle nach.

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false -p:NuGetAudit=true -p:NuGetAuditMode=all` | 0 | Keine Auditwarnung ausgegeben. Ein zusätzlicher Lauf mit `--force-evaluate` war ebenfalls erfolgreich. Er schrieb die Lockfiles neu. Alle fünf Dateien wurden anschließend bytegleich aus der Baseline wiederhergestellt und der reguläre Locked Restore erneut bestätigt. |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -c Debug --disable-build-servers -m:1` und `Release` | je 0 | Keine Warnungen oder Fehler. |
| `dotnet test backend/VIA.MiniDocs.sln --no-build -c Debug` und `Release` mit TRX | je 0 | Je 410 bestanden, darunter alle 385 Bestandsfälle. Kein Skip. |
| `npm --prefix backend run generate`, `check`, `typecheck` und `test:drift` | je 0 | Reproduzierbare OpenAPI-/TypeScript-Ausgabe, strikte Typprüfung. Der invertierte Test weist die erwartete Ablehnung eines veralteten Vertrags nach. |
| `npm --prefix frontend run check` | 1, dann 0 | Erstlauf mit einem Timeout in `TagUI.test.tsx`. Wiederholung mit 225 von 225 Tests, Format, Typprüfung, ESLint, Stylelint und Build. Vite meldet die bekannte Chunkgrößenwarnung. |
| `git diff --check`, `git diff --cached --exit-code`, `git ls-remote --exit-code origin refs/heads/main` | je 0 | Whitespace ohne Befund, leerer Index, Remote-main auf Baseline. |
| `rg` für Whitespace und Konfliktmarker in neuen Dateien sowie Secret-Indikatoren im gesamten Paket | je 1 | Keine Treffer. Secret-Suche mit gezielten Mustern und manueller Prüfung der neuen Schutz- und Loggingpfade. |

## Änderungsumfang

Der ursprüngliche D4-C-Stand umfasste 24 Paketdateien. D4-C-FIX umfasst 25 Paketdateien. Der Git-Index ist leer. `HEAD`, `main`, lokaler `origin/main` und Remote-main stehen weiterhin auf der Ausgangsrevision. Die drei vorbestehenden lokalen Einträge sind dem Paket nicht zugeordnet.

- API und Vertrag bilden `Program.cs`, `ProvisioningHttp.cs` sowie `backend/contracts/fixture.generated.ts`.
- Persistenz und Sicherheitsgrenze umfassen `Entities.cs`, `IdentityAndWorkspaceConfigurations.cs`, `ProvisioningPersistence.cs`, `GrantRuntimePrivileges.sql`, `MiniDocsDbContextModelSnapshot.cs`, `20261127100000_TechnicalProvisioning.cs` und `TechnicalProvisioning.sql`.
- Credentials und Orchestrierung umfassen `ProvisioningAdministration.cs`, `ProvisioningCredentialProvider.cs`, `ProvisioningProtection.cs`, `ProvisioningReadStore.cs`, `ProvisioningWorker.cs` und `ViaDocsAuthentication.cs`.
- Die Tests liegen in `PostgresFinalHardeningTests.cs`, `PostgresRoleAndRestoreTests.cs`, `ProvisioningHostTests.cs`, `ProvisioningProtectionTests.cs` und `TechnicalProvisioningTests.cs`.
- Die Dokumentation umfasst `D4-C-VERIFICATION.md`, `D4-PLAN.md`, `MASTERPLAN.md` und `TICKETS.md`.

## Betriebsübergabe

Die Aktivierung erfordert nutzbare VIA.Docs-Clientkonfiguration, die zur Datenbank gehörenden Data-Protection-Schlüssel und Entschlüsselungszertifikate sowie einen geprüften Restore-Pfad. Alte oder beschädigte Schutzwerte werden ohne Konvertierung oder Löschen gesperrt. Die Bestandsinventur meldet die Anzahl inkompatibler Hüllen in Redaktionsform. Eine notwendige Bestandsbereinigung erfordert einen gesonderten Auftrag. Ein ungeklärter externer Ausgang erfordert weiterhin eine verifizierbare Identität. Die Freigabe von D3-INTEGRATION, D8, D4-D und des produktiven Betriebs erfolgt gesondert. Der kombinierte produktive Datenbank-, Ring- und Zertifikatsrestore sowie reales Lastverhalten gehören zu D8.
