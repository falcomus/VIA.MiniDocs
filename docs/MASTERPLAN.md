# VIA.MiniDocs – Masterplan

Dieser Masterplan ist der zentrale Einstieg für den aktuellen Projektstand und die gültige Planung. Es gelten die [Entwicklungsregeln](../AGENTS.md), die [Engineering-Standards](ENGINEERING-STANDARDS.md), die [Frontend-Architektur](architecture/FRONTEND-ARCHITECTURE.md), das [UI-Inventar](architecture/KU-UI-INVENTORY.md) und der [KU-Migrationsplan](architecture/KU-MIGRATION-PLAN.md). Die lokale Original-HTML-Datei unter `.local-reference/` ist die verbindliche Design- und Verhaltensreferenz; sie wird nicht versioniert.

## Projektziel und Architektur

MiniDoc soll handschriftliche Besprechungs- und persönliche Notizen als PDFs mit Themenhierarchie, Tags und seitenweise bearbeitbaren Transkripten verwalten und durchsuchbar machen. Zunächst entsteht die originalgetreue React-Demo; die dauerhafte Datenanbindung wird gesondert beauftragt. Zusätzliche Ideen wie KI-Sammelscan-Zerlegung, semantische Suche und Mehrbenutzerbetrieb gehören nicht automatisch zum Prototypumfang.

Das Frontend verwendet React, TypeScript strict, Vite und CSS Modules. Kleine Komponenten trennen Darstellung, Fachlogik, Zustand und Datenzugriff. Die Demo verwendet Daten im Arbeitsspeicher. Weitere Abhängigkeiten, insbesondere State-Management-Bibliotheken, werden nur bei nachgewiesenem Bedarf eingeführt.

Das geplante Zielbild ist **React → ASP.NET Core MiniDocs API (.NET 10) → PostgreSQL** für Themen, Tags, Transkripte, Metadaten und Suche. Die MiniDocs-API bindet zusätzlich **VIA.Docs V1.2 REST** für Originaldateien, Dateiabruf und Vorschaubilder an. VIA.Docs verwendet Application-Authentifizierung, JWT und die vorhandene Registrierung per Einladung. Geheimnisse bleiben serverseitig; produktive VIA.Docs-Zugriffe benötigen einen gesonderten Testauftrag. VIA.Docs selbst und bestehende Datenbanken werden nicht verändert.

## Aktueller Stand und nächste Meilensteine

- M1 Grundlayout und M2 Themenbaum sind technisch abgeschlossen; ihre visuellen Browserabnahmen sind offen und werden in M8 nachverfolgt.
- Q1 verbessert die Tastaturbedienung und Dialogfokus technisch; Browser- und Screenreader-Prüfungen sind offen. M3 Listen- und Kachelansicht ist technisch umgesetzt; visuelle Abnahme offen. M4 Auswahl, Sortierung, Kachelzoom und Dokumentverschiebung mit Undo ist technisch umgesetzt; native Maus-DnD- und visuelle Abnahme offen. M5 mit Suche, Filtern und Tagverwaltung ist technisch umgesetzt; der vollständige visuelle und funktionale Vergleich bleibt offen. M6-A für Viewer und Navigation sowie M6-B für den Transkript-Editor sind technisch umgesetzt, M6-C und M7–M8 sind geplant gemäß [Ticketkatalog](TICKETS.md).
- Q10 bietet das zentrale Einstellungsmenü mit Hell-, Dunkel- und Systemdarstellung; visuelle und Screenreader-Abnahme sind offen.
- Q2 CSS-Qualitätsprüfung ist technisch umgesetzt; visuelle und Accessibility-Abnahmen bleiben offen.
- Q3 Modulgrenzen und Testaussagen sind technisch geprüft und korrigiert; Q4 automatisiert die Frontend-Formatierung; CSS-Typautomatisierung bleibt als Q5 geplant.
- Q6 sichert Dropmarkierung, interne Drag-Quellen und Wurzelablage mit gemeinsamem Sitzungsvertrag und gezielten Tests ab; Q8 setzt die Löschung ausschließlich leerer Themen technisch um; die Browser- und Sichtabnahme ist offen. Q5 bleibt separat geplant, Browser-/Screenreader-Abnahmen bleiben offen.
- Die KU-Vorgaben zu Importen, Papierkorb und sicherem Zugriff von unterwegs sind als Folgearbeiten im Ticketkatalog vorgesehen. Die Datenanbindung D1–D8 ist geplant. Backend, PostgreSQL-Anbindung und VIA.Docs-Verbindung sind noch nicht implementiert.

## Abhängigkeiten

Für die Demo ist die Reihenfolge M3 → M4 → M5 → M6 → M7 → M8 vorgesehen. M8 umfasst den funktionalen und visuellen Gesamtvergleich einschließlich der offenen M1-/M2-Abnahmen.

Für die spätere Datenanbindung bilden API-Verträge und Datenmodell die Grundlage. VIA.Docs-Client und Authentifizierung werden für die Dateiflüsse benötigt; Persistenz und Suche sind Voraussetzungen für den Ende-zu-Ende-Nachweis. Die konkreten Abhängigkeiten und Akzeptanzkriterien stehen im [Ticketkatalog](TICKETS.md). Integration und Betrieb erfolgen in einer freigegebenen Testumgebung; die Produktivfreigabe ist eine gesonderte Entscheidung.

## Offene Entscheidungen und Risiken

Zu klären sind die tatsächlichen VIA.Docs-Endpunkte, Berechtigungen und der Einladungs- und JWT-Lebenszyklus; MiniDocs-API-Verträge und Hosting; ID- und Vorschau-Mapping; Fehler- und Wiederanlaufverhalten bei Dateioperationen über zwei Systeme; Backup-/Restore-Zuständigkeiten; Suchsemantik und Indexstrategie sowie die PDF-Darstellung und Grenzen der Scrollsynchronisation. Der Datenmodell- und Suchentwurf des Begleitdokuments ist kein bestätigter API-Vertrag oder Performance-Nachweis.

Die visuelle Vergleichsumgebung für M1 und M2 sowie die Browser- und Screenreader-Prüfung der Tastaturalternative zum Themenverschieben sind offen. Browserunterschiede, PDF-Viewer-Grenzen und inkonsistente Daten bei Teilausfällen bleiben relevante Risiken. Es gelten die Sicherheits- und Qualitätsanforderungen der [Definition of Done](DEFINITION-OF-DONE.md).

## Dokumentationsregeln

- **MASTERPLAN.md:** ausschließlich aktueller Stand und gültige Planung.
- **TICKETS.md:** Aufgaben, Status, Abhängigkeiten, Abnahmekriterien und Nachweise.
- **DEFINITION-OF-DONE.md:** verbindliche Qualitätskriterien.
- **Git:** technische Änderungshistorie.

Keine parallelen Projektchroniken führen. Die bestehenden Detailpläne bleiben als fachliche und technische Referenzen gültig.
