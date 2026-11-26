# D2-06 – Befundmatrix und Verifikation

Stand 26.11.2026, Branch `codex/d2-postgresql-persistence`, Git-Basis `48af1e3`. Ausgangspunkt ist der lokale D2-05-Arbeitsbaum. Der unabhängige D2-FINAL-Nachreview bewertet diesen Ausgangsstand mit 2+ und stellt die technische Abnahme zurück. Die [vorherige Befundmatrix](D2-05-VERIFICATION.md) bleibt als Nachweis erhalten. Der gehärtete Stand wurde am 26.11.2026 im dritten unabhängigen Review technisch abgenommen; Ergebnis und Grenzen siehe unten.

Die Migration [FinalQualityHardening](../backend/src/VIA.MiniDocs.Infrastructure/Persistence/Migrations/20261126180000_FinalQualityHardening.cs) ergänzt D2-05. Die ursprüngliche und die D2-05-Migration bleiben eingefroren. Die Rollenarchitektur, die Solution, EF Core und die 38 öffentlich für die Runtime freigegebenen Signaturen bleiben erhalten. Es wurden keine Laufzeitbibliotheken ergänzt.

## Befundmatrix des Nachreviews

SQL-Dateinamen in der Matrix beziehen sich auf die [D2-06-Migrationsressourcen](../backend/src/VIA.MiniDocs.Infrastructure/Persistence/Migrations/FinalHardening), C#-Namen auf [Persistence](../backend/src/VIA.MiniDocs.Infrastructure/Persistence) oder die [Backendtests](../backend/tests/VIA.MiniDocs.Tests). Die vollständigen Protokolle stehen gesammelt im letzten Abschnitt.

