# Architecture and Clean Code Review - MoveMentorChess

Audit date: 2026-06-13
Baseline: updated `master`, commit `8531401`
Working branch: `architecture/repo-clean-code-review`
Scope: full repository review of `MoveMentorChess.sln`, project boundaries, composition, largest classes, test architecture, localization, persistence, and one-type-per-file hygiene.

## 1. Business Analyst

### Goal

Assess whether the current architecture can support further development of analysis, opening training, player profiles, localization, tracking, and persistence without increasing change cost or regression risk.

### Acceptance Criteria

- Review is based on current `master`.
- Findings are saved to a tracked Markdown file.
- Findings include architecture and clean-code risks, concrete files, priority, and next steps.
- The review explicitly checks files with multiple classes, records, structs, interfaces, or enums.
- No production refactoring is performed in this audit PR.

## 2. Executive Summary

Overall assessment: the repository is on a healthy cleanup trajectory, but it is still in a transitional architecture state.

Strong points:

- The solution has meaningful project boundaries: `Domain`, `Analysis`, `Opening`, `Training`, `Profiles`, `Persistence`, `Presentation`, `Tracking`, `Localization`, `Engine`, and `App`.
- Central quality settings are strong: nullable enabled, recommended analyzers, deterministic builds, and warnings as errors in `Directory.Build.props`.
- Existing architecture tests in `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs` already protect several important boundaries.
- `SqliteAnalysisStore` has become a thin facade over specialized SQLite modules instead of owning SQL directly.
- Recent localization work adds a dedicated `MoveMentorChess.Localization` project and starts moving UI strings into resources.

Main risks:

- Several public files still contain multiple top-level types. This is the clearest clean-code policy gap requested for this review.
- `OpeningTrainerWindowViewModel` and `MainWindowViewModel` remain very large orchestration hubs.
- `IAnalysisStore` and `SqliteAnalysisStore` still aggregate too many bounded contexts behind one facade.
- Static/global services remain in settings, localization, runtime cleanup, and cache paths.
- Presentation is useful, but not yet fully framework-neutral or dependency-light.
- Some architecture tests are good, but budgets are permissive enough to preserve large files instead of forcing continued extraction.

## 3. Repository Metrics

Current source metrics, excluding `bin` and `obj`:

| Area | C# files | Lines |
| --- | ---: | ---: |
| Production/source projects | 503 | 50,390 |
| Tests | 71 | 17,297 |

Largest source areas:

| Project | Files | Lines |
| --- | ---: | ---: |
| `MoveMentorChess.App` | 83 | 13,859 |
| `MoveMentorChess.Training` | 32 | 7,573 |
| `MoveMentorChess.Analysis` | 58 | 6,945 |
| `MoveMentorChess.Domain` | 193 | 5,683 |
| `MoveMentorChess.Persistence` | 26 | 3,935 |
| `MoveMentorChess.Tracking` | 32 | 3,378 |
| `MoveMentorChess.Profiles` | 22 | 2,606 |
| `MoveMentorChess.Presentation` | 27 | 1,816 |

Largest individual files:

| File | Lines | Risk |
| --- | ---: | --- |
| `MoveMentorChess.App/ViewModels/OpeningTrainerWindowViewModel.cs` | 2,498 | UI state machine, commands, telemetry, study flow, text, and board state |
| `MoveMentorChess.App/ViewModels/MainWindowViewModel.cs` | 1,673 | board state, PGN import, engine lifecycle, cache, analysis, formatting |
| `MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs` | 1,308 | snapshot loading, weakness integration, theory line construction, source selection |
| `MoveMentorChess.App.Snapshots/Program.cs` | 1,074 | harness, host, fixtures, factories, and telemetry fakes in one executable file |
| `MoveMentorChess.Training/OpeningTrainerWorkspaceService.cs` | 1,041 | app-level training orchestration |
| `MoveMentorChess.Domain/Models/ChessGame.cs` | 995 | rules engine plus public result records |
| `MoveMentorChess.App/Renderers/ProfileCoachSectionRenderer.cs` | 904 | large Avalonia renderer |

## 4. Architect View

Recommended target direction:

```text
App / Composition
  -> Presentation adapters / Avalonia renderers
  -> Application use cases: Analysis, Training, Profiles, Opening
  -> Infrastructure adapters: Persistence, Engine, Tracking, Localization runtime

Domain
  <- used by all layers

Persistence
  -> implements narrow ports
  -> owns SQLite, schema, storage paths, and cache persistence
```

