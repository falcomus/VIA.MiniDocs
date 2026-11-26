# D3-A – Technischer Nachweis

## Baseline und Umfang

- MiniDocs-Branch `feature/d3-via-docs-client`, HEAD `164aad5d370105ea44bfb61f45579ea7a85a4f33`. Die drei bereits bearbeiteten D3-PLAN-Dateien wurden fortgeschrieben. Der Git-Index blieb leer.
- VIA.Docs-Referenz `feature/v1.2-s3-storage`, HEAD `7ffed6ab7a1125c6f14c5f664f12ffefbeaf1da8`. Anmeldung und JWT wurden am lokalen Quellstand geprüft. Die Einladungseinlösung stammt aus dem dortigen uncommitteten Arbeitsbaum. VIA.Docs wurde ausschließlich gelesen.
- D3-A ergänzt Client, DI und Tests. Domain, D2-Persistenz, Datenbankrollen, Migrationen, Frontend und öffentliche HTTP-Verträge sind unverändert. Die D3-B-Dateioperationen und der geschützte D4-Credential-Provider gehören zu späteren Aufträgen.

## Architektur und Betrieb

`IViaDocsAuthentication` bietet gebundenen Login, ungebundene Login-basierte Wiederauffindung und technische Einladungseinlösung. `IViaDocsCredentialProvider` liefert die jeweils aktuelle autorisierte Credential-Generation. Die fachliche Grenze liefert typisierte Identitäten, Kandidaten und Fehler. JWT und Wire-DTOs verbleiben in Infrastructure. Ohne Credential-Provider oder ohne aktivierte Integration erfolgt kein Versand.

Der Authentifizierungsdienst ist scoped. Transport und Token-Cache sind pro Prozess Singleton. Der Auth-Handler und der Lesehandler haben getrennte HttpClientFactory-Pools. Für Auth-Requests gilt HTTP/1.1 mit Verbindungsabschluss und einmal serialisierbarem Body. Weiterleitungen, Cookies und impliziter Proxy sind deaktiviert. Standard-TLS und Hostnamenprüfung bleiben aktiv. Kein Admin-Registrierungsschlüssel und kein alternativer Registrierungsendpunkt.

