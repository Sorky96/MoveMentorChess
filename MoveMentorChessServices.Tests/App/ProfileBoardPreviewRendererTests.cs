using Avalonia.Media;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class ProfileBoardPreviewRendererTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void BuildPreviewArrows_CreatesArrowsFromUciAndSanWithEmbeddedUci()
    {
        List<BoardArrowViewModel> arrows = ProfileBoardPreviewRenderer.BuildPreviewArrows(
            StartFen,
            ("e2e4", Color.Parse("#F6C453")),
            ("Nf3 (g1f3)", Color.Parse("#58D68D")),
            ("not-a-move", Color.Parse("#FFFFFF")));

        Assert.Equal(2, arrows.Count);
        Assert.Equal(new BoardArrowViewModel("e2", "e4", Color.Parse("#F6C453")), arrows[0]);
        Assert.Equal(new BoardArrowViewModel("g1", "f3", Color.Parse("#58D68D")), arrows[1]);
    }

    [Fact]
    public void BuildPreviewArrows_ReturnsNoArrowsForInvalidFenOrBlankMoveText()
    {
        List<BoardArrowViewModel> invalidFenArrows = ProfileBoardPreviewRenderer.BuildPreviewArrows(
            "not a fen",
            ("e2e4", Color.Parse("#F6C453")));
        List<BoardArrowViewModel> blankMoveArrows = ProfileBoardPreviewRenderer.BuildPreviewArrows(
            StartFen,
            (null, Color.Parse("#F6C453")),
            ("", Color.Parse("#58D68D")));

        Assert.Empty(invalidFenArrows);
        Assert.Empty(blankMoveArrows);
    }

    [Fact]
    public void BuildRecommendationPreviewDetailLines_FormatsMoveAndTheme()
    {
        OpeningMoveRecommendation recommendation = new(
            "game-1",
            PlayerSide.White,
            "C20",
            5,
            3,
            "Qh5",
            "Nf3 (g1f3)",
            "hanging_piece",
            180,
            StartFen);

        IReadOnlyList<string> lines = ProfileBoardPreviewRenderer.BuildRecommendationPreviewDetailLines(recommendation);

        Assert.Equal(
            [
                "3. Qh5",
                "Your move: Qh5",
                "Suggested move: Nf3 (g1f3)",
                "Theme: Loose pieces | CPL 180"
            ],
            lines);
    }
}
