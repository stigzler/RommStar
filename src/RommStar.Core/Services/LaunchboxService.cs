using RommStar.Core.Dtos;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Extensions;
using RommStar.Core.Launchbox;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Used in conjunction with _platformGameIdMap. 
        /// Performant lookup of games with LaunchboxDatabaseIds.
        /// </summary>
        private HashSet<int?> _platformGameDatabaseIds = new HashSet<int?>();

        /// <summary>
        /// Used in conjunction with _platformGameIdMap. 
        /// Performant lookup of games with existing RommIds.
        /// </summary>
        private HashSet<int> _platformRommIds = new HashSet<int>();

        /// <summary>
        /// Used in conjunction with _platformGameDatabaseIds. Lookup once presence of launchboxDatabaseID Game
        /// </summary>
        private HashSet<GameIdMap> _platformGameIdMap = new HashSet<GameIdMap>();

        public LaunchboxService()
        {
            PopulateLaunchboxSettings();
        }
        public bool SetupGameUpserts(string platformName)
        {
            _operationalPlatform = PluginHelper.DataManager.GetPlatformByName(platformName);

            _platformGameDatabaseIds.Clear();
            _platformGameIdMap.Clear();

            if (_operationalPlatform == null) return false;

            IGame[] games = _operationalPlatform.GetAllGames(true, true);

            _platformGameDatabaseIds = new HashSet<int?>(games.Select(g => g.LaunchBoxDbId));

            foreach (IGame game in games)
            {
                GameIdMap gameIdMap = new GameIdMap(game.Id, game.LaunchBoxDbId);

                CustomField[] gameCustomFields = (CustomField[])game.GetAllCustomFields();

                if (gameCustomFields != null) 
                {
                    CustomField dave = gameCustomFields.FirstOrDefault(gcf => gcf.Name == CustomFieldTypes.Romm_RomId.GetCustomName());
                    gameIdMap.RommId = gameCustomFields(game.Id);
                }

                _platformGameIdMap.Add(gameIdMap);


            }

            return _operationalPlatform != null;
        }


        public async Task<bool> UpsertGame(RomDTO rommDTO, bool overwriteMetadata)
        {
            IGame game;

            if (_platformGameDatabaseIds.Contains((int)rommDTO.LaunchboxId))
            {
                game = PluginHelper.DataManager.GetGameById(_platformGameIdMap.Single(gim => gim.DatabaseId == rommDTO.LaunchboxId).LocalId);
            }
            else
            {

            }


            // IGame game = : 


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