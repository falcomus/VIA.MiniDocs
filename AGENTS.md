# Verbindliche Entwicklungsregeln

- React mit TypeScript und die vorhandene Vite-Struktur verwenden; keine parallele Anwendung oder unnötige Frameworkwechsel.
- Kleine Komponenten mit klarer Verantwortung; wiederverwendbare Fachlogik in Hooks und Services auslagern. Darstellung, Fachlogik, Zustand und Datenzugriff trennen.
- React-Rendering nicht durch direkte DOM-Manipulation umgehen. Für die Migration kein `dangerouslySetInnerHTML` verwenden. Browser-APIs über geeignete React-Refs und Effekte einsetzen.
- TypeScript strict beibehalten und nachvollziehbare Typen definieren; keine unnötigen Abhängigkeiten oder State-Bibliotheken einführen. Vorhandene CSS Modules bevorzugen.
- Wichtige Interaktionen sinnvoll testen und vorhandene funktionierende Implementierungen bewahren.
- Keine eigenmächtige UI-Neugestaltung: `.local-reference/MiniDoc_Prototyp.html` ist die verbindliche Designreferenz. Farben, Abstände, Schriften, Icons, Controls, Dialoge und Bedienverhalten möglichst 1:1 übernehmen; Abweichungen kenntlich machen.
- Niemals Secrets oder produktive Zugangsdaten ins Frontend oder Git aufnehmen. Keine produktiven VIA.Docs-Zugriffe ohne gesonderten Testauftrag.
- Keine Änderungen außerhalb des Workspaces ohne ausdrückliche Freigabe. Lokale Originalreferenzen nicht veröffentlichen.

## Engineering-Standards

Bei jeder Implementierung [Engineering-Standards](docs/ENGINEERING-STANDARDS.md) berücksichtigen: bestehende Architektur respektieren, CSS-Struktur sauber halten, vermeidbare doppelte Styles vermeiden und Design-Tokens konsistent verwenden. Libraries nach nachgewiesenem Nutzen auswählen, Accessibility beachten, Änderungen auf Senior-Niveau prüfen und keine neuen vermeidbaren technischen Schulden einführen. Kommentierung und Dokumentation nach den dortigen Regeln pflegen.
