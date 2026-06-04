using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RommStar.Core.Dtos;
using RommStar.Core.Models;
using RommStar.Core.Sync;

namespace RommStar.Core.Services
{
    /// <summary>
    /// Main Architecture Core Engine managing data coordination.
    /// </summary>
    public class RommService
    {
        private readonly HttpClient _client;

        // Channel pipelines handling FIFO operations natively across background threads
        private readonly Channel<PlatformSyncTask> _platformQueue = Channel.CreateUnbounded<PlatformSyncTask>();
        private readonly Channel<DownloadJob> _fileDownloadQueue = Channel.CreateUnbounded<DownloadJob>();

        // Tracks remaining active file counts per platform to enforce strict sequential progression
        private readonly ConcurrentDictionary<string, int> _activeFileCounters = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<PlatformSyncJob> ActiveSyncJobs { get; } = new();
        public RommServerConfig ActiveServer { get; set; }

        // Media Profiles configuration sets
        public MediaSelectionProfile CatalogProfile { get; set; } = new() { BoxFront = true, Screenshots = true };
        public MediaSelectionProfile InstallProfile { get; set; } = new() { BoxFront = true, Box3D = true, Videos = true, Manuals = true, Music = true };

        public event Action<PlatformSyncJob>? OnSyncCompletedNotification;

        public RommService(RommServerConfig initialServer)
        {
            // Thread-safe client initialization without global default authorization headers
            _client = new HttpClient();
            ActiveServer = initialServer;

            // Kick off long-running infrastructure daemons
            _ = Task.Run(StartPlatformQueueProcessorAsync);
            _ = Task.Run(StartFileQueueProcessorAsync);
        }

        // =========================================================================
        // MACRO MANAGEMENT: ENQUEUE PLATFORM RUN
        // =========================================================================
        public void QueuePlatformSync(string lbPlatformName, List<int> rommPlatformIds, bool downloadRoms)
        {
            var uiCard = new PlatformSyncJob
            {
                LaunchBoxPlatformName = lbPlatformName,
                ServerName = ActiveServer.ServerName,
                Status = SyncStatus.Queued
            };

            // Safely push to UI Collection from background hooks if necessary
            ActiveSyncJobs.Add(uiCard);

            var task = new PlatformSyncTask
            {
                LaunchBoxPlatformName = lbPlatformName,
                RommPlatformIds = rommPlatformIds,
                DownloadRomFiles = downloadRoms,
                UiCard = uiCard
            };

            _platformQueue.Writer.TryWrite(task);
        }

        public void CancelPlatformSync(string lbPlatformName)
        {
            // Locate the card/task and trip its cancellation token source
            var card = ActiveSyncJobs.FirstOrDefault(j => j.LaunchBoxPlatformName.Equals(lbPlatformName, StringComparison.OrdinalIgnoreCase));
            if (card != null && (card.Status == SyncStatus.Queued || card.Status == SyncStatus.SyncingFiles || card.Status == SyncStatus.ProcessingMetadata))
            {
                card.Status = SyncStatus.Cancelled;
                // Hook to trip associated PlatformSyncTask.Cts.Cancel() can be mapped here via tracking collection
            }
        }

        // =========================================================================
        // MACRO SEQUENTIAL PIPELINE PROCESSOR (1 Platform at a time)
        // =========================================================================
        private async Task StartPlatformQueueProcessorAsync()
        {
            while (await _platformQueue.Reader.WaitToReadAsync())
            {
                while (_platformQueue.Reader.TryRead(out var platformTask))
                {
                    // If user cancelled while sitting in the queue lane, skip instantly
                    if (platformTask.UiCard.Status == SyncStatus.Cancelled) continue;

                    platformTask.UiCard.Status = SyncStatus.ProcessingMetadata;
                    var currentSnapshot = ActiveServer; // Snap authorization state cleanly

                    // STEP 1: Metadata Request execution
                    var roms = await FetchMetadataFromRommAsync(platformTask.RommPlatformIds, currentSnapshot);
                    if (roms == null || roms.Count == 0)
                    {
                        platformTask.UiCard.Status = SyncStatus.CompletedWithErrors;
                        continue;
                    }

                    if (platformTask.Cts.Token.IsCancellationRequested) { platformTask.UiCard.Status = SyncStatus.Cancelled; continue; }

                    platformTask.UiCard.Status = SyncStatus.SyncingFiles;
                    var chosenProfile = platformTask.DownloadRomFiles ? InstallProfile : CatalogProfile;

                    // STEP 2: Process local LaunchBox Database mapping & calculate files payload
                    foreach (var rom in roms)
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;

                        // Zero code-behind execution: Inject record directly into Local Database layer
                        var lbGameMock = SyncWithLaunchBoxDatabase(rom, platformTask.LaunchBoxPlatformName);

                        // Schedule ROM file extraction if explicitly configured
                        if (platformTask.DownloadRomFiles)
                        {
                            EnqueueFileDownload(new DownloadJob
                            {
                                JobType = DownloadJobType.Rom,
                                RelativeUrl = rom.RomUrl,
                                DestinationPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.LaunchBoxPlatformName, rom.FileName),
                                LaunchBoxPlatformName = platformTask.LaunchBoxPlatformName,
                                ServerContext = currentSnapshot,
                                UiCard = platformTask.UiCard,
                                OnSuccessCallback = () => { /* Flip IGame.Installed = true; SaveChanges(); */ }
                            });
                        }

                        // Schedule individual media files by cross-checking profile toggles
                        ScheduleMediaDownloads(rom, platformTask, chosenProfile, currentSnapshot);
                    }

                    // STEP 3: Hold execution. Block loop until background file queue counters drain completely to 0.
                    while (_activeFileCounters.TryGetValue(platformTask.LaunchBoxPlatformName, out int fileCount) && fileCount > 0)
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;
                        await Task.Delay(250);
                    }

