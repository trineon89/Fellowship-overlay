using System;
using System.Drawing;
using System.Drawing.Imaging;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class ScreenCaptureService
{
    private static readonly Lazy<ScreenCaptureService> _instance = new(() => new ScreenCaptureService());

    public static ScreenCaptureService Instance => _instance.Value;

    private ScreenCaptureService()
    {
    }

    public Bitmap? Capture(CaptureRegionSettings region)
    {
        if (region == null || !region.IsValid)
        {
            return null;
        }

        try
        {
            var rect = region.ToRectangle();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }

            var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}