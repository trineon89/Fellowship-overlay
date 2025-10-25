using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Fellowship_overlay.Core;

namespace Fellowship_overlay;

public partial class OverlayWindow : Window
{
    private OverlaySettings _settings;
    private bool _isLocked = true;

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

        foreach (var (buff, definition) in buffs)
        {
            var total = (buff.ExpiresAt?.Subtract(buff.AppliedAt)).GetValueOrDefault(TimeSpan.FromSeconds(1));
            var left = buff.ExpiresAt.HasValue ? buff.ExpiresAt.Value - now : (TimeSpan?)null;
            var pct = left.HasValue ? Math.Max(0, Math.Min(1, left.Value.TotalMilliseconds / total.TotalMilliseconds)) : 1.0;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconContainer = CreateIcon(definition, buff.Name);
            row.Children.Add(iconContainer);

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
                Text = FormatDetails(buff, left),
                Foreground = System.Windows.Media.Brushes.LightGray,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                FontSize = 12
            };
            header.Children.Add(detailText);
            infoPanel.Children.Add(header);

            var bg = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Height = 8,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var bar = new System.Windows.Shapes.Rectangle
            {
                Height = 8,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(126, 195, 255)),
                RadiusX = 4,
                RadiusY = 4,
                Width = Math.Max(0, pct * 220)
            };

            bg.Child = bar;
            infoPanel.Children.Add(bg);

            row.Children.Add(infoPanel);
            BuffPanel.Children.Add(row);
        }
    }

    private UIElement CreateIcon(BuffDefinition? definition, string fallbackName)
    {
        var container = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = null
        };

        if (definition != null && TryFindResource(definition.IconResourceKey) is ImageSource img)
        {
            container.Child = new System.Windows.Controls.Image
            {
                Source = img,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(6)
            };
        }
        else
        {
            container.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(fallbackName) ? "?" : new string(fallbackName.Take(2).ToArray()).ToUpperInvariant(),
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
        }

        return container;
    }

    private static string FormatDetails(Buff buff, TimeSpan? left)
    {
        var parts = new List<string>();
        if (buff.Stacks > 1)
        {
            parts.Add($"x{buff.Stacks}");
        }

        parts.Add(left.HasValue ? FormatTime(left.Value) : "∞");
        return string.Join(" · ", parts);
    }

    private static string FormatTime(TimeSpan t) => t.TotalSeconds >= 10 ? $"{(int)t.TotalSeconds}s" : $"{t.TotalSeconds:F1}s";

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