# Projekt-Memory-Map

## 1. Projekt-Kern
- **Ziel:** Windows-Desktop-App zum Aufzeichnen, Editieren, Wiedergeben und Verketten von Tastatur-/Maus-Makros (inkl. Fokus-/Fenstererkennung).
- **Tech-Stack:** .NET 10, WPF (`net10.0-windows`, x64), MVVM (`CommunityToolkit.Mvvm`), `Microsoft.Extensions.Hosting`/DI, RESX-Lokalisierung (DE/EN), WinAPI Low-Level-Hooks + `SendInput`, JSON-Persistenz (`System.Text.Json`, `Ulid`).
- **Aktueller Status:** Version `0.0.10` in csproj; Release via Tag `v0.0.10` + GitHub Actions (portable ZIP). In-App-Update: Check + **Jetzt aktualisieren** via stabiler `MacroRecorderByRezondes.Updater.exe`.

## 2. Architektur & Abhängigkeiten (DDD-Schichten)
- **`MacroRecorder.Domain`** – pure Modelle (`Macro`, `RecordedInputEvent`-Hierarchie polymorph via JSON-Discriminator, `RecordingMetadata`, `MacroId`, `PlaybackKeyChord`, `MacroQueueDocument`/`QueueStep`). Keine Plattform-/UI-Abhängigkeit.
- **`MacroRecorder.Application`** – Ports + Orchestrierung. `IPlaybackService`, `IRecordingEngine`, `IMacroRepository`, `IPlaybackHotkeyStore`, `IUserDialogService`, `IUiLocalizer`, `IUpdateCheckService`, `IAppUpdateService`, `IExternalUriOpener`, Modal-Host-Ports (`I*ModalHost`). Services: `RecordingCoordinator`, `MacroWorkspaceService`, `MacroQueueRunner`, `TimelineNormalizer`, `MouseMovePathSimplifier`, `MouseMoveRecordingFilter`, `PlaybackDurationEstimator`. → ruft Domain.
- **`MacroRecorder.Infrastructure`** – Port-Impls. `LowLevelRecordingEngine` (WH_KEYBOARD_LL/WH_MOUSE_LL), `SendInputPlaybackService`, `JsonMacroRepository`/`JsonPlaybackHotkeyStore`/`MacroQueueFileStore` (in `%LocalAppData%/MacroRecorderByRezondes/`), `FocusWindowMatcher`, `Updates/GitHubReleaseUpdateCheckService` + `PortableAppUpdateLauncher`, `ProcessExternalUriOpener`. → registriert in `AddMacroRecorderInfrastructure()`.
- **`MacroRecorder.Logging`** – shared Serilog file bootstrap (`LoggingBootstrap`, `LogPaths`, privacy notes). Used by App, Infrastructure, and Updater. Logs under `%LocalAppData%/MacroRecorderByRezondes/logs/` (`app.log`, `updater.log`; rolling 5 × 5 MB).
- **`MacroRecorder.Updater`** – kleines Konsolen-EXE (`MacroRecorderByRezondes.Updater.exe`): wartet auf App-Exit, lädt Release-ZIP, ersetzt Installationsdateien außer Updater, startet Haupt-EXE neu; strukturiertes Phasen-Logging via `MacroRecorder.Logging`.
- **`MacroRecorder.App`** (WPF) – Shell + ViewModels. Ein `MainWindow` mit `ShellViewModel.CurrentPage` (DataTemplates für Overview/Editor/Settings/Record/QueueCreator). `ShellViewModel` implementiert alle Modal-Host-Ports (Content-Modal über `RunBlockingContentModal` mit `DispatcherFrame`). `App.xaml.cs` = Composition Root (Serilog, DI, `WpfGlobalExceptionHandler`), `UpdateCheckCoordinator` startet nach `MainWindow.Show()`.
- **`MacroRecorder.Wpf`** – wiederverwendbare UI-Bausteine: `ThemeCatalog`/`ThemeResourceBuilder`/`ThemePalettes`, `Controls/DigitsOnlyNumericBox`, `Controls/IconButton`. Wird von App referenziert.
- **Tests:** `MacroRecorder.Domain.Tests` (`net10.0` → Domain), `MacroRecorder.Application.Tests` (`net10.0` → Application + Domain), `MacroRecorder.Infrastructure.Tests` (`net10.0-windows` → Infrastructure + Updater). Ordner spiegeln Produktionsstruktur. Regeln: `.cursor/rules/macro-recorder-tests.mdc`.