| Original-ID | Ursache | Korrektur und Dateien | Positiver und negativer Nachweis | Status und verbleibende Grenze |
| --- | --- | --- | --- | --- |
| N-P1-01 | Das alte Gate prüfte nur einige ausgewählte Rechte und wurde nach Restore nicht vollständig verwendet. | Ein lesendes `GrantRuntimePrivileges.sql` vergleicht ein versioniertes Sollinventar mit effektiven Rollen-, DB-, Schema-, Tabellen-, Spalten-, Sequenz- und Funktionsrechten. Es prüft Owner, Definer, Suchpfad, Funktionskörper, Rollenattribute, Mitgliedschaften, PUBLIC und Defaults. `RestoreDatabaseConnect.sql` ergänzt nur die nicht im Dump enthaltene DB-ACL. | `GateRejectsCatalogDrift`, `RestrictedRolesMigrateOperateAndRestoreWithoutGrantingDdlToRuntime`, `UpgradeClosesLegacyTableAndColumnGrants`. Das Gate läuft auch in einer Read-only-Transaktion. | Lokal verifiziert. D8 prüft das gleiche Gate auf dem separaten Zielserver und den Schlüssel-/Remote-Restore. |
| N-P1-02 | Kombinierter Sperr-/Statusausdruck verwendete nach dem Warten den früheren Snapshot. | `Foundation.sql`, `Files.sql`, `Documents.sql`, `Provisioning.sql`. Separate Anweisungen. VOLATILE-Prüfung liest danach Workspace und sperrt die Identity-Zeile. Die Workspace-Zeilenänderung erhält den Repeatable-Read-Konflikt. | `PreviousCombinedPredicateUsedThePreWaitWorkspaceSnapshot` reproduziert unter D2-05 einen Commit nach Suspendierung. `NewWorkRechecksStateAfterWorkspaceWait` beobachtet echte Sperren für Upload und Provisionierung. Suspendierung und Kontosperre werden unter beiden Isolationsstufen geprüft. | Lokal verifiziert. D4 erzwingt die gleiche Reihenfolge im autorisierten Deaktivierungsdienst. |
| N-P1-03 | Der bisherige Befehl kopierte den Inhalt, behielt aber die Pending-Originalseiten. | `command_assign_pending_transcript` übernimmt Dokument, Revision und Pending-Status atomar, löscht alle Originalseiten und räumt nicht benötigte Kopfmetadaten ab. Die Migration bereinigt bereits terminale Köpfe. Der Owner erhält dafür nur DELETE auf den Pending-Seiten und UPDATE auf den neu benötigten Kopfspalten. | `PendingConcurrentAssignmentKeepsOneCopyAndOneRevision` und `FailedPendingAssignmentRetainsOriginalPagesAndDocument` prüfen gültige und veraltete Revisionen, unzulässige Seiten, Konkurrenz, Teileffekte und vollständigen Rollback. Direkte Runtime-Löschung ist verboten. | Lokal verifiziert. D7 ergänzt Import, atomare Anwendung von Quellmetadaten und Verwerfen. Ein lokaler Verwerfen-Befehl bestand in D2 noch nicht. |
| N-P2-01 | Präfixcheck konnte zusätzliche Funktionen übersehen. Defaults waren nicht vollständig geprüft. | Exakte Signaturen und Hashes der ausgelieferten Funktionskörper im lesenden Gate. Globale und schemaspezifische Default-ACLs werden geprüft. | Manipulationen an zusätzlichen Funktionen, Owner, SECURITY, Pfad, Körper, internen EXECUTE-Grants, Rollen und beiden Default-ACL-Ebenen werden abgewiesen. | Lokal verifiziert. Sollinventar bei zukünftigen Migrationen bewusst aktualisieren. |
| N-P2-02 | Ungültige Eingaben konnten verschiedene Fehler liefern oder bis zur Mutation gelangen. | Alle Befehle prüfen benötigte IDs, Versionen und fachliche Argumente, bevor sie schreiben. Namen/Titel werden getrimmt und die gespeicherte Länge wird geprüft. `D2Commands` übersetzt `22023/ck_command_argument` in einen Argumentfehler. Zustandskonflikte und unerwartete Datenbankfehler behalten getrennte Verträge. | `EveryApprovedRuntimeSignatureRejectsMissingRequiredInput` prüft 38 Routinen. `RuntimeWorkspaceCommandsDistinguishArgumentsFromScopeAndVersionConflicts`, Lease-, Digest- und Pending-Tests prüfen Domänenfälle. | Lokal verifiziert. HTTP-ProblemDetails-Zuordnung gehört in die späteren API-Tickets. |
| N-P2-03 | Nur eine positive Lease-Dauer war vorgeschrieben. | `LeaseMaximumSeconds.txt` ist gemeinsame Quelle für C# und die erzeugte PostgreSQL-Funktion. Maximal 900 s. DB-Constraints sichern endliche Leases und die Obergrenze auch für Tabellenänderungen. Bestehende überlange Leases erfordern Klärung vor der Migration. | `InvalidLeaseArgumentsAreRejectedBeforeReservingAnyWork` prüft negatives/fehlendes Intervall, null Dauer, exakte Grenze, Überschreitung und Infinity. Runtime-Recovery-Tests verwenden regulären Ablauf auf der DB-Uhr. | Lokal verifiziert. D4/D5 müssen die externen Aufrufbudgets anpassen und Commit-/Recovery-Fälle mit HTTP-Testdoubles prüfen. |
| N-P2-04 | Abgeschlossener Upload war im Test zum Teil mit `pending_upload` verbunden. | Terminaler privilegierter Testseed erzeugt konsistente Dokument-/Operationsdaten. Die neuen Runtime-Lebenszyklen erzeugen Reservierung und Abschluss über die zugelassenen Befehle. Imports/Bootstraps ohne bestehenden D2-Erzeugungsbefehl werden als gültige Testvoraussetzung gesät. | `RuntimeRetirementPreservesARequestCreatedThroughTheCompleteUploadLifecycle`, Upload-Recovery und beide Purge-Szenarien prüfen persistierte Endzustände. | Lokal verifiziert. Privilegierte Seeds bleiben für Constraint-, Upgrade- und Störungstests. |
| N-P2-05 | Zuordnung von SQL zu C# belegte keine fachliche Runtime-Ausführung. | Neue Runtime-Abläufe für Einladungen, Registrierung, Recovery, Upload, Löschung, Retention und Mutationen. Die folgende Matrix erfasst alle 38 Signaturen. | Positive Endzustände, falsche Workspace-/Ressourcenzuordnung, alte Fences und direkte ungültige SQL-Aufrufe. | Lokal verifiziert. Kein externer HTTP-Aufruf ist Teil des Nachweises. |
| N-P2-06 | Privilegierte Tabellenpfade konnten Provisionierungsstatus und technische Identität frei ändern. | `guard_provisioning_transition` fixiert erlaubte Übergänge, Versuchszähler und beobachtete Kandidaten. `guard_prepared_technical_identity` bewahrt vorbereitete Credentials und die verifizierte ID. Recovery kann mit neuer Prüflease abschließen. Ein suspendierter Workspace bleibt suspendiert. | `RuntimeProvisioningRecoversWithSameIdentityAndRejectsOldFence`, Rollen-/Restore-Test und `ActiveWorkspaceRequiresAndRetainsVerifiedTechnicalIdentity`. | Lokal verifiziert. Eine administrative Rückkehr von unklar zu bereit bleibt bis zum belegbaren D4-Remote-Klärungspfad gesperrt. |
| N-P3-01 | SQL-Rückgaben waren unterschiedlich, obwohl die C#-Grenze bereits typisiert war. | Stabile Signaturen erhalten. Neue Pfade folgen der Konvention aus dem nächsten Abschnitt. Bestehende Ein-Zeilen-Befehle mit SQL-Resultat 0/1 bleiben kompatibel. | Vollständiger Backendlauf und Katalogprüfung der Rückgabetypen. | Lokal verifiziert. |
| N-P3-02 | EF-Modell-Drift erfasst die manuell verwalteten PostgreSQL-Objekte nicht. | Additive Migration mit eingefrorenen SQL-Ressourcen. Das Gate prüft Funktionen/Rückgabetypen/Körper, Eigentum, ACLs, Triggerkonfiguration, vorhandene und aktive manuelle Constraints sowie den zusätzlichen partial Unique Index. | Rollen-/Restore-Test, D2-05-Upgrade, gezieltes Verändern von Funktionen, Trigger, Constraint und Index, EF-Modell-Drift. | Lokal verifiziert. Dump-/Restore-Probe auf getrenntem Ziel bleibt D8. |

