using System;
using System.Collections.Generic;

namespace Fellowship_overlay.Core;

public sealed class AppSettings
{
    public string? LogDirectory { get; set; }
    public string? PlayerName { get; set; }
    public string? PlayerGuid { get; set; }
    public bool ClickThrough { get; set; } = true; // legacy setting retained for backward compatibility
    public bool OverlaysLocked { get; set; } = true;
    public bool DebugEnabled { get; set; }
    public List<OverlaySettings> Overlays { get; set; } = new();
}

public sealed class OverlaySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Overlay";
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 260;
    public double Opacity { get; set; } = 0.85;
    public List<int> TrackedSpellIds { get; set; } = new();
	public bool ShowIconsOnly { get; set; }
}
