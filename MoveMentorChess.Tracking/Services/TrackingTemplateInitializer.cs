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

    private readonly object initializationGate = new();
    private volatile bool initialized;

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

        lock (initializationGate)
        {
            if (initialized)
            {
                return;
            }

            TrackingTemplateBank coldStartBoardTemplatesToAdd = new();
            TrackingTemplateBank genericShapeTemplatesToAdd = new();
            TrackingTemplateBank genericPieceTemplatesToAdd = new();

            PopulateTemplates(
                coldStartBoardTemplatesToAdd,
                genericShapeTemplatesToAdd,
                genericPieceTemplatesToAdd);

            CopyTemplates(coldStartBoardTemplatesToAdd, coldStartBoardTemplates, options.MaxColdStartBoardTemplateVariants);
            CopyTemplates(genericShapeTemplatesToAdd, genericShapeTemplates, options.MaxGenericShapeTemplateVariants);
            CopyTemplates(genericPieceTemplatesToAdd, genericPieceTemplates, options.MaxGenericPieceTemplateVariants);
            initialized = true;
        }
    }

    private void PopulateTemplates(
        TrackingTemplateBank coldStartBoardTemplatesToAdd,
        TrackingTemplateBank genericShapeTemplatesToAdd,
        TrackingTemplateBank genericPieceTemplatesToAdd)
    {
        foreach (string pieceType in GenericPieceTypes)
        {
            using Bitmap whiteTransparentTemplate = pieceTemplateRenderer.RenderFallbackTransparentTemplate(pieceType, true);
            AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToTemplateMaskVector(whiteTransparentTemplate));
            AddGenericPieceTemplate(genericPieceTemplatesToAdd, pieceType, templateVectorizer.ToTemplateGrayVector(whiteTransparentTemplate));

            using Bitmap blackTransparentTemplate = pieceTemplateRenderer.RenderFallbackTransparentTemplate(pieceType, false);
            AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToTemplateMaskVector(blackTransparentTemplate));
            AddGenericPieceTemplate(genericPieceTemplatesToAdd, pieceType.ToLowerInvariant(), templateVectorizer.ToTemplateGrayVector(blackTransparentTemplate));

            foreach (bool isLightSquare in new[] { true, false })
            {
                using Bitmap emptyBoardTemplate = pieceTemplateRenderer.RenderEmptyBoardSquare(isLightSquare);
                AddColdStartBoardTemplate(coldStartBoardTemplatesToAdd, BuildTemplateKey(null, isLightSquare), templateVectorizer.ToBoardTemplateVector(emptyBoardTemplate));

                using Bitmap whiteBoardTemplate = pieceTemplateRenderer.RenderFallbackTemplate(pieceType, true, isLightSquare);
                AddColdStartBoardTemplate(coldStartBoardTemplatesToAdd, BuildTemplateKey(pieceType, isLightSquare), templateVectorizer.ToBoardTemplateVector(whiteBoardTemplate));
                AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToMaskVector(whiteBoardTemplate, out _, out _, out _, out _));

                using Bitmap blackBoardTemplate = pieceTemplateRenderer.RenderFallbackTemplate(pieceType, false, isLightSquare);
                AddColdStartBoardTemplate(coldStartBoardTemplatesToAdd, BuildTemplateKey(pieceType.ToLowerInvariant(), isLightSquare), templateVectorizer.ToBoardTemplateVector(blackBoardTemplate));
                AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToMaskVector(blackBoardTemplate, out _, out _, out _, out _));
            }
        }

        if (!pieceImageRepository.IsAvailable)
        {
            return;
        }

        foreach (string pieceType in GenericPieceTypes)
        {
            TryAddImageTemplate(
                pieceType,
                $"w{pieceType}.png",
                coldStartBoardTemplatesToAdd,
                genericShapeTemplatesToAdd,
                genericPieceTemplatesToAdd);
            TryAddImageTemplate(
                pieceType,
                $"b{pieceType}.png",
                coldStartBoardTemplatesToAdd,
                genericShapeTemplatesToAdd,
                genericPieceTemplatesToAdd);
        }
    }

    private void TryAddImageTemplate(
        string pieceType,
        string fileName,
        TrackingTemplateBank coldStartBoardTemplatesToAdd,
        TrackingTemplateBank genericShapeTemplatesToAdd,
        TrackingTemplateBank genericPieceTemplatesToAdd)
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
                    AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToTemplateMaskVector(transparentBitmap));
                    AddGenericPieceTemplate(genericPieceTemplatesToAdd, isWhitePiece ? pieceType : pieceType.ToLowerInvariant(), templateVectorizer.ToTemplateGrayVector(transparentBitmap));

                    foreach (bool isLightSquare in new[] { true, false })
                    {
                        using Bitmap boardBitmap = pieceTemplateRenderer.RenderImageTemplate(source, isLightSquare, inset);
                        AddColdStartBoardTemplate(
                            coldStartBoardTemplatesToAdd,
                            BuildTemplateKey(isWhitePiece ? pieceType : pieceType.ToLowerInvariant(), isLightSquare),
                            templateVectorizer.ToBoardTemplateVector(boardBitmap));
                        AddGenericShapeTemplate(genericShapeTemplatesToAdd, pieceType, templateVectorizer.ToMaskVector(boardBitmap, out _, out _, out _, out _));
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

    private static void CopyTemplates(TrackingTemplateBank source, TrackingTemplateBank destination, int maxVariants)
    {
        foreach ((string key, IReadOnlyList<float[]> variants) in source.Enumerate())
        {
            foreach (float[] variant in variants)
            {
                destination.Add(key, variant, maxVariants);
            }
        }
    }

    private void AddGenericShapeTemplate(TrackingTemplateBank bank, string key, float[] vector)
    {
        bank.Add(key, vector, options.MaxGenericShapeTemplateVariants);
    }

    private void AddColdStartBoardTemplate(TrackingTemplateBank bank, string key, float[] vector)
    {
        bank.Add(key, vector, options.MaxColdStartBoardTemplateVariants);
    }

    private void AddGenericPieceTemplate(TrackingTemplateBank bank, string key, float[] vector)
    {
        bank.Add(key, vector, options.MaxGenericPieceTemplateVariants);
    }

    private static string BuildTemplateKey(string? piece, bool isLightSquare)
    {
        string symbol = string.IsNullOrEmpty(piece) ? EmptyKey : piece;
        return $"{symbol}|{(isLightSquare ? 'L' : 'D')}";
    }
}
