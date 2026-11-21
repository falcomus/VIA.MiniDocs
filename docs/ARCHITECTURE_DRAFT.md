# Architektur – vorläufiger Entwurf

> Historischer Entwurf der Testbasis. Aktueller Vorbereitungsvorschlag und Referenzinventar: `architecture/FRONTEND-ARCHITECTURE.md` und `architecture/KU-UI-INVENTORY.md`.

## Bewusste Entscheidungen dieser Testbasis

- React Function Components und Hooks; TypeScript mit strenger Typprüfung.
- Kleiner, feature-orientierter Quellcode (`features/topics`, `features/documents`).
- Lokaler Zustand ausschließlich für die aktuelle Auswahl und die aufgeklappten Ordner.
- Beispieldaten in separaten Modulen. Keine API und keine vermeintliche Persistenz.
- CSS Modules; keine unnötigen UI-, Routing- oder State-Management-Abhängigkeiten.
- Frühe automatisierte Tests, Lint und Build-Kommando.

## Noch gemeinsam zu entscheiden

- ASP.NET-Core-Backend, PostgreSQL-Schema und VIA.Docs-API-Verträge.
- Authentifizierung und Hosting.
- React Router, TanStack Query und weitere Bibliotheken erst beim echten Bedarf freigeben.
- Import- und Konfliktregeln, Transkript-/PDF-Zuordnung sowie Sicherheit beim Löschen.
- PDF-Viewer, Drag & Drop, mehrstufige Importe und Suchverhalten.
- Welche Entwicklungs-/Verifikationsprofile Chat ABC für React benötigt.

Die Testbasis wurde ursprünglich ohne den HTML-Prototyp erstellt. Für die bevorstehende Migration ist das Original nun die verbindliche Design- und Verhaltensreferenz; der Prototyp-Code wird nicht direkt als DOM-Manipulation übernommen.
