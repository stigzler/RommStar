using Microsoft.Data.Sqlite;
using RommStar.Core.Dtos;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Helpers;
using RommStar.Core.Launchbox;
using RommStar.Core.Mappers;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Xml.Linq;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Dapper;

namespace RommStar.Core.Services
{
    public class LaunchboxDataService
    {
        internal string? EmulatorId = null;
        private bool _deleteOldServerRoms = true;
        private LoggingService _loggingService;
        private IPlatform _operationalPlatform;
        private string? _operativeServerId = null;
        private bool _overwriteMetadata = true;

        private readonly string _dbConnectionString = "Data Source=" + 
                                                        Path.Combine(Constants.LaunchboxRootDir, "Metadata", "Launchbox.Metadata.db") +
                                                        ";Mode=ReadOnly;";


        /// <summary>
        /// Used in conjunction with _platformLbGameDatabaseIds. Lookup once presence of launchboxDatabaseID Game
        /// </summary>
        private HashSet<MetadataSyncHelperMap> _platformHelperMap = new HashSet<MetadataSyncHelperMap>();

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of platforms with LaunchboxDatabaseIds.
        /// </summary>
        private HashSet<int?> _platformLbGameDatabaseIds = new HashSet<int?>();

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of platforms with existing RommIds.
        /// </summary>
        private HashSet<string> _platformRommIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of platforms with existing ServerIds.
        /// </summary>
        private HashSet<string?> _platformServerIds = new HashSet<string?>();

        private RomMapper _romMapper;
        public LaunchboxSettings _launchboxSettings { get; set; } = new LaunchboxSettings();
        public SettingsService _settingsService { get; set; }
        public LaunchboxDataService(RomMapper romMapper, SettingsService settingsService, LoggingService loggingService)
        {
            _romMapper = romMapper;
            _settingsService = settingsService;
            _loggingService = loggingService;
            PopulateLaunchboxSettings();
        }
        public async Task<IEnumerable<LaunchboxDbPlatform>> GetDefaultDbPlatforms()
        {
            // The 'using var' statement creates the connection. 
            // As soon as this method finishes, C# automatically closes it and frees the file.
            using var connection = new SqliteConnection(_dbConnectionString);

            string sql = "SELECT * FROM Platforms ORDER BY Name ASC";

            // Dapper opens the connection, runs the query, maps the data, and lets the 'using' block close it down.
            var platforms = await connection.QueryAsync<LaunchboxDbPlatform>(sql);

            return platforms;
        }

        public async Task<IEnumerable<LaunchboxDbEmulator>> GetDefaultDbEmulators()
        {
            // The 'using var' statement creates the connection. 
            // As soon as this method finishes, C# automatically closes it and frees the file.
            using var connection = new SqliteConnection(_dbConnectionString);

            string sql = "SELECT * FROM Emulators ORDER BY Name ASC";

            // Dapper opens the connection, runs the query, maps the data, and lets the 'using' block close it down.
            var emulators = await connection.QueryAsync<LaunchboxDbEmulator>(sql);

            return emulators;
        }

        public async Task<IEnumerable<LaunchboxDbEmulatorPlatform>> GetDefaultDbEmulatorPlatforms()
        {
            // The 'using var' statement creates the connection. 
            // As soon as this method finishes, C# automatically closes it and frees the file.
            using var connection = new SqliteConnection(_dbConnectionString);

            string sql = "SELECT * FROM EmulatorPlatforms ORDER BY Name ASC";

            // Dapper opens the connection, runs the query, maps the data, and lets the 'using' block close it down.
            var emulatorPlatforms = await connection.QueryAsync<LaunchboxDbEmulatorPlatform>(sql);

            return emulatorPlatforms;
        }

