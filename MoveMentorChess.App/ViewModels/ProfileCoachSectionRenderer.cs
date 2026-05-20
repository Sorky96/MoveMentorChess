using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MoveMentorChess.App.Controls;
using static MoveMentorChess.App.ViewModels.ProfileCoachPresentationText;

namespace MoveMentorChess.App.ViewModels;

internal static class ProfileCoachSectionRenderer
{
    public const string TrainerPreparingSuggestionsText = "Your personal trainer is preparing suggestions...";

    public static Control CreateHeroCard(PlayerProfileReport report)
    {
        Border card = CreateCardBorder();
        StackPanel panel = CreateCardPanel();

        panel.Children.Add(new TextBlock
        {
            Text = report.DisplayName,
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            Text = $"Based on {report.GamesAnalyzed} games and {report.TotalAnalyzedMoves} analyzed moves.",
            FontSize = 15,
            Foreground = Brush.Parse("#D7E2EA"),
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = panel;
        return card;
    }

    public static Control CreateSnapshotCard(PlayerProfileReport report)
    {
        string mainIssue = report.TopMistakeLabels.Count > 0
            ? FormatMistakeLabel(report.TopMistakeLabels[0].Label)
            : "No dominant issue yet";
        string weakestPhase = report.MistakesByPhase.Count > 0
            ? FormatPhase(report.MistakesByPhase[0].Phase)
            : "mixed phases";
        string opening = report.MistakesByOpening.Count > 0
            ? FormatOpening(report.MistakesByOpening[0].Eco)
            : "mixed openings";

        return CreateInsightCard(
            "Profile snapshot",
            $"{FormatTrendHeadline(report.ProgressSignal.Direction)} • {mainIssue}",
            $"This player most often struggles in the {weakestPhase.ToLowerInvariant()} and the pattern clusters around {opening}. {BuildSnapshotSummary(report)}");
    }

    public static Control CreateMetricsCard(PlayerProfileReport report)
    {
        Border card = CreateCardBorder();
        WrapPanel wrap = new()
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 220
        };

        wrap.Children.Add(CreateMetricTile("Games analyzed", report.GamesAnalyzed.ToString(CultureInfo.InvariantCulture)));
        wrap.Children.Add(CreateMetricTile("Moves analyzed", report.TotalAnalyzedMoves.ToString(CultureInfo.InvariantCulture)));
        wrap.Children.Add(CreateMetricTile("Highlighted mistakes", report.HighlightedMistakes.ToString(CultureInfo.InvariantCulture)));
        wrap.Children.Add(CreateMetricTile("Average CPL", report.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"));
        if (report.RatingTrend.CurrentStrength is not null)
        {
            MoveMentorStrengthPoint strength = report.RatingTrend.CurrentStrength;
            wrap.Children.Add(CreateMetricTile(
                "MoveMentor estimated strength",
                $"{strength.EstimatedStrength} ({strength.Low}-{strength.High})",
                300));
        }

        if (report.GamesBySide.Count > 0)
        {
            string sides = string.Join(" | ", report.GamesBySide.Select(side =>
                $"{(side.Side == PlayerSide.White ? "White" : "Black")}: {side.GamesAnalyzed} games, {side.HighlightedMistakes} mistakes"));
            wrap.Children.Add(CreateMetricTile("By side", sides, 440));
        }

        card.Child = wrap;
        return card;
    }

    public static IEnumerable<Control> BuildPriorityRows(PlayerProfileReport report)
    {
        yield return CreateBodyText("Fix first", "#9EB5C5");
        foreach (string item in BuildFixFirstItems(report))
        {
            yield return CreateBulletText(item);
        }

        if (report.TrainingPlan.Topics.Count == 0)
        {
            yield return CreateBodyText("No focused work items yet.", "#D7E2EA");
            yield break;
        }

        yield return CreateBodyText("Training priorities", "#9EB5C5");
        foreach (TrainingPlanTopic topic in report.TrainingPlan.Topics.OrderBy(topic => topic.Priority).Take(3))
        {
            yield return CreateInsightCard(
                $"{BuildRoleLabel(topic.Category)}: {topic.Title}",
                topic.Summary,
                topic.WhyThisTopicNow);
        }
    }

    public static IEnumerable<Control> BuildEvidenceSnapshotRows(PlayerProfileReport report, OpeningWeaknessReport? openingReport)
    {
        yield return CreateBodyText(report.ProgressSignal.Summary, "#D7E2EA");

        if (report.MistakesByPhase.Count > 0)
        {
            ProfilePhaseStat phase = report.MistakesByPhase[0];
            yield return CreateBulletText($"Weakest phase: {FormatPhase(phase.Phase)} ({phase.Count} highlighted mistakes)");
        }

        if (openingReport is not null && openingReport.WeakOpenings.Count > 0)
        {
            OpeningWeaknessEntry opening = openingReport.WeakOpenings[0];
            yield return CreateBulletText($"Main opening signal: {opening.OpeningDisplayName} ({opening.Eco})");
        }

        if (report.CostliestMistakeLabels.Count > 0)
        {
            ProfileCostlyLabelStat costly = report.CostliestMistakeLabels[0];
            yield return CreateBulletText($"Costliest pattern: {FormatMistakeLabel(costly.Label)} | total CPL {costly.TotalCentipawnLoss}");
        }
    }

    public static IEnumerable<Control> BuildMistakePatternRows(PlayerProfileReport report)
    {
        if (report.TopMistakeLabels.Count == 0 && report.CostliestMistakeLabels.Count == 0)
        {
            yield return CreateBodyText("No recurring or costly patterns yet.");
            yield break;
        }

        yield return CreateBodyText("Recurring patterns", "#9EB5C5");
        foreach (ProfileLabelStat item in report.TopMistakeLabels.Take(6))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: {FormatTimes(item.Count)}");
        }

        yield return CreateBodyText("Costliest patterns", "#9EB5C5");
        foreach (ProfileCostlyLabelStat item in report.CostliestMistakeLabels.Take(6))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: total CPL {item.TotalCentipawnLoss}, avg {item.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        }
    }

    public static IEnumerable<Control> BuildRatingAndFormRows(PlayerProfileReport report)
    {
        yield return CreateBodyText(report.RatingTrend.Summary, "#D7E2EA");
        yield return CreateBodyText("Current estimate. It will get more reliable as more games are analyzed for this player.", "#9EB5C5");

        if (report.RatingTrend.RatingPoints.Count == 0 && report.RatingTrend.StrengthPoints.Count == 0)
        {
            yield return CreateBodyText("No rating or strength trend data yet.");
            yield break;
        }

        yield return CreateChartCard(
            "Rating trend",
            [
                new ProfileTrendChartSeries(
                    "Chess.com rating",
                    Brush.Parse("#7DD3FC"),
                    report.RatingTrend.RatingPoints.Select(point => new ProfileTrendChartPoint(FormatChartDate(point.GameDate), point.PlayerRating)).ToList()),
                new ProfileTrendChartSeries(
                    "MoveMentor estimated strength",
                    Brush.Parse("#FACC15"),
                    report.RatingTrend.StrengthPoints.Select(point => new ProfileTrendChartPoint(FormatChartDate(point.GameDate), point.EstimatedStrength)).ToList())
            ]);

        yield return CreateChartCard(
            "Average CPL",
            [
                new ProfileTrendChartSeries(
                    "Average CPL",
                    Brush.Parse("#FB7185"),
                    report.RatingTrend.AverageCentipawnLossTrend.Select(point => new ProfileTrendChartPoint(point.MonthKey, point.AverageCentipawnLoss)).ToList(),
                    ProfileTrendChartKind.Bars)
            ]);

        yield return CreateChartCard(
            "Move quality per game",
            [
                new ProfileTrendChartSeries(
                    "Blunders",
                    Brush.Parse("#F87171"),
                    report.RatingTrend.MoveQualityTrend.Select(point => new ProfileTrendChartPoint(point.PeriodKey, point.BlundersPerGame)).ToList(),
                    ProfileTrendChartKind.Bars),
                new ProfileTrendChartSeries(
                    "Mistakes",
                    Brush.Parse("#FDBA74"),
                    report.RatingTrend.MoveQualityTrend.Select(point => new ProfileTrendChartPoint(point.PeriodKey, point.MistakesPerGame)).ToList(),
                    ProfileTrendChartKind.Bars),
                new ProfileTrendChartSeries(
                    "Brilliant/great/best",
                    Brush.Parse("#86EFAC"),
                    report.RatingTrend.MoveQualityTrend.Select(point => new ProfileTrendChartPoint(point.PeriodKey, point.BrilliantGreatBestPerGame)).ToList(),
                    ProfileTrendChartKind.Bars)
            ]);

        if (report.RatingTrendsByTimeControl.Count > 0)
        {
            yield return CreateBodyText("By time control", "#9EB5C5");
            foreach (PlayerRatingTrendReport trend in report.RatingTrendsByTimeControl)
            {
                yield return CreateBulletText(trend.Summary);
            }
        }
    }

    public static IEnumerable<Control> BuildTopLabelRows(PlayerProfileReport report)
    {
        if (report.TopMistakeLabels.Count == 0)
        {
            yield return CreateBodyText("No recurring labels yet.");
            yield break;
        }

        foreach (ProfileLabelStat item in report.TopMistakeLabels.Take(8))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: {FormatTimes(item.Count)}");
        }
    }

    public static IEnumerable<Control> BuildFixFirstRows(PlayerProfileReport report)
    {
        foreach (string item in BuildFixFirstItems(report))
        {
            yield return CreateBulletText(item);
        }
    }

    public static IEnumerable<Control> BuildCostliestRows(PlayerProfileReport report)
    {
        if (report.CostliestMistakeLabels.Count == 0)
        {
            yield return CreateBodyText("No costly patterns yet.");
            yield break;
        }

        foreach (ProfileCostlyLabelStat item in report.CostliestMistakeLabels.Take(8))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: total CPL {item.TotalCentipawnLoss}, avg {item.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        }
    }

    public static IEnumerable<Control> BuildRecentTrendRows(PlayerProfileReport report)
    {
        yield return CreateBodyText(FormatTrendHeadline(report.ProgressSignal.Direction));
        yield return CreateBodyText(report.ProgressSignal.Summary, "#D7E2EA");

        if (report.ProgressSignal.Recent is not null)
        {
            yield return CreateBulletText($"Recent period: {FormatPeriod(report.ProgressSignal.Recent)}");
        }

        if (report.ProgressSignal.Previous is not null)
        {
            yield return CreateBulletText($"Earlier period: {FormatPeriod(report.ProgressSignal.Previous)}");
        }

        foreach (ProfileMonthlyTrend month in report.MonthlyTrend.Take(6))
        {
            yield return CreateBulletText($"{month.MonthKey}: {month.GamesAnalyzed} games, mistakes {month.HighlightedMistakes}, CPL {month.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        }
    }

    public static IEnumerable<Control> BuildWorkOnRows(
        PlayerProfileReport report,
        Func<ProfileMistakeExample, bool, Control> createExampleCard)
    {
        if (report.TrainingPlan.Topics.Count == 0)
        {
            yield return CreateBodyText("No focused work items yet.");
            yield break;
        }

        foreach (TrainingPlanTopic topic in report.TrainingPlan.Topics.Take(3))
        {
            Border innerCard = new()
            {
                Background = Brush.Parse("#182B37"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            StackPanel panel = new() { Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = $"{BuildRoleLabel(topic.Category)}: {topic.Title}",
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(CreateBodyText(topic.Summary, "#D7E2EA"));
            panel.Children.Add(CreateBodyText(topic.WhyThisTopicNow, "#D7E2EA"));

            if (topic.Blocks.Count > 0)
            {
                string blocks = string.Join(", ", topic.Blocks.Select(block =>
                    $"{FormatTrainingBlockPurpose(block.Purpose).ToLowerInvariant()} {FormatTrainingBlockKind(block.Kind).ToLowerInvariant()}"));
                panel.Children.Add(CreateBodyText($"Training blocks: {blocks}.", "#9EB5C5"));
            }

            foreach (ProfileMistakeExample example in topic.Examples.Take(2))
            {
                panel.Children.Add(createExampleCard(example, true));
            }

            innerCard.Child = panel;
            yield return innerCard;
        }
    }

    public static IEnumerable<Control> BuildDeepDiveRows(
        PlayerProfileReport report,
        Func<ProfileMistakeExample, bool, Control> createExampleCard)
    {
        if (report.TopMistakeLabels.Count == 0
            && report.CostliestMistakeLabels.Count == 0
            && report.MistakesByPhase.Count == 0
            && report.MistakesByOpening.Count == 0
            && report.GamesBySide.Count == 0
            && report.LabelTrends.Count == 0
            && report.MonthlyTrend.Count == 0
            && report.QuarterlyTrend.Count == 0)
        {
            yield return CreateBodyText("More detail becomes available once recurring patterns accumulate.");
            yield break;
        }

        yield return CreateBodyText("Detailed diagnosis behind the training plan.", "#D7E2EA");
        yield return CreateBodyText("Recurring patterns", "#9EB5C5");
        foreach (ProfileLabelStat item in report.TopMistakeLabels.Take(5))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: {FormatTimes(item.Count)} in highlighted mistakes");
        }

        yield return CreateBodyText("Costliest mistakes", "#9EB5C5");
        foreach (ProfileCostlyLabelStat item in report.CostliestMistakeLabels.Take(5))
        {
            yield return CreateBulletText($"{FormatMistakeLabel(item.Label)}: total CPL {item.TotalCentipawnLoss}, avg {item.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        }

        if (report.MistakesByPhase.Count > 0)
        {
            yield return CreateBodyText("By phase", "#9EB5C5");
            foreach (ProfilePhaseStat item in report.MistakesByPhase.Take(5))
            {
                yield return CreateBulletText($"{FormatPhase(item.Phase)}: {item.Count} highlighted mistakes");
            }
        }

        if (report.MistakesByOpening.Count > 0)
        {
            yield return CreateBodyText("By opening", "#9EB5C5");
            foreach (ProfileOpeningStat item in report.MistakesByOpening.Take(6))
            {
                yield return CreateBulletText($"{FormatOpening(item.Eco)}: {item.Count} highlighted mistakes");
            }
        }

        if (report.GamesBySide.Count > 0)
        {
            yield return CreateBodyText("By side", "#9EB5C5");
            foreach (ProfileSideStat item in report.GamesBySide)
            {
                string side = item.Side == PlayerSide.White ? "White" : "Black";
                yield return CreateBulletText($"{side}: {item.GamesAnalyzed} games, {item.HighlightedMistakes} highlighted mistakes");
            }
        }

        if (report.LabelTrends.Count > 0)
        {
            yield return CreateBodyText("Pattern trends", "#9EB5C5");
            foreach (ProfileLabelTrend trend in report.LabelTrends.Take(6))
            {
                yield return CreateBulletText(
                    $"{FormatMistakeLabel(trend.Label)}: {trend.Direction}, recent {trend.RecentCount}, previous {trend.PreviousCount}, recent CPL {trend.RecentAverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
            }
        }

        if (report.MonthlyTrend.Count > 0)
        {
            yield return CreateBodyText("Monthly trend", "#9EB5C5");
            foreach (ProfileMonthlyTrend item in report.MonthlyTrend.Take(6))
            {
                yield return CreateBulletText($"{item.MonthKey}: {item.GamesAnalyzed} games, mistakes {item.HighlightedMistakes}, CPL {item.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
            }
        }

        if (report.QuarterlyTrend.Count > 0)
        {
            yield return CreateBodyText("Quarterly trend", "#9EB5C5");
            foreach (ProfileQuarterlyTrend item in report.QuarterlyTrend.Take(4))
            {
                yield return CreateBulletText($"{item.QuarterKey}: {item.GamesAnalyzed} games, mistakes {item.HighlightedMistakes}, CPL {item.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
            }
        }

        IReadOnlyList<ProfileMistakeExample> rankedExamples = BuildDeepDiveExamples(report);
        if (rankedExamples.Count > 0)
        {
            yield return CreateBodyText($"Showing {rankedExamples.Count} ranked example positions from dominant motifs.", "#D7E2EA");

            foreach (IGrouping<string, ProfileMistakeExample> group in rankedExamples
                .GroupBy(example => example.Label, StringComparer.OrdinalIgnoreCase))
            {
                yield return CreateBodyText(FormatMistakeLabel(group.Key), "#9EB5C5");
                foreach (ProfileMistakeExample example in group)
                {
                    yield return createExampleCard(example, false);
                }
            }
        }
    }

    public static IEnumerable<Control> BuildExampleRows(
        PlayerProfileReport report,
        Func<ProfileMistakeExample, bool, Control> createExampleCard)
    {
        if (report.MistakeExamples.Count == 0)
        {
            yield return CreateBodyText("No example positions available yet.");
            yield break;
        }

        foreach (IGrouping<string, ProfileMistakeExample> group in report.MistakeExamples
            .Take(9)
            .GroupBy(example => example.Label, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return CreateBodyText(FormatMistakeLabel(group.Key), "#9EB5C5");
            foreach (ProfileMistakeExample example in group)
            {
                yield return createExampleCard(example, false);
            }
        }
    }

    public static IEnumerable<Control> BuildOpeningWeaknessRows(
        OpeningWeaknessReport? report,
        bool canPracticeOpening,
        Func<OpeningExampleGame, Control> createOpeningExampleCard,
        Func<OpeningMoveRecommendation, Control> createOpeningPositionCard,
        Func<string, Task> practiceOpeningAsync)
    {
        if (report is null || report.WeakOpenings.Count == 0)
        {
            yield return CreateBodyText("No recurring opening weaknesses available yet.");
            yield break;
        }

        yield return CreateInsightCard(
            "Opening signal",
            $"Across {report.OpeningGamesAnalyzed} opening samples, the sharpest problems cluster in {report.WeakOpenings.Count} recurring openings.",
            $"Average opening CPL: {report.AverageOpeningCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}. Use the example game and position shortcuts to jump straight into the board view.");

        foreach (OpeningWeaknessEntry opening in report.WeakOpenings.Take(5))
        {
            yield return CreateOpeningWeaknessCard(
                opening,
                report.OpeningGamesAnalyzed,
                canPracticeOpening,
                createOpeningExampleCard,
                createOpeningPositionCard,
                practiceOpeningAsync);
        }
    }

    private static Border CreateOpeningWeaknessCard(
        OpeningWeaknessEntry opening,
        int openingGamesAnalyzed,
        bool canPracticeOpening,
        Func<OpeningExampleGame, Control> createOpeningExampleCard,
        Func<OpeningMoveRecommendation, Control> createOpeningPositionCard,
        Func<string, Task> practiceOpeningAsync)
    {
        Border card = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10)
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = opening.OpeningDisplayName,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(CreateBodyText(
            $"{opening.Eco} | {FormatOpeningFrequency(opening.Count, openingGamesAnalyzed)} | Avg opening CPL {opening.AverageOpeningCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}",
            "#D7E2EA"));
        panel.Children.Add(CreateBodyText(
            $"{FormatOpeningWeaknessCategory(opening.Category)} | {FormatTrendHeadline(opening.TrendDirection)}",
            "#9EB5C5"));
        panel.Children.Add(CreateBodyText(opening.CategoryReason, "#9EB5C5"));
        panel.Children.Add(CreateBodyText(
            $"First recurring mistake: {FormatMistakeLabel(opening.FirstRecurringMistakeType ?? "unclassified")} ({opening.FirstRecurringMistakeCount} examples).",
            "#D7E2EA"));

        if (opening.RecurringMistakeSequences.Count > 0)
        {
            string sequenceSummary = string.Join(
                " | ",
                opening.RecurringMistakeSequences.Select(item =>
                    $"{string.Join(" -> ", item.Labels.Select(FormatMistakeLabel))} ({item.Count})"));
            panel.Children.Add(CreateBodyText($"Recurring sequence: {sequenceSummary}", "#9EB5C5"));
        }

        if (opening.ExampleGames.Count > 0)
        {
            panel.Children.Add(CreateBodyText("Example games", "#9EB5C5"));
            foreach (OpeningExampleGame example in opening.ExampleGames.Take(3))
            {
                panel.Children.Add(createOpeningExampleCard(example));
            }
        }

        if (opening.ExampleBetterMoves.Count > 0)
        {
            panel.Children.Add(CreateBodyText("Example positions", "#9EB5C5"));
            foreach (OpeningMoveRecommendation recommendation in opening.ExampleBetterMoves.Take(3))
            {
                panel.Children.Add(createOpeningPositionCard(recommendation));
            }
        }

        WrapPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Button trainingButton = new()
        {
            Content = "Practice this opening",
            IsEnabled = canPracticeOpening,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 220
        };
        trainingButton.Click += async (_, _) =>
        {
            trainingButton.IsEnabled = false;
            try
            {
                await practiceOpeningAsync(opening.Eco);
            }
            finally
            {
                trainingButton.IsEnabled = canPracticeOpening;
            }
        };
        actions.Children.Add(trainingButton);
        panel.Children.Add(actions);

        card.Child = panel;
        return card;
    }

    public static IEnumerable<Control> BuildCompactWeeklyPlanRows(
        PlayerProfileReport report,
        TrainingPlanFormattedOutput? formattedPlan,
        Func<string, Task> practiceOpeningAsync)
    {
        yield return formattedPlan is not null
            ? CreateInsightCard("Weekly plan", "Personalized plan", formattedPlan.ShortWeeklyPlan)
            : CreateInsightCard("Weekly plan", "Personalized plan", TrainerPreparingSuggestionsText);

        if (report.WeeklyPlan.Days.Count == 0)
        {
            yield return CreateBodyText("No weekly schedule available yet.", "#D7E2EA");
            yield break;
        }

        yield return CreateBodyText(report.WeeklyPlan.Budget.Summary, "#9EB5C5");
        foreach (WeeklyTrainingDay day in report.WeeklyPlan.Days.Take(7))
        {
            yield return CreateWeeklyDayCard(day, includeGoalInSummary: true, practiceOpeningAsync);
        }
    }

    public static IEnumerable<Control> BuildWeeklyPlanRows(
        PlayerProfileReport report,
        TrainingPlanFormattedOutput formattedPlan,
        Func<string, Task> practiceOpeningAsync)
    {
        yield return CreateInsightCard("Short weekly plan", "Personalized plan", formattedPlan.ShortWeeklyPlan);
        yield return CreateInsightCard("Detailed weekly plan", "Expanded version", formattedPlan.DetailedWeeklyPlan);
        yield return CreateInsightCard("Why these priorities", "Priority rationale", formattedPlan.PriorityRationale);
        yield return CreateInsightCard("Tone adapted plan", "Training voice", formattedPlan.ToneAdaptedVersion);

        yield return CreateInsightCard("Diagnosis to plan", report.TrainingPlan.Topics.Count == 0
            ? "Training plan"
            : $"Training plan built from {string.Join(", ", report.TrainingPlan.Topics.Select(topic => topic.Title))}.", report.TrainingPlan.Summary);

        yield return CreateInsightCard("Weekly budget", "Time budget", report.WeeklyPlan.Budget.Summary);

        yield return CreateBodyText("Priority order", "#9EB5C5");
        foreach (TrainingPlanTopic topic in report.TrainingPlan.Topics.OrderBy(topic => topic.Priority))
        {
            foreach (TrainingBlock block in topic.Blocks
                .OrderBy(block => GetBlockPurposeOrder(block.Purpose))
                .ThenBy(block => block.EstimatedMinutes))
            {
                Border planCard = new()
                {
                    Background = Brush.Parse("#182B37"),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                StackPanel planPanel = new() { Spacing = 6 };
                planPanel.Children.Add(new TextBlock
                {
                    Text = $"Priority {topic.Priority} • {topic.Title}",
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                });
                planPanel.Children.Add(CreateBodyText(block.Title, "#FFFFFF"));
                planPanel.Children.Add(CreateBodyText(
                    $"Block type: {FormatTrainingBlockKind(block.Kind)} | category: {FormatTrainingBlockPurpose(block.Purpose).ToLowerInvariant()} | estimated time: {block.EstimatedMinutes} min",
                    "#D7E2EA"));

                string topicContext = BuildTopicContext(topic);
                if (!string.IsNullOrWhiteSpace(topicContext))
                {
                    planPanel.Children.Add(CreateBodyText(topicContext, "#9EB5C5"));
                }

                planPanel.Children.Add(CreateBodyText("Why this topic now", "#9EB5C5"));
                planPanel.Children.Add(CreateBodyText(topic.WhyThisTopicNow, "#D7E2EA"));

                planCard.Child = planPanel;
                yield return planCard;
            }
        }

        yield return CreateBodyText("Topic breakdown", "#9EB5C5");
        foreach (TrainingPlanTopic topic in report.TrainingPlan.Topics.OrderBy(topic => topic.Priority))
        {
            Border topicCard = new()
            {
                Background = Brush.Parse("#182B37"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            StackPanel panel = new() { Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = $"{BuildRoleLabel(topic.Category)}: {topic.Title}",
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(CreateBodyText(topic.FocusArea, "#9EB5C5"));
            panel.Children.Add(CreateBodyText(topic.Summary, "#D7E2EA"));
            panel.Children.Add(CreateBodyText(topic.WhyThisTopicNow, "#D7E2EA"));

            if (!string.IsNullOrWhiteSpace(topic.Rationale))
            {
                panel.Children.Add(CreateBodyText(topic.Rationale, "#9EB5C5"));
            }

            string context = BuildTopicContext(topic);
            if (!string.IsNullOrWhiteSpace(context))
            {
                panel.Children.Add(CreateBodyText(context, "#9EB5C5"));
            }

            foreach (TrainingBlock block in topic.Blocks)
            {
                panel.Children.Add(CreateBulletText(
                    $"{FormatTrainingBlockKind(block.Kind)} | {FormatTrainingBlockPurpose(block.Purpose)} | {block.EstimatedMinutes} min | {block.Title}"));
            }

            topicCard.Child = panel;
            yield return topicCard;
        }

        yield return CreateBodyText("Weekly schedule", "#9EB5C5");
        foreach (WeeklyTrainingDay day in report.WeeklyPlan.Days)
        {
            yield return CreateWeeklyDayCard(day, includeGoalInSummary: false, practiceOpeningAsync);
        }
    }

    private static Border CreateWeeklyDayCard(
        WeeklyTrainingDay day,
        bool includeGoalInSummary,
        Func<string, Task> practiceOpeningAsync)
    {
        Border dayCard = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        StackPanel dayPanel = new() { Spacing = includeGoalInSummary ? 5 : 6 };
        dayPanel.Children.Add(new TextBlock
        {
            Text = $"Day {day.DayNumber}: {day.Topic}",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        if (includeGoalInSummary)
        {
            dayPanel.Children.Add(CreateBodyText($"{day.WorkType} | {day.EstimatedMinutes} min | {day.Goal}", "#D7E2EA"));
        }
        else
        {
            dayPanel.Children.Add(CreateBodyText($"{day.WorkType} | {day.EstimatedMinutes} min", "#D7E2EA"));
            dayPanel.Children.Add(CreateBodyText(day.Goal, "#D7E2EA"));
        }

        if (day.LaunchTrainingMode.HasValue && day.RelatedOpenings is { Count: > 0 })
        {
            string opening = day.RelatedOpenings[0];
            Button button = new()
            {
                Content = "Practice this opening",
                Margin = new Thickness(0, 0, 8, 8),
                MinWidth = 200
            };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    await practiceOpeningAsync(opening);
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
            dayPanel.Children.Add(button);
        }

        dayCard.Child = dayPanel;
        return dayCard;
    }

    private static Border CreateChartCard(string title, IReadOnlyList<ProfileTrendChartSeries> series)
    {
        Border card = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(CreateBodyText(title, "#FFFFFF"));
        panel.Children.Add(new ProfileTrendChartView
        {
            Height = 190,
            Series = series
        });
        card.Child = panel;
        return card;
    }

    private static Border CreateInsightCard(string label, string value, string? detail = null)
    {
        Border card = new()
        {
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(CreateBodyText(label, "#9EB5C5"));
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(detail))
        {
            panel.Children.Add(CreateBodyText(detail, "#D7E2EA"));
        }

        card.Child = panel;
        return card;
    }

    private static Border CreateCardBorder()
    {
        return new Border
        {
            Background = Brush.Parse("#203542"),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static StackPanel CreateCardPanel()
    {
        return new StackPanel
        {
            Spacing = 6
        };
    }

    private static Border CreateMetricTile(string label, string value, double? width = null)
    {
        Border tile = new()
        {
            Width = width ?? 220,
            Background = Brush.Parse("#182B37"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 8)
        };

        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush.Parse("#9EB5C5"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        tile.Child = panel;
        return tile;
    }

    private static TextBlock CreateBodyText(string text, string? color = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color is null ? Brushes.White : Brush.Parse(color),
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock CreateBulletText(string text)
    {
        return new TextBlock
        {
            Text = $"• {text}",
            Foreground = Brushes.White,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static string FormatPeriod(ProfileProgressPeriod period)
    {
        return $"{period.GamesAnalyzed.ToString(CultureInfo.InvariantCulture)} games, CPL {period.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, highlighted mistakes/game {period.HighlightedMistakesPerGame.ToString("F2", CultureInfo.InvariantCulture)}";
    }
}
