using System;
using System.IO;
using System.Text.Json;
using Fellowship_overlay.Core;

namespace Fellowship_overlay.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FellowshipOverlay");

    private static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // fall through to return defaults
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(SettingsFile, json);
    }
}