        public void AddOrUpdateAdditionalApplication(IGame parentGame, RomFileDTO fileDto, string targetDirectory,
                    string customAppName = null, bool usePlaceholderPath = false)
        {
            if (parentGame == null || fileDto == null || string.IsNullOrEmpty(fileDto.FileName)) return;

            // Determine the database lookup path based on whether a virtual placeholder override is requested
            string cleanAppPath = usePlaceholderPath
                ? Constants.RomPlaceholder
                : Path.Combine(targetDirectory, fileDto.FileName);

            var existingApps = parentGame.GetAllAdditionalApplications();
            var app = existingApps.FirstOrDefault(a => a.Name == customAppName);

            var tags = TagHelper.ParseFilename(fileDto.FileName);

            if (app == null)
            {
                app = parentGame.AddNewAdditionalApplication();
            }

            app.ApplicationPath = cleanAppPath;
            app.Version = tags.Version;
            app.Disc = tags.DiscNumber;
            app.SideA = tags.IsSideA;
            app.SideB = tags.IsSideB;
            app.Region = tags.Region;
            app.Priority = (tags.DiscNumber != null) ? (int)tags.DiscNumber : 0;
            app.Installed = false;
            app.Status = "Not Installed";
            app.Name = customAppName;
            app.EmulatorId = parentGame.EmulatorId;
            app.UseEmulator = (parentGame.EmulatorId != null) ? true : false;

        }

        public void CreateNewPlatform(string platformName)
        {
            var newPlatform = PluginHelper.DataManager.AddNewPlatform(platformName);
            PluginHelper.DataManager.Save();
        }

        public string GetPlatformDefaultEmulatorID(string platformName)
        {
            foreach (IEmulator emu in PluginHelper.DataManager.GetAllEmulators())
            {
                IEmulatorPlatform[] emulatorPlatforms = emu.GetAllEmulatorPlatforms()
                    .Where(ep => ep.Platform == platformName).ToArray();

                IEmulatorPlatform defaultEmulatorPlatform = emulatorPlatforms?.FirstOrDefault(ep => ep.IsDefault);

                if (defaultEmulatorPlatform != null) return defaultEmulatorPlatform.EmulatorId;
                else if (emulatorPlatforms.Count() > 0) return emulatorPlatforms[0].EmulatorId;
            }
            return null;
        }

        public string GetPlatformIconPath(string platformName)
        {
            string votiIconPath = Path.Combine(Constants.LaunchboxRootDir, Constants.MediaPacksPlatformIconsRelPath,
                _launchboxSettings.PlatformIconPack, "Platforms", $"{platformName}.png");
            return votiIconPath;
        }

        public List<LaunchboxPlatformDTO> GetUserPlatforms()
        {
            IPlatform[] livePlatforms = PluginHelper.DataManager.GetAllPlatforms();
            if (livePlatforms == null) return new List<LaunchboxPlatformDTO>();

            return livePlatforms.Select(p => new LaunchboxPlatformDTO
            {
                Name = p.Name,
                ScrapeAs = p.ScrapeAs,
                SortTitle = p.SortTitle,
                RomFolder = p.Folder
                // If you need NestedName or SortTitleOrTitle, calculate them cleanly here
            })
            .OrderBy(p => p.Name)
            .ToList();
        }

        internal string GetLaunchboxRomsFolderPath(string launchboxPlatformName)
        {
            string romFolder = PluginHelper.DataManager.GetPlatformByName(launchboxPlatformName)?.Folder;
            if (string.IsNullOrEmpty(romFolder)) romFolder = $"Games\\{launchboxPlatformName}";
            if (Directory.Exists(romFolder)) return romFolder;
            return string.Empty;
        }


