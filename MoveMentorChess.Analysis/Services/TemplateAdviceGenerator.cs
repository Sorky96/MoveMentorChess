using MoveMentorChess.Localization;

namespace MoveMentorChess.Analysis;

public sealed class TemplateAdviceGenerator : IAdviceGenerator
{
    private readonly AdviceGenerationSettings settings;

    public TemplateAdviceGenerator(AdviceGenerationSettings? settings = null)
    {
        this.settings = settings ?? AdviceGenerationSettings.Default;
    }

    public MoveExplanation Generate(
        ReplayPly replay,
        MoveQualityBucket quality,
        MistakeTag? tag,
        string? bestMoveUci,
        int? centipawnLoss,
        ExplanationLevel level = ExplanationLevel.Intermediate,
        AdviceGenerationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(replay);

        string label = tag?.Label ?? "general";
        string qualityText = FormatQuality(quality);
        string lossText = centipawnLoss is int cp
            ? Localizer.Format(LocalizedStrings.AdviceLostCentipawns, cp)
            : Localizer.Text(LocalizedStrings.AdviceChangedEvaluation);
        string bestMoveText = context?.PromptContext?.BestMoveSan ?? FormatMoveFromFen(replay.FenBefore, bestMoveUci);
        string openingName = context?.PromptContext?.OpeningName ?? string.Empty;

        string patternHint = label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceHintMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceHintHangingPiece),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceHintKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceHintOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceHintPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceHintEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceHintGeneral)
        };

        string shortText = Shorten(BuildShortText(replay, qualityText, lossText, label, bestMoveText, level), settings.MaxShortTextLength);
        string detailedText = Shorten(MergeSentences(
            BuildDetailedText(replay, qualityText, label, bestMoveText, centipawnLoss, level, openingName),
            string.Empty),
            settings.MaxDetailedTextLength);
        string trainingHint = Shorten(BuildTrainingHint(patternHint, label, level), settings.MaxTrainingHintLength);

        return new MoveExplanation(shortText, trainingHint, detailedText);
    }

    private static string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLength <= 0 || text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        int candidateLength = Math.Max(0, maxLength - 1);
        int lastSentenceBreak = text.LastIndexOfAny(['.', '!', '?'], candidateLength - 1);
        if (lastSentenceBreak >= 0 && lastSentenceBreak + 1 >= maxLength / 2)
        {
            return text[..(lastSentenceBreak + 1)].Trim();
        }

        int lastWordBreak = text.LastIndexOf(' ', candidateLength - 1);
        if (lastWordBreak >= maxLength / 2)
        {
            int maxWordLength = Math.Max(1, maxLength - 3);
            string shortened = text[..Math.Min(lastWordBreak, maxWordLength)].Trim();
            return $"{shortened}...";
        }

        int hardLimit = Math.Max(1, maxLength - 3);
        return $"{text[..hardLimit].Trim()}...";
    }

    private static string BuildShortText(
        ReplayPly replay,
        string qualityText,
        string lossText,
        string label,
        string bestMoveText,
        ExplanationLevel level)
    {
        return level switch
        {
            ExplanationLevel.Beginner => string.IsNullOrWhiteSpace(bestMoveText)
                ? $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdviceSimpleView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdvicePattern, DisplayLabel(label))}"
                : $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdviceSimpleView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdviceStrongerOption, bestMoveText)}",
            ExplanationLevel.Advanced => string.IsNullOrWhiteSpace(bestMoveText)
                ? $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdviceEngineView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdvicePattern, DisplayLabel(label))}"
                : $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdviceEngineView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdviceStrongerOption, bestMoveText)}",
            _ => string.IsNullOrWhiteSpace(bestMoveText)
                ? $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdvicePracticalView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdvicePattern, DisplayLabel(label))}"
                : $"{Localizer.Format(LocalizedStrings.AdviceWasQualityLoss, Localizer.Text(LocalizedStrings.AdvicePracticalView), replay.San, qualityText, lossText)} {Localizer.Format(LocalizedStrings.AdviceStrongerOption, bestMoveText)}"
        };
    }

    private static string BuildDetailedText(
        ReplayPly replay,
        string qualityText,
        string label,
        string bestMoveText,
        int? centipawnLoss,
        ExplanationLevel level,
        string openingName)
    {
        (string what, string why, string better, string watch) = level switch
        {
            ExplanationLevel.Beginner => (
                BuildProblemSentenceBeginner(replay, qualityText, centipawnLoss),
                BuildWhySentenceBeginner(label),
                BuildBetterSentence(bestMoveText, openingName, label, level),
                BuildRecognitionSentenceBeginner(label)),
            ExplanationLevel.Advanced => (
                BuildProblemSentenceAdvanced(replay, qualityText, centipawnLoss),
                BuildWhySentenceAdvanced(label),
                BuildBetterSentence(bestMoveText, openingName, label, level),
                BuildRecognitionSentenceAdvanced(label)),
            _ =>
                (BuildProblemSentence(replay, qualityText, centipawnLoss),
                BuildWhySentence(label),
                BuildBetterSentence(bestMoveText, openingName, label, level),
                BuildRecognitionSentence(label))
        };

        return ComposeDetailedStructure(what, why, better, watch);
    }

    private static string BuildTrainingHint(string baseHint, string label, ExplanationLevel level)
    {
        return level switch
        {
            ExplanationLevel.Beginner => Localizer.Format(LocalizedStrings.AdviceTrainingBeginner, baseHint),
            ExplanationLevel.Advanced => label switch
            {
                "material_loss" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedMaterialLoss),
                "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedHangingPiece),
                "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedMissedTactic),
                "king_safety" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedKingSafety),
                "opening_principles" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedOpeningPrinciples),
                "piece_activity" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedPieceActivity),
                "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceTrainingAdvancedEndgameTechnique),
                _ => baseHint
            },
            _ => Localizer.Format(LocalizedStrings.AdviceTrainingIntermediate, baseHint)
        };
    }

    private static string BuildProblemSentence(ReplayPly replay, string qualityText, int? centipawnLoss)
    {
        return centipawnLoss is int cp
            ? Localizer.Format(LocalizedStrings.AdviceProblemIntermediateCentipawns, replay.San, qualityText, cp)
            : Localizer.Format(LocalizedStrings.AdviceProblemIntermediateEvaluation, replay.San, qualityText);
    }

    private static string BuildProblemSentenceBeginner(ReplayPly replay, string qualityText, int? centipawnLoss)
    {
        return centipawnLoss is int cp
            ? Localizer.Format(LocalizedStrings.AdviceProblemBeginnerCentipawns, replay.San, qualityText, cp)
            : Localizer.Format(LocalizedStrings.AdviceProblemBeginnerEvaluation, replay.San, qualityText);
    }

    private static string BuildProblemSentenceAdvanced(ReplayPly replay, string qualityText, int? centipawnLoss)
    {
        return centipawnLoss is int cp
            ? Localizer.Format(LocalizedStrings.AdviceProblemAdvancedCentipawns, replay.San, qualityText, cp)
            : Localizer.Format(LocalizedStrings.AdviceProblemAdvancedEvaluation, replay.San, qualityText);
    }

    private static string BuildWhySentence(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWhyMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWhyHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWhyMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWhyKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWhyOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWhyPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWhyEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWhyGeneral)
        };
    }

    private static string BuildWhySentenceBeginner(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWhyBeginnerGeneral)
        };
    }

    private static string BuildWhySentenceAdvanced(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWhyAdvancedGeneral)
        };
    }

    private static string BuildRecognitionSentence(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWatchMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWatchHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWatchMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWatchKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWatchOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWatchPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWatchEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWatchGeneral)
        };
    }

    private static string BuildRecognitionSentenceBeginner(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWatchBeginnerGeneral)
        };
    }

    private static string BuildRecognitionSentenceAdvanced(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedMissedTactic),
            "king_safety" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedKingSafety),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedOpeningPrinciples),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedEndgameTechnique),
            _ => Localizer.Text(LocalizedStrings.AdviceWatchAdvancedGeneral)
        };
    }

    private static string BuildBetterSentence(string bestMoveText, string openingName, string label, ExplanationLevel level)
    {
        string main = string.IsNullOrWhiteSpace(bestMoveText)
            ? Localizer.Text(LocalizedStrings.AdviceBetterCalmer)
            : Localizer.Format(LocalizedStrings.AdviceBetterStronger, bestMoveText);

        if (string.IsNullOrWhiteSpace(openingName))
        {
            return main;
        }

        return level switch
        {
            ExplanationLevel.Beginner when label == "opening_principles" => $"{main} {Localizer.Format(LocalizedStrings.AdviceBetterOpeningBeginner, openingName)}",
            ExplanationLevel.Advanced when label == "opening_principles" => $"{main} {Localizer.Format(LocalizedStrings.AdviceBetterOpeningAdvanced, openingName)}",
            ExplanationLevel.Intermediate when label == "opening_principles" => $"{main} {Localizer.Format(LocalizedStrings.AdviceBetterOpeningIntermediate, openingName)}",
            _ => main
        };
    }

    private static string ComposeDetailedStructure(string what, string why, string better, string watch)
    {
        return string.Join(" ",
            PrefixSection(Localizer.Text(LocalizedStrings.AdviceWhat), what),
            PrefixSection(Localizer.Text(LocalizedStrings.AdviceWhy), why),
            PrefixSection(Localizer.Text(LocalizedStrings.AdviceBetter), better),
            PrefixSection(Localizer.Text(LocalizedStrings.AdviceWatchNextTime), watch));
    }

    private static string PrefixSection(string heading, string text)
    {
        return $"{heading}: {TrimSentence(text)}";
    }

    private static string TrimSentence(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().TrimEnd();
    }

    private static string DisplayLabel(string label)
    {
        return label switch
        {
            "material_loss" => Localizer.Text(LocalizedStrings.AdvicePatternMaterialLoss),
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdvicePatternHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdvicePatternMissedTactic),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdvicePatternOpeningPrinciples),
            "king_safety" => Localizer.Text(LocalizedStrings.AdvicePatternKingSafety),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdvicePatternPieceActivity),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdvicePatternEndgameTechnique),
            "general" => Localizer.Text(LocalizedStrings.AdvicePatternGeneral),
            _ => label.Replace('_', ' ')
        };
    }

    private static string FormatQuality(MoveQualityBucket quality)
    {
        return quality switch
        {
            MoveQualityBucket.Blunder => Localizer.Text(LocalizedStrings.AdviceQualityBlunder),
            MoveQualityBucket.Mistake => Localizer.Text(LocalizedStrings.AdviceQualityMistake),
            MoveQualityBucket.Inaccuracy => Localizer.Text(LocalizedStrings.AdviceQualityInaccuracy),
            _ => Localizer.Text(LocalizedStrings.AdviceQualityGood)
        };
    }

    private static string FormatMoveFromFen(string fenBefore, string? uciMove)
    {
        if (string.IsNullOrWhiteSpace(uciMove))
        {
            return string.Empty;
        }

        ChessGame game = new();
        if (!game.TryLoadFen(fenBefore, out _)
            || !game.TryApplyUci(uciMove, out AppliedMoveInfo? appliedMove, out _)
            || appliedMove is null)
        {
            return uciMove;
        }

        return FormatSanAndUci(appliedMove.San, appliedMove.Uci);
    }

    private static string MergeSentences(string primary, string secondary)
    {
        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        return string.IsNullOrWhiteSpace(primary)
            ? secondary.Trim()
            : $"{primary.Trim()} {secondary.Trim()}";
    }

    private static string FormatSanAndUci(string san, string uci)
    {
        return string.Equals(san, uci, StringComparison.OrdinalIgnoreCase)
            ? san
            : $"{san} ({uci})";
    }
}