## Laufzeitvertrag und weitere Reviewhinweise

- Mutationen liefern typisierten Erfolg oder Konflikt. Bestehendes SQL 0/1 wird in den C#-Adaptern in Boolean oder einen Fence übersetzt. Unmittelbar benachbarte nächste Fence-Versionen sind durch das exakte SQL-Inkrement bestimmt. `command_claim_provisioning_verification` gibt die gespeicherte neue Version zurück. Reservierungen liefern die gebundene Remote-ID als Teil des C#-Ergebnisses. Retention liefert die Zahl tatsächlich entfernter Zeilen.
- Ein fehlgeschlagener oder unbekannter Commit liefert keine frei verwendbare Startberechtigung. Der Wrapper wiederholt Transaktionen nicht. D4/D5 müssen nach Abbruch beim Commit per zuvor festgelegter Operations-/Request-ID die persistierte Autorität klären und erst danach entscheiden, welche externe Aktion zulässig ist.
- Jede Prüfung eines Sperrrennens wartet auf die beobachtete PostgreSQL-Blockierung. Die Wiederholungsläufe prüfen ausgewählte Rennen mehrfach. Langfristige Flakiness oder Last auf dem Zielsystem folgen D8. Die Workspace-first-Kosten wurden hier nicht als Leistungstest bewertet.
- Das Gate pinnt die freigegebenen SQL-Funktionskörper auf SHA-256 der eingecheckten Definitionen, normalisiert nur Zeilenenden und vergleicht nie gegen einen zur Laufzeit erlernten Sollzustand. Bei einer späteren Migration werden neue Sollwerte aus den dazugehörigen versionierten Definitionen übernommen.
- Die Funktionsbesitzerrolle ist eine lokale Privilegiengrenze. Sie ersetzt keine Authentifizierung des aufrufenden Benutzers und keinen Nachweis externer Operationen. Doppelregistrierung, MIME-/Dateivertrag, API- und Betriebsgates bleiben wie im [Ticketkatalog](TICKETS.md) festgelegt.

