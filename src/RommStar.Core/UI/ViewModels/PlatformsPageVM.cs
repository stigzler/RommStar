using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Services;

namespace RommStar.Core.UI.ViewModels
{
    public partial class PlatformsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly LaunchboxService _launchboxService;
        private readonly RommService _rommService;

        private string? _loadedServerNameInUi;

        // Master Left Sidebar Collection
        public ObservableCollection<MappedPlatformItemVM> DisplayPlatforms { get; } = new();

        // Detail Right Panel dropdown & selection targets
        public ObservableCollection<RommServer> AvailableServers { get; } = new();

        //public ObservableCollection<RommPlatformDTO> CurrentServerAvailablePlatforms { get; } = new();
        [ObservableProperty]
        private List<RommPlatformDTO> _currentServerAvailablePlatforms = new();

        [ObservableProperty] private MappedPlatformItemVM? _selectedPlatform;

        [ObservableProperty] private bool _isBusy; // Tracks loading state

        // Cache memory dictionary to avoid slamming RomM endpoints on rapid UI clicks
        private readonly Dictionary<string, List<RommPlatformDTO>> _rommPlatformsCacheByServer = new(StringComparer.OrdinalIgnoreCase);

        // 1. Parameterless constructor for the XAML Designer
        public PlatformsPageVM() : this(
            new SettingsService(new CryptoService()),
            new LaunchboxService(),
            new RommService())
        {
            // If you need design-time specific setup, do it here safely:
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

            PopulateServersDropdown();
            _ = LoadAndReconcileDataAsync();
        }

        private void PopulateServersDropdown()
        {
            AvailableServers.Clear();
            foreach (var server in _settingsService.Settings.RommServers)
            {
                AvailableServers.Add(server);
            }
        }

