using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels;

namespace AmfibiaStudio;

/// <summary>
/// Точка сборки MVVM: создаются Модель (данные сайта и хранилище), сервисы и корневая ViewModel.
/// Окно получает ViewModel через DataContext — дальше всё связано привязками и командами.
/// </summary>
public partial class App : Application
{
    private static string CrashLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AMFIBIA", "Lab_MVVM", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Любая непредвиденная ошибка записывается в crash.log, программа не закрывается
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        // МОДЕЛЬ: данные сайта AMFIBIA + изменения в %AppData%\AMFIBIA\Lab_MVVM\studio.json
        var context = new StudioContext(StudioData.LoadEmbedded(), StudioStore.CreateDefault(),
                                        new DialogService(), new ThemeService(), new NbrbRatesProvider());

        // ВИД + VIEWMODEL: окно и его DataContext
        var window = new MainWindow { DataContext = new MainViewModel(context) };
        MainWindow = window;
        window.Show();
    }

    private static void LogCrash(Exception? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // записать не удалось — ничего страшного
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.Handled = true;
        MessageBox.Show($"Произошла ошибка: {e.Exception.Message}\n\nПодробности записаны в {CrashLogPath}",
                        "AMFIBIA Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
