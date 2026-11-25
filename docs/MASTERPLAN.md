# VIA.MiniDocs – Masterplan

Dieser Masterplan ist der zentrale Einstieg für den aktuellen Projektstand und die gültige Planung. Es gelten die [Entwicklungsregeln](../AGENTS.md), [Engineering-Standards](ENGINEERING-STANDARDS.md) und [Definition of Done](DEFINITION-OF-DONE.md). Historische Vorbereitungs- und Vergleichsdokumente sind im [Dokumentationswegweiser](../README.md#dokumentationswegweiser) eingeordnet. Die lokale Original-HTML-Datei unter `.local-reference/` ist die verbindliche Design- und Verhaltensreferenz; sie wird nicht versioniert.

## Projektziel und Architektur

MiniDoc soll handschriftliche Besprechungs- und persönliche Notizen als PDFs mit Themenhierarchie, Tags und seitenweise bearbeitbaren Transkripten verwalten und durchsuchbar machen. Zunächst entsteht die originalgetreue React-Demo; die dauerhafte Datenanbindung wird gesondert beauftragt. Zusätzliche Ideen wie KI-Sammelscan-Zerlegung, semantische Suche und Mehrbenutzerbetrieb gehören nicht automatisch zum Prototypumfang.

Das Frontend verwendet React, TypeScript strict, Vite und CSS Modules. Kleine Komponenten trennen Darstellung, Fachlogik, Zustand und Datenzugriff. Die Demo verwendet Daten im Arbeitsspeicher. Weitere Abhängigkeiten, insbesondere State-Management-Bibliotheken, werden nur bei nachgewiesenem Bedarf eingeführt.

Das geplante Zielbild ist **React → ASP.NET Core MiniDocs API → eigene PostgreSQL-Datenbank** für Fachdaten; die API bindet VIA.Docs V1.2 serverseitig über REST für Dateien und Vorschauen an. Einladungsbasierte Benutzerregistrierung, private Workspaces, technische VIA.Docs-Identitäten und Same-Origin-Authentifizierung sind fachlich entschieden. .NET 10 und die Projekt-, Vertrags- und Engineering-Grundlagen sind gemäß ADR-014 bis ADR-016 freigegeben; eine Implementierung ist nicht beauftragt. [Backend-Architektur](ARCHITECTURE.md) und [ADR-Status](DECISIONS.md) sind maßgeblich. Der technische VIA.Docs-Einladungsweg ist im geprüften lokalen Arbeitsbaum, nicht im angegebenen Commit bestätigt; das Deployment ist ungeprüft. Keine produktiven Zugriffe ohne gesonderten Testauftrag.

## Aktueller Stand und nächste Meilensteine

- M1 Grundlayout und M2 Themenbaum sind technisch abgeschlossen; ihre visuellen Browserabnahmen sind offen und werden in M8 nachverfolgt.
- Q1 verbessert die Tastaturbedienung und Dialogfokus technisch; Browser- und Screenreader-Prüfungen sind offen. M3 Listen- und Kachelansicht ist technisch umgesetzt; visuelle Abnahme offen. M4 Auswahl, Sortierung, Kachelzoom und Dokumentverschiebung mit Undo ist technisch umgesetzt; native Maus-DnD- und visuelle Abnahme offen. M5 mit Suche, Filtern und Tagverwaltung ist technisch umgesetzt; der vollständige visuelle und funktionale Vergleich bleibt offen. M6-A, M6-B, M6-C und M7-A sind technisch umgesetzt; lokale PDFs erhalten beim Import eine PDF.js-Seitenzahl und bedarfsweise eine Miniatur; React-PDF zeigt sie mit gemeinsamer Seitennavigation. Vollständige visuelle und Accessibility-Nachweise bleiben offen; der Scrollumfang ist fachlich festgelegt. VIA.Docs-Ende-zu-Ende folgt mit der Backend-Anbindung. M7-B-TRANSKRIPT und M7-C bis M8 sind gemäß [Ticketkatalog](TICKETS.md) geplant.
- Q10 bietet das zentrale Einstellungsmenü mit Hell-, Dunkel- und Systemdarstellung; visuelle und Screenreader-Abnahme sind offen.
- M7-A-FIX ergänzt die hierarchische Themenzuordnung im Viewer einschließlich Abgleich mit verbundenen Viewer-Tabs. Der Projektverantwortliche hat die fünf vereinbarten Browserprüfungen einschließlich Tastatur, Fokus und Screenreader-Beschriftung am 25.11.2026 bestätigt. Der vollständige KU-Vergleich bleibt offen.
- Q2 CSS-Qualitätsprüfung ist technisch umgesetzt; visuelle und Accessibility-Abnahmen bleiben offen.
- Q3 Modulgrenzen und Testaussagen sind technisch geprüft und korrigiert; Q4 automatisiert die Frontend-Formatierung; CSS-Typautomatisierung bleibt als Q5 geplant.
- Q6 sichert Dropmarkierung, interne Drag-Quellen und Wurzelablage mit gemeinsamem Sitzungsvertrag und gezielten Tests ab; Q8 setzt die Löschung ausschließlich leerer Themen technisch um; die Browser- und Sichtabnahme ist offen. Q5 bleibt separat geplant, Browser-/Screenreader-Abnahmen bleiben offen.
- Die fortlaufende PDF-Darstellung mit seitenweiser Transkript-Nachführung ist als M7-B-PDF technisch umgesetzt und auf `main` integriert; die beauftragte Browserabnahme ist bestätigt. Der vollständige KU-Vergleich bleibt offen; eine manuelle Touch-Prüfung ist ausdrücklich aus dem aktuellen Abnahmeumfang ausgenommen. Der separate Transkriptdateiimport M7-B-TRANSKRIPT, Papierkorb und sicherer Zugriff von unterwegs sind Folgearbeiten. Die Datenanbindung D2–D8 ist geplant. Das D1-Backend-Fundament ist technisch abgenommen und abgeschlossen; PostgreSQL-Anbindung und VIA.Docs-Verbindung sind noch nicht implementiert.
- B0-FINAL einschließlich der dokumentierten Architektur- und Vertragsentscheidungen ist fachlich freigegeben. ADR-013 ist als Hosting-Zielarchitektur freigegeben, ausdrücklich ohne Betriebsfreigabe. Reale Integrations-, Deployment- und Betriebsnachweise bleiben offen; D1 ist ausdrücklich beauftragt; D2 und die weiteren Implementierungstickets benötigen gesonderte Aufträge.

- UI-DEMO-30 einschließlich 30 lokaler Demo-PDFs mit Metadaten und Transkripten ist freigegeben. Der Projektverantwortliche bestätigt Kachelansicht, PDF-Miniaturen, Download und Dokumentverschiebung; der vollständige KU-Vergleich bleibt offen.

- UI-POLISH – VIA Professional Design System ist nach der funktionalen Entwicklung geplant. Light-/Dark-Designstudien benötigen vor der Umsetzung die Freigabe des Projektverantwortlichen; Umfang und Abnahme stehen im [Ticket](TICKETS.md#ui-polish--via-professional-design-system).

## Abhängigkeiten

Für die Demo ist die Reihenfolge M3 → M4 → M5 → M6 → M7 → M8 vorgesehen. M8 umfasst den funktionalen und visuellen Gesamtvergleich einschließlich der offenen M1-/M2-Abnahmen.

Die freigegebene Implementierungsreihenfolge lautet D1 → D2 → D3 → D4 → D6 → D5 → D7 → D8. Für die spätere Datenanbindung bilden API-Verträge und Datenmodell die Grundlage. VIA.Docs-Client und Authentifizierung werden für die Dateiflüsse benötigt; Persistenz und Suche sind Voraussetzungen für den Ende-zu-Ende-Nachweis. Die konkreten Abhängigkeiten und Akzeptanzkriterien stehen im [Ticketkatalog](TICKETS.md). Integration und Betrieb erfolgen in einer freigegebenen Testumgebung; die Produktivfreigabe ist eine gesonderte Entscheidung.

## Offene Entscheidungen und Risiken

Zu klären sind insbesondere die Betriebsfreigabe für die Hosting-Zielarchitektur, der tatsächliche VIA.Docs-Vertrag einschließlich des nur im Arbeitsbaum vorgefundenen Einladungswegs, Backup-/Restore-Zuständigkeiten, der reale Nachweis der freigegebenen 25-MiB-Uploadgrenze und die anhand von Messungen zu prüfende Indexstrategie. Die fachliche Abgrenzung der Scrollsynchronisation für M6-C/M7-A ist in der [PDF-Entscheidung](architecture/M6-C-PDF-ENTSCHEIDUNG.md) festgelegt. Die Architekturfreigabe ist kein Implementierungs-, Integrations- oder Performance-Nachweis.

Die visuelle Vergleichsumgebung für M1 und M2 sowie die Browser- und Screenreader-Prüfung der Tastaturalternative zum Themenverschieben sind offen. Browserunterschiede, PDF-Viewer-Grenzen und inkonsistente Daten bei Teilausfällen bleiben relevante Risiken. Es gelten die Sicherheits- und Qualitätsanforderungen der [Definition of Done](DEFINITION-OF-DONE.md).

## Dokumentationsregeln

- **MASTERPLAN.md:** ausschließlich aktueller Stand und gültige Planung.
- **TICKETS.md:** Aufgaben, Status, Abhängigkeiten, Abnahmekriterien und Nachweise.
- **DEFINITION-OF-DONE.md:** verbindliche Qualitätskriterien.
- **Git:** technische Änderungshistorie.

Keine parallelen Projektchroniken führen. Historische Detailpläne und Vergleiche dienen als Nachweise und Originalreferenz, nicht als aktueller Projektstand.
