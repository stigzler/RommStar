using Microsoft.Xaml.Behaviors;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Extensions;
using RommStar.Core.Helpers;
using RommStar.Core.Models;
using RommStar.Core.Services;
using SQLitePCL;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Xml;
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
        /// Tracks remaining active masterSiblingRomDtoFile counts per platform to enforce strict sequential progression
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
        /// Macro and masterSiblingRomDtoFile tasks resolve their specific target servers natively via contextual properties.
        /// </summary>
        public RommServer ActiveServer { get; set; }

        /// <summary>
        /// Linked with the UI Job monitoring cards
        /// </summary>
        public ObservableCollection<PlatformSyncCardVM> ActiveSyncJobs { get; } = new();

        LoggingService _loggingService;
        public SyncManager(RommServer initialServer, RommService rommService, LaunchboxDataService launchboxService, SettingsService settingsService,
            NotificationService notificationService, LoggingService loggingService)
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
            _loggingService = loggingService;
        }

        public event Action<PlatformSyncCardVM>? OnSyncCompletedNotification;

        public void CancelPlatformSync(Guid jobId)
        {
            _loggingService.Log($"Cancel Platform Sync request received for Job Id: [{jobId}]");
            var card = ActiveSyncJobs.FirstOrDefault(j => j.Id == jobId);
            if (card != null && (card.Status == SyncStatus.Queued || card.Status == SyncStatus.SyncingFiles || card.Status == SyncStatus.ProcessingMetadata))
            {
                _loggingService.Log("Job found", Primitives.LoggingLevel.Verbose);

                // 1. Immediately visually update the UI state
                card.Status = SyncStatus.Cancelled;

                // 2. Extract the internal task token and trip it
                if (_activeTokens.TryRemove(jobId, out var cts))
                {
                    try
                    {
                        cts.Cancel();
                        _loggingService.Log("Job cancellation token set successfully");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        _loggingService.Log($"Error whislt trying to set the cancellaiton token: {ex.Message}");
                    }
                }
            }
            else
            {
                _loggingService.Log($"Could not complete: Either Job no longer exists, or its status is not Queued, SyncingFiles or ProcessingMetadata");
            }
        }

        internal bool PlatformQueuedAndIncomplete(string platformName)
        {
            return ActiveSyncJobs.Any(j => j.LaunchBoxPlatformName == platformName && j.Status < SyncStatus.Completed);
        }

        // =========================================================================
        // MACRO MANAGEMENT: ENQUEUE PLATFORM SYNC
        // =========================================================================
        public async Task EnqueuePlatformSync(string lbPlatformName, string lbPlatformRomFolder, IPlatformFolder[] lbMediaFolders, string emulatorId,
            List<int> rommPlatformIds, ExtendedSyncSettings syncSettings, RommServer targetServer, int? romCount, bool notifyLaunchboxOnMeatadataDone = false)
        {

            if (_settingsService.Settings.LoggingLevel > Primitives.LoggingLevel.Normal)
            {
                _loggingService.Log($"Enqueing Platform Sync for: [{lbPlatformName}]");
                _loggingService.Log($"Passed parameters:");
                _loggingService.Log($"Launchbox Rom Folder: [{lbPlatformRomFolder}]");
                _loggingService.Log($"Emulator ID: [{emulatorId}]");
                _loggingService.Log($"Romm Platform Ids: {String.Join(", ", rommPlatformIds)}");
                _loggingService.Log($"Target Romm Server: {targetServer.ServerName}");
                _loggingService.Log($"Rom Count: {romCount}");
                _loggingService.Log($"Sync Settings being used: {syncSettings.ToCsv()}");
            }

            var uiJobCard = new PlatformSyncCardVM
            {
                Id = Guid.NewGuid(),
                LaunchBoxPlatformName = lbPlatformName,
                ServerName = targetServer.ServerName,
                Status = SyncStatus.Queued,
                RomCount = romCount != null? (int)romCount: 0,
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

                // DownloadRom is part of syncProfile
                DownloadRomFiles = syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom
                        || syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia
                        || syncSettings.SyncProfile == SyncProfileTypes.DownloadRom,

                // UpdateMetadata is part of syncProfile
                UpdateMetadata = (syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia
                        || syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom
                        || syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata
                        || syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadMedia),

                // DownloadMedia is part of syncProfile
                DownloadMediaFiles = syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadMedia
                                        || syncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia

            };

            // REGISTER THE TASK CTS SO IT CAN BE RECOVERED BY THE CANCEL BUTTON CLICK
            _activeTokens[uiJobCard.Id] = task.Cts;

            _platformQueue.Writer.TryWrite(task);
            _loggingService.Log($"Platform Sync successfully enqueued: [{lbPlatformName}]");
        }

        private void EnqueueBatchRomDownloadJob(PlatformSyncTask platformTask, IGame game, RomDTO romDto, List<int> allRommIds, string masterFilename,
                    long totalSizeBytes, string serverId, List<RomQueueItem> stagedQueue, bool notifyLaunchbox = false,
                    List<RomFileDTO>? aggregatedFiles = null)
        {
            _loggingService.Log($"Request made to enqueue rom for batch download: {romDto.Name}",Primitives.LoggingLevel.Verbose);
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
                    platformTask.UiCard.AddLog($"Re-queued quarantined item '{game.Title}' for batch download.", PlatformSyncCardVM.LogType.Info);
                }
                _loggingService.Log($"Item already in queue.");
                return; // Return immediately to avoid duplicates
            }

            // 3. Check if we have already staged this item during THIS sync run
            if (stagedQueue.Any(q => q != null && q.LaunchboxId == game.Id))
            {
                _loggingService.Log($"Item already in staged queue. Not re-enqueuing here.");
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

            if (aggregatedFiles != null && aggregatedFiles.Count > 0)
            {
                queueItem.IsSiblingSet = true;
                queueItem.MultiFiles = aggregatedFiles; // Pack EVERYTHING into the manifest
            }
            else if (queueItem.IsMultiFileGame)
            {
                queueItem.MultiFiles = romDto.Files;
            }
            else if (romDto.SiblingRoms != null && romDto.SiblingRoms.Count > 0)
            {
                queueItem.IsSiblingSet = true;
                queueItem.MultiFiles = romDto.Files;
            }

            // ADD TO STAGED QUEUE ONLY - NO SETTINGS SAVE HERE
            stagedQueue.Add(queueItem);

            _loggingService.Log($"New RomQueueItem added to queue: {queueItem.ToCsv()}");

            // Optional: Log it for UI transparency
            platformTask.UiCard.AddLog($"Queued [{game.Title}] rom file/s for download.", PlatformSyncCardVM.LogType.Info);
        }


        // =========================================================================
        // MICRO-LEVEL FILE PIPELINE HANDLERS
        // =========================================================================
        private void EnqueueFileDownload(DownloadJob job)
        {
            //_loggingService.Log($"Request made to enqueue file for download: {job.ToCsv()}", Primitives.LoggingLevel.Verbose);

            _activeFileCounters.AddOrUpdate(job.JobId, 1, (key, current) => current + 1);
            if (job.UiCard != null) job.UiCard.TotalItems++;

            _fileDownloadQueue.Writer.TryWrite(job);
            _loggingService.Log($"File Download Job added: {job.ToCsv(_settingsService.Settings.LoggingRedact)}", Primitives.LoggingLevel.Verbose);
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
            _loggingService.Log($"Getting rom lists for RomM platforms: [{String.Join(", ", platformIds)}] from server: {server.ServerName}");
            var apiResult = await _rommService.GetRomCollectionAsync(server, platformIds, offset, cancellationToken);

            if (!apiResult.IsSuccess)
            {
                _loggingService.Log($"ERROR getting rom lists for RomM platforms. Reason: [{apiResult.FailureReason}]. Http response: [{apiResult.HttpResponse}]. " +
                    $"{apiResult.ExceptionMessage}");
            }

            if (apiResult.Data != null)
            {
                _loggingService.Log($"Platform roms data successfully retrieved.");
                return apiResult.Data;
            }

            _loggingService.Log($"WARNING: No data recived for these platforms.");

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

        //    // Enqueue or execute the specific ROM masterSiblingRomDtoFile streaming tasks here...

        //    // 2. Process On-Demand Media Downloads if requested
        //    bool downloadMedia = syncTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadMedia
        //                         || syncTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia;

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
                    continue; // Skip queuing entirely if masterSiblingRomDtoFile exists and overwrite is turned off
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
                                $" '{job.RomName}'. {outcome}", PlatformSyncCardVM.LogType.Error);

                        }
                        else
                        {
                            job.UiCard.AddLog($"{typeLabel} not present or download failed for" +
                            $" '{job.RomName}'. {outcome}", PlatformSyncCardVM.LogType.Info);

                        }

                    }
                    else if (outcome == String.Empty)
                    {
                        job.OnSuccessCallback?.Invoke();

                        // FIXED: Direct null check on job.MediaType here as well
                        string typeLabel = job.JobType == DownloadJobType.Rom ? "ROM"
                                           : (job.MediaType != null ? job.MediaType.ToString() : "Media");

                        // update relevant igame media paths (these have to be explicitly designed - LB doesn't auto calculate (of course!!))
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
                        job.UiCard.AddLog($"Downloaded {typeLabel} for [{job.RomName}] ({Path.GetFileName(job.DestinationPath)}) ", PlatformSyncCardVM.LogType.Success);
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

        /// <summary>
        /// Updates relevant install properties for IGame
        /// </summary>
        /// <param name="game"></param>
        /// <param name="installed"></param>
        /// <param name="path">Applicaiton Path to use (auto-populates Constants.RomPlaceholder on installed == false)</param>
        private void UpdateIGameInstallStatus(IGame game, bool installed, string path)
        {
            game.Installed = installed;
            if (installed)
            {
                game.ApplicationPath = path;
                game.Status = "Installed";
            }
            else
            {
                game.ApplicationPath = Constants.RomPlaceholder;
                game.Status = "Not Installed";
            }
        }

        // =========================================================================
        // MACRO SEQUENTIAL PIPELINE PROCESSOR (Paging Stream Integration)
        // =========================================================================

        /// <summary>
        /// It aint pretty, but good enough for the girls I date.
        /// </summary>
        /// <returns></returns>
        private async Task StartPlatformQueueProcessorAsync()
        {
            while (await _platformQueue.Reader.WaitToReadAsync())
            {
                // -------------------------------------------------------------------------
                // MAIN JOB LOOP START - This is for a Single Platform
                // -------------------------------------------------------------------------

                while (_platformQueue.Reader.TryRead(out var platformTask))
                {
                    if (platformTask.UiCard.Status == SyncStatus.Cancelled)
                    {
                        _activeTokens.TryRemove(platformTask.Id, out _);
                        continue;
                    }

                    var jobStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    platformTask.UiCard.AddLog($"Sync job started for {platformTask.PlatformName}...", PlatformSyncCardVM.LogType.Process);

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

                        // determine whether job asks for Rom files installation
                        var installRoms = platformTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom
                            || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia
                            || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.DownloadRom;


                        //var installMedia = platformTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadMedia
                        //    || platformTask.SyncSettings.SyncProfile == SyncProfileTypes.UpdateMetadata_DownloadRom_DownloadMedia;

                        // Setup flag for use later in process
                        bool useSha1InFileChecks = platformTask.SyncSettings.FileCheckMethod == Primitives.FileCheckMethod.FileAndSHA1;

                        // Kept in JIC want to reinstate original plan - Originally had a choice of media to install on metadata sync
                        // and what to install on rom files install. Now just left it as InstallMediaProfile
                        //var chosenProfile = installRoms
                        //    ? _settingsService.Settings.InstallMediaProfile
                        //    : _settingsService.Settings.SyncMediaProfile;
                        var chosenProfile = _settingsService.Settings.SyncMediaProfile;

                        // IGame creation complicated - essentially a two-pass process due to masterRomDtoSiblingDto roms system in romm
                        // This used in tracking which have already been added
                        var processedGamesLookup = new Dictionary<int, IGame>();

                        // This handles 'masterRomDtoSiblingDto' roms (romm concept) - eg. different versions of the same game
                        var siblingClusters = new Dictionary<int, List<RomDTO>>();

                        // Initialize our deferred staging list - defers any batchromdownmload start until after metadata sync complete. 
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

                            // ********************************************************************
                            // ROM ITERATION: Iterate through paged list of roms (romDTO) from RomM server
                            // ********************************************************************
                            foreach (var romDto in romCollection.Items)
                            {
                                if (platformTask.Cts.Token.IsCancellationRequested) break;

                                // determines if romDto is a single romDto, one of a masterRomDtoSiblingDto group or part of a multi-disc/media set
                                bool hasSiblings = romDto.SiblingRoms != null && romDto.SiblingRoms.Count > 0;
                                bool isMultiDiscLayout = romDto.HasMultipleFiles == true;

                                // --- SELECTIVE JUST-IN-TIME HYDRATION ---
                                // rommAPI.GetAllRoms4Platform return DOESN'T contain files object. 
                                // Depending on update profile, may need population 
                                bool needsRomFilesHydration = installRoms || isMultiDiscLayout || hasSiblings;

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

                                IGame targetedIGame = null;

                                // =========================================================================
                                // CASE 1: MULTI-MEDIA / MULTI-DISC GAMES
                                // =========================================================================
                                if (detailedRomDto.HasMultipleFiles == true && hasFiles)
                                {
                                    // Find Disc/Side/Tape/Cart (etc) 1 or fall back to the first available masterSiblingRomDtoFile entry
                                    var primaryFile = detailedRomDto.Files.FirstOrDefault(f => !string.IsNullOrEmpty(f.FileName)
                                                        && (Helpers.TagHelper.ParseFilename(f.FileName).DiscNumber == 1 ||
                                                            Helpers.TagHelper.ParseFilename(f.FileName).IsSideA)
                                                            )
                                                      ?? detailedRomDto.Files.First();

                                    // Metadata insert/Update set in SyncProfile
                                    if (platformTask.UpdateMetadata)
                                    {
                                        // Run iGame upsert routine. iGame returned and what action occurred (insert/update)
                                        var (gameResult, actionPerformed) = await _launchboxService.SyncRommDto(detailedRomDto);
                                        targetedIGame = gameResult;

                                        // Determine whether the primary rom is installed on the local disk.
                                        // If so, set iGame.Applicaiton path to this. If other files are missing
                                        // this is picked up in the batchRomDownload.
                                        // Todo: really need to refactor this to a method to cover this and Single/masterRomDtoSiblingDto romm entries below
                                        if (targetedIGame != null)
                                        {
                                            bool allRomFilesOnLocalDisk = false;

                                            if (!string.IsNullOrEmpty(primaryFile.FileName))
                                            {
                                                string fullpath = Path.Combine(targetDirectory, primaryFile.FileName);

                                                allRomFilesOnLocalDisk = FileSystemHelper.LocalFilePresent(useSha1InFileChecks,
                                                    fullpath, primaryFile.Sha1Hash);
                                                {
                                                    // Master masterSiblingRomDtoFile present. Now check sub files.
                                                    foreach (var file in detailedRomDto.Files)
                                                    {
                                                        string subFilePath = null;
                                                        if (file.Category == "soundtrack")
                                                        {
                                                            subFilePath = Path.Combine(Constants.LaunchboxRootDir, "Music", platformTask.PlatformName,
                                                            detailedRomDto.RommFilename, file.FileName);
                                                        }
                                                        else if (file.Category == "game")
                                                        { subFilePath = Path.Combine(targetDirectory, primaryFile.FileName); }

                                                        // Sadly, Romm doens' poulate the sha1 values for Files. Also, GetFile endpoint
                                                        // not working at time of writing. Thus, can only check on filename.
                                                        if (!FileSystemHelper.LocalFilePresent(false,
                                                            subFilePath, null))
                                                        {
                                                            allRomFilesOnLocalDisk = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                UpdateIGameInstallStatus(targetedIGame, allRomFilesOnLocalDisk, fullpath);
                                            }

                                            string actionLabel = actionPerformed.ToString();
                                            platformTask.UiCard.AddLog($"{actionLabel}d metadata for [{detailedRomDto.RommFilename}] <Multi File Game>. " +
                                                $"Already Installed: [{allRomFilesOnLocalDisk}]. Sub-Files: [{detailedRomDto.Files.Count()}]. Siblings: [{detailedRomDto.SiblingRoms?.Count()}]", PlatformSyncCardVM.LogType.Success);
                                        }
                                        else
                                        {
                                            platformTask.UiCard.WarningCount++;
                                            platformTask.UiCard.AddLog($"Could not construct or add Launchbox Game for '{detailedRomDto.Name}' from Romm. Multi-disc game.", PlatformSyncCardVM.LogType.Warning);
                                        }

                                    }

                                    foreach (var fileEntry in detailedRomDto.Files)
                                    {
                                        if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                        if (platformTask.UpdateMetadata && targetedIGame != null && fileEntry.Category == "game")
                                        {
                                            // If this item is the designated primary masterSiblingRomDtoFile, point its path to the placeholder
                                            bool isPrimaryDisc = (fileEntry.Id == primaryFile.Id || fileEntry.FileName == primaryFile.FileName);

                                            _launchboxService.AddOrUpdateAdditionalApplication(
                                                targetedIGame,
                                                fileEntry,
                                                targetDirectory,
                                                customAppName: Path.GetFileNameWithoutExtension(fileEntry.FileName),
                                                false
                                            //usePlaceholderPath: isPrimaryDisc
                                            );
                                        }

                                        //if (targetedIGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))
                                    }

                                    if (installRoms && targetedIGame != null)
                                    {
                                        long totalSize = detailedRomDto.CombinedFilesSizeBytes ?? 0;
                                        string masterFile = detailedRomDto.RommFilename ?? string.Empty;

                                        EnqueueBatchRomDownloadJob(platformTask, targetedIGame, detailedRomDto, new List<int> { detailedRomDto.Id ?? 0 },
                                            masterFile, totalSize, platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete);
                                    }

                                    if (targetedIGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))
                                    {
                                        processedGamesLookup.Add(detailedRomDto.Id.Value, targetedIGame);
                                    }

                                    if (platformTask.DownloadMediaFiles)
                                    {
                                        ScheduleMediaDownloads(detailedRomDto, platformTask, chosenProfile, currentServer, targetedIGame);
                                    }

                                    platformTask.UiCard.ProcessedItems++;

                                } // END Multi-File Processor



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
                                // CASE 3: STANDARD SINGLE-FILE GAMES (Games with one masterSiblingRomDtoFile only)
                                // =========================================================================
                                else
                                {
                                    if (platformTask.UpdateMetadata)
                                    {
                                        // De structure the tuple into the game object and the specific action type
                                        var (gameResult, actionPerformed) = await _launchboxService.SyncRommDto(detailedRomDto);
                                        targetedIGame = gameResult;

                                        if (targetedIGame != null)
                                        {
                                            string fullpath = Path.Combine(targetDirectory, romDto.RommFilename);

                                            bool allRomFilesOnLocalDisk = FileSystemHelper.LocalFilePresent(useSha1InFileChecks,
                                                    fullpath, romDto.Sha1Hash);

                                            UpdateIGameInstallStatus(targetedIGame, allRomFilesOnLocalDisk, fullpath);

                                            // Dynamically logs: "Successfully Inserted metadata..." or "Successfully Updated metadata..."
                                            string actionLabel = actionPerformed.ToString();
                                            platformTask.UiCard.AddLog($"{actionLabel}d metadata for [{detailedRomDto.Name}] <Single File Game>. " +
                                                $"Already Installed: [{allRomFilesOnLocalDisk}]", PlatformSyncCardVM.LogType.Success);
                                        }
                                        else
                                        {
                                            platformTask.UiCard.WarningCount++;
                                            platformTask.UiCard.AddLog($"Could not {actionPerformed.ToString()} metadata for '{detailedRomDto.Name}' Romm game. ",
                                                PlatformSyncCardVM.LogType.Warning);
                                        }
                                    }

                                    if (hasFiles)
                                    {
                                        var singleFile = detailedRomDto.Files.First();
                                        if (!string.IsNullOrEmpty(singleFile.FileName))
                                        {
                                            if (platformTask.UpdateMetadata && targetedIGame != null)
                                            {
                                                // CRITICAL FIX: Always keep it as a placeholder to light up the Install button 
                                                // unless we are explicitly running a profile that downloads the masterSiblingRomDtoFile right now.
                                                targetedIGame.ApplicationPath = installRoms
                                                    ? Path.Combine(targetDirectory, singleFile.FileName)
                                                    : Constants.RomPlaceholder;
                                                // also good
                                            }
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(detailedRomDto.RommFilename))
                                    {
                                        if (platformTask.UpdateMetadata && targetedIGame != null)
                                        {
                                            // CRITICAL FIX: Same logic for the filename fallback path
                                            targetedIGame.ApplicationPath = installRoms
                                                ? Path.Combine(targetDirectory, detailedRomDto.RommFilename)
                                                : Constants.RomPlaceholder;
                                        }
                                    }

                                    if (installRoms && targetedIGame != null)
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

                                        EnqueueBatchRomDownloadJob(platformTask, targetedIGame, detailedRomDto, new List<int> { detailedRomDto.Id ?? 0 },
                                            masterFile, totalSize, platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete);
                                    }

                                    if (targetedIGame != null && detailedRomDto.Id.HasValue && !processedGamesLookup.ContainsKey(detailedRomDto.Id.Value))
                                    {
                                        processedGamesLookup.Add(detailedRomDto.Id.Value, targetedIGame);
                                    }

                                    if (platformTask.DownloadMediaFiles)
                                    {
                                        ScheduleMediaDownloads(detailedRomDto, platformTask, chosenProfile, currentServer, targetedIGame);
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
                            platformTask.UiCard.AddLog($"Sync job dropped: No remote dataset found. Time taken: {FormatElapsedTime(jobStopwatch.Elapsed)}", PlatformSyncCardVM.LogType.Error);
                            _activeTokens.TryRemove(platformTask.Id, out _);
                            continue;
                        }


                        // =========================================================================
                        // LATE-BIND RESOLUTION FOR SIBLING SET ROMS (Pass 2 Processing) 
                        // =========================================================================
                        // blurgh - mindf**k

                        foreach (List<RomDTO> cluster in siblingClusters.Values)
                        {
                            if (platformTask.Cts.Token.IsCancellationRequested) break;

                            // 1. Identify the Master Title using explicit flags or an arbitrary ID fallback
                            var masterRomDto = cluster.FirstOrDefault(r => r.RomUserData?.IsMainSibling == true)
                                            ?? cluster.OrderBy(r => r.Id).First();

                            // Sort the list: masterRomDto first, followed by other items
                            var sortedRomDtosList = cluster.Where(r => r != masterRomDto).Prepend(masterRomDto).ToList();

                            // 2. Isolate variants from the group
                            var variantRomDtoList = sortedRomDtosList.Where(r => r.Id != masterRomDto.Id).ToList();

                            // 3. Compile a comprehensive context tracking list of all server IDs within this group
                            var allRomDtoIds = sortedRomDtosList.Select(r => r.Id ?? 0).Distinct().ToList();
                            string aggregatedRomDtoIdsCsv = string.Join(",", allRomDtoIds);

                            string basePlatformPath = NormalizeRomPath(Constants.LaunchboxRootDir, platformTask.LaunchBoxRomFolder);
                            string targetDirectory = basePlatformPath;

                            IGame masterIGameInstance = null;

                            // Initialize the aggregator with the Master's files
                            List<RomFileDTO> allClusterFiles = new List<RomFileDTO>();
                            if (masterRomDto.Files != null) allClusterFiles.AddRange(masterRomDto.Files);

                            // 4. Sync the Master entry to LaunchBox
                            if (platformTask.UpdateMetadata)
                            {
                                var (syncRomDtoResult, actionPerformed) = await _launchboxService.SyncRommDto(masterRomDto, aggregatedRomDtoIdsCsv);
                                masterIGameInstance = syncRomDtoResult;

                                // setup flag
                                bool allRomFilesOnLocalDisk = false;

                                if (masterIGameInstance != null)
                                {
                                    // hydrate the records to ensure RomDTO.Files is populated
                                    var hydratedRomDtoResult = await _rommService.GetRomDetailsAsync(currentServer, masterRomDto.Id ?? 0, platformTask.Cts.Token);
                                    if (hydratedRomDtoResult.IsSuccess && hydratedRomDtoResult.Data != null)
                                    {
                                        masterRomDto = hydratedRomDtoResult.Data;
                                    }
                                    else
                                    {
                                        // safety fallback: not sure will happen, but mark game as uninstalled if does
                                        allRomFilesOnLocalDisk = false;
                                        break;
                                    }

                                    // DESIGN DECISION:
                                    // RomM's database design is somewhat 'idiosyncratic' (🤐) - no Game data object, so 
                                    // files are a mishmash of siblings/multi-file etc under the ubiquitous rom data object. To top that, it doesn't even 
                                    // populate the Files.Sha1 and couldn't find a way to retrieve the RomFile object. 
                                    // {{baseUrl}}/api/roms/:id/files was giving me an internal server error. 
                                    // Lost the will to live with this. 
                                    // STRATEGY: Verify what files can via SHA1, but when not available - fall back to by name only

                                    RomFileDTO masterRomFileDto = masterRomDto.Files.Where(f => f.Sha1Hash == masterRomDto.Sha1Hash).FirstOrDefault();

                                    // todo - need to re-route to log or ui
                                    if (masterRomFileDto == null) Debug.WriteLine($"Could not identify the primary masterSiblingRomDtoFile for this Sibling Rom: {masterRomDto.RommFilename}");

                                    if (!string.IsNullOrEmpty(masterRomFileDto.FileName))
                                    {
                                        string masterRomFileDtoPath = Path.Combine(targetDirectory, masterRomFileDto.FileName);

                                        // First check master Sibling rom masterSiblingRomDtoFile is on disk
                                        allRomFilesOnLocalDisk = FileSystemHelper.LocalFilePresent(useSha1InFileChecks, masterRomFileDtoPath, masterRomFileDto.Sha1Hash);

                                        platformTask.UiCard.ProcessedItems++;

                                        // MUSIC
                                        // Master masterSiblingRomDtoFile present. Now check master masterRomDtoSiblingDto sub files (will be music, NOT any other masterRomDtoSiblingDto roms),
                                        // as siblings with multi-masterSiblingRomDtoFile gets caught earlier by multi-masterSiblingRomDtoFile processor
                                        foreach (var masterRomDtoFile in masterRomDto.Files)
                                        {
                                            var hydratedmasterRomDtoFile = await _rommService.GetRomDetailsAsync(currentServer, masterRomDto.Id ?? 0, platformTask.Cts.Token);
                                            if (hydratedmasterRomDtoFile.IsSuccess && hydratedmasterRomDtoFile.Data != null)
                                            {
                                                masterRomDto = hydratedmasterRomDtoFile.Data;
                                            }
                                            else
                                            {
                                                // safety fallback: not sure will happen, but mark game as uninstalled if does
                                                allRomFilesOnLocalDisk = false;
                                                break;
                                            }

                                            string subFilePath = null;
                                            if (masterRomDtoFile.Category == "soundtrack")
                                            {
                                                subFilePath = Path.Combine(Constants.LaunchboxRootDir, "Music", platformTask.PlatformName,
                                                masterRomDto.RommFilename, masterRomDtoFile.FileName);

                                            }
                                            else if (masterRomDtoFile.Category == "game")
                                            {
                                                subFilePath = Path.Combine(targetDirectory, masterRomFileDto.FileName);
                                            }

                                            // HACK: can only query by filename here as sha isn't populated by romm for sub files!
                                            if (!FileSystemHelper.LocalFilePresent(false, subFilePath, null))
                                            {
                                                allRomFilesOnLocalDisk = false;
                                                break;
                                            }
                                        }

                                        UpdateIGameInstallStatus(masterIGameInstance, allRomFilesOnLocalDisk, masterRomFileDtoPath);

                                    }           

                                    platformTask.UiCard.AddLog($"{actionPerformed.ToString()}d metadata for [{masterRomDto.Name}] <Sibling Set Game>. Already Installed: [{allRomFilesOnLocalDisk}]. Siblings: [{masterRomDto.SiblingRoms?.Count()}]", PlatformSyncCardVM.LogType.Success);
                                
                                }
                                else
                                {
                                    platformTask.UiCard.WarningCount++;
                                    platformTask.UiCard.AddLog($"Could not construct or add Launchbox Game for '{masterRomDto.Name}' from Romm game. Game is part of masterRomDtoSiblingDto set.", PlatformSyncCardVM.LogType.Warning);
                                }


                            }

                            bool masterHasFiles = masterRomDto.Files != null && masterRomDto.Files.Count > 0;

                            if (masterHasFiles)
                            {
                                foreach (var masterFile in masterRomDto.Files)
                                {
                                    if (string.IsNullOrEmpty(masterFile.FileName)) continue;

                                    if (platformTask.UpdateMetadata && masterIGameInstance != null && string.IsNullOrEmpty(masterIGameInstance.ApplicationPath))
                                    {
                                        masterIGameInstance.ApplicationPath = Path.Combine(targetDirectory, masterFile.FileName);
                                    }
                                }
                            }
                            else if (!string.IsNullOrEmpty(masterRomDto.RommFilename))
                            {
                                if (platformTask.UpdateMetadata && masterIGameInstance != null && string.IsNullOrEmpty(masterIGameInstance.ApplicationPath))
                                {
                                    masterIGameInstance.ApplicationPath = Path.Combine(targetDirectory, masterRomDto.RommFilename);
                                }
                            }

                            if (masterIGameInstance != null && masterRomDto.Id.HasValue)
                            {
                                processedGamesLookup[masterRomDto.Id.Value] = masterIGameInstance;
                            }

                            if (platformTask.DownloadMediaFiles)
                            {
                                ScheduleMediaDownloads(masterRomDto, platformTask, chosenProfile, currentServer, masterIGameInstance);
                            }

                            // =========================================================================
                            // NEW STEP 4.5: ALSO INJECT MASTER AS AN ADDITIONAL APPLICATION VARIANT
                            // This ensure Launchbox identifies the game as having multi-versions (badge)
                            // =========================================================================
                            if (platformTask.UpdateMetadata && masterIGameInstance != null)
                            {
                                if (masterHasFiles)
                                {
                                    foreach (var masterFile in masterRomDto.Files)
                                    {
                                        if (string.IsNullOrEmpty(masterFile.FileName) || masterFile.Category != "game") continue;

                                        string masterLabel = $"{Path.GetFileNameWithoutExtension(masterFile.FileName)}";

                                        _launchboxService.AddOrUpdateAdditionalApplication(masterIGameInstance, masterFile,
                                            targetDirectory, masterLabel);
                                    }
                                }
                                else if (!string.IsNullOrEmpty(masterRomDto.RommFilename))
                                {
                                    var masterPlaceholderFileDto = new RomFileDTO { FileName = masterRomDto.RommFilename };
                                    // dunno if i need to do the masterFile.Category != "game" check on this one!!??
                                    // Don't think so as a placeholder?
                                    string masterLabel = $"{Path.GetFileNameWithoutExtension(masterRomDto.RommFilename)}";
                                    _launchboxService.AddOrUpdateAdditionalApplication(masterIGameInstance, masterPlaceholderFileDto,
                                        targetDirectory, masterLabel);
                                }
                            }

                            // =========================================================================
                            // 5. Append Variant items to the freshly minted master record
                            // =========================================================================
                            foreach (var variantRomDTO in variantRomDtoList)
                            {
                                if (platformTask.Cts.Token.IsCancellationRequested) break;

                                var detailedVariant = variantRomDTO;

                                // Hydrate masterSiblingRomDtoFile definitions if we are running an active download profile
                                if (installRoms && (variantRomDTO.Files == null || variantRomDTO.Files.Count == 0))
                                {
                                    var detailResult = await _rommService.GetRomDetailsAsync(currentServer, variantRomDTO.Id ?? 0, platformTask.Cts.Token);
                                    if (detailResult.IsSuccess && detailResult.Data != null)
                                    {
                                        detailedVariant = detailResult.Data;
                                    }
                                }

                                // Dump the hydrated variant files (games + music) into the aggregator!
                                if (detailedVariant.Files != null)
                                {
                                    allClusterFiles.AddRange(detailedVariant.Files);
                                }

                                bool variantHasFiles = detailedVariant.Files != null && detailedVariant.Files.Count > 0;

                                if (masterIGameInstance != null)
                                {
                                    if (variantHasFiles)
                                    {
                                        foreach (var fileEntry in detailedVariant.Files)
                                        {
                                            if (string.IsNullOrEmpty(fileEntry.FileName)) continue;

                                            if (platformTask.UpdateMetadata && fileEntry.Category == "game")
                                            {
                                                string variantLabel = $"{Path.GetFileNameWithoutExtension(fileEntry.FileName)}";
                                                _launchboxService.AddOrUpdateAdditionalApplication(masterIGameInstance, fileEntry,
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
                                        if (platformTask.UpdateMetadata)
                                        {
                                            var placeholderFileDto = new RomFileDTO { FileName = detailedVariant.RommFilename };
                                            // dunno if i need to do the masterFile.Category != "game" check on this one!!??
                                            // Don't think so as a placeholder?
                                            string variantLabel = $"{Path.GetFileNameWithoutExtension(detailedVariant.RommFilename)}";
                                            _launchboxService.AddOrUpdateAdditionalApplication(masterIGameInstance, placeholderFileDto,
                                                targetDirectory, variantLabel);
                                        }
                                    }
                                }
                            }

                            // Inside Pass 2, immediately after the variantRomDtoList loop finishes for the current cluster:
                            if (installRoms && masterIGameInstance != null)
                            {
                                long totalGroupSize = sortedRomDtosList.Sum(r => r.CombinedFilesSizeBytes ?? 0);

                                string masterFile = masterRomDto.RommFilename;

                                EnqueueBatchRomDownloadJob(platformTask, masterIGameInstance, masterRomDto, allRomDtoIds, masterFile, totalGroupSize,
                                    platformTask.TargetServer.Id.ToString(), stagedBatchDownloadItems, platformTask.NotifyLauncboxWhenMetadataComplete,
                                    aggregatedFiles: allClusterFiles);
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
                            platformTask.UiCard.AddLog($"Sync job cancelled by user after {totalDuration}", PlatformSyncCardVM.LogType.Warning);
                        }

                        platformTask.UiCard.AddLog($"Platform Sync completed successfully in {totalDuration}", PlatformSyncCardVM.LogType.Process);

                        //HACK: Could I buggery get the metadata processed and files processed to align perfectly. 
                        // Gonna take a lot of spelunking to get this precise + not sure how important precision is anyway here.
                        // Thus, this'll do for now
                        //platformTask.UiCard.ProcessedItems = platformTask.UiCard.TotalItems;

                    }
                    catch (Exception ex)
                    {
                        // Capture partial runtime up to the point of structural failure:
                        jobStopwatch.Stop();
                        string partialDuration = FormatElapsedTime(jobStopwatch.Elapsed);

                        platformTask.UiCard.ErrorCount++;

                        // Injected time elapsed before crash:
                        platformTask.UiCard.AddLog($"[SyncManager] Fatal error executing platform run after {partialDuration}: {ex.Message}", PlatformSyncCardVM.LogType.Error);
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

                // 6. Cleanup Safeguard: If the download cuts out halfway through, delete the broken/partial masterSiblingRomDtoFile
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