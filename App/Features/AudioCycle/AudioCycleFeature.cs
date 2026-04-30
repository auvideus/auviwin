using App.Core.Audio;
using App.Core.Configuration;
using App.Core.Hotkeys;

namespace App.Features.AudioCycle;

/// <summary>
/// Listens for Next/Previous hotkeys and cycles the default render device
/// through the user-configured subset.
/// </summary>
public sealed class AudioCycleFeature : IDisposable
{
    private readonly IAudioDeviceService _audio;
    private readonly IHotkeyService _hotkeys;
    private readonly SettingsService _settings;

    private int _nextId = -1;
    private int _prevId = -1;

    public AudioCycleFeature(IAudioDeviceService audio, IHotkeyService hotkeys, SettingsService settings)
    {
        _audio = audio;
        _hotkeys = hotkeys;
        _settings = settings;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    public void ApplySettings()
    {
        // Unregister old hotkeys
        if (_nextId >= 0) { _hotkeys.Unregister(_nextId); _nextId = -1; }
        if (_prevId >= 0) { _hotkeys.Unregister(_prevId); _prevId = -1; }

        var cfg = _settings.Current.AudioCycle;

        if (cfg.NextDeviceHotkey?.IsValid == true)
            try { _nextId = _hotkeys.Register(cfg.NextDeviceHotkey); }
            catch (InvalidOperationException) { /* conflict — skip silently */ }

        if (cfg.PreviousDeviceHotkey?.IsValid == true)
            try { _prevId = _hotkeys.Register(cfg.PreviousDeviceHotkey); }
            catch (InvalidOperationException) { /* conflict — skip silently */ }
    }

    /// <summary>Cycle to the next device (also callable from tray menu).</summary>
    public void CycleNext() => TryCycleDevice(forward: true);

    /// <summary>Temporarily unregister hotkeys (e.g. while a hotkey-capture dialog is open).</summary>
    public void Suspend()
    {
        if (_nextId >= 0) { _hotkeys.Unregister(_nextId); _nextId = -1; }
        if (_prevId >= 0) { _hotkeys.Unregister(_prevId); _prevId = -1; }
    }

    /// <summary>Re-register hotkeys from current settings (call after dialog closes).</summary>
    public void Resume() => ApplySettings();

    /// <summary>Cycle to the previous device (also callable from tray menu).</summary>
    public void CyclePrevious() => TryCycleDevice(forward: false);

    private void OnHotkeyPressed(object? sender, int id)
    {
        if (id == _nextId) TryCycleDevice(forward: true);
        else if (id == _prevId) TryCycleDevice(forward: false);
    }

    private void TryCycleDevice(bool forward)
    {
        try { CycleDevice(forward); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AudioCycle] {ex.Message}"); }
    }

    private void CycleDevice(bool forward)
    {
        var enabledIds = _settings.Current.AudioCycle.EnabledDeviceIds;
        if (enabledIds.Count == 0) return;

        var current = _audio.GetDefaultRenderDevice();
        int currentIndex = current is null ? -1 : enabledIds.IndexOf(current.Id);

        int nextIndex = forward
            ? (currentIndex + 1) % enabledIds.Count
            : (currentIndex - 1 + enabledIds.Count) % enabledIds.Count;

        // If current device isn't in the subset, always jump to index 0
        if (currentIndex < 0) nextIndex = 0;

        _audio.SetDefaultRenderDevice(enabledIds[nextIndex]);
    }

    public void Dispose()
    {
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        if (_nextId >= 0) _hotkeys.Unregister(_nextId);
        if (_prevId >= 0) _hotkeys.Unregister(_prevId);
    }
}
