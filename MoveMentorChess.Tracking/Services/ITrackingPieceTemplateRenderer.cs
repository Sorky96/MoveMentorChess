using System.Drawing;

namespace MoveMentorChess.Tracking;

public interface ITrackingPieceTemplateRenderer
{
    Bitmap RenderFallbackTemplate(string pieceType, bool isWhitePiece, bool isLightSquare);

    Bitmap RenderEmptyBoardSquare(bool isLightSquare);

    Bitmap RenderFallbackTransparentTemplate(string pieceType, bool isWhitePiece);

    Bitmap RenderImageTemplate(Image image, bool isLightSquare, int inset);

    Bitmap RenderTransparentImageTemplate(Image image, int inset);
}
