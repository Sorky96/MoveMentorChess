---
name: avalonia-view-snapshots
description: Render and inspect MoveMentorChess Avalonia desktop views without opening the full GUI. Use when Codex needs to verify UI layout, clipping, overlap, responsive behavior, or visual regressions in this repository by building the snapshot harness, running headless Avalonia/Skia renders at one or more resolutions, and inspecting the generated PNG files.
---

# Avalonia View Snapshots

Use this skill when a MoveMentorChess UI change needs visual verification.

## Workflow

1. Build the solution or snapshot project before rendering:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet build MoveMentorChess.sln --no-restore -m:1 --verbosity minimal
```

2. Render snapshots with the headless harness:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
dotnet run --project MoveMentorChess.App.Snapshots\MoveMentorChess.App.Snapshots.csproj --no-build -- --output artifacts\view-snapshots
```

3. Inspect representative PNGs with `view_image`. Check at least the changed view at `1366x768` and `1920x1080`; for layout-sensitive work also inspect `1440x900` and `2560x1440`.

4. Report any visible clipping, overlapping text, empty states, suspicious scroll placement, or unexpectedly blank content before finalizing code changes.

## Useful Options

- `--views main,analysis,opening-trainer,profiles,opening-coverage,settings`
- `--sizes 1366x768,1920x1080`
- `--settle-ms 8000`
- `--database <path-to-sqlite-db>`
- `--output artifacts\view-snapshots`

The harness prefers the local analysis database and selects the richest available saved data: the analysis result with the most highlighted mistakes/moves, the longest imported game, and the profile with the most data. If no database is available, it still renders fallback empty states where possible.

## Review Heuristics

- Verify that text fits inside buttons, cards, badges, sidebars, and list rows.
- Compare compact and wide resolutions for clipped controls or oversized board/list areas.
- Confirm real-data views are not accidentally replaced by loading or empty placeholders.
- Treat generated PNGs as review artifacts, not golden baselines.
