using System.Windows;
using System.Windows.Input;
using AuviWin.Core.Hotkeys;
using HotkeyModifiers = AuviWin.Core.Hotkeys.ModifierKeys;

namespace AuviWin.UI.Settings;

public sealed partial class HotkeyDialog : Window
{
    public Hotkey? CapturedHotkey { get; private set; }

    public HotkeyDialog()
    {
        InitializeComponent();
        ContentRendered += (_, _) => HotkeyBox.Focus();
    }

    private void HotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // When Alt is held, WPF routes the event with e.Key = Key.System and
        // the real key in e.SystemKey. Unwrap it so Ctrl+Alt+H works correctly.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore pure modifier key presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var wpfMods = Keyboard.Modifiers;
        var mods = HotkeyModifiers.None;
        if (wpfMods.HasFlag(System.Windows.Input.ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (wpfMods.HasFlag(System.Windows.Input.ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (wpfMods.HasFlag(System.Windows.Input.ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (wpfMods.HasFlag(System.Windows.Input.ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        if (mods == HotkeyModifiers.None)
        {
            // Allow bare function keys (F1–F24); reject bare regular keys to avoid blocking typing
            bool isFunctionKey = key >= Key.F1 && key <= Key.F24;
            if (!isFunctionKey) return;
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var hotkey = new Hotkey(mods, vk);
        CapturedHotkey = hotkey;
        HotkeyBox.Text = hotkey.ToString();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CapturedHotkey is not null) DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
