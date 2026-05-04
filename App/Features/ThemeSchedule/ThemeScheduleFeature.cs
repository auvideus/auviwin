using System.Runtime.InteropServices;
using App.Core.Configuration;
using Microsoft.Win32;

namespace App.Features.ThemeSchedule;

/// <summary>
/// Switches the Windows OS color theme (dark/light) automatically at configured times.
/// Uses a one-minute polling timer; applies the correct theme immediately on startup
/// and whenever settings change.
/// </summary>
public sealed class ThemeScheduleFeature : IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly SettingsService _settings;
    private readonly System.Threading.Timer _timer;

    public ThemeScheduleFeature(SettingsService settings)
    {
        _settings = settings;
        // Check every 30 seconds; fire immediately on startup.
        _timer = new System.Threading.Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    /// <summary>Re-evaluates the schedule and applies the correct theme immediately.</summary>
    public void ApplySettings()
    {
        var cfg = _settings.Current.ThemeSchedule;
        if (cfg.IsEnabled)
            ApplyThemeForTime(TimeOnly.FromDateTime(DateTime.Now), cfg);
    }

    private void Tick(object? _)
    {
        var cfg = _settings.Current.ThemeSchedule;
        if (!cfg.IsEnabled) return;
        ApplyThemeForTime(TimeOnly.FromDateTime(DateTime.Now), cfg);
    }

    /// <summary>Determines whether the current time should be dark or light mode.</summary>
    private static void ApplyThemeForTime(TimeOnly now, ThemeScheduleSettings cfg)
    {
        bool isDark = IsDarkPeriod(now, cfg.DarkModeTime, cfg.LightModeTime);
        SetDarkMode(isDark);
    }

    /// <summary>
    /// Returns true when <paramref name="now"/> falls in the dark-mode window.
    /// Dark period runs from <paramref name="darkTime"/> until <paramref name="lightTime"/>
    /// (wrapping midnight if darkTime &gt; lightTime).
    /// </summary>
    internal static bool IsDarkPeriod(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)
    {
        if (darkTime == lightTime) return false;

        if (darkTime < lightTime)
        {
            // Dark window doesn't cross midnight: e.g. 20:00 → 07:00 next day
            // is split, so the simpler "same-day dark" case would be 20:00 → 23:59
            // and 00:00 → 07:00.  Handle by checking if we're OUTSIDE light window.
            // Actually: dark < light means dark window does NOT cross midnight.
            // e.g. dark=22:00 light=23:00 → dark from 22 to 23.
            // Typical: dark=20:00 light=07:00 → dark > light, handled below.
            return now >= darkTime && now < lightTime;
        }
        else
        {
            // Dark window crosses midnight: e.g. dark=20:00, light=07:00
            // Dark period: 20:00–midnight and midnight–07:00
            return now >= darkTime || now < lightTime;
        }
    }

    private static void SetDarkMode(bool dark)
    {
        int value = dark ? 0 : 1; // 0 = dark, 1 = light
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: true);
            if (key is null) return;
            key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);

            // Broadcast WM_SETTINGCHANGE so Explorer and apps pick up the change immediately.
            BroadcastSettingChange();
        }
        catch
        {
            // Registry access failure — silently ignore (e.g. policy restrictions).
        }
    }

    private static void BroadcastSettingChange()
    {
        const uint WM_SETTINGCHANGE = 0x001A;
        const nint HWND_BROADCAST = 0xFFFF;
        const uint SMTO_ABORTIFHUNG = 0x0002;

        var param = "ImmersiveColorSet";
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
            param, SMTO_ABORTIFHUNG, 100, out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern nint SendMessageTimeout(
        nint hWnd, uint msg, nint wParam, string lParam,
        uint fuFlags, uint uTimeout, out nint lpdwResult);

    public void Dispose() => _timer.Dispose();
}
