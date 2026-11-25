> **Historischer, gesicherter B0-DOC-Ausgangsstand:** Nicht mehr aktiv pflegen. Maßgeblich ist [die konsolidierte Fassung](../ARCHITECTURE.md); der ursprüngliche Inhalt ist nach diesem Hinweis vollständig erhalten.

# Hosting, Deployment und Betrieb

**Offen:** [ADR-013](ARCHITECTURE-DECISIONS.md) entscheidet Windows oder Linux, Reverse Proxy, Prozessidentität, Deployment- und Betriebsverfahren, Verschlüsselung des Data-Protection-Schlüsselbestands sowie konkrete Backup-/Restore-Verfahren. Same-Origin für React und MiniDocs-Backend ist bereits durch ADR-009 freigegeben. Die Form des gesicherten lokalen Administrator-Bootstraps aus ADR-012 ist noch offen.

Vor Betrieb sind dauerhafte, verschlüsselte und separat gesicherte Schlüsselbestände je Umgebung sowie stabile Anwendungsidentität und kryptografische Zwecke festzulegen. Alte Schlüssel müssen erhalten bleiben; Rotation, Neustart, Serverwechsel und Restore sind praktisch zu erproben. Fehlkonfiguration in Produktion führt kontrolliert zum Fehler, ohne temporäre Schlüssel. Backup und Wiederherstellung müssen MiniDocs-Daten, geschützte technische Identitäten und Schlüssel konsistent berücksichtigen; Zuständigkeiten für VIA.Docs sind gesondert zu klären.

Die tatsächliche VIA.Docs-Version, Migrationen und der aktive Vertrag sind nicht deploymentverifiziert. Der konkrete Nachweiskatalog steht in [Integration](VIA-DOCS-INTEGRATION.md). Für B0-DOC erfolgen weder Deployment noch produktive API-Aufrufe; spätere Tests benötigen einen gesonderten Auftrag und freigegebene Testdaten.
