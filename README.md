# VIA.MiniDocs – React-Testbasis

**Status:** frühes, bewusst begrenztes Testprojekt für die Zusammenarbeit über Chat ABC. **Kein produktives MiniDoc.**

## Was enthalten ist

- React + TypeScript (`strict`) mit Vite
- eine lesende Themenhierarchie mit lokal hinterlegten **Demodaten**
- eine Liste von **Beispieldokumenten** für das ausgewählte Thema
- CSS Modules ohne zusätzliche UI-Bibliothek
- Vitest + React Testing Library und ESLint
- Draft-Dokumente zu Umfang und offenen Architekturentscheidungen

**Nicht enthalten:** MiniDoc-API, PostgreSQL, VIA.Docs-Anbindung, echtes Speichern, PDF-Upload, Suche, Authentifizierung, Router, State-Management-Bibliothek. Es wurden keinerlei Projektdaten importiert.

## Einrichtung unter Windows

1. ZIP-Inhalt **direkt** nach `C:\VIA_DEVELOPMENT\PROJECTS\VIA.MiniDocs` entpacken. Dort sollten danach `frontend`, `docs` und diese `README.md` liegen.
2. In einem Terminal im Projektverzeichnis:

   ```powershell
   cd frontend
   npm install
   npm run check
   npm run dev
   ```

3. Die von Vite im Terminal angezeigte lokale Adresse im Browser öffnen (gewöhnlich `http://localhost:5173`).
4. Zur Beendigung des Entwicklungsservers `Strg+C` drücken.

Voraussetzung: **Node.js 22.12+** oder eine kompatible Node-20-Version (20.19+). Die Node-Version muss zur installierten Vite-Version passen.

Die vorhandene `package-lock.json` hält die aufgelösten Abhängigkeiten fest. `node_modules` und `dist` werden nicht versioniert.

## Einzelne Prüfungen

Alle Prüfungen über `npm run check`: **TypeScript → ESLint → Vitest → Produktionsbuild**. Individuell möglich: `npm run typecheck`, `npm run lint`, `npm test`, `npm run build`. Playwright ist **noch nicht eingerichtet**.

## Chat ABC

Der Chat-ABC-Workspace wurde zuvor als `ABC-D2FA39C3` / `VIA.MiniDocs` bestätigt. **In einem neuen Chat** oder bei unklarem Zustand zuerst `PROJECT-OVERVIEW` durchführen; ältere Projektinformationen ersetzen keine frische Prüfung. Vor einer Änderung den passenden Operation Contract und aktuelle Originaldateien abrufen. Chat ABC ist eine **separate Windows-Anwendung**, die Kommunikation erfolgt manuell über die Zwischenablage. Dieses ZIP wird **nicht automatisch** durch Chat ABC eingespielt.

Prüffragen für Chat ABC:

1. Werden die neuen React-/TypeScript-Dateien in der Projektübersicht erkannt?
2. Kann `READ_CODE` die vollständigen Dateien korrekt ausgeben?
3. Kann eine kleine, freigegebene Änderung über `CHANGE_PACKAGE` sicher vorbereitet, angewendet und geprüft werden?
4. Werden TypeScript-, Lint-, Test- und Vite-Build-Ergebnisse sichtbar und getrennt ausgewiesen? Ein .NET-zentrierter `FULL`-Check allein ersetzt diese Prüfungen nicht.

## Nächste Architekturentscheidung

Das Inventar und die vorbereitenden Architektur- und Migrationspläne stehen unter `docs/architecture/`. Vor dem Anschluss an VIA.Docs und PostgreSQL sind die Schnittstellen und offenen fachlichen Punkte abzustimmen. Eine Backend-Implementierung ist nicht enthalten.
