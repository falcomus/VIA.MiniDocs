# VIA.MiniDocs

React-/TypeScript-Demo zur Verwaltung von Notizen und lokalen PDFs. Daten und Dateien liegen bislang im Arbeitsspeicher; Das D1-Backend-Fundament ist implementiert; PostgreSQL und VIA.Docs-Anbindung sind nicht implementiert. Die KU-Gesamtabnahme ist offen.

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


## Backend lokal (D1)

Voraussetzungen: .NET SDK 10, Node.js und npm. Im Repository ausführen:

```powershell
dotnet restore backend/VIA.MiniDocs.sln --locked-mode
dotnet build backend/VIA.MiniDocs.sln --no-restore
dotnet test backend/VIA.MiniDocs.sln --no-restore
dotnet run --project backend/src/VIA.MiniDocs.Api --no-launch-profile --urls http://127.0.0.1:5137
```

Der Host unter http://127.0.0.1:5137 hat in D1 keine fachlichen Endpunkte; ein 404 ist daher zu erwarten. Für die reproduzierbare OpenAPI-/TypeScript-Fixturegenerierung und Prüfungen:

```powershell
cd backend
npm ci
npm run generate
npm run check
npm run typecheck
npm run test:drift
```

Die Generatorbefehle bauen den tatsächlichen API-Host mit der expliziten Umgebung `ContractTests` und vergleichen die generierten Typen. Die rein technischen Fixtures und `/openapi/v1.json` sind ausschließlich in dieser Umgebung erreichbar, nicht im regulären Produktionshost. Sie implementieren keine Geschäftsoperationen. Reale Authentifizierung, Datenbank, VIA.Docs und Betriebsfreigabe folgen separat.

Gepinnte Direktabhängigkeiten: Microsoft.AspNetCore.OpenApi 10.0.12, Microsoft.Extensions.ApiDescription.Server 10.0.0, Microsoft.AspNetCore.Mvc.Testing 10.0.12, Microsoft.NET.Test.Sdk 18.0.1 (MIT); xUnit 2.9.3 und xunit.runner.visualstudio 3.1.4 (Apache-2.0); openapi-typescript 7.13.0 und TypeScript 5.9.3 (MIT). NuGet- und npm-Lockdateien sichern transitiven Restore. Die TypeScript-Fixtures werden zusätzlich mit dem Frontend-Compiler geprüft.

## Dokumentationswegweiser

- [AGENTS.md](AGENTS.md): verbindliche Arbeitsregeln.
- [Masterplan](docs/MASTERPLAN.md) und [Tickets](docs/TICKETS.md): aktueller Stand, Planung und offene Abnahmen.
- [Engineering-Standards](docs/ENGINEERING-STANDARDS.md) und [Definition of Done](docs/DEFINITION-OF-DONE.md): Qualitäts- und Abnahmeregeln.
- [Architektur](docs/ARCHITECTURE.md): Frontend-Bestand, Backend-Zielbild, Sicherheit und Datenmodell.
- [Entscheidungen](docs/DECISIONS.md): maßgebliche ADRs und Freigabestatus.
- [VIA.Docs-API](docs/VIA-DOCS-API.md): statischer Quellstand und nicht verifiziertes Deployment.

Die früheren Architekturdateien unter `docs/architecture/` sind historische Ausgangsfassungen beziehungsweise Nachweise. Der [historische Ticketkatalog](docs/TICKETS-ARCHIV.md) bewahrt frühere Detailnachweise; aktuelle Aufgaben stehen in `TICKETS.md`. Die [PDF-Entscheidung](docs/architecture/M6-C-PDF-ENTSCHEIDUNG.md) bleibt als fachlicher Nachweis für Abgrenzung und Abnahme erhalten. Die lokale KU-Originalreferenz wird nicht veröffentlicht.
