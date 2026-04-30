namespace AuviWin.Core.Hotkeys;

[Flags]
public enum ModifierKeys : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

public sealed record Hotkey(ModifierKeys Modifiers, uint VirtualKey)
{
    // F1–F24 (0x70–0x87) may be registered without modifiers; all others require at least one modifier
    public bool IsValid => VirtualKey != 0 && (Modifiers != ModifierKeys.None || (VirtualKey >= 0x70 && VirtualKey <= 0x87));

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Win)) parts.Add("Win");
        parts.Add(VirtualKeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static string VirtualKeyName(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),  // A-Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),  // 0-9
        0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
        0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
        0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0x20 => "Space", 0x09 => "Tab", 0x0D => "Enter",
        0x1B => "Esc", 0x2E => "Delete", 0x24 => "Home", 0x23 => "End",
        0x21 => "PgUp", 0x22 => "PgDn",
        0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
        _ => $"0x{vk:X2}"
    };
}
