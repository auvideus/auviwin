using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using AuviWin.Core;
using AuviWin.Core.Audio;
using AuviWin.Core.Configuration;

namespace AuviWin.UI.Tray;

/// <summary>
/// Manages the system tray icon using raw Shell_NotifyIcon P/Invoke.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int TRAY_CALLBACK_MSG = WM_USER + 1001;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int NIIF_INFO = 0x00000001;

    private readonly IAudioDeviceService _audio;
    private readonly SettingsService _settings;
    private nint _hwnd;
    private nint _hIcon;
    private bool _added;
    private bool _disposed;
    private volatile string? _pendingTitle;
    private volatile string? _pendingMessage;
    private System.Threading.Timer? _pendingTimer;

    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action? NextDeviceRequested;
    public event Action? PreviousDeviceRequested;
    public event Action? ToggleDisplayRequested;

    public TrayIconManager(IAudioDeviceService audio, SettingsService settings)
    {
        _audio = audio;
        _settings = settings;
    }

    public void Initialize(nint hwnd)
    {
        _hwnd = hwnd;
        _hIcon = LoadDefaultIcon();
        AddIcon();
        UpdateTooltip();
    }

    /// <summary>Called when the shell restarts (WM_TASKBARCREATED) to restore the tray icon.</summary>
    public void Reinitialize()
    {
        _added = false;
        AddIcon();
        UpdateTooltip();
        // If a notification was queued before the shell restarted, fire it now.
        FlushPendingNotification();
    }

    /// <summary>
    /// Queues a notification that fires either when the shell restarts (WM_TASKBARCREATED)
    /// or after <paramref name="fallbackMs"/> ms — whichever comes first.
    /// Use this for notifications that follow a display topology change.
    /// </summary>
    public void QueueNotification(string title, string message, int fallbackMs = 3000)
    {
        _pendingTitle   = title;
        _pendingMessage = message;
        _pendingTimer?.Dispose();
        _pendingTimer = new System.Threading.Timer(
            _ => FlushPendingNotification(), null, fallbackMs, System.Threading.Timeout.Infinite);
    }

    private void FlushPendingNotification()
    {
        _pendingTimer?.Dispose();
        _pendingTimer = null;
        if (_disposed || !_added) return;
        var title = System.Threading.Interlocked.Exchange(ref _pendingTitle, null);
        var msg   = System.Threading.Interlocked.Exchange(ref _pendingMessage, null);
        if (title is not null)
            ShowNotification(title, msg ?? "");
    }

    public void UpdateTooltip()
    {
        var device = _audio.GetDefaultRenderDevice();
        var tip = device is null ? AppInfo.Name : $"{AppInfo.Name} — {device.Name}";
        ModifyIcon(tip);
    }

    public void ShowNotification(string title, string message)
    {
        var data = BuildNotifyIconData();
        data.uFlags |= NIF_INFO;
        data.szInfoTitle = title;
        data.szInfo = message;
        data.dwInfoFlags = NIIF_INFO;
        data.uTimeout = 3000;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public bool HandleWindowMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg != TRAY_CALLBACK_MSG) return false;

        var mouseMsg = (int)(lParam & 0xFFFF);
        switch (mouseMsg)
        {
            case WM_LBUTTONDBLCLK:
                SettingsRequested?.Invoke();
                break;
            case WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
        return true;
    }

    private void ShowContextMenu()
    {
        var cfg = _settings.Current;
        string? nextGesture   = cfg.AudioCycle.NextDeviceHotkey?.ToString();
        string? prevGesture   = cfg.AudioCycle.PreviousDeviceHotkey?.ToString();
        string? toggleGesture = cfg.DisplayAudioSync.ToggleHotkey?.ToString();

        var menu = new System.Windows.Controls.ContextMenu();

        var nextItem = new System.Windows.Controls.MenuItem { Header = "Next audio device", InputGestureText = nextGesture };
        nextItem.Click += (_, _) => NextDeviceRequested?.Invoke();
        menu.Items.Add(nextItem);

        var prevItem = new System.Windows.Controls.MenuItem { Header = "Previous audio device", InputGestureText = prevGesture };
        prevItem.Click += (_, _) => PreviousDeviceRequested?.Invoke();
        menu.Items.Add(prevItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var toggleItem = new System.Windows.Controls.MenuItem { Header = "Toggle display config", InputGestureText = toggleGesture };
        toggleItem.Click += (_, _) => ToggleDisplayRequested?.Invoke();
        menu.Items.Add(toggleItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings\u2026" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    private void AddIcon()
    {
        var data = BuildNotifyIconData();
        if (Shell_NotifyIcon(NIM_ADD, ref data))
            _added = true;
    }

    private void ModifyIcon(string tooltip)
    {
        if (!_added) return;
        var data = BuildNotifyIconData();
        data.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA BuildNotifyIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = TRAY_CALLBACK_MSG,
        hIcon = _hIcon,
        szTip = AppInfo.Name,
        szInfo = "",
        szInfoTitle = "",
        dwInfoFlags = 0,
        uTimeout = 0
    };

    private static nint LoadDefaultIcon()
    {
        // Use the app's embedded icon, or fall back to a system icon
        var exePath = Assembly.GetExecutingAssembly().Location;
        ExtractIconEx(exePath, 0, nint.Zero, out var hIconSmall, 1);
        if (hIconSmall != nint.Zero) return hIconSmall;
        return LoadIcon(nint.Zero, new nint(32512) /*IDI_APPLICATION*/);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pendingTimer?.Dispose();
        _pendingTimer = null;
        _pendingTitle = null;
        _pendingMessage = null;

        if (_added && _hwnd != 0)
        {
            var data = BuildNotifyIconData();
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_hIcon != nint.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = nint.Zero;
        }

        SettingsRequested = null;
        ExitRequested = null;
        NextDeviceRequested = null;
        PreviousDeviceRequested = null;
        ToggleDisplayRequested = null;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("user32.dll")] private static extern nint LoadIcon(nint hInstance, nint lpIconName);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int ExtractIconEx(string lpszFile, int nIconIndex, nint phiconLarge, out nint phiconSmall, int nIcons);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }
}
