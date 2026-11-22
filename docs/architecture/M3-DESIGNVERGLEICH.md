# M3 – Quellenvergleich und offene Abnahme

Referenz: lokale KU-HTML-Datei, CSS Zeilen 107–155 und Darstellung in `renderContent` (770–819). Keine lokale Originaldatei veröffentlicht.

Übernommen: Ordner vor Dokumenten, nur direkte Unterthemen, rekursive Dokumentzähler; Tabellenstruktur mit Auswahlplatz, Vorschau, Titel/Dateiname, Datum, Seiten, Tags, Transkript und Aktionen. Header 8/10 px, Zellen 7/10 px; Miniatur 30 × 42 px; Rahmen und Hoverfarben aus dem Original. Kacheln mit 190 px Mindestbreite, 16 px Abstand, A4-Vorschaufläche 210/297, Radius 10 px, Metadatenpadding 8/10/10 px, Titel 13 px/600 und Unterzeile 12 px. Ordnerverlauf und Ordner-/Tag-/Tab-/Download-Icons entsprechen den Referenzpfaden. Bestehende zentrale Tokens und globale Fokusregel bleiben erhalten.

Die Anwendung startet wie das Original mit aktiver Kachelansicht; die bisherige Startansicht-Abweichung ist behoben. Ein automatisierter Test prüft die aktive Schaltfläche und die initialen Dokumentkacheln. Ansicht ist ein einzelner App-State; beide Darstellungen erhalten dieselben abgeleiteten Daten. Ordnernavigation aktualisiert dieselbe Themenauswahl wie der Baum. Datumsformat ist TT.MM.JJJJ, fehlende und ungültige Kalenderdaten zeigen „ohne Datum“. Transkriptstatus unterscheidet fehlt/vorhanden/bearbeitet, fehlender Status bedeutet fehlt. Optionale Metadaten und rein synthetische Demo-Vorschau sind im Frontendmodell getrennt von künftigen Dateien.

## Bewusste Abweichungen und Grenzen

- Dokument- und Ordnerkacheln verwenden für mehr Kontrast und Tiefe einen blaugrauen Rahmen (`#b9c6d2`) und einen dezenten dauerhaften Schatten; Hover verstärkt den Schatten. Diese gezielte visuelle Verbesserung ist freigegeben; Auswahlring, Fokus und Drop-Markierung bleiben erhalten. Eine visuelle Browserabnahme steht aus.
- Kleiner bisheriger Demo-Datensatz, einfache synthetische SVG-Notizen statt des vollständigen Original-Scan-Generators; keine echten PDF-Vorschauen.
- Ordner als native Buttons mit einfachem Klick bzw. Enter/Leertaste statt ausschließlich Doppelklick. Dokumente öffnen noch keinen Viewer.
- Auswahl, Tags zuweisen, Tab und Download deaktiviert; Aktionen dauerhaft sichtbar statt nur bei Hover, damit der vorbereitete Zustand erkennbar bleibt. Keine Auswahl-/Sortier-/Zoomfunktion aus M4 implementiert.
- Typ ergänzend sichtbar; Tags bei vorhandenen Daten als Text statt vollständiger farbiger Tag-Chips (Tagverwaltung M5).
- Leerzustand erklärt die noch fehlenden Funktionen statt Upload/Drop vorzuschlagen. Kein falsches Funktionsversprechen.
- Grid-Minimum auf verfügbare Breite begrenzt, lange Texte umbrechbar; Tabelle mit horizontalem Scrollcontainer und 760 px Mindestbreite. Sticky-Header-Verhalten ist noch browserseitig zu prüfen.

## Nachweise und offene Abnahmen

Automatisierte Komponenten-/Interaktionstests prüfen Daten, Ansichten, Themenwechsel, direkte Ordner, Tastaturnavigation, leere Themen und Datumsfehler. Bestehende Tests bleiben erhalten; Textabfragen auf den eigentlichen Dokumenttitel eingegrenzt, weil Vorschauen ebenfalls Text enthalten.

Keine Browser- oder Screenreaderprüfung durchgeführt. Der bekannte Browserzugriff war durch Timeout/URL-Richtlinienunterstützung blockiert; kein Umgehungsversuch, keine erneute Prüfung in M3. Visueller Vergleich bei identischer Fenstergröße, Hover/Fokus, 320/768/1000 px, 200 % Zoom, lange Metadaten, Scroll-/Sticky-Verhalten und native Tastatur-/AT-Nutzung bleiben offen. Q1/M1/M2-Abnahmen bleiben ebenfalls offen. Quellcodevergleich ist keine pixelgenaue oder visuelle Freigabe.
