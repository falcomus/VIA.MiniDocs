# Prüfbericht – VIA.MiniDocs Demo-Datenbestand (30 PDFs)

Prüfstand: 25.11.2026. Ergebnis: PASS unter Berücksichtigung der absichtlich erzeugten Transkriptlücken.

## Prüfungen

- 30/30 unterschiedliche PDFs erfolgreich geöffnet (keine reparierten oder verschlüsselten PDFs; unterschiedliche SHA-256-Prüfsummen).
- 15/15 ursprüngliche PDFs und 15/15 ursprüngliche Transkripte bytegenau mit dem ersten ZIP verglichen.
- 207/207 PDF-Seiten entsprechen den Metadaten; alle 207 Seiten erfolgreich gerendert und auf nichtleeren sichtbaren Inhalt geprüft.
- 109/109 neue Seiten: extrahierter PDF-Text vollständig mit dem bei der Zeichnung erfassten Quelltext abgeglichen (Leerzeichen normalisiert), einschließlich der Seiten, deren JSON absichtlich nicht ausgeliefert wird.
- 27/27 Transkriptdateien: UTF-8 ohne BOM, gültiges JSON, unverändertes Schema, korrekter PDF-Dateiname, eindeutige aufsteigende Seitennummern innerhalb der PDF-Seitenzahl.
- 192 vorhandene, nichtleere und untereinander unterschiedliche Seitentranskripte; davon 190/190 mit der PDF-Textschicht übereinstimmend. Die beiden Bildseiten aus dem ursprünglichen Bestand wurden bytegenau übernommen; ihre Transkripte wurden bei der ursprünglichen Erstellung visuell kontrolliert, keine erneute OCR-Erkennung.
- 25 vollständige, 2 unvollständige und 3 fehlende Transkripte entsprechen der Sollverteilung; 15 PDF-Seiten ohne gelieferten Seitentext.
- 30/30 Metadateneinträge entsprechen dem bisherigen Schema, Titel und Seitenzahl stimmen mit den PDFs überein; ISO-Daten gültig.
- Textgrenzen aller Textseiten geprüft, keine Ersatzzeichen festgestellt. Alle 109 neuen Seiten in gerenderten Übersichten visuell kontrolliert; zusätzlich vier repräsentative Seiten in höherer Auflösung betrachtet (A5, A3, Rechnung und Handbuch).
- 187 Hochformatseiten und 20 Querformatseiten; Mischformatdatei mit vier unterschiedlichen Seitengrößen überprüft.
- ZIP-Inhalt und CRC-Integrität beim Verpacken geprüft.

Kein Import- oder Anwendungstest in VIA.MiniDocs; keine OCR-Erkennungsquote ermittelt. Die Fachtexte sind vollständig synthetisch und keine realen betrieblichen Vorgaben.

## Einzelprüfung

