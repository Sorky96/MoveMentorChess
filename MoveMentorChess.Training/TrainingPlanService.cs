namespace MoveMentorChess.Training;

public sealed class TrainingPlanService
{
    private readonly TrainingPlanTopicScorer topicScorer = new();

    public TrainingPlanReport Build(PlayerProfileReport profileReport, OpeningWeaknessReport? openingReport = null)
        => Build(profileReport, openingReport, []);

    public TrainingPlanReport Build(
        PlayerProfileReport profileReport,
        OpeningWeaknessReport? openingReport,
        IReadOnlyList<OpeningTrainingSessionResult>? trainingHistory)
    {
        ArgumentNullException.ThrowIfNull(profileReport);

        OpeningTrainingOutcomeSummary trainingSummary = OpeningTrainingOutcomeSummarizer.Build(trainingHistory);
        List<TrainingPlanTopic> topics = BuildTopics(profileReport, openingReport, trainingSummary);
        IReadOnlyList<TrainingRecommendation> recommendations = topics
            .Select(topic => new TrainingRecommendation(
                topic.Priority,
                topic.FocusArea,
                topic.Title,
                topic.Summary,
                topic.EmphasisPhase,
                topic.EmphasisSide,
                topic.RelatedOpenings,
                topic.Checklist,
                topic.SuggestedDrills,
                topic.Examples,
                topic.Blocks))
            .ToList();

        WeeklyTrainingPlan weeklyPlan = BuildWeeklyPlan(profileReport.DisplayName, topics);
        string summary = topics.Count == 0
            ? "Not enough stable profile data yet. Start with a light review loop and collect more analyzed games."
            : $"Built from deterministic priorities and block mapping: {string.Join(", ", topics.Select(topic => topic.Title))}.";
        IReadOnlyList<TrainingPlanDashboardItem> dashboard = BuildDashboard(profileReport, openingReport, trainingSummary, topics);

        return new TrainingPlanReport(
            profileReport.PlayerKey,
            profileReport.DisplayName,
            profileReport.ProgressSignal.Direction,
            summary,
            topics,
            recommendations,
            weeklyPlan,
            dashboard);
    }

