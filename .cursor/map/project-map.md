# Projekt-Memory-Map

## 1. Projekt-Kern
- **Ziel:** Windows-Desktop-App zum Aufzeichnen, Editieren, Wiedergeben und Verketten von Tastatur-/Maus-Makros (inkl. Fokus-/Fenstererkennung).
- **Tech-Stack:** .NET 10, WPF (`net10.0-windows`, x64), MVVM (`CommunityToolkit.Mvvm`), `Microsoft.Extensions.Hosting`/DI, RESX-Lokalisierung (DE/EN), WinAPI Low-Level-Hooks + `SendInput`, JSON-Persistenz (`System.Text.Json`, `Ulid`).
- **Aktueller Status:** Version `0.0.5` in csproj; Release via Tag `v0.0.5` + GitHub Actions (portable ZIP). In-App-Update: Check + **Jetzt aktualisieren** via stabiler `MacroRecorderByRezondes.Updater.exe`.

## 2. Architektur & Abhängigkeiten (DDD-Schichten)
- **`MacroRecorder.Domain`** – pure Modelle (`Macro`, `RecordedInputEvent`-Hierarchie polymorph via JSON-Discriminator, `RecordingMetadata`, `MacroId`, `PlaybackKeyChord`, `MacroQueueDocument`/`QueueStep`). Keine Plattform-/UI-Abhängigkeit.
- **`MacroRecorder.Application`** – Ports + Orchestrierung. `IPlaybackService`, `IRecordingEngine`, `IMacroRepository`, `IPlaybackHotkeyStore`, `IUserDialogService`, `IUiLocalizer`, `IUpdateCheckService`, `IAppUpdateService`, `IExternalUriOpener`, Modal-Host-Ports (`I*ModalHost`). Services: `RecordingCoordinator`, `MacroWorkspaceService`, `MacroQueueRunner`, `TimelineNormalizer`, `PlaybackDurationEstimator`. → ruft Domain.
- **`MacroRecorder.Infrastructure`** – Port-Impls. `LowLevelRecordingEngine` (WH_KEYBOARD_LL/WH_MOUSE_LL), `SendInputPlaybackService`, `JsonMacroRepository`/`JsonPlaybackHotkeyStore`/`MacroQueueFileStore` (in `%LocalAppData%/MacroRecorderByRezondes/`), `FocusWindowMatcher`, `Updates/GitHubReleaseUpdateCheckService` + `PortableAppUpdateLauncher`, `ProcessExternalUriOpener`. → registriert in `AddMacroRecorderInfrastructure()`.
- **`MacroRecorder.Updater`** – kleines Konsolen-EXE (`MacroRecorderByRezondes.Updater.exe`): wartet auf App-Exit, lädt Release-ZIP, ersetzt Installationsdateien außer Updater, startet Haupt-EXE neu.
- **`MacroRecorder.App`** (WPF) – Shell + ViewModels. Ein `MainWindow` mit `ShellViewModel.CurrentPage` (DataTemplates für Overview/Editor/Settings/Record/QueueCreator). `ShellViewModel` implementiert alle Modal-Host-Ports (Content-Modal über `RunBlockingContentModal` mit `DispatcherFrame`). `App.xaml.cs` = Composition Root (DI), `UpdateCheckCoordinator` startet nach `MainWindow.Show()`.
- **`MacroRecorder.Wpf`** – wiederverwendbare UI-Bausteine: `ThemeCatalog`/`ThemeResourceBuilder`/`ThemePalettes`, `Controls/DigitsOnlyNumericBox`, `Controls/IconButton`. Wird von App referenziert.

## 3. Dateistruktur (kritisch)
- `MacroRecorder.Domain/` – Modelle, JSON-Polymorphismus.
- `MacroRecorder.Application/Ports/` – alle Interfaces (UI + Infrastructure binden hier an).
- `MacroRecorder.Application/Timeline/` – `TimelineNormalizer`, `EventPlaybackSchedule`, `LegacyElapsedTimingMigration`.
- `MacroRecorder.Infrastructure/{Recording,Playback,Persistence,Interop,Input,Updates}/`.
- `MacroRecorder.App/{ViewModels,Views,Services,Localization,Editor,Controls,Converters}/` – `App.xaml.cs` = Composition Root.
- `MacroRecorder.App/Localization/UiStrings{,.de}.resx` – **generiert**, nicht direkt editieren.
- `scripts/build_ui_resx.py` – **Single Source of Truth** für UI-Strings (`python scripts/build_ui_resx.py`).
- `scripts/set-app-version.ps1` – set release version in csproj + project map (`.\scripts\set-app-version.ps1 0.0.3`).
- `scripts/build-portable.ps1` – self-contained win-x64 Publish → `MacroRecorderByRezondes.exe` + `MacroRecorderByRezondes.Updater.exe` in `artifacts/portable/MacroRecorder-portable-win-x64-<Version>.zip`.
- `README.md` – user guide (portable setup, features) and developer entry point (build, release, localization).
- `.github/workflows/release.yml` – Tag `v*.*.*` → Version-Check (Tag ↔ csproj) → portable ZIP → GitHub Release.
- `.cursor/map/project-map.md` – komprimierte Projekt-Memory-Map (Ist-Zustand für Agenten).
- `.cursor/prompts/project-map-prompt.md` – Anweisung Map zu lesen/aktualisieren.
- `.cursor/rules/macro-recorder-{conventions,localization,git-commit}.mdc` – verbindliche Regeln.
- `%LocalAppData%/MacroRecorderByRezondes/` – Laufzeitdaten (`settings.json`, `macros/`, Queue-, Hotkey-Stores).

