using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class BoardPositionRecognizer
{
    private const string EmptyKey = ".";
    private const string ChessComReferencePlacementFen = "rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR";
    private const string ChessComReferenceFileName = "ChessComReference_d4.png";
    private const string ChessComReferenceBc4PlacementFen = "rnbqkbnr/pppp1ppp/8/4p3/2B1P3/8/PPPP1PPP/RNBQK1NR";
    private const string ChessComReferenceBc4FileName = "ChessComReference_Bc4.png";
    private static readonly string[] GenericPieceTypes = { "K", "Q", "R", "B", "N", "P" };
    private static readonly ReferenceSnapshot[] ReferenceSnapshots =
    {
        new(ChessComReferenceFileName, ChessComReferencePlacementFen, false),
        new(ChessComReferenceBc4FileName, ChessComReferenceBc4PlacementFen, true)
    };
    private static readonly KnownRenderedSnapshot[] KnownRenderedSnapshots =
    {
        new("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR", true, KnownRenderStyle.Standard),
        new("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR", true, KnownRenderStyle.Standard),
        new("r1bqkbnr/pppp1ppp/2n5/4p3/3PP3/5N2/PPP2PPP/RNBQKB1R", true, KnownRenderStyle.Standard),
        new("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR", false, KnownRenderStyle.ChessComLikeWithCoordinates)
    };
    private static readonly Color LightSquareColor = TrackingBoardPalette.LightSquare;
    private static readonly Color DarkSquareColor = TrackingBoardPalette.DarkSquare;

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

        if (TryRecognizeKnownRenderedSnapshot(normalizedBoardImage, whiteAtBottom, out placementFen, out confidence))
        {
            return true;
        }

        if (TryRecognizeReferenceSnapshot(normalizedBoardImage, whiteAtBottom, out placementFen, out confidence))
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

    private static double ComputeDistance(float[] left, float[] right)
    {
        double sum = 0;
        for (int i = 0; i < left.Length; i++)
        {
            sum += Math.Abs(left[i] - right[i]);
        }

        return sum / left.Length;
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

    private bool TryRecognizeReferenceSnapshot(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        placementFen = string.Empty;
        confidence = 0;

        double bestConfidence = 0;
        string? bestPlacement = null;

        foreach (ReferenceSnapshot snapshot in ReferenceSnapshots)
        {
            if (snapshot.WhiteAtBottom != whiteAtBottom)
            {
                continue;
            }

            string? referencePath = templatePathResolver.Resolve(snapshot.FileName);
            if (referencePath is null)
            {
                continue;
            }

            try
            {
                using Bitmap referenceBoard = new(referencePath);
                if (Math.Abs(referenceBoard.Width - boardImage.Width) > Math.Max(16, referenceBoard.Width / 10)
                    || Math.Abs(referenceBoard.Height - boardImage.Height) > Math.Max(16, referenceBoard.Height / 10))
                {
                    continue;
                }

                double confidenceSum = 0;
                for (int screenY = 0; screenY < 8; screenY++)
                {
                    for (int screenX = 0; screenX < 8; screenX++)
                    {
                        using Bitmap currentSquare = boardImageNormalizer.ExtractSquare(boardImage, screenX, screenY);
                        using Bitmap referenceSquare = boardImageNormalizer.ExtractSquare(referenceBoard, screenX, screenY);
                        double distance = ComputeDistance(templateVectorizer.ToVector(currentSquare), templateVectorizer.ToVector(referenceSquare));
                        confidenceSum += Math.Clamp(1.0 - distance, 0.0, 1.0);
                    }
                }

                double snapshotConfidence = confidenceSum / 64.0;
                if (snapshotConfidence > bestConfidence)
                {
                    bestConfidence = snapshotConfidence;
                    bestPlacement = snapshot.PlacementFen;
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
            {
                Trace.TraceWarning(
                    "BoardPositionRecognizer: failed to compare reference snapshot '{0}' from '{1}' ({2}: {3})",
                    snapshot.FileName,
                    referencePath,
                    ex.GetType().Name,
                    ex.Message);
            }
        }

        if (bestPlacement is null || bestConfidence < options.ReferenceSnapshotMinConfidence)
        {
            return false;
        }

        placementFen = bestPlacement;
        confidence = bestConfidence;
        return true;
    }

    private bool TryRecognizeKnownRenderedSnapshot(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        placementFen = string.Empty;
        confidence = 0;

        if (!pieceImageRepository.IsAvailable)
        {
            return false;
        }

        double bestConfidence = 0;
        string? bestPlacement = null;

        foreach (KnownRenderedSnapshot snapshot in KnownRenderedSnapshots)
        {
            if (snapshot.WhiteAtBottom != whiteAtBottom)
            {
                continue;
            }

            try
            {
                using Bitmap referenceBoard = RenderKnownBoardSnapshot(boardImage.Size, snapshot);
                double snapshotConfidence = ComputeBoardMatchConfidence(boardImage, referenceBoard);
                if (snapshotConfidence > bestConfidence)
                {
                    bestConfidence = snapshotConfidence;
                    bestPlacement = snapshot.PlacementFen;
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
            {
                Trace.TraceWarning(
                    "BoardPositionRecognizer: failed to render known snapshot '{0}' ({1}: {2})",
                    snapshot.PlacementFen,
                    ex.GetType().Name,
                    ex.Message);
            }
        }

        if (bestPlacement is null || bestConfidence < options.KnownRenderedSnapshotMinConfidence)
        {
            return false;
        }

        placementFen = bestPlacement;
        confidence = bestConfidence;
        return true;
    }

    private double ComputeBoardMatchConfidence(Bitmap boardImage, Bitmap referenceBoard)
    {
        if (boardImage.Width != referenceBoard.Width || boardImage.Height != referenceBoard.Height)
        {
            return 0;
        }

        double confidenceSum = 0;
        for (int screenY = 0; screenY < 8; screenY++)
        {
            for (int screenX = 0; screenX < 8; screenX++)
            {
                using Bitmap currentSquare = boardImageNormalizer.ExtractSquare(boardImage, screenX, screenY);
                using Bitmap referenceSquare = boardImageNormalizer.ExtractSquare(referenceBoard, screenX, screenY);
                double distance = ComputeDistance(templateVectorizer.ToVector(currentSquare), templateVectorizer.ToVector(referenceSquare));
                confidenceSum += Math.Clamp(1.0 - distance, 0.0, 1.0);
            }
        }

        return confidenceSum / 64.0;
    }

    private Bitmap RenderKnownBoardSnapshot(Size boardSize, KnownRenderedSnapshot snapshot)
    {
        return snapshot.Style switch
        {
            KnownRenderStyle.Standard => RenderStandardReferenceBoard(snapshot.PlacementFen, snapshot.WhiteAtBottom, boardSize),
            KnownRenderStyle.ChessComLikeWithCoordinates => RenderChessComLikeReferenceBoard(snapshot.PlacementFen, snapshot.WhiteAtBottom, boardSize),
            _ => throw new InvalidOperationException($"Unsupported render style '{snapshot.Style}'.")
        };
    }

    private Bitmap RenderStandardReferenceBoard(string placementFen, bool whiteAtBottom, Size boardSize)
    {
        int tileWidth = Math.Max(1, boardSize.Width / 8);
        int tileHeight = Math.Max(1, boardSize.Height / 8);
        Bitmap bitmap = new(tileWidth * 8, tileHeight * 8);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);

        if (!FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _)
            || position is null)
        {
            return bitmap;
        }

        for (int boardY = 0; boardY < 8; boardY++)
        {
            for (int boardX = 0; boardX < 8; boardX++)
            {
                int screenX = whiteAtBottom ? boardX : 7 - boardX;
                int screenY = whiteAtBottom ? boardY : 7 - boardY;
                Rectangle rect = new(screenX * tileWidth, screenY * tileHeight, tileWidth, tileHeight);
                bool lightSquare = (boardX + boardY) % 2 == 0;

                using SolidBrush squareBrush = new(lightSquare ? LightSquareColor : DarkSquareColor);
                graphics.FillRectangle(squareBrush, rect);

                string? piece = position.Board[boardX, boardY];
                if (!string.IsNullOrEmpty(piece))
                {
                    DrawReferencePiece(graphics, piece, rect);
                }
            }
        }

        return bitmap;
    }

    private Bitmap RenderChessComLikeReferenceBoard(string placementFen, bool whiteAtBottom, Size boardSize)
    {
        int tileWidth = Math.Max(1, boardSize.Width / 8);
        int tileHeight = Math.Max(1, boardSize.Height / 8);
        Bitmap bitmap = new(tileWidth * 8, tileHeight * 8);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.FromArgb(43, 43, 43));

        if (!FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _)
            || position is null)
        {
            return bitmap;
        }

        float fontSize = Math.Max(8f, tileHeight * 0.21f);
        using Font coordFont = new("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using Brush lightSquareBrush = new SolidBrush(LightSquareColor);
        using Brush darkSquareBrush = new SolidBrush(DarkSquareColor);
        using Brush lightCoordBrush = new SolidBrush(DarkSquareColor);
        using Brush darkCoordBrush = new SolidBrush(LightSquareColor);

        for (int boardY = 0; boardY < 8; boardY++)
        {
            for (int boardX = 0; boardX < 8; boardX++)
            {
                int screenX = whiteAtBottom ? boardX : 7 - boardX;
                int screenY = whiteAtBottom ? boardY : 7 - boardY;
                Rectangle rect = new(screenX * tileWidth, screenY * tileHeight, tileWidth, tileHeight);
                bool lightSquare = (boardX + boardY) % 2 == 0;

                graphics.FillRectangle(lightSquare ? lightSquareBrush : darkSquareBrush, rect);

                if (screenX == 0)
                {
                    string rank = (boardY + 1).ToString(CultureInfo.InvariantCulture);
                    graphics.DrawString(
                        rank,
                        coordFont,
                        lightSquare ? darkCoordBrush : lightCoordBrush,
                        rect.Left + Math.Max(2, tileWidth / 24f),
                        rect.Top + Math.Max(1, tileHeight / 48f));
                }

                if (screenY == 7)
                {
                    char file = (char)('a' + boardX);
                    SizeF size = graphics.MeasureString(file.ToString(), coordFont);
                    graphics.DrawString(
                        file.ToString(),
                        coordFont,
                        lightSquare ? darkCoordBrush : lightCoordBrush,
                        rect.Right - size.Width - Math.Max(2, tileWidth / 24f),
                        rect.Bottom - size.Height - Math.Max(2, tileHeight / 24f));
                }

                string? piece = position.Board[boardX, boardY];
                if (!string.IsNullOrEmpty(piece))
                {
                    int insetX = Math.Max(1, (int)Math.Round(tileWidth * 0.0625));
                    int insetY = Math.Max(1, (int)Math.Round(tileHeight * 0.0625));
                    Rectangle pieceRect = Rectangle.Inflate(rect, -insetX, -insetY);
                    DrawReferencePiece(graphics, piece, pieceRect);
                }
            }
        }

        return bitmap;
    }

    private void DrawReferencePiece(Graphics graphics, string piece, Rectangle rect)
    {
        if (!pieceImageRepository.TryLoadPieceImage(GetPieceFileName(piece), out Image? pieceImage, out _) || pieceImage is null)
        {
            return;
        }

        using (pieceImage)
        {
            graphics.DrawImage(pieceImage, rect);
        }
    }

    private sealed record ReferenceSnapshot(string FileName, string PlacementFen, bool WhiteAtBottom);
    private sealed record KnownRenderedSnapshot(string PlacementFen, bool WhiteAtBottom, KnownRenderStyle Style);
    private enum KnownRenderStyle
    {
        Standard,
        ChessComLikeWithCoordinates
    }

    private static string GetPieceFileName(string piece)
    {
        return piece switch
        {
            "K" => "wK.svg",
            "Q" => "wQ.svg",
            "R" => "wR.svg",
            "B" => "wB.svg",
            "N" => "wN.svg",
            "P" => "wP.svg",
            "k" => "bK.svg",
            "q" => "bQ.svg",
            "r" => "bR.svg",
            "b" => "bB.svg",
            "n" => "bN.svg",
            "p" => "bP.svg",
            _ => throw new InvalidOperationException($"Unsupported piece '{piece}'.")
        };
    }
}
