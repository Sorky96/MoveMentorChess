using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class TrackingLearnedTemplateTrainer
{
    private const string EmptyKey = ".";

    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly IBoardImageNormalizer boardImageNormalizer;
    private readonly BoardRecognitionOptions options;
    private readonly TrackingTemplateBank templates;

    public TrackingLearnedTemplateTrainer(
        ITrackingTemplateVectorizer templateVectorizer,
        IBoardImageNormalizer boardImageNormalizer,
        BoardRecognitionOptions options,
        TrackingTemplateBank templates)
    {
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.boardImageNormalizer = boardImageNormalizer ?? throw new ArgumentNullException(nameof(boardImageNormalizer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    public void LearnFromBoard(Bitmap boardImage, string placementFen, bool whiteAtBottom)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        if (!FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _)
            || position is null)
        {
            return;
        }

        using Bitmap normalizedBoardImage = boardImageNormalizer.Normalize(boardImage);

        for (int screenY = 0; screenY < 8; screenY++)
        {
            for (int screenX = 0; screenX < 8; screenX++)
            {
                Point boardSquare = MapScreenSquareToBoard(screenX, screenY, whiteAtBottom);
                string? piece = position.Board[boardSquare.X, boardSquare.Y];
                string templateKey = BuildTemplateKey(piece, IsLightSquare(boardSquare));

                using Bitmap square = boardImageNormalizer.ExtractSquare(normalizedBoardImage, screenX, screenY);
                AddTemplate(templateKey, templateVectorizer.ToVector(square));
            }
        }
    }

    public void LearnFromFen(Bitmap boardImage, string fen, bool whiteAtBottom)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        if (!FenPosition.TryParse(fen, out FenPosition? position, out _)
            || position is null)
        {
            return;
        }

        LearnFromBoard(boardImage, position.GetPlacementFen(), whiteAtBottom);
    }

    private void AddTemplate(string key, float[] vector)
    {
        int maxVariants = key.StartsWith(EmptyKey, StringComparison.Ordinal)
            ? options.MaxEmptyTemplateVariants
            : options.MaxLearnedPieceTemplateVariants;
        templates.Add(key, vector, maxVariants);
    }

    private static Point MapScreenSquareToBoard(int screenX, int screenY, bool whiteAtBottom)
    {
        return whiteAtBottom
            ? new Point(screenX, screenY)
            : new Point(7 - screenX, 7 - screenY);
    }

    private static bool IsLightSquare(Point boardSquare) => (boardSquare.X + boardSquare.Y) % 2 == 0;

    private static string BuildTemplateKey(string? piece, bool isLightSquare)
    {
        string symbol = string.IsNullOrEmpty(piece) ? EmptyKey : piece;
        return $"{symbol}|{(isLightSquare ? 'L' : 'D')}";
    }
}
