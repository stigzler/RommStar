using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class PlatformsPageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly LaunchboxService _launchboxService;
        private readonly RommService _rommService;

        public ObservableCollection<MappedPlatformItemVM> DisplayPlatforms { get; } = new();

        public PlatformsPageVM(SettingsService settingsService, LaunchboxService launchboxService, RommService rommService)
        {
            _settingsService = settingsService;
            _launchboxService = launchboxService;
            _rommService = rommService;

            // Fire-and-forget data loading safely
            _ = LoadAndReconcileDataAsync();
        }

        [RelayCommand]
        private async void Test()
        {
            List<LaunchboxPlatformDTO> liveLbPlatforms = _launchboxService.GetPlatforms();

            RommServer rommServer = new RommServer()
            {
                ApiToken = "rmm_7d639eb487bbe8ddae4a89996603dc4e13d43af1bf604bb7742330a46576557a",
                BaseUrl = "https://roms.stig.life",
                ServerName = "Dave"
            };

            var result = await _rommService.GetRommPlatformsAsync(rommServer);
        }

        private async Task LoadAndReconcileDataAsync()
        {
            // 1. Grab clean data boundaries directly from your specialized services
            List<LaunchboxPlatformDTO> liveLbPlatforms = _launchboxService.GetPlatforms();

            var result = await _rommService.GetRommPlatformsAsync(new Models.RommServer());

            List<RommPlatformDTO> liveRommPlatforms = new List<RommPlatformDTO>();

            if (result != null && !result.IsSuccess && result.Data != null)
            {
                liveRommPlatforms = result.Data;
            }

            // 2. Fetch the primitive dictionary map from settings
            var savedMap = _settingsService.Settings.LaunchboxRommPlatformsMap
                ?? new Dictionary<string, List<int>>();

            // 3. Process Live LaunchBox Platforms
            foreach (var lbPlatform in liveLbPlatforms)
            {
                var rowVM = new MappedPlatformItemVM(lbPlatform.Name, isOrphaned: false)
                {
                    //IconPath = _launchboxService.ResolvePlatformIconPath(lbPlatform.Name)
                };

                if (savedMap.TryGetValue(lbPlatform.Name, out var rommIds))
                {
                    PopulateRommMappings(rowVM, rommIds, liveRommPlatforms);
                }

                DisplayPlatforms.Add(rowVM);
            }

            // 4. SIMPLE ORPHAN DETECTION: Map entries missing from live LaunchBox
            foreach (var savedKey in savedMap.Keys)
            {
                bool isLive = liveLbPlatforms.Any(p => p.Name.Equals(savedKey, StringComparison.OrdinalIgnoreCase));
                if (!isLive)
                {
                    var orphanVM = new MappedPlatformItemVM(savedKey, isOrphaned: true);
                    PopulateRommMappings(orphanVM, savedMap[savedKey], liveRommPlatforms);

                    DisplayPlatforms.Add(orphanVM);
                }
            }
        }

        private void PopulateRommMappings(MappedPlatformItemVM rowVM, List<int> ids, List<RommPlatformDTO> availableRomm)
        {
            foreach (var id in ids)
            {
                var match = availableRomm.FirstOrDefault(r => r.RommId == id);
                if (match != null)
                {
                    rowVM.MappedRommPlatforms.Add(match);
                }
            }
        }

        // =========================================================================
        // AUTO-SAVE ON NAVIGATE AWAY
        // =========================================================================
        public void OnNavigatedAway()
        {
            var cleanMap = new Dictionary<string, List<int>>();

            foreach (var rowVM in DisplayPlatforms)
            {
                if (rowVM.MappedRommPlatforms.Any())
                {
                    cleanMap[rowVM.LaunchboxPlatformName] = rowVM.MappedRommPlatforms
                        .Select(romm => romm.RommId)
                        .ToList();
                }
            }

            _settingsService.Settings.LaunchboxRommPlatformsMap = cleanMap;
            _settingsService.Save();
        }
    }
}