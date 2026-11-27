# VIA.MiniDocs

React-/TypeScript-Demo zur Verwaltung von Notizen und lokalen PDFs. Die Demo nutzt Daten im Arbeitsspeicher. Im Backend sind das D1-Fundament und die D2-Persistenz mit PostgreSQL und verbindlichen Schreibgrenzen implementiert. D2 ist im geprüften Stand unabhängig technisch abgenommen (Note 1−, 26.11.2026); der [D2-06-Nachweis](docs/D2-06-VERIFICATION.md) dokumentiert Umfang und Grenzen. Die fachliche API, die VIA.Docs-Anbindung und die KU-Gesamtabnahme stehen aus.

## Lokal starten

Node.js 22.12+ oder eine kompatible Node-20-Version ab 20.19; im Verzeichnis `frontend`:

```powershell
npm install
npm run check
npm run dev -- --mode demo
```

Vite zeigt die lokale Adresse; `Strg+C` beendet den Server. `npm run check` prüft Formatierung, TypeScript, ESLint, Stylelint, Vitest und Build. Der Demo-Modus wird ausschließlich mit `--mode demo` aktiviert. Der Standardmodus zeigt die Anmeldung und verwendet keine Demo-Daten.

## Lokaler Demo-Datenbestand

`demo-data/ui-demo-30/` enthält den fiktiven 30-Dokumente-Bestand samt Original-PDFs, Transkripten, Übersicht und Prüfbericht aus `VIA.MiniDocs_Demo-Datenbestand_30.zip` (SHA-256 `3D57E8D93B16ED2C08FEA1F97283E28BECD1D1A54161E75AB7CA423CE834845F`). Der Vite-Entwicklungsserver liest Metadaten und Transkripte aus der Übersicht und liefert Original-PDFs auf Anfrage; die Ressourcen werden nicht in den Produktionsbuild übernommen. Das Verzeichnis ist zur Versionierung vorgesehen und darf keine produktiven Nutzerdaten enthalten.


## Browserregression

Aus dem Repository-Root mit Docker, .NET SDK 10 und Node.js ausführen:

```powershell
npm --prefix frontend ci
npm --prefix frontend exec -- playwright install chromium
dotnet restore backend/VIA.MiniDocs.sln --locked-mode
npm --prefix frontend run test:e2e
```

Der Test baut das Frontend, startet einen eigenen PostgreSQL-Container, einen HTTPS-API-Host auf einem freien Loopback-Port und die HTTPS-Demo auf Port 4179. Es gilt ein temporäres Testzertifikat. Provisionierung verwendet ein lokales VIA.Docs-Testdouble. Bestehende Benutzer und Datenbanken werden nicht verwendet. Der Host und seine Testressourcen werden beim regulären Testende und bei Testfehlern freigegeben.

`npm --prefix frontend run test:e2e -- --functional` prüft gezielt die Abläufe. `npm --prefix frontend run test:e2e -- --candidates` erzeugt Referenzkandidaten unter `frontend/test-results/candidates/`. Die abgenommenen V2-Referenzen für Login hell, Login dunkel und die Dokumentenoberfläche liegen unter `frontend/e2e/reference/win32/`. Die reguläre Prüfung erstellt oder aktualisiert keine Referenz. Fehlende oder abweichende Referenzen führen zu einem Fehler. Neue Referenzen und Änderungen daran setzen eine ausdrückliche Sichtabnahme voraus. Für den Vergleich die gepinnte Chromium-Version und dieselbe Windows- und Schriftumgebung verwenden. Weitere Plattformen benötigen eigene abgenommene Referenzen.

## Backend lokal

Voraussetzungen: .NET SDK 10, Node.js und npm. Im Repository ausführen:

```powershell
dotnet restore backend/VIA.MiniDocs.sln --locked-mode
dotnet build backend/VIA.MiniDocs.sln --no-restore
dotnet test backend/VIA.MiniDocs.sln --no-restore
```

