using System.Windows;
using AuviWin.Core.Audio;
using AuviWin.Core.Configuration;
using AuviWin.Core.Display;
using AuviWin.Core.Hotkeys;
using AuviWin.Features.AudioCycle;
using AuviWin.Features.DisplayAudioSync;
using AuviWin.UI.Settings;
using AuviWin.UI.Settings.ViewModels;
using AuviWin.UI.Tray;
using Microsoft.Extensions.DependencyInjection;

namespace AuviWin;

public partial class App : Application
{
    private ServiceProvider _services = null!;
    private TrayIconManager? _tray;
    private HotkeyService? _hotkeys;
    private AudioCycleFeature? _audioCycle;
    private DisplayAudioSyncFeature? _displaySync;
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
        services.AddSingleton<SettingsViewModel>();

        _services = services.BuildServiceProvider();

        // ── Load settings ─────────────────────────────────────────────────
        var settings = _services.GetRequiredService<SettingsService>();
        settings.Load();

        // ── Start features ────────────────────────────────────────────────
        _hotkeys = (HotkeyService)_services.GetRequiredService<IHotkeyService>();
        _audioCycle = _services.GetRequiredService<AudioCycleFeature>();
        _displaySync = _services.GetRequiredService<DisplayAudioSyncFeature>();
        _audioCycle.ApplySettings();
        _displaySync.ApplySettings();

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
        _hotkeys?.Dispose();
        _services?.Dispose();        base.OnExit(e);
    }
}

