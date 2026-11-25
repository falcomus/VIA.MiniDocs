> **Historischer, gesicherter B0-DOC-Ausgangsstand:** Nicht mehr aktiv pflegen. Maßgeblich ist [die konsolidierte Fassung](../ARCHITECTURE.md); der ursprüngliche Inhalt ist nach diesem Hinweis vollständig erhalten.

# Identität und Provisionierung

Gültige Entscheidungen: [ADR-001–009, ADR-012](ARCHITECTURE-DECISIONS.md). MiniDocs-Benutzereinladungen und technische VIA.Docs-Einladungen haben getrennte Aussteller, Zwecke und Lebenszyklen. Die erste verwendet nur gehashte, zufällige, befristete Einmaltokens. VIA.Docs-Registrierungsschlüssel verbleiben außerhalb des regulären Betriebs; Details der technischen Schnittstelle und ihr Status: [Integration](VIA-DOCS-INTEGRATION.md).

```mermaid
sequenceDiagram
  actor A as Administrator
  actor U as Benutzer
  participant M as MiniDocs
  participant V as VIA.Docs
  A->>M: Benutzereinladung erstellen
  U->>M: Einladung einlösen und registrieren
  M->>M: Konto und privaten Workspace anlegen; warten
  A->>V: Technische Einladung außerhalb des Betriebs erzeugen
  A->>M: Einladung geschützt für Workspace hinterlegen
  M->>M: Namen und Secret geschützt dauerhaft vorbereiten
  M->>V: Anwendung registrieren
  M->>M: ApplicationId verifizieren, zuordnen und aktivieren
```

Der Benutzer wartet gegebenenfalls auf den Administrator. Kein Vorrat technischer Einladungen, kein separater Provisionierungsdienst in V1.0. Kontosperrung und Workspace-Provisionierung sind getrennte Zuständigkeiten. Die Zustände `PendingInvitation`, `ReadyToProvision`, `Provisioning`, `RecoveryRequired`, `Active` sind **vorgeschlagen**, Übergänge und technische Durchsetzung noch zu spezifizieren.

Vor dem externen Aufruf Name und Secret dauerhaft geschützt festhalten und höchstens einen aktiven Vorgang pro Workspace zulassen. Bei Timeout oder unklarem Ergebnis zunächst mit denselben Zugangsdaten anmelden und Identität wiederauffinden. Fehlgeschlagener Login beweist keine Nichtregistrierung; kontrollierte Klärung statt blinder Wiederholung oder automatischer Löschung. Keine verteilte Transaktion und keine Exactly-once-Behauptung. Nach erfolgreicher Provisionierung das technische Einladungstoken entfernen. Fehlerzustände, abgelaufene Einladungen und manueller Wiederanlauf müssen später konkretisiert und getestet werden.
