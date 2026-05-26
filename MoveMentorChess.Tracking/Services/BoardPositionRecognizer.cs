using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class BoardPositionRecognizer
{
    private readonly TrackingTemplateBank templates = new();
    private readonly TrackingTemplateBank coldStartBoardTemplates = new();
    private readonly TrackingTemplateBank genericShapeTemplates = new();
    private readonly TrackingTemplateBank genericPieceTemplates = new();
    private readonly ITrackingPieceImageRepository pieceImageRepository;
    private readonly ITrackingPieceTemplateRenderer pieceTemplateRenderer;
    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly ITrackingTemplatePathResolver templatePathResolver;
    private readonly IBoardImageNormalizer boardImageNormalizer;
    private readonly TrackingSquareClassifier squareClassifier;
    private readonly TrackingBoardSnapshotRecognizer snapshotRecognizer;
    private readonly TrackingTemplateInitializer templateInitializer;
    private readonly TrackingLearnedTemplateTrainer learnedTemplateTrainer;
    private readonly TrackingLearnedBoardRecognizer learnedBoardRecognizer;
    private readonly BoardRecognitionOptions options;

    public BoardPositionRecognizer(string? imagesDirectory = null)
        : this(
            new DirectoryTrackingPieceImageRepository(imagesDirectory),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            new DefaultTrackingTemplatePathResolver(),
            new DefaultBoardImageNormalizer())
    {
    }

    public BoardPositionRecognizer(string? imagesDirectory, ITrackingTemplatePathResolver templatePathResolver)
        : this(
            new DirectoryTrackingPieceImageRepository(imagesDirectory),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            templatePathResolver,
            new DefaultBoardImageNormalizer())
    {
    }

    public BoardPositionRecognizer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingTemplatePathResolver templatePathResolver)
        : this(
            pieceImageRepository,
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            templatePathResolver,
            new DefaultBoardImageNormalizer())
    {
    }

    public BoardPositionRecognizer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingPieceTemplateRenderer pieceTemplateRenderer,
        ITrackingTemplatePathResolver templatePathResolver)
        : this(
            pieceImageRepository,
            pieceTemplateRenderer,
            new DefaultTrackingTemplateVectorizer(),
            templatePathResolver,
            new DefaultBoardImageNormalizer())
    {
    }

    public BoardPositionRecognizer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingPieceTemplateRenderer pieceTemplateRenderer,
        ITrackingTemplateVectorizer templateVectorizer,
        ITrackingTemplatePathResolver templatePathResolver)
        : this(
            pieceImageRepository,
            pieceTemplateRenderer,
            templateVectorizer,
            templatePathResolver,
            new DefaultBoardImageNormalizer())
    {
    }

    public BoardPositionRecognizer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingPieceTemplateRenderer pieceTemplateRenderer,
        ITrackingTemplateVectorizer templateVectorizer,
        ITrackingTemplatePathResolver templatePathResolver,
        IBoardImageNormalizer boardImageNormalizer,
        BoardRecognitionOptions? options = null)
    {
        this.pieceImageRepository = pieceImageRepository ?? throw new ArgumentNullException(nameof(pieceImageRepository));
        this.pieceTemplateRenderer = pieceTemplateRenderer ?? throw new ArgumentNullException(nameof(pieceTemplateRenderer));
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.templatePathResolver = templatePathResolver ?? throw new ArgumentNullException(nameof(templatePathResolver));
        this.boardImageNormalizer = boardImageNormalizer ?? throw new ArgumentNullException(nameof(boardImageNormalizer));
        this.options = options ?? BoardRecognitionOptions.Default;
        this.options.Validate();
        squareClassifier = new TrackingSquareClassifier(
            this.templateVectorizer,
            this.options,
            templates,
            coldStartBoardTemplates,
            genericShapeTemplates,
            genericPieceTemplates);
        snapshotRecognizer = new TrackingBoardSnapshotRecognizer(
            this.pieceImageRepository,
            this.templateVectorizer,
            this.templatePathResolver,
            this.boardImageNormalizer,
            this.options);
        templateInitializer = new TrackingTemplateInitializer(
            this.pieceImageRepository,
            this.pieceTemplateRenderer,
            this.templateVectorizer,
            this.options,
            coldStartBoardTemplates,
            genericShapeTemplates,
            genericPieceTemplates);
        learnedTemplateTrainer = new TrackingLearnedTemplateTrainer(
            this.templateVectorizer,
            this.boardImageNormalizer,
            this.options,
            templates);
        learnedBoardRecognizer = new TrackingLearnedBoardRecognizer(
            this.templateVectorizer,
            this.boardImageNormalizer,
            squareClassifier,
            this.options,
            templates);
    }

    public bool HasTemplates => templates.Count > 0;

    public Bitmap NormalizeBoardImage(Bitmap boardImage)
    {
        return boardImageNormalizer.Normalize(boardImage);
    }

    public void LearnFromBoard(Bitmap boardImage, string placementFen, bool whiteAtBottom)
    {
        learnedTemplateTrainer.LearnFromBoard(boardImage, placementFen, whiteAtBottom);
    }

    public void LearnFromFen(Bitmap boardImage, string fen, bool whiteAtBottom)
    {
        learnedTemplateTrainer.LearnFromFen(boardImage, fen, whiteAtBottom);
    }

    public bool TryRecognize(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        return learnedBoardRecognizer.TryRecognize(boardImage, whiteAtBottom, out placementFen, out confidence);
    }

    public bool TryRecognizeColdStart(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        placementFen = string.Empty;
        confidence = 0;

        templateInitializer.EnsureInitialized();
        using Bitmap normalizedBoardImage = NormalizeBoardImage(boardImage);

        if (snapshotRecognizer.TryRecognizeKnownRenderedSnapshot(normalizedBoardImage, whiteAtBottom, out placementFen, out confidence))
        {
            return true;
        }

        if (snapshotRecognizer.TryRecognizeReferenceSnapshot(normalizedBoardImage, whiteAtBottom, out placementFen, out confidence))
        {
            return true;
        }

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
                Point boardSquare = MapScreenSquareToBoard(screenX, screenY, whiteAtBottom);
                bool isLightSquare = IsLightSquare(boardSquare);

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

    private static Point MapScreenSquareToBoard(int screenX, int screenY, bool whiteAtBottom)
    {
        return whiteAtBottom
            ? new Point(screenX, screenY)
            : new Point(7 - screenX, 7 - screenY);
    }

    private static bool IsLightSquare(Point boardSquare) => (boardSquare.X + boardSquare.Y) % 2 == 0;

}