The current dependency graph is close to this, but several transitional compatibility facades still create gravity:

- `MoveMentorChess.App/MoveMentorChess.App.csproj:20-29` references nearly every product project, which is expected for a composition/UI project, but means composition discipline matters.
- `MoveMentorChess.Presentation/MoveMentorChess.Presentation.csproj:14-18` references `Analysis`, `Domain`, `Localization`, `Opening`, and `Profiles`, so Presentation is not just passive models. It is already a presentation/use-case adapter layer.
- `MoveMentorChess.Domain/Models/IAnalysisStore.cs:3-88` defines many store contracts in one file, and `MoveMentorChess.Persistence/SqliteAnalysisStore.cs:3-14` implements almost all of them.

## 5. Findings

### P1 - Enforce One Public Top-Level Type Per File

Risk: high maintainability risk.

The repo has many files with multiple top-level types. Private nested helper types are sometimes acceptable, but public top-level records/enums/interfaces sharing one file makes ownership and review harder, increases merge conflicts, and hides API surface.

High-priority examples:

- `MoveMentorChess.Domain/Models/IAnalysisStore.cs:3` contains 11 public store interfaces in one file.
- `MoveMentorChess.Persistence/OpeningSeedBootstrapper.cs:3` contains `OpeningSeedBootstrapper`, `IOpeningSeedRuntimeEnvironment`, `SystemOpeningSeedRuntimeEnvironment`, and `OpeningSeedBootstrapResult`.
- `MoveMentorChess.Profiles/PlayerStrengthEstimator.cs:3` contains the public estimator interface, input record, heuristic implementation, and ML placeholder implementation.
- `MoveMentorChess.Engine/StockfishEngine.cs:9` contains `StockfishEngine`, `StockfishEngineOptions`, and `EvaluationSummary`.
- `MoveMentorChess.Domain/Models/ChessGame.cs:8` contains `ChessGame` plus public `AppliedMoveInfo` and `LegalMoveInfo`.
- `MoveMentorChess.Domain/Models/TrainingRecommendationCard.cs:3` contains a record plus three public enums.
- `MoveMentorChess.Presentation/Models/AnalysisSelectionState.cs:3` contains a public enum, two records, and a class.
- `MoveMentorChess.App/ViewModels/MainWindowViewModel.cs:15` contains the ViewModel plus public import result records near the end of the file.
- `MoveMentorChess.App/ViewModels/OpeningTrainerWindowViewModel.cs:11` contains the ViewModel plus public choice/card records near the end of the file.
- `MoveMentorChess.App/Controls/ChessBoardView.cs:10` contains `ChessBoardView` and `BoardSquarePressedEventArgs`.
- `MoveMentorChess.App/ViewModels/RelayCommand.cs:5` contains both `RelayCommand` and `RelayCommand<T>`.

Recommendation:

- Adopt a rule: one public top-level type per file.
- Allow private nested types only when they are small and truly implementation-local.
- Move public records/enums/interfaces into their own files even when they are small.
- Add an architecture test that scans production `.cs` files for multiple public top-level declarations, with a short allow-list for deliberate exceptions.

### P1 - Continue Splitting `OpeningTrainerWindowViewModel`

Risk: high regression risk for training UX.

`MoveMentorChess.App/ViewModels/OpeningTrainerWindowViewModel.cs:11` is 2,498 lines. It has useful extractions already, especially `OpeningTrainerSessionController`, `OpeningTrainerResultsViewModel`, `OpeningStudyFeedbackAnimator`, and telemetry adapter work. However, the ViewModel still coordinates:

- page navigation and state,
- recommendation and profile selection,
- command enablement,
- opening overview loading,
- guided study state,
- board interaction,
- telemetry,
- localized text composition,
- result and next-action presentation.

The architecture guard `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs:14` allows this file up to 2,500 lines. The current file is effectively at that ceiling.

Recommendation:

- Extract `OpeningTrainerSelectionViewModel` for profile, side, intensity, and today recommendation selection.
- Extract `OpeningTrainerOverviewViewModel` for selected opening overview and priorities.
- Keep `OpeningTrainerSessionController` focused on session flow and move it toward command-agnostic use-case state.
- Lower the architecture budget in steps after every extraction, instead of keeping a 2,500-line ceiling.

