# VIA.MiniDocs – Masterplan

Stand: 22.11.2026. Dieser Plan ist der zentrale Einstieg und steuert den Umfang über den [Ticketkatalog](TICKETS.md) und die [Definition of Done](DEFINITION-OF-DONE.md). Die vorhandenen Detailpläne bleiben gültig: [UI-Inventar](architecture/KU-UI-INVENTORY.md), [Frontend-Architektur](architecture/FRONTEND-ARCHITECTURE.md), [KU-Migrationsplan](architecture/KU-MIGRATION-PLAN.md), [M1-Vergleich](architecture/M1-DESIGNVERGLEICH.md) und [M2-Vergleich](architecture/M2-DESIGNVERGLEICH.md). Inventar und Architekturentwurf enthalten historische Bestandsangaben; für den heutigen Status gelten Git-Historie und dieser Plan. Die Originaldateien unter `.local-reference/` sind lokale, nicht versionierte Referenzen; ihre HTML-Datei ist die verbindliche Design- und Verhaltensreferenz. Das Begleitdokument unterscheidet den laufenden Prototyp, einen Datenmodellentwurf und zusätzliche Feature-Ideen.

## Ziel und Grenzen

MiniDoc soll handschriftliche Besprechungs- und persönliche Notizen als PDFs mit Themenhierarchie, Tags und seitenweise bearbeitbaren Transkripten verwalten und wieder auffindbar machen. Erstes Ziel ist die originalgetreue React-Demo für einen Nutzer, danach die gesondert beauftragte dauerhafte Datenanbindung. Der HTML-Prototyp verwendet simulierte Scan-Vorschauen und flüchtigen Speicher; Word-Import ist nicht implementiert. Erweiterungsideen wie KI-Sammelscan-Zerlegung, semantische Suche, Versionshistorie oder Mehrbenutzerbetrieb sind nicht automatisch Bestandteil des Prototypumfangs. VIA.Docs selbst, seine Server und bestehende Datenbanken werden nicht verändert.

## Festgelegte Architektur

React/TypeScript strict mit Vite und CSS Modules bleibt das Frontend. Komponenten bleiben klein; Fachlogik, UI-Zustand und Datenzugriff werden getrennt. Keine zusätzliche State-Bibliothek ohne nachgewiesenen Bedarf; kein `dangerouslySetInnerHTML` und keine DOM-Manipulation als Ersatz für React-Rendering. Die Demo hält Daten vorerst im Arbeitsspeicher.

Später: **React → ASP.NET Core MiniDocs API (.NET 10) → PostgreSQL** für Themen, Tags, Transkripte, Metadaten und Suche; die API spricht zusätzlich **VIA.Docs V1.2 REST** für Originaldateien, Dateiabruf und Vorschaubilder an. VIA.Docs nutzt Application-Authentifizierung mit JWT und vorhandener Einladungsregistrierung. Einzelheiten des tatsächlichen Vertrags und Tokenlebenszyklus müssen verifiziert werden; der Datenmodell- und API-Vorschlag im Begleitdokument ist kein bestätigter Vertrag. Anmeldedaten und Secrets bleiben serverseitig. Keine produktiven VIA.Docs-Zugriffe ohne gesonderten Testauftrag. Die konkrete Implementierung und Testumgebung der späteren Anbindung sind gesondert zu beauftragen.

## Entwicklungsstand

| Meilenstein | Stand und Abnahme |
|---|---|
| M1 Grundlayout | Technisch abgeschlossen, Commit `b8a657b`; quellenbasiert verglichen, **visuelle Browserabnahme offen**. |
| M2 Themenbaum | Technisch abgeschlossen, Commit `bf29b663`; Hierarchie, Zähler, Pfad, Dialoge und Themenverschiebung vorhanden; **visuelle Browserabnahme offen**. ZIP und wartende Transkripte gehören zu M7; Tastaturalternative für Themenverschiebung ist offen. |
| M3 Listen-/Kachelansicht | Nächstes geplantes Ticket; noch nicht implementiert. |
| M4–M8 | Geplant, siehe Tickets. |
| Echte Datenanbindung | Geplant; kein Backend, keine Datenbank oder VIA.Docs-Verbindung implementiert. |

