# VIA.MiniDocs

React-/TypeScript-Demo zur Verwaltung von Notizen und lokalen PDFs. Die Demo nutzt Daten im Arbeitsspeicher. Im Backend sind das D1-Fundament und die D2-Persistenz mit PostgreSQL und verbindlichen Schreibgrenzen implementiert. D2 ist im geprüften Stand unabhängig technisch abgenommen (Note 1−, 26.11.2026); der [D2-06-Nachweis](docs/D2-06-VERIFICATION.md) dokumentiert Umfang und Grenzen. Die fachliche API, die VIA.Docs-Anbindung und die KU-Gesamtabnahme stehen aus.

## Lokal starten

Node.js 22.12+ oder eine kompatible Node-20-Version ab 20.19; im Verzeichnis `frontend`:

```powershell
npm install
npm run check
npm run dev
```

Vite zeigt die lokale Adresse; `Strg+C` beendet den Server. `npm run check` prüft Formatierung, TypeScript, ESLint, Stylelint, Vitest und Build.

## Lokaler Demo-Datenbestand

`demo-data/ui-demo-30/` enthält den fiktiven 30-Dokumente-Bestand samt Original-PDFs, Transkripten, Übersicht und Prüfbericht aus `VIA.MiniDocs_Demo-Datenbestand_30.zip` (SHA-256 `3D57E8D93B16ED2C08FEA1F97283E28BECD1D1A54161E75AB7CA423CE834845F`). Der Vite-Entwicklungsserver liest Metadaten und Transkripte aus der Übersicht und liefert Original-PDFs auf Anfrage; die Ressourcen werden nicht in den Produktionsbuild übernommen. Das Verzeichnis ist zur Versionierung vorgesehen und darf keine produktiven Nutzerdaten enthalten.


## Backend lokal

Voraussetzungen: .NET SDK 10, Node.js und npm. Im Repository ausführen:

```powershell
dotnet restore backend/VIA.MiniDocs.sln --locked-mode
dotnet build backend/VIA.MiniDocs.sln --no-restore
dotnet test backend/VIA.MiniDocs.sln --no-restore
```

Der D4-A-Host benötigt vor dem Start den geschützten Key-Ring und für Session-Anfragen die Verbindung zur lokalen MiniDocs-Datenbank. Die Konfiguration einschließlich Erstinitialisierung steht im [D4-A-Nachweis](docs/D4-A-VERIFICATION.md#betriebskonfiguration). Mit konfiguriertem lokalen HTTPS-Zertifikat lässt sich der Host so starten.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/src/VIA.MiniDocs.Api --no-launch-profile --urls https://localhost:5137
```

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
