using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class OpeningTrainingPositionSelectorTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void Select_FiltersByTargetOpeningAndMode_AndKeepsOnlyUsedLines()
    {
        OpeningTrainingPosition selected = CreatePosition(
            "selected",
            "line-selected",
            OpeningTrainingMode.LineRecall,
            OpeningTrainingSourceKind.OpeningWeakness,
            "B01",
            "Scandinavian Defense",
            80);
        OpeningTrainingPosition wrongEco = CreatePosition(
            "wrong-eco",
            "line-wrong-eco",
            OpeningTrainingMode.LineRecall,
            OpeningTrainingSourceKind.ExampleGame,
            "C20",
            "King's Pawn Game",
            120);
        OpeningTrainingPosition wrongMode = CreatePosition(
            "wrong-mode",
            "line-wrong-mode",
            OpeningTrainingMode.BranchAwareness,
            OpeningTrainingSourceKind.OpeningWeakness,
            "B01",
            "Scandinavian Defense",
            140);
        Dictionary<string, OpeningTrainingLine> lines = new(StringComparer.Ordinal)
        {
            ["line-selected"] = CreateLine("line-selected", selected),
            ["line-wrong-eco"] = CreateLine("line-wrong-eco", wrongEco),
            ["line-wrong-mode"] = CreateLine("line-wrong-mode", wrongMode)
        };
        OpeningTrainingSessionOptions options = new(
            Modes: [OpeningTrainingMode.LineRecall],
            MaxPositions: 5,
            TargetOpenings: ["b01"]);

        OpeningTrainingPositionSelection result = new OpeningTrainingPositionSelector()
            .Select([wrongEco, wrongMode, selected], lines, options);

        OpeningTrainingPosition resultPosition = Assert.Single(result.Positions);
        Assert.Equal("selected", resultPosition.PositionId);
        OpeningTrainingLine resultLine = Assert.Single(result.Lines);
        Assert.Equal("line-selected", resultLine.LineId);
        OpeningTrainingSourceSummary summary = Assert.Single(result.SourceSummaries);
        Assert.Equal(OpeningTrainingSourceKind.OpeningWeakness, summary.SourceKind);
        Assert.Equal(1, summary.PositionCount);
        Assert.Equal(1, summary.LineCount);
        Assert.Equal(["B01"], summary.RelatedOpenings);
    }

    [Fact]
    public void Select_OrdersByPriorityThenPlyThenOpeningNameBeforeLimit()
    {
        OpeningTrainingPosition laterPly = CreatePosition("later", "line-later", OpeningTrainingMode.LineRecall, OpeningTrainingSourceKind.ExampleGame, "C20", "Beta", 100, ply: 6);
        OpeningTrainingPosition earlierPly = CreatePosition("earlier", "line-earlier", OpeningTrainingMode.LineRecall, OpeningTrainingSourceKind.ExampleGame, "C20", "Zeta", 100, ply: 3);
        OpeningTrainingPosition lowerPriority = CreatePosition("lower", "line-lower", OpeningTrainingMode.LineRecall, OpeningTrainingSourceKind.ExampleGame, "C20", "Alpha", 50, ply: 1);
        Dictionary<string, OpeningTrainingLine> lines = new(StringComparer.Ordinal)
        {
            ["line-later"] = CreateLine("line-later", laterPly),
            ["line-earlier"] = CreateLine("line-earlier", earlierPly),
            ["line-lower"] = CreateLine("line-lower", lowerPriority)
        };
        OpeningTrainingSessionOptions options = new(
            Modes: [OpeningTrainingMode.LineRecall],
            MaxPositions: 2);

        OpeningTrainingPositionSelection result = new OpeningTrainingPositionSelector()
            .Select([lowerPriority, laterPly, earlierPly], lines, options);

        Assert.Equal(["earlier", "later"], result.Positions.Select(position => position.PositionId).ToList());
        Assert.Equal(["line-later", "line-earlier"], result.Lines.Select(line => line.LineId).ToList());
    }

    private static OpeningTrainingPosition CreatePosition(
        string positionId,
        string lineId,
        OpeningTrainingMode mode,
        OpeningTrainingSourceKind sourceKind,
        string eco,
        string openingName,
        int priority,
        int ply = 1)
    {
        return new OpeningTrainingPosition(
            positionId,
            mode,
            sourceKind,
            eco,
            openingName,
            StartFen,
            ply,
            1,
            PlayerSide.White,
            "Prompt",
            "Instruction",
            priority,
            null,
            null,
            null,
            null,
            [],
            [],
            [],
            new OpeningTrainingReference("game", PlayerSide.White, "Opponent", null, null, sourceKind.ToString(), null, null),
            lineId);
    }

    private static OpeningTrainingLine CreateLine(string lineId, OpeningTrainingPosition position)
    {
        return new OpeningTrainingLine(
            lineId,
            position.SourceKind,
            position.Eco,
            position.OpeningName,
            position.Fen,
            position.Ply,
            position.MoveNumber,
            position.SideToMove,
            "Anchor",
            [],
            position.Reference);
    }
}
