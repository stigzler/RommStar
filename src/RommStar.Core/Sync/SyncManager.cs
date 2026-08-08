using Microsoft.Xaml.Behaviors;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Helpers;
using RommStar.Core.Models;
using RommStar.Core.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Windows;
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

        private readonly LaunchboxDataService _launchboxService;
        /// <summary>
        /// Channel pipelines handling FIFO operations natively across background threads
        /// </summary>
        private readonly Channel<PlatformSyncTask> _platformQueue = Channel.CreateUnbounded<PlatformSyncTask>();

        private readonly RommService _rommService;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        /// <summary>
        /// Used primarily as the default initial fallback server or design-time tracking context.
        /// Macro and file tasks resolve their specific target servers natively via contextual properties.
        /// </summary>
        public RommServer ActiveServer { get; set; }

        /// <summary>
        /// Linked with the UI Job monitoring cards
        /// </summary>
        public ObservableCollection<PlatformSyncJob> ActiveSyncJobs { get; } = new();
        public SyncManager(RommServer initialServer, RommService rommService, LaunchboxDataService launchboxService, SettingsService settingsService,
            NotificationService notificationService)
        {
            // Thread-safe client initialization without global default authorization headers
            _client = new HttpClient();
            ActiveServer = initialServer;

            // Kick off long-running infrastructure daemons
            _ = Task.Run(StartPlatformQueueProcessorAsync);
            _ = Task.Run(StartFileQueueProcessorAsync);

            _rommService = rommService;
            _launchboxService = launchboxService;
            _settingsService = settingsService;
            _notificationService = notificationService;
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

        internal bool PlatformQueuedAndIncomplete(string platformName)
          {
            return ActiveSyncJobs.Any(j => j.LaunchBoxPlatformName == platformName && j.Status < SyncStatus.Completed);
        }

        // =========================================================================
        // MACRO MANAGEMENT: ENQUEUE PLATFORM RUN
        // =========================================================================
        public async Task EnqueuePlatformSync(string lbPlatformName, string lbPlatformRomFolder, IPlatformFolder[] lbMediaFolders, string emulatorId,
            List<int> rommPlatformIds, ExtendedSyncSettings syncSettings, RommServer targetServer, int romCount, bool notifyLaunchboxOnMeatadataDone = false)
        {
            var uiJobCard = new PlatformSyncJob
            {
                Id = Guid.NewGuid(),
                LaunchBoxPlatformName = lbPlatformName,
                ServerName = targetServer.ServerName,
                Status = SyncStatus.Queued,
                RomCount = romCount,
                SupressSuccessLogItems = _settingsService.Settings.HideSuccessEntries,


            };

            // Safely push to UI Collection from background hooks if necessary
            ActiveSyncJobs.Add(uiJobCard);

            var task = new PlatformSyncTask
            {
                PlatformName = lbPlatformName,
                LaunchBoxRomFolder = lbPlatformRomFolder,
                PlatformMediaFolders = lbMediaFolders,
                EmulatorID = emulatorId,
                RommPlatformIds = rommPlatformIds,
                UiCard = uiJobCard,
                TargetServer = targetServer,
                SyncSettings = syncSettings,
                NotifyLauncboxWhenMetadataComplete = notifyLaunchboxOnMeatadataDone,

                DownloadRomFiles = syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                        || syncSettings.SyncProfile == SyncProfileTypes.DownloadRom,

                UpsertGame = (syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame
                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia),

                DownloadMediaFiles = syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia
                                        || syncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia

            };

            // REGISTER THE TASK CTS SO IT CAN BE RECOVERED BY THE CANCEL BUTTON CLICK
            _activeTokens[uiJobCard.Id] = task.Cts;

            _platformQueue.Writer.TryWrite(task);
        }

        private void EnqueueBatchRomDownloadJob(PlatformSyncTask platformTask, IGame game, RomDTO romDto, List<int> allRommIds, string masterFilename,
                    long totalSizeBytes, string serverId, List<RomQueueItem> stagedQueue, bool notifyLaunchbox = false, List<string>? siblingVariantRoms = null)
        {
            var existingQueue = _settingsService.Settings.RomDownloadQueue;

            // 1. Check if the game is already in the persisted settings queue
            var existingPersistedItem = existingQueue.FirstOrDefault(q => q != null && q.LaunchboxId == game.Id);

            if (existingPersistedItem != null)
            {
                // 2. The item already exists in the live queue. Is it quarantined?
                if (existingPersistedItem.IsQuarantined)
                {
                    existingPersistedItem.IsQuarantined = false;
                    existingPersistedItem.RetryCount = 0;
                    existingPersistedItem.LastError = string.Empty;
                    existingPersistedItem.AddedAt = DateTime.UtcNow;
                    existingPersistedItem.NotifyLaunchboxOnCompletion = notifyLaunchbox;

                    _settingsService.Save();
                    platformTask.UiCard.AddLog($"Re-queued quarantined item '{game.Title}' for batch download.", PlatformSyncJob.LogType.Info);
                }
                return; // Return immediately to avoid duplicates
            }

            // 3. Check if we have already staged this item during THIS sync run
            if (stagedQueue.Any(q => q != null && q.LaunchboxId == game.Id))
            {
                return;
            }

            // 4. The item does not exist at all. Safely build and insert a brand new queue item to the STAGED list.
            var queueItem = new RomQueueItem
            {
                LaunchboxId = game.Id,
                PlatformName = platformTask.PlatformName,
                PlatformStub = romDto.PlatformStub ?? string.Empty,
                MasterFilename = masterFilename,
                IsMultiFileGame = romDto.HasMultipleFiles == true,
                RommIds = allRommIds,
                TotalSizeBytes = totalSizeBytes,
                GameNameSanitised = RommStar.Core.Helpers.StringsHelper.SanitizeFileName(game.Title),
                AddedAt = DateTime.UtcNow,
                IsPriority = false,
                ServerId = serverId,
                NotifyLaunchboxOnCompletion = notifyLaunchbox
            };

            if (queueItem.IsMultiFileGame)
            {
                queueItem.MultiFiles = romDto.Files;
            }
            else if (romDto.SiblingRoms != null && romDto.SiblingRoms.Count > 0)
            {
                queueItem.IsSiblingSet = true;
                // Single file sibling rom
                // Urgh - below doesn't work. Sadly, .files is only the single sib file, and .siblingfiles
                // only contains the file name not the extension! Bit of a daft oversight on romm's part..
                queueItem.MultiFiles = romDto.Files;
            }

            // ADD TO STAGED QUEUE ONLY - NO SETTINGS SAVE HERE
            stagedQueue.Add(queueItem);

            // Optional: Log it for UI transparency
            platformTask.UiCard.AddLog($"Staged '{game.Title}' rom file/s for batch download.", PlatformSyncJob.LogType.Info);
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
        /// Centralized wrapper ensuring identical Job formatting logic across all execution patterns
        /// </summary>
        private void EnqueueRomDownloadJob(PlatformSyncTask platformTask, RommServer currentSnapshot,
            int romId, RomFileDTO fileDto, string targetDirectory, string romName)
        {
            //TODO: needs testing - emulatorId correct for all 4 calls?
            string emulatorId = _launchboxService.EmulatorId;

            EnqueueFileDownload(new DownloadJob
            {
                JobId = platformTask.Id,
                JobType = DownloadJobType.Rom,
                RomName = romName,
                LaunchBoxPlatformName = platformTask.PlatformName,
                ServerContext = currentSnapshot,
                UiCard = platformTask.UiCard,
                CancellationToken = platformTask.Cts.Token,
                RelativeUrl = $"/api/v1/roms/{romId}/files/{fileDto.Id}/download",
                DestinationPath = Path.Combine(targetDirectory, fileDto.FileName),
                OnSuccessCallback = () => { }
            });
        }

        //            // Direct parallel execution lane bypass instead of using macro FIFO channels
        //            mediaTasks.Add(StreamFileFromNetworkAsync(apiRelativeUrl, item.TargetLocalPath, targetServer, CancellationToken.None));
        //        }
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

        /// <summary>
        /// Formats execution metrics into human-readable timing strings based on task limits.
        /// </summary>
        private string FormatElapsedTime(TimeSpan time)
        {
            if (time.TotalMinutes >= 1)
            {
                return $"{Math.Floor(time.TotalMinutes)}m {time.Seconds}s";
            }
            if (time.TotalSeconds >= 1)
            {
                return $"{time.TotalSeconds:F2}s";
            }
            return $"{time.TotalMilliseconds:F0}ms";
        }

        /// <summary>
        /// Turns lb's variable rom path format into standardized one.
        /// </summary>
        /// <param name="baseRootDir"></param>
        /// <param name="rawPath"></param>
        /// <returns></returns>
        private string NormalizeRomPath(string baseRootDir, string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return baseRootDir;

            // If it's already rooted (e.g., "C:\Games..." or "\\HPServer\Games..."), return it fully evaluated
            if (Path.IsPathRooted(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            // Otherwise combine with LaunchBox root directory and collapse relative elements (..\)
            string combined = Path.Combine(baseRootDir, rawPath);
            return Path.GetFullPath(combined);
        }

        // =========================================================================
        // PARALLEL ON-DEMAND BYPASS (Bypasses macro structural sync channel entirely)
        // =========================================================================
        // ToDO: IGAME
        // NOTE: Legacy - not sure why this was in here - could be AI gen
        //public async Task ExecuteOnDemandInstallAsync(string lbPlatform, RomDTO rom, RommServer targetServer, PlatformSyncTask syncTask)
        //{
        //    //var currentSnapshot = targetServer;
        //    //var mediaTasks = new List<Task>();
        //    //await Task.WhenAll(mediaTasks);
        //    if (rom == null || targetServer == null) return;

        //    IPlatform platform = PluginHelper.DataManager.GetPlatformByName(lbPlatform);

        //    // 1. Normalize and resolve the target ROM path
        //    string baseRomDir = NormalizeRomPath(Constants.LaunchboxRootDir, platform.Folder); // Ensure this setting property is exposed/passed
        //    string targetRomDirectory = (rom.HasMultipleFiles == true || (rom.SiblingRoms != null && rom.SiblingRoms.Count > 0))
        //        ? Path.Combine(baseRomDir, rom.Name)
        //        : baseRomDir;

        //    // Enqueue or execute the specific ROM file streaming tasks here...

        //    // 2. Process On-Demand Media Downloads if requested
        //    bool downloadMedia = syncTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia
        //                         || syncTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia;

        //    if (downloadMedia)
        //    {
        //        // Pull the installation-specific media profile footprint
        //        var chosenProfile = _settingsService.Settings.InstallMediaProfile;

        //        // Extract native media folder paths straight from LaunchBox's global data memory
        //        var lbMediaFolders = PluginHelper.DataManager.GetPlatformByName(lbPlatform).GetAllPlatformFolders();

        //        string romFilename = !string.IsNullOrEmpty(rom.RommFilename)
        //            ? Path.GetFileNameWithoutExtension(rom.RommFilename)
        //            : rom.Name;

        //        var mediaManager = new MediaDownloadManager();

        //        var downloadItems = mediaManager.BuildDownloadItems(
        //            rom: rom,
        //            profile: chosenProfile,
        //            baseUrl: targetServer.BaseUrl,
        //            launchboxPlatformName: lbPlatform,
        //            launchboxMediaFolders: lbMediaFolders,
        //            romFilename: romFilename,
        //            forceMediaPriority: syncTask.SyncSettings.ForceMediaPriority
        //        );

        //        var mediaTasks = new List<Task>();

        //        foreach (var item in downloadItems)
        //        {
        //            // Apply the Upstream Overwrite setting check
        //            if (!syncTask.SyncSettings.OverwriteExistingMedia && File.Exists(item.TargetLocalPath))
        //            {
        //                continue;
        //            }

        //            // Map standard API path string for the download engine call
        //            string apiRelativeUrl = item.DownloadUrl.Replace(targetServer.BaseUrl, "").TrimStart('/');
        private void ScheduleMediaDownloads(RomDTO rom, PlatformSyncTask task, MediaSelectionProfile profile, RommServer server, IGame iGame)
        {
            // Extract extensionless ground-truth filename from your unified RommFilename property
            string romFilename = !string.IsNullOrEmpty(rom.RommFilename)
                ? Path.GetFileNameWithoutExtension(rom.RommFilename)
                : rom.Name;

            var mediaManager = new MediaDownloadManager();

            // Pull configuration toggles from the validated platform task settings
            bool forcePriority = task.SyncSettings.ForceMediaPriority;

            if (iGame == null) return;

            // Prospective urls - files not neccessarily there romm side
            var downloadItems = mediaManager.BuildDownloadItems(
                rom: rom,
                profile: profile,
                baseUrl: server.BaseUrl,
                launchboxPlatformName: task.PlatformName,
                launchboxMediaFolders: task.PlatformMediaFolders, // Direct IPlatformFolder tracking array
                romFilename: romFilename,
                forceMediaPriority: forcePriority,
                iGameId: iGame.Id
            );

            foreach (var item in downloadItems)
            {
                // Upstream Overwrite Media Filter Check
                if (!task.SyncSettings.OverwriteExistingMedia && File.Exists(item.TargetLocalPath))
                {
                    continue; // Skip queuing entirely if file exists and overwrite is turned off
                }

                EnqueueFileDownload(new DownloadJob
                {
                    JobId = task.Id,
                    JobType = DownloadJobType.Media,
                    MediaType = item.MediaType,
                    RomName = rom.Name,
                    RelativeUrl = item.DownloadUrl,
                    DestinationPath = item.TargetLocalPath,
                    LaunchBoxPlatformName = task.PlatformName,
                    ServerContext = server,
                    UiCard = task.UiCard,
                    CancellationToken = task.Cts.Token,
                    IGame = iGame
                });
            }
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

                    job.UiCard.Status = SyncStatus.SyncingFiles;

                    string outcome;

                    // =========================================================================
                    // TEMPORARY TESTING: Force 1-in-10 failure rate for media downloads
                    // =========================================================================
                    //if (job.JobType == DownloadJobType.Media && Random.Shared.Next(1, 11) == 1)
                    //{
                    //    outcome = false; // Fake a network failure immediately
                    //}
                    //else
                    //{
                    //    // Process normally (including ROMs)
                    //    outcome = await StreamFileFromNetworkAsync(job.RelativeUrl, job.DestinationPath, job.ServerContext, job.CancellationToken);
                    //}
                    // =========================================================================

                    outcome = await StreamFileFromNetworkAsync(job.RelativeUrl, job.DestinationPath, job.ServerContext, job.CancellationToken);

                    if (outcome != String.Empty && job.UiCard != null && !job.CancellationToken.IsCancellationRequested)
                    {
                        //job.UiCard.ErrorCount++;

                        string typeLabel = job.JobType == DownloadJobType.Rom ? "ROM"
                                           : (job.MediaType != null ? job.MediaType.ToString() : "Media");


                        if (outcome.StartsWith("Error"))
                        {
                            job.UiCard.ErrorCount++;
                            job.UiCard.AddLog($"Error whilst downloading {typeLabel} for" +
                                $" '{job.RomName}'. {outcome}", PlatformSyncJob.LogType.Error);

                        }
                        else
                        {
                            job.UiCard.AddLog($"{typeLabel} not present or download failed for" +
                            $" '{job.RomName}'. {outcome}", PlatformSyncJob.LogType.Info);

                        }

                    }
                    else if (outcome == String.Empty)
                    {
                        job.OnSuccessCallback?.Invoke();

                        // FIXED: Direct null check on job.MediaType here as well
                        string typeLabel = job.JobType == DownloadJobType.Rom ? "ROM"
                                           : (job.MediaType != null ? job.MediaType.ToString() : "Media");

                        // update relevant igame media paths (these have to be explicitly designed - LB doesn't auto calculate (of course!!))
                        bool iGameUpdated = false;
                        switch (job.MediaType)
                        {
                            case MediaType.Manual:
                                job.IGame.ManualPath = job.DestinationPath.Replace(Constants.LaunchboxRootDir + "\\", "");
                                break;
                            case MediaType.Video:
                                job.IGame.VideoPath = job.DestinationPath.Replace(Constants.LaunchboxRootDir + "\\", "");
                                break;
                            case MediaType.Music:
                                job.IGame.MusicPath = job.DestinationPath.Replace(Constants.LaunchboxRootDir + "\\", "");
                                break;
                        }

                        // if (iGameUpdated) PluginHelper.DataManager.Save()

                        // update sync log
                        job.UiCard.AddLog($"Downloaded {typeLabel} ({Path.GetFileName(job.DestinationPath)}) for '{job.RomName}'", PlatformSyncJob.LogType.Success);
                    }

                    if (job.UiCard != null) job.UiCard.ProcessedItems++;
                    _activeFileCounters.AddOrUpdate(job.JobId, 0, (key, current) => current - 1);
                }


                // HERE!

            }
        }

        //private bool LocalFilePresent(ExtendedSyncSettings syncSettings, string path, string sha1)
        //{
        //    if (syncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileOnly &&
        //        !File.Exists(path)) return false;

        //    if (syncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1 &&
        //        (string.IsNullOrEmpty(sha1) || !FileSystemHelper.LocalFilePresent(path, sha1))) return false;

        //    return true;
        //}


        // =========================================================================
        // MACRO SEQUENTIAL PIPELINE PROCESSOR (Paging Stream Integration)
        // =========================================================================

        /// <summary>
        /// Do you like monoliths?
        /// </summary>
        /// <returns></returns>
        private async Task StartPlatformQueueProcessorAsync()
        {
            while (await _platformQueue.Reader.WaitToReadAsync())
            {
                // -------------------------------------------------------------------------
                // MAIN JOB LOOP START
                // -------------------------------------------------------------------------

                while (_platformQueue.Reader.TryRead(out var platformTask))
                {
                    if (platformTask.UiCard.Status == SyncStatus.Cancelled)
                    {
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    var jobStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    platformTask.UiCard.AddLog($"Sync job started for {platformTask.PlatformName}...", PlatformSyncJob.LogType.Process);

                    try
                    {
                        // Setup LaunchboxDataService for this SyncJob setup
                        _launchboxService.SetupGameUpserts(platformTask.PlatformName, platformTask.EmulatorID,
                            platformTask.TargetServer.Id.ToString(), platformTask.SyncSettings);

                        platformTask.UiCard.Status = SyncStatus.ProcessingMetadata;

                        // ensures process uses right server (these can vary between jobs)
                        var currentServer = platformTask.TargetServer;

                        // These vars manage the romm API paging (50 at a time presently)
                        int safePageLimit = currentServer.PageLimit > 0 ? currentServer.PageLimit : 50;
                        int offset = 0;
                        int totalItems = 0;
                        bool isFirstFetch = true;
                        bool collectionHasProcessedAnyItems = false;

                        // determine whether job asks for rommDto installation
                        var installRoms = platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom
                            || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia
                            || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.DownloadRom;


                        //var installMedia = platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadMedia
                        //    || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.CreateGame_DownloadRom_DownloadMedia;


                        // determine which profile to use (install = minimal media - eg boxart; Catalg = full (eg when game installed)
                        var chosenProfile = installRoms
                            ? _settingsService.Settings.InstallMediaProfile
                            : _settingsService.Settings.SyncMediaProfile;

                        // IGame creation complicated - essentially a two-pass process. This used in tracking which have already been added
                        var processedGamesLookup = new Dictionary<int, IGame>();

                        // This handles 'sibling' roms (romm concept) - eg. different versions of the same game
                        var siblingClusters = new Dictionary<int, List<RomDTO>>();

                        // Initialize our deferred staging list - defers batchromdownmload start until after metadata sync complete. 
                        var stagedBatchDownloadItems = new List<RomQueueItem>();

                        do
                        {
                            if (platformTask.Cts.Token.IsCancellationRequested) break;

                            // get paged romDto collection from Romm API
                            RomCollectionDTO romCollection = await FetchMetadataFromRommAsync(platformTask.RommPlatformIds,
                                                                    currentServer, offset, platformTask.Cts.Token);

                            // because romm api access is pages @ 50 per time, need to increase total by the number returned:
                            platformTask.UiCard.TotalItems += romCollection.Items.Count;

                            if (isFirstFetch)
                            {
                                totalItems = romCollection.Total ?? 0;
                                isFirstFetch = false;

                                if (totalItems == 0 || romCollection.Items == null || romCollection.Items.Count == 0)
                                {
                                    break;
                                }
                            }

                            if (romCollection.Items == null || romCollection.Items.Count == 0)
                            {
                                break;
                            }

                            collectionHasProcessedAnyItems = true;

                            foreach (var romDto in romCollection.Items)
                            {
                                if (platformTask.Cts.Token.IsCancellationRequested) break;

                                int itemsProcessed = 0;

                                // determines if romDto is a single romDto, one of a sibling group or part of a multi-disc/media set
                                bool hasSiblings = romDto.SiblingRoms != null && romDto.SiblingRoms.Count > 0;
                                bool isMultiDiscLayout = romDto.HasMultipleFiles == true;

                                // --- SELECTIVE JUST-IN-TIME HYDRATION ---
                                // rommAPI.GetAllRoms4Platform return DOESN'T contain files object. 
                                // Depending on update profile, may need population 
                                bool needsRomFilesHydration = installRoms || isMultiDiscLayout;

                                var detailedRomDto = romDto;
                                if (needsRomFilesHydration && (romDto.Files == null || romDto.Files.Count == 0))
                                {
                                    var detailResult = await _rommService.GetRomDetailsAsync(currentServer, romDto.Id ?? 0, platformTask.Cts.Token);
                                    if (detailResult.IsSuccess && detailResult.Data != null)
                                    {
                                        detailedRomDto = detailResult.Data;
                                    }
                                }

                                bool hasFiles = detailedRomDto.Files != null && detailedRomDto.Files.Count > 0;
                                bool isGroupedLayout = isMultiDiscLayout || hasSiblings;

                                string basePlatformPath = NormalizeRomPath(Constants.LaunchboxRootDir, platformTask.LaunchBoxRomFolder);
                                string targetDirectory = isGroupedLayout ? Path.Combine(basePlatformPath, detailedRomDto.RommFilename) : basePlatformPath;

                                IGame targetedGame = null;

                                // =========================================================================
                                // CASE 1: MULTI-MEDIA / MULTI-DISC GAMES
                                // =========================================================================
                                if (detailedRomDto.HasMultipleFiles == true && hasFiles)
                                {
                                    // Find Disc/Side/Tape/Cart (etc) 1 or fall back to the first available file entry
                                    var primaryFile = detailedRomDto.Files.FirstOrDefault(f => !string.IsNullOrEmpty(f.FileName)
                                                        && (Helpers.TagHelper.ParseFilename(f.FileName).DiscNumber == 1 ||
                                                            Helpers.TagHelper.ParseFilename(f.FileName).IsSideA)
                                                            )
                                                      ?? detailedRomDto.Files.First();

                                    if (platformTask.UpsertGame)
                                    {
                                        var (gameResult, actionPerformed) = await _launchboxService.SyncRommDto(detailedRomDto);
                                        targetedGame = gameResult;

                                        // Determine whether the primary rom is installed on the local disk.
                                        // If so, set iGame.Applicaiton path to this. If other files are missing
                                        // this is picked up in the batchRomDownload.
                                        if (targetedGame != null)
                                        {
                                            if (installRoms && !string.IsNullOrEmpty(primaryFile.FileName))
                                            {
                                                string fullpath = Path.Combine(targetDirectory, primaryFile.FileName);

                                                if (FileSystemHelper.LocalFilePresent(platformTask.SyncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1,
                                                    fullpath, primaryFile.Sha1Hash) &&
                                                    !platformTask.SyncSettings.OverwriteExistingRoms)
                                                {
                                                    // master file present. Now check sub files.
                                                    foreach (var file in detailedRomDto.Files)
                                                    {
                                                        string subFilePath;
                                                        if (file.Category == "soundtrack")
                                                        { subFilePath = Path.Combine(Constants.LaunchboxRootDir, "Music", platformTask.PlatformName,
                                                            detailedRomDto.RommFilename, file.FileName); }
                                                        else if (file.Category == "game")
                                                        { subFilePath = Path.Combine(targetDirectory, primaryFile.FileName); }
                                                    }
                                                    targetedGame.ApplicationPath = fullpath;
                                                }
                                                else
                                                {
                                                    targetedGame.ApplicationPath = Constants.RomPlaceholder;
                                                }
                                            }

                                            string actionLabel = actionPerformed.ToString();
                                            platformTask.UiCard.AddLog($"Successfully {actionLabel}d metadata for game '{detailedRomDto.Name}'", PlatformSyncJob.LogType.Success);
                                        }
                                        else
                                        {
                                            platformTask.UiCard.WarningCount++;
                                            platformTask.UiCard.AddLog($"Could not construct or add Launchbox Game for '{detailedRomDto.Name}' from Romm. Multi-disc game.", PlatformSyncJob.LogType.Warning);
                                        }
                                    }

                                    foreach (var fileEntry in detailedRomDto.Files)
                                    {
                                        if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                        if (platformTask.UpsertGame && targetedGame != null && fileEntry.Category == "game")
                                        {
                                            // If this item is the designated primary file, point its path to the placeholder
                                            bool isPrimaryDisc = (fileEntry.Id == primaryFile.Id || fileEntry.FileName == primaryFile.FileName);

                                            _launchboxService.AddOrUpdateAdditionalApplication(
                                                targetedGame,
                                                fileEntry,
                                                targetDirectory,
                                                customAppName: Path.GetFileNameWithoutExtension(fileEntry.FileName),
                                                false
                                            //usePlaceholderPath: isPrimaryDisc
                                            );
                                        }

                                        //if (installRoms)
                                        //{
                                        //    EnqueueRomDownloadJob(platformTask, currentServer, detailedRomDto.Id ?? 0, fileEntry,
                                        //        targetDirectory, detailedRomDto.Name);
                                        //} 

                                        if (targetedGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))

                                            platformTask.UiCard.ProcessedItems++;
                                    }

                                    if (installRoms && targetedGame != null)
                                    {
                                        long totalSize = detailedRomDto.CombinedFilesSizeBytes ?? 0;
                                        string masterFile = detailedRomDto.RommFilename ?? string.Empty;

                                        EnqueueBatchRomDownloadJob(platformTask, targetedGame, detailedRomDto, new List<int> { detailedRomDto.Id ?? 0 },
                                            masterFile, totalSize, platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete);
                                    }

                                    if (targetedGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))
                                    {
                                        processedGamesLookup.Add(detailedRomDto.Id.Value, targetedGame);
                                    }

                                    if (platformTask.DownloadMediaFiles)
                                    {
                                        ScheduleMediaDownloads(detailedRomDto, platformTask, chosenProfile, currentServer, targetedGame);
                                    }
                                }
                                // =========================================================================
                                // CASE 2: SIBLING ROM REGION / VERSION GROUPS (Pass 1 Capture)
                                // =========================================================================
                                else if (hasSiblings)
                                {
                                    // Calculate the absolute lowest root identity mapping across this collection
                                    int clusterKey = Math.Min(detailedRomDto.Id ?? 0, detailedRomDto.SiblingRoms.Select(s => s.Id ?? 0).Min());

                                    if (!siblingClusters.ContainsKey(clusterKey))
                                    {
                                        siblingClusters[clusterKey] = new List<RomDTO>();
                                    }

                                    siblingClusters[clusterKey].Add(detailedRomDto);

                                    //platformTask.UiCard.ProcessedItems++;

                                    // Postpone processing until all pages are fully stored in memory!
                                    continue;
                                }


                                // =========================================================================
                                // CASE 3: STANDARD SINGLE-FILE GAMES (Games with one file only)
                                // =========================================================================
                                else
                                {
                                    if (platformTask.UpsertGame)
                                    {
                                        // Destructure the tuple into the game object and the specific action type
                                        var (gameResult, actionPerformed) = await _launchboxService.SyncRommDto(detailedRomDto);
                                        targetedGame = gameResult;

                                        if (targetedGame != null)
                                        {
                                            // Dynamically logs: "Successfully Inserted metadata..." or "Successfully Updated metadata..."
                                            string actionLabel = actionPerformed.ToString();
                                            platformTask.UiCard.AddLog($"Successfully {actionLabel}d metadata for '{detailedRomDto.Name}'", PlatformSyncJob.LogType.Success);
                                        }
                                        else
                                        {
                                            platformTask.UiCard.WarningCount++;
                                            platformTask.UiCard.AddLog($"Could not construct or add Launchbox Game for '{detailedRomDto.Name}' Romm game. Single File game.",
                                                PlatformSyncJob.LogType.Warning);
                                        }
                                    }


                                    if (hasFiles)
                                    {
                                        var singleFile = detailedRomDto.Files.First();
                                        if (!string.IsNullOrEmpty(singleFile.FileName))
                                        {
                                            if (platformTask.UpsertGame && targetedGame != null)
                                            {
                                                // CRITICAL FIX: Always keep it as a placeholder to light up the Install button 
                                                // unless we are explicitly running a profile that downloads the file right now.
                                                targetedGame.ApplicationPath = installRoms
                                                    ? Path.Combine(targetDirectory, singleFile.FileName)
                                                    : Constants.RomPlaceholder;
                                                // also good
                                            }                         
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(detailedRomDto.RommFilename))
                                    {
                                        if (platformTask.UpsertGame && targetedGame != null)
                                        {
                                            // CRITICAL FIX: Same logic for the filename fallback path
                                            targetedGame.ApplicationPath = installRoms
                                                ? Path.Combine(targetDirectory, detailedRomDto.RommFilename)
                                                : Constants.RomPlaceholder;
                                        }
                                    }

                                    if (installRoms && targetedGame != null)
                                    {
                                        long totalSize = detailedRomDto.CombinedFilesSizeBytes ?? 0;
                                        string masterFile = string.Empty;

                                        if (hasFiles)
                                        {
                                            masterFile = detailedRomDto.Files.First().FileName ?? string.Empty;
                                        }
                                        else if (!string.IsNullOrEmpty(detailedRomDto.RommFilename))
                                        {
                                            masterFile = detailedRomDto.RommFilename;
                                        }

                                        EnqueueBatchRomDownloadJob(platformTask, targetedGame, detailedRomDto, new List<int> { detailedRomDto.Id ?? 0 },
                                            masterFile, totalSize, platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete);
                                    }

                                    if (targetedGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))
                                    {
                                        processedGamesLookup.Add(detailedRomDto.Id.Value, targetedGame);
                                    }

                                    if (platformTask.DownloadMediaFiles)
                                    {
                                        ScheduleMediaDownloads(detailedRomDto, platformTask, chosenProfile, currentServer, targetedGame);
                                    }

                                    platformTask.UiCard.ProcessedItems++;
                                }


                            }

                            offset += safePageLimit;

                        } while (offset < totalItems && !platformTask.Cts.Token.IsCancellationRequested);

                        if (!collectionHasProcessedAnyItems && !platformTask.Cts.Token.IsCancellationRequested)
                        {
                            jobStopwatch.Stop();
                            platformTask.UiCard.ErrorCount++;
                            platformTask.UiCard.AddLog($"Sync job dropped: No remote dataset found. Time taken: {FormatElapsedTime(jobStopwatch.Elapsed)}", PlatformSyncJob.LogType.Error);
                            _activeTokens.TryRemove(platformTask.Id, out _);
                            continue;
                        }


                        // =========================================================================
                        // LATE-BIND RESOLUTION FOR SIBLING SET ROMS (Pass 2 Processing)
                        // =========================================================================

                        foreach (var cluster in siblingClusters.Values)
                        {
                            if (platformTask.Cts.Token.IsCancellationRequested) break;

                            // 1. Identify the Master Title using explicit flags or an arbitrary ID fallback
                            var masterRom = cluster.FirstOrDefault(r => r.RomUserData?.IsMainSibling == true)
                                            ?? cluster.OrderBy(r => r.Id).First();

                            // Sort the list: masterRom first, followed by other items
                            var sortedCluster = cluster.Where(r => r != masterRom).Prepend(masterRom).ToList();

                            // 2. Isolate variants from the group
                            var variantRoms = sortedCluster.Where(r => r.Id != masterRom.Id).ToList();

                            // 3. Compile a comprehensive context tracking list of all server IDs within this group
                            var allGroupIds = sortedCluster.Select(r => r.Id ?? 0).Distinct().ToList();
                            string aggregatedRomIdsCsv = string.Join(",", allGroupIds);

                            //string basePlatformPath = Path.Combine("C:\\LaunchBox\\Games", platformTask.PlatformName);
                            //string targetDirectory = Path.Combine(basePlatformPath, masterRom.Name);

                            string basePlatformPath = NormalizeRomPath(Constants.LaunchboxRootDir, platformTask.LaunchBoxRomFolder);
                            //string targetDirectory = Path.Combine(basePlatformPath, masterRom.Name);
                            string targetDirectory = basePlatformPath;


                            IGame masterGameInstance = null;

                            // 4. Sync the Master entry to LaunchBox
                            if (platformTask.UpsertGame)
                            {
                                var (gameResult, actionPerformed) = await _launchboxService.SyncRommDto(masterRom, aggregatedRomIdsCsv);
                                masterGameInstance = gameResult;

                                if (masterGameInstance != null)
                                {
                                    string actionLabel = actionPerformed.ToString();
                                    platformTask.UiCard.AddLog($"Successfully {actionLabel}d multi-version group master metadata for '{masterRom.Name}'", PlatformSyncJob.LogType.Success);
                                }
                                else
                                {
                                    platformTask.UiCard.WarningCount++;
                                    platformTask.UiCard.AddLog($"Could not construct or add Launchbox Game for '{masterRom.Name}' from Romm game. Game is part of sibling set.", PlatformSyncJob.LogType.Warning);
                                }

                                platformTask.UiCard.ProcessedItems++;
                            }

                            bool masterHasFiles = masterRom.Files != null && masterRom.Files.Count > 0;

                            if (masterHasFiles)
                            {
                                foreach (var masterFile in masterRom.Files)
                                {
                                    if (string.IsNullOrEmpty(masterFile.FileName)) continue;

                                    if (platformTask.UpsertGame && masterGameInstance != null && string.IsNullOrEmpty(masterGameInstance.ApplicationPath))
                                    {
                                        masterGameInstance.ApplicationPath = Path.Combine(targetDirectory, masterFile.FileName);
                                    }

                                    //if (installRoms)
                                    //{
                                    //    EnqueueRomDownloadJob(platformTask, currentServer, masterRom.Id ?? 0, masterFile, targetDirectory, masterRom.Name);
                                    //}                                    
                                }
                            }
                            else if (!string.IsNullOrEmpty(masterRom.RommFilename))
                            {
                                if (platformTask.UpsertGame && masterGameInstance != null && string.IsNullOrEmpty(masterGameInstance.ApplicationPath))
                                {
                                    masterGameInstance.ApplicationPath = Path.Combine(targetDirectory, masterRom.RommFilename);
                                }
                            }

                            if (masterGameInstance != null && masterRom.Id.HasValue)
                            {
                                processedGamesLookup[masterRom.Id.Value] = masterGameInstance;
                            }

                            if (platformTask.DownloadMediaFiles)
                            {
                                ScheduleMediaDownloads(masterRom, platformTask, chosenProfile, currentServer, masterGameInstance);
                            }

                            // =========================================================================
                            // NEW STEP 4.5: ALSO INJECT MASTER AS AN ADDITIONAL APPLICATION VARIANT
                            // This ensure Launchbox identifies the game as having multi-versions (badge)
                            // =========================================================================
                            if (platformTask.UpsertGame && masterGameInstance != null)
                            {
                                if (masterHasFiles)
                                {
                                    foreach (var masterFile in masterRom.Files)
                                    {
                                        if (string.IsNullOrEmpty(masterFile.FileName) || masterFile.Category != "game") continue;
                                        string masterLabel = $"{Path.GetFileNameWithoutExtension(masterFile.FileName)}";
                                        _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, masterFile,
                                            targetDirectory, masterLabel);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(masterRom.RommFilename))
                                {

                                    var masterPlaceholderFileDto = new RomFileDTO { FileName = masterRom.RommFilename };
                                    // dunno if i need to do the masterFile.Category != "game" check on this one!!??
                                    // Don't think so as a placeholder?
                                    string masterLabel = $"{Path.GetFileNameWithoutExtension(masterRom.RommFilename)}";
                                    _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, masterPlaceholderFileDto,
                                        targetDirectory, masterLabel);
                                }
                            }

                            // =========================================================================
                            // 5. Append Variant items to the freshly minted master record
                            // =========================================================================
                            foreach (var variantRom in variantRoms)
                            {
                                if (platformTask.Cts.Token.IsCancellationRequested) break;

                                var detailedVariant = variantRom;

                                // Hydrate file definitions if we are running an active download profile
                                if (installRoms && (variantRom.Files == null || variantRom.Files.Count == 0))
                                {
                                    var detailResult = await _rommService.GetRomDetailsAsync(currentServer, variantRom.Id ?? 0, platformTask.Cts.Token);
                                    if (detailResult.IsSuccess && detailResult.Data != null)
                                    {
                                        detailedVariant = detailResult.Data;
                                    }
                                }

                                bool variantHasFiles = detailedVariant.Files != null && detailedVariant.Files.Count > 0;

                                if (masterGameInstance != null)
                                {
                                    if (variantHasFiles)
                                    {
                                        foreach (var fileEntry in detailedVariant.Files)
                                        {
                                            if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                            if (platformTask.UpsertGame && fileEntry.Category == "game")
                                            {
                                                string variantLabel = $"{Path.GetFileNameWithoutExtension(fileEntry.FileName)}";
                                                _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, fileEntry,
                                                    targetDirectory, variantLabel);
                                            }

                                            //if (installRoms)
                                            //{
                                            //    EnqueueRomDownloadJob(platformTask, currentServer, detailedVariant.Id ?? 0, fileEntry, targetDirectory, detailedVariant.Name);
                                            //}
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(detailedVariant.RommFilename))
                                    {
                                        if (platformTask.UpsertGame)
                                        {
                                            var placeholderFileDto = new RomFileDTO { FileName = detailedVariant.RommFilename };
                                            // dunno if i need to do the masterFile.Category != "game" check on this one!!??
                                            // Don't think so as a placeholder?
                                            string variantLabel = $"{Path.GetFileNameWithoutExtension(detailedVariant.RommFilename)}";
                                            _launchboxService.AddOrUpdateAdditionalApplication(masterGameInstance, placeholderFileDto,
                                                targetDirectory, variantLabel);
                                        }
                                    }
                                }
                            }

                            // Inside Pass 2, immediately after the variantRoms loop finishes for the current cluster:
                            if (installRoms && masterGameInstance != null)
                            {
                                // Aggregate the file sizes across the master AND all variants using the API's combined field
                                long totalGroupSize = sortedCluster.Sum(r => r.CombinedFilesSizeBytes ?? 0);

                                string masterFile = string.Empty;
                                if (masterHasFiles)
                                {
                                    masterFile = masterRom.Files.First().FileName ?? string.Empty;
                                }
                                else if (!string.IsNullOrEmpty(masterRom.RommFilename))
                                {
                                    masterFile = masterRom.RommFilename;
                                }

                                EnqueueBatchRomDownloadJob(platformTask, masterGameInstance, masterRom, allGroupIds, masterFile, totalGroupSize,
                                    platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete);
                            }

                            // platformTask.UiCard.ProcessedItems += cluster.Count;
                        }

                        // Enforce download queue tracking restrictions
                        while (_activeFileCounters.TryGetValue(platformTask.Id, out int fileCount) && fileCount > 0)
                        {
                            if (platformTask.Cts.Token.IsCancellationRequested) break;
                            await Task.Delay(100);
                        }

                        PluginHelper.DataManager.Save();

                        PluginHelper.LaunchBoxMainViewModel?.RefreshData();




                       // await LaunchboxViewsHelper.SoftRefreshUi();

                        Thread.Sleep(1000);

                        // Now populate the batchRomDownload list - background daemon will pick these up
                        if (stagedBatchDownloadItems.Count > 0 && !platformTask.Cts.Token.IsCancellationRequested)
                        {
                            foreach (var stagedItem in stagedBatchDownloadItems)
                            {
                                // Double check it wasn't added by another parallel process
                                if (!_settingsService.Settings.RomDownloadQueue.Any(q => q != null && q.LaunchboxId == stagedItem.LaunchboxId))
                                {
                                    _settingsService.Settings.RomDownloadQueue.Add(stagedItem);
                                }
                            }

                            // Atomically save the queue once
                            _settingsService.Save();
                        }

                        // Update any LB UIs
                        if (PluginHelper.LaunchBoxMainViewModel != null)
                        {
                            await Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                            { _ = LaunchboxViewsHelper.SoftRefreshUi(); }));
                        }

                        // Stopped the stopwatch right before evaluating the final status strings:
                        jobStopwatch.Stop();
                        string totalDuration = FormatElapsedTime(jobStopwatch.Elapsed);

                        //throw new Exception("Test Exception");

                        if (platformTask.Cts.Token.IsCancellationRequested)
                        {
                            platformTask.UiCard.Status = SyncStatus.Cancelled;
                            platformTask.UiCard.AddLog($"Sync job cancelled by user after {totalDuration}", PlatformSyncJob.LogType.Warning);
                        }

                        platformTask.UiCard.AddLog($"Platform Sync completed successfully in {totalDuration}", PlatformSyncJob.LogType.Process);


                        //HACK: Could I buggery get the metadata processed and files processed to align perfectly. 
                        // Gonna take a lot of spelunking to get this precise + not sure how important precision is anyway here.
                        // Thus, this'll do for now
                        platformTask.UiCard.ProcessedItems = platformTask.UiCard.TotalItems;

                    }
                    catch (Exception ex)
                    {
                        // Capture partial runtime up to the point of structural failure:
                        jobStopwatch.Stop();
                        string partialDuration = FormatElapsedTime(jobStopwatch.Elapsed);

                        platformTask.UiCard.ErrorCount++;

                        // Injected time elapsed before crash:
                        platformTask.UiCard.AddLog($"[SyncManager] Fatal error executing platform run after {partialDuration}: {ex.Message}", PlatformSyncJob.LogType.Error);
                    }
                    finally
                    {
                        if (platformTask.UiCard.WarningCount > 0 && platformTask.UiCard.ErrorCount > 0)
                            platformTask.UiCard.Status = SyncStatus.CompletedWithWarningsAndErrors;
                        else if (platformTask.UiCard.WarningCount > 0)
                            platformTask.UiCard.Status = SyncStatus.CompletedWithWarnings;
                        else if (platformTask.UiCard.ErrorCount > 0)
                            platformTask.UiCard.Status = SyncStatus.CompletedWithErrors;
                        else
                            platformTask.UiCard.Status = SyncStatus.Completed;

                        OnSyncCompletedNotification?.Invoke(platformTask.UiCard);

                        // FIX: Ensure cleaning dictionaries always fires to prevent data leaks across sync retry bounds
                        _activeFileCounters.TryRemove(platformTask.Id, out _);
                        _activeTokens.TryRemove(platformTask.Id, out _);
                    }

                    if (platformTask.NotifyLauncboxWhenMetadataComplete)
                    {
                        StringBuilder sb = new StringBuilder($"Romm Metadata/Media Sync complete for [{platformTask.PlatformName}].");
                        if (platformTask.DownloadRomFiles) sb.Append($" Downloading rom files started in the background. You can quit Launchbox at any time. " +
                            $"Downloads will be resumed on restart.");
                        _notificationService.SendInfoNotification(sb.ToString(), 2);
                    }
                }
            }
        }


        private async Task<string> StreamFileFromNetworkAsync(string absoluteUrl, string targetPath, RommServer server,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(absoluteUrl)) return "File URL empty";

            try
            {
                //if (Random.Shared.Next(1, 11) == 1)
                //{
                //  throw new Exception("Test StreamFileFromNetworkAsync exception.");
                //}

                // 1. Use the absolute URL directly since MediaDownloadManager handles the full pathing
                using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl);

                // 2. Attach your RomM API Bearer Token for authorization
                if (!string.IsNullOrEmpty(server.ApiToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.ApiToken);
                }

                // 3. Request the stream headers first to handle raw binary data efficiently
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Explicitly check for outcome before touching the disk to prevent creating bad 1KB stubs
                if (!response.IsSuccessStatusCode)
                {
                    return $"Http response: {response.StatusCode} ({response.ReasonPhrase}). URL: " +
                        $"{absoluteUrl.Replace(server.BaseUrl, "[Base Server]")}";
                }

                // 4. Safely create target subdirectories if they don't exist yet
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 5. Open the network content stream and pipe it sequentially to your disk
                using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(targetStream, cancellationToken);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncManager] Download Error: {ex.Message} for target {targetPath}");

                // 6. Cleanup Safeguard: If the download cuts out halfway through, delete the broken/partial file
                if (File.Exists(targetPath))
                {
                    try { File.Delete(targetPath); } catch { /* Ignore secondary cleanup errors */ }
                }

                return $"Error: {ex.Message} for target {absoluteUrl.Replace(server.BaseUrl, "[Base Server]")}";
            }
        }

        private object SyncWithLaunchBoxDatabaseIfSet(RomDTO rom, string platformName)
        {
            // Check if defaul or platform specific profile indicates launchbox database sync disabled




            // TODO: Create custom fields:
            // RomId - local romDto id of the specific romDto - for ondemand romDto instals after syncing (ie. via install)
            // ServerId - local server id of the specific romDto - for ondemand romDto instals after syncing (ie. via install)


            // Core injection wrapper matching LaunchBox plugin SDK rules
            return new object();
        }
    }
}