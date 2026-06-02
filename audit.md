# Clean Code / Clean Architecture Audit

Audit date: 2026-06-02
Baseline: fresh `master`, commit `6eb556d`
Scope: `MoveMentorChess.sln`, project structure, layer dependencies, largest classes, high-coupling areas, architecture tests, and maintainability risks.

## 1. Business Analyst

### Business Goal

The goal of this audit is to assess whether the current MoveMentorChess architecture can support further product development without increasing the cost of changes in UI workflows, game analysis, opening training, player profiles, tracking, and SQLite-backed persistence.

### Acceptance Criteria

- The audit is based on the current `master` branch.
- Findings are added in a new `audit.md` file.
- Recommendations are practical and include priority, risk, and example files.
- No production refactoring is performed as part of the audit.

## 2. Executive Summary

Overall assessment: **the architecture is moving in the right direction, but it is still transitional**.

Main strengths:

- The solution is already split into meaningful areas: `Domain`, `Analysis`, `Training`, `Profiles`, `Persistence`, `Presentation`, `Tracking`, and `App`.
- Central quality settings are enabled: `TreatWarningsAsErrors`, `AnalysisMode=Recommended`, nullable reference types, and implicit usings.
- Architecture tests already guard several cleanup boundaries and project-reference budgets.
- Recent work shows a healthy trend toward ports and adapters, for example `IPlayerMistakeProfileSource`, runtime environment resolvers, and cache adapters.

Main risks:

- Application-level projects still know about `Persistence`, so the dependency direction is not clean yet.
- `App` and several ViewModels still aggregate too much behavior, rendering, navigation, formatting, and workflow state.
- Static global store/cache mechanisms make state isolation and tests harder than they need to be.
- Some presentation models and renderers are Avalonia-specific or live under `ViewModels`, which blurs the boundary between framework-neutral presentation and UI implementation.
- The test suite is valuable and broad, but several test files are large enough to slow down refactoring and increase fixture duplication.

## 3. Solution State

### Metrics

- 13 projects in `MoveMentorChess.sln`.
- Approximately 486 production C# files.
- Approximately 77 test C# files.
- Local validation executed **445 tests passed, 0 failed, 0 skipped** with `dotnet test MoveMentorChess.sln --no-restore -m:1 --verbosity minimal`.

Largest areas by line count:

- `MoveMentorChess.App`: about 11.4k lines.
- `MoveMentorChess.Training`: about 6.7k lines.
- `MoveMentorChess.Analysis`: about 5.8k lines.
- `MoveMentorChess.Domain`: about 5.0k lines.
- `MoveMentorChessServices.Tests`: about 14.3k lines.

Largest risk files:

- `MoveMentorChess.App/ViewModels/OpeningTrainerWindowViewModel.cs`: 2170 lines.
- `MoveMentorChess.App/ViewModels/MainWindowViewModel.cs`: 1383 lines.
- `MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs`: 1168 lines.
- `MoveMentorChess.Training/OpeningTrainerWorkspaceService.cs`: 945 lines.
- `MoveMentorChess.Domain/Models/ChessGame.cs`: 860 lines.
- `MoveMentorChess.App/ViewModels/ProfileCoachSectionRenderer.cs`: 802 lines.
- `MoveMentorChess.App/Views/ProfilesWindow.axaml.cs`: 686 lines.

## 4. Architecture Assessment

### What Works Well

`Domain` is the base layer for chess rules and core models. `Engine`, `Opening`, `Persistence`, `Tracking`, `Analysis`, `Training`, `Profiles`, and `Presentation` are already separated at project level. `App` is the UI/composition layer, which is the right overall direction.

The architecture tests in `MoveMentorChessServices.Tests/App/AppArchitectureTests.cs` are especially valuable. They already guard:

- direct `AnalysisStoreProvider.GetStore()` usage from Views/ViewModels,
- selected project-reference boundaries,
- size budgets for files touched by previous cleanup work,
- the SQLite facade boundary, so SQL does not move back into the facade.

### Primary Weakness

The architecture still mixes **application/use-case services** with **persistence infrastructure**. Examples:

- `MoveMentorChess.Analysis/MoveMentorChess.Analysis.csproj:10` references `MoveMentorChess.Persistence`.
- `MoveMentorChess.Training/MoveMentorChess.Training.csproj:12` references `MoveMentorChess.Persistence`.
- `MoveMentorChess.Profiles/MoveMentorChess.Profiles.csproj:13` references `MoveMentorChess.Persistence`.
- `MoveMentorChess.Analysis/GlobalUsings.cs:4`, `MoveMentorChess.Training/GlobalUsings.cs:4`, and `MoveMentorChess.Profiles/GlobalUsings.cs:4` propagate that dependency globally.

