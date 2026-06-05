using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class JobsPageVM : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<PlatformSyncJob> _activeJobs;

        private readonly RommService? _rommService;

        public JobsPageVM()
        {
        }

        public JobsPageVM(RommService rommService)
        {
            _rommService = rommService;
            _activeJobs = _rommService.ActiveSyncJobs;
        }

        [RelayCommand]
        private void StartSyncJob()
        {
            _rommService?.QueuePlatformSync("Atari 2600", new List<int>() { 1, 2, 4 }, false);
            _rommService?.QueuePlatformSync("Dave 2000", new List<int>() { 7, 9, 11 }, false);
            _rommService?.QueuePlatformSync("Amiga 7000", new List<int>() { 13, 19, 32 }, false);
        }

        [RelayCommand]
        private void CancelJob(Guid id)
        {
            if (id != Guid.Empty)
            {
                _rommService.CancelPlatformSync(id);
            }
        }
    }
}