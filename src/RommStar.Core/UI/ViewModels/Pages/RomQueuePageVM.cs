using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RommStar.Core.Services;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using RommStar.Core.Extensions;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins;
using System.Numerics;

namespace RommStar.Core.UI.ViewModels.Pages
{
    public partial class RomQueuePageVM : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private static readonly object _queueLock = new object(); // Add this thread lock given queue can change from separate threads

        [ObservableProperty]
        private ObservableCollection<RomQueueItem> _queueItems = new();

        public ICollectionView GroupedQueueView { get; }

        // Event for the View to hook into for showing the confirmation dialog
        public event Func<string, string, Task<bool>> RequestConfirmationDialog;

        public RomQueuePageVM()
        {

        }

        public RomQueuePageVM(SettingsService settingsService)
        {
            _settingsService = settingsService;

            // 1. Point DIRECTLY to the live settings collection instead of taking a copy
            QueueItems = _settingsService.Settings.RomDownloadQueue ?? new ObservableCollection<RomQueueItem>();

            // Ensure settings has the reference if it was null
            if (_settingsService.Settings.RomDownloadQueue == null)
            {
                _settingsService.Settings.RomDownloadQueue = QueueItems;
            }

            // 2. Magic Line: Tells WPF it is safe for background threads to add/remove items from this list!
            BindingOperations.EnableCollectionSynchronization(QueueItems, _queueLock);

            GroupedQueueView = CollectionViewSource.GetDefaultView(QueueItems);

            GroupedQueueView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RomQueueItem.PlatformName)));
            GroupedQueueView.SortDescriptions.Add(new SortDescription(nameof(RomQueueItem.AddedAt), ListSortDirection.Ascending));
        }

        [RelayCommand]
        private void ToggleQuarantine(RomQueueItem item)
        {
            if (item == null) return;

            // Manually flip the state since we are no longer using a Two-Way ToggleButton
            item.IsQuarantined = !item.IsQuarantined;

            // Optionally reset the retry count so it gets a fresh set of attempts when resumed
            if (!item.IsQuarantined)
            {
                item.RetryCount = 0;
                item.UpdateQueueItemStatus(RomQueueItemStatus.Queued);
            }

            _settingsService.Save();
        }

        //[RelayCommand]
        //private void ToggleQuarantine(RomQueueItem item)
        //{
        //    if (item == null) return;
        //    // The UI's TwoWay binding already flipped the boolean, we just need to save.
        //    _settingsService.Save();
        //}

        [RelayCommand]
        private void RemoveItem(RomQueueItem item)
        {
            if (item == null) return;

            if (!string.IsNullOrEmpty(item.LaunchboxId))
            {
                SetIGameToUninstalled(item.LaunchboxId);
                PluginHelper.DataManager.Save();
            }

            QueueItems.Remove(item);
            _settingsService.Settings.RomDownloadQueue.RemoveAll(q => q.LaunchboxId == item.LaunchboxId);
            _settingsService.Save();
        }

        [RelayCommand]
        private async Task ClearPlatform(string platformName)
        {
            if (string.IsNullOrEmpty(platformName) || RequestConfirmationDialog == null) return;

            bool confirmed = await RequestConfirmationDialog.Invoke("Clear Platform Queue?",
                $"Are you sure you want to remove all pending downloads for {platformName}?");

            if (!confirmed) return;

            // 1. Snapshot the items FIRST before removing anything
            var itemsToRemove = QueueItems.Where(q => q.PlatformName == platformName).ToList();

            // 2. Process your IGame updates and remove the items one by one
            foreach (var item in itemsToRemove)
            {
                if (!string.IsNullOrEmpty(item.LaunchboxId))
                {
                    SetIGameToUninstalled(item.LaunchboxId);
                }

                // This removes it from the UI AND the underlying settings list simultaneously
                QueueItems.Remove(item);
            }

            // 3. Save both contexts
            _settingsService.Save();
            PluginHelper.DataManager.Save();
        }



        private void SetIGameToUninstalled(string launchboxID)
        {
            IGame game = PluginHelper.DataManager.GetGameById(launchboxID);

            if (game != null)
            {
                game.Installed = false;
                game.Status = "Not Installed";
                game.ApplicationPath = Constants.RomPlaceholder;

                var additionalApps = game.GetAllAdditionalApplications().Where(app => app.Section() == "Version");
                foreach (var additionalApp in additionalApps)
                {
                    additionalApp.Installed = false;
                    additionalApp.Status = "Not Installed";
                }
            }
        }
    }
}
