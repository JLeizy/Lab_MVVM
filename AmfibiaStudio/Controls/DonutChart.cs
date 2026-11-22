using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AmfibiaStudio.ViewModels;

namespace AmfibiaStudio.Controls;

/// <summary>
/// Кольцевая диаграмма долей (статусы заказов на дашборде).
/// Получает доли через привязку Segments — сама ничего не считает.
/// </summary>
public class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IEnumerable), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSegmentsChanged));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(
        nameof(CenterText), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Segments
    {
        get => (IEnumerable?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (DonutChart)d;
        if (e.OldValue is INotifyCollectionChanged oldList) oldList.CollectionChanged -= chart.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newList) newList.CollectionChanged += chart.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 10) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double radius = (size - Thickness) / 2;
        var track = new Pen(Track, Thickness);
        dc.DrawEllipse(null, track, center, radius, radius);

        var segments = Segments?.OfType<SegmentItem>().Where(s => s.Fraction > 0).ToList();
        if (segments != null && segments.Count > 0)
        {
            double start = -90;
            foreach (SegmentItem segment in segments)
            {
                double sweep = segment.Fraction * 360;
                var brush = new SolidColorBrush(ProductArt.ToColor(segment.Color));
                brush.Freeze();
                var pen = new Pen(brush, Thickness);
                pen.Freeze();
                if (sweep >= 359.99)
                {
                    dc.DrawEllipse(null, pen, center, radius, radius);
                }
                else
                {
                    // Небольшой зазор между долями
                    double gap = segments.Count > 1 ? Math.Min(1.2, sweep / 4) : 0;
                    dc.DrawGeometry(null, pen, Arc(center, radius, start + gap / 2, sweep - gap));
                }

                start += sweep;
            }
        }

        if (!string.IsNullOrEmpty(CenterText))
        {
            FontFamily font = Application.Current?.TryFindResource("FontDisplay") as FontFamily ?? new FontFamily("Georgia");
            var text = new FormattedText(CenterText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size * 0.13, Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, size - Thickness * 2 - 8),
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(text, new Point(center.X - text.MaxTextWidth / 2, center.Y - text.Height / 2));
        }
    }

    private static Geometry Arc(Point center, double radius, double startDeg, double sweepDeg)
    {
        Point PointAt(double deg)
        {
            double rad = deg * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        var figure = new PathFigure { StartPoint = PointAt(startDeg), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(PointAt(startDeg + sweepDeg), new Size(radius, radius), 0, sweepDeg > 180,
            SweepDirection.Clockwise, true));
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }
}