        /// <summary>
        /// This moves the unzipped game files from the temp locaiton to the right locaiton on disk
        /// </summary>
        /// <param name="tempZipPath"></param>
        /// <param name="romQueueItems">List of game/roms</param>
        /// <returns></returns>
        public async Task UnzipRomsAndUpdateIGamesBatchAsync(string tempZipPath, List<RomQueueItem> romQueueItems,
            CancellationToken token, bool isBackgroundBatch = true)
        {
            if (romQueueItems == null || romQueueItems.Count == 0) return;

            // 1. Resolve Settings and Platform Roots
            var platformSettings = _settingsService.Settings.PlatformSyncSettings.FirstOrDefault(pss =>
                            pss.LaunchboxPlatformName == romQueueItems[0].PlatformName);

            bool individualGameFolders = (platformSettings?.ExtendedSyncSettings?.ApplySettings == true) ?
                platformSettings.ExtendedSyncSettings.UseIndividualGameFolders :
                 _settingsService.Settings.GlobalExtendedSyncSettings.UseIndividualGameFolders;

            IPlatform platform = PluginHelper.DataManager.GetPlatformByName(romQueueItems[0].PlatformName);
            if (platform == null)
            {
                _loggingService.Log($"[Extraction] Error: Platform '{romQueueItems[0].PlatformName}' not found in LaunchBox.");
                return;
            }

            string romRoot = FileSystemHelper.ResolvedRompath(platform.Folder, platform.Name);
            string expectedPrefix = $"roms/{romQueueItems[0].PlatformStub}/".Replace('\\', '/');

            var extractedFilesMap = new Dictionary<string, List<string>>();

            // 2. Open the Zip Archive for streaming extraction
            using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {

                    // Ttripwire. It checks the token before processing the next file. 
                    // If the user closed LaunchBox, it throws OperationCanceledException instantly, bubbling back to calling method
                    token.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string entryFullName = entry.FullName.Replace('\\', '/');

                    if (!entryFullName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 3. Resolve Destination Paths and Detect Soundtracks
                    string relativeRomPath;
                    string targetDirectory;
                    bool isSoundtrack = entryFullName.Contains("/soundtrack/", StringComparison.OrdinalIgnoreCase);

                    if (isSoundtrack)
                    {
                        relativeRomPath = entryFullName.Substring(expectedPrefix.Length).Replace("/soundtrack", "");
                        targetDirectory = Path.Combine(Constants.LaunchboxRootDir, "Music", platform.Name);
                    }
                    else
                    {
                        relativeRomPath = entryFullName.Substring(expectedPrefix.Length);
                        targetDirectory = romRoot;
                    }

                    // Route to correct queue item
                    RomQueueItem matchingItem = romQueueItems.Count == 1
                        ? romQueueItems[0]
                        : FindMatchingBatchItemForFile(romQueueItems, entryFullName);

                    if (matchingItem != null && !isSoundtrack)
                    {
                        // THE FLATTENING RULE:
                        // Since siblings are aggregated, we can't count the files. 
                        // Instead, trust the queue item. If it's NOT a multi-file game natively, 
                        // RomM only foldered it because of soundtracks or batch packaging to the single game file.
                        bool flattenFolder = !matchingItem.IsMultiFileGame;

                        if (flattenFolder || individualGameFolders)
                        {
                            relativeRomPath = Path.GetFileName(relativeRomPath);
                        }

                        if (individualGameFolders)
                        {
                            targetDirectory = Path.Combine(romRoot, matchingItem.GameNameSanitised);
                        }
                    }

                    // 4. Safe Atomic Extraction to Disk
                    string fullDestinationPath = Path.Combine(targetDirectory, relativeRomPath);
                    string destDirectoryPath = Path.GetDirectoryName(fullDestinationPath);

                    if (!Directory.Exists(destDirectoryPath))
                        Directory.CreateDirectory(destDirectoryPath);

                    // todo: need to find a way to log back to ui.
                    if (File.Exists(fullDestinationPath))
                    {
                        _loggingService.Log($"Attempting copying an unzipped game file where the file already exists: {fullDestinationPath}");
                    }

                    // If music file is playing when click Install, it is locked by LB. If you try to unzip onto the existing file, it throws an error
                    // Also possible edge case where rom is being used elsewhere and you try to unzip the rom back onto itself. 

                    matchingItem.UpdateQueueItemStatus(RomQueueItemStatus.Unzipping);

                    try
                    {
                        entry.ExtractToFile(fullDestinationPath, overwrite: true);
                        _loggingService.Log($"File extracted and copied successfully.", Primitives.LoggingLevel.Debug);
                    }
                    catch (Exception e)
                    {
                        if (isSoundtrack)
                        {
                            _loggingService.Log($"Could not unzip soundtrack to target destination. Launchbox may be playing it if it exists locally, thus you can't overwrite it. Exception: {e.Message}");

                        }
                        else
                        {
                            // is game
                            matchingItem.IsQuarantined = true;
                            matchingItem.UpdateQueueItemStatus(RomQueueItemStatus.Errored);
                            matchingItem.LastError = $"Could not unzip to target destination ({fullDestinationPath}). Exception: {e.Message}";
                            _loggingService.Log($"Could not unzip to target destination. Exception: {e.Message}");
                        }
                    }

                    // 5. Map the extracted file to its parent LaunchBox ID
                    if (matchingItem != null)
                    {
                        if (!extractedFilesMap.ContainsKey(matchingItem.LaunchboxId))
                        {
                            extractedFilesMap[matchingItem.LaunchboxId] = new List<string>();
                        }
                        extractedFilesMap[matchingItem.LaunchboxId].Add(fullDestinationPath);
                    }
                }
            }

            // 6. Update Launchbox Game objects
            foreach (var batchItem in romQueueItems)
            {
                var game = PluginHelper.DataManager.GetGameById(batchItem.LaunchboxId);

                if (game != null)
                {
                    game.Installed = true;
                    game.Status = "Installed";

                    if (extractedFilesMap.TryGetValue(batchItem.LaunchboxId, out var unzippedFiles) && unzippedFiles.Count > 0)
                    {
                        // Isolate music files to assign them cleanly
                        // Derive the exact LaunchBox-assigned Music directory for this platform
                        string canonicalMusicDirectory = Path.Combine(Constants.LaunchboxRootDir, "Music", platform.Name);

                        // Ensure trailing slash for precise matching, handling cross-platform separator differences
                        string normalizedMusicPrefix = canonicalMusicDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                        // Isolate music files by verifying they reside strictly within the canonical music folder tree
                        var musicFiles = unzippedFiles.Where(f => f.StartsWith(normalizedMusicPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
                        var gameFiles = unzippedFiles.Except(musicFiles).ToList();

                        // todo: this will always set the music to the first downloaded track. LB limitation as Romm
                        // may have more than 1 music file for each game/rom - h/e user will be able to choose the music track
                        // manually via Edit Metadata in LB. Maybe not a biggie? They can also order there music tracks on Romm
                        // to affect the primary music track? (researched - no dice 😔 )
                        if (musicFiles.Any())
                        {
                            game.MusicPath = musicFiles.First();
                        }

                        if (gameFiles.Any())
                        {
                            var additionalApps = game.GetAllAdditionalApplications();
                            string mainApplicationPath = string.Empty;

                            // will this work for multi-disc??
                            if (Path.HasExtension(batchItem.MasterFilename))
                            {
                                mainApplicationPath = gameFiles.FirstOrDefault(path => Path.GetFileName(path)
                                                           .Equals(batchItem.MasterFilename, StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                // Safely fallback to the first available game file if multi-disc parsing fails
                                var firstApp = additionalApps.FirstOrDefault(a => !string.IsNullOrEmpty(a.ApplicationPath)
                                                                   && (Helpers.TagHelper.ParseFilename(a.ApplicationPath).DiscNumber == 1 ||
                                                                        Helpers.TagHelper.ParseFilename(a.ApplicationPath).IsSideA));

                                mainApplicationPath = firstApp?.ApplicationPath ?? additionalApps.FirstOrDefault()?.ApplicationPath ?? gameFiles.First();
                            }

                            if (!string.IsNullOrEmpty(mainApplicationPath))
                            {
                                game.ApplicationPath = mainApplicationPath;
                            }

                            // FIXED: Correctly map the unzipped file to the Additional App's placeholder
                            foreach (var additionalApp in additionalApps)
                            {
                                string expectedFileName = Path.GetFileName(additionalApp.ApplicationPath);
                                string realExtractedPath = gameFiles.FirstOrDefault(path => Path.GetFileName(path).Equals(expectedFileName, StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(realExtractedPath))
                                {
                                    additionalApp.ApplicationPath = realExtractedPath;
                                }

                                additionalApp.Status = "Installed";
                                additionalApp.Installed = true;
                            }
                        }
                    }
                    else
                    {
                        batchItem.LastError = $"[Extraction] Warning: Unzipped files couldn't correlate directly to LaunchBox Game ID: {batchItem.LaunchboxId}";
                        batchItem.UpdateQueueItemStatus(RomQueueItemStatus.CompleteWithWarnings);
                        Debug.WriteLine($"[Extraction] Warning: Unzipped files couldn't correlate directly to LaunchBox Game ID: {batchItem.LaunchboxId}");
                    }
                }

                // game finished processing here
                // Force save the LaunchBox database changes for the batch
                PluginHelper.DataManager.Save();

                // Only trigger the background UI hacks if this is the background daemon!
                // The manual VIP install handles its own UI updates via UpdatePlayButtonUi.
                await Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    // If user browsing the same platform as the download, refresh to update Install badges. Otherwise don't to reduce UI noise.
                    // Note: AT LB startup, it defaults to display your last platform, but GetSelectedPlatform() returns null
                    // therefor refresh on null or same platform. 
                    IPlatform selectedPlatform = PluginHelper.StateManager.GetSelectedPlatform();
                    if (selectedPlatform == null || selectedPlatform.Name == platform.Name)
                    {
                        if (game != null) await LaunchboxViewsHelper.UpdatePlayButtonUi(game);
                        _ = LaunchboxViewsHelper.SoftRefreshUi();
                    }
                }));

                batchItem.UpdateQueueItemStatus(RomQueueItemStatus.Complete);

            }


        }



        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="platformName">Name in launcbox DB. Not: ScrapeAs, SortTitle etc.</param>
        /// <returns>Nothing if successful. Error message otherwise</returns>
        public string SaveNewPlatformIcon(string source, string platformName, bool overwrite = false)
        {
            string votiIconPath = Path.Combine(Constants.LaunchboxRootDir, Constants.MediaPacksPlatformIconsRelPath,
                _launchboxSettings.PlatformIconPack, "Platforms", $"{platformName}.png");

            if (File.Exists(votiIconPath) && overwrite == false)
            {
                return "Platform icon already exists.";
            }
            try
            {
                File.Copy(source, votiIconPath, overwrite);
                return null;
            }
            catch (Exception ex)
            {
                return "Failed to save platform icon:" + ex.Message;
            }
        }

        public bool SetupGameUpserts(string platformName, string emulatorID, string serverId, ExtendedSyncSettings syncSettings)
        {
            _operationalPlatform = PluginHelper.DataManager.GetPlatformByName(platformName);
            _operativeServerId = serverId;
            _overwriteMetadata = syncSettings.OverwriteMetadata;
            _deleteOldServerRoms = syncSettings.DeleteOldServerRoms;
            EmulatorId = emulatorID;

            _platformLbGameDatabaseIds.Clear();
            _platformRommIds.Clear();
            _platformHelperMap.Clear();

            if (_operationalPlatform == null) return false;

            IGame[] games = _operationalPlatform.GetAllGames(true, true);

            foreach (IGame game in games)
            {
                Debug.WriteLine(game.Title);

                // 1. Determine if LB iGame has an old romm server assigned. If so, delete it and do not include in the lookup lists
                var gameCustomFields = game.GetAllCustomFields();
                var rommServerField = game.GetAllCustomFields().FirstOrDefault(f => f.Name == "Romm_ServerId");
                if (rommServerField != null && !string.IsNullOrWhiteSpace(rommServerField.Value) && rommServerField.Value != _operativeServerId)
                {
                    PluginHelper.DataManager.TryRemoveGame(game);
                    PluginHelper.DataManager.Save();
                    continue;
                }

                MetadataSyncHelperMap gameIdMap = new MetadataSyncHelperMap(game.Id, game.LaunchBoxDbId);
                gameIdMap.GameName = game.Title;


                if (gameCustomFields != null)
                {
                    var romIdCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_RomIds.ToString());
                    var serverIdCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ServerId.ToString());
                    var protectMetadataCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ProtectMetadata.ToString());

                    gameIdMap.RommIds = (romIdCustomField != null) ? romIdCustomField.Value : null;
                    gameIdMap.RommServerId = (serverIdCustomField != null) ? serverIdCustomField.Value : null;

                    bool.TryParse(protectMetadataCustomField?.Value, out var boolResult);
                    gameIdMap.ProtectMetadata = boolResult;
                }


                _platformHelperMap.Add(gameIdMap);
            }

