using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class TrackingTemplateInitializer
{
    private const string EmptyKey = ".";
    private static readonly string[] GenericPieceTypes = { "K", "Q", "R", "B", "N", "P" };

    private readonly ITrackingPieceImageRepository pieceImageRepository;
    private readonly ITrackingPieceTemplateRenderer pieceTemplateRenderer;
    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly BoardRecognitionOptions options;
    private readonly TrackingTemplateBank coldStartBoardTemplates;
    private readonly TrackingTemplateBank genericShapeTemplates;
    private readonly TrackingTemplateBank genericPieceTemplates;

    private bool initialized;

    public TrackingTemplateInitializer(
        ITrackingPieceImageRepository pieceImageRepository,
        ITrackingPieceTemplateRenderer pieceTemplateRenderer,
        ITrackingTemplateVectorizer templateVectorizer,
        BoardRecognitionOptions options,
        TrackingTemplateBank coldStartBoardTemplates,
        TrackingTemplateBank genericShapeTemplates,
        TrackingTemplateBank genericPieceTemplates)
    {
        this.pieceImageRepository = pieceImageRepository ?? throw new ArgumentNullException(nameof(pieceImageRepository));
        this.pieceTemplateRenderer = pieceTemplateRenderer ?? throw new ArgumentNullException(nameof(pieceTemplateRenderer));
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.coldStartBoardTemplates = coldStartBoardTemplates ?? throw new ArgumentNullException(nameof(coldStartBoardTemplates));
        this.genericShapeTemplates = genericShapeTemplates ?? throw new ArgumentNullException(nameof(genericShapeTemplates));
        this.genericPieceTemplates = genericPieceTemplates ?? throw new ArgumentNullException(nameof(genericPieceTemplates));
    }

    public void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

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

    private static string BuildTemplateKey(string? piece, bool isLightSquare)
    {
        string symbol = string.IsNullOrEmpty(piece) ? EmptyKey : piece;
        return $"{symbol}|{(isLightSquare ? 'L' : 'D')}";
    }
}
