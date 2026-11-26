# D3-C – Technischer Gesamtnachweis

## Ausgangsstand

MiniDocs: feature/d3-via-docs-client, HEAD 164aad5d370105ea44bfb61f45579ea7a85a4f33, leerer Index. Die Arbeitsbaumänderungen aus D3-PLAN, D3-A, D3-A-FIX und D3-B sind die Ausgangsbasis. D3-A wurde laut Auftrag unabhängig mit Note 1− geprüft. D3-C verifizierte den technischen Arbeitsstand. D3 ist laut unabhängigem Abschlussreview mit Gesamtnote 1− technisch abgenommen. Download, Streaming und Testqualität erhielten jeweils Note 1−. A-01 bis A-06 sowie B-01 bis B-04 sind geschlossen. D3-INTEGRATION, Betriebsfreigabe und formeller Git-Abschluss stehen aus.

VIA.Docs wurde ausschließlich lesend geprüft. Der bei D3-C geprüfte lokale Branch feature/v1.2-s3-storage stand auf 95e4b23430f7b7c624c2184eb52ebd7dd70cd44f. D3-FINAL bestätigt ac991d9b58c4abd71b40a29e536a40e46f086af3 als aktuellen lokalen Referenzstand. Der Commit 8489ba8 enthält den Einladungsvertrag. 95e4b23 enthält beide PageCount-Korrekturen. Der Vergleich mit 7ffed6a bestätigt die Änderungen in Application-, File- und Document-Endpoints. Die öffentliche Deployment-Version bleibt unbestätigt.

## Reviewbefunde

| Befund | Ursache und Behandlung | Lokaler Nachweis |
| --- | --- | --- |
| B-01 | Unbekannte Inhaltslänge ließ EOF vor dem ersten PNG-Byte zu. Gelesene, leere Bodies werden als Contract klassifiziert. Content-Length 0 wird weiterhin direkt abgewiesen. | PNG mit bekannter und unbekannter Länge, gültiger nichtleerer Body, Limit ohne Content-Length, Ressourcenfreigabe. SVG bleibt ein typisiertes Fallback-Signal ohne Body. |
| B-02 | Freigabefehler konnten die Lesefehlerdiagnose verdrängen. Disposal konnte durch nachträgliche Cancellation einen Transportfehler als Timeout erscheinen lassen. Die Lesekategorie wird vor Cleanup bestimmt. Erwartbare Cleanup-Fehler bleiben redigiert. Der Deadline-Callback speichert Freigabefehler für den Besitzer. Ein unerwarteter Callback-Fehler bleibt als Programmfehler sichtbar. | I/O- und EOF-Fehler mit fehlschlagender Freigabe, explizites und mehrfaches Dispose, Deadline bei ungelesenen und aktiven Bodies sowie Cancellation während aktiver I/O. |
| B-03 | Kleine Teststreams belegten die Abschnittsgrenze nicht. Ein nicht seekbarer Originalstream von über 192 KiB wird schrittweise gelesen. | Null gelesene Bytes vor Body-Freigabe, mehrere tatsächliche Lesevorgänge, 64 KiB Maximum trotz größerem Aufruferpuffer, exakte Bytezahl, frühes EOF, überzählige Bytes, Cancellation nach erstem Abschnitt, genau ein GET. Uploadgrenztests bleiben erhalten. |
| B-04 | Es fehlte ein gemeinsamer Nachweis der Dateioperationen an der Mandantengrenze. Der Test erzwingt parallel laufende Operationen zweier Workspaces und Anwendungen. | Liste und Metadaten, Upload und DELETE, exakter Bearer pro Ziel, Credential-Wechsel, verspätete Write-401, Cache-Einträge beider Identitäten, keine Write-Wiederholung, lokale Ablehnung fremder Validatoren. |

## D3-Abnahmematrix

