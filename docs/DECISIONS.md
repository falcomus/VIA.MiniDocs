# Architekturentscheidungen V1.0

Diese Datei ist die zentrale Quelle für ADR-Kennungen, Inhalte und Freigabestatus. Freigaben sind keine Implementierungs-, Deployment- oder Produktivfreigabe. Siehe [Gesamtarchitektur](ARCHITECTURE.md) und [VIA.Docs-API](VIA-DOCS-API.md).

## ADR-001 – Geschlossene Registrierung: Freigegeben

Keine öffentliche Selbstregistrierung. MiniDocs-Benutzer registrieren sich ausschließlich auf Einladung; Tokens sind kryptografisch zufällig, zeitlich begrenzt, einmalig und werden nur gehasht gespeichert.

## ADR-002 – Getrennte Identitäten: Freigegeben

MiniDocs-Benutzeridentität, fachlicher Workspace und technische VIA.Docs-Anwendungsidentität sind getrennt. Alle Fachdaten sind einem Workspace zugeordnet.

## ADR-003 – 1:1 in V1.0: Freigegeben

Reguläre registrierte Benutzer besitzen genau einen privaten Workspace mit eigener VIA.Docs-Anwendung. Ausschließlich administrative Konten dürfen ohne Workspace existieren; Administratorrechte erlauben keinen Zugriff auf private Dokumente. Fachlich tätige Administratoren erhalten einen eigenen regulär provisionierten Workspace. `Claus_Demo` und `Klaus-Uwe-Demo` sind vorgesehene getrennte Anwendungen mit eigenen ApplicationIds und Dokumentbeständen. Gemeinsame Projekte, Mitgliedschaften und fachliche Projektrollen gehören zu einer späteren Ausbaustufe.

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

