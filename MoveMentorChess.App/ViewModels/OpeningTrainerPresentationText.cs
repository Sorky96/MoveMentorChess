using System.Text.RegularExpressions;

namespace MoveMentorChess.App.ViewModels;

public static class OpeningTrainerPresentationText
{
    public static string BuildWrongMoveFeedback(
        OpeningTrainingPosition position,
        OpeningTrainingMoveOption? preferred)
    {
        OpeningMoveIdea? idea = preferred?.Idea;
        if (idea?.IdeaTags.Contains(OpeningMoveIdeaTag.ControlCenter) == true)
        {
            return "Not this time. Think about the move that supports central control.";
        }

        if (idea?.IdeaTags.Contains(OpeningMoveIdeaTag.DevelopPiece) == true)
        {
            return "Not this time. Think about the move that improves piece activity.";
        }

        if (idea?.IdeaTags.Contains(OpeningMoveIdeaTag.KingSafety) == true)
        {
            return "Not this time. Think about the move that keeps your king and pieces coordinated.";
        }

        if (idea?.IdeaTags.Contains(OpeningMoveIdeaTag.TacticalResource) == true)
        {
            return "Not this time. Check for a forcing resource before moving quietly.";
        }

        return position.Mode switch
        {
            OpeningTrainingMode.BranchAwareness => "Not this time. Think about the opponent reply this branch is testing.",
            OpeningTrainingMode.MistakeRepair => "Not this time. Think about what the old mistake left unresolved.",
            _ => "Not this time. Use the plan hint before trying again."
        };
    }

    public static string FormatOpponentSummary(string summary)
    {
        Match match = Regex.Match(summary, @"Tracked (?<count>\d+) opponent branch\(es\)", RegexOptions.IgnoreCase);
        return match.Success
            ? $"We track {match.Groups["count"].Value} common opponent replies for this opening."
            : summary;
    }

    public static string FormatMainLine(IReadOnlyList<OpeningLineMove> moves, int maxPly)
    {
        string line = string.Join(" ", moves
            .Take(maxPly)
            .Select(move => move.Side == PlayerSide.White
                ? $"{move.MoveNumber}.{move.San}"
                : move.San));
        return string.IsNullOrWhiteSpace(line) ? "the selected opening" : line;
    }

    public static string FormatMoveLabel(OpeningLineMove move)
    {
        string tag = move.Idea?.IdeaTags.FirstOrDefault() switch
        {
            OpeningMoveIdeaTag.ControlCenter => "center",
            OpeningMoveIdeaTag.DevelopPiece => "development",
            OpeningMoveIdeaTag.KingSafety => "king safety",
            OpeningMoveIdeaTag.TacticalResource => "tactic",
            _ => string.Empty
        };
        string suffix = string.IsNullOrWhiteSpace(tag) ? string.Empty : $"  {tag}";
        return move.Side == PlayerSide.White
            ? $"{move.MoveNumber}. {move.San}{suffix}"
            : $"{move.San}{suffix}";
    }

    public static string FormatBranchFrequencyLabel(
        OpeningTrainingBranch branch,
        IReadOnlyList<OpeningTrainingBranch> branches)
    {
        if (branches.Count == 0)
        {
            return "reply";
        }

        int rank = branches
            .OrderByDescending(item => item.Frequency)
            .ThenBy(item => item.OpponentMove, StringComparer.OrdinalIgnoreCase)
            .Select((item, index) => new { item, index })
            .FirstOrDefault(pair => ReferenceEquals(pair.item, branch))?.index ?? 0;

        return rank switch
        {
            0 => "most common reply",
            1 or 2 => "common",
            _ => "less common"
        };
    }

    public static string PluralSuffix(int count)
        => count == 1 ? string.Empty : "s";

    public static string FormatPositionCount(int count, string singularVerb, string pluralVerb)
        => count == 1 ? $"1 position {singularVerb}" : $"{count} positions {pluralVerb}";
}
