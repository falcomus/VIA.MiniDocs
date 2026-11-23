# M6-C – Entscheidung zur PDF-Darstellung

## Kontext

Der KU-Prototyp zeigt vorhandene PDF-Blobs mit dem nativen Browser-PDF-Viewer in einem iframe und übergibt die gewählte Seite über ein URL-Fragment. Seine synthetischen Demoseiten und die seitenbezogenen Transkripte sind eigene Darstellungen. Scrollen der Demoseiten kann im Original die aktive Transkriptseite über Seitenanker nachführen; bei nativen PDFs ist der interne Viewer dagegen nicht zuverlässig von der Anwendung auslesbar.

## Entscheidung

MiniDocs erhält eine explizite Grenze für synthetische Demoseiten und einen tatsächlich gelieferten PDF-Blob. Eine MIME-/Signaturprüfung und eine nur während der Nutzung gültige Objekt-URL gehen der nativen iframe-Darstellung voraus. Dies ist keine vollständige PDF-Sicherheits- oder Strukturprüfung und kein Nachweis, dass der Browser jede Datei darstellen kann. Eine fehlende oder offensichtlich ungültige Quelle wird angezeigt. Demodaten bleiben als solche gekennzeichnet. Es wird weder eine PDF-Library noch ein Import- oder API-Adapter eingeführt.

## Konsequenzen

- Die Anwendung kann ihre gewählte Seite mittels `#page=` anfragen. Ob und wie der Browser das Fragment verarbeitet, ist browserabhängig; interne Seitenwechsel und Scrollpositionen des PDF-Plugins sind nicht zuverlässig zurücklesbar. Eine beidseitige Synchronisierung wird nicht behauptet.
- PDF-Zoom wird vom nativen Viewer verwaltet; der bestehende Demo-Zoom gilt ausschließlich für synthetische Seiten. Lade- und Darstellungsfehler innerhalb des Browser-Plugins sind nicht vollständig kontrollierbar.
- Die Demo verwendet mangels durchgehender Seitenanker eine optionale proportionale Scrollkopplung von der Dokumentseite zur Textarea; sie ist keine genaue inhaltliche Textzuordnung und keine native PDF-Kopplung.
- M7 beziehungsweise das Backend müssen später eine tatsächliche, berechtigte PDF-Quelle bereitstellen und den Lebenszyklus sowie Fehlerfälle im Gesamtsystem prüfen. Für zuverlässig steuerbare bidirektionale PDF-Seiten-, Scroll- und Zoomsynchronisierung wäre ein kontrollierter PDF-Renderer erneut zu evaluieren und gesondert freizugeben.
- Ein Browser-Smoke-Test mit einer tatsächlichen PDF-Datei sowie Screenreader-, Touch-, Zoom- und Responsive-Abnahmen stehen noch aus.
