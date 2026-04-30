using System.Collections.ObjectModel;
using AuviWin.Core.Audio;
using AuviWin.Core.Configuration;
using AuviWin.Core.Display;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuviWin.UI.Settings.ViewModels;

public sealed partial class DisplayAudioSyncViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audio;
    private readonly IDisplayService _display;
    private readonly SettingsService _settings;

    public ObservableCollection<AudioDevice> AllDevices { get; } = [];

    // Config A
    [ObservableProperty] private string _configAName = "";
    [ObservableProperty] private string _configAStatus = "Not captured";
    [ObservableProperty] private AudioDevice? _configADevice;

    // Config B
    [ObservableProperty] private string _configBName = "";
    [ObservableProperty] private string _configBStatus = "Not captured";
    [ObservableProperty] private AudioDevice? _configBDevice;

    [ObservableProperty] private string _toggleHotkeyDisplay = "Not set";
    [ObservableProperty] private string _activeConfigStatus = "";

    public DisplayAudioSyncViewModel(IAudioDeviceService audio, IDisplayService display, SettingsService settings)
    {
        _audio = audio;
        _display = display;
        _settings = settings;
    }

    public void Load()
    {
        var devices = _audio.GetActiveRenderDevices().ToList();
        AllDevices.Clear();
        foreach (var d in devices) AllDevices.Add(d);

        var cfg = _settings.Current.DisplayAudioSync;

        // If a saved device is not currently active, add a placeholder so the
        // selection is preserved and visible when that display mode is offline.
        ConfigADevice = LoadDeviceSelection(cfg.ConfigA, devices);
        ConfigBDevice = LoadDeviceSelection(cfg.ConfigB, devices);

        ConfigAName = cfg.ConfigA?.Name ?? "";
        ConfigBName = cfg.ConfigB?.Name ?? "";
        ConfigAStatus = cfg.ConfigA?.DisplaySnapshot is not null ? "Captured" : "Not captured";
        ConfigBStatus = cfg.ConfigB?.DisplaySnapshot is not null ? "Captured" : "Not captured";
        ToggleHotkeyDisplay = cfg.ToggleHotkey?.ToString() ?? "Not set";
        RefreshActiveConfig(cfg);
    }

    private void RefreshActiveConfig(DisplayAudioSyncSettings cfg)
    {
        if (cfg.ConfigA?.DisplaySnapshot is null && cfg.ConfigB?.DisplaySnapshot is null)
        {
            ActiveConfigStatus = "";
            return;
        }
        var current = _display.CaptureCurrentTopology();
        string labelA = string.IsNullOrWhiteSpace(cfg.ConfigA?.Name) ? "Config A" : $"Config A — {cfg.ConfigA.Name}";
        string labelB = string.IsNullOrWhiteSpace(cfg.ConfigB?.Name) ? "Config B" : $"Config B — {cfg.ConfigB.Name}";
        if (cfg.ConfigA?.DisplaySnapshot is not null && current.Matches(cfg.ConfigA.DisplaySnapshot))
            ActiveConfigStatus = $"Currently active: {labelA}";
        else if (cfg.ConfigB?.DisplaySnapshot is not null && current.Matches(cfg.ConfigB.DisplaySnapshot))
            ActiveConfigStatus = $"Currently active: {labelB}";
        else
            ActiveConfigStatus = $"Currently active: Unknown / other  —  toggle will switch to {labelA}";
    }

    partial void OnConfigANameChanged(string value)
    {
        var cfg = _settings.Current.DisplayAudioSync;
        cfg.ConfigA ??= new DisplayAudioConfig();
        cfg.ConfigA.Name = value;
        _settings.Save();
        RefreshActiveConfig(cfg);
    }

    partial void OnConfigBNameChanged(string value)
    {
        var cfg = _settings.Current.DisplayAudioSync;
        cfg.ConfigB ??= new DisplayAudioConfig();
        cfg.ConfigB.Name = value;
        _settings.Save();
        RefreshActiveConfig(cfg);
    }

    private const string UnavailableSuffix = " (unavailable)";

    private AudioDevice? LoadDeviceSelection(DisplayAudioConfig? config, List<AudioDevice> activeDevices)
    {
        if (config?.AudioDeviceId is null) return null;
        var match = activeDevices.FirstOrDefault(d => d.Id == config.AudioDeviceId);
        if (match is not null) return match;
        // Device is offline — reuse an existing placeholder if already added (e.g. ConfigA and ConfigB share the same offline device)
        var existing = AllDevices.FirstOrDefault(d => d.Id == config.AudioDeviceId);
        if (existing is not null) return existing;
        var placeholder = new AudioDevice(config.AudioDeviceId,
            (config.AudioDeviceName ?? config.AudioDeviceId) + UnavailableSuffix);
        AllDevices.Add(placeholder);
        return placeholder;
    }

    [RelayCommand]
    private void CaptureConfigA()
    {
        var snapshot = _display.CaptureCurrentTopology();
        _settings.Current.DisplayAudioSync.ConfigA ??= new DisplayAudioConfig();
        _settings.Current.DisplayAudioSync.ConfigA.DisplaySnapshot = snapshot;
        ConfigAStatus = "Captured";
        _settings.Save();
        RefreshActiveConfig(_settings.Current.DisplayAudioSync);
    }

    [RelayCommand]
    private void CaptureConfigB()
    {
        var snapshot = _display.CaptureCurrentTopology();
        _settings.Current.DisplayAudioSync.ConfigB ??= new DisplayAudioConfig();
        _settings.Current.DisplayAudioSync.ConfigB.DisplaySnapshot = snapshot;
        ConfigBStatus = "Captured";
        _settings.Save();
        RefreshActiveConfig(_settings.Current.DisplayAudioSync);
    }

    partial void OnConfigADeviceChanged(AudioDevice? value)
    {
        if (value is null) return;
        var cfg = _settings.Current.DisplayAudioSync;
        cfg.ConfigA ??= new DisplayAudioConfig();
        cfg.ConfigA.AudioDeviceId = value.Id;
        cfg.ConfigA.AudioDeviceName = value.Name.EndsWith(UnavailableSuffix)
            ? value.Name[..^UnavailableSuffix.Length]
            : value.Name;
        _settings.Save();
    }

    partial void OnConfigBDeviceChanged(AudioDevice? value)
    {
        if (value is null) return;
        var cfg = _settings.Current.DisplayAudioSync;
        cfg.ConfigB ??= new DisplayAudioConfig();
        cfg.ConfigB.AudioDeviceId = value.Id;
        cfg.ConfigB.AudioDeviceName = value.Name.EndsWith(UnavailableSuffix)
            ? value.Name[..^UnavailableSuffix.Length]
            : value.Name;
        _settings.Save();
    }
}
