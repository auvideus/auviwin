using App.Core.Display;
using App.Core.Hotkeys;

namespace App.Core.Configuration;

public sealed class AppSettings
{
    public AudioCycleSettings AudioCycle { get; set; } = new();
    public DisplayAudioSyncSettings DisplayAudioSync { get; set; } = new();
    public ThemeScheduleSettings ThemeSchedule { get; set; } = new();
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

public sealed class ThemeScheduleSettings
{
    public bool IsEnabled { get; set; }
    /// <summary>Time of day to switch to dark mode (HH:mm, 24-hour).</summary>
    public TimeOnly DarkModeTime { get; set; } = new TimeOnly(20, 0);
    /// <summary>Time of day to switch to light mode (HH:mm, 24-hour).</summary>
    public TimeOnly LightModeTime { get; set; } = new TimeOnly(7, 0);
}

public sealed class DisplayAudioConfig
{
    public string? Name { get; set; }
    public string? AudioDeviceId { get; set; }
    public string? AudioDeviceName { get; set; }
    public DisplayTopologySnapshot? DisplaySnapshot { get; set; }
}