### P1 - Reduce `MainWindowViewModel`

Risk: high regression risk for import, engine, and board workflows.

`MoveMentorChess.App/ViewModels/MainWindowViewModel.cs:15` is 1,673 lines and still owns too many reasons to change:

- `MainWindowViewModel.cs:22-28` holds chess game state, engine instance, imported game, replay list, and selection state.
- `MainWindowViewModel.cs:71-77` wires many commands directly to orchestration methods.
- `MainWindowViewModel.cs:374-452` handles PGN import, replay setup, persistence, status messaging, and UI refresh.
- The file also creates engine-facing summaries, board arrows, analysis summaries, and cache interactions.

Recommendation:

- Extract `ImportedGameReplayController` for replay cursor and board projection.
- Extract `MainBoardStatePresenter` for board squares, arrows, selected piece, and move options.
- Extract `BulkPgnAnalysisCoordinator` for multi-game import and analysis.
- Move display text and evaluation formatting into `MoveMentorChess.Presentation`.

### P1 - Split Store Contracts And Keep `SqliteAnalysisStore` As A Compatibility Facade

Risk: high coupling risk.

`MoveMentorChess.Domain/Models/IAnalysisStore.cs:3-88` centralizes many unrelated ports in one file. `MoveMentorChess.Persistence/SqliteAnalysisStore.cs:3-14` implements analysis result storage, imported game storage, feedback, window state, opening tree, theory, line context, training history, and telemetry.

The SQL itself is already extracted, which is good. The remaining risk is API shape: consumers can still receive a broad store when they need one narrow capability.

Recommendation:

- Split `IImportedGameStore`, `IAnalysisResultStore`, `IStoredMoveAnalysisStore`, `IAdviceFeedbackStore`, `IAnalysisWindowStateStore`, `IOpeningTreeStore`, `IOpeningTheoryStore`, `IOpeningLineContextStore`, `IOpeningTrainingHistoryStore`, and `IOpeningTrainingTelemetryStore` into separate files.
- Keep `IAnalysisStore` only as a temporary compatibility facade.
- Prefer constructor injection of narrow ports in new code.
- Add tests that block new constructor parameters typed as `IAnalysisStore` outside composition or compatibility adapters.

### P2 - Static Runtime And Settings Services Still Leak Into UI

Risk: medium testability and lifecycle risk.

Examples:

- `MoveMentorChess.App/App.axaml.cs:20-22` loads settings, cleans up Llama processes, wires shutdown, and creates the main window directly.
- `MoveMentorChess.App/Views/SettingsWindow.axaml.cs:11-17` loads GPU, Stockfish, and application settings from static stores in the window constructor.
- `MoveMentorChess.App/Views/SettingsWindow.axaml.cs:101-116` saves settings and shuts down `LlamaCppServerManager.Instance` from code-behind.
- `MoveMentorChess.Persistence/GameAnalysisCache.cs:3-14` is a static global in-memory cache backed by a global provider.
- `MoveMentorChess.Localization/Localizer.cs:6-39` uses static application and async-local culture state.

Recommendation:

- Introduce an `IApplicationSettingsService` or `ISettingsWorkflow` in the App layer and wire it from composition.
- Keep `SettingsWindow` as a view/controller for controls and file pickers.
- Keep static facades only as compatibility wrappers while tests and composition move to injectable services.

### P2 - Localization Is Useful But Still Transitional

Risk: medium UX consistency risk.

The new localization project is a good step, but the transition is incomplete:

- `MoveMentorChess.App/Localization/LocalizeExtension.cs:6-13` resolves text once through `Localizer.Text`. Existing controls will not automatically update when the application culture changes.
- `MoveMentorChess.App/Views/AnalysisWindow.axaml:80` and many later labels in the same file are still hard-coded English strings.
- `MoveMentorChess.App/Views/MainWindow.axaml:41-42` relies heavily on ViewModel-provided text, but other labels remain mixed across XAML and ViewModel properties.
- `MoveMentorChess.App/Views/SettingsWindow.axaml.cs:141-170` applies localized text manually in code-behind.

Recommendation:

- Choose one localization strategy for Avalonia: compiled resource bindings, reactive localizer service, or explicit window recreation after language change.
- Add an architecture or snapshot test that flags hard-coded visible text in XAML outside an allow-list.
- Move settings option labels and narration/explanation label construction out of code-behind.

