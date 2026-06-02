using System.Drawing;

namespace MoveMentorChess.Tracking;

internal static class TrackingBoardSquareMapper
{
    public static Point MapScreenSquareToBoard(int screenX, int screenY, bool whiteAtBottom)
    {
        return whiteAtBottom
            ? new Point(screenX, screenY)
            : new Point(7 - screenX, 7 - screenY);
    }

    public static bool IsLightSquare(Point boardSquare) => (boardSquare.X + boardSquare.Y) % 2 == 0;
}