This is not an immediate runtime defect, but it is the main barrier to a cleaner architecture. Use-case services should depend on ports close to the application/domain boundary, while SQLite, cache providers, and global store providers should remain infrastructure details.

## 5. Findings

### P1 - Invert Dependencies Away From `Persistence`

Risk: high maintainability risk.

`Analysis`, `Training`, and `Profiles` depend on `Persistence`, so infrastructure details leak into application logic. Evidence:

- `MoveMentorChess.Analysis/Services/GameAnalysisService.cs:35` creates `StoreBackedPlayerMistakeProfileSource` by default.
- `MoveMentorChess.Analysis/Services/StoreBackedPlayerMistakeProfileSource.cs:10` defaults to `AnalysisStoreProvider.GetStore`.
- `MoveMentorChess.Training/OpeningTrainingSessionBuilder.cs:245` and `MoveMentorChess.Training/OpeningWeaknessService.cs:175` use `GameAnalysisCacheKey` from persistence.
- `MoveMentorChess.Profiles/PlayerProfileService.cs:122` and `MoveMentorChess.Profiles/PlayerProfileService.cs:159` create `OpeningWeaknessService` directly.

Recommendation:

Create a small set of application ports for the data needed by `Analysis`, `Training`, and `Profiles`. Start incrementally by moving only the contracts those services need. Keep SQLite implementations and global providers in `Persistence`, and wire concrete implementations through `AppCompositionRoot` or small factories.

Acceptance criteria for the first cleanup PR:

- `GameAnalysisService` no longer creates a store-backed profile source by default.
- `Training` does not need to reference `Persistence` only to use cache-key data.
- An architecture test blocks new direct `AnalysisStoreProvider.GetStore` usage outside composition roots and adapters.

### P1 - Split `OpeningTrainerWindowViewModel`

Risk: high risk for future UI development.

`OpeningTrainerWindowViewModel.cs` has 2170 lines and still owns:

- wizard/page navigation state,
- commands,
- telemetry,
- daily recommendation selection,
- training session orchestration,
- animated study feedback,
- text formatting,
- move-to-UI mapping,
- study board interactions.

The file already contains a `Compatibility shims` comment around state delegated to `OpeningTrainerSessionController`, which shows a good but incomplete migration.

Recommendation:

Continue extracting one responsibility at a time:

1. `OpeningTrainerSelectionViewModel` for profile selection, filtering, and today's recommendation.
2. `OpeningTrainerOverviewViewModel` for overview and priorities.
3. `OpeningTrainerStudyViewModel` for current position, move input, hints, and board interaction.
4. `OpeningTrainerResultsViewModel` for outcome, learning plan, and next actions.

Avoid a big-bang refactor. Build and test after each meaningful slice.

### P1 - Reduce `MainWindowViewModel`

Risk: high risk for import, analysis, and board workflow stability.

`MainWindowViewModel.cs` has 1383 lines and mixes board state, PGN import, analysis cache access, evaluation formatting, bulk analysis, and UI state. It also uses `Avalonia.Media` and `MoveMentorChess.Persistence`, which makes it harder to test and harder to move toward a cleaner presentation boundary.

Recommendation:

Extract:

- `ImportedGameReplayController` or `ReplayNavigationService`,
- `MainBoardStatePresenter`,
- `BulkAnalysisCoordinator`,
- evaluation and status formatters into `Presentation`.

The ViewModel should become a thin holder of bindable state and commands.

### P2 - Separate Avalonia Renderers From ViewModels

Risk: medium, growing over time.

`MoveMentorChess.App/ViewModels/ProfileCoachSectionRenderer.cs` lives in `ViewModels`, but imports `Avalonia`, `Avalonia.Controls`, `Avalonia.Layout`, and `Avalonia.Media`, then builds controls directly. Similar Avalonia-specific signals exist in some ViewModels.

Recommendation:

- Move control renderers to `MoveMentorChess.App/Views` or `MoveMentorChess.App/Renderers`.
- Keep framework-neutral models in `MoveMentorChess.Presentation`: sections, rows, metrics, chart series, and color tokens/enums instead of `IBrush`.
- Let ViewModels expose presentation models while Avalonia renderers translate those models into controls.

### P2 - Reduce Dependence on Global State

Risk: medium risk for tests and predictability.

Global mechanisms include:

- `MoveMentorChess.Persistence/AnalysisStoreProvider.cs`,
- `MoveMentorChess.Persistence/GameAnalysisCache.cs`,
- `MoveMentorChess.Persistence/PersistenceDiagnostics.cs`,
- runtime environment singletons and `LlamaCppServerManager.Instance`.

Some of these are already wrapped by adapters, which is a good direction. Still, global state can affect test order, window isolation, and future multi-profile scenarios.

