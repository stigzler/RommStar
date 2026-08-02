using RommStar.Core.Dtos;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Helpers;
using RommStar.Core.Launchbox;
using RommStar.Core.Mappers;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Compression;
using System.Web;
using System.Xml.Linq;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Services
{
    public class LaunchboxDataService
    {
        internal string? EmulatorId = null;
        private bool _deleteOldServerRoms = true;
        private IPlatform _operationalPlatform;
        private string? _operativeServerId = null;
        private bool _overwriteMetadata = true;
        /// <summary>
        /// Used in conjunction with _platformLbGameDatabaseIds. Lookup once presence of launchboxDatabaseID Game
        /// </summary>
        private HashSet<MetadataSyncHelperMap> _platformHelperMap = new HashSet<MetadataSyncHelperMap>();

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with LaunchboxDatabaseIds.
        /// </summary>
        private HashSet<int?> _platformLbGameDatabaseIds = new HashSet<int?>();

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with existing RommIds.
        /// </summary>
        private HashSet<string> _platformRommIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with existing ServerIds.
        /// </summary>
        private HashSet<string?> _platformServerIds = new HashSet<string?>();

        private RomMapper _romMapper;
        public LaunchboxSettings _launchboxSettings { get; set; } = new LaunchboxSettings();
        public SettingsService _settingsService { get; set; }
        public LaunchboxDataService(RomMapper romMapper, SettingsService settingsService)
        {
            _romMapper = romMapper;
            _settingsService = settingsService;
            PopulateLaunchboxSettings();
        }

        public void AddOrUpdateAdditionalApplication(IGame parentGame, RomFileDTO fileDto, string targetDirectory,
                    string customAppName = null, bool usePlaceholderPath = false)
        {
            if (parentGame == null || fileDto == null || string.IsNullOrEmpty(fileDto.FileName)) return;

            // Determine the database lookup path based on whether a virtual placeholder override is requested
            string cleanAppPath = usePlaceholderPath
                ? Constants.romPlaceholder
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

        public List<LaunchboxPlatformDTO> GetPlatforms()
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tempZipPath"></param>
        /// <param name="romQueueItems">List of game/roms</param>
        /// <returns></returns>
        public async Task ProcessDownloadedRomBatchAsync(string tempZipPath, List<RomQueueItem> romQueueItems)
        {
            if (romQueueItems == null || romQueueItems.Count == 0) return;

            // 1. Resolve Settings and Platform Roots
            var platformSettings = _settingsService.Settings.PlatformSyncSettings.FirstOrDefault(pss =>
                            pss.LaunchboxPlatformName == romQueueItems[0].PlatformName);

            // Safe null propagation checks in case a platform profile configuration layout is raw or uninitialized
            bool individualGameFolders = (platformSettings?.ExtendedSyncSettings?.ApplySettings == true) ?
                platformSettings.ExtendedSyncSettings.UseIndividualGameFolders :
                 _settingsService.Settings.GlobalExtendedSyncSettings.UseIndividualGameFolders;

            IPlatform platform = PluginHelper.DataManager.GetPlatformByName(romQueueItems[0].PlatformName);
            if (platform == null)
            {
                // Todo: user feedback
                Debug.WriteLine($"[Extraction] Error: Platform '{romQueueItems[0].PlatformName}' not found in LaunchBox.");
                return;
            }

            string romRoot = FileSystemHelper.ResolvedRompath(platform.Folder, platform.Name);

            // Build the exact internal folder prefix used by RomM's zip generation engine
            string expectedPrefix = $"roms/{romQueueItems[0].PlatformStub}/".Replace('\\', '/');

            // Tracks all successfully extracted file paths mapped directly to their corresponding LaunchBox Game ID
            var extractedFilesMap = new Dictionary<string, List<string>>();

            // 2. Open the Zip Archive for streaming extraction
            using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Skip empty entries or pure directory markers (they end with a slash in zip specs)
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    // Normalize path delimiters to forward slashes for reliable zip matching
                    string entryFullName = entry.FullName.Replace('\\', '/');

                    // Filter out any anomalous files outside the expected platform tree
                    if (!entryFullName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Strip "roms/[Platform Name]/" to isolate the clean, relative file path
                    string relativeRomPath = entryFullName.Substring(expectedPrefix.Length);

                    // 3. Resolve Destination Paths and Correlate Game Item
                    string targetDirectory = romRoot;
                    RomQueueItem matchingItem = null;

                    // Resolved outside the directory-flag block to ensure path tracking works globally
                    if (romQueueItems.Count == 1)
                    {
                        matchingItem = romQueueItems[0];
                    }
                    else
                    {
                        // Background Queue Path: Match this specific file entry to its owning game item
                        matchingItem = FindMatchingBatchItemForFile(romQueueItems, entry.Name);
                    }

                    if (individualGameFolders && matchingItem != null)
                    {
                        targetDirectory = Path.Combine(romRoot, matchingItem.GameNameSanitised);
                    }

                    // 4. Safe Atomic Extraction to Disk
                    string fullDestinationPath = Path.Combine(targetDirectory, relativeRomPath);
                    string destDirectoryPath = Path.GetDirectoryName(fullDestinationPath);

                    // Build missing subdirectory trees (handles multi-file/multi-disc structures safely)
                    if (!Directory.Exists(destDirectoryPath))
                        Directory.CreateDirectory(destDirectoryPath);

                    // Extract and overwrite any stale or corrupted files matching this payload
                    entry.ExtractToFile(fullDestinationPath, overwrite: true);

                    // Map the extracted file to its parent LaunchBox ID tracking set
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

            // 5. Update Launchbox Game object
            foreach (var batchItem in romQueueItems)
            {
                var game = PluginHelper.DataManager.GetGameById(batchItem.LaunchboxId);

                if (game != null)
                {
                    game.Installed = true;
                    game.Status = "Installed";

                    if (extractedFilesMap.TryGetValue(batchItem.LaunchboxId, out var unzippedFiles) && unzippedFiles.Count > 0)
                    {

                        // Get all additional apps
                        var additionalApps = game.GetAllAdditionalApplications();

                        // switch between multi-file games (eg. Disc 1, Disc 2) or sibling sets - romm api for multi-file games means
                        // firstRomDTO.RommFilename DOESN'T have a extension (for some bloody reason)
                        string mainApplicationPath = string.Empty;
                        if (Path.HasExtension(batchItem.MasterFilename))
                        {
                            mainApplicationPath = unzippedFiles.FirstOrDefault(path => Path.GetFileName(path)
                                                       .Equals(batchItem.MasterFilename, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            mainApplicationPath = additionalApps.FirstOrDefault(a => !string.IsNullOrEmpty(a.ApplicationPath)
                                                               && (Helpers.TagHelper.ParseFilename(a.ApplicationPath).DiscNumber == 1 ||
                                                                    Helpers.TagHelper.ParseFilename(a.ApplicationPath).IsSideA)
                                                                    ).ApplicationPath
                                                                ?? additionalApps.First().ApplicationPath;
                        }                                            

                        game.ApplicationPath = mainApplicationPath;              

                        foreach (var additionalApp in additionalApps)
                        {
                            string applicationPath = unzippedFiles.FirstOrDefault(path => Path.GetFileName(path)
                            .Equals(batchItem.MasterFilename, StringComparison.OrdinalIgnoreCase));
                            additionalApp.Status = "Installed";
                            additionalApp.Installed = true;
                        }
                    }
                    else
                    {
                        // Safe defensive fallback in case matching index correlations had string formatting gaps
                        Debug.WriteLine($"[Extraction] Warning: Unzipped files couldn't correlate directly to LaunchBox Game ID: {batchItem.LaunchboxId}");
                    }
                }


            }

            PluginHelper.DataManager.Save();

            if (PluginHelper.LaunchBoxMainViewModel != null)
                PluginHelper.LaunchBoxMainViewModel.RefreshData();
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
                MetadataSyncHelperMap gameIdMap = new MetadataSyncHelperMap(game.Id, game.LaunchBoxDbId);
                gameIdMap.GameName = game.Title;

                var gameCustomFields = game.GetAllCustomFields();

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

            bool hasMatchingLaunchboxId = rommDTO.LaunchboxId.HasValue && _platformLbGameDatabaseIds.Contains(rommDTO.LaunchboxId.Value);

            bool hasMatchingServerId = !string.IsNullOrEmpty(_operativeServerId) &&
                                       (_platformServerIds.Contains(_operativeServerId) || _platformHelperMap.Any(m => m.RommServerId == _operativeServerId));

            bool hasMatchingRommId = rommDTO.Id.HasValue && _platformRommIds.Contains(rommDTO.Id.Value.ToString());

            MetadataSyncState metadataSyncState = new MetadataSyncState(hasMatchingLaunchboxId, hasMatchingRommId, hasMatchingServerId);

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

                // Update Romm ID Tracking Custom Field on existing games if a new context list is provided
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
                game.ApplicationPath = Constants.romPlaceholder;
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

        /// <summary>
        /// Correlates a generic zip entry filename back to its parent queue model inside multi-game background batches.
        /// </summary>
        private RomQueueItem FindMatchingBatchItemForFile(List<RomQueueItem> batchItems, string zipFileName)
        {
            return batchItems.FirstOrDefault(item =>
                zipFileName.Contains(item.GameNameSanitised, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(zipFileName), item.GameNameSanitised, StringComparison.OrdinalIgnoreCase)
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