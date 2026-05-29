using System.Text.Json;
using BoostModeCommon.Models;

namespace BoostModeCommon;

public static class ConfigManager
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "BoostModeSvc");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    private static readonly string StatusPath = Path.Combine(ConfigDir, "status.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                    return config;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load config, using defaults: {ex.Message}");
        }
        return GetDefaultConfig();
    }

    public static bool Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save config: {ex.Message}");
            return false;
        }
    }

    public static void SaveStatus(StatusInfo status)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(status, JsonOptions);
            File.WriteAllText(StatusPath, json);
        }
        catch { }
    }

    public static StatusInfo? LoadStatus()
    {
        try
        {
            if (File.Exists(StatusPath))
            {
                var json = File.ReadAllText(StatusPath);
                return JsonSerializer.Deserialize<StatusInfo>(json, JsonOptions);
            }
        }
        catch { }
        return null;
    }

    public static AppConfig GetDefaultConfig() => new();
    public static string GetConfigPath() => ConfigPath;
    public static string GetLogDir() => Path.Combine(ConfigDir, "logs");
    public static string GetStatusPath() => StatusPath;
    public static string GetConfigDir() => ConfigDir;
}