    private List<TrainingPlanTopic> BuildTopics(
        PlayerProfileReport profileReport,
        OpeningWeaknessReport? openingReport,
        OpeningTrainingOutcomeSummary trainingSummary)
    {
        List<string> candidateLabels = profileReport.TopMistakeLabels
            .Select(item => item.Label)
            .Concat(profileReport.CostliestMistakeLabels.Select(item => item.Label))
            .Concat(profileReport.MistakeExamples.Select(item => item.Label))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (TrainingPlanOpeningWeaknessSelector.HasActionableOpeningWeakness(openingReport)
            && !candidateLabels.Contains("opening_principles", StringComparer.Ordinal))
        {
            candidateLabels.Add("opening_principles");
        }

        if (candidateLabels.Count == 0)
        {
            return [CreateFallbackTopic(profileReport)];
        }

        List<TrainingPlanTopic> rankedTopics = candidateLabels
            .Select(label => BuildTopic(profileReport, openingReport, trainingSummary, label))
            .OrderByDescending(topic => topic.PriorityBreakdown.TotalScore)
            .ThenByDescending(topic => topic.PriorityBreakdown.TrainingScore)
            .ThenByDescending(topic => topic.PriorityBreakdown.CostScore)
            .ThenByDescending(topic => topic.PriorityBreakdown.FrequencyScore)
            .ThenBy(topic => topic.Label, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        int nonImprovingRank = 0;
        return rankedTopics
            .Select((topic, index) => topic with
            {
                Priority = index + 1,
                Category = DetermineCategory(topic.TrendDirection, nonImprovingRank += topic.TrendDirection == ProfileProgressDirection.Improving ? 0 : 1)
            })
            .ToList();
    }

    private TrainingPlanTopic BuildTopic(
        PlayerProfileReport profileReport,
        OpeningWeaknessReport? openingReport,
        OpeningTrainingOutcomeSummary trainingSummary,
        string label)
    {
        ProfileLabelStat? frequent = profileReport.TopMistakeLabels
            .FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.Ordinal));
        ProfileCostlyLabelStat? costly = profileReport.CostliestMistakeLabels
            .FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.Ordinal));
        List<ProfileMistakeExample> examples = profileReport.MistakeExamples
            .Where(item => string.Equals(item.Label, label, StringComparison.Ordinal))
            .Take(3)
            .ToList();

        GamePhase? emphasisPhase = DetermineEmphasisPhase(profileReport, examples);
        PlayerSide? emphasisSide = examples.Count == 0
            ? null
            : examples
                .GroupBy(item => item.Side)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => (PlayerSide?)group.Key)
                .FirstOrDefault();
        List<string> relatedOpenings = BuildRelatedOpenings(label, examples, openingReport);
        ProfileProgressDirection labelTrend = profileReport.LabelTrends
            .FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.Ordinal))
            ?.Direction
            ?? ProfileProgressDirection.InsufficientData;

        TrainingPlanTopicScoringResult scoring = topicScorer.Score(new TrainingPlanTopicScoringInput(
            label,
            frequent?.Count ?? 0,
            costly?.TotalCentipawnLoss ?? 0,
            costly?.AverageCentipawnLoss,
            labelTrend,
            emphasisPhase,
            profileReport.MistakesByPhase,
            relatedOpenings,
            openingReport,
            trainingSummary));
        TopicTemplate template = GetTemplate(label);
        IReadOnlyList<TrainingBlock> blocks = BuildBlocks(label, template, emphasisPhase, emphasisSide, relatedOpenings);

        string phaseSummary = emphasisPhase.HasValue
            ? $" Most often it appears in {TrainingTextFormatter.FormatPhase(emphasisPhase.Value).ToLowerInvariant()}."
            : string.Empty;
        string openingSummary = relatedOpenings.Count == 0
            ? string.Empty
            : $" It also clusters around {string.Join(" / ", relatedOpenings.Select(TrainingTextFormatter.FormatOpening))}.";
        string blockSummary = blocks.Count == 0
            ? string.Empty
            : $" Training blocks: {string.Join(", ", blocks.Select(block => $"{TrainingTextFormatter.FormatTrainingBlockPurpose(block.Purpose).ToLowerInvariant()} {TrainingTextFormatter.FormatTrainingBlockKind(block.Kind).ToLowerInvariant()}"))}.";
        TrainingPlanTopicNarrative narrative = TrainingPlanTopicNarrativeBuilder.Build(new TrainingPlanTopicNarrativeInput(
            label,
            frequent?.Count ?? 0,
            costly?.TotalCentipawnLoss ?? 0,
            costly?.AverageCentipawnLoss,
            labelTrend,
            profileReport.MistakesByPhase.Count > 0 ? profileReport.MistakesByPhase[0].Phase : null,
            emphasisPhase,
            relatedOpenings,
            openingReport,
            trainingSummary));
        string summary = $"{template.Description} {BuildTrendSummary(labelTrend)}{phaseSummary}{openingSummary}{blockSummary}".Trim();

        return new TrainingPlanTopic(
            0,
            TrainingPlanTopicCategory.CoreWeakness,
            label,
            template.FocusArea,
            template.Title,
            summary,
            narrative.WhyThisTopicNow,
            narrative.Rationale,
            labelTrend,
            emphasisPhase,
            emphasisSide,
            relatedOpenings,
            ExtractChecklist(blocks),
            ExtractSuggestedDrills(blocks),
            blocks,
            examples,
            scoring.PriorityBreakdown,
            scoring.Status);
    }

    private static TrainingPlanTopic CreateFallbackTopic(PlayerProfileReport profileReport)
    {
        GamePhase? fallbackPhase = profileReport.MistakesByPhase.Count > 0 ? profileReport.MistakesByPhase[0].Phase : null;
        IReadOnlyList<TrainingBlock> blocks =
        [
            CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.GameReview, "Review recent critical moments", "Replay one recent game and stop before every large evaluation swing.", 30, fallbackPhase, null, []),
            CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.SlowPlayFocus, "Slow down before committing", "Play one calmer practice block and name the first thing that had to be checked before moving.", 20, fallbackPhase, null, []),
            CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "Default board-scan checklist", "Keep one short board-scan phrase visible and use it before every critical move.", 15, fallbackPhase, null, [])
        ];

        return new TrainingPlanTopic(
            1,
            TrainingPlanTopicCategory.CoreWeakness,
            "general_review",
            "General review",
            "Review critical moments",
            "No single tactical or strategic pattern dominates yet, so keep a stable review rhythm and collect more analyzed games.",
            "There is not enough topic-specific data yet, so this stays as a general review anchor until stronger patterns emerge.",
            "Fallback topic created because the profile does not yet contain enough labeled mistakes.",
            ProfileProgressDirection.InsufficientData,
            fallbackPhase,
            null,
            [],
            ExtractChecklist(blocks),
            ExtractSuggestedDrills(blocks),
            blocks,
            [],
            new TrainingPlanPriorityBreakdown(0, 0, 0, 0, 0),
            TrainingPlanTopicStatus.NewWeakness);
    }

    private static WeeklyTrainingPlan BuildWeeklyPlan(string displayName, IReadOnlyList<TrainingPlanTopic> topics)
    {
        List<TrainingPlanTopic> planTopics = topics.ToList();
        TrainingPlanTopic core = planTopics[0];
        TrainingPlanTopic secondary = planTopics[Math.Min(1, planTopics.Count - 1)];
        TrainingPlanTopic maintenance = planTopics[Math.Min(2, planTopics.Count - 1)];

        List<WeeklyTrainingDay> days =
        [
            CreateDay(1, core, GetBlock(core, TrainingBlockPurpose.Repair)),
            CreateDay(2, core, GetBlock(core, TrainingBlockPurpose.Maintain)),
            CreateDay(3, secondary, GetBlock(secondary, TrainingBlockPurpose.Repair)),
            CreateDay(4, secondary, GetBlock(secondary, TrainingBlockPurpose.Checklist)),
            CreateDay(5, core, GetBlock(core, TrainingBlockPurpose.Checklist)),
            CreateDay(6, maintenance, GetBlock(maintenance, TrainingBlockPurpose.Maintain)),
            CreateDay(7, maintenance, GetBlock(maintenance, TrainingBlockPurpose.Checklist))
        ];

        WeeklyTrainingBudget budget = BuildWeeklyBudget(core, secondary, maintenance, days);

        return new WeeklyTrainingPlan(
            $"{displayName} Weekly Training Plan",
            $"Deterministic weekly cycle built from the core weakness ({core.Title}), the secondary weakness ({secondary.Title}) and the maintenance topic ({maintenance.Title}).",
            budget,
            days);
    }

    private static WeeklyTrainingDay CreateDay(int dayNumber, TrainingPlanTopic topic, TrainingBlock block)
    {
        return new WeeklyTrainingDay(
            dayNumber,
            topic.Title,
            $"{TrainingTextFormatter.FormatTrainingBlockPurpose(block.Purpose)} • {TrainingTextFormatter.FormatTrainingBlockKind(block.Kind)}",
            block.Description,
            block.EstimatedMinutes,
            topic.Category,
            block.Purpose,
            block.Kind,
            topic.RelatedOpenings,
            block.Kind == TrainingBlockKind.OpeningReview
                ? DetermineOpeningTrainingMode(block.Purpose)
                : null);
    }

    private static WeeklyTrainingBudget BuildWeeklyBudget(TrainingPlanTopic core, TrainingPlanTopic secondary, TrainingPlanTopic maintenance, IReadOnlyList<WeeklyTrainingDay> days)
    {
        int coreMinutes = days.Where(day => day.Category == TrainingPlanTopicCategory.CoreWeakness).Sum(day => day.EstimatedMinutes);
        int secondaryMinutes = days.Where(day => day.Category == TrainingPlanTopicCategory.SecondaryWeakness).Sum(day => day.EstimatedMinutes);
        int maintenanceMinutes = days.Where(day => day.Category == TrainingPlanTopicCategory.MaintenanceTopic).Sum(day => day.EstimatedMinutes);
        int integrationMinutes = 0;
        int totalMinutes = days.Sum(day => day.EstimatedMinutes);

        return new WeeklyTrainingBudget(
            totalMinutes,
            coreMinutes,
            secondaryMinutes,
            maintenanceMinutes,
            integrationMinutes,
            $"About {totalMinutes} minutes for the week: {coreMinutes} on {core.Title}, {secondaryMinutes} on {secondary.Title}, {maintenanceMinutes} on {maintenance.Title}. Each slot is selected deterministically from repair, maintain and checklist blocks.");
    }

    private static TrainingBlock GetBlock(TrainingPlanTopic topic, TrainingBlockPurpose purpose)
    {
        for (int i = 0; i < topic.Blocks.Count; i++)
        {
            if (topic.Blocks[i].Purpose == purpose)
            {
                return topic.Blocks[i];
            }
        }
        return topic.Blocks[0];
    }

    private static List<TrainingPlanDashboardItem> BuildDashboard(
        PlayerProfileReport profileReport,
        OpeningWeaknessReport? openingReport,
        OpeningTrainingOutcomeSummary trainingSummary,
        IReadOnlyList<TrainingPlanTopic> topics)
    {
        List<TrainingPlanDashboardItem> items = [];

        TrainingPlanTopic? primary = topics.OrderBy(topic => topic.Priority).FirstOrDefault();
        if (primary is not null)
        {
            items.Add(new TrainingPlanDashboardItem(
                "Current priority",
                $"{primary.Title} is marked {FormatStatus(primary.Status)}.",
                primary.WhyThisTopicNow));
        }

        if (trainingSummary.SessionCount > 0)
        {
            items.Add(new TrainingPlanDashboardItem(
                "Opening trainer results",
                $"{trainingSummary.CorrectCount} correct, {trainingSummary.PlayableCount} playable, {trainingSummary.WrongCount} wrong.",
                $"Based on {trainingSummary.AttemptCount} attempts from {trainingSummary.SessionCount} completed opening-trainer session(s)."));
        }
        else
        {
            items.Add(new TrainingPlanDashboardItem(
                "Opening trainer results",
                "No completed opening-trainer sessions are recorded yet.",
                "The plan is still driven by analyzed games and opening weakness reports."));
        }

        if (openingReport is not null && openingReport.WeakOpenings.Count > 0)
        {
            OpeningWeaknessEntry opening = openingReport.WeakOpenings[0];
            items.Add(new TrainingPlanDashboardItem(
                "Opening evidence",
                $"{TrainingTextFormatter.FormatOpening(opening.Eco)} is {FormatOpeningCategory(opening.Category)}.",
                opening.CategoryReason));
        }

        items.Add(new TrainingPlanDashboardItem(
            "Profile signal",
            TrainingTextFormatter.FormatTrendHeadline(profileReport.ProgressSignal.Direction),
            profileReport.ProgressSignal.Summary));

        return items;
    }

    private static List<string> BuildRelatedOpenings(
        string label,
        List<ProfileMistakeExample> examples,
        OpeningWeaknessReport? openingReport)
    {
        IEnumerable<string> exampleOpenings = examples
            .Select(item => item.Eco)
            .Where(item => !string.IsNullOrWhiteSpace(item));
        IEnumerable<string> values = exampleOpenings;

        if (string.Equals(label, "opening_principles", StringComparison.Ordinal)
            && TrainingPlanOpeningWeaknessSelector.HasActionableOpeningWeakness(openingReport))
        {
            values = TrainingPlanOpeningWeaknessSelector.GetActionableOpenings(openingReport)
                .Select(item => item.Eco)
                .Concat(exampleOpenings);
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static OpeningTrainingMode DetermineOpeningTrainingMode(TrainingBlockPurpose purpose)
    {
        return purpose switch
        {
            TrainingBlockPurpose.Repair => OpeningTrainingMode.MistakeRepair,
            TrainingBlockPurpose.Maintain => OpeningTrainingMode.LineRecall,
            TrainingBlockPurpose.Checklist => OpeningTrainingMode.BranchAwareness,
            _ => OpeningTrainingMode.LineRecall
        };
    }

    private static TrainingPlanTopicCategory DetermineCategory(ProfileProgressDirection direction, int nonImprovingRank)
    {
        if (direction == ProfileProgressDirection.Improving)
        {
            return TrainingPlanTopicCategory.MaintenanceTopic;
        }

        return nonImprovingRank switch
        {
            1 => TrainingPlanTopicCategory.CoreWeakness,
            2 => TrainingPlanTopicCategory.SecondaryWeakness,
            _ => TrainingPlanTopicCategory.MaintenanceTopic
        };
    }

    private static GamePhase? DetermineEmphasisPhase(PlayerProfileReport profileReport, List<ProfileMistakeExample> examples)
    {
        if (examples.Count > 0)
        {
            return examples
                .GroupBy(item => item.Phase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => (GamePhase?)group.Key)
                .FirstOrDefault();
        }

        return profileReport.MistakesByPhase.Count > 0 ? profileReport.MistakesByPhase[0].Phase : null;
    }

    private static string BuildTrendSummary(ProfileProgressDirection direction)
    {
        return direction switch
        {
            ProfileProgressDirection.Regressing => "The recent trend makes this theme more urgent.",
            ProfileProgressDirection.Improving => "Recent form is improving, so this becomes a lighter maintenance target.",
            ProfileProgressDirection.Stable => "The trend is stable, so this remains a reliable training target.",
            _ => "There is not enough trend data yet, so the ranking relies on mistake volume and cost."
        };
    }

    private static string FormatStatus(TrainingPlanTopicStatus status)
    {
        return status switch
        {
            TrainingPlanTopicStatus.NewWeakness => "new weakness",
            TrainingPlanTopicStatus.Improving => "improving",
            TrainingPlanTopicStatus.Stable => "stable",
            TrainingPlanTopicStatus.Urgent => "urgent",
            _ => "stable"
        };
    }

    private static string FormatOpeningCategory(OpeningWeaknessCategory category)
    {
        return category switch
        {
            OpeningWeaknessCategory.FixNow => "urgent",
            OpeningWeaknessCategory.ReviewLater => "under review",
            OpeningWeaknessCategory.Stable => "stable",
            _ => "tracked"
        };
    }

    private static TopicTemplate GetTemplate(string label)
    {
        return label switch
        {
            "hanging_piece" => new TopicTemplate("Board safety", "Protect loose pieces", "Repeated material losses come from leaving pieces underdefended after otherwise playable moves."),
            "missed_tactic" => new TopicTemplate("Tactics", "Checks, captures, threats", "Forcing resources are being missed often enough that tactical scanning should stay at the front of the move process."),
            "opening_principles" => new TopicTemplate("Opening discipline", "Clean up the opening", "Too many early inaccuracies come from development delays, side moves and king-safety shortcuts."),
            "king_safety" => new TopicTemplate("King safety", "Safer king decisions", "King shelter is breaking down too easily after pawn pushes or slow defensive reactions."),
            "endgame_technique" => new TopicTemplate("Endgames", "Sharpen endgame technique", "Reduced-material positions still leak points through technical slips and king-activity mistakes."),
            "material_loss" => new TopicTemplate("Material discipline", "Calculate the full exchange", "Material is being dropped in forcing lines that are not being checked to the end."),
            "piece_activity" => new TopicTemplate("Piece coordination", "Improve piece activity", "Useful tempi are being spent on moves that reduce coordination instead of improving the worst-placed piece."),
            _ => new TopicTemplate("Pattern review", TrainingTextFormatter.FormatMistakeLabel(label), "This recurring label keeps surfacing in the profile and deserves a focused training block.")
        };
    }

    private static List<string> ExtractChecklist(IReadOnlyList<TrainingBlock> blocks)
    {
        return blocks
            .Where(block => block.Purpose == TrainingBlockPurpose.Checklist)
            .Select(block => block.Description)
            .DefaultIfEmpty("Use one simple board-scan checklist before every critical move.")
            .ToList();
    }

    private static List<string> ExtractSuggestedDrills(IReadOnlyList<TrainingBlock> blocks)
    {
        return blocks
            .Where(block => block.Purpose != TrainingBlockPurpose.Checklist)
            .Select(block => block.Description)
            .ToList();
    }

    private static TrainingBlock CreateBlock(TrainingBlockPurpose purpose, TrainingBlockKind kind, string title, string description, int estimatedMinutes, GamePhase? emphasisPhase, PlayerSide? emphasisSide, List<string> relatedOpenings)
    {
        return new TrainingBlock(purpose, kind, title, description, estimatedMinutes, emphasisPhase, emphasisSide, relatedOpenings);
    }

    private static IReadOnlyList<TrainingBlock> BuildBlocks(string label, TopicTemplate template, GamePhase? emphasisPhase, PlayerSide? emphasisSide, List<string> relatedOpenings)
    {
        string phaseText = emphasisPhase.HasValue
            ? TrainingTextFormatter.FormatPhase(emphasisPhase.Value).ToLowerInvariant()
            : "critical positions";
        string sideText = emphasisSide.HasValue
            ? emphasisSide.Value == PlayerSide.White ? " as White" : " as Black"
            : string.Empty;
        string openingText = relatedOpenings.Count == 0
            ? "your own recurring structures"
            : string.Join(" / ", relatedOpenings.Select(TrainingTextFormatter.FormatOpening));

        return label switch
        {
            "hanging_piece" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.Tactics, "Loose-piece repair", $"Solve a short attacker-defender counting set and keep checking what becomes loose after each move{sideText}.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Loose-piece review", $"Replay two recent mistakes from {openingText} and mark the first move where a piece stopped being defended.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "Loose-piece checklist", $"In {phaseText}, ask after every candidate move: what did I leave loose{sideText}?", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ],
            "missed_tactic" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.Tactics, "Forcing-line repair", $"Run a tactics block built around checks, captures and threats, especially in {phaseText}.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Forcing-moment review", $"Review two of your own sharp positions from {openingText} and compare your move with the first forcing move you missed.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "CCT checklist", $"Before every critical move{sideText}, list checks, captures and threats for both sides.", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ],
            "opening_principles" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.OpeningReview, "Opening repair", $"Review the first 10 moves from {openingText} and replace drifting moves with development, center control or castling choices.", 25, emphasisPhase ?? GamePhase.Opening, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Opening phase review", "Annotate one recent game and mark the first move where opening discipline broke down.", 20, emphasisPhase ?? GamePhase.Opening, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "Opening checklist", "In the first 10 moves, ask whether the move develops, castles or fights for the center before anything else.", 15, emphasisPhase ?? GamePhase.Opening, emphasisSide, relatedOpenings)
            ],
            "king_safety" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.Tactics, "King-safety repair", $"Do a short mating-net and forcing-move set, then identify the first unsafe concession in your own {phaseText} positions.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "King-shelter review", $"Review one recent game from {openingText} and mark when your king shelter became harder to defend{sideText}.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "King-safety checklist", "Before pawn pushes near your king, name the weakened square or file and the opponent's forcing reply.", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ],
            "endgame_technique" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.EndgameDrill, "Endgame repair", "Run one king-and-pawn or rook-endgame drill block and compare candidate moves by king activity first.", 25, emphasisPhase ?? GamePhase.Endgame, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Technical ending review", "Replay one of your own reduced-material games slowly and mark the moment the clean conversion plan disappeared.", 20, emphasisPhase ?? GamePhase.Endgame, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "Endgame checklist", "Before every endgame move, compare king activity, passed pawns and counterplay in that order.", 15, emphasisPhase ?? GamePhase.Endgame, emphasisSide, relatedOpenings)
            ],
            "material_loss" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.Tactics, "Exchange calculation repair", "Run a capture-sequence exercise block and say the final material balance before accepting any forcing line.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Exchange review", $"Review two costly lines from {openingText} and stop where the calculation was cut short.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.SlowPlayFocus, "Material checklist", "Before every tactical capture, calculate the full exchange to the end and name the final count.", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ],
            "piece_activity" =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.SlowPlayFocus, "Worst-piece repair", "Play through one slow block and identify the worst-placed piece before every move.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.GameReview, "Coordination review", "Replay one drifting middlegame and mark the first moment you improved the wrong piece.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.OpeningReview, "Piece-activity checklist", $"Review {openingText} and keep asking which move improves the worst-placed piece instead of adding a side move.", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ],
            _ =>
            [
                CreateBlock(TrainingBlockPurpose.Repair, TrainingBlockKind.GameReview, $"{template.Title} repair", "Review two of your own positions with this label and write down the recurring decision error.", 25, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Maintain, TrainingBlockKind.SlowPlayFocus, $"{template.Title} maintenance", $"Play one slower block in {phaseText} and keep one practical reminder visible for this pattern.", 20, emphasisPhase, emphasisSide, relatedOpenings),
                CreateBlock(TrainingBlockPurpose.Checklist, TrainingBlockKind.GameReview, $"{template.Title} checklist", $"Before every critical move, repeat one short review question linked to {template.FocusArea.ToLowerInvariant()}.", 15, emphasisPhase, emphasisSide, relatedOpenings)
            ]
        };
    }

    private sealed record TopicTemplate(string FocusArea, string Title, string Description);
}
