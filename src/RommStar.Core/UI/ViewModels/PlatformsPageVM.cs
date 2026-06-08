using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Core.UI.Messages;
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
    public partial class PlatformsPageVM : ObservableObject, IRecipient<DeleteLaunchboxPlatformItemMessage>
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

            WeakReferenceMessenger.Default.Register<DeleteLaunchboxPlatformItemMessage>(this);
        }

        private void DeleteLaunchboxPlatformItem(LaunchboxPlatformItemVM launchboxPlatformItemVM)
        {
            LaunchboxPlatformItems.Remove(launchboxPlatformItemVM);
            // Todo: Delete form settings
            var launchboxSyncSettings = _settingsService.Settings.PlatformSyncSettings.Where(pss =>
                pss.LaunchboxPlatformName == launchboxPlatformItemVM.LaunchboxPlatformName).FirstOrDefault();

            if (launchboxSyncSettings != null)
            {
                _settingsService.Settings.PlatformSyncSettings.Remove(launchboxSyncSettings);
                _settingsService.Save();
            }
        }

        [RelayCommand]
        private void Test()
        {
            SavePlatformSyncSettings();
        }

        /// <summary>
        /// Translate LaunchboxPlatformItemsVms to PlatformsSyncSettings and persist data
        /// </summary>
        private void SavePlatformSyncSettings()
        {
            _settingsService.Settings.PlatformSyncSettings.Clear();
            foreach (var launchboxPLatformItem in LaunchboxPlatformItems)
            {
                PlatformSyncSettings newPlatformSyncSettings = new PlatformSyncSettings()
                {
                    LaunchboxPlatformName = launchboxPLatformItem.LaunchboxPlatformName,
                };
                if (launchboxPLatformItem.AssignedServer != null) newPlatformSyncSettings.RommServerId = launchboxPLatformItem.AssignedServer.Id;
                if (launchboxPLatformItem.MatchedRommPlatforms != null) newPlatformSyncSettings.RommServerPlatforms = launchboxPLatformItem.MatchedRommPlatforms;

                _settingsService.Settings.PlatformSyncSettings.Add(newPlatformSyncSettings);
            }
            _settingsService.Save();
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
                };

                // Test persisted Platform Maps for existing map
                PlatformSyncSettings? matchedPersistedPlatform = _settingsService.Settings.PlatformSyncSettings
                    .Where(pss => pss.LaunchboxPlatformName == liveLbPlatform.Name).FirstOrDefault();

                if (matchedPersistedPlatform != null)
                {
                    // There is a match. There is no guarantee that a persisted server is still registered in RommStar. Check and flag error if not.
                    RommServer? matchedRommServer = GetRommServerById(matchedPersistedPlatform.RommServerId);

                    if (matchedRommServer != null)
                    {
                        newLaunchboxPlatformItemVM.AssignedServer = matchedRommServer;

                        // Assign the previously matched Romm PlatformIds only if server still in RommStar setup (no point if not)
                        newLaunchboxPlatformItemVM.MatchedRommPlatforms = matchedPersistedPlatform.RommServerPlatforms;
                    }
                }

                LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
            }

            // now test for orphans in persisted PlatformSyncSettings (i.e. those with Lb PLatformName not in current LB collection
            foreach (PlatformSyncSettings platformSyncSettings in _settingsService.Settings.PlatformSyncSettings)
            {
                if (!LaunchboxPlatformItems.Any(item => item.LaunchboxPlatformName == platformSyncSettings.LaunchboxPlatformName))
                {
                    LaunchboxPlatformItemVM newLaunchboxPlatformItemVM = new LaunchboxPlatformItemVM()
                    {
                        LaunchboxPlatformName = platformSyncSettings.LaunchboxPlatformName,
                        MatchedRommPlatforms = platformSyncSettings.RommServerPlatforms,
                        AssignedServer = GetRommServerById(platformSyncSettings.RommServerId),
                        IsOrphaned = true
                    };
                    LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
                }
            }
        }

        private RommServer GetRommServerById(Guid id)
        {
            return RommServers.Where(rs => rs.Id == id).FirstOrDefault();
        }

        void IRecipient<DeleteLaunchboxPlatformItemMessage>.Receive(DeleteLaunchboxPlatformItemMessage message)
        {
            DeleteLaunchboxPlatformItem(message.Value);
        }
    }
}