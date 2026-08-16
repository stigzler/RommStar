using RommStar.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class FixToNotInstalledMenuItem : IGameMenuItem
    {
        public string Caption => "Set Game to 'Not Installed'";

        public IEnumerable<IGameMenuItem> Children => null;

        public bool Enabled => true;

        public Image Icon => Properties.Resources.uninstall;

        public void OnSelect(params IGame[] games)
        {
            IGame game = _selectedGame;
            if (game == null) return;

            game.Installed = false;
            game.Status = "Not Installed";
            foreach (var app in game.GetAllAdditionalApplications().Where(app => app.Section() == "Version"))
            {
                app.Installed = false;
                app.Status = "Not Installed";
            }

            // todo: UPdate Button code - tried usual, but didn't work. Requires spelunking.
            PluginHelper.DataManager.Save();
            _ = Helpers.LaunchboxViewsHelper.SoftRefreshUi();
        }

        private IGame? _selectedGame;

        public FixToNotInstalledMenuItem(IGame? selectedGame)
        {
            _selectedGame = selectedGame;
        }
    }
}