**D4-A-Präzisierung freigegeben:** Serverseitig widerrufbare Sitzungen mit Prüfung von Kontostatus, Rollen und Security Stamp, ein persistenter X.509-geschützter Data-Protection-Key-Ring ab erster Cookie- und Antiforgery-Ausgabe sowie die im [D4-Plan](D4-PLAN.md#2-zielarchitektur-und-sessionregeln) beschriebenen Cookie-, Passwort-, Lockout-, Rate-Limit-, Bootstrap- und Administrationsgrenzen. Der fachliche Logout-Effekt ist idempotent. Ein bestätigter Widerruf liefert 204. Ein neuer Request mit ungültiger, abgelaufener oder fehlender Sitzung liefert 401. Ein nach erfolgreicher Requestprüfung parallel entzogenes Handle darf bei bestätigt erreichtem Zustand 204 liefern. Ungültiges Antiforgery ergibt 403 und ein unklärbares Datenbankergebnis 503. Die konkurrierende Sitzungserzeugung und Deaktivierung ist in D4-A nachzuweisen.

## ADR-010 – Secret- und Schlüsselverwaltung: Freigegeben

ASP.NET Core Data Protection schützt VIA.Docs-Application-Secrets und technische Einladungstokens. Dauerhafter, verschlüsselter, separat gesicherter Schlüsselbestand; stabile getrennte kryptografische Zwecke und Bindung an Workspace beziehungsweise Provisionierungsidentität. Alte Schlüssel zur Entschlüsselung erhalten. Rotation, Neustart, Serverwechsel, Backup und Restore müssen nachweislich funktionieren. Bei fehlender oder fehlerhafter Produktionskonfiguration kontrolliert scheitern; keine unbemerkten temporären Schlüssel. Konkrete Verschlüsselung des Schlüsselbestands nach ADR-013; keine eigene Kryptografie, getrennte Schlüsselbestände für Entwicklung, Test und Produktion. Keine Passwörter, Secrets, Einladungstokens oder JWTs in Logs oder regulären API-Antworten. Technische Tokens nach erfolgreicher Provisionierung entfernen. VIA.Docs-JWTs vorzugsweise im serverseitigen Arbeitsspeicher. Automatische Application-Secret-Rotation ist mangels nachgewiesener aktiver VIA.Docs-Schnittstelle noch nicht geplant.

## ADR-011 – Tenant-sichere Autorisierung: Freigegeben

Jede Fachoperation serverseitig autorisieren; Workspace-bezogene Abfragen, EF-Core-Query-Filter als zusätzliche Ebene und Datenbank-Constraints einschließlich zusammengesetzter Fremdschlüssel gegen workspace-übergreifende Beziehungen. VIA.Docs-Identitäten und JWTs strikt trennen. WorkspaceId und ApplicationId aus dem Frontend nie ungeprüft übernehmen. Adminrechte implizieren keinen Dokumentzugriff; gesperrte Benutzer und nicht vollständig provisionierte Workspaces dürfen keine Dokumentoperationen ausführen. PostgreSQL Row-Level Security ist in V1.0 optional und später als zusätzliche Ebene möglich.

## ADR-012 – Administrator-Bootstrap: Freigegeben

Erste Administratoridentität durch gesicherten lokalen administrativen Vorgang, ohne öffentlichen Bootstrap-Endpunkt, Standardpasswörter oder Zugangsdaten in Git, Logs und Kommandozeilenargumenten. Unbeabsichtigte erneute Initialisierung verhindern. Betriebsbefehl oder Verwaltungswerkzeug bleibt bis zur Deployment-Planung offen.

## ADR-013 – Hosting und Deployment: Freigegeben als Zielarchitektur

**Zielarchitektur fachlich freigegeben; keine Betriebsfreigabe:** Linux mit einem systemd-geführten ASP.NET-Core-10-Prozess, Nginx/TLS und Same-Origin-React-Build, eigener PostgreSQL-Datenbank, separaten Laufzeit-/Migrationsrollen und dauerhaftem X.509-verschlüsseltem Data-Protection-Key-Ring. Kontrollierte Migrationen, geschützte Credentials, versioniertes Deployment und systemübergreifender Backup-/Restore-Prozess gemäß [Betriebskonzept](ARCHITECTURE.md#hosting-deployment-und-betrieb). Die erforderliche Infrastruktur, Zuständigkeiten, Limits und Wiederherstellbarkeit sind nicht nachgewiesen und müssen vom Betreiber bestätigt werden. Same-Origin ist durch ADR-009 bereits freigegeben; diese Architekturfreigabe erteilt keine Implementierungs-, Integrations- oder Betriebsfreigabe.

## ADR-014 – Solution-Architektur: Freigegeben

Modularer Monolith mit pragmatischer Clean Architecture: `backend/VIA.MiniDocs.sln`, `Directory.Build.props`, `Directory.Packages.props`; Projekte `src/VIA.MiniDocs.Api`, `.Application`, `.Domain`, `.Infrastructure` und `tests/VIA.MiniDocs.Tests`. Domain ohne Projektabhängigkeit, Application → Domain, Infrastructure → Application und Domain, Api → Application und Infrastructure, Tests → benötigte produktive Projekte. Keine Zyklen. Keine zusätzlichen Projekte oder Architekturframeworks ohne nachgewiesenen Bedarf. Die Architekturfreigabe ist keine Freigabe zur Anlage oder Implementierung.

## ADR-015 – REST und Verträge: Freigegeben

Minimal APIs, schlanke Handler, OpenAPI und ProblemDetails; getrennte MiniDocs- und VIA.Docs-Verträge, keine direkte Projektreferenz auf die VIA.Docs-Solution, kein separates Contracts-Projekt ohne Bedarf. TypeScript-Generierung aus OpenAPI evaluieren.

## ADR-016 – Persistenz und Engineering: Freigegeben

.NET 10, EF Core und Npgsql, PostgreSQL-Migrationen, xUnit, zentrale NuGet-Verwaltung, Nullable Reference Types, durchgängig asynchrones I/O, CancellationToken, strukturiertes Logging und Standard-Dependency-Injection. Konkrete Paketversionen erst nach Kompatibilitätsprüfung festlegen. Zusätzliche Architekturframeworks nur bei nachgewiesenem Bedarf. Die verbindlichen Query-Filter und PostgreSQL-Schutzmechanismen aus ADR-011 bleiben davon unberührt.

## ADR-017 – PostgreSQL-Modell und Wiederanlauf: Freigegeben

Das freigegebene [B0.6-Modell](ARCHITECTURE.md#b06--postgresql-datenmodell-und-persistenzarchitektur) empfiehlt UUID-/Workspace-Schlüssel, zusammengesetzte Fremdschlüssel, Adjacency List mit serialisierten Baum- und Zuordnungsänderungen, seitenbezogene und dokumentweite Transkriptrevisionen sowie persistierte Provisionierungs- und Dateivorgänge. ADR-003 erlaubt ausschließlich administrativen Identitäten ein Konto ohne Workspace; fachlich tätige Administratoren benötigen einen eigenen regulären Workspace. Bei unklaren externen Ergebnissen kein blindes Wiederholen; ohne nachgewiesene Dateikorrelation kontrollierte Klärung ohne automatischen Adminzugriff auf private Inhalte. Suchstrategie erhält die aktuelle Teilstringsemantik und prüft `pg_trgm` anhand von Messungen. Technische Details, Constraints und Tests stehen im Entwurf; keine Implementierungsfreigabe.

## ADR-018 – Papierkorb und endgültige Löschung: Freigegeben

Freigegeben ist eine logische Löschung mit Erhalt von Original, unverändertem Thema, Tags und Transkript. Papierkorbdokumente belegen ihr Thema; normale Listen und Suche blenden sie aus. Nur der berechtigte Benutzer stellt wieder her oder löst ausdrücklich die endgültige Löschung aus. Wiederherstellung ist bei begonnener oder ungeklärter endgültiger Löschung gesperrt. Ein minimaler technischer Abschlussnachweis ohne private Inhalte bleibt erhalten. Keine automatische Papierkorbleerung oder Aufbewahrungsfrist für Papierkorbdokumente. Abgeschlossene technische Löschreceipts werden 30 Tage aufbewahrt; ungeklärte Vorgänge nicht automatisch bereinigt. Fallback für inkonsistente Altdaten bleibt offen. Der vollständige Ablauf steht im [B0.6-Entwurf](ARCHITECTURE.md#b06--postgresql-datenmodell-und-persistenzarchitektur).

## ADR-019 – Verbindliche Persistenzbefehle: Freigegeben für D2-05

Die Laufzeitrolle erhält auf geschützten Fachtabellen Lesezugriff und explizite Signaturgrants für fachliche Schreibbefehle. Direkte Fach-DML bleibt gesperrt. Die zugelassenen Funktionen laufen mit einem eigens begrenzten Besitzer ohne Login und festem `search_path`. Sie sperren zuerst den Workspace, prüfen Version und Zustand und pflegen fachliche Versionen und Zeitstempel atomar. Bestehende Constraints und zusammengesetzte Fremdschlüssel bleiben Schutzebenen. Identity-Routineoperationen behalten eng begrenzte Spaltenrechte. Autorisierung, externe Nachweise und Orchestrierung liegen bei den vorgesehenen C#-Schichten. Eine Datenbanktransaktion umfasst keinen externen Aufruf. Der [D2-05-Nachweis](D2-05-VERIFICATION.md) hält die technische Prüfung und die ausstehenden unabhängigen und betrieblichen Abnahmen fest.

D2-06 präzisiert den Vertrag: Dasselbe lesende Kataloggate prüft das versionierte Sollinventar nach Neuinstallation, Upgrade und Restore. Fachlicher Zustand wird in einer getrennten Abfrage nach der Workspace-Sperre geprüft. Leases sind auf 15 Minuten begrenzt, für C# und PostgreSQL aus derselben versionierten Vorgabe. Pending-Übernahme bereinigt Originalseiten und überflüssige Kopfmetadaten in derselben Transaktion. Der [D2-06-Nachweis](D2-06-VERIFICATION.md) dokumentiert die verbliebenen Integrationsgrenzen.

## Freigegebene Präzisierungen aus B0-FINAL

Diese Architektur- und Vertragspräzisierungen sind fachlich freigegeben; technische Nachweise und Betreiberentscheidungen bleiben eigenständige Gates. Details stehen im [REST-Vertrag](ARCHITECTURE.md#b07--rest-vertragsentwurf-zur-architekturfreigabe) und im [Betriebskonzept](ARCHITECTURE.md#hosting-deployment-und-betrieb).

- **REST, DTOs und Grenzen:** B0.7-Routen, Felder, Nullbarkeit, Zustände, Validierung, ProblemDetails und Limits wie dokumentiert übernehmen; sie geben D1–D7 eine gemeinsame prüfbare Grundlage. Die Umsetzung erfordert einen gesonderten Implementierungsauftrag.
- **OpenAPI/TypeScript:** ASP.NET-Core-OpenAPI aus den tatsächlichen C#-Verträgen erzeugen und mit `openapi-typescript` (MIT-Lizenz) gepinnt in Strict-TypeScript-Typen überführen; dies vermeidet doppelte manuelle Vertragsquellen. Reproduzierbarkeit, Multipart, Binärantworten und Nullbarkeit in D1 testen.
- **Upload-Wiederholung:** serverseitig vollständig ermittelte Dateigröße und SHA-256 vergleichen, sonst keine Gleichheit bestätigen; unbekannte externe Ergebnisse blockieren erneute Remote-Writes. Keine zusätzliche dauerhafte PDF-Kopie. Das ist keine nachgewiesene Remote-Idempotenz.
- **Upload-Aufbewahrung:** abgeschlossenen Status 30 Tage speichern, danach inhaltsfreie Request-ID-Sperrmarke bis zum Ende des Workspaces; ungeklärte Vorgänge nicht automatisch entfernen. Damit löst eine alte ID keinen neuen Upload aus. Das ist von den bereits freigegebenen 30 Tagen für Löschreceipts zu unterscheiden.
- **Pagination:** manipulationsgeschützte undurchsichtige Cursor an Workspace, Filter, Sortierung und Sortierschlüssel binden; kein bestandsweiter Snapshot. UUID ist der bereits freigegebene letzte Such-Tie-Breaker.
- **PDF-Grenze:** zunächst zentral konfigurierbar höchstens 25 MiB pro Dokument; der gesichtete VIA.Docs-Quellstand erzwingt diesen Wert. Erhöhung erst mit bestätigtem Remote-Vertrag und Tests. Der Deploy-Stand bleibt offen.
- **ADR-013:** Linux, systemd, Kestrel auf Loopback, Nginx/TLS und X.509-geschützter dauerhafter Data-Protection-Key-Ring empfehlen; schlanke Ein-Prozess-Architektur unter dem ausdrücklichen Vorbehalt der Infrastruktur- und Betreiberprüfung. Die tatsächlichen Betreiberentscheidungen zu Host, Identitäten, Zertifikaten, Limits, Backup, RPO/RTO und Restore stehen im Betriebskonzept. Lokales D1 mit Testdoubles setzt diese spätere Betriebsabnahme nicht voraus.
