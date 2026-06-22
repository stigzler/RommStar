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
        public SettingsService _settingsService { get; set; }
        public LaunchboxSettings _launchboxSettings { get; set; } = new LaunchboxSettings();

        private IPlatform _operationalPlatform;

        private string? _operativeServerId = null;

        private bool _overwriteMetadata = true;

        private bool _deleteOldServerRoms = true;

        internal string? EmulatorId = null;

        private RomMapper _romMapper;


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

        /// <summary>
        /// Used in conjunction with _platformLbGameDatabaseIds. Lookup once presence of launchboxDatabaseID Game
        /// </summary>
        private HashSet<MetadataSyncHelperMap> _platformHelperMap = new HashSet<MetadataSyncHelperMap>();

        public LaunchboxDataService(RomMapper romMapper, SettingsService settingsService)
        {
            _romMapper = romMapper;
            _settingsService = settingsService;
            PopulateLaunchboxSettings();
        }

        public async Task ProcessDownloadedRomBatchAsync(string tempZipPath, List<RomQueueItem> batchItems)
        {
            // Unzip Roms
            // Get the right settings. First get AdvancedSyncSettings for the platform. batchItems[0] because all are same platform
            var platformSettings = _settingsService.Settings.PlatformSyncSettings.FirstOrDefault(pss =>
                            pss.LaunchboxPlatformName == batchItems[0].PlatformName);

            bool individualGameFolders = (platformSettings.ExtendedSyncSettings.ApplySettings) ?
                platformSettings.ExtendedSyncSettings.UseIndividualGameFolders :
                 _settingsService.Settings.GlobalExtendedSyncSettings.UseIndividualGameFolders;

            IPlatform platform = PluginHelper.DataManager.GetPlatformByName(batchItems[0].PlatformName);
            if (platform == null)
            {
                Debug.WriteLine($"[Extraction] Error: Platform '{batchItems[0].PlatformName}' not found in LaunchBox.");
                return;
            }

            string romRoot = FileSystemHelper.ResolvedRompath(platform.Folder, platform.Name);

            // Build the exact internal folder prefix used by RomM's zip generation engine
            string expectedPrefix = $"roms/{batchItems[0].PlatformStub}/".Replace('\\', '/');

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

                    // 3. Resolve Destination Paths
                    string targetDirectory = romRoot;

                    if (individualGameFolders)
                    {
                        RomQueueItem matchingItem = null;

                        // OPTIMIZATION: If this is a VIP on-demand download, the batch only contains 1 item.
                        if (batchItems.Count == 1)
                        {
                            matchingItem = batchItems[0];
                        }
                        else
                        {
                            // Background Queue Path: Match this specific file entry to its owning game item
                            matchingItem = FindMatchingBatchItemForFile(batchItems, entry.Name);
                        }

                        if (matchingItem != null)
                        {
                            targetDirectory = Path.Combine(romRoot, matchingItem.GameNameSanitised);
                        }
                    }

                    // 4. Safe Atomic Extraction to Disk
                    string fullDestinationPath = Path.Combine(targetDirectory, relativeRomPath);
                    string destDirectoryPath = Path.GetDirectoryName(fullDestinationPath);

                    // Build missing subdirectory trees (handles multi-file/multi-disc structures safely)
                    if (!Directory.Exists(destDirectoryPath))
                        Directory.CreateDirectory(destDirectoryPath);

                    // Extract and overwrite any stale or corrupted files matching this payload
                    entry.ExtractToFile(fullDestinationPath, overwrite: true);
                }
            }

            // 5. Finalize State: Flip the LaunchBox database flags to Installed across the batch
            foreach (var batchItem in batchItems)
            {
                var game = PluginHelper.DataManager.GetGameById(batchItem.LaunchboxId);
                if (game != null)
                {
                    game.Installed = true;
                    game.Status = "Installed";
                    game.ApplicationPath = $"{romRoot}//{batchItem.} // what here???

                    // Optional TODO: Update game.ApplicationPath here to point directly 
                    // to the newly extracted primary file if required by your launcher.
                }
            }

            PluginHelper.DataManager.Save();

            if (PluginHelper.LaunchBoxMainViewModel != null)
                PluginHelper.LaunchBoxMainViewModel.RefreshData();
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
                    var rommIdField = existingFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ProtectMetadata.ToString());

                    if (rommIdField != null)
                    {
                        rommIdField.Value = rommDTO.LaunchboxId?.ToString();
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

        private void PopulateLaunchboxSettings()
        {
            XDocument doc = XDocument.Load(Path.Combine(Constants.LaunchboxRootDir, "Data\\Settings.xml"));
            var settings = doc.Root?.Element("Settings");
            if (settings?.Element("PlatformIconPack")?.Value is string iconPack && !string.IsNullOrWhiteSpace(iconPack))
            {
                _launchboxSettings.PlatformIconPack = iconPack;
            }
        }

        public void CreateNewPlatform(string platformName)
        {
            var newPlatform = PluginHelper.DataManager.AddNewPlatform(platformName);
            PluginHelper.DataManager.Save();
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

        public string GetPlatformIconPath(string platformName)
        {
            string votiIconPath = Path.Combine(Constants.LaunchboxRootDir, Constants.MediaPacksPlatformIconsRelPath,
                _launchboxSettings.PlatformIconPack, "Platforms", $"{platformName}.png");
            return votiIconPath;
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
    }
}