using System.Globalization;

namespace MoveMentorChess.App.ViewModels;

internal static class ProfileCoachPresentationText
{
    public static string BuildCoachOverviewReason(PlayerProfileReport report, string trend)
    {
        string summary = BuildSnapshotSummary(report);
        if (report.CostliestMistakeLabels.Count > 0)
        {
            ProfileCostlyLabelStat costly = report.CostliestMistakeLabels[0];
            return $"{trend}. {summary} The most expensive pattern is {FormatMistakeLabel(costly.Label).ToLowerInvariant()}, costing {costly.TotalCentipawnLoss.ToString(CultureInfo.InvariantCulture)} total CPL.";
        }

        if (report.MistakesByPhase.Count > 0)
        {
            ProfilePhaseStat phase = report.MistakesByPhase[0];
            return $"{trend}. {summary} The issue shows up most in the {FormatPhase(phase.Phase).ToLowerInvariant()}, with {phase.Count.ToString(CultureInfo.InvariantCulture)} highlighted mistakes.";
        }

        return $"{trend}. {summary}";
    }

    public static IReadOnlyList<string> BuildFixFirstItems(PlayerProfileReport report)
    {
        List<string> items = [];

        if (report.Recommendations.Count > 0)
        {
            TrainingRecommendation primary = report.Recommendations[0];
            TryAddFixFirst(items, primary.Checklist, 0);
            TryAddFixFirst(items, primary.Checklist, 1);
        }

        if (report.Recommendations.Count > 1)
        {
            TryAddFixFirst(items, report.Recommendations[1].Checklist, 0);
        }

        if (items.Count < 3 && report.MistakesByOpening.Count > 0)
        {
            string opening = FormatOpening(report.MistakesByOpening[0].Eco);
            items.Add($"Review two recent positions from {opening} where this pattern showed up.");
        }

        if (items.Count < 3 && report.MistakesByPhase.Count > 0)
        {
            string phase = FormatPhase(report.MistakesByPhase[0].Phase).ToLowerInvariant();
            items.Add($"Slow down in the {phase} and do a full safety check before moving.");
        }

        if (items.Count == 0)
        {
            items.Add("Pause at every big evaluation swing and ask what had to be checked first.");
            items.Add("Review two recent mistakes from your own games before the next training session.");
        }

        return items.Take(3).ToList();
    }

    public static string? FindFirstTrainingOpening(PlayerProfileReport report)
    {
        WeeklyTrainingDay? trainingDay = report.WeeklyPlan.Days.FirstOrDefault(day =>
            day.LaunchTrainingMode.HasValue && day.RelatedOpenings is { Count: > 0 });
        var openings = trainingDay?.RelatedOpenings;
        string? relatedOpening = (openings != null && openings.Count > 0) ? openings[0] : null;
        if (!string.IsNullOrWhiteSpace(relatedOpening))
        {
            return relatedOpening;
        }

        return report.MistakesByOpening.Count > 0 ? report.MistakesByOpening[0].Eco : null;
    }

    public static string BuildRoleLabel(TrainingPlanTopicCategory category)
    {
        return category switch
        {
            TrainingPlanTopicCategory.CoreWeakness => "Core weakness",
            TrainingPlanTopicCategory.SecondaryWeakness => "Secondary weakness",
            TrainingPlanTopicCategory.MaintenanceTopic => "Maintenance topic",
            _ => "Training topic"
        };
    }

    public static string FormatMistakeLabel(string label)
    {
        return label switch
        {
            "hanging_piece" => "Loose pieces",
            "missed_tactic" => "Missed tactics",
            "opening_principles" => "Opening discipline",
            "king_safety" => "King safety",
            "endgame_technique" => "Endgame technique",
            "material_loss" => "Material losses",
            "piece_activity" => "Passive pieces",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((label ?? string.Empty).Replace('_', ' ').ToLowerInvariant())
        };
    }

    public static string FormatTrendHeadline(ProfileProgressDirection direction)
    {
        return direction switch
        {
            ProfileProgressDirection.Improving => "Improving lately",
            ProfileProgressDirection.Stable => "Mostly stable",
            ProfileProgressDirection.Regressing => "Results slipped recently",
            _ => "Need more games"
        };
    }

    public static string FormatTimes(int count) => count == 1 ? "1 time" : $"{count.ToString(CultureInfo.InvariantCulture)} times";

    public static string FormatChartDate(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Unknown";
    }

    public static string FormatOpening(string eco)
    {
        string description = OpeningCatalog.Describe(eco);
        return string.IsNullOrWhiteSpace(description) ? "Mixed openings" : description;
    }

