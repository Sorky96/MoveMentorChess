using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingLearnedTemplateTrainerTests
{
    [Fact]
    public void LearnFromBoard_AddsTemplatesForPlacementSquares()
    {
        TrackingTemplateBank templates = new();
        TrackingLearnedTemplateTrainer trainer = CreateTrainer(templates);
        using Bitmap board = CreateBoard();

        trainer.LearnFromBoard(board, "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR", whiteAtBottom: true);

        Assert.Contains(templates.Enumerate(), entry => entry.Key == "R|L");
        Assert.Contains(templates.Enumerate(), entry => entry.Key == ".|D");
    }

    [Fact]
    public void LearnFromFen_UsesPlacementPart()
    {
        TrackingTemplateBank templates = new();
        TrackingLearnedTemplateTrainer trainer = CreateTrainer(templates);
        using Bitmap board = CreateBoard();

        trainer.LearnFromFen(board, "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1", whiteAtBottom: true);

        Assert.Contains(templates.Enumerate(), entry => entry.Key == "r|L");
    }

    [Fact]
    public void LearnFromBoard_IgnoresInvalidPlacement()
    {
        TrackingTemplateBank templates = new();
        TrackingLearnedTemplateTrainer trainer = CreateTrainer(templates);
        using Bitmap board = CreateBoard();

        trainer.LearnFromBoard(board, "not-a-placement", whiteAtBottom: true);

        Assert.Empty(templates.Enumerate());
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            MaxLearnedPieceTemplateVariants = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTrainer(options: invalidOptions));
    }

    private static TrackingLearnedTemplateTrainer CreateTrainer(
        TrackingTemplateBank? templates = null,
        BoardRecognitionOptions? options = null)
    {
        return new TrackingLearnedTemplateTrainer(
            new DefaultTrackingTemplateVectorizer(),
            new DefaultBoardImageNormalizer(),
            options ?? BoardRecognitionOptions.Default,
            templates ?? new TrackingTemplateBank());
    }

    private static Bitmap CreateBoard()
    {
        Bitmap bitmap = new(128, 128);
        using Graphics graphics = Graphics.FromImage(bitmap);
        using Brush lightSquareBrush = new SolidBrush(TrackingBoardPalette.LightSquare);
        using Brush darkSquareBrush = new SolidBrush(TrackingBoardPalette.DarkSquare);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Rectangle rect = new(x * 16, y * 16, 16, 16);
                graphics.FillRectangle(((x + y) % 2 == 0) ? lightSquareBrush : darkSquareBrush, rect);
            }
        }

        return bitmap;
    }
}
