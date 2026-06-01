using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class TrackingColdStartBoardRecognizer
{
    private readonly IBoardImageNormalizer boardImageNormalizer;
    private readonly TrackingSquareClassifier squareClassifier;
    private readonly BoardRecognitionOptions options;
    private readonly TrackingTemplateBank genericShapeTemplates;
    private readonly TrackingTemplateBank genericPieceTemplates;

    public TrackingColdStartBoardRecognizer(
        IBoardImageNormalizer boardImageNormalizer,
        TrackingSquareClassifier squareClassifier,
        BoardRecognitionOptions options,
        TrackingTemplateBank genericShapeTemplates,
        TrackingTemplateBank genericPieceTemplates)
    {
        this.boardImageNormalizer = boardImageNormalizer ?? throw new ArgumentNullException(nameof(boardImageNormalizer));
        this.squareClassifier = squareClassifier ?? throw new ArgumentNullException(nameof(squareClassifier));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.genericShapeTemplates = genericShapeTemplates ?? throw new ArgumentNullException(nameof(genericShapeTemplates));
        this.genericPieceTemplates = genericPieceTemplates ?? throw new ArgumentNullException(nameof(genericPieceTemplates));
    }

    public bool TryRecognize(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        using Bitmap normalizedBoardImage = boardImageNormalizer.Normalize(boardImage);
        return TryRecognizeNormalized(normalizedBoardImage, whiteAtBottom, out placementFen, out confidence);
    }

    public bool TryRecognizeNormalized(Bitmap normalizedBoardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(normalizedBoardImage);

        placementFen = string.Empty;
        confidence = 0;

        if (genericShapeTemplates.Count == 0 || genericPieceTemplates.Count == 0)
        {
            return false;
        }

        string?[,] board = new string?[8, 8];
        double confidenceSum = 0;

        for (int screenY = 0; screenY < 8; screenY++)
        {
            for (int screenX = 0; screenX < 8; screenX++)
            {
                Point boardSquare = TrackingBoardSquareMapper.MapScreenSquareToBoard(screenX, screenY, whiteAtBottom);
                bool isLightSquare = TrackingBoardSquareMapper.IsLightSquare(boardSquare);

                using Bitmap square = boardImageNormalizer.ExtractSquare(normalizedBoardImage, screenX, screenY);
                if (!squareClassifier.TryClassifyColdStartSquare(square, isLightSquare, out string? piece, out double squareConfidence))
                {
                    return false;
                }

                board[boardSquare.X, boardSquare.Y] = piece;
                confidenceSum += squareConfidence;
            }
        }

        string candidatePlacement = FenPosition.FromBoardState(
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

        if (!FenPosition.TryParse($"{candidatePlacement} w - - 0 1", out _, out _))
        {
            return false;
        }

        confidence = confidenceSum / 64.0;
        if (confidence < options.ColdStartRecognitionMinConfidence)
        {
            return false;
        }

        placementFen = candidatePlacement;
        return true;
    }
}