Die geprüfte HTTPS-Basisadresse bleibt bei Prozesskonfiguration fix. Die Auth-Routen sind lokal definierte Pfade. Das Connect-Limit beträgt 5 s, die Deadline kurzer Aufrufe 15 s einschließlich Credential-Auflösung, Wartezeit und Antwortverarbeitung. Token-Erneuerung beginnt 60 s vor Ablauf. Auth-JSON wird auf 32 KiB begrenzt. Fehlerbodies werden verworfen. Die zentral validierten Datei-, Idle- und Mediengrenzen gelten bei Umsetzung der jeweiligen Dateipfade. Die zugehörige Runtime-Konfiguration steht im [Clientvertrag](VIA-DOCS-API.md#d3-plan--freigegebener-clientvertrag-für-d3-a-und-planung-für-d3-b).

## Fehler- und Wiederholungsvertrag

| Fall | Clientverhalten |
| --- | --- |
| Ungültige Konfiguration oder nachweisliche lokale Ablehnung | Typisierter Fehler ohne HTTP-Status und ohne Versand |
| 400/401/409/429/503 und weitere Fehlerstatus nach Einlösungsdispatch | `UnknownOutcome` mit originalem HTTP-Status und typisierter Ursache. Der lokale Endpoint liefert bestimmte Ablehnungen vor Commit. Status, JSON und MIME beweisen jedoch keinen Antwortursprung über den realen Proxy. |
| 404/405 bei Einlösung | Ergebnis möglicherweise ausgeführt. Sperrt weitere Einlösungen im Prozess. |
| Sonstige unerwartete Statuswerte, unterbrochene Antwort, Timeout oder Cancellation nach Einlösungsdispatch | `UnknownOutcome` mit typisierter Ursache und verfügbarem Status. Kein zweiter POST. |
| Plausible Einlösungsidentität mit ungültigem JWT | `UnknownOutcome` mit Kandidat. Separater Login mit vorbereiteten Credentials ist erforderlich. |
| Login | Kein automatischer Retry. Ein fehlgeschlagener Recovery-Login erlaubt keine Aussage über externe Registrierung. |
| GET/HEAD-Transportgrundlage | Ein gemeinsamer Zusatzversuch für 401-Erneuerung oder ausgewählte transiente Fehler vor Body-Freigabe. Spätes 401 invalidiert nur das abgelehnte Token. |
| 429/503 | Kein Last-Retry. Validierter optionaler `Retry-After` wird maximal bis 300 s weitergegeben. |

## Nachweismatrix

| Kriterium | Nachweis |
| --- | --- |
| URI und Konfigurationsgrenzen | Negativfälle für Scheme, Userinfo, Query, Fragment, Pfadtraversierung, Encoding und mehrdeutige Segmente. Präfix und feste Route am aufzeichnenden Handler geprüft. |
| Identität und JWT | ID, normalisierter Name, JWT-Form, `sub`, `exp`, Sekundenauflösung, abgelaufene und widersprüchliche Antworten. Claims dienen der Plausibilität innerhalb der HTTPS-Antwort. VIA.Docs validiert Tokens bei Ressourcenzugriffen. |
| Cache und Nebenläufigkeit | Maximal 256 Cache-Einträge und 16 parallele Auth-Aufrufe standardmäßig. Schlüssel umfasst Origin/Root, Workspace, Application-ID und Namen. Der Slot bindet Credential-Referenz und Generation. Abgelaufene Slots werden beim Zugriff bereinigt. Gemeinsamer Login, Abbruch eines Wartenden, Generationwechsel mit verspäteter Antwort, A/B-Bearer und gezielte 401-Invalidierung geprüft. |
| Einladung und Antwortverlust | Echter lokaler TLS-SocketsHttpHandler. Bei nach Empfang abgebrochener Antwort genau ein serverseitig beobachteter POST, danach separater Recovery-Login. Im Redirectfall blieb der zweite HTTPS-Origin auf einem anderen Port bei null Requests. Der Loopback-Test verwendet ausschließlich ein testseitig gepinntes Zertifikat. |
| Redaktion | Keine HttpClientFactory-Logger für beide Clients. Synthetische Secret-, Einladungs- und JWT-Sentinels über erfasste Logs und `System.Net.Http`-Activity-Tags geprüft. Private DTO-Darstellungen und externe Fehlertexte werden nicht weitergegeben. |
| D2 | Bestehende PostgreSQL-Fixtures verwenden lokale isolierte, eindeutig benannte Container und eigene Datenbanken. Das gepinnte Image war vor Testbeginn lokal vorhanden. |

## Befehle und Ergebnisse

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false` | 0 | Standard-Locked-Restore mit aktivem NuGet-Audit erfolgreich. |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -m:1 -nr:false` | 0 | Debug-Build ohne Warnungen. |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~ViaDocsAuthenticationTests` | 0 | 62 bestanden, einschließlich zweier realer HTTPS-Loopback-Fälle. |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false` | 0 | 231 bestanden einschließlich PostgreSQL, 0 übersprungen. |
| `npm run check` · `npm run typecheck` · `npm run test:drift` im Backend | jeweils 0 | OpenAPI stimmt mit TypeScript-Vertrag überein. TypeScript kompiliert. Der Negativtest weist einen veralteten Vertrag zurück. |
| `git diff --check` | 0 | Keine Whitespace-Verstöße. |
| Lokaler Secret-Musterscan und Sichtprüfung | geprüft | Treffer auf das ausdrücklich synthetische Test-Secret begrenzt. |

## Prüfgrenzen

Der Loopback-Nachweis gilt für die getesteten HTTP/1.1-TLS-Fälle. Reale Proxy-Strecken, Antwortursprung, Deployment und fachlich transaktionaler Wiederanlauf mit D4 bleiben gesonderte Abnahmen. Die D4-Credential-Quelle muss Workspace, Namen und Referenz autorisieren, Generationswechsel monoton führen, ältere Generationen ausschließen und das übergebene CancellationToken beachten. Der technische Nachweis dient der unabhängigen Teilprüfung.

## Befundmatrix zum Korrekturpaket

| Befund | Ursache und Änderung | Regression |
| --- | --- | --- |
| A-01 | Der HTTP-Status identifiziert den Antwortursprung nicht. Jede unerwartete Antwort nach Einlösungsdispatch bleibt ungeklärt. D4 bewahrt den Vorgang und klärt ihn über den bestehenden Verifikationspfad. D8 prüft den realen Antwortursprung. | 400/401/409/429/503, JSON, Proxy-HTML und Abbruch nach Empfang. Genau ein Einlösungs-POST. |
| A-02 | Erwartete Quellenfehler konnten die Application-Grenze passieren. Der D4-Provider meldet sie über `ViaDocsCredentialUnavailableException`. Der Client hält Quellenstörung, ungültige Credentials, Timeout und Cancellation auseinander. Unerwartete Fehler im gemeinsamen Login geben den Slot frei und beenden die Task mit einem redigierten Fehler. | Quellenstörung für Login, Find und Redeem, kein Versand, redigierte synthetische Provider- und Transportfehler |
| A-03 | Der Cache prüfte die Referenz bisher nicht und ungebundene Vorgänge hatten keine Nachprüfung. Slots binden Referenz und Generation. Nachprüfung nutzt einen eigenen Scope. Bei möglicherweise ausgeführter Einladung bleibt eine belastbare ID erhalten. | Referenzwechsel bei vollem Cache, Ablauf und Wiederbelegung, rückläufige Generation, Find/Redeem mit Credential-Wechsel, gleiche Application-ID in getrennten Workspaces, Scope-Dispose bei Single Flight |
| A-04 | Das bisherige Redirectziel verwendete denselben Port. Der TLS-Loopback prüft zwei Origins mit eigener Requestzählung am zweiten Port. Zertifikatspinning gilt ausschließlich im Test. | Redirect 307 und verlorenes ACK, typisierte Ursachen, ein POST am vorgesehenen Ziel und null Requests am zweiten Origin |
| A-05 | Fehlende Grenzfälle sind mit gezielten Tests abgedeckt. Die Uhr für den Erneuerungsvorlauf wird deterministisch fortgeschaltet. | Host-Startvalidierung, fehlender Provider, Retry-After-Varianten, JSON-Erweiterung und Duplikat, Kapazitätsgrenzen und redigierte Fehler |
| A-06 | Die Projekttexte benennen den implementierten Authentifizierungsvertrag, markieren die Dateioperationen als D3-B-Planung und beschreiben D4-/D8-Abnahmen. | Querverweise, Git-Diff und fachlicher Dokumentationsabgleich |