Recommendation:

- Do not remove everything at once.
- Require constructor-injected ports for new features.
- Keep global classes as compatibility facades, but avoid expanding their API.
- Add an architecture test that permits new `AnalysisStoreProvider.GetStore` usage only in composition roots or adapters.

### P2 - Continue Decomposing Training Services

Risk: medium/high.

`OpeningTrainingSessionBuilder.cs`, `OpeningTrainerWorkspaceService.cs`, `TrainingPlanService.cs`, and `OpeningWeaknessService.cs` are still large. Some responsibilities have already been extracted, but these classes still combine data loading, scoring, position selection, text construction, fallback handling, and result construction.

Recommendation:

Best extraction order:

1. Pure selectors/scorers with no store and no clock.
2. Snapshot loaders and mappers.
3. Reason/text builders moved into `Presentation` or dedicated formatter services.
4. A thin use-case orchestrator that coordinates the extracted collaborators.

### P2 - Keep Improving Test Maintainability

Risk: medium risk for refactoring speed.

The test suite is broad and useful, but some files are large. `MoveMentorChessServices.Tests/Analysis/GameAnalysisServiceTests.cs` has 2283 lines, and `MoveMentorChessServices.Tests/Persistence/SqliteAnalysisStoreTests.cs` has 1389 lines. Large test files can slow down architecture changes and encourage fixture duplication.

Recommendation:

- Add test data builders for games, analysis results, replay data, and SQLite records.
- Split tests by behavior, for example classification, advice context, cache/persistence, progress, and cancellation.
- Keep integration coverage, but move detailed edge cases into smaller unit tests where possible.

### P3 - Treat `ChessGame` as a Future Extraction Candidate

Risk: low/medium.

`ChessGame.cs` has 860 lines and owns FEN, PGN/SAN handling, legal moves, move execution, castling, en passant, promotion, attacked squares, and state snapshots. This is acceptable as a stable core, but future notation or move-generation changes will be easier if smaller components can be tested in isolation.

Recommendation:

Do not refactor it preemptively. When a notation or legal-move change appears, extract a small component such as:

- `FenParser` / `FenWriter`,
- `SanMoveResolver`,
- `LegalMoveGenerator`,
- `AttackMap`.

### P3 - Move Architecture Tests From Regression Budgets Toward Target Architecture

Risk: low, but strategically important.

`AppArchitectureTests` are useful regression guards, but current limits are permissive. For example, `OpeningTrainerWindowViewModel.cs` is allowed up to 2600 lines and `ProfilesWindow.axaml.cs` up to 820 lines. This prevents worsening, but it does not actively drive the code toward the desired structure.

Recommendation:

Add a second level of target-architecture tests:

- no new `Analysis` / `Training` / `Profiles` references to `Persistence`,
- no `Avalonia.*` in ViewModels except explicitly named adapters during transition,
- new Avalonia renderers only under `Views`, `Controls`, or `Renderers`,
- gradually lowered line budgets after each cleanup slice.

## 6. Proposed Roadmap

### Sprint 1 - Persistence Boundaries

Goal: stop further SQLite/cache leakage into use-case services.

Scope:

- Introduce ports for player profile sources, training history, imported games, and analysis results.
- Register concrete implementations in `AppCompositionRoot` or existing factories.
- Add architecture tests that block new direct `AnalysisStoreProvider.GetStore` usage outside approved locations.

### Sprint 2 - Opening Trainer ViewModel Split

Goal: reduce the largest UI hotspot.

Scope:

- Extract selection, overview, study, and results state in small steps.
- Keep the current public facade for the window during transition.
- Build and test after every meaningful extraction.

### Sprint 3 - Presentation Boundary

Goal: clean up the Avalonia boundary.

Scope:

- Move `ProfileCoachSectionRenderer` out of `ViewModels`.
- Replace ViewModel brushes/colors with presentation tokens where practical.
- Keep models in `Presentation`; render them in `App`.

### Sprint 4 - Training Service Decomposition

Goal: make recommendations and training plans easier to extend.

Scope:

- Extract selectors/scorers as pure services.
- Add dedicated snapshot loaders.
- Move text formatters out of orchestration services.

## 7. Reviewer Notes

This audit does not change production code. Runtime regression risk is negligible because the PR only adds documentation.

Content risk: the audit is based on static code review and metrics, not on a live UI walkthrough. The findings are still backed by concrete project references, file sizes, and existing architecture tests.

The main architecture decision before the next cleanup PR is where application ports should live. They can initially stay in the existing `Analysis`, `Training`, and `Profiles` projects. If the third or fourth port starts repeating patterns across those projects, a dedicated `MoveMentorChess.Application.Abstractions` project may become worthwhile.
