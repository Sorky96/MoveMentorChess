namespace MoveMentorChess.Profiles;

internal sealed class ProfileMistakeExampleSelector
{
    public IReadOnlyList<ProfileMistakeExample> BuildAllMistakeExamples(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        IReadOnlyList<ProfileLabelStat> topLabels,
        int maxTotal)
    {
        if (topLabels.Count == 0)
        {
            return [];
        }

        int perLabel = Math.Max(1, maxTotal / topLabels.Count);
        return topLabels
            .SelectMany(label =>
            {
                RecommendationContext context = BuildRecommendationContext(snapshots, label.Label);
                return BuildMistakeExamples(snapshots, label.Label, context, perLabel);
            })
            .OrderByDescending(example => example.CentipawnLoss ?? 0)
            .Take(maxTotal)
            .ToList();
    }

    private static List<ProfileMistakeExample> BuildMistakeExamples(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        string label,
        RecommendationContext context,
        int maxCount)
    {
        List<MistakeExampleCandidate> candidates = BuildMistakeExampleCandidates(snapshots, label);
        if (candidates.Count == 0)
        {
            return [];
        }

        List<ProfileMistakeExample> selected = [];
        HashSet<string> selectedKeys = [];

        AddRankedExample(
            selected,
            selectedKeys,
            SelectMostFrequentExample(candidates, selectedKeys),
            ProfileMistakeExampleRank.MostFrequent);
        AddRankedExample(
            selected,
            selectedKeys,
            SelectMostCostlyExample(candidates, selectedKeys),
            ProfileMistakeExampleRank.MostCostly);
        AddRankedExample(
            selected,
            selectedKeys,
            SelectMostRepresentativeExample(candidates, context, selectedKeys),
            ProfileMistakeExampleRank.MostRepresentative);

        foreach (MistakeExampleCandidate candidate in candidates
            .OrderByDescending(item => item.Move.IsHighlighted)
            .ThenByDescending(item => item.Move.CentipawnLoss ?? 0)
            .ThenByDescending(item => PlayerProfileStatsAggregator.SeverityWeight(item.Move.Quality))
            .ThenBy(item => item.Move.Ply))
        {
            if (selected.Count >= maxCount)
            {
                break;
            }

            AddRankedExample(
                selected,
                selectedKeys,
                candidate,
                ProfileMistakeExampleRank.MostRepresentative);
        }

        return selected
            .Take(maxCount)
            .ToList();
    }