            _platformLbGameDatabaseIds = _platformHelperMap.Select(phm => phm.LbDatabaseId).Where(id => id != null).ToHashSet();
            _platformServerIds = _platformHelperMap.Select(phm => phm.RommServerId).Where(id => id != null).ToHashSet();

            // Flatten the CSV strings into separate, easily searchable items inside the lookup set
            _platformRommIds = _platformHelperMap
                .Where(phm => !string.IsNullOrWhiteSpace(phm.RommIds))
                .SelectMany(phm => phm.RommIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);


            return _operationalPlatform != null;
        }

        public async Task<(IGame Game, MetadataSyncAction Action)> SyncRommDto(RomDTO rommDTO, string customRomIdsCsv = null)
        {
            // =========================================================================
            // TEMPORARY TESTING: 1 in 10 chance to simulate a metadata failure
            // =========================================================================
            //if (Random.Shared.Next(1, 11) == 1)
            //{
            //    // Return null to fake a catastrophic failure/timeout mapping database entries
            //    return (null, MetadataSyncAction.Update);
            //}

            bool hasMatchingLaunchboxId = rommDTO.LaunchboxId.HasValue &&
                                            _platformLbGameDatabaseIds.Contains(rommDTO.LaunchboxId.Value);

            bool hasMatchingRommId = rommDTO.Id.HasValue && _platformRommIds.Contains(rommDTO.Id.Value.ToString());

            bool hasMatchingServerId = !string.IsNullOrEmpty(_operativeServerId) &&
                                       (_platformServerIds.Contains(_operativeServerId) ||
                                       _platformHelperMap.Any(m => m.RommServerId == _operativeServerId));

            MetadataSyncState metadataSyncState = new MetadataSyncState(hasMatchingLaunchboxId, hasMatchingRommId, hasMatchingServerId,
                rommDTO.HasMultipleFiles.GetValueOrDefault());

            MetadataSyncAction syncAction = MetadataSyncDecisionEngine.DetermineAction(metadataSyncState, _overwriteMetadata, _deleteOldServerRoms);
            IGame game = null;
            bool metadataProtected = false;

            // ---------------------------------------------------------
            // UPDATE IGame
            // ---------------------------------------------------------

            if (syncAction == MetadataSyncAction.Update)
            {
                MetadataSyncHelperMap metadataSyncHelperMap;
                string launchboxLocalId;

                if (hasMatchingRommId == true && hasMatchingServerId == true && rommDTO.Id.HasValue)
                {
                    metadataSyncHelperMap = _platformHelperMap.Single(pg =>
                                    pg.RommServerId == _operativeServerId && pg.ContainsId(rommDTO.Id.Value));
                }
                else
                {
                    metadataSyncHelperMap = _platformHelperMap.Single(pg => pg.LbDatabaseId == rommDTO.LaunchboxId);
                }

                launchboxLocalId = metadataSyncHelperMap.LocalId;
                game = PluginHelper.DataManager.GetGameById(launchboxLocalId);

                // Get RommStar IGame.CustomFields if exist
                var existingFields = game.GetAllCustomFields();

                // test if metadata flagged as protected. If so, set flag
                var romProtectMetadataField = existingFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ProtectMetadata.ToString());
                if (romProtectMetadataField != null)
                {
                    bool.TryParse(existingFields?.FirstOrDefault(gcf =>
                                                            gcf.Name == CustomFieldTypes.Romm_ProtectMetadata.ToString()).Value,
                                                            out metadataProtected);
                }

                // now deal with any Romm metadata in event of launchboxDatabaseId match. Add or update.
                if (metadataSyncHelperMap.LbDatabaseId != null)
                {
                    var rommIdField = existingFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_RomIds.ToString());

                    if (rommIdField != null)
                    {
                        rommIdField.Value = rommDTO.Id.ToString();
                        var rommServerField = existingFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ServerId.ToString());
                        rommServerField.Value = _operativeServerId;
                    }
                    else
                    {
                        AddNewRommMetadata(game, rommDTO.LaunchboxId?.ToString());
                    }
                }

                // Update Romm ID Tracking Custom Field on existing platforms if a new context list is provided
                if (game != null && !string.IsNullOrEmpty(customRomIdsCsv))
                {
                    var romIdsField = existingFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_RomIds.ToString());
                    if (romIdsField != null)
                    {
                        romIdsField.Value = customRomIdsCsv;
                    }
                }

                var localAltNames = game.GetAllAlternateNames();
                foreach (var altName in rommDTO.AlternativeNames.Distinct())
                {
                    if (!localAltNames.Any(lan => lan.Name == altName && altName != rommDTO.Name))
                    {
                        var newAltName = game.AddNewAlternateName();
                        newAltName.Name = altName;
                    }
                }


            }

            // ---------------------------------------------------------
            // INSERT IGame
            // ---------------------------------------------------------

            else if (syncAction == MetadataSyncAction.Insert)
            {
                game = PluginHelper.DataManager.AddNewGame(rommDTO.Name);
                game.Installed = false;
                game.Status = "Not Installed";
                game.ApplicationPath = Constants.RomPlaceholder;
                //game.ApplicationPath = $"Plugins\\RommStar\\DummyPath\\{rommDTO.PlatformStub}\\{rommDTO.Name}" ;


                // Use custom aggregated CSV string if provided (Scenario 2), otherwise default to standard singular ID string
                string finalRomIdsValue = !string.IsNullOrEmpty(customRomIdsCsv) ? customRomIdsCsv : Convert.ToString(rommDTO.Id);

                AddNewRommMetadata(game, finalRomIdsValue);

                foreach (var altName in rommDTO.AlternativeNames.Distinct())
                {
                    if (altName != rommDTO.Name)
                    {
                        var newAltName = game.AddNewAlternateName();
                        newAltName.Name = altName;
                    }
                }
            }

            else if (syncAction == MetadataSyncAction.DeleteAndInsert)
            {
                Debug.WriteLine("DeleteAndInsert");
            }


            if (game != null)
            {
                game.Platform = _operationalPlatform.Name;
                game.EmulatorId = EmulatorId;

                switch (syncAction)
                {
                    // This actually does the lb database IGame population.
                    case MetadataSyncAction.Insert:
                        _romMapper.RommRomDtoToIGame(rommDTO, game);
                        break;
                    case MetadataSyncAction.Update:
                        if (!metadataProtected)
                            _romMapper.RommRomDtoToIGame(rommDTO, game);
                        break;
                }
            }

            return (game, syncAction);
        }

        internal async Task<bool> DeletePlatform(string launchboxPlatformName)
        {
            try
            {
                var platform = PluginHelper.DataManager.GetPlatformByName(launchboxPlatformName);
                if (platform == null) return false;

                bool success = PluginHelper.DataManager.TryRemovePlatform(platform);
                if (success) PluginHelper.DataManager.Save();

                return success;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void AddNewRommMetadata(IGame game, string finalRomIdsValue)
        {
            var romIdCustomField = game.AddNewCustomField();
            romIdCustomField.Name = CustomFieldTypes.Romm_RomIds.ToString();
            romIdCustomField.Value = finalRomIdsValue;

            var serverIdCustomField = game.AddNewCustomField();
            serverIdCustomField.Name = CustomFieldTypes.Romm_ServerId.ToString();
            serverIdCustomField.Value = _operativeServerId;

            var protectMetadataCustomField = game.AddNewCustomField();
            protectMetadataCustomField.Name = CustomFieldTypes.Romm_ProtectMetadata.ToString();
            protectMetadataCustomField.Value = "false";

        }

        //private RomQueueItem FindMatchingBatchItemForFile(List<RomQueueItem> batchItems, string zipEntryFullName)
        //{
        //    return batchItems.FirstOrDefault(item =>
        //        // 1. Single File / Sibling Match: The path ends exactly with the MasterFilename
        //        zipEntryFullName.EndsWith(item.MasterFilename, StringComparison.OrdinalIgnoreCase) ||

        //        // 2. Multi-Disc Match: The path contains the MasterFilename as a folder directory
        //        zipEntryFullName.Contains($"/{item.MasterFilename}/", StringComparison.OrdinalIgnoreCase)
        //    );
        //}

        private RomQueueItem FindMatchingBatchItemForFile(List<RomQueueItem> batchItems, string zipEntryFullName)
        {
            return batchItems.FirstOrDefault(item =>
                // 1. Check MasterFilename
                zipEntryFullName.EndsWith(item.MasterFilename, StringComparison.OrdinalIgnoreCase) ||
                zipEntryFullName.Contains($"/{item.MasterFilename}/", StringComparison.OrdinalIgnoreCase) ||

                // 2. Safely check all aggregated variant and sibling files
                (item.MultiFiles != null && item.MultiFiles.Any(f =>
                    !string.IsNullOrEmpty(f.FileName) &&
                    (zipEntryFullName.EndsWith(f.FileName, StringComparison.OrdinalIgnoreCase) ||
                     zipEntryFullName.Contains($"/{f.FileName}/", StringComparison.OrdinalIgnoreCase))
                ))
            );
        }

        /// <summary>
        /// Evaluates all unzipped file variations targeting a unique game entry and chooses the primary bootable application path.
        /// </summary>
        private string GetBestApplicationPath(List<string> filePaths)
        {
            if (filePaths.Count == 1) return filePaths[0];

            // Heuristic priority: Target master headers, unified archives, or executable scripts first
            var priorityExtensions = new[] { ".cue", ".gdi", ".chd", ".m3u", ".ccd", ".exe", ".bat", ".cmd" };

            foreach (var ext in priorityExtensions)
            {
                var match = filePaths.FirstOrDefault(f => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // Secondary cleanup: Filter out common non-playable sidecar documents or asset definitions
            var sidecarExtensions = new[] { ".txt", ".nfo", ".jpg", ".png", ".srm", ".sav", ".pdf" };
            var validBootables = filePaths
                .Where(f => !sidecarExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (validBootables.Count > 0)
            {
                // Alphabetical ascending sort safely isolates "Disc 1.iso" or "Part 1.bin" as the default execution node
                return validBootables.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).First();
            }

            return filePaths[0];
        }
        private void PopulateLaunchboxSettings()
        {
            XDocument doc = XDocument.Load(Path.Combine(Constants.LaunchboxRootDir, "Data\\Settings.xml"));
            var settings = doc.Root?.Element("Settings");
            if (settings?.Element("PlatformIconPack")?.Value is string iconPack && !string.IsNullOrWhiteSpace(iconPack))
            {
                _launchboxSettings.PlatformIconPack = iconPack;
            }
        }
    }
}