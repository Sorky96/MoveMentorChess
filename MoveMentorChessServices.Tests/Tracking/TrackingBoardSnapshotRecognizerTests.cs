using System.Drawing;
using System.Drawing.Imaging;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingBoardSnapshotRecognizerTests
{
    private const string Bc4PlacementFen = "rnbqkbnr/pppp1ppp/8/4p3/2B1P3/8/PPPP1PPP/RNBQK1NR";

    [Fact]
    public void TryRecognizeReferenceSnapshot_ReturnsMatchingReferencePlacement()
    {
        string referencePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            using Bitmap board = CreateSolidBoard();
            board.Save(referencePath, ImageFormat.Png);
            TrackingBoardSnapshotRecognizer recognizer = CreateRecognizer(new FixedTemplatePathResolver(referencePath));

            bool recognized = recognizer.TryRecognizeReferenceSnapshot(board, whiteAtBottom: true, out string placementFen, out double confidence);

            Assert.True(recognized);
            Assert.Equal(Bc4PlacementFen, placementFen);
            Assert.Equal(1.0, confidence);
        }
        finally
        {
            if (File.Exists(referencePath))
            {
                File.Delete(referencePath);
            }
        }
    }

    [Fact]
    public void TryRecognizeKnownRenderedSnapshot_ReturnsFalseWhenPieceRepositoryIsUnavailable()
    {
        TrackingBoardSnapshotRecognizer recognizer = CreateRecognizer(pieceImageRepository: new UnavailablePieceImageRepository());
        using Bitmap board = CreateSolidBoard();

        bool recognized = recognizer.TryRecognizeKnownRenderedSnapshot(board, whiteAtBottom: true, out string placementFen, out double confidence);

        Assert.False(recognized);
        Assert.Equal(string.Empty, placementFen);
        Assert.Equal(0, confidence);
    }

    [Fact]
    public void TryRecognizeKnownRenderedSnapshot_PreservesOddBoardSizeForReferenceRender()
    {
        RecordingBoardImageNormalizer normalizer = new();
        TrackingBoardSnapshotRecognizer recognizer = CreateRecognizer(
            pieceImageRepository: new AvailablePieceImageRepository(),
            boardImageNormalizer: normalizer);
        using Bitmap board = new(401, 401);

        bool recognized = recognizer.TryRecognizeKnownRenderedSnapshot(board, whiteAtBottom: true, out _, out double confidence);

        Assert.True(recognized);
        Assert.Equal(1.0, confidence);
        Assert.All(normalizer.ExtractedBitmapSizes, size => Assert.Equal(board.Size, size));
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            ReferenceSnapshotMinConfidence = -0.1
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateRecognizer(options: invalidOptions));
    }

    private static TrackingBoardSnapshotRecognizer CreateRecognizer(
        ITrackingTemplatePathResolver? templatePathResolver = null,
        ITrackingPieceImageRepository? pieceImageRepository = null,
        IBoardImageNormalizer? boardImageNormalizer = null,
        BoardRecognitionOptions? options = null)
    {
        return new TrackingBoardSnapshotRecognizer(
            pieceImageRepository ?? new UnavailablePieceImageRepository(),
            new DefaultTrackingTemplateVectorizer(),
            templatePathResolver ?? new MissingTemplatePathResolver(),
            boardImageNormalizer ?? new DefaultBoardImageNormalizer(),
            options ?? BoardRecognitionOptions.Default);
    }

    private static Bitmap CreateSolidBoard()
    {
        Bitmap bitmap = new(128, 128);
        using Graphics graphics = Graphics.FromImage(bitmap);
        using Brush brush = new SolidBrush(TrackingBoardPalette.LightSquare);
        graphics.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
        return bitmap;
    }

    private sealed class FixedTemplatePathResolver(string path) : ITrackingTemplatePathResolver
    {
        public string? Resolve(string fileName) => fileName == "ChessComReference_Bc4.png" ? path : null;
    }

    private sealed class MissingTemplatePathResolver : ITrackingTemplatePathResolver
    {
        public string? Resolve(string fileName) => null;
    }

    private sealed class UnavailablePieceImageRepository : ITrackingPieceImageRepository
    {
        public bool IsAvailable => false;

        public bool TryLoadPieceImage(string fileName, out Image? image, out string? path)
        {
            image = null;
            path = null;
            return false;
        }
    }

    private sealed class AvailablePieceImageRepository : ITrackingPieceImageRepository
    {
        public bool IsAvailable => true;

        public bool TryLoadPieceImage(string fileName, out Image? image, out string? path)
        {
            image = new Bitmap(1, 1);
            path = fileName;
            return true;
        }
    }

    private sealed class RecordingBoardImageNormalizer : IBoardImageNormalizer
    {
        public List<Size> ExtractedBitmapSizes { get; } = [];

        public Bitmap Normalize(Bitmap boardImage)
        {
            return (Bitmap)boardImage.Clone();
        }

        public Bitmap ExtractSquare(Bitmap boardImage, int screenX, int screenY)
        {
            ExtractedBitmapSizes.Add(boardImage.Size);
            return new Bitmap(1, 1);
        }
    }
}
