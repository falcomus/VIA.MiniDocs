> **Historischer, gesicherter B0-DOC-Ausgangsstand:** Nicht mehr aktiv pflegen. Maßgeblich ist [die konsolidierte Fassung](../DECISIONS.md); der ursprüngliche Inhalt ist nach diesem Hinweis vollständig erhalten.

# Architekturentscheidungen V1.0

Diese Datei ist die zentrale Quelle für ADR-Kennungen und Freigabestatus. „Freigegeben“ bezeichnet die fachliche Architekturentscheidung, keine Implementierungs-, Deployment- oder Produktivfreigabe. Thematische Konkretisierungen stehen in [Backend](BACKEND-ARCHITECTURE.md), [Identität](IDENTITY-AND-PROVISIONING.md), [Sicherheit](SECURITY-ARCHITECTURE.md), [Integration](VIA-DOCS-INTEGRATION.md), [Datenmodell](DATA-MODEL.md) und [Deployment](DEPLOYMENT.md).

## ADR-001 – Geschlossene Registrierung: Freigegeben

Keine öffentliche Selbstregistrierung. MiniDocs-Benutzer registrieren sich ausschließlich auf Einladung; Tokens sind kryptografisch zufällig, zeitlich begrenzt, einmalig und werden nur gehasht gespeichert.

## ADR-002 – Getrennte Identitäten: Freigegeben

MiniDocs-Benutzeridentität, fachlicher Workspace und technische VIA.Docs-Anwendungsidentität sind getrennt. Alle Fachdaten sind einem Workspace zugeordnet.

## ADR-003 – 1:1 in V1.0: Freigegeben

Ein MiniDocs-Benutzer besitzt einen privaten Workspace mit eigener VIA.Docs-Anwendung. `Claus_Demo` und `Klaus-Uwe-Demo` sind vorgesehene getrennte Anwendungen mit eigenen ApplicationIds und Dokumentbeständen. Gemeinsame Projekte, Mitgliedschaften und fachliche Projektrollen gehören zu einer späteren Ausbaustufe.

## ADR-004 – Serverseitige Integration: Freigegeben

React spricht ausschließlich die eigene MiniDocs-API an. Diese nutzt VIA.Docs V1.2 serverseitig über REST; Application-Secrets und VIA.Docs-JWTs gelangen nicht in das normale Benutzer-Frontend. Kein direkter Zugriff auf VIA.Docs-Datenbank oder interne Speicherprovider. MiniDocs besitzt eine eigene PostgreSQL-Datenbank für Fachdaten.

## ADR-005 – Getrennte Einladungen: Freigegeben

MiniDocs-Benutzereinladung und technische VIA.Docs-Anwendungseinladung sind unabhängig. Der administrative VIA.Docs-Registrierungsschlüssel bleibt vollständig außerhalb des regulären MiniDocs-Betriebs; technische Einladungen entstehen in einem getrennt abgesicherten Verwaltungsprozess.

## ADR-006 – Wiederanlauffähige Provisionierung: Freigegeben

Anwendungsname und Application-Secret werden vor dem externen Registrierungsaufruf dauerhaft geschützt gespeichert. Pro Workspace ist höchstens ein Provisionierungsvorgang gleichzeitig aktiv; unkontrollierte Doppelregistrierungen sind zu verhindern. Bei unklarem Ergebnis wird zunächst mit den vorbereiteten Zugangsdaten eine Wiederauffindung per Anmeldung versucht. Ein fehlgeschlagener Login beweist keine fehlende Registrierung. Keine blinde Wiederholung und keine automatische Löschung möglicherweise erfolgreich provisionierter Anwendungen; keine gemeinsame Transaktion beider Systeme annehmen.

## ADR-007 – Administrative Einzelbereitstellung: Freigegeben

Administrator erstellt Benutzereinladung → Benutzer registriert sich → Konto und privater Workspace entstehen → Workspace wartet → Administrator erzeugt außerhalb des regulären Betriebs eine technische VIA.Docs-Einladung und hinterlegt sie geschützt für diesen Workspace → MiniDocs provisioniert automatisch und wiederanlauffähig → verifizierte ApplicationId wird dauerhaft zugeordnet → Workspace wird aktiviert. Der Benutzer muss gegebenenfalls warten. Kein Einladungsvorrat und kein separater Provisionierungsdienst in V1.0. Benutzerkontostatus und Provisionierungsstatus sind getrennt. `PendingInvitation`, `ReadyToProvision`, `Provisioning`, `RecoveryRequired`, `Active` sind vorgeschlagene, technisch noch zu konkretisierende Zustände.

## ADR-008 – Kleine integrierte Verwaltungsoberfläche: Freigegeben

Umfang: MiniDocs-Einladungen verwalten, Provisionierungsstatus anzeigen, technische Einladung geschützt übergeben und kontrollierten Wiederanlauf ermöglichen. Administratorrechte geben keinen automatischen Zugriff auf private Dateien. Die konkrete Gestaltung ist offen.

## ADR-009 – MiniDocs-Authentifizierung: Freigegeben

