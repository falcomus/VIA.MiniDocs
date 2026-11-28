# Engineering-Standards

Diese Standards ergänzen [AGENTS.md](../AGENTS.md), die [Architektur](ARCHITECTURE.md) und die [Definition of Done](DEFINITION-OF-DONE.md). Der [Qualitätsaudit](architecture/REACT-QUALITY-REVIEW.md) dokumentiert historische Befunde und Vorschläge; aktuelle Aufgaben stehen in den Tickets. Das lokale KU-Original bleibt für Funktion, Bedienkonzept und Grundlayout verbindlich. Gezielt freigegebene Verbesserungen von Kontrast, Tiefe, Lesbarkeit und visueller Qualität sind zulässig; Abweichungen sind zu dokumentieren.

## React-Architektur und Zustand

- Verantwortlichkeiten anhand konkreter Aufgaben trennen: Darstellung, fachliche Zustandsänderung und Datenzugriff müssen einzeln nachvollziehbar und angemessen testbar sein. Komponenten/Hooks erst auslagern, wenn Verantwortung oder Wiederverwendung es begründet.
- React/TypeScript/Vite beibehalten; keine direkte DOM-Manipulation als Ersatz für React-Rendering und kein `dangerouslySetInnerHTML` zur UI-Migration. Refs und Effekte für Browser-APIs wie Fokus sind zulässig; Listener/Objekt-URLs aufräumen.
- Zustand nahe am Bedarf halten; vorhandene Daten und Filter als Quelle für abgeleitete Werte verwenden. Keine doppelten synchronisationspflichtigen Kopien ohne Begründung. `useState`/`useReducer` reichen bis zum konkreten Gegenbeweis; kein zusätzliches State-Framework aus Gewohnheit.
- Effekte nur zur Synchronisation mit externen Systemen einsetzen; Berechnungen im Render oder in reinen Funktionen. Für neue Hooks klaren Vertrag und Fehler-/Aufräumverhalten dokumentieren und relevante Zustandsübergänge testen.

## TypeScript und Schnittstellen

- `strict` und vorhandene Compilerprüfungen beibehalten; Props, Zustandsvarianten und Datenzugriffsgrenzen explizit typisieren. `any` und unbegründete Assertions vermeiden; externe Eingaben an der Grenze validieren.
- Null-/Fehlerzustände, Datums- und ID-Verträge passend zum tatsächlichen Fachmodell ausdrücken; keine Komplexität durch Branding oder Generics ohne belegten Nutzen. Fachliche Operationen als reine Funktionen isolieren, sofern praktikabel.

## CSS und Design-Tokens

- `global.css` enthält Reset, bestehende globale Tokens und Dokument-/Body-Grundregeln. Komponentenstyles bleiben in zugehörigen CSS Modules. Gemeinsame Styles nur bei echtem Wiederverwendungsbedarf; keine neue globale Utility-Sammlung und kein paralleles Styling-System.
- Bestehende Tokens für dieselbe semantische Bedeutung nutzen; lokale Originalwerte beibehalten, wenn sie fachlich anders gemeint sind. Neue Tokens für Farben, Abstände, Typografie, Radien, Schatten, Layer oder Breakpoints nur bei nachweislicher Wiederverwendung einführen; keine zweite Token-Hierarchie.
- Selektoren auf Komponenten begrenzen, unnötige Spezifität und Reihenfolgeabhängigkeiten vermeiden. Wiederholte Regeln konsolidieren, wenn Verhalten gleich bleibt. Keine unnötigen `!important`-Overrides; begründete Ausnahmen dokumentieren. Zustände, schmale Fenster, lange Inhalte und Scrollcontainer prüfen.
- `frontend/stylelint.config.js` definiert die verbindlichen CSS-Prüfungen. `npm run lint:css` ist Bestandteil von `npm run check`. Geprüft werden unter anderem unbekannte Eigenschaften und At-Rules, ungültige Werte, doppelte Deklarationen/Selektoren, `!important`, unbekannte Pseudoklassen und Spezifität. Bei CSS Modules sind nur `@value`, `composes`, `:global` und `:local` gezielt zugelassen; für das globale Vite-`#root` gilt eine eigene Spezifitätsgrenze. Änderungen an Regeln oder Ausnahmen brauchen einen konkreten Grund; keine pauschale Abschaltung für grüne Builds.

