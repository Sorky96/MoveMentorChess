using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace MoveMentorChess.Tracking.Interop;

public static class PixConverter
{
    public static Pix ToPix(Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
        stream.Position = 0;
        return Pix.LoadFromMemory(stream.ToArray());
    }
}
