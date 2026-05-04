using System.Windows;
using App.Core.Audio;
using App.Core.Configuration;
using App.Core.Display;
using App.Core.Hotkeys;
using App.Features.AudioCycle;
using App.Features.DisplayAudioSync;
using App.Features.ThemeSchedule;
using App.UI.Settings;
using App.UI.Settings.ViewModels;
using App.UI.Tray;
using Microsoft.Extensions.DependencyInjection;

namespace App;

public partial class WpfApp : Application
{
    private ServiceProvider _services = null!;
    private TrayIconManager? _tray;
    private HotkeyService? _hotkeys;
    private AudioCycleFeature? _audioCycle;
    private DisplayAudioSyncFeature? _displaySync;
    private ThemeScheduleFeature? _themeSchedule;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // ── Build DI container ────────────────────────────────────────────
        var services = new ServiceCollection();

        services.AddSingleton<SettingsService>();
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<TrayIconManager>();
        services.AddSingleton<AudioCycleFeature>();
        services.AddSingleton<DisplayAudioSyncFeature>();
        services.AddSingleton<AudioCycleViewModel>();
        services.AddSingleton<DisplayAudioSyncViewModel>();
        services.AddSingleton<ThemeScheduleViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ThemeScheduleFeature>();

        _services = services.BuildServiceProvider();

        // ── Load settings ─────────────────────────────────────────────────
        var settings = _services.GetRequiredService<SettingsService>();
        settings.Load();

        // ── Start features ────────────────────────────────────────────────
        _hotkeys = (HotkeyService)_services.GetRequiredService<IHotkeyService>();
        _audioCycle = _services.GetRequiredService<AudioCycleFeature>();
        _displaySync = _services.GetRequiredService<DisplayAudioSyncFeature>();
        _themeSchedule = _services.GetRequiredService<ThemeScheduleFeature>();
        _audioCycle.ApplySettings();
        _displaySync.ApplySettings();
        _themeSchedule.ApplySettings();

        // Wire up ViewModel→Feature for immediate theme application on save
        var themeVm = _services.GetRequiredService<ThemeScheduleViewModel>();
        themeVm.SettingsChanged += () => _themeSchedule.ApplySettings();

        // ── Create tray host window (message-only, invisible) ─────────────
        var host = new TrayHostWindow();
        _tray = _services.GetRequiredService<TrayIconManager>();
        _displaySync.Notify         = (title, msg) => _tray?.ShowNotification(title, msg);
        _displaySync.NotifyDeferred = (title, msg) => _tray?.QueueNotification(title, msg);
        host.TrayIconManager = _tray;
        host.SettingsRequested = ShowSettings;
        host.NextDeviceRequested = () => _audioCycle?.CycleNext();
        host.PreviousDeviceRequested = () => _audioCycle?.CyclePrevious();
        host.ToggleDisplayRequested = () => _displaySync?.ToggleDisplay();
        host.ExitRequested = Shutdown;
        host.Show();
        host.Hide();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = _services!.GetRequiredService<SettingsViewModel>();
        var settings = _services.GetRequiredService<SettingsService>();
        _settingsWindow = SettingsWindow.Create(vm, settings,
            suspendHotkeys: () => { _audioCycle?.Suspend(); _displaySync?.Suspend(); },
            resumeHotkeys:  () => { _audioCycle?.Resume();  _displaySync?.Resume();  });
        _settingsWindow.Closed += (_, _) =>
        {
            // Re-apply feature settings after the window closes; catch hotkey registration conflicts
            try { _audioCycle!.ApplySettings(); } catch { /* hotkey conflict — user needs to reconfigure */ }
            try { _displaySync!.ApplySettings(); } catch { /* hotkey conflict — user needs to reconfigure */ }
            _tray!.UpdateTooltip();
        };
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _audioCycle?.Dispose();
        _displaySync?.Dispose();
        _themeSchedule?.Dispose();
        _hotkeys?.Dispose();
        _services?.Dispose();        base.OnExit(e);
    }
}

