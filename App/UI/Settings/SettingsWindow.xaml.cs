using System.Windows;
using System.Windows.Input;
using AuviWin.Core;
using AuviWin.Core.Hotkeys;
using AuviWin.UI.Settings.ViewModels;

namespace AuviWin.UI.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} Settings";
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _vm.AudioCycle.Load();
        _vm.DisplayAudioSync.Load();
    }

    // ── Hotkey capture helpers ────────────────────────────────────────────────

    private void SetNextHotkey_Click(object sender, RoutedEventArgs e)
        => CaptureHotkey(h =>
        {
            if (IsConflict(h, exclude: "next")) return;
            _vm.AudioCycle.NextHotkeyDisplay = h.ToString();
            _settings.Current.AudioCycle.NextDeviceHotkey = h;
            _settings.Save();
        });

    private void SetPreviousHotkey_Click(object sender, RoutedEventArgs e)
        => CaptureHotkey(h =>
        {
            if (IsConflict(h, exclude: "prev")) return;
            _vm.AudioCycle.PreviousHotkeyDisplay = h.ToString();
            _settings.Current.AudioCycle.PreviousDeviceHotkey = h;
            _settings.Save();
        });

    private void SetToggleHotkey_Click(object sender, RoutedEventArgs e)
        => CaptureHotkey(h =>
        {
            if (IsConflict(h, exclude: "toggle")) return;
            _vm.DisplayAudioSync.ToggleHotkeyDisplay = h.ToString();
            _settings.Current.DisplayAudioSync.ToggleHotkey = h;
            _settings.Save();
        });

    /// <summary>
    /// Returns true (and shows a warning) if <paramref name="h"/> is already assigned
    /// to one of the other hotkey slots.
    /// </summary>
    private bool IsConflict(Hotkey h, string exclude)
    {
        var ac = _settings.Current.AudioCycle;
        var ds = _settings.Current.DisplayAudioSync;
        var conflicts = new (string slot, Hotkey? key)[]
        {
            ("Next audio device",     ac.NextDeviceHotkey),
            ("Previous audio device", ac.PreviousDeviceHotkey),
            ("Toggle display",        ds.ToggleHotkey),
        };
        var skipSlot = exclude switch
        {
            "next"   => "Next audio device",
            "prev"   => "Previous audio device",
            "toggle" => "Toggle display",
            _        => ""
        };
        foreach (var (slot, key) in conflicts)
        {
            if (slot == skipSlot) continue;
            if (key is not null && key.VirtualKey == h.VirtualKey && key.Modifiers == h.Modifiers)
            {
                MessageBox.Show(
                    $"{h} is already assigned to \"{slot}\". Choose a different key combination.",
                    "Hotkey conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
        }
        return false;
    }

    private void CaptureHotkey(Action<Hotkey> onCaptured)
    {
        SuspendHotkeys?.Invoke();
        try
        {
            var dialog = new HotkeyDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.CapturedHotkey is not null)
                onCaptured(dialog.CapturedHotkey);
        }
        finally
        {
            ResumeHotkeys?.Invoke();
        }
    }

    // Injected at construction time via DI
    private AuviWin.Core.Configuration.SettingsService _settings = null!;
    public Action? SuspendHotkeys { get; set; }
    public Action? ResumeHotkeys { get; set; }

    public static SettingsWindow Create(
        SettingsViewModel vm,
        AuviWin.Core.Configuration.SettingsService settings,
        Action? suspendHotkeys = null,
        Action? resumeHotkeys = null)
    {
        var win = new SettingsWindow(vm);
        win._settings = settings;
        win.SuspendHotkeys = suspendHotkeys;
        win.ResumeHotkeys = resumeHotkeys;
        return win;
    }
}
