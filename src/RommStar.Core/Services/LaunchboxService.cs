using RommStar.Core.Dtos;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Extensions;
using RommStar.Core.Launchbox;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Services
{
    public class LaunchboxService
    {
        public LaunchboxSettings LaunchboxSettings { get; set; } = new LaunchboxSettings();

        private IPlatform _operationalPlatform;

        private string _operativeServerId = null;

        private bool _overwriteMetadata = true;

        private bool _deleteOldServerRoms = true;

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with LaunchboxDatabaseIds.
        /// </summary>
        private HashSet<int?> _platformLbGameDatabaseIds = new HashSet<int?>();

        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with existing RommIds.
        /// </summary>
        private HashSet<int?> _platformRommIds = new HashSet<int?>();


        /// <summary>
        /// Used in conjunction with _platformHelperMap. 
        /// Performant lookup of games with existing ServerIds.
        /// </summary>
        private HashSet<string?> _platformServerIds = new HashSet<string?>();

        /// <summary>
        /// Used in conjunction with _platformLbGameDatabaseIds. Lookup once presence of launchboxDatabaseID Game
        /// </summary>
        private HashSet<MetadataSyncHelperMap> _platformHelperMap = new HashSet<MetadataSyncHelperMap>();

        public LaunchboxService()
        {
            PopulateLaunchboxSettings();
        }


        public bool SetupGameUpserts(string platformName, string serverId, ExtendedSyncSettings syncSettings)
        {
            _operationalPlatform = PluginHelper.DataManager.GetPlatformByName(platformName);
            _operativeServerId = serverId;
            _overwriteMetadata = syncSettings.OverwriteMetadata;
            _deleteOldServerRoms = syncSettings.DeleteOldServerRoms;

            _platformLbGameDatabaseIds.Clear();
            _platformRommIds.Clear();
            _platformHelperMap.Clear();

            if (_operationalPlatform == null) return false;

            IGame[] games = _operationalPlatform.GetAllGames(true, true);

            foreach (IGame game in games)
            {
                MetadataSyncHelperMap gameIdMap = new MetadataSyncHelperMap(game.Id, game.LaunchBoxDbId);
                gameIdMap.GameName = game.Title;

                var gameCustomFields = game.GetAllCustomFields();

                if (gameCustomFields != null)
                {
                    var romIdCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_RomId.ToString());
                    var serverIdCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ServerId.ToString());
                    var protectMetadataCustomField = gameCustomFields?.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_ProtectMetadata.ToString());

                    gameIdMap.RommId = (romIdCustomField != null) ? Convert.ToInt32(romIdCustomField.Value) : null;
                    gameIdMap.RommServerId = (serverIdCustomField != null) ? serverIdCustomField.Value : null;
                    gameIdMap.ProtectMetadata = (protectMetadataCustomField != null) ? Convert.ToBoolean(protectMetadataCustomField.Value) : null;
                }

                _platformHelperMap.Add(gameIdMap);
            }

            _platformLbGameDatabaseIds = _platformHelperMap.Select(phm => phm.LbDatabaseId).Where(id => id != null).ToHashSet();
            _platformRommIds = _platformHelperMap.Select(phm => phm.RommId).Where(id => id != null).ToHashSet();
            _platformServerIds = _platformHelperMap.Select(phm => phm.RommServerId).Where(id => id != null).ToHashSet();

            return _operationalPlatform != null;
        }


        public async Task<bool> UpsertGame(RomDTO rommDTO, bool overwriteMetadata)
        {
            bool? masMatchingLaunchboxId = _platformLbGameDatabaseIds.Contains(rommDTO.LaunchboxId);
            bool? HasMatchingRommId = _platformRommIds.Contains(rommDTO.Id);
            bool? HasMatchingServerId = _platformServerIds.Contains(_operativeServerId);

            MetadataSyncState metadataSyncState = new MetadataSyncState(masMatchingLaunchboxId, HasMatchingRommId, HasMatchingServerId);

            MetadataSyncAction syncAction = MetadataSyncDecisionEngine.DetermineAction(metadataSyncState, _overwriteMetadata, _deleteOldServerRoms);

        


            return false;
        }


        public List<LaunchboxPlatformDTO> GetPlatforms()
        {
            var livePlatforms = PluginHelper.DataManager.GetAllPlatforms();
            if (livePlatforms == null) return new List<LaunchboxPlatformDTO>();

            return livePlatforms.Select(p => new LaunchboxPlatformDTO
            {
                Name = p.Name,
                ScrapeAs = p.ScrapeAs,
                SortTitle = p.SortTitle,
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
                LaunchboxSettings.PlatformIconPack = iconPack;
            }
        }

        public void CreateNewPlatform(string platformName)
        {
            var newPlatform = PluginHelper.DataManager.AddNewPlatform(platformName);
            PluginHelper.DataManager.Save();
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
                LaunchboxSettings.PlatformIconPack, "Platforms", $"{platformName}.png");

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
                LaunchboxSettings.PlatformIconPack, "Platforms", $"{platformName}.png");
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