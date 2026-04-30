using AuviWin.Core.Audio;
using AuviWin.Core.Configuration;
using AuviWin.Core.Display;
using AuviWin.Core.Hotkeys;

namespace AuviWin.Features.DisplayAudioSync;

/// <summary>
/// On hotkey: detects which of the two saved display configs is active,
/// then applies the other one (display topology + audio device).
/// If neither matches, applies Config A.
/// </summary>
public sealed class DisplayAudioSyncFeature : IDisposable
{
    private readonly IAudioDeviceService _audio;
    private readonly IDisplayService _display;
    private readonly IHotkeyService _hotkeys;
    private readonly SettingsService _settings;

    private int _toggleId = -1;

    public DisplayAudioSyncFeature(
        IAudioDeviceService audio,
        IDisplayService display,
        IHotkeyService hotkeys,
        SettingsService settings)
    {
        _audio = audio;
        _display = display;
        _hotkeys = hotkeys;
        _settings = settings;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>Optional: called with (title, message) to surface notifications to the user.</summary>
    public Action<string, string>? Notify { get; set; }

    /// <summary>
    /// Like <see cref="Notify"/> but the caller will delay/queue the balloon itself
    /// (used after display topology changes where the shell may restart).
    /// </summary>
    public Action<string, string>? NotifyDeferred { get; set; }

    public void ApplySettings()
    {
        if (_toggleId >= 0) { _hotkeys.Unregister(_toggleId); _toggleId = -1; }

        var cfg = _settings.Current.DisplayAudioSync;
        if (cfg.ToggleHotkey?.IsValid == true)
            try { _toggleId = _hotkeys.Register(cfg.ToggleHotkey); }
            catch (InvalidOperationException) { /* conflict — skip silently */ }
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        if (id != _toggleId) return;
        TryToggle();
    }

    /// <summary>Toggle display+audio config (also callable from tray menu).</summary>
    public void ToggleDisplay() => TryToggle();

    /// <summary>Temporarily unregister hotkeys (e.g. while a hotkey-capture dialog is open).</summary>
    public void Suspend()
    {
        if (_toggleId >= 0) { _hotkeys.Unregister(_toggleId); _toggleId = -1; }
    }

    /// <summary>Re-register hotkeys from current settings (call after dialog closes).</summary>
    public void Resume() => ApplySettings();

    private void TryToggle()
    {
        try { Toggle(); }
        catch (Exception ex)
        {
            Notify?.Invoke("AuviWin — Display toggle failed", ex.Message);
        }
    }

    private void Toggle()
    {
        var cfg = _settings.Current.DisplayAudioSync;
        if (cfg.ConfigA?.DisplaySnapshot is null || cfg.ConfigB?.DisplaySnapshot is null)
        {
            Notify?.Invoke("AuviWin", "Display configs not set up. Open Settings to configure.");
            return;
        }

        var current = _display.CaptureCurrentTopology();
        bool isCurrentlyA = current.Matches(cfg.ConfigA.DisplaySnapshot);
        var target = isCurrentlyA ? cfg.ConfigB : cfg.ConfigA;
        string targetLabel = isCurrentlyA
            ? (string.IsNullOrWhiteSpace(cfg.ConfigB?.Name) ? "Config B" : cfg.ConfigB.Name)
            : (string.IsNullOrWhiteSpace(cfg.ConfigA?.Name) ? "Config A" : cfg.ConfigA.Name);

        if (target.DisplaySnapshot is null)
        {
            Notify?.Invoke("AuviWin", $"{targetLabel} display not captured. Open Settings to configure.");
            return;
        }

        _display.ApplyTopology(target.DisplaySnapshot);

        if (target.AudioDeviceId is not null)
            _audio.SetDefaultRenderDevice(target.AudioDeviceId);

        // Use deferred notification: fires on WM_TASKBARCREATED (shell restart) or
        // falls back after 3 s. This covers both adding and removing monitors.
        (NotifyDeferred ?? Notify)?.Invoke("AuviWin", $"Switched to {targetLabel}");
    }

    public void Dispose()
    {
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        if (_toggleId >= 0) _hotkeys.Unregister(_toggleId);
    }
}
