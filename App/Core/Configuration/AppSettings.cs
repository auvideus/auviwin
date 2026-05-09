using App.Core.Display;
using App.Core.Hotkeys;

namespace App.Core.Configuration;

public sealed class AppSettings
{
    public AudioCycleSettings AudioCycle { get; set; } = new();
    public DisplayAudioSyncSettings DisplayAudioSync { get; set; } = new();
}

public sealed class AudioCycleSettings
{
    public Hotkey? NextDeviceHotkey { get; set; }
    public Hotkey? PreviousDeviceHotkey { get; set; }
    /// <summary>Ordered list of device IDs in the cycle.</summary>
    public List<string> EnabledDeviceIds { get; set; } = [];
}

public sealed class DisplayAudioSyncSettings
{
    public Hotkey? ToggleHotkey { get; set; }
    public DisplayAudioConfig? ConfigA { get; set; }
    public DisplayAudioConfig? ConfigB { get; set; }
}

public sealed class DisplayAudioConfig
{
    public string? Name { get; set; }
    public string? AudioDeviceId { get; set; }
    public string? AudioDeviceName { get; set; }
    public DisplayTopologySnapshot? DisplaySnapshot { get; set; }
}
