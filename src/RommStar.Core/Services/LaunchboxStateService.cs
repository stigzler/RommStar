using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Extensions;
using RommStar.Core.Helpers;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Services
{
    /// <summary>
    /// Covers things like game launching/selection and LB/BB Views tasks
    /// </summary>
    public class LaunchboxStateService
    {
        private readonly LaunchboxDataService _launchboxDataService;
        private readonly SettingsService _settingsService;
        private readonly RommService _rommService;
        private readonly NotificationService _notificationService;
        private readonly SyncManager _syncManager;
        private CancellationTokenSource _onDemandCts = new CancellationTokenSource();

        public LaunchboxStateService(LaunchboxDataService launchboxDataService, SettingsService settingsService, RommService rommService,
                    NotificationService notificationService, SyncManager syncManager)
        {
            _launchboxDataService = launchboxDataService;
            _settingsService = settingsService;
            _rommService = rommService;
            _notificationService = notificationService;
            _syncManager = syncManager;
        }

        string _lastEmulatorApplicationPath;
        IEmulator _lastGameLaunchEmulator;

        internal void DoShutdownOperations()
        {
            _onDemandCts.Cancel();
            // Ensure that any manipulation of the last launch Emulator's application path
            // as part of the Game Install strategy is restored 
            RestoreGameLaunchEmulatorExe();
        }

        internal async Task SyncPlatform(string launchboxPlatformName)
        {
            // parameter computation and checks ----------------------------------------------------------

            string errorPrefix = $"ERROR: Could not start RomM Sync for [{launchboxPlatformName}]";

            // platform
            IPlatform platform = PluginHelper.DataManager.GetPlatformByName(launchboxPlatformName);
            if (platform == null)
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. Could not retrieve the Launchbox Platform");
                return;
            }

            if (_syncManager.PlatformQueuedAndIncomplete(platform.Name)) ;
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. A Sync Job for this Platform is already in the queue.");
                return;
            }

            // platform roms folder
            string platformRomsFolder = _launchboxDataService.GetLaunchboxRomsFolderPath(launchboxPlatformName);
            if (string.IsNullOrEmpty(platformRomsFolder))
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. Could not determine Platform Rom folder or it does not exist", 2);
                return;
            }

            // sync settings
            var platformSyncSettings = _settingsService.Settings.PlatformSyncSettings.FirstOrDefault(pss => pss.LaunchboxPlatformName == launchboxPlatformName);
            if (platformSyncSettings == null)
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. Could not find Sync settings for this platform. Have you set them up in [Tools > RommStar]?",2);
                return;
            }

            // RomM server
            Guid romServerId = platformSyncSettings.RommServerId;
            RommServer rommServer = (RommServer)_settingsService.Settings.RommServers.Where(rs => rs.Id == romServerId).FirstOrDefault();
            if (rommServer == null)
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. This platform's RomM server not in the RommStar Server list. " +
                    $"Platform may be linked with an old/deleted server.",2);
                return;
            }

            List<int> rommPlatformIds = platformSyncSettings.RommServerPlatforms.Select(p => p.RommId).ToList();
            if (rommPlatformIds.Count == 0)
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. No RomM platforms set against this Launchbox Platform in Sync Settings. Amend via [Tools > RomM > Platforms].", 2);
                return;
            }

            ExtendedSyncSettings resolvedExtSyncSettings = platformSyncSettings.ExtendedSyncSettings.ApplySettings ?
                                                            platformSyncSettings.ExtendedSyncSettings : _settingsService.Settings.GlobalExtendedSyncSettings;

            IPlatformFolder[] mediaFolders = platform.GetAllPlatformFolders();

            string platformDefaultEmulatorID = _launchboxDataService.GetPlatformDefaultEmulatorID(launchboxPlatformName);

            if (string.IsNullOrEmpty(platformDefaultEmulatorID))
            {
                _notificationService.SendErrorNotification($"WARNING: Whilst starting RomM Sync for {launchboxPlatformName} no default emulator was" +
                    $" found for this Platform. This will mean all imported games will have no emulator set. Consider setting this and re-syncing.",2 );
            }

            var apiQuery = await _rommService.GetRommPlatformsAsync(rommServer);
            if (!apiQuery.IsSuccess)
            {
                _notificationService.SendErrorNotification($"{errorPrefix}. Error communicating with rom server [{rommServer.ServerName}]. " +
                    $"Reason: [{apiQuery.FailureReason}]. Any Exception: [{apiQuery.ExceptionMessage}].  Http response: [{apiQuery.HttpResponse}] ", 2);
                return;
            }

            var lbPlatformRommPlatforms = ((List<PlatformDTO>)apiQuery.Data).Where(rp => rommPlatformIds.Contains(rp.RommId)).ToList();

            int? combinedRomCount = lbPlatformRommPlatforms.Sum(p => p.RomCount);
            
            // Do Sync

            // TODO: Need to check somewhere above that there isn't already a sync ongoing for the platform. Prevent overlapping syncs.

            await _syncManager?.QueuePlatformSync(launchboxPlatformName, platformRomsFolder, mediaFolders, platformDefaultEmulatorID, rommPlatformIds,
                resolvedExtSyncSettings, rommServer, (int)combinedRomCount, notifyLaunchboxOnMeatadataDone: true);

            StringBuilder sb = new StringBuilder($"Started RomM sync for [{launchboxPlatformName}]:\r\n" +
                $"{resolvedExtSyncSettings.SyncProfile.GetCustomName()}.");
            if (resolvedExtSyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia ||
                resolvedExtSyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom ||
                resolvedExtSyncSettings.SyncProfile == SyncProfileTypes.DownloadRom)
            {
                sb.Append($"\r\n\r\nRom files will download in the background, persisting across Launchbox sessions.");
            }
            _notificationService.SendInfoNotification(sb.ToString(), 3);
        }

        private async Task InstallGameOnDemandAsync(IGame game)
        {
            ILaunchBoxMainViewModel lbvm = PluginHelper.LaunchBoxMainViewModel;
            lbvm.SetProperty("TaskbarState", System.Windows.Shell.TaskbarItemProgressState.Indeterminate);

            // Declare this outside the try block so the catch block can see it for cleanup
            string targetZipPath = string.Empty;

            try
            {
                var rommIdField = game.GetAllCustomFields().FirstOrDefault(f => f.Name == "Romm_RomIds");
                if (rommIdField == null || string.IsNullOrWhiteSpace(rommIdField.Value))
                {
                    Debug.WriteLine($"[VIP Install] No romm_RomIds found for {game.Title}. Aborting.");
                    return;
                }

                List<int> rommIdsToDownload = rommIdField.Value
                    .Split(',')
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (rommIdsToDownload.Count == 0) return;

                var queue = _settingsService.Settings.RomDownloadQueue;
                var existingQueuedItem = queue.FirstOrDefault(q => q.LaunchboxId == game.Id);
                if (existingQueuedItem != null)
                {
                    queue.Remove(existingQueuedItem);
                    _settingsService.Save();
                }

                var serverId = _settingsService.Settings.PlatformSyncSettings.First(pss =>
                                    pss.LaunchboxPlatformName == game.Platform).RommServerId;

                var activeServer = _settingsService.Settings.RommServers.FirstOrDefault(s => s.Id == serverId);
                if (activeServer == null) return;

                var platExtSyncSetts = _settingsService.Settings.PlatformSyncSettings.FirstOrDefault(pss =>
                        pss.LaunchboxPlatformName == game.Platform)?.ExtendedSyncSettings;

                string rawPath = (platExtSyncSetts != null && platExtSyncSetts.ApplySettings) ?
                    platExtSyncSetts.TempDownloadsPath :
                    _settingsService.Settings.GlobalExtendedSyncSettings.TempDownloadsPath;

                if (!Path.IsPathRooted(rawPath))
                {
                    string pluginFolder = Path.GetDirectoryName(typeof(SettingsService).Assembly.Location);
                    rawPath = Path.Combine(pluginFolder, rawPath);
                }

                string tempDir = Path.GetFullPath(rawPath);
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                // POPULATE THE SCOPED VARIABLE HERE
                targetZipPath = Path.Combine(tempDir, $"onDemand_{game.Id}_{Guid.NewGuid()}.zip");

                var apiReturn = await _rommService.GetRomDetailsAsync(activeServer, rommIdsToDownload[0]);
                if (!apiReturn.IsSuccess) return;

                RomDTO firstRomDTO = apiReturn.Data;
                string platformStub = firstRomDTO.PlatformStub;

                // Pass the live cancellation token to the stream!
                string downloadApiReturn = await _rommService.DownloadRomsToDiskAsync(activeServer, rommIdsToDownload, targetZipPath, _onDemandCts.Token);

                bool success = string.IsNullOrEmpty(downloadApiReturn);

                if (success && File.Exists(targetZipPath))
                {
                    var vipBatchItem = new RomQueueItem
                    {
                        LaunchboxId = game.Id,
                        PlatformName = game.Platform,
                        PlatformStub = platformStub,
                        RommIds = rommIdsToDownload,
                        GameNameSanitised = StringsHelper.SanitizeFileName(game.Title),
                        MasterFilename = firstRomDTO.RommFilename
                    };

                    // Pass the live cancellation token to the extraction method!
                    await _launchboxDataService.UnzipRomsAndUpdateIGamesBatchAsync(targetZipPath, new List<RomQueueItem> { vipBatchItem }, _onDemandCts.Token, false);

                    try { File.Delete(targetZipPath); } catch { }
                }
                else
                {
                    lbvm.SetProperty("TaskbarState", System.Windows.Shell.TaskbarItemProgressState.Error);
                    _notificationService.SendErrorNotification($"Unknown Error installing {game.Title} ({game.Platform})");
                    game.Status = "Not Installed";
                }

                PluginHelper.DataManager.Save();
                _notificationService.SendInfoNotification($"{game.Title} ({game.Platform}) Installed", 1);
                lbvm.SetProperty("TaskbarState", System.Windows.Shell.TaskbarItemProgressState.None);
            }
            catch (OperationCanceledException)
            {
                // Caught when LaunchBox shuts down mid-download.
                Debug.WriteLine($"[OnDemand Install] Installation of {game.Title} aborted due to application shutdown.");

                game.Status = "Not Installed"; // Add this line to reset UI state
                game.Installed = false;

                // Clean up the partial zip file!
                if (!string.IsNullOrEmpty(targetZipPath) && File.Exists(targetZipPath))
                {
                    try { File.Delete(targetZipPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                lbvm.SetProperty("TaskbarState", System.Windows.Shell.TaskbarItemProgressState.Error);
                game.Status = "Not Installed";
                _notificationService.SendErrorNotification($"Error installing {game.Title} ({game.Platform}): {ex.Message}", 2);
            }
            finally
            {
                RestoreGameLaunchEmulatorExe();

                //await Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                //{
                //    // If user browsing the same platform as the download, refresh to update Install badges. Otherwise don't to reduce UI noise.
                //    // Note: AT LB startup, it defaults to display your last platform, but GetSelectedPlatform() returns null
                //    // therefor refresh on null or same platform. 
                //    IPlatform selectedPlatform = PluginHelper.StateManager.GetSelectedPlatform();
                //    if (selectedPlatform == null || selectedPlatform.Name == game.Platform)
                //    {
                //        await LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                //        _ = LaunchboxViewsHelper.SoftRefreshUi();
                //    }
                //}));


                //await LaunchboxViewsHelper.SoftRefreshUi();

            }
        }


        internal async Task OnGameSelectionChanged()
        {
            var selectedGames = PluginHelper.StateManager.GetAllSelectedGames();
            if (selectedGames != null && selectedGames.Count() > 0)
            {
                await LaunchboxViewsHelper.UpdatePlayButtonUi(selectedGames[0]);
            }
        }

        internal void RestoreGameLaunchEmulatorExe()
        {
            if (_lastGameLaunchEmulator != null && _lastEmulatorApplicationPath != Constants.DummyEmulatorExe)
            {
                _lastGameLaunchEmulator.ApplicationPath = _lastEmulatorApplicationPath;
                PluginHelper.DataManager.Save();
            }
        }

        internal async Task OnBeforeLaunch(IGame game, IEmulator emulator, IAdditionalApplication additionalApplication)
        {
            // At this stage, game may or may not be installed
            if (game == null && additionalApplication == null) return;

            // Check that game's emulator has not been set to the DummyEmulatorExe as a 
            // result of game Installation logic failing
            if (emulator != null)
            {
                if (emulator.ApplicationPath == Constants.DummyEmulatorExe)
                {
                    if (PluginHelper.StateManager.IsBigBox == false)
                    {
                        // Show in Launchbox                       

                        _notificationService.SendErrorNotification($"It appears that this game's emulator has been set to an operational file used by RommStar. " +
                            $"You will need to re-instate the correct Application Path for this emulator: {emulator.Title}", 2);

                        //TODO: ALSO LOG TO FILE OR ROMM LOG

                        //MessageBox.Show($"It appears that this game's emulator has been set to an operational file used by RommStar. " +
                        //    $"You will need to re-instate the correct Application Path for this emulator: {emulator.Title}",
                        //    "RommStar Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    _lastEmulatorApplicationPath = emulator.ApplicationPath; // order important here - beware emulator.ApplicationPath = Constants.DummyEmulatorExe;
                    _lastGameLaunchEmulator = emulator;
                }
            }

            // Additional apps can contain other exe's/alt versions etc
            var apps = game.GetAllAdditionalApplications();

            // Check if Rom Installation required
            // This covers both main roms and sibling roms/additional applications
            if (game?.Installed == false && game.Status != "Installing")
            {

                // Update any additional apps to also read updating
                foreach (var app in apps)
                {
                    app.Status = "Installing";
                    //app.ApplicationPath = Constants.DummyEmulatorExe;
                }

                game.Status = "Installing";

                // Change Launchbox "Play" button to "Installing" animation.
                LaunchboxViewsHelper.UpdatePlayButtonUi(game); // no await b/c fire and forget

                // Now set the emulator to an essentially empty exe to fake game launch
                // (No game launch cancel facility in LB sadly)
                if (emulator != null || apps.Count() > 0) emulator.ApplicationPath = Constants.DummyEmulatorExe;

                // Download ROM and install
                _ = Task.Run(() => InstallGameOnDemandAsync(game));
            }

            // This covers user launching AdditionalApp directly form UI whilst game is installing.
            if (additionalApplication != null && additionalApplication.Installed != true)
            {
                emulator.ApplicationPath = Constants.DummyEmulatorExe;
            }
        }
    }
}
