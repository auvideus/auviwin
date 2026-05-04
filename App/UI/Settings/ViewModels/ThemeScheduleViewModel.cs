using App.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace App.UI.Settings.ViewModels;

public sealed partial class ThemeScheduleViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private bool _loading;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _darkModeTime = "20:00";
    [ObservableProperty] private string _lightModeTime = "07:00";
    [ObservableProperty] private string _statusText = "";

    public ThemeScheduleViewModel(SettingsService settings)
    {
        _settings = settings;
    }

    public void Load()
    {
        _loading = true;
        try
        {
            var cfg = _settings.Current.ThemeSchedule;
            IsEnabled = cfg.IsEnabled;
            DarkModeTime = cfg.DarkModeTime.ToString("HH:mm");
            LightModeTime = cfg.LightModeTime.ToString("HH:mm");
        }
        finally
        {
            _loading = false;
        }
        RefreshStatus();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.ThemeSchedule.IsEnabled = value;
        _settings.Save();
        RefreshStatus();
        SettingsChanged?.Invoke();
    }

    partial void OnDarkModeTimeChanged(string value)
    {
        if (_loading) return;
        if (!TimeOnly.TryParse(value, out var t)) return;
        _settings.Current.ThemeSchedule.DarkModeTime = t;
        _settings.Save();
        RefreshStatus();
        SettingsChanged?.Invoke();
    }

    partial void OnLightModeTimeChanged(string value)
    {
        if (_loading) return;
        if (!TimeOnly.TryParse(value, out var t)) return;
        _settings.Current.ThemeSchedule.LightModeTime = t;
        _settings.Save();
        RefreshStatus();
        SettingsChanged?.Invoke();
    }

    private void RefreshStatus()
    {
        if (!_settings.Current.ThemeSchedule.IsEnabled)
        {
            StatusText = "Disabled — OS theme will not be changed automatically.";
            return;
        }

        var cfg = _settings.Current.ThemeSchedule;
        var now = TimeOnly.FromDateTime(DateTime.Now);
        bool isDark = Features.ThemeSchedule.ThemeScheduleFeature.IsDarkPeriod(
            now, cfg.DarkModeTime, cfg.LightModeTime);
        StatusText = isDark
            ? $"Dark mode active. Light mode starts at {cfg.LightModeTime:HH:mm}."
            : $"Light mode active. Dark mode starts at {cfg.DarkModeTime:HH:mm}.";
    }

    /// <summary>Raised when any setting changes so the feature can re-apply immediately.</summary>
    public event Action? SettingsChanged;
}
