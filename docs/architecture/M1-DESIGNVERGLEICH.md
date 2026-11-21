# M1: quellenbasierter Designvergleich

Die M1-Grundstruktur wurde gegen `.local-reference/MiniDoc_Prototyp.html` anhand des Quellcodes verglichen. Ein Browser-Screenshotvergleich und eine manuelle visuelle Abnahme bei gleicher Fenstergröße stehen noch aus: Der In-App-Browser erreichte den lokalen Server nicht; die Computer-Use-Steuerung des Windows-Browsers war wegen nicht verfügbarer URL-Richtlinienprüfung blockiert. Es wurde kein Sicherheitsmechanismus umgangen.

Übernommen sind insbesondere 260 px Sidebar, Systemschrift 14 px/1,45, die Farbvariablen, 8 px Standardradius, Schatten, Header-Abstände, 18 px SVG-Icons sowie das getrennte Scrollen von Baum und Inhalt. Die vorhandenen Themenauswahl- und Listenkomponenten sind eingebunden. Nicht implementierte Aktionen sind deaktiviert.

Bekannte Abweichungen: Andere Demo-Themen und -Dokumente, vereinfachte Liste statt ursprünglicher Kacheln und Vorschauen, keine Themenzähler oder Breadcrumb-Pfade, keine Original-Sammelaktionen. Das Original beginnt in Kachelansicht; M1 zeigt die vorhandene Liste. Die deaktivierten Controls sind optisch abgeschwächt; bei schmalem Fenster wird der Header zusätzlich umgebrochen. Die tatsächliche Darstellung, Schriftmetriken, Fokus-, Hover- und Scrollzustände müssen im Browser geprüft werden. Diese Abweichungen gehören zu späteren Paketen bzw. zur visuellen Abnahme.
