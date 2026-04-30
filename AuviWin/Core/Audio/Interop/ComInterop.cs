using System.Runtime.InteropServices;

namespace AuviWin.Core.Audio.Interop;

// ── IMMDeviceEnumerator ──────────────────────────────────────────────────────

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
    int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    int RegisterEndpointNotificationCallback(nint client);
    int UnregisterEndpointNotificationCallback(nint client);
}

[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[ComImport]
internal class MMDeviceEnumeratorClass { }

// ── IMMDeviceCollection ──────────────────────────────────────────────────────

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out uint count);
    int Item(uint index, out IMMDevice device);
}

// ── IMMDevice ────────────────────────────────────────────────────────────────

[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, int clsCtx, nint activationParams, out nint @interface);
    int OpenPropertyStore(int access, out IPropertyStore properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetState(out DeviceState state);
}

// ── IPropertyStore ───────────────────────────────────────────────────────────

[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out uint count);
    int GetAt(uint prop, out PropertyKey key);
    int GetValue(ref PropertyKey key, out PropVariant value);
    int SetValue(ref PropertyKey key, ref PropVariant value);
    int Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public short vt;
    public short reserved1;
    public short reserved2;
    public short reserved3;
    public nint data1;
    public nint data2;

    public string? GetStringValue() =>
        vt == 31 /*VT_LPWSTR*/ ? Marshal.PtrToStringUni(data1) : null;
}

// ── IPolicyConfig (undocumented, sets system default audio endpoint) ─────────

[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, nint format);
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool defaultFormat, nint format);
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, nint endpointFormat, nint mixFormat);
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool defaultPeriod, nint defaultDevicePeriod, nint minDevicePeriod);
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, nint processingPeriod);
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, nint shareMode);
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, nint shareMode);
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool defaultDevice, ref PropertyKey key, out PropVariant value);
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool defaultDevice, ref PropertyKey key, ref PropVariant value);
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, Role role);
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, bool visible);
}

[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
[ComImport]
internal class PolicyConfigClass { }

// ── Enums ────────────────────────────────────────────────────────────────────

internal enum DataFlow
{
    Render = 0,
    Capture = 1,
    All = 2
}

internal enum Role
{
    Console = 0,
    Multimedia = 1,
    Communications = 2
}

[Flags]
internal enum DeviceState : uint
{
    Active = 0x1,
    Disabled = 0x2,
    NotPresent = 0x4,
    Unplugged = 0x8,
    All = 0xF
}

// ── Well-known property keys ─────────────────────────────────────────────────

internal static class PropertyKeys
{
    // PKEY_Device_FriendlyName
    public static PropertyKey FriendlyName = new()
    {
        FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        PropertyId = 14
    };
}

// ── Native helpers ────────────────────────────────────────────────────────────

internal static class ComNative
{
    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref PropVariant pvar);
}
