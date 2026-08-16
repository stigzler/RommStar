using RommStar.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins.GameMenuItems
{
    internal class FixToInstalledMenuItem : IGameMenuItem
    {
        public string Caption => "Set Game to 'Installed'";

        public IEnumerable<IGameMenuItem> Children => null;

        public bool Enabled => true;

        public Image Icon => Properties.Resources.installed;

        public void OnSelect(params IGame[] games)
        {
            IGame game = _selectedGame;
            if (game == null) return;

            game.Installed = true;
            game.Status = "Installed";
            foreach (var app in game.GetAllAdditionalApplications().Where(app => app.Section() == "Version"))
            {
                app.Installed = true;
                app.Status = "Installed";
            }

            // todo: UPdate Button code - tried usual, but didn't work. Requires spelunking.
            PluginHelper.DataManager.Save();
            _ = Helpers.LaunchboxViewsHelper.SoftRefreshUi();
        }

        private IGame? _selectedGame;

        public FixToInstalledMenuItem(IGame? selectedGame)
        {
            _selectedGame = selectedGame;
        }

    }
}
