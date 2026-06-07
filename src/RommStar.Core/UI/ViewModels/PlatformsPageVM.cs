using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Core.UI.ViewModels.DataItems;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using System.Linq;

using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.UI.ViewModels
{
    //todo: re/load server on page navigate to (in case user adds/deletes a server)
    public partial class PlatformsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly LaunchboxService _launchboxService;
        private readonly RommService _rommService;

        [ObservableProperty]
        private ObservableCollection<LaunchboxPlatformItemVM> _launchboxPlatformItems = new ObservableCollection<LaunchboxPlatformItemVM>();

        [ObservableProperty]
        private ObservableCollection<RommServer> _rommServers = new ObservableCollection<RommServer>();

        // Parameterless constructor for the XAML Designer
        public PlatformsPageVM() : this(
            new SettingsService(new CryptoService()),
            new LaunchboxService(),
            new RommService())
        {
            // any test data
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // DisplayPlatforms.Add(new MappedPlatformItemVM("Super Nintendo", false));
            }
        }

        public PlatformsPageVM(SettingsService settingsService, LaunchboxService launchboxService, RommService rommService)
        {
            _settingsService = settingsService;
            _launchboxService = launchboxService;
            _rommService = rommService;
        }

        public void LoadPlatformsAndPersistedData()
        {
            LoadPersistedRommServers();
            LoadLaunchboxPlatforms();
        }

        private async void LoadPersistedRommServers()
        {
            RommServers = new ObservableCollection<RommServer>(_settingsService.Settings.RommServers);
        }

        [RelayCommand]
        private async Task ReloadLaunchboxPlatforms()
        {
            LoadLaunchboxPlatforms();
            // TESTS
            var platfroms = PluginHelper.DataManager.GetAllPlatforms();
            var dave = platfroms[1].GetAllPlatformFolders();
        }

        [RelayCommand]
        private async Task DeleteOrphanedMap()
        {
        }

        private async void LoadLaunchboxPlatforms()
        {
            // Get current LB platforms
            var liveLbPlatformDtos = _launchboxService.GetPlatforms();

            LaunchboxPlatformItems.Clear();

            foreach (var liveLbPlatform in liveLbPlatformDtos)
            {
                LaunchboxPlatformItemVM newLaunchboxPlatformItemVM = new LaunchboxPlatformItemVM()
                {
                    LaunchboxPlatformName = liveLbPlatform.Name,
                    IconPath = Path.Combine(Constants.LaunchboxRootDir, Constants.MediaPacksPlatformIconsRelPath,
                    _launchboxService.LaunchboxSettings.PlatformIconPack, "Platforms", $"{liveLbPlatform.Name}.png"),
                    IsOrphaned = true,

                    //todo: icon
                };

                // Test persisted Platform Maps for existing map
                PlatformSyncSettings? matchedPersistedPlatform = _settingsService.Settings.PlatformSyncSettings
                    .Where(pss => pss.LaunchboxPlatformName == liveLbPlatform.Name).FirstOrDefault();

                if (matchedPersistedPlatform != null)
                {
                    // There is a match. There is no guarantee that a persisted server is still registered in RommStar. Check and flag error if not.
                    RommServer? matchedRommServer = RommServers.Where(rs => rs.Id == matchedPersistedPlatform.RommServerId).FirstOrDefault();

                    if (matchedRommServer != null)
                    {
                        newLaunchboxPlatformItemVM.AssignedServer = matchedRommServer;

                        // Assign the previously matched Romm PlatformIds only if server still in RommStar setup (no point if not)
                        newLaunchboxPlatformItemVM.MatchedRommPlatforms = matchedPersistedPlatform.RommServerPlatforms;
                    }
                }

                LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
            }
        }
    }
}