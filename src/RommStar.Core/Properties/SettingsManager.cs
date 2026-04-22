using System;
using System.IO;
using System.Text.Json;

namespace RommStar.Core.Properties
{
    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Path.GetDirectoryName(typeof(SettingsManager).Assembly.Location)!,
            "UserSettings.json");

        public static UserSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            return new UserSettings();
        }

        public static void Save(UserSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}