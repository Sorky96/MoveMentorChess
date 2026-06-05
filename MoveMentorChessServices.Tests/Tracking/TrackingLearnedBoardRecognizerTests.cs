using System.Drawing;
using System.IO;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingLearnedBoardRecognizerTests
{
    private const string StartPlacement = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
    private const string E4Placement = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR";

    [Fact]
    public void TryRecognize_ReturnsFalseWithoutTemplates()
    {
        TrackingTemplateBank templates = new();
        TrackingLearnedBoardRecognizer recognizer = CreateRecognizer(templates);
        using Bitmap board = RenderBoard(StartPlacement, whiteAtBottom: true);

        bool recognized = recognizer.TryRecognize(board, whiteAtBottom: true, out string placementFen, out double confidence);

        Assert.False(recognized);
        Assert.Equal(string.Empty, placementFen);
        Assert.Equal(0, confidence);
    }

    [Fact]
    public void TryRecognize_ReturnsTrainedPlacement()
    {
        TrackingTemplateBank templates = new();
        DefaultTrackingTemplateVectorizer vectorizer = new();
        DefaultBoardImageNormalizer normalizer = new();
        BoardRecognitionOptions options = BoardRecognitionOptions.Default;
        TrackingSquareClassifier classifier = CreateClassifier(vectorizer, options, templates);
        TrackingLearnedTemplateTrainer trainer = new(vectorizer, normalizer, options, templates);
        TrackingLearnedBoardRecognizer recognizer = new(vectorizer, normalizer, classifier, options, templates);
        using Bitmap seedBoard = RenderBoard(StartPlacement, whiteAtBottom: true);
        using Bitmap trackedBoard = RenderBoard(E4Placement, whiteAtBottom: true);

        trainer.LearnFromBoard(seedBoard, StartPlacement, whiteAtBottom: true);
        bool recognized = recognizer.TryRecognize(trackedBoard, whiteAtBottom: true, out string placementFen, out double confidence);

        Assert.True(recognized);
        Assert.Equal(E4Placement, placementFen);
        Assert.True(confidence > 0.6);
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            LearnedRecognitionMinConfidence = 1.5
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateRecognizer(options: invalidOptions));
    }

    private static TrackingLearnedBoardRecognizer CreateRecognizer(
        TrackingTemplateBank? templates = null,
        BoardRecognitionOptions? options = null)
    {
        DefaultTrackingTemplateVectorizer vectorizer = new();
        BoardRecognitionOptions recognitionOptions = options ?? BoardRecognitionOptions.Default;
        TrackingTemplateBank learnedTemplates = templates ?? new TrackingTemplateBank();
        return new TrackingLearnedBoardRecognizer(
            vectorizer,
            new DefaultBoardImageNormalizer(),
            CreateClassifier(vectorizer, recognitionOptions, learnedTemplates),
            recognitionOptions,
            learnedTemplates);
    }

    private static TrackingSquareClassifier CreateClassifier(
        ITrackingTemplateVectorizer vectorizer,
        BoardRecognitionOptions options,
        TrackingTemplateBank templates)
    {
        return new TrackingSquareClassifier(
            vectorizer,
            options,
            templates,
            new TrackingTemplateBank(),
            new TrackingTemplateBank(),
            new TrackingTemplateBank());
    }

    private static Bitmap RenderBoard(string placementFen, bool whiteAtBottom)
    {
        const int tileSize = 48;
        Bitmap bitmap = new(tileSize * 8, tileSize * 8);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);

        Assert.True(FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _));
        Assert.NotNull(position);

        for (int boardY = 0; boardY < 8; boardY++)
        {
            for (int boardX = 0; boardX < 8; boardX++)
            {
                int screenX = whiteAtBottom ? boardX : 7 - boardX;
                int screenY = whiteAtBottom ? boardY : 7 - boardY;
                Rectangle rect = new(screenX * tileSize, screenY * tileSize, tileSize, tileSize);
                bool lightSquare = (boardX + boardY) % 2 == 0;

                using SolidBrush squareBrush = new(lightSquare ? TrackingBoardPalette.LightSquare : TrackingBoardPalette.DarkSquare);
                graphics.FillRectangle(squareBrush, rect);

                string? piece = position!.Board[boardX, boardY];
                if (!string.IsNullOrEmpty(piece))
                {
                    using Image pieceImage = Image.FromFile(Path.Combine(GetImagesDirectory(), GetPieceFileName(piece)));
                    graphics.DrawImage(pieceImage, rect);
                }
            }
        }

        return bitmap;
    }

    private static string GetImagesDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "MoveMentorChessServices", "Images");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(current.FullName, "Images");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "wK.png")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Images directory for chess piece fixtures.");
    }

    private static string GetPieceFileName(string piece)
    {
        return piece switch
        {
            "K" => "wK.png",
            "Q" => "wQ.png",
            "R" => "wR.png",
            "B" => "wB.png",
            "N" => "wN.png",
            "P" => "wP.png",
            "k" => "bK.png",
            "q" => "bQ.png",
            "r" => "bR.png",
            "b" => "bB.png",
            "n" => "bN.png",
            "p" => "bP.png",
            _ => throw new InvalidOperationException($"Unsupported piece '{piece}'.")
        };
    }
}
