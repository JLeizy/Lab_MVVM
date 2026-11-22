using System.Windows;
using AmfibiaStudio.Controls;
using AmfibiaStudio.Services;

namespace AmfibiaStudio;

/// <summary>
/// Главное окно. В MVVM код окна почти пуст: всё, что оно показывает, берётся из ViewModel через привязки.
/// Здесь только то, что относится к самому окну: цвет заголовка и размер под экран.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowTheme.SetDark(this, ThemeService.IsDark);

        // На небольших экранах (1366×768) окно сразу разворачивается
        Rect area = SystemParameters.WorkArea;
        if (area.Width < Width + 40 || area.Height < Height + 40)
        {
            WindowState = WindowState.Maximized;
        }
    }
}