## Accessibility und Tests

- Semantische HTML-Elemente, zugängliche Namen und sichtbaren Fokus verwenden. Neue Dialoge/Menüs müssen Fokus, Escape, Rückkehr zu einem existierenden Ziel und Hintergrundverhalten abdecken. Für Drag-and-drop eine bedienbare Tastaturalternative und verständliche Rückmeldung vorsehen.
- Kontraste, Zoom, reduzierte Bewegung und betroffene Viewports anhand tatsächlicher Zustände prüfen. Visuelle Referenz- und Screenreader-Prüfungen nicht durch jsdom-Ergebnisse ersetzen.
- Tests für fachliche Regeln und nutzerrelevante Interaktionen ergänzen; Fehler-/Schutzfälle prüfen. Bestehende Tests bewahren. Native Browserabläufe wie Drag-and-drop, PDF und Scrollen im Browser prüfen; Tests sollen beobachtbares Verhalten absichern.

## Performance und Dependencies

- Zunächst Datenmengen und Engpass messen; Indizes, Memoisierung oder Virtualisierung nur bei belegtem Bedarf. Teure wiederholte Baum- und Listenoperationen bei Wachstum im Blick behalten.
- Bibliotheken anhand konkreter Probleme, Lizenz, Wartung, Kompatibilität, Typen, Accessibility, Bundlewirkung und Migrationsrisiko vergleichen. Kleine fokussierte Pakete bevorzugen, wenn sie nachweisbar helfen; vor Einführung fachlich freigeben. Keine Package-Installation oder Refactor allein aufgrund des Audits.

## API-Integration und Security

- React spricht künftig die MiniDocs-API an; VIA.Docs-Credentials bleiben serverseitig. Keine produktiven VIA.Docs-Zugriffe ohne gesonderten Testauftrag. Verträge, Autorisierung, Fehlerfälle und Timeouts an der Servicegrenze prüfen; keine Secrets oder lokale Referenzen in Git oder Logs.
- Testdaten und freigegebene Testumgebungen verwenden. Persistenz-/Importvorgänge über mehrere Systeme benötigen definierte Teilfehler- und Wiederanlaufregeln.
- Für spätere Backend-Arbeit sind die freigegebenen [ADRs](DECISIONS.md) verbindlich: Identity mit Cookie und Antiforgery, serverseitige Workspace-Autorisierung in mehreren Ebenen, getrennte technische Identitäten und geschützte Schlüsselverwaltung. ADR-014 bis ADR-016 legen Projektgrenzen, Minimal APIs und .NET 10 mit EF Core/Npgsql sowie weitere Engineering-Grundlagen fest; konkrete Paketversionen erst nach Kompatibilitätsprüfung. Sicherheitsnachweise und Testanforderungen stehen in der [Sicherheitsarchitektur](ARCHITECTURE.md). Die Architekturfreigabe ist keine Implementierungsfreigabe.

## Code-Kommentierung und Dokumentation

- Verständlichkeit zuerst durch klare Namen, kleine Funktionen und nachvollziehbare Verantwortlichkeiten erreichen.
- Wichtige exportierte Funktionen, Custom Hooks und fachliche Schnittstellen erhalten aussagekräftige TSDoc-Kommentare, wenn ihr Vertrag nicht offensichtlich ist. Komplexe Algorithmen erklären fachliche Regeln und nicht offensichtliche Entscheidungen.
- Kommentare erklären bevorzugt **warum**, nicht nur **was** der Code bereits zeigt. CSS-Kommentare erläutern besondere Layoutentscheidungen, Abhängigkeiten und begründete Ausnahmen.
- Keine überflüssigen Zeilenkommentare, auskommentierten Codeleichen oder vom Code abweichenden Kommentare belassen. Bei Änderungen betroffene Dokumentation aktualisieren.
- Relevante Architekturentscheidungen in kurzen ADRs festhalten, wenn eine einfache Erklärung im Code nicht ausreicht; Kontext, Entscheidung und Konsequenzen nennen.

