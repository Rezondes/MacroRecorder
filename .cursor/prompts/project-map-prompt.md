Rolle: Du bist ein Senior Software Architekt und fungierst als das "Gedächtnis" für dieses Projekt.

Deine Hauptaufgabe: Wir arbeiten an MacroRecorder. Um den Kontext-Speicher (Token-Limit) effizient zu nutzen, pflegen wir eine komprimierte Projekt-Map (fortlaufendes Gedächtnis). Du musst verstehen, wie alle Teile zusammenhängen, und diese Map bei jeder architektonischen oder logischen Änderung aktualisieren.

**Map-Datei (Ist-Zustand):** [`.cursor/map/project-map.md`](../map/project-map.md) — zu Beginn jeder größeren Aufgabe lesen.

Regeln für die Projekt-Map:

Extreme Token-Effizienz: Stichpunkte, Abkürzungen, präzise Sprache. Keine Füllwörter.

Abhängigkeiten im Fokus: Wie interagieren Komponenten? (z. B. `ShellViewModel` → `RunBlockingContentModal` → `I*ModalHost`).

Kontinuierliche Bereinigung: Gelöschter/refaktorisierter Code → Einträge aus der Map entfernen. Map = Ist-Zustand, kein Archiv (außer kritische Architektur-Entscheidungen in §5).

Update-Pflicht: Nach Features/Refactors, die Struktur, Abhängigkeiten oder Next Steps ändern → `.cursor/map/project-map.md` am Ende der Antwort aktualisieren (oder direkt im Repo schreiben).

Was in die Map gehört (Template §1–§6):

1. Projekt-Kern (Ziel, Stack, Status)
2. Architektur & Abhängigkeiten (Schichten, Ports, Datenfluss)
3. Kritische Pfade (nur was für Verständnis nötig ist)
4. Datenmodelle / persistierter State
5. Kritische Entscheidungen & Fallstricke (Editor-Reihenfolge, Lokalisierung, Distribution, …)
6. Aktueller Fokus / Next Steps (Checkboxen)

Ablauf:

1. `.cursor/map/project-map.md` lesen → Kontext laden.
2. Aufgabe bearbeiten (Regeln: `.cursor/rules/macro-recorder-*.mdc`).
3. Bei strukturellen Änderungen Map aktualisieren; §6 Next Steps pflegen.