## 3. Dateistruktur (kritisch)
- `MacroRecorder.Domain/` – Modelle, JSON-Polymorphismus.
- `MacroRecorder.Application/Ports/` – alle Interfaces (UI + Infrastructure binden hier an).
- `MacroRecorder.Application/Timeline/` – `TimelineNormalizer`, `EventPlaybackSchedule`, `LegacyElapsedTimingMigration`, `MouseMovePathSimplifier`, `MouseMoveRecordingFilter`, `RecordingTimelineDelay`.
- `MacroRecorder.Infrastructure/{Recording,Playback,Persistence,Interop,Input,Updates}/`.
- `MacroRecorder.App/{ViewModels,Views,Services,Localization,Editor,Controls,Converters}/` – `App.xaml.cs` = Composition Root.
- `MacroRecorder.App/Localization/UiStrings{,.de}.resx` – **generiert**, nicht direkt editieren.
- `scripts/build_ui_resx.py` – **Single Source of Truth** für UI-Strings (`python scripts/build_ui_resx.py`).
- `scripts/set-app-version.ps1` – set release version in csproj + project map (`.\scripts\set-app-version.ps1 0.0.3`).
- `scripts/build-portable.ps1` – self-contained win-x64 Publish → `MacroRecorderByRezondes.exe` + `MacroRecorderByRezondes.Updater.exe` in `artifacts/portable/MacroRecorder-portable-win-x64-<Version>.zip`.
- `README.md` – user guide (portable setup, features) and developer entry point (build, release, localization).
- `.github/workflows/ci.yml` – PR gegen `master` → `dotnet test MacroRecorderByRezondes.sln -c Release` auf `windows-latest`.
- `.github/workflows/release.yml` – Tag `v*.*.*` → Version-Check → `dotnet test` → portable ZIP → GitHub Release.
- `MacroRecorder.Domain.Tests` / `MacroRecorder.Application.Tests` (`net10.0`) / `MacroRecorder.Infrastructure.Tests` (`net10.0-windows`) – xUnit + coverlet; Regeln in `.cursor/rules/macro-recorder-tests.mdc`.
- `.cursor/map/project-map.md` – komprimierte Projekt-Memory-Map (Ist-Zustand für Agenten).
- `.cursor/prompts/project-map-prompt.md` – Anweisung Map zu lesen/aktualisieren.
- `.cursor/rules/macro-recorder-{conventions,tests,localization,git-commit}.mdc` – verbindliche Regeln.
- `%LocalAppData%/MacroRecorderByRezondes/` – Laufzeitdaten (`settings.json`, `macros/`, `playback-hotkeys.json`, `logs/`, Queue-Store).

## 4. Wichtige Datenmodelle / State
- **`Macro`:** `Id`, `Name`, `Metadata`, `Events` (List<`RecordedInputEvent`>), `DocumentVersion` (Ulid, bumped bei Struktur-/Inhaltsänderung), `CreatedAtUtc`, `LastModifiedAtUtc`, `WasModifiedAfterRecording`. Events polymorph: `KeyDown/Up`, `MouseMove`, `MouseButtonDown/Up`, `MouseWheel`, `FocusChanged` (Hwnd null = Fokus verloren; `ReferenceClientWidth/Height` + Toleranz für fokusgebundene Playback-Matching), `SyntheticWait`. Jeder Event: `DelayBefore` + `Sequence`. **Aufnahme:** `DelayBefore` = Stopwatch-Abstand seit letztem gespeichertem Event (`RecordingTimelineDelay`); gefilterte/übersprungene Moves akkumulieren Wartezeit auf den nächsten gespeicherten Schritt. `SyntheticWait.AdditionalDelay` bei Anchor-Modus ohne Mausbewegungen bleibt automatisch.
- **`RecordingMetadata`:** `SchemaVersion=2`, `RecordedAtUtc`, `StopwatchFrequency`, `UseFocusBoundMouseCoordinates`, `MouseAnchor`, `RecordMouseMoves`, optional `RecordingEnvironment`.
- **`AppSettings`** (`AppSettingsStore`): `uiCulture` (de/en), `appearanceTheme`+`appearanceIsDark`, `recordingMouseMoveMinPixels`, `playbackUserInterruptGraceMs`, `playbackFocusBringWindowToForeground`/`RestoreIfMinimized`, `mainWindowPlacement`, `checkForUpdatesOnStartup`, `lastDismissedUpdateVersion`, `enableVerboseLogging`.
- **`UpdateCheckResult`:** `CurrentVersion`, `LatestVersion`, `IsUpdateAvailable`, `ReleasePageUrl`, `PortableZipDownloadUrl?`, `ReleaseNotes?`.
- **`MacroQueueDocument`/`QueueStep`:** Persistierte Queue-Definition (Wiederholungen, Pausen, Loop) für `MacroQueueRunner`.

