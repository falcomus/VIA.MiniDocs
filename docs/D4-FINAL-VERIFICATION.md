# D4-FINAL · Technische Gesamtabnahme

## Paket- und Sicherheitsnachweise

| Kriterium | Nachweis und Abnahmegrenze |
| --- | --- |
| D4-A und SEC-09 | Identity, Cookies, CSRF, Kontosperre, Session-Widerruf und R-03-Zeilenkonkurrenz sind im Paketnachweis belegt. [D4-A-Verifikation](D4-A-VERIFICATION.md) |
| D4-B und lokaler Anteil SEC-01–08 | Einmalige DB-zeitgebundene Einlösung, Registrierung und Workspace atomar, Kontorollen ohne automatischen Dokumentzugriff, Filter und Datenbank-Isolation. Fachliche HTTP-Nachweise sind Gegenstand von D6/D5, Transkripte von D7. [D4-B-Verifikation](D4-B-VERIFICATION.md) |
| D4-C und SEC-10, SEC-12–15 | Lease und Fence, geschützte Credential-Generationen, Recovery und Commit-Grenzen, Rotation und isolierter Restore sind im Paketnachweis belegt. Kombinierter Betriebsrestore und reale VIA.Docs-Identitäten erfordern D8 beziehungsweise D3-INTEGRATION. [D4-C-Verifikation](D4-C-VERIFICATION.md) |
| D4-D, lokaler Anteil SEC-11 und Vertrag | 15 D4-Operationen, ProblemDetails, HTTP 429 und 500, generierter TypeScript-Vertrag, Session- und Adminzustand, Linkablauf und automatisierte HTTPS-Browserregression. expiresAt bleibt optional. Die statische Nachprüfung von FIX3 und INVITE-LINK ist laut Projektverantwortlichem freigegeben. [D4-D-Verifikation](D4-D-VERIFICATION.md) |
| Bestandsschutz | Vorhandene Tests und die drei abgenommenen visuellen V2-Referenzen bleiben erhalten. Ein zusätzlicher Browserfall prüft einen Einladungslink bei aktiver Sitzung, inklusive Fragmentbereinigung und unverändertem Zugriff auf die Administration. |

BACKEND-QUALITY-GATE-01 prüft weiterhin verbindlich fehlenden Dispatch-Kontext beim produktiven Credential-Provider und den Erhalt der Bound-Identity-Pfade. D3-INTEGRATION, D8 und produktive Betriebsabnahmen benötigen separate Freigaben. Der automatisierte HTTPS-Lauf mit Testdouble belegt die geprüften Abläufe. Der Projektverantwortliche hat die erfolgreiche manuelle Anmeldung mit Benutzername und Passwort bestätigt. Die manuelle Tastatur-, Fokus- und Accessibility-Abnahme bleibt offen.

## Abschließendes Regression-Gate

| Prüfung | Ergebnis | Exitcode |
| --- | --- | --- |
| Locked Restore mit NuGet-Audit im Modus all | Erfolgreich, keine Auditwarnung | 0 |
| Debug- und Release-Build | Jeweils null Warnungen und Fehler | je 0 |
| Backendregression gegen isoliertes PostgreSQL | Je 436 in Debug und Release, keine Skips | je 0 |
| Gezielte Sicherheits- und Konkurrenzfälle | 26 bestanden, keine Skips | 0 |
| Backend-Vertragsvergleich, TypeScript und negativer Drift-Test | Alle bestanden | je 0 |
| Frontend-Gesamtprüfung | 266 Vitest-Tests und Produktionsbuild bestanden | 0 |
| Reguläres Playwright-Gate | 8 bestanden, davon 3 visuelle Vergleiche gegen unveränderte Referenzen | 0 |
| Umfang, Whitespace, Index und Secret-Mustersuche | 45 Pfade, keine Whitespace-Fehler oder Musterfunde, leerer Index | 0 |