ASP.NET Core Identity mit Cookie-basierter Browserauthentifizierung, HttpOnly und Secure, geeigneter SameSite-Konfiguration, ASP.NET Core Antiforgery, Lockout und Rate Limiting. React und Backend werden Same-Origin gehostet. Eigener einladungsbasierter Registrierungsablauf ohne öffentlich erreichbare Standardregistrierung; keine JWT-Verwaltung im React-Frontend. Identität unabhängig von VIA.Docs. Kontosperrung muss auch bestehende Sitzungen treffen; Sitzungsvalidierung ist technisch zu spezifizieren.

## ADR-010 – Secret- und Schlüsselverwaltung: Freigegeben

ASP.NET Core Data Protection schützt VIA.Docs-Application-Secrets und technische Einladungstokens. Dauerhafter, verschlüsselter, separat gesicherter Schlüsselbestand; stabile getrennte kryptografische Zwecke und Bindung an Workspace beziehungsweise Provisionierungsidentität. Alte Schlüssel zur Entschlüsselung erhalten. Rotation, Neustart, Serverwechsel, Backup und Restore müssen nachweislich funktionieren. Bei fehlender oder fehlerhafter Produktionskonfiguration kontrolliert scheitern; keine unbemerkten temporären Schlüssel. Konkrete Verschlüsselung des Schlüsselbestands nach ADR-013; keine eigene Kryptografie, getrennte Schlüsselbestände für Entwicklung, Test und Produktion. Keine Passwörter, Secrets, Einladungstokens oder JWTs in Logs oder regulären API-Antworten. Technische Tokens nach erfolgreicher Provisionierung entfernen. VIA.Docs-JWTs vorzugsweise im serverseitigen Arbeitsspeicher. Automatische Application-Secret-Rotation ist mangels nachgewiesener aktiver VIA.Docs-Schnittstelle noch nicht geplant.

## ADR-011 – Tenant-sichere Autorisierung: Freigegeben

Jede Fachoperation serverseitig autorisieren; Workspace-bezogene Abfragen, EF-Core-Query-Filter als zusätzliche Ebene und Datenbank-Constraints einschließlich zusammengesetzter Fremdschlüssel gegen workspace-übergreifende Beziehungen. VIA.Docs-Identitäten und JWTs strikt trennen. WorkspaceId und ApplicationId aus dem Frontend nie ungeprüft übernehmen. Adminrechte implizieren keinen Dokumentzugriff; gesperrte Benutzer und nicht vollständig provisionierte Workspaces dürfen keine Dokumentoperationen ausführen. PostgreSQL Row-Level Security ist in V1.0 optional und später als zusätzliche Ebene möglich.

## ADR-012 – Administrator-Bootstrap: Freigegeben

Erste Administratoridentität durch gesicherten lokalen administrativen Vorgang, ohne öffentlichen Bootstrap-Endpunkt, Standardpasswörter oder Zugangsdaten in Git, Logs und Kommandozeilenargumenten. Unbeabsichtigte erneute Initialisierung verhindern. Betriebsbefehl oder Verwaltungswerkzeug bleibt bis zur Deployment-Planung offen.

## ADR-013 – Hosting und Deployment: Offen

Windows oder Linux, Reverse Proxy, Prozessidentität, Betriebs- und Deployment-Verfahren, Verschlüsselung des Data-Protection-Schlüsselbestands sowie Backup und Restore sind offen. Same-Origin-Hosting ist bereits in ADR-009 freigegeben.

## ADR-014 – Solution-Architektur: Vorgeschlagen, Freigabe offen

Modularer Monolith mit pragmatischer Clean Architecture: `backend/VIA.MiniDocs.sln`, `Directory.Build.props`, `Directory.Packages.props`; Projekte `src/VIA.MiniDocs.Api`, `.Application`, `.Domain`, `.Infrastructure` und `tests/VIA.MiniDocs.Tests`. Domain ohne Projektabhängigkeit, Application → Domain, Infrastructure → Application und Domain, Api → Application und Infrastructure, Tests → benötigte produktive Projekte. Keine Zyklen. Nichts davon ist eine Freigabe zur Anlage.

## ADR-015 – REST und Verträge: Vorgeschlagen, Freigabe offen

Minimal APIs, schlanke Handler, OpenAPI und ProblemDetails; getrennte MiniDocs- und VIA.Docs-Verträge, keine direkte Projektreferenz auf die VIA.Docs-Solution, kein separates Contracts-Projekt ohne Bedarf. TypeScript-Generierung aus OpenAPI evaluieren.

## ADR-016 – Persistenz und Engineering: Vorgeschlagen, Freigabe offen

.NET 10, EF Core und Npgsql, PostgreSQL-Migrationen, xUnit, zentrale NuGet-Verwaltung, Nullable Reference Types, asynchrones I/O, CancellationToken, strukturiertes Logging und Standard-Dependency-Injection. Zusätzliche Architekturframeworks nur bei nachgewiesenem Bedarf. Die verbindlichen Query-Filter und PostgreSQL-Schutzmechanismen aus ADR-011 bleiben davon unberührt.
