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

                    // 1. Sort queue: Priorities first, then oldest added
                    var sortedCandidates = queue
                        .OrderByDescending(q => q.IsPriority)
                        .ThenBy(q => q.AddedAt)
                        .ToList();

                    // 2. Build the batch based on target filesize
                    List<RomQueueItem> currentBatch = new();
                    long currentBatchSize = 0;

                    // TODO: this needs testing. 
                    // setup using platform specific extended settings if set. If not, use global defaults
                    var platExtSyncSetts = _settingsService.Settings.PlatformSyncSettings
                        .FirstOrDefault(pss => pss.LaunchboxPlatformName == currentBatch[0].PlatformName)?.ExtendedSyncSettings;

                    if (platExtSyncSetts == null) platExtSyncSetts = _settingsService.Settings.GlobalExtendedSyncSettings;

                    long targetSize = platExtSyncSetts.TargetRomBatchFilesizeBytes;

                    foreach (var item in sortedCandidates)
                    {
                        currentBatch.Add(item);
                        currentBatchSize += item.TotalSizeBytes;

                        // Stop adding if we hit the limit (but ensure at least 1 item is always processed)
                        if (currentBatchSize >= targetSize)
                            break;
                    }

                    // 3. Pre-flight Disk Space Check (Supports Relative, Absolute, and UNC Network Paths)
                    string rawPath = platExtSyncSetts.TempDownloadsPath;

                    // If it is a relative path (e.g. "TemporaryDownloads"), resolve it relative to the plugin folder
                    if (!Path.IsPathRooted(rawPath))
                    {
                        string pluginFolder = Path.GetDirectoryName(typeof(SettingsService).Assembly.Location);
                        rawPath = Path.Combine(pluginFolder, rawPath);
                    }

                    // Standardizes slashes and normalizes paths cleanly (e.g. standardizes UNC network pathing)
                    string tempDir = Path.GetFullPath(rawPath);

                    // Safely ensure directory or network hierarchy exists
                    if (!Directory.Exists(tempDir))
                        Directory.CreateDirectory(tempDir);

                    // Call our new Win32 space helper instead of DriveInfo
                    long availableFreeSpace = Helpers.FileSystemHelper.GetAvailableFreeSpace(tempDir);
                    long requiredSpace = (long)(currentBatchSize * 2.5);

                    if (availableFreeSpace < requiredSpace)
                    {
                        Debug.WriteLine($"[RomBatchService] Pausing queue: Insufficient space on target location. Need {requiredSpace / 1024 / 1024}MB, have {availableFreeSpace / 1024 / 1024}MB.");
                        await Task.Delay(30000); // Sleep for 30s before checking again
                        continue;
                    }

                    // 4. Flatten all RomM IDs for the API request
                    List<int> allRommIdsToDownload = currentBatch.SelectMany(b => b.RommIds).Distinct().ToList();
                    string zipFilename = $"batch_{Guid.NewGuid()}.zip";
                    string targetZipPath = Path.Combine(tempDir, zipFilename);

                    // Grab the active server context (assumes index 0 for now, or fetch by ID if implemented)
                    var activeServer = _settingsService.Settings.RommServers.FirstOrDefault();
                    if (activeServer == null) continue;

                    // 5. Download the Zip
                    bool success = await _rommService.DownloadRomsToDiskAsync(activeServer, allRommIdsToDownload, targetZipPath, CancellationToken.None);

                    if (success && File.Exists(targetZipPath))
                    {
                        // 6. Handoff to LaunchboxDataService for extraction and IGame updates
                        // TODO: this needs updating to present system
                        //await _launchboxDataService.ProcessDownloadedRomBatchAsync(targetZipPath, currentBatch);

                        // 7. Cleanup & remove from queue on success
                        foreach (var completedItem in currentBatch)
                        {
                            _settingsService.Settings.RomDownloadQueue.Remove(completedItem);
                        }
                        _settingsService.Save();

                        try { File.Delete(targetZipPath); } catch { /* Ignore cleanup errors */ }
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

