using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Services
{
    public class RomBatchService
    {
        private readonly SettingsService _settingsService;
        private readonly RommService _rommService;
        private readonly LaunchboxDataService _launchboxDataService;
        private bool _isRunning = false;

        public RomBatchService(SettingsService settingsService, RommService rommService, LaunchboxDataService launchboxDataService)
        {
            _settingsService = settingsService;
            _rommService = rommService;
            _launchboxDataService = launchboxDataService;
        }

        public void StartService()
        {
            if (_isRunning) return;
            _isRunning = true;

            // Fire and forget the background daemon loop
            _ = Task.Run(ProcessQueueLoopAsync);
        }

        private async Task ProcessQueueLoopAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var queue = _settingsService.Settings.RomDownloadQueue;

                    if (queue == null || queue.Count == 0)
                    {
                        await Task.Delay(5000); // Sleep for 5 seconds if queue is empty
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
                    List<RomQueueItem> currentBatch = new();
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
                        await Task.Delay(30000); // Sleep for 30s before checking again
                        continue;
                    }

                    // 5. Flatten all RomM IDs and resolve the specific Server Context
                    List<int> allRommIdsToDownload = currentBatch.SelectMany(b => b.RommIds).Distinct().ToList();
                    string zipFilename = $"batch_{Guid.NewGuid()}.zip";
                    string targetZipPath = Path.Combine(tempDir, zipFilename);

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

                    // 6. Download the Zip
                    bool success = await _rommService.DownloadRomsToDiskAsync(activeServer, allRommIdsToDownload, targetZipPath, CancellationToken.None);

                    if (success && File.Exists(targetZipPath))
                    {
                        // 7. Handoff to LaunchboxDataService for extraction and IGame updates
                        // Note: The signature for ProcessDownloadedRomBatchAsync will be fixed in Phase 3.
                        // await _launchboxDataService.ProcessDownloadedRomBatchAsync(targetZipPath, currentBatch);

                        // 8. Cleanup & remove from queue on success
                        foreach (var completedItem in currentBatch)
                        {
                            _settingsService.Settings.RomDownloadQueue.Remove(completedItem);
                        }
                        _settingsService.Save();

                       // try { File.Delete(targetZipPath); } catch { /* Ignore cleanup errors */ }
                    }
                    else
                    {
                        Debug.WriteLine("[RomBatchService] Batch download failed. Retrying in 10 seconds...");
                        await Task.Delay(10000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RomBatchService] Critical error in queue loop: {ex.Message}");
                    await Task.Delay(10000); // Prevent tight failure loops
                }
            }
        }
    }
}

