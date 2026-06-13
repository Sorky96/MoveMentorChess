using System.Globalization;
using MoveMentorChess.Localization;

namespace MoveMentorChess.Profiles;

public static class PlayerProfileTextFormatter
{
    public static string FormatMistakeLabel(string label)
    {
        return label switch
        {
            "hanging_piece" => Localizer.Text(LocalizedStrings.AdvicePatternHangingPiece),
            "missed_tactic" => Localizer.Text(LocalizedStrings.AdvicePatternMissedTactic),
            "opening_principles" => Localizer.Text(LocalizedStrings.AdvicePatternOpeningPrinciples),
            "king_safety" => Localizer.Text(LocalizedStrings.AdvicePatternKingSafety),
            "endgame_technique" => Localizer.Text(LocalizedStrings.AdvicePatternEndgameTechnique),
            "material_loss" => Localizer.Text(LocalizedStrings.AdvicePatternMaterialLoss),
            "piece_activity" => Localizer.Text(LocalizedStrings.AdvicePatternPieceActivity),
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((label ?? string.Empty).Replace('_', ' ').ToLowerInvariant())
        };
    }

    public static string FormatPhase(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Opening => Localizer.Text(LocalizedStrings.FormatPhaseOpening),
            GamePhase.Middlegame => Localizer.Text(LocalizedStrings.FormatPhaseMiddlegame),
            GamePhase.Endgame => Localizer.Text(LocalizedStrings.FormatPhaseEndgame),
            _ => phase.ToString()
        };
    }

    public static string FormatOpening(string eco)
    {
        string description = OpeningCatalog.Describe(eco);
        return string.IsNullOrWhiteSpace(description)
            ? Localizer.Text(LocalizedStrings.FormatMixedOpenings)
            : description;
    }

    public static string FormatTrendHeadline(ProfileProgressDirection direction)
    {
        return direction switch
        {
            ProfileProgressDirection.Improving => Localizer.Text(LocalizedStrings.TrendImproving),
            ProfileProgressDirection.Stable => Localizer.Text(LocalizedStrings.TrendStable),
            ProfileProgressDirection.Regressing => Localizer.Text(LocalizedStrings.TrendRegressing),
            _ => Localizer.Text(LocalizedStrings.TrendNeedMoreGames)
        };
    }

    public static string FormatTimes(int count)
    {
        return Localizer.Plural(
            count,
            LocalizedStrings.CountOneTime,
            LocalizedStrings.CountFewTimes,
            LocalizedStrings.CountManyTimes);
    }

    public static string FormatMistakeCount(int count)
    {
        return Localizer.Plural(
            count,
            LocalizedStrings.CountOneMistake,
            LocalizedStrings.CountFewMistakes,
            LocalizedStrings.CountManyMistakes);
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

    public static string FormatTrainingBlockKind(TrainingBlockKind kind)
    {
        return kind switch
        {
            TrainingBlockKind.Tactics => Localizer.Text(LocalizedStrings.TrainingBlockTactics),
            TrainingBlockKind.OpeningReview => Localizer.Text(LocalizedStrings.TrainingBlockOpeningReview),
            TrainingBlockKind.EndgameDrill => Localizer.Text(LocalizedStrings.TrainingBlockEndgameDrill),
            TrainingBlockKind.GameReview => Localizer.Text(LocalizedStrings.TrainingBlockGameReview),
            TrainingBlockKind.SlowPlayFocus => Localizer.Text(LocalizedStrings.TrainingBlockSlowPlayFocus),
            _ => kind.ToString()
        };
    }

    public static string FormatTrainingBlockPurpose(TrainingBlockPurpose purpose)
    {
        return purpose switch
        {
            TrainingBlockPurpose.Repair => Localizer.Text(LocalizedStrings.TrainingPurposeRepair),
            TrainingBlockPurpose.Maintain => Localizer.Text(LocalizedStrings.TrainingPurposeMaintain),
            TrainingBlockPurpose.Checklist => Localizer.Text(LocalizedStrings.TrainingPurposeChecklist),
            _ => purpose.ToString()
        };
    }

    public static string TrimSentence(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().TrimEnd('.', ';', ':', '!');
    }
}
