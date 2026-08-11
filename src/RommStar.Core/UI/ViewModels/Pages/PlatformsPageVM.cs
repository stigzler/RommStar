using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using RommStar.Core.Dtos;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Mappers;
using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using RommStar.Core.UI.Converters;
using RommStar.Core.UI.Messages;
using RommStar.Core.UI.ViewModels.DataModels;
using RommStar.Core.UI.ViewModels.UserControls;
using RommStar.Core.UI.Views.UserControls;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.UI.ViewModels.Pages
{
    //todo: re/load server on page navigate to (in case user adds/deletes a server)
    public partial class PlatformsPageVM : ObservableObject, IRecipient<DeleteLaunchboxPlatformItemMessage>
    {


        private readonly LaunchboxDataService
            _launchboxDataService;

        private readonly LoggingService
            _loggingService;

        private AddNewPlatformUcView _addNewPlatformUcView;

        private readonly AddNewPlatformUcVM _addNewPlatformVm;

        private readonly LaunchboxLocalDatabaseMapper _launchboxLocalDatabaseMapper;

        /// <summary>
        /// Controls overlapping InfoBar calls
        /// </summary>
        private CancellationTokenSource? _infoBarCts;

        /// <summary>
        /// ===== PERFORMANCE-CRITICAL: Centralized Cache =====
        /// Central cache for all Romm server platforms. Key = RommServer.Id
        /// </summary>
        private readonly Dictionary<string, ObservableCollection<PlatformDTO>>
            _rommPlatformCache = new();

        private readonly RommService
            _rommService;

        private readonly SettingsService
            _settingsService;

        private readonly SyncManager
            _syncManager;

        [ObservableProperty]
        private bool
            _addLaunchboxPlatformDialogOpen = false;

        [ObservableProperty]
        private ObservableCollection<LaunchboxPlatformItemVM>
            _launchboxPlatformItems = new ObservableCollection<LaunchboxPlatformItemVM>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredLaunchboxPlatforms))]
        private string
            _launchboxPlatformSearchText = string.Empty;

        [ObservableProperty]
        private InfoBar?
            _launchboxPlatformsInfoBar = new InfoBar();

        [ObservableProperty]
        private InfoBar?
            _launchboxRommPlatformsInfoBar = new InfoBar();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredServerPlatforms))]
        private string
            _platformSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<RommServerItemVM>
            _rommServerItems = new ObservableCollection<RommServerItemVM>();

        [ObservableProperty]
        private LaunchboxPlatformItemVM
            _selectedPlatform;

        [ObservableProperty]
        private PlatformDTO?
            _selectedRommPlatform;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentServerPlatforms), nameof(FilteredServerPlatforms))]
        private RommServerItemVM
            _selectedRommServer;

        public ObservableCollection<PlatformDTO> CurrentServerPlatforms {
            get {
                if (SelectedRommServer == null) return new ObservableCollection<PlatformDTO>();

                if (_rommPlatformCache.TryGetValue(SelectedRommServer.RommServer.Id, out var platforms))
                {
                    return platforms;
                }

                // Lazy load if not cached
                _ = LoadServerPlatformsAsync(SelectedRommServer);
                return new ObservableCollection<PlatformDTO>();
            }
        }

        public List<LaunchboxPlatformItemVM> FilteredLaunchboxPlatforms => LaunchboxPlatformItems.Where(p => p.LaunchboxPlatformName.Contains(LaunchboxPlatformSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// Filtered list of platforms based on search text.
        /// Only renders matching items for better performance.
        /// </summary>
        public List<PlatformDTO> FilteredServerPlatforms {
            get {
                var allPlatforms = CurrentServerPlatforms;

                if (allPlatforms == null || allPlatforms.Count == 0)
                    return new List<PlatformDTO>();

                if (string.IsNullOrWhiteSpace(PlatformSearchText))
                    return new List<PlatformDTO>();//allPlatforms.ToList();

                return allPlatforms
                    .Where(p => p.RommName != null &&
                                p.RommName.Contains(PlatformSearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public PlatformsPageVM()
        {

        }


        public PlatformsPageVM(SettingsService settingsService, LaunchboxDataService launchboxService,
            RommService rommService, LoggingService loggingService, SyncManager syncManager, AddNewPlatformUcVM addNewPlatformVm,
            LaunchboxLocalDatabaseMapper launchboxLocalDatabaseMapper)
        {
            _settingsService = settingsService;
            _launchboxDataService = launchboxService;
            _rommService = rommService;
            _loggingService = loggingService;
            _syncManager = syncManager;
            _addNewPlatformVm = addNewPlatformVm;
            _launchboxLocalDatabaseMapper = launchboxLocalDatabaseMapper;

            WeakReferenceMessenger.Default.Register<DeleteLaunchboxPlatformItemMessage>(this);

            // order matters lbPlatforms depends on RommServers being loaded
            LoadPersistedRommServers();
            LoadLaunchboxPlatforms();
        }

        /// <summary>
        /// Used in AddNewLaunchboxPlatform: communicates with the View layer asynchronously
        /// </summary>
        public event Func<Task<string>> RequestAddPlatformNameDialog;

        public event ConfirmationDialogHandler RequestConfirmationDialog;

        public delegate Task<bool> ConfirmationDialogHandler(string title, string message);

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
            else
            {
                PersistPlatformSyncSettings();
            }
        }

        void IRecipient<DeleteLaunchboxPlatformItemMessage>.Receive(DeleteLaunchboxPlatformItemMessage message)
        {
            DeleteLaunchboxPlatformItem(message.Value);
            OnPropertyChanged(nameof(FilteredLaunchboxPlatforms));
        }

        [RelayCommand]
        private async Task AddNewLaunchboxPlatform()
        {
            // AddNewPlatformUcView.View Model returns:
            // Selected[Platform/Emulator] - The default platform/emulator taken from the launchbox.metadata.db
            
            var addPlatformDialog = new AddNewPlatformUcView(_addNewPlatformVm);

            ContentDialog dialog = new ContentDialog
            {
                Title = "Please select a default Launchbox Platform to add",
                Content = addPlatformDialog,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel"
            };

            addPlatformDialog.ViewModel.ClearData();

            LaunchboxPlatformsInfoBar.IsOpen = false;

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary) return;

            var dialogVM = addPlatformDialog.ViewModel;


            // Checks
            if (dialogVM.InfoSeverity != InfoBarSeverity.Success)
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Error, "Add new Platform Error",
                    $"Errors in the Platform setup: '{dialogVM.InfoMessage}'");
                return;
            }

            // This one is legacy and lost undertsaanding of it. Kept in in case detects edge cases
            // need to do a check of the actual LB database in case Auto-import cause re-creation 
            // of the platform without rommstar/user knowing (bloody auto import!)
            if (PluginHelper.DataManager.GetPlatformByName(dialogVM.SelectedDefaultPlatform.Name) != null)
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Error, "Add new Platform Error", "Platform name exists in the Launchbox database backend. " +
                    "Launchbox Auto-import can sometimes re-create Platforms even after their removal if roms still exist in the platforms folder " +
                    "(may not be visible in Launchbox).");
                return;
            }

            // at this point, you will have:
            // `Selected[Platform/Emulator]` - The lb db default platform/emulator abstraction (eg. LaunchboxDbEmulatorDTO) taken from the launchbox.metadata.db
            // `UserEmulator` A populated IEmulator if it already exists in the lb local db (eg. retroarch - multi-system). Unpopulated if not.
            // ExePath = the path to the exe for the Emulator

            // If UserEmulator null, instantiate a new emulator and populate with default data
            if (dialogVM.UserEmulator == null)
            {
                dialogVM.UserEmulator = PluginHelper.DataManager.AddNewEmulator();
                // do this in here as if already exists - likely added via laucnhbox and want to preserve properties as set by that
                _launchboxLocalDatabaseMapper.EmulatorDtoToIEmulator(dialogVM.SelectedDefaultEmulator, dialogVM.UserEmulator);
            }

            dialogVM.UserEmulator.ApplicationPath = dialogVM.ExePath;

            // Now process either existing or new IEmulator record (I think LB populates Retroarch with all the EmulatorPlatform
            // data for all platforms when you add retroarch. So you there may be a recorf for the emu/plat combination despite 
            // not having set it up.
            IEmulatorPlatform iEmulatorPlatform = dialogVM.UserEmulator.GetAllEmulatorPlatforms()
                .Where(ep => ep.Platform.Equals(dialogVM.SelectedDefaultPlatform.Name, StringComparison.OrdinalIgnoreCase) &&
                ep.EmulatorId.Equals(dialogVM.UserEmulator.Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // This is the lookup
            LaunchboxDbEmulatorPlatformDTO launchboxDbEmulatorPlatformDTO = dialogVM.DefaultEmultorPlatforms
                 .Where(ep => ep.Emulator.Equals(dialogVM.UserEmulator.Title, StringComparison.OrdinalIgnoreCase) &&
                 ep.Platform.Equals(dialogVM.SelectedDefaultPlatform.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // again, this shouldn't be null but do a check jic
            if (launchboxDbEmulatorPlatformDTO == null)
            {
                Debug.WriteLine("Error getting Default values for EmulatorPlatform. Cannot continue");
                return;
            }

            if (iEmulatorPlatform == null)
            {
                iEmulatorPlatform = dialogVM.UserEmulator.AddNewEmulatorPlatform(); // i think this also populates iEmuPLat.EmulatorId?
            }

            iEmulatorPlatform.CommandLine = launchboxDbEmulatorPlatformDTO.CommandLine;
            iEmulatorPlatform.Platform = dialogVM.SelectedDefaultPlatform.Name;
            //iEmulatorPlatform.IsDefault = launchboxDbEmulatorPlatformDTO.Recommended;
            iEmulatorPlatform.IsDefault = true; // ensures the selected emulator is used for this platform
            iEmulatorPlatform.M3uDiscLoadEnabled = dialogVM.M3uDiskLoadEnabled == true;
            iEmulatorPlatform.AutoExtract = dialogVM.AutoExtract;

            // now update any IEmulator Properties
            if (string.IsNullOrEmpty(dialogVM.UserEmulator.CommandLine) && !string.IsNullOrEmpty(iEmulatorPlatform.CommandLine))
                dialogVM.UserEmulator.CommandLine = iEmulatorPlatform.CommandLine;


            // we know the platform doesn't exist as AddNewPlatformUcView deosn't exist, so create new
            IPlatform newIPlatform = PluginHelper.DataManager.AddNewPlatform(dialogVM.SelectedDefaultPlatform.Name);

            // pretty sure this should not ever be null
            LaunchboxDbPlatformDTO lbDbPlatformDTO = dialogVM.DefaultPlatforms.Where(p => 
                        p.Name.Equals(dialogVM.SelectedDefaultPlatform.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            _launchboxLocalDatabaseMapper.PlatformDtoToIPlatform(lbDbPlatformDTO, newIPlatform);

            newIPlatform.ScrapeAs = newIPlatform.Name;
            
            PluginHelper.DataManager.Save();
            PluginHelper.DataManager.ForceReload();

            SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Success, "Added new Platform", $"New Platform {newIPlatform.Name} added successfully");
            LoadLaunchboxPlatforms();
            OnPropertyChanged(nameof(FilteredLaunchboxPlatforms));
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
        private async Task DeleteSelectedLaunchboxPlatform()
        {
            if (RequestConfirmationDialog == null) return;
            LaunchboxPlatformsInfoBar.IsOpen = false;

            // Fire the event and await the boolean return (true = confirmed)
            bool confirmed = await RequestConfirmationDialog.Invoke("Confirm Deletion?",
                $"This will permanently delete the platform \"{SelectedPlatform.LaunchboxPlatformName}\" from your Launchbox. Are you sure?");

            if (!confirmed)
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Informational, "Delete Platform", $"Platform deletion cancelled");
                return;
            }

            // deal with IsOrphaned case (doesn't need launchbox deletion)
            if (SelectedPlatform.IsOrphaned)
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Success, "Deleted Platform",
                    $"Orphaned Platform \"{SelectedPlatform.LaunchboxPlatformName}\" deleted successfully");

                DeleteLaunchboxPlatformItem(SelectedPlatform);
                return;
            }

            bool successfulDeletion = await _launchboxDataService.DeletePlatform(SelectedPlatform.LaunchboxPlatformName);

            if (successfulDeletion)
            {
                _settingsService.Settings.PlatformSyncSettings.RemoveAll(pss => pss.LaunchboxPlatformName == SelectedPlatform.LaunchboxPlatformName);
                _settingsService.Save();

                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Success, "Deleted Platform",
                    $"Launchbox Platform \"{SelectedPlatform.LaunchboxPlatformName}\" deleted successfully");

                LaunchboxPlatformItems.Remove(SelectedPlatform); // this MUST be last given SelectedPlatform.LaunchboxPlatformName refs above

                LoadLaunchboxPlatforms();
            }
            else
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Error, "Delete Platform Error",
                    $"Launchbox Platform \"{SelectedPlatform.LaunchboxPlatformName}\" could not be deleted via the Launchbox API.");
            }
        }

        private RommServerItemVM GetRommServerItemByServerId(string id)
        {
            return RommServerItems.FirstOrDefault(rs => rs.RommServer.Id == id);
            //return RommServerItems.Where(rs => rs.RommServer.Id == id).FirstOrDefault();
        }

        private async void LoadLaunchboxPlatforms()
        {
            // Get current LB platforms
            var liveLbPlatformDtos = _launchboxDataService.GetUserPlatforms();

            LaunchboxPlatformItems.Clear();

            foreach (var liveLbPlatform in liveLbPlatformDtos)
            {
                LaunchboxPlatformItemVM newLaunchboxPlatformItemVM = new LaunchboxPlatformItemVM(liveLbPlatform.Name, liveLbPlatform.RomFolder);

                string votiIconPath = _launchboxDataService.GetPlatformIconPath(liveLbPlatform.Name);

                if (File.Exists(votiIconPath))
                {
                    newLaunchboxPlatformItemVM.IconPath = votiIconPath;
                }

                // Test persisted Platform Maps for existing map
                PlatformSyncSettings? matchedPersistedPlatform = _settingsService.Settings.PlatformSyncSettings
                    .Where(pss => pss.LaunchboxPlatformName == liveLbPlatform.Name).FirstOrDefault();

                if (matchedPersistedPlatform != null)
                {
                    // There is a match. There is no guarantee that a persisted server is still registered in RommStar. Check and flag error if not.
                    RommServerItemVM? matchedRommServerItem = GetRommServerItemByServerId(matchedPersistedPlatform.RommServerId);

                    if (matchedRommServerItem != null)
                    {
                        newLaunchboxPlatformItemVM.AssignedServerItem = matchedRommServerItem;
                        // Assign the previously matched Romm PlatformIds only if server still in RommStar setup (no point if not)
                        newLaunchboxPlatformItemVM.MatchedRommPlatforms = new ObservableCollection<PlatformDTO>(matchedPersistedPlatform.RommServerPlatforms);
                        newLaunchboxPlatformItemVM.ExtendedSyncSettings = matchedPersistedPlatform.ExtendedSyncSettings;
                    }
                }

                LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
            }

            // now test for orphans in persisted PlatformSyncSettings (i.e. those with Lb PLatformName not in current LB collection
            foreach (PlatformSyncSettings platformSyncSettings in _settingsService.Settings.PlatformSyncSettings)
            {
                if (!LaunchboxPlatformItems.Any(item => item.LaunchboxPlatformName == platformSyncSettings.LaunchboxPlatformName))
                {
                    var matchedServer = GetRommServerItemByServerId(platformSyncSettings.RommServerId);

                    LaunchboxPlatformItemVM newLaunchboxPlatformItemVM = new LaunchboxPlatformItemVM()
                    {
                        LaunchboxPlatformName = platformSyncSettings.LaunchboxPlatformName,
                        MatchedRommPlatforms = new ObservableCollection<PlatformDTO>(platformSyncSettings.RommServerPlatforms),
                        IsOrphaned = true,
                        AssignedServerItem = matchedServer
                    };

                    string votiIconPath = _launchboxDataService.GetPlatformIconPath(platformSyncSettings.LaunchboxPlatformName);
                    if (File.Exists(votiIconPath))
                    {
                        newLaunchboxPlatformItemVM.IconPath = votiIconPath;
                    }
                    ;

                    LaunchboxPlatformItems.Add(newLaunchboxPlatformItemVM);
                }
            }

            OnPropertyChanged(nameof(FilteredLaunchboxPlatforms));
        }

        /// <summary>
        /// This only populates the RommServerItemVM.Server, not the ServerPlatformDTOs - this done elsewhere to
        /// prevent delays form the API call to Romm API
        /// </summary>
        private async void LoadPersistedRommServers()
        {
            foreach (var rommServer in _settingsService.Settings.RommServers)
            {
                RommServerItemVM existingRommServer = RommServerItems.Where(rs => rs.RommServer.Id == rommServer.Id).FirstOrDefault();
                if (existingRommServer != null)
                {
                    existingRommServer.RommServer = rommServer;
                }
                else
                {
                    RommServerItems.Add(new RommServerItemVM(rommServer));
                }
            }
        }

        private async Task LoadRommServersPlatformDTOs()
        {
            foreach (var rommServer in RommServerItems)
            {
                await LoadServerPlatformsAsync(rommServer);
            }
        }

        /// <summary>
        /// This also populates RommServerPLatformsDTOs
        /// </summary>
        /// <param name="rommServerItem"></param>
        /// <param name="showMessage">false suppresses info bar (for silent running calls - pip color suffices)</param>
        /// <param name="forceRefesh">forces update of RommPlatformDTOs. Otherwise follows cache system where initial load is canon of RommServer platforms</param>
        /// <returns></returns>
        private async Task LoadServerPlatformsAsync(RommServerItemVM rommServerItem, bool showMessage = false, bool forceRefesh = false)
        {
            if (rommServerItem == null) return;

            // Return early if cached and not forcing refresh
            if (!forceRefesh && _rommPlatformCache.ContainsKey(rommServerItem.RommServer.Id))
            {
                return;
            }

            RommApiResponse<List<PlatformDTO>> rommPlatformsQuery = await _rommService.GetRommPlatformsAsync(rommServerItem.RommServer);

            if (!rommPlatformsQuery.IsSuccess)
            {
                StringBuilder sb = new StringBuilder($"Romm Server: {rommServerItem.RommServer.ServerName}\r\n" +
                    $"Issue: {rommPlatformsQuery.FailureReason}\r\n");
                if (rommPlatformsQuery.HttpResponse != null) sb.AppendLine(rommPlatformsQuery.HttpResponse.ToString());
                if (rommPlatformsQuery.ExceptionMessage != null) sb.Append(rommPlatformsQuery.ExceptionMessage);

                rommServerItem.InfoBar = PopulatedInfoBar("Romm Server Error", sb.ToString(), isOpen: showMessage, InfoBarSeverity.Error);

                // Need to clear cache to withdraw any server platforms
                if (_rommPlatformCache.ContainsKey(rommServerItem.RommServer.Id))
                {
                    _rommPlatformCache.Remove(rommServerItem.RommServer.Id);

                    // ensure list is cleared in UI
                    if (SelectedRommServer?.RommServer.Id == rommServerItem.RommServer.Id)
                    {
                        OnPropertyChanged(nameof(CurrentServerPlatforms));
                    }
                }
                return;
            }

            // Store in cache
            _rommPlatformCache[rommServerItem.RommServer.Id] = new ObservableCollection<PlatformDTO>((List<PlatformDTO>)rommPlatformsQuery.Data);

            rommServerItem.InfoBar = PopulatedInfoBar("Success", $"{_rommPlatformCache[rommServerItem.RommServer.Id].Count} Romm Platforms retrieved successfully", isOpen: showMessage, InfoBarSeverity.Success);

            // Notify UI if this was the currently selected server
            if (SelectedRommServer?.RommServer.Id == rommServerItem.RommServer.Id)
            {
                OnPropertyChanged(nameof(CurrentServerPlatforms));
            }
        }

        partial void OnPlatformSearchTextChanged(string value)
        {
        }

        partial void OnSelectedPlatformChanged(LaunchboxPlatformItemVM value)
        {
            if (value == null) return;

            // first save settings - persists any changes to disk
            _settingsService.Save();

            if (((LaunchboxPlatformItemVM)value).AssignedServerItem == null)
            {
                SelectedRommServer = null;
                return;
            }

            SelectedRommServer = GetRommServerItemByServerId(((LaunchboxPlatformItemVM)value).AssignedServerItem.RommServer.Id);
        }

        partial void OnSelectedRommPlatformChanged(PlatformDTO? value)
        {
            if (value == null) return;

            // Adds Romm Platform to the Lauchbox PLatform Map if not already mapped.
            if (SelectedPlatform != null && !SelectedPlatform.MatchedRommPlatforms.Any(rpd => rpd.RommId == value.RommId))
            {
                SelectedPlatform.MatchedRommPlatforms.Add(value);
                OnPropertyChanged(nameof(SelectedPlatform));
            }
        }

        partial void OnSelectedRommServerChanged(RommServerItemVM newValue)
        {
            if (newValue is null)
                return;

            if (SelectedPlatform?.AssignedServerItem is null || SelectedPlatform.AssignedServerItem.RommServer.Id != newValue.RommServer.Id)
            {
                SelectedPlatform.AssignedServerItem = SelectedRommServer;
                SelectedPlatform.MatchedRommPlatforms.Clear();
            }
        }

        private void PersistPlatformSyncSettings()
        {
            _settingsService.Settings.PlatformSyncSettings.Clear();
            foreach (LaunchboxPlatformItemVM launchboxPlatformItem in LaunchboxPlatformItems)
            {
                PlatformSyncSettings platformSyncSettings = new PlatformSyncSettings()
                {
                    LaunchboxPlatformName = launchboxPlatformItem.LaunchboxPlatformName,
                    RommServerPlatforms = launchboxPlatformItem.MatchedRommPlatforms.ToList(),
                    ExtendedSyncSettings = launchboxPlatformItem.ExtendedSyncSettings
                };

                // Server can be null
                if (launchboxPlatformItem.AssignedServerItem != null)
                {
                    platformSyncSettings.RommServerId = launchboxPlatformItem.AssignedServerItem.RommServer.Id;
                }

                _settingsService.Settings.PlatformSyncSettings.Add(platformSyncSettings);
            }
            _settingsService.Save();
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

        /// <summary>
        /// Pre-load all server platforms on page load
        /// </summary>
        private async Task PreloadAllServerPlatformsAsync()
        {
            var loadTasks = RommServerItems.Select(server => LoadServerPlatformsAsync(server));
            await Task.WhenAll(loadTasks);
        }

        [RelayCommand]
        private async Task RefreshRommServerPlatforms(RommServerItemVM rommServer)
        {
            await LoadServerPlatformsAsync(rommServer, showMessage: true, forceRefesh: true);
        }

        [RelayCommand]
        private async Task ReloadLaunchboxPlatforms()
        {
            //LoadLaunchboxPlatforms();
            //OnPropertyChanged(nameof(FilteredServerPlatforms));

            // 1. Cache the name of the platform the user had highlighted before the reload
            string? previouslySelectedName = SelectedPlatform?.LaunchboxPlatformName;

            // 2. Run your existing structural reload
            LoadLaunchboxPlatforms();

            // 3. Force the UI to re-evaluate the read-only filtered property
            OnPropertyChanged(nameof(FilteredLaunchboxPlatforms));

            // 4. Restore the selection by finding the equivalent item in the newly loaded set
            if (!string.IsNullOrEmpty(previouslySelectedName))
            {
                SelectedPlatform = LaunchboxPlatformItems.FirstOrDefault(p =>
                    p.LaunchboxPlatformName.Equals(previouslySelectedName, StringComparison.OrdinalIgnoreCase));
            }
        }

        [RelayCommand]
        private async Task RemoveRommPlatformFromMap(PlatformDTO? rommPlatformId)
        {
            if (SelectedPlatform.MatchedRommPlatforms.Contains(rommPlatformId))
                SelectedPlatform.MatchedRommPlatforms.Remove(rommPlatformId);

            if (SelectedRommPlatform?.RommId == rommPlatformId.RommId)
            {
                SelectedRommPlatform = null;
            }
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
                if (launchboxPLatformItem.MatchedRommPlatforms != null) newPlatformSyncSettings.RommServerPlatforms = launchboxPLatformItem.MatchedRommPlatforms.ToList();

                _settingsService.Settings.PlatformSyncSettings.Add(newPlatformSyncSettings);
            }
            _settingsService.Save();
        }

        private async void SetInfoBar(InfoBar infoBar, bool isOpen, InfoBarSeverity severity, string title,
           string message, int autoCloseSeconds = 0)
        {
            // Cancel any existing auto-close timer
            _infoBarCts?.Cancel();
            _infoBarCts?.Dispose();
            _infoBarCts = null;

            infoBar.Title = title;
            infoBar.Message = message;
            infoBar.Severity = severity;
            infoBar.IsOpen = isOpen;

            // If autoCloseSeconds <= 0, it remains open indefinitely
            if (isOpen && autoCloseSeconds > 0)
            {
                _infoBarCts = new CancellationTokenSource();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(autoCloseSeconds), _infoBarCts.Token);
                    infoBar.IsOpen = false;
                }
                catch (TaskCanceledException)
                {
                    // Ignored - triggered if a new InfoBar message replaces this one before timeout
                }
            }
        }

        [RelayCommand]
        private async Task SyncSelectedPlatform()
        {
            if (SelectedPlatform == null) return;

            if (SelectedPlatform.AssignedServerItem == null)
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "No server assigned to this platform. Cannot Sync.");
                return;
            }

            if (SelectedPlatform.MatchedRommPlatforms.Count == 0)
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "No Romm Platforms matched to this Launchbox Platform. Cannot Sync.");
                return;
            }

            if (SelectedPlatform.IsOrphaned)
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "You cannot Sync an orphaned platform as it no longer exists in Launchbox." +
                    " Either recreate it or delete it from the PLatforms list.");
                return;
            }

            IPlatform platform = PluginHelper.DataManager.GetPlatformByName(SelectedPlatform.LaunchboxPlatformName);
            if (platform == null)
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "Platform cannot be retrieved from the Launchbox database." +
                    " This can happen if Launchbox and RommStar Platform adds/deletes fall out of sync.");
                return;
            }

            if (_syncManager.PlatformQueuedAndIncomplete(SelectedPlatform.LaunchboxPlatformName))
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "A Sync Job for this Platform is already in the queue.");
                return;

            }

            // do a settigns save to ensure future processes have up to date settings
            _settingsService.Save();

            string platformDefaultEmulatorID = _launchboxDataService.GetPlatformDefaultEmulatorID(SelectedPlatform.LaunchboxPlatformName);

            if (platformDefaultEmulatorID == null)
            {
                ContentDialog dialog = new ContentDialog();
                dialog.Title = "Platform Sync Warning";
                dialog.Content = $"This Platform has no default emulator associated with it. Games will be created/updated with no link to an emulator so will not boot.\r\n \r\nYou can back out of this and set up an emulator for this Platform in Launchbox.\r\n \r\nAre you sure you wish to continue?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No/Cancel";

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Secondary) return;
            }

            PersistPlatformSyncSettings();

            // note: at this stage, this list may include orphaned platforms that have been persisted in user settings,
            // but then deleted later on the romm server.

            List<int> rommPlatformIds = SelectedPlatform.MatchedRommPlatforms.Select(p => p.RommId).ToList();

            if (rommPlatformIds.Count == 0)
            {
                SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                    InfoBarSeverity.Error, "Sync Platform Error", "Cannot Sync: No RomM platforms set against this Launchbox Platform.");
                return;
            }

            // figure whether to send global extendedSyncSettings or platform specific
            ExtendedSyncSettings resolvedExtSyncSettings = _settingsService.Settings.GlobalExtendedSyncSettings;
            if (SelectedPlatform.ExtendedSyncSettings.ApplySettings)
                resolvedExtSyncSettings = SelectedPlatform.ExtendedSyncSettings;

            IPlatformFolder[] mediaFolders = platform.GetAllPlatformFolders();

            // figure total rom count across all romm platforms for the LB platform.
            int combinedRomCount = (int)SelectedPlatform.MatchedRommPlatforms.Sum(x => x.RomCount);

            // Queue PLatform
            _syncManager?.EnqueuePlatformSync(SelectedPlatform.LaunchboxPlatformName, SelectedPlatform.LaunchboxPlatformRomFolder,
                mediaFolders, platformDefaultEmulatorID, rommPlatformIds, resolvedExtSyncSettings, SelectedPlatform.AssignedServerItem.RommServer,
                combinedRomCount);

            SetInfoBar(LaunchboxRommPlatformsInfoBar, true,
                        InfoBarSeverity.Informational, "Platform Sync Started", $"Sync started for {platform.Name}. " +
                        $"See Sync Jobs page for progress.", autoCloseSeconds: 3);

        }

        [RelayCommand]
        private void Test()
        {
            SavePlatformSyncSettings();
        }



        [RelayCommand]
        private async Task UpdatePlatformIcon()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = "Select a new platform icon",
                Filter = "Image files (*.png)|*.png"
            };

            if (openFileDialog.ShowDialog() == false) return;

            var result = _launchboxDataService.SaveNewPlatformIcon(openFileDialog.FileName, SelectedPlatform.LaunchboxPlatformName, true);

            if (result != null)
            {
            }
            else
            {
                SelectedPlatform.IconPath = _launchboxDataService.GetPlatformIconPath(SelectedPlatform.LaunchboxPlatformName);
                OnPropertyChanged(nameof(SelectedPlatform));
                OnPropertyChanged(nameof(LaunchboxPlatformItems));
                SelectedPlatform.RefreshIcon();
            }
        }
    }
}