## Senior-Review-Checkliste

1. Verantwortlichkeiten, Datenfluss und Zustandsübergänge verständlich? Grenzen und Fehlerfälle typisiert und getestet?
2. CSS lokal, Tokens semantisch konsistent, keine vermeidbaren Doppelregeln oder unbegründeten globalen/`!important`-Overrides? Originaltreue und Responsive-Verhalten geprüft?
3. Tastatur, Fokus, Semantik, Kontrast und relevante Assistive-Technologie-/Browserfälle berücksichtigt? Offene Abnahmen klar ausgewiesen?
4. Tests sichern relevantes Verhalten, bestehende Funktionen bleiben erhalten und Leistungsentscheidungen sind belegt?
5. Abhängigkeiten und Sicherheitsgrenzen fachlich begründet? Dokumentation, TSDoc, Kommentare und gegebenenfalls ADRs korrekt, aktuell und auf das Warum fokussiert?

## Architekturgrenzen, Testaussage und Lesbarkeit

- Ein CSS Module gehört zur verantwortlichen Komponente bzw. zum verantwortlichen Modul. Keine fremden Feature-CSS-Module zur bloßen Klassenwiederverwendung importieren; gemeinsame Styles benötigen eine begründete gemeinsame Zuständigkeit.
- Testtitel und Assertions müssen denselben Funktionsumfang beschreiben. Ein Callback-Test belegt keine vollständige Zustandsänderung oder Schutzregel; bestehende Integrationstests gezielt nutzen statt duplizieren.
- Code und CSS gut lesbar und nachvollziehbar formatieren. Zusammenhängende Entscheidungen und komplexe fachliche Regeln sinnvoll kommentieren; keine globale Umformatierung ohne Auftrag.
- Nicht nachgewiesene Qualitätsanforderungen bleiben offen, auch bei grünen Tests. Hooks, native Dialoge und weitere Abstraktionen nur nach konkretem Nutzen wählen; Originalfarben nicht allein aus einer allgemeinen Tokenregel ersetzen.

## Automatische Frontend-Formatierung

Prettier 3.10.1 ist als feste Entwicklungsabhängigkeit eingerichtet. `npm run format` schreibt die Formatierung, `npm run format:check` prüft sie ohne Änderungen und ist verbindlicher erster Schritt von `npm run check`. ESLint und Stylelint bleiben für Code-/CSS-Qualitätsregeln zuständig; es werden keine konkurrierenden Formatierungsplugins eingeführt.

Die Frontend-Konfiguration `.prettierrc.json` legt Single Quotes, 100 Zeichen Zielzeilenlänge und LF fest. Die bestehende `.editorconfig` liefert UTF-8 und zwei Leerzeichen Einrückung. Umfang: `src/**/*.{ts,tsx,css}`, Konfiguration und Einstieg im Frontendroot (`*.{js,ts,json,html}`) sowie `.prettierrc.json`. `.prettierignore` schließt Abhängigkeiten, Build-/Coverage-Ausgaben, Deklarationsdateien, Lockfile und lokale Originalreferenzen aus. Neue generierte Dateitypen müssen bei Bedarf ausdrücklich ergänzt werden. Dokumentation und Dateien außerhalb des Frontends gehören nicht zum Format-Script.

Formatierungsänderungen und fachliche Änderungen in Reviews auseinanderhalten. Nach der einmaligen Einführung keine zusätzlichen Refactorings in Formatierungs-Diffs verstecken.

## EF Core und PostgreSQL

