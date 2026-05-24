using System.Drawing;

namespace MoveMentorChess.Tracking;

public interface ITrackingPieceImageRepository
{
    bool IsAvailable { get; }

    bool TryLoadPieceImage(string fileName, out Image? image, out string? path);
}
