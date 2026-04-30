using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace AuviWin.Core.Hotkeys;

/// <summary>
/// Registers global hotkeys using a hidden Win32 message-only window on a dedicated STA thread.
/// Register/Unregister calls are marshalled onto the hotkey thread to honour Win32's
/// thread-affinity requirement for window-based hotkeys.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const uint WM_APP_INVOKE = 0x8001;

    private readonly Dictionary<int, Hotkey> _registered = [];
    private readonly ConcurrentQueue<Action> _invocations = new();
    private readonly SynchronizationContext _uiContext;
    private nint _hwnd;
    private Thread? _thread;
    private int _nextId = 1;
    private volatile bool _ready;
    private volatile bool _disposed;

    public event EventHandler<int>? HotkeyPressed;

    public HotkeyService()
    {
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("HotkeyService must be created on the UI thread.");

        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "HotkeyService" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        // Spin until the hidden window is created on the hotkey thread
        while (!_ready) Thread.SpinWait(100);
        if (_hwnd == 0)
            throw new InvalidOperationException(
                $"Failed to create hotkey message window. Win32 error: {Marshal.GetLastWin32Error()}");
    }

    public int Register(Hotkey hotkey)
    {
        if (!hotkey.IsValid) throw new ArgumentException("Hotkey must have modifiers and a key.", nameof(hotkey));

        int id = 0;
        Invoke(() =>
        {
            id = _nextId++;
            bool ok = RegisterHotKey(_hwnd, id, (uint)hotkey.Modifiers | 0x4000 /*NOREPEAT*/, hotkey.VirtualKey);
            if (!ok)
            {
                _nextId--;
                throw new InvalidOperationException($"Failed to register hotkey {hotkey}. It may already be in use.");
            }
            _registered[id] = hotkey;
        });
        return id;
    }

    public void Unregister(int id)
    {
        if (_disposed) return;
        Invoke(() =>
        {
            if (_registered.Remove(id))
                UnregisterHotKey(_hwnd, id);
        });
    }

    public void UnregisterAll()
    {
        if (_disposed) return;
        Invoke(() =>
        {
            foreach (var id in _registered.Keys.ToList())
                UnregisterHotKey(_hwnd, id);
            _registered.Clear();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != 0 && _thread is not null)
        {
            var done = new ManualResetEventSlim(false);
            _invocations.Enqueue(() =>
            {
                foreach (var id in _registered.Keys.ToList())
                    UnregisterHotKey(_hwnd, id);
                _registered.Clear();
                done.Set();
                PostQuitMessage(0);
            });
            PostMessage(_hwnd, (int)WM_APP_INVOKE, 0, 0);
            done.Wait(2000);
        }

        _thread?.Join(2000);
        _thread = null;
    }

    // ── Marshals work onto the hotkey thread and blocks until done ────────────

    private void Invoke(Action work)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ExceptionDispatchInfo? captured = null;
        var done = new ManualResetEventSlim(false);

        _invocations.Enqueue(() =>
        {
            try { work(); }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
            finally { done.Set(); }
        });

        PostMessage(_hwnd, (int)WM_APP_INVOKE, 0, 0);
        done.Wait();
        captured?.Throw();
    }

    // ── Hidden window message loop ────────────────────────────────────────────

    private void MessageLoop()
    {
        _hwnd = CreateWindowHidden();
        _ready = true;
        if (_hwnd == 0) return;

        while (GetMessage(out var msg, nint.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                int id = (int)msg.wParam;
                _uiContext.Post(_ => HotkeyPressed?.Invoke(this, id), null);
            }
            else if (msg.message == WM_APP_INVOKE)
            {
                while (_invocations.TryDequeue(out var action))
                    action();
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static nint CreateWindowHidden()
    {
        var hInstance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = hInstance,
            lpszClassName = "AuviWinHotkeyWindow"
        };

        // RegisterClassEx returns 0 on failure; ERROR_CLASS_ALREADY_EXISTS (1410) is safe to ignore
        if (RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410)
            return nint.Zero;

        return CreateWindowEx(0, "AuviWinHotkeyWindow", "AuviWin Hotkey",
            0, 0, 0, 0, 0, new nint(-3) /*HWND_MESSAGE*/, nint.Zero, hInstance, nint.Zero);
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int w, int h, nint parent, nint menu, nint hInstance, nint lpParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int nExitCode);
    [DllImport("kernel32.dll")] private static extern nint GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX, ptY;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    // Keep a static reference to prevent GC collection
    private static readonly WndProcDelegate _wndProcDelegate = DefWindowProc;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }
}
