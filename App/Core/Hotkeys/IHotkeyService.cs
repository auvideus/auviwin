namespace AuviWin.Core.Hotkeys;

public interface IHotkeyService : IDisposable
{
    /// <summary>Registers a hotkey and returns an ID. Throws on conflict.</summary>
    int Register(Hotkey hotkey);

    /// <summary>Unregisters a previously registered hotkey by ID.</summary>
    void Unregister(int id);

    /// <summary>Unregisters all hotkeys.</summary>
    void UnregisterAll();

    /// <summary>Raised on the UI thread when a registered hotkey is pressed.</summary>
    event EventHandler<int> HotkeyPressed;
}
