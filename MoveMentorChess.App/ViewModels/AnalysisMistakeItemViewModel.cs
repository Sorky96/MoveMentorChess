using System.Globalization;
using System.Linq;
using System.Text;

namespace MoveMentorChess.App.ViewModels;

public sealed class AnalysisMistakeItemViewModel
{
    public AnalysisMistakeItemViewModel(SelectedMistake mistake)
    {
        Mistake = mistake;

        if (mistake.Moves == null || mistake.Moves.Count == 0)
        {
            LeadMove = null!;
            DisplayText = "No moves";
            Details = "No moves available";
            return;
        }

        LeadMove = mistake.Moves
            .OrderByDescending(move => move.Quality)
            .ThenByDescending(move => move.CentipawnLoss ?? 0)
            .First();

        string moveRange = "n/a";
        if (mistake.Moves != null && mistake.Moves.Count > 0)
        {
            var first = mistake.Moves[0];
            var last = mistake.Moves[mistake.Moves.Count - 1];
            string firstMove = $"{first.Replay.MoveNumber}{(first.Replay.Side == PlayerSide.White ? "." : "...")} {first.Replay.San}";
            string lastMove = $"{last.Replay.MoveNumber}{(last.Replay.Side == PlayerSide.White ? "." : "...")} {last.Replay.San}";
            moveRange = mistake.Moves.Count == 1 ? firstMove : $"{firstMove} -> {lastMove}";
        }

        string label = mistake.Tag?.Label ?? "unclassified";
        string cpl = LeadMove.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
        DisplayText = $"{moveRange} | {mistake.Quality} | {label} | CPL {cpl}";
        Details = BuildDetailsText(mistake, LeadMove);
    }

    public SelectedMistake Mistake { get; }

    public MoveAnalysisResult LeadMove { get; }

    public string DisplayText { get; }

    public string Details { get; }

    private static string BuildDetailsText(SelectedMistake mistake, MoveAnalysisResult lead)
    {
        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Moves: {string.Join(", ", mistake.Moves.Select(m => $"{m.Replay.MoveNumber}{(m.Replay.Side == PlayerSide.White ? "." : "...")} {m.Replay.San}"))}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Quality: {mistake.Quality}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Label: {mistake.Tag?.Label ?? "unclassified"}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Confidence: {(mistake.Tag?.Confidence ?? 0):0.00}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Played move: {lead.Replay.San} ({lead.Replay.Uci})");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Best move: {lead.BeforeAnalysis.BestMoveUci ?? "n/a"}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Eval before: {FormatScore(lead.EvalBeforeCp, lead.BestMateIn)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Eval after: {FormatScore(lead.EvalAfterCp, lead.PlayedMateIn)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Centipawn loss: {lead.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Material delta: {lead.MaterialDeltaCp}");
        builder.AppendLine();
        builder.AppendLine("Advice:");
        builder.AppendLine(lead.Explanation?.ShortText ?? mistake.Explanation.ShortText);

        if (!string.IsNullOrWhiteSpace(lead.Explanation?.DetailedText))
        {
            builder.AppendLine();
            builder.AppendLine("Detailed explanation:");
            builder.AppendLine(lead.Explanation!.DetailedText);
        }

        builder.AppendLine();
        builder.AppendLine("Training hint:");
        builder.AppendLine(lead.Explanation?.TrainingHint ?? mistake.Explanation.TrainingHint);

        if (mistake.Tag?.Evidence.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Evidence:");
            foreach (string evidence in mistake.Tag.Evidence)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {evidence}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatScore(int? centipawns, int? mateIn)
    {
        if (mateIn is int mate)
        {
            return $"mate {mate}";
        }

        return centipawns is int cp ? $"{cp} cp" : "n/a";
    }
}
