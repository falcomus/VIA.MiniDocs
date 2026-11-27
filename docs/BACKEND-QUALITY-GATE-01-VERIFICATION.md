# BACKEND-QUALITY-GATE-01-FIX

Ausgangscommit ist `9ffc324e0fead5b397405eae60d8c75e4ceb8a60` auf `fix/d4-d-fix2`.

| Auditbefund | Korrektur |
| --- | --- |
| Ungebundener Aufruf ohne Dispatch-Autorität | Der produktive Provider gibt Zugangsdaten nur für einen konkreten, typisierten Verwendungszweck frei. Provisionierung und Recovery prüfen Workspace, technischen Namen, Konto, Operation, Generation, Fence, Lease, Zustand, Aufrufart und Restbudget. Direkt vor HTTP prüft ein frischer DI-Scope erneut. |
| Gebundener Aufruf ohne exakte Application-ID-Prüfung | Der produktive Provider verlangt den aktiven Workspace, einen aktiven Eigentümer und das verifizierte Konto mit der exakt erwarteten Application-ID. Die Prüfung gilt vor Nutzung des Caches, bei Login beziehungsweise Erneuerung vor HTTP und in der frischen Prüfung vor Rückgabe. |
| Fehler der Datenbankprüfung | Abbruch durch den Aufrufer und Ablauf des Clientbudgets werden als Cancellation beziehungsweise Timeout klassifiziert. Datenbank- oder Entschlüsselungsfehler bei laufendem Budget ergeben CredentialUnavailable. Abweisung vor HTTP erzeugt keinen UnknownOutcome. Nach möglicherweise gesendeter Registrierung bleibt UnknownOutcome bei fehlender Nachprüfung erhalten. |
| Schwache gleichnamige D2-CHECKs nach Restore | Die 20 bestehenden kritischen D2-CHECKs im Kataloginventar müssen zusätzlich ihre kanonische PostgreSQL-Definition behalten. Der neue Negativfall mit gleichnamigem wirkungslosem CHECK wurde vom bisherigen Gate akzeptiert. Der Prüflauf mit dem alten Gate schlug deshalb erwartungsgemäß fehl. |

Die Freigabe unmittelbar vor HTTP begrenzt die Autorität auf den Prüfzeitpunkt. Lokale Transaktion und externer HTTP-Versand bleiben getrennt. Bereits möglicherweise gesendete Registrierungen behalten den konservativen Recovery-Pfad.

Historische Migrationen, Deferred Constraint Trigger, Registrierungskonkurrenz, Rate-Limit-Partitionierung und SameSite-Regeln wurden unverändert übernommen. D3-INTEGRATION, D8 und Betriebsabnahmen bleiben eigenständig.

Der gezielte Lauf gegen isoliertes PostgreSQL umfasst 34 erfolgreiche Fälle. Der kontrollierte HTTP-Handler zählt bei fehlendem Dispatch, falschem Fence, falscher Generation, falscher Operation, falscher Aufrufart, falscher gebundener Application-ID, suspendiertem Workspace und deaktiviertem Eigentümer exakt null zusätzliche Aufrufe. Derselbe Testaufbau prüft gültige Provisionierung, frische Verifikation, gebundenen Login, Cache-Treffer und die Isolation zwischen zwei Workspaces. Die injizierten Datenbankfehler unmittelbar vor dem Versand weisen CredentialUnavailable und Vorrang der Aufrufer-Cancellation nach.

## Regression-Gate

| Prüfung | Ergebnis | Exitcode |
| --- | --- | --- |
| Locked Restore mit vollständigem NuGet-Audit | Erfolgreich | 0 |
| Backend-Build in Debug und Release | Null Warnungen und Fehler | je 0 |
| Backendregression in Debug und Release mit isoliertem PostgreSQL | Je 438 bestanden, keine Skips. Der TRX-Abgleich bestätigt je 436 unveränderte Testnamen und zwei neue Fälle. | je 0 |
| Gezielte PostgreSQL-Prüfungen | 34 bestanden, keine Skips | 0 |
| Vollständiger Frontendcheck | 266 Vitest-Tests und Produktionsbuild bestanden | 0 |
| Backend-Vertragsabgleich, TypeScript und negativer Drift-Test | Bestanden. Ein veralteter Vertrag wird abgewiesen. | je 0 |
| Reguläres Playwright-Gate ohne Referenzaktualisierung | Acht bestanden, einschließlich drei visueller Vergleiche | 0 |
| Git-Diff, Whitespace, Index und Secret-Mustersuche | Bestanden, Index leer, HEAD unverändert | je 0 |

Der Playwright-Testhost protokollierte den bereits im D4-FINAL-Nachweis beschriebenen Data-Protection-Startfehler des anfänglich leeren Test-Key-Rings. Der HTTPS-Testlauf war erfolgreich. Der Nachweis für reale VIA.Docs-Anbindung und produktiven Betrieb bleibt offen.
