using RommStar.Core.Helpers;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Services
{
    public class RomBatchService
    {
        private readonly LaunchboxDataService _launchboxDataService;
        private readonly RommService _rommService;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        public RomBatchService(SettingsService settingsService, RommService rommService, LaunchboxDataService launchboxDataService,
            NotificationService notificationService)
        {
            _settingsService = settingsService;
            _rommService = rommService;
            _launchboxDataService = launchboxDataService;
            _notificationService = notificationService;
        }
        public void StartService()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Pass the token into the daemon loop
            _ = Task.Run(() => ProcessQueueLoopAsync(_cts.Token));
        }

        public void StopService()
        {
            if (!_isRunning) return;

            // This instantly trips the cancellation token
            _cts?.Cancel();
            _isRunning = false;
            Debug.WriteLine("[RomBatchService] Shutdown requested. Aborting active background tasks.");
        }

        private void RevertBatchInstallingStatus(List<RomQueueItem> batch)
        {
            if (batch == null || batch.Count == 0) return;

            foreach (var item in batch)
            {
                var game = Unbroken.LaunchBox.Plugins.PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                if (game != null && game.Status == "Installing")
                {
                    // todo: update this to lb api background update
                    game.Status = "Not Installed";
                    // Fire and forget the UI update to remove the spinner overlay
                    _ = RommStar.Core.Helpers.LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                }
            }
            Unbroken.LaunchBox.Plugins.PluginHelper.DataManager.Save();
        }

        private async Task ProcessQueueLoopAsync(CancellationToken token)
        {
            while (_isRunning && !token.IsCancellationRequested)
            {
                string targetZipPath = string.Empty;
                List<RomQueueItem> currentBatch = new();

                try
                {
                    var queue = _settingsService.Settings.RomDownloadQueue;

                    if (queue == null || queue.Count == 0)
                    {
                        await Task.Delay(5000, token); // Sleep for 5 seconds if queue is empty
                        continue;
                    }

                    // 1. Identify the most pressing item to determine our target platform and server for this run
                    var mostPressingItem = queue
                        .Where(q => q != null) // <-- Safely filter out mid-thread collision nulls on the fly
                        .OrderByDescending(q => q.IsPriority)
                        .ThenBy(q => q.AddedAt)
                        .FirstOrDefault();

                    if (mostPressingItem == null) continue;

                    string targetPlatform = mostPressingItem.PlatformName;
                    string targetServerId = mostPressingItem.ServerId;

                    // 2. Filter candidates ONLY for the exact target platform and server to prevent cross-contamination
                    var platformCandidates = queue
                        .Where(q => q != null && q.PlatformName == targetPlatform && q.ServerId == targetServerId && !q.IsQuarantined)
                        .OrderByDescending(q => q.IsPriority)
                        .ThenBy(q => q.AddedAt)
                        .ToList();

                    if (platformCandidates.Count == 0) continue; // Safety check in case the most pressing item was quarantined mid-loop

                    // 3. Build the batch based on target filesize using the correct property
                    currentBatch = new();
                    long currentBatchSize = 0;

                    var syncSettings = _settingsService.Settings.PlatformSyncSettings
                        .FirstOrDefault(pss => pss.LaunchboxPlatformName == targetPlatform)?.ExtendedSyncSettings;

                    if (syncSettings == null || syncSettings.ApplySettings == false) syncSettings = _settingsService.Settings.GlobalExtendedSyncSettings;

                    // Apply GB to Bytes conversion formula
                    long targetSizeBytes = syncSettings.TargetRomBatchFilesizeGb * 1024L * 1024L * 1024L;

                    // ISOLATION CHECK: If the next item has failed previously, set flag to force it to process entirely alone
                    bool isIsolationMode = platformCandidates.FirstOrDefault()?.RetryCount > 0;

                    // Todo: below needs to be a setting, likely platform scope
                    // Overwrite or skip existing local roms
                    if (!syncSettings.OverwriteExistingRoms)
                    {
                        // Selective Roms process -----------------------------------------------------------------
                        // If settings dictate, exclude roms that already exist in the LB roms location

                        // 1. Pre-calculate the platform's base ROM rules before the loop
                        bool individualGameFolders = (syncSettings != null && syncSettings.ApplySettings) ?
                            syncSettings.UseIndividualGameFolders :
                            _settingsService.Settings.GlobalExtendedSyncSettings.UseIndividualGameFolders;

                        IPlatform platform = PluginHelper.DataManager.GetPlatformByName(targetPlatform);
                        string romRoot = Helpers.FileSystemHelper.ResolvedRompath(platform.Folder, platform.Name);
                        bool queueModified = false;

                        // line up vars used in processes
                        bool useSha1InFilecheck = syncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1;
                        string candidatePath = null;


                        // 2. Build the batch, filtering out items that already exist on disk
                        foreach (var item in platformCandidates)
                        {
                            // Predict the final destination directory
                            string targetDirectory = individualGameFolders ?
                                Path.Combine(romRoot, item.GameNameSanitised) :
                                romRoot;

                            // 2.1 Selective Download routine ===================================================
                            bool skipDownload = true;
                            if (item.IsMultiFileGame)
                            {
                                // MULTI-FILE ROM FILTER =--------------------------------------------
                                // this also include multi-file games that have siblings sets (eg. FFVIII, two diff region sets. These get split into different igames
                                // so don't need to process in sibling set below. By design due to LB's auto cue sheet gen requiring it to work properly.
                                foreach (var file in item.MultiFiles)
                                {
                                    // this is essentially a double check. Metadata sync checks if file already exists, but user
                                    // may be returning in another session and file may have been deleted. 
                                    if (file.Category == "game")
                                    {
                                        candidatePath = Path.Combine(targetDirectory, item.MasterFilename, file.FileName);

                                        if (!FileSystemHelper.LocalFilePresent(syncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1,
                                            candidatePath, file.Sha1Hash))
                                        {
                                            skipDownload = false;
                                            IGame game = PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                                            if (game != null)
                                            {
                                                game.Installed = false;
                                                game.Status = "Not Installed";
                                            }
                                            _ = LaunchboxViewsHelper.SoftRefreshUi();
                                            break;
                                        }
                                    }
                                    else if (file.Category == "soundtrack")
                                    {
                                        candidatePath = Path.Combine(Constants.LaunchboxRootDir, "Music", platform.Name, item.MasterFilename, file.FileName);

                                    }

                                    if (!FileSystemHelper.LocalFilePresent(useSha1InFilecheck, candidatePath, file.Sha1Hash))
                                    {
                                        skipDownload = false;
                                        _ = LaunchboxViewsHelper.SoftRefreshUi();
                                        break;
                                    }
                                }
                            }


                            else if (item.RommIds.Count > 1)
                            {
                                // SIBLING-FILE ROM FILTER ============================================

                                // This is a single instance sibling set (e.g. Buck Rogers - different versions)
                                foreach (var file in item.MultiFiles)
                                {
                                    if (file.Category == "game")
                                    {
                                        candidatePath = Path.Combine(targetDirectory, file.FileName);
                                        if (!File.Exists(candidatePath))
                                        {
                                            skipDownload = false;
                                            break;
                                        }                    
                                    }
                                    else if (file.Category == "soundtrack")
                                    {
                                        candidatePath = Path.Combine(Constants.LaunchboxRootDir, "Music", platform.Name, item.MasterFilename, file.FileName);
                                        if (!File.Exists(candidatePath))
                                        {
                                            skipDownload = false;
                                            break;
                                        }
                                        // took this out - no idea why i put it in - unless missing something
                                        //else
                                        //{
                                        //    IGame game = PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                                        //    if (game == null) skipDownload = false;
                                        //    break;
                                        //}
                                    }

                                }
                            }
                            else
                            {
                                // Single file
                                //if (!FileSystemHelper.LocalFilePresent(syncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1,
                                //            candidatePath, file.Sha1Hash))


                                    if (!File.Exists(Path.Combine(targetDirectory, item.MasterFilename))) skipDownload = false;
                                // todo: soundtrack stuff - not sure need to as any soundtrack converts asingle file to a multi-file rom?
                            }


                            //= item.IsMultiFileGame? Path.Combine(targetDirectory, item.MasterFilename, "dave") : Path.Combine(targetDirectory, item.MasterFilename);

                            // 3. THE CHECK: Does this file already exist in LaunchBox?
                            if (skipDownload)
                            {
                                //Debug.WriteLine($"[RomBatchService] Skipping {item.GameNameSanitised}, file already exists at {primaryFilepath}.");

                                // Remove it from the live queue so we don't process it again
                                _settingsService.Settings.RomDownloadQueue.RemoveAll(q => q.LaunchboxId == item.LaunchboxId);
                                queueModified = true;

                                // Optionally, update the LaunchBox UI to reflect it's actually installed
                                var existingGame = PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                                if (existingGame != null && existingGame.Installed == false)
                                {
                                    existingGame.Installed = true;
                                    existingGame.Status = "Installed";
                                    //existingGame.ApplicationPath = primaryFilepath; // control for multi-file
                                    PluginHelper.DataManager.Save();
                                }
                                else
                                {

                                }

                                continue; // Skip adding to currentBatch and move to the next candidate
                            }
                            else
                            {
                                var existingGame = PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                                if (existingGame != null && existingGame.Installed == true)
                                {
                                    existingGame.Installed = false;
                                    existingGame.Status = "Installing";
                                    // do above via: PluginHelper.DataManager.BackgroundReloadSave(new Action(() => { game.Status = "Installing"; }));

                                    //existingGame.ApplicationPath = primaryFilepath; // control for multi-file
                                    PluginHelper.DataManager.Save();
                                }
                            }

                            // ISOLATION BREAK: If we are in isolation mode (triggered following abortive download), stop after adding exactly 1 item!
                            // focuses retries on problem rom alone.
                            if (isIsolationMode && currentBatch.Count > 0)
                                break;

                            // If the batch already has items, and adding this one pushes us over the limit, stop here.
                            if (currentBatch.Count > 0 && (currentBatchSize + item.TotalSizeBytes) > targetSizeBytes)
                                break;

                            currentBatch.Add(item);
                            currentBatchSize += item.TotalSizeBytes;
                        }

                        // Ensure we save the queue if we pruned any already-installed games
                        if (queueModified)
                        {
                            _settingsService.Save();
                        }

                        // If every single candidate was already installed on disk, 
                        // currentBatch is empty. Skip the rest of the network/download logic!
                        if (currentBatch.Count == 0)
                        {
                            // We must check platformCandidates because currentBatch is completely empty!
                            bool requestedNotification = platformCandidates.FirstOrDefault()?.NotifyLaunchboxOnCompletion == true;

                            if (requestedNotification)
                            {
                                // 2. Check if the live queue has anything else pending for this platform
                                bool platformHasMoreItems = _settingsService.Settings.RomDownloadQueue
                                    .Any(q => q.PlatformName == targetPlatform && q.ServerId == targetServerId);

                                // 3. If the queue is clear, raise the custom skipped message
                                if (!platformHasMoreItems)
                                {
                                    _notificationService.SendInfoNotification($"Romm Files Sync complete for [{targetPlatform}]. All required files were already present on disk.", 1);
                                }
                            }

                            continue;
                        }

                        // END Selective Roms Process -------------------------------------------------------------------------
                    }

                    else

                    {
                        // OVERWRITE ROMS
                        foreach (var item in platformCandidates)
                        {
                            // ISOLATION BREAK: If we are in isolation mode, stop after adding exactly 1 item!
                            if (isIsolationMode && currentBatch.Count > 0)
                                break;

                            // If the batch already has items, and adding this one pushes us over the limit, stop here.
                            if (currentBatch.Count > 0 && (currentBatchSize + item.TotalSizeBytes) > targetSizeBytes)
                                break;

                            currentBatch.Add(item);
                            currentBatchSize += item.TotalSizeBytes;
                        }
                    }


                    // 4. Pre-flight Disk Space Check (Supports Relative, Absolute, and UNC Network Paths)
                    string rawPath = syncSettings.TempDownloadsPath;

                    if (!Path.IsPathRooted(rawPath))
                    {
                        string pluginFolder = Path.GetDirectoryName(typeof(SettingsService).Assembly.Location);
                        rawPath = Path.Combine(pluginFolder, rawPath);
                    }

                    string tempDir = Path.GetFullPath(rawPath);

                    if (!Directory.Exists(tempDir))
                        Directory.CreateDirectory(tempDir);

                    long availableFreeSpace = Helpers.FileSystemHelper.GetAvailableFreeSpace(tempDir);
                    long requiredSpace = (long)(currentBatchSize * 2.5);

                    if (availableFreeSpace < requiredSpace)
                    {
                        Debug.WriteLine($"[RomBatchService] Pausing queue: Insufficient space on target location. Need {requiredSpace / 1024 / 1024}MB, have {availableFreeSpace / 1024 / 1024}MB.");
                        await Task.Delay(30000, token); // Sleep for 30s before checking again
                        continue;
                    }

                    // 5. Flatten all RomM IDs and resolve the specific Server Context
                    List<int> allRommIdsToDownload = currentBatch.SelectMany(b => b.RommIds).Distinct().ToList();
                    string zipFilename = $"batch_{Guid.NewGuid()}.zip";
                    targetZipPath = Path.Combine(tempDir, zipFilename);

                    // Fetch the exact server originally used to queue these items
                    var activeServer = _settingsService.Settings.RommServers.FirstOrDefault(s => s.Id.ToString() == targetServerId);

                    if (activeServer == null)
                    {
                        Debug.WriteLine($"[RomBatchService] Error: Dead server context ({targetServerId}). Removing invalid batch from queue.");
                        foreach (var badItem in currentBatch)
                        {
                            _settingsService.Settings.RomDownloadQueue.RemoveAll(q => q.LaunchboxId == badItem.LaunchboxId);
                        }
                        _settingsService.Save();
                        continue;
                    }

                    // 5.5 Lock UI for batch items to prevent manual install conflicts

                    foreach (var item in currentBatch)
                    {
                        var game = PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                        if (game != null && game.Status != "Installing")
                        {
                            //game.Status = "Installing";
                            //PluginHelper.DataManager.BackgroundReloadSave(new Action(() => { game.Status = "Installing"; game.Installed = false; }));
                            game.Status = "Installing";
                            game.Installed = false;

                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                //_ = RommStar.Core.Helpers.LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                            }));
                        }
                    }
                    PluginHelper.DataManager.Save();

                    await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // If user browsing the same platform as the download, refresh to update Install badges. Otherwise don't to reduce UI noise.
                        // Note: AT LB startup, it defaults to display your last platform, but GetSelectedPlatform() returns null
                        // therefor refresh on null or same platform. 
                        IPlatform selectedPlatform = PluginHelper.StateManager.GetSelectedPlatform();
                        if (selectedPlatform == null || selectedPlatform.Name == targetPlatform)
                        {
                            _ = LaunchboxViewsHelper.SoftRefreshUi();
                        }
                    }));

                    // 6. Download the Zip
                    string downloadError = await _rommService.DownloadRomsToDiskAsync(activeServer, allRommIdsToDownload, targetZipPath, token);
                    bool success = string.IsNullOrEmpty(downloadError);

                    if (success && File.Exists(targetZipPath))
                    {
                        // 7. Handoff to LaunchboxDataService for extraction and IGame updates
                        await _launchboxDataService.UnzipRomsAndUpdateIGamesBatchAsync(targetZipPath, currentBatch, token);
                        // 8. Cleanup & remove from queue on success
                        foreach (var completedItem in currentBatch)
                        {
                            _settingsService.Settings.RomDownloadQueue.RemoveAll(q => q.LaunchboxId == completedItem.LaunchboxId);
                        }
                        _settingsService.Save();

                        // 9. RAISE THE NOTIFICATION (Only when the entire platform is finished)
                        if (currentBatch.FirstOrDefault()?.NotifyLaunchboxOnCompletion == true)
                        {
                            // Check the live queue to see if this platform still has pending downloads
                            bool platformHasMoreItems = _settingsService.Settings.RomDownloadQueue
                                .Any(q => q.PlatformName == targetPlatform && q.ServerId == targetServerId);

                            // If nothing is left for this platform, the whole system is done!
                            if (!platformHasMoreItems)
                            {
                                _notificationService.SendInfoNotification($"Romm Files Sync complete for [{targetPlatform}].", 1);
                            }
                        }

                        try { File.Delete(targetZipPath); }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[RomBatchService] ERROR whilst trying to delete temporary zip file: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[RomBatchService] Batch download failed. Error: {downloadError}");

                        // todo: make this a setting?? Necessary?
                        int maxRetries = 3; // Hardcoded max retries

                        foreach (var badItem in currentBatch)
                        {
                            badItem.RetryCount++;

                            // If it hit the max limit, quarantine it!
                            if (badItem.RetryCount >= maxRetries)
                            {
                                badItem.IsQuarantined = true;
                                badItem.LastError = downloadError;

                                // If we are in isolation mode, we know the exact game name that failed
                                string gameName = currentBatch.Count == 1 ? badItem.GameNameSanitised : "A batch of games";
                                _notificationService.SendErrorNotification($"Quarantined '{gameName}' ({badItem.PlatformName}) after {maxRetries} failed downloads. Check settings to retry.", 3);

                                // Revert the UI state permanently for the quarantined item
                                var game = Unbroken.LaunchBox.Plugins.PluginHelper.DataManager.GetGameById(badItem.LaunchboxId);
                                if (game != null)
                                {
                                    game.Status = "Not Installed";
                                    _ = RommStar.Core.Helpers.LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                                }
                            }
                        }

                        _settingsService.Save(); // Save the incremented counters and quarantine states
                        PluginHelper.DataManager.Save(); // Save any UI reversions

                        // Only revert the UI for items still in the active queue (not quarantined)
                        RevertBatchInstallingStatus(currentBatch.Where(b => !b.IsQuarantined).ToList());

                        await Task.Delay(2000, token); // Brief pause before continuing
                    }
                }
                catch (OperationCanceledException)
                {
                    // This catches the exact moment the user closes LaunchBox mid-download or mid-unzip.
                    Debug.WriteLine("[RomBatchService] Daemon aborted via application shutdown.");

                    RevertBatchInstallingStatus(currentBatch); // Unlock the UI before shutting down

                    // Nuke the partial zip so it doesn't leave corrupted junk, but leave the queue intact!
                    try { if (File.Exists(targetZipPath)) File.Delete(targetZipPath); }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RomBatchService] ERROR whilst trying to delete temporary zip file: {ex.Message}");
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RomBatchService] Critical error in queue loop: {ex.Message}");
                    RevertBatchInstallingStatus(currentBatch); // Unlock the UI before shutting down

                    try { if (File.Exists(targetZipPath)) File.Delete(targetZipPath); }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"[RomBatchService] ERROR whilst trying to delete temporary zip file: {e.Message}");
                    }

                    await Task.Delay(10000, token); // Pass token to delay so it can wake up instantly on close
                }
            }
        }
    }
}