| PDF | PDF-Seiten | JSON-Seiten | Transkriptstatus | Fehlende Seiten | PDF-Größe (KiB) |
|---|---:|---:|---|---|---:|
| 01_Rechnung_Nordlicht.pdf | 1 | 1 | vollständig | – | 54.8 |
| 02_Memo_Dokumentenfreigabe.pdf | 1 | 1 | vollständig | – | 54.8 |
| 03_Notizbuch_Werkstatt.pdf | 2 | 2 | vollständig | – | 307.5 |
| 04_Datenschutz_Pruefvermerk.pdf | 2 | 2 | vollständig | – | 81.2 |
| 05_Angebot_Digitalisierungsprojekt.pdf | 3 | 3 | vollständig | – | 59.2 |
| 06_Servicebericht_Scanner.pdf | 3 | 3 | vollständig | – | 42.3 |
| 07_Besprechungsprotokoll_Nordlicht.pdf | 4 | 4 | vollständig | – | 43.6 |
| 08_Kurzanleitung_Dokumentensuche.pdf | 4 | 4 | vollständig | – | 61.3 |
| 09_Testbericht_Archivimport.pdf | 5 | 5 | vollständig | – | 45.1 |
| 10_Mustervertrag_Archivierung.pdf | 6 | 6 | vollständig | – | 87.2 |
| 11_Projektplan_Nordlicht.pdf | 7 | 7 | vollständig | – | 65.2 |
| 12_Schulungsunterlagen_Digitales_Archiv.pdf | 8 | 8 | vollständig | – | 67.6 |
| 13_Technische_Dokumentation_Uferbox.pdf | 10 | 10 | vollständig | – | 58.3 |
| 14_Forschungsbericht_Moorfink.pdf | 12 | 12 | vollständig | – | 99.0 |
| 15_Betriebshandbuch_Uferbox.pdf | 30 | 30 | vollständig | – | 112.4 |
| 16_Lieferschein_Nordlicht.pdf | 1 | 0 | fehlt | 1 | 54.3 |
| 17_Rechnung_Nordlicht_Korrektur.pdf | 2 | 2 | vollständig | – | 56.2 |
| 18_Besprechungsprotokoll_Nordlicht_November.pdf | 4 | 4 | vollständig | – | 42.4 |
| 19_Besprechungsprotokoll_Moorfink.pdf | 3 | 3 | vollständig | – | 41.1 |
| 20_Migrationskonzept_Nordlicht.pdf | 6 | 2 | unvollständig | 3, 4, 5, 6 | 62.1 |
| 21_Qualitaetsbericht_Dokumentensuche.pdf | 5 | 5 | vollständig | – | 44.2 |
| 22_Raumbelegungsplan_Archiv.pdf | 3 | 0 | fehlt | 1, 2, 3 | 57.5 |
| 23_Abnahmeprotokoll_Uferbox.pdf | 2 | 2 | vollständig | – | 38.5 |
| 24_Sicherheitsaudit_Archiv.pdf | 8 | 4 | unvollständig | 2, 4, 6, 7 | 89.6 |
| 25_Messblatt_Moorfink_Mischformat.pdf | 4 | 4 | vollständig | – | 84.4 |
| 26_Schulung_Datenschutz.pdf | 7 | 7 | vollständig | – | 65.6 |
| 27_Wartungscheckliste_Q7.pdf | 3 | 0 | fehlt | 1, 2, 3 | 56.9 |
| 28_Angebot_Archivservice.pdf | 6 | 6 | vollständig | – | 61.9 |
| 29_Forschungsbericht_Moorfink_Nachmessung.pdf | 5 | 5 | vollständig | – | 84.7 |
| 30_Archivhandbuch_Wellenhain.pdf | 50 | 50 | vollständig | – | 142.8 |

## Sollwerte für Volltextsuchen

Einfache Unicode-Teilstringsuche ohne Beachtung der Groß-/Kleinschreibung (`casefold`), nur in gelieferten Seitentranskripten einschließlich Kopf- und Fußzeilen, keine Wortstammerkennung, keine Metadaten, kein PDF-Text-Fallback. Dokumente und Seiten werden pro Begriff jeweils einmal gezählt. Fehlende Transkriptseiten erzeugen dabei keine Treffer.

| Suchbegriff | Dokumente | Seiten |
|---|---:|---:|
| Nordlicht | 18 | 49 |
| Archivierung | 13 | 21 |
| Dokumentensuche | 16 | 33 |
| Qualitätssicherung | 17 | 33 |
| Datenschutz | 12 | 25 |
| Freigabe | 23 | 67 |
| Rückmeldung | 15 | 30 |
| Löschkonzept | 11 | 14 |
| Müllerstraße | 7 | 7 |
| Moorfink | 7 | 27 |
| Kalibrierung | 8 | 14 |
| Wiederanlauf | 4 | 12 |
| Transkript | 17 | 43 |
| Uferbox | 3 | 42 |
| UnbekanntesSuchwortXYZ | 0 | 0 |
