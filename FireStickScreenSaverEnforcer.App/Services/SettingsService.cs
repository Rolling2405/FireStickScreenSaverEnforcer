using System.Text.Json;
using FireStickScreenSaverEnforcer.App.Models;

namespace FireStickScreenSaverEnforcer.App.Services;

/// <summary>
/// Service for loading and saving application settings to settings.json.
/// Settings are stored next to the executable for portability.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Loads settings from settings.json. Returns default settings if file doesn't exist or is invalid.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves settings to settings.json next to the executable.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently fail - settings will use defaults on next load
        }
    }
}
