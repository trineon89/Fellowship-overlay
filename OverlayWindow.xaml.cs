using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Fellowship_overlay.Core;

namespace Fellowship_overlay;

public partial class OverlayWindow : Window
{
    private const int WardenOfTheTempleSpellId = 2447;

    private OverlaySettings _settings;
    private bool _isLocked = true;
    private static readonly SolidColorBrush UnlockedBackgroundBrush;

    static OverlayWindow()
    {
        UnlockedBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0x00, 0x00));
        UnlockedBackgroundBrush.Freeze();
    }

    public OverlayWindow(OverlaySettings settings)
    {
        _settings = settings;
        InitializeComponent();
        ApplySettings(settings);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        MakeClickThrough(_isLocked);
    }

    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        Title = settings.Name;
        TitleTextBlock.Text = settings.Name;
        TitleTextBlock.Visibility = string.IsNullOrWhiteSpace(settings.Name)
            ? Visibility.Collapsed
            : Visibility.Visible;
        Left = settings.Left;
        Top = settings.Top;
        Width = settings.Width;
        Height = settings.Height;
        Opacity = settings.Opacity;
    }

    public void SetLockState(bool locked)
    {
        _isLocked = locked;
        if (IsLoaded)
        {
            MakeClickThrough(locked);
        }

        MoveHintTextBlock.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
        ResizeGripControl.Visibility = locked ? Visibility.Collapsed : Visibility.Visible;
        Cursor = locked ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.SizeAll;
        ChromeBorder.Background = locked ? System.Windows.Media.Brushes.Transparent : UnlockedBackgroundBrush;
    }

    public void SetStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            StatusOverlay.Visibility = Visibility.Collapsed;
            StatusTextBlock.Text = string.Empty;
        }
        else
        {
            StatusOverlay.Visibility = Visibility.Visible;
            StatusTextBlock.Text = message;
        }
    }

    public void UpdateBuffs(IReadOnlyList<(Buff Buff, BuffDefinition? Definition)> buffs, DateTimeOffset now)
    {
        BuffPanel.Children.Clear();

		if (_settings.ShowIconsOnly)
        {
            var iconWrap = new WrapPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            foreach (var (buff, definition) in buffs)
            {
                var total = buff.ExpiresAt.HasValue ? buff.ExpiresAt.Value - buff.AppliedAt : (TimeSpan?)null;
                var left = buff.ExpiresAt.HasValue ? buff.ExpiresAt.Value - now : (TimeSpan?)null;
                var pct = CalculateFill(total, left);
                var iconOnlyElement = CreateCooldownIcon(definition, buff, pct, total.HasValue, left, true);
                iconOnlyElement.Margin = new Thickness(0, 0, 12, 12);
                iconWrap.Children.Add(iconOnlyElement);
            }

            BuffPanel.Children.Add(iconWrap);
            return;
        }

        foreach (var (buff, definition) in buffs)
        {
            var total = buff.ExpiresAt.HasValue ? buff.ExpiresAt.Value - buff.AppliedAt : (TimeSpan?)null;
            var left = buff.ExpiresAt.HasValue ? buff.ExpiresAt.Value - now : (TimeSpan?)null;
            var pct = CalculateFill(total, left);
			var detailsText = FormatDetails(buff, left);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconElement = CreateCooldownIcon(definition, buff, pct, total.HasValue, left, false);
            row.Children.Add(iconElement);

            var infoPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(infoPanel, 1);

            var header = new DockPanel();
            var nameText = new TextBlock
            {
                Text = buff.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14
            };
            header.Children.Add(nameText);

            var detailText = new TextBlock
            {
                Text = detailsText,
                Foreground = System.Windows.Media.Brushes.LightGray,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                FontSize = 12
            };
            header.Children.Add(detailText);
            infoPanel.Children.Add(header);

            row.Children.Add(infoPanel);
            BuffPanel.Children.Add(row);
        }
    }

    private FrameworkElement CreateCooldownIcon(BuffDefinition? definition, Buff buff, double pct, bool hasTimer, TimeSpan? left, bool iconsOnly)
    {
        const double size = 56;
        const double ringThickness = 6;

        var grid = new Grid
        {
            Width = size,
            Height = size,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
		
		 var displayName = buff.Name;

        var halo = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 30, 144, 255)),
            IsHitTestVisible = false,
            Effect = new DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromArgb(180, 30, 144, 255),
                BlurRadius = 26,
                ShadowDepth = 0,
                Opacity = 0.65
            }
        };
        grid.Children.Add(halo);

        if (hasTimer && pct > 0)
        {
            var ring = new Path
            {
                StrokeThickness = ringThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(ClampToByte(160 + 95 * pct), 135, 206, 250)),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromArgb(ClampToByte(220 * pct), 135, 206, 250),
                    BlurRadius = 30,
                    ShadowDepth = 0,
                    Opacity = Math.Max(0.45, Math.Min(1.0, 0.55 + 0.45 * pct))
                }
            };

            var radius = (size - ringThickness) / 2;
            ring.Data = pct >= 0.999
                ? new EllipseGeometry(new System.Windows.Point(size / 2, size / 2), radius, radius)
                : CreateArcGeometry(pct, radius, new System.Windows.Point(size / 2, size / 2));
            grid.Children.Add(ring);
        }

        var iconBorder = new Border
        {
            Width = size - 12,
            Height = size - 12,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 0)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Padding = new Thickness(4)
        };

        if (definition != null && TryFindResource(definition.IconResourceKey) is ImageSource img)
        {
            iconBorder.Child = new System.Windows.Controls.Image
            {
                Source = img,
                Stretch = Stretch.Uniform
            };
        }
        else
        {
            iconBorder.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(displayName) ? "?" : new string(displayName.Take(2).ToArray()).ToUpperInvariant(),
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
        }

        grid.Children.Add(iconBorder);
		var detailsText = FormatDetails(buff, left);
        if (!string.IsNullOrWhiteSpace(detailsText))
        {
            grid.ToolTip = string.IsNullOrWhiteSpace(displayName) ? detailsText : $"{displayName}\n{detailsText}";
        }
        else if (!string.IsNullOrWhiteSpace(displayName))
        {
            grid.ToolTip = displayName;
        }

        if (iconsOnly)
        {
            var showStackBadge = buff.Stacks > 1 || (definition?.SpellId == WardenOfTheTempleSpellId && buff.Stacks >= 1);
            if (showStackBadge)
            {
                var stackBadge = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 30, 136, 229)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(4, 1, 4, 1),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(2),
                    Effect = CreateTextOutline()
                };
				var stackText = definition?.SpellId == WardenOfTheTempleSpellId
                    ? buff.Stacks.ToString()
                    : $"x{buff.Stacks}";
                stackBadge.Child = new TextBlock
                {
                    Text = stackText,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                grid.Children.Add(stackBadge);
            }

            var timeText = left.HasValue
                ? FormatTime(left.Value < TimeSpan.Zero ? TimeSpan.Zero : left.Value)
                : "∞";
            var timerBlock = new TextBlock
            {
                Text = timeText,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
				FontSize = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
                Effect = CreateTextOutline(10, 1.0)
            };
            grid.Children.Add(timerBlock);
        }

        return grid;
    }
	
	private static DropShadowEffect CreateTextOutline(double blurRadius = 7, double opacity = 0.9) => new()
    {
        Color = System.Windows.Media.Colors.Black,
        BlurRadius = 0,
        ShadowDepth = 0,
        Opacity = opacity,
        RenderingBias = RenderingBias.Quality
    };

	
    private static double CalculateFill(TimeSpan? total, TimeSpan? left)
    {
        if (!total.HasValue)
        {
            return 1.0;
        }

        var totalMs = total.Value.TotalMilliseconds;
        if (totalMs <= 0.0001)
        {
            return left.HasValue && left.Value > TimeSpan.Zero ? 1.0 : 0.0;
        }

        var leftMs = left?.TotalMilliseconds ?? totalMs;
        return Math.Max(0, Math.Min(1, leftMs / totalMs));
    }

    private static string FormatDetails(Buff buff, TimeSpan? left)
    {
        var parts = new List<string>();
        if (buff.Stacks > 1)
        {
            parts.Add($"x{buff.Stacks}");
        }

        if (left.HasValue)
        {
            var display = left.Value < TimeSpan.Zero ? TimeSpan.Zero : left.Value;
            parts.Add(FormatTime(display));
        }
        else
        {
            parts.Add("∞");
        }
        return string.Join(" · ", parts);
    }

    private static string FormatTime(TimeSpan t) => t.TotalSeconds >= 10 ? $"{(int)t.TotalSeconds}s" : $"{t.TotalSeconds:F1}s";

    private static Geometry CreateArcGeometry(double pct, double radius, System.Windows.Point center)
    {
        var angle = pct * 360;
        var startAngle = -90.0;
        var endAngle = startAngle + angle;
        var start = GetPointOnCircle(radius, startAngle, center);
        var end = GetPointOnCircle(radius, endAngle, center);
        var isLarge = angle > 180;

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new System.Windows.Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLarge
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static System.Windows.Point GetPointOnCircle(double radius, double angleDegrees, System.Windows.Point center)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var x = center.X + radius * Math.Cos(radians);
        var y = center.Y + radius * Math.Sin(radians);
        return new System.Windows.Point(x, y);
    }

    private static byte ClampToByte(double value) => (byte)Math.Max(0, Math.Min(255, value));
    private void MakeClickThrough(bool enable)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        if (enable)
        {
            style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        }
        else
        {
            style |= NativeMethods.WS_EX_TOOLWINDOW;
            style &= ~NativeMethods.WS_EX_TRANSPARENT;
        }

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked) return;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (IsWithinResizeGrip(e.OriginalSource))
            {
                return;
            }
            try
            {
                DragMove();
            }
            catch
            {
                // ignored - DragMove may throw if invoked during resize
            }
        }
    }

    private bool IsWithinResizeGrip(object? source)
    {
        if (ResizeGripControl.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (source is not DependencyObject current)
        {
            return false;
        }

        while (current != null)
        {
            if (ReferenceEquals(current, ResizeGripControl))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    }
}