## Runtime-Abdeckung aller 38 Fachbefehle

Alle Signaturen in dieser Tabelle haben zusätzlich den direkten Negativnachweis über `EveryApprovedRuntimeSignatureRejectsMissingRequiredInput`. Die folgende Zuordnung nennt die fachlichen Positivnachweise und ausgewählte zustandsbezogene Negativfälle. Die Namen sind ohne das Präfix `minidocs.command_` angegeben.

| Fachbefehl | Runtime-Test und beobachteter Zustand |
| --- | --- |
| `create_invitation` | `RuntimeInvitationsRequireAdministratorAndOnlyOneRegistrationCommits`: persistierte Einladung, unberechtigter Aussteller scheitert. |
| `revoke_invitation` | Derselbe Test: Widerruf, veraltete Version und schon eingelöster Datensatz. |
| `lock_valid_invitation` | Derselbe Test: gültiges Token und abgewiesenes widerrufenes Token. |
| `register_workspace` | Derselbe Test: nur ein Konto, ein Workspace und ein Systemknoten committen. |
| `prepare_provisioning` | `RuntimeProvisioningRecoversWithSameIdentityAndRejectsOldFence`: passende Credentials, doppelte Vorbereitung abgewiesen. |
| `begin_provisioning` | Derselbe Test und `NewWorkRechecksStateAfterWorkspaceWait`: erster Versuch mit Lease, Recovery und Sperre erlauben keinen zweiten Start. |
| `record_provisioning_candidate` | Provisionierungs-Recovery-Test: gebundener Kandidat, falscher Owner-Fence abgewiesen. |
| `recover_expired_provisioning` | Derselbe Test: DB-Zeit, Recovery-State und alte Version. |
| `claim_provisioning_verification` | Derselbe Test: Verifikationslease, fremder Workspace abgewiesen. |
| `complete_provisioning` | Derselbe Test und Restore: richtige ID, Einladung entfernt, falsche ID abgewiesen und keine Reaktivierung suspendierter Workspaces. |
| `replace_transcript` | `AlternativePageWritesAreDeniedWhileReplaceAllHoldsTheWorkspace` und `RuntimeWorkspaceCommandsDistinguishArgumentsFromScopeAndVersionConflicts`: Text und Revision, alter Replace-All abgewiesen. |
| `edit_transcript_page` | Workspace-Command-Test: Seitentext, Seitenversion, Dokumentrevision, alte Seitenversion abgewiesen. |
| `assign_pending_transcript` | Pending-Konkurrenz- und Rollback-Tests: eine gespeicherte Fassung, keine Originalseiten nach Erfolg. |
| `change_page_count` | `PendingAssignmentChecksBothRevisionsAndPageCount`: erfolgreiche Änderung, Reduktion unter vorhandene Seite abgewiesen. |
| `rename_document` | `TrackedUpdateCannotBypassVersionAndCommandRejectsStaleVersion` und Workspace-Command-Test: Titel/Version, alte Version und fremder Workspace abgewiesen. |
| `move_topic` | `RuntimeTopicCommandsRejectCyclesAndCannotDeleteNewAssignment`: Elternwechsel und abgewiesener Zyklus. |
| `delete_topic` | Workspace-Command-Test und Topic-Konkurrenztest: leeres Thema gelöscht, belegtes oder veraltetes Thema bleibt. |
| `move_document` | Topic-Konkurrenztest: Dokument bleibt im zugeordneten Thema und blockiert dessen Löschung. |
| `set_document_tag` | `TagAssignmentsAndDeletionInvalidateOnlyAffectedDocumentVersions`: Link, Versionspflege, No-op und alter ETag. |
| `delete_tag` | Derselbe Test: Links entfernt, ausschließlich betroffenes Dokument neu versioniert. |
| `reserve_upload` | `RuntimeUploadRecoveryKeepsRequestIdentityAndOnlyReclaimsVerification`, Snapshot- und Retention-Tests: Dokument/Operation, kein Doppel-Request. |
| `begin_upload` | Upload-Recovery- und Lease-Test: Start und Lease, kein Start nach Unknown. |
| `record_upload_remote` | Runtime-Purge- und Runtime-Retirement-Abläufe: echte Upload-Vorbereitung, alter Fence abgewiesen. |
| `record_upload_digest` | Upload-Recovery-Test: korrekter Digest bleibt gespeichert, alter Fence abgewiesen. |
| `reclaim_known_upload` | Upload-Recovery-Test: nur Verifikation kann übernommen werden, alte Version scheitert. |
| `recover_expired_upload` | Upload-Recovery-Test und Restore: persistierter Unknown-State, keine Wiederfreigabe für POST. |
| `reconcile_unknown_upload` | Upload-Recovery-Test: bestätigte Remote-ID, falsches Dokument scheitert. |
| `fail_unstarted_upload` | Upload-Recovery-Test: lokaler Fehler vor Start, korrekter Dokumentzustand, Unknown bleibt blockiert. |
| `complete_upload` | Upload-Recovery- und Runtime-Purge-Tests: verfügbar mit gebundener ID; falsche ID und erzwungener Fehler bleiben atomar. |
| `trash` | `RuntimePurgeAfterUnknownPreservesTombstoneAndEnforcesReceiptRetention`: gelöscht markiert, alter ETag scheitert. |
| `restore` | Derselbe Test: Papierkorbstatus wieder aufgehoben, falsche Version scheitert. |
| `begin_purge` | Derselbe Test: gebundene Remote-ID und offene Löschung, alter ETag scheitert. |
| `mark_delete_unknown` | Derselbe Test, Fall ohne Ablauf: spezifischer Fence, alter Fence scheitert, Verifikation möglich. |
| `recover_expired_delete` | Derselbe Test, Ablauf-Fall: reguläre Lease abgelaufen, alter Fence scheitert, keine nachträgliche direkte Löschbestätigung. |
| `begin_delete_verification` | Beide Purge-Fälle: neue Verifikationslease, alte Version scheitert. |
| `complete_purge` | Beide Purge-Fälle: Dokument weg, Receipt und Tombstone vorhanden. `RealDatabaseFailureMidCommandRollsBackAllWrites` prüft den Fehlerpfad. |
| `retire_completed_uploads` | `RuntimeRetirementPreservesARequestCreatedThroughTheCompleteUploadLifecycle` und `ReservationCannotRaceRetirementInEitherOrder`: Frist/Scope, dauerhafte Sperrmarke. |
| `remove_expired_receipts` | Beide Purge-Fälle: neuer Receipt bleibt, fälliger Receipt wird entfernt, Sperrmarke bleibt erhalten. |

