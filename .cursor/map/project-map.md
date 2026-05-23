# Projekt-Memory-Map

## 1. Projekt-Kern
- **Ziel:** Windows-Desktop-App zum Aufzeichnen, Editieren, Wiedergeben und Verketten von Tastatur-/Maus-Makros (inkl. Fokus-/Fenstererkennung).
- **Tech-Stack:** .NET 10, WPF (`net10.0-windows`, x64), MVVM (`CommunityToolkit.Mvvm`), `Microsoft.Extensions.Hosting`/DI, RESX-Lokalisierung (DE/EN), WinAPI Low-Level-Hooks + `SendInput`, JSON-Persistenz (`System.Text.Json`, `Ulid`).
- **Aktueller Status:** Portable Single-Build (v0.0.2), MSI/WiX entfernt; In-App-Update-Check gegen GitHub Releases live.

## 2. Architektur & Abhängigkeiten (DDD-Schichten)
- **`MacroRecorder.Domain`** – pure Modelle (`Macro`, `RecordedInputEvent`-Hierarchie polymorph via JSON-Discriminator, `RecordingMetadata`, `MacroId`, `PlaybackKeyChord`, `MacroQueueDocument`/`QueueStep`). Keine Plattform-/UI-Abhängigkeit.
- **`MacroRecorder.Application`** – Ports + Orchestrierung. `IPlaybackService`, `IRecordingEngine`, `IMacroRepository`, `IPlaybackHotkeyStore`, `IUserDialogService`, `IUiLocalizer`, `IUpdateCheckService`, `IExternalUriOpener`, Modal-Host-Ports (`I*ModalHost`). Services: `RecordingCoordinator`, `MacroWorkspaceService`, `MacroQueueRunner`, `TimelineNormalizer`, `PlaybackDurationEstimator`. → ruft Domain.
- **`MacroRecorder.Infrastructure`** – Port-Impls. `LowLevelRecordingEngine` (WH_KEYBOARD_LL/WH_MOUSE_LL), `SendInputPlaybackService`, `JsonMacroRepository`/`JsonPlaybackHotkeyStore`/`MacroQueueFileStore` (in `%LocalAppData%/MacroRecorderByRezondes/`), `FocusWindowMatcher`, `Updates/GitHubReleaseUpdateCheckService` (HttpClient → `api.github.com/repos/Rezondes/MacroRecorder/releases/latest`), `ProcessExternalUriOpener`. → registriert in `AddMacroRecorderInfrastructure()`.
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
- `scripts/build-portable.ps1` – self-contained win-x64 Publish → `artifacts/portable/MacroRecorder-portable-win-x64-<Version>.zip`.
- `.github/workflows/release.yml` – Tag `v*.*.*` → CI baut ZIP + `softprops/action-gh-release`.
- `.cursor/rules/macro-recorder-{conventions,localization,git-commit}.mdc` – verbindliche Regeln.
- `%LocalAppData%/MacroRecorderByRezondes/` – Laufzeitdaten (`settings.json`, `macros/`, Queue-, Hotkey-Stores).

## 4. Wichtige Datenmodelle / State
- **`Macro`:** `Id`, `Name`, `Metadata`, `Events` (List<`RecordedInputEvent`>), `DocumentVersion` (Ulid, bumped bei Struktur-/Inhaltsänderung), `CreatedAtUtc`, `LastModifiedAtUtc`, `WasModifiedAfterRecording`. Events polymorph: `KeyDown/Up`, `MouseMove`, `MouseButtonDown/Up`, `MouseWheel`, `FocusChanged` (Hwnd null = Fokus verloren; `ReferenceClientWidth/Height` + Toleranz für fokusgebundene Playback-Matching), `SyntheticWait`. Jeder Event: `DelayBefore` + `Sequence`.
- **`RecordingMetadata`:** `SchemaVersion=2`, `RecordedAtUtc`, `StopwatchFrequency`, `UseFocusBoundMouseCoordinates`, `MouseAnchor`, `RecordMouseMoves`, optional `RecordingEnvironment`.
- **`AppSettings`** (`AppSettingsStore`): `uiCulture` (de/en), `appearanceTheme`+`appearanceIsDark`, `recordingMouseMoveMinPixels`, `playbackUserInterruptGraceMs`, `playbackFocusBringWindowToForeground`/`RestoreIfMinimized`, `mainWindowPlacement`, `checkForUpdatesOnStartup`, `lastDismissedUpdateVersion`.
- **`UpdateCheckResult`:** `CurrentVersion`, `LatestVersion`, `IsUpdateAvailable`, `ReleasePageUrl`, `ReleaseNotes?`.
- **`MacroQueueDocument`/`QueueStep`:** Persistierte Queue-Definition (Wiederholungen, Pausen, Loop) für `MacroQueueRunner`.

## 5. Kritische Entscheidungen & Regeln
- **Ein Hauptfenster** (`MainWindow`) — keine weiteren Top-Level-`Window`s. Dialoge = Overlays via `ShellViewModel` (Content-/Confirm-/Info-/Unsaved-Modal).
- **Editor-Reihenfolge:** `EditorTimelineGrouper` darf **nicht** nach `Sequence` sortieren (neue Aufnahmen vergeben kleine Sequenznummern → Reihenfolge folgt `_flatEvents`). `OrderBy(Sequence)` nur beim Laden/Speichern/Playback. Zeile↔Event-Mapping via `ReferenceEquals` (doppelte Sequenznummern möglich).
- **Playback-Interrupt:** Echte Nutzer-Eingabe während Playback → `PlaybackInterruptedByUserException` (RESX-Key, nicht `OperationCanceledException`).
- **Lokalisierung:** ausschließlich `scripts/build_ui_resx.py` editieren, danach ausführen. `UiStrings*.resx` werden überschrieben. Keine harten Strings in ViewModels/XAML (außer tiefe technische Exceptions).
- **Numerische Eingaben:** immer `MacroRecorder.Wpf.Controls.DigitsOnlyNumericBox` (mit `MinimumValue`/`MaximumValue`), kein rohes `TextBox`.
- **Distribution:** ausschließlich portable ZIP. MSI/WiX dauerhaft entfernt. Updates über `UpdateCheckCoordinator` → Modal → Browser auf Release-Seite (Nutzer ersetzt Ordner manuell, Daten bleiben in `%LocalAppData%`).
- **Versionsquelle:** `MacroRecorder.App.csproj` `<Version>` = Single Source. CI-Tag `v0.0.x` muss matchen.
- **Commit-Konvention:** Karma/Angular (`<type>(<scope>): <subject>`), max. 72 Zeichen, kein `Co-authored-by: Cursor`.

## 6. Aktueller Fokus / Next Steps
- [x] WiX-Installer + MSI-Skripte entfernt (Solution + `.gitignore` bereinigt).
- [x] `scripts/build-portable.ps1` + `.github/workflows/release.yml` (Tag-getriggerte ZIP-Releases).
- [x] In-App-Update: `IUpdateCheckService` (GitHub API) + `UpdateCheckCoordinator` + `UpdateAvailableView`-Modal + Settings-Toggle.
- [x] Fix: `IUpdatePromptModalHost` direkt im DI registriert (Startup-Crash behoben).
- [ ] Erstes Tagging `v0.0.2` und CI-Release validieren (End-to-End Update-Flow).
- [ ] Optional: Version-Konsistenz-Check (csproj ↔ Git-Tag) im Workflow.