## 4. Wichtige Datenmodelle / State
- **`Macro`:** `Id`, `Name`, `Metadata`, `Events` (List<`RecordedInputEvent`>), `DocumentVersion` (Ulid, bumped bei Struktur-/Inhaltsänderung), `CreatedAtUtc`, `LastModifiedAtUtc`, `WasModifiedAfterRecording`. Events polymorph: `KeyDown/Up`, `MouseMove`, `MouseButtonDown/Up`, `MouseWheel`, `FocusChanged` (Hwnd null = Fokus verloren; `ReferenceClientWidth/Height` + Toleranz für fokusgebundene Playback-Matching), `SyntheticWait`. Jeder Event: `DelayBefore` + `Sequence`.
- **`RecordingMetadata`:** `SchemaVersion=2`, `RecordedAtUtc`, `StopwatchFrequency`, `UseFocusBoundMouseCoordinates`, `MouseAnchor`, `RecordMouseMoves`, optional `RecordingEnvironment`.
- **`AppSettings`** (`AppSettingsStore`): `uiCulture` (de/en), `appearanceTheme`+`appearanceIsDark`, `recordingMouseMoveMinPixels`, `playbackUserInterruptGraceMs`, `playbackFocusBringWindowToForeground`/`RestoreIfMinimized`, `mainWindowPlacement`, `checkForUpdatesOnStartup`, `lastDismissedUpdateVersion`.
- **`UpdateCheckResult`:** `CurrentVersion`, `LatestVersion`, `IsUpdateAvailable`, `ReleasePageUrl`, `PortableZipDownloadUrl?`, `ReleaseNotes?`.
- **`MacroQueueDocument`/`QueueStep`:** Persistierte Queue-Definition (Wiederholungen, Pausen, Loop) für `MacroQueueRunner`.

## 5. Kritische Entscheidungen & Regeln
- **Ein Hauptfenster** (`MainWindow`) — keine weiteren Top-Level-`Window`s. Dialoge = Overlays via `ShellViewModel` (Content-/Confirm-/Info-/Unsaved-Modal).
- **Editor-Reihenfolge:** `EditorTimelineGrouper` darf **nicht** nach `Sequence` sortieren (neue Aufnahmen vergeben kleine Sequenznummern → Reihenfolge folgt `_flatEvents`). `OrderBy(Sequence)` nur beim Laden/Speichern/Playback. Zeile↔Event-Mapping via `ReferenceEquals` (doppelte Sequenznummern möglich).
- **Playback-Interrupt:** Echte Nutzer-Eingabe während Playback → `PlaybackInterruptedByUserException` (RESX-Key, nicht `OperationCanceledException`).
- **Lokalisierung:** ausschließlich `scripts/build_ui_resx.py` editieren, danach ausführen. `UiStrings*.resx` werden überschrieben. Keine harten Strings in ViewModels/XAML (außer tiefe technische Exceptions).
- **Numerische Eingaben:** immer `MacroRecorder.Wpf.Controls.DigitsOnlyNumericBox` (mit `MinimumValue`/`MaximumValue`), kein rohes `TextBox`.
- **Distribution:** ausschließlich portable ZIP (2 EXEs). MSI/WiX entfernt. Updates: `UpdateCheckCoordinator` → Modal **Jetzt aktualisieren** → `IAppUpdateService` startet `Updater.exe` → Download/Sync → Neustart; `%LocalAppData%` unberührt.
- **Versionsquelle:** `MacroRecorder.App.csproj` `<Version>` = Single Source. CI-Tag `v0.0.x` muss matchen.
- **Commit-Konvention:** Karma/Angular (`<type>(<scope>): <subject>`), max. 72 Zeichen, kein `Co-authored-by: Cursor`.

## 6. Aktueller Fokus / Next Steps
- [x] WiX-Installer + MSI-Skripte entfernt (Solution + `.gitignore` bereinigt).
- [x] `scripts/build-portable.ps1` + `.github/workflows/release.yml` (Tag-getriggerte ZIP-Releases).
- [x] In-App-Update: `IUpdateCheckService` (GitHub API) + `UpdateCheckCoordinator` + `UpdateAvailableView`-Modal + Settings-Toggle.
- [x] Fix: `IUpdatePromptModalHost` direkt im DI registriert (Startup-Crash behoben).
- [x] Project-Map + Map-Update-Prompt unter `.cursor/` (committed).
- [x] Version-Konsistenz-Check (Git-Tag ↔ `MacroRecorder.App.csproj` `<Version>`) im Release-Workflow.
- [x] Release `v0.0.1` (portable ZIP auf GitHub Releases).
- [x] Release `v0.0.2` pushen (Update-Check-Fix, Branding, Queue-Experimentell-Hinweis, Tests).
- [x] Release `v0.0.3` pushen
- [x] Release `v0.0.4` pushen
- [ ] Release `v0.0.5` pushen
