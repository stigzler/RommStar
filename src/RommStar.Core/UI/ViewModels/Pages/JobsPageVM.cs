using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.Pages
{
    public partial class JobsPageVM : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<PlatformSyncJob> _activeJobs;

        [ObservableProperty]
        private bool _hideSuccessEntries = true;

        private readonly SyncManager? _syncManager;

        private readonly SettingsService _settingsService;

        public JobsPageVM()
        {
        }

        partial void OnHideSuccessEntriesChanged(bool value)
        {
            _settingsService.Settings.HideSuccessEntries = value;
            _settingsService.Save();
        }

        public JobsPageVM(SyncManager syncManager, SettingsService settingsService)
        {
            _syncManager = syncManager;
            _activeJobs = _syncManager.ActiveSyncJobs;
            _settingsService = settingsService;

            HideSuccessEntries = _settingsService.Settings.HideSuccessEntries;
        }

        /// <summary>
        /// Test - remove in production
        /// </summary>
        [RelayCommand]
        private void StartSyncJob()
        {
            //_syncManager?.QueuePlatformSync("Atari 2600", new List<int>() { 1, 2, 4 }, false);
            //_syncManager?.QueuePlatformSync("Dave 2000", new List<int>() { 7, 9, 11 }, false);
            //_syncManager?.QueuePlatformSync("Amiga 7000", new List<int>() { 13, 19, 32 }, false);
        }

 

        [RelayCommand]
        private void CancelJob(Guid id)
        {
            if (id != Guid.Empty)
            {
                _syncManager.CancelPlatformSync(id);
            }
        }

        [RelayCommand]
        private void RemoveJobCard(Guid id)
        {
            _activeJobs.Remove(_activeJobs.Where(aj => aj.Id == id).FirstOrDefault());
        }

    }
}