    private static List<MistakeExampleCandidate> BuildMistakeExampleCandidates(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        string label)
    {
        return snapshots
            .SelectMany(snapshot => snapshot.Moves
                .Where(move =>
                    !string.IsNullOrWhiteSpace(move.MistakeLabel)
                    && string.Equals(move.MistakeLabel, label, StringComparison.Ordinal)
                    && move.Quality is MoveQualityBucket.Mistake or MoveQualityBucket.Blunder
                    && !string.IsNullOrWhiteSpace(move.FenBefore))
                .Select(move => new MistakeExampleCandidate(snapshot, move)))
            .GroupBy(candidate => BuildExampleIdentity(candidate), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Move.IsHighlighted)
                .ThenByDescending(item => item.Move.CentipawnLoss ?? 0)
                .ThenByDescending(item => PlayerProfileStatsAggregator.SeverityWeight(item.Move.Quality))
                .ThenBy(item => item.Move.Ply)
                .First())
            .ToList();
    }

    private static MistakeExampleCandidate? SelectMostFrequentExample(
        List<MistakeExampleCandidate> candidates,
        HashSet<string> excludedKeys)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .GroupBy(
                candidate => new ExampleClusterKey(candidate.Move.Phase, candidate.Snapshot.Eco),
                ExampleClusterKeyComparer.Instance)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(item => item.Move.CentipawnLoss ?? 0))
            .ThenBy(group => group.Key.Phase)
            .ThenBy(group => group.Key.Eco, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Move.IsHighlighted)
                .ThenByDescending(item => item.Move.CentipawnLoss ?? 0)
                .ThenBy(item => item.Move.Ply)
                .FirstOrDefault(item => !excludedKeys.Contains(BuildExampleIdentity(item))))
            .FirstOrDefault();
    }

    private static MistakeExampleCandidate? SelectMostCostlyExample(
        List<MistakeExampleCandidate> candidates,
        HashSet<string> excludedKeys)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Move.CentipawnLoss ?? 0)
            .ThenByDescending(candidate => candidate.Move.IsHighlighted)
            .ThenByDescending(candidate => PlayerProfileStatsAggregator.SeverityWeight(candidate.Move.Quality))
            .ThenBy(candidate => candidate.Move.Ply)
            .Where(candidate => !excludedKeys.Contains(BuildExampleIdentity(candidate)))
            .FirstOrDefault();
    }

    private static MistakeExampleCandidate? SelectMostRepresentativeExample(
        List<MistakeExampleCandidate> candidates,
        RecommendationContext context,
        HashSet<string> excludedKeys)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        double averageCpl = candidates
            .Select(candidate => Math.Max(0, candidate.Move.CentipawnLoss ?? 0))
            .DefaultIfEmpty(0)
            .Average();

        return candidates
            .OrderByDescending(candidate => candidate.Move.IsHighlighted)
            .ThenBy(candidate => BuildRepresentativePenalty(candidate, context, averageCpl))
            .ThenByDescending(candidate => candidate.Move.MistakeConfidence ?? 0.0)
            .ThenByDescending(candidate => candidate.Move.CentipawnLoss ?? 0)
            .ThenBy(candidate => candidate.Move.Ply)
            .Where(candidate => !excludedKeys.Contains(BuildExampleIdentity(candidate)))
            .FirstOrDefault();
    }

    private static int BuildRepresentativePenalty(
        MistakeExampleCandidate candidate,
        RecommendationContext context,
        double averageCpl)
    {
        int penalty = Math.Abs((candidate.Move.CentipawnLoss ?? 0) - (int)Math.Round(averageCpl));

        if (context.DominantPhase.HasValue && candidate.Move.Phase != context.DominantPhase.Value)
        {
            penalty += 75;
        }

        if (context.TopOpenings.Count > 0
            && !context.TopOpenings.Any(opening => string.Equals(opening, candidate.Snapshot.Eco, StringComparison.OrdinalIgnoreCase)))
        {
            penalty += 60;
        }

        if (context.DominantSide.HasValue && candidate.Snapshot.Side != context.DominantSide.Value)
        {
            penalty += 25;
        }

        if (!candidate.Move.IsHighlighted)
        {
            penalty += 15;
        }

        return penalty;
    }

    private static void AddRankedExample(
        List<ProfileMistakeExample> selected,
        HashSet<string> selectedKeys,
        MistakeExampleCandidate? candidate,
        ProfileMistakeExampleRank rank)
    {
        if (candidate is null)
        {
            return;
        }

        string identity = BuildExampleIdentity(candidate);
        if (!selectedKeys.Add(identity))
        {
            return;
        }

        selected.Add(ToProfileMistakeExample(candidate, rank));
    }

    private static string BuildExampleIdentity(MistakeExampleCandidate candidate)
    {
        return $"{candidate.Snapshot.GameFingerprint}|{candidate.Move.Ply}|{candidate.Move.FenBefore}";
    }

    private static ProfileMistakeExample ToProfileMistakeExample(
        MistakeExampleCandidate candidate,
        ProfileMistakeExampleRank rank)
    {
        return new ProfileMistakeExample(
            candidate.Snapshot.GameFingerprint,
            candidate.Move.Ply,
            candidate.Move.MoveNumber,
            candidate.Move.AnalyzedSide,
            candidate.Move.San,
            FormatBetterMove(candidate.Move.FenBefore, candidate.Move.BestMoveUci),
            candidate.Move.MistakeLabel ?? "unclassified",
            candidate.Move.CentipawnLoss,
            candidate.Move.Quality,
            candidate.Move.Phase,
            candidate.Snapshot.Eco,
            candidate.Move.FenBefore,
            rank);
    }

    private static string FormatBetterMove(string fenBefore, string? bestMoveUci)
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

    private static RecommendationContext BuildRecommendationContext(IReadOnlyList<PlayerProfileSnapshot> snapshots, string label)
    {
        List<RecommendationOccurrence> occurrences = snapshots
            .SelectMany(snapshot => BuildRecommendationOccurrences(snapshot, label))
            .ToList();

        if (occurrences.Count == 0)
        {
            return new RecommendationContext(null, null, []);
        }

        GamePhase? dominantPhase = occurrences
            .Where(item => item.Phase.HasValue)
            .GroupBy(item => item.Phase!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (GamePhase?)group.Key)
            .FirstOrDefault();

        PlayerSide? dominantSide = occurrences
            .GroupBy(item => item.Side)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (PlayerSide?)group.Key)
            .FirstOrDefault();

        IReadOnlyList<string> topOpenings = occurrences
            .GroupBy(item => item.Eco, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(group => group.Key)
            .ToList();

        return new RecommendationContext(dominantPhase, dominantSide, topOpenings);
    }

    private static List<RecommendationOccurrence> BuildRecommendationOccurrences(PlayerProfileSnapshot snapshot, string label)
    {
        List<RecommendationOccurrence> highlightedOccurrences = PlayerProfileStatsAggregator.GetHighlightedGroups(snapshot)
            .Where(group => string.Equals(group.Label, label, StringComparison.Ordinal))
            .Select(group => new RecommendationOccurrence(snapshot.Side, group.DominantPhase, snapshot.Eco))
            .ToList();

        if (highlightedOccurrences.Any(item => item.Phase.HasValue))
        {
            return highlightedOccurrences;
        }

        List<RecommendationOccurrence> moveOccurrences = snapshot.Moves
            .Where(move => string.Equals(move.MistakeLabel ?? "unclassified", label, StringComparison.Ordinal))
            .Select(move => new RecommendationOccurrence(snapshot.Side, move.Phase, snapshot.Eco))
            .ToList();

        return moveOccurrences.Count > 0
            ? moveOccurrences
            : highlightedOccurrences;
    }
}
