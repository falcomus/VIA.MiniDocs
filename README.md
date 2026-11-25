# VIA.MiniDocs

React-/TypeScript-Demo zur Verwaltung von Notizen und lokalen PDFs. Daten und Dateien liegen bislang im Arbeitsspeicher; Backend, PostgreSQL und VIA.Docs-Anbindung sind nicht implementiert. Die KU-Gesamtabnahme ist offen.

## Lokal starten

Node.js 22.12+ oder eine kompatible Node-20-Version ab 20.19; im Verzeichnis `frontend`:

```powershell
npm install
npm run check
npm run dev
```

Vite zeigt die lokale Adresse; `Strg+C` beendet den Server. `npm run check` prüft Formatierung, TypeScript, ESLint, Stylelint, Vitest und Build.

## Dokumentationswegweiser

- [AGENTS.md](AGENTS.md): verbindliche Arbeitsregeln.
- [Masterplan](docs/MASTERPLAN.md) und [Tickets](docs/TICKETS.md): aktueller Stand, Planung und offene Abnahmen.
- [Engineering-Standards](docs/ENGINEERING-STANDARDS.md) und [Definition of Done](docs/DEFINITION-OF-DONE.md): Qualitäts- und Abnahmeregeln.
- [Architektur](docs/ARCHITECTURE.md): Frontend-Bestand, Backend-Zielbild, Sicherheit und Datenmodell.
- [Entscheidungen](docs/DECISIONS.md): maßgebliche ADRs und Freigabestatus.
- [VIA.Docs-API](docs/VIA-DOCS-API.md): statischer Quellstand und nicht verifiziertes Deployment.

Die früheren Architekturdateien unter `docs/architecture/` sind historische Ausgangsfassungen beziehungsweise Nachweise. Der [historische Ticketkatalog](docs/TICKETS-ARCHIV.md) bewahrt frühere Detailnachweise; aktuelle Aufgaben stehen in `TICKETS.md`. Die [PDF-Entscheidung](docs/architecture/M6-C-PDF-ENTSCHEIDUNG.md) bleibt als fachlicher Nachweis für Abgrenzung und Abnahme erhalten. Die lokale KU-Originalreferenz wird nicht veröffentlicht.
