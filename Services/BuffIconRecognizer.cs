using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public sealed class BuffIconRecognizer
{
    private const int GridSize = 4;
    private const double MatchThreshold = 0.18;

    private static readonly Lazy<BuffIconRecognizer> _instance = new(() => new BuffIconRecognizer());

    public static BuffIconRecognizer Instance => _instance.Value;

    private readonly List<TemplateFingerprint> _templates = new();
    private int _templateWidth;
    private int _templateHeight;

    private BuffIconRecognizer()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var definition in BuffCatalog.Buffs)
            {
                if (Application.Current.TryFindResource(definition.IconResourceKey) is not ImageSource source)
                {
                    continue;
                }

                var bitmap = ToBitmapSource(source);
                if (bitmap == null)
                {
                    continue;
                }

                var formatted = EnsureFormat(bitmap, PixelFormats.Pbgra32);
                var stride = formatted.PixelWidth * (formatted.Format.BitsPerPixel / 8);
                var pixels = new byte[stride * formatted.PixelHeight];
                formatted.CopyPixels(pixels, stride, 0);
                var fingerprint = BuildFingerprint(pixels, formatted.PixelWidth, formatted.PixelHeight, stride);

                _templates.Add(new TemplateFingerprint(definition, fingerprint, formatted.PixelWidth, formatted.PixelHeight));
                if (_templateWidth == 0 || _templateHeight == 0)
                {
                    _templateWidth = formatted.PixelWidth;
                    _templateHeight = formatted.PixelHeight;
                }
            }
        });
    }

    public IReadOnlyList<RecognizedBuff> Recognize(Bitmap capture, IEnumerable<int> trackedSpellIds)
    {
        if (capture == null)
        {
            return Array.Empty<RecognizedBuff>();
        }

        if (_templateWidth == 0 || _templateHeight == 0)
        {
            return Array.Empty<RecognizedBuff>();
        }

        var trackedSet = trackedSpellIds?.Any() == true ? new HashSet<int>(trackedSpellIds) : null;
        var rect = new Rectangle(0, 0, capture.Width, capture.Height);
        var data = capture.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return Recognize(bytes, capture.Width, capture.Height, data.Stride, trackedSet);
        }
        finally
        {
            capture.UnlockBits(data);
        }
    }

    private IReadOnlyList<RecognizedBuff> Recognize(byte[] pixels, int width, int height, int stride, HashSet<int>? trackedSpellIds)
    {
        if (width < _templateWidth || height < _templateHeight)
        {
            return Array.Empty<RecognizedBuff>();
        }

        var resultCounts = new Dictionary<int, (BuffDefinition Definition, int Count)>();
        var columns = Math.Max(1, width / _templateWidth);
        var rows = Math.Max(1, height / _templateHeight);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var offsetX = col * _templateWidth;
                var offsetY = row * _templateHeight;
                if (offsetX + _templateWidth > width || offsetY + _templateHeight > height)
                {
                    continue;
                }

                var fingerprint = BuildFingerprint(pixels, width, height, stride, offsetX, offsetY, _templateWidth, _templateHeight);
                var match = FindBestMatch(fingerprint, trackedSpellIds);
                if (match == null)
                {
                    continue;
                }

                if (!resultCounts.TryGetValue(match.Value.Template.Definition.SpellId, out var entry))
                {
                    entry = (match.Value.Template.Definition, 0);
                }

                if (match.Value.Distance <= MatchThreshold)
                {
                    entry.Count += 1;
                    resultCounts[match.Value.Template.Definition.SpellId] = entry;
                }
            }
        }

        return resultCounts.Values
            .Select(entry => new RecognizedBuff(entry.Definition, entry.Count))
            .ToArray();
    }

    private MatchResult? FindBestMatch(double[] fingerprint, HashSet<int>? tracked)
    {
        MatchResult? best = null;
        foreach (var template in _templates)
        {
            if (tracked != null && !tracked.Contains(template.Definition.SpellId))
            {
                continue;
            }

            var distance = ComputeDistance(fingerprint, template.Features);
            if (best == null || distance < best.Value.Distance)
            {
                best = new MatchResult(template, distance);
            }
        }

        return best;
    }

    private static double[] BuildFingerprint(byte[] pixels, int width, int height, int stride, int offsetX = 0, int offsetY = 0, int tileWidth = -1, int tileHeight = -1)
    {
        if (tileWidth <= 0)
        {
            tileWidth = width;
        }

        if (tileHeight <= 0)
        {
            tileHeight = height;
        }

        var features = new double[GridSize * GridSize * 3];
        int index = 0;
        for (int gy = 0; gy < GridSize; gy++)
        {
            for (int gx = 0; gx < GridSize; gx++)
            {
                var startX = offsetX + gx * tileWidth / GridSize;
                var endX = offsetX + (gx + 1) * tileWidth / GridSize;
                var startY = offsetY + gy * tileHeight / GridSize;
                var endY = offsetY + (gy + 1) * tileHeight / GridSize;

                double sumR = 0, sumG = 0, sumB = 0;
                int count = 0;

                for (int y = startY; y < endY; y++)
                {
                    if (y >= height)
                    {
                        break;
                    }

                    var rowOffset = y * stride;
                    for (int x = startX; x < endX; x++)
                    {
                        if (x >= width)
                        {
                            break;
                        }

                        var pixelOffset = rowOffset + x * 4;
                        var b = pixels[pixelOffset];
                        var g = pixels[pixelOffset + 1];
                        var r = pixels[pixelOffset + 2];
                        sumR += r;
                        sumG += g;
                        sumB += b;
                        count++;
                    }
                }

                if (count == 0)
                {
                    features[index++] = 0;
                    features[index++] = 0;
                    features[index++] = 0;
                }
                else
                {
                    features[index++] = (sumR / count) / 255.0;
                    features[index++] = (sumG / count) / 255.0;
                    features[index++] = (sumB / count) / 255.0;
                }
            }
        }

        return features;
    }

    private static double ComputeDistance(double[] left, double[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        double sum = 0;
        for (int i = 0; i < length; i++)
        {
            var diff = left[i] - right[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum / length);
    }

    private static BitmapSource? ToBitmapSource(ImageSource source)
    {
        switch (source)
        {
            case BitmapSource bitmapSource:
                return bitmapSource;
            case DrawingImage drawingImage:
                var bounds = drawingImage.Drawing.Bounds;
                var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
                var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawImage(source, new Rect(0, 0, width, height));
                }
                var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                render.Render(visual);
                render.Freeze();
                return render;
            default:
                return null;
        }
    }

    private static BitmapSource EnsureFormat(BitmapSource source, PixelFormat format)
    {
        if (source.Format == format)
        {
            source.Freeze();
            return source;
        }

        var formatted = new FormatConvertedBitmap(source, format, null, 0);
        formatted.Freeze();
        return formatted;
    }

    private readonly record struct TemplateFingerprint(BuffDefinition Definition, double[] Features, int Width, int Height);

    private readonly record struct MatchResult(TemplateFingerprint Template, double Distance);
}

public sealed record RecognizedBuff(BuffDefinition Definition, int Stacks);