using System.Runtime.InteropServices;
using App.Core.Audio.Interop;

namespace App.Core.Audio;

/// <summary>
/// Enumerates render devices and switches the system default using raw COM interop.
/// All three roles (Console, Multimedia, Communications) are updated on switch so
/// every app picks up the change.
/// </summary>
public sealed class AudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly IPolicyConfig _policyConfig;

    public AudioDeviceService()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!;
        _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;

        var policyType = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))!;
        _policyConfig = (IPolicyConfig)Activator.CreateInstance(policyType)!;
    }

    public IReadOnlyList<AudioDevice> GetActiveRenderDevices()
    {
        Marshal.ThrowExceptionForHR(
            _enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active, out var collection));
        try
        {
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            var devices = new List<AudioDevice>((int)count);
            for (uint i = 0; i < count; i++)
            {
                Marshal.ThrowExceptionForHR(collection.Item(i, out var device));
                try { devices.Add(ReadDevice(device)); }
                finally { Marshal.ReleaseComObject(device); }
            }
            return devices;
        }
        finally { Marshal.ReleaseComObject(collection); }
    }

    public AudioDevice? GetDefaultRenderDevice()
    {
        int hr = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console, out var device);
        if (hr != 0) return null;
        try { return ReadDevice(device); }
        finally { Marshal.ReleaseComObject(device); }
    }

    public void SetDefaultRenderDevice(string deviceId)
    {
        // Set all three roles so all apps respond to the switch
        Marshal.ThrowExceptionForHR(_policyConfig.SetDefaultEndpoint(deviceId, Role.Console));
        Marshal.ThrowExceptionForHR(_policyConfig.SetDefaultEndpoint(deviceId, Role.Multimedia));
        Marshal.ThrowExceptionForHR(_policyConfig.SetDefaultEndpoint(deviceId, Role.Communications));
    }

    private static AudioDevice ReadDevice(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out var id));
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0 /*STGM_READ*/, out var store));
        try
        {
            var key = PropertyKeys.FriendlyName;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var prop));
            try
            {
                var name = prop.GetStringValue() ?? id;
                return new AudioDevice(id, name);
            }
            finally { ComNative.PropVariantClear(ref prop); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    public void Dispose()
    {
        if (_enumerator is not null) Marshal.ReleaseComObject(_enumerator);
        if (_policyConfig is not null) Marshal.ReleaseComObject(_policyConfig);
    }
}