        private async Task LoadAndReconcileDataAsync()
        {
            // 1. Fetch live canon platform array from LaunchBox
            List<LaunchboxPlatformDTO> liveLbPlatforms = _launchboxService.GetPlatforms();

            // 2. Fetch target persistence configuration data list
            List<PlatformSyncSettings> savedSettings = _settingsService.Settings.PlatformSyncSettings
                ?? new List<PlatformSyncSettings>();

            DisplayPlatforms.Clear();

            // 3. Process Live LaunchBox Platforms
            foreach (var lbPlatform in liveLbPlatforms)
            {
                var rowVM = new MappedPlatformItemVM(lbPlatform.Name, isOrphaned: false)
                {
                    //IconPath = _launchboxService.ResolvePlatformIconPath(lbPlatform.Name)
                };

                var match = savedSettings.FirstOrDefault(s => s.LaunchboxPlatformName.Equals(lbPlatform.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    rowVM.AssignedServer = AvailableServers.FirstOrDefault(s => s.ServerName == match.RommServer?.ServerName);
                    rowVM.StoredRommPlatformIds = match.RommServerPlatforms ?? new List<int>();
                }

                DisplayPlatforms.Add(rowVM);
            }

            // 4. SIMPLE ORPHAN RECONCILIATION
            foreach (var setting in savedSettings)
            {
                bool isLive = liveLbPlatforms.Any(p => p.Name.Equals(setting.LaunchboxPlatformName, StringComparison.OrdinalIgnoreCase));
                if (!isLive)
                {
                    var orphanVM = new MappedPlatformItemVM(setting.LaunchboxPlatformName, isOrphaned: true)
                    {
                        AssignedServer = AvailableServers.FirstOrDefault(s => s.ServerName == setting.RommServer?.ServerName),
                        StoredRommPlatformIds = setting.RommServerPlatforms ?? new List<int>()
                    };

                    DisplayPlatforms.Add(orphanVM);
                }
            }
        }

        /// <summary>
        /// Command to refresh the LaunchBox sidebar platforms and re-evaluate orphans dynamically.
        /// </summary>
        [RelayCommand]
        public async Task RefreshLaunchboxPlatforms()
        {
            // Store current selection to try and preserve UI state after sidebar rebuild
            string? previouslySelectedName = SelectedPlatform?.LaunchboxPlatformName;

            await LoadAndReconcileDataAsync();

            if (!string.IsNullOrEmpty(previouslySelectedName))
            {
                SelectedPlatform = DisplayPlatforms.FirstOrDefault(p => p.LaunchboxPlatformName == previouslySelectedName);
            }
        }

        /// <summary>
        /// Command to force-refresh the current active RomM Server's configuration platform listings.
        /// </summary>
        [RelayCommand]
        public async Task RefreshServerPlatforms()
        {
            if (SelectedPlatform?.AssignedServer == null) return;

            // Explicitly clear cache for this server
            _rommPlatformsCacheByServer.Remove(SelectedPlatform.AssignedServer.ServerName);

            // Force the UI property to refresh by clearing the tracker
            _loadedServerNameInUi = null;

            await UpdateWorkspacePlatformsAsync(SelectedPlatform.AssignedServer, SelectedPlatform);
        }

        partial void OnSelectedPlatformChanged(MappedPlatformItemVM? value)
        {
            //CurrentServerAvailablePlatforms = new List<RommPlatformDTO>(); // one notification, clear
            if (value == null) return;

            if (value.AssignedServer != null)
                _ = UpdateWorkspacePlatformsAsync(value.AssignedServer, value);
        }

        [RelayCommand]
        public async Task HandleServerSelectionChanged(RommServer? newServer)
        {
            if (SelectedPlatform == null) return;

            SelectedPlatform.MappedRommPlatforms.Clear();
            SelectedPlatform.StoredRommPlatformIds.Clear();
            CurrentServerAvailablePlatforms = new List<RommPlatformDTO>();

            SelectedPlatform.AssignedServer = newServer;

            if (newServer != null)
                await UpdateWorkspacePlatformsAsync(newServer, SelectedPlatform);
        }

        // not this
        private async Task UpdateWorkspacePlatformsAsync(RommServer server, MappedPlatformItemVM targetRow)
        {
            try
            {
                IsBusy = true; // Start "Wait" state
                List<RommPlatformDTO> serverPlatforms;

                if (!_rommPlatformsCacheByServer.TryGetValue(server.ServerName, out serverPlatforms!))
                {
                    // This is the network await - UI stays responsive
                    var result = await _rommService.GetRommPlatformsAsync(server);
                    serverPlatforms = result is { IsSuccess: true, Data: not null }
                        ? result.Data
                        : new List<RommPlatformDTO>();

                    _rommPlatformsCacheByServer[server.ServerName] = serverPlatforms;
                }

                if (_loadedServerNameInUi != server.ServerName)
                {
                    CurrentServerAvailablePlatforms = serverPlatforms;
                    _loadedServerNameInUi = server.ServerName;
                }

                // OPTIMIZATION: Use a Dictionary for O(1) lookups instead of FirstOrDefault in a loop
                var platformLookup = serverPlatforms.ToDictionary(p => p.RommId);

                targetRow.MappedRommPlatforms.Clear();
                foreach (var id in targetRow.StoredRommPlatformIds)
                {
                    if (platformLookup.TryGetValue(id, out var match))
                    {
                        targetRow.MappedRommPlatforms.Add(match);
                    }
                }
            }
            finally
            {
                IsBusy = false; // End "Wait" state
            }
        }

        // =========================================================================
        // AUTO-SAVE LOGIC ON PAGE TRANSITION
        // =========================================================================
        public void OnNavigatedAway()
        {
            var cleanSettingsList = new List<PlatformSyncSettings>();

            foreach (var rowVM in DisplayPlatforms)
            {
                if (rowVM.AssignedServer != null)
                {
                    var assignedIds = rowVM.MappedRommPlatforms.Any()
                        ? rowVM.MappedRommPlatforms.Select(r => r.RommId).ToList()
                        : rowVM.StoredRommPlatformIds;

                    cleanSettingsList.Add(new PlatformSyncSettings
                    {
                        LaunchboxPlatformName = rowVM.LaunchboxPlatformName,
                        RommServer = rowVM.AssignedServer,
                        RommServerPlatforms = assignedIds
                    });
                }
            }

            _settingsService.Settings.PlatformSyncSettings = cleanSettingsList;
            _settingsService.Save();
        }

        /// <summary>
        /// Executed from view event boundaries when a rich item is selected out of the active DropDownButton array flyout.
        /// </summary>
        public void AddMappedPlatformToSelectedRow(RommPlatformDTO platform)
        {
            if (SelectedPlatform == null || platform == null) return;

            // Guard against duplicates inside the mapping array
            if (!SelectedPlatform.MappedRommPlatforms.Any(p => p.RommId == platform.RommId))
            {
                SelectedPlatform.MappedRommPlatforms.Add(platform);

                // Ensure data bridge tracks state consistently in case they navigate away immediately
                if (!SelectedPlatform.StoredRommPlatformIds.Contains(platform.RommId))
                {
                    SelectedPlatform.StoredRommPlatformIds.Add(platform.RommId);
                }
            }
        }

        /// <summary>
        /// Executed when the user targets the '✕' close button embedded inside a platform token capsule row wrapper.
        /// </summary>
        public void RemoveMappedPlatformFromSelectedRow(RommPlatformDTO platform)
        {
            if (SelectedPlatform == null || platform == null) return;

            SelectedPlatform.MappedRommPlatforms.Remove(platform);
            SelectedPlatform.StoredRommPlatformIds.Remove(platform.RommId);
        }
    }
}