Der isolierte Browserhost protokollierte beim Start einen Data-Protection-Hinweis zum anfänglich leeren Test-Key-Ring. Die geschützten HTTP-Läufe und der isolierte Persistenznachweis waren erfolgreich. Der produktive Key-Ring- und Restore-Nachweis bleibt ein Betriebsgate.

## Dateifreigabeliste für Commit und Merge

45 einzeln benannte Pfade relativ zum Repository-Root. Ausgangsrevision von HEAD, lokalem main und lokalem origin/main ist 8a60b0b5fb88c55a1ac6898ee8a0c3419b262462.

- `AGENTS.md`
- `backend/contracts/contract-check.ts`
- `backend/contracts/fixture.generated.ts`
- `backend/src/VIA.MiniDocs.Api/Program.cs`
- `backend/src/VIA.MiniDocs.Api/Security/AdministrationHttp.cs`
- `backend/src/VIA.MiniDocs.Api/Security/ProvisioningHttp.cs`
- `backend/src/VIA.MiniDocs.Api/Security/SecurityRegistration.cs`
- `backend/src/VIA.MiniDocs.Api/Security/SessionHttp.cs`
- `backend/tests/VIA.MiniDocs.Tests/BrowserRegressionTests.cs`
- `backend/tests/VIA.MiniDocs.Tests/HostTests.cs`
- `backend/tests/VIA.MiniDocs.Tests/ProvisioningHostTests.cs`
- `backend/tests/VIA.MiniDocs.Tests/RegistrationHostTests.cs`
- `backend/tests/VIA.MiniDocs.Tests/VIA.MiniDocs.Tests.csproj`
- `docs/D4-D-VERIFICATION.md`
- `docs/D4-FINAL-VERIFICATION.md`
- `docs/DEFINITION-OF-DONE.md`
- `docs/ENGINEERING-STANDARDS.md`
- `docs/MASTERPLAN.md`
- `docs/TICKETS.md`
- `frontend/e2e/auth.pw.ts`
- `frontend/e2e/demo-server.mjs`
- `frontend/e2e/demo.pw.ts`
- `frontend/e2e/reference/win32/dokumente.png`
- `frontend/e2e/reference/win32/login-dark.png`
- `frontend/e2e/reference/win32/login-light.png`
- `frontend/e2e/run.mjs`
- `frontend/e2e/tsconfig.json`
- `frontend/e2e/visual.pw.ts`
- `frontend/package-lock.json`
- `frontend/package.json`
- `frontend/playwright.config.ts`
- `frontend/src/components/AppHeader.module.css`
- `frontend/src/components/AppHeader.tsx`
- `frontend/src/components/AppShell.module.css`
- `frontend/src/components/AppShell.tsx`
- `frontend/src/features/auth/AdminPanel.test.tsx`
- `frontend/src/features/auth/AdminPanel.tsx`
- `frontend/src/features/auth/api.test.ts`
- `frontend/src/features/auth/api.ts`
- `frontend/src/features/auth/AuthApp.module.css`
- `frontend/src/features/auth/AuthApp.test.tsx`
- `frontend/src/features/auth/AuthApp.tsx`
- `frontend/src/main.tsx`
- `frontend/vite.config.ts`
- `README.md`

Ausgeschlossen sind #INTERNAL DOCS/, .local-review/, CHAT_START_MINIDOCS_PERSISTENT.ps1 und MiniDocs-Backend-Start.cmd sowie sämtliche Review-Archive, Browser-Kandidaten, Testausgaben und lokalen Konfigurationen. Die drei ausdrücklich freigegebenen PNGs im Referenzverzeichnis gehören zum Paket. Die 45 Dateien sind statisch freigegeben. Die Freigabe gilt für den Commit auf dem Arbeitsbranch fix/d4-d-fix2. Merge nach main und Push stehen aus und benötigen eine gesonderte Freigabe.
