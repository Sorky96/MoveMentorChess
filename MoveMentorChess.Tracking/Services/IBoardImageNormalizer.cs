using System.Drawing;

namespace MoveMentorChess.Tracking;

public interface IBoardImageNormalizer
{
    Bitmap Normalize(Bitmap boardImage);

    Bitmap ExtractSquare(Bitmap boardImage, int screenX, int screenY);
}