## Rohprotokolle

**Ergebnis:** Die Baseline bestand mit 127 von 127 Tests. Locked Restore und Debug-Build waren erfolgreich. Der Standardlauf und der Lauf mit separatem Artifacts-Pfad bestanden mit jeweils 169 von 169 Tests. Fünf zusätzliche Wiederholungen der zehn Konkurrenzfälle bestanden ohne Fehler oder übersprungene Tests. EF meldete keine Modellabweichung. OpenAPI-Vergleich, TypeScript und der negative Contract-Drift-Test waren erfolgreich. Gemeinsame Verträge und Frontend blieben unverändert.

Die ungekürzten Konsolenausgaben mit Exitcodes liegen im von Git ignorierten Workspace-Verzeichnis `backend/obj/d2-06`. Die finalen Ausgaben sind getrennt von den Diagnoseprotokollen der Umsetzung abgelegt.

| Prüfung | Protokoll |
| --- | --- |
| D2-05-Baseline | `baseline.log` |
| Locked Restore und Debug-Build | `locked-restore.log`, `debug-build.log` |
| Vollständige Suite | `backend-run-1.log` |
| Locked Restore und Suite mit separatem Artifacts-Pfad | `separate-restore.log`, `backend-run-2.log` |
| Fünf Konkurrenzwiederholungen | `concurrency-1.log` bis `concurrency-5.log` |
| Modell und Verträge | `ef-drift.log`, `openapi.log`, `typescript.log`, `negative-contract-drift.log` |
| Git-Diff einschließlich unversionierter Dateien | `git-diff.log` |