- Reine Leseabfragen mit Entitätsmaterialisierung grundsätzlich `AsNoTracking()`; reine skalare/DTO-Projektionen ohne Entitäten benötigen kein zusätzliches Tracking-Verbot. Enthält eine Projektion Entitäten, Tracking gesondert beurteilen. `AsTracking()` nur für bewusst nachverfolgte Schreibpfade; keine globale Umstellung ohne Prüfung von Identity und Schreibpfaden. `AutoDetectChangesEnabled` standardmäßig beibehalten, temporäre Änderungen nur gemessen und mit sicherer Rücksetzung. Die zentrale `Visible()`-Dokumentabfrage beibehalten.
- Benötigte Spalten gezielt projizieren; Navigationen nur bei Bedarf laden. `AnyAsync()` für Existenzprüfungen, Listen begrenzen und eindeutig sortieren (Cursor-Vertrag beachten). Kein Lazy Loading; N+1 und unnötige Roundtrips vermeiden. `AsAsyncEnumerable()` nur bei geeignetem Streaming unter Beachtung der Reader-, Verbindungs- und Kontextlebensdauer. Keine parallelen Operationen auf demselben DbContext; CancellationToken weiterreichen.
- `AsSplitQuery()` bei mehreren Collection-Navigationen und plausibler oder nachgewiesener kartesischer Vervielfachung prüfen, insbesondere bei D7. Roundtrips, Pufferbedarf und Snapshot-Konsistenz berücksichtigen: unter PostgreSQL `ReadCommitted` können Teilabfragen unterschiedliche Snapshots sehen. Keine globale Split-Query-Umstellung. `AsNoTrackingWithIdentityResolution()` nur bei benötigter Objektidentität oder gemessenem Vorteil; vorkompilierte Abfragen nur für gemessene Hot Paths.
- Workspace-Isolationsfilter niemals aus Komfort- oder Performancegründen deaktivieren. Fachlich zwingende Ausnahmen bei `IgnoreQueryFilters()` brauchen eine explizite Sicherheitsentscheidung, Autorisierungsnachweis und Isolationstests; Raw SQL benötigt eigene Workspace-Prädikate. `ExecuteUpdateAsync()`/`ExecuteDeleteAsync()` dürfen versionierte PostgreSQL-Schreibgrenzen, Lock-Reihenfolge, ETags, Atomarität und Runtime-Rechte nicht umgehen; keine Rechteerweiterung dafür.
- Kein DbContext-Pooling mit dem zustandsbehafteten `AuthorizedWorkspaceId` ohne belegten Nutzen, sicheren Reset-/Bindungslebenszyklus und workspaceübergreifende Wiederverwendungstests. PostgreSQL-Verbindungspooling ist davon unabhängig.
- Vor Optimierung repräsentative Dokumentlisten, Filter, SQL-Pläne und Batch-Roundtrips/Workspace-Sperrzeiten messen; danach Indexstrategie, D7-Transkripte, Reload/Tab-Abgleich und EF-Feinoptimierungen beurteilen. `TagWith()` und `ToQueryString()` ohne Secrets/personenbezogene Werte verwenden. `EXPLAIN (ANALYZE, BUFFERS)` führt SQL tatsächlich aus; Messungen isoliert, keine produktiven Schreibzugriffe. Vor-/Nachher-Vergleich von Antwortzeiten, SQL-Anzahl, Datenmenge und gegebenenfalls Sperrzeiten; Sicherheits- und Integritätstests bleiben verbindlich.
- Keine vorsorglichen EF-Schalter oder zusätzlichen Bibliotheken allein für theoretische Performancegewinne. Datenintegrität, Workspace-Isolation und Wiederherstellbarkeit haben Vorrang. D6-Textvalidierung folgt dem Unicode-Codepoint-Vertrag von PostgreSQL/OpenAPI.

## Regression als Freigabevoraussetzung

Vor der Änderung Funktionsumfang und Testbestand erfassen. Fehler möglichst zuerst durch einen fehlschlagenden Test reproduzieren. Nach der Änderung Bestand und Ergebnis vergleichen. Das Entfernen oder Abschwächen eines Bestandstests braucht eine ausdrückliche fachliche Begründung. Freigegebene Designs und Bedienabläufe erhalten und Änderungen auf den Auftrag begrenzen. Fehlgeschlagene oder ausstehende Pflichtprüfungen sperren die Freigabe. Referenzbilder nur nach ausdrücklicher Abnahme übernehmen. Abweichungen führen zu einem Prüfungsfehler.
