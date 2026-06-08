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
        private string
            _rommServerConnectionErrorMessage = "{default}";

        [ObservableProperty]
        private InfoBarSeverity
            _rommServerConnectionErrorSeverity = InfoBarSeverity.Informational;

        [ObservableProperty]
        private bool
            _rommServerConnectionErrorShown = false;

        [ObservableProperty]
        private string
            _rommServerConnectionErrorTitle = "{default}";

        [ObservableProperty]
        private ObservableCollection<RommServer>
            _rommServers = new ObservableCollection<RommServer>();

        [ObservableProperty]
        private Dictionary<RommServer, List<RommPlatformDTO>>
            _rommServersPlatformsDict = new Dictionary<RommServer, List<RommPlatformDTO>>();

        [ObservableProperty]
        private LaunchboxPlatformItemVM
            _selectedPlatform;

        [ObservableProperty]
        private RommServer
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
                RommServerConnectionErrorShown = false;
                LoadPersistedRommServers();
                LoadLaunchboxPlatforms();
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

        private RommServer GetRommServerById(Guid id)
        {
            return RommServers.Where(rs => rs.Id == id).FirstOrDefault();
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

        private async void LoadPersistedRommServers()
        {
            foreach (var rommServer in _settingsService.Settings.RommServers)
            {
                RommServer existingRommServer = RommServers.Where(rs => rs.Id == rommServer.Id).FirstOrDefault();
                if (existingRommServer != null)
                {
                    existingRommServer = rommServer;
                }
                else
                {
                    RommServers.Add(rommServer);
                }

                //RommServers = new ObservableCollection<RommServer>(_settingsService.Settings.RommServers);
            }
        }

        private async Task LoadRommServers()
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
                if (launchboxPLatformItem.AssignedServer != null) newPlatformSyncSettings.RommServerId = launchboxPLatformItem.AssignedServer.Id;
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
        private async Task UpdateRommServerPlatforms(RommServer rommServer)
        {
            await UpdateRommServerPlatformsDict(rommServer);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rommServer"></param>
        /// <param name="suppressErrorMessage">Used on page reload to stop error showing as Server not technically queried yet</param>
        /// <returns></returns>
        private async Task UpdateRommServerPlatformsDict(RommServer rommServer)
        {
            RommApiResponse<List<RommPlatformDTO>> rommPlatformsQuery = await _rommService.GetRommPlatformsAsync(rommServer);

            if (!rommPlatformsQuery.IsSuccess)
            {
                RommServerConnectionErrorSeverity = InfoBarSeverity.Error;
                RommServerConnectionErrorTitle = "Romm Server Error";

                StringBuilder sb = new StringBuilder($"Romm Server: {rommServer.ServerName}\r\n" +
                    $"Issue: {rommPlatformsQuery.FailureReason}\r\n");
                if (rommPlatformsQuery.HttpResponse != null) sb.AppendLine(rommPlatformsQuery.HttpResponse.ToString());
                if (rommPlatformsQuery.ExceptionMessage != null) sb.Append(rommPlatformsQuery.ExceptionMessage);
                RommServerConnectionErrorMessage = sb.ToString();
                RommServerConnectionErrorShown = true;

                return;
            }

            RommServerConnectionErrorShown = false; // clears any previous errors

            if (RommServersPlatformsDict.ContainsKey(rommServer))
            {
                // rommPlatformDTOs = RommServersPlatformsDict[rommServer];
            }
        }
    }
}