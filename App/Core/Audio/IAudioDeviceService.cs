namespace App.Core.Audio;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDevice> GetActiveRenderDevices();
    AudioDevice? GetDefaultRenderDevice();
    void SetDefaultRenderDevice(string deviceId);
}
