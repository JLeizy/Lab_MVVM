using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AmfibiaStudio.Models;

namespace AmfibiaStudio.Controls;

/// <summary>
/// Рисунок товара — как SVG-заглушки сайта (scripts/placeholder-art.js):
/// градиент в цветах бренда, название бренда водяным знаком и силуэт вещи
/// цветом самого товара. Фотографии не используются: они принадлежат правообладателям.
/// </summary>
public class ProductArt : FrameworkElement
{
    public static readonly DependencyProperty ProductProperty = DependencyProperty.Register(
        nameof(Product), typeof(Product), typeof(ProductArt),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowWatermarkProperty = DependencyProperty.Register(
        nameof(ShowWatermark), typeof(bool), typeof(ProductArt),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<string, Geometry> GeometryCache = new();

    public Product? Product
    {
        get => (Product?)GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    public bool ShowWatermark
    {
        get => (bool)GetValue(ShowWatermarkProperty);
        set => SetValue(ShowWatermarkProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 2 || h < 2)
        {
            return;
        }

        Product? product = Product;
        BrandPalette palette = product?.BrandInfo?.Palette ?? new BrandPalette();
        var rect = new Rect(0, 0, w, h);
        dc.PushClip(new RectangleGeometry(rect));

        // 1. Фон — градиент в цветах бренда
        var background = new LinearGradientBrush(ToColor(palette.Bg), ToColor(palette.Bg2), new Point(0, 0), new Point(1, 1));
        background.Freeze();
        dc.DrawRectangle(background, null, rect);

        // 2. Водяной знак — название бренда шрифтом бренда
        if (product != null && ShowWatermark)
        {
            var fg = new SolidColorBrush(ToColor(palette.Fg)) { Opacity = 0.07 };
            fg.Freeze();
            var text = new FormattedText(product.Brand.ToUpperInvariant(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(BrandFont(palette.Font), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                h * 0.30, fg, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(w * 0.04, h - text.Height * 0.92));
        }

        // 3. Силуэт вещи цветом товара
        if (product != null && product.Shapes.Count > 0)
        {
            double size = Math.Min(w, h) * 0.66;
            double scale = size / 200.0;
            Brush fill = FillBrush(product);
            var ink = new SolidColorBrush(ToColor(product.InkHex));
            ink.Freeze();
            double strokePx = Math.Max(1.2, 1.5 * Math.Min(w, h) / 260.0);

            dc.PushTransform(new TranslateTransform((w - size) / 2, (h - size) / 2 - h * 0.04));
            dc.PushTransform(new ScaleTransform(scale, scale));
            foreach (SilhouetteShape shape in product.Shapes)
            {
                Geometry geometry = GetGeometry(shape);
                var pen = new Pen(ink, strokePx * shape.Width / scale)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                if (shape.Dash)
                {
                    pen.DashStyle = new DashStyle(new[] { 2.0, 2.0 }, 0);
                }

                pen.Freeze();
                dc.DrawGeometry(shape.Fill ? fill : null, pen, geometry);
                if (shape.Fill)
                {
                    dc.DrawGeometry(Sheen, null, geometry);
                }
            }

            dc.Pop();
            dc.Pop();
        }

        // 4. Тонкая линия акцентного цвета бренда снизу
        var accent = new SolidColorBrush(ToColor(palette.Accent)) { Opacity = 0.85 };
        accent.Freeze();
        dc.DrawRectangle(accent, null, new Rect(0, h - 3, w, 3));
        dc.Pop();
    }

    private static readonly Brush Sheen = CreateSheen();

    private static Brush CreateSheen()
    {
        var brush = new LinearGradientBrush(Color.FromArgb(46, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0.7, 0.9));
        brush.Freeze();
        return brush;
    }

    private static Brush FillBrush(Product product)
    {
        if (product.ColorHex == null)
        {
            // Разноцветный товар
            var multi = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(ToColor("#B3122E"), 0), new GradientStop(ToColor("#E07B2A"), 0.25),
                    new GradientStop(ToColor("#F2D21B"), 0.5), new GradientStop(ToColor("#2F5D46"), 0.75),
                    new GradientStop(ToColor("#3557A0"), 1)
                }
            };
            multi.Freeze();
            return multi;
        }

        var solid = new SolidColorBrush(ToColor(product.ColorHex));
        solid.Freeze();
        return solid;
    }

    private static Geometry GetGeometry(SilhouetteShape shape)
    {
        // В SVG по умолчанию правило заливки nonzero (F1), evenodd — F0
        string key = (shape.EvenOdd ? "F0 " : "F1 ") + shape.D;
        if (!GeometryCache.TryGetValue(key, out Geometry? geometry))
        {
            geometry = Geometry.Parse(key);
            geometry.Freeze();
            GeometryCache[key] = geometry;
        }

        return geometry;
    }

    private static FontFamily BrandFont(string key) =>
        Application.Current?.TryFindResource("BrandFont." + key) as FontFamily
        ?? Application.Current?.TryFindResource("FontDisplayBold") as FontFamily
        ?? new FontFamily("Georgia");

    public static Color ToColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return Colors.Gray;
        }
    }
}
