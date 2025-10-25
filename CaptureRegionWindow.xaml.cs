using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fellowship_overlay;

public partial class CaptureRegionWindow : Window
{
    private Point? _dragStart;
    private Rect? _selectedRect;
    private Matrix _transformToDevice = Matrix.Identity;

    public Rect? SelectedRegion => _selectedRect;

    public CaptureRegionWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _transformToDevice = source.CompositionTarget.TransformToDevice;
        }

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        _dragStart = e.GetPosition(this);
        SelectionRectangle.Visibility = Visibility.Visible;
        UpdateSelection(e.GetPosition(this));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart.HasValue)
        {
            UpdateSelection(e.GetPosition(this));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart.HasValue)
        {
            UpdateSelection(e.GetPosition(this));
            ReleaseMouseCapture();
            _dragStart = null;

            if (SelectionRectangle.Width > 4 && SelectionRectangle.Height > 4)
            {
                var rect = new Rect(Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle), SelectionRectangle.Width, SelectionRectangle.Height);
                var topLeft = _transformToDevice.Transform(rect.TopLeft);
                var bottomRight = _transformToDevice.Transform(rect.BottomRight);
                _selectedRect = new Rect(topLeft, bottomRight);
                DialogResult = true;
            }
            else
            {
                SelectionRectangle.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateSelection(Point current)
    {
        if (!_dragStart.HasValue)
        {
            return;
        }

        var start = _dragStart.Value;
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(start.X - current.X);
        var height = Math.Abs(start.Y - current.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            _dragStart = null;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            DialogResult = false;
        }
    }
}