                    // Conclude Platform Lifecycle State
                    if (platformTask.Cts.Token.IsCancellationRequested)
                    {
                        platformTask.UiCard.Status = SyncStatus.Cancelled;
                    }
                    else
                    {
                        platformTask.UiCard.Status = platformTask.UiCard.ErrorCount > 0 ? SyncStatus.CompletedWithErrors : SyncStatus.Completed;
                        OnSyncCompletedNotification?.Invoke(platformTask.UiCard);
                    }

                    // Flush active counter memory tracking
                    _activeFileCounters.TryRemove(platformTask.LaunchBoxPlatformName, out _);
                }
            }
        }

        // =========================================================================
        // PARALLEL ON-DEMAND BYPASS (Bypasses macro structural sync channel entirely)
        // =========================================================================
        public async Task ExecuteOnDemandInstallAsync(string lbPlatform, RomDto rom)
        {
            var currentSnapshot = ActiveServer;
            string destinationRomPath = Path.Combine("C:\\LaunchBox\\Games", lbPlatform, rom.FileName);

            // 1. Instantly pull down the critical ROM execution payload on a dedicated task lane
            bool romSuccess = await StreamFileFromNetworkAsync(rom.RomUrl, destinationRomPath, currentSnapshot);
            if (!romSuccess) return;

            // Target Game update invocation block
            // targetIGameInstance.Installed = true;

            // 2. Scan and stream down heavy Install Profile assets concurrently on the fly
            var mediaTasks = new List<Task>();
            if (InstallProfile.Videos && !string.IsNullOrEmpty(rom.VideoUrl))
            {
                string videoPath = Path.Combine("C:\\LaunchBox\\Videos", lbPlatform, $"{rom.Name}.mp4");
                mediaTasks.Add(StreamFileFromNetworkAsync(rom.VideoUrl, videoPath, currentSnapshot));
            }
            // Append other contextual profiles smoothly...

            await Task.WhenAll(mediaTasks);
        }

        // =========================================================================
        // MICRO-LEVEL FILE PIPELINE HANDLERS
        // =========================================================================
        private void EnqueueFileDownload(DownloadJob job)
        {
            _activeFileCounters.AddOrUpdate(job.LaunchBoxPlatformName, 1, (key, current) => current + 1);
            if (job.UiCard != null) job.UiCard.TotalItems++;

            _fileDownloadQueue.Writer.TryWrite(job);
        }

        private async Task StartFileQueueProcessorAsync()
        {
            while (await _fileDownloadQueue.Reader.WaitToReadAsync())
            {
                while (_fileDownloadQueue.Reader.TryRead(out var job))
                {
                    bool success = await StreamFileFromNetworkAsync(job.RelativeUrl, job.DestinationPath, job.ServerContext);

                    if (!success && job.UiCard != null) job.UiCard.ErrorCount++;
                    else if (success) job.OnSuccessCallback?.Invoke();

                    if (job.UiCard != null) job.UiCard.ProcessedItems++;
                    _activeFileCounters.AddOrUpdate(job.LaunchBoxPlatformName, 0, (key, current) => current - 1);
                }
            }
        }

        private async Task<bool> StreamFileFromNetworkAsync(string relativeUrl, string targetPath, RommServerConfig server)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return true; // Gracefully pass missing remote paths
            try
            {
                string completeUrl = $"{server.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
                using var request = new HttpRequestMessage(HttpMethod.Get, completeUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.ApiToken);

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using var sourceStream = await response.Content.ReadAsStreamAsync();
                using var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await sourceStream.CopyToAsync(targetStream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Core Helper Methods
        private async Task<List<RomDto>?> FetchMetadataFromRommAsync(List<int> platformIds, RommServerConfig server)
        {
            // API implementation loop querying your target paths goes here...
            await Task.Delay(1000); // Simulate network
            return new List<RomDto>();
        }

        private object SyncWithLaunchBoxDatabase(RomDto rom, string platformName)
        {
            // Core injection wrapper matching LaunchBox plugin SDK rules
            return new object();
        }

        private void ScheduleMediaDownloads(RomDto rom, PlatformSyncTask task, MediaSelectionProfile profile, RommServerConfig server)
        {
            if (profile.BoxFront && !string.IsNullOrEmpty(rom.BoxFrontUrl))
            {
                EnqueueFileDownload(new DownloadJob
                {
                    JobType = DownloadJobType.Media,
                    RelativeUrl = rom.BoxFrontUrl,
                    DestinationPath = Path.Combine("C:\\LaunchBox\\Images", task.LaunchBoxPlatformName, "Box - Front", $"{rom.Name}.png"),
                    LaunchBoxPlatformName = task.LaunchBoxPlatformName,
                    ServerContext = server,
                    UiCard = task.UiCard
                });
            }
            // Replicate block cleanly for Box3D, Videos, Manuals, etc.
        }
    }
}
