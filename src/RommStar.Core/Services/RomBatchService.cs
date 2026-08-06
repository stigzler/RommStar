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
            NotificationService notificationService            )
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
                       .OrderByDescending(q => q.IsPriority)
                       .ThenBy(q => q.AddedAt)
                       .FirstOrDefault();

                    if (mostPressingItem == null) continue;

                    string targetPlatform = mostPressingItem.PlatformName;
                    string targetServerId = mostPressingItem.ServerId;

                    // 2. Filter candidates ONLY for the exact target platform and server to prevent cross-contamination
                    var platformCandidates = queue
                        .Where(q => q.PlatformName == targetPlatform && q.ServerId == targetServerId)
                        .OrderByDescending(q => q.IsPriority)
                        .ThenBy(q => q.AddedAt)
                        .ToList();

                    // 3. Build the batch based on target filesize using the correct property
                    currentBatch = new();
                    long currentBatchSize = 0;

                    var platExtSyncSetts = _settingsService.Settings.PlatformSyncSettings
                        .FirstOrDefault(pss => pss.LaunchboxPlatformName == targetPlatform)?.ExtendedSyncSettings;

                    if (platExtSyncSetts == null) platExtSyncSetts = _settingsService.Settings.GlobalExtendedSyncSettings;

                    // Apply GB to Bytes conversion formula
                    long targetSizeBytes = platExtSyncSetts.TargetRomBatchFilesizeGb * 1024L * 1024L * 1024L;

                    foreach (var item in platformCandidates)
                    {
                        // If the batch already has items, and adding this one pushes us over the limit, stop here.
                        if (currentBatch.Count > 0 && (currentBatchSize + item.TotalSizeBytes) > targetSizeBytes)
                            break;

                        currentBatch.Add(item);
                        currentBatchSize += item.TotalSizeBytes;
                    }

                    // 4. Pre-flight Disk Space Check (Supports Relative, Absolute, and UNC Network Paths)
                    string rawPath = platExtSyncSetts.TempDownloadsPath;

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
                            _settingsService.Settings.RomDownloadQueue.Remove(badItem);
                        }
                        _settingsService.Save();
                        continue;
                    }

                    // 5.5 Lock UI for batch items to prevent manual install conflicts

                    foreach (var item in currentBatch)
                    {
                        var game = Unbroken.LaunchBox.Plugins.PluginHelper.DataManager.GetGameById(item.LaunchboxId);
                        if (game != null && game.Status != "Installing")
                        {
                            game.Status = "Installing";
                           //_ = RommStar.Core.Helpers.LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                        }
                    }

                    Unbroken.LaunchBox.Plugins.PluginHelper.DataManager.Save();
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
                    bool success = await _rommService.DownloadRomsToDiskAsync(activeServer, allRommIdsToDownload, targetZipPath, token);
                    if (success && File.Exists(targetZipPath))
                    {
                        // 7. Handoff to LaunchboxDataService for extraction and IGame updates
                        await _launchboxDataService.UnzipRomsAndUpdateIGamesBatchAsync(targetZipPath, currentBatch, token);
                        // 8. Cleanup & remove from queue on success
                        foreach (var completedItem in currentBatch)
                        {
                            _settingsService.Settings.RomDownloadQueue.Remove(completedItem);
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
                        Debug.WriteLine("[RomBatchService] Batch download failed. Retrying in 10 seconds...");
                        RevertBatchInstallingStatus(currentBatch); // Unlock the UI
                        await Task.Delay(10000, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // This catches the exact moment the user closes LaunchBox mid-download or mid-unzip.
                    Debug.WriteLine("[RomBatchService] Daemon aborted via application shutdown.");

                    RevertBatchInstallingStatus(currentBatch); // Unlock the UI before shutting down

                    // Nuke the partial zip so it doesn't leave corrupted junk, but leave the queue intact!
                    try { if (File.Exists(targetZipPath)) File.Delete(targetZipPath); } 
                    catch (Exception ex) {
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

