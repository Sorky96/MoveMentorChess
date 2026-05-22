namespace MoveMentorChess.Profiles;

internal static class ProfileMistakeExampleSelector
{
    public static List<ProfileMistakeExample> BuildForLabel(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        string label,
        RecommendationContext context,
        int maxCount)
    {
        List<MistakeExampleCandidate> candidates = BuildCandidates(snapshots, label);
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
            .ThenByDescending(item => SeverityWeight(item.Move.Quality))
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

    private static List<MistakeExampleCandidate> BuildCandidates(
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
            .GroupBy(candidate => BuildIdentity(candidate), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Move.IsHighlighted)
                .ThenByDescending(item => item.Move.CentipawnLoss ?? 0)
                .ThenByDescending(item => SeverityWeight(item.Move.Quality))
                .ThenBy(item => item.Move.Ply)
                .First())
            .ToList();
    }

    private static MistakeExampleCandidate? SelectMostFrequentExample(
        IReadOnlyList<MistakeExampleCandidate> candidates,
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
                .FirstOrDefault(item => !excludedKeys.Contains(BuildIdentity(item))))
            .FirstOrDefault();
    }

    private static MistakeExampleCandidate? SelectMostCostlyExample(
        IReadOnlyList<MistakeExampleCandidate> candidates,
        HashSet<string> excludedKeys)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Move.CentipawnLoss ?? 0)
            .ThenByDescending(candidate => candidate.Move.IsHighlighted)
            .ThenByDescending(candidate => SeverityWeight(candidate.Move.Quality))
            .ThenBy(candidate => candidate.Move.Ply)
            .Where(candidate => !excludedKeys.Contains(BuildIdentity(candidate)))
            .FirstOrDefault();
    }

    private static MistakeExampleCandidate? SelectMostRepresentativeExample(
        IReadOnlyList<MistakeExampleCandidate> candidates,
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
            .Where(candidate => !excludedKeys.Contains(BuildIdentity(candidate)))
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

        string identity = BuildIdentity(candidate);
        if (!selectedKeys.Add(identity))
        {
            return;
        }

        selected.Add(ToProfileMistakeExample(candidate, rank));
    }

    private static string BuildIdentity(MistakeExampleCandidate candidate)
        => $"{candidate.Snapshot.GameFingerprint}|{candidate.Move.Ply}|{candidate.Move.FenBefore}";

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

    private static int SeverityWeight(MoveQualityBucket quality)
    {
        return quality switch
        {
            MoveQualityBucket.Blunder => 5,
            MoveQualityBucket.Mistake => 4,
            MoveQualityBucket.Inaccuracy => 3,
            MoveQualityBucket.Brilliant => 2,
            MoveQualityBucket.Great => 1,
            _ => 0
        };
    }

    private sealed record MistakeExampleCandidate(
        PlayerProfileSnapshot Snapshot,
        StoredMoveAnalysis Move);

    private readonly record struct ExampleClusterKey(
        GamePhase Phase,
        string Eco);

    private sealed class ExampleClusterKeyComparer : IEqualityComparer<ExampleClusterKey>
    {
        public static ExampleClusterKeyComparer Instance { get; } = new();

        public bool Equals(ExampleClusterKey x, ExampleClusterKey y)
        {
            return x.Phase == y.Phase
                && string.Equals(x.Eco, y.Eco, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ExampleClusterKey obj)
        {
            return HashCode.Combine(obj.Phase, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Eco ?? string.Empty));
        }
    }
}
