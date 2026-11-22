using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AmfibiaStudio.Controls;

namespace AmfibiaStudio.Converters;

/// <summary>«#RRGGBB» → кисть.</summary>
public class HexBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string hex && hex.Length > 0 ? new SolidColorBrush(ProductArt.ToColor(hex)) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Два цвета палитры бренда → диагональный градиент.</summary>
public class GradientConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string a = values.Length > 0 && values[0] is string s0 ? s0 : "#111112";
        string b = values.Length > 1 && values[1] is string s1 ? s1 : "#1D1D20";
        return new LinearGradientBrush(ProductArt.ToColor(a), ProductArt.ToColor(b), new Point(0, 0), new Point(1, 1));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Ключ шрифта бренда (serif, gothic, grotesk…) → шрифт.</summary>
public class BrandFontConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current?.TryFindResource("BrandFont." + value) as FontFamily
        ?? Application.Current?.TryFindResource("FontDisplayBold") as FontFamily
        ?? new FontFamily("Georgia");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>null, пустая строка или 0 → скрыть, иначе показать. С параметром «invert» — наоборот.</summary>
public class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool empty = value == null || value is string s && s.Length == 0 || value is int n && n == 0
                     || value is System.Collections.ICollection c && c.Count == 0;
        if (parameter as string == "invert") empty = !empty;
        return empty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>bool → видимость. С параметром «invert» — наоборот.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        if (parameter as string == "invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Доля 0…1 → длина в пикселях (параметр — длина при доле 1). Для столбцов диаграмм.</summary>
public class RatioToLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double ratio = value is double d ? d : 0;
        double max = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) ? m : 100;
        return Math.Max(ratio > 0 ? 3 : 0, Math.Clamp(ratio, 0, 1) * max);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Значение равно параметру → true (для выделения текущего шага и т. п.).</summary>
public class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Значение равно параметру → показать, иначе скрыть (переключение вкладок внутри Вида).</summary>
public class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
