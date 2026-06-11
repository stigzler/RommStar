using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Properties;
using RommStar.Core.Services;

namespace RommStar.Core.UI.ViewModels.Pages
{
    public partial class SettingsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private bool _isDarkTheme;

        [ObservableProperty]
        private PluginSettings _pluginSettings;

        public SettingsPageVM() : this(new SettingsService(new CryptoService()))
        {
        }

        public SettingsPageVM(SettingsService settingsService)
        {
            _settingsService = settingsService;
            PluginSettings = settingsService.Settings;
        }

        public async Task OnPageVisibilityChanged(bool madeVisible)
        {
            if (madeVisible)
            {
                //LoadPersistedRommServers();
                //LoadLaunchboxPlatforms();
            }
            else
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            _settingsService.Save();
        }
    }
}