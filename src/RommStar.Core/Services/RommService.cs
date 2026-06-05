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
        /// <summary>
        /// Tracks active cancellation tokens based on the LaunchBox platform name
        /// </summary>
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeTokens = new();

        /// <summary>
        /// Tracks remaining active file counts per platform to enforce strict sequential progression
        /// </summary>
        private readonly ConcurrentDictionary<Guid, int> _activeFileCounters = new();

        private readonly HttpClient _client;

        // Channel pipelines handling FIFO operations natively across background threads
        private readonly Channel<PlatformSyncTask> _platformQueue = Channel.CreateUnbounded<PlatformSyncTask>();

        private readonly Channel<DownloadJob> _fileDownloadQueue = Channel.CreateUnbounded<DownloadJob>();

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
                Id = Guid.NewGuid(),
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
                UiCard = uiCard,
            };

            // REGISTER THE TASK CTS SO IT CAN BE RECOVERED BY THE CANCEL BUTTON CLICK
            _activeTokens[uiCard.Id] = task.Cts;

            _platformQueue.Writer.TryWrite(task);
        }

        public void CancelPlatformSync(Guid jobId)
        {
            var card = ActiveSyncJobs.FirstOrDefault(j => j.Id == jobId);
            if (card != null && (card.Status == SyncStatus.Queued || card.Status == SyncStatus.SyncingFiles || card.Status == SyncStatus.ProcessingMetadata))
            {
                // 1. Immediately visually update the UI state
                card.Status = SyncStatus.Cancelled;

                // 2. Extract the internal task token and trip it
                if (_activeTokens.TryRemove(jobId, out var cts))
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch (ObjectDisposedException) { }
                }
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
                    if (platformTask.UiCard.Status == SyncStatus.Cancelled)
                    {
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    platformTask.UiCard.Status = SyncStatus.ProcessingMetadata;
                    var currentSnapshot = ActiveServer; // Snap authorization state cleanly

                    // STEP 1: Metadata Request execution
                    var roms = await FetchMetadataFromRommAsync(platformTask.RommPlatformIds, currentSnapshot);
                    if (roms == null || roms.Count == 0)
                    {
                        platformTask.UiCard.Status = SyncStatus.CompletedWithErrors;
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    if (platformTask.Cts.Token.IsCancellationRequested) { platformTask.UiCard.Status = SyncStatus.Cancelled; _activeTokens.TryRemove(platformTask.Id, out _); continue; }

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
                                JobId = platformTask.Id,
                                JobType = DownloadJobType.Rom,
                                RelativeUrl = rom.RomUrl,
                                DestinationPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.LaunchBoxPlatformName, rom.FileName),
                                LaunchBoxPlatformName = platformTask.LaunchBoxPlatformName,
                                ServerContext = currentSnapshot,
                                UiCard = platformTask.UiCard,
                                CancellationToken = platformTask.Cts.Token, // <-- PASS TOKEN HERE
                                OnSuccessCallback = () => { /* TODO: Flip IGame.Installed = true; SaveChanges(); */ }
                            });
                        }

                        // Schedule individual media files by cross-checking profile toggles
                        ScheduleMediaDownloads(rom, platformTask, chosenProfile, currentSnapshot);
                    }

                    // STEP 3: Wait using Guid key tracking
                    while (_activeFileCounters.TryGetValue(platformTask.Id, out int fileCount) && fileCount > 0)
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
                    _activeFileCounters.TryRemove(platformTask.Id, out _);

                    _activeTokens.TryRemove(platformTask.Id, out _);
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
            _activeFileCounters.AddOrUpdate(job.JobId, 1, (key, current) => current + 1);
            if (job.UiCard != null) job.UiCard.TotalItems++;

            _fileDownloadQueue.Writer.TryWrite(job);
        }

        private async Task StartFileQueueProcessorAsync()
        {
            while (await _fileDownloadQueue.Reader.WaitToReadAsync())
            {
                while (_fileDownloadQueue.Reader.TryRead(out var job))
                {
                    // 1. Check if the parent platform sync was aborted while this item sat in the queue
                    if (job.CancellationToken.IsCancellationRequested)
                    {
                        // Instantly tick down counters without touching the network
                        _activeFileCounters.AddOrUpdate(job.JobId, 0, (key, current) => current - 1);
                        continue;
                    }

                    bool success = await StreamFileFromNetworkAsync(job.RelativeUrl, job.DestinationPath, job.ServerContext, job.CancellationToken);

                    if (!success && job.UiCard != null && !job.CancellationToken.IsCancellationRequested)
                        job.UiCard.ErrorCount++;
                    else if (success)
                        job.OnSuccessCallback?.Invoke();

                    if (job.UiCard != null) job.UiCard.ProcessedItems++;
                    _activeFileCounters.AddOrUpdate(job.JobId, 0, (key, current) => current - 1);
                }
            }
        }

        private async Task<bool> StreamFileFromNetworkAsync(string relativeUrl, string targetPath, RommServerConfig server, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return true;

            // TEMP TEST DATA
            try
            {
                // Pass the token to Task.Delay so your test simulated code aborts immediately
                await Task.Delay(1000, cancellationToken);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            // END TEMP TEST DATA

            try
            {
                string completeUrl = $"{server.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
                using var request = new HttpRequestMessage(HttpMethod.Get, completeUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.ApiToken);

                // Pass cancellationToken to SendAsync to kill connection setup if canceled
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

                // Pass cancellationToken to CopyToAsync to kill the stream writing instantly if canceled
                await sourceStream.CopyToAsync(targetStream, cancellationToken);
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
            // TEMP test Data
            // Simulate the network delay of hitting the Romm API
            await Task.Delay(1000);

            // Return 3 fake games so we have items to process
            return new List<RomDto>
                {
                    new RomDto { Id = 1, Name = "Super Mario Bros", FileName = "mario.zip", RomUrl = "fake/mario.zip", BoxFrontUrl = "fake/mario.png" },
                    new RomDto { Id = 2, Name = "Sonic the Hedgehog", FileName = "sonic.zip", RomUrl = "fake/sonic.zip", BoxFrontUrl = "fake/sonic.png" },
                    new RomDto { Id = 3, Name = "The Legend of Zelda", FileName = "zelda.zip", RomUrl = "fake/zelda.zip", BoxFrontUrl = "fake/zelda.png" },
                    new RomDto { Id = 1, Name = "S Bros", FileName = "mario.zip", RomUrl = "fake/mario.zip", BoxFrontUrl = "fake/mario.png" },
                    new RomDto { Id = 2, Name = "Sonic ", FileName = "sonic.zip", RomUrl = "fake/sonic.zip", BoxFrontUrl = "fake/sonic.png" },
                    new RomDto { Id = 3, Name = "Zelda", FileName = "zelda.zip", RomUrl = "fake/zelda.zip", BoxFrontUrl = "fake/zelda.png" }
                };

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
                    JobId = task.Id, // Link media download job to Guid
                    JobType = DownloadJobType.Media,
                    RelativeUrl = rom.BoxFrontUrl,
                    DestinationPath = Path.Combine("C:\\LaunchBox\\Images", task.LaunchBoxPlatformName, "Box - Front", $"{rom.Name}.png"),
                    LaunchBoxPlatformName = task.LaunchBoxPlatformName,
                    ServerContext = server,
                    UiCard = task.UiCard,
                    CancellationToken = task.Cts.Token
                });
            }
            // Replicate block cleanly for Box3D, Videos, Manuals, etc.
        }
    }
}