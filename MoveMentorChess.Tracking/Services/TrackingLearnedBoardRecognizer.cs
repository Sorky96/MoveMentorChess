using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class TrackingLearnedBoardRecognizer
{
    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly IBoardImageNormalizer boardImageNormalizer;
    private readonly TrackingSquareClassifier squareClassifier;
    private readonly BoardRecognitionOptions options;
    private readonly TrackingTemplateBank templates;

    public TrackingLearnedBoardRecognizer(
        ITrackingTemplateVectorizer templateVectorizer,
        IBoardImageNormalizer boardImageNormalizer,
        TrackingSquareClassifier squareClassifier,
        BoardRecognitionOptions options,
        TrackingTemplateBank templates)
    {
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.boardImageNormalizer = boardImageNormalizer ?? throw new ArgumentNullException(nameof(boardImageNormalizer));
        this.squareClassifier = squareClassifier ?? throw new ArgumentNullException(nameof(squareClassifier));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    public bool TryRecognize(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        placementFen = string.Empty;
        confidence = 0;

        if (templates.Count == 0)
        {
            return false;
        }

        using Bitmap normalizedBoardImage = boardImageNormalizer.Normalize(boardImage);
        string?[,] board = new string?[8, 8];
        double confidenceSum = 0;

        for (int screenY = 0; screenY < 8; screenY++)
        {
            for (int screenX = 0; screenX < 8; screenX++)
            {
                Point boardSquare = MapScreenSquareToBoard(screenX, screenY, whiteAtBottom);
                bool isLightSquare = IsLightSquare(boardSquare);

                using Bitmap square = boardImageNormalizer.ExtractSquare(normalizedBoardImage, screenX, screenY);
                _ = templateVectorizer.ToMaskVector(square, out double occupancy, out double centralOccupancy, out _, out _);
                if (!squareClassifier.TryClassifyLearnedSquare(templateVectorizer.ToVector(square), isLightSquare, out string? piece, out double squareConfidence))
                {
                    return false;
                }

                if ((string.Equals(piece, "P", StringComparison.Ordinal) || string.Equals(piece, "p", StringComparison.Ordinal))
                    && centralOccupancy < options.MissingPawnCentralOccupancyMax
                    && occupancy < options.MissingPawnOccupancyMax)
                {
                    piece = null;
                    squareConfidence = Math.Max(
                        squareConfidence,
                        Math.Clamp(
                            1.0
                                - (occupancy * options.MissingPawnOccupancyPenaltyWeight)
                                - (centralOccupancy * options.MissingPawnCentralOccupancyPenaltyWeight),
                            0.0,
                            1.0));
                }

                board[boardSquare.X, boardSquare.Y] = piece;
                confidenceSum += squareConfidence;
            }
        }

        confidence = confidenceSum / 64.0;
        if (confidence < options.LearnedRecognitionMinConfidence)
        {
            return false;
        }

        placementFen = FenPosition.FromBoardState(
            board,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            null,
            0,
            1).GetPlacementFen();
        return true;
    }

    private static Point MapScreenSquareToBoard(int screenX, int screenY, bool whiteAtBottom)
    {
        return whiteAtBottom
            ? new Point(screenX, screenY)
            : new Point(7 - screenX, 7 - screenY);
    }

    private static bool IsLightSquare(Point boardSquare) => (boardSquare.X + boardSquare.Y) % 2 == 0;
}
