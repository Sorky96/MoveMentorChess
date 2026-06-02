using System.Drawing;
using System.Drawing.Drawing2D;

namespace MoveMentorChess.Tracking;

public sealed class DefaultTrackingPieceTemplateRenderer : ITrackingPieceTemplateRenderer
{
    private static readonly Color LightSquareColor = TrackingBoardPalette.LightSquare;
    private static readonly Color DarkSquareColor = TrackingBoardPalette.DarkSquare;

    public Bitmap RenderFallbackTemplate(string pieceType, bool isWhitePiece, bool isLightSquare)
    {
        Bitmap bitmap = new(64, 64);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(isLightSquare ? LightSquareColor : DarkSquareColor);

        DrawFallbackPiece(graphics, pieceType, isWhitePiece);
        return bitmap;
    }

    public Bitmap RenderEmptyBoardSquare(bool isLightSquare)
    {
        Bitmap bitmap = new(64, 64);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(isLightSquare ? LightSquareColor : DarkSquareColor);
        return bitmap;
    }

    public Bitmap RenderFallbackTransparentTemplate(string pieceType, bool isWhitePiece)
    {
        Bitmap bitmap = new(64, 64);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        DrawFallbackPiece(graphics, pieceType, isWhitePiece);
        return bitmap;
    }

    public Bitmap RenderImageTemplate(Image image, bool isLightSquare, int inset)
    {
        ValidateImageTemplateArguments(image, inset);

        Bitmap bitmap = new(64, 64);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.Clear(isLightSquare ? LightSquareColor : DarkSquareColor);
        graphics.DrawImage(image, inset, inset, 64 - inset * 2, 64 - inset * 2);
        return bitmap;
    }

    public Bitmap RenderTransparentImageTemplate(Image image, int inset)
    {
        ValidateImageTemplateArguments(image, inset);

        Bitmap bitmap = new(64, 64);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(image, inset, inset, 64 - inset * 2, 64 - inset * 2);
        return bitmap;
    }

    private static void ValidateImageTemplateArguments(Image image, int inset)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (inset is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inset),
                inset,
                "Inset must be between 0 and 31 inclusive so the rendered template width and height stay greater than 0.");
        }
    }

    private static void DrawFallbackPiece(Graphics graphics, string pieceType, bool isWhitePiece)
    {
        Rectangle inset = Rectangle.Inflate(new Rectangle(0, 0, 64, 64), -8, -8);
        using Brush fillBrush = new SolidBrush(isWhitePiece ? Color.WhiteSmoke : Color.FromArgb(24, 24, 24));
        using Brush outlineBrush = new SolidBrush(isWhitePiece ? Color.Black : Color.Gainsboro);
        using Font font = new("Segoe UI", 24, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.FillEllipse(fillBrush, inset);

        SizeF textSize = graphics.MeasureString(pieceType, font);
        PointF location = new(
            (64 - textSize.Width) / 2f,
            (64 - textSize.Height) / 2f - 1f);
        graphics.DrawString(pieceType, font, outlineBrush, location);
    }
}
