using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuviWin.Core.Configuration;

public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.Name, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private AppSettings _current = new();

    public AppSettings Current => _current;

    public void Load()
    {
        if (!File.Exists(SettingsPath))
        {
            _current = new AppSettings();
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            _current = new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(_current, JsonOptions);

        // Atomic write: write to temp file then replace
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}
