using System.Drawing;

namespace MoveMentorChess.Tracking;

public interface ITrackingTemplateVectorizer
{
    float[] ToVector(Bitmap bitmap);

    float[] ToPieceGrayVector(Bitmap bitmap);

    float[] ToBoardTemplateVector(Bitmap bitmap);

    float[] ToTemplateGrayVector(Bitmap bitmap);

    float[] ToMaskVector(Bitmap bitmap, out double occupancy, out double centralOccupancy, out double pieceLuminance, out double backgroundLuminance);

    float[] ToTemplateMaskVector(Bitmap bitmap);
}