| Ticket | Vertrags- und Testnachweis | Abnahmegrenze |
| --- | --- | --- |
| D3-01 | Fail-closed Hostvalidierung, fester HTTPS-Origin, Präfix, URI-Kodierung, deaktivierte Redirects, kontrollierte Fristen, typisierte Fehler, redigiertes Logging. | Lokale Handler und echter TLS-Transport. Realer Proxy separat. |
| D3-02 | Gebundener Login, Credential-Fehlervertrag, Single Flight, Tokenablauf, Cache-Isolation, verspätete 401 und Einladung mit einmaligem beobachtetem POST. | D4 stellt autorisierte Credential-Quelle, monotonen Generationswechsel, Persistenz und Recovery bereit. |
| D3-03 | PDF-Cursor, Liste, Einzelmetadaten, GET/HEAD, Range, Validatoren, Preview, Body-Ownership und Timeouts. Duplikate einschließlich Schreibvarianten sicherheitsrelevanter JSON-Namen werden abgelehnt. Zusatzfelder werden toleriert. Gemischte Listen scheitern vollständig. | D5 autorisiert die Identität, verantwortet If-Range am Browserpfad und den PDF-Nachweis. |
| D3-04 | Einmal lesbarer Multipart-Upload, 25-MiB-Grenze, belastbare ID und Bytezahl, Location-Prüfung, konservative Fehlerklassifikation und qualifiziertes logisches DELETE. | D5 gleicht Größe und Digest anhand separater Metadaten ab. Physische Löschung und Remote-Idempotenz sind unbelegt. |
| D3-05 | Locked Restore, Debug und Release, Client- und PostgreSQL-Regression, gezielte Sicherheitswiederholung, OpenAPI/TypeScript/Drift, Diff und Secret-Prüfung. | Die unabhängige technische Gesamtabnahme ist erteilt. Die ausdrücklich freizugebende Zielsystemprüfung steht aus. |

### Transport und Ressourcen

Der lokale HTTPS-Loopback verwendet den tatsächlichen SocketsHttpHandler. Bekannte Uploadlänge wurde mit Content-Length übertragen. Unbekannte Länge wurde im HTTP/1.1-Test ohne Content-Length übertragen. In beiden Fällen akzeptierte der Server je einen Upload und der Client las den nachfolgenden GET-Body vollständig. Das gepinnte Zertifikat ist eine reine Testkonfiguration. Die vorhandenen Antwortverlust- und Cross-Origin-Tests bleiben erhalten. Sie beobachten einen Write am Ziel und null Requests am fremden Origin. Ein logischer Clientversuch ist kein allgemeiner Nachweis gegen Wiederholungen durch eine reale Proxy-Kette.

Die Dateiverträge behalten ein gemeinsames Budget für maximal einen zusätzlichen Leseversuch, auch bei kombiniertem 401 und transientem Fehler. Body-Freigabe beendet die Wiederholungsmöglichkeit. Write-401 berührt nur den abgelehnten Token und löst keinen erneuten Write aus. Es gibt keinen Last-Retry. Die Zeitbudgets umfassen Credential-Auflösung, Authentifizierung, Header und Body. Der Client bricht kooperativ ab und schließt die Response bei Ablauf. Er startet keinen unabhängig weiterlaufenden Read. D5 muss einen asynchronen, kooperativ abbrechbaren Upload-Stream mit gültiger Lebensdauer bereitstellen. Nicht kooperatives I/O lässt sich durch verwaltete Tokens nicht erzwingen.

## VIA.Docs-Vertragsmatrix

