using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AmfibiaStudio.Controls;

/// <summary>Тёмный или светлый заголовок окна Windows 10/11 — в тон выбранной теме.</summary>
public static class WindowTheme
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void SetDark(Window window, bool dark)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => SetDark(window, dark);
            return;
        }

        try
        {
            int enabled = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Старая Windows без DWM — остаётся стандартный заголовок
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
