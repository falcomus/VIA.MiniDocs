# VIA.MiniDocs – Fiktiver Demo-Datenbestand (Erweiterung auf 30 Dokumente)

30 PDFs, 207 PDF-Seiten, 27 Transkript-JSON-Dateien, 192 vorhandene Seitentranskripte. Alle Personen, Organisationen, Preise, Vorgänge und Messdaten sind erfunden; Ähnlichkeiten mit realen Personen oder Organisationen sind zufällig. Die Texte sind keine Funktionsbeschreibung oder Testnachweise einer realen VIA.MiniDocs-Version.

## Dateien und Import

- Die ursprünglichen PDFs 01–15 und ihre JSON-Transkripte wurden bytegenau übernommen. Die Dateien 16–30 sind neu.
- Alle PDFs und die zugehörigen Transkripte liegen auf derselben Archivebene. Die Dateistämme stimmen überein.
- `Dokumentenuebersicht.json` enthält genau 30 Einträge im bisherigen Format: `dateiName`, `titel`, `seitenzahl`, `datum`, `empfohlenesThema`, `tags`, `textart`.
- Die Transkripte sind UTF-8 ohne BOM und behalten exakt die bisherigen Felder `fileName`, `pages`, `page`, `text`. Seitennummern beginnen bei 1; vorhandene Einträge sind aufsteigend sortiert.
- `Dokumentenuebersicht.csv` ist die gleiche Übersicht als semikolongetrennte UTF-8-Datei mit BOM.
- `Pruefbericht.md` enthält die Einzelprüfung, die absichtlichen Lücken und aktualisierte Sollwerte für Suchtests.

## Absichtlich abweichende Transkripte

| PDF | Vorhandene Seiten | Fehlende Seiten |
|---|---|---|
| 16_Lieferschein_Nordlicht.pdf | keine JSON-Datei | 1 |
| 20_Migrationskonzept_Nordlicht.pdf | 1, 2 | 3–6 |
| 22_Raumbelegungsplan_Archiv.pdf | keine JSON-Datei | 1–3 |
| 24_Sicherheitsaudit_Archiv.pdf | 1, 3, 5, 8 | 2, 4, 6, 7 |
| 27_Wartungscheckliste_Q7.pdf | keine JSON-Datei | 1–3 |

Die fehlenden Einträge sind absichtlich nicht als leere Platzhalter enthalten. Bei den drei Dokumenten ohne JSON ist der PDF-Text trotzdem eingebettet und lesbar. Bei den Teiltranskripten sind die enthaltenen Seiten vollständig transkribiert; es fehlen ganze Seiteneinträge. Die PDF-Dateien selbst sind vollständig. `textart` bezeichnet weiterhin die PDF-Textschicht, nicht den Transkriptstatus. Der Status ergibt sich aus der Existenz der JSON-Datei und der Anzahl ihrer Seiteneinträge.

## Weitere Sonderfälle

- `03_Notizbuch_Werkstatt.pdf`: zwei bildbasierte Seiten ohne eingebetteten Text, beide transkribiert.
- `25_Messblatt_Moorfink_Mischformat.pdf`: Seite 1 A4 hoch, Seite 2 A4 quer, Seite 3 A5 hoch, Seite 4 A3 quer.
- `22_Raumbelegungsplan_Archiv.pdf` und `26_Schulung_Datenschutz.pdf`: durchgehend A4 quer, ebenso das ursprüngliche Dokument 12.
- `30_Archivhandbuch_Wellenhain.pdf`: 50 Seiten, 10 Kapitel mit je fünf unterschiedlichen Verfahrensblättern, vollständiges Transkript.
- `07_Besprechungsprotokoll_Nordlicht.pdf` und `18_Besprechungsprotokoll_Nordlicht_November.pdf`: identischer Dokumenttitel, unterschiedliche Dateien, Inhalte und Dokumentdaten (12.10.2026 bzw. 12.11.2026).
- `01_Rechnung_Nordlicht.pdf` und `17_Rechnung_Nordlicht_Korrektur.pdf`: Originalbeleg und separate fiktive Korrektur.
- Dokumentdaten: 17.06.2025 bis 24.11.2026.

Die im Prüfbericht angegebenen Suchtreffer beziehen sich ausschließlich auf die gelieferten Transkripte, ohne PDF-Text-Fallback und ohne Metadaten. Andere Suchsemantiken können andere Ergebnisse liefern.
