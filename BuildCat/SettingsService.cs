using System.Text.Json;

namespace BuildCat;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuildCat",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = SettingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(SettingsPath))
        {
            File.Replace(tempPath, SettingsPath, null);
        }
        else
        {
            File.Move(tempPath, SettingsPath);
        }
    }

    private void BackupCorruptSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var backupPath = SettingsPath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(SettingsPath, backupPath, overwrite: false);
        }
        catch
        {
            // The app can keep running with defaults even if the backup cannot be written.
        }
    }
}