| VD | Abschlussabgleich |
| --- | --- |
| 01 | Die kanonische Upload-Zuordnung verwendet in 95e4b23 das benannte PageCount-Feld. MiniDocs verwendet RawWidth nie als Seitenzahl. |
| 02 | Die kanonischen Einzelmetadaten befüllen PageCount seit 95e4b23. Das Feld bleibt auf Clientseite optional. |
| 03 | FileUploadResponse hat keinen verpflichtenden SHA-256. Abgleich über autorisierte Einzelmetadaten ist ein D5-Schritt. |
| 04 | Ready kann beim Preview-Cache-Miss ein SVG liefern. Der Client klassifiziert den tatsächlichen MIME-Typ und unterdrückt den SVG-Body. |
| 05 | Der versionierte Einladungsendpoint kann 503 mit generischem Fehlercode liefern. HTTP-Status und konservative Dispatch-Semantik bleiben maßgeblich. |
| 06 | Retry-After ist optional. Eine Garantie für 429 ergibt sich aus dem geprüften Rate-Limit-Pfad nicht. Der Client prüft Wert und Mehrdeutigkeit. |
| 07 | Kanonischer Download unterstützt GET/HEAD, ausgewählte ETags und Single-Range. If-Range und If-Modified-Since werden nicht ausgewertet. Proxy- und Viewerprüfung bleiben separat. |
| 08 | Eine allgemeine Remote-Idempotenz ist aus den kanonischen Schreibverträgen nicht ableitbar. Einmaligkeit wurde für die lokalen Antwortverlustszenarien beobachtet. |

## SEC-01 bis SEC-15

| SEC | Lokal vorliegende Evidenz | Offener Nachweis |
| --- | --- | --- |
| 01 | Authentifizierte Dateioperationen und Identitätsbindung, Loopback mit gültiger eigener Datei | D4/D5 Workspace-Autorisierung und reales VIA.Docs |
| 02 | Getrennte Bearer, fremde Validatoren lokal abgewiesen | D4/D5 fachlicher Fremdzugriff und VIA.Server |
| 03 | Getrennte technische Identitäten beim Upload | D4/D5 fachlicher Fremdzugriff und Mutation im Zielsystem |
| 04 | Gebundene DELETE-ID, qualifiziertes 204/404 und parallele Identitäten | D4/D5/Betrieb, fremde Datei am realen Dienst |
| 05 | D2-PostgreSQL-Fremdschlüsseltest in Backend-Regression | Fachliche API-Grenze der Themenzuordnung |
| 06 | D2-PostgreSQL-Fremdschlüsseltest in Backend-Regression | Fachliche API-Grenze der Tagzuordnung |
| 07 | D2-Isolation im Persistenzmodell | Fachlicher Transkriptzugriff in den Folgetickets |
| 08 | D3-Client hat keinen öffentlichen Administratorpfad | D4/D5 Administratorrechte und Workspace-Zugriff |
| 09 | Technischer JWT-Fehler und Invalidierung getestet | D4 Kontosperre und bestehende MiniDocs-Sitzungen |
| 10 | Keine Client-Wiederholung nach Einladungsdispatch, getesteter Antwortverlust | D4-Transaktions- und Konkurrenznachweis plus reale Einlösung |
| 11 | Synthetische Sentinels in HTTP-Logs, Activities, Provider-, Transport- und Freigabefehlern | D4/D5 API-Redaktion und D8 Betriebsdiagnostik |
| 12 | D3 hält JWTs nur im Prozess | D4/D8 geschützter Bestand und Neustart |
| 13 | Technischer Generationswechsel im Cache getestet | D4/D8 nachweisbare Rotation und Zugriff auf Altbestand |
| 14 | Isolierte D2-Regression für Backup/Restore | D8 systemübergreifender Restore samt Key Ring und VIA.Docs-Identität |
| 15 | Parallele Dateioperationen mit exakt getrennten Tokens und spätem 401 | Reale isolierte Integrationsidentitäten und D4-Credential-Quelle |

## D2-Kompatibilität