## 5. Kritische Entscheidungen & Regeln
- **Ein Hauptfenster** (`MainWindow`) — keine weiteren Top-Level-`Window`s. Dialoge = Overlays via `ShellViewModel` (Content-/Confirm-/Info-/Unsaved-Modal).
- **Editor-Reihenfolge:** `EditorTimelineGrouper` darf **nicht** nach `Sequence` sortieren (neue Aufnahmen vergeben kleine Sequenznummern → Reihenfolge folgt `_flatEvents`). `OrderBy(Sequence)` nur beim Laden/Speichern/Playback. Zeile↔Event-Mapping via `ReferenceEquals` (doppelte Sequenznummern möglich).
- **Playback-Interrupt:** Echte Nutzer-Eingabe während Playback → `PlaybackInterruptedByUserException` (RESX-Key, nicht `OperationCanceledException`).
- **Fokusgebundene Aufnahme:** Maus-Down → `WindowFromPoint` vor Koordinaten; Client-Koordinaten via `_lastForeground`; Tastatur → `GetForegroundWindow`. Kein 120-ms-Fokus-Poll in diesem Modus (verhindert Oszillation mit `WindowFromPoint`). Fokus-Handles immer `GA_ROOT`; transientes Fokus-verloren (hwnd 0) wird nicht aufgezeichnet.
- **Hotkeys-Persistenz:** `playback-hotkeys.json` im App-Root (nicht `macros/`); Legacy-Migration aus `macros/` beim Start; `JsonMacroRepository.ListAsync` überspringt diese Datei.
- **Lokalisierung:** ausschließlich `scripts/build_ui_resx.py` editieren, danach ausführen. `UiStrings*.resx` werden überschrieben. Keine harten Strings in ViewModels/XAML (außer tiefe technische Exceptions).
- **Numerische Eingaben:** immer `MacroRecorder.Wpf.Controls.DigitsOnlyNumericBox` (mit `MinimumValue`/`MaximumValue`), kein rohes `TextBox`.
- **Distribution:** ausschließlich portable ZIP (2 EXEs). MSI/WiX entfernt. Updates: `UpdateCheckCoordinator` → Modal **Jetzt aktualisieren** → `IAppUpdateService` startet `Updater.exe` → Download/Sync → Neustart; `%LocalAppData%` unberührt.
- **Versionsquelle:** `MacroRecorder.App.csproj` `<Version>` = Single Source. CI-Tag `v0.0.x` muss matchen.
- **Commit-Konvention:** Karma/Angular (`<type>(<scope>): <subject>`), max. 72 Zeichen, kein `Co-authored-by: Cursor`.
- **Mausbewegungs-Reduktion:** Live in `LowLevelRecordingEngine` (Mindestabstand + Collinearity-Skip, Drag-Schutz via Button-State, Anchor-Flush vor Klick/Rad). Nachbearbeitung: `MouseMovePathSimplifier` (RDP, epsilon = `recordingMouseMoveMinPixels`) nach Stop/Save/Share; Drag-Segmente (Moves zwischen Button-Down/Up) unangetastet. Share-JSON: pretty (`MacroJsonFileFormat.Serialize`, `WriteIndented=true`).

## 6. Aktueller Fokus / Next Steps
- [x] Release `v0.0.9` pushen
- [ ] Release `v0.0.10` pushen
