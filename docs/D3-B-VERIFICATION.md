# D3-B – Technischer Nachweis

## Ausgangsstand und Grenzen

MiniDocs auf `feature/d3-via-docs-client`, HEAD `164aad5d370105ea44bfb61f45579ea7a85a4f33`. Der Index war leer. Die vorhandenen Änderungen aus Planung, D3-A und D3-A-FIX wurden übernommen. D3-A wurde laut unabhängiger Prüfung mit Note 1− zur technischen Teilabnahme empfohlen. D3-B implementiert D3-03 und D3-04 über `IViaDocsFiles`. D3-C, D4, D5 und die reale Integration bleiben separat.

Das VIA.Docs-Referenzrepository wurde auf `feature/v1.2-s3-storage`, HEAD `7ffed6ab7a1125c6f14c5f664f12ffefbeaf1da8`, ausschließlich gelesen. Quellbelege sind die kanonischen Datei-, Dokument- und Vorschaupfade, die File-DTOs und die relevanten Testimplementierungen. Der lokale Arbeitsstand ist vom referenzierten Commit und vom Deployment zu unterscheiden.

Die nachfolgende Verifikation dokumentiert den D3-B-Stand. Für leere PNG-Bodies, Fehler bei der Ressourcenfreigabe, große nicht seekbare Streams, Mandantentrennung und erfolgreiche reale Loopback-Übertragungen gilt der ergänzende [D3-C-Nachweis](D3-C-VERIFICATION.md).

## Vertrag, Ressourcen und Fehler

| Vorgang | Nachgewiesenes Clientverhalten |
| --- | --- |
| Liste und Metadaten | `kind=document`, Seitengröße 50/100, opaker Cursor bis 128 Zeichen, separate Einzel-ID-Prüfung, SHA-256, Medien- und Zeitfelder, kontrollierte JSON-Größe und optionale Seitenzahl. Leere Seite behält einen Folgezursor. |
| Original und HEAD | 200 vollständig, 206 nur mit konsistentem Single-Range, 304 mit passendem Kontext und Validator, 416 mit konsistenter Gesamtlänge. HEAD gibt keinen Body frei. |
| Preview | PNG als gerenderte Vorschau, SVG ausschließlich als Fallback-Signal ohne SVG-Body, 4 MiB Grenze. Der Renderstatus bleibt Teil der separaten Metadaten. |
| Streaming | Response, Stream und Gesamtdauer gehören zum asynchron freizugebenden Ergebnis. Leseabschnitte sind auf 64 KiB begrenzt. Early EOF, Disposal ohne Lesen, Fristablauf, Idle und Cancellation sind geprüft. Nach Body-Freigabe findet kein Retry statt. |
| Upload | Genau ein PDF-Multipart-Feld, einmal lesbarer Stream, 32 KiB Dateipuffer, 25 MiB Nutzlastgrenze. Bekannte Übergröße wird lokal abgewiesen. Ein vollständiger Multipart-Transfer und ein konsistentes 201 liefern Kandidat plus gesendete Bytezahl. Frühantwort oder unvollständiges Ergebnis bleibt ungeklärt. Eine widerspruchsfreie ID kann als Kandidat erhalten bleiben. Location wird verglichen und nie gefolgt. |
| DELETE | Qualifiziertes 204 bestätigt den logischen Löschabschluss. 404 meldet fehlende Sichtbarkeit oder fehlende Datei. Beide Fälle lassen physische Löschung offen. Sonstige ungesicherte Antworten, Dispatch-Timeout oder Verbindungsabbruch bleiben ungeklärt. |
| Authentifizierung und Retry | Der D3-A-Cache liefert mandantengebundene JWTs. Ein gemeinsames Budget erlaubt höchstens einen zusätzlichen Leseversuch für 401 bzw. ausgewählte transiente Fehler. Write-401 invalidiert das abgelehnte JWT nur für Folgeoperationen. Proxyfehler oder Antwortverlust lösen keinen erneuten Write aus. |
| Diagnose | Factory-Logging und HTTP-Aktivitätsintegration sind für die Datei-Handler deaktiviert. Erfasste Logs und System.Net.Http-Activity-Tags mit synthetischen Dateinamen, IDs, Workspace, Cursor, Secret und JWT blieben frei von diesen Werten. |

Die Dauergrenzen sind 5 s Connect, 15 s kurze Aufrufe bzw. Download bis Body-Freigabe, Download 30 s Idle und 120 s insgesamt, Upload 60 s Idle und 300 s insgesamt. Fehlerbodies werden verworfen. Datei-JSON ist auf 1 MiB begrenzt. D5 besitzt die Quelle des Upload-Streams und muss gemessenen Digest und Bytezahl im selben Durchlauf bestimmen.

## HTTP-Transportnachweis

