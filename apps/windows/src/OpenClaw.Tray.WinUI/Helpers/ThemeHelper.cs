using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.UI;

namespace OpenClawTray.Helpers;

/// <summary>
/// Helpers for detecting and applying Windows theme (dark/light mode).
/// </summary>
public static class ThemeHelper
{
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    public static ElementTheme GetCurrentTheme()
    {
        return IsDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
    }

    public static Color GetAccentColor()
    {
        // Lobster red accent
        return Color.FromArgb(255, 255, 99, 71);
    }

    public static Color GetBackgroundColor()
    {
        return IsDarkMode() 
            ? Color.FromArgb(255, 32, 32, 32) 
            : Color.FromArgb(255, 249, 249, 249);
    }

    public static Color GetForegroundColor()
    {
        return IsDarkMode()
            ? Color.FromArgb(255, 255, 255, 255)
            : Color.FromArgb(255, 28, 28, 28);
    }

    public static Color GetSubtleTextColor()
    {
        return IsDarkMode()
            ? Color.FromArgb(255, 180, 180, 180)
            : Color.FromArgb(255, 100, 100, 100);
    }
}
