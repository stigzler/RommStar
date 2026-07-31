using Microsoft.Xaml.Behaviors.Input;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Helpers;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

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

        public LaunchboxStateService(
                    LaunchboxDataService launchboxDataService,
                    SettingsService settingsService,
                    RommService rommService)
        {
            _launchboxDataService = launchboxDataService;
            _settingsService = settingsService;
            _rommService = rommService;
        }

        string _lastEmulatorApplicationPath;
        IEmulator _lastGameLaunchEmulator;

        internal void DoShutdownOperations()
        {
            // Ensure that any manipulation of the last launch Emulator's application path
            // as part of the Game Install strategy is restored 
            RestoreGameLaunchEmulatorExe();
        }


        private async Task InstallGameOnDemandAsync(IGame game)
        {
            try
            {
                // 1. Extract the RomM IDs (csv) from the custom field Romm_RomIds
                // Note: For games with sibling roms (NOT multi-disk), the one set to Default in Romm is set first in the RomIds list. This used later
                var rommIdField = game.GetAllCustomFields().FirstOrDefault(f => f.Name == "Romm_RomIds");
                if (rommIdField == null || string.IsNullOrWhiteSpace(rommIdField.Value))
                {
                    Debug.WriteLine($"[VIP Install] No romm_RomIds found for {game.Title}. Aborting.");
                    // TODO: have to return something that indicates stop Installing Status
                    return;
                }

                List<int> rommIdsToDownload = rommIdField.Value
                    .Split(',')
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (rommIdsToDownload.Count == 0) return; // TODO: Again - indicated installing status needs changing

                // 2. Intercept and remove from the Background Queue (if it's already in the queue but waiting)
                var queue = _settingsService.Settings.RomDownloadQueue;
                var existingQueuedItem = queue.FirstOrDefault(q => q.LaunchboxId == game.Id);
                if (existingQueuedItem != null)
                {
                    queue.Remove(existingQueuedItem);
                    _settingsService.Save();
                }

                // 3. Prepare the Download Path (Relative, Absolute, or Network UNC)
                var serverId = _settingsService.Settings.PlatformSyncSettings.First(pss =>
                                    pss.LaunchboxPlatformName == game.Platform).RommServerId;

                var activeServer = _settingsService.Settings.RommServers.FirstOrDefault(s => s.Id == serverId);
                if (activeServer == null) return; // TODO: Again - indicated installing status needs changing

                // figure if platform using global or specific settings:
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

                // Setup Temp Dir for download
                string tempDir = Path.GetFullPath(rawPath);

                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                string zipFilename = $"vip_{game.Id}_{Guid.NewGuid()}.zip";
                string targetZipPath = Path.Combine(tempDir, zipFilename);

                // 3.5 Get the romm platform stub - use first rom as all same platform
                var apiReturn = await _rommService.GetRomDetailsAsync(activeServer, rommIdsToDownload[0]);
                if (!apiReturn.IsSuccess) return; // TODO: Need to feedback to user/log somehow.

                RomDTO firstRomDTO = apiReturn.Data;
                string platformStub = firstRomDTO.PlatformStub;      

                // 4. Download immediately to disk
                bool success = await _rommService.DownloadRomsToDiskAsync(activeServer, rommIdsToDownload, targetZipPath, CancellationToken.None);

                if (success && File.Exists(targetZipPath))
                {
                    // 5. Build a temporary "Job Item" to hand off to your existing extraction method
                    var vipBatchItem = new RomQueueItem
                    {
                        LaunchboxId = game.Id,
                        PlatformName = game.Platform,
                        PlatformStub = platformStub,
                        RommIds = rommIdsToDownload,
                        GameNameSanitised = StringsHelper.SanitizeFileName(game.Title),
                        MasterFilename = firstRomDTO.RommFilename
                    };

                    await _launchboxDataService.ProcessDownloadedRomBatchAsync(targetZipPath, new List<RomQueueItem> { vipBatchItem });

                    // 6. Cleanup the zip file
                    try { File.Delete(targetZipPath); } catch { /* Ignore locked file errors */ }
                }
                else
                {
                    Debug.WriteLine($"[VIP Install] Download failed for {game.Title}.");
                    game.Status = "Not Installed";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VIP Install] Error during manual install: {ex.Message}");
                game.Status = "Not Installed";
            }
            finally
            {
                // Ensure LaunchBox is taken out of its "fake launch" state
                RestoreGameLaunchEmulatorExe();

                // Change PLay button from "Installing" to "Play"
                await LaunchboxViewsHelper.UpdatePlayButtonUi(game);

                // Force LaunchBox to save state and refresh
                PluginHelper.DataManager.Save();
                if (PluginHelper.LaunchBoxMainViewModel != null)
                {
                    PluginHelper.LaunchBoxMainViewModel.RefreshData();
                }
            }
        }

        internal async Task DownloadRoms()
        {

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
                        MessageBox.Show($"It appears that this game's emulator has been set to an operational file used by RommStar. " +
                            $"You will need to re-instate the correct Application Path for this emulator: {emulator.Title}",
                            "RommStar Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
      
                // Now set the emulator to an essentially empty exe to fake game launch
                // (No game launch cancel facility in LB sadly)
                if (emulator != null || apps.Count() > 0) emulator.ApplicationPath = Constants.DummyEmulatorExe;

                // Change Launchbox "Play" button to "Installing" animation.
                LaunchboxViewsHelper.UpdatePlayButtonUi(game); // no await b/c fire and forget

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