Getestet mit tatsächlichem SocketsHttpHandler und lokalem Kestrel über HTTP/1.1 und TLS. Das nur im Test gepinnte Zertifikat gilt für zwei verschiedene Origins auf unterschiedlichen Ports.

- Nach angenommenem Upload und verlorenem ACK zählte der Server genau einen POST.
- Nach angenommenem DELETE und verlorenem ACK zählte der Server genau einen DELETE.
- Bei Cross-Origin-307 für Upload, DELETE und GET zählte der vorgesehene Origin genau einen Datei-Request. Der zweite Origin zählte null Requests.
- Reale Proxy-Replays, Remote-Idempotenz, End-to-End-Pufferung und Providerkosten sind durch diese Fälle nicht nachgewiesen. Der Deploy-Stand bleibt ein gesonderter Testauftrag.

## D2-Übergabematrix

| D3-B-Ergebnis | Zuständigkeit von D5 über bestehende D2-Kommandos |
| --- | --- |
| Vor Versand geprüfter lokaler Fehler | Dispatch und Fehlerbehandlung anhand des committeten Vorgangs und aktiven Fence beurteilen. |
| Uploadkandidat mit nachvollziehbarer ID | ID früh mit `TryRecordUploadRemoteAsync` festhalten. Tatsächlich gemessenen Digest und Größe mit `TryRecordUploadDigestAsync` sichern. Einzelmetadaten unter derselben technischen Identität vergleichen. Erst dann `TryCompleteUploadAsync`. |
| Unbekannter Uploadausgang | Reservierung erhalten. Bekannte, eindeutige ID ausschließlich lesend prüfen. Ohne belastbare ID weder automatisch zuordnen noch erneut senden. |
| Logisches DELETE-204 für exakt gebundene Datei | Mit gültiger D2-Reservierung den lokalen Abschluss samt Receipt über `TryCompleteConfirmedPurgeAsync` vornehmen. |
| 404 oder ungesichertes DELETE | Unklaren Löschvorgang bewahren. `TryBeginDeleteVerificationAsync` erlaubt den autorisierten Abwesenheitsnachweis derselben ID. Ein unqualifiziertes 404 reicht für den Abschluss nicht aus. |

Client und fachlicher Persistenzpfad bleiben eigenständig. Der Client führt keine fachlichen Datenbankzugriffe durch. Die Orchestrierung beendet den lokalen Dispatch-Commit vor Remote-I/O.

## VIA.Docs-Befunde

| ID | Bewertung und Umsetzung |
| --- | --- |
| VD-01 / VD-02 | Beide PageCount-Korrekturen sind im lokalen VIA.Docs-Arbeitsstand sichtbar. Deployment ungeprüft. MiniDocs liest das optionale Metadatenfeld und interpretiert RawWidth nicht als Seitenzahl. |
| VD-03 | Upload liefert keinen SHA-256. D3-B liefert daher Kandidat und Bytezahl. D5 vergleicht autorisierte Einzelmetadaten mit lokal gemessenen Bytes. |
| VD-04 | Ready und SVG können zusammen auftreten. Fallback wird ohne SVG-Body signalisiert. |
| VD-05 / VD-06 | HTTP-Status ist maßgeblich. Retry-After bleibt ein optionaler, geprüfter Hinweis. Kein automatischer Last-Retry. |
| VD-07 | GET/HEAD, Single-Range und ETag-Semantik sind typisiert. If-Range am Browserpfad bleibt bei D5. |
| VD-08 | Die Testfälle belegen die beobachtete Requestanzahl, keine allgemeine Remote-Idempotenz. |

## Prüfbefehle

| Befehl | Exitcode | Ergebnis |
| --- | --- | --- |
| `dotnet restore backend/VIA.MiniDocs.sln --locked-mode -m:1 -nr:false` | 0 | Standard-Locked-Restore mit aktivem NuGet-Audit |
| `dotnet build backend/VIA.MiniDocs.sln --no-restore -m:1 -nr:false` | 0 | Debug-Build, 0 Warnungen und 0 Fehler |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false --filter "FullyQualifiedName~ViaDocsFilesTests\|FullyQualifiedName~ViaDocsAuthenticationTests"` | 0 | 118 bestanden, davon 62 D3-A und 56 D3-B, 0 übersprungen |
| `dotnet test backend/VIA.MiniDocs.sln --no-build --no-restore -m:1 -nr:false` | 0 | 287 bestanden einschließlich isolierter PostgreSQL-Fixtures, 0 übersprungen |
| `npm run check`, `npm run typecheck`, `npm run test:drift` im Backend | jeweils 0 | OpenAPI, TypeScript und negativer Drift-Test erfolgreich |
| Git-Diff, Whitespace neuer Dateien und Secret-Musterscan | 0 | Diff und neue Dateien ohne Whitespace-Fehler. Geprüfte Produktionsdateien ohne Secret-Treffer. |