    public static string FormatPhase(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Opening => "Opening",
            GamePhase.Middlegame => "Middlegame",
            GamePhase.Endgame => "Endgame",
            _ => phase.ToString()
        };
    }

    public static string FormatOpeningFrequency(int count, int total)
    {
        if (total <= 0)
        {
            return $"{count.ToString(CultureInfo.InvariantCulture)} games";
        }

        double percentage = (double)count / total * 100.0;
        return $"{count.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)} games ({percentage.ToString("0.#", CultureInfo.InvariantCulture)}%)";
    }

    public static string FormatPlyLabel(PlayerSide side, int? ply, string? san)
    {
        if (!ply.HasValue)
        {
            return string.IsNullOrWhiteSpace(san) ? "Unknown move" : san!;
        }

        int moveNumber = (ply.Value + 1) / 2;
        string prefix = ply.Value % 2 == 1 ? $"{moveNumber.ToString(CultureInfo.InvariantCulture)}." : $"{moveNumber.ToString(CultureInfo.InvariantCulture)}...";
        return string.IsNullOrWhiteSpace(san) ? prefix : $"{prefix} {san}";
    }

    public static string TrimSentence(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().TrimEnd('.', ';', ':', '!');
    }

    public static string FormatTrainingBlockKind(TrainingBlockKind kind)
    {
        return kind switch
        {
            TrainingBlockKind.Tactics => "Tactics",
            TrainingBlockKind.OpeningReview => "Opening review",
            TrainingBlockKind.EndgameDrill => "Endgame drill",
            TrainingBlockKind.GameReview => "Game review",
            TrainingBlockKind.SlowPlayFocus => "Slow play focus",
            _ => kind.ToString()
        };
    }

    public static string FormatTrainingBlockPurpose(TrainingBlockPurpose purpose)
    {
        return purpose switch
        {
            TrainingBlockPurpose.Repair => "Repair",
            TrainingBlockPurpose.Maintain => "Maintain",
            TrainingBlockPurpose.Checklist => "Checklist",
            _ => purpose.ToString()
        };
    }

    public static string FormatOpeningWeaknessCategory(OpeningWeaknessCategory category)
    {
        return category switch
        {
            OpeningWeaknessCategory.FixNow => "Opening to fix now",
            OpeningWeaknessCategory.ReviewLater => "Opening to review later",
            OpeningWeaknessCategory.Stable => "Opening stable",
            _ => category.ToString()
        };
    }

    public static int GetBlockPurposeOrder(TrainingBlockPurpose purpose)
    {
        return purpose switch
        {
            TrainingBlockPurpose.Repair => 0,
            TrainingBlockPurpose.Maintain => 1,
            TrainingBlockPurpose.Checklist => 2,
            _ => 3
        };
    }

    public static string BuildTopicContext(TrainingPlanTopic topic)
    {
        List<string> parts = [];

        if (topic.EmphasisPhase.HasValue)
        {
            parts.Add(FormatPhase(topic.EmphasisPhase.Value));
        }

        if (topic.EmphasisSide.HasValue)
        {
            parts.Add(topic.EmphasisSide.Value == PlayerSide.White ? "Mostly as White" : "Mostly as Black");
        }

        if (topic.RelatedOpenings.Count > 0)
        {
            parts.Add(string.Join(" / ", topic.RelatedOpenings.Take(2).Select(FormatOpening)));
        }

        return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
    }

    public static IReadOnlyList<ProfileMistakeExample> BuildDeepDiveExamples(PlayerProfileReport report)
    {
        if (report.MistakeExamples.Count == 0)
        {
            return [];
        }

        List<ProfileMistakeExample> selected = [];
        IEnumerable<string> dominantLabels = report.TopMistakeLabels
            .Select(item => item.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(3);

        foreach (string label in dominantLabels)
        {
            List<ProfileMistakeExample> examplesForLabel = report.MistakeExamples
                .Where(example => string.Equals(example.Label, label, StringComparison.OrdinalIgnoreCase))
                .OrderBy(example => GetExampleRankOrder(example.Rank))
                .ThenByDescending(example => example.CentipawnLoss ?? 0)
                .Take(3)
                .ToList();

            selected.AddRange(examplesForLabel);
        }

        if (selected.Count == 0)
        {
            selected.AddRange(report.MistakeExamples
                .OrderBy(example => GetExampleRankOrder(example.Rank))
                .ThenByDescending(example => example.CentipawnLoss ?? 0)
                .Take(6));
        }

        return selected
            .GroupBy(example => $"{example.GameFingerprint}|{example.Ply}|{example.Rank}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    public static string FormatExampleRank(ProfileMistakeExampleRank rank)
    {
        return rank switch
        {
            ProfileMistakeExampleRank.MostFrequent => "Most frequent",
            ProfileMistakeExampleRank.MostCostly => "Most costly",
            ProfileMistakeExampleRank.MostRepresentative => "Most representative",
            _ => rank.ToString()
        };
    }

    public static string BuildSnapshotSummary(PlayerProfileReport report)
    {
        string cpl = report.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
        return $"Across {report.GamesAnalyzed.ToString(CultureInfo.InvariantCulture)} games, the player has an average loss score of {cpl} with {report.HighlightedMistakes.ToString(CultureInfo.InvariantCulture)} mistakes to practice.";
    }

    private static void TryAddFixFirst(List<string> items, IReadOnlyList<string> checklist, int index)
    {
        if (checklist.Count <= index)
        {
            return;
        }

        string action = TrimSentence(checklist[index]);
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        if (items.Any(existing => string.Equals(existing, action, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        items.Add(action + ".");
    }

    private static int GetExampleRankOrder(ProfileMistakeExampleRank rank)
    {
        return rank switch
        {
            ProfileMistakeExampleRank.MostFrequent => 0,
            ProfileMistakeExampleRank.MostCostly => 1,
            ProfileMistakeExampleRank.MostRepresentative => 2,
            _ => 3
        };
    }
}
