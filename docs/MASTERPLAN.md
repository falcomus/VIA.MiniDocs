# VIA.MiniDocs – Masterplan

Dieser Masterplan ist der zentrale Einstieg für den aktuellen Projektstand und die gültige Planung. Es gelten die [Entwicklungsregeln](../AGENTS.md), [Engineering-Standards](ENGINEERING-STANDARDS.md) und [Definition of Done](DEFINITION-OF-DONE.md). Historische Vorbereitungs- und Vergleichsdokumente sind im [Dokumentationswegweiser](../README.md#dokumentationswegweiser) eingeordnet. Die lokale Original-HTML-Datei unter `.local-reference/` ist die verbindliche Design- und Verhaltensreferenz; sie wird nicht versioniert.

## Projektziel und Architektur

MiniDoc soll handschriftliche Besprechungs- und persönliche Notizen als PDFs mit Themenhierarchie, Tags und seitenweise bearbeitbaren Transkripten verwalten und durchsuchbar machen. Zunächst entsteht die originalgetreue React-Demo; die dauerhafte Datenanbindung wird gesondert beauftragt. Zusätzliche Ideen wie KI-Sammelscan-Zerlegung, semantische Suche und Mehrbenutzerbetrieb gehören nicht automatisch zum Prototypumfang.

Das Frontend verwendet React, TypeScript strict, Vite und CSS Modules. Kleine Komponenten trennen Darstellung, Fachlogik, Zustand und Datenzugriff. Die Demo verwendet Daten im Arbeitsspeicher. Weitere Abhängigkeiten, insbesondere State-Management-Bibliotheken, werden nur bei nachgewiesenem Bedarf eingeführt.

Das geplante Zielbild ist **React → ASP.NET Core MiniDocs API → eigene PostgreSQL-18-Datenbank** für Fachdaten. Die API bindet VIA.Docs V1.2 serverseitig über REST für Dateien und Vorschauen an. Einladungsbasierte Benutzerregistrierung, private Workspaces, technische VIA.Docs-Identitäten und Same-Origin-Authentifizierung sind fachlich entschieden. .NET 10 und die Projekt-, Vertrags- und Engineering-Grundlagen sind gemäß ADR-014 bis ADR-016 freigegeben. Die lokale Datenbankprüfung verwendet das offizielle Image `postgres:18.7`. [Backend-Architektur](ARCHITECTURE.md) und [ADR-Status](DECISIONS.md) sind maßgeblich. Die Einladungsrouten sind im geprüften lokalen VIA.Docs-Commit versioniert. Das Deployment einschließlich Einladungsfunktion ist vom Betreiber gemeldet und benötigt eine eigenständige Integrationsabnahme. Produktive Zugriffe erfordern einen gesonderten Testauftrag. [Quell- und Betriebsstand](VIA-DOCS-API.md#d3-plan--evidenz-und-kritische-vertragsbefunde)

## Aktueller Stand und nächste Meilensteine

- Das aktuelle Login-Design und die statischen Nachprüfungen von D4-D-FIX3 und D4-D-INVITE-LINK sind freigegeben. D4-FINAL hat das vollständige automatisierte Regression-Gate bestanden. Der 45-Dateien-Diff ist statisch freigegeben und der Arbeitsbranch-Commit ist genehmigt. Die manuelle Anmeldung mit Benutzername und Passwort war erfolgreich. Die manuelle Tastatur-, Fokus- und Accessibility-Abnahme sowie Merge und Push stehen aus. Danach folgen BACKEND-QUALITY-GATE-01 und D3-INTEGRATION vor D6 und D5 sowie anschließend D7, erforderlicher UI-Feinschliff und D8. Die bestehende Dokumentenoberfläche bleibt das produktive Frontend und wird später mit eigenen Daten aktiver regulärer Benutzer angebunden. [D4-FINAL-Nachweis](D4-FINAL-VERIFICATION.md)

- M1 Grundlayout und M2 Themenbaum sind technisch abgeschlossen; ihre visuellen Browserabnahmen sind offen und werden in M8 nachverfolgt.
- Q1 verbessert die Tastaturbedienung und Dialogfokus technisch; Browser- und Screenreader-Prüfungen sind offen. M3 Listen- und Kachelansicht ist technisch umgesetzt; visuelle Abnahme offen. M4 Auswahl, Sortierung, Kachelzoom und Dokumentverschiebung mit Undo ist technisch umgesetzt; native Maus-DnD- und visuelle Abnahme offen. M5 mit Suche, Filtern und Tagverwaltung ist technisch umgesetzt; der vollständige visuelle und funktionale Vergleich bleibt offen. M6-A, M6-B, M6-C und M7-A sind technisch umgesetzt; lokale PDFs erhalten beim Import eine PDF.js-Seitenzahl und bedarfsweise eine Miniatur; React-PDF zeigt sie mit gemeinsamer Seitennavigation. Vollständige visuelle und Accessibility-Nachweise bleiben offen; der Scrollumfang ist fachlich festgelegt. VIA.Docs-Ende-zu-Ende folgt mit der Backend-Anbindung. M7-B-TRANSKRIPT und M7-C bis M8 sind gemäß [Ticketkatalog](TICKETS.md) geplant.
- Q10 bietet das zentrale Einstellungsmenü mit Hell-, Dunkel- und Systemdarstellung; visuelle und Screenreader-Abnahme sind offen.
- M7-A-FIX ergänzt die hierarchische Themenzuordnung im Viewer einschließlich Abgleich mit verbundenen Viewer-Tabs. Der Projektverantwortliche hat die fünf vereinbarten Browserprüfungen einschließlich Tastatur, Fokus und Screenreader-Beschriftung am 25.11.2026 bestätigt. Der vollständige KU-Vergleich bleibt offen.
- Q2 CSS-Qualitätsprüfung ist technisch umgesetzt; visuelle und Accessibility-Abnahmen bleiben offen.
- Q3 Modulgrenzen und Testaussagen sind technisch geprüft und korrigiert; Q4 automatisiert die Frontend-Formatierung; CSS-Typautomatisierung bleibt als Q5 geplant.
- Q6 sichert Dropmarkierung, interne Drag-Quellen und Wurzelablage mit gemeinsamem Sitzungsvertrag und gezielten Tests ab; Q8 setzt die Löschung ausschließlich leerer Themen technisch um; die Browser- und Sichtabnahme ist offen. Q5 bleibt separat geplant, Browser-/Screenreader-Abnahmen bleiben offen.
- Die fortlaufende PDF-Darstellung mit seitenweiser Transkript-Nachführung ist als M7-B-PDF technisch umgesetzt und auf `main` integriert. Die beauftragte Browserabnahme ist bestätigt. Der vollständige KU-Vergleich bleibt offen. Eine manuelle Touch-Prüfung ist ausgenommen. Transkriptdateiimport, Papierkorb im Frontend und sicherer Zugriff von unterwegs sind Folgearbeiten.
- D1 und D2 sind technisch abgeschlossen. D3-01 bis D3-05 sind unabhängig mit Note 1− abgenommen und mit dem bestätigten Stand auf origin/main formal im Repository abgeschlossen. D3-INTEGRATION bleibt verbindlich offen. D4-PLAN einschließlich FIX ist unabhängig mit Note 1− (1,3) abgenommen. D4-A ist unabhängig mit Note 1− (1,3) technisch freigegeben. D4-B ist nach unabhängiger statischer Prüfung mit Note 1− (1,3) technisch freigegeben. D4-C-FIX ist nach unabhängiger statischer Prüfung der 25 Paketdateien mit Note 1− (1,3) technisch freigegeben. Alle drei FIX-Befunde sind geschlossen. Die Codex-Verifikation ergab je 435 bestandene Backendtests in Debug und Release bei Erhalt aller 410 Bestandstests sowie 225 bestandene Frontendtests. Der Reviewer führte keine eigenen Testläufe aus. BACKEND-QUALITY-GATE-01-FIX sichert produktive Dispatch- und Bound-Identity-Autorisierung sowie das D2-Restore-Gate ab. Die technische Verifikation steht im [Auditnachweis](BACKEND-QUALITY-GATE-01-VERIFICATION.md). D4-D-FIX3 und D4-D-INVITE-LINK sind statisch freigegeben und im D4-Arbeitsbranch gesichert. D4-D umfasst die Vertragskorrektur für `expiresAt` und den HTTP-429-Nachweis. Die vollständigen HTTP-Ende-zu-Ende-Nachweise der fachlichen Mandantenisolation sind für D6 vorgesehen. D8 und die produktiven Betriebsabnahmen bleiben offen. [D3-Gesamtnachweis](D3-C-VERIFICATION.md) · [D3-INTEGRATION](TICKETS.md#d3-integration--reale-viaserver-abnahme) · [D4-Detailplanung](D4-PLAN.md) · [D4-A-Nachweis](D4-A-VERIFICATION.md) · [D4-B-Nachweis](D4-B-VERIFICATION.md) · [D4-C-Nachweis](D4-C-VERIFICATION.md)
- B0-FINAL ist fachlich freigegeben. ADR-013 ist die freigegebene Hosting-Zielarchitektur. Integrations-, Deployment- und Betriebsfreigaben stehen aus.

- UI-DEMO-30 einschließlich 30 lokaler Demo-PDFs mit Metadaten und Transkripten ist freigegeben. Der Projektverantwortliche bestätigt Kachelansicht, PDF-Miniaturen, Download und Dokumentverschiebung; der vollständige KU-Vergleich bleibt offen.

- UI-POLISH – VIA Professional Design System ist nach der funktionalen Entwicklung geplant. Light-/Dark-Designstudien benötigen vor der Umsetzung die Freigabe des Projektverantwortlichen; Umfang und Abnahme stehen im [Ticket](TICKETS.md#ui-polish--via-professional-design-system).

## Abhängigkeiten

Für die Demo ist die Reihenfolge M3 → M4 → M5 → M6 → M7 → M8 vorgesehen. M8 umfasst den funktionalen und visuellen Gesamtvergleich einschließlich der offenen M1-/M2-Abnahmen.

Die freigegebene Implementierungsreihenfolge lautet D1 → D2 → D3 → D4 → D6 → D5 → D7 → D8. Für die spätere Datenanbindung bilden API-Verträge und Datenmodell die Grundlage. VIA.Docs-Client und Authentifizierung werden für die Dateiflüsse benötigt; Persistenz und Suche sind Voraussetzungen für den Ende-zu-Ende-Nachweis. Die konkreten Abhängigkeiten und Akzeptanzkriterien stehen im [Ticketkatalog](TICKETS.md). Integration und Betrieb erfolgen in einer freigegebenen Testumgebung; die Produktivfreigabe ist eine gesonderte Entscheidung.

## Offene Entscheidungen und Risiken

Offen sind insbesondere die Betriebsfreigabe für die Hosting-Zielarchitektur, der deployte VIA.Docs-Build samt Einladungsmigration und Testidentität, die Bestätigung des dokumentierten Verhaltens für Upload, Löschung, Preview und Range in der Zielumgebung, Backup-/Restore-Zuständigkeiten sowie die anhand von Messungen zu prüfende Indexstrategie. Die freigegebene 25-MiB-Uploadgrenze gehört zur gesonderten Ende-zu-Ende-Abnahme. Implementierung, Integration und Performance haben eigene Abnahmekriterien. [VIA.Docs-Vertragsbefunde](VIA-DOCS-API.md#d3-plan--evidenz-und-kritische-vertragsbefunde) · [Fachliche Entscheidung zur Scrollsynchronisation](architecture/M6-C-PDF-ENTSCHEIDUNG.md)

Die visuelle Vergleichsumgebung für M1 und M2 sowie die Browser- und Screenreader-Prüfung der Tastaturalternative zum Themenverschieben sind offen. Browserunterschiede, PDF-Viewer-Grenzen und inkonsistente Daten bei Teilausfällen bleiben relevante Risiken. Es gelten die Sicherheits- und Qualitätsanforderungen der [Definition of Done](DEFINITION-OF-DONE.md).

## Dokumentationsregeln

- **MASTERPLAN.md:** ausschließlich aktueller Stand und gültige Planung.
- **TICKETS.md:** Aufgaben, Status, Abhängigkeiten, Abnahmekriterien und Nachweise.
- **DEFINITION-OF-DONE.md:** verbindliche Qualitätskriterien.
- **Git:** technische Änderungshistorie.

Keine parallelen Projektchroniken führen. Historische Detailpläne und Vergleiche dienen als Nachweise und Originalreferenz, nicht als aktueller Projektstand.

## REGRESSION-GATE-01 · abgeschlossen

Automatisierte Bestands- und Browserregression sind verbindliche Freigabenachweise. Der Paketnachweis umfasst 436 Backend- und 266 Vitest-Bestandstests. Die drei abgenommenen V2-Referenzen sind als Windows-Referenzen übernommen. Der reguläre Lauf `npm --prefix frontend run test:e2e` bestand alle sechs Chromium-Tests einschließlich der drei visuellen Vergleiche mit Exitcode 0 und deaktivierter Referenzaktualisierung. D4-D-INVITE-LINK bleibt ein separates Ticket.
