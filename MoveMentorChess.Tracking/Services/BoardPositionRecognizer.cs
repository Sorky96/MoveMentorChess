using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class BoardPositionRecognizer
{
    private const string EmptyKey = ".";
    private static readonly string[] GenericPieceTypes = { "K", "Q", "R", "B", "N", "P" };

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
    private readonly BoardRecognitionOptions options;
    private bool genericTemplatesInitialized;

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
    }

    public bool HasTemplates => templates.Count > 0;

    public Bitmap NormalizeBoardImage(Bitmap boardImage)
    {
        return boardImageNormalizer.Normalize(boardImage);
    }

    public void LearnFromBoard(Bitmap boardImage, string placementFen, bool whiteAtBottom)
    {
        if (!FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _)
            || position is null)
        {
            return;
        }

        using Bitmap normalizedBoardImage = NormalizeBoardImage(boardImage);

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
        if (!FenPosition.TryParse(fen, out FenPosition? position, out _)
            || position is null)
        {
            return;
        }

        LearnFromBoard(boardImage, position.GetPlacementFen(), whiteAtBottom);
    }

    public bool TryRecognize(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        placementFen = string.Empty;
        confidence = 0;

        if (!HasTemplates)
        {
            return false;
        }

        using Bitmap normalizedBoardImage = NormalizeBoardImage(boardImage);
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

    public bool TryRecognizeColdStart(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        placementFen = string.Empty;
        confidence = 0;

        EnsureGenericTemplatesInitialized();
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

    private void AddTemplate(string key, float[] vector)
    {
        int maxVariants = key.StartsWith(EmptyKey, StringComparison.Ordinal)
            ? options.MaxEmptyTemplateVariants
            : options.MaxLearnedPieceTemplateVariants;
        templates.Add(key, vector, maxVariants);
    }

    private void AddGenericShapeTemplate(string key, float[] vector)
    {
        genericShapeTemplates.Add(key, vector, options.MaxGenericShapeTemplateVariants);
    }

    private void AddColdStartBoardTemplate(string key, float[] vector)
    {
        coldStartBoardTemplates.Add(key, vector, options.MaxColdStartBoardTemplateVariants);
    }

    private void AddGenericPieceTemplate(string key, float[] vector)
    {
        genericPieceTemplates.Add(key, vector, options.MaxGenericPieceTemplateVariants);
    }

    private void EnsureGenericTemplatesInitialized()
    {
        if (genericTemplatesInitialized)
        {
            return;
        }

        genericTemplatesInitialized = true;

        foreach (string pieceType in GenericPieceTypes)
        {
            using Bitmap whiteTransparentTemplate = pieceTemplateRenderer.RenderFallbackTransparentTemplate(pieceType, true);
            AddGenericShapeTemplate(pieceType, templateVectorizer.ToTemplateMaskVector(whiteTransparentTemplate));
            AddGenericPieceTemplate(pieceType, templateVectorizer.ToTemplateGrayVector(whiteTransparentTemplate));

            using Bitmap blackTransparentTemplate = pieceTemplateRenderer.RenderFallbackTransparentTemplate(pieceType, false);
            AddGenericShapeTemplate(pieceType, templateVectorizer.ToTemplateMaskVector(blackTransparentTemplate));
            AddGenericPieceTemplate(pieceType.ToLowerInvariant(), templateVectorizer.ToTemplateGrayVector(blackTransparentTemplate));

            foreach (bool isLightSquare in new[] { true, false })
            {
                using Bitmap emptyBoardTemplate = pieceTemplateRenderer.RenderEmptyBoardSquare(isLightSquare);
                AddColdStartBoardTemplate(BuildTemplateKey(null, isLightSquare), templateVectorizer.ToBoardTemplateVector(emptyBoardTemplate));

                using Bitmap whiteBoardTemplate = pieceTemplateRenderer.RenderFallbackTemplate(pieceType, true, isLightSquare);
                AddColdStartBoardTemplate(BuildTemplateKey(pieceType, isLightSquare), templateVectorizer.ToBoardTemplateVector(whiteBoardTemplate));
                AddGenericShapeTemplate(pieceType, templateVectorizer.ToMaskVector(whiteBoardTemplate, out _, out _, out _, out _));

                using Bitmap blackBoardTemplate = pieceTemplateRenderer.RenderFallbackTemplate(pieceType, false, isLightSquare);
                AddColdStartBoardTemplate(BuildTemplateKey(pieceType.ToLowerInvariant(), isLightSquare), templateVectorizer.ToBoardTemplateVector(blackBoardTemplate));
                AddGenericShapeTemplate(pieceType, templateVectorizer.ToMaskVector(blackBoardTemplate, out _, out _, out _, out _));
            }
        }

        if (!pieceImageRepository.IsAvailable)
        {
            return;
        }

        foreach (string pieceType in GenericPieceTypes)
        {
            TryAddImageTemplate(pieceType, $"w{pieceType}.svg");
            TryAddImageTemplate(pieceType, $"b{pieceType}.svg");
        }
    }

    private void TryAddImageTemplate(string pieceType, string fileName)
    {
        string? path = null;

        try
        {
            if (!pieceImageRepository.TryLoadPieceImage(fileName, out Image? source, out path) || source is null)
            {
                return;
            }

            using (source)
            {
                bool isWhitePiece = fileName.StartsWith("w", StringComparison.OrdinalIgnoreCase);

                foreach (int inset in new[] { 3, 5, 7, 9 })
                {
                    using Bitmap transparentBitmap = pieceTemplateRenderer.RenderTransparentImageTemplate(source, inset);
                    AddGenericShapeTemplate(pieceType, templateVectorizer.ToTemplateMaskVector(transparentBitmap));
                    AddGenericPieceTemplate(isWhitePiece ? pieceType : pieceType.ToLowerInvariant(), templateVectorizer.ToTemplateGrayVector(transparentBitmap));

                    foreach (bool isLightSquare in new[] { true, false })
                    {
                        using Bitmap boardBitmap = pieceTemplateRenderer.RenderImageTemplate(source, isLightSquare, inset);
                        AddColdStartBoardTemplate(
                            BuildTemplateKey(isWhitePiece ? pieceType : pieceType.ToLowerInvariant(), isLightSquare),
                            templateVectorizer.ToBoardTemplateVector(boardBitmap));
                        AddGenericShapeTemplate(pieceType, templateVectorizer.ToMaskVector(boardBitmap, out _, out _, out _, out _));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OutOfMemoryException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            Trace.TraceWarning(
                "BoardPositionRecognizer: failed to load image template '{0}' from '{1}' ({2}: {3})",
                pieceType,
                path ?? fileName,
                ex.GetType().Name,
                ex.Message);
        }
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