Der Host benötigt eine bereits migrierte lokale MiniDocs-Datenbank mit Runtime-Rolle, einen geschützten Key-Ring und ein vertrauenswürdiges localhost-HTTPS-Zertifikat. Die Konfiguration einschließlich Erstinitialisierung und Bootstrap steht im [D4-A-Nachweis](docs/D4-A-VERIFICATION.md#betriebskonfiguration). Die folgenden Platzhalter werden durch lokal bereitgestellte Testwerte ersetzt. Das Datenbankkennwort und das Kennwort des Key-Ring-Zertifikats gehören in einen geschützten lokalen Konfigurationskanal.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MiniDocs = '<Runtime-Verbindungszeichenfolge>'
$env:Security__KeyRing__Directory = 'C:\sicher\minidocs-keyring'
$env:Security__KeyRing__ApplicationName = 'MiniDocs-Local'
$env:Security__KeyRing__CertificateFile = 'C:\sicher\minidocs-keyring.pfx'
$env:Security__KeyRing__CertificatePassword = Read-Host 'Key-Ring-Zertifikatkennwort' -MaskInput
dotnet run --project backend/src/VIA.MiniDocs.Api --no-launch-profile --urls https://localhost:5137
```

Für den lokalen Browserzugriff startet Vite mit einem vertrauenswürdigen localhost-Zertifikat und einem verifizierten HTTPS-Proxy. Ein lokales PEM-Zertifikat und dessen privater Schlüssel müssen bereits sicher bereitgestellt sein. Die Zertifizierungsstelle des Backend-Zertifikats muss Node bekannt sein, beispielsweise über `NODE_EXTRA_CA_CERTS` vor dem Start. Der Browser muss dem Frontend-Zertifikat ebenfalls vertrauen. Die drei Pfade und der Backend-Port werden lokal angepasst.

```powershell
$env:MINIDOCS_DEV_CERT = 'C:\sicher\localhost-cert.pem'
$env:MINIDOCS_DEV_KEY = 'C:\sicher\localhost-key.pem'
$env:MINIDOCS_DEV_API = 'https://localhost:5137'
$env:NODE_EXTRA_CA_CERTS = 'C:\sicher\localhost-ca.pem'
cd frontend
npm run dev
```

Die Browseradresse aus Vite verwendet HTTPS. `/api` wird auf den lokalen API-Host weitergeleitet. Session-Cookies und Antiforgery bleiben an denselben Browser-Origin gebunden. Der lokale API-Host benötigt die separat konfigurierte isolierte PostgreSQL-Datenbank und den geschützten Key-Ring.

Die OpenAPI-/TypeScript-Generierung verwendet den separaten Metadatenhost.

```powershell
cd backend
npm ci
npm run generate
npm run check
npm run typecheck
npm run test:drift
```

Die Generatorbefehle bauen den API-Host in `ContractTests` und vergleichen die generierten Typen. Die technischen Fixtures und `/openapi/v1.json` sind ausschließlich in dieser Umgebung erreichbar. Der Metadatenhost gibt keine Sitzungen aus und benötigt keine Datenbankverbindung. Die reale VIA.Docs-Integration und die Betriebsfreigabe folgen separat.

Gepinnte Direktabhängigkeiten: Microsoft.AspNetCore.OpenApi 10.0.12, Microsoft.Extensions.ApiDescription.Server 10.0.0, Microsoft.AspNetCore.Mvc.Testing 10.0.12, Microsoft.NET.Test.Sdk 18.0.1 (MIT); xUnit 2.9.3 und xunit.runner.visualstudio 3.1.4 (Apache-2.0); openapi-typescript 7.13.0 und TypeScript 5.9.3 (MIT). NuGet- und npm-Lockdateien sichern transitiven Restore. Die TypeScript-Fixtures werden zusätzlich mit dem Frontend-Compiler geprüft.

Die vollständige Backendsuite umfasst seit D2 PostgreSQL-Integrationstests. Docker muss erreichbar sein und das gepinnte PostgreSQL-18.7-Image bereitstellen können. Die Fixture verwendet eigene Einwegcontainer mit Loopback-Port und nimmt keine externe Datenbankverbindung an. Die Anleitung für den Rollen- und Migrationswechsel steht in der [Architektur](docs/ARCHITECTURE.md#implementierte-schreibgrenze-d2-05).

## Dokumentationswegweiser

- [AGENTS.md](AGENTS.md): verbindliche Arbeitsregeln.
- [Masterplan](docs/MASTERPLAN.md) und [Tickets](docs/TICKETS.md): aktueller Stand, Planung und offene Abnahmen.
- [Engineering-Standards](docs/ENGINEERING-STANDARDS.md) und [Definition of Done](docs/DEFINITION-OF-DONE.md): Qualitäts- und Abnahmeregeln.
- [Architektur](docs/ARCHITECTURE.md): Frontend-Bestand, Backend-Zielbild, Sicherheit und Datenmodell.
- [Entscheidungen](docs/DECISIONS.md): maßgebliche ADRs und Freigabestatus.
- [VIA.Docs-API](docs/VIA-DOCS-API.md): statischer Quellstand und nicht verifiziertes Deployment.
- [D2-06-Nachweis](docs/D2-06-VERIFICATION.md): Korrekturen des unabhängigen Nachreviews, Testabdeckung und Verifikationsprotokolle. [D2-05-Nachweis](docs/D2-05-VERIFICATION.md) dokumentiert den vorherigen Stand.
- [D4-A-Nachweis](docs/D4-A-VERIFICATION.md) dokumentiert das lokale Sicherheitsfundament mit Session- und Bootstrap-Prüfungen.

Die früheren Architekturdateien unter `docs/architecture/` sind historische Ausgangsfassungen beziehungsweise Nachweise. Der [historische Ticketkatalog](docs/TICKETS-ARCHIV.md) bewahrt frühere Detailnachweise; aktuelle Aufgaben stehen in `TICKETS.md`. Die [PDF-Entscheidung](docs/architecture/M6-C-PDF-ENTSCHEIDUNG.md) bleibt als fachlicher Nachweis für Abgrenzung und Abnahme erhalten. Die lokale KU-Originalreferenz wird nicht veröffentlicht.
