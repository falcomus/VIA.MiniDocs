> **Historischer, gesicherter B0-DOC-Ausgangsstand:** Nicht mehr aktiv pflegen. Maßgeblich ist [die konsolidierte Fassung](../VIA-DOCS-API.md); der ursprüngliche Inhalt ist nach diesem Hinweis vollständig erhalten.

# VIA.Docs V1.2 – Integrationsgrenze und Quellenstand

**Kein Deployment verifiziert.** [ADR-004 bis ADR-007](ARCHITECTURE-DECISIONS.md) legen serverseitiges REST und getrennte Einladungsverfahren fest. Keine produktiven Aufrufe im Rahmen von B0-DOC. Die folgende statische Betrachtung ist kein Produktionsvertrag oder Integrationstest.

## Herkunft und Einschränkung

Als Referenz wurde der VS2AI-Export vom 25.11.2026 mit Branchangabe `v1.2-s3-storage` und Commit `7ffed6ab7a1125c6f14c5f664f12ffefbeaf1da8` genannt. Lesend geprüft wurde das lokale Repository `C:\VIA_DEVELOPMENT\PROJECTS\VIA.Docs V1.2\VIA.Docs`: HEAD ist dieser Commit, der aktuelle Branch heißt jedoch `feature/v1.2-s3-storage`; der Arbeitsbaum enthält uncommittete Änderungen und neue Dateien. Historische Prüfstände und Deployment-Artefakte im übergeordneten Ordner sind keine aktuelle Solution.

**Am Commit bestätigt:** `Program.cs` registriert Application-, File- und Image-Endpunkte, aber nicht `MapDocumentEndpoints()`. Vorhandene Documents-Implementierungen sind damit kein dort aktiver öffentlicher Vertrag. Die File-Routen und das Application-Login unten sind im Commit registriert. **Nur im uncommitteten Arbeitsbaum bestätigt:** Die zwei Einladungsrouten und ihre unten beschriebenen Implementierungsdetails stehen in einer geänderten `ApplicationEndpoints.cs`; die benötigten Entity- und Migrationsdateien sind dort teilweise unversioniert. Sie sind nicht Bestandteil des angegebenen Commits und dürfen nicht als dort oder im Deployment verfügbar vorausgesetzt werden. Vor Integration müssen Änderungen, Migration und deployte Version überprüft werden.

## Statische Routenübersicht

| Methode und Pfad | Status im geprüften Quellstand |
| --- | --- |
| POST `/api/v1/applications/invitations` | Nur uncommitteter Arbeitsbaum; technische Einladung erzeugen. |
| POST `/api/v1/applications/register-invitation` | Nur uncommitteter Arbeitsbaum; technische Einladung einlösen. |
| POST `/api/v1/applications/login` | Am Commit registriert; ApplicationName und Secret. |
| GET `/api/v1/files` | Am Commit registriert; Dateiübersicht. |
| POST `/api/v1/files` | Am Commit registriert; `multipart/form-data`, Feld `file`. |
| GET `/api/v1/files/{id}` | Am Commit registriert; Metadaten. |
| GET und HEAD `/api/v1/files/{id}/content` | Am Commit registriert; Originalinhalt. |
| GET `/api/v1/files/{id}/preview` | Am Commit registriert; Vorschau. |
| DELETE `/api/v1/files/{id}` | Am Commit registriert; Löschen. |
| GET `/api/v1/files/stats` | Am Commit registriert; Statistik. |

Die zusätzlich vorhandene direkte Application-Registrierung ist nicht der vorgesehene MiniDocs-Provisionierungsweg. Im uncommitteten Einladungsentwurf sind Tokens einmalig und 24 Stunden gültig; Erzeugung prüft einen administrativen Registrierungsschlüssel, Einlösung benötigt diesen nicht. Einladungsverbrauch und Registrierung erfolgen dort in einer PostgreSQL-Transaktion. Die tatsächliche Verfügbarkeit und das Verhalten müssen erst nachweislich bestätigt werden. JWTs werden pro Anwendung getrennt verwaltet; Erneuerung über reguläre Anmeldung, Ablauf und parallele Erneuerungen berücksichtigen. Eine Rotation des Application-Secrets ist nicht als aktive API belegt.

## Autorisierter PDF-Abruf

Das eigene Backend autorisiert jeden Abruf vor Weiterleitung. Keine vollständige PDF-Pufferung als `byte[]`; unterstützte Range-Requests weiterleiten, HTTP 200, 206, 304 und 416 sowie relevante Content-, Range- und Cache-Header korrekt behandeln. Cancellation und Stream-Lebensdauer durchgängig beachten, keine mandantenübergreifende Cache-Wiederverwendung. Tatsächliches Range- und Headerverhalten des deployten Dienstes und der MiniDocs-Grenze nachweisen; Speicherverbrauch großer PDFs messen. Die VIA.Docs-Dateimetadaten liefern bisher keine zuverlässig verfügbare Seitenzahl; Herkunft und Aktualisierung sind im Modell zu klären. Bestehender React-PDF-Viewer bleibt fachliche und visuelle Grundlage.

## Späterer Deployment-Nachweis

In gesondert freigegebener Testumgebung deployte Version und Quellcodezuordnung, aktiven API-Vertrag und Migrationsstand prüfen; Registrierung, Login, JWT-Lebenszyklus und Mandantentrennung testen; Upload, Originalabruf, HEAD, Range und Vorschauen prüfen; Fehlerwiederanlauf, Performance, Speicherverbrauch sowie Backup und Restore nachweisen. Keine dieser Prüfungen wurde durch die statische Analyse ersetzt.