D3 führt keine fachlichen Datenbankoperationen aus. D4/D5 müssen den Dispatch-Commit belegen, bevor Netzwerk-I/O beginnt. Die bestehende Lease und der aktuelle Fence autorisieren ausschließlich den vorgesehenen Schritt. Unbekannte Einlösung geht in Login-basierte Verifikation. Der Uploadkandidat wird früh gesichert, vollständiger Digest und Bytezahl getrennt gespeichert und gegen dieselbe Remote-ID unter derselben Identität geprüft. Hash oder Dateiname allein liefern keine Korrelation. Unbekannte Writes werden nicht wiederholt. Ein qualifiziertes DELETE-204 erlaubt den lokalen Abschluss mit Purge-Receipt. 404 oder ein unsicherer DELETE benötigen den qualifizierten Abwesenheitsnachweis mit bestehender Verifikations-Lease. Domain, Migrationen, Rollen und Persistenzkommandos blieben unverändert.

## VIA.Server – Testplan für einen gesonderten Auftrag

1. Betreiber bestätigt Build, Einladungsmigration, HTTPS-Origin und Proxyregeln. Zwei isolierte technische Identitäten und einmalige Einladungen über geschützten Kanal bereitstellen. Testressourcen und Bereinigung freigeben. Zugangsdaten bleiben außerhalb von Aufzeichnung und Git.
2. Beide Identitäten einloggen und Application-ID/Name prüfen. Falls Einladungen im Umfang liegen, Einlösung kontrolliert genau einmal je Vorgang auslösen und separat verifizieren.
3. Eindeutig markierte PDFs mit bekannten Größen und lokalem SHA-256 erstellen. Je Identität uploaden, Kandidaten-ID dokumentieren, separat gelesene Einzelmetadaten mit Größe und Digest abgleichen. Listenseiten und Einzelmetadaten vergleichen. Exakt 25 MiB und 25 MiB plus ein Byte nach vereinbartem Verfahren prüfen.
4. Original vollständig und per HEAD abrufen. Geschlossenen, offenen und Suffix-Range prüfen. Bytes, ETag, Content-Length, Content-Range sowie 304 und 416 protokollieren. Browserseitiges If-Range und Proxy-Pufferung gesondert mit D5/D8 prüfen.
5. Dokumentvorschauen mit tatsächlichem PNG und kontrolliertem SVG-Fallback beobachten. Renderstatus und MIME getrennt protokollieren. Preview-Größe und Timeouts prüfen.
6. Beide Richtungen der Mandantentrennung mit den jeweils fremden Test-IDs für Lesen, Vorschau und genehmigtes DELETE prüfen. Danach den eigenen logischen DELETE belegen, Nicht-Sichtbarkeit nachprüfen und ausschließlich die erzeugten Testdaten bereinigen.
7. Antwortursprung, Redirect-Verhalten, Header, Requestzählung und Ressourcenmesswerte an der freigegebenen Proxy-Strecke prüfen. Unklare Writes im Testprotokoll festhalten und fachlich klären. Prozess-, Schlüssel-, Backup- und Restore-Nachweise gehören zu D8.

## Prüfbefehle und Ergebnisse

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false | 0 | Standard-Locked-Restore mit aktivem NuGet-Audit |
| dotnet build backend/VIA.MiniDocs.sln --no-restore -m:1 -nr:false | 0 | Debug, 0 Warnungen, 0 Fehler |
| dotnet build backend/VIA.MiniDocs.sln -c Release --no-restore -m:1 -nr:false | 0 | Release, 0 Warnungen, 0 Fehler |
| dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false mit Clientfilter | 0 | 137 bestanden, davon 62 Authentifizierungs- und 75 Dateioperationstests, 0 übersprungen |
| dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false | 0 | 306 bestanden einschließlich isoliertem PostgreSQL, 0 übersprungen |
| Gleicher Testbefehl mit Sicherheitsfilter | 0 | 18 bestanden, 0 übersprungen |
| npm run check, npm run typecheck, npm run test:drift im Backend | jeweils 0 | OpenAPI, TypeScript und negativer Drift-Test erfolgreich |
| git diff --check, neue Dateien und Secret-Musterscan | 0 | Keine Whitespace-Verstöße und keine produktiven Secret-Treffer |

Die beiden Mit-Filter-Aufrufe lauteten in PowerShell:

