using System;
using System.Drawing;

namespace Fellowship_overlay.Core;

public sealed class CaptureRegionSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public bool IsValid => Width > 0 && Height > 0;

    public Rectangle ToRectangle()
    {
        var x = (int)Math.Round(Left);
        var y = (int)Math.Round(Top);
        var width = Math.Max(0, (int)Math.Round(Width));
        var height = Math.Max(0, (int)Math.Round(Height));
        return new Rectangle(x, y, width, height);
    }

    public CaptureRegionSettings Clone()
    {
        return new CaptureRegionSettings
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };
    }
}