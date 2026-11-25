# Verbindliche Entwicklungsregeln

- React mit TypeScript und die vorhandene Vite-Struktur verwenden; keine parallele Anwendung oder unnötige Frameworkwechsel.
- Kleine Komponenten mit klarer Verantwortung; wiederverwendbare Fachlogik in Hooks und Services auslagern. Darstellung, Fachlogik, Zustand und Datenzugriff trennen.
- React-Rendering nicht durch direkte DOM-Manipulation umgehen. Für die Migration kein `dangerouslySetInnerHTML` verwenden. Browser-APIs über geeignete React-Refs und Effekte einsetzen.
- TypeScript strict beibehalten und nachvollziehbare Typen definieren; keine unnötigen Abhängigkeiten oder State-Bibliotheken einführen. Vorhandene CSS Modules bevorzugen.
- Wichtige Interaktionen sinnvoll testen und vorhandene funktionierende Implementierungen bewahren.
- Keine eigenmächtige UI-Neugestaltung: `.local-reference/MiniDoc_Prototyp.html` ist die verbindliche Referenz für Funktion, Bedienkonzept und Grundlayout. Gezielt freigegebene Verbesserungen von Kontrast, Tiefe, Lesbarkeit und visueller Qualität sind zulässig; Abweichungen kenntlich machen.
- Niemals Secrets oder produktive Zugangsdaten ins Frontend oder Git aufnehmen. Keine produktiven VIA.Docs-Zugriffe ohne gesonderten Testauftrag.
- Keine Änderungen außerhalb des Workspaces ohne ausdrückliche Freigabe. Lokale Originalreferenzen nicht veröffentlichen.

## Engineering-Standards

Bei jeder Implementierung [Engineering-Standards](docs/ENGINEERING-STANDARDS.md) berücksichtigen: bestehende Architektur respektieren, CSS-Struktur sauber halten, vermeidbare doppelte Styles vermeiden und Design-Tokens konsistent verwenden. Libraries nach nachgewiesenem Nutzen auswählen, Accessibility beachten, Änderungen auf Senior-Niveau prüfen und keine neuen vermeidbaren technischen Schulden einführen. Kommentierung und Dokumentation nach den dortigen Regeln pflegen.

Die dortigen Regeln zu CSS-Modulzuständigkeit, überprüfbaren Testaussagen, Lesbarkeit und fachlicher Kommentierung sind verbindlicher Bestandteil jeder Implementierungsprüfung. Offene manuelle Abnahmen dürfen nicht durch grüne Tests ersetzt werden.

Für Backendplanung und spätere Umsetzung gelten die [Backend-Architektur](docs/ARCHITECTURE.md), die [Architekturentscheidungen und ihr Freigabestatus](docs/DECISIONS.md) und die dort verlinkten Sicherheits- und Integrationsdokumente. Planung ist keine Implementierungsfreigabe.

Der [Dokumentationswegweiser](README.md#dokumentationswegweiser) unterscheidet aktuelle Regeln, offene Aufgaben und historische Nachweise. Historische Originalbeschreibungen überschreiben keine später bestätigten Entscheidungen oder den aktuellen Implementierungsstand.
