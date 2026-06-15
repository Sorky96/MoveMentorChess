using MoveMentorChess.Engine;
using MoveMentorChess.Localization;

namespace MoveMentorChess.Presentation.Models;

public enum AnalysisSnapshotMode
{
    Played,
    Best,
    Threat
}

public sealed record AnalysisSnapshotArrow(string FromSquare, string ToSquare, string ColorHex);

public static class AnalysisSnapshotPresentation
{
    public static string BuildPositionContextText(MoveAnalysisResult lead, string label)
        => AnalysisSnapshotTextFormatter.BuildPositionContextText(lead, label);

    public static string BuildThreatText(string label)
        => AnalysisSnapshotTextFormatter.BuildThreatText(label);

    public static string BuildSnapshotThreatText(MoveAnalysisResult lead, string label, AnalysisSnapshotMode mode)
    {
        if (mode == AnalysisSnapshotMode.Best)
        {
            return Localizer.Text(LocalizedStrings.AnalysisWindowBestMoveViewDescription);
        }

        if (mode == AnalysisSnapshotMode.Threat)
        {
            EngineLine? threatLine = lead.AfterAnalysis.Lines.Count > 0 ? lead.AfterAnalysis.Lines[0] : null;
            string threatMove = AnalysisDetailsTextFormatter.FormatMoveFromFen(lead.Replay.FenAfter, threatLine?.MoveUci);
            return threatLine is null
                ? BuildThreatText(label)
                : Localizer.Format(LocalizedStrings.AnalysisWindowOpponentKeyReply, threatMove);
        }

        return BuildThreatText(label);
    }

    public static string BuildMissedIdeaText(MoveAnalysisResult lead)
        => AnalysisSnapshotTextFormatter.BuildMissedIdeaText(lead);

    public static string BuildPositionSnapshotText(MoveAnalysisResult lead, string label)
    {
        string material = lead.MaterialDeltaCp == 0
            ? Localizer.Text(LocalizedStrings.AnalysisWindowMaterialBalanced)
            : Localizer.Format(
                LocalizedStrings.AnalysisWindowMaterialValue,
                AnalysisCoachingTextFormatter.FormatSignedPawns(lead.MaterialDeltaCp));
        string kingSquare = PositionInspector.GetKingSquare(lead.Replay.FenAfter, lead.Replay.Side)
            ?? Localizer.Text(LocalizedStrings.CommonUnknown);

        return Localizer.Format(LocalizedStrings.AnalysisWindowPositionSnapshotText, material, kingSquare, BuildThreatText(label));
    }

    public static IReadOnlyList<AnalysisSnapshotArrow> BuildSnapshotArrows(MoveAnalysisResult lead, AnalysisSnapshotMode mode)
    {
        List<AnalysisSnapshotArrow> arrows = [];

        if (mode == AnalysisSnapshotMode.Played)
        {
            arrows.Add(new AnalysisSnapshotArrow(lead.Replay.FromSquare, lead.Replay.ToSquare, "#D9822B"));
        }

        if (mode is AnalysisSnapshotMode.Played or AnalysisSnapshotMode.Best
            && TryBuildMoveArrow(lead.Replay.FenBefore, lead.BeforeAnalysis.BestMoveUci, "#56C271", out AnalysisSnapshotArrow bestArrow))
        {
            arrows.Add(bestArrow);
        }

        EngineLine? threatLine = lead.AfterAnalysis.Lines.Count > 0 ? lead.AfterAnalysis.Lines[0] : null;
        if (mode == AnalysisSnapshotMode.Threat
            && TryBuildMoveArrow(lead.Replay.FenAfter, threatLine?.MoveUci, "#D84A4A", out AnalysisSnapshotArrow threatArrow))
        {
            arrows.Add(threatArrow);
        }

        return arrows;
    }

    public static string BuildBestMoveIdeaText(MoveAnalysisResult lead)
    {
        string bestMove = AnalysisDetailsTextFormatter.FormatMoveFromFen(lead.Replay.FenBefore, lead.BeforeAnalysis.BestMoveUci);
        EngineLine? bestLine = lead.BeforeAnalysis.Lines.Count > 0 ? lead.BeforeAnalysis.Lines[0] : null;
        string note = bestLine is null
            ? Localizer.Text(LocalizedStrings.AnalysisWindowKeepsCleanerPosition)
            : AnalysisCoachingTextFormatter.BuildCandidateCoachNote(lead, bestLine, isBest: true);
        return $"{bestMove}: {note}.";
    }

    public static string BuildPlayerMistakeText(MoveAnalysisResult lead, string label)
    {
        if (lead.PlayedMateIn is < 0)
        {
            return Localizer.Format(
                LocalizedStrings.AnalysisWindowMoveAllowedForcedMate,
                AnalysisDetailsTextFormatter.FormatSanAndUci(lead.Replay.San, lead.Replay.Uci));
        }

        return label switch
        {
            "material_loss" => Localizer.Format(
                LocalizedStrings.AnalysisWindowMoveLeftMaterialVulnerable,
                AnalysisDetailsTextFormatter.FormatSanAndUci(lead.Replay.San, lead.Replay.Uci)),
            "hanging_piece" => Localizer.Format(
                LocalizedStrings.AnalysisWindowMoveLeftPieceLoose,
                AnalysisDetailsTextFormatter.FormatSanAndUci(lead.Replay.San, lead.Replay.Uci)),
            _ => Localizer.Format(
                LocalizedStrings.AnalysisWindowMoveCreatedProblem,
                AnalysisDetailsTextFormatter.FormatSanAndUci(lead.Replay.San, lead.Replay.Uci),
                AnalysisMistakePresentation.FormatMistakeLabel(label))
        };
    }

    public static (string Text, string Brush) BuildMovedPieceSafetyBadge(MoveAnalysisResult lead)
    {
        PositionInspector.SquareSafetySummary? safety = PositionInspector.AnalyzeSquareSafety(
            lead.Replay.FenAfter,
            lead.Replay.ToSquare,
            lead.Replay.Side);

        if (safety is null)
        {
            return (Localizer.Text(LocalizedStrings.AnalysisWindowMovedPieceStatusUnknown), "#657386");
        }

        if (safety.Value.IsHanging || safety.Value.IsFreeToTake)
        {
            return (Localizer.Text(LocalizedStrings.AnalysisWindowMovedPieceHanging), "#B93838");
        }

        if (safety.Value.LikelyLosesExchange || safety.Value.Attackers > safety.Value.Defenders)
        {
            return (Localizer.Text(LocalizedStrings.AnalysisWindowMovedPieceUnderPressure), "#D9822B");
        }

        return (Localizer.Text(LocalizedStrings.AnalysisWindowMovedPieceSafe), "#1F7A55");
    }

    public static string BuildBeforeMoveChecklistText(string label)
        => AnalysisSnapshotTextFormatter.BuildBeforeMoveChecklistText(label);

    private static bool TryBuildMoveArrow(string fenBefore, string? uciMove, string colorHex, out AnalysisSnapshotArrow arrow)
    {
        arrow = new AnalysisSnapshotArrow("a1", "a1", colorHex);
        if (string.IsNullOrWhiteSpace(uciMove))
        {
            return false;
        }

        ChessGame game = new();
        if (!game.TryLoadFen(fenBefore, out _)
            || !game.TryApplyUci(uciMove, out AppliedMoveInfo? appliedMove, out _)
            || appliedMove is null)
        {
            return false;
        }

        arrow = new AnalysisSnapshotArrow(appliedMove.FromSquare, appliedMove.ToSquare, colorHex);
        return true;
    }
}
