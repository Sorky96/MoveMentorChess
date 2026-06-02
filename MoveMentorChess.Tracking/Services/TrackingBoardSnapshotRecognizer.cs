using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class TrackingBoardSnapshotRecognizer
{
    private const string ChessComReferencePlacementFen = "rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR";
    private const string ChessComReferenceFileName = "ChessComReference_d4.png";
    private const string ChessComReferenceBc4PlacementFen = "rnbqkbnr/pppp1ppp/8/4p3/2B1P3/8/PPPP1PPP/RNBQK1NR";
    private const string ChessComReferenceBc4FileName = "ChessComReference_Bc4.png";

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

    private readonly ITrackingPieceImageRepository pieceImageRepository;
    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly ITrackingTemplatePathResolver templatePathResolver;
    private readonly IBoardImageNormalizer boardImageNormalizer;
    private readonly BoardRecognitionOptions options;

    public TrackingBoardSnapshotRecognizer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingTemplateVectorizer templateVectorizer,
        ITrackingTemplatePathResolver templatePathResolver,
        IBoardImageNormalizer boardImageNormalizer,
        BoardRecognitionOptions options)
    {
        this.pieceImageRepository = pieceImageRepository ?? throw new ArgumentNullException(nameof(pieceImageRepository));
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.templatePathResolver = templatePathResolver ?? throw new ArgumentNullException(nameof(templatePathResolver));
        this.boardImageNormalizer = boardImageNormalizer ?? throw new ArgumentNullException(nameof(boardImageNormalizer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    public bool TryRecognizeReferenceSnapshot(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

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

                double snapshotConfidence = ComputeBoardMatchConfidence(boardImage, referenceBoard, requireSameSize: false);
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

    public bool TryRecognizeKnownRenderedSnapshot(Bitmap boardImage, bool whiteAtBottom, out string placementFen, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

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
                double snapshotConfidence = ComputeBoardMatchConfidence(boardImage, referenceBoard, requireSameSize: true);
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

    private double ComputeBoardMatchConfidence(Bitmap boardImage, Bitmap referenceBoard, bool requireSameSize)
    {
        if (requireSameSize && (boardImage.Width != referenceBoard.Width || boardImage.Height != referenceBoard.Height))
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
        Bitmap bitmap = new(boardSize.Width, boardSize.Height);
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
                Rectangle rect = GetSquareBounds(boardSize, screenX, screenY);
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
        Bitmap bitmap = new(boardSize.Width, boardSize.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.FromArgb(43, 43, 43));

        if (!FenPosition.TryParse($"{placementFen} w - - 0 1", out FenPosition? position, out _)
            || position is null)
        {
            return bitmap;
        }

        float fontSize = Math.Max(8f, (boardSize.Height / 8f) * 0.21f);
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
                Rectangle rect = GetSquareBounds(boardSize, screenX, screenY);
                bool lightSquare = (boardX + boardY) % 2 == 0;

                graphics.FillRectangle(lightSquare ? lightSquareBrush : darkSquareBrush, rect);

                if (screenX == 0)
                {
                    string rank = (boardY + 1).ToString(CultureInfo.InvariantCulture);
                    graphics.DrawString(
                        rank,
                        coordFont,
                        lightSquare ? darkCoordBrush : lightCoordBrush,
                        rect.Left + Math.Max(2, rect.Width / 24f),
                        rect.Top + Math.Max(1, rect.Height / 48f));
                }

                if (screenY == 7)
                {
                    char file = (char)('a' + boardX);
                    SizeF size = graphics.MeasureString(file.ToString(), coordFont);
                    graphics.DrawString(
                        file.ToString(),
                        coordFont,
                        lightSquare ? darkCoordBrush : lightCoordBrush,
                        rect.Right - size.Width - Math.Max(2, rect.Width / 24f),
                        rect.Bottom - size.Height - Math.Max(2, rect.Height / 24f));
                }

                string? piece = position.Board[boardX, boardY];
                if (!string.IsNullOrEmpty(piece))
                {
                    int insetX = Math.Max(1, (int)Math.Round(rect.Width * 0.0625));
                    int insetY = Math.Max(1, (int)Math.Round(rect.Height * 0.0625));
                    Rectangle pieceRect = Rectangle.Inflate(rect, -insetX, -insetY);
                    DrawReferencePiece(graphics, piece, pieceRect);
                }
            }
        }

        return bitmap;
    }

    private static Rectangle GetSquareBounds(Size boardSize, int screenX, int screenY)
    {
        int left = boardSize.Width * screenX / 8;
        int top = boardSize.Height * screenY / 8;
        int right = boardSize.Width * (screenX + 1) / 8;
        int bottom = boardSize.Height * (screenY + 1) / 8;
        return Rectangle.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
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

    private static double ComputeDistance(float[] left, float[] right)
    {
        double sum = 0;
        for (int i = 0; i < left.Length; i++)
        {
            sum += Math.Abs(left[i] - right[i]);
        }

        return sum / left.Length;
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
