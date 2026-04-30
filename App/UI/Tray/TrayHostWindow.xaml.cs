using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace App.UI.Tray;

public sealed partial class TrayHostWindow : Window
{
    public TrayIconManager? TrayIconManager { get; set; }
    public Action? SettingsRequested { get; set; }
    public Action? NextDeviceRequested { get; set; }
    public Action? PreviousDeviceRequested { get; set; }
    public Action? ToggleDisplayRequested { get; set; }
    public Action? ExitRequested { get; set; }

    private HwndSource? _source;
    private static readonly uint WM_TASKBARCREATED =
        RegisterWindowMessage("TaskbarCreated");

    public TrayHostWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WndProc);

        // Wire up tray icon to this window's HWND
        if (TrayIconManager is not null)
        {
            TrayIconManager.Initialize(new WindowInteropHelper(this).Handle);
            TrayIconManager.SettingsRequested += () => SettingsRequested?.Invoke();
            TrayIconManager.NextDeviceRequested += () => NextDeviceRequested?.Invoke();
            TrayIconManager.PreviousDeviceRequested += () => PreviousDeviceRequested?.Invoke();
            TrayIconManager.ToggleDisplayRequested += () => ToggleDisplayRequested?.Invoke();
            TrayIconManager.ExitRequested += () => ExitRequested?.Invoke();
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // Shell restarted (e.g. after display topology change) — re-add the tray icon
        if ((uint)msg == WM_TASKBARCREATED)
            TrayIconManager?.Reinitialize();

        if (TrayIconManager?.HandleWindowMessage((uint)msg, wParam, lParam) == true)
            handled = true;
        return nint.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WndProc);
        base.OnClosed(e);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);
}
