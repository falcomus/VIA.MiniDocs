# M2: Themenbaum – Quellenvergleich

Die Umsetzung wurde mit den Baum-, Kontextmenü- und Dialogregeln der lokalen HTML-Referenz anhand des Quellcodes verglichen. Ein Browservergleich bei identischer Fenstergröße und die noch offene M1-Browserabnahme stehen weiterhin aus; der bisherige Browserzugriff war gesperrt. Es wurde kein Sicherheitsmechanismus umgangen.

Der vorhandene kleine Demo-Datensatz ersetzt weiterhin die Originaldaten. Die Spezialansicht „Transkripte ohne PDF“ und der ZIP-Export sind späteren Paketen zugeordnet; der ZIP-Menüpunkt ist sichtbar deaktiviert. Themen können wie im Original per Drag-and-drop umgehängt werden. Die Tastaturalternative über Kontextmenü und Zielwahldialog ist seit Q1 implementiert; automatisierte Tests belegen Verschiebung, Zielschutz und Abbruch mit Fokusrückgabe in jsdom. Die echte Browser-/Screenreader-Abnahme dieser Alternative ist offen.

Zeilen- und Wurzelablage verwenden dieselbe interne Drag-Sitzung. Die lokale Quell-ID ist maßgeblich; fehlende oder abweichende übertragene IDs, Drops ohne aktive Sitzung sowie fachlich ungültige Ziele werden ohne Verschiebung mit Statusmeldung zurückgewiesen. Die gültige Wurzelablage bleibt erhalten. Synthetische Tests belegen diesen Vertrag, ersetzen aber keinen nativen Browser-DnD-Test. Die vollständige visuelle Prüfung einschließlich Drop-Markierung und Scrollverhalten ist ausstehend. Alle Änderungen liegen nur im Arbeitsspeicher.
## Freigegebene UX-Abweichung: Systemstatus

Das unveränderte Systemthema „Nicht zugeordnet“ zeigt im Baum und in der aktuellen Themenüberschrift bei null **direkt** zugeordneten Dokumenten ein Häkchen und „Alle zugeordnet“. Bei mindestens einem direkten Dokument erscheinen wieder Inbox-Symbol, ursprünglicher Name und Zähler. ID, Systemschutz, Anklickbarkeit, Drop und Undo bleiben unverändert; der Status wird aus den Dokumentzuordnungen abgeleitet. Die Kennzeichnung ist bewusst eine kleine freigegebene Abweichung vom KU-Original.

Grün `#17643d` ergibt rechnerisch gegen Weiß 7,18:1, Hover `#eef0f3` 6,28:1, Auswahl `#d5e7f2` 5,65:1 und Drop `#e4eff6` 6,14:1. Das ist keine visuelle oder Screenreader-Abnahme; diese bleibt offen. Tests verwenden synthetische Drag-Ereignisse, keinen nativen Browser-Drop.
