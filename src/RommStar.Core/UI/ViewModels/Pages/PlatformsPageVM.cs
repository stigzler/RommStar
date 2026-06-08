using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Core.UI.Messages;
using RommStar.Core.UI.ViewModels.DataModels;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;

using System.Text;
using Unbroken.LaunchBox.Plugins;

namespace RommStar.Core.UI.ViewModels
{
    //todo: re/load server on page navigate to (in case user adds/deletes a server)
    public partial class PlatformsPageVM : ObservableObject, IRecipient<DeleteLaunchboxPlatformItemMessage>
    {
        private readonly LaunchboxService _launchboxService;
        private readonly RommService _rommService;
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private ObservableCollection<LaunchboxPlatformItemVM>
            _launchboxPlatformItems = new ObservableCollection<LaunchboxPlatformItemVM>();

        [ObservableProperty]
        private ObservableCollection<RommServerItemVM>
            _rommServerItems = new ObservableCollection<RommServerItemVM>();

        [ObservableProperty]
        private ObservableCollection<RommServerItemVM>
            _rommServers = new ObservableCollection<RommServerItemVM>();

        //[ObservableProperty]
        //private Dictionary<RommServer, List<RommPlatformDTO>>
        //    _rommServerItemsCache = new Dictionary<RommServer, List<RommPlatformDTO>>();

        [ObservableProperty]
        private LaunchboxPlatformItemVM
            _selectedPlatform;

        [ObservableProperty]
        private RommServerItemVM
            _selectedRommServer;

        /// <summary>
        /// Parameterless constructor for the XAML Designer
        /// </summary>
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

            LoadLaunchboxPlatforms();
            LoadPersistedRommServers();
        }

        /// <summary>
        /// Fires ever time page made visible/concealed
        /// </summary>
        /// <param name="madeVisible"></param>
        /// <returns></returns>
        public async Task OnPageVisibilityChanged(bool madeVisible)
        {
            if (madeVisible)
            {
                //LoadPersistedRommServers();
                //LoadLaunchboxPlatforms();
            }
        }

        void IRecipient<DeleteLaunchboxPlatformItemMessage>.Receive(DeleteLaunchboxPlatformItemMessage message)
        {
            DeleteLaunchboxPlatformItem(message.Value);
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

        private RommServerItemVM GetRommServerById(Guid id)
        {
            return RommServerItems.Where(rs => rs.RommServer.Id == id).FirstOrDefault();
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
                    RommServerItemVM? matchedRommServer = GetRommServerById(matchedPersistedPlatform.RommServerId);

                    if (matchedRommServer != null)
                    {
                        newLaunchboxPlatformItemVM.AssignedServerItem.RommServer = matchedRommServer.RommServer;

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
                        IsOrphaned = true,
                        AssignedServerItem = new RommServerItemVM(GetRommServerById(platformSyncSettings.RommServerId).RommServer)
                        //AssignedServerItem.RommServer = GetRommServerById(platformSyncSettings.RommServerId).RommServer,
                        //AssignedServerItem.RommServer = null
                    };
                    LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
                }
            }
        }

        /// <summary>
        /// This only populates the RommServerItemVM.Server, not the ServerPlatformDTOs - this done elsewhere to
        /// prevent delays form the API call to Romm API
        /// </summary>
        private async void LoadPersistedRommServers()
        {
            foreach (var rommServer in _settingsService.Settings.RommServers)
            {
                RommServerItemVM existingRommServer = RommServers.Where(rs => rs.RommServer.Id == rommServer.Id).FirstOrDefault();
                if (existingRommServer != null)
                {
                    existingRommServer.RommServer = rommServer;
                }
                else
                {
                    RommServers.Add(new RommServerItemVM(rommServer));
                }
            }
        }

        private async Task LoadRommServersPlatformDTOs()
        {
            foreach (var rommServer in RommServers)
            {
                await UpdateRommServerPlatformsDict(rommServer);
            }
        }

        [RelayCommand]
        private async Task ReloadLaunchboxPlatforms()
        {
            LoadLaunchboxPlatforms();
            // TESTS
            var platfroms = PluginHelper.DataManager.GetAllPlatforms();
            var dave = platfroms[1].GetAllPlatformFolders();
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
                if (launchboxPLatformItem.AssignedServerItem != null) newPlatformSyncSettings.RommServerId = launchboxPLatformItem.AssignedServerItem.RommServer.Id;
                if (launchboxPLatformItem.MatchedRommPlatforms != null) newPlatformSyncSettings.RommServerPlatforms = launchboxPLatformItem.MatchedRommPlatforms;

                _settingsService.Settings.PlatformSyncSettings.Add(newPlatformSyncSettings);
            }
            _settingsService.Save();
        }

        [RelayCommand]
        private void Test()
        {
            SavePlatformSyncSettings();
        }

        [RelayCommand]
        private async Task UpdateRommServerPlatforms(RommServerItemVM rommServer)
        {
            await UpdateRommServerPlatformsDict(rommServer);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rommServerItem"></param>
        /// <returns></returns>
        private async Task UpdateRommServerPlatformsDict(RommServerItemVM rommServerItem)
        {
            RommApiResponse<List<RommPlatformDTO>> rommPlatformsQuery = await _rommService.GetRommPlatformsAsync(rommServerItem.RommServer);

            if (!rommPlatformsQuery.IsSuccess)
            {
                StringBuilder sb = new StringBuilder($"Romm Server: {rommServerItem.RommServer.ServerName}\r\n" +
                    $"Issue: {rommPlatformsQuery.FailureReason}\r\n");
                if (rommPlatformsQuery.HttpResponse != null) sb.AppendLine(rommPlatformsQuery.HttpResponse.ToString());
                if (rommPlatformsQuery.ExceptionMessage != null) sb.Append(rommPlatformsQuery.ExceptionMessage);

                rommServerItem.InfoBar = PopulatedInfoBar("Romm Server Error", sb.ToString(), isOpen: true, InfoBarSeverity.Error);
                return;
            }
            else
            {
                rommServerItem.InfoBar = PopulatedInfoBar("Success", "Platforms updated successfully", isOpen: true, InfoBarSeverity.Success);
            }
        }

        private InfoBar PopulatedInfoBar(string title, string message, bool isOpen = false,
            InfoBarSeverity infoBarSeverity = InfoBarSeverity.Informational)
        {
            return new InfoBar()
            {
                Title = title,
                Message = message,
                IsOpen = isOpen,
                Severity = infoBarSeverity
            };
        }
    }
}