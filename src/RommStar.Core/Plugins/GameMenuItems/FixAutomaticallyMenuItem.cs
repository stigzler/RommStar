using iNKORE.UI.WPF.Helpers;
using RommStar.Core.Extensions;
using RommStar.Core.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class FixAutomaticallyMenuItem : IGameMenuItem
    {
        public string Caption => "Fix Automatically";

        public IEnumerable<IGameMenuItem> Children => null;

        public bool Enabled => true;

        public Image Icon => Resources.wand_hat;

        public void OnSelect(params IGame[] games)
        {
            IGame game = _selectedGame;

            bool allFilesPresent = true;

            if (game.ApplicationPath == Constants.RomPlaceholder)
            {
                allFilesPresent = false;
            }

            if (File.Exists(game.ApplicationPath))
            {
                {
                    foreach (var app in game.GetAllAdditionalApplications()
                        .Where(app => app.Section() == "Version" || app.Section() == "Unknown"))
                    {
                        if (!File.Exists(app.ApplicationPath))
                        {
                            allFilesPresent = false;
                            break;
                        }
                    }                    
                }
            }
            else
            {
                allFilesPresent = false;
            }

            if (allFilesPresent) SetToInstalled();
            else SetToUnInstalled();

            PluginHelper.DataManager.Save();
            _ = Helpers.LaunchboxViewsHelper.SoftRefreshUi();
        }

        private void SetToInstalled()
        {
            IGame game = _selectedGame;
            game.Installed = true;
            game.Status = "Installed";
            foreach (var app in game.GetAllAdditionalApplications().Where(app => app.Section() == "Version"))
            {
                app.Installed = true;
                app.Status = "Installed";
            }
        }

        private void SetToUnInstalled()
        {
            IGame game = _selectedGame;
            game.Installed = false;
            game.Status = "Not Installed";
            foreach (var app in game.GetAllAdditionalApplications().Where(app => app.Section() == "Version"))
            {
                app.Installed = false;
                app.Status = "Not Installed";
            }
        }

        private IGame? _selectedGame;

        public FixAutomaticallyMenuItem(IGame? selectedGame)
        {
            _selectedGame = selectedGame;
        }
    }
}
