using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using RommStar.Core.Models;
using RommStar.Core.Properties;

namespace RommStar.Core.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private readonly object _fileLock = new();

        // Cached settings instance in memory
        public PluginSettings Settings { get; private set; }

        public SettingsService()
        {
            // Determine the folder where the RommStar plugin assembly is located inside LaunchBox\Plugins
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                 ?? AppContext.BaseDirectory;

            _settingsFilePath = Path.Combine(pluginFolder, "settings.json");

            // Load existing settings or initialize empty defaults
            Settings = Load();
        }

        /// <summary>
        /// Reads settings from the settings.json file inside the plugin folder.
        /// </summary>
        private PluginSettings Load()
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_settingsFilePath))
                    {
                        string json = File.ReadAllText(_settingsFilePath);
                        return JsonSerializer.Deserialize<PluginSettings>(json) ?? new PluginSettings();
                    }
                }
                catch (Exception ex)
                {
                    // Fall back to defaults if parsing fails
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                }

                return new PluginSettings();
            }
        }

        /// <summary>
        /// Persists the active settings back to the settings.json file.
        /// </summary>
        public void Save()
        {
            lock (_fileLock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(Settings, options);
                    File.WriteAllText(_settingsFilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                }
            }
        }
    }
}