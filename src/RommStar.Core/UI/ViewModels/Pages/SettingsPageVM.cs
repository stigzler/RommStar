using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Models;
using RommStar.Core.Properties;
using RommStar.Core.Services;
using RommStar.Core.UI.Views.UserControls;
using System.Collections.ObjectModel;

namespace RommStar.Core.UI.ViewModels.Pages
{
    public partial class SettingsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private bool _isDarkTheme;

        [ObservableProperty]
        private PluginSettings _pluginSettings;

        /// <summary>
        /// Which media to download at the Sync Stage
        /// </summary>
        public ObservableCollection<MediaSelectionItemViewModel> SyncMediaTypes { get; } = new();

        /// <summary>
        /// Which media to download at the Rom Install Stage
        /// </summary>
        public ObservableCollection<MediaSelectionItemViewModel> InstallMediaTypes { get; } = new();

        public SettingsPageVM() : this(new SettingsService(new CryptoService()))
        {
        }

        public SettingsPageVM(SettingsService settingsService)
        {
            _settingsService = settingsService;
            PluginSettings = settingsService.Settings;
            // Initialize the UI collection items from our Enums and settings data state
            PopulateMediaUICollections();

        }
        private void PopulateMediaUICollections()
        {
            SyncMediaTypes.Clear();
            InstallMediaTypes.Clear();

            foreach (MediaType type in Enum.GetValues(typeof(MediaType)))
            {
                bool isSyncEnabled = PluginSettings.SyncMediaProfile.EnabledTypes.Contains(type);
                SyncMediaTypes.Add(new MediaSelectionItemViewModel(type, isSyncEnabled));

                bool isInstallEnabled = PluginSettings.InstallMediaProfile.EnabledTypes.Contains(type);
                InstallMediaTypes.Add(new MediaSelectionItemViewModel(type, isInstallEnabled));
            }
        }


        public async Task OnPageVisibilityChanged(bool madeVisible)
        {
            if (madeVisible)
            {
                PopulateMediaUICollections();
            }
            else
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            // 1. Commit UI states back into the raw PluginSettings Data Models before saving to disk
            SaveProfileFromUI(SyncMediaTypes, PluginSettings.SyncMediaProfile);
            SaveProfileFromUI(InstallMediaTypes, PluginSettings.InstallMediaProfile);

            _settingsService.Save();
        }

        private void SaveProfileFromUI(ObservableCollection<MediaSelectionItemViewModel> uiCollection, MediaSelectionProfile profile)
        {
            profile.EnabledTypes.Clear();

            var activeTypes = uiCollection
                .Where(x => x.IsSelected)
                .Select(x => x.Type);

            foreach (var type in activeTypes)
            {
                profile.EnabledTypes.Add(type);
            }
        }
    }
}