### P2 - Presentation Layer Is Not Yet Fully Neutral

Risk: medium boundary risk.

`MoveMentorChess.Presentation` is useful, but it is not a pure model/formatting layer:

- `MoveMentorChess.Presentation/MoveMentorChess.Presentation.csproj:14-18` references `Analysis`, `Domain`, `Localization`, `Opening`, and `Profiles`.
- `MoveMentorChess.Presentation/Helpers/BoardThumbnailRenderer.cs:1-8` returns `System.Drawing.Bitmap` and consumes `System.Drawing.Image`.
- `MoveMentorChess.Presentation/Models/AnalysisMistakePresentation.cs:8` defines `SelectedMistakeViewItem` in the same file as the static presenter.

Recommendation:

- Treat Presentation as a deliberate adapter layer, or split it into `Presentation.Models` and `Presentation.Rendering`.
- Keep Avalonia and GDI-specific rendering outside framework-neutral presentation models.
- Split presentation records and presenters into one public type per file.

### P2 - Training Session Builder Is Still A Pipeline Object With Too Many Steps

Risk: medium change amplification risk.

`MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs:11` is 1,308 lines. It performs snapshot loading, deduplication, weakness report integration, replay loading, source-specific position construction, branch construction, tags, references, and line assembly.

Recommendation:

- Extract `OpeningTrainingSnapshotLoader`.
- Extract source builders for example-game, opening-weakness, and first-mistake positions.
- Keep `OpeningTrainingSessionBuilder` as a coordinator that composes source builders and delegates selection to `OpeningTrainingPositionSelector`.

### P3 - Architecture Tests Need Sharper Budgets

Risk: medium-to-low governance risk.

Existing tests are valuable, but several budgets encode "do not get worse" instead of "continue cleanup":

- `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs:14` allows `OpeningTrainerWindowViewModel.cs` up to 2,500 lines.
- `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs:15` allows `AnalysisWindow.axaml.cs` up to 550 lines.
- There is no current guard for one-public-type-per-file.

Recommendation:

- Add a new architecture test for public top-level type count.
- Ratchet line budgets down after each extraction.
- Add project-boundary tests for `Presentation` once its intended role is clarified.

### P3 - Tracked Legacy Project Needs An Ownership Decision

Risk: low confusion risk.

`MoveMentorChessServices/MoveMentorChessServices.csproj:4` is tracked, but `MoveMentorChess.sln` only includes `MoveMentorChessServices.Tests`, not the `MoveMentorChessServices` project itself. The tracked project currently has no tracked C# source files and carries an old service-style name while the active code lives in split `MoveMentorChess.*` projects.

Recommendation:

- Decide whether `MoveMentorChessServices` is a compatibility artifact, an obsolete migration shell, or should be removed.
- If it is kept, document its purpose and add it to the solution intentionally.
- If it is obsolete, remove the tracked project and icon in a separate cleanup PR.

## 6. Suggested Implementation Order

1. Add one-public-type-per-file architecture test with a short allow-list.
2. Split the highest-signal public type files: `IAnalysisStore.cs`, `OpeningSeedBootstrapper.cs`, `PlayerStrengthEstimator.cs`, `StockfishEngine.cs`, `ChessGame.cs`, and the public records at the ends of `MainWindowViewModel.cs` and `OpeningTrainerWindowViewModel.cs`.
3. Extract one slice from `OpeningTrainerWindowViewModel`, preferably selection/recommendation state, then lower the budget.
4. Extract PGN replay/import coordination from `MainWindowViewModel`.
5. Create an injectable settings workflow for `SettingsWindow`.
6. Finish localization for hard-coded XAML labels and decide how runtime language changes should refresh visible text.
7. Decide the fate of `MoveMentorChessServices`.

## 7. Validation

Executed:

```powershell
dotnet test MoveMentorChess.sln --no-restore -m:1 --verbosity minimal
```

Result: 481 passed, 0 failed, 0 skipped.

## 8. Reviewer Notes

No production code was changed as part of this audit. The only intended repo change is this Markdown review.

Residual risk: this is a static review. It identifies architecture and clean-code risks, but does not prove runtime correctness or UI layout. Use targeted tests and Avalonia snapshots for follow-up UI refactors.