M1 und M2 werden wegen ausstehender Abnahmen oder späterer Erweiterungen nicht wieder geöffnet. Technisch abgeschlossen ist nicht gleich vollständig nach heutiger UI-DoD abgenommen; die offenen Prüfungen werden ausdrücklich in M8 nachverfolgt.

## Phasen, Abhängigkeiten und Abnahme

| Phase | Reihenfolge / Abhängigkeit | Abnahmekriterium |
|---|---|---|
| Vorbereitung | abgeschlossen (`cf5cb29`) | Regeln, Inventar und Entwurf vorhanden; Referenzen bleiben lokal. |
| KU-Demo M1–M8 | M1 → M2 → M3 → M4 → M5 → M6 → M7 → M8; frühe Dialogbausteine dürfen bedarfsweise entstehen. | Je Paket die Ticketkriterien; M8 vergleicht das gesamte Inventar und klärt M1-/M2-Sichtprüfung. M6 nutzt Demo-/lokale PDF-Pfade; M7 ergänzt Importe. |
| Datenanbindung D1–D8 | Nach gesondertem Auftrag: Verträge/API und Datenmodell als Grundlagen; VIA.Docs-Client und Authentifizierung vor Dateiflüssen; Persistenz und Suche vor Ende-zu-Ende-Abnahme. Die detaillierten Kanten stehen je Ticket. | API-, Datenbank-, Sicherheits- und Integrationstests in freigegebener Testumgebung; UI-Funktionen mit echter Persistenz geprüft, keine produktiven Zugriffe ohne Auftrag. |
| Betrieb | Durch D8 abgedeckt, nach integrationsfähiger Gesamtlösung. | Sicherheits-, Backup-/Restore-, Monitoring- und Wiederanlaufnachweise gemäß DoD; Produktivfreigabe ist eine eigene Entscheidung. |

Kritischer Pfad als Planungsannahme: M3 → M4 → M5 → M6 → M7 → M8; für die echte Anbindung D1 + D2 → D5/D6/D7, wobei D3 + D4 für VIA.Docs-Dateiflüsse benötigt werden → D8. D1, D2 und die Prüfung von D3/D4 können nach Auftrag teilweise parallel vorbereitet werden; die tatsächliche Terminreihenfolge hängt von Verträgen und Testumgebung ab. M8 und die Integration sind getrennte Abnahmen.

## Sicherheit, Qualität und offene Entscheidungen

[AGENTS.md](../AGENTS.md) und die [DoD](DEFINITION-OF-DONE.md) gelten verbindlich. Wichtige Interaktionen werden getestet; keine Geheimnisse, Referenzdateien oder Build-Artefakte in Git. Keine neuen Abhängigkeiten ohne konkreten Bedarf. Für spätere Tests sind synthetische Daten und eine ausdrücklich freigegebene Umgebung vorzusehen.

Offen: tatsächliche VIA.Docs-Endpunkte, Berechtigungen, Einladungs- und JWT-Lebenszyklus; API-Verträge und Hosting; Mapping von VIA.Docs-IDs und Vorschaugrößen; Konflikt-/Fehler- und Wiederanlaufverhalten bei PDF-Upload über zwei Systeme; Backup-/Restore-Verantwortung für PostgreSQL und VIA.Docs; Suchsemantik und Indexstrategie gegenüber der HTML-Teilstringsuche; geeignete PDF-Darstellung und Grenzen der Scrollsynchronisation. PostgreSQL-Volltext/Trigramme im Begleitdokument sind Vorschläge, keine bestätigten Messwerte oder API-Zusagen. Außerdem: Termin und Umgebung für die visuellen M1-/M2-Abnahmen und die Tastaturalternative zum Themenverschieben.
