using AuviWin.UI.Settings.ViewModels;

namespace AuviWin.UI.Settings.ViewModels;

/// <summary>Root view model — DataContext for SettingsWindow.</summary>
public sealed class SettingsViewModel(AudioCycleViewModel audioCycle, DisplayAudioSyncViewModel displayAudioSync)
{
    public AudioCycleViewModel AudioCycle { get; } = audioCycle;
    public DisplayAudioSyncViewModel DisplayAudioSync { get; } = displayAudioSync;
}
