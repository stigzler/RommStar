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
using Accessibility;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Services;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Main Architecture Core Engine managing data coordination.
    /// </summary>
    public class SyncManager
    {
        /// <summary>
        /// Tracks remaining active file counts per platform to enforce strict sequential progression
        /// </summary>
        private readonly ConcurrentDictionary<Guid, int> _activeFileCounters = new();

        /// <summary>
        /// Tracks active cancellation tokens based on the LaunchBox platform name
        /// </summary>
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeTokens = new();

        private readonly HttpClient _client;

        private readonly Channel<DownloadJob> _fileDownloadQueue = Channel.CreateUnbounded<DownloadJob>();

        private readonly RommService _rommService;

        /// <summary>
        /// Channel pipelines handling FIFO operations natively across background threads
        /// </summary>
        private readonly Channel<PlatformSyncTask> _platformQueue = Channel.CreateUnbounded<PlatformSyncTask>();


        /// <summary>
        /// Used primarily as the default initial fallback server or design-time tracking context.
        /// Macro and file tasks resolve their specific target servers natively via contextual properties.
        /// </summary>
        public RommServer ActiveServer { get; set; }

        public ObservableCollection<PlatformSyncJob> ActiveSyncJobs { get; } = new();

        /// <summary>
        /// Media Profiles configuration sets
        /// </summary>
        public MediaSelectionProfile CatalogProfile { get; set; } = new() { BoxFront = true, Screenshots = true };

        public MediaSelectionProfile InstallProfile { get; set; } = new() { BoxFront = true, Box3D = true, Videos = true, Manuals = true, Music = true };

        public SyncManager(RommServer initialServer, RommService rommService)
        {
            // Thread-safe client initialization without global default authorization headers
            _client = new HttpClient();
            ActiveServer = initialServer;

            // Kick off long-running infrastructure daemons
            _ = Task.Run(StartPlatformQueueProcessorAsync);
            _ = Task.Run(StartFileQueueProcessorAsync);

            _rommService = rommService;
        }

        public event Action<PlatformSyncJob>? OnSyncCompletedNotification;

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
        // PARALLEL ON-DEMAND BYPASS (Bypasses macro structural sync channel entirely)
        // =========================================================================
        // ToDO: IGAME
        public async Task ExecuteOnDemandInstallAsync(string lbPlatform, RomDTO rom, RommServer targetServer)
        {
            var currentSnapshot = targetServer;
            // string destinationRomPath = Path.Combine("C:\\LaunchBox\\Games", lbPlatform, rom.FileName); // TODO: Reinstate from new DTOs

            // TODO: Reinstate from new DTOs:
            // 1. Instantly pull down the critical ROM execution payload on a dedicated task lane
            // bool romSuccess = await StreamFileFromNetworkAsync(rom.RomUrl, destinationRomPath, currentSnapshot);
            //if (!romSuccess) return;

            // Target Game update invocation block
            // targetIGameInstance.Installed = true;

            // 2. Scan and stream down heavy Install Profile assets concurrently on the fly
            var mediaTasks = new List<Task>();

            // TODO: Reinstate from new DTOs:
            //if (InstallProfile.Videos && !string.IsNullOrEmpty(rom.VideoUrl))
            //{
            //    string videoPath = Path.Combine("C:\\LaunchBox\\Videos", lbPlatform, $"{rom.Name}.mp4");
            //    mediaTasks.Add(StreamFileFromNetworkAsync(rom.VideoUrl, videoPath, currentSnapshot));
            //}


            // Append other contextual profiles smoothly...

            await Task.WhenAll(mediaTasks);
        }

        // =========================================================================
        // MACRO MANAGEMENT: ENQUEUE PLATFORM RUN
        // =========================================================================
        public void QueuePlatformSync(string lbPlatformName, List<int> rommPlatformIds, SyncProfile? syncProfile, RommServer targetServer)
        {
            var uiCard = new PlatformSyncJob
            {
                Id = Guid.NewGuid(),
                LaunchBoxPlatformName = lbPlatformName,
                ServerName = targetServer.ServerName,
                Status = SyncStatus.Queued
            };

            // Safely push to UI Collection from background hooks if necessary
            ActiveSyncJobs.Add(uiCard);

            var task = new PlatformSyncTask
            {
                LaunchBoxPlatformName = lbPlatformName,
                RommPlatformIds = rommPlatformIds,
                //DownloadRomFiles = downloadRoms,
                UiCard = uiCard,
                TargetServer = targetServer,
            };

            if (syncProfile == SyncProfile.CreateGame_DownloadRom_DownloadMedia ||
                syncProfile == SyncProfile.CreateGame_DownloadRom ||
                syncProfile == SyncProfile.DownloadRom)
            {
                task.DownloadRomFiles = true;
            }

            // REGISTER THE TASK CTS SO IT CAN BE RECOVERED BY THE CANCEL BUTTON CLICK
            _activeTokens[uiCard.Id] = task.Cts;

            _platformQueue.Writer.TryWrite(task);
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

        /// <summary>
        /// Core Helper Methods
        /// </summary>
        /// <param name="platformIds"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        private async Task<RomCollectionDTO> FetchMetadataFromRommAsync(List<int> platformIds, RommServer server, int offset)
        {
            // TEMP test Data
            // Simulate the network delay of hitting the Romm API
            //await Task.Delay(1000);

            // Return 3 fake games so we have items to process
            //return new List<RomDTO>
            //    {
            //        new RomDTO { Id = 1, Name = "Super Mario Bros", FileName = "mario.zip", RomUrl = "fake/mario.zip", BoxFrontUrl = "fake/mario.png" },
            //        new RomDTO { Id = 2, Name = "Sonic the Hedgehog", FileName = "sonic.zip", RomUrl = "fake/sonic.zip", BoxFrontUrl = "fake/sonic.png" },
            //        new RomDTO { Id = 3, Name = "The Legend of Zelda", FileName = "zelda.zip", RomUrl = "fake/zelda.zip", BoxFrontUrl = "fake/zelda.png" },
            //        new RomDTO { Id = 1, Name = "S Bros", FileName = "mario.zip", RomUrl = "fake/mario.zip", BoxFrontUrl = "fake/mario.png" },
            //        new RomDTO { Id = 2, Name = "Sonic ", FileName = "sonic.zip", RomUrl = "fake/sonic.zip", BoxFrontUrl = "fake/sonic.png" },
            //        new RomDTO { Id = 3, Name = "Zelda", FileName = "zelda.zip", RomUrl = "fake/zelda.zip", BoxFrontUrl = "fake/zelda.png" }
            //    };

            // API implementation loop querying your target paths goes here...
            //await Task.Delay(1000); // Simulate network

            var apiResult = await _rommService.GetRomCollectionAsync(server, platformIds, offset);

            if (!apiResult.IsSuccess)
            {
                // TODO: Error handling
            }

            if (apiResult.Data != null)
            {
                return apiResult.Data;
            }

            return new RomCollectionDTO();
        }

        private void ScheduleMediaDownloads(RomDTO rom, PlatformSyncTask task, MediaSelectionProfile profile, RommServer server)
        {
            // TODO: Reinstate from new DTOs
            //if (profile.BoxFront && !string.IsNullOrEmpty(rom.BoxFrontUrl))
            //{
            //    EnqueueFileDownload(new DownloadJob
            //    {
            //        JobId = task.Id, // Link media download job to Guid
            //        JobType = DownloadJobType.Media,
            //        RelativeUrl = rom.BoxFrontUrl,
            //        DestinationPath = Path.Combine("C:\\LaunchBox\\Images", task.LaunchBoxPlatformName, "Box - Front", $"{rom.Name}.png"),
            //        LaunchBoxPlatformName = task.LaunchBoxPlatformName,
            //        ServerContext = server,
            //        UiCard = task.UiCard,
            //        CancellationToken = task.Cts.Token
            //    });
            //}
            // Replicate block cleanly for Box3D, Videos, Manuals, etc.
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

                    var currentSnapshot = platformTask.TargetServer; // Snaps the job-specific server config safely

                    // STEP 1: Metadata Request execution
                    RomCollectionDTO romCollection = await FetchMetadataFromRommAsync(platformTask.RommPlatformIds, currentSnapshot);

                    // TODO: romCollection has additional data at its root such as all genres, companies etc retrieved for all 
                    // the roms in the collection
                    // ALSO: do we need to page retrieval? How expensive is 400 psx game + numerous sub items (files/scraper results etc)
                    // Atari 8-bit took 1.38s to fetch 50 items on LAN. That is without all the scraper metadata in the mix
                    // 2700 items = 1 minute to fetch on LAN. Hmmmmm


                    if (romCollection == null || romCollection.Items.Count == 0)
                    {
                        platformTask.UiCard.Status = SyncStatus.CompletedWithErrors;
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    if (platformTask.Cts.Token.IsCancellationRequested) { platformTask.UiCard.Status = SyncStatus.Cancelled; _activeTokens.TryRemove(platformTask.Id, out _); continue; }

                    platformTask.UiCard.Status = SyncStatus.SyncingFiles;
                    var chosenProfile = platformTask.DownloadRomFiles ? InstallProfile : CatalogProfile;

                    // STEP 2A: Process local LaunchBox Database mapping & calculate files payload
                    foreach (var rom in romCollection.Items)
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;

                        // Zero code-behind execution: Inject record directly into Local Database layer
                        var lbGameMock = SyncWithLaunchBoxDatabase(rom, platformTask.LaunchBoxPlatformName);

                        // STEP 2B:  Schedule ROM file extraction if explicitly configured
                        if (platformTask.DownloadRomFiles)
                        {
                            EnqueueFileDownload(new DownloadJob
                            {
                                JobId = platformTask.Id,
                                JobType = DownloadJobType.Rom,
                                //RelativeUrl = rom., // TODO: Reinstate from new DTOs
                                //DestinationPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.LaunchBoxPlatformName, rom.FileName), // TODO: Reinstate from new DTOs
                                LaunchBoxPlatformName = platformTask.LaunchBoxPlatformName,
                                ServerContext = currentSnapshot,
                                UiCard = platformTask.UiCard,
                                CancellationToken = platformTask.Cts.Token, // <-- PASS TOKEN HERE
                                OnSuccessCallback = () => { /* TODO: Flip IGame.Installed = true; SaveChanges(); */ }
                            });
                        }

                        // STEP 2C: Schedule individual media files by cross-checking profile toggles
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
        private async Task<bool> StreamFileFromNetworkAsync(string relativeUrl, string targetPath, RommServer server, 
            CancellationToken cancellationToken = default)
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
        private object SyncWithLaunchBoxDatabase(RomDTO rom, string platformName)
        {
            // TODO: Create custom fields:
            // RomId - local rom id of the specific rom - for ondemand rom instals after syncing (ie. via install)
            // ServerId - local server id of the specific rom - for ondemand rom instals after syncing (ie. via install)


            // Core injection wrapper matching LaunchBox plugin SDK rules
            return new object();
        }
    }
}