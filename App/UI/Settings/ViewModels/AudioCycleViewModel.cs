using System.Collections.ObjectModel;
using App.Core.Audio;
using App.Core.Configuration;
using App.Core.Display;
using App.Core.Hotkeys;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.UI.Settings.ViewModels;

public sealed partial class AudioCycleViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audio;
    private readonly SettingsService _settings;

    public ObservableCollection<SelectableDevice> AllDevices { get; } = [];

    [ObservableProperty] private string _nextHotkeyDisplay = "Not set";
    [ObservableProperty] private string _previousHotkeyDisplay = "Not set";

    public AudioCycleViewModel(IAudioDeviceService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;
    }

    public void Load()
    {
        var cfg = _settings.Current.AudioCycle;
        var allDevices = _audio.GetActiveRenderDevices().ToDictionary(d => d.Id);

        foreach (var d in AllDevices)
            d.PropertyChanged -= OnDevicePropertyChanged;
        AllDevices.Clear();

        // First: enabled devices in their saved order
        foreach (var id in cfg.EnabledDeviceIds)
        {
            if (allDevices.TryGetValue(id, out var d))
                AllDevices.Add(new SelectableDevice(d, true));
        }

        // Then: remaining active devices not in the enabled set
        var enabledSet = new HashSet<string>(cfg.EnabledDeviceIds);
        foreach (var d in allDevices.Values)
        {
            if (!enabledSet.Contains(d.Id))
                AllDevices.Add(new SelectableDevice(d, false));
        }

        NextHotkeyDisplay = cfg.NextDeviceHotkey?.ToString() ?? "Not set";
        PreviousHotkeyDisplay = cfg.PreviousDeviceHotkey?.ToString() ?? "Not set";

        foreach (var d in AllDevices)
            d.PropertyChanged += OnDevicePropertyChanged;
    }

    private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => SaveDevices();

    private void SaveDevices()
    {
        var cfg = _settings.Current.AudioCycle;
        cfg.EnabledDeviceIds = AllDevices
            .Where(d => d.IsEnabled)
            .Select(d => d.Device.Id)
            .ToList();
        _settings.Save();
    }

    [RelayCommand]
    private void MoveUp(SelectableDevice? device)
    {
        if (device is null) return;
        var idx = AllDevices.IndexOf(device);
        if (idx > 0) { AllDevices.Move(idx, idx - 1); SaveDevices(); }
    }

    [RelayCommand]
    private void MoveDown(SelectableDevice? device)
    {
        if (device is null) return;
        var idx = AllDevices.IndexOf(device);
        if (idx < AllDevices.Count - 1) { AllDevices.Move(idx, idx + 1); SaveDevices(); }
    }
}

public sealed partial class SelectableDevice(AudioDevice device, bool enabled) : ObservableObject
{
    public AudioDevice Device { get; } = device;
    public string Name => Device.Name;

    [ObservableProperty] private bool _isEnabled = enabled;
}