```powershell
dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~ViaDocsFilesTests|FullyQualifiedName~ViaDocsAuthenticationTests'
dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~ParallelFileOperationsIsolateTenantsAndLateWrite401|FullyQualifiedName~RealHttpsTransport|FullyQualifiedName~OperationalCleanupFailure|FullyQualifiedName~ReadRetryBudgetIsShared'
```

Eine zusätzliche Frontendregression war an gemeinsame Vertragsänderungen geknüpft. Die OpenAPI-Vertragsprüfung bestätigt den unveränderten öffentlichen Vertrag. Der Release-Build aus dem Arbeitsbaum ist eine Compilerprüfung. Das formelle Release-Artefakt wird gemäß Projektregeln später aus dem freigegebenen Commit gebaut.

## Selbstreview

Die wichtigsten Risiken liegen nach dem lokalen Nachweis an den Systemgrenzen. Die Teststrecke belegt keinen veröffentlichten VIA.Docs-Build, keine physische Löschung und keine Proxy-übergreifende Exactly-once-Garantie. Die strikte Ablehnung gemischter Seiten ist beabsichtigt und setzt bei D4/D5 passende technische Test- und Produktidentitäten voraus. D5 muss Uploadquellen kooperativ abbrechen können. Die unabhängige Gesamtabnahme ist erteilt. Für den formellen Git-Abschluss ist eine ausdrückliche Freigabe gemäß Definition of Done erforderlich.

## D3-FINAL – Prüfprotokoll und vorgesehener Commit

Der D3-FINAL-Auftrag ist dokumentarisch. Die bestehenden D3-C-Ergebnisse mit 306 Backendtests und 137 Clienttests bleiben gültig. Eine erneute Testausführung und Serverzugriffe fanden nicht statt.

| Prüfung in D3-FINAL | Exitcode | Ergebnis |
| --- | --- | --- |
| Repository, Branch, HEAD, Index und Working Tree | 0 | Erwartete Baseline und leerer Index |
| Lesender VIA.Docs-Abgleich | 0 | Referenz-HEAD, nullable PageCount, Einladungsrouten und Migration bestätigt |
| SHA-256-Vergleich aller 15 Backend-Dateien im D3-Umfang | 0 | Bytegenau unverändert seit Beginn von D3-FINAL |
| Git-Diff, Whitespace neuer Dateien und Secret-/Artefaktprüfung | 0 | Nur vorgesehene Quell-, Test- und Dokumentdateien. Keine Secrets oder unerwünschten Artefakte im vorgesehenen Commit. |
| Dokumentationsabgleich und Umfangsprüfung | 0 | Status, Referenz, D3-INTEGRATION und D4-PLAN konsistent. 21 Dateien vorgesehen. |

Der folgende Umfang ist zur gesonderten Commit-, Merge- und Push-Freigabe vorbereitet. Die Liste enthält ausschließlich Pfade relativ zum Repository-Root.

- `backend/src/VIA.MiniDocs.Api/Program.cs`
- `backend/src/VIA.MiniDocs.Application/ViaDocsAuthentication.cs`
- `backend/src/VIA.MiniDocs.Application/ViaDocsFiles.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/VIA.MiniDocs.Infrastructure.csproj`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsAuthentication.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsAuthTransport.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsFileDownloads.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsFiles.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsFileStreams.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsFileWrites.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsOptions.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsRegistration.cs`
- `backend/src/VIA.MiniDocs.Infrastructure/ViaDocs/ViaDocsTokenCache.cs`
- `backend/tests/VIA.MiniDocs.Tests/ViaDocsAuthenticationTests.cs`
- `backend/tests/VIA.MiniDocs.Tests/ViaDocsFilesTests.cs`
- `docs/D3-A-VERIFICATION.md`
- `docs/D3-B-VERIFICATION.md`
- `docs/D3-C-VERIFICATION.md`
- `docs/MASTERPLAN.md`
- `docs/TICKETS.md`
- `docs/VIA-DOCS-API.md`
