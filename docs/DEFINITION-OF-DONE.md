# Definition of Done

Gilt für neue Tickets im [Ticketkatalog](TICKETS.md); siehe auch [Masterplan](MASTERPLAN.md) und [AGENTS.md](../AGENTS.md). Jedes Ticket dokumentiert konkrete Nachweise, Ergebnisse und gegebenenfalls ausdrücklich offene Punkte. Ein blockierter oder nicht ausgeführter Prüfschritt ist **nicht bestanden**. Ausnahme oder Verschiebung einer Abnahme nur als ausdrücklich dokumentierte Entscheidung; sie macht eine ausstehende Abnahme nicht grün.

## Allgemein

- Alle ticketbezogenen Akzeptanzkriterien erfüllt; vorhandene Funktionen bewahrt und Änderungen auf den Auftrag begrenzt.
- Architektur- und Sicherheitsregeln eingehalten; keine unnötigen Abhängigkeiten. Fehler und Grenzen nachvollziehbar dokumentiert.
- Für betroffene Frontend-Arbeit `npm run check` erfolgreich: TypeScript, ESLint, bestehende und erforderliche neue Vitest-Tests sowie Vite-Build. Für API-/DB-Arbeit zusätzlich die im Ticket festgelegten projektspezifischen Build-, Lint- und Testbefehle erfolgreich; Befehle und Exitcodes festhalten.
- `git diff --check` und Prüfung des vorgemerkten Diffs erfolgreich. Keine Secrets, Zugangsdaten, lokalen Originalreferenzen, lokalen Hilfsdateien oder Build-/Testartefakte im Commit. Relevante Dokumentation und offene Entscheidungen aktualisiert.
- Beabsichtigte Änderungen committed und regulär nach `origin/main` gepusht; lokales `main` und tatsächliches `origin/main` abgeglichen. Bei Remote-Konflikt oder fehlender Berechtigung nicht force-pushen und Ticketabschluss nicht behaupten.

## UI zusätzlich

- Referenzzustände und Bedienverhalten gegen das Original bei dokumentierter gleicher Fenstergröße vergleichen; Farben, Typografie, Icons, Abstände, Fokus-, Hover-, Auswahl- und Scrollzustände prüfen und Abweichungen festhalten.
- Tastaturbedienung, Fokusführung, Dialog-/Escape-Verhalten und sinnvolle ARIA-Semantik für betroffene Interaktionen prüfen; passende automatisierte und Browser-Interaktionstests durchführen.
- Visuelle Abnahme durch eine zuständige Person dokumentieren. Ein Quellcodevergleich oder automatisierter Test ersetzt sie nicht. M1/M2 bleiben technisch abgeschlossen; ihre offenen visuellen Abnahmen werden in M8 gesondert erledigt.

## API zusätzlich

- Versionierter, überprüfter Vertrag inklusive Eingaben, Ausgaben, Statuscodes und Fehlerfällen; Authentifizierung und Autorisierung für betroffene Endpunkte geklärt und getestet.
- Integrations-, Negativ- und Fehlerfalltests gegen kontrollierte Stubs bzw. freigegebene Testdienste, ohne produktive Daten; Timeouts und Fehlermeldungen geprüft. Keine produktiven VIA.Docs-Zugriffe ohne gesonderten Testauftrag.

## Datenbank zusätzlich

- Versionierte Migration und passender Rollback bzw. dokumentierter Wiederherstellungsweg in Testumgebung erprobt; Datenintegrität, Beziehungen, Constraints und relevante Konfliktfälle getestet.
- Sicherung und Wiederherstellung für betroffene Daten nachgewiesen; irreversible Änderungen und Auswirkungen auf vorhandene Daten vor Ausführung geklärt.

## Deployment und Betrieb zusätzlich

- Freigegebene Konfiguration, Geheimnisverwaltung, Transport- und Zugriffsschutz geprüft; keine produktiven Zugangsdaten im Frontend oder Repository.
- Backup und Restore für alle beteiligten Daten einschließlich Zuständigkeiten und Konsistenz geprüft; Wiederherstellung praktisch nachgewiesen.
- Monitoring, aussagekräftige Logs ohne Secrets, Fehleralarmierung und Wiederanlauf nach Ausfall getestet; Betriebsanleitung und Zuständigkeiten dokumentiert. Deployment und produktive Zugriffe erfolgen nur im jeweils ausdrücklich autorisierten Rahmen.
