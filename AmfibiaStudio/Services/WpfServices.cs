using System;
using System.Linq;
using System.Windows;
using AmfibiaStudio.Controls;
using AmfibiaStudio.Views;

namespace AmfibiaStudio.Services;

/// <summary>
/// Смена темы без перезапуска: первый словарь ресурсов приложения (цвета) заменяется другим.
/// Все стили берут цвета через DynamicResource, поэтому окна перерисовываются сразу.
/// </summary>
public class ThemeService : IThemeService
{
    public static bool IsDark { get; private set; } = true;

    public void Apply(string theme)
    {
        string file = theme switch
        {
            "light" => "Colors.Light.xaml",
            "graphite" => "Colors.Graphite.xaml",
            _ => "Colors.Amfibia.xaml"
        };

        Application app = Application.Current;
        if (app == null) return;

        var colors = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{file}", UriKind.Absolute) };
        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = colors;
        else merged.Insert(0, colors);

        IsDark = theme != "light";
        foreach (Window window in app.Windows.OfType<Window>())
        {
            WindowTheme.SetDark(window, IsDark);
        }
    }
}

/// <summary>Окно подтверждения в стиле приложения (ViewModel видит только IDialogService).</summary>
public class DialogService : IDialogService
{
    public bool Confirm(string title, string message, string confirmText)
    {
        var dialog = new ConfirmWindow(title, message, confirmText);
        Window? owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
        if (owner != null && owner != dialog && owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
