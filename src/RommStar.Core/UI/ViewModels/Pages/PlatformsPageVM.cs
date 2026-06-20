using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Mappers;
using RommStar.Core.Models;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using RommStar.Core.UI.Messages;
using RommStar.Core.UI.ViewModels.DataModels;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;

using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.UI.ViewModels.Pages
{
    //todo: re/load server on page navigate to (in case user adds/deletes a server)
    public partial class PlatformsPageVM : ObservableObject, IRecipient<DeleteLaunchboxPlatformItemMessage>
    {
        private readonly LaunchboxDataService
            _launchboxService;

        private readonly LoggingService
            _loggingService;

        /// <summary>
        /// ===== PERFORMANCE-CRITICAL: Centralized Cache =====
        /// Central cache for all Romm server platforms. Key = RommServer.Id
        /// </summary>
        private readonly Dictionary<Guid, ObservableCollection<PlatformDTO>>
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

        /// <summary>
        /// Parameterless constructor for the XAML Designer
        /// </summary>
        public PlatformsPageVM() : this(
            new SettingsService(new CryptoService()),
            new LaunchboxDataService(new RomMapper(new SettingsService(new CryptoService()))),
            new RommService(),
            new LoggingService(),
            new SyncManager(new RommServer(), new RommService(),
                new LaunchboxDataService(new RomMapper(new SettingsService(new CryptoService()))) // urgh. boy, thas uuuggllleeeeee! 🤮
                , new SettingsService(new CryptoService()))
            )
        {
            // any test data
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // DisplayPlatforms.Add(new MappedPlatformItemVM("Super Nintendo", false));
            }
        }

        public PlatformsPageVM(SettingsService settingsService, LaunchboxDataService launchboxService,
            RommService rommService, LoggingService loggingService, SyncManager syncManager)
        {
            _settingsService = settingsService;
            _launchboxService = launchboxService;
            _rommService = rommService;
            _loggingService = loggingService;
            _syncManager = syncManager;

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
            if (RequestAddPlatformNameDialog == null) return;
            LaunchboxPlatformsInfoBar.IsOpen = false;

            // Fire the event and await the text input from the dialog
            string votiNewPlatformName = await RequestAddPlatformNameDialog.Invoke();
            // remove unsafe filename chars + trim
            votiNewPlatformName = Core.Helpers.StringsHelper.SanitizeFileName(votiNewPlatformName).Trim();

            // On blank, or platform name already existing (must be unique in lb), return error
            // Check rommStar cached platforms
            if (string.IsNullOrWhiteSpace(votiNewPlatformName) ||
                LaunchboxPlatformItems.Any(lpi => lpi.LaunchboxPlatformName.ToLower() == votiNewPlatformName.ToLower()))
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Error, "Add new Platform Error", "Platform name was null or already exists. It has to be unique.");
                return;
            }

            // need to do a check of the actual LB database in case Auto-import cause re-creation 
            // of the platform without rommstar/user knowing (bloody auto import!)
            if (PluginHelper.DataManager.GetPlatformByName(votiNewPlatformName) != null)
            {
                SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Error, "Add new Platform Error", "Platform name exists in Launchbox backend database. Launchbox Auto-import can sometimes re-create Platforms even after their removal if roms still exist in the platforms folder (may not be visible in Launchbox).");
                return;
            }


            // Success!
            _launchboxService.CreateNewPlatform(votiNewPlatformName);
            SetInfoBar(LaunchboxPlatformsInfoBar, true, InfoBarSeverity.Success, "Added new Platform", $"New Platform {votiNewPlatformName} added successfully");
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

            bool successfulDeletion = await _launchboxService.DeletePlatform(SelectedPlatform.LaunchboxPlatformName);

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

        private RommServerItemVM GetRommServerItemByServerId(Guid id)
        {
            return RommServerItems.FirstOrDefault(rs => rs.RommServer.Id == id);
            //return RommServerItems.Where(rs => rs.RommServer.Id == id).FirstOrDefault();
        }

        private async void LoadLaunchboxPlatforms()
        {
            // Get current LB platforms
            var liveLbPlatformDtos = _launchboxService.GetPlatforms();

            LaunchboxPlatformItems.Clear();

            foreach (var liveLbPlatform in liveLbPlatformDtos)
            {
                LaunchboxPlatformItemVM newLaunchboxPlatformItemVM = new LaunchboxPlatformItemVM(liveLbPlatform.Name,
                    liveLbPlatform.RomFolder);

                string votiIconPath = _launchboxService.GetPlatformIconPath(liveLbPlatform.Name);

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

                    string votiIconPath = _launchboxService.GetPlatformIconPath(platformSyncSettings.LaunchboxPlatformName);
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

        private void SetInfoBar(InfoBar infoBar, bool isOpen, InfoBarSeverity severity, string title, string message)
        {
            infoBar.IsOpen = isOpen;
            infoBar.Title = title;
            infoBar.Message = message;
            infoBar.Severity = severity;
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
                    InfoBarSeverity.Error, "Sync Platform Error", "Platform cannot be retrieved fom the Launchbox database." +
                    " This can happen if Launchbox and RommStar Platform adds/deletes fall out of sync.");
                return;
            }

            string platformDefaultEmulatorID = _launchboxService.GetPlatformDefaultEmulatorID(SelectedPlatform.LaunchboxPlatformName);

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

            

            // note: at this stage, this list may include orphaned platforms that have been persisted in user settings,
            // but then deleted later on the romm server.
            List<int> rommPlatformIds = SelectedPlatform.MatchedRommPlatforms.Select(p => p.RommId).ToList();

            ExtendedSyncSettings resolvedExtSyncSettings = _settingsService.Settings.GlobalExtendedSyncSettings;

            if (SelectedPlatform.ExtendedSyncSettings.ApplySettings)
                resolvedExtSyncSettings = SelectedPlatform.ExtendedSyncSettings;

            IPlatformFolder[] mediaFolders = platform.GetAllPlatformFolders();

      



            // Queue PLatform
            _syncManager?.QueuePlatformSync(SelectedPlatform.LaunchboxPlatformName, SelectedPlatform.LaunchboxPlatformRomFolder,
                mediaFolders, platformDefaultEmulatorID, rommPlatformIds, resolvedExtSyncSettings, SelectedPlatform.AssignedServerItem.RommServer);

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

            var result = _launchboxService.SaveNewPlatformIcon(openFileDialog.FileName, SelectedPlatform.LaunchboxPlatformName, true);

            if (result != null)
            {
            }
            else
            {
                SelectedPlatform.IconPath = _launchboxService.GetPlatformIconPath(SelectedPlatform.LaunchboxPlatformName);
                OnPropertyChanged(nameof(SelectedPlatform));
                OnPropertyChanged(nameof(LaunchboxPlatformItems));
                SelectedPlatform.RefreshIcon();
            }
        }
    }
}