using RommStar.Core.Launchbox;
using RommStar.Core.Plugins.GameMenuItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Plugins
{
    internal class GameMultiMenuItemPlugin : IGameMultiMenuItemPlugin
    {
        public IEnumerable<IGameMenuItem> GetMenuItems(params IGame[] selectedGames)
        {
            IGameMenuItem syncPlatform = new SyncPlatformGameMenuItem(selectedGames.LastOrDefault());

            IGameMenuItem openAdmin = new OpenAdminWindowMenuItem();

            IGameMenuItem uninstallGame = new UninstallGameMenuItem(selectedGames.LastOrDefault());

            // hacky but no other way
            GameMenuItem separator = new GameMenuItem()
            {
                Icon = null,
                Caption = "------------",
                Enabled = false
            };

            GameMenuItem rommMenuItem = new GameMenuItem()
            {
                Icon = Properties.Resources.rommIcon64px,
                Caption = "RomM",
                Enabled = true,
                Children = new List<IGameMenuItem>() { syncPlatform,openAdmin, uninstallGame    } // NB uninstallGame must be last else crashes subsequent menu items
            };





            return new List<IGameMenuItem>() { rommMenuItem };
        }
    }
}