D3–D8 und die fachlichen Zielsystemnachweise wurden nicht begonnen.

## Unabhängiger Endreview und technische Abnahme (26.11.2026)

Der dritte unabhängige Review des exportierten D2-06-Working-Tree (ABC-Export 26.11.2026, 17:32 Uhr, Git-Basis `48af1e3`) bestätigt die Gesamtnote **1− (sehr gut)**, Softwarearchitektur **1,0**, keine verbleibenden fachlichen D2-Abnahmeblocker und empfiehlt den Übergang zu D3 ohne weitere verpflichtende D2-Korrekturrunde. Das Gutachten berücksichtigt die bereitgestellten lokalen Verifikationsprotokolle; es ist keine eigenständige Produktions- oder Deploymentfreigabe. Zwei Hauptläufe mit je 169 bestandenen Tests (105 PostgreSQL-Integrationsfälle) und fünf zusätzliche erfolgreiche Wiederholungen von je zehn Konkurrenzfällen sind dokumentiert.

Die verbleibenden Hinweise aus dem Endgutachten sind keine D2-Abnahmeblocker:

| Hinweis | Behandlung |
| --- | --- |
| R-01: Kein vollständiger Ausdrucksvergleich aller Constraints und des gesamten Schemas | Normalisierten Schema-Vergleich bei D8 als ergänzende Betriebsprüfung bewerten. |
| R-02: Zusätzliche befüllte Upgrade-Fälle (terminale Pending-Seiten, überlange Alt-Lease) | Bei weiteren relevanten Migrationen berücksichtigen; kein neuer D2-Auftrag. |
| R-03: Isolierte konkurrierende Identity-Änderung | Mit dem tatsächlichen Deaktivierungsdienst in D4 prüfen. |
| R-04: Zusätzliche befehlsspezifische Negativfälle | Bei Umsetzung der jeweiligen Fachservices ergänzen; keine vollständige kombinatorische Matrix. |
| R-05: Pflege der Funktionshashes | Bei Bedarf deterministische Ableitung aus versionierten Sollquellen, niemals aus der zu prüfenden Ziel-Datenbank. |
| R-06: Formeller Git-Abschluss | Gemäß [DoD](DEFINITION-OF-DONE.md) durch gezielte Veröffentlichung und Abgleich mit origin/main nachweisen. |

Die technische Abnahme gilt ausschließlich für den geprüften Backend-Stand. Die verbindlichen Integrations-, Autorisierungs-, Wiederanlauf- und Betriebsgates aus D3–D8 bleiben unverändert bestehen. Der formelle Git-Abschluss ist gesondert nachzuweisen.
