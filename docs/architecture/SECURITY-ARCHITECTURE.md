> **Historischer, gesicherter B0-DOC-Ausgangsstand:** Nicht mehr aktiv pflegen. Maßgeblich ist [die konsolidierte Fassung](../ARCHITECTURE.md); der ursprüngliche Inhalt ist nach diesem Hinweis vollständig erhalten.

# Sicherheitsarchitektur

Die verbindlichen Entscheidungen zu Browserauthentifizierung, Secrets, Workspace-Isolation und Administrator-Bootstrap stehen in [ADR-009 bis ADR-012](ARCHITECTURE-DECISIONS.md). Same-Origin, Identity-Cookies, Antiforgery, Lockout und Rate Limiting schützen die MiniDocs-Sitzung; eine Kontosperre muss bestehende Sitzungen erfassen. Workspace und VIA.Docs-Application werden serverseitig bestimmt und autorisiert. Query-Filter sind eine zusätzliche Ebene; Datenbank-Constraints und zusammengesetzte Fremdschlüssel schützen Beziehungen. Administratoren erhalten keinen automatischen Zugriff auf Benutzerdateien. Nicht aktive Workspaces und gesperrte Benutzer dürfen keine Dokumentoperationen ausführen. Row-Level Security bleibt optional.

Data Protection benötigt getrennte, dauerhafte und gesicherte Schlüsselbestände je Umgebung und zweckgebundene, an Workspace oder Provisionierung gebundene Secrets; alte Schlüssel müssen lesbar bleiben. Konkrete Schutzmechanismen hängen von [Deployment](DEPLOYMENT.md) ab. Fehlende Produktionskonfiguration darf nicht auf temporäre Schlüssel zurückfallen. Tokens, Secrets, Passwörter und JWTs gehören weder in Logs noch reguläre API-Antworten. Die vollständigen Anforderungen stehen in ADR-010.

## Verbindliche zukünftige Sicherheitstests

Anforderungen, **nicht implementiert oder bestanden**. Sicherheitskritische Persistenz- und Isolationstests müssen mit echtem PostgreSQL stattfinden.

| ID | Nachweis |
| --- | --- |
| SEC-01 | Zugriff auf eigene Dokumente möglich. |
| SEC-02 | Fremde Dokumente nicht lesbar. |
| SEC-03 | Fremde Dokumente nicht veränderbar. |
| SEC-04 | Fremde Dokumente nicht löschbar. |
| SEC-05 | Workspace-übergreifende Themenzuordnung scheitert. |
| SEC-06 | Workspace-übergreifende Tagzuordnung scheitert. |
| SEC-07 | Fremde Transkripte nicht abrufbar. |
| SEC-08 | Administrator besitzt keinen automatischen Dokumentzugriff. |
| SEC-09 | Gesperrte Benutzer verlieren ihren Zugriff einschließlich bestehender Sitzungen. |
| SEC-10 | Parallele Provisionierung erzeugt keine Doppelregistrierung. |
| SEC-11 | Secrets erscheinen nicht in API-Antworten und Logs. |
| SEC-12 | Secrets bleiben nach Neustart verfügbar. |
| SEC-13 | Secrets bleiben nach Schlüsselrotation verfügbar. |
| SEC-14 | Backup und Restore erhalten die technische Identität. |
| SEC-15 | Parallele Anfragen verschiedener Workspaces vermischen keine JWTs. |
