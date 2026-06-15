namespace MoveMentorChess.Training;

/// <summary>
/// Builds an <see cref="OpeningTrainingSession"/> for a given player from the available analysis
/// data.  This class owns the position-building pipeline that was previously inlined inside
/// <see cref="OpeningTrainerService.TryBuildSession"/>; the service now delegates to it so that
/// it has a single reason to change.
/// </summary>
internal sealed class OpeningTrainingSessionBuilder
{
    private const int TheoryExitThresholdCp = 70;
    private const int SignificantMistakeThresholdCp = 90;

    private static readonly HashSet<string> FallbackLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "opening_principles",
        "king_safety",
        "piece_activity",
        "material_loss"
    };

    private readonly IImportedGameStore importedGameStore;
    private readonly TrainingAnalysisDataSource analysisDataSource;
    private readonly OpeningTheoryQueryService? openingTheory;
    private readonly IOpeningTrainingHistoryStore? historyStore;
    private readonly IClock clock;
    private readonly OpeningTrainingPositionSelector positionSelector = new();
    private readonly OpeningTrainingSnapshotLoader snapshotLoader;
    private readonly OpeningTrainingExampleGamePositionBuilder exampleGamePositionBuilder = new();
    private readonly OpeningTrainingOpeningWeaknessPositionBuilder openingWeaknessPositionBuilder = new();
    private readonly OpeningTrainingFirstMistakePositionBuilder firstMistakePositionBuilder = new();

    internal OpeningTrainingSessionBuilder(
        IImportedGameStore importedGameStore,
        TrainingAnalysisDataSource analysisDataSource,
        OpeningTheoryQueryService? openingTheory,
        IOpeningTrainingHistoryStore? historyStore,
        IClock clock)
    {
        this.importedGameStore = importedGameStore ?? throw new ArgumentNullException(nameof(importedGameStore));
        this.analysisDataSource = analysisDataSource ?? throw new ArgumentNullException(nameof(analysisDataSource));
        this.openingTheory = openingTheory;
        this.historyStore = historyStore;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        snapshotLoader = new OpeningTrainingSnapshotLoader(analysisDataSource);
    }

    internal bool TryBuild(
        string playerKeyOrName,
        OpeningTrainingSessionOptions? options,
        out OpeningTrainingSession? session)
    {
        if (string.IsNullOrWhiteSpace(playerKeyOrName))
        {
            session = null;
            return false;
        }

        string normalizedPlayerKey = NormalizePlayerKey(playerKeyOrName);
        List<OpeningTrainerSnapshot> snapshots = snapshotLoader.Load(playerKeyOrName, 2000)
            .Where(snapshot => snapshot.PlayerKey == normalizedPlayerKey
                || string.Equals(snapshot.DisplayName, playerKeyOrName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (snapshots.Count == 0)
        {
            session = null;
            return false;
        }

        OpeningTrainingSessionOptions effectiveOptions = NormalizeOptions(options);
        if (!new OpeningWeaknessService(analysisDataSource, openingTheory).TryBuildReport(playerKeyOrName, out OpeningWeaknessReport? weaknessReport)
            || weaknessReport is null)
        {
            session = null;
            return false;
        }

        string displayName = snapshots
            .GroupBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .First();
        List<SavedOpeningReplay> savedReplays = LoadSavedOpeningReplays(snapshots);

        Dictionary<SnapshotKey, OpeningTrainerSnapshot> snapshotIndex = snapshots
            .ToDictionary(snapshot => new SnapshotKey(snapshot.GameFingerprint, snapshot.Side));
        Dictionary<string, OpeningTrainingLine> linesById = new(StringComparer.Ordinal);
        List<OpeningTrainingPosition> positions = effectiveOptions.Sources!
            .SelectMany(source => source switch
            {
                OpeningTrainingSourceKind.ExampleGame => exampleGamePositionBuilder.Build(weaknessReport, snapshotIndex, linesById, effectiveOptions, openingTheory),
                OpeningTrainingSourceKind.OpeningWeakness => openingWeaknessPositionBuilder.Build(weaknessReport, snapshotIndex, savedReplays, linesById, effectiveOptions, openingTheory),
                OpeningTrainingSourceKind.FirstOpeningMistake => firstMistakePositionBuilder.Build(snapshots, linesById, effectiveOptions, openingTheory),
                _ => []
            })
            .ToList();

        OpeningTrainingPositionSelection selection = positionSelector.Select(positions, linesById, effectiveOptions);
        positions = selection.Positions.ToList();
        IReadOnlyList<OpeningTrainingLine> lines = selection.Lines;
        IReadOnlyList<OpeningTrainingSourceSummary> sourceSummaries = selection.SourceSummaries;

        DateTime createdUtc = clock.UtcNow;
        session = new OpeningTrainingSession(
            $"opening-trainer:{normalizedPlayerKey}:{createdUtc:yyyyMMddHHmmss}",
            normalizedPlayerKey,
            displayName,
            createdUtc,
            effectiveOptions.TrainingStyle,
            effectiveOptions.Strictness,
            effectiveOptions.SelectedSide,
            positions.Select(position => position.Mode).Distinct().ToList(),
            positions.Select(position => position.SourceKind).Distinct().ToList(),
            sourceSummaries,
            lines,
            positions);
        return positions.Count > 0;
    }

    private List<SavedOpeningReplay> LoadSavedOpeningReplays(IReadOnlyList<OpeningTrainerSnapshot> snapshots)
    {
        GameReplayService replayService = new();
        List<SavedOpeningReplay> replays = [];

        foreach (OpeningTrainerSnapshot snapshot in snapshots)
        {
            if (!importedGameStore.TryLoadImportedGame(snapshot.GameFingerprint, out ImportedGame? game) || game is null)
            {
                continue;
            }

            IReadOnlyList<ReplayPly> replay = replayService.Replay(game);
            if (replay.Count == 0)
            {
                continue;
            }

            replays.Add(new SavedOpeningReplay(snapshot, game, replay));
        }

        return replays;
    }

    // -------------------------------------------------------------------------
    // Line / option / reference helpers
    // -------------------------------------------------------------------------

    internal static OpeningTrainingLine CreateLine(
        string lineId,
        OpeningTrainingSourceKind sourceKind,
        OpeningTrainerSnapshot snapshot,
        StoredMoveAnalysis anchorMove,
        string anchorLabel,
        IReadOnlyList<StoredMoveAnalysis> lineMoves,
        OpeningIssue? issue)
    {
        return new OpeningTrainingLine(
            lineId,
            BuildOpeningLineKey(snapshot.Eco, snapshot.OpeningName, lineId),
            BuildOpeningKey(snapshot.Eco, snapshot.OpeningName),
            sourceKind,
            snapshot.Eco,
            OpeningCatalog.Describe(snapshot.Eco),
            anchorMove.FenBefore,
            OpeningPositionKeyBuilder.BuildKey(anchorMove.FenBefore),
            anchorMove.Ply,
            anchorMove.MoveNumber,
            snapshot.Side,
            anchorLabel,
            lineMoves.Select((move, index) => ToTrainingMove(move, index == 0 ? OpeningTrainingMoveRole.Expected : OpeningTrainingMoveRole.Continuation, index == 0)).ToList(),
            CreateReference(snapshot, sourceKind.ToString(), issue),
            ToRepertoireSide(snapshot.Side));
    }

    internal static List<OpeningTrainingMoveOption> BuildLineRecallOptions(
        StoredMoveAnalysis anchorMove,
        OpeningTheoryQueryService? openingTheory)
    {
        IReadOnlyList<OpeningTheoryMove> theoryMoves = OpeningTheoryBranchBuilder.GetTheoryMoves(openingTheory, anchorMove.FenBefore);
        if (theoryMoves.Count == 0)
        {
            return [];
        }

        Dictionary<string, OpeningTrainingMoveOption> options = new(StringComparer.OrdinalIgnoreCase);
        bool hasMainMove = theoryMoves.Any(move => move.IsMainMove);

        foreach ((OpeningTheoryMove move, int index) in theoryMoves.Select((move, index) => (move, index)))
        {
            bool isPreferred = move.IsMainMove || (!hasMainMove && index == 0);
            AddOrUpgradeOption(
                options,
                new OpeningTrainingMoveOption(
                    move.MoveSan,
                    move.MoveUci,
                    isPreferred ? OpeningTrainingMoveRole.Expected : OpeningTrainingMoveRole.Alternative,
                    isPreferred,
                    isPreferred
                        ? "Main move from imported opening theory"
                        : "Playable move from imported opening theory",
                    isPreferred
                        ? OpeningLineRecallReferenceKind.ReferenceLine
                        : OpeningLineRecallReferenceKind.BetterMove,
                    OpeningTrainingMoveSourceKind.OpeningBook,
                    move.Idea,
                    move.ToOpeningPositionKey));
        }

        return options.Values
            .OrderByDescending(option => option.IsPreferred)
            .ThenBy(option => option.Role)
            .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<OpeningTrainingMoveOption> BuildRepairOptions(
        StoredMoveAnalysis move,
        OpeningTheoryQueryService? openingTheory)
    {
        StoredMoveContext played = move.Move;
        IReadOnlyList<OpeningTheoryMove> theoryMoves = OpeningTheoryBranchBuilder.GetTheoryMoves(openingTheory, played.FenBefore);
        if (theoryMoves.Count == 0)
        {
            return [];
        }

        Dictionary<string, OpeningTrainingMoveOption> options = new(StringComparer.OrdinalIgnoreCase)
        {
            [BuildMoveKey(played.Uci, played.San)] = new(
                played.San,
                played.Uci,
                OpeningTrainingMoveRole.Alternative,
                false,
                "Played move to replace",
                OpeningLineRecallReferenceKind.HistoricalGame,
                OpeningTrainingMoveSourceKind.UserGame)
        };
        bool hasMainMove = theoryMoves.Any(item => item.IsMainMove);

        foreach ((OpeningTheoryMove theoryMove, int index) in theoryMoves.Select((item, index) => (item, index)))
        {
            bool isPreferred = theoryMove.IsMainMove || (!hasMainMove && index == 0);
            AddOrUpgradeOption(
                options,
                new OpeningTrainingMoveOption(
                    theoryMove.MoveSan,
                    theoryMove.MoveUci,
                    OpeningTrainingMoveRole.Repair,
                    isPreferred,
                    isPreferred
                        ? "Best repair from imported opening theory"
                        : "Playable repair from imported opening theory",
                    OpeningLineRecallReferenceKind.BetterMove,
                    OpeningTrainingMoveSourceKind.OpeningBook,
                    theoryMove.Idea,
                    theoryMove.ToOpeningPositionKey));
        }

        return options.Values
            .OrderByDescending(option => option.IsPreferred)
            .ThenBy(option => option.Role)
            .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddOrUpgradeOption(
        Dictionary<string, OpeningTrainingMoveOption> options,
        OpeningTrainingMoveOption candidate)
    {
        string key = BuildMoveKey(candidate.Uci, candidate.DisplayText);
        if (!options.TryGetValue(key, out OpeningTrainingMoveOption? existing))
        {
            options[key] = candidate;
            return;
        }

        options[key] = new OpeningTrainingMoveOption(
            existing.DisplayText.Length >= candidate.DisplayText.Length ? existing.DisplayText : candidate.DisplayText,
            existing.Uci ?? candidate.Uci,
            existing.Role <= candidate.Role ? existing.Role : candidate.Role,
            existing.IsPreferred || candidate.IsPreferred,
            existing.Note ?? candidate.Note,
            existing.ReferenceKind ?? candidate.ReferenceKind,
            existing.SourceKind == OpeningTrainingMoveSourceKind.UserGame
                ? candidate.SourceKind
                : existing.SourceKind,
            existing.Idea ?? candidate.Idea,
            existing.ToPositionKey ?? candidate.ToPositionKey);
    }

    internal static OpeningTrainingMove ToTrainingMove(StoredMoveAnalysis move, OpeningTrainingMoveRole role, bool isPreferred)
    {
        StoredMoveContext played = move.Move;

        return new OpeningTrainingMove(
            played.Ply,
            played.MoveNumber,
            move.Analysis.AnalyzedSide,
            played.San,
            played.Uci,
            role,
            isPreferred,
            move.Advice.MistakeLabel);
    }

    // -------------------------------------------------------------------------
    // Branch root helpers
    // -------------------------------------------------------------------------

    internal static List<(OpeningWeaknessEntry Entry, BranchRoot root)> BuildBranchRoots(
        OpeningWeaknessEntry entry,
        IReadOnlyDictionary<SnapshotKey, OpeningTrainerSnapshot> snapshotIndex,
        List<SavedOpeningReplay> savedReplays)
    {
        HashSet<string> exampleGameFingerprints = entry.ExampleGames
            .Select(example => example.GameFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> recurringLabels = entry.RecurringMistakeSequences
            .SelectMany(sequence => sequence.Labels)
            .Append(entry.FirstRecurringMistakeType)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<BranchOccurrence> occurrences = savedReplays
            .Where(replay => string.Equals(replay.Snapshot.Eco, entry.Eco, StringComparison.OrdinalIgnoreCase))
            .SelectMany(replay => BuildBranchOccurrences(replay, snapshotIndex, exampleGameFingerprints, recurringLabels))
            .ToList();

        return occurrences
            .GroupBy(occurrence => occurrence.RootFen, StringComparer.Ordinal)
            .Select(group => (Entry: entry, root: CreateBranchRoot(entry, group.ToList())))
            .Where(item => item.root.Branches.Count > 0)
            .ToList();
    }

    private static List<BranchOccurrence> BuildBranchOccurrences(
        SavedOpeningReplay replay,
        IReadOnlyDictionary<SnapshotKey, OpeningTrainerSnapshot> snapshotIndex,
        HashSet<string> exampleGameFingerprints,
        HashSet<string> recurringLabels)
    {
        List<BranchOccurrence> occurrences = [];
        IReadOnlyList<ReplayPly> openingPlies = replay.Replay
            .Where(ply => ply.Phase == GamePhase.Opening)
            .OrderBy(ply => ply.Ply)
            .ToList();
        Dictionary<int, ReplayPly> byPly = openingPlies.ToDictionary(ply => ply.Ply);
        snapshotIndex.TryGetValue(new SnapshotKey(replay.Snapshot.GameFingerprint, replay.Snapshot.Side), out OpeningTrainerSnapshot? analyzedSnapshot);
        List<StoredMoveAnalysis> laterMoves = analyzedSnapshot?.OpeningMoves.OrderBy(move => move.Ply).ToList() ?? [];

        foreach (ReplayPly anchorMove in openingPlies.Where(ply => ply.Side == replay.Snapshot.Side))
        {
            if (!byPly.TryGetValue(anchorMove.Ply + 1, out ReplayPly? opponentMove)
                || opponentMove.Side == replay.Snapshot.Side)
            {
                continue;
            }

            ReplayPly? playerReply = byPly.TryGetValue(opponentMove.Ply + 1, out ReplayPly? nextReply) && nextReply.Side == replay.Snapshot.Side
                ? nextReply
                : null;

            StoredMoveAnalysis? responseAnalysis = laterMoves
                .FirstOrDefault(move => move.Ply == opponentMove.Ply + 1);
            StoredMoveAnalysis? firstIssue = laterMoves
                .Where(move => move.Ply > opponentMove.Ply && IsOpeningIssue(move))
                .OrderBy(move => move.Ply)
                .FirstOrDefault();
            bool matchesRecurring = firstIssue is not null
                && recurringLabels.Contains(firstIssue.MistakeLabel ?? string.Empty);

            occurrences.Add(new BranchOccurrence(
                replay.Snapshot,
                anchorMove,
                opponentMove,
                playerReply,
                anchorMove.FenAfter,
                exampleGameFingerprints.Contains(replay.Snapshot.GameFingerprint),
                matchesRecurring,
                responseAnalysis,
                firstIssue));
        }

        return occurrences;
    }

    private static BranchRoot CreateBranchRoot(OpeningWeaknessEntry entry, IReadOnlyList<BranchOccurrence> occurrences)
    {
        BranchOccurrence sample = occurrences
            .OrderByDescending(item => item.IsExampleGame)
            .ThenByDescending(item => item.PlayerIssue?.CentipawnLoss ?? 0)
            .ThenBy(item => item.AnchorMove.Ply)
            .First();
        List<OpeningTrainingBranch> branches = occurrences
            .GroupBy(item => BuildMoveKey(item.OpponentMove.Uci, item.OpponentMove.San), StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateBranch(group.ToList()))
            .OrderByDescending(branch => branch.Frequency)
            .ThenByDescending(branch => branch.SourceStats.FirstOrDefault(item => item.SourceKind == OpeningTrainingBranchSourceKind.RecurringMistake)?.Count ?? 0)
            .ThenBy(branch => branch.OpponentMove, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        OpeningTrainingMoveOption? primaryRecommendedResponse = branches
            .Select(branch => branch.RecommendedResponse)
            .FirstOrDefault(option => option is not null);
        IReadOnlyList<OpeningTrainingMoveOption> candidateMoves = branches
            .Select(branch => new OpeningTrainingMoveOption(
                branch.OpponentMove,
                branch.OpponentMoveUci,
                OpeningTrainingMoveRole.Alternative,
                false,
                branch.SourceSummary,
                OpeningLineRecallReferenceKind.HistoricalGame,
                OpeningTrainingMoveSourceKind.UserGame,
                branch.RecommendedResponse?.Idea,
                branch.ResultingPositionKey))
            .Concat(branches
                .Where(branch => branch.RecommendedResponse is not null)
                .Select(branch => branch.RecommendedResponse!))
            .ToList();
        IReadOnlyList<OpeningTrainingMove> primaryContinuation = branches.Count > 0
            ? branches[0].Continuation
            : [];
        string branchSelectionSummary = BuildBranchSelectionSummary(branches);
        int priority = occurrences.Count * 25
            + occurrences.Count(item => item.IsExampleGame) * 10
            + occurrences.Count(item => item.MatchesRecurring) * 12
            + branches.Count * 8;

        return new BranchRoot(
            sample.Snapshot,
            sample.Snapshot.OpeningMoves.FirstOrDefault(move => move.Ply == sample.AnchorMove.Ply) ?? sample.Snapshot.OpeningMoves[0],
            sample.AnchorMove.FenAfter,
            sample.AnchorMove.Ply,
            sample.AnchorMove.San,
            sample.PlayerIssue?.MistakeLabel ?? entry.FirstRecurringMistakeType,
            sample.PlayerIssue is null ? null : new OpeningIssue(sample.Snapshot, sample.PlayerIssue),
            sample.PlayerIssue?.MistakeLabel ?? entry.FirstRecurringMistakeType,
            priority,
            sample.Snapshot.OpeningMoves.Where(move => move.Ply >= sample.AnchorMove.Ply).Take(4).ToList(),
            branches,
            candidateMoves,
            primaryRecommendedResponse,
            primaryContinuation,
            branchSelectionSummary);
    }

    private static OpeningTrainingBranch CreateBranch(IReadOnlyList<BranchOccurrence> occurrences)
    {
        BranchOccurrence sample = occurrences
            .OrderByDescending(item => item.IsExampleGame)
            .ThenByDescending(item => item.MatchesRecurring)
            .ThenBy(item => item.OpponentMove.Ply)
            .First();
        int exampleCount = occurrences.Count(item => item.IsExampleGame);
        int recurringCount = occurrences.Count(item => item.MatchesRecurring);
        OpeningTrainingMoveOption? recommendedResponse = BuildRecommendedResponse(occurrences);
        List<OpeningTrainingMove> continuation =
        [
            new OpeningTrainingMove(
                sample.OpponentMove.Ply,
                sample.OpponentMove.MoveNumber,
                sample.OpponentMove.Side,
                sample.OpponentMove.San,
                sample.OpponentMove.Uci,
                OpeningTrainingMoveRole.Continuation,
                false)
        ];
        if (recommendedResponse is not null)
        {
            continuation.Add(new OpeningTrainingMove(
                sample.OpponentMove.Ply + 1,
                sample.OpponentMove.Side == PlayerSide.White ? sample.OpponentMove.MoveNumber + 1 : sample.OpponentMove.MoveNumber,
                Opponent(sample.OpponentMove.Side),
                recommendedResponse.DisplayText,
                recommendedResponse.Uci,
                OpeningTrainingMoveRole.Expected,
                true,
                recommendedResponse.Note));
        }

        List<OpeningTrainingBranchSourceStat> sourceStats =
        [
            new(OpeningTrainingBranchSourceKind.SavedContinuation, occurrences.Count)
        ];
        if (exampleCount > 0)
        {
            sourceStats.Add(new OpeningTrainingBranchSourceStat(OpeningTrainingBranchSourceKind.ExampleGame, exampleCount));
        }

        if (recurringCount > 0)
        {
            sourceStats.Add(new OpeningTrainingBranchSourceStat(OpeningTrainingBranchSourceKind.RecurringMistake, recurringCount));
        }

        return new OpeningTrainingBranch(
            new OpeningBranchKey($"{sample.Snapshot.Eco}:{sample.AnchorMove.Ply}:{sample.OpponentMove.Uci ?? sample.OpponentMove.San}"),
            sample.OpponentMove.San,
            sample.OpponentMove.Uci,
            occurrences.Count,
            BuildBranchSourceSummary(occurrences.Count, exampleCount, recurringCount),
            recommendedResponse,
            continuation,
            sourceStats,
            OpeningPositionKeyBuilder.BuildKey(sample.OpponentMove.FenAfter));
    }

    private static OpeningTrainingMoveOption? BuildRecommendedResponse(IReadOnlyList<BranchOccurrence> occurrences)
    {
        Dictionary<string, ReplyOptionAccumulator> options = new(StringComparer.OrdinalIgnoreCase);

        foreach (BranchOccurrence occurrence in occurrences)
        {
            if (occurrence.PlayerResponseAnalysis is not null && !string.IsNullOrWhiteSpace(occurrence.PlayerResponseAnalysis.BestMoveUci))
            {
                string responseDisplay = FormatMove(occurrence.OpponentMove.FenAfter, occurrence.PlayerResponseAnalysis.BestMoveUci);
                string key = BuildMoveKey(occurrence.PlayerResponseAnalysis.BestMoveUci, responseDisplay);
                if (!options.TryGetValue(key, out ReplyOptionAccumulator? accumulator))
                {
                    accumulator = new ReplyOptionAccumulator(responseDisplay, occurrence.PlayerResponseAnalysis.BestMoveUci);
                    options[key] = accumulator;
                }

                accumulator.BestMoveCount++;
                if (occurrence.IsExampleGame)
                {
                    accumulator.ExampleCount++;
                }

                if (occurrence.MatchesRecurring)
                {
                    accumulator.RecurringCount++;
                }
            }

            if (occurrence.PlayerReply is null)
            {
                continue;
            }

            string replyKey = BuildMoveKey(occurrence.PlayerReply.Uci, occurrence.PlayerReply.San);
            if (!options.TryGetValue(replyKey, out ReplyOptionAccumulator? replyAccumulator))
            {
                replyAccumulator = new ReplyOptionAccumulator(occurrence.PlayerReply.San, occurrence.PlayerReply.Uci);
                options[replyKey] = replyAccumulator;
            }

            replyAccumulator.PlayedCount++;
            if (occurrence.IsExampleGame)
            {
                replyAccumulator.ExampleCount++;
            }

            if (occurrence.MatchesRecurring)
            {
                replyAccumulator.RecurringCount++;
            }
        }

        ReplyOptionAccumulator? best = options.Values
            .OrderByDescending(option => option.BestMoveCount)
            .ThenByDescending(option => option.PlayedCount)
            .ThenByDescending(option => option.ExampleCount)
            .ThenByDescending(option => option.RecurringCount)
            .ThenBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (best is null)
        {
            return null;
        }

        return new OpeningTrainingMoveOption(
            best.DisplayText,
            best.Uci,
            OpeningTrainingMoveRole.Expected,
            true,
            BuildRecommendedResponseNote(best),
            best.BestMoveCount > 0
                ? OpeningLineRecallReferenceKind.BestMove
                : OpeningLineRecallReferenceKind.ReferenceLine,
            best.BestMoveCount > 0
                ? OpeningTrainingMoveSourceKind.EngineBestMove
                : OpeningTrainingMoveSourceKind.UserGame,
            OpeningMoveIdeaHeuristics.Build(best.DisplayText, best.BestMoveCount > 0));
    }

    // -------------------------------------------------------------------------
    // Pure static helpers
    // -------------------------------------------------------------------------

    internal static bool IsOpeningIssue(StoredMoveAnalysis move)
    {
        StoredMoveContext played = move.Move;
        string? label = move.Advice.MistakeLabel;
        int loss = Math.Max(0, played.CentipawnLoss ?? 0);
        if (played.Quality is MoveQualityBucket.Blunder or MoveQualityBucket.Mistake)
        {
            return true;
        }

        if (label is null || !FallbackLabels.Contains(label))
        {
            return played.Quality.IsProblem() && loss >= SignificantMistakeThresholdCp;
        }

        if (label.Equals("opening_principles", StringComparison.OrdinalIgnoreCase))
        {
            return loss >= TheoryExitThresholdCp;
        }

        return loss >= SignificantMistakeThresholdCp;
    }

    internal static OpeningIssue? FindFirstIssue(OpeningTrainerSnapshot snapshot, int? preferredPly)
    {
        if (preferredPly.HasValue)
        {
            StoredMoveAnalysis? exact = snapshot.OpeningMoves.FirstOrDefault(move => move.Ply == preferredPly.Value);
            if (exact is not null && IsOpeningIssue(exact))
            {
                return new OpeningIssue(snapshot, exact);
            }
        }

        StoredMoveAnalysis? first = snapshot.OpeningMoves
            .Where(IsOpeningIssue)
            .OrderBy(move => move.Ply)
            .FirstOrDefault();

        return first is null ? null : new OpeningIssue(snapshot, first);
    }

    internal static string NormalizePlayerKey(string playerName)
        => playerName.Trim().ToLowerInvariant();

    internal static string NormalizeEco(string? eco)
        => string.IsNullOrWhiteSpace(eco) ? "Unknown" : eco.Trim().ToUpperInvariant();

    internal static string GetOpponentName(StoredMoveAnalysis move)
    {
        return move.Analysis.AnalyzedSide == PlayerSide.White
            ? move.Game.BlackPlayer ?? "Unknown opponent"
            : move.Game.WhitePlayer ?? "Unknown opponent";
    }

    internal static string GetOpponentName(ImportedGame game, PlayerSide analyzedSide)
    {
        return analyzedSide == PlayerSide.White
            ? game.BlackPlayer ?? "Unknown opponent"
            : game.WhitePlayer ?? "Unknown opponent";
    }

    private static string FormatMove(string fenBefore, string? bestMoveUci)
    {
        if (string.IsNullOrWhiteSpace(bestMoveUci))
        {
            return "Unknown";
        }

        ChessGame game = new();
        if (!game.TryLoadFen(fenBefore, out _)
            || !game.TryApplyUci(bestMoveUci, out AppliedMoveInfo? appliedMove, out _)
            || appliedMove is null)
        {
            return bestMoveUci;
        }

        return ChessMoveDisplayHelper.FormatSanAndUci(appliedMove.San, appliedMove.Uci);
    }

    internal static string BuildRepairReason(StoredMoveAnalysis move)
    {
        StoredMoveAdviceContext advice = move.Advice;
        if (!string.IsNullOrWhiteSpace(advice.TrainingHint))
        {
            return OpeningTrainingMoveEvaluator.EnsureSentence(advice.TrainingHint!);
        }

        if (!string.IsNullOrWhiteSpace(advice.ShortExplanation))
        {
            return OpeningTrainingMoveEvaluator.EnsureSentence(advice.ShortExplanation!);
        }

        return advice.MistakeLabel?.Trim().ToLowerInvariant() switch
        {
            "opening_principles" => "It follows opening principles more closely and improves development.",
            "king_safety" => "It keeps the king safer and avoids creating early weaknesses.",
            "piece_activity" => "It improves piece activity and coordinates the position better.",
            "material_loss" => "It avoids the material concession that followed in the game.",
            _ => "It fits the position better than the move played in the game."
        };
    }

    private static string BuildRecommendedResponseNote(ReplyOptionAccumulator option)
    {
        List<string> parts = [];
        if (option.BestMoveCount > 0)
        {
            parts.Add($"matched saved best-move analysis in {option.BestMoveCount} game(s)");
        }

        if (option.PlayedCount > 0)
        {
            parts.Add($"played as the local follow-up in {option.PlayedCount} game(s)");
        }

        if (option.RecurringCount > 0)
        {
            parts.Add($"appears inside {option.RecurringCount} recurring-mistake branch(es)");
        }

        return parts.Count == 0
            ? "Stable local reaction"
            : $"Recommended because it {string.Join(", and ", parts)}.";
    }

    private static string BuildBranchSourceSummary(int savedCount, int exampleCount, int recurringCount)
    {
        List<string> parts = [$"saved continuations: {savedCount}"];
        if (exampleCount > 0)
        {
            parts.Add($"example games: {exampleCount}");
        }

        if (recurringCount > 0)
        {
            parts.Add($"recurring-mistake links: {recurringCount}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildBranchSelectionSummary(List<OpeningTrainingBranch> branches)
    {
        if (branches.Count == 0)
        {
            return "No local opponent branches were found for this setup.";
        }

        return $"Showing {branches.Count} local opponent branch(es). Ordered by saved-game frequency, then recurring-mistake support.";
    }

    private static string BuildMoveKey(string? uci, string displayText)
    {
        return !string.IsNullOrWhiteSpace(uci)
            ? $"uci:{uci.Trim().ToLowerInvariant()}"
            : $"san:{SanNotation.NormalizeSan(displayText)}";
    }

    internal static string BuildLineId(OpeningTrainingSourceKind sourceKind, string gameFingerprint, int ply)
        => $"{sourceKind}:{gameFingerprint}:{ply}";

    internal static PlayerSide Opponent(PlayerSide side)
        => side == PlayerSide.White ? PlayerSide.Black : PlayerSide.White;

    internal static OpeningKey BuildOpeningKey(string eco, string openingName)
    {
        string name = string.IsNullOrWhiteSpace(openingName) ? OpeningCatalog.GetName(eco) : openingName;
        return new OpeningKey($"{NormalizeEco(eco)}:{name}");
    }

    internal static OpeningLineKey BuildOpeningLineKey(string eco, string openingName, string lineId)
    {
        string name = string.IsNullOrWhiteSpace(openingName) ? OpeningCatalog.GetName(eco) : openingName;
        return new OpeningLineKey($"{NormalizeEco(eco)}:{name}:{lineId}");
    }

    internal static RepertoireSide ToRepertoireSide(PlayerSide side)
        => side == PlayerSide.White ? RepertoireSide.White : RepertoireSide.Black;

    internal static List<string> BuildTags(string eco, string? label, params string[] tags)
    {
        return tags
            .Append(eco)
            .Append(label)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static OpeningTrainingReference CreateReference(OpeningTrainerSnapshot snapshot, string sourceLabel, OpeningIssue? issue)
    {
        return new OpeningTrainingReference(
            snapshot.GameFingerprint,
            snapshot.Side,
            snapshot.OpponentName,
            snapshot.DateText,
            snapshot.Result,
            sourceLabel,
            issue?.Move.Ply,
            issue?.Move.MistakeLabel);
    }

    private static OpeningTrainingSessionOptions NormalizeOptions(OpeningTrainingSessionOptions? options)
    {
        IReadOnlyList<OpeningTrainingMode> modes = (options?.Modes is { Count: > 0 } ? options.Modes : Enum.GetValues<OpeningTrainingMode>())
            .Distinct()
            .ToList();
        IReadOnlyList<OpeningTrainingSourceKind> sources = (options?.Sources is { Count: > 0 } ? options.Sources : Enum.GetValues<OpeningTrainingSourceKind>())
            .Distinct()
            .ToList();

        return new OpeningTrainingSessionOptions(
            modes,
            sources,
            Math.Max(1, options?.MaxPositions ?? 18),
            Math.Max(1, options?.MaxPositionsPerSource ?? 6),
            Math.Clamp(options?.MaxContinuationMoves ?? 6, 1, 12),
            options?.TargetOpenings?
                .Where(opening => !string.IsNullOrWhiteSpace(opening))
                .Select(NormalizeEco)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            options?.SelectedOpeningKeys,
            options?.SelectedLineKeys,
            options?.SelectedSide ?? RepertoireSide.Both,
            options?.TrainingStyle ?? OpeningTrainingStyle.Mixed,
            options?.Strictness ?? OpeningTrainingStrictness.BookFlexible,
            Math.Max(2, options?.MaxDepth ?? 12),
            options?.IncludeSideVariations ?? true,
            options?.PrioritizeOpponentFrequency ?? false,
            options?.IncludeTranspositions ?? true,
            options?.SpecialMode,
            options?.TimeLimitMinutes);
    }

    // -------------------------------------------------------------------------
    // Private branch helper types
    // -------------------------------------------------------------------------

    private sealed record BranchOccurrence(
        OpeningTrainerSnapshot Snapshot,
        ReplayPly AnchorMove,
        ReplayPly OpponentMove,
        ReplayPly? PlayerReply,
        string RootFen,
        bool IsExampleGame,
        bool MatchesRecurring,
        StoredMoveAnalysis? PlayerResponseAnalysis,
        StoredMoveAnalysis? PlayerIssue);

    private sealed class ReplyOptionAccumulator(string displayText, string? uci)
    {
        public string DisplayText { get; } = displayText;
        public string? Uci { get; } = uci;
        public int BestMoveCount { get; set; }
        public int PlayedCount { get; set; }
        public int ExampleCount { get; set; }
        public int RecurringCount { get; set; }
    }

}
