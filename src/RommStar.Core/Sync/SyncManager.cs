using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Channels;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

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

        private readonly LaunchboxService _launchboxService;

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

        public SyncManager(RommServer initialServer, RommService rommService, LaunchboxService launchboxService)
        {
            // Thread-safe client initialization without global default authorization headers
            _client = new HttpClient();
            ActiveServer = initialServer;

            // Kick off long-running infrastructure daemons
            _ = Task.Run(StartPlatformQueueProcessorAsync);
            _ = Task.Run(StartFileQueueProcessorAsync);

            _rommService = rommService;
            _launchboxService = launchboxService;
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
        public void QueuePlatformSync(string lbPlatformName, List<int> rommPlatformIds, ExtendedSyncSettings syncSettings, RommServer targetServer)
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
                UiCard = uiCard,
                TargetServer = targetServer,
                SyncSettings = syncSettings,

                DownloadRomFiles = syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                        || syncSettings.SyncProfile == SyncProfileTypes.DownloadRom,

                UpsertIGame = (syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                || syncSettings.SyncProfile == SyncProfileTypes.CreateGame
                || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia),

                DownloadMediaFiles = syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia
                                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia

            };

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
        private async Task<RomCollectionDTO> FetchMetadataFromRommAsync(List<int> platformIds,
            RommServer server, int offset, CancellationToken cancellationToken)
        {
            var apiResult = await _rommService.GetRomCollectionAsync(server, platformIds, offset, cancellationToken);

            if (!apiResult.IsSuccess)
            {
                //Debug.WriteLine($"Romm Collection Paging offset: {apiResult.Data.Offset}");
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
            EnqueueFileDownload(new DownloadJob
            {
                JobId = task.Id, // Link media download job to Guid
                JobType = DownloadJobType.Media,
                //RelativeUrl = rom.BoxFrontUrl,
                RelativeUrl = "rom.BoxFrontUrl",
                DestinationPath = Path.Combine("C:\\LaunchBox\\Images", task.LaunchBoxPlatformName, "Box - Front", $"{rom.Name}.png"),
                LaunchBoxPlatformName = task.LaunchBoxPlatformName,
                ServerContext = server,
                UiCard = task.UiCard,
                CancellationToken = task.Cts.Token
            });
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
        // MACRO SEQUENTIAL PIPELINE PROCESSOR (Paging Stream Integration)
        // =========================================================================
        private async Task StartPlatformQueueProcessorAsync()
        {
            while (await _platformQueue.Reader.WaitToReadAsync())
            {
                while (_platformQueue.Reader.TryRead(out var platformTask))
                {
                    if (platformTask.UiCard.Status == SyncStatus.Cancelled)
                    {
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    _launchboxService.SetupGameUpserts(platformTask.LaunchBoxPlatformName, platformTask.TargetServer.Id.ToString(), platformTask.SyncSettings);
                    platformTask.UiCard.Status = SyncStatus.ProcessingMetadata;
                    var currentSnapshot = platformTask.TargetServer;

                    int safePageLimit = currentSnapshot.PageLimit > 0 ? currentSnapshot.PageLimit : 50;
                    int offset = 0;
                    int totalItems = 0;
                    bool isFirstFetch = true;
                    bool collectionHasProcessedAnyItems = false;

                    var installRoms = platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                        || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                        || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.DownloadRom;

                    var chosenProfile = installRoms ? InstallProfile : CatalogProfile;

                    var processedGamesLookup = new Dictionary<int, IGame>();
                    var siblingQueue = new List<(RomDTO SiblingRom, int ParentRomId)>();

                    do
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;

                        RomCollectionDTO romCollection = await FetchMetadataFromRommAsync(platformTask.RommPlatformIds, currentSnapshot, offset, platformTask.Cts.Token);

                        if (isFirstFetch)
                        {
                            totalItems = romCollection.Total ?? 0;
                            isFirstFetch = false;

                            if (totalItems == 0 || romCollection.Items == null || romCollection.Items.Count == 0)
                            {
                                break;
                            }
                            platformTask.UiCard.Status = SyncStatus.SyncingFiles;
                        }

                        if (romCollection.Items == null || romCollection.Items.Count == 0)
                        {
                            break;
                        }

                        collectionHasProcessedAnyItems = true;

                        foreach (var rom in romCollection.Items)
                        {
                            if (platformTask.Cts.Token.IsCancellationRequested) break;

                            bool hasSiblings = rom.SiblingRoms != null && rom.SiblingRoms.Count > 0;
                            bool isMultiDiscLayout = rom.HasMultipleFiles == true;

                            // --- STRATEGY A: SELECTIVE JUST-IN-TIME HYDRATION ---
                            // Hydrate if: We need files for immediate download OR it's a multi-disc directory structure.
                            // Notice that single-file sibling variants don't trigger hydration here if doing a metadata sync!
                            bool needsHydration = installRoms || isMultiDiscLayout;

                            var detailedRom = rom;
                            if (needsHydration && (rom.Files == null || rom.Files.Count == 0))
                            {
                                var detailResult = await _rommService.GetRomDetailsAsync(currentSnapshot, rom.Id ?? 0, platformTask.Cts.Token);
                                if (detailResult.IsSuccess && detailResult.Data != null)
                                {
                                    detailedRom = detailResult.Data;
                                }
                            }

                            bool hasFiles = detailedRom.Files != null && detailedRom.Files.Count > 0;
                            bool isGroupedLayout = isMultiDiscLayout || hasSiblings;

                            string basePlatformPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.LaunchBoxPlatformName);
                            string targetDirectory = isGroupedLayout ? Path.Combine(basePlatformPath, detailedRom.Name) : basePlatformPath;

                            IGame targetedGame = null;

                            // =========================================================================
                            // CASE 1: MULTI-FILE / MULTI-DISC GAMES
                            // =========================================================================
                            if (detailedRom.HasMultipleFiles == true && hasFiles)
                            {
                                // TODO: update this logic to encompass other scenarios
                                var primaryFile = detailedRom.Files.FirstOrDefault(f => !string.IsNullOrEmpty(f.FileName) 
                                && f.FileName.Contains("Disc 1", StringComparison.OrdinalIgnoreCase))
                                                  ?? detailedRom.Files.First();

                                if (platformTask.UpsertIGame)
                                {
                                    targetedGame = await _launchboxService.SyncRommDto(detailedRom);
                                    if (targetedGame != null && installRoms && !string.IsNullOrEmpty(primaryFile.FileName))
                                    {
                                        targetedGame.ApplicationPath = Path.Combine(targetDirectory, primaryFile.FileName);
                                    }
                                }

                                foreach (var fileEntry in detailedRom.Files)
                                {
                                    if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                    if (platformTask.UpsertIGame && targetedGame != null)
                                    {
                                        _launchboxService.AddOrUpdateAdditionalApplication(targetedGame, fileEntry, targetDirectory);
                                    }

                                    if (installRoms)
                                    {
                                        EnqueueRomDownloadJob(platformTask, currentSnapshot, detailedRom.Id ?? 0, fileEntry, targetDirectory);
                                    }
                                }
                            }
                            // =========================================================================
                            // CASE 2: SIBLING ROM REGION / VERSION GROUPS
                            // =========================================================================
                            else if (hasSiblings)
                            {
                                bool isExplicitMaster = detailedRom.RomUserData?.IsMainSibling ?? false;
                                int lowestGroupId = Math.Min(detailedRom.Id ?? 0, detailedRom.SiblingRoms.Select(s => s.Id ?? 0).Min());

                                if (!isExplicitMaster && (detailedRom.Id ?? 0) > lowestGroupId)
                                {
                                    siblingQueue.Add((detailedRom, lowestGroupId));
                                    continue;
                                }

                                // Aggregate Master ID + Sibling IDs into a comma-separated tracking string
                                var romIdsList = new List<int> { detailedRom.Id ?? 0 };
                                romIdsList.AddRange(detailedRom.SiblingRoms.Select(s => s.Id ?? 0));
                                string aggregatedRomIdsCsv = string.Join(",", romIdsList);

                                if (platformTask.UpsertIGame)
                                {
                                    targetedGame = await _launchboxService.SyncRommDto(detailedRom, aggregatedRomIdsCsv);
                                }

                                // Use hydrated files if present, fallback to top-level property otherwise
                                if (hasFiles)
                                {
                                    foreach (var masterFile in detailedRom.Files)
                                    {
                                        if (string.IsNullOrEmpty(masterFile.FileName)) continue;

                                        if (platformTask.UpsertIGame && targetedGame != null && string.IsNullOrEmpty(targetedGame.ApplicationPath))
                                        {
                                            targetedGame.ApplicationPath = Path.Combine(targetDirectory, masterFile.FileName);
                                        }

                                        if (installRoms)
                                        {
                                            EnqueueRomDownloadJob(platformTask, currentSnapshot, detailedRom.Id ?? 0, masterFile, targetDirectory);
                                        }
                                    }
                                }
                                else if (!string.IsNullOrEmpty(detailedRom.RommFilename))
                                {
                                    if (platformTask.UpsertIGame && targetedGame != null && string.IsNullOrEmpty(targetedGame.ApplicationPath))
                                    {
                                        targetedGame.ApplicationPath = Path.Combine(targetDirectory, detailedRom.RommFilename);
                                    }
                                }
                            }
                            // =========================================================================
                            // CASE 3: STANDARD SINGLE-FILE GAMES
                            // =========================================================================
                            else
                            {
                                if (platformTask.UpsertIGame)
                                {
                                    targetedGame = await _launchboxService.SyncRommDto(detailedRom);
                                }

                                if (hasFiles)
                                {
                                    var singleFile = detailedRom.Files.First();
                                    if (!string.IsNullOrEmpty(singleFile.FileName))
                                    {
                                        if (platformTask.UpsertIGame && targetedGame != null)
                                        {
                                            targetedGame.ApplicationPath = Path.Combine(targetDirectory, singleFile.FileName);
                                        }

                                        if (installRoms)
                                        {
                                            EnqueueRomDownloadJob(platformTask, currentSnapshot, detailedRom.Id ?? 0, singleFile, targetDirectory);
                                        }
                                    }
                                }
                                else if (!string.IsNullOrEmpty(detailedRom.RommFilename))
                                {
                                    if (platformTask.UpsertIGame && targetedGame != null)
                                    {
                                        targetedGame.ApplicationPath = Path.Combine(targetDirectory, detailedRom.RommFilename);
                                    }
                                }
                            }

                            if (targetedGame != null && detailedRom.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRom.Id.Value))
                            {
                                processedGamesLookup.Add(detailedRom.Id.Value, targetedGame);
                            }

                            if (platformTask.DownloadMediaFiles)
                            {
                                ScheduleMediaDownloads(detailedRom, platformTask, chosenProfile, currentSnapshot);
                            }
                        }

                        offset += safePageLimit;

                    } while (offset < totalItems && !platformTask.Cts.Token.IsCancellationRequested);

                    if (!collectionHasProcessedAnyItems && !platformTask.Cts.Token.IsCancellationRequested)
                    {
                        platformTask.UiCard.Status = SyncStatus.CompletedWithErrors;
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    // =========================================================================
                    // LATE-BIND DEFERRED SIBLINGS (Pass 2)
                    // =========================================================================
                    foreach (var (siblingRom, parentRomId) in siblingQueue)
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;

                        // For deferred sibling variants, hydrate their specific file targets if we are running an active download profile
                        var detailedSibling = siblingRom;
                        if (installRoms && (siblingRom.Files == null || siblingRom.Files.Count == 0))
                        {
                            var detailResult = await _rommService.GetRomDetailsAsync(currentSnapshot, siblingRom.Id ?? 0, platformTask.Cts.Token);
                            if (detailResult.IsSuccess && detailResult.Data != null)
                            {
                                detailedSibling = detailResult.Data;
                            }
                        }

                        if (processedGamesLookup.TryGetValue(parentRomId, out IGame masterGameInstance))
                        {
                            string basePlatformPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.LaunchBoxPlatformName);
                            string targetDirectory = Path.Combine(basePlatformPath, masterGameInstance.Title);

                            if (detailedSibling.Files != null && detailedSibling.Files.Count > 0)
                            {
                                foreach (var fileEntry in detailedSibling.Files)
                                {
                                    if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                    if (platformTask.UpsertIGame)
                                    {
                                        string variantLabel = $"Play Version: {Path.GetFileNameWithoutExtension(fileEntry.FileName)}";
                                        _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, fileEntry, targetDirectory, variantLabel);
                                    }

                                    if (installRoms)
                                    {
                                        EnqueueRomDownloadJob(platformTask, currentSnapshot, detailedSibling.Id ?? 0, fileEntry, targetDirectory);
                                    }
                                }
                            }
                            else if (!string.IsNullOrEmpty(detailedSibling.RommFilename))
                            {
                                if (platformTask.UpsertIGame)
                                {
                                    var placeholderFileDto = new RomFileDTO { FileName = detailedSibling.RommFilename };
                                    string variantLabel = $"Play Version: {Path.GetFileNameWithoutExtension(detailedSibling.RommFilename)}";
                                    _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, placeholderFileDto, targetDirectory, variantLabel);
                                }
                            }
                        }
                    }

                    while (_activeFileCounters.TryGetValue(platformTask.Id, out int fileCount) && fileCount > 0)
                    {
                        if (platformTask.Cts.Token.IsCancellationRequested) break;
                        await Task.Delay(100);
                    }

                    PluginHelper.DataManager.Save();
                    PluginHelper.LaunchBoxMainViewModel.RefreshData();

                    if (platformTask.Cts.Token.IsCancellationRequested)
                    {
                        platformTask.UiCard.Status = SyncStatus.Cancelled;
                    }
                    else
                    {
                        platformTask.UiCard.Status = platformTask.UiCard.ErrorCount > 0 ? SyncStatus.CompletedWithErrors : SyncStatus.Completed;
                        OnSyncCompletedNotification?.Invoke(platformTask.UiCard);
                    }

                    _activeFileCounters.TryRemove(platformTask.Id, out _);
                    _activeTokens.TryRemove(platformTask.Id, out _);
                }
            }
        }



        /// <summary>
        /// Centralized wrapper ensuring identical Job formatting logic across all execution patterns
        /// </summary>
        private void EnqueueRomDownloadJob(PlatformSyncTask platformTask, RommServer currentSnapshot,
            int romId, RomFileDTO fileDto, string targetDirectory)
        {
            EnqueueFileDownload(new DownloadJob
            {
                JobId = platformTask.Id,
                JobType = DownloadJobType.Rom,
                LaunchBoxPlatformName = platformTask.LaunchBoxPlatformName,
                ServerContext = currentSnapshot,
                UiCard = platformTask.UiCard,
                CancellationToken = platformTask.Cts.Token,
                RelativeUrl = $"/api/v1/roms/{romId}/files/{fileDto.Id}/download",
                DestinationPath = Path.Combine(targetDirectory, fileDto.FileName),
                OnSuccessCallback = () => { }
            });
        }


        private async Task<bool> StreamFileFromNetworkAsync(string relativeUrl, string targetPath, RommServer server,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return true;

            // TEMP TEST DATA
            try
            {
                // Pass the token to Task.Delay so your test simulated code aborts immediately
                await Task.Delay(100, cancellationToken);
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
        private object SyncWithLaunchBoxDatabaseIfSet(RomDTO rom, string platformName)
        {
            // Check if defaul or platform specific profile indicates launchbox database sync disabled




            // TODO: Create custom fields:
            // RomId - local rom id of the specific rom - for ondemand rom instals after syncing (ie. via install)
            // ServerId - local server id of the specific rom - for ondemand rom instals after syncing (ie. via install)


            // Core injection wrapper matching LaunchBox plugin SDK rules
            return new object();
        }
    }
}