namespace MoveMentorChess.Training;

public sealed class OpeningTrainingPositionSelector
{
    public OpeningTrainingPositionSelection Select(
        IReadOnlyList<OpeningTrainingPosition> positions,
        IReadOnlyDictionary<string, OpeningTrainingLine> linesById,
        OpeningTrainingSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(linesById);
        ArgumentNullException.ThrowIfNull(options);

        HashSet<OpeningTrainingMode> modes = (options.Modes ?? Enum.GetValues<OpeningTrainingMode>())
            .ToHashSet();
        IEnumerable<OpeningTrainingPosition> filteredPositions = positions;
        if (options.TargetOpenings is { Count: > 0 } targetOpenings)
        {
            HashSet<string> targetEco = targetOpenings
                .Select(NormalizeEco)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filteredPositions = filteredPositions
                .Where(position => targetEco.Contains(NormalizeEco(position.Eco)));
        }

        List<OpeningTrainingPosition> selectedPositions = filteredPositions
            .Where(position => modes.Contains(position.Mode))
            .OrderByDescending(position => position.Priority)
            .ThenBy(position => position.Ply)
            .ThenBy(position => position.OpeningName, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxPositions)
            .ToList();

        HashSet<string> usedLineIds = selectedPositions
            .Select(position => position.LineId)
            .Where(lineId => !string.IsNullOrWhiteSpace(lineId))
            .Select(lineId => lineId!)
            .ToHashSet(StringComparer.Ordinal);
        List<OpeningTrainingLine> selectedLines = linesById.Values
            .Where(line => usedLineIds.Contains(line.LineId))
            .OrderBy(line => line.SourceKind)
            .ThenBy(line => line.OpeningName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.AnchorPly)
            .ToList();

        return new OpeningTrainingPositionSelection(
            selectedPositions,
            selectedLines,
            BuildSourceSummaries(selectedPositions, selectedLines));
    }

    private static List<OpeningTrainingSourceSummary> BuildSourceSummaries(
        IReadOnlyList<OpeningTrainingPosition> positions,
        IReadOnlyList<OpeningTrainingLine> lines)
    {
        Dictionary<OpeningTrainingSourceKind, int> lineCounts = lines
            .GroupBy(line => line.SourceKind)
            .ToDictionary(group => group.Key, group => group.Count());

        return positions
            .GroupBy(position => position.SourceKind)
            .Select(group => new OpeningTrainingSourceSummary(
                group.Key,
                group.Count(),
                lineCounts.TryGetValue(group.Key, out int count) ? count : 0,
                group.Select(position => position.Eco)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(summary => summary.SourceKind)
            .ToList();
    }

    private static string NormalizeEco(string? eco)
    {
        return string.IsNullOrWhiteSpace(eco) ? "Unknown" : eco.Trim().ToUpperInvariant();
    }
}

public sealed record OpeningTrainingPositionSelection(
    IReadOnlyList<OpeningTrainingPosition> Positions,
    IReadOnlyList<OpeningTrainingLine> Lines,
    IReadOnlyList<OpeningTrainingSourceSummary> SourceSummaries);
