using RommStar.Core.Models;
using RommStar.Core.Properties;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RommStar.Core.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private readonly object _fileLock = new();
        private readonly CryptoService _crypto;

        // Cached settings instance in memory
        public PluginSettings Settings { get; private set; }

        public SettingsService(CryptoService cryptoService)
        {
            _crypto = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

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
                        var settings = JsonSerializer.Deserialize<PluginSettings>(json, GetSerializerOptions()) ?? new PluginSettings();

                        // Decrypt only marked properties (in-place)
                        ProcessEncryptionRecursive(settings, encrypt: false, visited: new HashSet<object>(ReferenceEqualityComparer.Instance));

                        return settings;
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
                    // Deep clone to avoid mutating in-memory plaintext values
                    var cloneJson = JsonSerializer.Serialize(Settings, GetSerializerOptions());
                    var clone = JsonSerializer.Deserialize<PluginSettings>(cloneJson, GetSerializerOptions()) ?? new PluginSettings();

                    // Encrypt only marked properties on the clone
                    ProcessEncryptionRecursive(clone, encrypt: true, visited: new HashSet<object>(ReferenceEqualityComparer.Instance));

                    string json = JsonSerializer.Serialize(clone, GetSerializerOptions());
                    File.WriteAllText(_settingsFilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                }
            }
        }

        private static JsonSerializerOptions GetSerializerOptions() =>
            new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

        // Walk object graph and encrypt/decrypt string properties annotated with [Encrypted]
        private void ProcessEncryptionRecursive(object? obj, bool encrypt, HashSet<object> visited)
        {
            if (obj == null) return;

            // avoid value types and strings
            var type = obj.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return;

            if (visited.Contains(obj)) return;
            visited.Add(obj);

            // handle collections
            if (obj is IEnumerable enumerable && !(obj is string))
            {
                foreach (var item in enumerable)
                {
                    ProcessEncryptionRecursive(item, encrypt, visited);
                }
                return;
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in props)
            {
                var propType = prop.PropertyType;

                // string properties marked with [Encrypted]
                if (propType == typeof(string) &&
                    prop.GetCustomAttribute(typeof(RommStar.Core.Primitives.EncryptedAttribute), inherit: true) != null)
                {
                    var current = (string?)prop.GetValue(obj);
                    if (encrypt)
                    {
                        if (!string.IsNullOrEmpty(current))
                        {
                            prop.SetValue(obj, _crypto.ProtectToBase64(current));
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(current))
                        {
                            var decrypted = _crypto.TryUnprotectFromBase64(current);
                            if (decrypted != null)
                                prop.SetValue(obj, decrypted);
                            // If decryption fails, leave value as-is (backwards compat)
                        }
                    }

                    continue;
                }

                // complex types -> recurse
                if (!propType.IsPrimitive && propType != typeof(string) && !propType.IsEnum)
                {
                    var value = prop.GetValue(obj);
                    if (value != null)
                    {
                        ProcessEncryptionRecursive(value, encrypt, visited);
                    }
                }
            }
        }

        // Small reference-equality comparer for visited set
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}